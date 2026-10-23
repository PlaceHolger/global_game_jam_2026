# Warmask — upgrade plan

Date: 2026-10-22
Status: approved. Phase 1 done; local two-player next.
Rule: nothing is committed until the work runs and you create the branch. Until then, edits stay uncommitted in the working tree (creating the branch later carries them over).

---

## 1. Where the project is

- Started on Unity 6000.3.6f1; upgraded to **6000.7.0b6** (revision 820f8ee8e58b) in phase 1. URP 17.7.0, Input System 6.7.0, test framework 1.9.0.
- Two scenes: `Menu.unity`, `SampleScene.unity`. 11 planets.
- Ships are GameObjects with a `Collider2D`, pooled per shipyard. Boid steering already runs in a Burst job; neighbor lookup still uses `Physics2D.OverlapCircle` per ship.
- Current caps: 24 ground units per planet, 20 own ships assigned per shipyard (sending ships moves the slot to the destination, so origins can rebuild; a planet can hold more than 20 by receiving), fleet cap per player = owned planets × per-planet ship cap + 20 buffer (`Globals.fleetCapBuffer`), pool 50.
- No netcode, no tests, no central config. Tuning values live on prefabs, scene overrides and the `Globals` ScriptableObject.
- `main` is clean and pushed. Jam-quality code with known bugs (section 5).

## 2. Goals

1. Upgrade to a stable engine/package baseline.
2. Fix the bugs that would poison multiplayer and tuning.
3. One settings/options system, with a debug panel for experiments.
4. More ships, proven by measurement, not by assumption.
5. Combat and planet-attack juice.
6. Local two-player on one screen.
7. Online two-player via Unity Lobby/Relay: **co-op vs AI** and **1v1 versus**.
8. Automated tests for the rules, headless test + build commands.

## 3. Non-goals

- 3+ players at launch (see section 15), dedicated servers, host migration, reconnect.
- Matchmaking beyond a join code.
- Full DOTS rewrite of the game.
- Dual physical mice beyond the phase-5 spike, see 4.4.
- Progression, unlocks, accounts beyond anonymous auth.
- Mobile/console. WebGL is an open decision, not a commitment.

## 4. Modes

Assumption to confirm: "co-op and against AI" means these three:

| Mode | Players | AI | Notes |
|---|---|---|---|
| Solo | 1 human | yes | the current game |
| Co-op | 2 humans, same faction | yes | shared command of the faction |
| Versus | 2 humans, opposite factions | no | neutral planets stay neutral |

If Versus is not wanted at launch, co-op ships first and Versus becomes phase 7b.

## 5. Decisions

### 4.1 Engine: 6.7.0b6 (decided)

- Target is **6000.7.0b6**, released 2026-10-21. It is the LTS beta, so starting here avoids a second upgrade later, and it brings the 2D light shader graph targets and soft shadows that help phase 6 effects.
- URP goes 17.3.0 → **17.7.0**. Renderer2D has no custom renderer features, so risk is low.
- 6.7 moves **Netcode for GameObjects to 3.x** and Netcode for Entities back to an independent package. We have no netcode yet, so 3.x is a greenfield choice, not a migration.
- Beta discipline: pin **b6**, do not chase weekly betas mid-phase, and upgrade only when a fix we need lands or 6.7 reaches RC. Record the exact editor version in the README so builds are reproducible.
- Open blockers exist in the beta (e.g. editor crash on Vulkan boot when a cloud project id is set). If the editor misbehaves, the fallback is 6000.6.7f1 with URP 17.6.0, and the upgrade repeats.
- DOTween: the project ships an old DLL. Try it under 6.7 first; replace with current free DOTween only if it misbehaves (then run the Utility Panel, verify `DOTweenSettings`).
- DOTween: the project ships an old DLL. Replace it with the current free DOTween (Asset Store/Demigiant), run the DOTween Utility Panel, verify `DOTweenSettings`. DOTween's API surface is stable; expect zero call-site changes.
- Input System: bump to the latest 6.7-compatible version (1.20.x).
- `Entities 6.7` is a core package in 6.7; Netcode for Entities 7.0 is independent again. Both stay unused unless the DOTS gate is hit.

