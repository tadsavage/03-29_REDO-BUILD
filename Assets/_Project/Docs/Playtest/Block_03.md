# Playtest block 3 — days 1–8

## Score: **23.2 (-28.0)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 14 late) | 15% | 0.0 (-44.4) |
| Fill rate | 10% | 0.0 (-44.4) |
| Throughput (0 plt vs target 84) | 15% | 0.0 (-88.1) |
| Revenue ($0) | 15% | 0.0 (-83.6) |
| Profit (cash -139,456) | 10% | 0.0 (0.0) |
| Cash health (floor $10,000) | 5% | 81.1 (-18.9) |
| Reliability (bugs) | 25% | 66.7 (+39.4) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **0**, late/fined **14**, pallets shipped **0**. Cash $144,538 → $5,082 (-139,456).
Bot did: 9 contracts signed, 50 dock bookings, 16 orders released, 15 POs, 5 rush POs.

## Change since last block
- Fixed / no longer seen: **14**
  - NO-TASK| lane pallet #A# sku=# load=#
  - ORPHAN-OUTBOUND| #B# (order ##f##a##c## gone)
  - FLOATING| OutboundPallet_##f##a##c##_# @#B# bottom=#
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'return pallet pickup' ((#, #, #)) after #s.
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Mark Bennett' as its operator though nobody is on it or running it - clearing the stale link.
  - NO-TASK| lane pallet #B# sku=# load=#
  - WARN|[OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - VEHICLE-STATIONARY|PalletJack has a driver, hasn't moved in #s while # task(s) wait
  - WARN|[AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ORPHAN-OUTBOUND| #A# (order #-d#ed#ab#-b### gone)
  - ORPHAN-OUTBOUND| #B# (order ##e#c#df#-b### gone)
  - VEHICLE-STATIONARY|ReachTruck has a driver, hasn't moved in #s while # task(s) wait
  - WARN|[OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **3**
  - TRUCK-OVERLAP| Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #)
  - WARN|[AiNavigation] Employee_Douglas Stewart: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Donna Ortiz: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (3 distinct, 4 incidents)

| Incidents | Bug |
|---|---|
| 2 | TRUCK-OVERLAP —  Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #) |
| 1 | WARN — [AiNavigation] Employee_Douglas Stewart: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Donna Ortiz: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Douglas Stewart: stuck at (-21.51, 1.16, 22.66) — walking out to (-8.52, 1.13, 24.12) (12.08m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Donna Ortiz: stuck at (-21.51, 1.16, 22.68) — walking out to (-8.52, 1.13, 24.12) (12.07m to mesh).
- **TRUCK-OVERLAP** (day 5): TRUCK-OVERLAP Truck→Outbound_Door1 & Truck→Outbound_Door2 at (3.8, 0.0, -2.0)

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 released 2 order(s)
- d1 RUSH PO IG1430 Rush Courier: 4 plt $12,064
- d1 placed parked Inbound Rush Courier day 1 block 5 door 1
- d1 PO IG1431 Backdoor Bill's Overstock: 4 plt $2,416
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 5 door 2
- d1 PO IG1432 Anchor Point Foods: 7 plt $4,116
- d1 placed parked Inbound Anchor Point Foods day 1 block 6 door 1
- d1 signed bulk BULK_Sprout Scout Foods_D1_0
- d1 signed bulk BULK_Crunch-Time Cereals_D1_1
- d1 booked Crunch-Time Cereals day 1 block 6 door 2
- d1 booked Sprout Scout Foods day 1 block 7 door 1
- d1 released 2 order(s) (waiting for lane)
- d1 PO IG1433 Homestead Market Co.: 1 plt $1,596
- d1 placed parked Inbound Homestead Market Co. day 1 block 7 door 2
- d1 PO IG1434 Bulk Basin Foods: 4 plt $2,758
- d1 placed parked Inbound Bulk Basin Foods day 1 block 8 door 1
- d1 PO IG1435 Fairweather Trading Co.: 14 plt $8,066
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 8 door 2
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 8 door 2
- d2 RUSH PO IG2436 Rush Courier: 4 plt $12,120
- d2 placed parked Inbound Rush Courier day 2 block 1 door 1
- d2 PO IG2437 Blue Ridge Provisions: 2 plt $2,760
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 1 door 2
- d2 PO IG2438 Millbrook Family Foods: 3 plt $2,700
- d2 placed parked Inbound Millbrook Family Foods day 2 block 2 door 1
- d2 PO IG2439 Last Call Liquidators: 1 plt $624
- d2 placed parked Inbound Last Call Liquidators day 2 block 2 door 2
- d2 PO IG2440 Backdoor Bill's Overstock: 3 plt $864
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 3 door 1
- d2 PO IG2441 Cornerstone Staples: 5 plt $2,919
- d2 placed parked Inbound Cornerstone Staples day 2 block 3 door 2
- d2 PO IG2442 Backdoor Bill's Overstock: 1 plt $288
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 4 door 1
- d2 signed bulk BULK_Mama Tomato Sauces_D2_0
- d2 booked Mama Tomato Sauces day 2 block 9 door 1
- d2 released 1 order(s) (waiting for lane)
- d2 released 1 order(s)
- d3 booked Crunch-Time Cereals day 3 block 1 door 1
- d3 booked Sprout Scout Foods day 3 block 1 door 2
- d3 RUSH PO IG3443 Rush Courier: 4 plt $12,496
- d3 placed parked Inbound Rush Courier day 3 block 2 door 1
- d3 PO IG3444 Blue Ridge Provisions: 3 plt $4,140
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 2 door 2
- d3 signed bulk BULK_Grizzly Ridge Jerky_D3_0
- d3 signed bulk BULK_Neon Nectar Juice Co._D3_1
- d3 booked Grizzly Ridge Jerky day 3 block 6 door 1
- d3 booked Neon Nectar Juice Co. day 3 block 6 door 2
- d3 released 2 order(s) (waiting for lane)
- d3 released 1 order(s)
- d4 booked Sneaky Pete's Seafood day 4 block 1 door 1
- d4 booked Mama Tomato Sauces day 4 block 1 door 2
- d4 RUSH PO IG4445 Rush Courier: 4 plt $11,600
- d4 placed parked Inbound Rush Courier day 4 block 2 door 1
- d4 signed bulk BULK_Crunch-Time Cereals_D4_0
- d4 booked Crunch-Time Cereals day 4 block 9 door 1
- d4 released 1 order(s) (waiting for lane)
- d4 released 1 order(s)
- d5 booked Crunch-Time Cereals day 5 block 1 door 1
- d5 booked Sneaky Pete's Seafood day 5 block 1 door 2
- d5 booked Sprout Scout Foods day 5 block 2 door 1
- d5 booked Grizzly Ridge Jerky day 5 block 2 door 2
- d5 booked Neon Nectar Juice Co. day 5 block 3 door 1
- d5 RUSH PO IG5446 Rush Courier: 2 plt $5,344
- d5 placed parked Inbound Rush Courier day 5 block 3 door 2
- d5 PO IG5447 Iron Gate Staples: 4 plt $608
- d5 placed parked Inbound Iron Gate Staples day 5 block 4 door 1
- d5 signed bulk BULK_Sneaky Pete's Seafood_D5_0
- d5 signed bulk BULK_Sprout Scout Foods_D5_1
- d5 booked Sneaky Pete's Seafood day 5 block 6 door 1
- d5 booked Sprout Scout Foods day 5 block 6 door 2
- d5 released 2 order(s) (waiting for lane)
- d6 booked Sneaky Pete's Seafood day 6 block 1 door 1
- d6 booked Sneaky Pete's Seafood day 6 block 1 door 2
- d6 booked Mama Tomato Sauces day 6 block 2 door 1
- d6 booked Crunch-Time Cereals day 6 block 2 door 2
- d6 released 1 order(s) (waiting for lane)
- d6 PO IG6448 Homestead Market Co.: 1 plt $624
- d6 placed parked Inbound Homestead Market Co. day 6 block 3 door 1
- d6 PO IG6449 Steady Supply Grocers: 1 plt $200
- d6 placed parked Inbound Steady Supply Grocers day 6 block 3 door 2
- d6 released 1 order(s) (waiting for lane)
- d7 booked Crunch-Time Cereals day 7 block 1 door 1
- d7 booked Sneaky Pete's Seafood day 7 block 1 door 2
- d7 booked Sneaky Pete's Seafood day 7 block 2 door 1
- d7 booked Sprout Scout Foods day 7 block 2 door 2
- d7 booked Grizzly Ridge Jerky day 7 block 3 door 1
- d7 booked Neon Nectar Juice Co. day 7 block 3 door 2
- d7 booked Sneaky Pete's Seafood day 7 block 4 door 1
- d7 booked Sprout Scout Foods day 7 block 4 door 2
- d7 released 1 order(s) (waiting for lane)
