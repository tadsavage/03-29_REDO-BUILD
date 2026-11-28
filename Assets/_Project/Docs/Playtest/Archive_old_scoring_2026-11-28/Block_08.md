# Playtest block 8 — days 1–8

## Score: **23.7 (-17.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (1 shipped, 13 late) | 15% | 7.1 (-23.3) |
| Fill rate | 10% | 100.0 (+66.0) |
| Throughput (2 plt vs target 84) | 15% | 2.4 (-44.0) |
| Revenue ($1,148) | 15% | 2.1 (-40.6) |
| Profit (cash -125,666) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 96.1 (+11.0) |
| Reliability (bugs) | 25% | 18.7 (-33.5) |
| Reputation (0 to 0) | 5% | 50.0 (-10.0) |

Orders shipped **1**, late/fined **13**, pallets shipped **2**. Cash $139,039 → $13,373 (-125,666).
Bot did: 8 contracts signed, 51 dock bookings, 13 orders released, 27 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **5**
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Cheryl Thompson' as its operator though nobody is on it or running it - clearing the stale link.
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.
  - WARN|[OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock.
- New this block: **10**
  - VEHICLE-STATIONARY|PalletJack has a driver, hasn't moved in #s while # task(s) wait
  - VEHICLE-STATIONARY|ReachTruck has a driver, hasn't moved in #s while # task(s) wait
  - VEHICLE-STATIONARY|Dockstocker has a driver, hasn't moved in #s while # task(s) wait
  - NO-TASK| lane pallet #A# sku=# load=#
  - NO-TASK| lane pallet #B# sku=# load=#
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per # s)
  - NO-TASK| lane pallet #E# sku=# load=#
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Jack Miller: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##b#e#d#cfc## but the employee is #m away (max #m) — refusing to start receiving.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Jack Miller: released Receive task for pallet ##b#e#d#cfc## back to the queue (employee strayed outside receiving range).

## Bugs found (11 distinct, 107 incidents)

| Incidents | Bug |
|---|---|
| 38 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) wait |
| 35 | VEHICLE-STATIONARY — ReachTruck has a driver, hasn't moved in #s while # task(s) wait |
| 26 | VEHICLE-STATIONARY — Dockstocker has a driver, hasn't moved in #s while # task(s) wait |
| 1 | NO-TASK —  lane pallet #A# sku=# load=# |
| 1 | NO-TASK —  lane pallet #B# sku=# load=# |
| 1 | REPEATING — x# and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per # s) |
| 1 | REPEATING — x# and counting: [OrderService] Order OG#: no reserve or pick-face pallet of # left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it. |
| 1 | NO-TASK —  lane pallet #E# sku=# load=# |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Jack Miller: receive workflow cancelled (out of range) — releasing the task and resuming patrol. |
| 1 | REPEATING — x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##b#e#d#cfc## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Jack Miller: released Receive task for pallet ##b#e#d#cfc## back to the queue (employee strayed outside receiving range). |

## First sightings (with stack traces)

