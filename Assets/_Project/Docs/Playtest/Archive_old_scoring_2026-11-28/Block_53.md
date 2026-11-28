# Playtest block 53 — days 1–3

## Score: **32.6 (-11.7)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (3 shipped, 1 late) | 10% | 66.7 (-33.3) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (12 plt vs target 24) | 10% | 50.0 (-8.3) |
| Revenue ($28,176) | not scored | 100.0 (0.0) |
| Net Profit (-48,119) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 16.1 (-10.1) |
| Reputation (19 to 6) | 10% | 28.3 (-25.0) |

Orders shipped **3**, late/fined **1**, pallets shipped **12**. Cash $127,764 → $143,122 (+15,358). Stock at cost $200,106 → $136,629 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 14 dock bookings, 0 orders released, 7 POs, 0 rush POs.

## Where the money went

Income **$28,176**. Expenses **$12,818** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 27,264 |
| Handling | 912 |

| Expense | $ |
|---|---|
| Inventory | 6,480 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Fines | 300 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x staged but no trailer loaded it

- day 2: OG1738 (Chubby Chipmunk Cookies) was staged but no trailer loaded it (door 2). Fined $300. Lines 1, 60 cs, released=True.

## Change since last block
- Fixed / no longer seen: **3**
  - WARN|[AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ### anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[InventoryService] Lane #B lock was held for over #s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over.
- New this block: **9**
  - ERROR|[TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #B: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - VEHICLE-STATIONARY|PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - ERROR|[GridNav] 'Employee_Shirley Edwards': 'staging lane #A line-up' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Mark Peterson': 'staging lane #A line-up' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'staging lane #A line-up' was interrupted # times - giving the leg up.
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - WARN|[OrderSelectionTaskDriver] Order #-be##-af### (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#.

## Bugs found (18 distinct, 62 incidents)

| Incidents | Bug |
|---|---|
| 6 | ERROR — [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #B: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 12 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 7 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 6 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | ERROR — [GridNav] 'Employee_Shirley Edwards': 'staging lane #A line-up' was interrupted # times - giving the leg up. |
| 4 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': 'staging lane #A line-up' was interrupted # times - giving the leg up. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': 'staging lane #A line-up' was interrupted # times - giving the leg up. |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 2 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-be##-af### (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (2.50, 1.12, 58.23) — walking out to (9.00, 1.21, 46.97) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-2.50, 1.12, 58.74) — walking out to (-2.50, 1.23, 45.74) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-11.31, 1.14, 54.22) — walking out to (-4.81, 1.21, 42.96) (12.00m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 105/248.
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **WARN** (day 2): [AiNavigation] Employee_Larry Morgan: stuck at (-10.74, 1.15, 52.69) — walking out to (-23.74, 1.13, 53.20) (12.01m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-12.17, 1.15, 52.68) — walking out to (-25.17, 1.12, 53.20) (12.01m to mesh).
- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone): stuck at (-8.18, 1.11, 57.15) — walking out to (-7.61, 1.17, 58.53) (12.03m to mesh).
- **REPEATING** (day 1): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-12.39, 1.11, 46.11) runs through something solid - taking a clear way out to (-12.39, 1.25, 44.73) instead.
- **ERROR** (day 2): [GridNav] 'Employee_Shirley Edwards': 'staging lane 2A line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Mark Peterson': 'staging lane 3A line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': 'staging lane 2A line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-16.26, 1.11, 47.66).
- **ERROR** (day 2): [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → 1B: NO NAVMESH PATH. anchor=(-11.31, 1.11, 39.69) → driveTarget=(-11.31, 1.21, 39.69), from=(3.67, 1.11, 40.51), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerOffloadController/<SeekViaNavMesh>d__60:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:1224)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 368110a7-be45-4027-af00-3c57e3568069 (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at 42/53.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order b72defae-0fda-441b-9c33-e78561011633 (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at 46/48.

## Bot actions (first 400)
- d1 signed bulk BULK_Sprout Scout Foods_D1_0
- d1 PO IG1021 Backdoor Bill's Overstock: 1 plt $1,080
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 25 door 1
- d1 placed parked Inbound Halloran Household day 1 block 26 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 2
- d1 PO IG1022 Ambrose Fine Foods: 7 plt $23,100
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 3
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 27 door 1
- d1 PO IG1023 Halloran Household: 1 plt $288
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 28 door 1
- d1 placed parked Inbound Halloran Household day 1 block 28 door 3
- d1 placed parked Inbound Halloran Household day 1 block 30 door 2
- d1 PO IG1024 Halloran Household: 1 plt $288
- d1 placed parked Inbound Halloran Household day 1 block 36 door 1
- d1 PO IG1025 Ambrose Fine Foods: 8 plt $24,960
- d1 placed parked Inbound Ambrose Fine Foods day 2 block 0 door 1
- d1 PO IG1026 Meridian Provisions: 1 plt $288
- d1 placed parked Inbound Meridian Provisions day 1 block 47 door 1
- d2 PO IG2027 Meridian Provisions: 1 plt $288
- d2 placed parked Inbound Meridian Provisions day 2 block 6 door 1
- d2 placed parked Inbound Millbrook Family Foods day 2 block 33 door 2
- d2 booked The Big Cheese Inc. day 3 block 26 door 2 (pallet-only, 12 pallet pick(s) / 2 reach truck(s): stock ready hour 47 + 13.8h of work)
