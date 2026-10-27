using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warmask.Planet;
using Warmask.Scenes;

namespace Warmask.Tests
{
    public class MatchSetupTests
    {
        [TearDown]
        public void RestoreTwoPlayers()
        {
            Globals.Instance.playerCount = 2;
        }

        [Test]
        public void EnemyCount_ClampsToOneToThreeAndWritesPlayerCount()
        {
            var menuObject = new GameObject("menu settings");
            var menu = menuObject.AddComponent<MapMenuSettings>();

            menu.SetEnemyCount(0f);
            Assert.AreEqual(2, Globals.Instance.playerCount, "0 enemies clamps to one enemy / two players");

            menu.SetEnemyCount(2.4f);
            Assert.AreEqual(3, Globals.Instance.playerCount, "2.4 rounds to two enemies / three players");

            menu.SetEnemyCount(9f);
            Assert.AreEqual(4, Globals.Instance.playerCount, "9 clamps to three enemies / four players");

            Object.DestroyImmediate(menuObject);
        }

        [UnityTest]
        public IEnumerator ThreePlayers_OwnThreeHomesAndRunTwoAis()
        {
            Globals.Instance.playerCount = 3;
            yield return GameplaySceneLoader.Load("LargeMap");

            PlanetInstance[] homes = Object.FindObjectsByType<PlanetInstance>()
                .Where(p => Mathf.Abs(Mathf.Abs(p.transform.position.x) - 16f) < 0.01f
                         && Mathf.Abs(Mathf.Abs(p.transform.position.y) - 7f) < 0.01f)
                .ToArray();

            Assert.AreEqual(4, homes.Length);
            Assert.AreEqual(Globals.ePlayer.Player1, HomeNear(homes, x: -1, y: 1).OwnedBy, "NW home");
            Assert.AreEqual(Globals.ePlayer.Player2, HomeNear(homes, x: 1, y: -1).OwnedBy, "SE home");
            Assert.AreEqual(Globals.ePlayer.Player3, HomeNear(homes, x: 1, y: 1).OwnedBy, "NE home");
            Assert.AreEqual(Globals.ePlayer.None, HomeNear(homes, x: -1, y: -1).OwnedBy, "SW home stays neutral");

            Assert.IsNotNull(GameObject.Find("EnemyAI P3"), "P3 has no AI");
            Assert.IsNull(GameObject.Find("EnemyAI P4"), "P4 must not exist in a three-player match");
        }

        private static PlanetInstance HomeNear(PlanetInstance[] homes, int x, int y) =>
            homes.Single(p => Mathf.Sign(p.transform.position.x) == x && Mathf.Sign(p.transform.position.y) == y);
    }
}
