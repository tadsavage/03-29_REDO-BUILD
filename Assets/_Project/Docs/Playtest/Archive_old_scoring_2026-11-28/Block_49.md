# Playtest block 49 — days 1–3

## Score: **40.2 (+28.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (4 shipped, 0 late) | 10% | 100.0 (+100.0) |
| Fill rate | 10% | 100.0 (+100.0) |
| Throughput (17 plt vs target 24) | 10% | 70.8 (+70.8) |
| Revenue ($38,262) | not scored | 100.0 (+100.0) |
| Net Profit (-40,777) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 16.3 (-2.7) |
| Reputation (19 to 19) | 10% | 50.0 (+31.7) |

Orders shipped **4**, late/fined **0**, pallets shipped **17**. Cash $127,764 → $152,244 (+24,480). Stock at cost $200,106 → $134,849 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 10 dock bookings, 0 orders released, 7 POs, 0 rush POs.

## Where the money went

Income **$38,262**. Expenses **$13,782** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 37,230 |
| Handling | 1,032 |

| Expense | $ |
|---|---|
| Inventory | 7,744 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Change since last block
- Fixed / no longer seen: **4**
  - VEHICLE-STATIONARY|PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
- New this block: **9**
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #C line-up' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'pallet jack parking' (#m short) - giving up on this stop.
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane #B: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s).
  - ERROR|[TrailerLoad] Could not reach lane #B — leaving OutboundPallet_##bc#ec##d-#_# staged.
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#).
  - WARN|[OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - WARN|[OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#.

## Bugs found (21 distinct, 67 incidents)

| Incidents | Bug |
|---|---|
| 13 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #C line-up' (#m short) - giving up on this stop. |
| 5 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 5 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 4 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'pallet jack parking' (#m short) - giving up on this stop. |
| 1 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane #B: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s). |
| 1 | ERROR — [TrailerLoad] Could not reach lane #B — leaving OutboundPallet_##bc#ec##d-#_# staged. |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#). |
| 1 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-4.62, 1.12, 59.15) — walking out to (-4.62, 1.24, 46.15) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-4.62, 1.12, 59.15) — walking out to (-4.62, 1.24, 46.15) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-20.70, 1.11, 44.11) — walking out to (-20.70, 1.23, 45.50) (12.03m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-25.74, 1.11, 50.19).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-34.95, 1.12, 49.68) — walking out to (-23.70, 1.12, 42.95) (12.11m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 83/248.
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-33.99, 1.12, 49.61) — walking out to (-22.74, 1.12, 42.95) (12.08m to mesh).
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (-10.42, 1.08, 35.50) — walking out to (-9.03, 1.08, 35.50) (12.00m to mesh).
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone): stuck at (-32.05, 1.11, 51.13) — walking out to (-30.66, 1.19, 51.16) (12.83m to mesh).
- **WARN** (day 2): [AiNavigation] Employee_Mark Peterson: stuck at (-4.11, 1.13, 56.59) — walking out to (-4.11, 1.21, 43.59) (12.00m to mesh).
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-01 (aisle entry)' (16.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'pallet jack parking' (10.9m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 2C line-up' (3.7m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane 1B: NO NAVMESH PATH to (-11.31, 1.19, 39.19) from (-15.08, 1.23, 46.01) (elapsed 30.0s).
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
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-19.33, 1.11, 56.33) runs through something solid - taking a clear way out to (-20.72, 1.16, 56.33) instead.
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 15.8m short of 'bay 01-01 (aisle entry)' - re-issuing the drive (3/6).
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order a73ffce7-e0a7-4563-8ce5-522a69ed33da (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at 42/55.

## Bot actions (first 400)
- d1 signed bulk BULK_Rooted Robotics_D1_0
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 PO IG1993 Halloran Household: 4 plt $1,664
- d1 placed parked Inbound Halloran Household day 1 block 28 door 2
- d1 placed parked Inbound Halloran Household day 1 block 30 door 1
- d1 PO IG1994 Halloran Household: 2 plt $800
- d1 placed parked Inbound Halloran Household day 1 block 36 door 1
- d1 PO IG1995 Meridian Provisions: 4 plt $1,664
- d1 placed parked Inbound Meridian Provisions day 2 block 0 door 1
- d2 PO IG2996 Meridian Provisions: 2 plt $768
- d2 placed parked Inbound Meridian Provisions day 2 block 6 door 1
- d2 PO IG2997 Pantry Row Distributors: 4 plt $1,600
- d2 placed parked Inbound Pantry Row Distributors day 2 block 18 door 1
- d2 PO IG2998 Pantry Row Distributors: 2 plt $768
- d2 placed parked Inbound Pantry Row Distributors day 2 block 24 door 1
- d2 PO IG2999 Cornerstone Staples: 1 plt $480
- d2 placed parked Inbound Cornerstone Staples day 2 block 36 door 1
