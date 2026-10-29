using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warmask.Planet;

namespace Warmask.Tests
{
    public class FourPlayerMatchTests
    {
        [TearDown]
        public void RestoreTwoPlayers()
        {
            Globals.Instance.playerCount = 2;
            MatchConfig.Reset();
        }

        [UnityTest]
        public IEnumerator FourPlayerMatch_OwnsEveryCornerAndRunsThreeAis()
        {
            Globals.Instance.playerCount = 4;
            yield return GameplaySceneLoader.Load("LargeMap");

            var homes = Object.FindObjectsByType<PlanetInstance>()
                .Where(p => Mathf.Abs(Mathf.Abs(p.transform.position.x) - 16f) < 0.01f
                         && Mathf.Abs(Mathf.Abs(p.transform.position.y) - 7f) < 0.01f)
                .ToArray();

            Assert.AreEqual(4, homes.Length);
            foreach (MapPlanet planet in MapLayout.For("LargeMap"))
                Assert.IsNotNull(Object.FindObjectsByType<PlanetInstance>().SingleOrDefault(p => p.name == planet.name),
                    "MapLayout planet is missing from the scene: " + planet.name);
            Assert.AreEqual(Globals.ePlayer.Player1, HomeNear(homes, x: -1, y: 1).OwnedBy, "NW home");
            Assert.AreEqual(Globals.ePlayer.Player2, HomeNear(homes, x: 1, y: -1).OwnedBy, "SE home");
            Assert.AreEqual(Globals.ePlayer.Player3, HomeNear(homes, x: 1, y: 1).OwnedBy, "NE home");
            Assert.AreEqual(Globals.ePlayer.Player4, HomeNear(homes, x: -1, y: -1).OwnedBy, "SW home");

            foreach (var home in homes)
                Assert.Greater(home.DefendingUnityOnGround, 0, home.name + " starts undefended");

            Assert.IsNotNull(GameObject.Find("EnemyAI P3"), "P3 has no AI");
            Assert.IsNotNull(GameObject.Find("EnemyAI P4"), "P4 has no AI");
        }

        [UnityTest]
        public IEnumerator FourPlayerMatch_EndsOnlyWhenNoAiIsLeft()
        {
            Globals.Instance.playerCount = 4;
            yield return GameplaySceneLoader.Load("LargeMap");

            var planets = PlanetsManager.Instance;
            int wins = 0;
            int losses = 0;
            planets.OnPlayerWin.AddListener(() => wins++);
            planets.OnPlayerLost.AddListener(() => losses++);

            PlanetInstance p1 = OwnedHome(Globals.ePlayer.Player1);
            PlanetInstance p2 = OwnedHome(Globals.ePlayer.Player2);
            PlanetInstance p3 = OwnedHome(Globals.ePlayer.Player3);
            PlanetInstance p4 = OwnedHome(Globals.ePlayer.Player4);

            Eliminate(p3, Globals.ePlayer.Player3);
            Assert.AreEqual(0, wins, "one AI down must not win the match");

            Eliminate(p2, Globals.ePlayer.Player2);
            Eliminate(p4, Globals.ePlayer.Player4);
            Assert.AreEqual(1, wins, "the last AI down wins the match");
            Assert.AreEqual(0, losses);

            Eliminate(p1, Globals.ePlayer.Player1);
            Assert.AreEqual(1, losses, "the human losing all planets shows the lost screen");
        }

        [UnityTest]
        public IEnumerator TeamMatch_AlliesDoNotEndItAndLastTeamWins()
        {
            MatchConfig.Apply(new[]
            {
                new MatchSlot { mode = MatchSlotMode.Human, home = 0, input = MatchInput.MouseKeyboard, team = 1 },
                new MatchSlot { mode = MatchSlotMode.Ai, home = 1, team = 1 },
                new MatchSlot { mode = MatchSlotMode.Ai, home = 2, team = 2 },
                new MatchSlot { mode = MatchSlotMode.Off, home = 3, team = 3 }
            }, "LargeMap");
            yield return GameplaySceneLoader.Load("LargeMap");

            var planets = PlanetsManager.Instance;
            int wins = 0;
            int losses = 0;
            planets.OnPlayerWin.AddListener(() => wins++);
            planets.OnPlayerLost.AddListener(() => losses++);

            PlanetInstance ally = OwnedHome(Globals.ePlayer.Player2);
            PlanetInstance enemy = OwnedHome(Globals.ePlayer.Player3);

            Eliminate(ally, Globals.ePlayer.Player2);
            Assert.AreEqual(0, wins, "an allied AI going down must not win the match");
            Assert.AreEqual(0, losses, "an allied AI going down must not end the human's match");

            Eliminate(enemy, Globals.ePlayer.Player3);
            Assert.AreEqual(1, wins, "eliminating the last hostile team wins for the human team");
            Assert.AreEqual(0, losses);
            Assert.AreEqual(Globals.ePlayer.Player1, MatchStats.Winner,
                "the end screen credits a human on the winning team");
        }

        private static void Eliminate(PlanetInstance home, Globals.ePlayer player)
        {
            home.SetOwner(Globals.ePlayer.None, spawnCaptureEffect: false);
            PlanetsManager.Instance.CheckIfPlayerLostAllPlanets(player);
        }

        private static PlanetInstance OwnedHome(Globals.ePlayer owner) =>
            Object.FindObjectsByType<PlanetInstance>()
                .Single(p => p.OwnedBy == owner && p.name == MapLayout.For(MatchConfig.MapScene)[(int)owner - 1].name);

        private static PlanetInstance HomeNear(PlanetInstance[] homes, int x, int y) =>
            homes.Single(p => Mathf.Sign(p.transform.position.x) == x && Mathf.Sign(p.transform.position.y) == y);
    }
}
