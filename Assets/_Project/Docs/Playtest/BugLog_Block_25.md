# Bug log - block 25

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 12:06 | **WARN** | [AiNavigation] Employee_Barbara Johnson: stuck at (16.46, 1.15, 25.63) — walking out to (16.46, 0.04, 13.00) (11.63m to mesh).
- day 1 19:55 | **WARN** | [AiNavigation] Employee_Barbara Johnson: stuck at (-0.03, 1.16, 24.03) — walking out to (-11.27, 1.14, 31.17) (12.31m to mesh).
- day 1 20:58 | **WARN** | [AiNavigation] Employee_Barbara Johnson: stuck at (-0.21, 1.16, 21.32) — walking out to (-11.43, 1.08, 14.00) (12.56m to mesh).
- day 1 21:20 | **WARN** | [AiNavigation] ReachTruck: stuck at (-0.95, 1.11, 19.45) — walking out to (-0.95, 0.08, 12.17) (6.36m to mesh).
- day 1 21:24 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (-0.95, 0.36, 12.84) (cell (49, 59)). Putting it back on its last inside spot (-0.95, 0.64, 13.51). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 2 02:27 | **NO-TASK** | NO-TASK lane pallet 2B-2 sku=801236 load=4501819977  <<HISTORY: d1 18:47 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:42 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d1 19:42 PalletPick task created (STG2B-2->) by OrderService.FileReleasedOrderTasks | d1 19:42 PalletPick task Available->Open by OrderService.FileReleasedOrderTasks | d1 19:42 PalletPick task Open->Available by OrderService.AllocateHeldWork | d2 02:26 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 2 19:12 | **OVERLAP** | OVERLAP Pallet_10_Slot8_Tier0 & Pallet_00_Slot0_Tier0 @2E-1  <<HISTORY: d1 20:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d1 22:36 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 22:49 moved (53, 65)->(-1, -1) by InventoryService.MovePallet | d1 22:59 adopted as outbound freight of order 667e534d-e975-4912-9848-e11032ff5765 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d1 22:59 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 22:59 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d1 20:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d1 20:50 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 21:05 moved (34, 62)->(-1, -1) by InventoryService.MovePallet | d1 21:37 adopted as outbound freight of order 667e534d-e975-4912-9848-e11032ff5765 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d1 21:37 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 21:37 record destroyed by InventoryService.DestroyPallet>>
- day 2 19:12 | **OVERLAP** | OVERLAP OutboundPallet_667e534d-e975-4912-9848-e11032ff5765_1 & OutboundPallet_667e534d-e975-4912-9848-e11032ff5765_2 @2D-5
- day 3 02:57 | **OVERLAP** | OVERLAP Pallet_00_Slot0_Tier0 & Pallet_01_Slot1_Tier0 @3A-5  <<HISTORY: d3 02:37 registered at (61, 69) 16x172495 by InventoryService.RegisterPhysicalPallet | d3 02:37 Receive task created (->STG3A-5) by ReceivingService.CreateReceiveTaskForPallet | d3 02:37 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>  <<HISTORY: d3 02:52 registered at (61, 69) 16x172495 by InventoryService.RegisterPhysicalPallet | d3 02:52 Receive task created (->STG3A-5) by ReceivingService.CreateReceiveTaskForPallet | d3 02:52 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 3 02:57 | **FLOATING** | FLOATING Pallet_01_Slot1_Tier0 @3A-5 bottom=2.57  <<HISTORY: d3 02:52 registered at (61, 69) 16x172495 by InventoryService.RegisterPhysicalPallet | d3 02:52 Receive task created (->STG3A-5) by ReceivingService.CreateReceiveTaskForPallet | d3 02:52 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 3 20:10 | **WARN** | [AiNavigation] Employee_Barbara Johnson: stuck at (5.89, 1.14, 25.56) — walking out to (18.34, 1.13, 32.02) (13.02m to mesh).
- day 4 05:44 | **WARN** | [ReachTruckOperator] Replenish: could not reach pallet at 02-09-01 within 0.15m.
- day 4 21:27 | **DOOR-DEADLOCK** | OG4771 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 4 22:12 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 135 min with no one loading it.
- day 5 00:42 | **DOOR-DEADLOCK** | OG4771 is Staged but door 1's truck has waited 285 min with no one loading it.
- day 5 02:59 | **REPEATING** | x25 and counting: [EquipmentBattery] Dockstocker: not sliding 9.9 m by hand - that is a navigation job. Trying the path again.
- day 5 03:42 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 465 min with no one loading it.
- day 5 06:12 | **DOOR-DEADLOCK** | OG4771 is Staged but door 1's truck has waited 615 min with no one loading it.
- day 5 08:57 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 5 09:12 | **DOOR-DEADLOCK** | OG4771 is Staged but door 1's truck has waited 105 min with no one loading it.
- day 5 10:12 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 165 min with no one loading it.
- day 5 12:27 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 300 min with no one loading it.
- day 5 14:57 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 450 min with no one loading it.
- day 5 17:12 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 585 min with no one loading it.
- day 5 19:27 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 720 min with no one loading it.
- day 5 21:57 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 870 min with no one loading it.
- day 6 00:12 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 1005 min with no one loading it.
- day 6 02:57 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 1170 min with no one loading it.
- day 6 05:27 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 1320 min with no one loading it.
- day 6 07:57 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 1470 min with no one loading it.
- day 6 11:42 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 1695 min with no one loading it.
- day 6 14:42 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 1875 min with no one loading it.
- day 6 16:27 | **OVERLAP** | OVERLAP Pallet_00_Slot0_Tier0 & Pallet_06_Slot6_Tier0 @1C-1  <<HISTORY: d6 00:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d6 00:12 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d6 00:24 moved (42, 76)->(-1, -1) by InventoryService.MovePallet | d6 00:34 adopted as outbound freight of order e96882d6-aa45-489a-90ed-842465ecbf51 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d6 00:34 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d6 00:34 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d6 00:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d6 00:12 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d6 00:22 moved (34, 76)->(-1, -1) by InventoryService.MovePallet | d6 00:39 adopted as outbound freight of order e96882d6-aa45-489a-90ed-842465ecbf51 (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d6 00:39 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d6 00:39 record destroyed by InventoryService.DestroyPallet>>
- day 6 16:27 | **FLOATING** | FLOATING Pallet_06_Slot6_Tier0 @1C-1 bottom=2.08  <<HISTORY: d6 00:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d6 00:12 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d6 00:22 moved (34, 76)->(-1, -1) by InventoryService.MovePallet | d6 00:39 adopted as outbound freight of order e96882d6-aa45-489a-90ed-842465ecbf51 (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d6 00:39 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d6 00:39 record destroyed by InventoryService.DestroyPallet>>
- day 6 16:27 | **FLOATING** | FLOATING OutboundPallet_e96882d6-aa45-489a-90ed-842465ecbf51_1 @1C-2 bottom=1.81
- day 6 16:27 | **OVERLAP** | OVERLAP OutboundPallet_e96882d6-aa45-489a-90ed-842465ecbf51_1 & OutboundPallet_e96882d6-aa45-489a-90ed-842465ecbf51_1 @1C-2
- day 6 16:27 | **OVERLAP** | OVERLAP OutboundPallet_e96882d6-aa45-489a-90ed-842465ecbf51_1 & OutboundPallet_e96882d6-aa45-489a-90ed-842465ecbf51_2 @1C-3
- day 6 17:12 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 2025 min with no one loading it.
- day 6 19:42 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 2175 min with no one loading it.
- day 6 21:57 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 2310 min with no one loading it.
- day 7 00:12 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 2445 min with no one loading it.
- day 7 02:57 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 2610 min with no one loading it.
- day 7 05:27 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 2760 min with no one loading it.
- day 7 08:57 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 2970 min with no one loading it.
- day 7 10:42 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 3075 min with no one loading it.
- day 7 12:42 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 3195 min with no one loading it.
- day 7 14:42 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 3315 min with no one loading it.
- day 7 16:57 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 3450 min with no one loading it.
- day 7 18:08 | **REPEATING** | x25 and counting: [TrailerLoad] 1B: door end blocked (the pallet at pos3 belongs to order 6ee03fae-659c-4efb-855a-6f475b24161d, not one being loaded here); the far end gives 0 - stopping with 0 (the pallet at pos3 belongs to order 6ee03fae-659c-4efb-855a-6f475b24161d, not one being loaded here).
- day 7 18:35 | **REPEATING** | x25 and counting: [OrderService] Order b1d21933-8a18-4d87-a823-c439da435ce5 (Sneaky Pete's Seafood) was released to loading from 1B but no staged pallet for it could be found there — reverted to Staged so it can be re-released. Its pallets are most likely sitting somewhere other than the lane.
- day 7 18:35 | **REPEATING** | x25 and counting: [TrailerLoad] 1 order(s) at 1B had no staged pallets to load — reverted to Staged.
- day 7 18:42 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 3555 min with no one loading it.
- day 7 20:42 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 3675 min with no one loading it.
- day 7 22:42 | **DOOR-DEADLOCK** | OG4771 is Loading but door 1's truck has waited 3795 min with no one loading it.
