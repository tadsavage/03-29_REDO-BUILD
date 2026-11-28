# Playtest block 32 — days 1–8

## Score: **47.0 (+4.5)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (6 shipped, 0 late) | 15% | 100.0 (0.0) |
| Fill rate | 10% | 66.3 (-1.4) |
| Throughput (6 plt vs target 84) | 15% | 7.1 (0.0) |
| Revenue ($1,781) | 15% | 3.2 (0.0) |
| Profit (cash -42,981) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 60.0 (+18.6) |
| Reputation (0 to 16) | 5% | 76.7 (0.0) |

Orders shipped **6**, late/fined **0**, pallets shipped **6**. Cash $206,718 → $163,737 (-42,981). Stock at cost $0 → $23,878 (not part of profit).
Bot did: 1 contracts signed, 14 dock bookings, 5 orders released, 14 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **12**
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
- New this block: **6**
  - WARN|[OrderSelectionTaskDriver] Order ##c##-ae#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-e#be#ade#d### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-aafd#ed#b#e## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ###e#### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##d##b#-b#d### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##d##f#aa##f#ae# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.

## Bugs found (8 distinct, 8 incidents)

| Incidents | Bug |
|---|---|
| 1 | WARN — [AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##c##-ae#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e#be#ade#d### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-aafd#ed#b#e## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###e#### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##d##b#-b#d### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##d##f#aa##f#ae# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Dockstocker cannot reach task position at (-47.09, 1.12, 45.66) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order a4417741-2c53-4100-ae45-d8377c6762bd (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 12/15.
- **WARN** (day 2): [AiNavigation] Dockstocker: stuck at (-15.04, 1.11, 41.98) — walking out to (-28.04, 1.16, 41.88) (12.00m to mesh).
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 0f475802-e9be-4ade-9d90-7d2048119430 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 10/19.
- **WARN** (day 4): [OrderSelectionTaskDriver] Order 18db30c4-aafd-43ed-8b6e-353d18b0a6bf (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 11/16.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 03432df2-3083-4e34-9530-9f503b609569 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 10/16.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 2dc768d3-7d97-4b69-b7d5-24d9a8429cba (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 12/16.
- **WARN** (day 7): [OrderSelectionTaskDriver] Order eab944a4-15d9-419f-9aa9-17788f4ae689 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 10/16.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1565 Anchor Point Foods: 5 plt $3,263
- d1 placed parked Inbound Anchor Point Foods day 1 block 18 door 1
- d1 PO IG1566 Homestead Market Co.: 1 plt $513
- d1 placed parked Inbound Homestead Market Co. day 1 block 19 door 2
- d1 PO IG1567 Blue Ridge Provisions: 3 plt $3,188
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 20 door 2
- d1 PO IG1568 Bulk Basin Foods: 3 plt $1,265
- d1 placed parked Inbound Bulk Basin Foods day 1 block 22 door 1
- d1 PO IG1569 Homestead Market Co.: 3 plt $3,136
- d1 placed parked Inbound Homestead Market Co. day 1 block 24 door 1
- d2 released 1 order(s)
- d2 PO IG2570 Anchor Point Foods: 2 plt $1,568
- d2 placed parked Inbound Anchor Point Foods day 2 block 36 door 1
- d2 PO IG2571 Bulk Basin Foods: 2 plt $1,248
- d2 placed parked Inbound Bulk Basin Foods day 2 block 37 door 2
- d3 released 1 order(s)
- d3 PO IG3572 Blue Ridge Provisions: 4 plt $5,953
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 36 door 1
- d4 released 1 order(s)
- d4 PO IG4573 Homestead Market Co.: 1 plt $800
- d4 placed parked Inbound Homestead Market Co. day 4 block 36 door 1
- d4 PO IG4574 Bulk Basin Foods: 1 plt $512
- d4 placed parked Inbound Bulk Basin Foods day 4 block 37 door 1
- d5 released 1 order(s)
- d6 PO IG6575 Anchor Point Foods: 1 plt $520
- d6 placed parked Inbound Anchor Point Foods day 6 block 2 door 1
- d6 released 1 order(s)
- d6 PO IG6576 Anchor Point Foods: 1 plt $420
- d6 placed parked Inbound Anchor Point Foods day 6 block 36 door 1
- d6 PO IG6577 Homestead Market Co.: 1 plt $1,520
- d6 placed parked Inbound Homestead Market Co. day 6 block 37 door 1
- d6 PO IG6578 Backdoor Bill's Overstock: 1 plt $288
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 38 door 1
