# Playtest block 4 — days 1–8

## Score: **14.7 (-8.5)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 8 late) | 15% | 0.0 (0.0) |
| Fill rate | 10% | 0.0 (0.0) |
| Throughput (0 plt vs target 84) | 15% | 0.0 (0.0) |
| Revenue ($0) | 15% | 0.0 (0.0) |
| Profit (cash -127,013) | 10% | 0.0 (0.0) |
| Cash health (floor $10,000) | 5% | 100.0 (+18.9) |
| Reliability (bugs) | 25% | 28.9 (-37.8) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **0**, late/fined **8**, pallets shipped **0**. Cash $139,038 → $12,025 (-127,013).
Bot did: 3 contracts signed, 35 dock bookings, 10 orders released, 15 POs, 6 rush POs.

## Change since last block
- Fixed / no longer seen: **3**
  - TRUCK-OVERLAP| Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #)
  - WARN|[AiNavigation] Employee_Douglas Stewart: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Donna Ortiz: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **10**
  - VEHICLE-STATIONARY|PalletJack has a driver, hasn't moved in #s while # task(s) wait
  - NO-TASK| lane pallet #B# sku=# load=#
  - REPEATING|x# and counting: [OrderService] Order OG#: no reserve or pick-face pallet of # left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Martha Miller' as its operator though nobody is on it or running it - clearing the stale link.
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - TRUCK-OVERLAP| Truck→PO_IG# & Truck→PO_IG# at (#, #, #)
  - REPEATING|x# and counting: [TrailerLoad] # order(s) at #A had no staged pallets to load — reverted to Staged.
  - REPEATING|x# and counting: [OrderService] Order #-f#a##bb### (Blocky Bakeshop) was released to loading from #A but no staged pallet for it could be found there — reverted to Staged so it can be re-released. Its pallets are most likely sitting somewhere other than the lane.
  - REPEATING|x# and counting: [TrailerLoad] #A: pallet at pos# belongs to order ##dc##b##b#-#, not one being loaded here — stopping before it.

## Bugs found (10 distinct, 21 incidents)

| Incidents | Bug |
|---|---|
| 9 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) wait |
| 3 | NO-TASK —  lane pallet #B# sku=# load=# |
| 1 | REPEATING — x# and counting: [OrderService] Order OG#: no reserve or pick-face pallet of # left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it. |
| 2 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Martha Miller' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 1 | REPEATING — x# and counting: [TrailerLoad] # order(s) at #A had no staged pallets to load — reverted to Staged. |
| 1 | REPEATING — x# and counting: [OrderService] Order #-f#a##bb### (Blocky Bakeshop) was released to loading from #A but no staged pallet for it could be found there — reverted to Staged so it can be re-released. Its pallets are most likely sitting somewhere other than the lane. |
| 1 | REPEATING — x# and counting: [TrailerLoad] #A: pallet at pos# belongs to order ##dc##b##b#-#, not one being loaded here — stopping before it. |

## First sightings (with stack traces)

