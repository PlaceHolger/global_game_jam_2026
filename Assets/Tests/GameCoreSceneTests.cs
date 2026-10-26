using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Warmask.Planet;
using Warmask.Ship;

namespace Warmask.Tests
{
    public class GameCoreSceneTests
    {
        [UnityTest]
        public IEnumerator GameCore_HasCameraAndManagers_AndNoPlanets()
        {
            SceneManager.LoadScene("GameCore");
            for (int i = 0; i < 5; i++)
                yield return null;

            Assert.IsNotNull(Camera.main);
            // Main camera plus the overlay UI camera.
            Assert.AreEqual(2, Object.FindObjectsByType<Camera>().Length);
            Assert.AreEqual(1, Object.FindObjectsByType<ShipManagerInstance>().Length);
            Assert.AreEqual(1, Object.FindObjectsByType<PlanetSelectionManager>().Length);
            Assert.AreEqual(0, Object.FindObjectsByType<PlanetInstance>().Length);
            Assert.AreEqual(0, Object.FindObjectsByType<CursorLight>().Length);
            Assert.IsNull(PlanetsManager.Instance);
        }

        [UnityTest]
        public IEnumerator SampleScene_LoadsWithCore()
        {
            yield return GameplaySceneLoader.Load("SampleScene");

            Assert.IsNotNull(Camera.main);
            Assert.IsNotEmpty(Object.FindObjectsByType<PlanetInstance>());
            Assert.IsNotNull(PlanetsManager.Instance);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length);
        }

        [UnityTest]
        public IEnumerator EndScreenController_ShowsWinAfterMapLoads()
        {
            yield return GameplaySceneLoader.Load("SampleScene");

            PlanetsManager.Instance.OnPlayerWin.Invoke();
            yield return null;

            Assert.IsTrue(FindIncludingInactive("End Screen").gameObject.activeSelf);
            Assert.IsTrue(FindIncludingInactive("Win Button").gameObject.activeSelf);
            Assert.IsFalse(FindIncludingInactive("Lost Button").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator CursorLight_CreatesAfterMapLoads()
        {
            SceneManager.LoadScene("GameCore");
            yield return null;
            yield return null;

            SceneManager.LoadScene("SampleScene", LoadSceneMode.Additive);
            yield return null;
            yield return null;
            yield return null;
            Assert.IsNotEmpty(Object.FindObjectsByType<CursorLight>());
        }

        private static Transform FindIncludingInactive(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == name)
                    return t;

            Assert.Fail($"No object named {name}.");
            return null;
        }
    }
}
