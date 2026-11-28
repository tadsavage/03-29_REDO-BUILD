# Playtest block 31 — days 1–8

## Score: **42.5 (-9.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (6 shipped, 0 late) | 15% | 100.0 (0.0) |
| Fill rate | 10% | 67.7 (-0.5) |
| Throughput (6 plt vs target 84) | 15% | 7.1 (0.0) |
| Revenue ($1,789) | 15% | 3.2 (+0.1) |
| Profit (cash -42,693) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 41.4 (-38.6) |
| Reputation (0 to 16) | 5% | 76.7 (-3.3) |

Orders shipped **6**, late/fined **0**, pallets shipped **6**. Cash $205,367 → $162,674 (-42,693). Stock at cost $0 → $22,463 (not part of profit).
Bot did: 1 contracts signed, 14 dock bookings, 6 orders released, 14 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **1**
  - REPEATING|x# and counting: Parameter 'IsWaving' does not exist.
- New this block: **13**
  - WARN|[AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] Employee_Janice Peterson: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - WARN|[OrderSelectionTaskDriver] Order ##d#a#de##c### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f#b#b##e### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##c##df##ba#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] Order ##e#e#-b#df## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ###c#-bbce## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] Employee_Janice Peterson: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived.
  - WARN|[OrderSelectionTaskDriver] Order #-cd##b#e-bda### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] Employee_Melissa Gonzalez cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[OrderSelectionTaskDriver] Order ##c#bae-ac#b-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).
  - WARN|[OrderSelectionTaskDriver] Order #-c#c##ba##b#b-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.

## Bugs found (14 distinct, 17 incidents)

| Incidents | Bug |
|---|---|
| 2 | WARN — [AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 2 | WARN — [AiNavigation] Employee_Janice Peterson: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 2 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##d#a#de##c### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f#b#b##e### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##c##df##ba#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##e#e#-b#df## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###c#-bbce## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Employee_Janice Peterson: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-cd##b#e-bda### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Employee_Melissa Gonzalez cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##c#bae-ac#b-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-c#c##ba##b#b-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |

## First sightings (with stack traces)

- **WARN** (day 2): [AiNavigation] Dockstocker cannot reach task position at (-44.44, 1.12, 49.64) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 736247e2-9d5a-4de1-85c7-047b9ded1307 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 9/15.
- **WARN** (day 2): [AiNavigation] Employee_Janice Peterson: task target (-2.94, 1.16, 46.99) is not reachable — the partial path ends 11.97m short of it. Handing the task back.
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet d97868b9-f91b-4b62-97e5-59f7f908b49f but the employee is 13.6m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 63e7b28a-3c64-4df5-9ba3-a4ba0f942b8d but the employee is 13.5m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 3): [AiNavigation] Dockstocker: stuck at (-3.80, 1.11, 51.00) — walking out to (9.20, 1.16, 51.00) (12.00m to mesh).
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 1efb1f00-301e-45e2-b9df-6ea6f27348f8 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 16/19.
- **WARN** (day 4): [OrderSelectionTaskDriver] Order b3a6188d-2490-4c25-bbce-716a9a5481f6 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 10/18.
- **WARN** (day 4): [AiNavigation] Employee_Janice Peterson: task target (-4.89, 1.16, 45.50) only partially reachable — stopped 2.78m short. Treating as arrived.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 18a7213a-cd22-4b4e-bda0-9eb5c014bbec (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 9/15.
- **WARN** (day 6): [AiNavigation] Employee_Melissa Gonzalez cannot reach task position at (-20.59, 1.11, 37.71) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order b17241d2-904c-4bae-ac6b-e4039b5c5b0c (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 14/17 case(s).
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 6c8a27ca-c0c9-4ba0-8b3b-ce0fa24ce617 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 9/15.
- **WARN** (day 7): [AiNavigation] ReachTruck cannot reach task position at (-7.34, 1.12, 49.64) (path invalid) — abandoning the walk and handing the task back.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1550 Blue Ridge Provisions: 3 plt $3,099
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 18 door 1
- d1 PO IG1551 Anchor Point Foods: 6 plt $4,051
- d1 placed parked Inbound Anchor Point Foods day 1 block 19 door 2
- d1 PO IG1552 Bulk Basin Foods: 2 plt $336
- d1 placed parked Inbound Bulk Basin Foods day 1 block 20 door 1
- d1 PO IG1553 Homestead Market Co.: 3 plt $3,388
- d1 placed parked Inbound Homestead Market Co. day 1 block 22 door 1
- d1 PO IG1554 Homestead Market Co.: 3 plt $3,136
- d1 placed parked Inbound Homestead Market Co. day 1 block 24 door 1
- d2 released 1 order(s)
- d2 PO IG2555 Bulk Basin Foods: 2 plt $1,064
- d2 placed parked Inbound Bulk Basin Foods day 2 block 36 door 1
- d2 PO IG2556 Anchor Point Foods: 1 plt $704
- d2 placed parked Inbound Anchor Point Foods day 2 block 37 door 2
- d3 released 1 order(s)
- d3 PO IG3557 Blue Ridge Provisions: 3 plt $4,294
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 36 door 1
- d3 PO IG3558 Anchor Point Foods: 1 plt $468
- d3 placed parked Inbound Anchor Point Foods day 3 block 37 door 2
- d4 released 1 order(s)
- d4 PO IG4559 Anchor Point Foods: 1 plt $480
- d4 placed parked Inbound Anchor Point Foods day 4 block 36 door 1
- d4 PO IG4560 Homestead Market Co.: 1 plt $675
- d4 placed parked Inbound Homestead Market Co. day 4 block 37 door 1
- d5 released 1 order(s)
- d6 PO IG6561 Bulk Basin Foods: 1 plt $544
- d6 placed parked Inbound Bulk Basin Foods day 6 block 2 door 1
- d6 released 1 order(s)
- d6 PO IG6562 Bulk Basin Foods: 1 plt $624
- d6 placed parked Inbound Bulk Basin Foods day 6 block 36 door 1
- d6 PO IG6563 Backdoor Bill's Overstock: 1 plt $288
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 37 door 1
- d7 released 1 order(s)
