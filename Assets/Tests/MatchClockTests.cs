using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warmask.Planet;

namespace Warmask.Tests
{
    public class MatchClockTests
    {
        [TearDown]
        public void RestoreNormalTime()
        {
            Time.timeScale = 1f;
        }

        [Test]
        public void Pause_FreezesTimeAndResumeRestoresIt()
        {
            var clockObject = new GameObject("match clock");
            var clock = clockObject.AddComponent<MatchClock>();

            clock.TogglePause();
            Assert.IsTrue(clock.Paused);
            Assert.AreEqual(0f, clock.Speed, "pause is the 0x entry in the speed list");
            Assert.AreEqual(0f, Time.timeScale);

            clock.TogglePause();
            Assert.IsFalse(clock.Paused);
            Assert.AreEqual(1f, Time.timeScale);

            Object.DestroyImmediate(clockObject);
        }

        [Test]
        public void PauseResume_ReturnsToTheLastNonZeroSpeed()
        {
            var clockObject = new GameObject("match clock");
            var clock = clockObject.AddComponent<MatchClock>();

            clock.StepSpeed(1); // 1.5x
            clock.TogglePause();
            Assert.AreEqual(0f, Time.timeScale);

            clock.TogglePause();
            Assert.AreEqual(1.5f, clock.Speed, "P restores the speed you paused at");
            Assert.AreEqual(1.5f, Time.timeScale);

            clock.StepSpeed(-1); // 1x
            clock.StepSpeed(-1); // 0.75x
            clock.TogglePause();
            clock.TogglePause();
            Assert.AreEqual(0.75f, clock.Speed);

            Object.DestroyImmediate(clockObject);
        }

        [Test]
        public void SpeedSteps_ThroughTheWholeListIncludingPause()
        {
            var clockObject = new GameObject("match clock");
            var clock = clockObject.AddComponent<MatchClock>();

            Assert.AreEqual(1f, clock.Speed);
            clock.StepSpeed(1);
            Assert.AreEqual(1.5f, clock.Speed);
            Assert.AreEqual(1.5f, Time.timeScale);
            clock.StepSpeed(1);
            Assert.AreEqual(2f, clock.Speed);

            clock.StepSpeed(1);
            Assert.AreEqual(2f, clock.Speed, "2x is the fastest");

            clock.StepSpeed(-1);
            Assert.AreEqual(1.5f, clock.Speed);
            clock.StepSpeed(-1);
            Assert.AreEqual(1f, clock.Speed);
            clock.StepSpeed(-1);
            Assert.AreEqual(0.75f, clock.Speed);
            clock.StepSpeed(-1);
            Assert.AreEqual(0.5f, clock.Speed);
            clock.StepSpeed(-1);
            Assert.AreEqual(0f, clock.Speed);
            Assert.IsTrue(clock.Paused);
            Assert.AreEqual(0f, Time.timeScale);

            clock.StepSpeed(-1);
            Assert.AreEqual(0f, clock.Speed, "0 is the slowest");

            clock.StepSpeed(1);
            Assert.AreEqual(0.5f, clock.Speed, "stepping up from pause starts at 0.5x");

            Object.DestroyImmediate(clockObject);
        }

        [Test]
        public void PointerOverTheStrip_BlocksGameClicks()
        {
            var clockObject = new GameObject("match clock");
            clockObject.AddComponent<MatchClock>();

            Assert.IsTrue(MatchClock.BlocksPointer(new Vector2(Screen.width * 0.5f, 20f)),
                "the strip sits 8 px above the bottom edge");
            Assert.IsFalse(MatchClock.BlocksPointer(new Vector2(0f, Screen.height - 4f)),
                "the top-left corner is outside the strip");

            Object.DestroyImmediate(clockObject);
        }

        [UnityTest]
        public IEnumerator GameCore_ContainsTheClock()
        {
            yield return GameplaySceneLoader.Load("LargeMap");

            MatchClock clock = Object.FindFirstObjectByType<MatchClock>();
            Assert.IsNotNull(clock, "GameCore must carry the match clock");

            clock.TogglePause();
            Assert.AreEqual(0f, Time.timeScale);
            clock.TogglePause();
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void Destroy_RestoresNormalTime()
        {
            var clockObject = new GameObject("match clock");
            var clock = clockObject.AddComponent<MatchClock>();
            clock.TogglePause();

            Object.DestroyImmediate(clockObject);

            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
