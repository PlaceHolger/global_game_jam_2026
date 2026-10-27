using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
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
            Assert.AreEqual(1, manager.SelectedCount, "an enemy planet is never selected");
            CollectionAssert.Contains(manager.Selected.ToArray(), p1Home);
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
    }
}
