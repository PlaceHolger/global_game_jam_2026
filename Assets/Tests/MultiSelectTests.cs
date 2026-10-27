using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Warmask.Planet;

namespace Warmask.Tests
{
    public class MultiSelectTests
    {
        [TearDown]
        public void RestoreTwoPlayers()
        {
            Globals.Instance.playerCount = 2;
        }

        [UnityTest]
        public IEnumerator ClickingOwnPlanets_TogglesTheSelection()
        {
            Globals.Instance.playerCount = 2;
            yield return GameplaySceneLoader.Load("LargeMap");

            PlanetSelectionManager manager = PlanetSelectionManager.Instance;
            PlanetInstance p1Home = Object.FindObjectsByType<PlanetInstance>()
                .Single(p => p.OwnedBy == Globals.ePlayer.Player1);
            PlanetInstance p2Home = Object.FindObjectsByType<PlanetInstance>()
                .Single(p => p.OwnedBy == Globals.ePlayer.Player2);

            manager.HandlePlanetClick(p1Home);
            Assert.AreEqual(1, manager.SelectedCount, "the first click selects the home");

            manager.HandlePlanetClick(p1Home);
            Assert.AreEqual(0, manager.SelectedCount, "a second click on the same planet deselects it");

            manager.HandlePlanetClick(p1Home);
            manager.HandlePlanetClick(p2Home);
            Assert.AreEqual(0, manager.SelectedCount,
                "the failed send to an enemy ends the order and never selects the enemy");
            CollectionAssert.DoesNotContain(manager.Selected.ToArray(), p2Home);
        }

        [UnityTest]
        public IEnumerator ClickingAnotherOwnPlanet_IsAnOrderNotASelection()
        {
            Globals.Instance.playerCount = 2;
            yield return GameplaySceneLoader.Load("LargeMap");

            PlanetSelectionManager manager = PlanetSelectionManager.Instance;
            PlanetInstance[] planets = Object.FindObjectsByType<PlanetInstance>();
            PlanetInstance home = planets.First(p => p.OwnedBy == Globals.ePlayer.Player1);
            PlanetInstance spare = planets.First(p => p.OwnedBy == Globals.ePlayer.None);
            spare.SetOwner(Globals.ePlayer.Player1, spawnCaptureEffect: false);

            manager.HandlePlanetClick(home);
            Assert.AreEqual(1, manager.SelectedCount, "the home is selected first");

            manager.HandlePlanetClick(spare);
            CollectionAssert.DoesNotContain(manager.Selected.ToArray(), spare);
            Assert.Less(manager.SelectedCount, 2, "clicking a second own planet sends instead of adding it");
        }

        [UnityTest]
        public IEnumerator AddToSelectionHeld_AddsASecondPlanet()
        {
            Globals.Instance.playerCount = 2;
            yield return GameplaySceneLoader.Load("LargeMap");

            PlanetSelectionManager manager = PlanetSelectionManager.Instance;
            PlanetInstance[] planets = Object.FindObjectsByType<PlanetInstance>();
            PlanetInstance home = planets.First(p => p.OwnedBy == Globals.ePlayer.Player1);
            PlanetInstance spare = planets.First(p => p.OwnedBy == Globals.ePlayer.None);
            spare.SetOwner(Globals.ePlayer.Player1, spawnCaptureEffect: false);

            manager.Initialize(Globals.ePlayer.Player1, new AdditivePointer());

            manager.HandlePlanetClick(home);
            manager.HandlePlanetClick(spare);

            Assert.AreEqual(2, manager.SelectedCount, "holding the add button adds instead of sending");
            CollectionAssert.Contains(manager.Selected.ToArray(), spare);
        }

        [UnityTest]
        public IEnumerator SelectedPlanet_HighlightUsesThePlayerColour()
        {
            Globals.Instance.playerCount = 2;
            yield return GameplaySceneLoader.Load("LargeMap");

            PlanetSelectionManager manager = PlanetSelectionManager.Instance;
            PlanetInstance home = Object.FindObjectsByType<PlanetInstance>()
                .First(p => p.OwnedBy == Globals.ePlayer.Player1);

            manager.HandlePlanetClick(home);

            Light2D highlight = home.GetComponentsInChildren<Light2D>(true)
                .First(l => l.gameObject.name == "Spot Light 2D");
            Assert.AreEqual(Globals.Instance.GetPlayerColor(Globals.ePlayer.Player1), highlight.color);
        }

        [UnityTest]
        public IEnumerator FailedSend_EndsTheOrder()
        {
            Globals.Instance.playerCount = 2;
            yield return GameplaySceneLoader.Load("LargeMap");

            PlanetSelectionManager manager = PlanetSelectionManager.Instance;
            PlanetInstance[] planets = Object.FindObjectsByType<PlanetInstance>();
            PlanetInstance home = planets.First(p => p.OwnedBy == Globals.ePlayer.Player1);
            PlanetInstance enemy = planets.First(p => p.OwnedBy == Globals.ePlayer.Player2);

            manager.HandlePlanetClick(home);
            Assert.AreEqual(1, manager.SelectedCount, "the home is selected first");

            home.SetTransferableShipCount(0);
            manager.HandlePlanetClick(enemy);

            Assert.AreEqual(0, manager.SelectedCount, "a send with no ships must end the order");
        }

        private sealed class AdditivePointer : IPointerSource
        {
            public Globals.ePlayer Player => Globals.ePlayer.Player1;
            public Vector2 ScreenPosition => Vector2.zero;
            public bool ConfirmPressed => false;
            public bool CancelPressed => false;
            public bool SendAllHeld => false;
            public bool AddToSelectionHeld => true;

            public bool TryGetWorldPoint(Camera camera, out Vector3 worldPoint)
            {
                worldPoint = default;
                return false;
            }
        }
    }
}
