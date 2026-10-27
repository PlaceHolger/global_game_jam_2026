using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Warmask.Scenes
{
    /// <summary>
    /// Menu hooks for the next match: which map loads and how many players sit at the table.
    /// </summary>
    public class MapMenuSettings : MonoBehaviour
    {
        [SerializeField, Tooltip("Builds the enemies slider here; only the 4 Players toggle sets this.")]
        private bool enemySliderHost;

        private Slider _enemySlider;
        private TMP_Text _enemyLabel;
        private Toggle _fourPlayersToggle;

        private void Start()
        {
            // The menu reloads from disk with its saved toggles, so forget the last match's choices.
            GameSession.MapScene = "LargeMap";
            Globals.Instance.playerCount = 2;

            if (!enemySliderHost)
                return;

            _fourPlayersToggle = GetComponent<Toggle>();
            BuildEnemySlider();
        }

        public void SetLargeMap(bool isLarge) => GameSession.MapScene = isLarge ? "LargeMap" : "SampleScene";

        public void SetFourPlayers(bool isFour) => SetEnemyCount(isFour ? 3 : 1);

        /// <summary>Menu slider entry point: 1..3 AI opponents means 2..4 players.</summary>
        public void SetEnemyCount(float enemies)
        {
            int enemyCount = Mathf.Clamp(Mathf.RoundToInt(enemies), 1, 3);

            if (!Globals.Instance)
                return;

            Globals.Instance.playerCount = enemyCount + 1;

            if (_enemySlider && !Mathf.Approximately(_enemySlider.value, enemyCount))
                _enemySlider.SetValueWithoutNotify(enemyCount);

            if (_enemyLabel)
                _enemyLabel.text = "Enemies: " + enemyCount;

            // Keep the older 4 Players shortcut showing the same state as the slider.
            if (_fourPlayersToggle)
                _fourPlayersToggle.SetIsOnWithoutNotify(enemyCount == 3);
        }

        private void BuildEnemySlider()
        {
            if (_enemySlider)
                return;

            GameObject sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = "Enemies Slider";
            sliderObject.transform.SetParent(transform.parent, false);

            var rect = (RectTransform)sliderObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(19f, 104f);
            rect.sizeDelta = new Vector2(160f, 20f);

            _enemySlider = sliderObject.GetComponent<Slider>();
            _enemySlider.minValue = 1f;
            _enemySlider.maxValue = 3f;
            _enemySlider.wholeNumbers = true;
            _enemySlider.value = 1f;
            _enemySlider.onValueChanged.AddListener(SetEnemyCount);

            // Reuse the toggle's label so the slider matches the menu style.
            TMP_Text template = GetComponentInChildren<TMP_Text>();
            if (template)
            {
                _enemyLabel = Instantiate(template, sliderObject.transform);
                RectTransform labelRect = _enemyLabel.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0f);
                labelRect.pivot = new Vector2(0f, 0f);
                labelRect.anchoredPosition = new Vector2(0f, 22f);
                labelRect.sizeDelta = new Vector2(160f, 20f);
            }

            SetEnemyCount(1f);
        }
    }
}
