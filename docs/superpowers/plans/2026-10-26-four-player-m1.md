# Four-player M1 (1 human vs 3 AIs) — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Play `LargeMap` with four players: the human at Player1 and three AIs at
Player2/3/4, each owning a corner home, with a four-sided fleet HUD and a
last-player-standing end screen. Two-player matches stay byte-for-byte identical.

**Architecture:** `Globals.ePlayer` gains two values and its two per-player arrays
gain two entries. The match player count moves into `Globals` (`playerCount`) so
both `Warmask.Scenes` and `Warmask.Shipyard` can read it. One new Assembly-CSharp
component (`FourPlayerSetup`) lives on the `LargeMap` scene, hands the two neutral
corner homes to Player3/Player4 and clones the existing `EnemyAi` twice. Win
detection moves from "an AI lost" to "no AI is left".

**Tech Stack:** Unity 6.7, C# 9, PlayMode tests, MCP for scene edits.

**Spec:** `docs/superpowers/specs/2026-10-26-four-player-mode-design.md` (M1).

## Global Constraints

- Two-player matches behave exactly as today: same map, homes, AI count, HUD, end screen.
- Masks stay global; no per-player mask work.
- Do not stage `Assets/Warmask.Planet/Prefabs/Planet.prefab`, `Assets/_Recovery/`, `Captures/`.
- Every array indexed by `(int)ePlayer` must have 5 entries (None + 4 players) before any four-player play test.
- Four-player forces `LargeMap` (the only map with four corner homes).

## Review Focus

- A P3 or P4 home must be owned, coloured, producing and defended like a P1 home.
- Elimination of one AI must not end the match; the human dying must still show "lost".
- An AI must treat every player that is not itself as an enemy, not just Player1.
- `FleetStatusHud` must not draw a slot for a player that does not exist in a two-player match.

---

### Task 1: Player 3 and 4 exist

**Files:** `Assets/Warmask.Globals/Globals.cs`, `Assets/Resources/Globals.asset`,
`Assets/Warmask.Ship/Runtime/ShipInstance.cs`, test `Assets/Tests/FourPlayerGlobalsTests.cs`

- [ ] Add `Player3, Player4` to `ePlayer`; extend `playerColors` default to 5
      (added: orange `(1, 0.52, 0.12)`, pink `(1, 0.45, 0.72)`) and add a
      `[NonSerialized] public int playerCount = 2;`.
- [ ] Add the same two colours and two sprites to `Assets/Resources/Globals.asset`
      (sprites: `Ship-Default` for P3, `Ship-Triangle` for P4).
- [ ] Widen `ShipInstance.PlayerLaserGradients` to 5 so P3/P4 get their own laser colour.
- [ ] Test: enum values are 3/4; both new colours are non-white and distinct; both new sprites are non-null.

### Task 2: Match length is a global, four players forces the large map

**Files:** `Assets/Warmask.Scenes/Runtime/GameSession.cs`, `Assets/Warmask.Scenes/Warmask.Scenes.asmdef`, `Assets/Warmask.Scenes/Runtime/MapMenuSettings.cs`, test `Assets/Tests/GameSessionTests.cs`

- [ ] `Warmask.Scenes.asmdef` references `Warmask.Globals`.
- [ ] `GameSession.MapScene` becomes a property: getter returns `"LargeMap"` when
      `Globals.Instance.playerCount >= 4`, otherwise the stored value.
- [ ] Remove `GameSession.PlayerCount`; the count lives in `Globals.playerCount`.
- [ ] `MapMenuSettings.SetFourPlayers(bool)` sets `playerCount = 4 : 2`.
- [ ] Test: with count 4 the getter is `LargeMap` even after setting `SampleScene`; with count 2 it returns the stored value. Restore count 2 in TearDown.

### Task 3: Menu entry point

**Files:** `Assets/Warmask.Scenes/Menu.unity`

- [ ] Duplicate the `Large Map Toggle` to `4 Players Toggle` at `y = 60`, label
      "4 Players", unchecked, wired to `MapMenuSettings.SetFourPlayers`.
- [ ] Verify in play mode that checking it sets `Globals.Instance.playerCount = 4`.

### Task 4: Last player standing

**Files:** `Assets/Warmask.Planet/Runtime/PlanetsManager.cs`, test `Assets/Tests/MatchEndTests.cs`

- [ ] `CheckIfPlayerLostAllPlanets` keeps the human branch unchanged; the AI branch
      only fires `OnPlayerWin` when `IsOnlyHumanLeft(owners)` is true.
- [ ] `public static bool IsOnlyHumanLeft(IEnumerable<Globals.ePlayer> owners)`:
      true when at least one non-neutral owner exists and every non-neutral owner is human.
- [ ] Test: `{P1}` true, `{P1,P2}` false, `{P1,P2,P3}` false, `{P2,P3,P4}` false, `{None}` false, empty false.

### Task 5: A P3/P4 planet shows its owner

**Files:** `Assets/Warmask.Planet/Runtime/PlanetInstance.cs`

- [ ] `SetOwner` clamps the indicator index to the last indicator when the owner is
      Player3/Player4, so the existing two-image badge tints in that player's colour
      instead of vanishing. No prefab change.

### Task 6: Three AIs, one per slot

**Files:** `Assets/Warmask.EnemyAi/Runtime/EnemyAi.cs`, new `Assets/Warmask.EnemyAi/Runtime/FourPlayerSetup.cs`, `Assets/Warmask.Scenes/LargeMap.unity`

- [ ] Drop the serialized `enemyPlayer`; score the enemy bonus for any owner that is
      neither `ownPlayer` nor `None`.
- [ ] Add `public void ConfigurePlayer(Globals.ePlayer own)`.
- [ ] `FourPlayerSetup` (Assembly-CSharp, so it sees `EnemyAi`, `PlanetInstance` and `GameSession`):
      if `Globals.Instance.playerCount < 4`, disable itself. Otherwise set the two
      serialized corner homes to Player3/Player4 and instantiate the serialized
      `EnemyAi` twice, configured for Player3 and Player4, named `EnemyAI P3`/`EnemyAI P4`.
- [ ] Wire the component on a new `Four Player Setup` object in `LargeMap` with
      `p3Home = Home NE`, `p4Home = Home SW`, `prototype = EnemyAI`.
- [ ] Play mode: 3 AI senders in the log, all four homes owned, no exceptions.

### Task 7: Four-sided fleet HUD

**Files:** `Assets/Warmask.Shipyard/Runtime/FleetStatusHud.cs`

- [ ] Loop over players `1..Globals.Instance.playerCount`; left column for odd slots,
      right column for even slots, second row on the bottom. Content and blink rules unchanged.
- [ ] Play mode: four numbers in a four-player match, two in a two-player match.

### Task 8: Verify and commit

- [ ] Full PlayMode suite green; revert `EditorSettings.asset`, build profile, font asset, `.slnx`.
- [ ] Commit on `vibe-coding-4-players`.
