# CLAUDE.md

## HANDOFF — Happy Thanksgiving (2026-11-26, read this FIRST; pick up on the other PC)

Branch `polyperfect-men`, everything below is committed and pushed. Pull with Unity CLOSED, then open Unity.

**NEXT STEPS, in order (Tad, 2026-11-26: "make a note of next steps for my next session on another PC"):**
1. `git pull` with Unity closed; open Unity; wait for the compile; check `Logs/Editor.log` for `error CS` (expect 0). Confirm the UnityMCP bridge with `mcpforunity://instances`.
2. **Catch the intermittent receiver navigation failure** (details under OPEN below). Run a new-company HAL block as in "How to run the test", and BEFORE the failures start install the
   `Application.logMessageReceived` diagnostic hook via `execute_code`. When the first `is not reachable` / `cannot reach task position` warning appears, dump the reachability grid from
   that employee's position (both agent types) and the carving-obstacle count around them, WITHOUT stopping Play. Then fix the root cause (suspects: lane-mouth carving from floor pallets,
   a navmesh rebake in progress, an employee on the yard layer under the building).
3. Run at 3x, not 8x, for any result you intend to trust (8x steps are 0.33 s per frame and can cause their own travel failures).
4. Small fixes after that: the 0.2 m arrival-vs-receiving-range near-miss in `ReceiverReceivingWorkflow` (4.7 m vs max 4.5 m); the jack stall 1.4 m short of rack bay 01-03; the
   "no reachable pick location ... staging short" warnings on big Seafood orders.
5. Then the step 1 gate: three consecutive clean blocks (>=80% on time, no deadlocked doors, no REPEATING bugs), ideally 7-day. Throughput (0-2 pallets vs target 36) is an economy /
   bot-demand problem, NOT travel: keep it out of the navigation work.
6. Do NOT start the tutorial (roadmap step 2) or anything from `FUTURE_IDEAS.md` until the loop closes. Never commit the bot's report / metrics files with a code change.

**Fixed today (3 commits, all compiled clean):**
1. `4a601bb45` `ReachTruckOperator`: the "resume already assigned task" path started `PalletPickRoutine` without `TryBindPalletPickSource`, so a PalletPick with no PalletId
   (cleared on save load) ran with an empty pallet id and aborted "missing pallet". It now binds first (or hands the task back).
2. `b7c4f033b` `MheTravel.Leg`: the "lost path" check ran BEFORE the arrival check. A long frame (low fps / 8x: a reach truck moves 1-2 m a frame) steps the agent straight onto
   the goal, it consumes its path (hasPath false), the leg read that as lost, re-seated the agent a frame behind the machine and failed after 12 re-asserts, 0.7-2.4 m short.
   Arrival is now judged on the agent first. Also logs a warning "GOAL OUTSIDE BUILDING" if a leg's snapped goal fails `MHEBoundary.IsInside` (never fired yet).
   `[MheTravel]` failures per 3-day block at 8x: **134 -> 4 -> 2 -> 2**.
3. `41f43b54d` `AiNavigation`: no false "cannot reach task position (path invalid)" warning when the employee is already within 1 m of the target (it was a false bug incident).

**HAL blocks (new company, 3 days, 8x, `Small_two_door_setup_NavTesting`, bot planner off, no code change between 75/76/77):**
| Block | Score | Reliability | Shipped | `[MheTravel]` fails | Notes |
|---|---|---|---|---|---|
| 74 (before fix 2) | 28.2 | 36 | 0 | 134 | |
| 75 | 44.4 | 21 | 2 | 4 | receiver flood (below) |
| 76 | 60.2 | 67 | 2 | 2 | clean, 6 bug incidents |
| 77 | 20.2 | 20 | 0 | 2 | receiver flood again: 274 unreachable-target events, 303 refused receiving |

**OPEN, the next thing to do: intermittent human-receiver navigation failure.** In blocks 75 and 77 (not 74/76) two receivers (75: Ann Peterson / Ruth Murphy; 77: Joshua Ramirez /
Jonathan Scott) log `[AiNavigation] ... task target (...) is not reachable - the partial path ends 12-17 m short` ~25x each (REPEATING), then `[ReceiverReceivingWorkflow] Arrival callback
fired ... employee is N m away (max 4.5m) - refusing to start receiving`, `receive workflow cancelled (out of range)`. Targets are staging-lane slots (x -8..-3 and +4.6..5.6, z 43-49).
Block 77 also had a reach truck `PathPartial` 22.7 m short of lane 2A (0.61, 50.31) from (-21.67, 54.59) (static `NavMesh.CalculatePath` also said PathPartial).
- What was ruled out: a PERMANENT gap. In block 76 a grid of floor points (x -30..12, z 38..58) was reachable from the yard spawn (-15.3, 0.04, 18.5) AND from inside (7.7, 47) in both
  directions. Idle employees at the yard spawn are selectors waiting for work (normal). A one-off `PathInvalid` from the yard to (5.6, 1.14, 48.1) at day 1 10:25 (block 76) was
  gone minutes later, so the cut is TRANSIENT or crew/state dependent.
- Prime suspects (not tested): carving `NavMeshObstacle`s on floor pallets / lane contents closing a lane mouth or aisle; a navmesh rebake in progress; an employee on the yard-level
  layer (y ~0.03) under the building footprint (a `stuck ... (12.00m to mesh)` line at y 0.0-0.3 inside x 11-13, z 43-45).
- How to catch it next time (this was tried once and Play ended before it fired): BEFORE the failures, install a hook with `execute_code`
  (`Application.logMessageReceived`; on a warning containing "is not reachable" / "cannot reach task position": find the employee `GameObject`, record position, `agent.isOnNavMesh`,
  `NavMesh.CalculatePath` status + end point to the target, `SamplePosition` of the target, count of carving obstacles within 3 m of the agent and the target). The moment a failure
  appears, ALSO dump a reachability grid like the one above from the failing employee's position (B reachable / . not) for BOTH agent types. Do not stop Play first.
- Smaller things: "arrival callback ... 4.7 m away (max 4.5 m)" is a 0.2 m tolerance near-miss in `ReceiverReceivingWorkflow`; "no reachable pick location ... staging short" on the big
  Seafood orders (pick faces `(behind)` other stock); one jack stall 1.4 m short of rack bay 01-03; one jack kept being dragged back by `MHEBoundary` near (3, 48.5) in an early block-75 run.
- The economy side is weak and separate from travel: 0-2 pallets shipped vs target 36, bot releases only 0-4 orders per block (see the demand cap notes below). Step 1 gate = three
  consecutive 7-day blocks (>=80% on time, no deadlocked doors, no REPEATING bugs); today's 3-day blocks at 8x are a regression check only.

**How to run the test (UnityMCP bridge, Unity focused, `Logs/Editor.log` is the evidence):**
1. `mcpforunity://instances` then `set_active_instance`. Check `Library/ScriptAssemblies/GameCore.Simulation.dll` is newer than the edited `.cs` and `Logs/Editor.log` has no `error CS`.
2. `execute_code` (edit mode): `var s = Resources.Load<GameCore.Diagnostics.PlaytestBotSettings>("PlaytestBotSettings"); s.startFromSlot = 0; s.buildOnly = false; s.gameSpeed = 8;`
   `s.pauseAtBlockEnd = true;` then `GameCore.Diagnostics.HalBuilder.SetLaunchPrefs();` then `manage_editor play`. HAL builds a new company, hires, buys, books and runs a 3-day block.
   These settings are changed IN MEMORY only (the asset on disk still says buildOnly 1, gameSpeed 3, startFromSlot 0). A domain reload resets them: set them again before each Play.
   (`startFromSlot = 1` instead loads slot 0 "FreshPlaytest 11-12" but that save has no crew / POs and does nothing: do not use it for a soak.)
3. Watch `Logs/Editor.log` by line number, NOT by text: it is cumulative across Play sessions and old "Block N started" / "BLOCK N DONE" lines match forever. Note `wc -l` before Play.
   The `Block N started` line proves the run began; then check the clock advances (`SimulationTimeService` in `GameCore.Economy`, `.Day/.Hour/.Minute`).
