using System.Collections;
using UnityEngine;
using Warmask.Ship;

namespace Warmask.Shipyard
{
    /// <summary>
    /// Deterministic ship-count stress harness. Spawns ships through the shipyards
    /// while bypassing production caps, then measures FPS over a fixed window.
    /// Attach to an empty GameObject in a scene or drive it from a PlayMode test.
    /// </summary>
    public class ShipStressSpawner : MonoBehaviour
    {
        [Header("Load")]
        [SerializeField, Min(0)] private int targetShipCount = 500;
        [SerializeField, Min(1)] private int spawnPerFrame = 50;

        [Header("Measurement")]
        [SerializeField, Min(0f)] private float warmupSeconds = 2f;
        [SerializeField, Min(0.1f)] private float measureSeconds = 5f;
        [SerializeField] private bool runOnStart = true;

        [Header("Feature Toggles (A/B profiling)")]
        [SerializeField] private bool boidSteering = true;
        [SerializeField] private bool trails = true;
        [SerializeField] private bool lasers = true;
        [SerializeField] private bool audio = true;
        [SerializeField] private bool rendering = true;
        [SerializeField] private bool hitFlash = true;
        [SerializeField] private bool targeting = true;
        [SerializeField] private bool firing = true;
        [SerializeField] private bool damage = true;
        [SerializeField] private bool verbosePhases;

        public bool IsFinished { get; private set; }
        public bool IsRunning { get; private set; }
        public float AverageFps { get; private set; }
        public float MinFps { get; private set; }
        public float MaxFps { get; private set; }
        public int MeasuredShipCount { get; private set; }
        public int SpawnedShipCount { get; private set; }
        public string LastReport { get; private set; }

        public int TargetShipCount
        {
            get => targetShipCount;
            set => targetShipCount = Mathf.Max(0, value);
        }

        public bool RunOnStart
        {
            get => runOnStart;
            set => runOnStart = value;
        }

        public bool Damage
        {
            get => damage;
            set => damage = value;
        }

        public bool Trails
        {
            get => trails;
            set => trails = value;
        }

        public bool Targeting
        {
            get => targeting;
            set => targeting = value;
        }

        public bool Firing
        {
            get => firing;
            set => firing = value;
        }

        public bool Audio
        {
            get => audio;
            set => audio = value;
        }

        public bool Lasers
        {
            get => lasers;
            set => lasers = value;
        }

        public bool HitFlash
        {
            get => hitFlash;
            set => hitFlash = value;
        }

        public bool Rendering
        {
            get => rendering;
            set => rendering = value;
        }

        public bool BoidSteering
        {
            get => boidSteering;
            set => boidSteering = value;
        }

        public bool VerbosePhases
        {
            get => verbosePhases;
            set => verbosePhases = value;
        }

        private void Start()
        {
            if (runOnStart)
                StartCoroutine(Run());
        }

        public IEnumerator Run()
        {
            if (IsRunning)
                yield break;

            IsRunning = true;
            IsFinished = false;
            AverageFps = 0f;
            MinFps = float.MaxValue;
            MaxFps = 0f;
            MeasuredShipCount = 0;
            SpawnedShipCount = 0;
            LastReport = null;

            ApplyFeatureToggles();

            ShipyardInstance[] shipyards = FindObjectsByType<ShipyardInstance>();
            if (shipyards.Length == 0)
            {
                LastReport = "[Stress] No ShipyardInstance found.";
                Debug.LogError(LastReport);
                IsRunning = false;
                IsFinished = true;
                yield break;
            }

            int spawned = 0;
            int yardIndex = 0;
            while (spawned < targetShipCount)
            {
                int batch = Mathf.Min(spawnPerFrame, targetShipCount - spawned);
                for (int i = 0; i < batch; i++)
                {
                    shipyards[yardIndex % shipyards.Length].SpawnShipUnchecked();
                    yardIndex++;
                }

                spawned += batch;
                yield return null;
            }

            SpawnedShipCount = spawned;

            float elapsed = 0f;
            while (elapsed < warmupSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            float total = 0f;
            int frames = 0;
            MinFps = float.MaxValue;
            MaxFps = 0f;

            elapsed = 0f;
            while (elapsed < measureSeconds)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                total += dt;
                frames++;

                float fps = 1f / Mathf.Max(dt, 0.0001f);
                if (fps < MinFps) MinFps = fps;
                if (fps > MaxFps) MaxFps = fps;

                yield return null;
            }

            AverageFps = frames / Mathf.Max(total, 0.0001f);
            MeasuredShipCount = ShipManagerInstance.Instance != null
                ? ShipManagerInstance.Instance.ShipCount
                : 0;

            LastReport =
                $"[Stress] ships={MeasuredShipCount} avg={AverageFps:F1} min={MinFps:F1} max={MaxFps:F1} " +
                $"boid={boidSteering} trails={trails} lasers={lasers} audio={audio} render={rendering} flash={hitFlash} target={targeting} firing={firing} damage={damage}";

            Debug.Log(LastReport);
            IsRunning = false;
            IsFinished = true;
        }

        private void ApplyFeatureToggles()
        {
            ShipProfiling.EnableBoidSteering = boidSteering;
            ShipProfiling.EnableTrails = trails;
            ShipProfiling.EnableLasers = lasers;
            ShipProfiling.EnableAudio = audio;
            ShipProfiling.EnableRendering = rendering;
            ShipProfiling.EnableHitFlash = hitFlash;
            ShipProfiling.EnableTargeting = targeting;
            ShipProfiling.EnableFiring = firing;
            ShipProfiling.EnableDamage = damage;
            ShipProfiling.LogPhaseTimings = verbosePhases;
        }
    }
}
