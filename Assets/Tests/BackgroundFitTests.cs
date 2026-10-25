using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Warmask.Tests
{
    public class BackgroundFitTests
    {
        private GameObject cameraObject;
        private GameObject backgroundObject;
        private Texture2D texture;

        [TearDown]
        public void TearDown()
        {
            if (backgroundObject)
                Object.Destroy(backgroundObject);
            if (cameraObject)
                Object.Destroy(cameraObject);
            if (texture)
                Object.Destroy(texture);
        }

        [UnityTest]
        public IEnumerator Background_CoversWideViewport_AndStaysCentered()
        {
            cameraObject = new GameObject("Fit Camera") { tag = "MainCamera" };
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.aspect = 2.16f;
            cameraObject.transform.position = new Vector3(3f, -2f, -10f);

            backgroundObject = new GameObject("Fit Background");
            backgroundObject.transform.position = new Vector3(0f, 0f, 0.01f);
            backgroundObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var renderer = backgroundObject.AddComponent<SpriteRenderer>();
            texture = new Texture2D(1, 1);
            renderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

            var fit = backgroundObject.AddComponent<BackgroundColorChanger>();
            fit.imageToChange = renderer;
            fit.targetCamera = cam;

            yield return null;

            float viewHeight = cam.orthographicSize * 2f;
            float viewWidth = viewHeight * cam.aspect;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            Vector3 scale = backgroundObject.transform.localScale;

            Assert.AreEqual(scale.x, scale.y, 0.0001f, "Cover scale must be uniform.");
            Assert.GreaterOrEqual(spriteSize.x * scale.x, viewWidth - 0.001f, "Background leaves horizontal bars.");
            Assert.GreaterOrEqual(spriteSize.y * scale.y, viewHeight - 0.001f, "Background leaves vertical bars.");
            Assert.AreEqual(3f, backgroundObject.transform.position.x, 0.001f, "Background is not centred on the camera.");
            Assert.AreEqual(-2f, backgroundObject.transform.position.y, 0.001f, "Background is not centred on the camera.");
            Assert.AreEqual(0.01f, backgroundObject.transform.position.z, 0.001f, "Background depth changed.");
            Assert.AreEqual(180f, backgroundObject.transform.eulerAngles.y, 0.1f, "Mask rotation was overwritten.");
        }
    }
}
