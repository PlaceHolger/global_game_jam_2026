namespace Warmask.Scenes
{
    /// <summary>
    /// The match the menu is about to start. Static so it survives the scene loads.
    /// The player count lives in <see cref="Globals.playerCount"/>.
    /// </summary>
    public static class GameSession
    {
        private static string _mapScene = "LargeMap";

        /// <summary>
        /// Three or four players need the extra homes, so those matches ignore the menu choice.
        /// </summary>
        public static string MapScene
        {
            get => Globals.Instance != null && Globals.Instance.playerCount > 2 ? "LargeMap" : _mapScene;
            set => _mapScene = value;
        }
    }
}