4. A script compile fired right after Play started once ("Requested script compilation ... Reloading assemblies"), which wipes the services (clock null, timeScale 0, editor looks hung).
   If the clock is null, stop Play and start again. Never edit scripts during a run.
5. 8x gives ~2.5 game-minutes per real second (the editor only manages 7-13 fps; each frame is capped at `maximumDeltaTime` 0.33 s), so a 3-day block is ~25-30 real minutes. Coarse steps
   can themselves cause travel failures: re-check anything odd at 3x. A WRITTEN `Block_N.md` (in `Assets/_Project/Docs/Playtest`) has the score, bug table and stack traces; the bot also
   rewrites `BugLog_*`, `Scoreboard.md`, `builds.csv`, `Metrics/*.json` (uncommitted noise: do not commit them with a code change).

**This clone:** today ran on `C:\dev\03-29_REDO-BUILD` (UnityMCP bridge live). The other PC is `C:\Users\tadsa\Documents\GitHub\03-29_REDO-BUILD`; same repo and branch.

## CURRENT WORK — NavmeshAgentRework: clean slate (2026-11-20)

Tad scrapped ALL the custom machine traffic code (MheMotor, MhePlanner, MheAisleMap, MheTraffic, the NavGrid / GridPlanner family, aisle turns / gates,
MheAvoidanceObstacle, test bench) because reach trucks crashed into everyone and jacks stuck on racks, forklifts and the foundation edge. Deleted with `git rm`
(recoverable from commit `725e4614c`). What exists now:

- **`MheTravel`** (`Actors/MheTravel.cs`) is the ONLY travel code: `Leg` = plain NavMeshAgent (updatePosition stays false, the transform copies `agent.nextPosition`),
  `Turn`, `Straight`. All machines + people avoidancePriority 50 (`AiNavigation.MachineAvoidancePriority`, `MheTravel.AvoidancePriority`). Callers: the 3 dock stocker
  `SeekViaNavMesh` bodies, `EquipmentBattery.NavTo/Face/MoveStraight`, `ReachTruckOperator.RtLeg` (`ReachTruckOperator.Travel.cs`), jack `DriveJackTo/JackFace/JackMoveStraight`.
- **One walkable area.** NavMeshManager no longer paints Dock Apron / Rack Aisle; ObjData `navArea` 3/4/6 and prefab modifier areas 3/4/5 were reset to 0; area names
  in `ProjectSettings/NavMeshAreas.asset` cleared. Only Walkable (0) and Not Walkable (1, walls/barriers) remain. NavMeshManager still builds the floor / stair /
  dock-top sources (needed for any walkable surface).
- **Dock stocker:** scripted choreography (offload/load, `DriveInternal` transforms, agent off while commandeered) UNCHANGED. Idle pockets: `DockStockerHome.WallGap` = 1.325 m.
- **Traffic code stripped too (2026-11-20, second pass):** deleted `EquipmentTraffic`, `EquipmentTrafficAgent`, `AisleTraffic`, `EquipmentTrafficTests`; removed the reach truck's
  `SeekViaNavMesh`, pass / go-round / jam-recovery / aisle gate / untangle code and the dock stocker spacing guard + yield-reverse in `AiNavigation`. `DockStockerHome` has no traffic
  knowledge (a restless stocker keeps still). Still present and still used by scripted moves: `MachineClearance`, `MheParking`, `DockApron`.
- **PathInvalid fixed (2026-11-20).** Causes found, all in how machines were set up, not the mesh: (1) prefab agent area masks (89 / 217 / 345) excluded the old custom areas, so jacks could
  not path anywhere near them: `AiNavigation.SetupAgentType` and `MheTravel.Leg` now set `areaMask = AllAreas`; (2) goal snapping used the default (Rat) agent type: now the agent's own;
  (3) a re-enabled agent keeps its old internal position: `Leg` always `Warp`s it onto the machine; (4) `ReachTruckOperator.Update`'s Manual-phase enforcement switched the agent off every frame
  (`_inTravelLeg` now exempts a leg); (5) other code (parking, watchdogs) disables an agent mid-leg: `GoIdle*` refuse while `MheTravel.IsTravelling`, and `Leg` re-seats the agent and
  re-issues the destination (12 times) when it finds it off or its path cleared. A rack anchor in an aisle has no MHE mesh: the leg ends at the nearest mesh point (8 m) and the caller's precise drive finishes.
  A 2-minute run at 3x: 24 legs, 0 `[MheTravel]` failures.
- **Dock stocker "fell below the floor" loop FIXED (2026-11-20).** A save loaded with a free dock stocker standing inside a trailer: the apron rule called that "inside", but there is no floor mesh in a trailer, so its agent
  dropped to the yard layer (y 0) and `MHEBoundary` put it back into the trailer, over and over. `MHEBoundary` now only remembers / restores spots with floor-level mesh under them and falls back to the mesh just inside
  the nearest dock door. Also: pallets under a machine no longer carve (`BuildingData.OnTransformParentChanged`). Still open: one reach truck task aborts with an empty pallet id (not a travel failure).

## PREVIOUS WORK (superseded 2026-11-20: all of this code is deleted) — One motor for every machine (MHE traffic rework, 2026-11-18, read this first)

All machine travel (pallet jacks, reach trucks, dock stockers, charge / park trips) now goes through **`MheMotor`**
(`Assets/_Project/Scripts/Actors/Mhe/`). Machines' NavMeshAgents no longer steer them. Full write-up, rules and test results:
`Assets/_Project/Docs/Playtest/MheTraffic_2026-11-18.md`. Short version:

- **Planner (`MhePlanner`)**: open floor = `NavMesh.CalculatePath` (MHE agent; new NavMesh area 8 "Rack Aisle", painted by
  `NavMeshManager.AddAisleSources`, is EXCLUDED), points pushed off walls / pallets for the machine's real size, a straight line-up before a headed
  goal, a straight "back out" first when starting somewhere tight. Inside an aisle = the aisle's own lane (`MheAisleMap`: faces, ends, travel =
  higher bays = the chevron), in by an end, gentle lane changes (18 deg, cap 30), out by an end. Jacks: low-bay end in, far end out, never back
  up more than 1.5 m. Reach trucks: either end, REVERSE along the lane instead of turning round, keep right / go round when traffic comes the other way.
- **Motor (`MheMotor`)**: pure pursuit on the machine's centre, turns about its centre. Sensor sweeps the full outline (forks, carried pallets)
  along the route: stops 0.25 m from machines (+0.6 m behind a moving one), 0.2 m from people, short of racks / walls / floor pallets. Right of way
  RT > DS > jack (a lower rank treats a higher rank's next stretch as occupied). Standoffs: the lower rank / the follower makes way (back off, or
  pull aside on open floor). Standing machines are driven round. Work spots: a leg's destination is reserved; another machine whose destination
  clashes and is not the nearer one stops at a holding line until the owner sets off. Aisle entry gate: one direction of moving traffic at a time.
- **Anti-clip guard** (`MheMotor.LateUpdate`, every machine, every frame): any move by anything (agent, hand-driven RT lane / rack moves) that comes
  within 0.1 m of another machine is undone. A motor machine that something keeps pushing into gets out of its way.
- **Jack specifics**: pick stop = parallel to the aisle, facing the chevron, 0.5 m off the face, pallet level with the slot; selector works in the gap
  and walks round the jack's end. `SpreadTripStarts` is OFF (trips always start at the lowest aisle/bay). Staging pivot: the jack's CENTRE drives
  onto the pivot point, turns, reverses in. The no-progress watchdog ignores a jack on a motor leg (the motor has its own limits: 2 min traffic,
  7 s no progress).
- **Test bench**: HAL tab > TEST SCENARIO > **RUN MHE TRAFFIC TEST (slot 6, 4 orders, 3x)**, or `GameCore.Actors.MheTestBench.Run(6, 4, 3f, 8f)`
  in Play. `MheTrafficProbe` logs `[MheProbe] CLIP / TOO CLOSE / IN RACK / ANGLE / HELD / FAILED`; `[MheTest]` lines give per-minute summaries.
  The bench suppresses the day-end recap (`DailyObjectivesHud.SuppressRecap`), which otherwise pauses the game.
