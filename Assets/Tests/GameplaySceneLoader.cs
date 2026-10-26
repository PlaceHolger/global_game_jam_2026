using System.Collections;
using UnityEngine.SceneManagement;

namespace Warmask.Tests
{
    public static class GameplaySceneLoader
    {
        public const string CoreScene = "GameCore";

        public static IEnumerator Load(string mapSceneName)
        {
            SceneManager.LoadScene(CoreScene);
            yield return null;
            SceneManager.LoadScene(mapSceneName, LoadSceneMode.Additive);
            yield return null;
            yield return null;
        }
    }
}
