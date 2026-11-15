# Playtest block 57 — days 1–3

## Score: **39.3 (+3.7)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (3 shipped, 0 late) | 10% | 100.0 (+25.0) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (12 plt vs target 24) | 10% | 50.0 (0.0) |
| Revenue ($32,109) | not scored | 100.0 (0.0) |
| Net Profit (-89,824) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 19.2 (-1.2) |
| Reputation (19 to 17) | 10% | 46.7 (+18.3) |

Orders shipped **3**, late/fined **0**, pallets shipped **12**. Cash $127,764 → $107,491 (-20,273). Stock at cost $200,106 → $130,555 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 12 dock bookings, 0 orders released, 3 POs, 0 rush POs.

## Where the money went

Income **$32,109**. Expenses **$52,382** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 31,257 |
| Handling | 852 |

| Expense | $ |
|---|---|
| Inventory | 46,344 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Change since last block
- Fixed / no longer seen: **5**
  - ERROR|"IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  - OVERLAP| A Chep & A Chep @(#, #)
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[AiNavigation] Employee_Charles Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[InventoryService] Lane #B lock was held for over #s by reach truck RT_Full(Clone) (Amanda Dixon, Putaway) - treating it as leaked and taking it over.
- New this block: **9**
  - VEHICLE-STATIONARY|PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - WARN|[AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order #-be##-af### (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[OrderSelectionTaskDriver] Order ###bc#c#d-# (Cluck & Cover Poultry) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ###d-a### but the employee is #m away (max #m) — refusing to start receiving.

## Bugs found (20 distinct, 59 incidents)

| Incidents | Bug |
|---|---|
| 4 | ERROR — [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #B: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 11 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 4 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 4 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 2 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | WARN — [AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-be##-af### (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###bc#c#d-# (Cluck & Cover Poultry) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###d-a### but the employee is #m away (max #m) — refusing to start receiving. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.20, 1.22, 51.71) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (12.14, 1.20, 36.17) — walking out to (12.14, 1.14, 49.17) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-37.02, 1.11, 42.34).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-25.50, 1.11, 43.21) — walking out to (-37.02, 1.11, 42.34) (11.55m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (2.24, 1.12, 58.50) — walking out to (8.74, 1.21, 47.24) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-42.07, 1.12, 49.79) — walking out to (-30.82, 1.12, 42.94) (12.17m to mesh).
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone) cannot reach task position at (-15.33, 1.16, 38.24) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 116/248.
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-16.57, 1.15, 42.34) — walking out to (-27.80, 1.08, 35.17) (12.46m to mesh).
- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone): stuck at (-7.47, 1.11, 57.59) — walking out to (-7.47, 1.16, 59.00) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-6.85, 1.13, 57.79) — walking out to (-0.35, 1.24, 46.53) (12.00m to mesh).
- **VEHICLE-STATIONARY** (day 2): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 16 task(s) it could take wait
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 368110a7-be45-4027-af00-3c57e3568069 (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at 42/53.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order a73ffce7-e0a7-4563-8ce5-522a69ed33da (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at 42/55.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-0.59, 1.11, 59.00) runs through something solid - taking a clear way out to (0.80, 1.16, 59.00) instead.
- **WARN** (day 2): [AiNavigation] RT_Full(Clone) cannot reach task position at (-21.91, 1.21, 40.71) (path invalid) — abandoning the walk and handing the task back.
- **ERROR** (day 2): [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → 1B: NO NAVMESH PATH. anchor=(-11.31, 1.11, 39.69) → driveTarget=(-11.31, 1.21, 39.69), from=(-10.62, 1.13, 37.19), elapsed=30.4s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerOffloadController/<SeekViaNavMesh>d__60:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:1224)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 4abe618e-1860-46bc-8c6d-c29fafe16629 (Cluck & Cover Poultry) has no reachable pick location for its remaining line item(s) — staging short at 425/812.
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 9723d155-2000-471d-a023-6da5ef1071c7 but the employee is 18.6m away (max 4.5m) — refusing to start receiving.

## Bot actions (first 400)
- d1 signed bulk BULK_The Big Cheese Inc._D1_0
- d1 PO IG1058 Blue Ridge Provisions: 4 plt $4,320
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 26 door 3
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 PO IG1059 Bulk Basin Foods: 8 plt $5,880
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 2
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 26 door 3
- d1 placed parked Inbound Bulk Basin Foods day 1 block 28 door 2
- d1 placed parked Inbound Blue Ridge Provisions day 2 block 0 door 1
- d1 booked Cluck & Cover Poultry day 3 block 11 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 16 pallet pick(s) / 2 reach truck(s): stock ready hour 32 + 19.0h of work)
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 13 door 1
- d2 PO IG2063 Halloran Household: 4 plt $2,208
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 31 door 2
- d2 placed parked Inbound Meridian Provisions day 2 block 32 door 3
- d2 placed parked Inbound Meridian Provisions day 2 block 35 door 3
