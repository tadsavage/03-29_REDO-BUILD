# MHE traffic review and next steps (2026-11-14)

Scope: HAL blocks 42-51 (the jacks NavMesh vs grid A/B on slot 8), the jack / reach truck / dock stocker traffic code, the grid planner,
NavMesh agent types and surfaces, and the three machine prefabs. Every number below is from `Logs/Editor.log` (split per block) or the
`Block_NN.md` reports. Nothing here has been changed in code yet.

## 1. What the 10 blocks say

| Block | Jacks | Score | Shipped | Pallets | Reliability | RT stuck | Emp stuck | JackSolid | No progress | Leave jack | Grid no-route |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 42 | NavMesh | 42.2 | 4 | 15 | 17.5 | 53 | 81 | 44 | 42 | 29 | 0 |
| 43 | Grid | 39.0 | 4 | 13 | 18.9 | 60 | 91 | 51 | 41 | 25 | 3 |
| 44 | NavMesh | 41.6 | 4 | 15 | 19.0 | 52 | 71 | 37 | 32 | 18 | 0 |
| 45 | Grid | 26.3 | 2 | 8 | 9.0 | 73 | 158 | 45 | 73 | 19 | 44 |
| 46 | Grid | 42.8 | 4 | 14 | 23.6 | 81 | 86 | 52 | 45 | 26 | 0 |
| 47 | NavMesh | 30.9 | 2 | 10 | 14.8 | 40 | 67 | 30 | 33 | 19 | 0 |
| 48 | Grid | 11.3 | 0 | 0 | 18.9 | 34 | 75 | 38 | 50 | 21 | 2 |
| 49 | NavMesh | 40.2 | 4 | 17 | 16.3 | 48 | 74 | 40 | 36 | 21 | 0 |
| 50 | Grid | 35.8 | 4 | 12 | 13.2 | 68 | 95 | 47 | 35 | 25 | 26 |
| 51 | NavMesh | 35.3 | 2 | 12 | 15.3 | 69 | 88 | 43 | 32 | 19 | 0 |

