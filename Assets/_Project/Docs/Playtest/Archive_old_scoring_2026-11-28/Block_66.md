# Playtest block 66 — days 1–3

## Score: **38.1 (+18.3)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (1 shipped, 1 late) | 10% | 0.0 (0.0) |
| Fill rate | 10% | 100.0 (+100.0) |
| Throughput (8 plt vs target 24) | 10% | 33.3 (+33.3) |
| Revenue ($21,969) | not scored | 100.0 (+100.0) |
| Net Profit (+1,793) | 10% | 79.9 (+79.9) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 13.5 (-6.7) |
| Reputation (19 to 53) | 10% | 100.0 (+3.3) |

Orders shipped **1**, late/fined **1**, pallets shipped **8**. Cash $127,764 → $138,967 (+11,203). Stock at cost $200,106 → $190,696 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 2 dock bookings, 1 orders released, 1 POs, 0 rush POs.

## Where the money went

Income **$21,969**. Expenses **$10,766** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 21,117 |
| Handling | 852 |

| Expense | $ |
|---|---|
| Inventory | 3,672 |
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
- Fixed / no longer seen: **5**
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - REPEATING|x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (#, #, #) - the next move would violate the # m vehicle clearance from PltJack_FULL(Clone).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##d#f##-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **8**
  - ERROR|"IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #).
  - WARN|[AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'staging lane #A line-up' was interrupted # times - giving the leg up.
  - WARN|[AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Kelly Harmon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (16 distinct, 75 incidents)

| Incidents | Bug |
|---|---|
| 10 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 10 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 4 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 11 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 11 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 4 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 3 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 5 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 4 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 3 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': 'staging lane #A line-up' was interrupted # times - giving the leg up. |
| 2 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Kelly Harmon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.09, 1.21, 52.29) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (12.88, 1.16, 36.88) — walking out to (-0.12, 1.11, 36.89) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Kelly Harmon: stuck at (7.30, 1.15, 50.99) — walking out to (-3.96, 1.22, 44.49) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (3.15, 1.11, 48.06) — walking out to (3.72, 1.23, 49.45) (12.49m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-10.41, 1.12, 37.95) — walking out to (-23.41, 1.10, 38.64) (12.02m to mesh).
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B0 anchor: NO NAVMESH PATH. anchor=(-28.54, 5.11, 49.29) → driveTarget=(-28.54, 1.20, 49.29), from=(-26.16, 1.11, 49.53), elapsed=30.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=2.4, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 170.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-24.16, 1.11, 47.66).
- **ERROR** (day 1): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 105/248.
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-13.47, 1.17, 49.04) — walking out to (-13.47, 1.09, 35.89) (12.00m to mesh).
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- **VEHICLE-STATIONARY** (day 1): RT_Full(Clone) has a driver, hasn't moved in 360s while 41 task(s) it could take wait
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-12-A0 anchor: NO NAVMESH PATH. anchor=(-25.89, 3.11, 49.29) → driveTarget=(-25.89, 1.21, 49.29), from=(-9.66, 1.11, 49.15), elapsed=30.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=16.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 234.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-05-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 40.71) → driveTarget=(-24.56, 1.20, 40.71), from=(-15.36, 1.24, 44.41), elapsed=32.3s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=9.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x00535] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1341 < ] 216.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] Employee_Larry Morgan: stuck at (-21.89, 1.12, 42.67) — walking out to (-33.11, 1.08, 35.17) (12.65m to mesh).
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': 'staging lane 3A line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:103)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```

## Bot actions (first 400)
- d1 signed bulk BULK_Happy Cow Creamery_D1_0
- d1 PO IG1101 Steady Supply Grocers: 6 plt $3,672
- d1 placed parked Inbound Steady Supply Grocers day 1 block 26 door 3
- d1 booked Happy Cow Creamery day 3 block 11 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 7 pallet pick(s) / 2 reach truck(s): stock ready hour 18 + 27.4h of work)
- d2 released 1 order(s): OG4782 (trailer 28.8 h away; 26.8 h work + 2 h)
