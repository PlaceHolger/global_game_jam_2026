# Input, Match Setup and Clock — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Make map orders reliable with multi-planet selection, let the menu pick 1–3 AI opponents, add in-match pause/speed controls, and remove the black square around explosion debris.

**Architecture:** `PlanetSelectionManager` grows from one source planet to a selection list: left-click toggles owned planets, a left-drag box selects owned planets, and every send iterates the selection. `MapMenuSettings` writes `Globals.playerCount = enemies + 1` from a runtime-built slider; `FourPlayerSetup` consumes the spare homes one slot at a time. A new `MatchClock` in `GameCore` owns `Time.timeScale`. The explosion material moves from the legacy additive shader to the project's URP particle shader.

**Tech Stack:** Unity 6.7, C# 9, URP 17.7, Input System, Unity Test Framework (PlayMode), IMGUI for in-match overlays, uGUI for the menu.

**Spec:** `docs/superpowers/specs/2026-10-27-enemy-count-speed-pause-design.md`

## Global Constraints

- A two-player match must keep behaving exactly as today: same homes, same AI, same HUD, same win rule.
- `Globals.playerCount` is 2..4; enemies are `playerCount - 1`.
- Three or more players always load `LargeMap`.
- Restore `Time.timeScale = 1` whenever the clock leaves the scene; never leave the menu or next match slowed.
- No per-player mask art; masks stay global.
- Do not modify `Assets/Warmask.Planet/Prefabs/Planet.prefab`, `Assets/_Recovery/`, or anything under Downloads.
- Revert test-run rewrites of `EditorSettings.asset`, `Web - Desktop - Development.asset`, `spaceranger SDF.asset`, and `global_game_jam_2026.slnx` before committing.

## Review Focus

- Right-click on a planet after a drag-select must send from every selected source, not just the last one clicked.
- A right-click shorter than 0.5 s and smaller than 12 px on a planet must send; a longer or larger gesture must only pan.
- Left-clicking an owned planet must never send troops; it must toggle that planet in the selection.
- A three-player match must leave the fourth corner neutral and run exactly two AIs.
- Leaving a paused or 2× match must return the next scene to `Time.timeScale == 1`.

---

### Task 1: Multi-select and reliable right-click

**Files:**
- Modify: `Assets/Warmask.Planet/Runtime/PlanetSelectionManager.cs`
- Test: `Assets/Tests/MultiSelectTests.cs` (create)

**Interfaces:**
- Produces: `public int SelectedCount`, `public IReadOnlyList<PlanetInstance> Selected`, `public void HandlePlanetClick(PlanetInstance planet, bool sendAll = false)`.
- Consumes: `PlanetInstance.OwnedBy`, `PlanetInstance.SetSelection(bool)`, `TroopMovementManager.MoveTroops(...)`.

- [x] **Step 1: Write the failing test**

Create a PlayMode test that loads `LargeMap`, gets P1's home, calls `HandlePlanetClick(home)` twice via `PlanetSelectionManager.Instance`, and asserts `SelectedCount == 2`. Also assert a click on the P2 home does not add it.

- [x] **Step 2: Run it and watch it fail** (expected: `SelectedCount` does not exist / compile error).

- [x] **Step 3: Implement the selection list**

Replace `_startPlanet` with `List<PlanetInstance> _selected`, a `_primary` (line start), and the box-drag state (`_leftPressScreen`, `_boxing`, `_boxStart`, `_boxVisible`, `const float BoxDragThreshold = 8f`). Right press records `_rightPressPlanet`, `_rightPressScreen`, `_rightPressTime`; right release sends only when `< 0.5 s` and `< 12 px`, using the press-time planet.

- [x] **Step 4: Run the new test and the full suite.**

- [x] **Step 5: Commit.**

### Task 2: Enemy slider and configurable slot setup

**Files:**
- Modify: `Assets/Warmask.Scenes/Runtime/MapMenuSettings.cs`
- Modify: `Assets/Warmask.Scenes/Runtime/GameSession.cs`
- Modify: `Assets/Warmask.EnemyAi/Runtime/FourPlayerSetup.cs`
- Modify: `Assets/Warmask.Scenes/Menu.unity` (one serialized flag only)
- Modify: `Assets/Warmask.Scenes/LargeMap.unity` (home array only)
- Modify: `Assets/Warmask.Scenes/Runtime/Warmask.Scenes.asmdef` (UI + TMP references)
- Test: `Assets/Tests/MatchSetupTests.cs` (create)

