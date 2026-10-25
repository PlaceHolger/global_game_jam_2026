# Two mice on one Windows PC — research note

Date: 2026-10-22
Status: research complete; spike not started. This is the phase-5 note from `docs/upgrade-plan.md` section 4.4.

## Verdict

Unity's Input System cannot see two physical mice as two independent pointers. The official Mouse manual says:

> Note: The Input System does not currently support: Input from multiple mice at the platform level.

`Mouse.current` is one device, and the OS drives one system cursor. Rewired also does not support independent multiple mice (their FAQ says so).

The practical route is **Windows Raw Input**, not a different Unity input package:

1. Register mouse input with `RegisterRawInputDevices` on a hidden window.
2. Read `RAWINPUTHEADER.hDevice` to tell the devices apart.
3. Accumulate relative movement and button events per device in C#.
4. Hide the OS cursor and draw one virtual cursor per mouse.
5. Feed each cursor into the existing `IPointerSource` interface.
6. Forward the raw events back to Unity with `Windows.Input.ForwardRawInput` so Unity's normal keyboard/mouse input keeps working. Windows allows only one raw-input registration per process, so this forwarding step is mandatory after we take over the registration.

Unity 6 ships `UnityEngine.Windows.Input.ForwardRawInput` and a complete C# example that creates a hidden window, handles `WM_INPUT`, and forwards events. That removes the need for a native C++ DLL for the first spike.

## What the spike has to build

### `RawInputMouseRouter` (Windows only)

- P/Invoke: `RegisterClassExW`, `CreateWindowExW`, `RegisterRawInputDevices`, `GetRawInputData`, `GetRawInputBuffer`, `GetRawInputDeviceList`, `GetRawInputDeviceInfoW`, `DefWindowProcW`.
- Register `HID_USAGE_PAGE_GENERIC / HID_USAGE_GENERIC_MOUSE` with `RIDEV_INPUTSINK | RIDEV_DEVNOTIFY` so input still arrives when the game is focused.
- Parse `RAWMOUSE`:
  - `lLastX`, `lLastY` for relative movement;
  - `usButtonFlags` for left/right/middle down/up and wheel;
  - `header.hDevice` as the stable-enough device key.
- Keep the WndProc delegate in a static field so the GC cannot collect it. Use `[MonoPInvokeCallback]` for IL2CPP.
- Forward the same `RAWINPUT` blocks to Unity with `Windows.Input.ForwardRawInput`, using the `IntPtr` overload and `Marshal.AllocHGlobal` (no `unsafe` toggle needed for the forwarding call itself).
- Handle `WM_INPUT_DEVICE_CHANGE`: a mouse can arrive, leave, or be re-enumerated.
- Prefer a message-only window (`HWND_MESSAGE`) or a hidden normal window; the Unity example uses a normal hidden window.

### Device identity

- `GetRawInputDeviceList` gives the connected mouse handles at startup.
- `GetRawInputDeviceInfoW(RIDI_DEVICENAME)` gives a device path, useful for logs and for keeping P1/P2 assignments across a reconnect.
- Do not assume "first handle = player 1". Show a binding screen or assign the first mouse that moves after the join prompt.
- The OS pointer remains a single pointer; ignore its position while two-mouse mode is on.

### `RawInputMousePointer : IPointerSource`

- One instance per physical mouse.
- Holds a screen-space position, initialised at the centre of the screen.
- `ScreenPosition`, `ConfirmPressed` (left button), `CancelPressed` (right button).
- `TryGetWorldPoint` uses the same camera conversion as the gamepad pointer.
- The selection code consumes only `IPointerSource`; no gameplay code sees Raw Input.

### Cursors and UI

- `Cursor.visible = false` while dual-mouse mode is active.
- Two world-space or screen-space cursor sprites, one per `IPointerSource`.
- The game scene can use camera raycasts exactly like the gamepad cursor.
- Menus are the hard part: the current menu expects the OS pointer. Options:
  1. keep the OS pointer for menus and enable dual-mouse only after the match starts;
  2. drive the menu with a virtual cursor and `EventSystem.RaycastAll`;
  3. require gamepad/keyboard for menu navigation in dual-mouse mode.
