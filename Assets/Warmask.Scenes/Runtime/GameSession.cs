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
        /// Four players only fit on the large map, so a four-player match ignores the menu choice.
        /// </summary>
        public static string MapScene
        {
            get => Globals.Instance != null && Globals.Instance.playerCount >= 4 ? "LargeMap" : _mapScene;
            set => _mapScene = value;
        }
    }
}
