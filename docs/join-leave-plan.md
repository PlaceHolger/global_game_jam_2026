# Join/Leave + Exit Plan

## Exit to menu
- Add "Exit to Menu" button in MatchClock options menu.
- Call SceneLoader.LoadScene("Menu") (or LoadSceneAsync) and reset Time.timeScale = 1.
- Guard against double-clicks with a flag.

## Join/Leave (mid-match)
1. Track runtime slot ownership in MatchConfig (human vs AI) and expose SetSlotHuman(player, bool).
2. Options menu lists current slots (P1-P4) with mode and input.
3. Join: detect Start (gamepad) / J (keyboard) during gameplay; convert an AI slot to human:
   - Create selection manager + pointer (reuse LocalMultiplayerBootstrap.CreateHuman pattern).
   - Disable the AI controller for that player (or mark slot human).
4. Leave: convert human slot to AI:
   - Destroy the player's selection manager GameObject.
   - Spawn/enable an EnemyAi for that player (reuse FourPlayerSetup clone).
5. Update HUD/fleet status to reflect new human/AI status.
6. Tests: join/leave state transitions, input assignment, AI activation.

## Notes
- Reuse existing LocalMultiplayerBootstrap and FourPlayerSetup patterns.
- Keep MatchConfig as source of truth.
- Pause game while menu open (already implemented).
