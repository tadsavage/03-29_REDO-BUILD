# Playtest block 5 — days 1–8

## Score: **35.7 (+20.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (5 shipped, 13 late) | 15% | 27.8 (+27.8) |
| Fill rate | 10% | 58.4 (+58.4) |
| Throughput (51 plt vs target 84) | 15% | 60.7 (+60.7) |
| Revenue ($17,319) | 15% | 30.9 (+30.9) |
| Profit (cash -125,545) | 10% | 0.0 (0.0) |
| Cash health (floor $10,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 17.7 (-11.2) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **5**, late/fined **13**, pallets shipped **51**. Cash $139,038 → $13,493 (-125,545).
Bot did: 10 contracts signed, 46 dock bookings, 17 orders released, 18 POs, 7 rush POs.

## Change since last block
- Fixed / no longer seen: **4**
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Martha Miller' as its operator though nobody is on it or running it - clearing the stale link.
  - REPEATING|x# and counting: [TrailerLoad] # order(s) at #A had no staged pallets to load — reverted to Staged.
  - REPEATING|x# and counting: [OrderService] Order #-f#a##bb### (Blocky Bakeshop) was released to loading from #A but no staged pallet for it could be found there — reverted to Staged so it can be re-released. Its pallets are most likely sitting somewhere other than the lane.
  - REPEATING|x# and counting: [TrailerLoad] #A: pallet at pos# belongs to order ##dc##b##b#-#, not one being loaded here — stopping before it.
- New this block: **14**
  - NO-TASK| lane pallet #C# sku=# load=#
  - ORPHAN-OUTBOUND| #B# (order #-cbfa#f#f#b### gone)
  - ORPHAN-OUTBOUND| #C# (order #-db##c##e### gone)
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nicholas Cartwright' as its operator though nobody is on it or running it - clearing the stale link.
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - FLOATING| OutboundPallet_#-db##c##e###_# @#C# bottom=#
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per # s)
  - WARN|[OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - WARN|[OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - ORPHAN-OUTBOUND| #B# (order ##f##a-ab### gone)
  - REPEATING|x# and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Return: no slot or pallet of # left to put a case back into - the case is written off.
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: no progress toward task target (#, #, #) for #s (stopped Infinitym short, path PathComplete) — treating as arrived.
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (20 distinct, 34 incidents)

| Incidents | Bug |
|---|---|
| 6 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) wait |
| 3 | NO-TASK —  lane pallet #B# sku=# load=# |
| 2 | NO-TASK —  lane pallet #C# sku=# load=# |
| 2 | ORPHAN-OUTBOUND —  #B# (order #-cbfa#f#f#b### gone) |
| 2 | ORPHAN-OUTBOUND —  #C# (order #-db##c##e### gone) |
| 3 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nicholas Cartwright' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | TRUCK-STALLED —  Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #) |
| 1 | FLOATING —  OutboundPallet_#-db##c##e###_# @#C# bottom=# |
| 1 | REPEATING — x# and counting: [OrderService] Order OG#: no reserve or pick-face pallet of # left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it. |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per # s) |
| 2 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 2 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | ORPHAN-OUTBOUND —  #B# (order ##f##a-ab### gone) |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Return: no slot or pallet of # left to put a case back into - the case is written off. |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: no progress toward task target (#, #, #) for #s (stopped Infinitym short, path PathComplete) — treating as arrived. |
| 1 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 1 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **REPEATING** (day 1): x25 and counting: [OrderService] Order OG1370: no reserve or pick-face pallet of 638412 left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
- **VEHICLE-STATIONARY** (day 1): PalletJack has a driver, hasn't moved in 255s while 1 task(s) wait
- **NO-TASK** (day 1): NO-TASK lane pallet 1B-3 sku=193587 load=4671372066
- **WARN** (day 1): [AiNavigation] ReachTruck: stuck at (15.49, 1.16, 33.60) — walking out to (15.49, 0.08, 42.67) (8.14m to mesh).
- **NO-TASK** (day 1): NO-TASK lane pallet 2C-3 sku=193587 load=9594508000
- **WARN** (day 1): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nicholas Cartwright' as its operator though nobody is on it or running it - clearing the stale link.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (-25.70, 1.11, 29.88) (no waypoint reachable for 8s) — warping to (3.26, 0.01, 31.09) to rejoin patrol.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-23.89, 1.11, 34.18) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **REPEATING** (day 2): x25 and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per 30 s)
- **WARN** (day 4): [OrderService] Order OG3368 closed with a STAGED pallet (35 cs) that never made the trailer - not billed, returning it to stock.
- **WARN** (day 4): [OrderService] Order OG4372 closed with a IN TRANSIT pallet (23 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 1B-5 (order f734d5b4-62f3-430a-ab98-95ee9169cf73 gone)
- **REPEATING** (day 4): x25 and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 1B-1 (order 020890c4-cbfa-4f5f-8b24-408aa8115191 gone)
- **TRUCK-STALLED** (day 4): TRUCK-STALLED Truck→Outbound_Door1 state=DepartToApproach 270s at (7.2, 0.0, 6.7)
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 1C-3 (order 2b5909a0-db55-47c4-96e4-6fa11cc928b5 gone)
- **REPEATING** (day 5): x25 and counting: [OrderSelectionTaskDriver] Return: no slot or pallet of 172495 left to put a case back into - the case is written off.
- **FLOATING** (day 5): FLOATING OutboundPallet_2b5909a0-db55-47c4-96e4-6fa11cc928b5_2 @1C-3 bottom=2.20
- **REPEATING** (day 7): x25 and counting: [AiNavigation] Dockstocker: no progress toward task target (17.49, 1.13, 39.04) for 10s (stopped Infinitym short, path PathComplete) — treating as arrived.
- **TRUCK-OVERLAP** (day 7): TRUCK-OVERLAP Truck→PO_IG7504 & Truck→PO_IG7505 at (-7.6, 0.0, -50.7)

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 released 2 order(s)
- d1 RUSH PO IG1481 Rush Courier: 4 plt $10,664
- d1 placed parked Inbound Rush Courier day 1 block 5 door 1
- d1 PO IG1482 Anchor Point Foods: 5 plt $2,944
- d1 placed parked Inbound Anchor Point Foods day 1 block 5 door 2
- d1 PO IG1483 Bulk Basin Foods: 10 plt $4,015
- d1 placed parked Inbound Bulk Basin Foods day 1 block 6 door 1
- d1 signed bulk BULK_Honest Harvest Rice_D1_0
- d1 signed bulk BULK_Puff & Stuff Pastries_D1_1
- d1 booked Puff & Stuff Pastries day 1 block 6 door 2
- d1 booked Honest Harvest Rice day 1 block 7 door 1
- d1 released 2 order(s) (waiting for lane)
- d1 PO IG1484 Bulk Basin Foods: 16 plt $2,496
- d1 placed parked Inbound Bulk Basin Foods day 1 block 7 door 2
- d1 PO IG1485 Homestead Market Co.: 5 plt $3,444
- d1 placed parked Inbound Homestead Market Co. day 1 block 8 door 1
- d1 PO IG1486 Homestead Market Co.: 1 plt $576
- d1 placed parked Inbound Homestead Market Co. day 1 block 8 door 2
- d1 PO IG1487 Anchor Point Foods: 1 plt $420
- d1 placed parked Inbound Anchor Point Foods day 1 block 9 door 1
- d1 PO IG1488 Bulk Basin Foods: 3 plt $952
- d1 placed parked Inbound Bulk Basin Foods day 1 block 9 door 2
- d1 PO IG1489 Homestead Market Co.: 1 plt $621
- d1 placed parked Inbound Homestead Market Co. day 1 block 10 door 1
- d2 RUSH PO IG2490 Rush Courier: 4 plt $10,704
- d2 placed parked Inbound Rush Courier day 2 block 1 door 1
- d2 signed bulk BULK_Blocky Bakeshop_D2_0
- d2 booked Blocky Bakeshop day 2 block 9 door 1
- d2 released 1 order(s) (waiting for lane)
- d2 PO IG2491 Homestead Market Co.: 8 plt $8,160
- d2 placed parked Inbound Homestead Market Co. day 2 block 9 door 2
- d2 PO IG2492 Homestead Market Co.: 2 plt $2,336
- d2 placed parked Inbound Homestead Market Co. day 2 block 10 door 1
- d2 released 1 order(s)
- d3 booked Puff & Stuff Pastries day 3 block 1 door 1
- d3 booked Honest Harvest Rice day 3 block 1 door 2
- d3 RUSH PO IG3493 Rush Courier: 4 plt $7,664
- d3 placed parked Inbound Rush Courier day 3 block 2 door 1
- d3 PO IG3494 Bulk Basin Foods: 3 plt $1,200
- d3 placed parked Inbound Bulk Basin Foods day 3 block 2 door 2
- d3 PO IG3495 Bulk Basin Foods: 2 plt $672
- d3 placed parked Inbound Bulk Basin Foods day 3 block 3 door 1
- d3 signed bulk BULK_Twisted Twine Pasta_D3_0
- d3 signed bulk BULK_Fizz-Pop Soda Works_D3_1
- d3 booked Twisted Twine Pasta day 3 block 6 door 1
- d3 booked Fizz-Pop Soda Works day 3 block 6 door 2
- d3 released 2 order(s) (waiting for lane)
- d3 PO IG3496 Homestead Market Co.: 10 plt $5,760
- d3 placed parked Inbound Homestead Market Co. day 3 block 7 door 1
- d3 released 1 order(s)
- d4 booked Blocky Bakeshop day 4 block 1 door 1
- d4 booked Sneaky Pete's Seafood day 4 block 1 door 2
- d4 RUSH PO IG4497 Rush Courier: 4 plt $7,136
- d4 placed parked Inbound Rush Courier day 4 block 2 door 1
- d4 PO IG4498 Homestead Market Co.: 1 plt $1,680
- d4 placed parked Inbound Homestead Market Co. day 4 block 2 door 2
- d4 PO IG4499 Bulk Basin Foods: 6 plt $4,032
- d4 placed parked Inbound Bulk Basin Foods day 4 block 3 door 1
- d4 signed bulk BULK_Caffeine Fiend Coffee_D4_0
- d4 signed bulk BULK_Bramble & Bite Berry Co._D4_1
- d4 booked Caffeine Fiend Coffee day 4 block 9 door 1
- d4 booked Bramble & Bite Berry Co. day 4 block 9 door 2
- d4 released 2 order(s) (waiting for lane)
- d4 released 1 order(s) (waiting for lane)
- d5 booked Twisted Twine Pasta day 5 block 1 door 1
- d5 booked Fizz-Pop Soda Works day 5 block 1 door 2
- d5 RUSH PO IG5500 Rush Courier: 4 plt $7,856
- d5 placed parked Inbound Rush Courier day 5 block 2 door 1
- d5 signed bulk BULK_The Daily Grind Tofu_D5_0
- d5 booked The Daily Grind Tofu day 5 block 6 door 1
- d5 released 1 order(s) (waiting for lane)
- d6 booked Blocky Bakeshop day 6 block 1 door 1
- d6 booked Caffeine Fiend Coffee day 6 block 1 door 2
- d6 booked Bramble & Bite Berry Co. day 6 block 2 door 1
- d6 released 1 order(s) (waiting for lane)
- d6 RUSH PO IG6501 Rush Courier: 2 plt $4,160
- d6 placed parked Inbound Rush Courier day 6 block 2 door 2
- d6 PO IG6502 Anchor Point Foods: 1 plt $448
- d6 placed parked Inbound Anchor Point Foods day 6 block 3 door 1
- d6 signed bulk BULK_Grizzly Ridge Jerky_D6_0
- d6 booked Grizzly Ridge Jerky day 6 block 9 door 1
- d6 released 2 order(s) (waiting for lane)
- d7 booked Twisted Twine Pasta day 7 block 1 door 1
- d7 booked Fizz-Pop Soda Works day 7 block 1 door 2
- d7 booked The Daily Grind Tofu day 7 block 2 door 1
- d7 RUSH PO IG7503 Rush Courier: 1 plt $960
- d7 placed parked Inbound Rush Courier day 7 block 2 door 2
- d7 PO IG7504 Homestead Market Co.: 1 plt $480
- d7 placed parked Inbound Homestead Market Co. day 7 block 3 door 1
- d7 PO IG7505 Millbrook Family Foods: 2 plt $840
- d7 placed parked Inbound Millbrook Family Foods day 7 block 3 door 2
- d7 released 1 order(s) (waiting for lane)
