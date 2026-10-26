using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warmask.Core;

namespace Warmask.Tests
{
    public class CameraControllerTests
    {
        [Test]
        public void FitOrthoSize_UsesCurrentAspect()
        {
            Assert.AreEqual(12.875f, OrthographicCameraController.CalculateFitOrthoSize(new Vector2(44f, 22f), 16f / 9f, 0.5f), 0.001f);
            Assert.AreEqual(17f, OrthographicCameraController.CalculateFitOrthoSize(new Vector2(44f, 22f), 4f / 3f, 0.5f), 0.001f);
        }

        [Test]
        public void ClampCenter_KeepsViewInsideMap()
        {
            Vector2 center = OrthographicCameraController.ClampCenter(new Vector2(100f, 100f), Vector2.zero, new Vector2(44f, 22f), 4.5f, 16f / 9f);
            Assert.AreEqual(14f, center.x, 0.001f);
            Assert.AreEqual(6.5f, center.y, 0.001f);
        }

        [UnityTest]
        public IEnumerator FitAndZoom_ClampToBounds()
        {
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 16f / 9f;

            var boundsObject = new GameObject("Bounds");
            var bounds = boundsObject.AddComponent<MapBounds>();
            bounds.Configure(Vector2.zero, new Vector2(44f, 22f), 4.5f, 0.5f);

            var controller = cameraObject.AddComponent<OrthographicCameraController>();
            controller.SetBounds(bounds);
            controller.FitToBounds();
            Assert.AreEqual(12.875f, controller.CurrentOrthoSize, 0.001f);

            controller.SetZoom(4.5f);
            controller.Pan(new Vector2(100f, 100f));
            yield return null;
            Assert.AreEqual(14f, controller.ViewCenter.x, 0.001f);
            Assert.AreEqual(6.5f, controller.ViewCenter.y, 0.001f);

            Object.Destroy(cameraObject);
            Object.Destroy(boundsObject);
        }
    }
}
