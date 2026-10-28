using TMPro;
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
        private TMP_Text _statsText;

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

            EnsureStatsText();
            if (_statsText)
                _statsText.text = MatchStats.BuildReport();
        }

        private void EnsureStatsText()
        {
            if (!endScreen || _statsText)
                return;

            var go = new GameObject("Match Stats", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(endScreen.transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 235f);
            rect.sizeDelta = new Vector2(1120f, 320f);

            _statsText = go.GetComponent<TextMeshProUGUI>();
            _statsText.font = TMP_Settings.defaultFontAsset;
            _statsText.fontSize = 21f;
            _statsText.alignment = TextAlignmentOptions.Top;
            _statsText.color = Color.white;
            _statsText.raycastTarget = false;
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
