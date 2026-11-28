# Playtest block 52 — days 1–3

## Score: **44.3 (+9.0)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (4 shipped, 0 late) | 10% | 100.0 (0.0) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (14 plt vs target 24) | 10% | 58.3 (+8.3) |
| Revenue ($38,082) | not scored | 100.0 (0.0) |
| Net Profit (-54,143) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 26.2 (+10.9) |
| Reputation (19 to 21) | 10% | 53.3 (+26.7) |

Orders shipped **4**, late/fined **0**, pallets shipped **14**. Cash $127,764 → $143,232 (+15,468). Stock at cost $200,106 → $130,495 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 10 dock bookings, 1 orders released, 6 POs, 0 rush POs.

## Where the money went

Income **$38,082**. Expenses **$22,614** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 37,230 |
| Handling | 852 |

| Expense | $ |
|---|---|
| Inventory | 16,576 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Change since last block
- Fixed / no longer seen: **15**
  - VEHICLE-STATIONARY|PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - ERROR|"GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|"IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane #B: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s).
  - ERROR|[TrailerLoad] Could not reach lane #B — leaving OutboundPallet_##bc#ec##d-#_# staged.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] PltJack_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] Employee_Shirley Edwards cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
- New this block: **2**
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ### anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[InventoryService] Lane #B lock was held for over #s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over.

## Bugs found (12 distinct, 53 incidents)

| Incidents | Bug |
|---|---|
| 11 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ### anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 2 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [InventoryService] Lane #B lock was held for over #s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.09, 1.21, 52.29) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-15.31, 1.11, 57.94) — walking out to (-15.31, 1.16, 59.00) (12.41m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (5.10, 1.12, 59.19) — walking out to (5.10, 1.23, 46.19) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (3.10, 1.12, 59.25) — walking out to (3.10, 1.17, 46.25) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-40.81, 1.11, 42.34).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-12.10, 1.17, 51.88) — walking out to (-25.10, 1.12, 51.37) (12.01m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-11.32, 1.17, 51.49) — walking out to (-24.32, 1.12, 51.41) (12.00m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 105/248.
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-10.30, 1.17, 49.45) — walking out to (-10.30, 1.09, 36.45) (12.00m to mesh).
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-01-01 anchor: NO NAVMESH PATH. anchor=(-37.81, 1.11, 48.66) → driveTarget=(-37.81, 1.18, 48.66), from=(-41.16, 1.11, 46.66), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=3.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 121.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [InventoryService] Lane 1B lock was held for over 120s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over.

## Bot actions (first 400)
- d1 signed bulk BULK_Mama Tomato Sauces_D1_0
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 PO IG1015 Halloran Household: 6 plt $3,456
- d1 placed parked Inbound Halloran Household day 1 block 28 door 2
- d1 placed parked Inbound Halloran Household day 1 block 30 door 1
- d1 PO IG1016 Halloran Household: 1 plt $576
- d1 placed parked Inbound Halloran Household day 1 block 36 door 1
- d1 PO IG1017 Halloran Household: 6 plt $3,456
- d1 placed parked Inbound Halloran Household day 2 block 0 door 1
- d2 PO IG2018 Meridian Provisions: 1 plt $576
- d2 placed parked Inbound Meridian Provisions day 2 block 6 door 1
- d2 PO IG2019 Meridian Provisions: 6 plt $3,648
- d2 placed parked Inbound Meridian Provisions day 2 block 18 door 1
- d2 PO IG2020 Fairweather Trading Co.: 8 plt $4,864
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 36 door 2
- d2 booked Mama Tomato Sauces day 3 block 10 door 2 (pallet-only, 9 pallet pick(s) / 2 reach truck(s): stock ready hour 48 + 3.5h of work)
- d2 released 1 order(s): OG3734 (trailer 8.8 h away; 7.9 h work + 2 h)
