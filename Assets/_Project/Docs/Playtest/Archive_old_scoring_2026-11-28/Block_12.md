# Playtest block 12 — days 1–8

## Score: **39.1** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (5 shipped, 11 late) | 15% | 8.3 |
| Fill rate | 10% | 67.7 |
| Throughput (57 plt vs target 84) | 15% | 67.9 |
| Revenue ($34,826) | 15% | 62.2 |
| Profit (cash + stock -115,804) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 82.4 |
| Reliability (bugs) | 25% | 20.0 |
| Reputation (0 to 0) | 5% | 50.0 |

Orders shipped **5**, late/fined **11**, pallets shipped **57**. Cash $139,040 → $8,550 (-130,490). Stock at cost $0 → $14,686; cash + stock -115,804.
Bot did: 7 contracts signed, 31 dock bookings, 9 orders released, 26 POs, 0 rush POs.

## Bugs found (12 distinct, 31 incidents)

| Incidents | Bug |
|---|---|
| 14 | VEHICLE-STATIONARY — Dockstocker has a driver, hasn't moved in #s while # task(s) wait |
| 3 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it. |
| 3 | NO-TASK —  lane pallet #B# sku=# load=# |
| 2 | NO-TASK —  lane pallet #C# sku=# load=# |
| 2 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 1 | REPEATING — x# and counting: Parameter 'IsWaving' does not exist. |
| 1 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) wait |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | ORPHAN-OUTBOUND —  #B# (order #-a#c#fe##a### gone) |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **REPEATING** (day 1): x25 and counting: Parameter 'IsWaving' does not exist.
- **NO-TASK** (day 1): NO-TASK lane pallet 2B-3 sku=864209 load=7173993645
- **VEHICLE-STATIONARY** (day 2): PalletJack has a driver, hasn't moved in 615s while 31 task(s) wait
- **NO-TASK** (day 2): NO-TASK lane pallet 1C-5 sku=659083 load=8273443677
- **TRUCK-OVERLAP** (day 3): TRUCK-OVERLAP Truck→PO_IG2619 & Truck→PO_IG2618 at (-7.6, 0.0, -42.0)
- **DOOR-DEADLOCK** (day 3): OG2443 is Staged but door 1's truck has waited 90 min with no one loading it.
- **REPEATING** (day 3): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-23.71, 1.11, 34.16) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **REPEATING** (day 4): x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (-8.61, 1.11, 40.10) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **DOOR-DEADLOCK** (day 4): OG3444 is Loading but door 1's truck has waited 90 min with no one loading it.
- **WARN** (day 4): [OrderService] Order OG3444 closed with a STAGED pallet (26 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 1B-1 (order 78321068-a89c-4fe6-98a0-322d23810771 gone)
- **VEHICLE-STATIONARY** (day 5): Dockstocker has a driver, hasn't moved in 255s while 10 task(s) wait

## Bot actions (first 400)
- d1 released 1 order(s)
- d2 PO IG2612 Ambrose Fine Foods: 6 plt $11,544
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 1 door 2
- d2 booked Fizz-Pop Soda Works day 2 block 6 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 31 + 3.3h of work)
- d2 PO IG2614 Bulk Basin Foods: 3 plt $848
- d2 PO IG2615 Anchor Point Foods: 2 plt $1,268
- d2 PO IG2616 Bulk Basin Foods: 1 plt $120
- d2 placed parked Inbound Homestead Market Co. day 2 block 3 door 2
- d2 released 1 order(s)
- d2 released 1 order(s)
- d2 signed bulk BULK_Odd-Shape Orchards_D2_0
- d2 signed bulk BULK_Mama Tomato Sauces_D2_1
- d2 PO IG2618 Bulk Basin Foods: 8 plt $5,760
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 10 door 2
- d2 placed parked Inbound Bulk Basin Foods day 2 block 11 door 1
- d2 placed parked Inbound Anchor Point Foods day 2 block 11 door 2
- d3 PO IG3622 Homestead Market Co.: 2 plt $1,056
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 1 door 2
- d3 PO IG3624 Homestead Market Co.: 10 plt $5,472
- d3 released 1 order(s)
- d3 PO IG3625 Backdoor Bill's Overstock: 2 plt $1,384
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 3 door 2
- d3 PO IG3626 Anchor Point Foods: 1 plt $768
- d3 placed parked Inbound Homestead Market Co. day 3 block 2 door 2
- d3 PO IG3627 Cornerstone Staples: 8 plt $4,352
- d3 PO IG3628 Cornerstone Staples: 2 plt $1,088
- d3 placed parked Inbound Anchor Point Foods day 3 block 5 door 2
- d3 placed parked Inbound Cornerstone Staples day 3 block 6 door 1
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 5 door 2
- d3 placed parked Inbound Anchor Point Foods day 3 block 6 door 2
- d3 signed bulk BULK_Sneaky Pete's Seafood_D3_0
- d3 signed bulk BULK_Grizzly Ridge Jerky_D3_1
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 7 door 1
- d3 released 1 order(s)
- d4 PO IG4632 Iron Gate Staples: 2 plt $1,360
- d4 placed parked Inbound Homestead Market Co. day 4 block 1 door 2
- d4 PO IG4634 Backdoor Bill's Overstock: 4 plt $1,408
- d4 PO IG4635 Iron Gate Staples: 3 plt $600
- d4 PO IG4636 Fairweather Trading Co.: 16 plt $6,240
- d4 placed parked Inbound Last Call Liquidators day 4 block 3 door 2
- d4 placed parked Inbound Steady Supply Grocers day 4 block 4 door 1
- d4 placed parked Inbound Last Call Liquidators day 4 block 4 door 2
- d4 placed parked Inbound Steady Supply Grocers day 4 block 4 door 1
- d4 released 1 order(s)
- d5 PO IG5639 Steady Supply Grocers: 10 plt $5,440
- d5 placed parked Inbound Halloran Household day 5 block 1 door 2
- d5 placed parked Inbound Steady Supply Grocers day 5 block 2 door 1
- d5 released 1 order(s)
- d6 PO IG6642 Steady Supply Grocers: 2 plt $400
- d6 released 1 order(s)
- d7 released 1 order(s)
