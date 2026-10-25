# Performance Findings

**Last updated:** 2026-10-25  
**Engine:** Unity 6000.7.0b6, URP 17.7.0  
**Target:** Web build, **WebGPU first with WebGL2 fallback**  
**Goal:** stable 60 FPS with **300–500 ships** (the original 1,000-ship gate was
replaced after the Web target was confirmed).

---

## TL;DR (verified 2026-10-23)

- **DOTS / Entities Graphics was reverted.** Entities Graphics does not support
  Web platforms, for either WebGL2 or WebGPU. The 117 FPS render spike was a
  desktop-only experiment and cannot ship.
- **Ship simulation is one manager tick** with a Burst boid job and a native
  spatial hash. Ships no longer have colliders or their own `Update` /
  `LateUpdate`.
- **500 ships:** ~100–103 avg FPS in the Windows editor after the 2026-10-24
  fire changes (was ~72–78), ~2.0 ms/frame for the entire ship simulation.
- **The ~35 FPS firing cost no longer reproduces.** After mask-aware fire visuals
  and the 10 Hz line-of-fire throttle, firing-off runs are *slower* than baseline
  in the same batch, i.e. the residual cost is below the harness's noise.
- **Visual fix:** the magenta ships were caused by a missing `Sprite-Lit-Default`
  material on the ship prefab (a null material renders magenta in URP 2D). It is
  restored; trails now use player colours.
- **Task feedback (2026-10-25):** ships with a move order or an enemy target get
  a long, full-width trail and an HDR-bright hull (player colour × 1.5, bloom
  picks it up). Idle ships keep a visible 1 s trail at half width and 20% alpha,
  so they still read as moving but do not compete with the action. Trail state,
  width and HDR brightness are covered by
  `IdleShip_UsesShortTrail_AssignedShip_UsesLongTrail`.
- **Tests:** 22/22 PlayMode tests pass, including mask-aware firing,
  full-speed coarse ticking, the trail/HDR state test and the dev-overlay smoke test.
- **Browser numbers need a Development Build.** A dev-only overlay
  (`FrameStatsOverlay`) reports FPS, CPU/GPU frame time and GC allocations
  in the player; it is compiled out of release builds.
- Editor numbers are not browser numbers. A WebGPU build profile is still
  pending.

---

## Target and platform constraints

| Constraint | Verified fact | Consequence |
|---|---|---|
| Web graphics API | Unity 6.7 defaults to **WebGL2**. WebGPU is opt-in and has limited browser/device support. | Project now declares `WebGPU, OpenGLES3` so WebGPU is used when available and WebGL2 is the fallback. |
| Entities Graphics | Package compatibility table: **Web = Not supported** (both APIs). | No ECS rendering. `com.unity.entities`, `com.unity.entities.graphics` and `Assets/DotsTest` were removed. |
| Threads | `webGLThreadsSupport` is **off**. | Jobs/Burst still compile, but run on the main thread. No worker-thread speedup. |
| WebGPU compute | Compute exists but has no synchronous GPU readback and no async compute. | A GPU ship sim would add 1+ frame latency and is not worth it for 500 ships. |
| Testing | Editor measurements use `Time.unscaledDeltaTime`. | Editor FPS is a proxy only; a WebGPU/WebGL2 build must be profiled in a browser. |

---

## Current architecture

1. **One manager tick.** `ShipInstance.Update`, `ShipInstance.LateUpdate` and
   `ShipMaskFader.Update` were removed. `ShipManagerInstance.LateUpdate`:
   snapshots positions/velocities/player ids → builds the spatial hash → builds
   neighbour lists → runs the Burst boid job → calls `ShipInstance.Tick` for
   every ship → processes deaths in a separate pass.
2. **No ship colliders.** The prefab's `BoxCollider2D` was removed. It had no
   `Rigidbody2D`, so every moving ship forced a Physics2D static-broadphase
   rebuild. All ship-to-ship detection and firing now use the spatial hash;
   friendlies still block shots.
3. **Native spatial hash.** Replaced `Dictionary<int, List<int>>` (one managed
   `List` allocation per occupied cell per frame) with a reusable
   `NativeParallelMultiHashMap<int,int>`.
