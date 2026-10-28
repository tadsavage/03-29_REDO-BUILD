# Playtest block 16 — days 1–8

## Score: **35.0 (-20.6)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (5 shipped, 14 late) | 15% | 12.5 (-22.8) |
| Fill rate | 10% | 43.0 (-17.2) |
| Throughput (40 plt vs target 84) | 15% | 47.6 (-52.4) |
| Revenue ($26,446) | 15% | 47.2 (-51.5) |
| Profit (cash + stock -53,329) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 97.3 (-2.7) |
| Reliability (bugs) | 25% | 29.0 (+1.0) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **5**, late/fined **14**, pallets shipped **40**. Cash $139,040 → $13,450 (-125,590). Stock at cost $0 → $72,261; cash + stock -53,329.
Bot did: 3 contracts signed, 23 dock bookings, 1 orders released, 12 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **9**
  - EXCEPTION|MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  - ORPHAN-OUTBOUND| #B# (order #-dc##f#-bb### gone)
  - TRUCK-OVERLAP| Truck→Outbound_Door# & Truck→PO_IG# at (#, #, #)
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - NO-TASK| lane pallet #C# sku=# load=#
  - ORPHAN-OUTBOUND| #C# (order ##ce#f##e### gone)
  - ORPHAN-OUTBOUND| #B# (order #-a#b#cc##a### gone)
  - WARN|[OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock.
- New this block: **5**
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it.
  - ORPHAN-OUTBOUND| #D# (order #-ada##cf-b### gone)
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Daniel Sanders' as its operator though nobody is on it or running it - clearing the stale link.
  - ORPHAN-OUTBOUND| #C# (order ##b#-b#c### gone)
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (8 distinct, 16 incidents)

| Incidents | Bug |
|---|---|
| 2 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it. |
| 3 | NO-TASK —  lane pallet #A# sku=# load=# |
| 3 | NO-TASK —  lane pallet #B# sku=# load=# |
| 2 | ORPHAN-OUTBOUND —  #D# (order #-ada##cf-b### gone) |
| 2 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Daniel Sanders' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | ORPHAN-OUTBOUND —  #C# (order ##b#-b#c### gone) |
| 2 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **NO-TASK** (day 1): NO-TASK lane pallet 1A-1 sku=113489 load=5660366934
- **NO-TASK** (day 1): NO-TASK lane pallet 1B-5 sku=113489 load=0245169499
- **WARN** (day 1): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Daniel Sanders' as its operator though nobody is on it or running it - clearing the stale link.
- **WARN** (day 2): [OrderService] Order OG1496 closed with a STAGED pallet (34 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 2): ORPHAN-OUTBOUND 1D-1 (order 7e94fd20-ada5-42cf-b294-4134ddbdd87c gone)
- **ORPHAN-OUTBOUND** (day 2): ORPHAN-OUTBOUND 1C-1 (order b5946e3f-220b-4129-b6c4-6f2bc3d6e1bd gone)
- **WARN** (day 2): [AiNavigation] ReachTruck: stuck at (5.69, 1.16, 20.76) — walking out to (1.22, 0.08, 12.28) (8.65m to mesh).
- **DOOR-DEADLOCK** (day 4): OG4499 is Loading but door 1's truck has waited 90 min with no one loading it.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1713 Anchor Point Foods: 3 plt $1,612
- d1 placed parked Inbound Anchor Point Foods day 1 block 5 door 1
- d1 PO IG1714 Bulk Basin Foods: 5 plt $1,776
- d1 placed parked Inbound Bulk Basin Foods day 1 block 5 door 2
- d1 PO IG1715 Backdoor Bill's Overstock: 2 plt $512
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 6 door 1
- d1 signed bulk BULK_Sprout Scout Foods_D1_0
- d1 PO IG1716 Bulk Basin Foods: 7 plt $4,900
- d1 placed parked Inbound Bulk Basin Foods day 1 block 6 door 2
- d1 PO IG1717 Homestead Market Co.: 6 plt $4,610
- d1 placed parked Inbound Homestead Market Co. day 1 block 7 door 1
- d1 PO IG1718 Homestead Market Co.: 1 plt $640
- d1 placed parked Inbound Homestead Market Co. day 1 block 7 door 2
- d1 PO IG1719 Golden Grove Gourmet: 7 plt $15,400
- d1 placed parked Inbound Golden Grove Gourmet day 1 block 8 door 1
- d1 booked Sprout Scout Foods day 1 block 11 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 18 + 3.3h of work)
- d1 released 1 order(s)
- d2 placed parked Inbound Bulk Basin Foods day 2 block 1 door 1
- d2 PO IG2721 Fairweather Trading Co.: 12 plt $7,127
- d2 PO IG2722 Blue Ridge Provisions: 4 plt $2,832
- d2 placed parked Inbound Bulk Basin Foods day 2 block 2 door 2
- d2 placed parked Inbound Anchor Point Foods day 2 block 3 door 1
- d2 placed parked Inbound Homestead Market Co. day 2 block 3 door 2
- d2 placed parked Inbound Homestead Market Co. day 2 block 4 door 1
- d2 placed parked Inbound Bulk Basin Foods day 2 block 4 door 2
- d2 placed parked Inbound Coastal Cargo Exchange day 2 block 10 door 1
- d2 PO IG2729 Anchor Point Foods: 8 plt $3,360
- d2 placed parked Inbound Anchor Point Foods day 2 block 11 door 1
- d2 placed parked Inbound Homestead Market Co. day 2 block 11 door 2
- d2 placed parked Inbound Backdoor Bill's Overstock day 3 block 0 door 1
- d2 placed parked Inbound Blue Ridge Provisions day 3 block 0 door 2
- d2 PO IG2734 Bulk Basin Foods: 3 plt $1,240
- d3 placed parked Inbound Ambrose Fine Foods day 3 block 1 door 2
- d3 signed bulk BULK_The Daily Grind Tofu_D3_2
- d4 placed parked Inbound Homestead Market Co. day 4 block 1 door 1
- d4 placed parked Inbound Anchor Point Foods day 4 block 1 door 2
- d4 placed parked Inbound Backdoor Bill's Overstock day 4 block 2 door 1
- d4 PO IG4739 Bulk Basin Foods: 1 plt $104
