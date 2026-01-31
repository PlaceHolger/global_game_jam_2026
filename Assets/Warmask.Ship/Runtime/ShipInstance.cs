using System.Collections.Generic;
using UnityEngine;

namespace Warmask.Ship
{
    [RequireComponent(typeof(Collider2D))]
    public class ShipInstance : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float maxTurnAnglePerSecond = 90f;

        [Header("Boid Behavior")]
        [SerializeField] private float neighborRadius = 5f;
        [SerializeField] private float separationDistance = 2f;
        [SerializeField] private float alignmentWeight = 1f;
        [SerializeField] private float cohesionWeight = 1f;
        [SerializeField] private float separationWeight = 1.5f;
        [SerializeField] private float targetWeight = 2f;

        [Header("Combat")]
        [SerializeField] private int playerId;
        [SerializeField] private float detectionRadius = 10f;
        [SerializeField] private float weaponRange = 5f;
        [SerializeField] private float fireCooldown = 1f;
        [SerializeField] private float weaponDamage = 20f;
        [SerializeField] private float raycastStartOffset = 0.5f;
        [SerializeField] private LineRenderer laserVisual;

        [Header("Enemy Handling")]
        [SerializeField] private float enemyAvoidanceWeight = 2f;
        [SerializeField] private float enemyAvoidanceRadius = 3f;
        [SerializeField] private float maxDistanceFromTarget = 5f;
        [SerializeField] private float targetBehindDistance = 3f;

        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Physics")]
        [SerializeField] private LayerMask shipLayerMask = ~0;

        [Header("Performance")]
        [SerializeField] private float aiUpdateInterval = 0.1f;
        [SerializeField] private int maxColliderBufferSize = 50;

        [Header("Orbit Breaking")]
        [SerializeField] private float orbitBreakTime = 3f;
        [SerializeField] private float aggressiveTurnMultiplier = 2f;
        [SerializeField] private float orbitBreakJitterStrength = 0.5f;

        [Header("Effects")]
        [SerializeField] private GameObject explosionPrefab;

        // Cached components
        private Transform cachedTransform;
        private Collider2D ownCollider;

        // State
        private Vector2 velocity;
        private float currentHealth;
        private ShipInstance currentEnemyTarget;
        private float lastFireTime;
        private float nextAiUpdate;
        private bool pendingDeath;

        // Orbit breaking state
        private float lastSuccessfulHitTime;
        private Vector2 orbitBreakJitter;
        private float nextJitterUpdateTime;

        // Cached squared distances to avoid sqrt calculations
        private float sqrDetectionRadius;
        private float sqrNeighborRadius;
        private float sqrSeparationDistance;
        private float sqrEnemyAvoidanceRadius;
        private float sqrMaxDistanceFromTarget;

        // Collider caching to reduce allocations
        private Collider2D[] colliderBuffer;
        private int cachedColliderCount;
        private int frameOfLastCache = -1;
        private float cachedMaxRadius;

        // Neighbor caching to avoid list allocations
        private readonly List<ShipInstance> neighborCache = new List<ShipInstance>();

        // Cached position to avoid repeated transform access
        private Vector2 cachedPosition;

        private Vector2 boidResultFromJob;

        private bool showLaserThisFrame;
        private float laserTargetDistance;

        public bool IsAlive => currentHealth > 0 && !pendingDeath;

        [SerializeField]
        private Transform target;

        private Vector3 laserEndPosition;

        private int lifeId;
        private int currentEnemyTargetLifeId = -1;

        public int LifeId => lifeId;

