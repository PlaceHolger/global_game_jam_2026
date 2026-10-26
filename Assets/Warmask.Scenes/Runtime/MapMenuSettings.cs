using UnityEngine;

namespace Warmask.Scenes
{
    /// <summary>
    /// Menu hook: picks the map the next match loads. Large is the default.
    /// </summary>
    public class MapMenuSettings : MonoBehaviour
    {
        public void SetLargeMap(bool isLarge) => GameSession.MapScene = isLarge ? "LargeMap" : "SampleScene";
    }
}
