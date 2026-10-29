using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Audio;

namespace Warmask.Planet
{
    /// <summary>
    /// One scaled-time control for the whole match: 0 (pause) and 0.5x..2x speed.
    /// Lives in GameCore so every map gets it and the menu never does.
    /// Also hosts the in-game options menu (hamburger) with speed and volume controls.
    /// </summary>
    public class MatchClock : MonoBehaviour
    {
        private static readonly float[] Speeds = { 0f, 0.5f, 0.75f, 1f, 1.5f, 2f };
        private const int DefaultSpeedIndex = 3;

        private const float StripWidth = 48f;
        private const float StripHeight = 32f;
        private const float StripBottomMargin = 8f;

        private static MatchClock _instance;
        private int _speedIndex = DefaultSpeedIndex;
        private int _lastSpeedIndex = DefaultSpeedIndex;

        private bool _menuOpen;
        private int _savedSpeedIndex = -1;
        private float _musicVolume = 0.5f;
        private float _sfxVolume = 1f;
        private LocalMultiplayerBootstrap _bootstrap;
        private AudioSource _musicSource;

        public bool Paused => Speed == 0f;
        public float Speed => Speeds[_speedIndex];
        public bool MenuOpen => _menuOpen;
        public float MusicVolume => _musicVolume;
        public float SfxVolume => _sfxVolume;

        private void Awake()
        {
            _instance = this;
            _bootstrap = FindAnyObjectByType<LocalMultiplayerBootstrap>();
            ApplyVolumes();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.pKey.wasPressedThisFrame)
                    TogglePause();
                if (keyboard.leftBracketKey.wasPressedThisFrame)
                    StepSpeed(-1);
                if (keyboard.rightBracketKey.wasPressedThisFrame)
                    StepSpeed(1);
                if (keyboard.escapeKey.wasPressedThisFrame && _menuOpen)
                    CloseMenu();
            }

            ApplyVolumes();
        }

        public void TogglePause()
        {
            if (Paused)
                _speedIndex = _lastSpeedIndex;
            else
            {
                _lastSpeedIndex = _speedIndex;
                _speedIndex = 0;
            }

            Apply();
        }

        public void StepSpeed(int direction)
        {
            _speedIndex = Mathf.Clamp(_speedIndex + direction, 0, Speeds.Length - 1);
            if (!Paused)
                _lastSpeedIndex = _speedIndex;

            Apply();
        }

        public void SetSpeedIndex(int index)
        {
            _speedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1);
            if (!Paused)
                _lastSpeedIndex = _speedIndex;
            if (_menuOpen)
                _savedSpeedIndex = -1;
            Apply();
        }

        public void ToggleMenu()
        {
            if (_menuOpen)
                CloseMenu();
            else
                OpenMenu();
        }

        public void OpenMenu()
        {
            if (_menuOpen)
                return;

            if (!Paused)
            {
                _savedSpeedIndex = _speedIndex;
                _speedIndex = 0;
                Apply();
            }
            _menuOpen = true;
        }

        public void CloseMenu()
        {
            if (!_menuOpen)
                return;

            _menuOpen = false;
            if (_savedSpeedIndex > 0)
            {
                _speedIndex = _savedSpeedIndex;
                _lastSpeedIndex = _savedSpeedIndex;
                _savedSpeedIndex = -1;
                Apply();
            }
        }

        public void SetMusicVolume(float v)
        {
            _musicVolume = Mathf.Clamp01(v);
            ApplyVolumes();
        }

        public void SetSfxVolume(float v)
        {
            _sfxVolume = Mathf.Clamp01(v);
            ApplyVolumes();
        }

        private void Apply()
        {
            Time.timeScale = Speed;
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = _sfxVolume;

            if (_musicSource == null)
            {
                foreach (var src in FindObjectsByType<AudioSource>())
                {
                    if (src != null && src.loop)
                    {
                        _musicSource = src;
                        break;
                    }
                }
            }

            if (_musicSource != null)
                _musicSource.volume = _musicVolume;
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
            if (rect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y)))
                return true;

            // Also block while the options menu is open.
            if (_instance._menuOpen)
            {
                Rect menuRect = new Rect(
                    (Screen.width - 320f) * 0.5f,
                    (Screen.height - 260f) * 0.5f,
                    320f, 260f);
                if (menuRect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y)))
                    return true;
            }

            return false;
        }

        private Rect ControlRect => new Rect(
            (Screen.width - StripWidth) * 0.5f,
            Screen.height - StripHeight - StripBottomMargin,
            StripWidth,
            StripHeight);

        private void OnGUI()
        {
            Rect rect = ControlRect;
            if (GUI.Button(rect, "☰"))
                ToggleMenu();

            if (!_menuOpen)
                return;

            Rect menuRect = new Rect(
                (Screen.width - 320f) * 0.5f,
                (Screen.height - 280f) * 0.5f,
                320f, 280f);
            GUI.Box(menuRect, "Options");

            GUILayout.BeginArea(new Rect(menuRect.x + 10f, menuRect.y + 30f, menuRect.width - 20f, menuRect.height - 40f));
            GUILayout.Label("Speed");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Speeds.Length; i++)
            {
                if (GUILayout.Button(Speeds[i] == 0f ? "Pause" : Speeds[i].ToString("0.##") + "x"))
                    SetSpeedIndex(i);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label($"Music Volume: {_musicVolume:0.00}");
            _musicVolume = GUILayout.HorizontalSlider(_musicVolume, 0f, 1f);
            SetMusicVolume(_musicVolume);

            GUILayout.Space(4f);
            GUILayout.Label($"Sound Volume: {_sfxVolume:0.00}");
            _sfxVolume = GUILayout.HorizontalSlider(_sfxVolume, 0f, 1f);
            SetSfxVolume(_sfxVolume);

            GUILayout.Space(8f);
            GUILayout.Label("Players");
            if (_bootstrap == null)
                _bootstrap = FindAnyObjectByType<LocalMultiplayerBootstrap>();

            if (MatchConfig.IsConfigured)
            {
                MatchSlot[] slots = MatchConfig.Current;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].mode == MatchSlotMode.Off)
                        continue;
                    bool human = MatchConfig.IsHuman((Globals.ePlayer)(i + 1));
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"P{i + 1}: {(human ? "Human" : "AI")}");
                    if (human)
                    {
                        if (i > 0 && GUILayout.Button("Leave"))
                            _bootstrap?.LeaveSlot(i);
                    }
                    else
                    {
                        if (GUILayout.Button("Join"))
                            _bootstrap?.JoinSlot(i, MatchInput.Keyboard);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("Exit to Menu"))
            {
                Time.timeScale = 1f;
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("Close"))
                CloseMenu();

            GUILayout.EndArea();
        }
    }
}