**Interfaces:**
- Produces: `MapMenuSettings.SetEnemyCount(float enemies)`, `MapMenuSettings.SetFourPlayers(bool)` now routes through it; `Globals.playerCount = clamp(round(enemies), 1, 3) + 1`.
- Consumes: `Globals.playerCount`, `FourPlayerSetup.spareHomes`.

- [x] **Step 1: Write failing tests**

EditMode test: `SetEnemyCount(0)` → `playerCount == 2`; `SetEnemyCount(2.4f)` → `3`; `SetEnemyCount(9)` → `4`. PlayMode test: `playerCount = 3`, load `LargeMap`, assert P1/P2/P3 own NW/SE/NE, SW is `None`, and `EnemyAI P3` exists while `EnemyAI P4` does not.

- [x] **Step 2: Run and watch them fail.**

- [x] **Step 3: Implement**

`SetEnemyCount` clamps/rounds and writes `playerCount`. The slider is built once at runtime from `DefaultControls.CreateSlider`, placed under the same menu panel at `(19, 104)`, min 1, max 3, whole numbers, label cloned from the toggle's TMP label. Add `enemySliderHost` serialized flag and set it on the `4 Players Toggle` instance in `Menu.unity`. `SetFourPlayers` calls `SetEnemyCount(isFour ? 3 : 1)`. `FourPlayerSetup` uses `PlanetInstance[] spareHomes` and assigns slots `2..playerCount-1`; `GameSession.MapScene` forces `LargeMap` for `playerCount > 2`.

- [x] **Step 4: Run tests + full suite.**

- [x] **Step 5: Commit.**

### Task 3: Match clock (pause and speed)

**Files:**
- Create: `Assets/Warmask.Planet/Runtime/MatchClock.cs`
- Modify: `Assets/Warmask.Scenes/GameCore.unity` (component on the selection manager object)
- Test: `Assets/Tests/MatchClockTests.cs` (create)

**Interfaces:**
- Produces: `public bool Paused`, `public float Speed`, `public void TogglePause()`, `public void StepSpeed(int direction)`, `public static bool BlocksPointer(Vector2 screenPosition)`.
- Consumes: `Keyboard.current.pKey`, `leftBracketKey`, `rightBracketKey`, `Time.timeScale`.

- [x] **Step 1: Write failing tests**

EditMode test: add `MatchClock` to a fresh GameObject; `TogglePause()` → `Time.timeScale == 0`; `TogglePause()` → `1`; `StepSpeed(1)` → `1.5f`; `Object.DestroyImmediate(clock.gameObject)` → `Time.timeScale == 1`.

- [x] **Step 2: Run and watch them fail.**

- [x] **Step 3: Implement**

`MatchClock` holds `{0.5f, 1f, 1.5f, 2f}`, defaults to index 1. `P` toggles pause, `[`/`]` step. `OnGUI` draws a bottom-centre strip with `II`/`>` and `-`/`+` buttons and the current value; `BlocksPointer` reports the strip rect (IMGUI coordinates converted from the Input System's bottom-left origin). Restore `Time.timeScale = 1` in `OnDisable`/`OnDestroy`. `PlanetSelectionManager` ignores clicks whose screen position is inside the strip.

- [x] **Step 4: Run tests + full suite.**

- [x] **Step 5: Commit.**

### Task 4: Explosion debris material

**Files:**
- Modify: `Assets/Warmask.Content-Assets/Materials/TriangleParticle.mat`
- Test: Play-mode visual check (no automated pixel test).

- [x] **Step 1: Switch the material to `Universal Render Pipeline/Particles/Unlit`** (guid `0406db5a14f94604a8c57ccfbc9f3b46`), `_BaseMap` = `Ship-Default` (guid `a0f122d527fea5d4b8acb090321377d0`), surface transparent, **additive blend** (`SrcAlpha`/`One`), `ZWrite 0`. Additive keeps the original glow look; alpha blending exposed the particles' dark old-age colour as dark polygons.
- [x] **Step 2: Play mode: simulate the explosion at 0.15/0.35/0.7 s; no dark quads. Legacy shader comparison rendered dull dark particles.**
- [x] **Step 3: Commit.**

### Task 5: Full verification

- [x] Run the full PlayMode suite. 62/62 passed.
- [x] Revert the four test-run artifacts.
- [x] Update the spec's verification section with what actually ran.
- [x] Commit.
