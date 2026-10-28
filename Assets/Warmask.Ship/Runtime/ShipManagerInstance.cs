using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Warmask.Ship
{
    public class ShipManagerInstance : MonoBehaviour
    {
        public static ShipManagerInstance Instance { get; private set; }
        public int ShipCount => allShips.Count;

        [Header("Boid Settings (Global)")]
        [SerializeField] private float neighborRadius = 5f;
        [SerializeField] private float separationDistance = 2f;
        [SerializeField] private float enemyAvoidanceRadius = 3f;
        [SerializeField] private float separationWeight = 1.5f;
        [SerializeField] private float alignmentWeight = 1f;
        [SerializeField] private float cohesionWeight = 1f;
        [SerializeField] private float enemyAvoidanceWeight = 2f;

        [Header("Performance")]
        [SerializeField] private int maxNeighborsPerShip = 10;

        private readonly List<ShipInstance> allShips = new List<ShipInstance>();
        private readonly Dictionary<ShipInstance, int> shipToIndex = new Dictionary<ShipInstance, int>();
        private readonly List<ShipInstance> pendingDeaths = new List<ShipInstance>();

        private NativeParallelMultiHashMap<int, int> spatialHash;
        private int spatialHashCapacity;
        private float cellSize;
        private float invCellSize;

        // Persistent arrays
        private NativeArray<float2> positions;
        private NativeArray<float2> velocities;
        private NativeArray<int> playerIds;
        private NativeArray<float2> results;
        private NativeList<int> neighborIndices;
        private NativeArray<int2> neighborOffsets;
        private int allocatedCapacity;

        // Lightweight phase timing for verification builds.
        private long phaseSnapshotTicks;
        private long phaseHashTicks;
        private long phaseNeighborTicks;
        private long phaseJobTicks;
        private long phaseTickTicks;
        private long phaseDeathTicks;
        private double phaseLogTimer;
        private int phaseFrames;

        // Cached values
        private float sqrNeighborRadius;
        private float sqrSeparationDistance;
        private float sqrEnemyAvoidanceRadius;
        private float maxRadius;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CacheSquaredDistances();
        }

        private void CacheSquaredDistances()
        {
            sqrNeighborRadius = neighborRadius * neighborRadius;
            sqrSeparationDistance = separationDistance * separationDistance;
            sqrEnemyAvoidanceRadius = enemyAvoidanceRadius * enemyAvoidanceRadius;
            maxRadius = Mathf.Max(neighborRadius, enemyAvoidanceRadius);
            cellSize = Mathf.Max(0.001f, maxRadius);
            invCellSize = 1f / cellSize;
        }

        private void OnDestroy()
        {
            DisposeArrays();

            if (spatialHash.IsCreated)
            {
                spatialHash.Dispose();
                spatialHashCapacity = 0;
            }
        }

        public int GetPlayerShipCount(int playerId)
        {
            int count = 0;
            for (int i = 0; i < allShips.Count; i++)
            {
                if (allShips[i] && allShips[i].IsAlive && allShips[i].GetPlayerId() == playerId)
                    count++;
            }

            return count;
        }

        public void Register(ShipInstance ship)
        {
            if (ship == null || shipToIndex.ContainsKey(ship)) return;

            shipToIndex[ship] = allShips.Count;
            allShips.Add(ship);
        }

        public void Unregister(ShipInstance ship)
        {
            if (ship == null || !shipToIndex.TryGetValue(ship, out int index)) return;

            int lastIndex = allShips.Count - 1;
            if (index != lastIndex)
            {
                allShips[index] = allShips[lastIndex];
                shipToIndex[allShips[index]] = index;
            }

            allShips.RemoveAt(lastIndex);
            shipToIndex.Remove(ship);
        }

        private void EnsureCapacity(int count)
        {
            if (allocatedCapacity >= count && neighborIndices.IsCreated)
            {
                neighborIndices.Clear();
                return;
            }

            DisposeArrays();

            int newCapacity = Mathf.Max(count, allocatedCapacity * 2, 64);
            positions = new NativeArray<float2>(newCapacity, Allocator.Persistent);
            velocities = new NativeArray<float2>(newCapacity, Allocator.Persistent);
            playerIds = new NativeArray<int>(newCapacity, Allocator.Persistent);
            results = new NativeArray<float2>(newCapacity, Allocator.Persistent);
            neighborOffsets = new NativeArray<int2>(newCapacity, Allocator.Persistent);
            neighborIndices = new NativeList<int>(newCapacity * maxNeighborsPerShip, Allocator.Persistent);
            allocatedCapacity = newCapacity;
        }

        private void DisposeArrays()
        {
            if (positions.IsCreated) positions.Dispose();
            if (velocities.IsCreated) velocities.Dispose();
            if (playerIds.IsCreated) playerIds.Dispose();
            if (results.IsCreated) results.Dispose();
            if (neighborOffsets.IsCreated) neighborOffsets.Dispose();
            if (neighborIndices.IsCreated) neighborIndices.Dispose();
            allocatedCapacity = 0;
        }

        private static int HashCell(int x, int y)
        {
            return (x * 73856093) ^ (y * 19349663);
        }

        /// <summary>
        /// Single manager tick: snapshot ship data, build the spatial hash and boid
        /// neighbours, run the steering job, then tick every ship once. Deaths are
        /// handled after the loop so the ship list is never mutated mid-iteration.
        /// </summary>
        private void LateUpdate()
        {
            int count = allShips.Count;
            if (count == 0) return;

            long t0 = ShipProfiling.LogPhaseTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

            // Phase markers live in ShipProfiling.Phases. To remove profiling, delete
            // that nested class and these six using scopes.
            using (ShipProfiling.Phases.Snapshot.Auto())
            {
                EnsureCapacity(count);

                for (int i = 0; i < count; i++)
                {
                    ShipInstance ship = allShips[i];
                    positions[i] = ship.SimPosition;
                    velocities[i] = ship.SimVelocity;
                    playerIds[i] = ship.GetPlayerId();
                }
            }

            long t1 = ShipProfiling.LogPhaseTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

            using (ShipProfiling.Phases.SpatialHash.Auto())
                BuildSpatialHash(count);

            long t2 = ShipProfiling.LogPhaseTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            long tBoidEnd = t2;

            if (ShipProfiling.EnableBoidSteering)
            {
                using (ShipProfiling.Phases.Neighbors.Auto())
                    BuildNeighborLists(count);

                long t3 = ShipProfiling.LogPhaseTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

                using (ShipProfiling.Phases.BoidJob.Auto())
                {
                    var job = new ShipCalculationJob
                    {
                        Positions = positions,
                        Velocities = velocities,
                        PlayerIds = playerIds,
                        NeighborIndices = neighborIndices.AsArray(),
                        NeighborOffsets = neighborOffsets,
                        SqrNeighborRadius = sqrNeighborRadius,
                        SqrSeparationDistance = sqrSeparationDistance,
                        SqrEnemyAvoidanceRadius = sqrEnemyAvoidanceRadius,
                        SeparationWeight = separationWeight,
                        AlignmentWeight = alignmentWeight,
                        CohesionWeight = cohesionWeight,
                        EnemyAvoidanceWeight = enemyAvoidanceWeight,
                        ResultDirections = results
                    };

                    job.Schedule(count, 64).Complete();
                }

                long t4 = ShipProfiling.LogPhaseTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                tBoidEnd = t4;

                if (ShipProfiling.LogPhaseTimings)
                {
                    phaseNeighborTicks += t3 - t2;
                    phaseJobTicks += t4 - t3;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                    results[i] = float2.zero;
            }

            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            using (ShipProfiling.Phases.Tick.Auto())
            {
                for (int i = 0; i < count; i++)
                {
                    ShipInstance ship = allShips[i];
                    if (ship == null) continue;

                    ship.ApplyBoidResult(new Vector2(results[i].x, results[i].y));
                    ship.Tick(dt, this, i);
                }
            }

            long t5 = ShipProfiling.LogPhaseTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

            using (ShipProfiling.Phases.Deaths.Auto())
            {
                pendingDeaths.Clear();
                for (int i = 0; i < count; i++)
                {
                    ShipInstance ship = allShips[i];
                    if (ship != null && ship.IsPendingDeath)
                        pendingDeaths.Add(ship);
                }

                for (int i = 0; i < pendingDeaths.Count; i++)
                {
                    pendingDeaths[i].LateTick();
                }
            }

            long t6 = ShipProfiling.LogPhaseTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

            if (ShipProfiling.LogPhaseTimings)
            {
                phaseSnapshotTicks += t1 - t0;
                phaseHashTicks += t2 - t1;
                phaseTickTicks += t5 - tBoidEnd;
                phaseDeathTicks += t6 - t5;
                LogPhaseTimings(count);
            }
        }

        private void LogPhaseTimings(int shipCount)
        {
            phaseFrames++;
            phaseLogTimer += Time.unscaledDeltaTime;

            if (phaseLogTimer < 2.0)
                return;

            double frames = Mathf.Max(1, phaseFrames);
            double frequency = System.Diagnostics.Stopwatch.Frequency;
            double snapshot = phaseSnapshotTicks * 1000.0 / frequency / frames;
            double hash = phaseHashTicks * 1000.0 / frequency / frames;
            double neighbors = phaseNeighborTicks * 1000.0 / frequency / frames;
            double job = phaseJobTicks * 1000.0 / frequency / frames;
            double tick = phaseTickTicks * 1000.0 / frequency / frames;
            double deaths = phaseDeathTicks * 1000.0 / frequency / frames;

            Debug.Log(
                $"[ShipPhases] ships={shipCount} snapshot={snapshot:F3}ms hash={hash:F3}ms " +
                $"neighbors={neighbors:F3}ms job={job:F3}ms tick={tick:F3}ms deaths={deaths:F3}ms " +
                $"total={snapshot + hash + neighbors + job + tick + deaths:F3}ms");

            phaseSnapshotTicks = 0;
            phaseHashTicks = 0;
            phaseNeighborTicks = 0;
            phaseJobTicks = 0;
            phaseTickTicks = 0;
            phaseDeathTicks = 0;
            phaseLogTimer = 0;
            phaseFrames = 0;
        }

        private void BuildSpatialHash(int count)
        {
            int required = Mathf.NextPowerOfTwo(Mathf.Max(64, count * 2));
            if (!spatialHash.IsCreated || spatialHashCapacity < required)
            {
                if (spatialHash.IsCreated)
                    spatialHash.Dispose();

                spatialHash = new NativeParallelMultiHashMap<int, int>(required, Allocator.Persistent);
                spatialHashCapacity = required;
            }

            spatialHash.Clear();

            for (int i = 0; i < count; i++)
            {
                int cx = (int)math.floor(positions[i].x * invCellSize);
                int cy = (int)math.floor(positions[i].y * invCellSize);
                spatialHash.Add(HashCell(cx, cy), i);
            }
        }

        private void BuildNeighborLists(int count)
        {
            // The 3x3 block around a cell always covers the full cell size from any
            // point inside the centre cell, which matches maxRadius.
            float sqrQueryRadius = sqrNeighborRadius * 4f;

            for (int i = 0; i < count; i++)
            {
                float2 position = positions[i];
                int cx = (int)math.floor(position.x * invCellSize);
                int cy = (int)math.floor(position.y * invCellSize);

                int startIndex = neighborIndices.Length;
                int neighborCount = 0;

                for (int dx = -1; dx <= 1 && neighborCount < maxNeighborsPerShip; dx++)
                {
                    for (int dy = -1; dy <= 1 && neighborCount < maxNeighborsPerShip; dy++)
                    {
                        int key = HashCell(cx + dx, cy + dy);
                        if (!spatialHash.TryGetFirstValue(key, out int neighborIdx, out var iterator))
                            continue;

                        do
                        {
                            if (neighborIdx == i) continue;

                            float2 delta = positions[neighborIdx] - position;
                            if (math.lengthsq(delta) > sqrQueryRadius) continue;

                            neighborIndices.Add(neighborIdx);
                            neighborCount++;

                            if (neighborCount >= maxNeighborsPerShip)
                                break;
                        }
                        while (spatialHash.TryGetNextValue(out neighborIdx, ref iterator));
                    }
                }

                neighborOffsets[i] = new int2(startIndex, neighborCount);
            }
        }

        /// <summary>
        /// Returns the best enemy inside the detection radius using the same
        /// angle/distance score as the old Physics2D version.
        /// </summary>
        public ShipInstance FindClosestEnemy(int selfIndex, Vector2 position, Vector2 forward, float detectionRadius)
        {
            if (!spatialHash.IsCreated || detectionRadius <= 0f)
                return null;

            int myPlayerId = allShips[selfIndex].GetPlayerId();
            int cellRange = Mathf.Max(1, Mathf.CeilToInt(detectionRadius * invCellSize));
            int cx = Mathf.FloorToInt(position.x * invCellSize);
            int cy = Mathf.FloorToInt(position.y * invCellSize);
            float sqrDetectionRadius = detectionRadius * detectionRadius;

            ShipInstance best = null;
            float bestScore = float.MinValue;

            for (int dx = -cellRange; dx <= cellRange; dx++)
            {
                for (int dy = -cellRange; dy <= cellRange; dy++)
                {
                    int key = HashCell(cx + dx, cy + dy);
                    if (!spatialHash.TryGetFirstValue(key, out int neighborIdx, out var iterator))
                        continue;

                    do
                    {
                        if (neighborIdx == selfIndex) continue;

                        ShipInstance other = allShips[neighborIdx];
                        if (other == null || !other.IsAlive ||
                            !MatchConfig.AreEnemies((Globals.ePlayer)myPlayerId, (Globals.ePlayer)other.GetPlayerId()))
                            continue;

                        Vector2 toEnemy = other.SimPosition - position;
                        float sqrDistance = toEnemy.sqrMagnitude;
                        if (sqrDistance > sqrDetectionRadius || sqrDistance < 0.0001f)
                            continue;

                        float distance = Mathf.Sqrt(sqrDistance);
                        float angleScore = Vector2.Dot(forward, toEnemy / distance);
                        float score = angleScore - (distance / detectionRadius);

                        if (score > bestScore)
                        {
                            best = other;
                            bestScore = score;
                        }
                    }
                    while (spatialHash.TryGetNextValue(out neighborIdx, ref iterator));
                }
            }

            return best;
        }

        /// <summary>
        /// Replacement for Physics2D.Raycast against ship colliders. Returns the
        /// closest alive ship whose centre is within ShipInstance.HitRadius of the
        /// ray. Friendlies are returned too, matching the old "first collider blocks
        /// the shot" behaviour.
        /// </summary>
        public bool TryFindEnemyInFront(int selfIndex, Vector2 origin, Vector2 direction, float maxDistance,
            out ShipInstance hit, out Vector2 hitPoint)
        {
            hit = null;
            hitPoint = default;

            if (!spatialHash.IsCreated || maxDistance <= 0f)
                return false;

            float pad = ShipInstance.HitRadius;
            Vector2 end = origin + direction * maxDistance;

            int minX = Mathf.FloorToInt((Mathf.Min(origin.x, end.x) - pad) * invCellSize);
            int maxX = Mathf.FloorToInt((Mathf.Max(origin.x, end.x) + pad) * invCellSize);
            int minY = Mathf.FloorToInt((Mathf.Min(origin.y, end.y) - pad) * invCellSize);
            int maxY = Mathf.FloorToInt((Mathf.Max(origin.y, end.y) + pad) * invCellSize);

            float sqrHitRadius = ShipInstance.HitRadius * ShipInstance.HitRadius;
            float bestDistanceAlongRay = float.MaxValue;

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cy = minY; cy <= maxY; cy++)
                {
                    int key = HashCell(cx, cy);
                    if (!spatialHash.TryGetFirstValue(key, out int neighborIdx, out var iterator))
                        continue;

                    do
                    {
                        if (neighborIdx == selfIndex) continue;

                        ShipInstance other = allShips[neighborIdx];
                        if (other == null || !other.IsAlive) continue;

                        Vector2 toShip = other.SimPosition - origin;
                        float distanceAlongRay = Vector2.Dot(toShip, direction);
                        if (distanceAlongRay < 0f || distanceAlongRay > maxDistance ||
                            distanceAlongRay >= bestDistanceAlongRay)
                            continue;

                        Vector2 closestPoint = origin + direction * distanceAlongRay;
                        if ((other.SimPosition - closestPoint).sqrMagnitude > sqrHitRadius)
                            continue;

                        bestDistanceAlongRay = distanceAlongRay;
                        hit = other;
                    }
                    while (spatialHash.TryGetNextValue(out neighborIdx, ref iterator));
                }
            }

            if (hit == null)
                return false;

            hitPoint = origin + direction * bestDistanceAlongRay;
            return true;
        }
    }
}
