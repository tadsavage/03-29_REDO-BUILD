# Playtest block 1023 — days 1–8

## Score: **34.4** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (11 shipped, 5 late) | 15% | 58.3 |
| Fill rate | 10% | 60.9 |
| Throughput (21 plt vs target 84) | 15% | 25.0 |
| Revenue ($14,831) | 15% | 26.5 |
| Profit (cash -324,559) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 7.3 |
| Reputation (0 to 31) | 5% | 100.0 |

Orders shipped **11**, late/fined **5**, pallets shipped **21**. Cash $500,000 → $175,441 (-324,559). Stock at cost $0 → $14,798 (not part of profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Bugs found (39 distinct, 139 incidents)

| Incidents | Bug |
|---|---|
| 49 | OVERLAP —  A Chep & A Chep @(#, #) |
| 8 | OVERLAP —  A Chep & A Chep @#A# |
| 5 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 14 | WARN — [AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 4 | OVERLAP —  OutboundPallet_#-b#f##-b###_# & OutboundPallet_#-b#f##-b###_# @#B# |
| 4 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 4 | OVERLAP —  A Chep & A Chep @#C# |
| 4 | OVERLAP —  A Chep & A Chep @#B# |
| 3 | FLOATING —  OutboundPallet_#-b#f##-b###_# @#B# bottom=# |
| 7 | WARN — [AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 2 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-b#f##-b###_# @#B# |
| 5 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_###d##-#_# @#A# |
| 1 | OVERLAP —  OutboundPallet_#-b#fa#f#-bf#d#dcf#b_# & OutboundPallet_#-b#fa#f#-bf#d#dcf#b_# @#A# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##cb##-b###_# @#A# |
| 1 | FLOATING —  OutboundPallet_##cb##-b###_# @#A# bottom=# |
| 1 | GHOST-SLOT — RECORD # # x# cell=(#, #) lane= - no object |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 1 | REPEATING — x# and counting: Internal: JobTempAlloc has allocations that are more than the maximum lifespan of # frames old - this is not allowed and likely a leak |
| 1 | REPEATING — x# and counting: To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations. |
| 2 | WARN — [ReachTruckOperator] Replenish: could not reach pallet at ##-A# within #m. |
| 1 | NO-TASK —  lane pallet #B# sku=# load=# |
| 1 | REPEATING — x# and counting: Failed to create agent because it is not close enough to the NavMesh |
| 2 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [OrderSelectionTaskDriver] Order ###baa-bfb#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [InventoryService] Lane #B lock was held for over #s - treating it as leaked and taking it over. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##c##cd#f#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-b##a#-ac### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###e-b#d### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-bbfb#ec#f#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##a###e#e# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##a###e#e# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##c#b#d#-a#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [AiNavigation] Employee_Ann Reyes: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###baa-bfb#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |

## First sightings (with stack traces)

- **REPEATING** (day 6): x25 and counting: Failed to create agent because it is not close enough to the NavMesh
- **WARN** (day 1): [AiNavigation] Dockstocker cannot reach task position at (-24.56, 1.12, 40.36) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 823e9e24-5c46-42cd-9f42-a9bf11afc248 but the employee is 25.6m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] ReachTruck cannot reach task position at (-43.11, 1.16, 50.96) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] ReachTruck: stuck at (8.33, 1.16, 46.47) — walking out to (-2.93, 1.16, 39.97) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Dockstocker: stuck at (-13.96, 1.11, 39.69) — walking out to (-20.44, 1.16, 51.00) (12.60m to mesh).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order af429487-b180-4a28-ac36-8fc0738f045a (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 113/261 case(s).
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 421a7dff-4311-432e-b8d5-4feccd508e45 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 105/257 case(s).
- **WARN** (day 3): [AiNavigation] PalletJack: stuck at (-14.51, 1.11, 48.17) — walking out to (-14.08, 1.08, 35.50) (12.54m to mesh).
- **WARN** (day 4): [OrderSelectionTaskDriver] Order 55e50480-bbfb-45ec-86f8-aedcc42a12db (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 5/82.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 0829b8d5-0a32-4971-8e4e-203373825629 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 13/85 case(s).
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 0829b8d5-0a32-4971-8e4e-203373825629 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 48/85.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order ddce5673-7c0b-4d11-a658-b0cb98c84fb1 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 30/128 case(s).
- **OVERLAP** (day 6): OVERLAP Pallet_00_Slot0_Tier0 & Pallet_01_Slot0_Tier1 @(26, 87)  <<HISTORY: d6 06:02 picked 1 (left 27) by InventoryService.PickFromPallet | d6 06:03 picked 1 (left 26) by InventoryService.PickFromPallet | d6 06:04 picked 1 (left 25) by InventoryService.PickFromPallet | d6 06:04 picked 1 (left 24) by InventoryService.PickFromPallet | d6 06:05 picked 1 (left 23) by InventoryService.PickFromPallet | d6 06:06 picked 1 (left 22) by InventoryService.PickFromPallet>>  <<HISTORY: d4 09:28 picked 1 (left 4) by InventoryService.PickFromPallet | d4 09:32 picked 1 (left 3) by InventoryService.PickFromPallet | d5 06:45 picked 1 (left 2) by InventoryService.PickFromPallet | d5 06:46 picked 1 (left 1) by InventoryService.PickFromPallet | d5 06:47 picked 1 (left 0) by InventoryService.PickFromPallet | d5 06:47 record destroyed by InventoryService.DestroyPallet>>
- **WARN** (day 7): [AiNavigation] Employee_Ann Reyes: task target (-31.19, 1.11, 45.59) is not reachable — the partial path ends 10.23m short of it. Handing the task back.
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 1243a4b4-5103-4baa-bfb8-c51a83a2c9ac (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 19/130 case(s).
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 1243a4b4-5103-4baa-bfb8-c51a83a2c9ac (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 69/130.

## Bot actions (first 400)
