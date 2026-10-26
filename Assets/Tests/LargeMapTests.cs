using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Warmask.Planet;

namespace Warmask.Tests
{
    public class LargeMapTests
    {
        [UnityTest]
        public IEnumerator LargeMap_HasFourEqualQuadrants()
        {
            yield return GameplaySceneLoader.Load("LargeMap");

            var planets = Object.FindObjectsByType<PlanetInstance>();
            Assert.AreEqual(17, planets.Length);

            var quadrants = new Dictionary<string, List<string>>();
            foreach (var planet in planets)
            {
                Vector2 p = planet.transform.position;
                if (p == Vector2.zero)
                    continue;

                string key = (p.y > 0 ? "N" : "S") + (p.x > 0 ? "E" : "W");
                if (!quadrants.ContainsKey(key))
                    quadrants[key] = new List<string>();

                float distance = Mathf.Round(Vector2.Distance(Vector2.zero, p) * 100f) / 100f;
                quadrants[key].Add($"{planet.PlanetType}:{planet.PlanetSize:0.0}:{distance:0.00}");
            }

            Assert.AreEqual(4, quadrants.Count);
            var expected = quadrants["NE"].OrderBy(x => x).ToArray();
            foreach (var pair in quadrants)
                CollectionAssert.AreEqual(expected, pair.Value.OrderBy(x => x).ToArray(), pair.Key);
        }

        [UnityTest]
        public IEnumerator LargeMap_HasFourHomePlanets_AndNeutralCentre()
        {
            yield return GameplaySceneLoader.Load("LargeMap");

            var planets = Object.FindObjectsByType<PlanetInstance>();
            var homes = planets.Where(p => Mathf.Abs(Mathf.Abs(p.transform.position.x) - 16f) < 0.01f
                                        && Mathf.Abs(Mathf.Abs(p.transform.position.y) - 7f) < 0.01f).ToArray();

            Assert.AreEqual(4, homes.Length);
            Assert.AreEqual(Globals.ePlayer.Player1, homes.Single(p => p.transform.position.x < 0 && p.transform.position.y > 0).OwnedBy);
            Assert.AreEqual(Globals.ePlayer.Player2, homes.Single(p => p.transform.position.x > 0 && p.transform.position.y < 0).OwnedBy);
            Assert.AreEqual(Globals.ePlayer.None, planets.Single(p => p.transform.position == Vector3.zero).OwnedBy);
        }
    }
}
