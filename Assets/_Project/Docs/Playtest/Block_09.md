# Playtest block 9 — days 1–8

## Score: **12.8 (-10.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 12 late) | 15% | 0.0 (-7.1) |
| Fill rate | 10% | 0.0 (-100.0) |
| Throughput (0 plt vs target 84) | 15% | 0.0 (-2.4) |
| Revenue ($0) | 15% | 0.0 (-2.1) |
| Profit (cash -124,153) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 99.8 (+3.7) |
| Reliability (bugs) | 25% | 21.3 (+2.6) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **0**, late/fined **12**, pallets shipped **0**. Cash $139,039 → $14,886 (-124,153).
Bot did: 7 contracts signed, 54 dock bookings, 12 orders released, 33 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **8**
  - NO-TASK| lane pallet #A# sku=# load=#
  - NO-TASK| lane pallet #B# sku=# load=#
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per # s)
  - REPEATING|x# and counting: [OrderService] Order OG#: no reserve or pick-face pallet of # left to fill a pallet pick â€” filed unassigned; a Reach Truck resolves a source once one claims it.
  - NO-TASK| lane pallet #E# sku=# load=#
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Jack Miller: receive workflow cancelled (out of range) â€” releasing the task and resuming patrol.
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##b#e#d#cfc## but the employee is #m away (max #m) â€” refusing to start receiving.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Jack Miller: released Receive task for pallet ##b#e#d#cfc## back to the queue (employee strayed outside receiving range).
- New this block: **4**
  - TRUCK-OVERLAP| Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #)
  - REPEATING|x# and counting: [OrderService] Order OG#: no reserve or pick-face pallet of # left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.

## Bugs found (7 distinct, 89 incidents)

| Incidents | Bug |
|---|---|
| 33 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) wait |
| 32 | VEHICLE-STATIONARY — Dockstocker has a driver, hasn't moved in #s while # task(s) wait |
| 18 | VEHICLE-STATIONARY — ReachTruck has a driver, hasn't moved in #s while # task(s) wait |
| 2 | TRUCK-OVERLAP —  Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #) |
| 1 | REPEATING — x# and counting: [OrderService] Order OG#: no reserve or pick-face pallet of # left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it. |
| 2 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll. |

## First sightings (with stack traces)