### 4.2 Ship scale: optimize Mono first, DOTS only behind a gate

Full ECS means rewriting ships, spawning, pooling, ownership, targeting, rendering and the planet/UI glue. The boid math — the only part that is already cheap — is already jobified. The measurable costs are:

- one `Physics2D.OverlapCircle` per ship per frame in `ShipManagerInstance` **plus** a second one per ship in `ShipInstance.CacheCollidersIfNeeded`;
- a raycast per ship per frame regardless of fire cooldown;
- `job.Schedule(...).Complete()` on the main thread, so the job buys nothing;
- per-ship `Update`, audio sources and trails.

Plan: build one spatial hash per frame (Burst job, `NativeParallelMultiHashMap`), use it for neighbors and targeting, remove the double lookup, remove the forced job completion, add distance-based LOD for trails/audio/neighbor quality. Then measure.

Gate: **60 fps at 1,000 ships** in a stress scene on the target machine. If it fails, run a time-boxed DOTS spike (Entities + Entities Graphics for ships only, Mono bridge for planets/UI) and decide from numbers. Until then, DOTS is not in the schedule.

Note: online bandwidth becomes the real cap before CPU does. Snapshots of 1,000 ships are expensive; online modes will likely sync 300–500 ships or apply relevance culling (section 9). This interaction is a reason not to chase DOTS figures that no network can carry.

### 4.3 Online: NGO, host-authoritative, orders + batched snapshots

- **Netcode for GameObjects** 3.x (greenfield, no migration) plus **Multiplayer Services** (Sessions API = Lobby + Relay) and anonymous auth.
- Host runs the simulation. Clients send **orders**, not positions: send troops (from, to, count), mask switch, pause. The host validates ownership and cooldowns.
- Planet state (11 objects) syncs cheaply as `NetworkVariable`s: owner, ground units, production, mask.
- Ships are **not** NetworkObjects. One custom batched snapshot at 10–15 Hz: ship id, quantized position, team, state flags; clients interpolate; fire/death events are separate reliable messages. Per-ship `NetworkTransform` at 200–1,000 ships is not viable.
- Deterministic lockstep was considered and rejected: the sim depends on `Physics2D`, `Time.deltaTime` and `Random`, so clients would drift. Command sync needs a fixed-step rewrite that is larger than the snapshot approach.
- Relay/Lobby free tier: verify current CCU/bandwidth limits before launch; a 2-player session should sit far below them, but WebGL is a different story.
- Mode rules live on the host: Solo/Co-op turn the AI on; Versus turns it off. In co-op both clients share control of one faction; orders from either are accepted for friendly planets.

### 4.4 Local input: gamepad first, dual mice later at most

- Input System exposes a single `Mouse.current`. Two physical mice on Windows require raw-input handling and two custom-drawn cursors — Windows-only, and extra work for one mode. Not in the first cut.
- P1: mouse as today. P2: gamepad virtual cursor (right stick), A confirm, B cancel, D-pad cycles planets, Start pause. Keyboard fallback for a second player without a pad.
- UI: planet selection must not rely only on `IPointerClickHandler`/EventSystem (single pointer). Use a camera raycast against planet colliders for the virtual cursor; keep EventSystem for mouse.
- `PlanetSelectionManager` becomes per-player (start planet and line per player) instead of one shared start planet.
- **Encapsulation:** all local pointers implement one interface, `IPointerSource` — `PlayerSlot Slot`, `Vector2 ScreenPosition`, `bool ConfirmPressed/CancelPressed`, `bool TryGetWorldPoint(Camera, out Vector3)`. Implementations: `MousePointer`, `GamepadCursorPointer`, `RawInputMousePointer`. `PlanetSelectionManager` and the hover/line code consume the interface only, so a second mouse is a new implementation, not a new selection system. The gamepad cursor is a prefab component (`VirtualCursor`) that moves a sprite and feeds the interface.
- Dual-mouse spike only if local play sticks: raw input plugin, two cursor sprites, device-to-cursor mapping. Windows-only, behind the same interface, and it must not leak into gameplay code. Precondition: device enumeration (two mice show up as distinct raw-input handles) and a custom cursor per device; the OS cursor stays single.

