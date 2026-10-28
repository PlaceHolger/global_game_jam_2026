namespace Warmask.Scenes
{
    /// <summary>
    /// The match the menu is about to start. Static so it survives the scene loads.
    /// The player count and slot choices live in <see cref="Globals"/> / <see cref="MatchConfig"/>.
    /// </summary>
    public static class GameSession
    {
        public static string MapScene { get; set; } = "LargeMap";
    }
}