4. **Allocation-free combat visuals.** Player laser gradients are cached
   statically, hit flash uses a timer instead of a coroutine, trails are cached
   once per ship, laser audio is globally throttled, and laser visibility is
   tracked so `LineRenderer.enabled` only changes on state transitions.
5. **Mask culling.** `ShipMaskFader.Tick` drives visibility from the current
   mask. Fully transparent ships disable their `SpriteRenderer` and
   `TrailRenderer`. The per-ship `OnMaskChanged` subscription (500 delegate calls
   per mask switch) was removed, and fades update at 20 Hz instead of every
   frame.
6. **AI tuning.** `aiUpdateInterval` is 0.5 s in the prefab, staggered per ship.
   Ships only fire while in the `Engage` state.
7. **Player-coloured visuals.** Sprites, trails and lasers use the player colour.
   `ApplyPlayerVisuals` runs on `SetPlayerId`, `SetType` and `OnEnable`, with
   lazy component caches so pooled ships are covered.
8. **Mask-aware firing (2026-10-24, verified).** Off-mask
   ships keep simulating and fighting, but their laser, audio and hit flash are
   suppressed (`ShipInstance.FireWeapon`); previously they fired full-opacity
   lasers from invisible ships. Line-of-fire queries are throttled to 10 Hz,
   staggered (`FireQueryInterval`), instead of every frame while off cooldown.
   Local MP no longer overrides `inactiveShipAlpha`; off-mask ships are hidden
   exactly as in solo. Verified by
   `OffMaskShip_KeepsFighting_WithoutVisibleLaser` (damage dealt, no laser).
9. **Off-mask coarse ticking (2026-10-24, verified).** Ships that are not on the
   active mask move at 30 Hz with the accumulated `dt` instead of every frame,
   and re-target at half the AI interval (`ShipProfiling.EnableOffMaskCoarseTick`,
   on by default). They keep full speed, firing, damage and death. At 500 ships
   this cuts the ship tick phase by ~18% (0.96 → 1.17 ms average across the 5 s
   window; every 2 s sample 16–19% lower). Boid neighbours are still collected
   for off-mask ships, so spacing behaviour is unchanged.

### Current tuning values (ship prefab)

| Field | Value |
|---|---:|
| Manager `neighborRadius` (SampleScene) | 1 |
| Manager `separationDistance` (SampleScene) | 0.5 |
| Manager `enemyAvoidanceRadius` (SampleScene) | 2 |
| Manager `maxNeighborsPerShip` (SampleScene) | 5 |
| Ship prefab boid fields (`neighborRadius`, `separationDistance`, `enemyAvoidanceRadius`) | removed 2026-10-25 — unused after the manager rewrite |
| `detectionRadius` | 3 |
| `weaponRange` | 2 |
| `fireCooldown` | 0.25 s |
| `aiUpdateInterval` | 0.5 s |
| `maxDistanceFromTarget` | 3 (was 4; live battle measured max 3.27 units from the nearest planet) |
| Idle trail | 1 s, width ×0.5, start alpha 0.2 |
| Assigned trail | 5 s, width ×1, start alpha 0.75 |
| Assigned hull | lerp 0.35 toward white, then ×1.5 HDR for bloom |

---

## Current performance

### Editor, PlayMode, 5 s measurement window, damage disabled

| Scenario | Avg FPS | Min FPS | Max FPS |
|---|---:|---:|---:|
| 500 ships, **before** AI interval/stagger/audio/laser fixes | 64.4 | 19.9 | 83.3 |
| 500 ships, after fixes (run 1) | 78.2 | 45.8 | 94.4 |
| 500 ships, after fixes (run 2, phase logging on) | 77.7 | 37.3 | 93.7 |
| 500 ships, after fixes (run 3, mask/fade change) | 76.6 | 43.5 | 93.5 |
| 500 ships, after fire changes (2026-10-24, batch 1) | 100.1 | 56.7 | 133.5 |
| 500 ships, after fire changes (batch 2, same order) | 103.5 | 55.8 | 128.9 |
| 300 ships | 82.7–92.7 | 36.8–46.3 | 127.6–131.5 |