### 4.5 Settings: one runtime config

- `GameSettings` ScriptableObject holds defaults; `RuntimeSettings` holds the active values; overrides persist as JSON in `persistentDataPath`.
- Values are applied at match start for everything cached in `Awake`. Only a small set is live-adjustable (table in section 8). Some values (boid radii, damage) are read every frame and can be live; others (pool sizes, max units) need re-init.
- In online play the host owns settings; clients see them read-only.
- Options UI: main menu and pause menu, uGUI, matching the existing style. Plus a debug panel (F3) for experiments: spawn N ships, clear ships, capture selected planet, toggle AI.

## 6. Phases

### Phase 0 — Safety net
- You create `feature/upgrade` when ready. Until then the plan and all edits stay uncommitted; `git switch -c feature/upgrade` later carries them over.
- Add `docs/` plan, `.gitignore` check (`Library/`, `Logs/`, `TestResults/`).
- Add editor build script + headless test/build commands; record a baseline Windows build and a screenshot of both scenes.

Acceptance: baseline build runs from `main`.

### Phase 1 — Engine upgrade (first real work)
- Open with **6000.7.0b6**, let the upgrader rewrite project settings and record the diff.
- URP 17.7.0, Input System latest, DOTween DLL tested first and replaced only if it misbehaves.
- Fix compile errors, re-check both scenes, volumes, 2D lights, camera, UI scaling.
- Rebuild Windows player.

Acceptance: menu → game → menu works, no console errors, player build runs, headless test command returns 0 tests without errors.

**Result (2026-10-22): done.**
- Editor 6000.7.0b6; `ProjectVersion.txt` updated. Upgrader bumped 2D packages, URP 17.7.0, Input System 6.7.0, test framework 1.9.0, ugui 2.7.0, timeline 6.7.0.
- New `ProjectSettings/PhysicsCoreProjectSettings2D.asset` generated by 6.7.
- One compile break: `GetInstanceID()` is now an error. Migrated `IShipOwner.OwnerId` from `int` to `EntityId` and used `GetEntityId()`. No other code changes were needed.
- Scene smoke: Menu (5 roots) and SampleScene (11 roots), **0 missing scripts**.
- Windows build: **succeeded**, 143,774,424 bytes, `Builds/upgrade-smoke/Warmask.exe`.
- Player ran 15 s and shut down cleanly; no exceptions. D3D12 was denied by a machine-level device filter, so it fell back to D3D11 — unrelated to the project.
- Old DOTween DLL compiled and built; runtime path still unverified until menu/gameplay tweens are exercised.
- Remaining warnings for later phases: `OverlapCircleNonAlloc` obsolete (phase 7), `FindFirstObjectByType` deprecated (3 files), unreachable code in `ShipMaskFader`/`ShipInstance`, unused boid weight fields in `ShipInstance`.
- Uncommitted at this point: `docs/`, manifest/lock, new physics settings, `Assets/Editor/UpgradeSmoke.cs`, three code files, `Builds/`.

### Phase 2 — Bug fixes + first tests
Fix the verified bugs (section 5) and add tests around the rules. Behavior-changing fixes are called out and confirmed before landing:
- capture ownership must not bounce back;
- mask switch must not resize planets;
- if "troops arrive before they act" is wanted, add a travel-order state machine; otherwise document the instant-order behavior.

