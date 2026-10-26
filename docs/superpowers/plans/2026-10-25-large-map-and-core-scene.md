# Large Map and Shared Core Scene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the game into a reusable additive core scene plus map-only scenes, add a 2× larger symmetric map, and add an orthographic camera that zooms and pans within the map.

**Architecture:** `GameCore.unity` owns the camera, UI, lights, background, mask, ship manager, selection, troop movement, and end screen. `SampleScene.unity` and the new `LargeMap.unity` own only planets, enemy AI, and map bounds. `SceneManager` loads core first, then the selected map additively. A `MapBounds` component and `OrthographicCameraController` provide aspect-aware fit, zoom, pan, and clamping.

**Tech Stack:** Unity 6000.7.0b6, URP 17.7, Input System 1.20.x, PlayMode tests, WebGPU/WebGL2 development build.

**Spec:** `docs/superpowers/specs/2026-10-25-large-map-and-core-scene-design.md`

## Global Constraints

- Do not change gameplay rules, ship tuning, post-processing, or mask behaviour in this phase.
- The default map becomes `LargeMap`; `SampleScene` stays in Build Settings.
- `GameCore` must be in Build Settings because it is loaded additively.
- Every map scene must contain no camera, UI camera, EventSystem, mask manager, ship manager, planet selection manager, or end screen.
- `SampleScene` keeps its current planet layout and remains loadable with `GameCore`.
- Camera fit is aspect-aware; do not hard-code 16:9.
- `LargeMap` is `44 × 22` world units, mirror-symmetric across both axes, with four equal home planets and a neutral centre.
- Two-player mode remains the only playable mode in this phase.
- Keep the existing 23 PlayMode tests green.
- Do not commit generated build output under `Builds/`.

## Review Focus

- Whole map remains visible at 16:9, 16:10, 4:3, and ultrawide; zoom/pan never reveals background edges.
- Core loads before the map, so managers that need planets must retry rather than permanently giving up.
- Pointer selection and cursor lights stay aligned after zooming and panning.
- Win/loss end screen still appears after the cross-scene move and after loading a second map.
- `SampleScene` and the test suite still work with the new load order.

---

### Task 1: Shared core scene and additive loading

**Files:**
- Create: `Assets/Warmask.Scenes/GameCore.unity`
- Create: `Assets/Warmask.Scenes/Runtime/EndScreenController.cs`
- Create: `Assets/Warmask.Core/Runtime/MapBounds.cs`
- Create: `Assets/Tests/GameplaySceneLoader.cs`
- Modify: `Assets/Warmask.Scenes/SampleScene.unity`
- Modify: `Assets/Warmask.Scenes/Runtime/SceneManager.cs`
- Modify: `Assets/Warmask.Scenes/Runtime/Warmask.Scenes.asmdef`
- Modify: `Assets/Tests/ShipBehaviorTests.cs`
- Modify: `Assets/Tests/ShipPerformanceTests.cs`
- Modify: `Assets/Editor/UpgradeSmoke.cs`
- Modify: `ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Produces: `GameCore` scene name `GameCore`.
- Produces: `EndScreenController.Configure(GameObject endScreen, GameObject winButton, GameObject lostButton)`.
- Produces: `EndScreenController.ShowWin()` and `ShowLost()`.
- Produces: `GameplaySceneLoader.Load(string mapSceneName)` returning `IEnumerator`.
- Consumes: `PlanetsManager.OnPlayerWin` and `PlanetsManager.OnPlayerLost` (`UnityEvent`).

- [ ] **Step 1: Add the scene loader test helper**

Create `Assets/Tests/GameplaySceneLoader.cs`:

```csharp
using System.Collections;
using UnityEngine.SceneManagement;

namespace Warmask.Tests
{
    public static class GameplaySceneLoader
    {
        public const string CoreScene = "GameCore";

        public static IEnumerator Load(string mapSceneName)
        {
            SceneManager.LoadScene(CoreScene);
            yield return null;
            SceneManager.LoadScene(mapSceneName, LoadSceneMode.Additive);
            yield return null;
            yield return null;
        }
    }
}
```

Add `"Warmask.Scenes"` to the `references` array in `Assets/Tests/Warmask.Tests.asmdef` only if a test later needs a `Warmask.Scenes` type; the tests below do not.

- [ ] **Step 2: Add the end-screen controller**

Create `Assets/Warmask.Scenes/Runtime/EndScreenController.cs`:

```csharp
using UnityEngine;
using Warmask.Planet;

