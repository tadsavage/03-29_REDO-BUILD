# Playtest block 36 — days 1–8

## Score: **23.4 (-22.6)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 4 late) | 15% | 33.3 (-50.0) |
| Fill rate | 10% | 3.6 (-40.2) |
| Throughput (2 plt vs target 84) | 15% | 2.4 (-28.6) |
| Revenue ($500) | 15% | 0.9 (-19.0) |
| Profit (cash -46,135) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 34.2 (-11.7) |
| Reputation (0 to 18) | 5% | 80.0 (-20.0) |

Orders shipped **2**, late/fined **4**, pallets shipped **2**. Cash $206,716 → $160,581 (-46,135). Stock at cost $0 → $24,450 (not part of profit).
Bot did: 1 contracts signed, 17 dock bookings, 634 orders released, 17 POs, 0 rush POs.

## Late orders - state when it came due
- 4 x never released

- day 3: OG3132 (Sneaky Pete's Seafood) was never released to the floor. Fined $658. Lines 15, 156 cs, released=False.
- day 4: OG4133 (Sneaky Pete's Seafood) was never released to the floor. Fined $373. Lines 19, 88 cs, released=False.
- day 5: OG5134 (Sneaky Pete's Seafood) was never released to the floor. Fined $363. Lines 17, 92 cs, released=False.
- day 6: OG6135 (Sneaky Pete's Seafood) was never released to the floor. Fined $987. Lines 20, 209 cs, released=False.

## Change since last block
- Fixed / no longer seen: **7**
  - WARN|[OrderSelectionTaskDriver] Order ###d##-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##aaa##-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##a##-ad#b-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - WARN|[AiNavigation] Employee_Rachel Lopez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order ###dc#-b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ###f#-b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
- New this block: **13**
  - WARN|[AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Order ###### (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #.
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Order ##a#a##c## (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Order #-e#b#cd#-badd## (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Order ##d##c#c-# (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #.
  - WARN|[OrderSelectionTaskDriver] Order ###### (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #.
  - WARN|[OrderSelectionTaskDriver] Order ##a#a##c## (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #.
  - WARN|[OrderSelectionTaskDriver] Order #-e#b#cd#-badd## (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #.
  - WARN|[OrderSelectionTaskDriver] Order ##d##c#c-# (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #.
  - WARN|[AiNavigation] Employee_Steven Morgan: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f##-bdb### but the employee is #m away (max #m) — refusing to start receiving.

## Bugs found (13 distinct, 24 incidents)

| Incidents | Bug |
|---|---|
| 7 | WARN — [AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 5 | WARN — [AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Order ###### (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 2 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Order ##a#a##c## (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Order #-e#b#cd#-badd## (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Order ##d##c#c-# (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###### (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##a#a##c## (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e#b#cd#-badd## (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##d##c#c-# (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | WARN — [AiNavigation] Employee_Steven Morgan: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f##-bdb### but the employee is #m away (max #m) — refusing to start receiving. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] ReachTruck cannot reach task position at (4.59, 1.13, 44.34) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] Dockstocker cannot reach task position at (-44.44, 1.12, 41.69) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 4646f8e5-9514-4336-9407-9c1289b09f0e (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 1.
- **REPEATING** (day 3): x25 and counting: [OrderSelectionTaskDriver] Order 4646f8e5-9514-4336-9407-9c1289b09f0e (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 0.
- **WARN** (day 3): [AiNavigation] ReachTruck: stuck at (9.89, 1.16, 48.31) — walking out to (9.89, 1.08, 35.50) (12.68m to mesh).
- **WARN** (day 4): [OrderSelectionTaskDriver] Order c851ce6f-642a-46a6-896c-2e150a60c089 (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 1.
- **REPEATING** (day 4): x25 and counting: [OrderSelectionTaskDriver] Order c851ce6f-642a-46a6-896c-2e150a60c089 (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 1.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order bb4329ee-e86b-4cd7-badd-78be34dc6272 (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 1.
- **REPEATING** (day 5): x25 and counting: [OrderSelectionTaskDriver] Order bb4329ee-e86b-4cd7-badd-78be34dc6272 (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 1.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order e16697ea-182d-4515-9c3c-fe3394ae76d9 (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 1.
- **REPEATING** (day 6): x25 and counting: [OrderSelectionTaskDriver] Order e16697ea-182d-4515-9c3c-fe3394ae76d9 (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 0.
- **WARN** (day 7): [AiNavigation] Employee_Steven Morgan: task target (6.34, 1.16, 46.99) is not reachable — the partial path ends 16.47m short of it. Handing the task back.
- **WARN** (day 7): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 14723d21-f256-4922-bdb0-32acb21bec2d but the employee is 14.8m away (max 4.5m) — refusing to start receiving.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1165 Bulk Basin Foods: 3 plt $664
- d1 placed parked Inbound Bulk Basin Foods day 1 block 18 door 1
- d1 PO IG1166 Anchor Point Foods: 6 plt $3,728
- d1 placed parked Inbound Anchor Point Foods day 1 block 19 door 2
- d1 PO IG1167 Homestead Market Co.: 3 plt $3,275
- d1 placed parked Inbound Homestead Market Co. day 1 block 20 door 1
- d1 PO IG1168 Homestead Market Co.: 3 plt $3,168
- d1 placed parked Inbound Homestead Market Co. day 1 block 21 door 3
- d1 PO IG1169 Backdoor Bill's Overstock: 1 plt $1,140
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 24 door 1
- d2 released 1 order(s)
- d2 PO IG2170 Bulk Basin Foods: 1 plt $576
- d2 placed parked Inbound Bulk Basin Foods day 2 block 36 door 2
- d2 PO IG2171 Anchor Point Foods: 2 plt $1,288
- d2 placed parked Inbound Anchor Point Foods day 2 block 37 door 2
- d2 PO IG2172 Homestead Market Co.: 1 plt $576
- d2 placed parked Inbound Homestead Market Co. day 2 block 38 door 3
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 PO IG3173 Blue Ridge Provisions: 3 plt $4,027
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 36 door 2
- d3 released 1 order(s)
- d3 PO IG3174 Bulk Basin Foods: 2 plt $872
- d3 placed parked Inbound Bulk Basin Foods day 3 block 37 door 3
- d3 released 1 order(s)
- d3 PO IG3175 Backdoor Bill's Overstock: 1 plt $1,400
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 38 door 1
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 PO IG4176 Backdoor Bill's Overstock: 1 plt $272
- d4 placed parked Inbound Backdoor Bill's Overstock day 4 block 36 door 1
- d4 released 2 order(s)
- d4 PO IG4177 Bulk Basin Foods: 1 plt $608
- d4 placed parked Inbound Bulk Basin Foods day 4 block 37 door 1
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d4 released 2 order(s)
- d5 released 2 order(s)
- d5 released 2 order(s)
- d5 released 2 order(s)
- d5 released 2 order(s)
- d5 released 2 order(s)
- d5 released 2 order(s)
- d5 released 2 order(s)
- d5 released 2 order(s)
- d5 released 2 order(s)
- d5 released 2 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d5 released 3 order(s)
- d6 released 3 order(s)
- d6 PO IG6178 Bulk Basin Foods: 3 plt $857
- d6 placed parked Inbound Bulk Basin Foods day 6 block 2 door 1
- d6 released 3 order(s)
- d6 PO IG6179 Anchor Point Foods: 1 plt $672
- d6 placed parked Inbound Anchor Point Foods day 6 block 3 door 2
- d6 released 3 order(s)
- d6 PO IG6180 Homestead Market Co.: 1 plt $512
- d6 placed parked Inbound Homestead Market Co. day 6 block 4 door 1
- d6 released 3 order(s)
- d6 released 3 order(s)
- d6 released 3 order(s)
- d6 released 3 order(s)
- d6 released 3 order(s)
- d6 released 3 order(s)
- d6 released 3 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d6 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 5 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 PO IG7181 Backdoor Bill's Overstock: 1 plt $240
- d7 placed parked Inbound Backdoor Bill's Overstock day 7 block 36 door 2
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
- d7 released 4 order(s)
