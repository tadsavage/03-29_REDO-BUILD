# Playtest block 58 — days 1–3

## Score: **26.9 (-12.4)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (1 shipped, 1 late) | 10% | 50.0 (-50.0) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (7 plt vs target 24) | 10% | 29.2 (-20.8) |
| Revenue ($21,909) | not scored | 100.0 (0.0) |
| Net Profit (-60,682) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 14.3 (-5.0) |
| Reputation (19 to 0) | 10% | 18.3 (-28.3) |

Orders shipped **1**, late/fined **1**, pallets shipped **7**. Cash $127,764 → $122,667 (-5,097). Stock at cost $200,106 → $144,521 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 11 dock bookings, 1 orders released, 6 POs, 0 rush POs.

## Where the money went

Income **$21,909**. Expenses **$27,006** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 21,117 |
| Handling | 792 |

| Expense | $ |
|---|---|
| Inventory | 17,248 |
| Fines | 3,720 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x still being picked

- day 3: OG2756 (Blocky Bakeshop) was still being picked (464/630 cs). Fined $3,720. Lines 2, 630 cs, released=True.

## Change since last block
- Fixed / no longer seen: **10**
  - ERROR|[TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #B: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - WARN|[AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order #-be##-af### (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[OrderSelectionTaskDriver] Order ###bc#c#d-# (Cluck & Cover Poultry) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ###d-a### but the employee is #m away (max #m) — refusing to start receiving.
- New this block: **13**
  - ERROR|[TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #E: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryGoAround>d__#.MoveNext () [#x#f] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'staging lane #B wait' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'staging lane #B wait' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'bay ##' from (#, #, #) to (#, #, #) (GoalBlocked, # expanded) - the jack will not drive it.
  - WARN|[OrderSelectionTaskDriver] Order #-d#cd#e#e#f#-# (Fizz-Pop Soda Works) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-be##-af### (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).
  - WARN|[OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.

## Bugs found (23 distinct, 56 incidents)

| Incidents | Bug |
|---|---|
| 10 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 5 | ERROR — [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #E: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 6 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 6 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 6 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryGoAround>d__#.MoveNext () [#x#f] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': no route for 'staging lane #B wait' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'staging lane #B wait' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 3 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': no route for 'bay ##' from (#, #, #) to (#, #, #) (GoalBlocked, # expanded) - the jack will not drive it. |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 2 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-d#cd#e#e#f#-# (Fizz-Pop Soda Works) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-be##-af### (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |

## First sightings (with stack traces)

- **ERROR** (day 1): [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → 3E: NO NAVMESH PATH. anchor=(9.89, 1.11, 39.69) → driveTarget=(9.89, 1.22, 39.69), from=(-12.72, 1.15, 37.87), elapsed=30.1s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerOffloadController/<SeekViaNavMesh>d__60:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:1224)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- **VEHICLE-STATIONARY** (day 1): RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B0 anchor: NO NAVMESH PATH. anchor=(-28.54, 5.11, 49.29) → driveTarget=(-28.54, 1.20, 49.29), from=(-19.63, 1.20, 49.89), elapsed=30.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=8.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [0x00025] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2375 < at GameCore.Actors.ReachTruckOperator+<TryGoAround>d__155.MoveNext () [0x0029f] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:3020 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 462.8s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 108/248.
- **ERROR** (day 1): [GridNav] 'Employee_Mark Peterson': no route for 'staging lane 1B wait' from (-16.26, 1.11, 47.66) to (-11.31, 1.11, 33.34) (NoPath, 1777 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:70)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **ERROR** (day 1): [GridNav] 'Employee_Shirley Edwards': no route for 'staging lane 1B wait' from (-16.26, 1.11, 47.66) to (-11.31, 1.11, 33.34) (NoPath, 1777 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:70)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.16, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **WARN** (day 2): [AiNavigation] Employee_Larry Morgan: stuck at (-9.38, 1.14, 55.09) — walking out to (-9.38, 1.25, 42.09) (12.00m to mesh).
- **WARN** (day 2): [AiNavigation] Employee_Douglas Garcia: stuck at (-6.30, 1.12, 59.12) — walking out to (0.20, 1.19, 47.86) (12.00m to mesh).
- **WARN** (day 2): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-37.90, 1.11, 42.34).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a (Fizz-Pop Soda Works) has no reachable pick location for its remaining line item(s) — staging short at 41/44.
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-09-B0 anchor: NO NAVMESH PATH. anchor=(-29.86, 5.11, 40.71) → driveTarget=(-29.86, 1.18, 40.71), from=(-20.67, 1.23, 44.17), elapsed=30.2s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=10.5, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 297.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] Employee_Mary Young: stuck at (-8.05, 1.12, 58.44) — walking out to (-8.05, 1.20, 45.44) (12.00m to mesh).
- **WARN** (day 2): [AiNavigation] Employee_Shirley Edwards: stuck at (-16.68, 1.14, 46.59) — walking out to (-23.16, 0.04, 34.58) (12.64m to mesh).
- **OVERLAP** (day 2): OVERLAP Pallet_01_Slot0_Tier1 & Pallet_11_Slot7_Tier1 @(27, 78)  <<HISTORY: d2 06:48 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 07:28 Putaway task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d2 07:28 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 07:45 moved (57, 85)->(-1, -1) by InventoryService.MovePallet | d2 08:50 moved (-1, -1)->(27, 78) by InventoryService.MovePallet | d2 08:50 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 09:57 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 22:06 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 22:34 moved (57, 82)->(-1, -1) by InventoryService.MovePallet | d2 01:25 moved (-1, -1)->(27, 78) by InventoryService.MovePallet | d2 01:25 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 01:26 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-04-B1 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 41.34) → driveTarget=(-23.24, 1.22, 41.34), from=(-9.99, 1.24, 48.10), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=15.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__141.MoveNext () [0x00786] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2769 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 171.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] Employee_Mark Peterson: stuck at (-10.74, 1.19, 47.86) — walking out to (-10.74, 1.08, 35.17) (12.00m to mesh).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 368110a7-be45-4027-af00-3c57e3568069 (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at 41/53 case(s).
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': no route for 'bay 02-01 (aisle entry)' from (-35.12, 1.11, 42.34) to (-40.81, 1.11, 47.66) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:70)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 02-01' - driving straight to the stop instead.
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': no route for 'bay 02-01' from (-35.12, 1.11, 42.34) to (-37.14, 1.11, 47.66) (GoalBlocked, 0 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:70)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```

## Bot actions (first 400)
- d1 signed bulk BULK_Blocky Bakeshop_D1_0
- d1 PO IG1067 Homestead Market Co.: 3 plt $5,472
- d1 placed parked Inbound Homestead Market Co. day 1 block 26 door 3
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 PO IG1068 Bulk Basin Foods: 5 plt $2,880
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 2
- d1 placed parked Inbound Homestead Market Co. day 1 block 26 door 3
- d1 placed parked Inbound Bulk Basin Foods day 1 block 28 door 2
- d1 PO IG1069 Cornerstone Staples: 1 plt $576
- d1 placed parked Inbound Cornerstone Staples day 1 block 37 door 1
- d1 PO IG1070 Millbrook Family Foods: 3 plt $5,472
- d1 placed parked Inbound Millbrook Family Foods day 1 block 44 door 1
- d1 PO IG1071 Cornerstone Staples: 4 plt $2,304
- d1 placed parked Inbound Cornerstone Staples day 2 block 0 door 1
- d2 booked Blocky Bakeshop day 3 block 11 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 12 pallet pick(s) / 2 reach truck(s): stock ready hour 30 + 17.5h of work)
- d2 released 1 order(s): OG2756 (trailer 17.8 h away; 15.9 h work + 2 h)
- d2 PO IG2072 Iron Gate Staples: 1 plt $544
- d2 placed parked Inbound Iron Gate Staples day 2 block 36 door 2
