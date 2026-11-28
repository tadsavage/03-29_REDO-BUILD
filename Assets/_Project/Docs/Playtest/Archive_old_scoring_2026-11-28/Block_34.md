# Playtest block 34 — days 1–8

## Score: **46.0 (+3.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (6 shipped, 0 late) | 15% | 100.0 (0.0) |
| Fill rate | 10% | 60.9 (+6.2) |
| Throughput (21 plt vs target 84) | 15% | 25.0 (+17.9) |
| Revenue ($14,831) | 15% | 26.5 (+23.8) |
| Profit (cash -31,274) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 28.6 (-16.5) |
| Reputation (0 to 31) | 5% | 100.0 (+23.3) |

Orders shipped **6**, late/fined **0**, pallets shipped **21**. Cash $206,715 → $175,441 (-31,274). Stock at cost $0 → $14,798 (not part of profit).
Bot did: 1 contracts signed, 18 dock bookings, 6 orders released, 18 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **8**
  - WARN|[AiNavigation] Employee_Madison Hernandez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order ##c#bce##a# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ###-bed##ce#c#b# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-fbf##-b#ff## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##e##bf##bc## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-eb##c#-b#a#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ###fc##e### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] Dockstocker: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived.
- New this block: **14**
  - WARN|[AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - WARN|[OrderSelectionTaskDriver] Order ###baa-bfb#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##c##cd#f#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] Order #-b##a#-ac### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).
  - WARN|[OrderSelectionTaskDriver] Order ###e-b#d### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).
  - WARN|[AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReachTruckOperator] Replenish: could not reach pallet at ##-A# within #m.
  - WARN|[OrderSelectionTaskDriver] Order #-bbfb#ec#f#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##a###e#e# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).
  - WARN|[OrderSelectionTaskDriver] Order ##a###e#e# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##c#b#d#-a#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).
  - WARN|[AiNavigation] Employee_Ann Reyes: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - WARN|[OrderSelectionTaskDriver] Order ###baa-bfb#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).

## Bugs found (17 distinct, 42 incidents)

| Incidents | Bug |
|---|---|
| 14 | WARN — [AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 7 | WARN — [AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 5 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 2 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [OrderSelectionTaskDriver] Order ###baa-bfb#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##c##cd#f#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-b##a#-ac### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###e-b#d### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReachTruckOperator] Replenish: could not reach pallet at ##-A# within #m. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-bbfb#ec#f#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##a###e#e# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##a###e#e# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##c#b#d#-a#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [AiNavigation] Employee_Ann Reyes: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###baa-bfb#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Dockstocker cannot reach task position at (-24.56, 1.12, 40.36) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 823e9e24-5c46-42cd-9f42-a9bf11afc248 but the employee is 25.6m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] ReachTruck cannot reach task position at (-43.11, 1.16, 50.96) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] ReachTruck: stuck at (8.33, 1.16, 46.47) — walking out to (-2.93, 1.16, 39.97) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Dockstocker: stuck at (-13.96, 1.11, 39.69) — walking out to (-20.44, 1.16, 51.00) (12.60m to mesh).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order af429487-b180-4a28-ac36-8fc0738f045a (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 113/261 case(s).
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 421a7dff-4311-432e-b8d5-4feccd508e45 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 105/257 case(s).
- **WARN** (day 3): [AiNavigation] PalletJack: stuck at (-14.51, 1.11, 48.17) — walking out to (-14.08, 1.08, 35.50) (12.54m to mesh).
- **WARN** (day 3): [ReachTruckOperator] Replenish: could not reach pallet at 01-07-A1 within 0.15m.
- **WARN** (day 4): [OrderSelectionTaskDriver] Order 55e50480-bbfb-45ec-86f8-aedcc42a12db (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 5/82.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 0829b8d5-0a32-4971-8e4e-203373825629 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 13/85 case(s).
- **OVERLAP** (day 5): OVERLAP Pallet_00_Slot0_Tier0 & Pallet_01_Slot0_Tier1 @(26, 87)  <<HISTORY: d3 22:33 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 06:47 Replenish task created (02-08-A0->02-08-00) by ReplenishmentService.CreateReplenishTask | d5 06:48 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 07:08 moved (27, 87)->(-1, -1) by InventoryService.MovePallet | d5 07:24 moved (-1, -1)->(27, 87) by InventoryService.MovePallet | d5 07:24 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d4 09:28 picked 1 (left 4) by InventoryService.PickFromPallet | d4 09:32 picked 1 (left 3) by InventoryService.PickFromPallet | d5 06:45 picked 1 (left 2) by InventoryService.PickFromPallet | d5 06:46 picked 1 (left 1) by InventoryService.PickFromPallet | d5 06:47 picked 1 (left 0) by InventoryService.PickFromPallet | d5 06:47 record destroyed by InventoryService.DestroyPallet>>
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 0829b8d5-0a32-4971-8e4e-203373825629 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 48/85.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order ddce5673-7c0b-4d11-a658-b0cb98c84fb1 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 30/128 case(s).
- **WARN** (day 7): [AiNavigation] Employee_Ann Reyes: task target (-31.19, 1.11, 45.59) is not reachable — the partial path ends 10.23m short of it. Handing the task back.
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 1243a4b4-5103-4baa-bfb8-c51a83a2c9ac (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 19/130 case(s).
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 1243a4b4-5103-4baa-bfb8-c51a83a2c9ac (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 69/130.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1116 Backdoor Bill's Overstock: 3 plt $2,868
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 18 door 1
- d1 PO IG1117 Anchor Point Foods: 5 plt $3,344
- d1 placed parked Inbound Anchor Point Foods day 1 block 19 door 2
- d1 PO IG1118 Homestead Market Co.: 3 plt $3,248
- d1 placed parked Inbound Homestead Market Co. day 1 block 20 door 1
- d1 PO IG1119 Homestead Market Co.: 3 plt $3,104
- d1 placed parked Inbound Homestead Market Co. day 1 block 21 door 3
- d1 PO IG1120 Bulk Basin Foods: 2 plt $1,064
- d1 placed parked Inbound Bulk Basin Foods day 1 block 23 door 1
- d1 PO IG1121 Blue Ridge Provisions: 4 plt $5,782
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 24 door 2
- d2 released 1 order(s)
- d2 PO IG2122 Anchor Point Foods: 1 plt $520
- d2 placed parked Inbound Anchor Point Foods day 2 block 36 door 2
- d2 PO IG2123 Homestead Market Co.: 1 plt $672
- d2 placed parked Inbound Homestead Market Co. day 2 block 37 door 2
- d2 PO IG2124 Bulk Basin Foods: 1 plt $544
- d2 placed parked Inbound Bulk Basin Foods day 2 block 38 door 2
- d3 released 1 order(s)
- d3 PO IG3125 Bulk Basin Foods: 2 plt $1,305
- d3 placed parked Inbound Bulk Basin Foods day 3 block 36 door 2
- d3 PO IG3126 Anchor Point Foods: 1 plt $720
- d3 placed parked Inbound Anchor Point Foods day 3 block 37 door 3
- d3 PO IG3127 Backdoor Bill's Overstock: 1 plt $272
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 38 door 2
- d3 PO IG3128 Homestead Market Co.: 1 plt $576
- d3 placed parked Inbound Homestead Market Co. day 3 block 39 door 1
- d4 released 1 order(s)
- d4 PO IG4129 Bulk Basin Foods: 1 plt $120
- d4 placed parked Inbound Bulk Basin Foods day 4 block 36 door 1
- d4 PO IG4130 Anchor Point Foods: 1 plt $816
- d4 placed parked Inbound Anchor Point Foods day 4 block 37 door 1
- d5 released 1 order(s)
- d6 PO IG6131 Bulk Basin Foods: 1 plt $112
- d6 placed parked Inbound Bulk Basin Foods day 6 block 2 door 1
- d6 released 1 order(s)
- d7 released 1 order(s)
- d7 PO IG7132 Anchor Point Foods: 1 plt $420
- d7 placed parked Inbound Anchor Point Foods day 7 block 36 door 2
- d7 PO IG7133 Bulk Basin Foods: 1 plt $112
- d7 placed parked Inbound Bulk Basin Foods day 7 block 37 door 2
