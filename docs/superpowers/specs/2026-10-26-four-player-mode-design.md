# Four-player mode — design sketch (2026-10-26)

Branch: `vibe-coding-4-players`. Sketch first, milestones below; not built yet.

## Intent

Play a Warmask match with four slots on `LargeMap`. Each slot is either a human
(local) or an AI. The user asked for three shapes: 1 human vs 3 AIs, a mix of
humans and AIs, and all-human.

## What exists today

- `Globals.ePlayer` = `None, Player1, Player2`. `playerColors` and `playerSprites`
  are arrays indexed by `(int)player` (3 and N entries).
- `Globals.IsHuman` = `Player1 || (localMultiplayer && Player2)`.
- `GameSession.PlayerCount = 2` and `MapScene` (default `LargeMap`).
- `LocalMultiplayerBootstrap.Join` builds P2 on demand: root object, line renderer,
  pointer source (gamepad or keyboard), `PlanetSelectionManager.Initialize(player, pointer)`.
  It hardcodes `Player2` and a single join.
- `GamepadCursorPointer` and `KeyboardCursorPointer` both hardcode `Player2`.
- `FleetStatusHud` draws exactly two sides (P1 left, P2 right).
- One `EnemyAi` per map, with serialized `ownPlayer` / `enemyPlayer`; targeting
  scores "planets not owned by us", but `enemyPlayer` is used for decisions.
- `PlanetsManager.CheckIfPlayerLostAllPlanets` + `EndScreenController` (one win and
  one lost button) assume a two-player outcome.
- `LargeMap` has four corner homes: NW (P1) and SE (P2) owned, NE and SW neutral.
- Masks are **global** (`Globals.currentMask`, one shared world state), so four
  players do **not** need four mask types or extra art. Good news.

## Milestones

### M1 — 1 human vs 3 AIs (first working four-player match)

- `ePlayer` gains `Player3`, `Player4`; colours and sprites get four entries.
- `GameSession` gains a slot plan: `Players = 4`, and `IsHumanSlot[1..4]` with a
  default of `{true, false, false, false}`. `Globals.IsHuman` reads it.
- `LargeMap` gives the fourth-corner homes to P3/P4 at match start (the two neutral
  corners), only when the session has four players; two-player matches stay exactly
  as they are today.
- Two extra `EnemyAi` instances, one per AI slot, each targeting every other player.
- `FleetStatusHud` shows four sides (top-left/bottom-left, top-right/bottom-right).
- End screen: "last player standing" instead of win/lost buttons; the existing
  two-player path is kept for two-player matches.

### M2 — Humans in slots 2–4

- Generalise `LocalMultiplayerBootstrap` into a join loop: slot 2/3/4 joins with a
  gamepad Start (any of `Gamepad.all` not yet bound) or a key for the keyboard slot.
- Pointer classes take their `ePlayer` from the slot instead of hardcoding it.
- Per-player control hints replace the single P2 hint.
- Keyboard slots: only **one** keyboard can be split into one player; a second
  keyboard player is not possible without raw input.

### M3 — Raw mice as slots 3/4 (Windows only)

- `RawInputSpike` already enumerates every mouse (`Devices`) and reads movement per
  device handle; it hardcodes `devices[0]`/`devices[1]` and P1/P2. Generalise to N
  devices with a binding step ("move the mouse you want for P3").
- Not available in the WebGL build — browsers merge all mice into one pointer.

## Constraints

- Masks stay global and shared. Per-player masks are a later decision, not part of
  this feature.
- Two-player matches must behave exactly as today, including the menu path.
- Performance: four fleets means four planets producing at once; the 500-ship
  budget in `performance-findings.md` still applies.

## Risks

- `EnemyAi` decides around a single `enemyPlayer`; multi-target scoring is the
  largest logic change in M1.
- Win/lose bookkeeping and the end screen are written for two players and are
  likely to hide a "fourth player still alive" bug.
- `FleetStatusHud` gets crowded; four labels may need a different layout, not just
  two more anchors.
- `playerColors`/`playerSprites` arrays are indexed by the enum, so every player
  reference must gain two colours/sprites before any four-player play test.