- **VEHICLE-STATIONARY** (day 1): PalletJack has a driver, hasn't moved in 240s while 4 task(s) wait
- **REPEATING** (day 2): x25 and counting: [OrderService] Order OG2188: no reserve or pick-face pallet of 304759 left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
- **VEHICLE-STATIONARY** (day 2): Dockstocker has a driver, hasn't moved in 750s while 1 task(s) wait
- **VEHICLE-STATIONARY** (day 2): ReachTruck has a driver, hasn't moved in 1080s while 1 task(s) wait
- **WARN** (day 4): [AiNavigation] ReachTruck: stuck at (0.00, 1.11, 40.50) — walking out to (3.46, 0.08, 42.50) (3.17m to mesh).
- **REPEATING** (day 4): x25 and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.
- **TRUCK-OVERLAP** (day 5): TRUCK-OVERLAP Truck→Outbound_Door1 & Truck→Outbound_Door2 at (-7.6, 0.0, -46.4)

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1199 Homestead Market Co.: 2 plt $1,440
- d1 placed parked Inbound Homestead Market Co. day 1 block 5 door 1
- d1 PO IG1200 Fairweather Trading Co.: 10 plt $6,850
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 5 door 2
- d1 PO IG1201 Anchor Point Foods: 7 plt $4,132
- d1 placed parked Inbound Anchor Point Foods day 1 block 6 door 1
- d1 signed bulk BULK_Rooted Robotics_D1_0
- d1 signed bulk BULK_Sneaky Pete's Seafood_D1_1
- d1 booked Rooted Robotics day 1 block 6 door 2
- d1 booked Sneaky Pete's Seafood day 1 block 7 door 1
- d1 released 1 order(s)
- d1 PO IG1202 Backdoor Bill's Overstock: 3 plt $1,352
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 7 door 2
- d1 released 1 order(s)
- d1 PO IG1203 Bulk Basin Foods: 8 plt $3,297
- d1 placed parked Inbound Bulk Basin Foods day 1 block 8 door 1
- d1 PO IG1204 Homestead Market Co.: 1 plt $1,920
- d1 placed parked Inbound Homestead Market Co. day 1 block 8 door 2
- d1 PO IG1205 Fairweather Trading Co.: 12 plt $7,356
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 9 door 1
- d1 PO IG1206 Backdoor Bill's Overstock: 5 plt $1,280
- d1 PO IG1207 Bulk Basin Foods: 1 plt $120
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 9 door 2
- d1 placed parked Inbound Bulk Basin Foods day 1 block 10 door 1
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 9 door 2
- d1 placed parked Inbound Bulk Basin Foods day 1 block 10 door 1
- d2 PO IG2208 Cornerstone Staples: 11 plt $5,641
- d2 placed parked Inbound Cornerstone Staples day 2 block 1 door 1
- d2 PO IG2209 Millbrook Family Foods: 1 plt $448
- d2 placed parked Inbound Millbrook Family Foods day 2 block 1 door 2
- d2 PO IG2210 Last Call Liquidators: 2 plt $1,536
- d2 placed parked Inbound Last Call Liquidators day 2 block 2 door 1
- d2 PO IG2211 Halloran Household: 4 plt $1,600
- d2 placed parked Inbound Halloran Household day 2 block 2 door 2
- d2 PO IG2212 Halloran Household: 1 plt $608
- d2 placed parked Inbound Halloran Household day 2 block 3 door 1
- d2 PO IG2213 Halloran Household: 12 plt $7,540
- d2 placed parked Inbound Halloran Household day 2 block 3 door 2
- d2 PO IG2214 Halloran Household: 4 plt $4,464
- d2 placed parked Inbound Halloran Household day 2 block 4 door 1
- d2 placed parked Inbound Halloran Household day 2 block 4 door 2
- d2 placed parked Inbound Halloran Household day 2 block 4 door 1
- d2 released 1 order(s)
- d3 PO IG3215 Riverside Housewares: 3 plt $1,600
- d3 placed parked Inbound Riverside Housewares day 3 block 1 door 1
- d3 PO IG3216 Iron Gate Staples: 3 plt $736
- d3 placed parked Inbound Iron Gate Staples day 3 block 1 door 2
- d3 PO IG3217 Meridian Provisions: 2 plt $768
- d3 placed parked Inbound Meridian Provisions day 3 block 2 door 1
- d3 PO IG3218 Bulk Basin Foods: 1 plt $768
- d3 placed parked Inbound Bulk Basin Foods day 3 block 2 door 2
- d3 PO IG3219 Blue Ridge Provisions: 11 plt $7,344
- d3 PO IG3220 Meridian Provisions: 4 plt $1,728
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 3 door 1
- d3 placed parked Inbound Meridian Provisions day 3 block 3 door 2
- d3 PO IG3221 Riverside Housewares: 3 plt $2,208
- d3 placed parked Inbound Riverside Housewares day 3 block 4 door 1
- d3 placed parked Inbound Meridian Provisions day 3 block 3 door 2
- d3 placed parked Inbound Riverside Housewares day 3 block 4 door 1
- d3 signed bulk BULK_Crunch-Time Cereals_D3_0
- d3 signed bulk BULK_Sneaky Pete's Seafood_D3_1
- d3 booked Crunch-Time Cereals day 3 block 6 door 1
- d3 booked Sneaky Pete's Seafood day 3 block 6 door 2
- d3 released 2 order(s)
- d3 released 1 order(s)
- d4 PO IG4222 Anchor Point Foods: 2 plt $1,296
- d4 placed parked Inbound Anchor Point Foods day 4 block 1 door 1
- d4 PO IG4223 Steady Supply Grocers: 3 plt $752
- d4 placed parked Inbound Steady Supply Grocers day 4 block 1 door 2
- d4 PO IG4224 Salvage Row Traders: 1 plt $480
- d4 placed parked Inbound Salvage Row Traders day 4 block 2 door 1
- d4 PO IG4225 Pantry Row Distributors: 2 plt $768
- d4 placed parked Inbound Pantry Row Distributors day 4 block 2 door 2
- d4 PO IG4226 Cornerstone Staples: 1 plt $720
- d4 placed parked Inbound Cornerstone Staples day 4 block 3 door 1
- d4 PO IG4227 Fairweather Trading Co.: 12 plt $6,912
- d4 placed parked Inbound Fairweather Trading Co. day 4 block 3 door 2
- d4 placed parked Inbound Fairweather Trading Co. day 4 block 4 door 1
- d4 released 1 order(s)
- d5 booked Sneaky Pete's Seafood day 5 block 1 door 1
- d5 booked Sneaky Pete's Seafood day 5 block 1 door 2
- d5 PO IG5228 Fairweather Trading Co.: 14 plt $8,424
- d5 placed parked Inbound Fairweather Trading Co. day 5 block 2 door 1
- d5 signed bulk BULK_Bramble & Bite Berry Co._D5_0
- d5 signed bulk BULK_Fizz-Pop Soda Works_D5_1
- d5 booked Bramble & Bite Berry Co. day 5 block 6 door 1
- d5 booked Fizz-Pop Soda Works day 5 block 6 door 2
- d5 released 2 order(s) (waiting for lane)
- d5 released 1 order(s)
- d6 booked Sneaky Pete's Seafood day 6 block 1 door 1
- d6 PO IG6229 Iron Gate Staples: 9 plt $2,472
- d6 PO IG6230 Backdoor Bill's Overstock: 2 plt $608
- d6 placed parked Inbound Iron Gate Staples day 6 block 1 door 2
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 2 door 1
- d6 released 1 order(s) (waiting for lane)
- d7 booked Sneaky Pete's Seafood day 7 block 1 door 1
- d7 booked Sneaky Pete's Seafood day 7 block 1 door 2
- d7 booked Bramble & Bite Berry Co. day 7 block 2 door 1
- d7 booked Sneaky Pete's Seafood day 7 block 2 door 2
- d7 booked Fizz-Pop Soda Works day 7 block 3 door 1
- d7 PO IG7231 Steady Supply Grocers: 8 plt $1,024
- d7 placed parked Inbound Steady Supply Grocers day 7 block 3 door 2
- d7 released 1 order(s) (waiting for lane)
