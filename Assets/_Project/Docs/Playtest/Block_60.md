# Playtest block 60 — days 1–3

## Score: **14.3 (+3.2)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 1 late) | 10% | 0.0 (0.0) |
| Fill rate | 10% | 0.0 (0.0) |
| Throughput (0 plt vs target 24) | 10% | 0.0 (0.0) |
| Revenue ($0) | not scored | 0.0 (0.0) |
| Net Profit (-135,331) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 91.4 (-8.6) |
| Reliability (bugs) | 50% | 25.0 (+6.3) |
| Reputation (19 to 0) | 10% | 18.3 (0.0) |

Orders shipped **0**, late/fined **1**, pallets shipped **0**. Cash $127,764 → $13,394 (-114,370). Stock at cost $200,106 → $179,145 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 19 dock bookings, 0 orders released, 14 POs, 0 rush POs.

## Where the money went

Income **$0**. Expenses **$114,370** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|

| Expense | $ |
|---|---|
| Inventory | 107,276 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Fines | 1,056 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x loaded but the truck had not left

- day 2: OG2624 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,056. Lines 27, 248 cs, released=True.

## Change since last block
- Fixed / no longer seen: **6**
  - WARN|[AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #).
  - WARN|[AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress on order OG# for # game minutes - handing the task back to the queue.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' made no progress on order OG# for # game minutes - staging the # case(s) picked and freeing the rest.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' leaving the pallet jack (no progress) - its # case(s) stay on the jack for the next free selector to take over (order OG#).
- New this block: **5**
  - ERROR|[TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #E: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - TRUCK-OVERLAP| Truck→PO_IG# & Truck→PO_IG# at (#, #, #)
  - WARN|[AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ec##b#d## but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (12 distinct, 34 incidents)

| Incidents | Bug |
|---|---|
| 9 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 7 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 8 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #E: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 2 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ec##b#d## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (12.14, 1.17, 36.10) — walking out to (5.64, 1.23, 47.36) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.20, 1.22, 51.71) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-12.00, 1.12, 58.93) — walking out to (-12.00, 1.22, 45.93) (12.00m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 98c3775e-16ec-4529-9b1d-66e6a432e2d4 but the employee is 14.6m away (max 4.5m) — refusing to start receiving.
- **ERROR** (day 1): [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → 3E: NO NAVMESH PATH. anchor=(9.89, 1.11, 39.69) → driveTarget=(9.89, 1.25, 39.69), from=(5.85, 1.13, 35.51), elapsed=30.2s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerOffloadController/<SeekViaNavMesh>d__60:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:1224)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 145/248.
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- **TRUCK-OVERLAP** (day 1): TRUCK-OVERLAP Truck→PO_IG1916 & Truck→PO_IG1915 at (-6.4, 0.0, 17.9)
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **VEHICLE-STATIONARY** (day 2): RT_Full(Clone) has a driver, hasn't moved in 360s while 6 task(s) it could take wait
- **WARN** (day 2): [AiNavigation] Employee_Mark Peterson: stuck at (-15.79, 1.15, 46.96) — walking out to (-22.25, 0.04, 34.58) (12.97m to mesh).
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-16.42, 1.11, 44.20), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=23.5, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__141.MoveNext () [0x00786] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2769 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 354.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```

## Bot actions (first 400)
- d1 signed bulk BULK_Odd-Shape Orchards_D1_0
- d1 PO IG1074 Backdoor Bill's Overstock: 2 plt $3,200
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 26 door 3
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 PO IG1075 Ambrose Fine Foods: 6 plt $19,152
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 2
- d1 PO IG1076 Millbrook Family Foods: 1 plt $720
- d1 placed parked Inbound Millbrook Family Foods day 1 block 26 door 3
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 27 door 3
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 28 door 2
- d1 placed parked Inbound Millbrook Family Foods day 1 block 29 door 3
- d1 PO IG1077 Millbrook Family Foods: 1 plt $720
- d1 placed parked Inbound Millbrook Family Foods day 1 block 38 door 1
- d1 PO IG1078 Blue Ridge Provisions: 3 plt $4,800
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 44 door 1
- d1 PO IG1079 Ambrose Fine Foods: 7 plt $21,756
- d1 placed parked Inbound Ambrose Fine Foods day 2 block 0 door 1
- d1 PO IG1080 Millbrook Family Foods: 1 plt $720
- d1 placed parked Inbound Millbrook Family Foods day 1 block 47 door 1
- d2 PO IG2081 Homestead Market Co.: 1 plt $816
- d2 placed parked Inbound Homestead Market Co. day 2 block 8 door 1
- d2 PO IG2082 Backdoor Bill's Overstock: 3 plt $5,100
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 14 door 1
- d2 PO IG2083 Ambrose Fine Foods: 7 plt $21,756
- d2 placed parked Inbound Ambrose Fine Foods day 2 block 16 door 1
- d2 PO IG2084 Homestead Market Co.: 1 plt $816
- d2 placed parked Inbound Homestead Market Co. day 2 block 18 door 2
- d2 PO IG2085 Homestead Market Co.: 1 plt $864
- d2 placed parked Inbound Homestead Market Co. day 2 block 26 door 1
- d2 PO IG2086 Blue Ridge Provisions: 3 plt $5,100
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 32 door 1
- d2 PO IG2087 Ambrose Fine Foods: 7 plt $21,756
- d2 placed parked Inbound Ambrose Fine Foods day 2 block 34 door 2
