using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Warmask.Ship;
using Warmask.Shipyard;

namespace Warmask.Tests
{
    /// <summary>
    /// Load-level performance tests. The harness spawns through the real shipyards
    /// with capacity checks bypassed, so the production caps in SampleScene
    /// (9 yards x 20 ships) do not limit the measured ship count.
    ///
    /// Damage is disabled by default so the measured ship count stays stable and
    /// comparable between runs.
    /// </summary>
    public class ShipPerformanceTests
    {
        private const string StressScene = "SampleScene";

        private ShipStressSpawner harness;

        [UnityTest]
        public IEnumerator Measure300Ships()
        {
            yield return RunScenario(300);
            AssertScenario(300, minimumFps: 60f);
        }

        [UnityTest]
        public IEnumerator Measure500Ships()
        {
            yield return RunScenario(500);
            AssertScenario(500, minimumFps: 45f);
        }

        [UnityTest]
        public IEnumerator Ablate500_NoTrails()
        {
            yield return RunScenario(500, trails: false);
            AssertScenario(500, minimumFps: 45f, label: "no trails");
        }

        [UnityTest]
        public IEnumerator Ablate500_NoTargeting()
        {
            yield return RunScenario(500, targeting: false);
            AssertScenario(500, minimumFps: 45f, label: "no targeting");
        }

        [UnityTest]
        public IEnumerator Ablate500_NoFiring()
        {
            yield return RunScenario(500, firing: false);
            AssertScenario(500, minimumFps: 45f, label: "no firing");
        }

        [UnityTest]
        public IEnumerator Ablate500_NoAudio()
        {
            yield return RunScenario(500, audio: false);
            AssertScenario(500, minimumFps: 45f, label: "no audio");
        }

        [UnityTest]
        public IEnumerator Ablate500_NoLaserVisuals()
        {
            yield return RunScenario(500, lasers: false);
            AssertScenario(500, minimumFps: 45f, label: "no laser visuals");
        }

        [UnityTest]
        public IEnumerator Ablate500_NoHitFlash()
        {
            yield return RunScenario(500, hitFlash: false);
            AssertScenario(500, minimumFps: 45f, label: "no hit flash");
        }

        [UnityTest]
        public IEnumerator Ablate500_NoRendering()
        {
            yield return RunScenario(500, rendering: false);
            AssertScenario(500, minimumFps: 45f, label: "no rendering");
        }

        [UnityTest]
        public IEnumerator Ablate500_NoBoid()
        {
            yield return RunScenario(500, boid: false);
            AssertScenario(500, minimumFps: 45f, label: "no boid");
        }

        [UnityTest]
        public IEnumerator Diagnose500Phases()
        {
            yield return RunScenario(500, verbosePhases: true);
            AssertScenario(500, minimumFps: 30f, label: "phase diagnostics");
        }

        [UnityTest]
        public IEnumerator Diagnose500Phases_OffMaskFullTick()
        {
            yield return RunScenario(500, verbosePhases: true, offMaskCoarseTick: false);
            AssertScenario(500, minimumFps: 30f, label: "phase diagnostics, off-mask full tick");
        }

        private IEnumerator RunScenario(
            int shipCount,
            bool damage = false,
            bool trails = true,
            bool targeting = true,
            bool firing = true,
            bool audio = true,
            bool lasers = true,
            bool hitFlash = true,
            bool rendering = true,
            bool boid = true,
            bool verbosePhases = false,
            bool offMaskCoarseTick = true)
        {
            ShipProfiling.EnableOffMaskCoarseTick = offMaskCoarseTick;

            yield return GameplaySceneLoader.Load(StressScene);

            GameObject host = new GameObject("ShipStressSpawner");
            host.SetActive(false);
            harness = host.AddComponent<ShipStressSpawner>();
            harness.RunOnStart = false;
            harness.TargetShipCount = shipCount;
            harness.Damage = damage;
            harness.Trails = trails;
            harness.Targeting = targeting;
            harness.Firing = firing;
            harness.Audio = audio;
            harness.Lasers = lasers;
            harness.HitFlash = hitFlash;
            harness.Rendering = rendering;
            harness.BoidSteering = boid;
            harness.VerbosePhases = verbosePhases;
            host.SetActive(true);

            yield return harness.Run();

            Debug.Log($"[PerfTest] requested={shipCount} {harness.LastReport}");
        }

        private void AssertScenario(int requestedShips, float minimumFps, string label = "baseline")
        {
            Assert.IsTrue(harness != null && harness.IsFinished, "Stress harness did not finish.");
            Assert.GreaterOrEqual(
                harness.MeasuredShipCount,
                Mathf.CeilToInt(requestedShips * 0.95f),
                $"[{label}] Only {harness.MeasuredShipCount} of {requestedShips} ships existed during measurement.");
            Assert.GreaterOrEqual(
                harness.AverageFps,
                minimumFps,
                $"[{label}] Average FPS {harness.AverageFps:F1} was below {minimumFps}.");
        }
    }
}
