using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warmask.Scenes
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private string menuSceneName = "Menu";
        [SerializeField] private string coreSceneName = "GameCore";

        private AsyncOperation _currentLoadOperation;

        public static SceneLoader Instance { get; private set; }

        public bool IsLoading => _currentLoadOperation != null && !_currentLoadOperation.isDone;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async Task LoadMenuSceneAsync()
        {
            await LoadSceneAsync(menuSceneName);
        }

        public async Task LoadGameSceneAsync()
        {
            await LoadSceneAsync(coreSceneName);
            await LoadSceneAsync(GameSession.MapScene, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(GameSession.MapScene));
        }

        public async Task LoadSceneAsync(string sceneName)
        {
            await LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        private async Task LoadSceneAsync(string sceneName, LoadSceneMode mode)
        {
            if (IsLoading)
            {
                Debug.LogWarning("Es wird bereits eine Scene geladen.");
                return;
            }

            _currentLoadOperation = SceneManager.LoadSceneAsync(sceneName, mode);
            _currentLoadOperation.allowSceneActivation = false;

            while (_currentLoadOperation.progress < 0.9f)
            {
                await Task.Yield();
            }

            _currentLoadOperation.allowSceneActivation = true;

            while (!_currentLoadOperation.isDone)
            {
                await Task.Yield();
            }

            _currentLoadOperation = null;
        }

        public async Task PreloadSceneAsync(string sceneName)
        {
            if (IsLoading)
            {
                Debug.LogWarning("Es wird bereits eine Scene geladen.");
                return;
            }

            _currentLoadOperation = SceneManager.LoadSceneAsync(sceneName);
            _currentLoadOperation.allowSceneActivation = false;

            while (_currentLoadOperation.progress < 0.9f)
            {
                await Task.Yield();
            }
        }

        public void ActivatePreloadedScene()
        {
            if (_currentLoadOperation == null)
            {
                Debug.LogWarning("Keine vorgeladene Scene vorhanden.");
                return;
            }

            _currentLoadOperation.allowSceneActivation = true;
        }

        public float GetLoadingProgress()
        {
            if (_currentLoadOperation == null) return 1f;
            return Mathf.Clamp01(_currentLoadOperation.progress / 0.9f);
        }
    }
}