Run-to-run variance in the editor is significant (±5 FPS typical, larger on
min FPS). Treat these as trends, not exact benchmarks. Within one test job the
editor also drifts: each successive 5 s scenario in a batch was up to 13 FPS
slower than the previous one, so cross-scenario deltas smaller than that are
not signal.

### Ablations at 500 ships

Indicative single runs from **before** the final laser `enabled` and trail
colour changes. Baseline in this batch was ~78 avg.

| Disabled feature | Avg FPS | Interpretation |
|---|---:|---|
| Firing (detection kept) | 113.0 | Firing (laser + audio + flash + ray query) is the dominant cost, ~35 FPS. |
| Targeting (detection + firing) | 112.0 | Detection alone is cheap; the firing path drives the delta. |
| Boid steering | 81.2 | Boid steering costs ~3 FPS. It is **not** free (the old “0%” claim was invalid). |
| Rendering (sprites) | 75.2 | ~3 FPS lower; inconclusive in a single run. Sprite batching is not an obvious bottleneck. |
| Trails | 74.2 | ~4 FPS lower; inconclusive in a single run. Trail mesh updates remain a candidate for batching. |
| Audio | 70.2 | Noisy; the global audio throttle is already active. |

The `NoLaserVisuals` and `NoHitFlash` sub-ablations ran on 2026-10-24, after the
fire changes. Two batches, same execution order, 500 ships:

| Disabled feature | Batch 1 avg | Batch 2 avg |
|---|---:|---:|
| *(baseline)* | 100.1 | 103.5 |
| Laser visuals | 99.5 | 101.6 |
| Hit flash | 96.8 | 101.2 |
| Firing (query + laser + audio + flash) | 91.0 | 93.7 |
| Audio | 87.5 | 95.3 |

**These do not split the firing cost.** Every batch runs its scenarios in
sequence and each successive scenario is slower (up to 13 FPS drift), which is
larger than any toggle delta. The physically impossible result — disabling
firing measuring *slower* than baseline — is the tell. What the batch does show:
the old ~35 FPS firing gap is gone, consistent with the throttle removing the
per-frame spatial query. Laser and hit flash now cost less than the noise floor;
do not optimize them further without a better harness (alternate configs inside
one measurement window, or measure on a build).

### Manager phase cost at 500 ships

`ShipProfiling.LogPhaseTimings`, measured before the final trail-colour change
(which does not run per frame):

| Phase | ms/frame |
|---|---:|
| Snapshot | 0.03 |
| Spatial hash | 0.03 |
| Neighbour lists | 0.38 |
| Boid job | 0.07 |
| Ship tick (AI, movement, firing, masks) | 1.5 |
| Death pass | 0.006 |
| **Total** | **~2.0** |

Coarse off-mask ticking (`Diagnose500Phases` A/B, 2026-10-24): the tick phase
drops from 0.81/1.06/1.64 ms to 0.68/0.86/1.34 ms in the three 2 s samples
(16–19% lower, ~0.2 ms/frame). Snapshot, hash, neighbours and boid are unchanged.

### Profiler phase markers (2026-10-24)

`ShipManagerInstance.LateUpdate` now emits six markers: `Ship.Snapshot`,
`Ship.SpatialHash`, `Ship.Neighbors`, `Ship.BoidJob`, `Ship.Tick`,
`Ship.Deaths`. They appear in the editor Profiler's **Scripts** area and are
readable as counters:

```
manage_profiler  action=get_counters  category=Scripts
  counters=[Ship.Snapshot, Ship.SpatialHash, Ship.Neighbors,
            Ship.BoidJob, Ship.Tick, Ship.Deaths]
```

Spot check at 139 ships (one frame, spawner still ramping, not averaged):
snapshot 7.3 µs, hash 11.1 µs, neighbours 83 µs, boid job 46 µs, tick 147 µs,
deaths 1.4 µs — ~0.3 ms total, in line with the per-500-ship table above.

The markers are defined once in `ShipProfiling.Phases`; `LateUpdate` wraps each
phase in `using (… .Auto())`. **To remove them:** delete that nested class and
the six `using` scopes. Release cost is negligible — Unity compiles `Auto()` to
return `null` in non-development builds.

Use `ProfilerMarker`, not `Profiler.BeginSample`: `BeginSample` records into the
CPU timeline but does not register a `ProfilerRecorder` counter, so
`get_counters` reports it as invalid (verified).

