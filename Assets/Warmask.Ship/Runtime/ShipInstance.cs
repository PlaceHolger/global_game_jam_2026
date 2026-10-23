using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Warmask.Core;

namespace Warmask.Ship
{
    [RequireComponent(typeof(Collider2D))]
    public class ShipInstance : MonoBehaviour
    {
        [Header("Movement")] [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float maxTurnAnglePerSecond = 90f;
        [SerializeField] private float moveCommandSpeedBoost = 1.8f;
        [SerializeField] private float moveCommandBoostDuration = 1.2f;

        [Header("Boid Behavior")] [SerializeField]
        private float neighborRadius = 5f;

        [SerializeField] private float separationDistance = 2f;
        [SerializeField] private float targetWeight = 2f;

        [Header("Combat")] [SerializeField] private float detectionRadius = 10f;
        [SerializeField] private float weaponRange = 5f;
        [SerializeField] private float fireCooldown = 1f;
        [SerializeField] private float weaponDamage = 20f;
        [SerializeField] private float raycastStartOffset = 0.5f;
        [SerializeField] private LineRenderer laserVisual;

        [Header("Enemy Handling")] [SerializeField]
        private float enemyAvoidanceRadius = 3f;
        [SerializeField] private float maxDistanceFromTarget = 5f;
        [SerializeField] private float minDistanceFromTarget = 2f;
        [SerializeField] private float targetBehindDistance = 3f;

        [Header("Health")] [SerializeField] private float maxHealth = 100f;

        [Header("Physics")] [SerializeField] private LayerMask shipLayerMask = ~0;

        [Header("Performance")] [SerializeField]
        private float aiUpdateInterval = 0.1f;

        [SerializeField] private int maxColliderBufferSize = 50;
        [Header("Orbit Breaking")] [SerializeField]
        private float orbitBreakTime = 3f;

        [SerializeField] private float aggressiveTurnMultiplier = 2f;
        [SerializeField] private float orbitBreakJitterStrength = 0.5f;

        [Header("Effects")] [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private SpriteRenderer spriteRenderer;
        
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
        private int playerId;
        private Globals.eType shipType;
        private bool hasMoveCommand;
        private float moveCommandBoostRemaining;
        private ShipMaskFader maskFader;

        private const float MoveCommandArriveTolerance = 0.75f;

        public Globals.eType ShipType => shipType;

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
        private float sqrMinDistanceFromTarget;

        // Collider caching to reduce allocations
        private Collider2D[] colliderBuffer;
        private ContactFilter2D colliderFilter;
        private int cachedColliderCount;
        private int frameOfLastCache = -1;
        private float cachedMaxRadius;

        // Cached position to avoid repeated transform access
        private Vector2 cachedPosition;

        private Vector2 boidResultFromJob;

        private bool showLaserThisFrame;

        [SerializeField] private Transform target;

        private Vector3 laserEndPosition;

        private int lifeId;
        private int currentEnemyTargetLifeId = -1;

        public int LifeId => lifeId;
        
        private IShipOwner ownerShipyard;
        private EntityId ownerShipyardIdAtRegistration = EntityId.None;
        private bool isRegisteredWithShipyard;
        private bool isBeingDestroyed;
        
        [SerializeField] private AudioSource laserAudioEffect;

        public bool IsAlive => currentHealth > 0 && !pendingDeath && !isBeingDestroyed;

        public void SetOwnerShipyard(IShipOwner shipyard)
        {
            // Alte Registrierung aufräumen
            if (isRegisteredWithShipyard && ownerShipyard != null)
            {
                Debug.LogWarning($"[ShipInstance] Überschreibe Shipyard-Referenz ohne vorherige Abmeldung!");
            }

            ownerShipyard = shipyard;
            ownerShipyardIdAtRegistration = shipyard?.OwnerId ?? EntityId.None;
            isRegisteredWithShipyard = shipyard != null;
        }
        
        public void ClearOwnerShipyard()
        {
            ownerShipyard = null;
            ownerShipyardIdAtRegistration = EntityId.None;
            isRegisteredWithShipyard = false;
        }

        private void OnEnable()
        {
            // Reset aller Zustände für Pool-Wiederverwendung
            lifeId = (lifeId == int.MaxValue) ? 0 : lifeId + 1;

            ShipManagerInstance.Instance?.Register(this);

            // Zustand komplett zurücksetzen
            ResetState();
        }

        private void ResetState()
        {
            showLaserThisFrame = false;
            pendingDeath = false;
            isBeingDestroyed = false;
            currentHealth = maxHealth;
            lastFireTime = 0f;
            lastSuccessfulHitTime = Time.time;
            currentEnemyTarget = null;
            currentEnemyTargetLifeId = -1;
            orbitBreakJitter = Vector2.zero;
            laserEndPosition = Vector3.zero;
            maxTurnAnglePerSecond = 90.0f;
            hasMoveCommand = false;
            moveCommandBoostRemaining = 0f;
            if (maskFader)
                maskFader.SetMoving(false);

            // Shipyard-Referenz wird NICHT hier zurückgesetzt
            // Das macht SpawnShip/RegisterShip

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

            var trailCompsInChildren = GetComponentsInChildren<TrailRenderer>();
            foreach (var trail in trailCompsInChildren)
            {
                trail.Clear();
            }

            // Wichtig: Shipyard-Referenz hier NICHT löschen
            // Das wurde bereits in HandleDeath gemacht
        }

        private void Awake()
        {
            cachedTransform = transform;
            ownCollider = GetComponent<Collider2D>();
            maskFader = GetComponent<ShipMaskFader>();
            colliderBuffer = new Collider2D[maxColliderBufferSize];
            colliderFilter = new ContactFilter2D();
            colliderFilter.useTriggers = Physics2D.queriesHitTriggers;
            colliderFilter.SetLayerMask(shipLayerMask);

            sqrDetectionRadius = detectionRadius * detectionRadius;
            sqrNeighborRadius = neighborRadius * neighborRadius;
            sqrSeparationDistance = separationDistance * separationDistance;
            sqrEnemyAvoidanceRadius = enemyAvoidanceRadius * enemyAvoidanceRadius;
            sqrMaxDistanceFromTarget = maxDistanceFromTarget * maxDistanceFromTarget;
            sqrMinDistanceFromTarget = minDistanceFromTarget * minDistanceFromTarget;

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
            bool isInOrbitBreakMode = timeSinceHit > orbitBreakTime && currentEnemyTarget;

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

            if (currentEnemyTarget && !IsTooFarFromTarget())
            {
                Vector2 enemyFlightDirection = currentEnemyTarget.velocity.normalized;
                overrideTargetPosition = currentEnemyTarget.cachedTransform.position -
                                         (Vector3)(enemyFlightDirection * targetBehindDistance);
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

            float speed = moveSpeed;
            if (hasMoveCommand && moveCommandBoostRemaining > 0f)
            {
                moveCommandBoostRemaining = Mathf.Max(0f, moveCommandBoostRemaining - Time.deltaTime);
                float boostT = moveCommandBoostDuration > 0f
                    ? moveCommandBoostRemaining / moveCommandBoostDuration
                    : 0f;
                speed *= Mathf.Lerp(1f, Mathf.Max(1f, moveCommandSpeedBoost),
                    Mathf.SmoothStep(0f, 1f, boostT));
            }

            velocity = velocity.normalized * speed;
            cachedTransform.position += (Vector3)(velocity * Time.deltaTime);
            UpdateMoveCommandArrival();

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
            cachedColliderCount = Physics2D.OverlapCircle(
                cachedPosition,
                cachedMaxRadius,
                colliderFilter,
                colliderBuffer
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

        public void SetMoveCommand(Transform newTarget, float targetRadius)
        {
            if (!newTarget) return;

            SetTarget(newTarget);
            SetMinTargetDistance(targetRadius);
            hasMoveCommand = true;
            moveCommandBoostRemaining = Mathf.Max(0f, moveCommandBoostDuration);

            Vector2 toTarget = (Vector2)newTarget.position - (Vector2)cachedTransform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
                velocity = toTarget.normalized * moveSpeed;

            if (maskFader)
                maskFader.SetMoving(true);
        }

        private void UpdateMoveCommandArrival()
        {
            if (!hasMoveCommand) return;

            if (!target)
            {
                ClearMoveCommand();
                return;
            }

            float arriveDistance = minDistanceFromTarget + MoveCommandArriveTolerance;
            Vector2 toTarget = (Vector2)target.position - (Vector2)cachedTransform.position;
            if (toTarget.sqrMagnitude <= arriveDistance * arriveDistance)
                ClearMoveCommand();
        }

        private void ClearMoveCommand()
        {
            hasMoveCommand = false;
            moveCommandBoostRemaining = 0f;
            if (maskFader)
                maskFader.SetMoving(false);
        }

        public void SetType(Globals.eType type)
        {
            shipType = type;
            if (maskFader)
                maskFader.SetType(type);
        }


        public bool TakeDamage(float damage)
        {
            // Frühzeitiger Abbruch wenn bereits tot
            if (!IsAlive) return false;

            currentHealth -= damage;

            if (currentHealth <= 0)
            {
                RequestDeath();
                return true;
            }

            return false;
        }
        
        private void RequestDeath()
        {
            if (pendingDeath || isBeingDestroyed) return;
            pendingDeath = true;
        }

        public void Die() => RequestDeath();

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

            if (hit.collider && hit.collider != ownCollider)
            {
                if (hit.collider.TryGetComponent(out ShipInstance hitShip) &&
                    hitShip.playerId != playerId &&
                    hitShip.IsAlive)
                {
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
                    
                    // Set laser color to player color
                    if (Globals.Instance)
                    {
                        Color playerColor = Globals.Instance.GetPlayerColor((Globals.ePlayer)playerId);
                        var gradient = new Gradient();
                        gradient.SetKeys(
                            new GradientColorKey[] { new GradientColorKey(playerColor * 2f, 0f), new GradientColorKey(playerColor, 1f) },
                            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.3f, 1f) }
                        );
                        laserVisual.colorGradient = gradient;
                    }
                    
                    if (laserAudioEffect)
                    {
                        laserAudioEffect.PlayOneShot(laserAudioEffect.clip);
                    }
                }

                // Flash effect
                if (spriteRenderer)
                {
                    StartCoroutine(FlashSprite());
                }

                showLaserThisFrame = true;
            }
        }

        private IEnumerator FlashSprite()
        {
            if (!spriteRenderer) yield break;
            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = originalColor;
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
                if (!col || col == ownCollider) continue;

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
            Vector2 targetPos;
    
            if (overridePosition.HasValue)
            {
                targetPos = overridePosition.Value;
            }
            else if (target)
            {
                targetPos = target.position;
            }
            else
            {
                return Vector2.zero;
            }

            Vector2 toTarget = targetPos - cachedPosition;
            float sqrDistance = toTarget.sqrMagnitude;

            // Zu nah -> abstoßen
            if (sqrDistance < sqrMinDistanceFromTarget && sqrDistance > 0.001f)
            {
                return -toTarget.normalized;
            }

            // Normalfall -> anziehen
            return toTarget.normalized;
        }


        public void SetPlayerId(int id)
        {
            playerId = id;
            Color playerColor = Globals.Instance.GetPlayerColor((Globals.ePlayer)playerId);
            spriteRenderer.sprite = Globals.Instance.GetPlayerSprite((Globals.ePlayer)playerId);
            spriteRenderer.color = playerColor;
            if (maskFader)
                maskFader.SetPlayerColor(spriteRenderer, playerColor);
            
            // Color the trail
            var trails = GetComponentsInChildren<TrailRenderer>();
            foreach (var trail in trails)
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(playerColor, 0f), new GradientColorKey(playerColor, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) }
                );
                trail.colorGradient = gradient;
            }
        }
        
