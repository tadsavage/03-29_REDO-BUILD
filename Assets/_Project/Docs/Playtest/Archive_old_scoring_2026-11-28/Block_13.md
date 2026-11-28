# Playtest block 13 — days 1–8

## Score: **44.8 (-6.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (8 shipped, 11 late) | 15% | 21.4 (+3.8) |
| Fill rate | 10% | 50.6 (-20.2) |
| Throughput (69 plt vs target 84) | 15% | 82.1 (-17.9) |
| Revenue ($41,593) | 15% | 74.3 (-25.7) |
| Profit (cash + stock -49,047) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 22.4 (+4.4) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **8**, late/fined **11**, pallets shipped **69**. Cash $139,040 → $25,452 (-113,588). Stock at cost $0 → $64,541; cash + stock -49,047.
Bot did: 10 contracts signed, 34 dock bookings, 9 orders released, 31 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **11**
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it.
  - FLOATING| OutboundPallet_##a##ac-ac###_# @#A# bottom=#
  - ERROR|[OrderSelectionTaskDriver] No free slot in lane #C for pallet #/# of order ##f#b#-b### (Sneaky Pete's Seafood) — dropping it beside the last one, where the loader will NOT find it. The lane was verified to have room before placement began, so something claimed a slot mid-drop.
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_##f#b#-b###_# @#C#
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nicole Barton' as its operator though nobody is on it or running it - clearing the stale link.
  - ORPHAN-OUTBOUND| #A# (order ##a##ac-ac### gone)
  - TRUCK-OVERLAP| Truck→Outbound_Door# & Truck→PO_IG# at (#, #, #)
  - REPEATING|x# and counting: [DockSchedule] Door #'s #:#–#:# slot on day # elapsed for PO IG# (status=not found) — door freed for rebooking, kept on the grid (IsComplete decides finished vs missed).
  - ORPHAN-OUTBOUND| #C# (order ##f#b#-b### gone)
  - ORPHAN-OUTBOUND| #B# (order ##f##f#f### gone)
- New this block: **6**
  - NO-TASK| lane pallet #C# sku=# load=#
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Maria Edwards: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered).
  - ORPHAN-OUTBOUND| #A# (order ##f#e#a-a#d### gone)
  - ORPHAN-OUTBOUND| #C# (order ##d##b#b-ae#d## gone)
  - ORPHAN-OUTBOUND| #C# (order ##b#f#-b#c-# gone)
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Patricia Hamilton' as its operator though nobody is on it or running it - clearing the stale link.

## Bugs found (13 distinct, 29 incidents)

| Incidents | Bug |
|---|---|
| 6 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) wait |
| 4 | NO-TASK —  lane pallet #C# sku=# load=# |
| 3 | NO-TASK —  lane pallet #A# sku=# load=# |
| 3 | NO-TASK —  lane pallet #B# sku=# load=# |
| 3 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 3 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Maria Edwards: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered). |
| 1 | ORPHAN-OUTBOUND —  #A# (order ##f#e#a-a#d### gone) |
| 1 | ORPHAN-OUTBOUND —  #C# (order ##d##b#b-ae#d## gone) |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | ORPHAN-OUTBOUND —  #C# (order ##b#f#-b#c-# gone) |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Patricia Hamilton' as its operator though nobody is on it or running it - clearing the stale link. |

## First sightings (with stack traces)

