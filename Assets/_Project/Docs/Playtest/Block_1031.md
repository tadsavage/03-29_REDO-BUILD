# Playtest block 1031 — days 1–8

## Score: **61.5** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (19 shipped, 5 late) | 15% | 73.7 |
| Fill rate | 10% | 96.1 |
| Throughput (183 plt vs target 84) | 15% | 100.0 |
| Revenue ($176,194) | 15% | 100.0 |
| Profit (cash -370,290) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 3.3 |
| Reputation (0 to 235) | 5% | 100.0 |

Orders shipped **19**, late/fined **5**, pallets shipped **183**. Cash $500,000 → $129,710 (-370,290). Stock at cost $0 → $67,470 (not part of profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Late orders - state when it came due
- 3 x loaded but the truck had not left
- 2 x still being picked

- day 3: OG4414 (Salty Sailor Pretzel Co.) was loaded but the truck had not left. Fined $11,303. Lines 2, 1055 cs, released=True.
- day 3: OG4413 (The Happy Herd Co.) was loaded but the truck had not left. Fined $2,438. Lines 2, 394 cs, released=True.
- day 5: OG8421 (Chubby Chipmunk Cookies) was loaded but the truck had not left. Fined $1,061. Lines 1, 283 cs, released=True.
- day 5: OG7422 (Happy Cow Creamery) was still being picked (336/336 cs). Fined $1,830. Lines 2, 336 cs, released=True.
- day 6: OG9426 (Puff & Stuff Pastries) was still being picked (120/120 cs). Fined $840. Lines 1, 120 cs, released=True.

## Bugs found (80 distinct, 226 incidents)

| Incidents | Bug |
|---|---|
| 20 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 19 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 12 | FLOATING —  Pallet_#_Slot#_Tier# @#D# bottom=# |
| 12 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#D# |
| 12 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 10 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 10 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 8 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 5 | FLOATING —  Pallet_#_Slot#_Tier# @#E# bottom=# |
| 5 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#E# |
| 3 | FLOATING —  OutboundPallet_#-b#f#de####_# @#C# bottom=# |
| 3 | OVERLAP —  OutboundPallet_#-ef##-aa#-#_# & OutboundPallet_#-ef##-aa#-#_# @#A# |
| 3 | FLOATING —  OutboundPallet_#-ef##-aa#-#_# @#A# bottom=# |
| 3 | FLOATING —  OutboundPallet_###a#-#_# @#B# bottom=# |
| 3 | OVERLAP —  OutboundPallet_#-cb#f#f#c-b#d##_# & OutboundPallet_#-cb#f#f#c-b#d##_# @#C# |
| 3 | FLOATING —  OutboundPallet_#-cb#f#f#c-b#d##_# @#C# bottom=# |
| 2 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-b#f#de####_# @#C# |
| 2 | OVERLAP —  OutboundPallet_#-b#f#de####_# & OutboundPallet_#-b#f#de####_# @#C# |
| 2 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 2 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_###a#-#_# @#B# |
| 2 | FLOATING —  OutboundPallet_##bf##eb###_# @#A# bottom=# |
| 2 | OVERLAP —  OutboundPallet_##bf##eb###_# & OutboundPallet_##bf##eb###_# @#A# |
| 5 | WARN — [OrderSelectionTaskDriver] Order #-cb#f#f#c-b#d## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 2 | STALE-ASSIGNED —  Putaway Putaway pallet [#] staged at #A# #s |
| 2 | STALE-ASSIGNED —  Receive Receive pallet # (# cases) #s |
| 4 | WARN — [AiNavigation] Employee_Eric Ritter: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 4 | WARN — [OrderSelectionTaskDriver] Order #-dda##a#c-a#a-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — Assertion failed |
| 3 | WARN — [AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | TRUCK-STALLED —  Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #) |
| 3 | WARN — [OrderSelectionTaskDriver] Order #-cb#f#f#c-b#d## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | OVERLAP —  OutboundPallet_###eb-a#-#_# & OutboundPallet_###eb-a#-#_# @#A# |
| 1 | FLOATING —  OutboundPallet_###eb-a#-#_# @#A# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##e#c#b#b-be#c-#_# @#B# |
| 3 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-d#b##f-acf###_# @#A# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-cb#f#f#c-b#d##_# @#B# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-cb#f#f#c-b#d##_# @#C# |
| 1 | FLOATING —  OutboundPallet_#-cb#f#f#c-b#d##_# @#A# bottom=# |
| 1 | OVERLAP —  OutboundPallet_#-cb#f#f#c-b#d##_# & OutboundPallet_#-cb#f#f#c-b#d##_# @#A# |
| 1 | FLOATING —  OutboundPallet_#-cb#f#f#c-b#d##_# @#B# bottom=# |
| 1 | OVERLAP —  OutboundPallet_###a#-#_# & OutboundPallet_###a#-#_# @#B# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-e##-b###_# @#A# |
| 1 | FLOATING —  OutboundPallet_#-e##-b###_# @#A# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##bf##eb###_# @#A# |
| 1 | FLOATING —  OutboundPallet_##bf##eb###_# @#B# bottom=# |
| 1 | OVERLAP —  OutboundPallet_##bf##eb###_# & OutboundPallet_##bf##eb###_# @#B# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-dda##a#c-a#a-#_# @#A# |
| 1 | OVERLAP —  OutboundPallet_#-dda##a#c-a#a-#_# & OutboundPallet_#-dda##a#c-a#a-#_# @#A# |
| 1 | FLOATING —  OutboundPallet_#-dda##a#c-a#a-#_# @#A# bottom=# |
| 1 | OVERLAP —  OutboundPallet_#-dda##a#c-a#a-#_# & OutboundPallet_#-dda##a#c-a#a-#_# @#B# |
| 1 | FLOATING —  OutboundPallet_#-dda##a#c-a#a-#_# @#B# bottom=# |
| 1 | REPEATING — x# and counting: Attempting to render to a depth only surface with no dummy color attachment |
| 1 | REPEATING — x# and counting: Assertion failed |
| 1 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 2 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Joseph Jackson' as its operator though nobody is on it or running it - clearing the stale link. |
| 2 | WARN — [OrderSelectionTaskDriver] Order ###a#-# (Puff & Stuff Pastries) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | REPEATING — x# and counting: [OrderService] Order OG#: # case(s) not in stock - cut from the order, not billed. |
| 1 | TRUCK-OVERLAP —  Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #) |
| 1 | REPEATING — x# and counting: [OrderService] Order OG#: stock is back for # case(s) that were cut - filed another selector trip. |
| 1 | ORPHAN-OUTBOUND —  #B# (order ###a#-# gone) |
| 2 | WARN — [OrderSelectionTaskDriver] Order ##bf##eb### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | ORPHAN-OUTBOUND —  #A# (order ##d##fe#-aaaf## gone) |
| 2 | WARN — [OrderSelectionTaskDriver] Order #-dda##a#c-a#a-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###cc##b#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-ef##-aa#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Employee_Brian Johnson cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f###c### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##e#c#b#b-be#c-# (The Happy Herd Co.) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [AiNavigation] PalletJack cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###a#-# (Puff & Stuff Pastries) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e##-b### (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##bf##eb### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-b#c#-af#b## (Happy Cow Creamery) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##dae#-bb#a-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [ReachTruckOperator] Replenish: could not reach pallet at ##-A# within #m. |

## First sightings (with stack traces)

- **REPEATING** (day 1): x25 and counting: Attempting to render to a depth only surface with no dummy color attachment
- **ERROR** (day 1): Assertion failed
  ```
  UnityEngine.UIElements.UIElementsRuntimeUtilityNative:RepaintPanels (bool)
  
  ```
- **REPEATING** (day 1): x25 and counting: Assertion failed
- **TRUCK-OVERLAP** (day 1): TRUCK-OVERLAP Truck→PO_IG1649 & Truck→PO_IG1651 at (-3.5, 0.0, 18.5)
- **STALE-ASSIGNED** (day 1): STALE-ASSIGNED Putaway Putaway pallet [2057072448] staged at 2A-3 400s
- **STALE-ASSIGNED** (day 1): STALE-ASSIGNED Receive Receive pallet 421698 (55 cases) 401s
- **WARN** (day 1): [AiNavigation] Employee_Eric Ritter: stuck at (7.78, 1.16, 46.50) — walking out to (1.31, 1.08, 35.17) (12.57m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet c8e195ba-2716-4cc0-97b4-a6cd02774cd9 but the employee is 13.5m away (max 4.5m) — refusing to start receiving.
- **FLOATING** (day 2): FLOATING Pallet_04_Slot2_Tier0 @2B-3 bottom=1.93  <<HISTORY: d2 02:03 Putaway task created (STG3A-2->) by OrderService.RefileStrandedLanePallets | d2 02:26 moved (53, 82)->(-1, -1) by InventoryService.MovePallet | d2 02:34 adopted as outbound freight of order 6b636fe0-08fc-45ce-9558-a75499fd7554 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 02:34 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 02:34 record destroyed by InventoryService.DestroyPallet | d2 02:34 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **OVERLAP** (day 2): OVERLAP Pallet_04_Slot2_Tier0 & Pallet_03_Slot1_Tier1 @2B-3  <<HISTORY: d2 02:03 Putaway task created (STG3A-2->) by OrderService.RefileStrandedLanePallets | d2 02:26 moved (53, 82)->(-1, -1) by InventoryService.MovePallet | d2 02:34 adopted as outbound freight of order 6b636fe0-08fc-45ce-9558-a75499fd7554 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 02:34 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 02:34 record destroyed by InventoryService.DestroyPallet | d2 02:34 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d2 02:15 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d2 02:16 adopted as outbound freight of order 6b636fe0-08fc-45ce-9558-a75499fd7554 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 02:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 02:16 record destroyed by InventoryService.DestroyPallet | d2 02:16 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 02:16 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **OVERLAP** (day 2): OVERLAP Pallet_07_Slot3_Tier1 & Pallet_06_Slot3_Tier0 @3A-1  <<HISTORY: d2 02:11 Putaway task created (STG3A-1->) by ReceiverReceivingWorkflow.CreatePutawayTask | d2 02:11 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 02:11 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 02:11 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d2 02:17 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 02:18 Putaway task created (STG3A-1->) by OrderService.RefileStrandedLanePallets>>  <<HISTORY: d2 02:39 Putaway task created (STG3A-1->) by ReceiverReceivingWorkflow.CreatePutawayTask | d2 02:39 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 02:39 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 02:39 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d2 02:42 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 02:45 Putaway task created (STG3A-1->) by OrderService.RefileStrandedLanePallets>>
- **FLOATING** (day 2): FLOATING Pallet_06_Slot3_Tier0 @3A-1 bottom=1.90  <<HISTORY: d2 02:39 Putaway task created (STG3A-1->) by ReceiverReceivingWorkflow.CreatePutawayTask | d2 02:39 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 02:39 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 02:39 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d2 02:42 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 02:45 Putaway task created (STG3A-1->) by OrderService.RefileStrandedLanePallets>>
- **WARN** (day 2): [AiNavigation] ReachTruck cannot reach task position at (-45.76, 1.11, 43.01) (path invalid) — abandoning the walk and handing the task back.
- **FLOATING** (day 2): FLOATING Pallet_00_Slot0_Tier0 @2D-4 bottom=1.94  <<HISTORY: d2 01:26 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 01:30 moved (53, 85)->(-1, -1) by InventoryService.MovePallet | d2 01:35 adopted as outbound freight of order 6b636fe0-08fc-45ce-9558-a75499fd7554 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 01:35 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 01:35 record destroyed by InventoryService.DestroyPallet | d2 01:35 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **OVERLAP** (day 2): OVERLAP Pallet_00_Slot0_Tier0 & Pallet_01_Slot0_Tier1 @2D-4  <<HISTORY: d2 01:26 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 01:30 moved (53, 85)->(-1, -1) by InventoryService.MovePallet | d2 01:35 adopted as outbound freight of order 6b636fe0-08fc-45ce-9558-a75499fd7554 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 01:35 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 01:35 record destroyed by InventoryService.DestroyPallet | d2 01:35 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d1 21:55 moved (50, 85)->(-1, -1) by InventoryService.MovePallet | d1 21:57 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 22:01 adopted as outbound freight of order 6b636fe0-08fc-45ce-9558-a75499fd7554 (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d1 22:01 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 22:01 record destroyed by InventoryService.DestroyPallet | d1 22:01 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **FLOATING** (day 2): FLOATING Pallet_07_Slot3_Tier1 @2C-1 bottom=1.90  <<HISTORY: d2 02:57 Putaway task created (STG3A-1->) by OrderService.RefileStrandedLanePallets | d2 03:14 moved (53, 81)->(-1, -1) by InventoryService.MovePallet | d2 03:24 adopted as outbound freight of order 6b636fe0-08fc-45ce-9558-a75499fd7554 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 03:24 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 03:24 record destroyed by InventoryService.DestroyPallet | d2 03:24 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **OVERLAP** (day 2): OVERLAP Pallet_07_Slot3_Tier1 & Pallet_06_Slot3_Tier0 @2C-1  <<HISTORY: d2 02:57 Putaway task created (STG3A-1->) by OrderService.RefileStrandedLanePallets | d2 03:14 moved (53, 81)->(-1, -1) by InventoryService.MovePallet | d2 03:24 adopted as outbound freight of order 6b636fe0-08fc-45ce-9558-a75499fd7554 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 03:24 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 03:24 record destroyed by InventoryService.DestroyPallet | d2 03:24 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d2 02:45 Putaway task created (STG3A-1->) by OrderService.RefileStrandedLanePallets | d2 03:03 moved (53, 81)->(-1, -1) by InventoryService.MovePallet | d2 03:13 adopted as outbound freight of order 6b636fe0-08fc-45ce-9558-a75499fd7554 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 03:13 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 03:13 record destroyed by InventoryService.DestroyPallet | d2 03:13 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 936dd9d9-ef92-4443-aa71-c78188060e76 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 260/273.
- **TRUCK-STALLED** (day 2): TRUCK-STALLED Truck→Outbound_Door2 state=DepartToApproach 270s at (-2.0, 0.0, 27.9)
- **OVERLAP** (day 2): OVERLAP Pallet_01_Slot1_Tier0 & OutboundPallet_b0af284e-b15f-4de2-8803-3e7d09acf7d3_2 @1C-2  <<HISTORY: d1 15:34 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d1 20:59 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 21:05 moved (47, 84)->(-1, -1) by InventoryService.MovePallet | d1 21:14 adopted as outbound freight of order b0af284e-b15f-4de2-8803-3e7d09acf7d3 (8 cs) by OutboundPalletBuilder.AdoptFullPallet | d1 21:14 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 21:14 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 2): OVERLAP OutboundPallet_b0af284e-b15f-4de2-8803-3e7d09acf7d3_1 & OutboundPallet_b0af284e-b15f-4de2-8803-3e7d09acf7d3_2 @1C-4
- **FLOATING** (day 2): FLOATING OutboundPallet_b0af284e-b15f-4de2-8803-3e7d09acf7d3_2 @1C-3 bottom=2.29
- **WARN** (day 2): [AiNavigation] Dockstocker cannot reach task position at (-44.44, 1.17, 37.71) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 2): [AiNavigation] Employee_Brian Johnson cannot reach task position at (5.23, 1.11, 49.96) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 453dc099-f297-4741-8c96-7dd6561a0da6 but the employee is 5.9m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 1cc436d7-cb1f-4f0c-b59d-040f5bfdb785 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 103/301 case(s).
- **OVERLAP** (day 3): OVERLAP Pallet_04_Slot3_Tier0 & Pallet_02_Slot2_Tier0 @(25, 82)  <<HISTORY: d3 02:39 picked 1 (left 4) by InventoryService.PickFromPallet | d3 02:40 picked 1 (left 3) by InventoryService.PickFromPallet | d3 02:41 picked 1 (left 2) by InventoryService.PickFromPallet | d3 02:41 picked 1 (left 1) by InventoryService.PickFromPallet | d3 02:42 picked 1 (left 0) by InventoryService.PickFromPallet | d3 02:42 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d2 16:25 Putaway task Available->Complete by WorkQueueSystem.CompleteTask | d3 02:44 Replenish task created (01-12-A0->01-12-00) by ReplenishmentService.CreateReplenishTask | d3 02:53 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:09 moved (25, 82)->(-1, -1) by InventoryService.MovePallet | d3 03:19 moved (-1, -1)->(25, 82) by InventoryService.MovePallet | d3 03:19 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **OVERLAP** (day 3): OVERLAP OutboundPallet_cf34f912-2556-44eb-a697-ca3b126f9890_1 & OutboundPallet_cf34f912-2556-44eb-a697-ca3b126f9890_2 @3A-1
- **FLOATING** (day 3): FLOATING OutboundPallet_cf34f912-2556-44eb-a697-ca3b126f9890_2 @3A-1 bottom=2.27
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 1cc436d7-cb1f-4f0c-b59d-040f5bfdb785 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 171/301.
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 8cc67bad-0e3c-4b9b-be9c-da872992f0f3 (The Happy Herd Co.) has no reachable pick location for its remaining line item(s) — staging short at 288/394 case(s).
- **FLOATING** (day 3): FLOATING Pallet_08_Slot4_Tier0 @1E-2 bottom=2.06  <<HISTORY: d3 02:08 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:18 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 02:28 moved (56, 85)->(-1, -1) by InventoryService.MovePallet | d3 02:39 adopted as outbound freight of order 8cc67bad-0e3c-4b9b-be9c-da872992f0f3 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 02:39 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 02:39 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 3): OVERLAP Pallet_08_Slot4_Tier0 & Pallet_02_Slot1_Tier0 @1E-2  <<HISTORY: d3 02:08 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:18 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 02:28 moved (56, 85)->(-1, -1) by InventoryService.MovePallet | d3 02:39 adopted as outbound freight of order 8cc67bad-0e3c-4b9b-be9c-da872992f0f3 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 02:39 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 02:39 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d3 00:48 Putaway task created (STG3C-3->) by OrderService.RefileStrandedLanePallets | d3 01:43 moved (55, 84)->(-1, -1) by InventoryService.MovePallet | d3 01:53 adopted as outbound freight of order 8cc67bad-0e3c-4b9b-be9c-da872992f0f3 (52 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:53 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:53 record destroyed by InventoryService.DestroyPallet | d3 01:53 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **OVERLAP** (day 3): OVERLAP Pallet_07_Slot3_Tier1 & OutboundPallet_8cc67bad-0e3c-4b9b-be9c-da872992f0f3_1 @1B-2  <<HISTORY: d3 06:03 PalletPick task created (STG3C-1->1E) by OrderService.RefilePalletPickShortfalls | d3 06:40 moved (55, 81)->(-1, -1) by InventoryService.MovePallet | d3 06:52 adopted as outbound freight of order 8cc67bad-0e3c-4b9b-be9c-da872992f0f3 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 06:52 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 06:52 record destroyed by InventoryService.DestroyPallet | d3 06:52 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **WARN** (day 3): [AiNavigation] PalletJack cannot reach task position at (-38.64, 1.16, 47.54) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 43d55e0e-6501-467a-8387-ed3d7a73d2e7 (Puff & Stuff Pastries) has no reachable pick location for its remaining line item(s) — staging short at 131/657 case(s).
- **OVERLAP** (day 3): OVERLAP OutboundPallet_936dd9d9-ef92-4443-aa71-c78188060e76_2 & OutboundPallet_936dd9d9-ef92-4443-aa71-c78188060e76_1 @1A-4
- **FLOATING** (day 3): FLOATING OutboundPallet_936dd9d9-ef92-4443-aa71-c78188060e76_1 @1A-3 bottom=2.27
- **WARN** (day 3): [AiNavigation] Dockstocker: stuck at (1.04, 1.11, 49.04) — walking out to (-11.96, 1.16, 49.04) (12.00m to mesh).
- **WARN** (day 3): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Joseph Jackson' as its operator though nobody is on it or running it - clearing the stale link.
- **WARN** (day 4): [AiNavigation] ReachTruck: stuck at (3.59, 1.11, 51.00) — walking out to (-9.41, 1.16, 51.00) (12.00m to mesh).
- **OVERLAP** (day 4): OVERLAP Pallet_11_Slot6_Tier1 & OutboundPallet_1f2de374-d0b8-482f-acf8-35f93db8e855_1 @2A-4  <<HISTORY: d4 00:58 moved (48, 85)->(-1, -1) by InventoryService.MovePallet | d4 00:59 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d4 01:02 adopted as outbound freight of order 1f2de374-d0b8-482f-acf8-35f93db8e855 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 01:02 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 01:02 record destroyed by InventoryService.DestroyPallet | d4 01:02 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **WARN** (day 4): [OrderSelectionTaskDriver] Order 43d55e0e-6501-467a-8387-ed3d7a73d2e7 (Puff & Stuff Pastries) has no reachable pick location for its remaining line item(s) — staging short at 411/657.
- **REPEATING** (day 4): x25 and counting: [OrderService] Order OG6417: 14 case(s) not in stock - cut from the order, not billed.
- **TRUCK-OVERLAP** (day 4): TRUCK-OVERLAP Truck→Outbound_Door2 & Truck→Outbound_Door1 at (-3.4, 0.0, 18.4)
- **OVERLAP** (day 4): OVERLAP Pallet_01_Slot1_Tier0 & OutboundPallet_43d55e0e-6501-467a-8387-ed3d7a73d2e7_1 @2B-3  <<HISTORY: d3 17:25 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 17:45 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 17:52 moved (53, 82)->(-1, -1) by InventoryService.MovePallet | d3 18:00 adopted as outbound freight of order 43d55e0e-6501-467a-8387-ed3d7a73d2e7 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 18:00 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 18:00 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 4): FLOATING OutboundPallet_43d55e0e-6501-467a-8387-ed3d7a73d2e7_1 @2B-3 bottom=2.41
- **OVERLAP** (day 4): OVERLAP Pallet_00_Slot0_Tier0 & OutboundPallet_1cc436d7-cb1f-4f0c-b59d-040f5bfdb785_1 @1B-1  <<HISTORY: d3 15:42 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 15:42 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 15:50 moved (48, 82)->(-1, -1) by InventoryService.MovePallet | d3 16:00 adopted as outbound freight of order 1cc436d7-cb1f-4f0c-b59d-040f5bfdb785 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 16:00 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 16:00 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 4): OVERLAP Pallet_03_Slot2_Tier1 & OutboundPallet_1cc436d7-cb1f-4f0c-b59d-040f5bfdb785_1 @1C-4  <<HISTORY: d3 14:42 Putaway task created (STG2C-3->) by OrderService.RefileStrandedLanePallets | d3 15:19 moved (48, 84)->(-1, -1) by InventoryService.MovePallet | d3 15:25 adopted as outbound freight of order 1cc436d7-cb1f-4f0c-b59d-040f5bfdb785 (8 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 15:25 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 15:25 record destroyed by InventoryService.DestroyPallet | d3 15:25 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **OVERLAP** (day 4): OVERLAP OutboundPallet_1cc436d7-cb1f-4f0c-b59d-040f5bfdb785_1 & OutboundPallet_1cc436d7-cb1f-4f0c-b59d-040f5bfdb785_1 @1C-2
- **FLOATING** (day 4): FLOATING OutboundPallet_1cc436d7-cb1f-4f0c-b59d-040f5bfdb785_1 @1C-1 bottom=2.19
- **FLOATING** (day 4): FLOATING OutboundPallet_1cc436d7-cb1f-4f0c-b59d-040f5bfdb785_1 @1A-3 bottom=1.81
- **OVERLAP** (day 4): OVERLAP OutboundPallet_1cc436d7-cb1f-4f0c-b59d-040f5bfdb785_1 & OutboundPallet_1cc436d7-cb1f-4f0c-b59d-040f5bfdb785_1 @1A-3
- **FLOATING** (day 4): FLOATING OutboundPallet_1cc436d7-cb1f-4f0c-b59d-040f5bfdb785_1 @1B-1 bottom=2.57
- **REPEATING** (day 4): x25 and counting: [OrderService] Order OG6417: stock is back for 52 case(s) that were cut - filed another selector trip.
- **OVERLAP** (day 4): OVERLAP OutboundPallet_43d55e0e-6501-467a-8387-ed3d7a73d2e7_1 & OutboundPallet_43d55e0e-6501-467a-8387-ed3d7a73d2e7_2 @2B-1
- **WARN** (day 4): [OrderService] Order OG6417 closed with a STAGED pallet (42 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 2B-1 (order 43d55e0e-6501-467a-8387-ed3d7a73d2e7 gone)
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 2f301e10-5bf4-49eb-8496-67bdc7a92494 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 71/480 case(s).
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 7088c6b5-e816-4102-b421-0e92fa36ddc8 (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at 70/283.
- **OVERLAP** (day 5): OVERLAP Pallet_04_Slot2_Tier0 & OutboundPallet_7088c6b5-e816-4102-b421-0e92fa36ddc8_1 @3A-2  <<HISTORY: d5 15:31 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 15:35 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 15:42 moved (48, 85)->(-1, -1) by InventoryService.MovePallet | d5 15:50 adopted as outbound freight of order 7088c6b5-e816-4102-b421-0e92fa36ddc8 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 15:50 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 15:50 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 5): FLOATING OutboundPallet_7088c6b5-e816-4102-b421-0e92fa36ddc8_1 @3A-2 bottom=2.06
- **OVERLAP** (day 5): OVERLAP Pallet_00_Slot0_Tier0 & OutboundPallet_2f301e10-5bf4-49eb-8496-67bdc7a92494_1 @1A-3  <<HISTORY: d5 06:40 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 06:50 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 07:01 moved (46, 85)->(-1, -1) by InventoryService.MovePallet | d5 07:08 adopted as outbound freight of order 2f301e10-5bf4-49eb-8496-67bdc7a92494 (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 07:08 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 07:08 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 5): FLOATING OutboundPallet_2f301e10-5bf4-49eb-8496-67bdc7a92494_1 @1A-4 bottom=2.55
- **OVERLAP** (day 5): OVERLAP OutboundPallet_2f301e10-5bf4-49eb-8496-67bdc7a92494_1 & OutboundPallet_2f301e10-5bf4-49eb-8496-67bdc7a92494_1 @1A-4
- **FLOATING** (day 5): FLOATING OutboundPallet_2f301e10-5bf4-49eb-8496-67bdc7a92494_2 @1B-4 bottom=2.09
- **OVERLAP** (day 5): OVERLAP OutboundPallet_2f301e10-5bf4-49eb-8496-67bdc7a92494_2 & OutboundPallet_2f301e10-5bf4-49eb-8496-67bdc7a92494_1 @1B-4
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 2f301e10-5bf4-49eb-8496-67bdc7a92494 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 476/480.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 6118902d-b07c-4778-af5b-8e876036d6bf (Happy Cow Creamery) has no reachable pick location for its remaining line item(s) — staging short at 305/336 case(s).
- **WARN** (day 6): [OrderSelectionTaskDriver] Order a5714df9-6dae-4923-bb6a-a561659db3e0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 83/277 case(s).
- **WARN** (day 6): [OrderService] Order OG9426 closed with a IN TRANSIT pallet (20 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 7): ORPHAN-OUTBOUND 3A-1 (order e4da9725-2d68-4fe0-aaaf-9f4f420cc08d gone)
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 9fd26337-dda0-4a9c-a21a-b1b2675a3093 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 46/285 case(s).
- **WARN** (day 7): [ReachTruckOperator] Replenish: could not reach pallet at 01-09-A0 within 0.15m.
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 9fd26337-dda0-4a9c-a21a-b1b2675a3093 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 65/285.
- **OVERLAP** (day 7): OVERLAP Pallet_03_Slot2_Tier1 & OutboundPallet_9fd26337-dda0-4a9c-a21a-b1b2675a3093_1 @1A-4  <<HISTORY: d7 05:18 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d7 05:57 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 06:06 moved (40, 84)->(-1, -1) by InventoryService.MovePallet | d7 06:13 adopted as outbound freight of order 9fd26337-dda0-4a9c-a21a-b1b2675a3093 (8 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 06:13 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 06:13 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 7): OVERLAP OutboundPallet_9fd26337-dda0-4a9c-a21a-b1b2675a3093_1 & OutboundPallet_9fd26337-dda0-4a9c-a21a-b1b2675a3093_1 @1A-3
- **FLOATING** (day 7): FLOATING OutboundPallet_9fd26337-dda0-4a9c-a21a-b1b2675a3093_1 @1A-3 bottom=2.17
- **OVERLAP** (day 7): OVERLAP OutboundPallet_9fd26337-dda0-4a9c-a21a-b1b2675a3093_1 & OutboundPallet_9fd26337-dda0-4a9c-a21a-b1b2675a3093_2 @1B-1
- **FLOATING** (day 7): FLOATING OutboundPallet_9fd26337-dda0-4a9c-a21a-b1b2675a3093_2 @1B-1 bottom=2.42

## Bot actions (first 400)
