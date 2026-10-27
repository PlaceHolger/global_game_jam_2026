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
        [SerializeField, Tooltip("Menu slider for the enemy count (1-3).")]
        private Slider enemySlider;

        [SerializeField, Tooltip("Label that shows the current enemy count.")]
        private TMP_Text enemyLabel;

        private void Start()
        {
            // The menu reloads from disk with its saved toggles, so forget the last match's choices.
            GameSession.MapScene = "LargeMap";
            Globals.Instance.playerCount = 2;

            if (!enemySlider)
                return;

            enemySlider.minValue = 1f;
            enemySlider.maxValue = 3f;
            enemySlider.wholeNumbers = true;
            enemySlider.onValueChanged.AddListener(SetEnemyCount);
            SetEnemyCount(enemySlider.value);
        }

        public void SetLargeMap(bool isLarge) => GameSession.MapScene = isLarge ? "LargeMap" : "SampleScene";

        /// <summary>Menu slider entry point: 1..3 AI opponents means 2..4 players.</summary>
        public void SetEnemyCount(float enemies)
        {
            int enemyCount = Mathf.Clamp(Mathf.RoundToInt(enemies), 1, 3);

            if (!Globals.Instance)
                return;

            Globals.Instance.playerCount = enemyCount + 1;

            if (enemySlider && !Mathf.Approximately(enemySlider.value, enemyCount))
                enemySlider.SetValueWithoutNotify(enemyCount);

            if (enemyLabel)
                enemyLabel.text = "Enemies: " + enemyCount;
        }
    }
}
