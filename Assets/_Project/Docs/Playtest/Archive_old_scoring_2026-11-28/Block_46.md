# Playtest block 46 — days 1–3

## Score: **42.8 (+16.4)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (4 shipped, 0 late) | 10% | 100.0 (+33.3) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (14 plt vs target 24) | 10% | 58.3 (+25.0) |
| Revenue ($38,082) | not scored | 100.0 (0.0) |
| Net Profit (-33,670) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 23.6 (+14.6) |
| Reputation (19 to 20) | 10% | 51.7 (+33.3) |

Orders shipped **4**, late/fined **0**, pallets shipped **14**. Cash $127,764 → $159,808 (+32,044). Stock at cost $200,106 → $134,392 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 2 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went

Income **$38,082**. Expenses **$6,038** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 37,230 |
| Handling | 852 |

| Expense | $ |
|---|---|
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Change since last block
- Fixed / no longer seen: **18**
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (aisle entry) (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (aisle entry) (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - REPEATING|x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- New this block: **1**
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (13 distinct, 68 incidents)

| Incidents | Bug |
|---|---|
| 16 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 12 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 7 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 7 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 7 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 6 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 2 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 2 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (4.40, 1.12, 59.25) — walking out to (4.40, 1.23, 46.25) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-3.24, 1.12, 59.25) — walking out to (-3.24, 1.24, 46.25) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (-10.68, 1.11, 56.22) — walking out to (-10.68, 1.25, 43.22) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-41.57, 1.12, 51.33) — walking out to (-48.07, 1.08, 40.07) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-23.98, 1.11, 50.19).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 91/248.
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-41.17, 1.12, 49.11) — walking out to (-47.67, 1.08, 37.85) (12.00m to mesh).
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-6.01, 1.11, 53.57) runs through something solid - taking a clear way out to (-5.44, 1.19, 54.96) instead.
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone): stuck at (-33.25, 1.11, 48.96) — walking out to (-34.75, 1.19, 48.96) (9.00m to mesh).
- **WARN** (day 2): [AiNavigation] Employee_Larry Morgan: stuck at (-33.57, 1.12, 49.68) — walking out to (-22.32, 1.12, 42.95) (12.11m to mesh).
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): stuck at (-41.37, 1.11, 44.97) — walking out to (-42.76, 1.16, 44.40) (12.13m to mesh).
- **VEHICLE-STATIONARY** (day 2): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 8 task(s) it could take wait

## Bot actions (first 400)
- d1 signed bulk BULK_Neon Nectar Juice Co._D1_0
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
