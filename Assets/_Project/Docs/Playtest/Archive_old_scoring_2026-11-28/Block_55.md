# Playtest block 55 — days 1–3

## Score: **40.5 (-2.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (3 shipped, 1 late) | 10% | 66.7 (-33.3) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (11 plt vs target 24) | 10% | 45.8 (-4.2) |
| Revenue ($33,375) | not scored | 100.0 (0.0) |
| Net Profit (-57,930) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 28.1 (+3.7) |
| Reputation (19 to 20) | 10% | 51.7 (-10.0) |

Orders shipped **3**, late/fined **1**, pallets shipped **11**. Cash $127,764 → $136,549 (+8,785). Stock at cost $200,106 → $133,391 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 14 dock bookings, 0 orders released, 11 POs, 0 rush POs.

## Where the money went

Income **$33,375**. Expenses **$24,590** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 32,583 |
| Handling | 792 |

| Expense | $ |
|---|---|
| Inventory | 17,496 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Fines | 1,056 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x still loading

- day 2: OG2624 (Sneaky Pete's Seafood) was still loading when it came due. Fined $1,056. Lines 27, 248 cs, released=True.

## Change since last block
- Fixed / no longer seen: **10**
  - WARN|[OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - OVERLAP| A Chep & A Chep @(#, #)
  - VEHICLE-STATIONARY|PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - WARN|[AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] Employee_Kelly Harmon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##bce#f#-ac#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[AiNavigation] Employee_Mary Young: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
- New this block: **4**
  - WARN|[AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ### anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.

## Bugs found (11 distinct, 35 incidents)

| Incidents | Bug |
|---|---|
| 10 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 3 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 3 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ### anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (12.88, 1.16, 36.88) — walking out to (-0.12, 1.11, 36.89) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.20, 1.22, 51.71) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-37.10, 1.11, 42.34).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-25.58, 1.11, 43.21) — walking out to (-37.10, 1.11, 42.34) (11.55m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-10.10, 1.12, 59.09) — walking out to (-10.10, 1.20, 46.09) (12.00m to mesh).
- **WARN** (day 2): [AiNavigation] Employee_Shirley Edwards: stuck at (-16.25, 1.14, 48.20) — walking out to (-16.25, 0.04, 34.46) (12.74m to mesh).
- **REPEATING** (day 2): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-09-01 anchor: NO NAVMESH PATH. anchor=(-31.19, 1.11, 40.71) → driveTarget=(-31.19, 1.18, 40.71), from=(-33.22, 1.17, 40.54), elapsed=32.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=2.0, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 189.4s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-41.35, 1.11, 45.16) runs through something solid - taking a clear way out to (-42.74, 1.16, 44.59) instead.
- **WARN** (day 2): [AiNavigation] Employee_Mark Peterson: stuck at (-38.87, 1.12, 47.57) — walking out to (-38.87, 0.04, 34.46) (12.11m to mesh).
- **VEHICLE-STATIONARY** (day 2): RT_Full(Clone) has a driver, hasn't moved in 360s while 1 task(s) it could take wait

## Bot actions (first 400)
- d1 signed bulk BULK_Honest Harvest Rice_D1_0
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 PO IG1034 Halloran Household: 6 plt $3,456
- d1 placed parked Inbound Halloran Household day 1 block 28 door 2
- d1 placed parked Inbound Halloran Household day 1 block 30 door 1
- d1 PO IG1035 Halloran Household: 1 plt $576
- d1 placed parked Inbound Halloran Household day 1 block 36 door 1
- d1 PO IG1036 Halloran Household: 6 plt $3,456
- d1 placed parked Inbound Halloran Household day 2 block 0 door 1
- d2 PO IG2037 Meridian Provisions: 1 plt $576
- d2 placed parked Inbound Meridian Provisions day 2 block 6 door 1
- d2 PO IG2038 Meridian Provisions: 6 plt $3,648
- d2 placed parked Inbound Meridian Provisions day 2 block 18 door 1
- d2 PO IG2039 Cornerstone Staples: 1 plt $120
- d2 placed parked Inbound Cornerstone Staples day 2 block 24 door 1
- d2 PO IG2040 Cornerstone Staples: 2 plt $240
- d2 placed parked Inbound Cornerstone Staples day 2 block 26 door 1
- d2 PO IG2041 Fairweather Trading Co.: 8 plt $4,864
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 36 door 2
- d2 PO IG2042 Iron Gate Staples: 1 plt $200
- d2 placed parked Inbound Iron Gate Staples day 2 block 37 door 3
- d2 PO IG2043 Iron Gate Staples: 1 plt $120
- d2 placed parked Inbound Iron Gate Staples day 2 block 42 door 1
- d2 PO IG2044 Iron Gate Staples: 2 plt $240
- d2 placed parked Inbound Iron Gate Staples day 2 block 44 door 1
