# Bug log - block 29

Every incident as it happened. Format: day hour:minute | category | what.

- day 6 21:22 | **OVERLAP** | OVERLAP A Chep & A Chep @3B-1  <<HISTORY: d6 21:22 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d6 21:22 adopted as outbound freight of order 81c8252a-3187-422a-a999-94e80f1c647b (40 cs) by OutboundPalletBuilder.AdoptFullPallet>>  <<HISTORY: d6 21:22 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d6 21:22 adopted as outbound freight of order 81c8252a-3187-422a-a999-94e80f1c647b (24 cs) by OutboundPalletBuilder.AdoptFullPallet>>
- day 6 21:22 | **FLOATING** | FLOATING A Chep @3B-1 bottom=1.30  <<HISTORY: d6 21:22 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d6 21:22 adopted as outbound freight of order 81c8252a-3187-422a-a999-94e80f1c647b (24 cs) by OutboundPalletBuilder.AdoptFullPallet>>
- day 6 21:22 | **OVERLAP** | OVERLAP OutboundPallet_81c8252a-3187-422a-a999-94e80f1c647b_restored & OutboundPallet_81c8252a-3187-422a-a999-94e80f1c647b_restored @3B-4
- day 6 21:22 | **OVERLAP** | OVERLAP OutboundPallet_81c8252a-3187-422a-a999-94e80f1c647b_restored & OutboundPallet_81c8252a-3187-422a-a999-94e80f1c647b_restored @3B-3
- day 6 21:22 | **FLOATING** | FLOATING OutboundPallet_81c8252a-3187-422a-a999-94e80f1c647b_restored @3B-4 bottom=1.30
- day 1 21:03 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack: no path to (-0.07, 1.11, 50.96) for 'pallet jack parking'.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__96:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1256)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 23:44 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jonathan Moore' as its operator though nobody is on it or running it - clearing the stale link.
- day 2 00:27 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 1D: destination did not take — no path and none pending for driveTarget=(-9.99, 1.12, 48.99) (anchor=(-9.99, 1.09, 48.99), from=(-24.56, 1.15, 40.71), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2683)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 02:43 | **OVERLAP** | OVERLAP Pallet_04_Slot4_Tier0 & Pallet_05_Slot4_Tier1 @(25, 82)  <<HISTORY: d1 22:12 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 01:18 Replenish task created (01-12-A0->01-12-00) by ReplenishmentService.CreateReplenishTask | d2 02:08 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 02:23 moved (25, 82)->(-1, -1) by InventoryService.MovePallet | d2 02:37 moved (-1, -1)->(25, 82) by InventoryService.MovePallet | d2 02:37 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d2 01:13 picked 1 (left 4) by InventoryService.PickFromPallet | d2 01:14 picked 1 (left 3) by InventoryService.PickFromPallet | d2 01:14 picked 1 (left 2) by InventoryService.PickFromPallet | d2 01:15 picked 1 (left 1) by InventoryService.PickFromPallet | d2 01:16 picked 1 (left 0) by InventoryService.PickFromPallet | d2 01:16 record destroyed by InventoryService.DestroyPallet>>
- day 2 17:58 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack: no path to (-26.54, 1.11, 46.44) for 'bay 02-05'.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__96:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1256)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 21:01 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-09-A1 anchor: destination did not take — no path and none pending for driveTarget=(-27.21, 1.13, 47.34) (anchor=(-27.21, 3.11, 47.34), from=(-4.69, 1.16, 48.99), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2683)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 05:58 | **DOOR-DEADLOCK** | OG3328 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1B: [1: own/Staged/y2.2, own/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.3] [3: own/Staged/y2.2, own/Staged/y1.2] [4: own/Staged/y2.0, own/Staged/y1.2] [5: own/Staged/y1.2, own/Staged/y1.9] lane 1D: [1: own/Staged/y1.2, own/Staged/y2.8] lane 1A: [1: own/Staged/y2.6, own/Staged/y1.2] 
- day 3 06:13 | **DOOR-DEADLOCK** | OG3328 is Staged but door 1's truck has waited 105 min with no one loading it. | lane 1B: [1: own/Staged/y2.2, own/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.3] [3: own/Staged/y2.2, own/Staged/y1.2] [4: own/Staged/y2.0, own/Staged/y1.2] [5: own/Staged/y1.2, own/Staged/y1.9] lane 1D: [1: own/Staged/y1.2, own/Staged/y2.8] lane 1A: [1: own/Staged/y2.6, own/Staged/y1.2] [2: own/Staged/y1.2] 
- day 3 11:43 | **DOOR-DEADLOCK** | OG3328 is Staged but door 1's truck has waited 435 min with no one loading it. | lane 1B: [1: own/Staged/y2.2, own/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.3] [3: own/Staged/y2.2, own/Staged/y1.2] [4: own/Staged/y2.0, own/Staged/y1.2] [5: own/Staged/y1.2, own/Staged/y1.9] lane 1D: [1: own/Staged/y1.2, own/Staged/y2.8] lane 1A: [1: own/Staged/y2.6, own/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y1.6] 
- day 3 17:03 | **WARN** | [AiNavigation] Dockstocker: stuck at (-2.04, 1.11, 40.36) — walking out to (4.46, 1.25, 51.00) (12.00m to mesh).
- day 4 07:19 | **ERROR** | [TrailerLoad] 'Dockstocker' cross-door load: to trailer: destination did not take (no path) for (-2.64, 1.12, 38.89) from (-9.99, 1.16, 39.19).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__100:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1145)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 4 07:19 | **ERROR** | [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<PlaceCarriedInTrailer>d__96:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:977)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 4 11:13 | **DOOR-DEADLOCK** | OG4329 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1C: [1: own/Staged/y1.2, own/Staged/y2.4] [2: own/Staged/y1.2, own/Staged/y2.5] [3: own/Staged/y1.2, own/Staged/y2.1] [4: own/Staged/y2.2, own/Staged/y1.2] [5: own/Staged/y1.2, own/Staged/y1.5] lane 1E: [1: own/Staged/y1.2, own/Staged/y2.3] [2: own/Staged/y1.2, own/Staged/y2.3] 
- day 5 02:07 | **WARN** | [AiNavigation] ReachTruck: stuck at (-35.16, 1.11, 40.01) — walking out to (-28.68, 1.16, 51.00) (12.31m to mesh).
- day 5 06:12 | **ERROR** | [TrailerLoad] 'Dockstocker' cross-door load: to lane 1C: destination did not take (no path) for (-11.31, 1.12, 39.19) from (7.84, 1.16, 38.89).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__100:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1145)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 5 06:12 | **ERROR** | [TrailerLoad] Could not reach lane 1C — leaving OutboundPallet_c23261e7-f4e9-4b11-bd33-236b8f6bf425_1 staged.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<LoadOnePallet>d__95:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:830)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 5 07:57 | **ERROR** | [TrailerLoad] 'Dockstocker' cross-door load: to lane 1C: destination did not take (no path) for (-11.31, 1.12, 39.19) from (6.64, 1.16, 38.89).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__100:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1145)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 5 07:57 | **ERROR** | [TrailerLoad] Could not reach lane 1C — leaving OutboundPallet_c23261e7-f4e9-4b11-bd33-236b8f6bf425_1 staged.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<LoadOnePallet>d__95:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:830)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 5 11:05 | **WARN** | [AiNavigation] ReachTruck: stuck at (-24.69, 1.16, 47.90) — walking out to (-24.69, 1.16, 40.89) (6.01m to mesh).
- day 5 12:56 | **WARN** | [ReachTruckOperator] Replenish: could not reach pallet at 02-03-A0 within 0.15m.
- day 6 07:00 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 1D: destination did not take — no path and none pending for driveTarget=(-9.99, 1.12, 48.99) (anchor=(-9.99, 1.09, 48.99), from=(-31.19, 1.16, 40.01), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2683)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 11:27 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→PO_IG6514 & Truck→PO_IG6513 at (-2.3, 0.0, 18.5)
- day 6 13:13 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 01-04-00 anchor: destination did not take — no path and none pending for driveTarget=(-21.91, 1.12, 40.01) (anchor=(-21.91, 1.11, 40.01), from=(-3.36, 1.16, 48.99), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2683)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 21:02 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 243s while 6 task(s) it could take wait
- day 6 21:03 | **WARN** | [AiNavigation] ReachTruck: stuck at (0.61, 1.16, 49.64) — walking out to (12.67, 1.16, 49.64) (12.00m to mesh).
- day 7 01:03 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 6 task(s) it could take wait
- day 7 05:03 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 6 task(s) it could take wait
- day 7 10:05 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 01-12-01 anchor: destination did not take — no path and none pending for driveTarget=(-33.84, 1.12, 40.01) (anchor=(-33.84, 1.11, 40.01), from=(5.91, 1.16, 48.99), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2683)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 7 12:03 | **WARN** | [AiNavigation] ReachTruck: stuck at (-33.84, 1.11, 40.01) — walking out to (-40.32, 1.16, 51.00) (12.31m to mesh).
- day 7 12:05 | **WARN** | [AiNavigation] Employee_Pamela Garcia: stuck at (-27.20, 1.16, 42.13) — walking out to (-38.46, 1.09, 35.63) (12.00m to mesh).
- day 7 16:06 | **DOOR-DEADLOCK** | OG7333 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1D: [1: own/Staged/y1.2, own/Staged/y2.6] [2: own/Staged/y1.2, own/Staged/y2.3] [3: own/Staged/y1.2, own/Staged/y1.9] [4: own/Staged/y2.0, own/Staged/y1.2] [5: own/Staged/y1.5, own/Staged/y1.2] lane 1E: [1: own/Staged/y1.2, own/Staged/y2.6] lane 1B: [1: OTHER d369c64a-6779-4bda-85f7-c7bf4b0a91e3/Staged/y2.7, OTHER d369c64a-6779-4bda-85f7-c7bf4b0a91e3/Staged/y1.2] [2: OTHER d369c64a-6779-4bda-85f7-c7bf4b0a91e3/Staged/y1.8, OTHER d369c64a-6779-4bda-85f7-c7bf4b0a91e3/Staged/y1.2] [3: own/Staged/y2.3, own/Staged/y1.2] [4: own/Staged/y1.2, own/Staged/y1.8] [5: own/Staged/y1.2] 
- day 7 18:13 | **WARN** | [AiNavigation] ReachTruck: stuck at (-24.56, 1.11, 40.71) — walking out to (-11.56, 1.16, 40.60) (12.00m to mesh).
- day 7 20:08 | **OVERLAP** | OVERLAP Pallet_00_Slot0_Tier0 & Pallet_01_Slot1_Tier0 @1B-1  <<HISTORY: d5 05:10 Putaway task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 06:04 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 06:22 moved (29, 78)->(-1, -1) by InventoryService.MovePallet | d5 06:41 adopted as outbound freight of order d369c64a-6779-4bda-85f7-c7bf4b0a91e3 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 06:41 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 06:41 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d5 04:14 PalletPick task Open->Available by OrderService.AllocateHeldWork | d5 06:15 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 06:35 moved (25, 82)->(-1, -1) by InventoryService.MovePallet | d5 06:59 adopted as outbound freight of order d369c64a-6779-4bda-85f7-c7bf4b0a91e3 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 06:59 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 06:59 record destroyed by InventoryService.DestroyPallet>>
- day 7 20:08 | **FLOATING** | FLOATING Pallet_01_Slot1_Tier0 @1B-1 bottom=2.57  <<HISTORY: d5 04:14 PalletPick task Open->Available by OrderService.AllocateHeldWork | d5 06:15 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 06:35 moved (25, 82)->(-1, -1) by InventoryService.MovePallet | d5 06:59 adopted as outbound freight of order d369c64a-6779-4bda-85f7-c7bf4b0a91e3 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 06:59 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 06:59 record destroyed by InventoryService.DestroyPallet>>
- day 7 20:08 | **FLOATING** | FLOATING OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 @1A-5 bottom=2.17
- day 7 20:08 | **OVERLAP** | OVERLAP OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 & OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 @1A-5
- day 7 20:08 | **OVERLAP** | OVERLAP OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 & OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 @1A-4
- day 7 20:08 | **FLOATING** | FLOATING OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 @1B-2 bottom=1.66
- day 7 20:08 | **OVERLAP** | OVERLAP OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 & OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 @1B-2
- day 7 20:08 | **FLOATING** | FLOATING OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 @1A-4 bottom=2.07
- day 7 20:59 | **ERROR** | [TrailerLoad] 'Dockstocker' cross-door load: to lane 1A: destination did not take (no path) for (-13.96, 1.12, 39.19) from (-2.64, 1.16, 38.89).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__100:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1145)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 7 20:59 | **ERROR** | [TrailerLoad] Could not reach lane 1A — leaving OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 staged.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<LoadOnePallet>d__95:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:830)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 7 21:53 | **OVERLAP** | OVERLAP Pallet_01_Slot1_Tier0 & Pallet_02_Slot2_Tier0 @3E-1  <<HISTORY: d7 20:22 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 20:23 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 21:03 moved (25, 82)->(-1, -1) by InventoryService.MovePallet | d7 21:46 adopted as outbound freight of order 8f8bf42e-61d4-46d9-88f2-463b3642a5dd (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 21:46 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 21:46 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d7 20:22 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 20:37 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 21:06 moved (23, 78)->(-1, -1) by InventoryService.MovePallet | d7 21:46 adopted as outbound freight of order 8f8bf42e-61d4-46d9-88f2-463b3642a5dd (8 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 21:46 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 21:46 record destroyed by InventoryService.DestroyPallet>>
