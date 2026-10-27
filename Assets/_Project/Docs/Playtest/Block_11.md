# Playtest block 11 — days 1–8

## Score: **51.9 (+24.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (8 shipped, 12 late) | 15% | 7.7 (+0.5) |
| Fill rate | 10% | 71.7 (-14.0) |
| Throughput (87 plt vs target 84) | 15% | 100.0 (+92.9) |
| Revenue ($55,573) | 15% | 99.2 (+91.4) |
| Profit (cash + stock -41,485) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (+15.1) |
| Reliability (bugs) | 25% | 24.7 (-8.8) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **8**, late/fined **12**, pallets shipped **87**. Cash $139,040 → $40,908 (-98,132). Stock at cost $0 → $56,647; cash + stock -41,485.
Bot did: 10 contracts signed, 29 dock bookings, 9 orders released, 26 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **4**
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Ann Jones' as its operator though nobody is on it or running it - clearing the stale link.
  - REPEATING|x# and counting: [DockSchedule] Door #'s #:#–#:# slot on day # elapsed for PO IG# (status=not found) — door freed for rebooking, kept on the grid (IsComplete decides finished vs missed).
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Brandon Bennett: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered).
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **8**
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop.
  - WARN|[OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - ORPHAN-OUTBOUND| #A# (order #-bb##cbd#b#-# gone)
  - REPEATING|x# and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Brian Jackson: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered).
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Joshua Myers' as its operator though nobody is on it or running it - clearing the stale link.
  - WARN|[AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (12 distinct, 20 incidents)

| Incidents | Bug |
|---|---|
| 6 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) wait |
| 3 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop. |
| 1 | REPEATING — x# and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per # s) |
| 2 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | ORPHAN-OUTBOUND —  #A# (order #-bb##cbd#b#-# gone) |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Brian Jackson: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered). |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Joshua Myers' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | WARN — [AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **VEHICLE-STATIONARY** (day 1): PalletJack has a driver, hasn't moved in 690s while 1 task(s) wait
- **WARN** (day 2): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Joshua Myers' as its operator though nobody is on it or running it - clearing the stale link.
- **REPEATING** (day 3): x25 and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per 30 s)
- **WARN** (day 3): [OrderService] Order OG2384 closed with a STAGED pallet (7 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 3): ORPHAN-OUTBOUND 1A-1 (order dac418a5-bb78-4cbd-95b1-b69a9a367537 gone)
- **REPEATING** (day 4): x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (-28.17, 1.11, 36.25) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **REPEATING** (day 4): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-11.93, 1.11, 32.32) (no waypoint reachable for 8s) — warping to (3.26, 0.01, 31.09) to rejoin patrol.
- **DOOR-DEADLOCK** (day 5): OG4388 is Loading but door 1's truck has waited 90 min with no one loading it.
- **WARN** (day 5): [AiNavigation] PalletJack: stuck at (18.73, 1.11, 32.89) — walking out to (27.73, 0.09, 32.89) (8.07m to mesh).
- **ERROR** (day 5): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-02' (5.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__92:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1125)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 6): x25 and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.
- **REPEATING** (day 7): x25 and counting: [ReceivingTaskDriver] Employee_Brian Jackson: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered).

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1506 Anchor Point Foods: 8 plt $5,660
- d1 placed parked Inbound Anchor Point Foods day 1 block 5 door 1
- d1 PO IG1507 Backdoor Bill's Overstock: 3 plt $1,716
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 5 door 2
- d1 PO IG1508 Blue Ridge Provisions: 3 plt $5,721
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 6 door 1
- d1 signed bulk BULK_Fizz-Pop Soda Works_D1_0
- d1 PO IG1509 Anchor Point Foods: 1 plt $672
- d1 placed parked Inbound Anchor Point Foods day 1 block 10 door 1
- d1 PO IG1510 Bulk Basin Foods: 7 plt $2,596
- d1 placed parked Inbound Bulk Basin Foods day 1 block 11 door 1
- d1 PO IG1511 Blue Ridge Provisions: 3 plt $3,780
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 11 door 2
- d2 PO IG2512 Coastal Cargo Exchange: 5 plt $7,180
- d2 placed parked Inbound Coastal Cargo Exchange day 2 block 1 door 1
- d2 PO IG2513 Fairweather Trading Co.: 14 plt $7,528
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 2 door 1
- d2 PO IG2514 Homestead Market Co.: 2 plt $2,239
- d2 placed parked Inbound Homestead Market Co. day 2 block 2 door 2
- d2 released 1 order(s)
- d2 signed bulk BULK_Neon Nectar Juice Co._D2_0
- d2 PO IG2515 Backdoor Bill's Overstock: 6 plt $1,632
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 9 door 1
- d2 PO IG2516 Anchor Point Foods: 3 plt $1,852
- d2 PO IG2517 Bulk Basin Foods: 2 plt $808
- d2 placed parked Inbound Anchor Point Foods day 2 block 9 door 2
- d2 placed parked Inbound Bulk Basin Foods day 2 block 10 door 1
- d3 PO IG3518 Homestead Market Co.: 1 plt $1,040
- d3 placed parked Inbound Homestead Market Co. day 3 block 1 door 1
- d3 PO IG3519 Golden Grove Gourmet: 9 plt $18,810
- d3 placed parked Inbound Golden Grove Gourmet day 3 block 2 door 1
- d3 signed bulk BULK_Blocky Bakeshop_D3_0
- d3 signed bulk BULK_Crunch-Time Cereals_D3_1
- d3 released 1 order(s)
- d4 PO IG4520 Anchor Point Foods: 1 plt $392
- d4 placed parked Inbound Anchor Point Foods day 4 block 1 door 1
- d4 PO IG4521 Bulk Basin Foods: 2 plt $1,255
- d4 placed parked Inbound Bulk Basin Foods day 4 block 1 door 2
- d4 PO IG4522 Fairweather Trading Co.: 13 plt $9,112
- d4 placed parked Inbound Fairweather Trading Co. day 4 block 2 door 1
- d4 PO IG4523 Bulk Basin Foods: 3 plt $1,560
- d4 placed parked Inbound Bulk Basin Foods day 4 block 2 door 2
- d4 released 1 order(s)
- d4 signed bulk BULK_The Cracker Barrel Caravan_D4_0
- d4 signed bulk BULK_The Cracker Barrel Caravan_D4_1
- d4 booked The Cracker Barrel Caravan day 4 block 9 door 1 (pallet-only: stock ready hour 88 + 1.5h of work)
- d5 PO IG5524 Fairweather Trading Co.: 16 plt $9,980
- d5 PO IG5525 Anchor Point Foods: 2 plt $784
- d5 placed parked Inbound Fairweather Trading Co. day 5 block 1 door 1
- d5 placed parked Inbound Anchor Point Foods day 5 block 1 door 2
- d5 released 1 order(s)
- d5 signed bulk BULK_Neon Nectar Juice Co._D5_0
- d5 released 1 order(s)
- d6 PO IG6526 Homestead Market Co.: 4 plt $2,688
- d6 placed parked Inbound Homestead Market Co. day 6 block 1 door 1
- d6 booked Blocky Bakeshop day 6 block 5 door 1 (pallet-only: stock ready hour 131 + 1.5h of work)
- d6 PO IG6527 Fairweather Trading Co.: 12 plt $6,080
- d6 placed parked Inbound Fairweather Trading Co. day 6 block 1 door 2
- d6 booked The Cracker Barrel Caravan day 6 block 5 door 2 (pallet-only: stock ready hour 131 + 1.5h of work)
- d6 released 1 order(s)
- d6 signed bulk BULK_Barnacle Bill's Bait & Bite_D6_0
- d6 released 2 order(s)
- d6 PO IG6528 Bulk Basin Foods: 5 plt $2,264
- d6 placed parked Inbound Bulk Basin Foods day 6 block 11 door 1
- d7 PO IG7529 Anchor Point Foods: 3 plt $1,794
- d7 placed parked Inbound Anchor Point Foods day 7 block 1 door 1
- d7 PO IG7530 Anchor Point Foods: 10 plt $6,720
- d7 placed parked Inbound Anchor Point Foods day 7 block 1 door 2
- d7 PO IG7531 Backdoor Bill's Overstock: 1 plt $1,500
- d7 placed parked Inbound Backdoor Bill's Overstock day 7 block 6 door 1
- d7 signed bulk BULK_Barnacle Bill's Bait & Bite_D7_0
- d7 released 1 order(s)
