# Enemy count, match speed and pause — design (2026-10-27)

Branch: `vibe-coding-4-players`. Small change: one menu control, one in-match
clock, one material fix.

## Intent

- Pick how many AI opponents share the map: 1, 2 or 3 (2–4 player matches).
- Pause a match, and play it faster or slower.
- Remove the black squares around explosion debris.

## Non-goals

- Six or eight players. Needs a map with more than four homes and a HUD that wraps;
  noted in `docs/upgrade-plan.md` ideas instead.
- Local humans in slots 2–4 (M2) and raw mice (M3).
- Per-player masks.

## Approaches

**Enemy count.** (a) One integer slider in the menu, 1–3, writing
`Globals.playerCount = enemies + 1`. (b) Three mutually exclusive toggles.
(c) A dropdown. Chosen: **a** — the menu is already toggle-based, a slider is one
component and maps straight onto the existing `playerCount`, which the HUD, the
AI cloning and the win rule already read.

**Speed and pause.** (a) In-match controls: a small strip with a pause button and
a speed stepper (0.5×, 1×, 1.5×, 2×), plus `P` and `[`/`]` keys, driving one
`Time.timeScale` value. (b) Menu-only speed setting, no pause. (c) Continuous
sliders. Chosen: **a** — pausing is a mid-match action, and one timescale already
drives ships, planets, AI and DOTween; the HUDs refresh on unscaled time so they
stay live. Space is already the mask toggle, so pause uses `P`.

**Debris squares.** Keep the explosion prefab's material and look, but move it from
the built-in `Legacy Shaders/Particles/Additive` to URP `Particles/Unlit` in
additive mode. The legacy shader does not blend correctly under URP, so each particle
quad rendered as a dark square around the triangle. No code, no new texture.

**Input fixes (same batch).** Right-click sampled the planet only on release, so a
small drift or a late frame lost it; now it records the planet and screen position on
press, accepts release within 0.5 s and 12 px, and still lets a right-drag pan. A plain
left click on an unselected owned planet while a selection exists sends an order there,
so reinforcing your own planet keeps working; Shift-click adds or removes a planet, a
click on an already selected planet deselects it, and a box drag on empty space selects
several. Sending iterates every selected source. Right-click still sends to any planet,
including your own.

## How it works

- **Slider** → `MapMenuSettings.SetEnemyCount(float)` rounds and clamps to 1–3 and
  sets `Globals.playerCount`. Default 1 keeps today's match.
- **Slot setup** — `FourPlayerSetup` keeps its name and slots, gains a `spareHomes`
  array and walks it for every player after P1. Three players: NW/SE/NE owned, SW
  neutral; clones the scene's `EnemyAi` for each AI slot.
- **Clock** — `MatchClock` on the GameCore scene: `P` toggles pause, `[`/`]` step
  speed; OnGUI draws a bottom-centre strip (PAUSED / 1×) with clickable buttons.
  `Time.timeScale = paused ? 0 : speed`; restores 1 on disable and destroy so the
  menu and the next match are never stuck. `BlocksPointer` keeps the selection
  manager from treating clicks on the strip as map clicks.
- **Win rule, HUD** already count-driven; no change.

## Constraints

- A two-player match must feel and look exactly as today.
- Pause must not break input, the HUD, or the end screen.
- `Time.timeScale` restore is mandatory (leaving the scene, returning to the menu).

## Risks

- At 2× ships move far per frame; collisions are frame-based, so contacts may be
  missed. If it feels wrong, cap at 1.5×.
- `WaitForSeconds` effects (explosion cleanup, planet feedback) freeze while paused —
  accepted.
- The frustrated player may read "speed 0.5×" as lag; the strip always shows the
  current value.

## Verification

- PlayMode tests: slider clamps 1–3 and keeps the 4 Players toggle in sync; a
  three-player match owns NW/SE/NE, leaves SW neutral and runs two AIs; `GameCore`
  forces `LargeMap` for more than two players.
- PlayMode tests: pause sets `Time.timeScale` 0 and restores 1 on resume and destroy;
  speed steps 0.5×–2× and stops at both ends; the strip blocks map clicks; the
  `GameCore` scene actually carries the clock.
- PlayMode tests: right-click keeps working after a small drift and after a second own
  planet is clicked; box drag selects several owned planets; sending iterates all.
- Play mode: slider set to 3 shows "Enemies: 3" and `playerCount` 4.
- Visual: an explosion rendered at 0.15 s, 0.35 s and 0.7 s shows the intended additive
  glow with no dark quads. The same frames with the old legacy shader showed dull,
  dark-edged particles, which is the artifact being fixed.
- Full PlayMode suite: 62/62.
