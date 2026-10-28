using UnityEngine;


namespace Warmask.Scenes
{
    public class SceneButtons : MonoBehaviour
    {
        public void LoadMenuScene()
        {
            if (!SceneLoader.Instance)
            {
                Debug.LogWarning("No SceneLoader in the open scenes. Start from Menu to use scene buttons.");
                return;
            }

            _ = SceneLoader.Instance.LoadMenuSceneAsync();
        }

        public void LoadGameScene()
        {
            MatchSetupScreen.Open();
        }
    }
}