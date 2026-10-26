# Large map, shared core scene, and camera zoom — design

Date: 2026-10-25
Status: proposed

## Intent

Build the foundation for a four-player local match without changing the rules of
the current two-player game. The first deliverable is a larger, fair map and a
camera that can zoom and pan. The generic objects currently duplicated in every
game scene move into one shared additive `GameCore` scene, so later four-player
work (HUD, input slots, owner UI) lives in one place.

The four-player mode itself is a second phase. This phase must make that phase
smaller, not pretend to finish it.

## Goals

- Add `Assets/Warmask.Scenes/GameCore.unity`: camera, UI camera, lights,
  background, event system, mask manager/UI, ship manager, planet selection,
  troop movement, and end screen.
- Turn `SampleScene` into a map-only scene: planets, enemy AI, and map bounds.
- Add `Assets/Warmask.Scenes/LargeMap.unity`: roughly twice the linear size of
  the current map, with four equal corner starts and symmetric planets. It is
  playable as the existing two-player mode now and is the future four-player map.
- Add an orthographic camera controller: whole-map default view, mouse-wheel
  zoom anchored at the cursor, middle-mouse drag pan, gamepad right-stick pan and
  D-pad zoom, clamped to the map.
- Keep the current two-player flow, mask tint, cursor lights, background fill,
  UI crispness, ship behaviour, and all existing tests working.
- Update tests and scene loading for the additive core + map setup.

## Non-goals

- Playable four-player mode, four HUD slots, four owner pips, four joining
  players, four-player victory screen, or AI filling empty slots. These are the
  next phase.
- Online play, fog of war, teams, new ship art, planet snapping, or a map
  selection UI.
- New gameplay rules, ship tuning, or post-processing changes.
- A general map-data pipeline. The two maps remain hand-authored scenes.

## Constraints

- The project uses additive scenes only if all additive scenes are in Build
  Settings. `GameCore` must be listed there.
- `SceneLoader` is a persistent menu object that already survives scene loads.
  It currently loads one gameplay scene by name.
- PlayMode tests load `SampleScene` directly. After the split they must load
  `GameCore` first, then the map.
- `PlanetSelectionManager` creates each player’s cursor light by finding a planet
  light template. With core loaded before the map, the search must retry until a
  map exists instead of giving up permanently.
- `PlanetInstance`, `ShipyardInstance`, and `FleetStatusHud` start from map
  objects. Core managers must tolerate a frame with no planets while the map is
  still loading.
- The background is a world-space sprite that scales to the camera; moving and
  zooming the camera must not expose edges or break mask tint/rotation.
- Pointer, cursor-light, selection-line, and raw-input code already convert
  through `Camera.main`, so camera movement is transparent to them.

## Chosen approach

**Additive shared core scene.** `GameCore` owns all objects that are not part of
a map. Each map owns only its planets, map-specific AI, and map bounds.

This was chosen over duplicating the scene (fast but creates drift) and over a
core prefab placed in every map (one source of truth but weaker separation). The
additive scene gives one authoritative copy of camera, UI, managers, and mask
setup, and it matches how the game will grow to more maps and players.

The refactor lands in four commits:

1. **Core extraction only.** Move generic objects out of `SampleScene` into
   `GameCore`, update loading/tests, and prove the existing game and suite still
   work with no behaviour changes.
2. **Large map.** Add `LargeMap` and its symmetry tests.
3. **Camera.** Add the zoom/pan controller and its tests.
4. **Default + verification.** Point the menu at `LargeMap`, update the smoke
   check and docs, run the suite, and build.

## Scene architecture

### GameCore.unity

Contains:

- `Main Camera` (with `UniversalAdditionalCameraData` and `Physics2DRaycaster`),
  plus the new orthographic camera controller.
