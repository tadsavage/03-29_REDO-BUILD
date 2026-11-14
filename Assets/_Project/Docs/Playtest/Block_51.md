# Playtest block 51 — days 1–3

## Score: **35.3 (-0.5)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 0 late) | 10% | 100.0 (+20.0) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (12 plt vs target 24) | 10% | 50.0 (0.0) |
| Revenue ($27,582) | not scored | 100.0 (0.0) |
| Net Profit (-92,953) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 15.3 (+2.1) |
| Reputation (19 to 5) | 10% | 26.7 (-35.0) |

Orders shipped **2**, late/fined **0**, pallets shipped **12**. Cash $127,764 → $81,772 (-45,992). Stock at cost $200,106 → $153,145 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 15 dock bookings, 0 orders released, 11 POs, 0 rush POs.

## Where the money went

Income **$27,582**. Expenses **$73,574** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 26,610 |
| Handling | 972 |

| Expense | $ |
|---|---|
| Inventory | 67,536 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Change since last block
- Fixed / no longer seen: **8**
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (loop in) (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'staging lane #B wait (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'staging lane #B wait' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - WARN|[OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#.
- New this block: **13**
  - ERROR|"GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|"IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane #B: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s).
  - ERROR|[TrailerLoad] Could not reach lane #B — leaving OutboundPallet_##bc#ec##d-#_# staged.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] PltJack_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] Employee_Shirley Edwards cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.

## Bugs found (25 distinct, 69 incidents)

| Incidents | Bug |
|---|---|
| 11 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 7 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 7 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 5 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — "GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 3 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane #B: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s). |
| 1 | ERROR — [TrailerLoad] Could not reach lane #B — leaving OutboundPallet_##bc#ec##d-#_# staged. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Shirley Edwards cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-37.90, 1.11, 42.34).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-26.38, 1.11, 43.21) — walking out to (-37.90, 1.11, 42.34) (11.55m to mesh).
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (-6.01, 1.11, 48.70) — walking out to (-5.43, 1.23, 50.08) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-1.30, 1.12, 59.15) — walking out to (-1.30, 1.24, 46.15) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-44.53, 1.12, 50.35) — walking out to (-33.30, 1.12, 42.91) (12.47m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 91/248.
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (5.96, 1.12, 58.91) — walking out to (5.96, 1.22, 45.91) (12.00m to mesh).
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-23.08, 1.12, 41.81) — walking out to (-9.49, 1.25, 41.81) (12.60m to mesh).
- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone): stuck at (2.97, 1.22, 50.61) — walking out to (3.54, 1.21, 52.00) (12.00m to mesh).
- **ERROR** (day 1): "GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_remainingDistance ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__211:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2049)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__211:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2049)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone) cannot reach task position at (-29.86, 1.16, 56.26) (path invalid) — abandoning the walk and handing the task back.
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-32.92, 1.12, 49.19) — walking out to (-39.42, 1.10, 38.59) (12.00m to mesh).
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-3.87, 1.11, 56.41) runs through something solid - taking a clear way out to (-3.30, 1.17, 57.79) instead.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): stuck at (-12.05, 1.11, 51.31) — walking out to (-11.47, 1.21, 52.70) (12.00m to mesh).
- **ERROR** (day 2): [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane 1B: NO NAVMESH PATH to (-11.31, 1.19, 39.19) from (-9.64, 1.11, 39.13) (elapsed 30.0s).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__103:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1222)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] Could not reach lane 1B — leaving OutboundPallet_a99ac8a2-18bc-4ec5-861d-a92b572e82ae_1 staged.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<LoadOnePallet>d__96:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:832)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (-17.59, 1.23, 42.34) (path invalid) — abandoning the walk and handing the task back.
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-03 (loop in)' (9.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] Employee_Shirley Edwards cannot reach task position at (5.91, 1.14, 41.69) (path invalid) — abandoning the walk and handing the task back.
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-03 (aisle entry)' (3.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 02-03' - driving straight to the stop instead.
- **VEHICLE-STATIONARY** (day 3): RT_Full(Clone) has a driver, hasn't moved in 329s while 7 task(s) it could take wait

## Bot actions (first 400)
- d1 signed bulk BULK_Chubby Chipmunk Cookies_D1_0
- d1 PO IG1004 Coastal Cargo Exchange: 6 plt $10,560
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 26 door 3
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 PO IG1005 Anchor Point Foods: 7 plt $5,040
- d1 placed parked Inbound Anchor Point Foods day 1 block 26 door 2
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 26 door 3
- d1 placed parked Inbound Anchor Point Foods day 1 block 28 door 2
- d1 PO IG1006 Riverside Housewares: 1 plt $720
- d1 placed parked Inbound Riverside Housewares day 1 block 38 door 1
- d1 PO IG1007 Coastal Cargo Exchange: 6 plt $10,560
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 44 door 1
- d1 PO IG1008 Riverside Housewares: 7 plt $5,040
- d1 placed parked Inbound Riverside Housewares day 2 block 0 door 1
- d2 PO IG2009 Anchor Point Foods: 1 plt $816
- d2 placed parked Inbound Anchor Point Foods day 2 block 8 door 1
- d2 PO IG2010 Dockside Closeouts: 6 plt $11,040
- d2 placed parked Inbound Dockside Closeouts day 2 block 14 door 1
- d2 PO IG2011 Anchor Point Foods: 7 plt $5,712
- d2 placed parked Inbound Anchor Point Foods day 2 block 16 door 2
- d2 PO IG2012 Last Call Liquidators: 1 plt $816
- d2 placed parked Inbound Last Call Liquidators day 2 block 26 door 1
- d2 PO IG2013 Coastal Cargo Exchange: 6 plt $11,520
- d2 placed parked Inbound Coastal Cargo Exchange day 2 block 32 door 1
- d2 PO IG2014 Last Call Liquidators: 7 plt $5,712
- d2 placed parked Inbound Last Call Liquidators day 2 block 34 door 3