- **VEHICLE-STATIONARY** (day 1): PalletJack has a driver, hasn't moved in 240s while 3 task(s) wait
- **NO-TASK** (day 1): NO-TASK lane pallet 2A-3 sku=564219 load=7715743255
- **REPEATING** (day 2): x25 and counting: [ReceivingTaskDriver] Employee_Maria Edwards: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered).
- **NO-TASK** (day 2): NO-TASK lane pallet 1C-5 sku=435689 load=8029595860
- **WARN** (day 2): [OrderService] Order OG4428 closed with a STAGED pallet (45 cs) that never made the trailer - not billed, returning it to stock.
- **WARN** (day 2): [OrderService] Order OG2426 closed with a IN TRANSIT pallet (39 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 2): ORPHAN-OUTBOUND 1A-1 (order 6db90509-1f3e-474a-a2d5-437b58fa42af gone)
- **WARN** (day 2): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Patricia Hamilton' as its operator though nobody is on it or running it - clearing the stale link.
- **NO-TASK** (day 4): NO-TASK lane pallet 2B-5 sku=118432 load=6426321843
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 1C-1 (order a75c3580-3d69-4b9b-ae3d-9740b82fdc6a gone)
- **REPEATING** (day 5): x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (10.35, 1.11, 39.80) (no waypoint reachable for 8s) — warping to (11.21, 0.01, 39.04) to rejoin patrol.
- **ORPHAN-OUTBOUND** (day 6): ORPHAN-OUTBOUND 1C-1 (order 1bffc181-572b-4f66-b91c-d25865baa7fc gone)
- **REPEATING** (day 7): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-20.50, 1.11, 39.60) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1573 Bulk Basin Foods: 12 plt $3,248
- d1 placed parked Inbound Bulk Basin Foods day 1 block 5 door 1
- d1 PO IG1574 Bulk Basin Foods: 2 plt $1,465
- d1 placed parked Inbound Bulk Basin Foods day 1 block 5 door 2
- d1 PO IG1575 Backdoor Bill's Overstock: 2 plt $2,520
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 6 door 1
- d1 signed bulk BULK_The Happy Herd Co._D1_0
- d1 signed bulk BULK_Odd-Shape Orchards_D1_1
- d1 PO IG1576 Coastal Cargo Exchange: 9 plt $10,116
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 6 door 2
- d1 booked Odd-Shape Orchards day 1 block 11 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 17 + 3.3h of work)
- d1 PO IG1577 Anchor Point Foods: 2 plt $1,536
- d1 placed parked Inbound Anchor Point Foods day 1 block 7 door 1
- d1 PO IG1578 Bulk Basin Foods: 7 plt $3,080
- d1 placed parked Inbound Bulk Basin Foods day 1 block 7 door 2
- d1 PO IG1579 Blue Ridge Provisions: 4 plt $3,240
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 8 door 1
- d1 PO IG1580 Backdoor Bill's Overstock: 2 plt $608
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 8 door 2
- d1 PO IG1581 Homestead Market Co.: 3 plt $2,981
- d1 placed parked Inbound Homestead Market Co. day 1 block 9 door 1
- d1 released 1 order(s)
- d2 PO IG2582 Homestead Market Co.: 3 plt $1,888
- d2 placed parked Inbound Homestead Market Co. day 2 block 1 door 1
- d2 PO IG2583 Bulk Basin Foods: 4 plt $2,440
- d2 placed parked Inbound Bulk Basin Foods day 2 block 1 door 2
- d2 PO IG2584 Anchor Point Foods: 1 plt $448
- d2 placed parked Inbound Anchor Point Foods day 2 block 2 door 1
- d2 PO IG2585 Fairweather Trading Co.: 18 plt $18,333
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 2 door 2
- d2 booked The Happy Herd Co. day 2 block 7 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 33 + 3.3h of work)
- d2 released 1 order(s)
- d2 released 1 order(s)
- d2 signed bulk BULK_The Daily Grind Tofu_D2_0
- d3 PO IG3586 Backdoor Bill's Overstock: 2 plt $1,292
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 1 door 1
- d3 PO IG3587 Homestead Market Co.: 1 plt $540
- d3 placed parked Inbound Homestead Market Co. day 3 block 1 door 2
- d3 PO IG3588 Bulk Basin Foods: 4 plt $2,275
- d3 placed parked Inbound Bulk Basin Foods day 3 block 2 door 1
- d3 PO IG3589 Fairweather Trading Co.: 13 plt $9,620
- d3 placed parked Inbound Fairweather Trading Co. day 3 block 2 door 2
- d3 PO IG3590 Backdoor Bill's Overstock: 1 plt $272
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 3 door 1
- d3 PO IG3591 Homestead Market Co.: 1 plt $513
- d3 placed parked Inbound Homestead Market Co. day 3 block 3 door 2
- d3 signed bulk BULK_The Big Cheese Inc._D3_0
- d3 signed bulk BULK_Sprout Scout Foods_D3_1
- d3 released 1 order(s)
- d3 PO IG3592 Bulk Basin Foods: 5 plt $3,200
- d3 placed parked Inbound Bulk Basin Foods day 3 block 10 door 1
- d4 PO IG4593 Backdoor Bill's Overstock: 7 plt $7,140
- d4 placed parked Inbound Backdoor Bill's Overstock day 4 block 1 door 1
- d4 PO IG4594 Bulk Basin Foods: 9 plt $5,036
- d4 placed parked Inbound Bulk Basin Foods day 4 block 1 door 2
- d4 booked The Big Cheese Inc. day 4 block 6 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 79 + 3.3h of work)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 PO IG4595 Anchor Point Foods: 2 plt $952
- d4 PO IG4596 Homestead Market Co.: 1 plt $960
- d4 signed bulk BULK_Puff & Stuff Pastries_D4_0
- d4 placed parked Inbound Anchor Point Foods day 4 block 10 door 1
- d4 placed parked Inbound Homestead Market Co. day 4 block 10 door 2
- d5 PO IG5597 Golden Grove Gourmet: 8 plt $12,740
- d5 PO IG5598 Bulk Basin Foods: 4 plt $472
- d5 placed parked Inbound Golden Grove Gourmet day 5 block 1 door 1
- d5 placed parked Inbound Bulk Basin Foods day 5 block 1 door 2
- d5 signed bulk BULK_Fizz-Pop Soda Works_D5_0
- d5 released 1 order(s)
- d6 PO IG6599 Homestead Market Co.: 1 plt $920
- d6 placed parked Inbound Homestead Market Co. day 6 block 1 door 1
- d6 PO IG6600 Backdoor Bill's Overstock: 3 plt $816
- d6 PO IG6601 Anchor Point Foods: 2 plt $1,536
- d6 PO IG6602 Bulk Basin Foods: 2 plt $1,470
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 4 door 1
- d6 placed parked Inbound Anchor Point Foods day 6 block 4 door 2
- d6 placed parked Inbound Bulk Basin Foods day 6 block 5 door 1
- d6 released 1 order(s)
- d6 signed bulk BULK_Pony Express Oats_D6_0
- d6 signed bulk BULK_Pony Express Oats_D6_1
- d6 PO IG6603 Backdoor Bill's Overstock: 5 plt $1,280
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 10 door 1
- d7 released 1 order(s)
