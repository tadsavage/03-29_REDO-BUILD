# Playtest block 61 — days 1–3

## Score: **40.1 (+25.7)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (3 shipped, 0 late) | 10% | 100.0 (+100.0) |
| Fill rate | 10% | 100.0 (+100.0) |
| Throughput (11 plt vs target 24) | 10% | 45.8 (+45.8) |
| Revenue ($32,049) | not scored | 100.0 (+100.0) |
| Net Profit (-51,944) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (+8.6) |
| Reliability (bugs) | 50% | 20.3 (-4.8) |
| Reputation (19 to 21) | 10% | 53.3 (+35.0) |

Orders shipped **3**, late/fined **0**, pallets shipped **11**. Cash $127,764 → $139,375 (+11,611). Stock at cost $200,106 → $136,551 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 8 dock bookings, 1 orders released, 4 POs, 0 rush POs.

## Where the money went

Income **$32,049**. Expenses **$20,438** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 31,257 |
| Handling | 792 |

| Expense | $ |
|---|---|
| Inventory | 14,400 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Change since last block
- Fixed / no longer seen: **4**
  - ERROR|[TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #E: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - TRUCK-OVERLAP| Truck→PO_IG# & Truck→PO_IG# at (#, #, #)
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ec##b#d## but the employee is #m away (max #m) — refusing to start receiving.
- New this block: **11**
  - WARN|[AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Justin Waller: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #).
  - WARN|[AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - ERROR|[GridNav] 'Employee_Shirley Edwards': 'bay ## (aisle entry)' was interrupted # times - giving the leg up.
  - WARN|[AiNavigation] Employee_Justin Waller cannot reach equipment at (#, #, #) (path invalid) — seeking next available
  - WARN|[OrderSelectionTaskDriver] Order #-d#cd#e#e#f#-# (Fizz-Pop Soda Works) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] Employee_Mark Peterson: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.

## Bugs found (19 distinct, 52 incidents)

| Incidents | Bug |
|---|---|
| 7 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Justin Waller: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 4 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 3 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 3 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | TRUCK-STALLED —  Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #) |
| 1 | ERROR — [GridNav] 'Employee_Shirley Edwards': 'bay ## (aisle entry)' was interrupted # times - giving the leg up. |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Justin Waller cannot reach equipment at (#, #, #) (path invalid) — seeking next available |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-d#cd#e#e#f#-# (Fizz-Pop Soda Works) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Employee_Mark Peterson: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.09, 1.21, 52.29) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-7.34, 1.11, 53.61) — walking out to (-7.34, 1.23, 40.61) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (12.14, 1.17, 36.18) — walking out to (-0.86, 1.09, 35.86) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Justin Waller cannot reach equipment at (-2.04, 1.11, 48.31) (path invalid) — seeking next available
- **WARN** (day 1): [AiNavigation] Employee_Justin Waller: stuck at (-5.40, 1.12, 59.11) — walking out to (-5.40, 1.24, 46.11) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-5.40, 1.12, 58.55) — walking out to (-5.40, 1.23, 45.55) (12.00m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 108/248.
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (2.52, 1.13, 56.59) — walking out to (-3.98, 1.23, 45.33) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-23.14, 1.11, 47.66).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-12.36, 1.15, 51.64) — walking out to (-25.36, 1.12, 51.39) (12.00m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a (Fizz-Pop Soda Works) has no reachable pick location for its remaining line item(s) — staging short at 32/44.
- **WARN** (day 2): [AiNavigation] Employee_Shirley Edwards: stuck at (-4.39, 1.19, 49.94) — walking out to (-4.39, 1.11, 36.94) (12.00m to mesh).
- **REPEATING** (day 2): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **VEHICLE-STATIONARY** (day 2): PltJack_FULL(Clone) has a driver, hasn't moved in 450s while 17 task(s) it could take wait
- **WARN** (day 2): [AiNavigation] Employee_Mark Peterson: task target (-4.69, 1.14, 41.69) only partially reachable — stopped 0.83m short. Treating as arrived.
- **TRUCK-STALLED** (day 2): TRUCK-STALLED Truck→Outbound_Door1 state=DepartToApproach 90s at (-11.3, 0.0, 27.9)
- **ERROR** (day 2): [GridNav] 'Employee_Shirley Edwards': 'bay 01-01 (aisle entry)' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:103)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- **VEHICLE-STATIONARY** (day 3): RT_Full(Clone) has a driver, hasn't moved in 330s while 8 task(s) it could take wait

## Bot actions (first 400)
- d1 signed bulk BULK_Blocky Bakeshop_D1_0
- d1 PO IG1088 Bulk Basin Foods: 6 plt $3,456
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 3
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 2
- d1 PO IG1089 Cornerstone Staples: 6 plt $3,456
- d1 placed parked Inbound Cornerstone Staples day 1 block 44 door 1
- d1 booked Blocky Bakeshop day 3 block 10 door 2 (pallet-only, 9 pallet pick(s) / 2 reach truck(s): stock ready hour 27 + 18.5h of work)
- d2 PO IG2090 Iron Gate Staples: 6 plt $3,648
- d2 placed parked Inbound Iron Gate Staples day 2 block 14 door 1
- d2 PO IG2091 Bulk Basin Foods: 6 plt $3,840
- d2 placed parked Inbound Bulk Basin Foods day 2 block 32 door 2
- d2 released 1 order(s): OG4765 (trailer 12.8 h away; 10.9 h work + 2 h)
