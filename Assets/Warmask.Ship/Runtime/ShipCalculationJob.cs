using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Warmask.Ship
{
    [BurstCompile]
    public struct ShipCalculationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<float2> Velocities;
        [ReadOnly] public NativeArray<int> PlayerIds;

        [ReadOnly] public NativeArray<int> NeighborIndices;
        [ReadOnly] public NativeArray<int2> NeighborOffsets; // x=start, y=count

        [ReadOnly] public float SqrNeighborRadius;
        [ReadOnly] public float SqrSeparationDistance;
        [ReadOnly] public float SqrEnemyAvoidanceRadius;
        [ReadOnly] public float SeparationWeight;
        [ReadOnly] public float AlignmentWeight;
        [ReadOnly] public float CohesionWeight;
        [ReadOnly] public float EnemyAvoidanceWeight;

        [WriteOnly] public NativeArray<float2> ResultDirections;

        public void Execute(int index)
        {
            float2 myPos = Positions[index];
            int myPlayerId = PlayerIds[index];

            float2 separation = float2.zero;
            float2 alignment = float2.zero;
            float2 cohesion = float2.zero;
            float2 enemyAvoidance = float2.zero;
            int friendCount = 0;
            int enemyCount = 0;

            int2 offset = NeighborOffsets[index];
            int start = offset.x;
            int count = offset.y;

            for (int i = 0; i < count; i++)
            {
                int otherIdx = NeighborIndices[start + i];

                float2 toOther = Positions[otherIdx] - myPos;
                float sqrDist = math.lengthsq(toOther);

                if (PlayerIds[otherIdx] == myPlayerId)
                {
                    if (sqrDist <= SqrNeighborRadius && sqrDist > 0.0001f)
                    {
                        if (sqrDist <= SqrSeparationDistance)
                        {
                            float dist = math.sqrt(sqrDist);
                            separation -= toOther / (dist * dist);
                        }
                        alignment += Velocities[otherIdx];
                        cohesion += Positions[otherIdx];
                        friendCount++;
                    }
                }
                else
                {
                    if (sqrDist <= SqrEnemyAvoidanceRadius && sqrDist > 0.0001f)
                    {
                        float dist = math.sqrt(sqrDist);
                        enemyAvoidance -= toOther / (dist * dist);
                        enemyCount++;
                    }
                }
            }

            float2 result = float2.zero;

            if (friendCount > 0)
            {
                result += (separation / friendCount) * SeparationWeight;
                result += math.normalizesafe(alignment / friendCount) * AlignmentWeight;
                result += math.normalizesafe((cohesion / friendCount) - myPos) * CohesionWeight;
            }

            if (enemyCount > 0)
            {
                result += (enemyAvoidance / enemyCount) * EnemyAvoidanceWeight;
            }

            ResultDirections[index] = result;
        }
    }
}
