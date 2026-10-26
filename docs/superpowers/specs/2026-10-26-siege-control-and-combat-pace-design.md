# Siege control, combat pace, and feel fixes — design (2026-10-26)

Short design for one batch. Small changes on top of what exists; no new systems.

## Intent

Play-testing on the large map surfaced one control hole and two pacing problems:

1. A fleet parked in orbit over a planet you are besieging cannot be commanded. It
   stays there until the planet falls, so an attack you regret cannot be aborted
   and 100 ships can sit idle for a minute.
2. Attackers do not pay off: the capture countdown is 1 stored unit per second
   regardless of how many ships are orbiting.
3. The AI is slow in the opening, when it should be racing for the nearest neutrals.

Plus four feel/layout items already requested: explosion boxes, right-drag pan,
shift multi-select, clearer under-attack feedback.

## Goals

- A besieged planet is selectable as a fleet source, so your orbiting ships can be
  redirected anywhere or recalled home.
- More attackers shorten the capture countdown, gently, on the curve below.
- The AI behaves like a race in the first seconds, then settles back to normal.
- The four feel items land in the same batch.

## Non-goals

- Difficulty levels. Only a binary `EasyMode` exists today (menu toggle that slows
  the AI decision interval 2.66×). Real levels go in `docs/upgrade-plan.md` ideas.
- Four players. `ePlayer` has None/Player1/Player2 only; see notes.
- Changing the capture rule itself (orbit must be clear of defenders, then stored
  units tick down).

## Decisions

### 1. Besieged planet is a fleet source

- Selecting a planet you own stays as now.
- Selecting a planet you do **not** own is allowed when at least one of your ships
  is in orbit. Otherwise the existing "Not yours" feedback.
- Available ships for such a source = **your** ships in orbit (not the owner's).
  `DefendingShipsInOrbit` stays owner-relative; a new count answers "how many of
  player X's ships are in orbit".
- Sending from it uses the normal order maths (half, or enough to conquer, or all
  with Ctrl), then the ships fly off as usual.
- When the planet flips, nothing changes: the old owner's orbiting ships die and
  your fleet survives (existing `OnPlanetOwnerChanged`).

Touches: `PlanetInstance` (per-player orbit count), `PlanetSelectionManager`
(source guard + available count), `TroopMovementManager` (owner guard),
`ShipyardInstance.CanTransferShip` (accept ships owned by the executing player).

Risk: the AI goes through the same path, so AI sieges become resumable too. Fine.

### 2. Capture speed scales with attackers

Multiplier on the stored-unit countdown, piecewise linear through the agreed points:

| attackers | multiplier | seconds per stored unit |
|-----------|------------|-------------------------|
| 1         | 0.5        | 2.0                     |
| 5         | 1.0        | 1.0                     |
| 10        | 1.5        | 0.67                    |
| 40+       | 2.0        | 0.5                     |

`tick = unitReductionIntervalWhenAttacked / multiplier`, clamped to the table ends.
Zero attackers never ticks (unchanged). A 100-unit planet therefore takes 200 s with
one ship, 100 s with five, 67 s with ten, 50 s with forty or more.

This applies to both players and the AI.

### 3. AI opening boost

Serialized window, default 8 s. Inside it the AI decides every 0.35 s instead of
`decisionInterval` (1 s) and waits 1 s after an attack instead of
`IntervalAfterAttack` (4 s). After the window it reverts exactly to today's values.
`EasyMode` still multiplies the normal interval; the boost applies on top.

### 4. The four feel items

- **Explosion boxes:** `Ship-Explosion.prefab`'s three systems use
  `Legacy Shaders/Particles/Additive` with the ship sprite sheet as particle texture.
  Switch them to a URP particles material sampled from `Circle.png` (256², alpha).
  The planet already spawns this prefab on capture, so planets get it for free.
- **Right-drag pan:** pan while the right button is pressed; kill the order only on a
  right-click that did not move (a drag must not clear the selection).
- **Shift multi-select:** shift-click adds or removes own planets from a group; the
  next click on a target makes every selected planet send its share, scaled so the
  planet with the most ships sends the most and no planet sends more than it has.
  Ctrl still means "everything". One ring per selected planet, one preview line from
  the nearest/first, one summed count.
- **Under attack:** the stored count turns red and pulses while enemy ships orbit;
  a small explosion puff fires on the planet surface every ~0.7 s (pool exists, and
  `Warmask.Planet` already references `Warmask.Ship`).

## What's left to the builder

- Exact pulse curve and puff timing are cosmetic; keep them cheap and pooled.
- Whether the AI should also get the opening boost on the old map is not specified —
  it applies to both, since it is one component.
- Four-player notes for the ideas file (not built): `ePlayer` needs Player3/Player4,
  a colour and sprite each, mask profiles beyond Blue/Red, pointer sources for P3/P4
  (the raw-input router exists), a four-sided `FleetStatusHud`, and two more home
  planets — the large map already reserves four corner homes, so the map is the only
  part that is ready.
