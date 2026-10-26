using UnityEngine;


namespace Warmask.Scenes
{
    public class SceneButtons : MonoBehaviour
    {
        public void LoadMenuScene()
        {
            _ = SceneLoader.Instance.LoadMenuSceneAsync();
        }

        public void LoadGameScene()
        {
            _ = SceneLoader.Instance.LoadGameSceneAsync();
        }
    }
}