namespace Warmask.Scenes
{
    public class EndScreenController : MonoBehaviour
    {
        [SerializeField] private GameObject endScreen;
        [SerializeField] private GameObject winButton;
        [SerializeField] private GameObject lostButton;

        private PlanetsManager _planets;

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
```

Add `"Warmask.Planet"` to `Warmask.Scenes.asmdef` references.

- [ ] **Step 3: Move the generic objects into GameCore**

Create `Assets/Warmask.Scenes/GameCore.unity` as an empty additive scene. Move these roots from `SampleScene` into it, preserving components and references:

- `Main Camera`
- `UI Camera`
- `Global Light 2D`
- `Background`
- `EventSystem`
- `MaskSwitch`
- `MaskManager`
- `ShipManager`
- `PlanetSelectionManager` (including `MouseLineHelper` and `TroopMovementManager` children)
- `End Screen`

Add `EndScreenController` to a new active root GameObject named
`End Screen Controller` in `GameCore` (not to `End Screen`, which starts
inactive, so its `Update` would never run). Assign `endScreen`, `winButton`, and
`lostButton` to `End Screen`, `Win Button`, and `Lost Button` from that object.

Remove the serialized `OnPlayerLost`/`OnPlayerWin` persistent calls from the map's `PlanetsManager`; the controller now subscribes at runtime.

Move the two mask profile assets (`BlueMask.asset`, `RedMask.asset`) from `Assets/Warmask.Scenes/SampleScene/` to `Assets/Warmask.Scenes/GameCore/` using Unity, so GUIDs and references are preserved.

- [ ] **Step 4: Make SampleScene map-only**

`SampleScene` must contain only:

- `Planets` (`PlanetsManager` plus its planet children)
- `EnemyAI`

Add a `MapBounds` component to the `Planets` object with `center = (0, 0)`, `size = (22, 11)`, `minOrthoSize = 4.5`, `padding = 0.5`. The `MapBounds` type is created in Step 5 and is not used until the camera controller lands in Task 3.

Save `SampleScene` and `GameCore`.

- [ ] **Step 5: Add MapBounds**

Create `Assets/Warmask.Core/Runtime/MapBounds.cs`:

```csharp
using UnityEngine;

namespace Warmask.Core
{
    public class MapBounds : MonoBehaviour
    {
        [SerializeField] private Vector2 center;
        [SerializeField] private Vector2 size = new Vector2(44f, 22f);
        [SerializeField, Min(0.1f)] private float minOrthoSize = 4.5f;
        [SerializeField, Min(0f)] private float padding = 0.5f;

        public Vector2 Center => center;
        public Vector2 Size => size;
        public float MinOrthoSize => minOrthoSize;
        public float Padding => padding;