Also clean: unused `Die()`, the NUnit import in runtime code, the per-frame string allocation in the planet label.

Acceptance: tests green, both scenes still playable, no new warnings.

### Phase 3 — Settings + options screen
- `GameSettings`, `RuntimeSettings`, persistence, reset, presets.
- Options screen with Match / Combat / Visual / Audio / Controls tabs.
- Debug panel.
- Wire existing prefab values to settings without breaking prefab defaults.

Acceptance: max units, ship cap, production, spawn interval, damage and AI difficulty change from the UI and survive a restart; online clients cannot alter host rules.

### Phase 4 — Local two-player (gamepad first)
- `IPointerSource` abstraction + per-player selection, gamepad virtual cursor, keyboard fallback, pause handling.
- Co-op and Versus local.
- PlayMode tests for input routing; manual checklist for two devices.

Acceptance: two players can play a full match on one screen without touching the mouse for P2.

### Phase 5 — Second mouse spike (encapsulated)
- `RawInputMousePointer : IPointerSource` behind the existing interface, two cursor prefabs, device-to-cursor binding.
- Time-boxed. It is a spike: no gameplay code may reference raw input, and the default build keeps one mouse if the spike fails.
- Investigate first: whether Windows raw input distinguishes both devices under Unity, and whether the OS cursor can be hidden without breaking menus.

Acceptance: a written verdict with the component API; if it works, a prefab that can be dropped in without touching selection logic.

### Phase 6 — Combat and planet effects
(See section 7.)

Acceptance: effects pooled, no per-hit allocations, a toggle to disable for performance tests.

### Phase 7 — Ship scaling
- Spatial hash, single neighbor pass, remove forced job completion, targeting cooldown instead of per-frame raycast, LOD for trails/audio.
- Stress scene + profiler runs at 250 / 500 / 1,000 / 2,000 ships; numbers recorded in this file.
- DOTS spike only if the gate fails.

Acceptance: documented fps and GC at each count; default match unchanged in feel.

### Phase 8 — Online
- Unity project linked, Relay/Lobby enabled, anonymous auth.
- Main menu: Host (mode, private) / Join (code); lobby with ready state.
- NGO bootstrap, planet NetworkVariables, order RPCs, ship snapshots, events, disconnect handling.
- Co-op vs AI and 1v1; host settings lock.
- Two-instance testing via Multiplayer Play Mode.

Acceptance: two machines on different networks finish a co-op match and a Versus match; disconnect ends cleanly; no ship drift visible at 200 ships.

### Phase 8 — Polish
- Tutorial/onboarding for new modes, co-op pings, kill feed.
- Accessibility pass (colorblind-safe teams, icon/pattern on planet types, remappable keys).
- Perf pass, release build, CI command documented in README.

## 7. Bug list (verified by reading)

| # | Bug | Where | Action |
|---|---|---|---|
| 1 | Captured planet keeps old owner's ships, which count as attackers and flip it back | `PlanetInstance.cs:166-176`, `ShipyardInstance.cs:121-135` | fixed: old owner's in-orbit ships die with the planet; in-transit ships keep their owner |
| 2 | `Globals.OnMaskChanged` listeners never removed; duplicate callbacks after scene reload | `PlanetInstance.cs:76`, `UpdateUiBasedOnMask.cs:12`, `BackgroundColorChanger.cs:14` | fix + test |
| 3 | Mask switch scales planets (`planet_size * maskModifier`) | `PlanetInstance.cs:131-133` | fix + test |
| 4 | Turn rate reset hardcodes 90, ignoring the prefab value; kill bonus never reset | `ShipInstance.cs:158` | fix + test |
| 5 | Two overlap queries per ship per frame + `Schedule().Complete()` on main thread | `ShipManagerInstance.cs:158-201`, `ShipInstance.cs:302` | phase 4 |
| 6 | AI calls `MoveTroops(null, ...)` when no planet has spare ships | `EnemyAi.cs:86` | fix + test |
| 7 | `using NUnit.Framework` in runtime asmdef | `ShipyardInstance.cs:1` | remove |
| 8 | Instant troop transfer starts ground drain before ships arrive | `ShipyardInstance.cs:101-107` | decision |
| 9 | `Die()` dead code; label string allocated every frame; register double-counts cache | `ShipInstance.cs:357`, `PlanetInstance.cs:191`, `ShipyardInstance.cs:171-173` | cleanup in touched files |
| 10 | Sent ships kept occupying the origin's production cap; received ships did not count toward the destination's | `ShipyardInstance.cs:155-217` | fixed: cap counts own ships assigned to the planet, plus a fleet cap per player (owned planets × per-planet cap + `fleetCapBuffer`) |

