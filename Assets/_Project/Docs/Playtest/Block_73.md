# Playtest block 73 — days 1–3

## Score: **46.1 (+10.5)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (1 shipped, 1 late) | 10% | 50.0 (-16.7) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (7 plt vs target 24) | 10% | 29.2 (-8.3) |
| Revenue ($21,909) | not scored | 100.0 (0.0) |
| Net Profit (-935) | 10% | 34.4 (+34.4) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 29.6 (+19.1) |
| Reputation (19 to 60) | 10% | 100.0 (0.0) |

Orders shipped **1**, late/fined **1**, pallets shipped **7**. Cash $127,764 → $137,347 (+9,583). Stock at cost $200,106 → $189,588 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 2 dock bookings, 1 orders released, 1 POs, 0 rush POs.

## Where the money went

Income **$21,909**. Expenses **$12,326** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 21,117 |
| Handling | 792 |

| Expense | $ |
|---|---|
| Inventory | 4,608 |
| Wages | 2,835 |
| Fines | 1,680 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x still being picked

- day 2: OG4824 (Sneaky Pete's Seafood) was still being picked (288/336 cs). Fined $1,680. Lines 1, 336 cs, released=True.

## Change since last block
- Fixed / no longer seen: **32**
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop.
  - ERROR|"GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|"IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: own/Staged/y#, own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: [#: own/Staged/y#] lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#, OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#] 
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry) (route)' (#m short) - giving up on this stop.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__#.MoveNext () [#x#e] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (loop in)' ((#, #, #)) after #s.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (aisle entry)' ((#, #, #)) after #s.
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' blockers: back to source lane: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=#, reissues=#, lastSeekCancelledBy=[at AiNavigation.Update () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago]
  - WARN|[AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #).
- New this block: **2**
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - WARN|[OrderSelectionTaskDriver] Order ##ba##c#f-a#fb-# (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#.

## Bugs found (8 distinct, 41 incidents)

| Incidents | Bug |
|---|---|
| 16 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 16 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 3 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##ba##c#f-a#fb-# (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.16, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-1.93, 1.12, 59.25) — walking out to (4.57, 1.18, 47.99) (12.00m to mesh).
- **VEHICLE-STATIONARY** (day 1): RT_Full(Clone) has a driver, hasn't moved in 360s while 34 task(s) it could take wait
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 22 task(s) it could take wait
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-23.44, 1.12, 43.43) — walking out to (-12.18, 1.16, 49.93) (12.00m to mesh).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 2371db5b-4ba1-4c6f-a5fb-dbdcbd441229 (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at 43/59.
- **WARN** (day 2): [AiNavigation] Employee_Douglas Garcia: stuck at (-9.10, 1.12, 59.25) — walking out to (-2.60, 1.18, 47.99) (12.00m to mesh).
- **WARN** (day 2): [AiNavigation] Employee_Shirley Edwards: stuck at (-28.89, 1.12, 41.80) — walking out to (-40.14, 1.08, 35.17) (12.17m to mesh).

## Bot actions (first 400)
- d1 signed bulk BULK_Sneaky Pete's Seafood_D1_0
- d1 PO IG1140 Bulk Basin Foods: 8 plt $4,608
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 3
- d1 booked Sneaky Pete's Seafood day 2 block 34 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 10 pallet pick(s) / 2 reach truck(s): stock ready hour 19 + 22.0h of work)
- d1 released 1 order(s): OG4824 (trailer 20.8 h away; 18.9 h work + 2 h)
