using System;
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

        [Header("Boid Settings (Global)")]
        [SerializeField] private float neighborRadius = 5f;
        [SerializeField] private float separationDistance = 2f;
        [SerializeField] private float enemyAvoidanceRadius = 3f;
        [SerializeField] private float separationWeight = 1.5f;
        [SerializeField] private float alignmentWeight = 1f;
        [SerializeField] private float cohesionWeight = 1f;
        [SerializeField] private float enemyAvoidanceWeight = 2f;

        [Header("Performance")]
        [SerializeField] private LayerMask shipLayerMask = ~0;
        [SerializeField] private int maxNeighborsPerShip = 20;
        [SerializeField] private int overlapBufferSize = 100;

        private readonly List<ShipInstance> allShips = new List<ShipInstance>();
        private readonly Dictionary<ShipInstance, int> shipToIndex = new Dictionary<ShipInstance, int>();
        private readonly Dictionary<Collider2D, ShipInstance> colliderToShip = new Dictionary<Collider2D, ShipInstance>();
        
        private Collider2D[] overlapBuffer;

        // Persistente Arrays
        private NativeArray<float2> positions;
        private NativeArray<float2> velocities;
        private NativeArray<int> playerIds;
        private NativeArray<float2> results;
        private NativeList<int> neighborIndices;
        private NativeArray<int2> neighborOffsets;
        private int allocatedCapacity;

        // Gecachte Werte
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
            overlapBuffer = new Collider2D[overlapBufferSize];
            
            CacheSquaredDistances();
        }

        private void CacheSquaredDistances()
        {
            sqrNeighborRadius = neighborRadius * neighborRadius;
            sqrSeparationDistance = separationDistance * separationDistance;
            sqrEnemyAvoidanceRadius = enemyAvoidanceRadius * enemyAvoidanceRadius;
            maxRadius = Mathf.Max(neighborRadius, enemyAvoidanceRadius);
        }

        private void OnDestroy()
        {
            DisposeArrays();
        }

        public void Register(ShipInstance ship)
        {
            if (shipToIndex.ContainsKey(ship)) return;
            
            shipToIndex[ship] = allShips.Count;
            allShips.Add(ship);
            
            if (ship.TryGetComponent(out Collider2D col))
            {
                colliderToShip[col] = ship;
            }
        }

        public void Unregister(ShipInstance ship)
        {
            if (!shipToIndex.TryGetValue(ship, out int index)) return;
            
            int lastIndex = allShips.Count - 1;
            if (index != lastIndex)
            {
                allShips[index] = allShips[lastIndex];
                shipToIndex[allShips[index]] = index;
            }
            allShips.RemoveAt(lastIndex);
            shipToIndex.Remove(ship);
            
            if (ship.TryGetComponent(out Collider2D col))
            {
                colliderToShip.Remove(col);
            }
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

        private void LateUpdate()
        {
            int count = allShips.Count;
            if (count == 0) return;

            EnsureCapacity(count);

            // Fill ship data
            for (int i = 0; i < count; i++)
            {
                allShips[i].FillJobData(out Vector2 pos, out Vector2 vel, out int id);
                positions[i] = new float2(pos.x, pos.y);
                velocities[i] = new float2(vel.x, vel.y);
                playerIds[i] = id;
            }

            // Find neighbors using cached collider mapping
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = new Vector2(positions[i].x, positions[i].y);
                int hitCount = Physics2D.OverlapCircleNonAlloc(pos, maxRadius, overlapBuffer, shipLayerMask);

                int startIndex = neighborIndices.Length;
                int neighborCount = 0;
                ShipInstance currentShip = allShips[i];

                for (int j = 0; j < hitCount && neighborCount < maxNeighborsPerShip; j++)
                {
                    Collider2D col = overlapBuffer[j];
                    if (!col) continue;
                    
                    // Schneller Dictionary-Lookup statt TryGetComponent
                    if (!colliderToShip.TryGetValue(col, out ShipInstance neighbor)) continue;
                    if (neighbor == currentShip) continue;

                    if (shipToIndex.TryGetValue(neighbor, out int neighborIdx))
                    {
                        neighborIndices.Add(neighborIdx);
                        neighborCount++;
                    }
                }

                neighborOffsets[i] = new int2(startIndex, neighborCount);
            }

            // Schedule job
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

            // Apply results
            for (int i = 0; i < count; i++)
            {
                allShips[i].ApplyBoidResult(new Vector2(results[i].x, results[i].y));
            }
        }
    }
}
