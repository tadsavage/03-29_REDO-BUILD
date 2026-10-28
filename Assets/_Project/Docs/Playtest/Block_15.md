# Playtest block 15 — days 1–8

## Score: **35.0** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (5 shipped, 14 late) | 15% | 12.5 |
| Fill rate | 10% | 43.0 |
| Throughput (40 plt vs target 84) | 15% | 47.6 |
| Revenue ($26,446) | 15% | 47.2 |
| Profit (cash + stock -53,329) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 97.3 |
| Reliability (bugs) | 25% | 29.0 |
| Reputation (0 to 0) | 5% | 50.0 |

Orders shipped **5**, late/fined **14**, pallets shipped **40**. Cash $139,040 → $13,450 (-125,590). Stock at cost $0 → $72,261; cash + stock -53,329.
Bot did: 11 contracts signed, 15 dock bookings, 7 orders released, 24 POs, 0 rush POs.

## Bugs found (8 distinct, 16 incidents)

| Incidents | Bug |
|---|---|
| 2 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it. |
| 3 | NO-TASK —  lane pallet #B# sku=# load=# |
| 3 | NO-TASK —  lane pallet #A# sku=# load=# |
| 2 | ORPHAN-OUTBOUND —  #D# (order #-ada##cf-b### gone) |
| 2 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Daniel Sanders' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | ORPHAN-OUTBOUND —  #C# (order ##b#-b#c### gone) |
| 2 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **NO-TASK** (day 1): NO-TASK lane pallet 1B-5 sku=113489 load=0245169499
- **NO-TASK** (day 1): NO-TASK lane pallet 1A-1 sku=113489 load=5660366934
- **WARN** (day 1): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Daniel Sanders' as its operator though nobody is on it or running it - clearing the stale link.
- **WARN** (day 2): [OrderService] Order OG1496 closed with a STAGED pallet (34 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 2): ORPHAN-OUTBOUND 1D-1 (order 7e94fd20-ada5-42cf-b294-4134ddbdd87c gone)
- **ORPHAN-OUTBOUND** (day 2): ORPHAN-OUTBOUND 1C-1 (order b5946e3f-220b-4129-b6c4-6f2bc3d6e1bd gone)
- **WARN** (day 2): [AiNavigation] ReachTruck: stuck at (5.69, 1.16, 20.76) — walking out to (1.22, 0.08, 12.28) (8.65m to mesh).
- **DOOR-DEADLOCK** (day 4): OG4499 is Loading but door 1's truck has waited 90 min with no one loading it.

## Bot actions (first 400)
- d2 PO IG2720 Bulk Basin Foods: 1 plt $96
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 1 door 2
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 2 door 1
- d2 PO IG2723 Bulk Basin Foods: 4 plt $2,359
- d2 PO IG2724 Anchor Point Foods: 2 plt $1,584
- d2 PO IG2725 Homestead Market Co.: 1 plt $1,900
- d2 PO IG2726 Homestead Market Co.: 1 plt $720
- d2 released 1 order(s)
- d2 PO IG2727 Bulk Basin Foods: 2 plt $796
- d2 signed bulk BULK_Odd-Shape Orchards_D2_0
- d2 signed bulk BULK_Sneaky Pete's Seafood_D2_1
- d2 PO IG2728 Coastal Cargo Exchange: 5 plt $5,376
- d2 placed parked Inbound Anchor Point Foods day 2 block 10 door 2
- d2 PO IG2730 Anchor Point Foods: 2 plt $784
- d2 PO IG2731 Homestead Market Co.: 1 plt $2,000
- d2 PO IG2732 Backdoor Bill's Overstock: 1 plt $1,300
- d2 PO IG2733 Blue Ridge Provisions: 2 plt $3,718
- d2 placed parked Inbound Bulk Basin Foods day 3 block 1 door 1
- d3 PO IG3735 Ambrose Fine Foods: 7 plt $18,816
- d3 released 1 order(s)
- d3 signed bulk BULK_Caffeine Fiend Coffee_D3_0
- d3 signed bulk BULK_The Big Cheese Inc._D3_1
- d4 PO IG4736 Homestead Market Co.: 7 plt $3,360
- d4 PO IG4737 Anchor Point Foods: 7 plt $4,424
- d4 PO IG4738 Backdoor Bill's Overstock: 2 plt $512
- d4 released 1 order(s)
- d4 placed parked Inbound Bulk Basin Foods day 4 block 10 door 1
- d4 signed bulk BULK_Mama Tomato Sauces_D4_0
- d5 released 1 order(s)
- d5 signed bulk BULK_Twisted Twine Pasta_D5_0
- d6 released 1 order(s)
- d6 signed bulk BULK_Sprout Scout Foods_D6_0
- d6 signed bulk BULK_Mama Tomato Sauces_D6_1
- d7 released 1 order(s)