- Recommendation: option 1 for the spike. Turn dual-mouse on at match start and turn it off on the results screen. This keeps the existing menu untouched and tests the gameplay risk first.
- P1 and P2 both need custom clicks then, because the OS cursor is hidden and moved by both devices. The existing `PlanetInstance.OnPointerClick` path is mouse/EventSystem-only; the P1 `PlanetSelectionManager` needs a pointer-source path too. The interface already supports this.

### Platform and build concerns

- Windows standalone and Windows editor only. No WebGL/macOS/Linux promise.
- **Web build:** browsers merge every mouse into a single pointer, so two physical mice cannot be separated there even with Pointer Lock. Local multiplayer in the browser uses **P1 mouse + P2 gamepad** (Start to join), or P2 keyboard (J, then WASD/arrows + Enter). Two-mouse mode remains a Windows standalone/editor feature.
- IL2CPP: `[MonoPInvokeCallback]`, delegate lifetime, and marshalling need a build test.
- The raw-input window must be created on the main thread; the Unity example does it in `Awake`.
- `Windows.Input.ForwardRawInput` requires Unity 6; the project is on 6000.7, so it is available. Keep a version guard if the project ever goes back to 6000.3.
- DPI scaling: raw deltas are device counts, not screen pixels. Apply a sensitivity value calibrated on the target display; clamp the virtual cursor to the game view, not the desktop.
- Focus: use `RIDEV_INPUTSINK` only while the game should receive input; stop/re-register on focus changes if the OS cursor must work in other windows.
- Steam overlay, Alt-Tab, remote desktop, and some gaming-mouse drivers can change or merge devices; test on the actual hardware.
- Anti-cheat is not a concern for a local jam game, but raw input bypassing the OS pointer may look unusual to overlays.

## Alternatives considered

| Option | Result |
|---|---|
| Unity Input System `Mouse.current` | One OS mouse only; official limitation. |
| Rewired | One mouse; their FAQ explicitly rules out independent multiple mice. |
| InControl | No independent physical mice. |
| `UnityRawInput` (elringus/zilberkoenig) | Hooks raw keyboard/buttons, but does not expose per-device mouse movement. Useful reference, not enough. |
| Native C++ Raw Input DLL | Works, but the Unity 6 C# redirector example makes a DLL unnecessary for the first spike. |
| DirectInput | Can enumerate devices, but Windows mouse behavior is inconsistent and Raw Input is the documented path for per-device mouse data. |
| Two OS cursors | Windows has one system cursor; not possible without a virtual cursor layer. |

## Spike plan (time-boxed)

1. Add a Windows-only `RawInputMouseRouter` and a console/log view that prints device handle, delta, and buttons for two mice. Run it in the editor.
2. Verify that two USB mice produce distinct handles and that moving one does not move the other's accumulated position. Verify that keyboard/mouse still work through `ForwardRawInput`.
3. Add `RawInputMousePointer` and two cursor sprites. Bind mouse A to P1 and mouse B to P2.
4. Enable dual-mouse only during a match; leave the menu on the OS pointer.
5. Play one full local match with P1 mouse + P2 mouse. Check selection, line colours, mask switching (keyboard/gamepad), pause, and disconnect/reconnect.
6. Build a Windows IL2CPP player and repeat step 2 and 5.
7. Write the verdict here: ship, keep as a debug option, or drop.

## Acceptance for the spike

- Two physical mice move two independent cursors.
- P1 clicks only select P1 planets; P2 only P2 planets.
- Keyboard and the normal OS pointer still work after forwarding.
- No gameplay assembly references Windows Raw Input directly; only `IPointerSource` implementations do.
- A Windows build starts with one mouse, and dual-mouse mode can be disabled without changing selection logic.

## Decision

Ship gamepad local play first. Two-mouse is a Windows-only spike behind `IPointerSource`; do not block local multiplayer or online work on it.
