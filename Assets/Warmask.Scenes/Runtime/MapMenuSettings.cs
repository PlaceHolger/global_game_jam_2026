using UnityEngine;

namespace Warmask.Scenes
{
    /// <summary>
    /// Menu hooks for the next match: which map loads and how many players sit at the table.
    /// </summary>
    public class MapMenuSettings : MonoBehaviour
    {
        private void Start()
        {
            // The menu reloads from disk with its saved toggles, so forget the last match's choices.
            GameSession.MapScene = "LargeMap";
            Globals.Instance.playerCount = 2;
        }

        public void SetLargeMap(bool isLarge) => GameSession.MapScene = isLarge ? "LargeMap" : "SampleScene";

        public void SetFourPlayers(bool isFour) => Globals.Instance.playerCount = isFour ? 4 : 2;
    }
}
