using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warmask.Scenes
{

    public class SceneManager : MonoBehaviour
    {
        [SerializeField] private string menuSceneName = "MenuScene";
        [SerializeField] private string gameSceneName = "GameScene";

        private AsyncOperation _currentLoadOperation;

        public static SceneManager Instance { get; private set; }

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
            await LoadSceneAsync(gameSceneName);
        }

        public async Task LoadSceneAsync(string sceneName)
        {
            if (IsLoading)
            {
                Debug.LogWarning("Es wird bereits eine Scene geladen.");
                return;
            }

            _currentLoadOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
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

            _currentLoadOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
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