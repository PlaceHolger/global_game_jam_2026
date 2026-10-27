using UnityEngine;
using UnityEngine.InputSystem;

namespace Warmask.Planet
{
    /// <summary>
    /// One scaled-time control for the whole match: pause and 0.5x..2x speed.
    /// Lives in GameCore so every map gets it and the menu never does.
    /// </summary>
    public class MatchClock : MonoBehaviour
    {
        private static readonly float[] Speeds = { 0.5f, 1f, 1.5f, 2f };

        private const float StripWidth = 220f;
        private const float StripHeight = 38f;
        private const float StripBottomMargin = 8f;

        private static MatchClock _instance;

        private int _speedIndex = 1;
        private bool _paused;

        public bool Paused => _paused;
        public float Speed => Speeds[_speedIndex];

        private void Awake()
        {
            _instance = this;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.pKey.wasPressedThisFrame)
                TogglePause();
            if (keyboard.leftBracketKey.wasPressedThisFrame)
                StepSpeed(-1);
            if (keyboard.rightBracketKey.wasPressedThisFrame)
                StepSpeed(1);
        }

        public void TogglePause()
        {
            _paused = !_paused;
            Apply();
        }

        public void StepSpeed(int direction)
        {
            _speedIndex = Mathf.Clamp(_speedIndex + direction, 0, Speeds.Length - 1);
            Apply();
        }

        private void Apply()
        {
            Time.timeScale = _paused ? 0f : Speed;
        }

        private void OnDisable()
        {
            if (_instance == this)
                _instance = null;

            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            Time.timeScale = 1f;
        }

        /// <summary>True when the pointer is over the clock strip, in Input System screen space.</summary>
        public static bool BlocksPointer(Vector2 screenPosition)
        {
            if (!_instance)
                return false;

            Rect rect = _instance.ControlRect;
            return rect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        private Rect ControlRect => new Rect(
            (Screen.width - StripWidth) * 0.5f,
            Screen.height - StripHeight - StripBottomMargin,
            StripWidth,
            StripHeight);

        private void OnGUI()
        {
            Rect rect = ControlRect;
            GUI.Box(rect, GUIContent.none);

            GUILayout.BeginArea(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f));
            GUILayout.BeginHorizontal();

            if (GUILayout.Button(_paused ? ">" : "II", GUILayout.Width(38f)))
                TogglePause();

            if (GUILayout.Button("-", GUILayout.Width(30f)))
                StepSpeed(-1);

            GUILayout.Label(_paused ? "PAUSED" : Speed.ToString("0.#") + "x", GUILayout.Width(82f));

            if (GUILayout.Button("+", GUILayout.Width(30f)))
                StepSpeed(1);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