### Profiling a browser build

**Current build (2026-10-25):** `Builds/WebGPU` — Web target, Development Build,
Deep Profiling off, Script Debugging off. The project declares
`WebGPU, OpenGLES3`, so WebGPU is used when the browser supports it and WebGL2
is the fallback. Open `Builds/WebGPU/index.html` through the usual local server.

**Build settings:** `Web - Desktop - Development` profile, Development Build on,
**Deep Profiling off**, **Script Debugging off**. The release profile has no
overlay by design.

- **Why Script Debugging stays off:** it selects the Debug managed code variant,
  which disables code optimization; numbers from that build are not
  representative. In this project's Web Development profile
  (`m_AllowDebugging: 0`) the player scripts still compile with `DEBUG`,
  `DEVELOPMENT_BUILD`, `ENABLE_PROFILER` and `UNITY_ASSERTIONS`, but **not**
  `UNITY_INCLUDE_INSTRUMENTATION` (verified in the Bee `.rsp` for
  `Warmask.Core`). So `FrameStatsOverlay` is gated on
  `#if DEBUG || UNITY_EDITOR`. The managed-code-variants manual table does not
  match this build; trust the `.rsp`, not the table.
- **Deep Profiling** instruments every script method, uses a lot of memory, and
  makes the app significantly slower; the manual recommends it only for small
  games. It is the right tool for "which method inside `Ship.Tick`", not for
  frame numbers. Leave it off unless a gap in the markers needs explaining.
- The overlay is created automatically and shows FPS, CPU/GPU frame ms,
  main/render thread ms, GC KB/frame and managed MB, refreshed every 0.5 s.
