# Playtest block 56 — days 1–3

## Score: **35.6 (-4.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (3 shipped, 1 late) | 10% | 75.0 (+8.3) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (12 plt vs target 24) | 10% | 50.0 (+4.2) |
| Revenue ($32,109) | not scored | 100.0 (0.0) |
| Net Profit (-72,224) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 20.5 (-7.7) |
| Reputation (19 to 6) | 10% | 28.3 (-23.3) |

Orders shipped **3**, late/fined **1**, pallets shipped **12**. Cash $127,764 → $119,912 (-7,852). Stock at cost $200,106 → $135,734 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 18 dock bookings, 1 orders released, 13 POs, 0 rush POs.

## Where the money went

Income **$32,109**. Expenses **$39,961** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 31,257 |
| Handling | 852 |

| Expense | $ |
|---|---|
| Inventory | 30,344 |
| Fines | 3,579 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x still being picked

- day 3: OG2749 (Cluck & Cover Poultry) was still being picked (0/679 cs). Fined $3,579. Lines 3, 679 cs, released=True.

## Change since last block
- Fixed / no longer seen: **3**
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ### anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
- New this block: **8**
  - ERROR|[TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #B: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - ERROR|"IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  - OVERLAP| A Chep & A Chep @(#, #)
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Charles Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[InventoryService] Lane #B lock was held for over #s by reach truck RT_Full(Clone) (Amanda Dixon, Putaway) - treating it as leaked and taking it over.

## Bugs found (16 distinct, 49 incidents)

| Incidents | Bug |
|---|---|
| 9 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | ERROR — [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #B: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 8 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | OVERLAP —  A Chep & A Chep @(#, #) |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 2 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | WARN — [AiNavigation] Employee_Charles Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [InventoryService] Lane #B lock was held for over #s by reach truck RT_Full(Clone) (Amanda Dixon, Putaway) - treating it as leaked and taking it over. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.09, 1.21, 52.29) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (12.96, 1.16, 36.99) — walking out to (6.46, 1.16, 48.25) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (12.14, 1.24, 36.18) — walking out to (12.14, 1.14, 49.18) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Charles Edwards: stuck at (-0.01, 1.11, 36.89) — walking out to (13.14, 1.21, 36.89) (12.15m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-13.96, 1.11, 57.59) — walking out to (-13.96, 1.16, 59.00) (12.22m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-20.06, 1.11, 47.66).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-10.88, 1.15, 52.91) — walking out to (-10.88, 1.12, 39.86) (12.04m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 108/248.
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-9.37, 1.16, 55.09) — walking out to (-2.87, 1.22, 43.83) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-15.41, 1.17, 43.92) — walking out to (-28.41, 1.12, 43.42) (12.01m to mesh).
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **ERROR** (day 2): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **OVERLAP** (day 2): OVERLAP A Chep & A Chep @(22, 84)  <<HISTORY: d2 14:45 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 15:25 Replenish task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d2 15:38 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 15:47 moved (24, 84)->(-1, -1) by InventoryService.MovePallet | d2 15:54 moved (-1, -1)->(23, 84) by InventoryService.MovePallet | d2 15:54 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d2 14:29 moved (23, 83)->(-1, -1) by InventoryService.MovePallet | d2 14:41 Replenish task Assigned->Available by WorkQueueSystem.ReleaseStaleAssignments | d2 14:41 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 14:41 Replenish task Assigned->Cancelled by ReachTruckOperator.ReplenishRoutine | d2 15:36 moved (-1, -1)->(23, 84) by InventoryService.MovePallet | d2 15:36 Replenish task Cancelled->Complete by WorkQueueSystem.CompleteTask>>
- **ERROR** (day 2): [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → 1B: NO NAVMESH PATH. anchor=(-11.31, 1.11, 39.69) → driveTarget=(-11.31, 1.16, 39.69), from=(-7.59, 1.11, 41.31), elapsed=30.2s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerOffloadController/<SeekViaNavMesh>d__60:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:1224)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [InventoryService] Lane 1B lock was held for over 120s by reach truck RT_Full(Clone) (Amanda Dixon, Putaway) - treating it as leaked and taking it over.
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-05-B0 anchor: NO NAVMESH PATH. anchor=(-33.84, 5.11, 48.66) → driveTarget=(-33.84, 1.19, 48.66), from=(-17.51, 1.20, 49.63), elapsed=31.6s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=16.4, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 396.7s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```

## Bot actions (first 400)
- d1 signed bulk BULK_Cluck & Cover Poultry_D1_0
- d1 PO IG1045 Homestead Market Co.: 3 plt $5,472
- d1 placed parked Inbound Homestead Market Co. day 1 block 26 door 3
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 PO IG1046 Bulk Basin Foods: 3 plt $1,440
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 2
- d1 placed parked Inbound Homestead Market Co. day 1 block 26 door 3
- d1 placed parked Inbound Bulk Basin Foods day 1 block 28 door 2
- d1 PO IG1047 Cornerstone Staples: 2 plt $960
- d1 placed parked Inbound Cornerstone Staples day 1 block 37 door 1
- d1 PO IG1048 Millbrook Family Foods: 3 plt $5,472
- d1 placed parked Inbound Millbrook Family Foods day 1 block 44 door 1
- d1 PO IG1049 Cornerstone Staples: 2 plt $960
- d1 placed parked Inbound Cornerstone Staples day 1 block 46 door 1
- d2 PO IG2050 Riverside Housewares: 2 plt $880
- d2 placed parked Inbound Riverside Housewares day 2 block 8 door 1
- d2 PO IG2051 Homestead Market Co.: 3 plt $5,700
- d2 placed parked Inbound Homestead Market Co. day 2 block 14 door 1
- d2 PO IG2052 Riverside Housewares: 2 plt $880
- d2 placed parked Inbound Riverside Housewares day 2 block 16 door 1
- d2 PO IG2053 Anchor Point Foods: 2 plt $960
- d2 placed parked Inbound Anchor Point Foods day 2 block 26 door 1
- d2 PO IG2054 Millbrook Family Foods: 3 plt $5,700
- d2 placed parked Inbound Millbrook Family Foods day 2 block 32 door 1
- d2 PO IG2055 Anchor Point Foods: 2 plt $960
- d2 placed parked Inbound Anchor Point Foods day 2 block 34 door 2
- d2 PO IG2056 Bulk Basin Foods: 1 plt $480
- d2 placed parked Inbound Bulk Basin Foods day 2 block 44 door 1
- d2 booked Cluck & Cover Poultry day 3 block 14 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 12 pallet pick(s) / 2 reach truck(s): stock ready hour 50 + 4.2h of work)
- d2 released 1 order(s): OG2749 (trailer 8.8 h away; 8.5 h work + 2 h)
- d2 PO IG2057 Bulk Basin Foods: 1 plt $480
- d2 placed parked Inbound Bulk Basin Foods day 2 block 46 door 1