- Old code still in the files but no longer called for travel: `ReachTruckOperator.SeekViaNavMesh` / grid leg / aisle gates, the dock stockers'
  `SeekViaNavMesh` bodies (now one-line motor calls), `OrderSelectionTaskDriver.AisleTurns.cs`.
- **This machine**: on 2026-11-18 Unity ran on THIS clone (`C:\Users\MURILLO\...\03-29-26 REDO`) with the UnityMCP bridge live, so tests were run
  directly. Check `mcpforunity://instances` first; the bridge drops for a few seconds on every Play entry / save load.

## PREVIOUS WORK — Pallet jacks back on the plain NavMeshAgent (2026-11-17; movement superseded by MheMotor above)

Tad scrapped the heavy scripted jack movement (grid planner legs, queue / pass / walk-out traffic code, jack avoidance tuning) because every change
made behaviour hard to predict. **Pallet jacks now use the NavMeshAgent for everything, and the driver deviates from it at exactly two points.**
Full description + what is verified: `Assets/_Project/Docs/Playtest/NavProgress_2026-11-17.md`. Short version:

- **Staging a pallet** (`JackStageRoutine`): pick lane + cell + slot, CLAIM the lane (`InventoryService.TryClaimLane`, closes it to every other machine
  from the moment it is chosen; released after the pivot), NavMesh to the lane's pivot point (`LaneAnchorPoint`, `StageAnchorOut` = 2.5 m out from the open
  end), turn on the spot to face away, reverse in until the pallet is on the cell centre, lower, raise forks, straight forward to the pivot, nav resumes.
- **Picking** (`DriveBayLeg`): nav to the aisle mouth (in line), then up the aisle to the pick's anchor (`ComputeBayPark`, jack parallel to the aisle); jack yaw is
  held within 25 degrees of an aisle's direction during every nav leg (`SteerJackYaw`, `AisleHeadingMaxDeg`); selector steps off, walks to the face, picks every
  case, walks back; next stop up to 3 bays ahead in the SAME aisle = straight guarded move (`TryStraightHop`), anything else = nav (out of an aisle by its far
  end, into the next by its low-bay end); a jack found moved off its stop between cases drives back (`RecoverIfMovedFromPark`). `MheAvoidanceObstacle` is on jacks.
- Pick order = ascending slot name (`OrderPickPath.SequenceKey`: aisle, bay, level, position). `SpreadTripStarts` can start a trip past the lowest slot.
- `AiNavigation.DriverOwnsMovement` (set while a driver holds a jack) switches off the jack's own patrol / stuck-rebake / walk-out; this was the "bouncing
  between waypoints" bug. A "selector pallet" holds only the LOOSE cases: full pallets of a line go to reach trucks as pallet picks (`FileReleasedOrderTasks`).
- Deleted: `OrderSelectionTaskDriver.GridLeg.cs`, `.JackTraffic.cs`, the jack grid switch. Reach trucks and dock stockers still have the grid switches (OFF by default).
  `OrderSelectionTaskDriver.AisleTurns.cs` (`DriveAisleTurn`, turn markers) is no longer called: dead code, not yet deleted.
- **Test save:** HAL tab (Dev Console key 1) > TEST SCENARIO > **BUILD 'ORDER SELECTION TEST' SAVE (slot 6)**: stocks every item, 10 unreleased case-pick orders,
  stops HAL, saves slot 6. Load the "three doors aisle" warehouse first. Book the orders on the Scheduler and release them yourself.
- **Status:** committed + pushed (`c27176ccc` on `polyperfect-men`). Compiles (Roslyn gate, 0 errors). Tad's first tests: "working better". Not proven: charging-bay
  lining up / one jack per station, hit recovery, aisle direction vs layout (aisle 2 enters at its low-bay end: confirm that matches the building), the stranded-load
  cleanup (deletes pallets of finished orders left on a jack), the rider's own `AiNavigation` "stuck... walking out" lines. Diagnostic: a short trip logs
  "staging short ... Still owed (pick faces with stock): ..." listing each pick face's stock.
- **TWO COMPUTERS (important):** Tad's Unity runs on `C:\Users\tadsa\Documents\GitHub\03-29_REDO-BUILD`; Claude's sessions have run on `C:\Users\MURILLO\...\03-29-26 REDO`.
  Same GitHub repo (`tadsavage/03-29_REDO-BUILD`), branch `polyperfect-men`. A Claude session cannot see Tad's Unity, console, logs or saves, and the MCP editor bridge is
  not connected: changes reach Tad ONLY by commit + push, then he pulls with Unity CLOSED. Ask him to paste console lines or screenshots instead of looking for logs.
- **Next:** Tad playtests; he sends console lines / screenshots of anything wrong. Then the A/B plan below (jacks on the NavMesh first), then delete the dead aisle-turn code.

## OLDER WORK — Grid navigation + traffic testing (2026-11-12; the JACK parts of this were removed on 2026-11-16/17, see above)

The MHE navigation (NavMeshAgent + `AiNavigation` seek + scripted moves + avoidance obstacles) is being replaced one role at a time by a grid
planner. Everything below is committed and pushed on `polyperfect-men`, and **every grid switch is OFF by default**. The 2026-11-02 playtest-benchmark
notes (blocks 29-33, the bot throughput cap, the cross-aisle layout, the `AiNavigation` backstop fixes) are archived verbatim in
`docs/history/09_playtest_benchmark_2026-11-02.md`. Handoff notes from the other session: `Assets/_Project/Docs/Playtest/HANDOFF_2026-11-12_nav.md`
and `HalStressScenario.md`.

### What exists (`Assets/_Project/Scripts/Navigation/`)

- `NavGrid` (data: blocked static + dynamic, per-cell cost, exit directions, aisle id + travel heading, no-smooth) <- `NavGridBuilder` (from `PlacementGrid`;
  a cell is blocked by a solid placed object (`MachineClearance.IsSolidData`) or by having no floor/foundation; aisles imported from live racks via
  `rackAisleFacing`, travel = toward higher bay numbers; a free cell touching a blocked one gets a clearance cost). `NavGridService` holds it and rebuilds
  when the placed-object count changes or after 30 s.
- `GridPlanner`: A* over (cell, 4 headings): forward, quarter turn, reverse (x2 cost). The machine's real outline is tested at every state. Jacks
  (`FollowAisleTraffic`) must face the aisle's travel direction on every aisle cell their outline touches and never back up an aisle; reach trucks go either
  way; dock stockers `AvoidAisles`. `PlanNearest` moves an unusable goal to the nearest fitting cell (<= 2 cells) and reports how far (the NavMesh snaps goals
  silently). Smoothing cuts open-floor staircases but keeps a 0.25 m margin to blocked cells and never enters aisles or their mouths.
- `NavDynamicLayer`: unmanned machines + floor pallets, refreshed at plan time (RT / dock stocker legs inflate machines by the 1 m vehicle buffer).
  **Manned machines are NOT in the grid** (the queue / buffer code handles them), so the planner has no notion of a crowd.
- `IMachineMover` (the seam) / `GridMachineMover` (generic: plan, scripted drive with `MachineClearance.StepBlocked`, re-plan round a stationary machine, no
  fallback). Used by the dock stockers.
- Role dispatch, each replacing ONLY the travel leg and keeping the queue / 1 m buffer / solid guard / aisle-exit stop: `JackLeg` in
  `OrderSelectionTaskDriver.GridLeg.cs` (replaces `JackNavLeg`); `RtLeg` in `ReachTruckOperator.GridLeg.cs` (5 call sites of `SeekViaNavMesh`); the three dock stocker
  `SeekViaNavMesh` copies (`TrailerLoadController` x2, `TrailerOffloadController`) hand to `GridMachineMover.DockStockerLeg`.
- **Tools > Navigation**: three checkable switches (Pallet Jacks / Reach Trucks / Dock Stockers = PlayerPrefs `NavGrid.Jacks/RT/DS`), "All back to NavMesh",
  and "Audit Reachability (in Play, paused)" (`NavAudit`, writes `Logs/NavAudit.txt`). The block start line prints which nav it ran on
  (`NavGridService.FlagSummary`). The switches are shared state: a script that sets them changes YOUR ticks.
