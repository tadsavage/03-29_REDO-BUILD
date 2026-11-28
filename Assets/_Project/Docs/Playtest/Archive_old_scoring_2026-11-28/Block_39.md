# Playtest block 39 — days 1–4

## Score: **28.8 (-5.2)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 14 late) | 10% | 12.5 (-87.5) |
| Fill rate | 10% | 100.0 (+40.4) |
| Throughput (31 plt vs target 36) | 10% | 86.1 (+70.6) |
| Revenue ($30,989) | not scored | 100.0 (+77.9) |
| Net Profit (-10,801) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 14.3 (-1.4) |
| Reputation (19 to 0) | 10% | 18.3 (-68.3) |

Orders shipped **2**, late/fined **14**, pallets shipped **31**. Cash $127,764 → $124,403 (-3,361). Stock at cost $200,106 → $192,666 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 7 dock bookings, 1 orders released, 2 POs, 0 rush POs.

## Where the money went

Income **$30,989**. Expenses **$34,350** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 30,989 |

| Expense | $ |
|---|---|
| Inventory | 16,292 |
| Fines | 8,660 |
| Wages | 4,347 |
| Maintenance | 2,592 |
| Groundskeeping | 1,251 |
| Lease / Mortgage | 1,176 |
| Loss Prevention | 32 |

## Late orders - state when it came due
- 14 x never released

- day 2: OG2624 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,056. Lines 27, 248 cs, released=False.
- day 3: OG3672 (Fizz-Pop Soda Works) was never released to the floor. Fined $387. Lines 9, 44 cs, released=False.
- day 3: OG3673 (Pony Express Oats) was never released to the floor. Fined $498. Lines 10, 59 cs, released=False.
- day 3: OG3674 (Mama Tomato Sauces) was never released to the floor. Fined $458. Lines 10, 57 cs, released=False.
- day 3: OG3675 (Blocky Bakeshop) was never released to the floor. Fined $307. Lines 10, 44 cs, released=False.
- day 3: OG3676 (The Big Cheese Inc.) was never released to the floor. Fined $648. Lines 9, 53 cs, released=False.
- day 3: OG3677 (Chubby Chipmunk Cookies) was never released to the floor. Fined $360. Lines 10, 48 cs, released=False.
- day 3: OG3678 (Neon Nectar Juice Co.) was never released to the floor. Fined $414. Lines 10, 45 cs, released=False.
- day 3: OG3679 (The Daily Grind Tofu) was never released to the floor. Fined $620. Lines 9, 55 cs, released=False.
- day 3: OG3680 (Crunch-Time Cereals) was never released to the floor. Fined $763. Lines 10, 55 cs, released=False.
- day 3: OG3681 (Chubby Chipmunk Cookies) was never released to the floor. Fined $467. Lines 9, 51 cs, released=False.
- day 3: OG3682 (Salty Sailor Pretzel Co.) was never released to the floor. Fined $356. Lines 9, 47 cs, released=False.
- day 3: OG3683 (Bramble & Bite Berry Co.) was never released to the floor. Fined $583. Lines 10, 47 cs, released=False.
- day 3: OG3625 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,743. Lines 19, 420 cs, released=False.

## Change since last block
- Fixed / no longer seen: **24**
  - WARN|[AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Judith Martin: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_James Williams: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Jonathan Allen: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Victoria Lewis: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'bay ##' ((#, #, #)) after #s.
  - WARN|[AiNavigation] Employee_Janet Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: no progress toward task target (#, #, #) for #s (stopped #m short, path PathComplete) — handing the task back.
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: no progress toward task target (#, #, #) for #s (stopped #m short, path PathComplete) — handing the task back.
- New this block: **20**
  - WARN|[AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|"IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  - WARN|[AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #D: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - WARN|[AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #).
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - REPEATING|x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
  - REPEATING|x# and counting: [AiNavigation] DS_FULL(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.

## Bugs found (20 distinct, 100 incidents)

| Incidents | Bug |
|---|---|
| 19 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 12 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 12 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 10 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #D: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 2 | TRUCK-STALLED —  Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #) |
| 4 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | REPEATING — x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building. |
| 1 | REPEATING — x# and counting: [AiNavigation] DS_FULL(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | REPEATING — x# and counting: [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##dd##b#-a#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone) cannot reach task position at (-47.09, 1.19, 39.04) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-14.74, 1.16, 43.43) — walking out to (-1.74, 1.21, 43.43) (12.00m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet a4074603-9dd4-49b1-a861-f926a6747989 but the employee is 6.6m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (5.91, 1.11, 39.69) — walking out to (7.30, 1.23, 40.26) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-16.43, 1.12, 58.92) — walking out to (-9.93, 1.21, 47.66) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (5.04, 1.12, 59.25) — walking out to (11.54, 1.16, 47.99) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-16.46, 1.12, 58.81) — walking out to (-9.96, 1.22, 47.55) (12.00m to mesh).
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-06-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 41.34) → driveTarget=(-24.56, 1.21, 41.34), from=(-12.89, 1.11, 38.89), elapsed=30.2s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=11.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 171.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-7.99, 1.12, 59.17) — walking out to (-7.99, 1.20, 46.17) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone): stuck at (-6.27, 1.11, 54.80) — walking out to (-5.69, 1.18, 56.19) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-34.10, 1.11, 42.34).
- **ERROR** (day 1): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-16-A1 anchor: NO NAVMESH PATH. anchor=(-19.26, 3.11, 49.29) → driveTarget=(-19.26, 1.21, 49.29), from=(-11.94, 1.23, 49.19), elapsed=31.7s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=7.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 188.7s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 2): x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] DS_FULL(Clone): the straight way out of (-19.33, 1.11, 56.33) runs through something solid - taking a clear way out to (-20.72, 1.16, 56.33) instead.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-10.68, 1.11, 55.44) runs through something solid - taking a clear way out to (-11.25, 1.20, 54.06) instead.
- **ERROR** (day 3): [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2D: NO NAVMESH PATH. anchor=(-0.71, 1.14, 48.99) → driveTarget=(-0.71, 1.24, 48.99), from=(-1.93, 1.11, 48.74), elapsed=31.7s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=1.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 187.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 3): x25 and counting: [AiNavigation] DS_FULL(Clone): stuck at (-5.23, 1.11, 57.92) — walking out to (-5.23, 1.16, 59.00) (12.00m to mesh).
- **TRUCK-STALLED** (day 4): TRUCK-STALLED Truck→Outbound_Door2 state=DepartToApproach 420s at (-2.0, 0.0, 27.9)

## Bot actions (first 400)
- d1 signed bulk BULK_Pony Express Oats_D1_0
- d1 PO IG1938 Steady Supply Grocers: 6 plt $3,672
- d1 placed parked Inbound Steady Supply Grocers day 1 block 26 door 3
- d1 booked Pony Express Oats day 2 block 0 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 10 pallet pick(s) / 2 reach truck(s): stock ready hour 18 + 4.0h of work)
- d1 released 1 order(s)
- d2 placed parked Inbound Steady Supply Grocers day 2 block 33 door 2
- d2 PO IG2940 Last Call Liquidators: 6 plt $7,560
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 36 door 2
- d2 booked Sneaky Pete's Seafood day 3 block 24 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 18 pallet pick(s) / 2 reach truck(s): stock ready hour 46 + 7.1h of work)
- d3 placed parked Inbound Last Call Liquidators day 3 block 10 door 1
- d3 placed parked Inbound Steady Supply Grocers day 3 block 35 door 3
