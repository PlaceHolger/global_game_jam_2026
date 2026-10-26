using System.Collections.Generic;
using NUnit.Framework;
using Warmask.Planet;

namespace Warmask.Tests
{
    public class ContestedPlanetTests
    {
        [Test]
        public void StrongestAttacker_TakesTheCapture()
        {
            var inOrbit = new Dictionary<int, int>
            {
                { (int)Globals.ePlayer.Player2, 1 },
                { (int)Globals.ePlayer.Player3, 6 },
                { (int)Globals.ePlayer.Player4, 2 }
            };

            Assert.AreEqual(Globals.ePlayer.Player3,
                PlanetInstance.StrongestAttacker(inOrbit, Globals.ePlayer.Player1));
        }

        [Test]
        public void StrongestAttacker_IgnoresTheOwnerAndEmptyOrbit()
        {
            Assert.AreEqual(Globals.ePlayer.None,
                PlanetInstance.StrongestAttacker(new Dictionary<int, int>
                {
                    { (int)Globals.ePlayer.Player1, 9 }
                }, Globals.ePlayer.Player1));

            Assert.AreEqual(Globals.ePlayer.None,
                PlanetInstance.StrongestAttacker(new Dictionary<int, int>(), Globals.ePlayer.Player1));
        }
    }
}
