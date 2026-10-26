using UnityEngine;
using Warmask.Planet;

namespace Warmask.Scenes
{
    /// <summary>
    /// Bridges the map's win/loss events to the end screen. Lives in the core
    /// scene, so it waits for the map and re-subscribes when a new map loads.
    /// </summary>
    public class EndScreenController : MonoBehaviour
    {
        [SerializeField] private GameObject endScreen;
        [SerializeField] private GameObject winButton;
        [SerializeField] private GameObject lostButton;

        private PlanetsManager _planets;

        public void Configure(GameObject endScreenObject, GameObject win, GameObject lost)
        {
            endScreen = endScreenObject;
            winButton = win;
            lostButton = lost;
        }

        public void ShowWin() => Show(winButton, lostButton);
        public void ShowLost() => Show(lostButton, winButton);

        private void Show(GameObject activeButton, GameObject inactiveButton)
        {
            if (endScreen) endScreen.SetActive(true);
            if (activeButton) activeButton.SetActive(true);
            if (inactiveButton) inactiveButton.SetActive(false);
        }

        private void Update()
        {
            PlanetsManager found = PlanetsManager.Instance;
            if (found == _planets)
                return;

            Unsubscribe();
            _planets = found;
            if (_planets == null)
                return;

            _planets.OnPlayerWin.AddListener(ShowWin);
            _planets.OnPlayerLost.AddListener(ShowLost);
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_planets == null)
                return;
            _planets.OnPlayerWin.RemoveListener(ShowWin);
            _planets.OnPlayerLost.RemoveListener(ShowLost);
        }
    }
}
