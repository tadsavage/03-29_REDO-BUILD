# Playtest block 14 — days 1–8

## Score: **39.3 (-5.5)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (5 shipped, 11 late) | 15% | 8.3 (-13.1) |
| Fill rate | 10% | 67.7 (+17.1) |
| Throughput (57 plt vs target 84) | 15% | 67.9 (-14.3) |
| Revenue ($34,826) | 15% | 62.2 (-12.1) |
| Profit (cash + stock -115,803) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 82.4 (-17.6) |
| Reliability (bugs) | 25% | 20.6 (-1.7) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **5**, late/fined **11**, pallets shipped **57**. Cash $139,039 → $8,550 (-130,489). Stock at cost $0 → $14,686; cash + stock -115,803.
Bot did: 3 contracts signed, 27 dock bookings, 0 orders released, 22 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **8**
  - VEHICLE-STATIONARY|PalletJack has a driver, hasn't moved in #s while # task(s) wait
  - NO-TASK| lane pallet #A# sku=# load=#
  - WARN|[OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Maria Edwards: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered).
  - ORPHAN-OUTBOUND| #A# (order ##f#e#a-a#d### gone)
  - ORPHAN-OUTBOUND| #C# (order ##d##b#b-ae#d## gone)
  - ORPHAN-OUTBOUND| #C# (order ##b#f#-b#c-# gone)
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Patricia Hamilton' as its operator though nobody is on it or running it - clearing the stale link.
- New this block: **6**
  - VEHICLE-STATIONARY|Dockstocker has a driver, hasn't moved in #s while # task(s) wait
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it.
  - TRUCK-OVERLAP| Truck→PO_IG# & Truck→PO_IG# at (#, #, #)
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it.
  - REPEATING|x# and counting: Parameter 'IsWaving' does not exist.
  - ORPHAN-OUTBOUND| #B# (order #-a#c#fe##a### gone)

## Bugs found (11 distinct, 30 incidents)