## 8. Settings values

| Value | Scope | Live? |
|---|---|---|
| Max units per planet | all planets | restart/re-init |
| Ship cap per shipyard, pool size | shipyards | restart |
| Fleet cap buffer (owned × per-planet cap + buffer) | ship production | live |
| Production factor, spawn interval | planets | live |
| Ground unit drain rate | planets | live |
| Ship speed, turn rate, damage, fire cooldown, ranges | ships | live (new + existing) |
| Boid weights and radii | manager | live |
| AI decision interval, aggression weights | AI | live |
| Team colors, planet type colors | globals | live |
| Bloom/shake/particle density, volume | visuals | live |
| Master/music/SFX volume | audio | live |
| Match mode, map seed | match | restart |

## 9. Networking detail

- **Transport:** Unity Transport (UTP) over Relay; WebSocket for WebGL if that platform is chosen.
- **Host authority:** all rules (ownership, send validity, capture, damage) on host. Clients predict nothing except their own cursor/selection.
- **Planet sync:** `NetworkVariable<PlanetState>` per planet, dirty only.
- **Orders:** reliable RPC `SubmitTroopOrder(fromId, toId, count, playerSlot)`; host validates then executes the existing `TroopMovementManager`.
- **Ships:** unreliable batched snapshot (10–15 Hz), interpolation on clients with 100 ms buffer; reliable events for spawn/fire/death so effects are not lost. Snapshot budget target: ≤ 32 KB/s per client at 300 ships. Relevance culling by distance/importance if exceeded.
- **Co-op:** one faction, both players command it; orders tagged with player slot for the UI and pings only.
- **Versus:** separate factions; AI disabled; neutral planets keep spawning (they already do) and fight everyone.
- **Failure handling:** host quit = match over; client quit = ships stay, planet production continues; no reconnect in scope.

## 10. Testing

- **EditMode:** extract pure rules into testable statics/classes: `TroopSendRules` (how many to send), `CaptureRules`, `SettingsValidation`, `SnapshotCodec`, `AiTargetScoring`. Tests for capture stability, ownership validation, clamping, snapshot round-trip, versioned payload.
- **PlayMode:** scene boots, spawn/despawn/pool reuse, mask toggle does not resize, settings apply, local input routing (two simulated devices).
- **Not automated:** two-machine online play (manual checklist), visual effects quality.
- **CI:** headless EditMode tests and Windows build via an editor method. `-quit` is not combined with `-runTests`.
- Goal is coverage of rules, not a percentage.

## 11. My additional ideas

