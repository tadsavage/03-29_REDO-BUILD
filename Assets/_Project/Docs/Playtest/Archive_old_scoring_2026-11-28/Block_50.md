# Playtest block 50 — days 1–3

## Score: **35.8 (-4.4)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (4 shipped, 1 late) | 10% | 80.0 (-20.0) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (12 plt vs target 24) | 10% | 50.0 (-20.8) |
| Revenue ($38,022) | not scored | 100.0 (0.0) |
| Net Profit (-50,167) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 13.2 (-3.1) |
| Reputation (19 to 26) | 10% | 61.7 (+11.7) |

Orders shipped **4**, late/fined **1**, pallets shipped **12**. Cash $127,764 → $146,688 (+18,924). Stock at cost $200,106 → $131,015 (offsets stock bought in Net Profit).
Bot did: 2 contracts signed, 6 dock bookings, 1 orders released, 3 POs, 0 rush POs.

## Where the money went

Income **$38,022**. Expenses **$19,098** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 37,230 |
| Handling | 792 |

| Expense | $ |
|---|---|
| Inventory | 10,266 |
| Wages | 2,835 |
| Fines | 2,794 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x never released

- day 2: OG1727 (Caffeine Fiend Coffee) was never released to the floor. Fined $2,794. Lines 2, 469 cs, released=False.

## Change since last block
- Fixed / no longer seen: **9**
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #C line-up' (#m short) - giving up on this stop.
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'pallet jack parking' (#m short) - giving up on this stop.
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane #B: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s).
  - ERROR|[TrailerLoad] Could not reach lane #B — leaving OutboundPallet_##bc#ec##d-#_# staged.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#).
  - WARN|[OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - WARN|[OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#.
- New this block: **8**
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (loop in) (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - VEHICLE-STATIONARY|PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'staging lane #B wait (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'staging lane #B wait' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - WARN|[OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#.

## Bugs found (20 distinct, 76 incidents)

| Incidents | Bug |
|---|---|
| 15 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (loop in) (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 3 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 3 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 3 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 8 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 7 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 4 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 3 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'staging lane #B wait (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'staging lane #B wait' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 2 | WARN — [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-13.96, 1.11, 54.49) — walking out to (-7.46, 1.25, 43.23) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (-0.88, 1.11, 53.63) — walking out to (-1.45, 1.19, 55.01) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-6.53, 1.12, 58.46) — walking out to (-13.03, 1.16, 47.20) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (3.21, 1.12, 59.22) — walking out to (3.21, 1.18, 46.22) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.59, 1.11, 50.19).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-35.67, 1.12, 49.93) — walking out to (-24.42, 1.12, 42.93) (12.24m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 227/248.
- **ERROR** (day 1): [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02 (loop in) (route)' from (-33.28, 1.11, 48.46) to (-17.59, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02 (loop in)' from (-33.28, 1.11, 48.46) to (-42.14, 1.11, 50.19) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02 (aisle entry)' from (-33.28, 1.11, 48.46) to (-16.26, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-02' - driving straight to the stop instead.
- **ERROR** (day 1): [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02' from (-33.28, 1.11, 48.46) to (-23.91, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-36.01, 1.12, 46.54) — walking out to (-42.48, 0.04, 34.58) (12.60m to mesh).
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-36.46, 1.12, 48.34) — walking out to (-36.46, 0.04, 34.46) (12.88m to mesh).
- **ERROR** (day 1): [GridNav] 'Employee_Shirley Edwards': no route for 'staging lane 1B wait (route)' from (-24.16, 1.11, 47.28) to (-42.14, 1.11, 50.19) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): [GridNav] 'Employee_Shirley Edwards': no route for 'staging lane 1B wait' from (-24.16, 1.11, 47.28) to (-11.31, 1.11, 55.33) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 2): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 21 task(s) it could take wait
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-5.94, 1.11, 58.17) runs through something solid - taking a clear way out to (-6.52, 1.16, 59.00) instead.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order b72defae-0fda-441b-9c33-e78561011633 (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at 46/48.

## Bot actions (first 400)
- d1 signed bulk BULK_Caffeine Fiend Coffee_D1_0
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 PO IG1001 Millbrook Family Foods: 1 plt $594
- d1 placed parked Inbound Millbrook Family Foods day 1 block 39 door 1
- d1 PO IG1002 Coastal Cargo Exchange: 6 plt $6,072
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 42 door 1
- d2 signed bulk BULK_Salty Sailor Pretzel Co._D2_0
- d2 PO IG2003 Dockside Closeouts: 3 plt $3,600
- d2 placed parked Inbound Dockside Closeouts day 2 block 34 door 2
- d2 booked Salty Sailor Pretzel Co. day 3 block 14 door 2 (pallet-only, 5 pallet pick(s) / 2 reach truck(s): stock ready hour 45 + 9.6h of work)
- d2 released 1 order(s): OG5728 (trailer 11.8 h away; 9.9 h work + 2 h)