| Incidents | Bug |
|---|---|
| 14 | VEHICLE-STATIONARY — Dockstocker has a driver, hasn't moved in #s while # task(s) wait |
| 3 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it. |
| 3 | NO-TASK —  lane pallet #B# sku=# load=# |
| 2 | NO-TASK —  lane pallet #C# sku=# load=# |
| 2 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 1 | REPEATING — x# and counting: Parameter 'IsWaving' does not exist. |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | ORPHAN-OUTBOUND —  #B# (order #-a#c#fe##a### gone) |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **REPEATING** (day 1): x25 and counting: Parameter 'IsWaving' does not exist.
- **NO-TASK** (day 1): NO-TASK lane pallet 2B-3 sku=864209 load=7173993645
- **NO-TASK** (day 2): NO-TASK lane pallet 1C-5 sku=659083 load=8273443677
- **TRUCK-OVERLAP** (day 3): TRUCK-OVERLAP Truck→PO_IG2619 & Truck→PO_IG2618 at (-7.6, 0.0, -48.1)
- **DOOR-DEADLOCK** (day 3): OG2443 is Staged but door 1's truck has waited 90 min with no one loading it.
- **REPEATING** (day 3): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-23.71, 1.11, 34.16) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **REPEATING** (day 4): x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (-8.61, 1.11, 40.10) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **DOOR-DEADLOCK** (day 4): OG3444 is Loading but door 1's truck has waited 90 min with no one loading it.
- **WARN** (day 4): [OrderService] Order OG3444 closed with a STAGED pallet (26 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 1B-1 (order 78321068-a89c-4fe6-98a0-322d23810771 gone)
- **VEHICLE-STATIONARY** (day 5): Dockstocker has a driver, hasn't moved in 240s while 10 task(s) wait

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1604 Anchor Point Foods: 4 plt $2,608
- d1 placed parked Inbound Anchor Point Foods day 1 block 5 door 1
- d1 PO IG1605 Bulk Basin Foods: 3 plt $1,484
- d1 placed parked Inbound Bulk Basin Foods day 1 block 5 door 2
- d1 PO IG1606 Homestead Market Co.: 3 plt $3,616
- d1 placed parked Inbound Homestead Market Co. day 1 block 6 door 1
- d1 PO IG1607 Backdoor Bill's Overstock: 3 plt $1,716
- d1 signed bulk BULK_Fizz-Pop Soda Works_D1_0
- d1 signed bulk BULK_Sneaky Pete's Seafood_D1_1
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 6 door 2
- d1 PO IG1608 Anchor Point Foods: 9 plt $7,560
- d1 placed parked Inbound Anchor Point Foods day 1 block 7 door 1
- d1 booked Sneaky Pete's Seafood day 1 block 11 door 1 (pallet-only: stock ready hour 19 + 1.5h of work)
- d1 PO IG1609 Bulk Basin Foods: 1 plt $520
- d1 placed parked Inbound Bulk Basin Foods day 1 block 7 door 2
- d1 PO IG1610 Homestead Market Co.: 3 plt $3,360
- d1 placed parked Inbound Homestead Market Co. day 1 block 8 door 1
- d1 PO IG1611 Homestead Market Co.: 13 plt $7,872
- d1 placed parked Inbound Homestead Market Co. day 1 block 8 door 2
- d2 placed parked Inbound Ambrose Fine Foods day 2 block 1 door 1
- d2 PO IG2613 Blue Ridge Provisions: 8 plt $7,638
- d2 placed parked Inbound Bulk Basin Foods day 2 block 2 door 1
- d2 placed parked Inbound Anchor Point Foods day 2 block 2 door 2
- d2 placed parked Inbound Bulk Basin Foods day 2 block 3 door 1
- d2 PO IG2617 Homestead Market Co.: 1 plt $540
- d2 placed parked Inbound Bulk Basin Foods day 2 block 10 door 1
- d2 booked Odd-Shape Orchards day 3 block 2 door 1 (pallet-only: stock ready hour 49 + 1.5h of work)
- d2 PO IG2619 Backdoor Bill's Overstock: 8 plt $8,640
- d2 PO IG2620 Bulk Basin Foods: 2 plt $1,440
- d2 PO IG2621 Anchor Point Foods: 1 plt $672
- d3 placed parked Inbound Homestead Market Co. day 3 block 1 door 1
- d3 PO IG3623 Backdoor Bill's Overstock: 4 plt $1,216
- d3 placed parked Inbound Homestead Market Co. day 3 block 2 door 2
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 3 door 1
- d3 placed parked Inbound Anchor Point Foods day 3 block 4 door 1
- d3 placed parked Inbound Cornerstone Staples day 3 block 4 door 2
- d3 placed parked Inbound Cornerstone Staples day 3 block 5 door 1
- d3 PO IG3629 Anchor Point Foods: 1 plt $672
- d3 PO IG3630 Cornerstone Staples: 1 plt $544
- d3 PO IG3631 Blue Ridge Provisions: 10 plt $10,356
- d4 placed parked Inbound Iron Gate Staples day 4 block 1 door 1
- d4 PO IG4633 Homestead Market Co.: 1 plt $640
- d4 placed parked Inbound Backdoor Bill's Overstock day 4 block 2 door 1
- d4 placed parked Inbound Iron Gate Staples day 4 block 2 door 2
- d4 placed parked Inbound Fairweather Trading Co. day 4 block 3 door 1
- d4 PO IG4637 Last Call Liquidators: 1 plt $672
- d4 PO IG4638 Steady Supply Grocers: 14 plt $6,928
- d5 placed parked Inbound Steady Supply Grocers day 5 block 1 door 1
- d5 PO IG5640 Halloran Household: 12 plt $3,712
- d5 PO IG5641 Steady Supply Grocers: 3 plt $336
- d6 placed parked Inbound Steady Supply Grocers day 6 block 1 door 1
