using UnityEngine;
using UnityEngine.InputSystem;

namespace Warmask.Planet
{
    public class LocalMultiplayerBootstrap : MonoBehaviour
    {
        private PlanetSelectionManager _p1;
        private MouseLineHelper _p1Line;
        private bool _joined;
        private float _previousCooldown;
        private float _joinedAt;
        private string _controlHint;
        private GUIStyle _hudStyle;

        private void Awake()
        {
            _p1 = GetComponent<PlanetSelectionManager>();
            _p1Line = _p1 ? _p1.GetComponentInChildren<MouseLineHelper>() : null;
            Globals.Instance.localMultiplayer = false;
            Debug.Log("[LocalMP] Ready. Press gamepad Start or J to join P2.");
        }

        private void Update()
        {
            // shortcut: four-player matches are 1 human + 3 AI until the join loop lands (M2).
            // Joining P2 today would flip localMultiplayer and stop every AI.
            if (Globals.Instance.playerCount >= 4)
                return;

            if (_joined || Globals.Instance.localMultiplayer)
                return;

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null && gamepad.startButton.wasPressedThisFrame)
            {
                Join(gamepad);
                return;
            }

            if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
                Join(null);
        }

        public bool EnableDualMouse(IPointerSource p1Pointer, IPointerSource p2Pointer)
        {
            if (_joined || Globals.Instance.localMultiplayer || !_p1 || p1Pointer == null || p2Pointer == null)
                return false;

            _p1.Initialize(Globals.ePlayer.Player1, p1Pointer);
            if (_p1Line)
                _p1Line.SetPointer(p1Pointer);

            Join(null, p2Pointer, "left mouse select, right mouse all, empty space cancel");
            return _joined;
        }

        private void Join(Gamepad gamepad, IPointerSource pointerOverride = null, string hintOverride = null)
        {
            if (_joined)
                return;

            _joined = true;
            _joinedAt = Time.unscaledTime;
            _previousCooldown = Globals.Instance.maskSwitchCooldown;

            Globals.Instance.localMultiplayer = true;
            Globals.Instance.maskSwitchCooldown = Globals.Instance.localMultiplayerMaskCooldown;

            var p2Root = new GameObject("P2 Selection");
            p2Root.SetActive(false);
            p2Root.transform.SetParent(transform, false);

            var lineObject = new GameObject("P2 Line");
            lineObject.transform.SetParent(p2Root.transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            CopyLine(_p1Line ? _p1Line.lineRenderer : null, line);
            var lineHelper = lineObject.AddComponent<MouseLineHelper>();
            lineHelper.lineRenderer = line;

            IPointerSource pointer = pointerOverride;
            string controlHint = hintOverride;
            if (pointer == null)
            {
                if (gamepad != null)
                {
                    var gamepadPointer = p2Root.AddComponent<GamepadCursorPointer>();
                    gamepadPointer.Initialize(gamepad);
                    pointer = gamepadPointer;
                    controlHint = "left stick move, A send, LT+A all, B cancel, LB/RB mask";
                }
                else
                {
                    pointer = p2Root.AddComponent<KeyboardCursorPointer>();
                    controlHint = "WASD/arrows move, Enter send, Ctrl+Enter all, Esc cancel, 1/2/Space mask";
                }
            }

            _controlHint = controlHint;
            var manager = p2Root.AddComponent<PlanetSelectionManager>();
            manager.Initialize(Globals.ePlayer.Player2, pointer);
            lineHelper.SetPointer(pointer);

            p2Root.SetActive(true);
            Debug.Log($"[LocalMP] Player 2 joined. {_controlHint}");
        }

        private void OnGUI()
        {
            if (Globals.Instance.playerCount >= 4)
                return;

            if (!_joined)
            {
                if (GUI.Button(new Rect(Screen.width * 0.5f - 180f, 10f, 360f, 32f),
                        "P2: press Start / J (or click here)"))
                    Join(null);
                return;
            }

            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }

            string cooldown = Globals.Instance.maskCooldownRemaining > 0f
                ? $"{Globals.Instance.maskCooldownRemaining:0.0}s"
                : "READY";
            GUI.Label(new Rect(Screen.width * 0.5f - 150f, 10f, 300f, 30f),
                $"MASK {Globals.Instance.currentMask}  {cooldown}", _hudStyle);

            if (Time.unscaledTime - _joinedAt < 10f)
            {
                _hudStyle.alignment = TextAnchor.MiddleLeft;
                GUI.Label(new Rect(20f, Screen.height - 42f, 700f, 30f), $"P2: {_controlHint}", _hudStyle);
                _hudStyle.alignment = TextAnchor.MiddleCenter;
            }
        }

        private void OnDestroy()
        {
            if (!_joined)
                return;

            Globals.Instance.localMultiplayer = false;
            Globals.Instance.maskSwitchCooldown = _previousCooldown;
            Globals.Instance.maskCooldownRemaining = 0f;
        }

        private static void CopyLine(LineRenderer source, LineRenderer target)
        {
            target.positionCount = 2;
            target.useWorldSpace = true;
            target.enabled = false;
            if (!source)
                return;

            target.sharedMaterial = source.sharedMaterial;
            target.widthMultiplier = source.widthMultiplier;
            target.widthCurve = source.widthCurve;
            target.numCapVertices = source.numCapVertices;
            target.numCornerVertices = source.numCornerVertices;
            target.alignment = source.alignment;
            target.textureMode = source.textureMode;
            target.sortingOrder = source.sortingOrder;
        }
    }
}