Beyond the brief: **fog of war** would do more for Versus than any ship count — it makes co-op communication and scouting meaningful, and it is cheap compared with netcode. **Commander abilities** (orbital strike, temporary shield, rally) add tactical depth without new units or art. **Ship veterancy** is already hinted at by the kill-based turn bonus; formalize it with ranks and chevrons and reset it on death. **Co-op differentiation**: share one faction but let each player own the planets they capture, with a ping system and a shared "high command" pool for abilities — this creates cooperation without splitting the map. **Pirate/neutral raids** make the existing neutral planets an active third party and give co-op a common enemy. **Mask-switch shockwave** and a short slow-motion on a planet capture sell the moment. **Save the command log** (orders only, tiny) and you get replays and a netcode debugging tool for free. **Colorblind-safe team identity** matters here because team colors and planet types already overlap in red/blue; add icons or patterns. **A sandbox toggle** (spawn 500 ships, disable AI) pays for itself in every tuning session. **Audio**: layered laser/hit/explosion pools with pitch variation and ducking would add more perceived quality than another particle system.

## 12. Open decisions

1. Co-op control: shared faction (recommended) or split ownership.
2. Versus at launch, or co-op first.
3. Windows only, or WebGL too.
4. Target ship count for online (300–500 is the sane band; 1,000+ needs relevance culling).
5. Keep matches at 2 players.

## 13. Risks

- **Upgrade + netcode + scaling on one branch** is a long-lived branch. Mitigation: phases land independently and each has an acceptance gate.
- **6.7 upgrader rewrites ProjectSettings and package manifests**; review the diff before committing.
- **Old DOTween DLL** may misbehave under 6.7; if it does, replace it in phase 1 rather than patching around it.
- **Unity Services setup** needs a linked Unity project and dashboard toggles; that is user-side work, not code.
- **Singletons and UnityEvents** (Globals, PlanetsManager, ShipManagerInstance) will fight netcode; expect refactors, not just additions.
- **Bug fixes change gameplay** (capture, mask scaling, instant orders); confirm each one.
- **Relay free tier / WebGL** limits should be checked before promising online play broadly.
- **DOTS** is a schedule risk disguised as a performance fix; the gate exists to prevent that.

## 14. Commands

```
# upgrade / open
"C:\Program Files\Unity\Hub\Editor\6000.7.0b6\Editor\Unity.exe" -projectPath "C:\Unity\global_game_jam_2026"

# editmode tests (no -quit)
"..."\Unity.exe -batchmode -runTests -projectPath "C:\Unity\global_game_jam_2026" ^
  -testPlatform EditMode -testResults TestResults\editmode.xml -logFile -

# windows build (editor method added in phase 0)
"..."\Unity.exe -batchmode -quit -projectPath "C:\Unity\global_game_jam_2026" ^
  -executeMethod Warmask.Editor.BuildCi.BuildWindows -logFile -
```

## 16. Warmask polish — active

User-visible polish pass. Code is not finished until each item is checked in a Play session.

- [ ] **Planet lighting (reset + planet-spot cursor light):** scene back to pre-session values; cursor light rebuilt from the planet's own light.
  - **Why lights "did nothing" before:** every light used Renderer2D blend style 0 = "Multiply"; white multiply light is a visual no-op (`white × sprite = sprite`). Also `m_UseNormalMap` is deprecated in URP 17.7 (migrates into `m_NormalMapQuality`) — flags still un-pin quality correctly.
  - **Reset done (user call: "too bright / bloom"):** the activated 6 "Sprite Light 2D" pool setup was reverted to HEAD values — inactive again, Multiply style, original intensities (10/9/8/6.06/9/6.06), radius 1, `m_UseNormalMap: 0`; Global Light 2D back to white `1.5` intensity. Only `Planet.prefab`'s "Spot Light 2D" keeps `m_UseNormalMap: 1` (used by the cursor light below; inactive on planets).
  - `Warmask.Planet/CursorLight.cs` now clones the **planet prefab's "Spot Light 2D"** (radius 3, cookie-free, smooth falloff, crater normals) instead of a scene point light: forced **Additive**, no cookie, player color from `Globals.GetPlayerColor`, intensity 0.8, radius 1.8 override, thin core. Same pointer world-point logic as selection. `Warmask.Planet.asmdef` gained `GUID:516a5277b8c3b4f4c8cc86b77b1591ff` (URP 2D runtime).
  - Knobs: intensity/radius fields on `CursorLight`; if star sparkles still read too bright *after* the reset, that is the pre-session baseline (Randomizer-driven) — separate tweak, ask first.
  - "Selection highlight" is a canvas UI Image, deliberately unlit — unchanged.