- **WebGL2 cannot report GPU time** (`FrameTimingManager` is "partial: GPU Frame
  Time measurement is unsupported"), so `GPU` reads `n/a` on the fallback path.
  `Frame Timing Stats` is enabled in Player Settings for player builds.

### Deep profiling: what one captured frame was good for (2026-10-24)

One deep-profiled WebGL frame (frame 962, **107 `ShipInstance.Tick` samples**,
28.6 ms / 35 FPS) was captured before the clean build replaced it. Deep profiling instruments every
call, so the **absolute times are inflated by orders of magnitude**: the
`Ship.Neighbors` marker read 10.2 ms in this frame but 83 µs at 139 ships in the
editor. Use the **call counts and ranking**, not the milliseconds.

Script self time: ~9.2 ms of the 28.6 ms frame.

| Method | Self (deep) | Calls |
|---|---:|---:|
| `ShipManagerInstance.BuildNeighborLists` | 3.1 ms | 1 |
| `ShipManagerInstance.GetPlayerShipCount` | 1.8 ms | 6 |
| `ShipyardInstance.UpdateProximityShipCache` | 0.8 ms | 9 |
| `ShipInstance.IsAlive` | 0.6 ms | 1,851 |
| `ShipyardInstance.CanTransferShip` | 0.5 ms | 181 |
| `Globals.Instance` | 0.5 ms | 559 |
| `ShipInstance.UpdateMovement` | 0.3 ms | 106 |
| `ShipInstance.Tick` | 0.3 ms | 107 |

What it surfaced:

- `BuildNeighborLists` is the dominant ship-sim phase, so the six markers cover
  the right loop.
- `FleetStatusHud.OnGUI` calls `GetPlayerShipCount` twice per GUI pass
  (6 scans/frame here). Each scan walks every ship, so at 500 ships that is
  ~3,000 ship checks per frame for one text label.
- Each shipyard runs `UpdateProximityShipCache` every frame; 9 shipyards, each
  walking its `ownedShips` to feed the planet labels.
- `CanTransferShip` reads `Globals.Instance` twice per call, and
  `Globals.Instance` does a Unity null check on every access. The frame had
  ~2,600 `Object.op_Implicit` / ~3,100 `CompareBaseObjects` calls. Deep
  profiling inflates their cost; the counts are real.
- These HUD/shipyard costs are outside the `Ship.*` markers. The markers are a
  ship-sim budget, not a whole-frame budget.

Caveat: one frame at 107 ships. Re-measure in a non-deep build before changing
code.

**Build blocker fixed (2026-10-24):** `RawInputMouseRouter.cs` had its `#endif`
inside the class, so the WebGL player compile failed with `CS1022` while the
editor compiled fine. The guard must end after the class brace, as in the two
sibling RawInput files.

The ship simulation is ~2 ms at 500 ships. Almost all remaining frame time is
rendering, firing effects/audio, and other scene systems (planets, UI, scene AI,
2D lights).

---

## Visual correctness findings

### Magenta ship sprites (missing material)

**Symptom:** every ship sprite rendered magenta/violet, while trails still had
their own colours.

**Cause:** the ship prefab’s `SpriteRenderer` had `m_Materials: - {fileID: 0}`.
It used to reference URP `Sprite-Lit-Default`
(`a97c105638bdf8b4a8650670310a4cd3`), but the reference was lost. URP 2D renders
a null sprite material with its magenta error shader. The trail had its own valid
`Trail.mat`, which is why the trails were unaffected.

**Fix:** restored `Sprite-Lit-Default` on `Ship-Sprite` in
`Assets/Warmask.Ship/Prefabs/Ship-Prefab.prefab` (the same material the planet
prefab uses). Verified visually; `TrailsAndSpritesUsePlayerColor` now also
asserts that the sprite has a material and that it is not an error shader.

**Lesson:** in URP 2D, a null `SpriteRenderer` material does not mean “use the
default”; it renders the magenta error shader. Check `m_Materials` after any
prefab edit done by script or YAML.

### Violet/green trails (type tint)

**Symptom:** trail colours did not match the player colour.

**Cause:** `Globals.trailInPlayerColor` is `false`, so `ShipMaskFader` set
`TrailRenderer.startColor` to the **planet/type colour**. TypeB is violet. A
player-coloured gradient was also assigned, so the rendered trail was
`player colour × type colour` (yellow-green for P1, violet for P2). Some pooled
or newly spawned ships missed the gradient and kept the prefab’s white trail.

**Fix:**

- Trails use the **player colour** via `startColor`/`endColor`; the mask fader
  only drives alpha and preserves the RGB.
- `ApplyPlayerVisuals` re-runs after `SetType` and on `OnEnable`, with lazy
  `GetComponentsInChildren` caches so pooled ships are covered.
- Added `TrailsAndSpritesUsePlayerColor` PlayMode test. **Passing.**

### `ShipTypeIcon` (removed)

A runtime-only `SpriteRenderer` created by
`PlanetInstance.UpdateShipTypeIcon`:

- Small default ship sprite in the planet’s lower-left corner, tinted with the
  planet’s type colour.
- **Nothing else referenced it** — no prefab/scene, no other script. Only
  `PlanetInstance` created and updated it.
- It rendered black because it copied the planet’s **lit** material but was on
  the UI sorting layer, which is excluded from the 2D cursor light.
- Redundant with the planet’s type colour, so it was removed.

If a ship-type badge is wanted later, use an unlit sprite material.

---

## Adaptive Performance

- `com.unity.modules.adaptiveperformance` is in `Packages/manifest.json` (built-in
  module, direct dependency).
- URP has `m_UseAdaptivePerformance: 1` in `Assets/Settings/UniversalRP.asset`.
- The Web build profile has `m_AdaptivePerformanceEnabled: 0`.
- **No provider package is installed** (Samsung/Android/console providers are
  separate packages).

Adaptive Performance is a framework for providers to report thermal/performance
state and for scalers to adjust quality. It is not new (around since 2019) and
targets mobile/console throttling. WebGL2/WebGPU expose no thermal API, so it
will not improve FPS here. Leave it off; explicit quality budgets (ship count,
trails, firing effects, audio) are more useful.

---

## Tests and harness

### `ShipStressSpawner`
`Assets/Warmask.Shipyard/Runtime/Stress/ShipStressSpawner.cs`

- Spawns a deterministic ship count through the real shipyards via
  `SpawnShipUnchecked()` (capacity checks bypassed).
- Warmup + fixed 5 s measurement window.
- Feature toggles for boid, trails, lasers, audio, sprites, hit flash, targeting,
  firing, damage, and phase logging.
- Damage is disabled in perf runs so the count stays stable.

### `ShipPerformanceTests`
`Assets/Tests/ShipPerformanceTests.cs`

- 300 ships, 500 ships.
- Ablations: trails, targeting, firing, audio, laser visuals, hit flash,
  rendering, boid.
- `Diagnose500Phases` enables `ShipProfiling.LogPhaseTimings`.

### `ShipBehaviorTests`
`Assets/Tests/ShipBehaviorTests.cs`

- Manager ticking without per-ship `Update`/`LateUpdate`.
- Spatial-hash targeting without colliders.
- Friendly ship blocks line of fire.
- Player-coloured sprites and trails.
- Mask culling disables sprite/trail renderers.
- Deferred death unregisters the ship.

### Status

- **22/22 PlayMode tests passed** on 2026-10-25 after the task-glow, coloured
  planet numbers and leash-3 changes. `Measure500Ships` still passes its FPS
  gate; the full suite takes ~98 s.
- **If every C# phase suddenly costs 5–20× more, check
  `ProfilerDriver.deepProfiling`.** It persists across domain reloads and made
  the 500-ship suite measure 27 FPS with 4–9 ms neighbour lists. Turning it off
  restored 83.6 FPS with no code change. `ProfilerDriver.enabled` and
  `profileEditor` are not enough.
- `OffMaskShip_MovesAtFullSpeed_WithCoarseTick` passes; it was red-checked by
  temporarily moving at `dt` per coarse step (half speed) and failed as intended.
- `Ablate500_NoLaserVisuals` and `Ablate500_NoHitFlash` ran on 2026-10-24; see
  the ablation table for why they are inconclusive.
- `OffMaskShip_KeepsFighting_WithoutVisibleLaser` **passes**: the off-mask
  shooter deals damage while never enabling its laser.
- `Ship_Testscene` has missing prefab references and was removed from the build
  scene list; tests use `SampleScene`.

---

## Historical findings (superseded / corrected)

These were measured before the Web target and the 300–500 ship focus. They are
kept for context, with the incorrect claims marked.

| Old claim | Status |
|---|---|
| “`Physics2D.OverlapCircle` per ship per frame was the main bottleneck” | **Partly incorrect.** Detection ran `OverlapCircle` on the 0.1 s AI interval inside `ShipInstance`, not every frame. Firing ran a `Physics2D.Raycast` every frame until the cooldown gate. Removing the colliders also removed the moving-static-collider broadphase cost. |
| Original baseline: 87.9 avg / 42.1 min at 172 ships | Valid historical measurement, but capped at ~180 ships by production. |
| Spatial hash: +9.3% | Kept. Measured at ~180 ships. |
| Targeting cooldown: +1.8% | Kept. The per-frame raycast is gated by `fireCooldown`. |
| AI tuning: +4.4%, `aiUpdateInterval` 0.1 → 0.5, `detectionRadius` 10 → 8 | **Incorrect.** The prefab still had `aiUpdateInterval: 0.1` and `detectionRadius: 3`; SampleScene serializes `maxNeighborsPerShip: 5` (not 10). The 0.5 s interval was applied on 2026-10-23. |
| Explosion pooling: +1.9% | Kept. Explosions are infrequent. |
| Async boid job: reverted | Correct; one-frame steering delay caused oscillation. |
| Staggered boid updates: reverted | Correct. |
| Instanced material: “kept, no harm” | **Unused.** `Ship-Instanced.mat` has no references; the prefab uses the default sprite material. Safe to delete. |
| Radius reduction: reverted | Correct; caused clumping. |
| Boid disabled = ~0% | **Invalid.** `ShipProfiling.EnableBoidSteering` was not wired to any code, so the test measured a no-op. New measurement: no-boid 81.2 vs ~78 baseline (~3 FPS). |
| DOTS spike: 117 FPS at 1,000 entities | Desktop-only. Entities Graphics does not support Web; packages and spike were removed. |
| 60 FPS at 1,000 ships / ~18 FPS Mono projection | **Superseded.** The projection was extrapolated from ~180 ships and was not reliable. Target is now 60 FPS at 300–500 ships. |

---

## Remaining work

1. **Profile a WebGPU/WebGL2 build in a browser.** Editor numbers are not
   representative, especially with WebGL2 fallback.
2. **Firing cost is no longer the dominant delta** after the 10 Hz query throttle
   and mask-gated visuals (see ablations). Re-check in the browser profile before
   adding fire-rate budgets or batched lasers.
3. **Batch or cap trails** if trail mesh updates show up in the browser profile.
4. **Investigate low-min-FPS spikes** (GC from non-ship systems, mask changes,
   scene AI/UI).
5. Decide whether to remove the Adaptive Performance module. The unused
   `Ship-Instanced.mat` and the dead per-ship boid fields are already removed.

### Low-hanging performance tasks (ranked, 2026-10-25)

These are known per-frame costs that are cheap to fix and do not need a browser
profile first. None of them touch ship simulation behaviour.

| # | Where | What it does today | Cheap fix | Why it matters |
|---|---|---|---|---|
| 1 | `FleetStatusHud.OnGUI` (`Assets/Warmask.Shipyard/Runtime/FleetStatusHud.cs:46-50`) | Calls `GetPlayerShipCount` twice per GUI pass. Unity runs OnGUI at least twice per frame (Layout + Repaint), so 4 full ship scans/frame. | Cache both counts and refresh at 4 Hz (`Time.unscaledTime`), independent of GUI passes. | At 500 ships this is ~1,000–2,000 ship checks per frame for two text labels. |
| 2 | `ShipyardInstance.UpdateProximityShipCache` (`ShipyardInstance.cs:221`) | Runs every frame for every shipyard and walks its `ownedShips` to rebuild `shipCountCache`. | Throttle to 0.1–0.2 s, or mark dirty when a ship spawns/dies/enters/leaves. | Nine shipyards walk overlapping ship lists every frame just to feed labels. |
| 3 | `PlanetInstance.Update` under-attack branch (`PlanetInstance.cs:219-224`) | Rebuilds the `"Under Attack! (X vs Y)"` rich-text string every frame. | Cache the last under-attack text/target; rebuild only when owner, attacker or counts change. | Removes per-frame string/GC churn on every contested planet during battles. |
| 4 | `Globals.Instance` accesses | Unity null check on each property read; ~2,600 reads in one deep-profiled frame. | Cache the instance in the few hot loops (`ShipInstance`, `ShipyardInstance`). | Micro unless a profile shows it; the counts are real, the deep timings are inflated. |

**Are trails the cost?** Not known to be. The editor trail ablation was ~4 FPS,
inside the run-to-run noise. Idle ships now keep only a 1 s trail at half width,
so most ships build far fewer trail vertices than the old fixed 5 s trail; only
tasked ships still use the full 5 s. Do not batch trails until a browser profile
shows trail mesh updates in the frame. The known expensive parts are the ship
tick (~1.5 ms of ~2 ms ship simulation at 500 ships), rendering/effects/audio,
and the scene systems above — not the trails.

### Next session checklist

- [x] Run `Ablate500_NoLaserVisuals` and `Ablate500_NoHitFlash` (2026-10-24;
      inconclusive, see the ablation note).
- [x] Run `OffMaskShip_KeepsFighting_WithoutVisibleLaser` — passes.
- [x] Re-run the full PlayMode suite — 21/21 pass.
- [x] Add per-phase profiler markers (`Ship.*`, 2026-10-24); suite re-run 21/21.
- [x] Prototype coarse ticking for off-mask ships — shipped behind
      `ShipProfiling.EnableOffMaskCoarseTick`; tick phase ~18% cheaper, full
      speed verified by test.
- [ ] Create and profile a WebGPU build (with WebGL2 fallback) in a browser.
- [ ] Check trails in the browser profile; batch/cap only if they matter there.
- [ ] Investigate the remaining min-FPS spikes.
- [x] Cache-free cleanup: remove the unused `Ship-Instanced.mat` and the dead
      per-ship boid fields (`neighborRadius`, `separationDistance`,
      `enemyAvoidanceRadius`). Done 2026-10-25; suite 22/22.
- [ ] Optional: decide whether a planet ship-type badge is wanted; the old
      `ShipTypeIcon` was removed because it rendered black and was redundant.
- [ ] Optional: throttle `FleetStatusHud` / shipyard ship-count scans and cache
      the under-attack label string (see low-hanging table).