        public void Configure(Vector2 newCenter, Vector2 newSize, float newMinOrthoSize, float newPadding)
        {
            center = newCenter;
            size = newSize;
            minOrthoSize = newMinOrthoSize;
            padding = newPadding;
        }
    }
}
```

- [ ] **Step 6: Update SceneManager to load core then map**

Modify `Assets/Warmask.Scenes/Runtime/SceneManager.cs`:

- Add `[SerializeField] private string coreSceneName = "GameCore";`.
- Change `LoadGameSceneAsync()` to load `coreSceneName` in single mode, then `gameSceneName` in additive mode, then set the map scene active.
- Keep `LoadMenuSceneAsync()` loading `Menu` in single mode.
- Add a private `LoadSceneAsync(string sceneName, LoadSceneMode mode)` overload; keep the existing single-mode method for compatibility.

Exact public surface:

```csharp
public async Task LoadGameSceneAsync();
public async Task LoadMenuSceneAsync();
public async Task LoadSceneAsync(string sceneName);
public bool IsLoading { get; }
public float GetLoadingProgress();
```

The loader must not leave `_currentLoadOperation` set after the map finishes.

- [ ] **Step 7: Update tests and smoke check**

Replace direct `SceneManager.LoadScene("SampleScene")` calls in:

- `Assets/Tests/ShipBehaviorTests.cs`
- `Assets/Tests/ShipPerformanceTests.cs`

with:

```csharp
yield return GameplaySceneLoader.Load("SampleScene");
```

Update `Assets/Editor/UpgradeSmoke.cs`:

- Add `GameCore.unity` to the scene list.
- Check each scene for missing scripts.
- After the loop, open `GameCore` single and `SampleScene` additive, then assert `Camera.main != null`.

- [ ] **Step 8: Add Build Settings entries**

Add `Assets/Warmask.Scenes/GameCore.unity` to Build Settings. Keep `Menu` first and keep `SampleScene` enabled. Expected order after this task:

```text
0 Menu
1 GameCore
2 SampleScene
```

Task 2 appends `LargeMap`.

- [ ] **Step 9: Add the core/map tests**

Add these PlayMode tests:

```csharp
[UnityTest]
public IEnumerator GameCore_HasCameraAndManagers_AndNoPlanets()
{
    SceneManager.LoadScene("GameCore");
    yield return null;
    yield return null;

    Assert.IsNotNull(Camera.main);
    // Main camera plus the overlay UI camera.
    Assert.AreEqual(2, Object.FindObjectsByType<Camera>().Length);
    Assert.AreEqual(1, Object.FindObjectsByType<ShipManagerInstance>().Length);
    Assert.AreEqual(1, Object.FindObjectsByType<MaskManager>().Length);
    Assert.AreEqual(1, Object.FindObjectsByType<PlanetSelectionManager>().Length);
    Assert.AreEqual(0, Object.FindObjectsByType<PlanetInstance>().Length);
    Assert.IsNull(PlanetsManager.Instance);
}

[UnityTest]
public IEnumerator SampleScene_LoadsWithCore()
{
    yield return GameplaySceneLoader.Load("SampleScene");

    Assert.IsNotNull(Camera.main);
    Assert.IsNotEmpty(Object.FindObjectsByType<PlanetInstance>());
    Assert.IsNotNull(PlanetsManager.Instance);
    Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length);
}

[UnityTest]
public IEnumerator EndScreenController_ShowsWinAfterMapLoads()
{
    yield return GameplaySceneLoader.Load("SampleScene");

    PlanetsManager.Instance.OnPlayerWin.Invoke();
    yield return null;

    Assert.IsTrue(FindIncludingInactive("End Screen").gameObject.activeSelf);
    Assert.IsTrue(FindIncludingInactive("Win Button").gameObject.activeSelf);
    Assert.IsFalse(FindIncludingInactive("Lost Button").gameObject.activeSelf);
}

private static Transform FindIncludingInactive(string name)
{
    foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        if (t.name == name)
            return t;

    Assert.Fail($"No object named {name}.");
    return null;
}

[UnityTest]
public IEnumerator CursorLight_CreatesAfterMapLoads()
{
    SceneManager.LoadScene("GameCore");
    yield return null;
    yield return null;

    SceneManager.LoadScene("SampleScene", LoadSceneMode.Additive);
    yield return null;
    yield return null;
    yield return null;
    Assert.IsNotEmpty(Object.FindObjectsByType<CursorLight>());
}
```

Add any missing `using` directives.

- [ ] **Step 10: Run the PlayMode suite**

Run the full PlayMode suite. Expected: all existing tests plus the new core/map tests pass.

- [ ] **Step 11: Commit**

```bash
git add Assets/Warmask.Scenes Assets/Tests ProjectSettings/EditorBuildSettings.asset
git commit -m "refactor: split gameplay into shared core scene and map scenes - move camera, UI, lights, mask, ship manager and end screen into GameCore - load core then map additively - keep SampleScene working as a map-only scene - end screen now subscribes to the map at runtime"
```

---

### Task 2: Large symmetric map

**Files:**
- Create: `Assets/Warmask.Scenes/LargeMap.unity`
- Modify: `Assets/Warmask.Scenes/SampleScene.unity`
- Create: `Assets/Tests/LargeMapTests.cs`
- Modify: `ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Consumes: `GameplaySceneLoader.Load(string mapSceneName)`.
- Consumes: `MapBounds` from Task 1.
- Produces: map scene name `LargeMap`.
- Produces: 17 planets: 4 homes at `(±16, ±7)`, 12 neutrals, 1 centre at `(0, 0)`.

- [ ] **Step 1: Create LargeMap from the map-only SampleScene**

Duplicate the map-only `SampleScene` as `Assets/Warmask.Scenes/LargeMap.unity`. Keep the `EnemyAI` and `Planets` roots. Delete all existing planet children. Keep one `PlanetsManager` on the `Planets` root.

Set `Planets` `MapBounds` to:

```text
center = (0, 0)
size = (44, 22)
minOrthoSize = 4.5
padding = 0.5
```

- [ ] **Step 2: Place the 17 planets**

Use the existing `Planet.prefab` for every planet. Place all 17 exactly as listed:

| Name | Position | Type | Size | Owner |
|---|---|---|---|---|
| Home NE | `(16, 7)` | TypeA | 1.0 | None |
| Home NW | `(-16, 7)` | TypeA | 1.0 | Player1 |
| Home SE | `(16, -7)` | TypeA | 1.0 | Player2 |
| Home SW | `(-16, -7)` | TypeA | 1.0 | None |
| Near NE | `(11, 3.5)` | TypeB | 0.7 | None |
| Near NW | `(-11, 3.5)` | TypeB | 0.7 | None |
| Near SE | `(11, -3.5)` | TypeB | 0.7 | None |
| Near SW | `(-11, -3.5)` | TypeB | 0.7 | None |
| Flank NE | `(7, 8)` | TypeA | 0.7 | None |
| Flank NW | `(-7, 8)` | TypeA | 0.7 | None |
| Flank SE | `(7, -8)` | TypeA | 0.7 | None |
| Flank SW | `(-7, -8)` | TypeA | 0.7 | None |
| Outer NE | `(19, 1)` | TypeB | 1.0 | None |
| Outer NW | `(-19, 1)` | TypeB | 1.0 | None |
| Outer SE | `(19, -1)` | TypeB | 1.0 | None |
| Outer SW | `(-19, -1)` | TypeB | 1.0 | None |
| Centre | `(0, 0)` | TypeB | 1.5 | None |

Every position mirrors exactly across X and Y, so all four quadrants are identical in
type, size, and distance from the centre. If a planet's art extends past the
`44 × 22` bounds, move that role inward symmetrically for all four quadrants.

Name the four homes `Home NW`, `Home NE`, `Home SE`, `Home SW`. Name neutrals by role and quadrant, for example `Near NE`, `Flank NW`, `Outer SE`.

All planets start active. Remove the disabled duplicates from the old scene.

- [ ] **Step 3: Add symmetry tests**

Create `Assets/Tests/LargeMapTests.cs`:

```csharp
[UnityTest]
public IEnumerator LargeMap_HasFourEqualQuadrants()
{
    yield return GameplaySceneLoader.Load("LargeMap");

    var planets = Object.FindObjectsByType<PlanetInstance>();
    Assert.AreEqual(17, planets.Length);

    var quadrants = new Dictionary<string, List<string>>();
    foreach (var planet in planets)
    {
        Vector2 p = planet.transform.position;
        if (p == Vector2.zero)
            continue;

        string key = (p.y > 0 ? "N" : "S") + (p.x > 0 ? "E" : "W");
        if (!quadrants.ContainsKey(key))
            quadrants[key] = new List<string>();

        float distance = Mathf.Round(Vector2.Distance(Vector2.zero, p) * 100f) / 100f;
        quadrants[key].Add($"{planet.PlanetType}:{planet.PlanetSize:0.0}:{distance:0.00}");
    }

    Assert.AreEqual(4, quadrants.Count);
    var expected = quadrants["NE"].OrderBy(x => x).ToArray();
    foreach (var pair in quadrants)
        CollectionAssert.AreEqual(expected, pair.Value.OrderBy(x => x).ToArray(), pair.Key);
}

[UnityTest]
public IEnumerator LargeMap_HasFourHomePlanets_AndNeutralCentre()
{
    yield return GameplaySceneLoader.Load("LargeMap");

    var planets = Object.FindObjectsByType<PlanetInstance>();
    var homes = planets.Where(p => Mathf.Abs(Mathf.Abs(p.transform.position.x) - 16f) < 0.01f
                                && Mathf.Abs(Mathf.Abs(p.transform.position.y) - 7f) < 0.01f).ToArray();

    Assert.AreEqual(4, homes.Length);
    Assert.AreEqual(Globals.ePlayer.Player1, homes.Single(p => p.transform.position.x < 0 && p.transform.position.y > 0).OwnedBy);
    Assert.AreEqual(Globals.ePlayer.Player2, homes.Single(p => p.transform.position.x > 0 && p.transform.position.y < 0).OwnedBy);
    Assert.AreEqual(Globals.ePlayer.None, planets.Single(p => p.transform.position == Vector3.zero).OwnedBy);
}
```

Add `System.Collections.Generic` and `System.Linq` usings. The test assembly already references `Warmask.Planet` and `Warmask.Globals`.

- [ ] **Step 4: Add LargeMap to Build Settings**

Add `Assets/Warmask.Scenes/LargeMap.unity` after `GameCore` and before `SampleScene`:

```text
0 Menu
1 GameCore
2 LargeMap
3 SampleScene
```

- [ ] **Step 5: Run the map tests**

Run `LargeMapTests` and `SampleScene_LoadsWithCore`. Expected: pass.

- [ ] **Step 6: Commit**

```bash
git add Assets/Warmask.Scenes/LargeMap.unity Assets/Warmask.Scenes/SampleScene.unity Assets/Tests/LargeMapTests.cs ProjectSettings/EditorBuildSettings.asset
git commit -m "added: large symmetric map for four corner starts - 44x22 map with four equal homes, mirrored neutrals and a neutral centre - playable as the existing two-player mode"
```

---

### Task 3: Zoom and pan camera

**Files:**
- Create: `Assets/Warmask.Core/Runtime/OrthographicCameraController.cs`
- Modify: `Assets/Warmask.Core/Runtime/Warmask.Core.asmdef`
- Modify: `Assets/Warmask.Scenes/GameCore.unity`
- Modify: `Assets/Warmask.Planet/Runtime/PlanetSelectionManager.cs`
- Create: `Assets/Tests/CameraControllerTests.cs`

**Interfaces:**
- Consumes: `MapBounds` from Task 1.
- Produces: `OrthographicCameraController.SetBounds(MapBounds bounds)`.
- Produces: `OrthographicCameraController.FitToBounds()`.
- Produces: `OrthographicCameraController.SetZoom(float orthographicSize)`.
- Produces: `OrthographicCameraController.Pan(Vector2 worldDelta)`.
- Produces: `OrthographicCameraController.Zoom(float wheelDelta, Vector2 screenAnchor)`.
- Produces: `OrthographicCameraController.CalculateFitOrthoSize(Vector2 mapSize, float aspect, float padding)`.
- Produces: `OrthographicCameraController.ClampCenter(Vector2 center, Vector2 mapCenter, Vector2 mapSize, float orthoSize, float aspect)`.
- Produces: `OrthographicCameraController.ViewCenter`, `OrthographicCameraController.CurrentOrthoSize`.

- [ ] **Step 1: Add the camera controller**

Create `Assets/Warmask.Core/Runtime/OrthographicCameraController.cs` with this public surface:

```csharp
[RequireComponent(typeof(Camera))]
public class OrthographicCameraController : MonoBehaviour
{
    public Vector2 ViewCenter { get; }
    public float CurrentOrthoSize { get; }
    public void SetBounds(MapBounds bounds);
    public void FitToBounds();
    public void SetZoom(float orthographicSize);
    public void Pan(Vector2 worldDelta);
    public void Zoom(float wheelDelta, Vector2 screenAnchor);
    public static float CalculateFitOrthoSize(Vector2 mapSize, float aspect, float padding);
    public static Vector2 ClampCenter(Vector2 center, Vector2 mapCenter, Vector2 mapSize, float orthoSize, float aspect);
}
```

Implementation rules:

- `CalculateFitOrthoSize` returns `max(mapSize.y * 0.5f, mapSize.x * 0.5f / aspect) + padding`, with `aspect` clamped to at least `0.01f`.
- `ClampCenter`: if the view is wider than the map, centre X on `mapCenter.x`; otherwise clamp X to `[mapCenter.x - size.x/2 + halfWidth, mapCenter.x + size.x/2 - halfWidth]`. Do the same for Y.
- `FitToBounds`: centre on the map and set orthographic size to `CalculateFitOrthoSize` using the camera's current aspect.
- `SetZoom`: clamp to `[bounds.MinOrthoSize, CalculateFitOrthoSize(bounds.Size, camera.aspect, bounds.Padding)]`, then clamp the centre. Recompute the fit; never cache it.
- `Zoom`: remember the world point under `screenAnchor`, apply the new zoom, then move the centre so that world point stays under the anchor; finally clamp.
- `Pan`: add world delta, then clamp.
- `Update`: find a `MapBounds` with `FindAnyObjectByType` if none is assigned, call `FitToBounds` when one first appears, then read input. When `camera.aspect` changes, call `SetZoom(CurrentOrthoSize)` to re-clamp to the new aspect-aware range; do not jump back to the whole-map view.
- Mouse input: wheel zooms with `Zoom(scroll.y / 120f, Mouse.current.position.ReadValue())`. Middle-button drag pans by the world delta of the screen movement.
- Gamepad input: `Gamepad.current` right stick pans at `gamepadPanSpeed` world units/second; D-pad up/down zooms at `gamepadZoomSpeed` units/second. Any connected gamepad may drive the shared camera.
- Keep `Camera.main` tag and z position unchanged.
- Add `"Unity.InputSystem"` to `Warmask.Core.asmdef` references.

- [ ] **Step 2: Fix the cursor-light retry**

In `Assets/Warmask.Planet/Runtime/PlanetSelectionManager.cs`, remove `_lightSearchDone` and change the cursor-light block to:

```csharp
if (!_cursorLight)
    _cursorLight = CursorLight.TryGetOrCreate(transform, player, _pointerSource);
```

This makes the core selection manager retry after the map scene arrives.

- [ ] **Step 3: Add camera tests**

Create `Assets/Tests/CameraControllerTests.cs`:

```csharp
[Test]
public void FitOrthoSize_UsesCurrentAspect()
{
    Assert.AreEqual(12.875f, OrthographicCameraController.CalculateFitOrthoSize(new Vector2(44f, 22f), 16f / 9f, 0.5f), 0.001f);
    Assert.AreEqual(17f, OrthographicCameraController.CalculateFitOrthoSize(new Vector2(44f, 22f), 4f / 3f, 0.5f), 0.001f);
}

[Test]
public void ClampCenter_KeepsViewInsideMap()
{
    Vector2 center = OrthographicCameraController.ClampCenter(new Vector2(100f, 100f), Vector2.zero, new Vector2(44f, 22f), 4.5f, 16f / 9f);
    Assert.AreEqual(14f, center.x, 0.001f);
    Assert.AreEqual(6.5f, center.y, 0.001f);
}

[UnityTest]
public IEnumerator FitAndZoom_ClampToBounds()
{
    var cameraObject = new GameObject("Camera");
    var camera = cameraObject.AddComponent<Camera>();
    camera.orthographic = true;
    camera.aspect = 16f / 9f;

    var boundsObject = new GameObject("Bounds");
    var bounds = boundsObject.AddComponent<MapBounds>();
    bounds.Configure(Vector2.zero, new Vector2(44f, 22f), 4.5f, 0.5f);

    var controller = cameraObject.AddComponent<OrthographicCameraController>();
    controller.SetBounds(bounds);
    controller.FitToBounds();
    Assert.AreEqual(12.875f, controller.CurrentOrthoSize, 0.001f);

    controller.SetZoom(4.5f);
    controller.Pan(new Vector2(100f, 100f));
    yield return null;
    Assert.AreEqual(14f, controller.ViewCenter.x, 0.001f);
    Assert.AreEqual(6.5f, controller.ViewCenter.y, 0.001f);

    Object.Destroy(cameraObject);
    Object.Destroy(boundsObject);
}
```

Add `using Warmask.Core;`. Adjust the exact values only if the implementation's padding differs; the spec pins padding `0.5`.

- [ ] **Step 4: Wire the camera into GameCore**

Add `OrthographicCameraController` to `GameCore`'s `Main Camera`. Assign the map bounds at runtime; leave the serialized bounds empty so each map supplies its own. Ensure the controller does not run in `Menu` (it is only in GameCore).

- [ ] **Step 5: Run camera and lifecycle tests**

Run `CameraControllerTests`, `CursorLight_CreatesAfterMapLoads`, and `SampleScene_LoadsWithCore`. Expected: pass.

- [ ] **Step 6: Commit**

```bash
git add Assets/Warmask.Core Assets/Warmask.Planet/Runtime/PlanetSelectionManager.cs Assets/Warmask.Scenes/GameCore.unity Assets/Tests/CameraControllerTests.cs
git commit -m "added: orthographic zoom and pan camera - aspect-aware whole-map fit - cursor-anchored wheel zoom plus middle-drag and gamepad pan - clamp camera to each map's MapBounds - retry cursor light after map load"
```

---

### Task 4: Wire the large map as the default and verify

**Files:**
- Modify: `Assets/Warmask.Scenes/Menu.unity`
- Modify: `Assets/Warmask.Scenes/Runtime/SceneManager.cs`
- Modify: `docs/upgrade-plan.md`
- Modify: `docs/performance-findings.md`
- Modify: `Assets/Editor/UpgradeSmoke.cs`

**Interfaces:**
- Consumes: all previous tasks.
- Produces: Menu → `LargeMap` flow.
- Produces: documented editor workflow: open `GameCore` plus a map additively.

- [ ] **Step 1: Point the menu at LargeMap**

Set the serialized `gameSceneName` on `Menu`'s `SceneManager` to `LargeMap`. Keep `menuSceneName = Menu`. Confirm `coreSceneName = GameCore`.

- [ ] **Step 2: Update editor docs**

Add a short section to `docs/upgrade-plan.md`:

- Shared core scene layout.
- How to play in the editor: open `GameCore` and `LargeMap` additively.
- Menu default is `LargeMap`; `SampleScene` is kept for comparison and tests.
- Camera controls: wheel zoom, middle-drag pan, gamepad right stick/D-pad.
- Known limit: one shared camera for all local players.

Add the camera/map checklist to `docs/performance-findings.md` or the upgrade plan:

- Whole map visible at 16:9 and 4:3.
- Zoom keeps pointer over the same planet.
- Pan clamps, no background edges.
- Mask tint and rotation survive zoom/pan.
- Cursor lights follow pointer.
- Planet labels and HUD stay crisp.
- Two-player local join still works.
- `SampleScene` still loads with core.

- [ ] **Step 3: Update UpgradeSmoke scene list**

Add `LargeMap` and `GameCore` to `UpgradeSmoke.Scenes`, and make the camera assertion load `GameCore` + `LargeMap` rather than checking the last single scene.

- [ ] **Step 4: Run the full PlayMode suite**

Run the full suite. Expected: all previous tests plus the new tests pass. Record the count in the docs.

- [ ] **Step 5: Build and play-test**

Build the WebGPU development profile to `Builds/WebGPU`. Then manually check:

- Menu → LargeMap.
- 2-player local join.
- Wheel zoom at cursor.
- Middle-drag pan and clamp.
- Gamepad right stick/D-pad.
- Background fill and mask tint while zoomed/panned.
- Planet selection, cursor lights, HUD, end screen.

- [ ] **Step 6: Commit**

```bash
git add Assets/Warmask.Scenes/Menu.unity Assets/Warmask.Scenes/Runtime/SceneManager.cs Assets/Editor/UpgradeSmoke.cs docs
git commit -m "added: large map as default game scene and verification notes - menu loads LargeMap through GameCore - docs cover shared-scene editor workflow and camera controls - smoke checks core plus map"
```

---

## Self-Review

- Spec coverage: core extraction, map-only scenes, additive loading, end-screen boundary, 44×22 symmetric map, aspect-aware camera, 4-player compatibility notes, tests, and WebGPU verification each have a task.
- The plan intentionally leaves exact planet names and minor offsets to the implementer inside the symmetry constraints.
- `MapBounds` is created in Task 1 and used by Task 2 (map authoring) and Task 3 (camera clamping).
- All new public names used by later tasks are defined in the Interfaces blocks.