- Tests: `GridPlannerTests` (16) + `EquipmentTrafficTests` (14) = 30 of the 35 EditMode tests, all passing (2026-11-14).
- 2026-11-14 review of blocks 42-51 and the next steps: `Assets/_Project/Docs/Playtest/NavReview_2026-11-14.md`. Step 1 done: the planner's
  start cells are exempt from `Fits` (every grid "no route" was "NoPath, 1 expanded" from a start overlapping a neighbour or an aisle cell).
  Step 2 (idle part) done: idle dock stockers wait in door pockets on the dock apron (`Actors/DockStockerHome.cs`, `EquipmentBattery`
  IdleRoutine / RestlessRoutine), no laps, make way when something is held behind them. Block 54: 0 stocker stuck (was 12), 0 cross-door failures.
  A dock stocker's pivot is near its FRONT (body ~2.6 m behind it): place it by its outline, and a full turn does not fit the 4.6 m apron.

### Evidence so far (be honest about what is NOT proven)

- NavAudit on the quicksave Play loads by default ("three doors aisle", 1208 placed objects, 2 aisles): all 60 pick locations reachable by jack AND reach truck on the grid AND the NavMesh, 0
  disagreements, goal shift 0.0 m. So layout geometry is not the problem; failures are dynamic (traffic, guards, stock).
- Live: jacks and reach trucks drove grid legs in short runs with no `[GridNav]` errors, and jacks followed aisle direction. The dock stocker leg was verified
  only through the generic mover on a jack. **Never run in a scored or recorded block.** A NavMesh baseline and a grid run both completed no putaway in ~130
  game-seconds on that quicksave (empty pick faces), so there is no throughput claim either way.

### Starting a run from a save (2026-11-12)

- `PlaytestBotSettings.startFromSlot`: 0 = INITIATE HAL 9000 builds a new company; 1-8 = it loads that save slot (numbered as in the save window; slot 8 =
  `slot_7_data.json`) and starts HAL's block from it with no building or hiring. Code: `HalBuilder.SetLaunchPrefs` (main menu button and HAL tab NEW COMPANY),
  the `fromSlot` branch in `HalBuilder.Start`, a slider on the HAL tab. A missing slot file falls back to building. The asset currently says 1.
- The HAL stress scenario (HAL tab > TEST SCENARIO > BUILD THE SCENARIO) builds large case-pick orders on a fully stocked warehouse and saves to **slot 8**.
- A loaded save restarts HAL's block, and a block number is reused until a block finishes. `plannerEnabled` fires one growth change at each block start
  unless `changes.json` already has a row for that block number: turn it off for any A/B so the crew is identical. For traffic recordings stop the bot first
  (`PlaytestBot.Instance.StopBot()`).
- Unity throttles when it is not focused (about 0.15 game-seconds per real second even at "8x"), and `execute_code` times out during a scene reload: live
  checks take minutes, and a long block needs a focused Unity.

### Test method and baseline (the other session's recorder)

`JackTrafficRecorder`: 5x, 08:13 -> 12:00, a few real minutes per run. Clean slot 8, jacks on the NavMeshAgent, 6 runs: crew picking 8-20%, held 39-62%; 69% of
held samples are the dock/staging corridor (z 48-59), where jacks, dock stockers and reach trucks all contend. Runs are chaotic (picking 3-21%, held up to
87% from the same start): never judge from one run.

### Next steps (agreed order)

