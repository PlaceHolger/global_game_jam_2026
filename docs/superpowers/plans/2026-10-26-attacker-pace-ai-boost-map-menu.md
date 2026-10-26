# Attacker pace, AI opening boost, and map choice — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scale the capture countdown with attacker count, add a normal/large map choice to the menu, and make the AI race for neutrals in its first 5 seconds.

**Architecture:** Three independent, local changes: one static curve in `PlanetInstance`, three serialized constants plus a time check in `EnemyAi`, and one setter on `MainMenuSettings` wired to a duplicated menu toggle. No new systems, no new scenes.

**Tech Stack:** Unity 6.7, C# 9, Unity Test Framework (PlayMode), MCP for scene edits.

**Spec:** `docs/superpowers/specs/2026-10-26-siege-control-and-combat-pace-design.md`

## Global Constraints

- Capture tick must hit exactly: 1 attacker = 0.5×, 5 = 1.0×, 10 = 1.5×, 40+ = 2.0×; linear between.
- Zero attackers never tick.
- AI opening boost lasts 5 s, then the previous values return unchanged.
- `EasyMode` keeps multiplying the normal decision interval; the boost applies on top.
- Default map stays `LargeMap`; `SampleScene` is the normal map.
- Do not stage `Assets/Warmask.Planet/Prefabs/Planet.prefab`, `Assets/_Recovery/`, `Captures/`.

## Review Focus

- A planet with defenders in orbit must still never tick, whatever the attacker count.
- The AI must not keep the boost forever; after 5 s its cadence must equal today's.
- Neither menu choice may leave `GameSession.MapScene` pointing at a scene that is not in build settings.
- Capture speed must apply to the AI's attacks as well as the player's.

---

### Task 1: Attacker-count capture speed

**Files:**
- Modify: `Assets/Warmask.Planet/Runtime/PlanetInstance.cs` (tick block, ~line 260)
- Test: `Assets/Tests/PlanetAttackSpeedTests.cs` (create)

**Interfaces:**
- Produces: `public static float PlanetInstance.AttackerSpeedMultiplier(int attackers)`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void AttackerSpeedMultiplier_MatchesAgreedCurve()
{
    Assert.AreEqual(0.5f, PlanetInstance.AttackerSpeedMultiplier(1), 0.001f);
    Assert.AreEqual(1.0f, PlanetInstance.AttackerSpeedMultiplier(5), 0.001f);
    Assert.AreEqual(1.5f, PlanetInstance.AttackerSpeedMultiplier(10), 0.001f);
    Assert.AreEqual(2.0f, PlanetInstance.AttackerSpeedMultiplier(40), 0.001f);
    Assert.AreEqual(2.0f, PlanetInstance.AttackerSpeedMultiplier(100), 0.001f);
    Assert.AreEqual(1.3f, PlanetInstance.AttackerSpeedMultiplier(8), 0.001f);
}
```

- [ ] **Step 2: Run it, expect a compile failure naming `AttackerSpeedMultiplier`**

- [ ] **Step 3: Implement the curve and use it**

Piecewise `Mathf.Lerp` between 1→5 (0.5→1.0), 5→10 (1.0→1.5),
10→40 (1.5→2.0), clamped above 40. In the capture block, compare the timer
against `unitReductionIntervalWhenAttacked / AttackerSpeedMultiplier(numEnemyShipsInOrbit)`.

- [ ] **Step 4: Run the test, expect PASS; run the full suite**

- [ ] **Step 5: Commit**

### Task 2: AI opening boost

**Files:**
- Modify: `Assets/Warmask.EnemyAi/Runtime/EnemyAi.cs`

**Interfaces:**
- Produces: serialized `openingBoostDuration = 5f`, `openingDecisionInterval = 0.35f`, `openingPostAttackDelay = 1f`

- [ ] **Step 1: Add `bool InOpening => Time.time < openingBoostDuration;`**
- [ ] **Step 2: Use it where the next decision is scheduled** (`nextDecisionTime = Time.time + (InOpening ? openingDecisionInterval : decisionInterval)`) **and where the post-attack wait is added** (`+= InOpening ? openingPostAttackDelay : IntervalAfterAttack`).
- [ ] **Step 3: Play-test in the editor** that the AI sends its first wave earlier and that cadence visibly slows after 5 s.
- [ ] **Step 4: Commit**

### Task 3: Menu map choice

**Files:**
- Modify: `Assets/Warmask.Globals/MainMenuSettings.cs`
- Modify: `Assets/Warmask.Scenes/Menu.unity` (duplicate the existing Easy Mode toggle)

**Interfaces:**
- Produces: `public void MainMenuSettings.SetLargeMap(bool isLarge)`

- [ ] **Step 1: Implement the setter** — `GameSession.MapScene = isLarge ? "LargeMap" : "SampleScene";` (add the `Warmask.Scenes` using).
- [ ] **Step 2: Duplicate the Easy Mode toggle in `Menu.unity`** via MCP, rename it `Large Map Toggle`, change its label to "Large Map", set `isOn = true`, rewire its `onValueChanged` to `MainMenuSettings.SetLargeMap`, and offset it below the easy toggle.
- [ ] **Step 3: Verify in play mode** that toggling switches `GameSession.MapScene` and that Start loads the chosen scene.
- [ ] **Step 4: Commit**