- **REPEATING** (day 1): x25 and counting: [OrderService] Order OG3356: no reserve or pick-face pallet of 276945 left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
- **VEHICLE-STATIONARY** (day 1): PalletJack has a driver, hasn't moved in 255s while 1 task(s) wait
- **WARN** (day 1): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Martha Miller' as its operator though nobody is on it or running it - clearing the stale link.
- **NO-TASK** (day 1): NO-TASK lane pallet 2B-4 sku=903561 load=7990160229
- **REPEATING** (day 2): x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (4.23, 1.11, 37.91) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (10.30, 1.11, 32.27) (no waypoint reachable for 8s) — warping to (11.21, 0.01, 39.04) to rejoin patrol.
- **TRUCK-OVERLAP** (day 3): TRUCK-OVERLAP Truck→PO_IG3463 & Truck→PO_IG3462 at (-7.6, 0.0, -51.9)
- **REPEATING** (day 6): x25 and counting: [TrailerLoad] 1 order(s) at 2A had no staged pallets to load — reverted to Staged.
- **REPEATING** (day 7): x25 and counting: [OrderService] Order e05ac114-f1a9-41bb-8216-0d81a58310fd (Blocky Bakeshop) was released to loading from 2A but no staged pallet for it could be found there — reverted to Staged so it can be re-released. Its pallets are most likely sitting somewhere other than the lane.
- **REPEATING** (day 7): x25 and counting: [TrailerLoad] 2A: pallet at pos1 belongs to order 74766301-3dc7-4b93-9b33-f1ab0fdb4b0f, not one being loaded here — stopping before it.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 released 2 order(s)
- d1 RUSH PO IG1450 Rush Courier: 4 plt $10,488
- d1 placed parked Inbound Rush Courier day 1 block 5 door 1
- d1 PO IG1451 Anchor Point Foods: 4 plt $2,854
- d1 placed parked Inbound Anchor Point Foods day 1 block 5 door 2
- d1 PO IG1452 Homestead Market Co.: 2 plt $1,621
- d1 placed parked Inbound Homestead Market Co. day 1 block 6 door 1
- d1 PO IG1453 Bulk Basin Foods: 4 plt $2,361
- d1 signed bulk BULK_Neon Nectar Juice Co._D1_0
- d1 signed bulk BULK_Blocky Bakeshop_D1_1
- d1 booked Blocky Bakeshop day 1 block 6 door 2
- d1 booked Neon Nectar Juice Co. day 1 block 7 door 1
- d1 placed parked Inbound Bulk Basin Foods day 1 block 7 door 2
- d1 released 2 order(s) (waiting for lane)
- d1 PO IG1454 Blue Ridge Provisions: 7 plt $6,315
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 8 door 1
- d1 PO IG1455 Homestead Market Co.: 1 plt $640
- d1 placed parked Inbound Homestead Market Co. day 1 block 8 door 2
- d1 PO IG1456 Anchor Point Foods: 9 plt $6,336
- d1 placed parked Inbound Anchor Point Foods day 1 block 9 door 1
- d2 RUSH PO IG2457 Rush Courier: 4 plt $14,922
- d2 placed parked Inbound Rush Courier day 2 block 1 door 1
- d2 PO IG2458 Cornerstone Staples: 4 plt $2,430
- d2 placed parked Inbound Cornerstone Staples day 2 block 1 door 2
- d2 PO IG2459 Anchor Point Foods: 3 plt $1,320
- d2 placed parked Inbound Anchor Point Foods day 2 block 2 door 1
- d2 PO IG2460 Cornerstone Staples: 1 plt $640
- d2 placed parked Inbound Cornerstone Staples day 2 block 2 door 2
- d2 PO IG2461 Anchor Point Foods: 2 plt $1,080
- d2 placed parked Inbound Anchor Point Foods day 2 block 3 door 1
- d2 released 1 order(s)
- d3 booked Blocky Bakeshop day 3 block 1 door 1
- d3 booked Neon Nectar Juice Co. day 3 block 1 door 2
- d3 RUSH PO IG3462 Rush Courier: 4 plt $11,380
- d3 placed parked Inbound Rush Courier day 3 block 2 door 1
- d3 PO IG3463 Homestead Market Co.: 1 plt $567
- d3 placed parked Inbound Homestead Market Co. day 3 block 2 door 2
- d3 PO IG3464 Bulk Basin Foods: 1 plt $720
- d3 placed parked Inbound Bulk Basin Foods day 3 block 3 door 1
- d3 PO IG3465 Iron Gate Staples: 3 plt $1,916
- d3 placed parked Inbound Iron Gate Staples day 3 block 3 door 2
- d3 released 1 order(s)
- d4 booked Sneaky Pete's Seafood day 4 block 1 door 1
- d4 RUSH PO IG4466 Rush Courier: 4 plt $12,100
- d4 placed parked Inbound Rush Courier day 4 block 1 door 2
- d4 released 1 order(s)
- d5 booked Blocky Bakeshop day 5 block 1 door 1
- d5 booked Neon Nectar Juice Co. day 5 block 1 door 2
- d5 booked Sneaky Pete's Seafood day 5 block 2 door 1
- d5 RUSH PO IG5467 Rush Courier: 1 plt $4,320
- d5 placed parked Inbound Rush Courier day 5 block 2 door 2
- d5 PO IG5468 Millbrook Family Foods: 5 plt $2,340
- d5 placed parked Inbound Millbrook Family Foods day 5 block 3 door 1
- d6 booked Sneaky Pete's Seafood day 6 block 1 door 1
- d6 booked Sneaky Pete's Seafood day 6 block 1 door 2
- d6 released 1 order(s)
- d6 RUSH PO IG6469 Rush Courier: 1 plt $4,480
- d6 placed parked Inbound Rush Courier day 6 block 2 door 1
- d6 released 1 order(s) (waiting for lane)
- d7 booked Blocky Bakeshop day 7 block 1 door 1
- d7 booked Neon Nectar Juice Co. day 7 block 1 door 2
- d7 booked Sneaky Pete's Seafood day 7 block 2 door 1
- d7 booked Sneaky Pete's Seafood day 7 block 2 door 2
- d7 PO IG7470 Anchor Point Foods: 4 plt $1,792
- d7 placed parked Inbound Anchor Point Foods day 7 block 3 door 1
- d7 released 1 order(s) (waiting for lane)