1. Rerun NavAudit on the slot 8 layout (same building, expect the same result).
2. A/B on slot 8, ONE switch at a time (jacks first), 3-6 runs per arm, bot stopped, recorder on; compare held share and corridor share to the baseline above.
3. Grid wins -> make it the default. It does not -> try manned machines as a soft cost in the planner.
4. Then continuous HAL blocks (`pauseAtBlockEnd = 0`: `FinishBlock` ends with `if (S.pauseAtBlockEnd) Time.timeScale = 0f; else StartBlock();`, so the company carries cash,
   reputation and stock forward and the planner's growth changes compound unattended): run as a batch, then stop. `saveAtBlockEnd` still writes `bot_block_N`.
5. Delete the old NavMesh movement code only after a switch has held up over several blocks. Humans, rats and trucks stay on the NavMesh.

### Still true / open from the 2026-11-02 benchmark (details in the history file)

- Throughput is capped by the bot, not the warehouse: only `Contract_Starter` is ever on offer, bulk orders need 12 free pallets, so demand never grows.
- NavMesh goals at the floor edge or inside the rack block are snapped 1-1.6 m and reported as arrived; the grid refuses them (GoalBlocked / NoPath).
- Fill rate was falling because of stock-outs, not reachability.
- "B" was never done: the intended placement rule is *no lane cell more than one row from open floor* (staging at most 2 deep between aisles), which would make
  HAL's layouts correct by construction.

### Geometry reference (`three doors` / `three doors aisle`)

`PlacementGrid`: `CellSize 1.325`, `Origin (-66.3, 0.005, -66.3)`, 100 x 100 cells (about 8% have floor). World position = `Origin + cell * 1.325 + 0.6625`.

```
RACKS       x -37.81 .. -19.26   z 37.71 .. 49.64    (96 cells)
SHIP LANES  x -13.96 .. +9.89    z 41.69, 43.01, [cross-aisle], 46.99   (45 cells after the fix)
DOCK DOORS  x -11.31, -2.04, +7.24    z 36.39
FOUNDATION  x -47.09 .. +11.21   z 36.39 .. 49.64   (the loaded saves show floor up to z ~58)
```

The X aisles between the three lane groups are 3.97m (1.83m walkable after erosion). The Z cross-aisle is 2.83m clear / 1.83m walkable.

### Gotchas worth keeping

- Saves: `SaveSystem.SaveFolder` returns `Assets/_Saves` only if it is non-empty, otherwise `persistentDataPath/Saves` (`%USERPROFILE%/AppData/LocalLow/SavageDev/Warehouse/Saves`).
  Slot files are `slot_<index>_data.json`; the save window shows index + 1. Check the slot names in `metadata.json` before assuming which warehouse a slot holds.
- `PlaytestBot` is created with `HideFlags.DontSave`, so it does not show up in a GameObject search. Use the console (`[PlaytestBot] Block N started...`) to tell
  whether a block is running.
- `HalBuilder` has no editor menu item. It is launched by **INITIATE HAL 9000 on the main menu** (PlayerPrefs `HalAutoStart`) or the HAL tab's NEW COMPANY. With
  `buildOnly = 1` it builds, hires the crew, then pauses and hands over so you can save the result as a baseline.
- Editing any `.cs` while in Play mode triggers a domain reload that kills the running block (and strands reach trucks mid-carry). Stop the bot first.
- Static state survives between Play sessions (Domain Reload is off): `NavGridService` resets its statics in a `SubsystemRegistration` hook; any new static cache must too.


This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ALWAYS consult the system skills first

When a task touches one of these systems, invoke its skill (`.claude/skills/<name>/SKILL.md`) BEFORE reading or editing code, and keep it current (add new gotchas/changes to the skill, not only to this file):

| Skill | Covers |
|---|---|
| `roadmap` | The agreed 7-step plan and scope guard; read at the start of planning and whenever a new idea comes up |
| `work-queue` | WorkQueueSystem task model, WorkQueuePanel (key 7), row phases, columns, live-signature refresh |
| `order-system` | Inbound (vendors, POs, shipments, Purchasing key 9) and outbound (contracts, negotiation, orders, scheduler key 0, close-out, fines), reputation |
| `inventory-system` | Pallets, SKUs, slots/locations, putaway, replenishment, staging lanes, persistence, inventory UIs |
| `finance-system` | MoneyService, EconomyService, PayrollService, FinanceCategory/GL lines, TopBar finance panels |
| `warehouse-strategy` | Tad's real planning rules (full-pallet vs case-pick, work-backwards timing, buy/book/release order) and how HAL 9000's decisions encode them; read before changing bot decisions, and add every new rule he explains |
| `physics-system` | PLANNED (not built): dormant-rigidbody cases, rack collapse, damage; see `FUTURE_IDEAS.md` |
| `lane_setup` | Dock doors, lane naming, LaneConfig |
| `racking-system` | Racks, chevrons, aisles, location naming |
| `ModularAvatarSystem` | Avatars, AOD, Blender pipeline |
| `unity:ui-uitk` | General UI Toolkit rules (note: most panels here are programmatic with inline styles) |

## Project Overview

This is a Unity 6 (URP) warehouse/facility builder simulation game. Players place objects on a 2D grid in a 3D world, with a simulated economy (money + time) and full save/load support.

**Unity Version:** 6 (project name: `03-29-26 REDO`)
**Render Pipeline:** Universal Render Pipeline (URP) 17.3.0
**Input System:** Unity Input System 1.18.0

## Development Commands

All development is done through the **Unity Editor** — open the project in Unity 6, then:

- **Play/Test:** Press Play in the Editor
- **Build:** File → Build Settings → Build
- **Run Tests:** Window → General → Test Runner (uses `com.unity.test-framework`)
- **Editor Tools (custom menus):**
  - `Tools/ObjData/Auto‑Assign Unique IDs` — assigns unique IDs to all `ObjDataSO` assets after creating new ones
  - `Tools/Checklist/Open Checklist JSON` — opens the dev checklist file
  - Window → `Build Phase Checklist` — shows the in-Editor checklist window

**Quicksave/Quickload (in Play Mode):**
- `F5` — quicksave to `autosave`
- `F9` — quickload from `autosave`
- `F6` — open the slot-based save/load window

**UI hotkeys (in Play Mode, number row — rebound from F1–F4 on 2026-06-26 to make room for 5):**
- `1` — Dev Console (`ToolsWindowController` — currently non-functional, see BUGS & ISSUES)
- `2` — Hiring Board
- `3` — Employee Roster
- `4` — Employee List
- `5` — Shift Manager (first draft, UI-only — see Economy & Financial Reporting System below)

## Architecture

### Core Systems

**Placement FSM** (`Assets/1. Scripts/1. FSM/`)

The heart of the game. `PlacementStateMachine` drives a stack-based FSM with five states:
- `IdleState` — hover inspection, popup display
- `RaycastPlacementState` — grid hover without placement
- `BuildState` — place new objects (click or click-drag for multi-place)
- `MoveState` — pick up and re-place existing objects
- `DeleteState` — remove objects with destruction animation

`PlacementController` translates UI button events (from `BuildMenuUI`) into FSM transitions. The FSM owns a `CommandHistory` for undo/redo (Ctrl+Z / Ctrl+Y, also available via UI buttons). ESC or RMB pops the state stack back to the previous state.

**Key rule:** Only `IdleState` drives the hover popup (`WorldHoverPopupUI`). All other states must not show it.

**PlacementGrid** (`Assets/1. Scripts/1. FSM/7. Math/PlacementGrid.cs`)

2D array of cell stacks (`List<PlacedObject>[,]`). Manages object stacking, height tracking, and world↔cell coordinate conversion. Default cell size is `1.33f` units. Object ordering within a cell is: Foundations/Grounds → Floors → normal objects.

After a save is loaded or objects are manually placed in the Editor, call `grid.RebuildFromRegistry()` to sync the internal grid state with `PlacedObjectRegistry`.

**Command Pattern** (`Assets/1. Scripts/1. FSM/6. Commands/`)

Every placement/delete/move action is wrapped in an `ICommand` and pushed to `CommandHistory`. Multi-cell drag-place uses `DragPlaceCommand`; batching uses `CommandBatch`. Commands execute immediately on `Push()` and support `Undo()`/`Redo()`.

**GameContext** (`Assets/1. Scripts/1. FSM/7. Math/TimeAndMoney/GameContext.cs`)

Scene singleton (`[DefaultExecutionOrder(-100)]`) that owns and initializes `MoneyService` and `SimulationTimeService`. Wires hourly cost deduction and daily spending resets. Reads `PlayerPrefs` for difficulty and save-slot routing in `Start()`, then bakes the NavMesh synchronously.

Difficulty levels (set via Main Menu, stored in `PlayerPrefs("Difficulty")`):
- `0` — Clerk (easy): $120k starting capital, 100% sell-back
- `1` — Supervisor (normal): $100k starting capital, 75% sell-back
- `2` — Manager (hard): $80k starting capital, 50% sell-back

**⚠️ Difficulty balance needs review** — numbers are placeholder, not playtested.

**MoneyService / SimulationTimeService**

Plain C# classes (not MonoBehaviours) held by `GameContext`, registered with `ServiceLocator`. `SimulationTimeService` runs at 1 real-second = 1 in-game-minute. `MoneyService` fires `OnMoneyChanged` on any balance change; hourly costs are applied via event subscription. `MoneyService` now takes a `sellBackRate` (0–1 float) that scales all deletion refunds — `DeleteCommand` uses `_money.SellBackRate` to compute the adjusted refund.

`MoneyService`, `EconomyService`, and `PayrollService` together drive the full economy — see [Economy & Financial Reporting System](#economy--financial-reporting-system) below for the complete picture (GL_Line cost tracking, wage tiers, overtime, the 4 TopBar reporting panels).

### Data Model

**ObjDataSO** (`Assets/1. Scripts/3. ScriptableObjects/SO Scripts/ObjDataSO.cs`)

ScriptableObject describing a placeable item: `id` (unique int), `prefab`, `cost`, `hourlyCost`, `category`, `GL_Line`, `footprint` (Vector2Int), optional `customShapeOffsets`, stacking rules, pathfinding flags, and special behavior flags (`isFloor`, `ClearsGridAfterPlacement`, `ignorePlacementRules`).

`GL_Line` is the General Ledger line for hourly-cost reporting — see [Economy & Financial Reporting System](#economy--financial-reporting-system) below. It's a plain hand-editable string per asset (no runtime default-computation); the `Tools/ObjData/Auto-Assign GL Lines` one-time batch (run via `Unity_RunCommand`, not an Editor menu item yet) populated it across all 105 assets from `category` with named overrides.

`GetFootprintOffsets(rotation)` returns the rotated cell offsets for a placement. The convention is: pass `-currentRotation` to get footprint offsets (negated because world rotation and grid rotation are inversed).

**ObjDataRegistry** (`Assets/1. Scripts/3. ScriptableObjects/SO Scripts/ObjDataRegistry.cs`)

ScriptableObject list of all `ObjDataSO` assets. Lookup by `id` via `GetByID(int)`. After creating new `ObjDataSO` assets, run `Tools/ObjData/Auto‑Assign Unique IDs` to ensure no ID collisions.

**PlacedObject** (`Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts/PlacedObject.cs`)

MonoBehaviour on every placed prefab. Stores `gridX`, `gridY`, `rotation` (in 90° increments), and a reference to its `ObjDataSO`. Automatically registers/unregisters with the static `PlacedObjectRegistry` via `OnEnable`/`OnDisable`/`OnDestroy`. Nested `PlacedObject` children (e.g. items on a pallet) skip registration to avoid being saved at (0,0).

**BuildingData** (`Assets/1. Scripts/1. FSM/8. Data/BuildingData.cs`)

Stores the root cell, rotation (degrees), footprint offsets, and data reference for a placed object. Used by `MoveState` and `PlacementGrid` to know where an object's root and footprint cells are.

### Save System

**PlacedObjectRegistry** (static) — global `HashSet<PlacedObject>` of all active placed objects.

**PlacementSystem** (`Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts/PlacementSystem.cs`) — serializes/deserializes the world state to/from JSON via `ObjDataRegistry` IDs. The quicksave (F5/F9) path writes to `Assets/_Saves/autosave.json` using the legacy `SaveSystem` static class.

**SaveManager** (`Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts/SaveManager.cs`) — manages up to 8 named save slots. Each slot has a game data JSON file and a thumbnail PNG. Metadata (names, timestamps, file references) is persisted to `Assets/_Saves/metadata.json`.

Save files are stored at `Application.dataPath + "/_Saves/"` (inside the project's `Assets` folder — intentional for this prototype).

### Audio

**AudioManager** — singleton MonoBehaviour that persists across scenes. Sound effects are defined in a `SoundDefinition` ScriptableObject and played by name: `AudioManager.Play("soundName")`. Music plays probabilistically on an interval with fade-in/fade-out.

### Editor Tools (`Assets/10. Editor/`)

- **ObjDataRegistryEditor** — custom Inspector for `ObjDataRegistry`
- **ObjDataIDAssigner** — menu item to auto-assign unique IDs to all `ObjDataSO` assets
- **Build PhaseChecklistWindow** — in-Editor task tracker reading from a JSON file

### Input

Input is handled via the Unity Input System. `PlacementActions` is the generated C# wrapper for `InputSystem_Actions.inputactions`. States subscribe/unsubscribe their action bindings in `OnEnter`/`OnExit`. Global shortcuts (Ctrl+Z, Ctrl+Y, ESC, RMB) are handled directly in `PlacementStateMachine.Update()`.

### Key Conventions

- **Rotation convention:** rotations are stored in 90° increments as integers on `PlacedObject.rotation`. In `BuildingData` and state logic, rotation is stored as float degrees. Pass `-currentRotation` to `GetFootprintOffsets()`.
- **Cell stacking order matters:** Foundations and Grounds go at index 0; Floors go after grounds; normal objects append to the top. `PlacementGrid` enforces this in `AddStackObject`.
- **Undo disables objects, not destroys:** Undo for placement calls `SetActive(false)` on the GameObject so it can be re-enabled on redo, keeping the same instance alive.
- **`FindFirstObjectByType` usage:** `PlacementStateMachine.Start()` uses `FindFirstObjectByType` to locate shared systems — these must all be present in the scene at startup.

---

## Game Vision

This is a warehouse simulation game with a serious logistics core and a whimsical/absurdist chaos layer on top — think Sims 4 build mode meets real operations management meets escalating insanity.

**Current phase:** Build mode only. The placement system, grid, economy, and save/load are the foundation. The actual gameplay loop comes next.

**Player character:** The player is **Tug Dudley**, the warehouse manager / "The Boss." This name appears in the welcome overlay and is stored via `PlayerPrefs("PlayerName")`.

**Design principle:** The chaos should *emerge from* the simulation, not be bolted on. Serious systems create the conditions for absurd outcomes.

---

### Keybindings (rebound 2026-06-26; 6–8 revised 2026-07-29)

**1** = Dev Console (`ToolsWindowController`), **2** = Hiring Board, **3** = Employee Roster,
**4** = Employee List, **5** = Shift Manager, **6** = **Wholesale Contracts** (`ContractsPanel`),
**7** = Work Queue, **8** = New Item / Slotter. All via `Keyboard.current.digitNKey`, not the numpad.
**9** = Purchasing, **0** = Scheduler (dock appointment book). Tab closes all panels. Quicksave/load (F5/F6/F9) are unrelated F-keys, untouched. Panel registration rules are under Hard rules below.

**6 was Slot Assignment until 2026-07-29.** `SlotAssignmentPanel` now has no number key and is opened
by clicking a rack (`PlacementStateMachine` → `ShowForAisle`).


---

## Hard rules (each one cost real time — keep these current)

**Unity / tooling**
- **Compile gate DLL:** the game scripts build into `Library/ScriptAssemblies/GameCore.Simulation.dll` (Assembly-CSharp.dll is a 15 KB stub). Time THAT dll against the edited `.cs`.
- Private (non-`[NonSerialized]`) arrays on a MonoBehaviour come back EMPTY, not null, after an Editor reload: lazy-init with a length check (GraphicsPresetManager, 2026-11-05).
- **Never edit scripts while Play is running.** A recompile is a domain reload: `ServiceLocator`/`EventManager`/service objects are wiped and the UI looks alive while nothing runs. Stop Play, edit, let it compile, then Play. (Domain Reload is OFF for Play entry, so *statics persist between Play sessions* — clear static caches explicitly.)
- **Compile gate:** newest `Library/ScriptAssemblies/*.dll` mtime must be newer than the edited `.cs`, then grep `Logs/Editor.log` (project-relative, not `%LOCALAPPDATA%`) for `error CS`. A compile error leaves the OLD assembly running silently. `Unity_ValidateScript` is a syntax check only. Unity only compiles while its window has OS focus.
- Never `git reset` / `git pull` while Unity is open. `Debug.Log` is muted by `DebugLogSwitch` (warnings/errors show; toggle `Tools > Logging`). `read_console` is unreliable — read `Logs/Editor.log` / Player.log (`%USERPROFILE%/AppData/LocalLow/SavageDev/Warehouse/`).
- Never call `get_components` on Camera/Transform via the MCP bridge (crashes the Editor). `Unity_RunCommand`/`execute_code` mid-Play compiles are unsafe.
- Never use `FindAnyObjectByType`/`FindObjectsByType` in a per-frame path (scene is huge).
- Never borrow PanelSettings with a bare `FindAnyObjectByType<UIDocument>()` — use `SharedPanelSettings.Find()`.
- Graphics presets (2026-11-08): bloom/DoF live in `Assets/Settings/{Good,Ultra,Toaster}_Volume.asset` (each URP asset points at its own profile). A RUNNING game does not re-read an edited Volume asset: cycle the in-game Graphics Profile button (Good→Ultra→Toaster→Good) to reload it, and leave it on the player's preset. Unity re-saves these assets itself (strips a trailing space), so re-read before a second edit. Bloom is a small part of the "vaseline" look (measured: ~8 brightness levels on a wall light); the glowing white blobs on pallets/forklifts are the Bokeh DoF smearing blown-out white surfaces (the scene was ~39% pure-white pixels). Tad likes the DoF - do not touch it. Fixed by EXPOSURE, not bloom: Good/Ultra now run ColorAdjustments postExposure -1.5 (contrast/saturation 0) + Neutral Tonemapping (pure white 39% -> 4%; -1.0 only got 18%). That is the knob for scene brightness; check a night scene if it ever looks too dark.

**Builds**
- A build boots **MainMenu**, Editor Play boots Main. Anything loaded at runtime must live under a `Resources/` folder (`#if UNITY_EDITOR AssetDatabase.Load…` is null in a player; never load by path — use a ScriptableObject reference like `SharedMaterials`). Read `[InputHealth]` lines in Player.log first when a build "doesn't respond".
- Release build: `Tools > Build > Release Windows Build` (IL2CPP, DX11).

**UI Toolkit**
- A fixed-`height` flex child also needs `flexShrink = 0`. Per-frame-changing text needs a fixed width. `ExecuteLater` reschedules; use `.StartingIn()` for a first delay. Toggle modals via the `display` style, not `SetActive`. UXML roots must declare `xmlns:xsi`.
- Feedback UI/FX ticks on `Time.unscaledDeltaTime` (particles: `useUnscaledTime`).
- Floating labels must not be world-space canvases (the tilt-shift DoF blurs them): call `WorldLabelOverlay.Convert(canvas)` after building. A Render Objects pass at `AfterRenderingPostProcessing` breaks the render graph under 4x MSAA (2026-11-07) - don't retry it.
- Bottom HUD layout (2026-11-08): the ORDERS/BUILD/STAFF/REPORTS groups live in an auto-hiding LEFT rail built in code (`BuildMenuUI.BuildRailAndTray`, `.hud-rail` USS); the bottom bar is only a widget tray (`WidgetTray`, and `ActiveBar` now returns it). The tray has LEFT/CENTER/RIGHT dock zones (`TrayZone(i)`); DevHud, SystemsLog, the mini dock schedule and PlayerScoreHud all dock/float through `DockableHudCard` (left-drag 6px to lift; placement saved in PlayerPrefs `HudCard_*`). A card that is a direct child of a UIDocument root gets forced to PickingMode.Ignore by `RaycastController.Start()` - that is why the FPS card "wouldn't move"; `DockableHudCard` re-asserts picking every 250 ms. Synthetic PointerEvents in Play fight the real mouse (same pointer id) - test drags by hand.
- Rail groups (2026-11-08): seven tabs - ORDERS, BUILD, STAFF, REPORTS, UTILITIES, DEV TOOLS, UI CONTROL. A group = a tab in `ModeTabs` + a `BottomBar*UI` bar in `BuildMenu.uxml` + a `HudMode` value; `BuildRailAndTray` moves every bar into the flyout, `ApplyHudMode`/`ActiveBar`/`SyncPointerOverBuildMenu`/`RegisterBarPointerGuards` each list them, so adding an 8th means touching all of those. UTILITIES = Pallet Builder, Item Creator, AOD; DEV TOOLS = Dev Console (key 1), Dev Settings, HAL 9000, Balance; UI CONTROL = Objectives (`DailyObjectivesHud.Toggle`, persisted in PlayerPrefs `DailyObjectives.hidden`). Buttons are wired in `BuildMenuUI.WireToolButtons` and lit by `SyncToolSelection` (polled). The top bar no longer has ITEMS/AOD, and Dev Console left the STAFF bar (`PlayBtn1` is gone on purpose). The four dev tools are ONE `ToolsWindowController` window showing one tool at a time (`ToggleTab`, `ActiveTab`; tab strip hidden, title per tool); the Pallet Builder is the pallet section of the settings content (`_tab == "pallet"`, `ApplySettingsFilter`). Ctrl+clicking a pallet still opens it. Testing note: when Unity Hub steals focus the Input System sees no mouse; `InputState.Change(Mouse.current.position, ...)` via `execute_code` plus a Button's `clickable.clicked` backing field (reflection) drives the rail without the OS mouse.
- `worldBound` FOLLOWS a `translate` slide transform (2026-11-08): a slid-out panel's rect sits OFF-screen (x = -W..0), so a "cursor over it?" test against it fires for a cursor pushed past the screen edge, shows the panel, the rect moves, the test fails, it hides, repeat = a left/right jerk (the left rail did this). Test a sliding panel at its SHOWN rect, and give open vs stay-open different zones (hysteresis). The rail's visual states are in `buildmenuNEW.uss` (`.hud-rail ...`); editing a USS in Play hot-reloads it (no domain reload) but re-imports it, so its pre-existing invalid-property warnings (`-unity-text-letter-spacing`, `-unity-text-wrap`, moved icon GUIDs) fire again as `BUG: REPEATING` toasts and pollute a bot block.
- A second UIDocument for an overlay layer (2026-11-08, OrderFlowWidget's fly layer): (1) a UIDocument on a CHILD GameObject of an object that already has a UIDocument is NESTED inside the parent's document and its sortingOrder is ignored - the flyers drew under every window. Make it a ROOT object (`DontDestroyOnLoad`). (2) Build it like `UIToast.MoveToOwnLayer`: GameObject INACTIVE -> AddComponent -> assign `panelSettings` + `sortingOrder` -> `SetActive(true)`. Enabling first and configuring after made UI Toolkit assert in `RepaintPanels` the next time another window opened ("Assertion failed" / "VisualElements cannot change their render data..."), which the playtest bot's log hook then amplified into a `BUG: REPEATING` toast loop. Sort layers: HUD 999999, windows `UILayers.WindowAboveHud` 1000000, flyers +5, ConfirmationModal +10, toasts +100. Debug UI in Play with `execute_code` (reflection on the widget's private fields, `UQueryExtensions.Q`, listing every UIDocument's sortingOrder) instead of squinting at screenshots, whose capture latency is 1-2 s.
- World-blocking modals (2026-11-08): the pause menu, its settings overlay and the Save/Load window register with `UIModalGuard.PushBlocking(this, stillOpenCheck)` and `Pop` on close. While one is up, and for `UIModalGuard.WorldGraceSeconds` (0.3 s) after, `UIModalGuard.IsBlockingWorld` is true: `UIInputGuard.IsPointerOverUIToolkit` and `RaycastController.CheckIfPointerOverUI` answer true everywhere, `HandleIdleHover` hides the world tooltips, and (via `IsCapturing`) hotkeys, F5/F9 and the camera stand down. Do not rely on a scrim being "pickable" to block the world: the order-pallet cards live in the same HUD root and drew OVER the menu, and a click on the frame the menu hides used to land on the pallets behind it. Use `UIInputGuard.RaiseToFront(el)` to put an overlay above every sibling at every level. Any new full-screen modal that should freeze the world does the same.
- The HUD renders dark UI colours LIFTED (a dark blue `rgb(46,98,140)` shows as pale cyan, white text on it is hard to read): judge colours in the Game view, not by the number. Unity's Button theme paints focused/hover buttons itself - use `!important` (the base sheet does) to win. `letter-spacing` is the valid USS property, `-unity-text-letter-spacing` is not (do NOT bulk-fix the old uses: that would switch -1px tracking on across the HUD).
- New hotkey panel: implement `IUIPanel`, call `RegisterUI`, route the key through `UIKeyBindingManager.ToggleUI` — skipping any step fails silently. Sub-popups register as auxiliaries.
- A new placeable must be added to **both** `ObjDataRegistry` and the `BuildMenuUI` category list; run `Tools/ObjData/Auto-Assign Unique IDs`.

**Simulation / data**
- **Game clock rate (2026-11-10): 20 game seconds per real second at 1x** (Skyrim; was 60 = 1 real second per game minute). It is `SimulationTimeService.GameSecondsPerRealSecond` - one constant. The HUD speed buttons are `Time.timeScale` only (the clock's own `TimeSpeed` stays 1; setting both compounds). Anything that means "game minutes" but counts `Time.deltaTime` / `Time.time` must go through `GameClock` (`DeltaMinutes`, `ToMinutes`): done for `EquipmentBattery`, the jack watchdogs (`NoProgressGameMinutes`, held, dead-jack rider) and `OpsMetrics.Record` (callers still pass Time.time differences). Movement, animation and "real seconds" rules stay on `Time.deltaTime`. The clock-driven systems (wages, hourly costs, dock appointments, deadlines, truck dwell) read the sim clock and need nothing. Play starts paused (`ProjectSettings/TimeManager.asset` m_TimeScale 0).
- `transform.parent != null` does NOT mean "carried" (restored pallets sit under PlacedObjectsContainer). Use `OutboundPalletBuilder.IsOnFloor`.
- Any "within X metres" test on something moving by `speed*deltaTime` must cap the step or sweep the test (frame-rate dependence broke every reach-truck grab).
- A coroutine's `finally` does NOT run on `StopCoroutine`/destroy. Release locks/reservations explicitly (`ReleaseLaneLock`, `ReleaseStagingReservation`).
- Money: new distinct GL_Line ⇒ add a case in `FinanceCategory.ForGLLine`; one-time costs go through `MoneyService.Deduct`; restoring a balance is `RestoreCapital`, never `SetMoney` (it books a fake expense).
- Restore anything on load via the existing paths and call `RebuildFromRegistry()` for grid and economy. Don't read `Time.timeScale`-scaled time for UI.
- MHE navigation: rack approach anchors are at SHELF height — project to floor before NavMesh use. Machines never descend (yard-level NavMesh layer under the building). Parking an operated machine's nav = `GoIdleKeepOperator`, never `GoIdle`. Only `SetDriveMode` may flip the vehicle agent.
- **Machine travel goes through `MheMotor.Drive` / `MheMotor.Leg` (2026-11-18).** Never re-enable a machine's NavMeshAgent to move it, and never
  write a new hand-driven travel loop: the motor's sensor, right of way and the anti-clip guard are what keep machines apart.
- **Machines NEVER drive through racks, walls, barriers, other machines or pallets — NavMesh or not (Tad, 2026-11-07).** "No NavMesh there" is never a licence to slide through. Aisles have no MHE NavMesh, so jacks are scripted in them. Any new scripted machine move (turn, slide, walk-out) must step through `MachineClearance.StepBlocked` (`Actors/MachineClearance.cs`). Rack colliders are only click triggers on the beams, so physics can't see racks; the guard uses PlacementGrid solid cells plus each object's renderer outline. Floor shadow decals (`VehicleShadow_*`) are not part of a machine's footprint.
- **Pallet jack traffic (2026-11-07, `OrderSelectionTaskDriver.JackTraffic.cs`):** behind a manned jack with no room to pass → WAIT, with no time limit (the old 6 s "go anyway" caused the weaving and driving through). Two jacks blocking each other → the one with more room ahead goes. Queued 5 REAL seconds on the way to a bay → the selector steps off and walks to pick, max 10 m walking distance, routed round racks and jacks. A run resumed from a save must `SetJackDrive(false)` (it didn't, and the jack's own agent patrolled off with the selector).
- Two NavMesh agent types matter: default 0 is "Rat"; workers use "Human", vehicles "MHE" — query with the agent's own type.

**Avatars** (full detail: skill `ModularAvatarSystem`)
- Blender `Female_Body_Reg-White.blend` is the single source. Export ONLY with the `Unity_Avatar` preset, armature + every mesh, to `2. FBX/female_regular_body.fbx` (male: `Male/2. FBX/Male_Regular_Body.fbx`). Never leave a second FBX with the same mesh names in the folder. Stop Play → Scan & Rebuild Library → Finalize All Pending after every re-export. Empty material slots arrive as grey `Lit`.
- Never move humanoid-mapped bones (Eye_L/R, EyeEnd) in Blender. NSFW parts: weight 0, female, Dirty Dev gate.

**No interruptions (Tad, 2026-11-12):** bugs go to the LOG only. `BugFocus.Show` pauses nothing, moves no camera, writes no quicksave and shows no toast (it used to replace `quicksave.json`); HAL's purple pop (`PlaytestBot.HalPop`) writes to the Systems Log. The offload's "no room in any staging lane" and "using Door X instead" modals are log lines (`TrailerOffloadController`). Do not add new modal notices for things the sim can handle by itself.

**Process**
- **Verify the thing, not the value you set** — screenshot, measure, read the runtime log. Simulate risk mechanics a few hundred times before believing them.
- Change ONE thing per HAL playtest block so scores are comparable. Don't build anything from `FUTURE_IDEAS.md` unasked; use the `roadmap` skill when new ideas appear.
- Never commit unasked. "Wipe the slate" for HAL = delete HAL data only, never Tad's own saves.
- Tad's Unity/Blender may run on a different clone than this one: if "I don't see it", check `git ls-tree origin/<branch>` before assuming code is wrong.

---

## Latest session notes (2026-11-04, player block 1029, score ~61-63)

Details also in `Assets/_Project/Docs/Playtest/Cleanup_Plan_2026-11-04.md`. Commits `1101b7332` (UI + plan) and `e98eb8ad5` (fixes). **Fixes below are compiled but NOT yet proven in a played block** — run a block with no code changes and look for the new log lines.

- **Phantom pick face (OG3814).** `LocationRegistry.ReleasePhantomOccupancy` compared x/z only, so a reserve pallet on the beam ABOVE an empty pick face made it read "physical" for ever: face stayed Occupied/blank, `ReplenishmentService` skipped it, order's planned pallets were picked off empty shelves. Now also compares height (0.8 m). Rack levels share x/z — never test slot occupancy in 2D.
- **Planned pallets must not be burned.** A plan `OrderSelect` task whose selector finds every pick face empty used to finish Complete with 0 cases, so the order could never finish (and was cut at ship time). `OrderSelectionTaskDriver.HandPlanTaskBack` (used by both the on-foot and jack paths) returns it to Available and stands the selector down 45 s. Log line: "every pick face is empty - handing the task back".
- **Stale duplicate PalletPick (OG8851).** `RefilePalletPickShortfalls` re-filed a pick for a staged pallet whose pick was already Complete (its Putaway task was still live); the extra "Still to pick: 1" held a fully loaded truck. `TryFindStagedPalletAwaitingPutaway` now skips pallets with a Complete PalletPick, and `OrderService.CancelStalePalletPicks` (every 3 game-min) cancels unclaimed picks once an order `IsFullyPicked`. Log: "is fully picked - cancelled a leftover pallet pick".
- **Post-load phantoms.** `PalletInventoryTracker.SuppressPhantomsUntil` (set 4 s after `ApplySaveData`) stops it registering unidentified pallets as new "A Chep" stock before restored pallets are re-linked (was the OVERLAP/FLOATING pairs after F9).
- Null guards: `NegotiatorGestures.PoseArm`, `TrailerLoadController.PlaceCarriedInTrailer`. `PlaytestBot.OnLog` ignores its own `[PlaytestBot]` lines (it counted "PLAYER BLOCK DONE" as REPEATING). **Bug counts before/after this are not comparable.**
- UI: Accept Order tooltip shows pallet counts; Scheduler item table has an `Ord Qty` column (green covered / orange covered by inbound / red potential scratch — orange not yet seen live); message log default height 720 (clears the bottom bar).
- Earlier this session: carried/staged pallet persistence across F5/F9, jack charge-trip + double-parking fixes, case-pick x3 / pallet-pick +25% revenue, time-study metrics (TaskWait/TaskRun/JackMount/ReplenishA-C/ForkLift). Right-click "Charge Battery" menu removed (battery system stays).
- **Still open:** edge-of-floor / in-rack NavMesh targets ("cannot reach task position", "stuck, walking out"); "closed with a STAGED/IN TRANSIT pallet that never made the trailer" (watch after the duplicate-pick fix); jack staging stall (`jackBlocked`); economy dials stacked untested (revenue multipliers, handling fees, contract gates — HAL scores not comparable to old blocks); metrics tabs per-role filter.
- Gotchas learned: `execute_code` needs fully qualified names (`GameCore.Services.ServiceLocator`, `GameCore.Inventory.OrderService`, `GameCore.Labor.WorkQueueSystem`); after Play stops, statics persist but scene objects are gone, so pallet/location reads are NOT representative — check logs instead. Orders are shown by `OrderNumber` (OG####) but keyed by GUID `OrderId`.

## Current focus (as of 2026-11-02)

**Roadmap step 1 of 7: close the CORE LOOP** (see `roadmap` skill). Step 1 closes only when, with no code changes between blocks, three consecutive 7-day HAL blocks each show ≥80% orders on time, zero deadlocked doors/lanes, zero `REPEATING` bugs, positive profit, and no manual intervention. Then step 2 (Tutorial). Latest HAL blocks 27–28 scored ~58–59. Open items: rack double-booking overlaps, unreleased big orders, click-on-avatar/equipment info cards not opening (`[ClickProbe]` logs), staged pallets not persisted, reach-truck/dock-stocker reliability near NavMesh islands.

---

## Where the detail went

Full session history was moved out of this file on 2026-11-02 (it was 406 KB / ~100K tokens, loaded every session). It is all preserved verbatim, grouped by topic — `Read` the matching file only when working in that area:

| File | Contents |
|---|---|
| `docs/history/01_architecture_and_racking.md` | Pallet persistence, racking/aisle system, hybrid NavMesh MHE drive, dock lanes |
| `docs/history/02_ideas_waves_todo.md` | Lighting/Rat/Employee ideas, Wave 4/5 refactor, old TODO + bug lists |
| `docs/history/03_economy_inventory_loop.md` | Economy/payroll/Money, TopBar panels, Shift Manager, panel exclusivity, loop chunks |
| `docs/history/04_contracts_purchasing.md` | Contracts, scheduler, purchasing/vendors/Broker, reputation |
| `docs/history/05_graphics_ui_lanes_aug.md` | Graphics presets, floor collider, unscaled time, staging/lane locks |
| `docs/history/06_avatar_sep_oct.md` | AOD, modular avatar pipeline, Pimp My Employee |
| `docs/history/07_oct_gameplay_log.md` | Oct log: negotiation, release flow, batteries, soak tests, builds, HAL, metrics |
| `docs/history/08_latest_2026-10-31.md` | Latest addenda |
| `docs/history/09_playtest_benchmark_2026-11-02.md` | Playtest benchmark blocks 29-33: bot throughput cap, `AiNavigation` backstop fixes, cross-aisle layout (the old CURRENT WORK block) |

The system skills (table at top) are the first stop; these files are the archive. Original untouched copy: `docs/CLAUDE.md.backup-2026-11-02`. Add new session notes to the matching topic file (or a new `docs/history/` file), and put only *rules that always apply* in the Hard rules section above.