- **VEHICLE-STATIONARY** (day 1): PalletJack has a driver, hasn't moved in 255s while 2 task(s) wait
- **VEHICLE-STATIONARY** (day 1): ReachTruck has a driver, hasn't moved in 255s while 10 task(s) wait
- **NO-TASK** (day 2): NO-TASK lane pallet 2A-5 sku=435689 load=0920000888
- **NO-TASK** (day 2): NO-TASK lane pallet 1B-5 sku=975031 load=0593778602
- **REPEATING** (day 2): x25 and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per 30 s)
- **REPEATING** (day 2): x25 and counting: [OrderService] Order OG2177: no reserve or pick-face pallet of 903561 left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
- **NO-TASK** (day 2): NO-TASK lane pallet 2E-1 sku=435689 load=2531998897
- **REPEATING** (day 3): x25 and counting: [ReceivingTaskDriver] Employee_Jack Miller: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
- **REPEATING** (day 3): x25 and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet e84dd4fd-0b7e-412d-9cfc-8a308858538b but the employee is 3.6m away (max 3m) — refusing to start receiving.
- **REPEATING** (day 3): x25 and counting: [ReceivingTaskDriver] Employee_Jack Miller: released Receive task for pallet e84dd4fd-0b7e-412d-9cfc-8a308858538b back to the queue (employee strayed outside receiving range).
- **VEHICLE-STATIONARY** (day 3): Dockstocker has a driver, hasn't moved in 255s while 40 task(s) wait

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1172 Anchor Point Foods: 1 plt $560
- d1 placed parked Inbound Anchor Point Foods day 1 block 5 door 1
- d1 PO IG1173 Bulk Basin Foods: 4 plt $1,124
- d1 placed parked Inbound Bulk Basin Foods day 1 block 5 door 2
- d1 PO IG1174 Homestead Market Co.: 2 plt $1,233
- d1 placed parked Inbound Homestead Market Co. day 1 block 6 door 1
- d1 PO IG1175 Homestead Market Co.: 2 plt $2,416
- d1 placed parked Inbound Homestead Market Co. day 1 block 6 door 2
- d1 PO IG1176 Backdoor Bill's Overstock: 2 plt $640
- d1 signed bulk BULK_Odd-Shape Orchards_D1_0
- d1 signed bulk BULK_Bramble & Bite Berry Co._D1_1
- d1 booked Odd-Shape Orchards day 1 block 7 door 1
- d1 booked Bramble & Bite Berry Co. day 1 block 7 door 2
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 8 door 1
- d1 released 2 order(s) (waiting for lane)
- d1 PO IG1177 Anchor Point Foods: 11 plt $7,140
- d1 placed parked Inbound Anchor Point Foods day 1 block 8 door 2
- d1 PO IG1178 Bulk Basin Foods: 1 plt $640
- d1 placed parked Inbound Bulk Basin Foods day 1 block 9 door 1
- d1 PO IG1179 Fairweather Trading Co.: 11 plt $9,454
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 9 door 2
- d1 PO IG1180 Bulk Basin Foods: 6 plt $2,528
- d1 placed parked Inbound Bulk Basin Foods day 1 block 10 door 1
- d1 PO IG1181 Anchor Point Foods: 4 plt $2,280
- d1 placed parked Inbound Anchor Point Foods day 1 block 10 door 2
- d2 PO IG2182 Backdoor Bill's Overstock: 1 plt $1,500
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 1 door 1
- d2 PO IG2183 Anchor Point Foods: 2 plt $1,280
- d2 placed parked Inbound Anchor Point Foods day 2 block 1 door 2
- d2 PO IG2184 Homestead Market Co.: 1 plt $1,596
- d2 placed parked Inbound Homestead Market Co. day 2 block 2 door 1
- d2 PO IG2185 Blue Ridge Provisions: 4 plt $5,965
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 2 door 2
- d2 PO IG2186 Cornerstone Staples: 6 plt $2,512
- d2 placed parked Inbound Cornerstone Staples day 2 block 3 door 1
- d2 released 1 order(s) (waiting for lane)
- d2 signed bulk BULK_Crunch-Time Cereals_D2_0
- d2 signed bulk BULK_Crunch-Time Cereals_D2_1
- d2 booked Crunch-Time Cereals day 2 block 9 door 1
- d2 booked Crunch-Time Cereals day 2 block 9 door 2
- d2 released 2 order(s) (waiting for lane)
- d2 PO IG2187 Blue Ridge Provisions: 13 plt $7,836
- d2 PO IG2188 Halloran Household: 1 plt $1,170
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 10 door 1
- d2 placed parked Inbound Halloran Household day 2 block 10 door 2
- d3 booked Bramble & Bite Berry Co. day 3 block 1 door 1
- d3 PO IG3189 Anchor Point Foods: 4 plt $2,592
- d3 placed parked Inbound Anchor Point Foods day 3 block 1 door 2
- d3 PO IG3190 Coastal Cargo Exchange: 10 plt $14,040
- d3 PO IG3191 Cornerstone Staples: 1 plt $200
- d3 placed parked Inbound Coastal Cargo Exchange day 3 block 2 door 1
- d3 placed parked Inbound Cornerstone Staples day 3 block 2 door 2
- d3 signed bulk BULK_Honest Harvest Rice_D3_0
- d3 booked Honest Harvest Rice day 3 block 6 door 1
- d3 released 1 order(s) (waiting for lane)
- d3 released 1 order(s) (waiting for lane)
- d4 booked Crunch-Time Cereals day 4 block 1 door 1
- d4 booked Sneaky Pete's Seafood day 4 block 1 door 2
- d4 booked Crunch-Time Cereals day 4 block 2 door 1
- d4 PO IG4192 Anchor Point Foods: 4 plt $2,544
- d4 placed parked Inbound Anchor Point Foods day 4 block 2 door 2
- d4 PO IG4193 Blue Ridge Provisions: 7 plt $6,488
- d4 PO IG4194 Meridian Provisions: 4 plt $1,280
- d4 placed parked Inbound Blue Ridge Provisions day 4 block 3 door 1
- d4 placed parked Inbound Meridian Provisions day 4 block 3 door 2
- d4 released 1 order(s) (waiting for lane)
- d4 signed bulk BULK_Mama Tomato Sauces_D4_0
- d4 signed bulk BULK_Chubby Chipmunk Cookies_D4_1
- d4 booked Chubby Chipmunk Cookies day 4 block 9 door 1
- d4 booked Mama Tomato Sauces day 4 block 9 door 2
- d4 released 2 order(s) (waiting for lane)
- d5 booked Bramble & Bite Berry Co. day 5 block 1 door 1
- d5 booked Sneaky Pete's Seafood day 5 block 1 door 2
- d5 booked Honest Harvest Rice day 5 block 2 door 1
- d5 PO IG5195 Pantry Row Distributors: 12 plt $5,248
- d5 PO IG5196 Millbrook Family Foods: 1 plt $448
- d5 placed parked Inbound Pantry Row Distributors day 5 block 2 door 2
- d5 placed parked Inbound Millbrook Family Foods day 5 block 3 door 1
- d5 released 1 order(s) (waiting for lane)
- d6 booked Crunch-Time Cereals day 6 block 1 door 1
- d6 booked Sneaky Pete's Seafood day 6 block 1 door 2
- d6 booked Sneaky Pete's Seafood day 6 block 2 door 1
- d6 booked Crunch-Time Cereals day 6 block 2 door 2
- d6 booked Chubby Chipmunk Cookies day 6 block 3 door 1
- d6 booked Mama Tomato Sauces day 6 block 3 door 2
- d6 PO IG6197 Iron Gate Staples: 4 plt $2,484
- d6 placed parked Inbound Iron Gate Staples day 6 block 4 door 1
- d6 released 1 order(s) (waiting for lane)
- d7 booked Bramble & Bite Berry Co. day 7 block 1 door 1
- d7 booked Sneaky Pete's Seafood day 7 block 1 door 2
- d7 booked Sneaky Pete's Seafood day 7 block 2 door 1
- d7 booked Honest Harvest Rice day 7 block 2 door 2
- d7 PO IG7198 Backdoor Bill's Overstock: 2 plt $640
- d7 placed parked Inbound Backdoor Bill's Overstock day 7 block 3 door 1
- d7 released 1 order(s) (waiting for lane)
