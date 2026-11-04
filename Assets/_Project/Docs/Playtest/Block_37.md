# Playtest block 37 — days 1–8

## Score: **43.4 (+20.0)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (6 shipped, 0 late) | 15% | 100.0 (+66.7) |
| Fill rate | 10% | 53.7 (+50.1) |
| Throughput (11 plt vs target 84) | 15% | 13.1 (+10.7) |
| Revenue ($6,836) | 15% | 12.2 (+11.3) |
| Profit (cash -39,132) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 36.8 (+2.7) |
| Reputation (0 to 31) | 5% | 100.0 (+20.0) |

Orders shipped **6**, late/fined **0**, pallets shipped **11**. Cash $206,718 → $167,586 (-39,132). Stock at cost $0 → $19,326 (not part of profit).
Bot did: 1 contracts signed, 18 dock bookings, 6 orders released, 18 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **11**
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
- New this block: **16**
  - WARN|[AiNavigation] Employee_Daniel Wilson: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - WARN|[AiNavigation] Employee_Daniel Wilson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f#fe#a##c#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ae##d#f### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] Order ###d-a#b#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ###cff-b#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a#a##-ac#e## but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] Order ###e##db## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-fd##ae#-bac#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ###a#dca## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a###c#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##f#f#afd-a#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ###ca#e### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ad#ceb#afa## but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] Order ###d#-bb#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).

## Bugs found (18 distinct, 21 incidents)

| Incidents | Bug |
|---|---|
| 3 | WARN — [AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 2 | WARN — [AiNavigation] Employee_Daniel Wilson: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Daniel Wilson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f#fe#a##c#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ae##d#f### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###d-a#b#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###cff-b#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a#a##-ac#e## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###e##db## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-fd##ae#-bac#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###a#dca## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a###c#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##f#f#afd-a#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###ca#e### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ad#ceb#afa## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###d#-bb#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###ae#bde## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Daniel Wilson: stuck at (-12.55, 1.16, 46.29) — walking out to (-12.32, 1.50, 33.29) (12.01m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 8f11b0f8-f6fe-4a55-9c27-b393635ad1c7 but the employee is 10.1m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet a16b3f17-0ae5-486d-94f2-524c11d77491 but the employee is 20.1m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 945a76d0-6789-486d-a5b9-f659ed979758 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 35/83.
- **WARN** (day 2): [AiNavigation] ReachTruck cannot reach task position at (-44.44, 1.16, 41.69) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 2): [AiNavigation] Employee_Daniel Wilson: task target (6.34, 1.16, 46.99) is not reachable — the partial path ends 10.61m short of it. Handing the task back.
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 637bf923-9663-4cff-b112-be326a42aec7 but the employee is 8.7m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 41c8092b-a5a3-4831-ac4e-4c9e5163678d but the employee is 8.5m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 8e29a2fd-9844-4e76-97db-3b4e7bdfeb19 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 9/23.
- **WARN** (day 4): [OrderSelectionTaskDriver] Order 89112840-fd79-4ae3-bac0-d04ef6f31677 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 2/80.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 9da46061-8241-493a-8dca-9bcc3fb54c74 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 53/138.
- **WARN** (day 5): [AiNavigation] Dockstocker cannot reach task position at (-45.76, 1.18, 36.39) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 6): [ReceiverReceivingWorkflow] Arrival callback fired for pallet b5152cad-a275-4543-9c54-ec25361da1f6 but the employee is 14.4m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 6): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 27f6969c-2f0f-4afd-a054-a67ac12d9dd9 but the employee is 14.4m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 6): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 26fc3f51-2647-44ca-89e8-5ed92ac08dfc but the employee is 5.6m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 6): [ReceiverReceivingWorkflow] Arrival callback fired for pallet ae5bb520-16ad-4ceb-8afa-8e02a8bf7439 but the employee is 5.4m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order eb202361-7567-4d47-bb98-b99aeed29b62 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 29/136 case(s).
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 32c9f31a-7464-40ae-9bde-7f81ce64405e (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 38/95.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1606 Anchor Point Foods: 5 plt $3,213
- d1 placed parked Inbound Anchor Point Foods day 1 block 18 door 1
- d1 PO IG1607 Homestead Market Co.: 2 plt $1,287
- d1 placed parked Inbound Homestead Market Co. day 1 block 19 door 2
- d1 PO IG1608 Bulk Basin Foods: 4 plt $2,300
- d1 placed parked Inbound Bulk Basin Foods day 1 block 20 door 3
- d1 PO IG1609 Backdoor Bill's Overstock: 1 plt $1,600
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 22 door 1
- d2 released 1 order(s)
- d2 PO IG2610 Homestead Market Co.: 3 plt $3,172
- d2 placed parked Inbound Homestead Market Co. day 2 block 36 door 1
- d2 PO IG2611 Anchor Point Foods: 2 plt $1,728
- d2 placed parked Inbound Anchor Point Foods day 2 block 37 door 2
- d3 released 1 order(s)
- d3 PO IG3612 Bulk Basin Foods: 2 plt $712
- d3 placed parked Inbound Bulk Basin Foods day 3 block 36 door 1
- d3 PO IG3613 Homestead Market Co.: 1 plt $1,920
- d3 placed parked Inbound Homestead Market Co. day 3 block 37 door 2
- d3 PO IG3614 Anchor Point Foods: 1 plt $448
- d3 placed parked Inbound Anchor Point Foods day 3 block 38 door 1
- d4 released 1 order(s)
- d4 PO IG4615 Homestead Market Co.: 1 plt $576
- d4 placed parked Inbound Homestead Market Co. day 4 block 36 door 1
- d4 PO IG4616 Anchor Point Foods: 1 plt $468
- d4 placed parked Inbound Anchor Point Foods day 4 block 37 door 1
- d5 released 1 order(s)
- d6 PO IG6617 Blue Ridge Provisions: 2 plt $4,050
- d6 placed parked Inbound Blue Ridge Provisions day 6 block 2 door 1
- d6 PO IG6618 Backdoor Bill's Overstock: 2 plt $1,308
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 3 door 2
- d6 released 1 order(s)
- d6 PO IG6619 Bulk Basin Foods: 1 plt $128
- d6 placed parked Inbound Bulk Basin Foods day 6 block 16 door 1
- d6 PO IG6620 Backdoor Bill's Overstock: 1 plt $272
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 36 door 2
- d7 released 1 order(s)
- d7 PO IG7621 Backdoor Bill's Overstock: 1 plt $272
- d7 placed parked Inbound Backdoor Bill's Overstock day 7 block 36 door 1
- d7 PO IG7622 Bulk Basin Foods: 2 plt $224
- d7 placed parked Inbound Bulk Basin Foods day 7 block 37 door 1
- d7 PO IG7623 Homestead Market Co.: 1 plt $512
- d7 placed parked Inbound Homestead Market Co. day 7 block 38 door 2