Pallets shipped: NavMesh 69 in 5 blocks (13.8 a block), grid 47 in 5 blocks (9.4; 11.8 without block 48's zero).

**Conclusions**

- **The score is not a traffic measure.** Reliability (bug count) is 50% of it, so the grid arm is marked down for its own `[GridNav]` error
  lines. `JackStall` and "stopped short" lines are only written by the NavMesh leg, so those counts can't be compared across arms either.
  Use pallets shipped (and the recorder's held / corridor %) to compare.
- **On pallets shipped, the grid is not better yet.** Most of the gap is two blocks: 45 (44 no-route errors) and 48 (nothing shipped).
- **The jams are the same on both arms.** Reach trucks stuck 34-81 times a block, selectors stuck 67-158, `JackSolid` (a jack stopped
  because the next step would hit something) 30-52, selectors giving up "no progress" 32-73 and leaving their jack 18-29. Switching the jack's
  open-floor leg does not touch any of this, because the problems are in the shared traffic rules, the aisles and the dock, not in the leg planner.
- **Dock stockers** refuse 12-14 cross-dock trips a block because the only route runs down an aisle, and the offload logs "no reachable
  Inbound/Both staging-lane slot" 24-86 times a block.

## 2. Root causes, by the rule you asked for

### 2.1 Jacks must always agree who goes first in an aisle
There is no single authority. Each jack decides for itself, every frame, with three different tie-breaks that can disagree:
- `HasRightOfWayOver` (`OrderSelectionTaskDriver.JackTraffic.cs:251`): more room ahead wins, a tie goes to the lower `GetHashCode()`.
- `JackBufferHold` (`:347`): only the hash.
- `ShouldBackUpFor` (`:291`): whoever is further from its target loses, a tie goes to the higher hash.

On top of that, `AiNavigation.cs:244` gives every NavMeshAgent a **random** `avoidancePriority` (0-99) at start-up, so the NavMesh's own
avoidance picks a different winner every session, and it ignores the "reach trucks first" rule. The prefab values (jack 20, RT 30, DS 30) are
overwritten, and they were inverted anyway (in Unity a LOWER number is MORE important, so jacks outranked reach trucks).

**Fix: one aisle reservation.** An aisle (and each aisle-end turning box, 2.3) is entered only with a token from `AisleTraffic`. Tokens are
granted first come, first served at the mouth, in the aisle's one travel direction. Priority is one function used everywhere:
reach truck > dock stocker > loaded jack > empty jack, then whoever asked first. Remove the three hash tie-breaks and the random avoidance
priority; set fixed priorities (RT 10, DS 20, jacks 40).

### 2.2 Jacks must know when and how to pass each other
Today a jack held 0.4 real seconds behind a stopped jack passes if its next pick is more than 4 m beyond it (`PassMinPickAhead`), otherwise
waits 5 REAL seconds (`QueueWaitRealSeconds`) and walks up to 10 m. A one-sided aisle has no pass lane, so its jacks only queue.
**Fix:** decide once, when the jack ahead stops: it is PICKING (parked, selector off) -> pass if the aisle has a pass lane and the next pick is
beyond it; it is MOVING or queued -> follow at 1 m, never pass. Count the wait in game time, not real seconds, so 1x and 8x behave the same.

### 2.3 No pile-ups turning at aisle ends
`JackSolid` "(turning) stopped ... would have driven it into Full Bay ... / PltJack_FULL" is the most common jack event: jacks turn in place at the
aisle mouth and their 3.3 m outline swings into the rack end or the jack behind. Rule 3 in `AisleTraffic` (stop at the exit, wait for the
cross-aisle) only covers leaving.
**Fix:** a turning box at each aisle end, reserved like an aisle (2.1): one machine turns in it at a time, entering jacks wait OUTSIDE it in the
cross-aisle, and the turn is a swept arc into the cross-aisle (checked with `MachineClearance` before starting), not a spin at the mouth.

### 2.4 Staging without getting stuck
- `JackStall` 'staging lane NX line-up' is the second most common stall.
- `MheParking.TryFindIdleSpot(..., relaxed: true)` and `TryFindSpot` park idle machines in the **nearest empty staging lane**, and "plain floor"
  in this building is mostly the dock apron (z 48-59), the same corridor the recorder found holds 69% of all held time.
- Idle machines take a **lap** every 5-15 game minutes (`EquipmentBattery.LapRoutine`), adding traffic to the busiest area.

**Fix:** never park in a staging lane or on the apron; no idle laps; a line-up reservation per lane mouth (one jack lines up at a time, the next
waits beside the lane, not across the mouth).

### 2.5 Selectors step off and walk, never further than 5-6 m
`QueueWalkMax = 10f` (`JackTraffic.cs:32`): a queued selector walks up to 10 m of route. The normal pick reach is already 5 m
(`MaxPickReach`, `OnFootMaxPickReach`).
**Fix:** `QueueWalkMax` = 5 m. Beyond that the selector waits on the jack (or the jack passes, 2.2).

### 2.6 The jack always points nose first and never turns sideways in an aisle
- `DriveJackTo` (`PalletJack.cs:1700`) reverses up to `ReverseMaxDistance = 8 m` when `allowReverse` is set, and turns in place to face a target
  that can be up to ~37 degrees off the aisle line (`|dot| > 0.8`), then turns back.
- On open floor the NavMeshAgent leg turns the jack around its pivot (angular speed 100) wherever the path corner is, including straight back.
**Fix:** inside an aisle the heading is locked to the aisle's travel direction (lane changes at most `PassAngleDeg` 25 degrees); no reversing in
an aisle at all (a stop behind = walk, within 5 m); open-floor legs steer like a vehicle (turn while moving, minimum turn radius), which the
grid leg already does once 3.1 is fixed.

### 2.7 Dock stockers stay at their door, out of the lane ends, and off the lanes
Today a free dock stocker goes to `IdleRoutine` -> a random plain-floor spot (or an empty lane), takes laps, and crosses the dock by NavMesh
straight over lane mouths.
**Fix (your rule):**
- Each door has a home pocket on the dock face beside its door, away from the dock end of its staging lanes. A free stocker drives there,
  never to a lane, a charger row in the corridor, or the apron.
- Restless: about every 30 game seconds it either shuffles to the other side of its door (still on the dock face) or just changes facing. It
  never takes a lap.
- It gives way to any reach truck or jack within ~4 m of a lane end at its door before moving (they have the lane end).
- Cross-door trips run along the dock face (trailer side of the lane ends); crossing a shipping lane is a high cost in its route, used only when
  there is no other way.

**Done 2026-11-14 (step 2, the idle part):** `Actors/DockStockerHome.cs` + `EquipmentBattery` (IdleRoutine / RestlessRoutine).
- Pockets: one in each gap between two neighbouring doors on a wall (one machine fits per gap), plus one beyond each end door as a fallback.
  The machine lies along the wall, placed by its real outline (its pivot is near the front, the body runs ~2.6 m behind it), stepped away
  from the wall 0.25 m at a time until `MheParking.IsClear` passes (that check uses the ~2 m wide collider box + 0.15 m, which touches the
  wall/door frame at the hugging depth). In slot 8 the pockets land at z 38.24.
- No laps for dock stockers. Restless every 20-40 s: heading eased within +-10 degrees of the wall line, or a slide of up to 0.8 m along it,
  every step through `MachineClearance.StepBlocked`. A full turn does not fit the 4.6 m apron. Keeps still when a jack / reach truck moves within 4 m.
- Make-way: a parked one that another machine has been held behind for 1 real second (`EquipmentTrafficAgent.BlockedBy`) leaves for
  another pocket or an out-of-the-way spot. Never triggered in block 54.
- Lessons: v1 (pockets mid-apron, block 53) put parked stockers on the only route between doors: 6 cross-door offloads timed out behind
  them (the "agentOnMesh=False" in that log line is printed after the agent is switched off - it is always False). v2 first try: no pocket
  passed IsClear, and the retry dropped the corridor spot every 20-40 s, so they hopped around the corridor; stopped and fixed.

| | Block 54 (v2) | Block 53 (v1) | Block 52 (before) |
|---|---|---|---|
| Score / shipped / late / pallets | 43.4 / 4 / 0 / 12 | 32.6 / 3 / 1 / 12 | 44.3 / 4 / 0 / 14 |
| Cash change | +20,744 | +15,358 | +15,468 |
| Dock stocker stuck / refused (aisle route) | 0 / 4 | 0 / 2 | 12 / 14 |
| Cross-door trips failed / jacks stopped by a stocker | 0 / 0 | 6 / 3 | 0 / 5 |
| Selectors stuck | 35 | 34 | 69 |

**Not done in step 2:** cross-door and charging trips still go by NavMesh and may cross lanes (chargers are at z 57.59, behind the lanes);
an MHE NavMesh area cost on lane tiles for the dock stocker agent would make it avoid them. One block each, jacks on the grid in all three.

## 3. Navigation setup

- **NavMesh agent radius mismatch.** The single MHE NavMesh is baked for radius 0.5 m (`NavMeshAreas.asset`, agent type "MHE"), but the
  reach truck and dock stocker agents have radius 1.0 and the jack is a 3.3 m long body on a 0.5 m disc. Paths run closer to racks and walls
  than the machines fit, which is what "stuck ... walking out (12 m to mesh)" and the "straight way out runs through something solid" lines are.
  Either bake a separate type at the real half-width (RT / DS ~1.0 m) or move every machine onto the grid planner, which tests the real outline.
- **No MHE NavMesh in aisles**, so a reach truck in an aisle is off-mesh by design and every stop there becomes a "stuck, walking out" (5-19 a
  block of "stuck in aisle").
- `ReachTruckOperator` also logs "path lost mid-leg (likely a NavMesh rebake)": the mesh is rebuilt during play when objects change.
- Surfaces (Main.unity): three NavMeshSurfaces (Rat, Human, MHE), all "collect all objects / render meshes", no voxel or tile overrides. Fine.

## 4. Grid planner bug (blocks 43, 45, 48, 50)

All 75 grid "no route" errors are `NoPath, 1 expanded`: the search never leaves its start. Two places they start from:
- **The jack parking row** at z 57.59 (block 45: 44 errors from x -10.65 and -9.30). Parked jacks stand 1.35 m apart, so a neighbour's outline
  rasterises into the same 1.325 m cells as the jack being planned, and the row sits next to the floor edge (z ~58).
- **Inside the rack block** (blocks 43, 48, 50: 31 errors from x -20.6 .. -38.1, z 44.9 .. 48.5): a jack in or at an aisle whose heading or
  outline doesn't match the aisle cells it already touches (`FollowAisleTraffic` rejects any aisle cell not facing the travel direction).

`GridPlanner.Plan` (`GridPlanner.cs:167`) puts the START state on the open list without checking it, and every move out of it must pass `Fits`
for its whole outline. When the start already overlaps a blocked or wrong-heading cell, no neighbour fits and the search ends after one node.
**Fix:** a move out of the start only has to be free in the cells it newly enters, and the aisle-heading test ignores the cells the machine is
already on (or snap the start to the nearest fitting state, <= 1 cell); space the parking bays at least two cells apart. Add `GridPlannerTests`
cases: start overlapping a neighbour, start in an aisle facing the wrong way.

**Done 2026-11-14 (step 1):** `GridPlanner` now keeps the cells the outline covers at the start (`_startCells`) exempt from `Fits` for the
whole search; the goal check stays strict. New tests `LongMachine_StartingOverABlockedCell_DrivesOutOfIt` (the "1 expanded" case: a full-length
jack in a one-cell corridor over its neighbour's cell) and `StartExemption_DoesNotOpenBlockedCellsElsewhere_OrTheGoal`. 35/35 EditMode tests pass.
**Still open:** a jack entirely inside an aisle facing AGAINST the traffic has no legal move under the one-way rule (no reverse in an aisle, no
sideways turn), so it still gets NoPath. That is a rules decision for step 5 (let it drive out forward, or reverse out), not a planner bug.
**Live check, block 52 (jacks on the grid, slot 8, same settings):** score 44.3, 4 shipped / 0 late / 14 pallets, **0 `[GridNav]` lines**.
Also lower than every earlier block: no-progress 17 (was 32-73), leave-jack 7 (18-29), jack-into-jack 8 (15-34). One block only.

## 5. Next steps (in order, one change per block)

1. **Grid start-state fix** (section 4) + unit test. Small, isolated, and every grid comparison is meaningless until it's in.
2. **Dock stocker home pockets, restless shuffle, no laps, no lane/apron parking, dock-face routing** (2.7, part of 2.4). Your explicit rule; it
   also clears the corridor for everyone.
3. **One aisle authority** (2.1): aisle and turning-box tokens, one priority function, fixed avoidance priorities, remove the random priority.
4. **Aisle-end turning box + swept turns** (2.3).
5. **Nose-first rules** (2.6) and **pass-or-follow** (2.2), with `QueueWalkMax` = 5 m (2.5) and game-time waits.
6. **Staging line-up reservation** (2.4).
7. **Then** decide NavMesh vs grid for open floor (radius fix vs grid for all machines), re-run the A/B with the recorder metrics, not the score.

Measure each step on slot 8: pallets shipped, recorder held % and corridor %, and the counts in the table above (RT stuck, JackSolid, no
progress, leave jack). The block score stays useful only as a bug count.
