# Playtest block 10 — days 1–8

## Score: **27.0 (+14.2)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (1 shipped, 13 late) | 15% | 7.1 (+7.1) |
| Fill rate | 10% | 85.7 (+85.7) |
| Throughput (6 plt vs target 84) | 15% | 7.1 (+7.1) |
| Revenue ($4,410) | 15% | 7.9 (+7.9) |
| Profit (cash -131,218) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 84.9 (-15.0) |
| Reliability (bugs) | 25% | 33.5 (+12.3) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **1**, late/fined **13**, pallets shipped **6**. Cash $139,039 → $7,821 (-131,218).
Bot did: 9 contracts signed, 46 dock bookings, 13 orders released, 32 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **5**
  - VEHICLE-STATIONARY|Dockstocker has a driver, hasn't moved in #s while # task(s) wait
  - VEHICLE-STATIONARY|ReachTruck has a driver, hasn't moved in #s while # task(s) wait
  - TRUCK-OVERLAP| Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #)
  - REPEATING|x# and counting: [OrderService] Order OG#: no reserve or pick-face pallet of # left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
  - REPEATING|x# and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.
- New this block: **6**
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per # s)
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Ann Jones' as its operator though nobody is on it or running it - clearing the stale link.
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [DockSchedule] Door #'s #:#–#:# slot on day # elapsed for PO IG# (status=not found) — door freed for rebooking, kept on the grid (IsComplete decides finished vs missed).
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Brandon Bennett: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered).

## Bugs found (8 distinct, 29 incidents)

