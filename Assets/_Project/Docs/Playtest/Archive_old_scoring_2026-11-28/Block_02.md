# Playtest block 2 — days 1–8

## Score: **51.2 (+41.3)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (8 shipped, 10 late) | 15% | 44.4 (+44.4) |
| Fill rate | 10% | 44.4 (+44.4) |
| Throughput (74 plt vs target 84) | 15% | 88.1 (+88.1) |
| Revenue ($46,792) | 15% | 83.6 (+83.6) |
| Profit (cash -123,445) | 10% | 0.0 (0.0) |
| Cash health (floor $10,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 27.3 (+17.9) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **8**, late/fined **10**, pallets shipped **74**. Cash $139,038 → $15,593 (-123,445).
Bot did: 8 contracts signed, 36 dock bookings, 15 orders released, 22 POs, 7 rush POs.

## Change since last block
- Fixed / no longer seen: **17**
  - ERROR|[ReachTruckOperator] 'ReachTruck' deliver: → location ### anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'ReachTruck' putaway: → lane #C exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'ReachTruck' deliver: → location ### anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'ReachTruck' putaway: → lane #B exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'ReachTruck' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'ReachTruck' pallet pick: → source lane #B exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[TrailerLoad] 'Dockstocker' dig: → #C far end: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets.
  - ERROR|[TrailerLoad] 'Dockstocker' dig: back to source lane: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets.
  - ERROR|[ReachTruckOperator] 'ReachTruck' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'ReachTruck' pallet pick: → source lane #C exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'ReachTruck' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[TrailerLoad] 'Dockstocker' dig: return to source lane: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets.
  - ERROR|[ReachTruckOperator] 'ReachTruck' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'ReachTruck' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - TRUCK-OVERLAP| Truck→PO_IG# & Truck→PO_IG# at (#, #, #)
- New this block: **11**
  - NO-TASK| lane pallet #A# sku=# load=#
  - ORPHAN-OUTBOUND| #B# (order ##f##a##c## gone)
  - FLOATING| OutboundPallet_##f##a##c##_# @#B# bottom=#
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'return pallet pickup' ((#, #, #)) after #s.
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Mark Bennett' as its operator though nobody is on it or running it - clearing the stale link.
  - WARN|[OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - WARN|[AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ORPHAN-OUTBOUND| #A# (order #-d#ed#ab#-b### gone)
  - ORPHAN-OUTBOUND| #B# (order ##e#c#df#-b### gone)
  - VEHICLE-STATIONARY|ReachTruck has a driver, hasn't moved in #s while # task(s) wait
  - WARN|[OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock.

## Bugs found (14 distinct, 19 incidents)

| Incidents | Bug |
|---|---|
| 2 | NO-TASK —  lane pallet #A# sku=# load=# |
| 2 | ORPHAN-OUTBOUND —  #B# (order ##f##a##c## gone) |
| 1 | FLOATING —  OutboundPallet_##f##a##c##_# @#B# bottom=# |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack did not reach 'return pallet pickup' ((#, #, #)) after #s. |
| 2 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Mark Bennett' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | NO-TASK —  lane pallet #B# sku=# load=# |
| 2 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) wait |
| 2 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ORPHAN-OUTBOUND —  #A# (order #-d#ed#ab#-b### gone) |
| 1 | ORPHAN-OUTBOUND —  #B# (order ##e#c#df#-b### gone) |
| 1 | VEHICLE-STATIONARY — ReachTruck has a driver, hasn't moved in #s while # task(s) wait |
| 1 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Mark Bennett' as its operator though nobody is on it or running it - clearing the stale link.
- **NO-TASK** (day 1): NO-TASK lane pallet 1B-5 sku=178439 load=4054918295
- **WARN** (day 3): [OrderService] Order OG3325 closed with a STAGED pallet (40 cs) that never made the trailer - not billed, returning it to stock.
- **WARN** (day 3): [OrderService] Order OG4326 closed with a IN TRANSIT pallet (38 cs) that never made the trailer - not billed, returning it to stock.
- **NO-TASK** (day 3): NO-TASK lane pallet 2A-5 sku=148972 load=5468522926
- **VEHICLE-STATIONARY** (day 3): PalletJack has a driver, hasn't moved in 315s while 1 task(s) wait
- **ORPHAN-OUTBOUND** (day 3): ORPHAN-OUTBOUND 1B-1 (order 1d7a2751-83f5-4a90-910c-6cf0e11dd123 gone)
- **FLOATING** (day 3): FLOATING OutboundPallet_1d7a2751-83f5-4a90-910c-6cf0e11dd123_2 @1B-1 bottom=2.35
- **WARN** (day 4): [AiNavigation] ReachTruck: stuck at (15.49, 1.16, 33.60) — walking out to (15.49, 0.08, 42.67) (8.14m to mesh).
- **WARN** (day 4): [AiNavigation] Dockstocker: stuck at (15.49, 1.16, 33.60) — walking out to (15.49, 0.08, 42.67) (8.14m to mesh).
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 1A-1 (order f3eb1dec-d7ed-4ab8-b046-0b4627b011f2 gone)
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 2B-1 (order 2d159039-1e2c-4df9-b433-7bd836a61d6e gone)
- **ERROR** (day 5): [OrderSelectionTaskDriver] Pallet jack did not reach 'return pallet pickup' ((16.51, 1.13, 16.14)) after 7.9s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__96:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1269)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 7): ReachTruck has a driver, hasn't moved in 405s while 1 task(s) wait

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 released 2 order(s)
- d1 RUSH PO IG1401 Rush Courier: 4 plt $10,708
- d1 placed parked Inbound Rush Courier day 1 block 5 door 1
- d1 PO IG1402 Anchor Point Foods: 7 plt $4,648
- d1 placed parked Inbound Anchor Point Foods day 1 block 5 door 2
- d1 PO IG1403 Blue Ridge Provisions: 5 plt $6,754
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 6 door 1
- d1 signed bulk BULK_Salty Sailor Pretzel Co._D1_0
- d1 booked Salty Sailor Pretzel Co. day 1 block 6 door 2
- d1 released 1 order(s)
- d1 PO IG1404 Blue Ridge Provisions: 5 plt $5,400
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 7 door 1
- d1 PO IG1405 Homestead Market Co.: 2 plt $2,619
- d1 placed parked Inbound Homestead Market Co. day 1 block 7 door 2
- d1 PO IG1406 Millbrook Family Foods: 1 plt $567
- d1 placed parked Inbound Millbrook Family Foods day 1 block 11 door 1
- d1 PO IG1407 Salvage Row Traders: 1 plt $880
- d2 RUSH PO IG2408 Rush Courier: 4 plt $11,020
- d2 placed parked Inbound Rush Courier day 2 block 1 door 1
- d2 PO IG2409 Bulk Basin Foods: 1 plt $520
- d2 placed parked Inbound Bulk Basin Foods day 2 block 1 door 2
- d2 PO IG2410 Millbrook Family Foods: 2 plt $1,152
- d2 placed parked Inbound Millbrook Family Foods day 2 block 2 door 1
- d2 PO IG2411 Fairweather Trading Co.: 18 plt $13,224
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 2 door 2
- d2 signed bulk BULK_Sneaky Pete's Seafood_D2_0
- d2 booked Sneaky Pete's Seafood day 2 block 9 door 1
- d2 released 1 order(s)
- d2 released 1 order(s)
- d3 RUSH PO IG3412 Rush Courier: 4 plt $11,096
- d3 placed parked Inbound Rush Courier day 3 block 1 door 1
- d3 PO IG3413 Anchor Point Foods: 2 plt $1,088
- d3 placed parked Inbound Anchor Point Foods day 3 block 1 door 2
- d3 PO IG3414 Bulk Basin Foods: 4 plt $1,800
- d3 placed parked Inbound Bulk Basin Foods day 3 block 2 door 1
- d3 PO IG3415 Anchor Point Foods: 2 plt $1,120
- d3 placed parked Inbound Anchor Point Foods day 3 block 2 door 2
- d3 PO IG3416 Bulk Basin Foods: 2 plt $256
- d3 placed parked Inbound Bulk Basin Foods day 3 block 3 door 1
- d3 PO IG3417 Backdoor Bill's Overstock: 6 plt $1,632
- d3 PO IG3418 Bulk Basin Foods: 6 plt $3,968
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 4 door 1
- d3 placed parked Inbound Bulk Basin Foods day 3 block 4 door 2
- d3 signed bulk BULK_Fizz-Pop Soda Works_D3_0
- d3 booked Fizz-Pop Soda Works day 3 block 6 door 1
- d3 released 1 order(s)
- d3 released 1 order(s)
- d4 RUSH PO IG4419 Rush Courier: 4 plt $7,534
- d4 placed parked Inbound Rush Courier day 4 block 1 door 1
- d4 PO IG4420 Blue Ridge Provisions: 10 plt $5,280
- d4 placed parked Inbound Blue Ridge Provisions day 4 block 1 door 2
- d4 PO IG4421 Anchor Point Foods: 2 plt $1,540
- d4 placed parked Inbound Anchor Point Foods day 4 block 2 door 1
- d4 PO IG4422 Blue Ridge Provisions: 3 plt $2,520
- d4 PO IG4423 Anchor Point Foods: 2 plt $784
- d4 placed parked Inbound Blue Ridge Provisions day 4 block 6 door 1
- d4 placed parked Inbound Anchor Point Foods day 4 block 6 door 2
- d4 signed bulk BULK_Happy Cow Creamery_D4_0
- d4 booked Happy Cow Creamery day 4 block 9 door 1
- d4 released 1 order(s)
- d4 released 1 order(s)
- d5 RUSH PO IG5424 Rush Courier: 4 plt $11,394
- d5 placed parked Inbound Rush Courier day 5 block 1 door 1
- d5 PO IG5425 Anchor Point Foods: 1 plt $468
- d5 placed parked Inbound Anchor Point Foods day 5 block 1 door 2
- d5 PO IG5426 Bulk Basin Foods: 2 plt $1,040
- d5 placed parked Inbound Bulk Basin Foods day 5 block 2 door 1
- d5 signed bulk BULK_The Happy Herd Co._D5_0
- d5 signed bulk BULK_The Big Cheese Inc._D5_1
- d5 booked The Big Cheese Inc. day 5 block 6 door 1
- d5 booked The Happy Herd Co. day 5 block 6 door 2
- d5 released 2 order(s) (waiting for lane)
- d6 released 1 order(s)
- d6 RUSH PO IG6427 Rush Courier: 1 plt $4,000
- d6 placed parked Inbound Rush Courier day 6 block 1 door 1
- d6 PO IG6428 Anchor Point Foods: 3 plt $1,824
- d6 placed parked Inbound Anchor Point Foods day 6 block 1 door 2
- d6 signed bulk BULK_Cluck & Cover Poultry_D6_0
- d6 booked Cluck & Cover Poultry day 6 block 9 door 1
- d6 released 2 order(s) (waiting for lane)
- d7 booked The Big Cheese Inc. day 7 block 1 door 1
- d7 RUSH PO IG7429 Rush Courier: 1 plt $4,320
- d7 placed parked Inbound Rush Courier day 7 block 1 door 2
- d7 released 1 order(s)