        private void OnEnable()
        {
            lifeId = (lifeId == int.MaxValue) ? 0 : lifeId + 1;

            ShipManagerInstance.Instance?.Register(this);

            showLaserThisFrame = false;
            pendingDeath = false;
            currentHealth = maxHealth;
            lastFireTime = 0f;
            lastSuccessfulHitTime = Time.time;
            currentEnemyTarget = null;
            currentEnemyTargetLifeId = -1;
            orbitBreakJitter = Vector2.zero;
            laserEndPosition = Vector3.zero;
            maxTurnAnglePerSecond = 90.0f;

            if (laserVisual != null)
            {
                laserVisual.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            ShipManagerInstance.Instance?.Unregister(this);
            if (laserVisual != null)
            {
                laserVisual.gameObject.SetActive(false);
            }
            showLaserThisFrame = false;
            TrailRenderer trail = GetComponentInChildren<TrailRenderer>();
            if (trail) {
                trail.Clear();
            }
        }

        private void Awake()
        {
            cachedTransform = transform;
            ownCollider = GetComponent<Collider2D>();
            colliderBuffer = new Collider2D[maxColliderBufferSize];

            sqrDetectionRadius = detectionRadius * detectionRadius;
            sqrNeighborRadius = neighborRadius * neighborRadius;
            sqrSeparationDistance = separationDistance * separationDistance;
            sqrEnemyAvoidanceRadius = enemyAvoidanceRadius * enemyAvoidanceRadius;
            sqrMaxDistanceFromTarget = maxDistanceFromTarget * maxDistanceFromTarget;

            cachedMaxRadius = Mathf.Max(neighborRadius, detectionRadius, enemyAvoidanceRadius);
            showLaserThisFrame = false;
            laserEndPosition = Vector3.zero;
            if (laserVisual != null)
            {
                laserVisual.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * moveSpeed;
        }

        private void Update()
        {
            cachedPosition = cachedTransform.position;

            if (Time.time >= nextAiUpdate)
            {
                nextAiUpdate = Time.time + aiUpdateInterval;
                UpdateAI();
            }

            UpdateMovement();
            TryFireAtEnemyInFront();
        }

        private void UpdateAI()
        {
            if (!IsEnemyTargetValid())
            {
                currentEnemyTarget = DetectEnemy();
            }
        }

        private void UpdateMovement()
        {
            float timeSinceHit = Time.time - lastSuccessfulHitTime;
            bool isInOrbitBreakMode = timeSinceHit > orbitBreakTime && currentEnemyTarget != null;

            if (isInOrbitBreakMode && Time.time >= nextJitterUpdateTime)
            {
                nextJitterUpdateTime = Time.time + 0.3f + Random.Range(0f, 0.2f);
                orbitBreakJitter = Random.insideUnitCircle * orbitBreakJitterStrength;
            }
            else if (!isInOrbitBreakMode)
            {
                orbitBreakJitter = Vector2.zero;
            }

            Vector3? overrideTargetPosition = null;

            if (currentEnemyTarget != null && !IsTooFarFromTarget())
            {
                Vector2 enemyFlightDirection = currentEnemyTarget.velocity.normalized;
                overrideTargetPosition = currentEnemyTarget.cachedTransform.position - (Vector3)(enemyFlightDirection * targetBehindDistance);
            }

            Vector2 desiredDirection = CalculateCombinedBehavior(overrideTargetPosition);
            desiredDirection += orbitBreakJitter;

            if (desiredDirection.sqrMagnitude > 0.001f)
            {
                desiredDirection.Normalize();

                float angle = Vector2.SignedAngle(velocity, desiredDirection);
                float turnMultiplier = isInOrbitBreakMode ? aggressiveTurnMultiplier : 1f;
                float maxTurnAngle = maxTurnAnglePerSecond * Time.deltaTime * turnMultiplier;
                angle = Mathf.Clamp(angle, -maxTurnAngle, maxTurnAngle);

                Quaternion rotation = Quaternion.Euler(0, 0, angle);
                velocity = rotation * velocity;
            }

            velocity = velocity.normalized * moveSpeed;
            cachedTransform.position += (Vector3)(velocity * Time.deltaTime);

            if (velocity.sqrMagnitude > 0.001f)
            {
                cachedTransform.up = velocity;
            }
        }

        private Vector2 CalculateCombinedBehavior(Vector3? overrideTargetPosition)
        {
            Vector2 result = boidResultFromJob;
            result += CalculateTargetAttraction(overrideTargetPosition) * targetWeight;
            return result;
        }

        private void CacheCollidersIfNeeded()
        {
            if (frameOfLastCache == Time.frameCount) return;

            frameOfLastCache = Time.frameCount;
            cachedColliderCount = Physics2D.OverlapCircleNonAlloc(
                cachedPosition,
                cachedMaxRadius,
                colliderBuffer,
                shipLayerMask
            );
        }

        private bool IsEnemyTargetValid()
        {
            if (!currentEnemyTarget) return false;
            if (!currentEnemyTarget.gameObject.activeInHierarchy) return false;
            if (currentEnemyTarget.LifeId != currentEnemyTargetLifeId) return false;

            Vector2 toEnemy = (Vector2)currentEnemyTarget.cachedTransform.position - cachedPosition;
            return toEnemy.sqrMagnitude <= sqrDetectionRadius;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public bool TakeDamage(float damage)
        {
            currentHealth -= damage;

            if (currentHealth <= 0)
            {
                Die();
                return true; // Dieser Treffer hat das Schiff zerstört
            }

            return false;
        }

        private void Die()
        {
            if (pendingDeath) return;
            pendingDeath = true;
        }

        private bool IsTooFarFromTarget()
        {
            if (target == null) return false;

            Vector2 toTarget = (Vector2)target.position - cachedPosition;
            return toTarget.sqrMagnitude > sqrMaxDistanceFromTarget;
        }

        private void TryFireAtEnemyInFront()
        {
            Vector2 direction = cachedTransform.up;
            Vector2 startPos = cachedPosition + direction * raycastStartOffset;

            RaycastHit2D hit = Physics2D.Raycast(startPos, direction, weaponRange - raycastStartOffset, shipLayerMask);

            if (hit.collider != null && hit.collider != ownCollider)
            {
                if (hit.collider.TryGetComponent(out ShipInstance hitShip) &&
                    hitShip.playerId != playerId &&
                    hitShip.IsAlive)
                {
                    laserTargetDistance = hit.distance + raycastStartOffset;
                    FireWeapon(hitShip, hit.point);
                }
            }
        }

        private void FireWeapon(ShipInstance targetShip, Vector2 hitWOrldPosition)
        {
            if (targetShip.pendingDeath) return;
            
            if (Time.time - lastFireTime >= fireCooldown)
            {
                laserEndPosition = hitWOrldPosition;

                Debug.DrawRay(cachedTransform.position, cachedTransform.up * weaponRange, Color.purple, 0.1f);
                lastFireTime = Time.time;
                lastSuccessfulHitTime = Time.time;
                bool targetKilled = targetShip.TakeDamage(weaponDamage);

                if (targetKilled)
                {
                    maxTurnAnglePerSecond = Mathf.Min(180.0f, maxTurnAnglePerSecond + 15);
                }

                if (laserVisual)
                {
                    laserVisual.SetPosition(0, transform.position);
                    laserVisual.SetPosition(1, laserEndPosition);
                    laserVisual.gameObject.SetActive(true);
                }
                showLaserThisFrame = true;
            }
        }

        private ShipInstance DetectEnemy()
        {
            CacheCollidersIfNeeded();

            ShipInstance closestEnemy = null;
            float closestScore = float.MinValue;

            Vector2 forward = cachedTransform.up;

            for (int i = 0; i < cachedColliderCount; i++)
            {
                Collider2D col = colliderBuffer[i];
                if (col == null || col == ownCollider) continue;

                if (!col.TryGetComponent(out ShipInstance ship)) continue;
                if (ship.playerId == playerId) continue;

                Vector2 toEnemy = (Vector2)ship.cachedTransform.position - cachedPosition;
                float sqrDistance = toEnemy.sqrMagnitude;

                if (sqrDistance > sqrDetectionRadius) continue;

                float distance = Mathf.Sqrt(sqrDistance);
                Vector2 directionToEnemy = toEnemy / distance;
                float angleScore = Vector2.Dot(forward, directionToEnemy);

                float score = angleScore - (distance / detectionRadius);

                if (score > closestScore)
                {
                    closestEnemy = ship;
                    closestScore = score;
                }
            }

            currentEnemyTargetLifeId = closestEnemy?.LifeId ?? -1;

            return closestEnemy;
        }

        private Vector2 CalculateTargetAttraction(Vector3? overridePosition)
        {
            if (overridePosition.HasValue)
            {
                return ((Vector2)overridePosition.Value - cachedPosition).normalized;
            }

            if (target == null) return Vector2.zero;

            return ((Vector2)target.position - cachedPosition).normalized;
        }

        private List<ShipInstance> GetNeighbors()
        {
            neighborCache.Clear();
            CacheCollidersIfNeeded();

            for (int i = 0; i < cachedColliderCount; i++)
            {
                Collider2D col = colliderBuffer[i];
                if (col == null || col == ownCollider) continue;

                Vector2 toOther = (Vector2)col.transform.position - cachedPosition;
                if (toOther.sqrMagnitude > sqrNeighborRadius) continue;

                if (col.TryGetComponent(out ShipInstance ship) && ship.playerId == playerId)
                {
                    neighborCache.Add(ship);
                }
            }

            return neighborCache;
        }

        public void SetPlayerId(int id)
        {
            playerId = id;
        }

        public void FillJobData(out Vector2 position, out Vector2 vel, out int id)
        {
            position = cachedPosition;
            vel = velocity;
            id = playerId;
        }

        public void ApplyBoidResult(Vector2 boidDirection)
        {
            boidResultFromJob = boidDirection;
        }

        private void LateUpdate()
        {
            if (laserVisual && !showLaserThisFrame)
            {
                laserVisual.gameObject.SetActive(false);
            }
            showLaserThisFrame = false;

            if (pendingDeath)
            {
                if (explosionPrefab)
                {
                    GameObject explosion = Instantiate(explosionPrefab, cachedTransform.position, Quaternion.identity);
                    Destroy(explosion, 1.0f);
                }

                if (TryGetComponent(out PooledShip pooled))
                {
                    pooled.ReturnToPool();
                }
                else
                {
                    Destroy(gameObject);
                }}
        }
    }
}
