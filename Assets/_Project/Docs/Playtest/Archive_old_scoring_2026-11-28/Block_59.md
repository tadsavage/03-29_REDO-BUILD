# Playtest block 59 — days 1–3

## Score: **11.2 (-15.7)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 2 late) | 10% | 0.0 (-50.0) |
| Fill rate | 10% | 0.0 (-100.0) |
| Throughput (0 plt vs target 24) | 10% | 0.0 (-29.2) |
| Revenue ($0) | not scored | 0.0 (-100.0) |
| Net Profit (-51,610) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 18.7 (+4.4) |
| Reputation (19 to 0) | 10% | 18.3 (0.0) |

Orders shipped **0**, late/fined **2**, pallets shipped **0**. Cash $127,764 → $117,558 (-10,206). Stock at cost $200,106 → $158,702 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 6 dock bookings, 0 orders released, 1 POs, 0 rush POs.

## Where the money went

Income **$0**. Expenses **$10,206** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|

| Expense | $ |
|---|---|
| Fines | 4,056 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Inventory | 112 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x still being picked
- 1 x never released

- day 2: OG2624 (Sneaky Pete's Seafood) was still being picked (114/248 cs). Fined $1,056. Lines 27, 248 cs, released=True.
- day 3: OG2759 (Twisted Twine Pasta) was never released to the floor. Fined $3,000. Lines 1, 200 cs, released=False.

## Change since last block
- Fixed / no longer seen: **13**
  - ERROR|[TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #E: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryGoAround>d__#.MoveNext () [#x#f] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'staging lane #B wait' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'staging lane #B wait' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'bay ##' from (#, #, #) to (#, #, #) (GoalBlocked, # expanded) - the jack will not drive it.
  - WARN|[OrderSelectionTaskDriver] Order #-d#cd#e#e#f#-# (Fizz-Pop Soda Works) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order #-be##-af### (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).
  - WARN|[OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
- New this block: **3**
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress on order OG# for # game minutes - handing the task back to the queue.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' made no progress on order OG# for # game minutes - staging the # case(s) picked and freeing the rest.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' leaving the pallet jack (no progress) - its # case(s) stay on the jack for the next free selector to take over (order OG#).

## Bugs found (13 distinct, 75 incidents)

| Incidents | Bug |
|---|---|
| 22 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 19 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 16 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 3 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 4 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress on order OG# for # game minutes - handing the task back to the queue. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' made no progress on order OG# for # game minutes - staging the # case(s) picked and freeing the rest. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' leaving the pallet jack (no progress) - its # case(s) stay on the jack for the next free selector to take over (order OG#). |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (10.59, 1.20, 45.94) — walking out to (4.09, 1.09, 35.17) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-36.40, 1.11, 42.34).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-24.88, 1.11, 43.21) — walking out to (-36.40, 1.11, 42.34) (11.55m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (11.17, 1.19, 40.84) — walking out to (11.17, 1.14, 53.84) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-26.86, 1.12, 47.82) — walking out to (-26.86, 0.04, 34.46) (12.36m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 91/248.
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-24.05, 1.22, 46.90), elapsed=30.8s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=15.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__141.MoveNext () [0x00786] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2769 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 428.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- **VEHICLE-STATIONARY** (day 1): RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress on order OG3625 for 10 game minutes - handing the task back to the queue.
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' made no progress on order OG2624 for 10 game minutes - staging the 14 case(s) picked and freeing the rest.
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' leaving the pallet jack (no progress) - its 14 case(s) stay on the jack for the next free selector to take over (order OG2624).
- **REPEATING** (day 2): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.

## Bot actions (first 400)
- d1 signed bulk BULK_Twisted Twine Pasta_D1_0
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d2 PO IG2073 Cornerstone Staples: 1 plt $112
- d2 placed parked Inbound Cornerstone Staples day 2 block 36 door 2
- d2 booked Sneaky Pete's Seafood day 4 block 0 door 3 (case-pick, 2 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 45 + 26.0h of work)
- d2 booked Sneaky Pete's Seafood day 4 block 2 door 3 (case-pick, 2 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 46 + 26.0h of work)
- d2 booked Sneaky Pete's Seafood day 4 block 4 door 3 (case-pick, 2 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 47 + 26.0h of work)
