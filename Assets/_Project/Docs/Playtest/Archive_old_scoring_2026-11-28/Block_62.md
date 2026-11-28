# Playtest block 62 — days 1–3

## Score: **12.9 (-27.1)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 2 late) | 10% | 0.0 (-100.0) |
| Fill rate | 10% | 0.0 (-100.0) |
| Throughput (0 plt vs target 24) | 10% | 0.0 (-45.8) |
| Revenue ($0) | not scored | 0.0 (-100.0) |
| Net Profit (-65,749) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 22.2 (+1.9) |
| Reputation (19 to 0) | 10% | 18.3 (-35.0) |

Orders shipped **0**, late/fined **2**, pallets shipped **0**. Cash $127,764 → $115,614 (-12,150). Stock at cost $200,106 → $146,507 (offsets stock bought in Net Profit).
Bot did: 2 contracts signed, 8 dock bookings, 1 orders released, 4 POs, 0 rush POs.

## Where the money went

Income **$0**. Expenses **$12,150** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|

| Expense | $ |
|---|---|
| Fines | 3,104 |
| Inventory | 3,008 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x never released
- 1 x loaded but the truck had not left

- day 2: OG1768 (Blocky Bakeshop) was never released to the floor. Fined $2,048. Lines 3, 388 cs, released=False.
- day 2: OG2624 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,056. Lines 27, 248 cs, released=True.

## Change since last block
- Fixed / no longer seen: **9**
  - WARN|[AiNavigation] Employee_Justin Waller: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - ERROR|[GridNav] 'Employee_Shirley Edwards': 'bay ## (aisle entry)' was interrupted # times - giving the leg up.
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - WARN|[AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Justin Waller cannot reach equipment at (#, #, #) (path invalid) — seeking next available
  - WARN|[OrderSelectionTaskDriver] Order #-d#cd#e#e#f#-# (Fizz-Pop Soda Works) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] Employee_Mark Peterson: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
- New this block: **5**
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress on order OG# for # game minutes - staging the # case(s) picked and freeing the rest.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' leaving the pallet jack (no progress) - its # case(s) stay on the jack for the next free selector to take over (order OG#).
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Shirley Edwards' made no progress on order OG# for # game minutes - staging the # case(s) picked and freeing the rest.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Shirley Edwards' leaving the pallet jack (no progress) - its # case(s) stay on the jack for the next free selector to take over (order OG#).
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (15 distinct, 49 incidents)

| Incidents | Bug |
|---|---|
| 12 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 7 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress on order OG# for # game minutes - staging the # case(s) picked and freeing the rest. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' leaving the pallet jack (no progress) - its # case(s) stay on the jack for the next free selector to take over (order OG#). |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Shirley Edwards' made no progress on order OG# for # game minutes - staging the # case(s) picked and freeing the rest. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Shirley Edwards' leaving the pallet jack (no progress) - its # case(s) stay on the jack for the next free selector to take over (order OG#). |
| 1 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.16, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-36.41, 1.11, 42.34).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (4.89, 1.12, 59.19) — walking out to (4.89, 1.23, 46.19) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (4.89, 1.12, 59.19) — walking out to (4.89, 1.23, 46.19) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-42.22, 1.12, 49.55) — walking out to (-30.96, 1.12, 42.96) (12.04m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 108/248.
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-18.93, 1.13, 41.31) — walking out to (-7.67, 1.20, 47.81) (12.00m to mesh).
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **WARN** (day 2): [AiNavigation] Employee_Mark Peterson: stuck at (-16.61, 1.15, 44.52) — walking out to (-29.61, 1.12, 43.37) (12.05m to mesh).
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone): stuck at (-6.30, 1.11, 55.65) — walking out to (0.20, 1.25, 44.39) (12.00m to mesh).
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress on order OG2624 for 10 game minutes - staging the 15 case(s) picked and freeing the rest.
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' leaving the pallet jack (no progress) - its 15 case(s) stay on the jack for the next free selector to take over (order OG2624).
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] 'Employee_Shirley Edwards' made no progress on order OG3625 for 10 game minutes - staging the 34 case(s) picked and freeing the rest.
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] 'Employee_Shirley Edwards' leaving the pallet jack (no progress) - its 34 case(s) stay on the jack for the next free selector to take over (order OG3625).

## Bot actions (first 400)
- d1 signed bulk BULK_Blocky Bakeshop_D1_0
- d1 PO IG1092 Bulk Basin Foods: 1 plt $672
- d1 placed parked Inbound Bulk Basin Foods day 1 block 25 door 1
- d1 placed parked Inbound Halloran Household day 1 block 26 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 2
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 3
- d1 PO IG1093 Cornerstone Staples: 2 plt $1,344
- d1 placed parked Inbound Cornerstone Staples day 1 block 37 door 1
- d1 PO IG1094 Cornerstone Staples: 1 plt $672
- d1 placed parked Inbound Cornerstone Staples day 1 block 44 door 1
- d2 signed bulk BULK_Crunch-Time Cereals_D2_0
- d2 PO IG2095 Halloran Household: 1 plt $320
- d2 placed parked Inbound Halloran Household day 2 block 34 door 2
- d2 booked Crunch-Time Cereals day 3 block 34 door 2 (pallet-only, 4 pallet pick(s) / 2 reach truck(s): stock ready hour 45 + 19.3h of work)
- d2 released 1 order(s): OG4769 (trailer 20.8 h away; 19.3 h work + 2 h)