- `UI Camera` (Overlay in Main's stack, post-processing off).
- `Global Light 2D`.
- `Background` (`SpriteRenderer` + `BackgroundColorChanger`).
- `EventSystem`.
- `MaskSwitch` canvas.
- `MaskManager` and its two mask profiles.
- `ShipManager`.
- `PlanetSelectionManager`, including its `MouseLineHelper`,
  `TroopMovementManager`, and the runtime-created P2 selection root.
- `End Screen`.

No planets, no `PlanetsManager`, no `EnemyAi`, and no map bounds.

### SampleScene.unity (existing map)

Contains only:

- `Planets` root (`PlanetsManager` + planet prefabs).
- `EnemyAI` root.
- `MapBounds` for the camera controller.

Its content and planet layout stay unchanged.

### LargeMap.unity (new map)

Contains only:

- `Planets` root with the new symmetric layout.
- `EnemyAI` root, so solo and one-human-vs-AI still work. It disables itself
  automatically in local two-player mode.
- `MapBounds` describing the playable rectangle.

### Loading

`SceneLoader` (renamed from `SceneManager` to stop colliding with Unity's
`SceneManager`) loads `GameCore` in single mode, then `GameSession.MapScene`
additively, then sets the map as the active scene. `GameSession` is a small
static holder for the next match (map scene, player count). The menu button
still calls `LoadGameSceneAsync()`. The default map becomes `LargeMap`;
`SampleScene` stays in Build Settings and is still usable for tests and editor
comparison. No map-selection UI is added in this phase.

`LoadMenuSceneAsync()` continues to load `Menu` in single mode, which unloads
the core and map. `SceneLoader` itself remains `DontDestroyOnLoad`.

### Cross-scene event boundary

`PlanetsManager` stays in the map, but its `OnPlayerLost`/`OnPlayerWin` UnityEvents
currently point at `End Screen` children. Those references cannot survive moving
the end screen into `GameCore`. Add a small `EndScreenController` in core that
finds the map’s `PlanetsManager` when it appears and subscribes to both events at
runtime. The serialized cross-scene calls on `PlanetsManager` are removed.

This keeps the dependency direction map → core events and gives phase 2 one
place to replace the two-player win/lost rule with a last-player-standing result.
The controller must retry until `PlanetsManager.Instance` exists, because core
loads first and the map arrives one load later. It must unsubscribe from the
previous map before subscribing to a new one and must tolerate the map unloading
before core does.

Direct editor play is supported by opening `GameCore` and a map additively. The
README/design notes will say this explicitly. The project will not add hidden
auto-loading scene logic.

## Map layout

Working name: `LargeMap`. Approximate bounds: `44 × 22` world units, twice the
current map’s linear size.

The map is mirror-symmetric across both the X and Y axes. This gives each of the
four corners exactly the same local neighbourhood, even through the map is wider
than it is tall.

- Four home planets, one per corner, at approximately `(±16, ±7)`.
  All four have the same type, size, and starting units.
- Three neutral planets per quadrant, mirrored across both axes. Example local
  offsets from a home: one near planet, one flank planet, one outer planet.
- One shared centre planet at `(0, 0)`, larger and neutral.
- Total: 17 planets (4 homes + 12 neutrals + 1 centre).
- Planet types and sizes repeat per quadrant, so no corner gets a better mask
  matchup or production start.
- In the current two-player mode, P1 starts top-left and P2 bottom-right. The
  other two home planets stay neutral. When four-player mode lands, P3 takes
  top-right and P4 bottom-left.

The layout is data in the scene, not generated at runtime. The symmetry is
covered by a test that compares planet type, size, and distance from the centre
across the four quadrants.

## Camera

Add one `OrthographicCameraController` to `Main Camera` in `GameCore`.

- Default view: the whole `MapBounds` is visible, with a small margin. This is
  computed from the current aspect ratio, not hard-coded to 16:9. At 16:9 the
  44 × 22 map needs about `orthographicSize` 12.5; a 16:10 window needs about
  13.75, and a 4:3 window about 16.5. `FitToBounds` returns whatever value the
  current aspect needs, so resizing a browser never cuts off a corner.
- Zoom range: per map, stored on `MapBounds` as `minOrthoSize`; the maximum is
  always the aspect-aware `FitToBounds` value. The large map starts with
  `minOrthoSize = 4.5`. `SampleScene` gets its own `minOrthoSize` and its own
  fit value, so the old map does not inherit the large map’s zoom range.
- Mouse wheel zooms toward the cursor world point, so the point under the cursor
  stays under the cursor while zooming.
- Middle-mouse drag pans.
- The last-used gamepad can pan with the right stick and zoom with D-pad up/down.
  Any connected gamepad can drive the shared camera.
- Camera centre is clamped so the view never leaves the map bounds. If the view
  is wider/taller than the map at the current aspect, it centres that axis.
- Camera input uses normal time in this phase. If pause is added later, it can
  switch to unscaled time.
- The controller exposes public `Zoom`, `Pan`, and `FitToBounds` methods so tests
  can verify clamping without synthesising device input.

The controller does not change `Camera.main`, UI camera stacking, background
behaviour, cursor lights, or pointer conversion.

## Player compatibility

Phase 1 still uses `Globals.ePlayer.None/Player1/Player2`. The large map has
four home bases but only two are owned by playable players.

Phase 2 will:

- Extend player identity to four (prefer an id-based model over more enum cases
  if the change stays contained; otherwise `Player1..Player4` is the smallest
  complete step).
- Add four player colours and owner pips.
- Draw a four-slot fleet HUD.
- Generalize the join flow so P2–P4 can use gamepads, with one of them allowed to
  take the keyboard fallback.
- Disable `EnemyAi` in four-human matches and add last-player-standing victory.
- Reuse the four home bases already placed here.

The large map and core scene must not block those changes. No four-player input
or HUD code is added in phase 1.

## Visual and UX checks

- Menu → LargeMap loads with no missing references.
- Whole map is visible at default zoom; all four bases and centre are readable.
- Zooming at the cursor keeps the cursor over the same planet.
- Panning stops at the map edges; no background gaps.
- Mask tint and rotation still apply while zoomed and panned.
- Cursor lights follow the pointer at every zoom level and stay on the
  background only.
- Planet labels and HUD stay crisp (UI camera overlay unaffected).
- Two-player local join still works, with P2 gamepad or keyboard.
- `SampleScene` still works when opened with `GameCore`.

## Tests

Update existing PlayMode tests to use one helper that loads `GameCore` and a map
in order. Add:

- `GameCore_ContainsSingleCameraAndManagers_AndNoPlanets`.
- `LargeMap_HasFourEqualQuadrants` (count, type, size, distance from centre).
- `LargeMap_HasFourHomePlanets_AndNeutralCentre`.
- `Camera_FitToBounds_ShowsWholeMap` at both 16:9 and 4:3 aspects.
- `Camera_ZoomAndPan_ClampToBounds`.
- `EndScreenController_SubscribesAfterMapLoads`.
- `SampleScene_StillLoadsWithCore` smoke test.

Update `UpgradeSmoke` to load `GameCore` and the map together; its current
“main camera after loading SampleScene” check is invalid after the split.

Keep the existing 23 PlayMode tests green. Run a WebGPU development build after
the camera and map work, because background coverage and input are browser
sensitive.

## Risks

- **Scene reference churn.** Moving objects between scenes can break serialized
  references. The extraction commit is behaviour-only and must be verified before
  adding the new map.
- **Core/map lifecycle.** Managers that run before the map loads can give up on
  missing planets. The cursor-light retry is one known case; audit others during
  extraction.
- **Test loading.** Every test that assumes `SampleScene` contains the camera or
  managers must load `GameCore`. A helper prevents copy-pasted load order drift.
- **Shared camera in local play.** One camera means players share the view; a
  player can pan away from another player’s action. This is accepted for phase 1
  and should be revisited if local four-player feels bad.
- **Background resolution at full zoom-out.** The nebula is scaled up to cover
  the larger view. If it looks soft, the fix is a higher-resolution background or
  a separate far-background layer, not a camera hack.
- **Menu default.** The menu will load `LargeMap` by default. If the old map
  should remain reachable from the menu, that needs a small selection UI in a
  later change.

## Open decisions left to the builder

- Exact planet offsets, names, types, and sizes inside the symmetry constraints.
- Whether `GameCore` uses a `MapBounds` component or a serialized rectangle on
  the camera controller. Prefer the component so each map owns its bounds.
- Whether the camera applies smoothing or snaps directly; either is acceptable
  if the clamped public API stays testable.
