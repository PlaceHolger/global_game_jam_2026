using NUnit.Framework;
using UnityEngine;

namespace Warmask.Tests
{
    public class FourPlayerGlobalsTests
    {
        [Test]
        public void PlayerEnum_HasFourSlots()
        {
            Assert.AreEqual(3, (int)Globals.ePlayer.Player3);
            Assert.AreEqual(4, (int)Globals.ePlayer.Player4);
        }

        [Test]
        public void EveryPlayer_HasAColourAndASprite()
        {
            Globals globals = Globals.Instance;
            for (int i = 1; i <= 4; i++)
            {
                var player = (Globals.ePlayer)i;
                Assert.AreNotEqual(Color.white, globals.GetPlayerColor(player), "colour for " + player);
                Assert.IsNotNull(globals.GetPlayerSprite(player), "sprite for " + player);
            }
        }

        [Test]
        public void YellowAndOrange_HaveDifferentShipForms()
        {
            Globals globals = Globals.Instance;
            Assert.AreNotEqual(
                globals.GetPlayerSprite(Globals.ePlayer.Player1),
                globals.GetPlayerSprite(Globals.ePlayer.Player3),
                "P1 yellow and P3 orange are too close in colour to share a silhouette");
        }

        [Test]
        public void PlayerColours_AreAllDifferent()
        {
            Globals globals = Globals.Instance;
            for (int a = 1; a <= 4; a++)
            {
                for (int b = a + 1; b <= 4; b++)
                {
                    Color ca = globals.GetPlayerColor((Globals.ePlayer)a);
                    Color cb = globals.GetPlayerColor((Globals.ePlayer)b);
                    float distance = Mathf.Abs(ca.r - cb.r) + Mathf.Abs(ca.g - cb.g) + Mathf.Abs(ca.b - cb.b);
                    Assert.Greater(distance, 0.3f, $"P{a} and P{b} look the same");
                }
            }
        }
    }
}
