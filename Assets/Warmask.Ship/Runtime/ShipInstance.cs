using UnityEngine;
using Warmask.Core;

namespace Warmask.Ship
{
    public class ShipInstance : MonoBehaviour
    {
        public const float HitRadius = 0.3f;

        public static event System.Action<Globals.ePlayer> ShipDestroyed;

        [Header("Movement")] [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float maxTurnAnglePerSecond = 90f;
        [SerializeField] private float moveCommandSpeedBoost = 1.8f;
        [SerializeField] private float moveCommandBoostDuration = 1.2f;

        [Header("Boid Behavior")] [SerializeField]
        private float targetWeight = 2f;

        [Header("Combat")] [SerializeField] private float detectionRadius = 8f;
        [SerializeField] private float weaponRange = 5f;
        [SerializeField] private float fireCooldown = 1f;
        [SerializeField] private float weaponDamage = 20f;
        [SerializeField] private float raycastStartOffset = 0.5f;
        [SerializeField] private LineRenderer laserVisual;

        [Header("Enemy Handling")]
        [SerializeField] private float maxDistanceFromTarget = 5f;
        [SerializeField] private float minDistanceFromTarget = 2f;
        [SerializeField] private float targetBehindDistance = 3f;

        [Header("Health")] [SerializeField] private float maxHealth = 100f;

        [Header("Performance")] [SerializeField]
        private float aiUpdateInterval = 0.5f;

        [Header("Orbit Breaking")] [SerializeField]
        private float orbitBreakTime = 3f;

        [SerializeField] private float aggressiveTurnMultiplier = 2f;
        [SerializeField] private float orbitBreakJitterStrength = 0.5f;

        [Header("Effects")] [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Trail seconds/width while the ship has no move order and no enemy.")]
        [SerializeField] private float idleTrailTime = 1f;
        [SerializeField] private float idleTrailWidth = 0.5f;

        [Tooltip("Trail seconds/width while moving to a target or engaging an enemy.")]
        [SerializeField] private float activeTrailTime = 5f;
        [SerializeField] private float activeTrailWidth = 1f;

        private enum ShipState
        {
            Orbit,
            Engage
        }

        // Cached components
        private Transform cachedTransform;
        private ShipMaskFader maskFader;
        private TrailRenderer[] cachedTrails;

        // State
        private Vector2 velocity;
        private float currentHealth;
        private ShipInstance currentEnemyTarget;
        private float lastFireTime;
        private float nextAiUpdate;
        private float nextFireQueryTime;
        private bool pendingDeath;
        private int playerId;
        private Globals.eType shipType;
        private bool visualsApplied;
        private bool hasMoveCommand;
        private float moveCommandBoostRemaining;
        private ShipState shipState;
        private bool trailAssigned;
        private int currentEnemyTargetLifeId = -1;

        private const float MoveCommandArriveTolerance = 0.75f;

        // Fire queries scan the spatial hash; the laser renders for one frame, so
        // 10 Hz is visually identical to every frame.
        private const float FireQueryInterval = 0.1f;

        // Off-mask ships are invisible, so their movement runs at 30 Hz with the
        // accumulated dt. They still fly at full speed and keep fighting.
        private const float OffMaskMoveInterval = 1f / 30f;

        private float offMaskMoveAccumulator;

        public Globals.eType ShipType => shipType;

        // Orbit breaking state
        private float lastSuccessfulHitTime;
        private Vector2 orbitBreakJitter;
        private float nextJitterUpdateTime;

        // Cached squared distances to avoid sqrt calculations
        private float sqrDetectionRadius;
        private float sqrMaxDistanceFromTarget;
        private float sqrMinDistanceFromTarget;

        // Cached position to avoid repeated transform access
        private Vector2 cachedPosition;

        private Vector2 boidResultFromJob;

        private bool showLaserThisFrame;
        private bool laserActive;
        private Vector3 laserEndPosition;

        private int lifeId;

        public int LifeId => lifeId;

        private IShipOwner ownerShipyard;
        private EntityId ownerShipyardIdAtRegistration = EntityId.None;
        private bool isRegisteredWithShipyard;
        private bool isBeingDestroyed;

        [SerializeField] private Transform target;
        [SerializeField] private AudioSource laserAudioEffect;

        // Flash timer replaces the old per-hit coroutine (no allocation per shot).
        private float flashUntil;
        private Color flashOriginalColor;

        // Shared per-player gradient keeps laser colouring allocation-free.
        private static readonly Gradient[] PlayerLaserGradients = new Gradient[5];

        // One shared throttle stops hundreds of ships from stacking one-shots in a frame.
        private static float nextLaserAudioTime;

        public bool IsAlive => currentHealth > 0 && !pendingDeath && !isBeingDestroyed;
        public bool IsPendingDeath => pendingDeath;
        public Vector2 SimPosition => cachedPosition;
        public Vector2 SimVelocity => velocity;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        public float AiUpdateInterval
        {
            get => aiUpdateInterval;
            set
            {
                aiUpdateInterval = Mathf.Max(0f, value);
                nextAiUpdate = Time.time + aiUpdateInterval;
            }
        }

        public void SetOwnerShipyard(IShipOwner shipyard)
        {
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
            // Reset all state for pool reuse
            lifeId = (lifeId == int.MaxValue) ? 0 : lifeId + 1;

            ShipManagerInstance.Instance?.Register(this);

            ResetState();
            ApplyPlayerVisuals();

            cachedPosition = cachedTransform != null ? (Vector2)cachedTransform.position : Vector2.zero;
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
            offMaskMoveAccumulator = 0f;
            shipState = ShipState.Orbit;
            flashUntil = 0f;
            visualsApplied = false;
            // Stagger AI refreshes so 500 ships do not all query targeting in the
            // same frame. Spread across the full interval.
            nextAiUpdate = Time.time + (lifeId % 10) * (Mathf.Max(0.02f, aiUpdateInterval) * 0.1f);
            nextFireQueryTime = Time.time + (lifeId % 5) * 0.02f;
            if (maskFader)
                maskFader.SetMoving(false);

            SetLaserActive(false);
        }

        private void OnDisable()
        {
            ShipManagerInstance.Instance?.Unregister(this);

            SetLaserActive(false);

            showLaserThisFrame = false;

            if (cachedTrails != null)
            {
                for (int i = 0; i < cachedTrails.Length; i++)
                {
                    if (cachedTrails[i])
                        cachedTrails[i].Clear();
                }
            }
        }

        private void Awake()
        {
            cachedTransform = transform;
            EnsureVisualCaches();

            sqrDetectionRadius = detectionRadius * detectionRadius;
            sqrMaxDistanceFromTarget = maxDistanceFromTarget * maxDistanceFromTarget;
            sqrMinDistanceFromTarget = minDistanceFromTarget * minDistanceFromTarget;

            cachedPosition = cachedTransform.position;
            showLaserThisFrame = false;
            laserEndPosition = Vector3.zero;
            if (laserVisual != null)
            {
                laserVisual.enabled = false;
                laserActive = false;
            }
        }

        private void Start()
        {
            velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * moveSpeed;
        }

        /// <summary>
        /// Pooled ships can have SetPlayerId called before Awake has run (or after a
        /// domain reload), so resolve every visual reference lazily as well.
        /// </summary>
        private void EnsureVisualCaches()
        {
            if (cachedTransform == null)
                cachedTransform = transform;
            if (maskFader == null)
                maskFader = GetComponent<ShipMaskFader>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (cachedTrails == null || cachedTrails.Length == 0)
                cachedTrails = GetComponentsInChildren<TrailRenderer>(true);
        }

        /// <summary>
        /// Called once per frame by ShipManagerInstance. This replaces the old
        /// per-ship Update/LateUpdate pair so 500 ships cost one manager loop
        /// instead of 500 MonoBehaviour dispatches.
        /// </summary>
        public void Tick(float dt, ShipManagerInstance manager, int index)
        {
            if (isBeingDestroyed)
                return;

            if (!visualsApplied)
                ApplyPlayerVisuals();

            cachedPosition = cachedTransform.position;

            bool onActiveMask = shipType == Globals.Instance.currentMask;
            bool coarseTick = !onActiveMask && ShipProfiling.EnableOffMaskCoarseTick;

            if (ShipProfiling.EnableTargeting && Time.time >= nextAiUpdate)
            {
                float interval = Mathf.Max(0.02f, aiUpdateInterval);
                if (coarseTick)
                    interval *= 2f; // invisible ships re-target half as often
                nextAiUpdate = Time.time + interval;
                UpdateAI(manager, index);
            }

            if (IsAlive && ShipProfiling.EnableMovement)
            {
                if (coarseTick)
                {
                    offMaskMoveAccumulator += dt;
                    if (offMaskMoveAccumulator >= OffMaskMoveInterval)
                    {
                        UpdateMovement(offMaskMoveAccumulator);
                        offMaskMoveAccumulator = 0f;
                    }
                }
                else
                {
                    offMaskMoveAccumulator = 0f;
                    UpdateMovement(dt);
                }
            }

            if (ShipProfiling.EnableTargeting && ShipProfiling.EnableFiring &&
                IsAlive && shipState == ShipState.Engage && Time.time >= nextFireQueryTime)
            {
                nextFireQueryTime = Time.time + FireQueryInterval;
                TryFireAtEnemyInFront(manager, index);
            }

            bool assigned = hasMoveCommand || (IsAlive && shipState == ShipState.Engage);
            if (assigned != trailAssigned)
            {
                trailAssigned = assigned;
                if (maskFader)
                    maskFader.SetMoving(assigned);
                ApplyTrailTimes();
            }

            if (maskFader)
                maskFader.Tick();

            UpdateFlash();

            if (!showLaserThisFrame)
                SetLaserActive(false);

            showLaserThisFrame = false;
        }

        /// <summary>
        /// Deferred death handling. Kept out of Tick so the manager never mutates
        /// its ship list while iterating it.
        /// </summary>
        public void LateTick()
        {
            if (pendingDeath && !isBeingDestroyed)
                HandleDeath();
        }

        private void UpdateAI(ShipManagerInstance manager, int index)
        {
            if (!IsEnemyTargetValid())
            {
                currentEnemyTarget = manager.FindClosestEnemy(
                    index, cachedPosition, cachedTransform.up, detectionRadius);
                currentEnemyTargetLifeId = currentEnemyTarget != null ? currentEnemyTarget.LifeId : -1;
            }

            shipState = currentEnemyTarget != null ? ShipState.Engage : ShipState.Orbit;
        }

        private void UpdateMovement(float dt)
        {
            float timeSinceHit = Time.time - lastSuccessfulHitTime;
            // Past the home leash the ship must return, so neither the chase override
            // nor the random orbit-break jitter may outweigh the home attraction.
            bool tooFarFromHome = IsTooFarFromTarget();
            bool isInOrbitBreakMode = !tooFarFromHome && timeSinceHit > orbitBreakTime && currentEnemyTarget;

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

            if (currentEnemyTarget && !tooFarFromHome)
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
                float maxTurnAngle = maxTurnAnglePerSecond * dt * turnMultiplier;
                angle = Mathf.Clamp(angle, -maxTurnAngle, maxTurnAngle);

                Quaternion rotation = Quaternion.Euler(0, 0, angle);
                velocity = rotation * velocity;
            }

            float speed = moveSpeed;
            if (hasMoveCommand && moveCommandBoostRemaining > 0f)
            {
                moveCommandBoostRemaining = Mathf.Max(0f, moveCommandBoostRemaining - dt);
                float boostT = moveCommandBoostDuration > 0f
                    ? moveCommandBoostRemaining / moveCommandBoostDuration
                    : 0f;
                speed *= Mathf.Lerp(1f, Mathf.Max(1f, moveCommandSpeedBoost),
                    Mathf.SmoothStep(0f, 1f, boostT));
            }

            velocity = velocity.normalized * speed;
            cachedTransform.position += (Vector3)(velocity * dt);
            cachedPosition = cachedTransform.position;
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
            Vector2 toTarget = (Vector2)target.position - cachedPosition;
            if (toTarget.sqrMagnitude <= arriveDistance * arriveDistance)
                ClearMoveCommand();
        }

        private void ClearMoveCommand()
        {
            hasMoveCommand = false;
            moveCommandBoostRemaining = 0f;
        }

        public void SetType(Globals.eType type)
        {
            EnsureVisualCaches();
            shipType = type;
            if (maskFader)
                maskFader.SetType(type);

            // SetType toggles renderer visibility (and may clear the trail), so
            // reapply the player colours afterwards.
            ApplyPlayerVisuals();
        }

        public bool TakeDamage(float damage)
        {
            if (!IsAlive || !ShipProfiling.EnableDamage) return false;

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
            if (!ShipProfiling.EnableDamage) return;
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

        private void TryFireAtEnemyInFront(ShipManagerInstance manager, int index)
        {
            if (Time.time - lastFireTime < fireCooldown)
                return;

            Vector2 direction = cachedTransform.up;
            Vector2 startPos = cachedPosition + direction * raycastStartOffset;

            if (!manager.TryFindEnemyInFront(
                    index, startPos, direction, weaponRange - raycastStartOffset,
                    out ShipInstance hitShip, out Vector2 hitPoint))
                return;

            if (hitShip.IsAlive &&
                MatchConfig.AreEnemies((Globals.ePlayer)playerId, (Globals.ePlayer)hitShip.GetPlayerId()))
                FireWeapon(hitShip, hitPoint);
        }

        private void FireWeapon(ShipInstance targetShip, Vector2 hitWorldPosition)
        {
            if (targetShip.pendingDeath) return;

            if (Time.time - lastFireTime >= fireCooldown)
            {
                laserEndPosition = hitWorldPosition;

                lastFireTime = Time.time;

                // Scan again exactly when the weapon is ready, so the 10 Hz
                // query throttle never delays the next shot.
                nextFireQueryTime = Time.time + fireCooldown;
                lastSuccessfulHitTime = Time.time;
                bool targetKilled = targetShip.TakeDamage(weaponDamage);

                if (targetKilled)
                    maxTurnAnglePerSecond = Mathf.Min(180.0f, maxTurnAnglePerSecond + 15);

                // Off-mask ships keep fighting, but their shots must not leak into
                // the view: laser, audio and hit flash are all visual feedback.
                bool onActiveMask = shipType == Globals.Instance.currentMask;

                if (onActiveMask && laserVisual && ShipProfiling.EnableLasers)
                {
                    laserVisual.SetPosition(0, cachedTransform.position);
                    laserVisual.SetPosition(1, laserEndPosition);
                    SetLaserActive(true);

                        if (laserAudioEffect && ShipProfiling.EnableAudio && Time.time >= nextLaserAudioTime)
                        {
                            nextLaserAudioTime = Time.time + 0.12f;
                            laserAudioEffect.pitch = 0.85f + (int)shipType * 0.07f + Random.Range(-0.06f, 0.06f);
                            if (laserAudioEffect.clip)
                                laserAudioEffect.PlayOneShot(laserAudioEffect.clip, 0.6f);
                        }
                }

                if (onActiveMask && spriteRenderer && ShipProfiling.EnableRendering && ShipProfiling.EnableHitFlash)
                {
                    flashOriginalColor = spriteRenderer.color;
                    spriteRenderer.color = Color.white;
                    flashUntil = Time.time + 0.05f;
                }

                showLaserThisFrame = true;
            }
        }

        private void SetLaserActive(bool active)
        {
            if (!laserVisual || laserActive == active)
                return;

            laserActive = active;
            laserVisual.enabled = active;
        }

        private void UpdateFlash()
        {
            if (flashUntil <= 0f || Time.time < flashUntil)
                return;

            flashUntil = 0f;
            if (spriteRenderer)
                spriteRenderer.color = flashOriginalColor;
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

            // Too close -> repel
            if (sqrDistance < sqrMinDistanceFromTarget && sqrDistance > 0.001f)
                return -toTarget.normalized;

            // Past the leash the pull grows with distance, so boid cohesion/separation
            // can never strand a ship far from home.
            float distance = Mathf.Sqrt(sqrDistance);
            float leash = distance > maxDistanceFromTarget ? 1f + distance - maxDistanceFromTarget : 1f;

            return toTarget.normalized * leash;
        }

        public void SetPlayerId(int id)
        {
            playerId = id;
            ApplyPlayerVisuals();
        }

        /// <summary>
        /// Applies sprite, trail and laser colours. Called from both SetPlayerId and
        /// OnEnable so a pooled ship whose SetPlayerId runs before Awake still ends
        /// up with the correct player colours.
        /// </summary>
        private void ApplyPlayerVisuals()
        {
            if (Globals.Instance == null)
                return;

            EnsureVisualCaches();

            Color playerColor = Globals.Instance.GetPlayerColor((Globals.ePlayer)playerId);

            if (spriteRenderer)
            {
                spriteRenderer.sprite = Globals.Instance.GetPlayerSprite((Globals.ePlayer)playerId);
                spriteRenderer.color = playerColor;
                // Some atlas hulls are drawn side-on; the sprite child carries the
                // per-ship correction so every player's nose points up.
                spriteRenderer.transform.localRotation =
                    Quaternion.Euler(0f, 0f, Globals.Instance.GetPlayerRotation((Globals.ePlayer)playerId));
            }

            if (maskFader)
                maskFader.SetPlayerColor(spriteRenderer, playerColor);

            if (laserVisual)
                laserVisual.colorGradient = GetPlayerLaserGradient(playerId);

            visualsApplied = true;

            trailAssigned = hasMoveCommand || (IsAlive && shipState == ShipState.Engage);
            if (maskFader)
                maskFader.SetMoving(trailAssigned);
            ApplyTrailTimes();
        }

        private void ApplyTrailTimes()
        {
            if (cachedTrails == null || Globals.Instance == null)
                return;

            float time = trailAssigned ? activeTrailTime : idleTrailTime;
            float width = trailAssigned ? activeTrailWidth : idleTrailWidth;
            Color color = Globals.Instance.GetPlayerColor((Globals.ePlayer)playerId);

            for (int i = 0; i < cachedTrails.Length; i++)
            {
                TrailRenderer trail = cachedTrails[i];
                if (!trail)
                    continue;

                // start/endColor are one gradient, so set both and never assign colorGradient separately.
                // Alpha stays owned by ShipMaskFader, which already knows the mask fade and task state.
                float alpha = trail.startColor.a;
                trail.time = time;
                trail.widthMultiplier = width;
                trail.startColor = new Color(color.r, color.g, color.b, alpha);
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }

        private static Gradient GetPlayerLaserGradient(int id)
        {
            int index = Mathf.Clamp(id, 0, PlayerLaserGradients.Length - 1);
            if (PlayerLaserGradients[index] != null)
                return PlayerLaserGradients[index];

            Color color = Globals.Instance != null
                ? Globals.Instance.GetPlayerColor((Globals.ePlayer)id)
                : Color.white;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color * 2f, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.3f, 1f) });

            PlayerLaserGradients[index] = gradient;
            return gradient;
        }

        public int GetPlayerId()
        {
            return playerId;
        }

        public bool IsInTransit => hasMoveCommand;

        public void ApplyBoidResult(Vector2 boidDirection)
        {
            boidResultFromJob = boidDirection;
        }

        private void HandleDeath()
        {
            isBeingDestroyed = true;
            if (playerId > 0)
                ShipDestroyed?.Invoke((Globals.ePlayer)playerId);

            if (explosionPrefab)
            {
                if (ExplosionPool.Instance != null)
                {
                    ExplosionPool.Instance.Get(cachedTransform.position);
                }
                else
                {
                    GameObject explosion = Instantiate(explosionPrefab, cachedTransform.position, Quaternion.identity);
                    Destroy(explosion, 1.0f);
                }
            }

            UnregisterFromShipyardSafely();

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
                return;

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

            ClearOwnerShipyard();
            SetOwnerShipyard(newOwner);
        }

        public void SetMinTargetDistance(float distance)
        {
            minDistanceFromTarget = distance + 0.5f;
            sqrMinDistanceFromTarget = (distance + 0.5f) * (distance + 0.5f);
        }
    }
}
