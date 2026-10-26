using NUnit.Framework;
using Warmask.Planet;

namespace Warmask.Tests
{
    public class MatchEndTests
    {
        [SetUp]
        public void FreshMatch()
        {
            Globals.Instance.localMultiplayer = false;
        }

        [TearDown]
        public void Reset()
        {
            Globals.Instance.localMultiplayer = false;
        }

        [Test]
        public void OnlyTheHumanLeft_IsAWonMatch()
        {
            Assert.IsTrue(PlanetsManager.IsOnlyHumanLeft(new[] { Globals.ePlayer.Player1 }));
        }

        [Test]
        public void AnAiStillAlive_IsNotAWonMatch()
        {
            Assert.IsFalse(PlanetsManager.IsOnlyHumanLeft(new[]
            {
                Globals.ePlayer.Player1, Globals.ePlayer.Player2, Globals.ePlayer.Player3
            }));
        }

        [Test]
        public void NoHumanLeft_IsNotAWonMatch()
        {
            Assert.IsFalse(PlanetsManager.IsOnlyHumanLeft(new[]
            {
                Globals.ePlayer.Player2, Globals.ePlayer.Player3, Globals.ePlayer.Player4
            }));
        }

        [Test]
        public void NeutralPlanetsOnly_IsNotAWonMatch()
        {
            Assert.IsFalse(PlanetsManager.IsOnlyHumanLeft(new[]
            {
                Globals.ePlayer.None, Globals.ePlayer.None
            }));
            Assert.IsFalse(PlanetsManager.IsOnlyHumanLeft(new Globals.ePlayer[0]));
        }

        [Test]
        public void TwoLocalHumans_AreStillAWonMatch()
        {
            Globals.Instance.localMultiplayer = true;

            Assert.IsTrue(PlanetsManager.IsOnlyHumanLeft(new[]
            {
                Globals.ePlayer.Player1, Globals.ePlayer.Player2
            }));
        }
    }
}
