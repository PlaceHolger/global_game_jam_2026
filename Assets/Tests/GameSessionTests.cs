using NUnit.Framework;
using Warmask.Scenes;

namespace Warmask.Tests
{
    public class GameSessionTests
    {
        [TearDown]
        public void RestoreDefaults()
        {
            Globals.Instance.playerCount = 2;
            GameSession.MapScene = "LargeMap";
        }

        [Test]
        public void FourPlayers_AlwaysUsesTheLargeMap()
        {
            Globals.Instance.playerCount = 4;
            GameSession.MapScene = "SampleScene";

            Assert.AreEqual("LargeMap", GameSession.MapScene);
        }

        [Test]
        public void ThreePlayers_AlwaysUsesTheLargeMap()
        {
            Globals.Instance.playerCount = 3;
            GameSession.MapScene = "SampleScene";

            Assert.AreEqual("LargeMap", GameSession.MapScene);
        }

        [Test]
        public void TwoPlayers_KeepsTheChosenMap()
        {
            Globals.Instance.playerCount = 2;
            GameSession.MapScene = "SampleScene";

            Assert.AreEqual("SampleScene", GameSession.MapScene);
        }
    }
}
