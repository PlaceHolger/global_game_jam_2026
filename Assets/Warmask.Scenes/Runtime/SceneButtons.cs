using UnityEngine;


namespace Warmask.Scenes
{
    public class SceneButtons : MonoBehaviour
    {
        public void LoadMenuScene()
        {
            _ = SceneManager.Instance.LoadMenuSceneAsync();
        }

        public void LoadGameScene()
        {
            _ = SceneManager.Instance.LoadGameSceneAsync();
        }
    }
}