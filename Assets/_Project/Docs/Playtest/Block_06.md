# Playtest block 6 — days 1–8

## Score: **16.4 (-19.3)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 8 late) | 15% | 0.0 (-27.8) |
| Fill rate | 10% | 0.0 (-58.4) |
| Throughput (0 plt vs target 84) | 15% | 0.0 (-60.7) |
| Revenue ($0) | 15% | 0.0 (-30.9) |
| Profit (cash -123,530) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 35.5 (+17.9) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **0**, late/fined **8**, pallets shipped **0**. Cash $139,039 → $15,509 (-123,530).
Bot did: 3 contracts signed, 19 dock bookings, 8 orders released, 7 POs, 6 rush POs.

## Change since last block
- Fixed / no longer seen: **19**
  - NO-TASK| lane pallet #B# sku=# load=#
  - NO-TASK| lane pallet #C# sku=# load=#
  - ORPHAN-OUTBOUND| #B# (order #-cbfa#f#f#b### gone)
  - ORPHAN-OUTBOUND| #C# (order #-db##c##e### gone)
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nicholas Cartwright' as its operator though nobody is on it or running it - clearing the stale link.
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - FLOATING| OutboundPallet_#-db##c##e###_# @#C# bottom=#
  - REPEATING|x# and counting: [OrderService] Order OG#: no reserve or pick-face pallet of # left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per # s)
  - WARN|[OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - WARN|[OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - ORPHAN-OUTBOUND| #B# (order ##f##a-ab### gone)
  - REPEATING|x# and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.
- New this block: **3**
  - VEHICLE-STATIONARY|ReachTruck has a driver, hasn't moved in #s while # task(s) wait
  - VEHICLE-STATIONARY|Dockstocker has a driver, hasn't moved in #s while # task(s) wait
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nancy Stout' as its operator though nobody is on it or running it - clearing the stale link.

## Bugs found (4 distinct, 28 incidents)

| Incidents | Bug |
|---|---|
| 14 | VEHICLE-STATIONARY — ReachTruck has a driver, hasn't moved in #s while # task(s) wait |
| 12 | VEHICLE-STATIONARY — Dockstocker has a driver, hasn't moved in #s while # task(s) wait |
| 1 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) wait |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nancy Stout' as its operator though nobody is on it or running it - clearing the stale link. |

## First sightings (with stack traces)

- **VEHICLE-STATIONARY** (day 1): PalletJack has a driver, hasn't moved in 240s while 3 task(s) wait
- **WARN** (day 2): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nancy Stout' as its operator though nobody is on it or running it - clearing the stale link.
- **VEHICLE-STATIONARY** (day 2): ReachTruck has a driver, hasn't moved in 840s while 1 task(s) wait
- **VEHICLE-STATIONARY** (day 2): Dockstocker has a driver, hasn't moved in 960s while 1 task(s) wait

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 RUSH PO IG1121 Rush Courier: 4 plt $12,688
- d1 placed parked Inbound Rush Courier day 1 block 5 door 1
- d1 PO IG1122 Anchor Point Foods: 6 plt $4,266
- d1 placed parked Inbound Anchor Point Foods day 1 block 5 door 2
- d1 PO IG1123 Bulk Basin Foods: 4 plt $2,025
- d1 placed parked Inbound Bulk Basin Foods day 1 block 6 door 1
- d1 signed bulk BULK_Puff & Stuff Pastries_D1_0
- d1 signed bulk BULK_Fizz-Pop Soda Works_D1_1
- d1 booked Puff & Stuff Pastries day 1 block 6 door 2
- d1 booked Fizz-Pop Soda Works day 1 block 7 door 1
- d1 released 1 order(s)
- d1 PO IG1124 Homestead Market Co.: 2 plt $2,708
- d1 placed parked Inbound Homestead Market Co. day 1 block 7 door 2
- d1 released 1 order(s) (waiting for lane)
- d1 PO IG1125 Fairweather Trading Co.: 11 plt $8,785
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 8 door 1
- d2 RUSH PO IG2126 Rush Courier: 4 plt $14,946
- d2 placed parked Inbound Rush Courier day 2 block 1 door 1
- d2 PO IG2127 Salvage Row Traders: 1 plt $1,000
- d2 placed parked Inbound Salvage Row Traders day 2 block 1 door 2
- d2 PO IG2128 Halloran Household: 6 plt $3,711
- d2 placed parked Inbound Halloran Household day 2 block 2 door 1
- d2 released 1 order(s)
- d3 RUSH PO IG3129 Rush Courier: 4 plt $16,530
- d3 placed parked Inbound Rush Courier day 3 block 1 door 1
- d3 released 1 order(s)
- d4 RUSH PO IG4130 Rush Courier: 3 plt $11,700
- d4 placed parked Inbound Rush Courier day 4 block 1 door 1
- d4 released 1 order(s)
- d5 booked Sneaky Pete's Seafood day 5 block 1 door 1
- d5 RUSH PO IG5131 Rush Courier: 2 plt $7,440
- d5 placed parked Inbound Rush Courier day 5 block 1 door 2
- d5 released 1 order(s)
- d6 booked Sneaky Pete's Seafood day 6 block 1 door 1
- d6 RUSH PO IG6132 Rush Courier: 1 plt $4,400
- d6 placed parked Inbound Rush Courier day 6 block 1 door 2
- d6 released 1 order(s)
- d7 booked Sneaky Pete's Seafood day 7 block 1 door 1
- d7 booked Sneaky Pete's Seafood day 7 block 1 door 2
- d7 PO IG7133 Backdoor Bill's Overstock: 1 plt $1,600
- d7 placed parked Inbound Backdoor Bill's Overstock day 7 block 2 door 1
- d7 released 1 order(s)