- [ ] **Planet HUD rework:** shield badge behind the count.
  - `Shield.png` sliced from the HUD sheet (128×151, luminance-alpha), assigned via `Globals.shieldIcon` in `Resources/Globals.asset`.
  - `PlanetInstance.EnsureShieldBadge()`: badge now sits **centered behind the count** (translucent white 45%), so the number reads inside the shield; hides whenever the label stops being a plain number (Under Attack / Falling / Victory / feedback). Known oddity to judge in game: the count label still slowly spins over the static shield — one line to drop if disliked.
- [ ] **Fleet HUD restyle** (was "Player1 ships 180/180" black bar):
  - `FleetStatusHud` now draws P1 top-left, P2 top-right, fontSize ~38, **only the number** in the player color; at fleet cap it alternates red/player color every 0.4s with a `MAX` suffix.
- [ ] **Play-test:** cursor glow subtle (no blowout, no flare haze), bump shading visible inside light pools, ambient dim reads okay, shield-number reads well (and spin?), HUD numbers large + correct, `MAX` blink at cap, planet clicks/cooldowns/ship feedback unchanged.
- [x] Remove temporary raw-input diagnostics. Done: `RawInputMouseRouter` device-list/first-input/left-down logs, `RawInputMousePointer` movement logs, `RawInputSpike` status logs removed; only error/exception logs remain. Final build + commit still pending (needs the user's editor).

## 15. Later backlog (agreed, not scheduled)

### More and larger maps, camera zoom
- Map layouts become data: a `MapDefinition` (planet positions, types, owners, sizes) or map prefabs, so adding a map is content, not code. First two or three hand-made layouts, procedural later.
- Larger maps need camera zoom and pan (mouse wheel plus gamepad stick/triggers), a fit-to-screen default, and a minimap. Zoom must be a camera concern only: all pointer code already converts screen→world through the camera, so `IPointerSource` keeps working unchanged.
- A minimap doubles as the co-op ping surface.
- Do this after ship scaling (phase 7): more planets means more ships, and online bandwidth scales with both.

### 3–8 players
- This is a data-model change, not a lobby setting. Today's model is hardcoded to two: `Globals.ePlayer` enum (None/Player1/Player2), a 3-entry color array, 2 owner indicators per planet, `EnemyAi` bound to P1/P2, and `IsPlayer` returning true only for P1.
- Replace with `PlayerId` (byte, 0 = neutral) plus `TeamId` and a per-mode team assignment. New code must never assume exactly two players; the pointer slot is already an id, not an enum.
- Needs: 8+ distinguishable team colors plus patterns/icons (colorblind), lobby size, host + 7 clients over Relay, co-op teams vs free-for-all vs team modes, spectating, 8 spawn positions, and per-player ship caps.
- After online 2-player works. Designing the id-based data model earlier is cheap; implementing 8-player is not.

### Art pipeline
- I cannot generate raster art. I can generate placeholder shapes and integrate whatever you produce.
- For nanobanana output: PNG with alpha, one sprite per file (or a sheet with a documented grid), consistent pixels-per-unit (e.g. 100 px = 1 world unit), centered pivot, no baked shadows where 2D lights should apply, and 2x resolution for zoom. Team-colored parts should be neutral/white so tinting works.
- Easy wins for effects: soft circles, streaks, rings, sparks and smoke puffs as textures; sprite sheets for explosions and impacts.
- Delivery: drop into `Assets/Warmask.Content-Assets/`; I write the import presets (sprite mode, PPU, compression, mipmaps) so every asset lands consistently.
