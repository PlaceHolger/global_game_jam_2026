using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warmask.Core;

namespace Warmask.Tests
{
    public class FrameStatsOverlayTests
    {
        [UnityTest]
        public IEnumerator Overlay_AutoCreatesAndSamplesFrameStats()
        {
            yield return null;

            Assert.IsNotNull(FrameStatsOverlay.Instance,
                "The dev-only frame stats overlay should create itself on play.");

            yield return new WaitForSecondsRealtime(0.8f);

            Assert.Greater(FrameStatsOverlay.Instance.Fps, 0f, "Overlay did not compute an FPS.");
            Assert.Greater(FrameStatsOverlay.Instance.SampleCount, 0, "Overlay did not produce a sample.");
        }
    }
}
