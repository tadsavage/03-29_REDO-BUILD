# Playtest block 64 — days 1–3

## Score: **33.3 (+17.1)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (1 shipped, 1 late) | 10% | 50.0 (+50.0) |
| Fill rate | 10% | 100.0 (+100.0) |
| Throughput (5 plt vs target 24) | 10% | 20.8 (+20.8) |
| Revenue ($800) | not scored | 5.0 (+5.0) |
| Net Profit (-18,082) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 12.5 (-0.6) |
| Reputation (19 to 56) | 10% | 100.0 (+3.3) |

Orders shipped **1**, late/fined **1**, pallets shipped **5**. Cash $127,764 → $120,990 (-6,774). Stock at cost $200,106 → $188,798 (offsets stock bought in Net Profit).
Bot did: 2 contracts signed, 2 dock bookings, 1 orders released, 1 POs, 0 rush POs.

## Where the money went

Income **$800**. Expenses **$7,574** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 800 |

| Expense | $ |
|---|---|
| Wages | 2,835 |
| Maintenance | 1,618 |
| Fines | 1,056 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Inventory | 480 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x loaded but the truck had not left

- day 2: OG2624 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,056. Lines 27, 248 cs, released=True.

## Change since last block
- Fixed / no longer seen: **4**
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__#.MoveNext () [#x#f#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **9**
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'staging lane #E wait' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'staging lane #E line-up' was interrupted # times - giving the leg up.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - REPEATING|x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (#, #, #) - the next move would violate the # m vehicle clearance from PltJack_FULL(Clone).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-c#ee#f#-a#d-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##d#b#-af#a## but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (24 distinct, 62 incidents)

| Incidents | Bug |
|---|---|
| 6 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 8 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 3 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 3 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 7 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 3 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 5 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 3 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': 'staging lane #E line-up' was interrupted # times - giving the leg up. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': 'staging lane #E wait' was interrupted # times - giving the leg up. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': 'staging lane #E line-up' was interrupted # times - giving the leg up. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (#, #, #) - the next move would violate the # m vehicle clearance from PltJack_FULL(Clone). |
| 1 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-c#ee#f#-a#d-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##d#b#-af#a## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.09, 1.21, 52.29) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (12.88, 1.15, 36.88) — walking out to (-0.12, 1.11, 36.89) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (1.90, 1.18, 42.18) — walking out to (-9.35, 1.08, 35.37) (12.15m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 4e64b88f-c1ee-4f10-a03d-a305d54141d6 but the employee is 4.5m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-22.85, 1.19, 47.66).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-34.37, 1.19, 49.88) — walking out to (-22.85, 1.19, 47.66) (11.73m to mesh).
- **ERROR** (day 1): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 2ccd4c4b-900d-46b6-af5a-8e05290ac78e but the employee is 10.0m away (max 4.5m) — refusing to start receiving.
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B1 anchor: NO NAVMESH PATH. anchor=(-27.21, 5.11, 49.29) → driveTarget=(-27.21, 1.21, 49.29), from=(-9.54, 1.11, 49.88), elapsed=31.8s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=17.7, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 143.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-22.03, 1.11, 41.40) — walking out to (-9.47, 1.25, 41.40) (12.00m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 108/248.
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-03-B1 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 40.71) → driveTarget=(-23.24, 1.21, 40.71), from=(-7.53, 1.25, 46.91), elapsed=31.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 206.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] RT_Full(Clone) cannot reach task position at (-8.66, 1.24, 48.99) (path invalid) — abandoning the walk and handing the task back.
- **OVERLAP** (day 1): OVERLAP Pallet_01_Slot1_Tier0 & Pallet_00_Slot0_Tier0 @(33, 83)  <<HISTORY: d1 15:21 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 16:01 Replenish task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 16:42 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 16:47 moved (31, 78)->(-1, -1) by InventoryService.MovePallet | d1 16:54 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 16:54 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 15:26 Putaway task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 15:28 moved (-1, -1)->(43, 85) by InventoryService.MovePallet | d1 16:53 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 16:56 moved (43, 85)->(-1, -1) by InventoryService.MovePallet | d1 17:02 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 17:02 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-19.19, 1.13, 42.68) — walking out to (-30.40, 1.08, 35.17) (12.65m to mesh).
- **VEHICLE-STATIONARY** (day 2): RT_Full(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- **VEHICLE-STATIONARY** (day 2): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 26 task(s) it could take wait
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-13-A1 anchor: NO NAVMESH PATH. anchor=(-21.91, 3.11, 48.66) → driveTarget=(-21.91, 1.21, 48.66), from=(-15.89, 1.23, 45.83), elapsed=30.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=6.7, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 175.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Mark Peterson': 'staging lane 3E line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:103)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': 'staging lane 3E wait' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:103)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone): stuck at (-11.06, 1.11, 46.52) — walking out to (-12.56, 1.25, 46.52) (12.54m to mesh).
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': 'staging lane 3E line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:103)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-11-A1 anchor: NO NAVMESH PATH. anchor=(-33.84, 3.11, 40.71) → driveTarget=(-33.84, 1.17, 40.71), from=(-5.28, 1.23, 49.32), elapsed=32.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 204.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 2): x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (-13.04, 1.11, 45.51) - the next move would violate the 1 m vehicle clearance from PltJack_FULL(Clone).

## Bot actions (first 400)
- d1 signed bulk BULK_Crunch-Time Cereals_D1_0
- d1 PO IG1098 Bulk Basin Foods: 4 plt $480
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 3
- d1 booked Crunch-Time Cereals day 2 block 44 door 2 (pallet-only, 5 pallet pick(s) / 2 reach truck(s): stock ready hour 18 + 27.4h of work)
- d1 released 1 order(s): OG2775 (trailer 26.8 h away; 25.3 h work + 2 h)
- d2 signed bulk BULK_Bramble & Bite Berry Co._D2_0
