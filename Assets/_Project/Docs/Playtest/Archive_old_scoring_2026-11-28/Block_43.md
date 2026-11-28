# Playtest block 43 — days 1–3

## Score: **39.0 (-3.1)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (4 shipped, 1 late) | 10% | 80.0 (-20.0) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (13 plt vs target 24) | 10% | 54.2 (-8.3) |
| Revenue ($38,022) | not scored | 100.0 (0.0) |
| Net Profit (-45,625) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 18.9 (+1.4) |
| Reputation (19 to 26) | 10% | 61.7 (-10.0) |

Orders shipped **4**, late/fined **1**, pallets shipped **13**. Cash $127,764 → $146,038 (+18,274). Stock at cost $200,106 → $136,207 (offsets stock bought in Net Profit).
Bot did: 2 contracts signed, 9 dock bookings, 1 orders released, 5 POs, 0 rush POs.

## Where the money went

Income **$38,022**. Expenses **$19,748** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 37,230 |
| Handling | 792 |

| Expense | $ |
|---|---|
| Inventory | 11,616 |
| Wages | 2,835 |
| Fines | 2,094 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x never released

- day 2: OG1705 (Sneaky Pete's Seafood) was never released to the floor. Fined $2,094. Lines 2, 444 cs, released=False.

## Change since last block
- Fixed / no longer seen: **11**
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (loop in)' ((#, #, #)) after #s.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #A line-up' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - ERROR|[MHEBoundary] 'PltJack_FULL(Clone)' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#).
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order ##ba##c#f-a#fb-# (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - WARN|[OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#.
- New this block: **4**
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - WARN|[AiNavigation] Employee_Mark Peterson cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.

## Bugs found (17 distinct, 67 incidents)

| Incidents | Bug |
|---|---|
| 16 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 7 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 5 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 4 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Mark Peterson cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (7.75, 1.11, 56.26) — walking out to (1.25, 1.25, 45.00) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-5.43, 1.12, 58.56) — walking out to (-5.43, 1.23, 45.56) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (2.11, 1.11, 54.32) — walking out to (-4.39, 1.25, 43.06) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-34.39, 1.11, 42.34).
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-10.14, 1.12, 58.94) — walking out to (-10.14, 1.20, 45.94) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-9.06, 1.13, 56.28) — walking out to (-2.56, 1.23, 45.02) (12.00m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 115/248.
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-37.43, 1.12, 50.75) — walking out to (-26.22, 1.12, 42.89) (12.69m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-36.24, 1.12, 48.07) — walking out to (-36.24, 0.04, 34.46) (12.61m to mesh).
- **ERROR** (day 2): [GridNav] 'Employee_Mark Peterson': no route for 'bay 02-03 (loop in)' from (-38.11, 1.11, 47.69) to (-17.59, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (6.14, 1.11, 59.00) runs through something solid - taking a clear way out to (7.53, 1.16, 59.00) instead.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): stuck at (-16.80, 1.11, 49.90) — walking out to (-18.19, 1.20, 50.48) (8.00m to mesh).
- **WARN** (day 2): [AiNavigation] Employee_Mark Peterson cannot reach task position at (5.91, 1.14, 41.69) (path invalid) — abandoning the walk and handing the task back.
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': no route for 'staging lane 3C line-up (route)' from (-29.76, 1.11, 48.49) to (-42.14, 1.11, 50.19) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': no route for 'staging lane 3C line-up' from (-29.76, 1.11, 48.49) to (7.24, 1.11, 51.33) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```

## Bot actions (first 400)
- d1 signed bulk BULK_Sneaky Pete's Seafood_D1_0
- d1 PO IG1960 Anchor Point Foods: 6 plt $2,688
- d1 placed parked Inbound Anchor Point Foods day 1 block 26 door 3
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 placed parked Inbound Anchor Point Foods day 1 block 26 door 2
- d1 PO IG1961 Blue Ridge Provisions: 4 plt $2,432
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 36 door 1
- d1 PO IG1962 Millbrook Family Foods: 1 plt $448
- d1 placed parked Inbound Millbrook Family Foods day 1 block 38 door 2
- d1 PO IG1963 Millbrook Family Foods: 6 plt $2,688
- d1 placed parked Inbound Millbrook Family Foods day 1 block 44 door 1
- d2 signed bulk BULK_Sneaky Pete's Seafood_D2_0
- d2 PO IG2964 Riverside Housewares: 5 plt $3,360
- d2 placed parked Inbound Riverside Housewares day 2 block 34 door 2
- d2 booked Sneaky Pete's Seafood day 3 block 18 door 2 (pallet-only, 7 pallet pick(s) / 2 reach truck(s): stock ready hour 46 + 10.2h of work)
- d2 released 1 order(s): OG4706 (trailer 11.8 h away; 11.2 h work + 2 h)
