namespace Warmask.Scenes
{
    /// <summary>
    /// The match the menu is about to start. Static so it survives the scene loads;
    /// map selection, modes and player slots plug in here later.
    /// </summary>
    public static class GameSession
    {
        public static string MapScene = "SampleScene";
        public static int PlayerCount = 2;
    }
}
