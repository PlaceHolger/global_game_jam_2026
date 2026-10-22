using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace Warmask.RawInput
{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    /// <summary>
    /// One hidden window that owns Windows raw mouse input, parses it per device,
    /// and forwards the original events back to Unity.
    /// </summary>
    public sealed class RawInputMouseRouter : MonoBehaviour
    {
        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_GENERIC_MOUSE = 0x02;
        private const uint WM_INPUT = 0x00FF;
        private const uint WM_INPUT_DEVICE_CHANGE = 0x00FE;
        private const uint RID_INPUT = 0x10000003;
        private const uint RIDI_DEVICENAME = 0x20000007;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RIDEV_DEVNOTIFY = 0x00002000;
        private const int RIM_TYPEMOUSE = 0;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int MOUSE_MOVE_ABSOLUTE = 0x01;
        private const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
        private const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;

        public readonly struct DeviceInfo
        {
            public readonly IntPtr Handle;
            public readonly string Name;

            public DeviceInfo(IntPtr handle, string name)
            {
                Handle = handle;
                Name = name;
            }
        }

        public static RawInputMouseRouter Instance { get; private set; }

        public IReadOnlyList<DeviceInfo> Devices => _devices;

        private static readonly int s_HeaderSize = Marshal.SizeOf<RAWINPUTHEADER>();
        private static readonly int s_DeviceSize = Marshal.SizeOf<RAWINPUTDEVICE>();
        private static readonly int s_DeviceListSize = Marshal.SizeOf<RAWINPUTDEVICELIST>();
        private static readonly WndProcDelegate s_WndProc = WndProc;

        private readonly List<DeviceInfo> _devices = new List<DeviceInfo>();
        private readonly Dictionary<IntPtr, MouseState> _states = new Dictionary<IntPtr, MouseState>();

        private IntPtr _windowClass;
        private IntPtr _hwnd;
        private IntPtr _rawBuffer;
        private IntPtr _headerIndices;
        private IntPtr _dataIndices;
        private int _rawBufferSize = 8192;
        private RAWINPUTDEVICE[] _registeredDevices = new RAWINPUTDEVICE[1];
        private IntPtr _deviceZeroAlias;

        private sealed class MouseState
        {
            public int DeltaX;
            public int DeltaY;
            public bool LeftPressed;
            public bool RightPressed;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _rawBuffer = Marshal.AllocHGlobal(_rawBufferSize);
            _headerIndices = Marshal.AllocHGlobal(sizeof(int));
            _dataIndices = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(_headerIndices, 0, 0);
            Marshal.WriteInt32(_dataIndices, 0, s_HeaderSize);

            _windowClass = RegisterWindowClass();
            if (_windowClass == IntPtr.Zero)
                return;

            _hwnd = CreateWindowExW(0, _windowClass, "Warmask Raw Input", 0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                Debug.LogError($"[RawInput] Failed to create window: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                return;
            }

            RegisterMouse();
            RefreshDevices("startup");
        }

        private void Update()
        {
            // Unity re-registers raw input on focus changes. Take the mouse
            // registration back if that happened.
            if (_hwnd == IntPtr.Zero)
                return;

            uint count = 0;
            GetRegisteredRawInputDevices(null, ref count, (uint)s_DeviceSize);
            if (count == 0)
                return;

            if (_registeredDevices.Length < count)
                _registeredDevices = new RAWINPUTDEVICE[count];

            if (GetRegisteredRawInputDevices(_registeredDevices, ref count, (uint)s_DeviceSize) == uint.MaxValue)
                return;

            for (int i = 0; i < count; i++)
            {
                if (_registeredDevices[i].usUsagePage == HID_USAGE_PAGE_GENERIC &&
                    _registeredDevices[i].usUsage == HID_USAGE_GENERIC_MOUSE &&
                    _registeredDevices[i].hwndTarget != _hwnd)
                {
                    RegisterMouse();
                    break;
                }
            }
        }

        public void SetDeviceZeroAlias(IntPtr device)
        {
            _deviceZeroAlias = device;
        }

        public bool ConsumeDelta(IntPtr device, out int deltaX, out int deltaY)
        {
            if (!_states.TryGetValue(device, out MouseState state))
            {
                deltaX = 0;
                deltaY = 0;
                return false;
            }

            deltaX = state.DeltaX;
            deltaY = state.DeltaY;
            state.DeltaX = 0;
            state.DeltaY = 0;
            return deltaX != 0 || deltaY != 0;
        }

        public bool WasLeftPressed(IntPtr device)
        {
            if (!_states.TryGetValue(device, out MouseState state) || !state.LeftPressed)
                return false;

            state.LeftPressed = false;
            return true;
        }

        public bool WasRightPressed(IntPtr device)
        {
            if (!_states.TryGetValue(device, out MouseState state) || !state.RightPressed)
                return false;

            state.RightPressed = false;
            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_hwnd != IntPtr.Zero)
                DestroyWindow(_hwnd);
            if (_windowClass != IntPtr.Zero)
                UnregisterClassW(_windowClass, GetModuleHandleW(null));

            if (_rawBuffer != IntPtr.Zero)
                Marshal.FreeHGlobal(_rawBuffer);
            if (_headerIndices != IntPtr.Zero)
                Marshal.FreeHGlobal(_headerIndices);
            if (_dataIndices != IntPtr.Zero)
                Marshal.FreeHGlobal(_dataIndices);
        }

        private void RegisterMouse()
        {
            _registeredDevices[0] = new RAWINPUTDEVICE
            {
                usUsagePage = HID_USAGE_PAGE_GENERIC,
                usUsage = HID_USAGE_GENERIC_MOUSE,
                dwFlags = RIDEV_INPUTSINK | RIDEV_DEVNOTIFY,
                hwndTarget = _hwnd
            };

            if (!RegisterRawInputDevices(_registeredDevices, 1, (uint)s_DeviceSize))
            {
                Debug.LogError($"[RawInput] Failed to register mouse: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
            }
        }

        private void ProcessRawInput(IntPtr lParam)
        {
            int size = _rawBufferSize;
            int result = GetRawInputData(lParam, RID_INPUT, _rawBuffer, ref size, s_HeaderSize);
            if (result == -1)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != ERROR_INSUFFICIENT_BUFFER)
                {
                    Debug.LogError($"[RawInput] GetRawInputData failed: {new Win32Exception(error).Message}");
                    return;
                }

                Marshal.FreeHGlobal(_rawBuffer);
                _rawBufferSize = size;
                _rawBuffer = Marshal.AllocHGlobal(_rawBufferSize);
                size = _rawBufferSize;
                result = GetRawInputData(lParam, RID_INPUT, _rawBuffer, ref size, s_HeaderSize);
                if (result == -1)
                    return;
            }

            RAWINPUTHEADER header = Marshal.PtrToStructure<RAWINPUTHEADER>(_rawBuffer);
            if (header.dwType == RIM_TYPEMOUSE)
            {
                RAWMOUSE mouse = Marshal.PtrToStructure<RAWMOUSE>(IntPtr.Add(_rawBuffer, s_HeaderSize));
                ApplyMouse(header.hDevice, mouse);
            }

            UnityEngine.Windows.Input.ForwardRawInput(
                _headerIndices, _dataIndices, 1, _rawBuffer, (uint)size);
        }

        private void ApplyMouse(IntPtr device, RAWMOUSE mouse)
        {
            // Precision touchpads/injected input can report device 0. Use the
            // explicit alias if set, otherwise the only device when unambiguous.
            if (device == IntPtr.Zero)
            {
                if (_deviceZeroAlias != IntPtr.Zero)
                    device = _deviceZeroAlias;
                else if (_devices.Count == 1)
                    device = _devices[0].Handle;
            }

            if (!_states.TryGetValue(device, out MouseState state))
            {
                state = new MouseState();
                _states.Add(device, state);
                Debug.Log($"[RawInput] First input from handle={device} name={DeviceName(device)}");
            }

            if ((mouse.usFlags & MOUSE_MOVE_ABSOLUTE) == 0)
            {
                state.DeltaX += mouse.lLastX;
                state.DeltaY += mouse.lLastY;
            }

            ushort buttons = (ushort)(mouse.ulButtons & 0xFFFF);
            if ((buttons & RI_MOUSE_LEFT_BUTTON_DOWN) != 0)
            {
                state.LeftPressed = true;
                Debug.Log($"[RawInput] left down handle={device}");
            }
            if ((buttons & RI_MOUSE_RIGHT_BUTTON_DOWN) != 0)
                state.RightPressed = true;
        }

        private void RefreshDevices(string reason)
        {
            uint count = 0;
            uint result = GetRawInputDeviceList(null, ref count, (uint)s_DeviceListSize);
            if (result == uint.MaxValue || count == 0)
                return;

            var devices = new RAWINPUTDEVICELIST[(int)count];
            result = GetRawInputDeviceList(devices, ref count, (uint)s_DeviceListSize);
            if (result == uint.MaxValue)
                return;

            _devices.Clear();
            for (int i = 0; i < count; i++)
            {
                if (devices[i].dwType != RIM_TYPEMOUSE)
                    continue;

                _devices.Add(new DeviceInfo(devices[i].hDevice, DeviceName(devices[i].hDevice)));
            }

            string names = string.Join(" | ", _devices.ConvertAll(device => $"handle={device.Handle} name={device.Name}"));
            Debug.Log($"[RawInput] {_devices.Count} mouse device(s) ({reason}): {names}");
        }

        private static string DeviceName(IntPtr device)
        {
            uint size = 0;
            if (GetRawInputDeviceInfoW(device, RIDI_DEVICENAME, IntPtr.Zero, ref size) == uint.MaxValue || size == 0)
                return "unknown";

            IntPtr buffer = Marshal.AllocHGlobal((int)size * 2);
            try
            {
                if (GetRawInputDeviceInfoW(device, RIDI_DEVICENAME, buffer, ref size) == uint.MaxValue)
                    return "unknown";
                return Marshal.PtrToStringUni(buffer) ?? "unknown";
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static IntPtr RegisterWindowClass()
        {
            IntPtr instance = GetModuleHandleW(null);
            var windowClass = new WNDCLASSEXW
            {
                cbSize = Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_WndProc),
                hInstance = instance,
                lpszClassName = "WarmaskRawInput"
            };

            ushort atom = RegisterClassExW(ref windowClass);
            if (atom == 0)
            {
                Debug.LogError($"[RawInput] Failed to register window class: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                return IntPtr.Zero;
            }

            return new IntPtr(atom);
        }

        [MonoPInvokeCallback(typeof(WndProcDelegate))]
        private static IntPtr WndProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (message == WM_INPUT)
                    Instance?.ProcessRawInput(lParam);
                else if (message == WM_INPUT_DEVICE_CHANGE)
                    Instance?.RefreshDevices("device change");

                return DefWindowProcW(window, message, wParam, lParam);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return IntPtr.Zero;
            }
        }

        private delegate IntPtr WndProcDelegate(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct WNDCLASSEXW
        {
            public int cbSize;
            public int style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICELIST
        {
            public IntPtr hDevice;
            public uint dwType;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public int dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE
        {
            public ushort usFlags;
            public ushort padding;
            public uint ulButtons;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string moduleName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U2)]
        private static extern ushort RegisterClassExW([In] ref WNDCLASSEXW windowClass);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterClassW(IntPtr windowClass, IntPtr instance);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowExW(
            uint exStyle, IntPtr windowClass, [MarshalAs(UnmanagedType.LPWStr)] string windowName,
            uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu,
            IntPtr instance, IntPtr param);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            [In][MarshalAs(UnmanagedType.LPArray)] RAWINPUTDEVICE[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRegisteredRawInputDevices(
            [In][Out][MarshalAs(UnmanagedType.LPArray)] RAWINPUTDEVICE[] devices, ref uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceList(
            [In][Out][MarshalAs(UnmanagedType.LPArray)] RAWINPUTDEVICELIST[] devices, ref uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceInfoW(
            IntPtr device, uint command, IntPtr data, ref uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetRawInputData(
            IntPtr rawInput, uint command, IntPtr data, ref int size, int headerSize);
#endif
    }
}
