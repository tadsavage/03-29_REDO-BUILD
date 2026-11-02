# Playtest block 33 — days 1–8

## Score: **42.1 (-5.0)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (6 shipped, 0 late) | 15% | 100.0 (0.0) |
| Fill rate | 10% | 54.6 (-11.7) |
| Throughput (6 plt vs target 84) | 15% | 7.1 (0.0) |
| Revenue ($1,520) | 15% | 2.7 (-0.5) |
| Profit (cash -43,034) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 45.1 (-14.9) |
| Reputation (0 to 16) | 5% | 76.7 (0.0) |

Orders shipped **6**, late/fined **0**, pallets shipped **6**. Cash $206,716 → $163,682 (-43,034). Stock at cost $0 → $22,584 (not part of profit).
Bot did: 1 contracts signed, 14 dock bookings, 6 orders released, 14 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **6**
  - WARN|[OrderSelectionTaskDriver] Order ##c##-ae#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-e#be#ade#d### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-aafd#ed#b#e## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ###e#### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##d##b#-b#d### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##d##f#aa##f#ae# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
- New this block: **9**
  - WARN|[AiNavigation] Employee_Madison Hernandez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order ##c#bce##a# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ###-bed##ce#c#b# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-fbf##-b#ff## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##e##bf##bc## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-eb##c#-b#a#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ###fc##e### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] Dockstocker: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived.

## Bugs found (11 distinct, 15 incidents)

| Incidents | Bug |
|---|---|
| 3 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [AiNavigation] Employee_Madison Hernandez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##c#bce##a# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###-bed##ce#c#b# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-fbf##-b#ff## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##e##bf##bc## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-eb##c#-b#a#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###fc##e### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Dockstocker: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Madison Hernandez: stuck at (-2.44, 1.28, 47.00) — walking out to (-13.70, 1.13, 40.50) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Dockstocker cannot reach task position at (-44.44, 1.13, 37.71) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order eb3ddc14-841c-4bce-9425-4762117865a3 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 9/16.
- **WARN** (day 2): [AiNavigation] ReachTruck: stuck at (-14.50, 1.16, 42.76) — walking out to (-25.75, 1.13, 36.08) (12.09m to mesh).
- **WARN** (day 2): [AiNavigation] Dockstocker: stuck at (-15.04, 1.11, 42.00) — walking out to (-28.04, 1.16, 41.88) (12.00m to mesh).
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 5f29b330-1446-4204-bed1-07750ce7c8b2 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 10/16.
- **WARN** (day 4): [OrderSelectionTaskDriver] Order b7fcb411-fbf5-4586-b4ff-68d04edd91b0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 5/16.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 9042d324-07e0-4bf2-94bc-5f713d6088ee (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 11/16.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order a5a95604-eb56-45c6-b2a2-ec1a307f1e0c (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 7/16.
- **WARN** (day 7): [OrderSelectionTaskDriver] Order db895b22-6466-4fc8-94e3-7a9d20bff908 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 11/17.
- **WARN** (day 7): [AiNavigation] Dockstocker: task target (-0.71, 0.01, 50.96) only partially reachable — stopped 2.27m short. Treating as arrived.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1579 Backdoor Bill's Overstock: 2 plt $1,340
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 18 door 1
- d1 PO IG1580 Anchor Point Foods: 5 plt $3,513
- d1 placed parked Inbound Anchor Point Foods day 1 block 19 door 2
- d1 PO IG1581 Homestead Market Co.: 3 plt $3,378
- d1 placed parked Inbound Homestead Market Co. day 1 block 20 door 1
- d1 PO IG1582 Bulk Basin Foods: 4 plt $1,824
- d1 placed parked Inbound Bulk Basin Foods day 1 block 22 door 1
- d1 PO IG1583 Homestead Market Co.: 3 plt $3,168
- d1 placed parked Inbound Homestead Market Co. day 1 block 24 door 2
- d2 released 1 order(s)
- d2 PO IG2584 Anchor Point Foods: 1 plt $672
- d2 placed parked Inbound Anchor Point Foods day 2 block 36 door 1
- d3 released 1 order(s)
- d3 PO IG3585 Backdoor Bill's Overstock: 1 plt $1,600
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 36 door 1
- d3 PO IG3586 Anchor Point Foods: 1 plt $440
- d3 placed parked Inbound Anchor Point Foods day 3 block 37 door 1
- d3 PO IG3587 Bulk Basin Foods: 1 plt $624
- d3 placed parked Inbound Bulk Basin Foods day 3 block 38 door 1
- d4 released 1 order(s)
- d4 PO IG4588 Blue Ridge Provisions: 3 plt $4,671
- d4 placed parked Inbound Blue Ridge Provisions day 4 block 36 door 1
- d4 PO IG4589 Homestead Market Co.: 1 plt $720
- d4 placed parked Inbound Homestead Market Co. day 4 block 37 door 2
- d5 released 1 order(s)
- d6 PO IG6590 Anchor Point Foods: 1 plt $672
- d6 placed parked Inbound Anchor Point Foods day 6 block 2 door 1
- d6 released 1 order(s)
- d6 PO IG6591 Bulk Basin Foods: 1 plt $595
- d6 placed parked Inbound Bulk Basin Foods day 6 block 36 door 1
- d7 released 1 order(s)
- d7 PO IG7592 Bulk Basin Foods: 1 plt $112
- d7 placed parked Inbound Bulk Basin Foods day 7 block 36 door 1