| Incidents | Bug |
|---|---|
| 21 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) wait |
| 1 | REPEATING — x# and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per # s) |
| 2 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Ann Jones' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [DockSchedule] Door #'s #:#–#:# slot on day # elapsed for PO IG# (status=not found) — door freed for rebooking, kept on the grid (IsComplete decides finished vs missed). |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Brandon Bennett: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered). |
| 1 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] ReachTruck: stuck at (5.66, 1.16, 22.99) — walking out to (-0.84, 0.08, 11.73) (12.05m to mesh).
- **VEHICLE-STATIONARY** (day 1): PalletJack has a driver, hasn't moved in 255s while 4 task(s) wait
- **REPEATING** (day 2): x25 and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per 30 s)
- **WARN** (day 3): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Ann Jones' as its operator though nobody is on it or running it - clearing the stale link.
- **REPEATING** (day 4): x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (-22.21, 1.11, 36.10) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **REPEATING** (day 4): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-26.08, 1.11, 31.13) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **REPEATING** (day 5): x25 and counting: [DockSchedule] Door 1's 08:00–10:00 slot on day 5 elapsed for PO IG5259 (status=not found) — door freed for rebooking, kept on the grid (IsComplete decides finished vs missed).
- **REPEATING** (day 6): x25 and counting: [ReceivingTaskDriver] Employee_Brandon Bennett: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered).

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1232 Coastal Cargo Exchange: 5 plt $6,512
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 5 door 1
- d1 PO IG1233 Homestead Market Co.: 2 plt $2,632
- d1 placed parked Inbound Homestead Market Co. day 1 block 5 door 2
- d1 PO IG1234 Blue Ridge Provisions: 4 plt $3,596
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 6 door 1
- d1 PO IG1235 Anchor Point Foods: 4 plt $3,304
- d1 signed bulk BULK_Mama Tomato Sauces_D1_0
- d1 signed bulk BULK_The Happy Herd Co._D1_1
- d1 placed parked Inbound Anchor Point Foods day 1 block 6 door 2
- d1 PO IG1236 Bulk Basin Foods: 8 plt $5,540
- d1 placed parked Inbound Bulk Basin Foods day 1 block 7 door 1
- d1 booked Mama Tomato Sauces day 1 block 11 door 1 (pallet-only: stock ready hour 19 + 1.5h of work)
- d1 PO IG1237 Homestead Market Co.: 2 plt $1,216
- d1 placed parked Inbound Homestead Market Co. day 1 block 7 door 2
- d1 PO IG1238 Golden Grove Gourmet: 5 plt $7,175
- d1 placed parked Inbound Golden Grove Gourmet day 1 block 8 door 1
- d1 released 1 order(s)
- d2 PO IG2239 Bulk Basin Foods: 1 plt $576
- d2 placed parked Inbound Bulk Basin Foods day 2 block 1 door 1
- d2 PO IG2240 Homestead Market Co.: 1 plt $608
- d2 placed parked Inbound Homestead Market Co. day 2 block 1 door 2
- d2 PO IG2241 Bulk Basin Foods: 1 plt $700
- d2 placed parked Inbound Bulk Basin Foods day 2 block 2 door 1
- d2 PO IG2242 Homestead Market Co.: 1 plt $576
- d2 placed parked Inbound Homestead Market Co. day 2 block 2 door 2
- d2 released 1 order(s)
- d2 signed bulk BULK_Blocky Bakeshop_D2_0
- d2 signed bulk BULK_Sneaky Pete's Seafood_D2_1
- d2 PO IG2243 Homestead Market Co.: 8 plt $3,888
- d2 placed parked Inbound Homestead Market Co. day 2 block 9 door 1
- d2 booked Blocky Bakeshop day 3 block 2 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 47 + 3.3h of work)
- d2 PO IG2244 Homestead Market Co.: 1 plt $459
- d2 placed parked Inbound Homestead Market Co. day 2 block 9 door 2
- d2 PO IG2245 Coastal Cargo Exchange: 5 plt $5,896
- d2 placed parked Inbound Coastal Cargo Exchange day 2 block 10 door 1
- d2 booked Sneaky Pete's Seafood day 3 block 3 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 49 + 3.3h of work)
- d2 PO IG2246 Homestead Market Co.: 2 plt $1,040
- d2 placed parked Inbound Homestead Market Co. day 2 block 10 door 2
- d2 PO IG2247 Blue Ridge Provisions: 3 plt $5,441
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 11 door 1
- d2 PO IG2248 Bulk Basin Foods: 4 plt $2,256
- d2 placed parked Inbound Bulk Basin Foods day 2 block 11 door 2
- d3 PO IG3249 Bulk Basin Foods: 2 plt $1,381
- d3 placed parked Inbound Bulk Basin Foods day 3 block 1 door 1
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 signed bulk BULK_Chubby Chipmunk Cookies_D3_0
- d3 PO IG3250 Fairweather Trading Co.: 10 plt $6,382
- d3 placed parked Inbound Fairweather Trading Co. day 3 block 6 door 1
- d3 booked The Happy Herd Co. day 3 block 11 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 65 + 3.3h of work)
- d3 booked Chubby Chipmunk Cookies day 3 block 11 door 2 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 65 + 3.3h of work)
- d3 released 1 order(s)
- d3 PO IG3251 Bulk Basin Foods: 6 plt $1,440
- d3 PO IG3252 Backdoor Bill's Overstock: 2 plt $672
- d3 PO IG3253 Homestead Market Co.: 1 plt $480
- d3 placed parked Inbound Bulk Basin Foods day 3 block 9 door 1
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 9 door 2
- d3 placed parked Inbound Homestead Market Co. day 3 block 10 door 1
- d3 released 2 order(s) (waiting for lane)
- d3 PO IG3254 Bulk Basin Foods: 1 plt $544
- d3 placed parked Inbound Bulk Basin Foods day 3 block 10 door 2
- d4 released 1 order(s)
- d4 signed bulk BULK_Twisted Twine Pasta_D4_0
- d4 signed bulk BULK_Sneaky Pete's Seafood_D4_1
- d4 PO IG4255 Blue Ridge Provisions: 10 plt $9,120
- d4 PO IG4256 Bulk Basin Foods: 7 plt $5,390
- d4 placed parked Inbound Blue Ridge Provisions day 4 block 9 door 1
- d4 placed parked Inbound Bulk Basin Foods day 4 block 9 door 2
- d4 booked Sneaky Pete's Seafood day 5 block 1 door 1 (pallet-only: stock ready hour 95 + 1.5h of work)
- d4 released 1 order(s) (waiting for lane)
- d5 booked Blocky Bakeshop day 5 block 1 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked Sneaky Pete's Seafood day 5 block 2 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 2 door 2 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 96 + 3.3h of work)
- d5 PO IG5257 Anchor Point Foods: 11 plt $7,620
- d5 PO IG5258 Bulk Basin Foods: 3 plt $1,055
- d5 PO IG5259 Homestead Market Co.: 3 plt $1,328
- d5 placed parked Inbound Anchor Point Foods day 5 block 3 door 1
- d5 placed parked Inbound Bulk Basin Foods day 5 block 3 door 2
- d5 placed parked Inbound Homestead Market Co. day 5 block 4 door 1
- d5 booked Twisted Twine Pasta day 5 block 8 door 2 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 107 + 3.3h of work)
- d5 signed bulk BULK_Cluck & Cover Poultry_D5_0
- d5 released 2 order(s) (waiting for lane)
- d6 PO IG6260 Backdoor Bill's Overstock: 2 plt $576
- d6 PO IG6261 Anchor Point Foods: 1 plt $420
- d6 PO IG6262 Bulk Basin Foods: 2 plt $1,136
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 1 door 1
- d6 placed parked Inbound Anchor Point Foods day 6 block 1 door 2
- d6 placed parked Inbound Bulk Basin Foods day 6 block 2 door 1
- d6 released 1 order(s) (waiting for lane)
- d6 PO IG6263 Anchor Point Foods: 1 plt $720
- d6 placed parked Inbound Anchor Point Foods day 6 block 9 door 1
- d7 booked Blocky Bakeshop day 7 block 1 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 7 block 1 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 7 block 2 door 1 (pallet-only: stock ready hour 144 + 1.5h of work)
- d7 booked Twisted Twine Pasta day 7 block 2 door 2 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 144 + 3.3h of work)
- d7 released 1 order(s) (waiting for lane)
