using NUnit.Framework;
using Warmask.Planet;

namespace Warmask.Tests
{
    public class PlanetAttackSpeedTests
    {
        [Test]
        public void AttackerSpeedMultiplier_MatchesAgreedCurve()
        {
            Assert.AreEqual(0.5f, PlanetInstance.AttackerSpeedMultiplier(1), 0.001f);
            Assert.AreEqual(1.0f, PlanetInstance.AttackerSpeedMultiplier(5), 0.001f);
            Assert.AreEqual(1.3f, PlanetInstance.AttackerSpeedMultiplier(8), 0.001f);
            Assert.AreEqual(1.5f, PlanetInstance.AttackerSpeedMultiplier(10), 0.001f);
            Assert.AreEqual(2.0f, PlanetInstance.AttackerSpeedMultiplier(40), 0.001f);
            Assert.AreEqual(2.0f, PlanetInstance.AttackerSpeedMultiplier(100), 0.001f);
        }

        [Test]
        public void AttackerSpeedMultiplier_AboveFortyStaysCapped()
        {
            Assert.AreEqual(2.0f, PlanetInstance.AttackerSpeedMultiplier(1000), 0.001f);
        }
    }
}
