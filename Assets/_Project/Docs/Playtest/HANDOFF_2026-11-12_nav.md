# Handoff 2026-11-12: jack traffic, stress scenario, grid navigation

Branch `polyperfect-men`. Everything of mine is committed and pushed. Working tree has only unrelated churn
(portrait deletions, Metrics/*.json, BugLog_Block_1091.md): leave it alone.

## State of play
- Clock is 20 game-seconds per real second (`SimulationTimeService.GameSecondsPerRealSecond`). HUD speed buttons set `Time.timeScale` only.
- Bugs go to the LOG only: `BugFocus.Show` pauses nothing, moves no camera, no quicksave, no toast. The two staging-lane modals in
  `TrailerOffloadController` are log lines too. Do not add modal notices.
- Jack passing/lane rules, 1.25 m lane spacing, door-bypass (fix B, on), fluid on-foot case picking: all in
  `OrderSelectionTaskDriver.PalletJack.cs` / `.JackTraffic.cs`. `WorkQueueSystem.SpreadByStartAisle` is built but OFF (A/B showed no benefit).
- Stress scenario: HAL tab > TEST SCENARIO > BUILD THE SCENARIO (`HalStressScenario`), saved to **slot 8**. Details in `HalStressScenario.md`.
  `PlaytestBotSettings.startFromSlot` = 8 makes INITIATE HAL load it (asset currently says 1; set it before a run).
  For traffic recordings stop the bot first (`PlaytestBot.Instance.StopBot()`); a loaded save restarts HAL's block.
- Recorder: `JackTrafficRecorder` (5x, 08:13 -> 12:00). Clean-slot baseline (NavMeshAgent, 6 runs): picking 8-20%, held 39-62%;
  69% of held samples are the dock/staging corridor, z 48-59 (jacks, dock stockers and reach trucks all contend).

## Tools > Navigation (the other session's grid planner) - reviewed, NEVER run in a recorded block
All three switches ("Grid legs - Pallet Jacks / Reach Trucks / Dock Stockers") are PlayerPrefs, default OFF; unticked is normal.
- `NavGrid` (data: blocked, cost, exits, aisle id/travel) <- `NavGridBuilder` (from PlacementGrid; rebuilds on object-count change or 30 s)
- `GridPlanner` A* over (cell, heading); jack: face aisle traffic, no reverse up an aisle; dock stocker: avoid aisles; smoothing 0.25 m margin
- `NavDynamicLayer` blocks parked machines + floor pallets (NOT manned machines, so it has no notion of a crowd)
- `IMachineMover` / `GridMachineMover` (used by dock stockers); `OrderSelectionTaskDriver.GridLeg.cs`, `ReachTruckOperator.GridLeg.cs`
  replace only the open-floor travel leg and keep the queue / 1 m buffer / solid guard
- `NavAudit` (menu: Audit Reachability, in Play paused) writes `Logs/NavAudit.txt`: grid vs NavMesh per pick location
- `EquipmentTraffic`: right of way reach truck > dock stocker > jack, one manoeuvre lease at a time, look-both-ways at aisle exits

## Next steps (agreed order)
1. Run NavAudit in Play with the clock paused; read `Logs/NavAudit.txt` for grid-vs-NavMesh disagreements.
2. A/B on slot 8, ONE switch at a time (jacks first), 3-6 runs per arm, bot stopped, compare held share and corridor share to the baseline above.
   Runs are chaotic: never judge from one.
3. Grid wins -> make default. Not -> consider manned machines as soft cost in the planner.
4. Update the stale "CURRENT WORK" block at the top of CLAUDE.md (still describes blocks 29-33 / continuous blocks plan).
5. Then continuous HAL blocks (`pauseAtBlockEnd = 0`), run as a batch.

## Rules to remember
Never edit scripts during Play; never `git pull`/reset with Unity open; one change per HAL block; machines never drive through
racks/walls/machines/pallets; compile gate = `GameCore.Simulation.dll` mtime + `error CS` in `Logs/Editor.log`.
Open/unproven: mid-pick save/load dropping cases (probably not real), reach-truck vs jack standoffs, start-walk backstop (coded, stall not reproduced).