        public int GetPlayerId()
        {
            return playerId;
        }

        public bool IsInTransit => hasMoveCommand;

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

            if (pendingDeath && !isBeingDestroyed)
            {
                HandleDeath();
            }
        }
        
        private void HandleDeath()
        {
            // Verhindere mehrfache Ausführung
            isBeingDestroyed = true;

            // Explosion spawnen
            if (explosionPrefab)
            {
                GameObject explosion = Instantiate(explosionPrefab, cachedTransform.position, Quaternion.identity);
                Destroy(explosion, 1.0f);
            }

            // Sicher aus Shipyard abmelden
            UnregisterFromShipyardSafely();

            // In Pool zurückgeben oder zerstören
            if (TryGetComponent(out PooledShip pooled))
            {
                pooled.ReturnToPool();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void UnregisterFromShipyardSafely()
        {
            if (!isRegisteredWithShipyard || ownerShipyard == null)
            {
                return;
            }

            // Validierung: Gehört das Schiff noch zum gleichen Shipyard?
            if (ownerShipyard.OwnerId != ownerShipyardIdAtRegistration)
            {
                Debug.LogWarning($"[ShipInstance] Shipyard-ID mismatch bei Abmeldung. " +
                                 $"Erwartet: {ownerShipyardIdAtRegistration}, Aktuell: {ownerShipyard.OwnerId}");
            }

            ownerShipyard.UnregisterShip(this);
            ClearOwnerShipyard();
        }
        
        public void TransferToNewOwner(IShipOwner newOwner)
        {
            if (isBeingDestroyed || pendingDeath)
            {
                Debug.LogWarning("[ShipInstance] Kann totes Schiff nicht transferieren");
                return;
            }

            // Alte Registrierung aufräumen (ohne UnregisterShip - das macht TransferShipsTo)
            ClearOwnerShipyard();

            // Neue Registrierung setzen
            SetOwnerShipyard(newOwner);
        }

        public void SetMinTargetDistance(float distance)
        {
            minDistanceFromTarget = distance + 0.5f;
            sqrMinDistanceFromTarget = (distance + 0.5f) * (distance + 0.5f);
        }
    }
}
