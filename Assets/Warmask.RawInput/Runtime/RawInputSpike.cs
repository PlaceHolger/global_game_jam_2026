using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Warmask.Planet;

namespace Warmask.RawInput
{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    /// <summary>
    /// Temporary F9/F10 harness for the Windows two-mouse spike.
    /// </summary>
    public sealed class RawInputSpike : MonoBehaviour
    {
        private readonly List<RawInputMousePointer> _pointers = new List<RawInputMousePointer>();
        private RawInputMouseRouter _router;
        private GameObject _routerObject;
        private bool _active;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLockState;
        private IntPtr _p2DeviceHandle;
        private string _p1Name;
        private string _p2Name;
        private bool _deviceZeroForP2;
        private string _status = "F9: two-mouse spike (needs 2 mice; 1 mouse runs a P1-only check)";
        private GUIStyle _style;

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Keyboard.current.f9Key.wasPressedThisFrame)
                StartSpike();
            if (_active && Keyboard.current.f10Key.wasPressedThisFrame)
                ToggleCursor();
            if (_active && Keyboard.current.f8Key.wasPressedThisFrame)
                ToggleDeviceZero();
        }

        private void StartSpike()
        {
            if (_active)
                return;

            _previousCursorVisible = Cursor.visible;
            _previousCursorLockState = Cursor.lockState;
            _routerObject = new GameObject("Raw Input Router");
            _router = _routerObject.AddComponent<RawInputMouseRouter>();
            IReadOnlyList<RawInputMouseRouter.DeviceInfo> devices = _router.Devices;

            if (devices.Count == 0)
            {
                _status = "No raw mouse devices found.";
                Debug.LogError("[RawInputSpike] No raw mouse devices found.");
                Destroy(_routerObject);
                _router = null;
                return;
            }

            if (devices.Count == 1)
            {
                StartPlayerOneOnly(devices[0]);
                return;
            }

            StartDualMouse(devices[0], devices[1]);
        }

        private void StartPlayerOneOnly(RawInputMouseRouter.DeviceInfo p1Device)
        {
            PlanetSelectionManager p1 = PlanetSelectionManager.Instance;
            if (!p1)
            {
                _status = "P1 selection manager not found.";
                Destroy(_routerObject);
                _router = null;
                return;
            }

            RawInputMousePointer pointer = CreatePointer("P1 Raw Mouse", p1Device, Globals.ePlayer.Player1);
            p1.Initialize(Globals.ePlayer.Player1, pointer);
            MouseLineHelper line = p1.GetComponentInChildren<MouseLineHelper>();
            if (line)
                line.SetPointer(pointer);

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _active = true;
            _status = $"1 mouse: P1 raw-input check ({ShortName(p1Device.Name)}). F10 releases cursor. Plug a second mouse for P2.";
        }

        private void StartDualMouse(RawInputMouseRouter.DeviceInfo p1Device, RawInputMouseRouter.DeviceInfo p2Device)
        {
            LocalMultiplayerBootstrap bootstrap = FindAnyObjectByType<LocalMultiplayerBootstrap>();
            if (!bootstrap)
            {
                _status = "LocalMultiplayerBootstrap not found.";
                Debug.LogError("[RawInputSpike] LocalMultiplayerBootstrap not found.");
                Destroy(_routerObject);
                _router = null;
                return;
            }

            RawInputMousePointer p1Pointer = CreatePointer("P1 Raw Mouse", p1Device, Globals.ePlayer.Player1);
            RawInputMousePointer p2Pointer = CreatePointer("P2 Raw Mouse", p2Device, Globals.ePlayer.Player2);

            if (!bootstrap.EnableDualMouse(p1Pointer, p2Pointer))
            {
                _status = "Two-mouse mode could not start (P2 already joined?).";
                Debug.LogError("[RawInputSpike] EnableDualMouse failed. P2 may already be joined.");
                DestroyPointers();
                Destroy(_routerObject);
                _router = null;
                return;
            }

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _active = true;
            _p1Name = ShortName(p1Device.Name);
            _p2Name = ShortName(p2Device.Name);
            _p2DeviceHandle = p2Device.Handle;
            UpdateDualStatus();
        }

        private RawInputMousePointer CreatePointer(string name, RawInputMouseRouter.DeviceInfo device, Globals.ePlayer player)
        {
            var pointerObject = new GameObject(name);
            var pointer = pointerObject.AddComponent<RawInputMousePointer>();
            pointer.Initialize(_router, device.Handle, player);
            _pointers.Add(pointer);
            return pointer;
        }

        private void ToggleCursor()
        {
            bool release = Cursor.lockState == CursorLockMode.Locked || !Cursor.visible;
            Cursor.visible = release;
            Cursor.lockState = release ? CursorLockMode.None : CursorLockMode.Locked;
            _status = release
                ? "OS cursor released. F10 locks it again."
                : "Raw input active; OS cursor locked. F10 releases it.";
        }

        private void ToggleDeviceZero()
        {
            if (_p2DeviceHandle == IntPtr.Zero)
                return;

            _deviceZeroForP2 = !_deviceZeroForP2;
            _router.SetDeviceZeroAlias(_deviceZeroForP2 ? _p2DeviceHandle : IntPtr.Zero);
            UpdateDualStatus();
        }

        private void UpdateDualStatus()
        {
            _status = $"Two mice active. F10 cursor, F8 device 0 -> P2: {(_deviceZeroForP2 ? "ON" : "OFF")}.\nP1: {_p1Name}\nP2: {_p2Name}";
        }

        private void OnGUI()
        {
            // Nothing until F9 starts the spike; this panel used to sit on the menu's Start button.
            if (!_active)
                return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
            }

            var rect = new Rect(10f, Screen.height - 92f, 520f, 82f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), _status, _style);
        }

        private void DestroyPointers()
        {
            foreach (RawInputMousePointer pointer in _pointers)
            {
                if (pointer)
                    Destroy(pointer.gameObject);
            }
            _pointers.Clear();
        }

        private static string ShortName(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
                return "unknown";
            int slash = devicePath.LastIndexOf('\\');
            return slash >= 0 ? devicePath.Substring(slash + 1) : devicePath;
        }

        private void OnDestroy()
        {
            if (!_active)
                return;

            Cursor.visible = _previousCursorVisible;
            Cursor.lockState = _previousCursorLockState;
        }
    }

#endif
}
