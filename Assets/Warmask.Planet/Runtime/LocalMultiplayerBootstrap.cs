using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Warmask.Planet
{
    /// <summary>
    /// Turns the setup-screen human slots into input sources at match start.
    /// The scene already owns P1's selection manager; extra humans get a clone.
    /// </summary>
    public class LocalMultiplayerBootstrap : MonoBehaviour
    {
        private PlanetSelectionManager _p1;
        private MouseLineHelper _p1Line;
        private readonly List<GameObject> _created = new();
        private float _previousCooldown;
        private bool _configured;

        private void Awake()
        {
            _p1 = GetComponent<PlanetSelectionManager>();
            _p1Line = _p1 ? _p1.GetComponentInChildren<MouseLineHelper>() : null;
        }

        private void Start()
        {
            MatchSlot[] slots = MatchConfig.CloneCurrent();
            int humans = 0;
            int mouseSlot = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsHuman)
                    continue;

                humans++;
                if (slots[i].input == MatchInput.MouseKeyboard)
                    mouseSlot = i;
            }

            _previousCooldown = Globals.Instance.maskSwitchCooldown;
            Globals.Instance.localMultiplayer = humans > 1;
            if (humans > 1)
                Globals.Instance.maskSwitchCooldown = Globals.Instance.localMultiplayerMaskCooldown;
            _configured = true;

            bool p1Handled = false;
            if (_p1)
            {
                if (slots[0].IsHuman)
                {
                    ConfigureManager(_p1, _p1Line, 0, slots[0].input, _p1.gameObject);
                    p1Handled = true;
                }
                else if (mouseSlot > 0)
                {
                    // The OS mouse is free, so P1's manager becomes that human's cursor.
                    _p1.Initialize((Globals.ePlayer)(mouseSlot + 1), null);
                    if (_p1Line)
                        _p1Line.SetPointer(null);
                    p1Handled = true;
                }
                else
                {
                    _p1.enabled = false;
                }
            }

            for (int i = 1; i < slots.Length; i++)
            {
                if (!slots[i].IsHuman)
                    continue;
                if (i == mouseSlot && p1Handled)
                    continue;
                CreateHuman(i, slots[i].input);
            }
        }

        private void ConfigureManager(PlanetSelectionManager manager, MouseLineHelper line, int slot, MatchInput input, GameObject host)
        {
            IPointerSource pointer = input == MatchInput.MouseKeyboard ? null : CreatePointer(input, host);
            manager.Initialize((Globals.ePlayer)(slot + 1), pointer);
            if (line)
                line.SetPointer(pointer);
            manager.enabled = true;
        }

        private void CreateHuman(int slot, MatchInput input, IPointerSource pointerOverride = null)
        {
            var root = new GameObject("P" + (slot + 1) + " Selection");
            root.SetActive(false);
            root.transform.SetParent(transform, false);

            var lineObject = new GameObject("P" + (slot + 1) + " Line");
            lineObject.transform.SetParent(root.transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            CopyLine(_p1Line ? _p1Line.lineRenderer : null, line);
            var lineHelper = lineObject.AddComponent<MouseLineHelper>();
            lineHelper.lineRenderer = line;

            IPointerSource pointer = pointerOverride != null ? pointerOverride : CreatePointer(input, root);
            lineHelper.SetPointer(pointer);

            var manager = root.AddComponent<PlanetSelectionManager>();
            manager.Initialize((Globals.ePlayer)(slot + 1), pointer);

            root.SetActive(true);
            _created.Add(root);
            Debug.Log($"[Match] Player {slot + 1}: {(pointerOverride != null ? "Raw Mouse" : MatchConfig.InputName(input))}");
        }

        /// <summary>Debug-spike hook: hand P1 a raw mouse and add a second raw-mouse human.</summary>
        public bool EnableDualMouse(IPointerSource p1Pointer, IPointerSource p2Pointer)
        {
            if (!_p1 || p1Pointer == null || p2Pointer == null || Globals.Instance.localMultiplayer)
                return false;

            _p1.Initialize(Globals.ePlayer.Player1, p1Pointer);
            if (_p1Line)
                _p1Line.SetPointer(p1Pointer);

            Globals.Instance.localMultiplayer = true;
            Globals.Instance.maskSwitchCooldown = Globals.Instance.localMultiplayerMaskCooldown;
            CreateHuman(1, MatchInput.Gamepad2, p2Pointer);
            return true;
        }

        private static IPointerSource CreatePointer(MatchInput input, GameObject host)
        {
            if (input == MatchInput.Keyboard)
                return host.AddComponent<KeyboardCursorPointer>();

            int index = (int)input - (int)MatchInput.Gamepad1;
            Gamepad gamepad = index >= 0 && index < Gamepad.all.Count ? Gamepad.all[index] : null;
            if (gamepad == null)
                Debug.LogWarning($"[Match] {MatchConfig.InputName(input)} is not connected; that player cannot select.");

            var pointer = host.AddComponent<GamepadCursorPointer>();
            pointer.Initialize(gamepad);
            return pointer;
        }

        private void OnDestroy()
        {
            if (!_configured)
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
