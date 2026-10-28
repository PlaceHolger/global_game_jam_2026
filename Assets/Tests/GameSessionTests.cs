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
        public void MapChoice_IsKeptForFourPlayers()
        {
            Globals.Instance.playerCount = 4;
            GameSession.MapScene = "SampleScene";

            Assert.AreEqual("SampleScene", GameSession.MapScene,
                "setup decides the map; the player count no longer forces LargeMap");
        }

        [Test]
        public void MapChoice_IsKeptForTwoPlayers()
        {
            Globals.Instance.playerCount = 2;
            GameSession.MapScene = "SampleScene";

            Assert.AreEqual("SampleScene", GameSession.MapScene);
        }
    }
}
