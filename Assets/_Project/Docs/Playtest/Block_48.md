# Playtest block 48 — days 1–3

## Score: **11.3 (-19.6)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 2 late) | 10% | 0.0 (-66.7) |
| Fill rate | 10% | 0.0 (-100.0) |
| Throughput (0 plt vs target 24) | 10% | 0.0 (-41.7) |
| Revenue ($0) | not scored | 0.0 (-100.0) |
| Net Profit (-142,816) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 18.9 (+4.1) |
| Reputation (19 to 0) | 10% | 18.3 (-8.3) |

Orders shipped **0**, late/fined **2**, pallets shipped **0**. Cash $127,764 → $32,170 (-95,594). Stock at cost $200,106 → $152,884 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 13 dock bookings, 1 orders released, 8 POs, 0 rush POs.

## Where the money went

Income **$0**. Expenses **$95,594** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|

| Expense | $ |
|---|---|
| Inventory | 80,835 |
| Fines | 8,721 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x never released
- 1 x loaded but the truck had not left

- day 2: OG1721 (Pony Express Oats) was never released to the floor. Fined $7,665. Lines 2, 1130 cs, released=False.
- day 2: OG2624 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,056. Lines 27, 248 cs, released=True.

## Change since last block
- Fixed / no longer seen: **14**
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: [#: own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: own/Staged/y#] 
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (aisle entry)' ((#, #, #)) after #s.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#).
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - WARN|[OrderSelectionTaskDriver] Order ##c##bd-ae#e## (Mama Tomato Sauces) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##ba##c#f-a#fb-# (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##e##ab#f## (Neon Nectar Juice Co.) has no reachable pick location for its remaining line item(s) — staging short at #/#.
- New this block: **5**
  - VEHICLE-STATIONARY|PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - WARN|[OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.

## Bugs found (16 distinct, 63 incidents)

| Incidents | Bug |
|---|---|
| 7 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 6 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 10 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 4 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 2 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-18.36, 1.11, 53.41) — walking out to (-19.74, 1.18, 53.41) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-2.08, 1.12, 59.16) — walking out to (-2.08, 1.24, 46.16) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-3.85, 1.12, 59.16) — walking out to (-3.85, 1.24, 46.16) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-23.25, 1.11, 50.19).
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (-10.48, 1.11, 59.00) — walking out to (-9.09, 1.16, 59.00) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-39.69, 1.12, 50.96) — walking out to (-28.49, 1.12, 42.88) (12.81m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-38.20, 1.12, 50.55) — walking out to (-26.98, 1.12, 42.90) (12.58m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 105/248.
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **ERROR** (day 1): [GridNav] 'Employee_Mark Peterson': no route for 'bay 02-04 (aisle entry)' from (-20.61, 1.11, 44.93) to (-42.14, 1.11, 47.66) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 02-04' - driving straight to the stop instead.
- **ERROR** (day 1): [GridNav] 'Employee_Mark Peterson': no route for 'bay 02-04' from (-20.61, 1.11, 44.93) to (-29.19, 1.11, 47.66) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] Employee_Shirley Edwards: stuck at (-15.59, 1.15, 43.68) — walking out to (-28.59, 1.12, 43.44) (12.00m to mesh).
- **VEHICLE-STATIONARY** (day 2): RT_Full(Clone) has a driver, hasn't moved in 360s while 3 task(s) it could take wait
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone): stuck at (-31.51, 1.11, 41.14) — walking out to (-32.90, 1.17, 40.56) (12.28m to mesh).

## Bot actions (first 400)
- d1 signed bulk BULK_Pony Express Oats_D1_0
- d1 PO IG1985 Fairweather Trading Co.: 7 plt $9,625
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 26 door 3
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 PO IG1986 Coastal Cargo Exchange: 6 plt $10,560
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 26 door 2
- d1 booked Pony Express Oats day 3 block 11 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 16 pallet pick(s) / 2 reach truck(s): stock ready hour 19 + 28.1h of work)
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 26 door 3
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 28 door 2
- d1 PO IG1987 Halloran Household: 7 plt $10,010
- d1 placed parked Inbound Halloran Household day 2 block 0 door 1
- d1 PO IG1988 Coastal Cargo Exchange: 6 plt $10,560
- d1 placed parked Inbound Coastal Cargo Exchange day 2 block 0 door 2
- d2 released 1 order(s): OG1721 (trailer 24.8 h away; 24.8 h work + 2 h)
- d2 PO IG2989 Meridian Provisions: 7 plt $9,240
- d2 placed parked Inbound Meridian Provisions day 2 block 14 door 1
- d2 PO IG2990 Dockside Closeouts: 6 plt $10,560
- d2 placed parked Inbound Dockside Closeouts day 2 block 16 door 2
- d2 PO IG2991 Pantry Row Distributors: 7 plt $9,240
- d2 placed parked Inbound Pantry Row Distributors day 2 block 32 door 1
- d2 PO IG2992 Coastal Cargo Exchange: 6 plt $11,040
- d2 placed parked Inbound Coastal Cargo Exchange day 2 block 34 door 3
