# Bug log - block 1087

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 09:57 | **WARN** | [AiNavigation] Dockstocker: stuck at (-11.21, 1.11, 51.00) — walking out to (-17.81, 1.16, 39.71) (12.07m to mesh).
- day 1 09:57 | **WARN** | [AiNavigation] Dockstocker: stuck at (-11.21, 1.11, 51.00) — walking out to (-17.81, 1.16, 39.71) (12.07m to mesh).
- day 1 10:18 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-43.15, 1.11, 40.00) — walking out to (-36.66, 1.12, 51.33) (12.11m to mesh).
- day 1 10:18 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-43.15, 1.11, 40.00) — walking out to (-36.66, 1.12, 51.33) (12.11m to mesh).
- day 1 14:58 | **WARN** | [AiNavigation] Employee_Catherine Miller: stuck at (-23.03, 1.11, 40.27) — walking out to (-10.03, 1.16, 40.27) (12.00m to mesh).
- day 1 14:58 | **WARN** | [AiNavigation] Employee_Catherine Miller: stuck at (-23.03, 1.11, 40.27) — walking out to (-10.03, 1.16, 40.27) (12.00m to mesh).
- day 1 18:57 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-6.36, 1.12, 49.68) — walking out to (-12.86, 1.16, 38.42) (12.00m to mesh).
- day 1 18:57 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-6.36, 1.12, 49.68) — walking out to (-12.86, 1.16, 38.42) (12.00m to mesh).
- day 1 19:28 | **WARN** | [AiNavigation] Dockstocker: stuck at (-2.00, 1.11, 51.00) — walking out to (-13.24, 1.16, 44.09) (12.21m to mesh).
- day 1 19:28 | **WARN** | [AiNavigation] Dockstocker: stuck at (-2.00, 1.11, 51.00) — walking out to (-13.24, 1.16, 44.09) (12.21m to mesh).
- day 1 21:19 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 2B: NO NAVMESH PATH. anchor=(-3.36, 1.09, 48.99) → driveTarget=(-3.36, 1.16, 48.99), from=(-12.66, 1.11, 48.82), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=8.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x0024a] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:992 < ] 36.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 21:19 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 2B: NO NAVMESH PATH. anchor=(-3.36, 1.09, 48.99) → driveTarget=(-3.36, 1.16, 48.99), from=(-12.66, 1.11, 48.82), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=8.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x0024a] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:992 < ] 36.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 22:02 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (-47.09, 1.10, 37.71) (path invalid) — abandoning the walk and handing the task back.
- day 1 22:02 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (-47.09, 1.10, 37.71) (path invalid) — abandoning the walk and handing the task back.
- day 1 23:29 | **WARN** | [OrderSelectionTaskDriver] Order 0cba0375-bb93-4696-93bd-fb562ea7b3fa (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 24/460.
- day 1 23:29 | **WARN** | [OrderSelectionTaskDriver] Order 0cba0375-bb93-4696-93bd-fb562ea7b3fa (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 24/460.
- day 2 00:37 | **WARN** | [AiNavigation] PalletJack cannot reach task position at (-37.14, 1.16, 46.44) (path invalid) — abandoning the walk and handing the task back.
- day 2 00:37 | **WARN** | [AiNavigation] PalletJack cannot reach task position at (-37.14, 1.16, 46.44) (path invalid) — abandoning the walk and handing the task back.
- day 2 01:28 | **WARN** | [OrderSelectionTaskDriver] Order 0cba0375-bb93-4696-93bd-fb562ea7b3fa (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 103/460.
- day 2 01:28 | **WARN** | [OrderSelectionTaskDriver] Order 0cba0375-bb93-4696-93bd-fb562ea7b3fa (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 103/460.
- day 2 01:44 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (11.21, 1.21, 40.36) (path invalid) — abandoning the walk and handing the task back.
- day 2 01:44 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (11.21, 1.21, 40.36) (path invalid) — abandoning the walk and handing the task back.
- day 2 04:20 | **WARN** | [OrderSelectionTaskDriver] Order 0cba0375-bb93-4696-93bd-fb562ea7b3fa (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 170/460.
- day 2 04:20 | **WARN** | [OrderSelectionTaskDriver] Order 0cba0375-bb93-4696-93bd-fb562ea7b3fa (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 170/460.
- day 2 04:43 | **WARN** | [AiNavigation] ReachTruck: stuck at (-0.83, 1.11, 47.72) — walking out to (-12.07, 1.16, 40.05) (12.61m to mesh).
- day 2 04:43 | **WARN** | [AiNavigation] ReachTruck: stuck at (-0.83, 1.11, 47.72) — walking out to (-12.07, 1.16, 40.05) (12.61m to mesh).
- day 2 06:08 | **WARN** | [AiNavigation] ReachTruck: stuck at (-14.54, 1.11, 48.82) — walking out to (-25.76, 1.16, 41.33) (12.49m to mesh).
- day 2 06:08 | **WARN** | [AiNavigation] ReachTruck: stuck at (-14.54, 1.11, 48.82) — walking out to (-25.76, 1.16, 41.33) (12.49m to mesh).
- day 2 06:20 | **WARN** | [AiNavigation] PalletJack: stuck at (-0.30, 1.11, 49.37) — walking out to (6.20, 1.16, 38.11) (12.00m to mesh).
- day 2 06:20 | **WARN** | [AiNavigation] PalletJack: stuck at (-0.30, 1.11, 49.37) — walking out to (6.20, 1.16, 38.11) (12.00m to mesh).
- day 2 06:28 | **WARN** | [OrderSelectionTaskDriver] Order 0cba0375-bb93-4696-93bd-fb562ea7b3fa (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 192/460.
- day 2 06:28 | **WARN** | [OrderSelectionTaskDriver] Order 0cba0375-bb93-4696-93bd-fb562ea7b3fa (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 192/460.
- day 2 09:19 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-30.70, 1.12, 42.14) — walking out to (-41.96, 1.08, 35.64) (12.00m to mesh).
- day 2 09:19 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-30.70, 1.12, 42.14) — walking out to (-41.96, 1.08, 35.64) (12.00m to mesh).
- day 2 09:37 | **WARN** | [OrderSelectionTaskDriver] Order 0cba0375-bb93-4696-93bd-fb562ea7b3fa (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 216/460.
- day 2 09:37 | **WARN** | [OrderSelectionTaskDriver] Order 0cba0375-bb93-4696-93bd-fb562ea7b3fa (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 216/460.
- day 2 11:00 | **OVERLAP** | OVERLAP Pallet_05_Slot2_Tier1 & Pallet_06_Slot3_Tier0 @2B-3  <<HISTORY: d2 02:07 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d2 05:13 moved (56, 84)->(-1, -1) by InventoryService.MovePallet | d2 05:32 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 05:32 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 05:32 record destroyed by InventoryService.DestroyPallet | d2 05:32 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d2 03:57 Putaway task created (STG3D-2->) by OrderService.RefileStrandedLanePallets | d2 06:47 moved (56, 82)->(-1, -1) by InventoryService.MovePallet | d2 06:59 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 06:59 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 06:59 record destroyed by InventoryService.DestroyPallet | d2 06:59 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 2 11:00 | **FLOATING** | FLOATING Pallet_02_Slot1_Tier0 @2B-1 bottom=2.33  <<HISTORY: d2 03:40 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d2 03:53 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 03:53 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 03:53 record destroyed by InventoryService.DestroyPallet | d2 03:53 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 03:53 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 2 11:00 | **OVERLAP** | OVERLAP Pallet_02_Slot1_Tier0 & Pallet_01_Slot1_Tier0 @2B-1  <<HISTORY: d2 03:40 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d2 03:53 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 03:53 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 03:53 record destroyed by InventoryService.DestroyPallet | d2 03:53 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 03:53 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d1 21:23 moved (-1, -1)->(33, 78) by InventoryService.MovePallet | d1 21:51 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 21:57 moved (33, 78)->(-1, -1) by InventoryService.MovePallet | d1 22:20 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (0 cs) by OutboundPalletBuilder.AdoptFullPallet | d1 22:20 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 22:20 record destroyed by InventoryService.DestroyPallet>>
- day 2 11:00 | **OVERLAP** | OVERLAP Pallet_03_Slot1_Tier1 & Pallet_04_Slot2_Tier0 @2B-2  <<HISTORY: d2 04:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 04:16 record destroyed by InventoryService.DestroyPallet | d2 04:16 PalletPick task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 04:16 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 05:43 PalletPick task Cancelled->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d2 06:38 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>  <<HISTORY: d2 04:29 moved (56, 84)->(-1, -1) by InventoryService.MovePallet | d2 05:00 PalletPick task Assigned->Available by WorkQueueSystem.ReleaseStaleAssignments | d2 05:01 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 05:01 PalletPick task Available->Complete by WorkQueueSystem.CompleteTask | d2 05:01 record destroyed by InventoryService.DestroyPallet | d2 05:01 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 2 11:00 | **FLOATING** | FLOATING Pallet_06_Slot3_Tier0 @2B-3 bottom=2.33  <<HISTORY: d2 03:57 Putaway task created (STG3D-2->) by OrderService.RefileStrandedLanePallets | d2 06:47 moved (56, 82)->(-1, -1) by InventoryService.MovePallet | d2 06:59 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 06:59 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 06:59 record destroyed by InventoryService.DestroyPallet | d2 06:59 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 2 11:00 | **FLOATING** | FLOATING Pallet_04_Slot2_Tier0 @2B-2 bottom=2.33  <<HISTORY: d2 04:29 moved (56, 84)->(-1, -1) by InventoryService.MovePallet | d2 05:00 PalletPick task Assigned->Available by WorkQueueSystem.ReleaseStaleAssignments | d2 05:01 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 05:01 PalletPick task Available->Complete by WorkQueueSystem.CompleteTask | d2 05:01 record destroyed by InventoryService.DestroyPallet | d2 05:01 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 2 11:00 | **OVERLAP** | OVERLAP Pallet_05_Slot2_Tier1 & Pallet_06_Slot3_Tier0 @2B-3  <<HISTORY: d2 02:07 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d2 05:13 moved (56, 84)->(-1, -1) by InventoryService.MovePallet | d2 05:32 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 05:32 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 05:32 record destroyed by InventoryService.DestroyPallet | d2 05:32 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d2 03:57 Putaway task created (STG3D-2->) by OrderService.RefileStrandedLanePallets | d2 06:47 moved (56, 82)->(-1, -1) by InventoryService.MovePallet | d2 06:59 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 06:59 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 06:59 record destroyed by InventoryService.DestroyPallet | d2 06:59 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 2 11:00 | **FLOATING** | FLOATING Pallet_02_Slot1_Tier0 @2B-1 bottom=2.33  <<HISTORY: d2 03:40 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d2 03:53 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 03:53 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 03:53 record destroyed by InventoryService.DestroyPallet | d2 03:53 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 03:53 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 2 11:00 | **OVERLAP** | OVERLAP Pallet_02_Slot1_Tier0 & Pallet_01_Slot1_Tier0 @2B-1  <<HISTORY: d2 03:40 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d2 03:53 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 03:53 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 03:53 record destroyed by InventoryService.DestroyPallet | d2 03:53 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 03:53 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d1 21:23 moved (-1, -1)->(33, 78) by InventoryService.MovePallet | d1 21:51 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 21:57 moved (33, 78)->(-1, -1) by InventoryService.MovePallet | d1 22:20 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (0 cs) by OutboundPalletBuilder.AdoptFullPallet | d1 22:20 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 22:20 record destroyed by InventoryService.DestroyPallet>>
- day 2 11:00 | **OVERLAP** | OVERLAP Pallet_03_Slot1_Tier1 & Pallet_04_Slot2_Tier0 @2B-2  <<HISTORY: d2 04:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 04:16 record destroyed by InventoryService.DestroyPallet | d2 04:16 PalletPick task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 04:16 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 05:43 PalletPick task Cancelled->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d2 06:38 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>  <<HISTORY: d2 04:29 moved (56, 84)->(-1, -1) by InventoryService.MovePallet | d2 05:00 PalletPick task Assigned->Available by WorkQueueSystem.ReleaseStaleAssignments | d2 05:01 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 05:01 PalletPick task Available->Complete by WorkQueueSystem.CompleteTask | d2 05:01 record destroyed by InventoryService.DestroyPallet | d2 05:01 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 2 11:00 | **FLOATING** | FLOATING Pallet_06_Slot3_Tier0 @2B-3 bottom=2.33  <<HISTORY: d2 03:57 Putaway task created (STG3D-2->) by OrderService.RefileStrandedLanePallets | d2 06:47 moved (56, 82)->(-1, -1) by InventoryService.MovePallet | d2 06:59 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 06:59 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 06:59 record destroyed by InventoryService.DestroyPallet | d2 06:59 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 2 11:00 | **FLOATING** | FLOATING Pallet_04_Slot2_Tier0 @2B-2 bottom=2.33  <<HISTORY: d2 04:29 moved (56, 84)->(-1, -1) by InventoryService.MovePallet | d2 05:00 PalletPick task Assigned->Available by WorkQueueSystem.ReleaseStaleAssignments | d2 05:01 adopted as outbound freight of order 815b0b94-2942-41eb-bc23-8533828bbe89 (100 cs) by OutboundPalletBuilder.AdoptFullPallet | d2 05:01 PalletPick task Available->Complete by WorkQueueSystem.CompleteTask | d2 05:01 record destroyed by InventoryService.DestroyPallet | d2 05:01 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 2 11:41 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (-47.09, 1.10, 46.99) (path invalid) — abandoning the walk and handing the task back.
- day 2 11:41 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (-47.09, 1.10, 46.99) (path invalid) — abandoning the walk and handing the task back.
- day 2 13:02 | **WARN** | [AiNavigation] ReachTruck: stuck at (-4.20, 1.11, 50.76) — walking out to (7.05, 1.16, 44.09) (12.09m to mesh).
- day 2 13:02 | **WARN** | [AiNavigation] ReachTruck: stuck at (-4.20, 1.11, 50.76) — walking out to (7.05, 1.16, 44.09) (12.09m to mesh).
- day 2 13:10 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-35.08, 1.12, 45.57) — walking out to (-41.58, 0.04, 34.31) (12.00m to mesh).
- day 2 13:10 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-35.08, 1.12, 45.57) — walking out to (-41.58, 0.04, 34.31) (12.00m to mesh).
- day 2 14:25 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-27.80, 1.11, 40.07) — walking out to (-21.31, 1.12, 51.33) (12.05m to mesh).
- day 2 14:25 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-27.80, 1.11, 40.07) — walking out to (-21.31, 1.12, 51.33) (12.05m to mesh).
- day 2 15:12 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] Order OG4437 pallet 1 of 1: every pick face is empty - handing the task back until replenishment stocks them.
- day 2 15:12 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] Order OG4437 pallet 1 of 1: every pick face is empty - handing the task back until replenishment stocks them.
- day 2 15:24 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-16.25, 1.12, 38.91) — walking out to (-27.50, 1.12, 45.73) (12.15m to mesh).
- day 2 15:24 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-16.25, 1.12, 38.91) — walking out to (-27.50, 1.12, 45.73) (12.15m to mesh).
- day 2 16:53 | **WARN** | [AiNavigation] Employee_Catherine Miller cannot reach equipment at (-0.07, 1.11, 50.96) (path invalid) — seeking next available
- day 2 16:53 | **WARN** | [AiNavigation] Employee_Catherine Miller cannot reach equipment at (-0.07, 1.11, 50.96) (path invalid) — seeking next available
- day 2 17:11 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-8.77, 1.13, 50.27) — walking out to (-21.76, 1.12, 50.53) (12.00m to mesh).
- day 2 17:11 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-8.77, 1.13, 50.27) — walking out to (-21.76, 1.12, 50.53) (12.00m to mesh).
- day 2 17:54 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-9.05, 1.16, 40.86) — walking out to (-22.05, 1.11, 40.86) (12.00m to mesh).
- day 2 17:54 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-9.05, 1.16, 40.86) — walking out to (-22.05, 1.11, 40.86) (12.00m to mesh).
- day 2 17:57 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 6a12e672-27b8-43be-8a56-4bbb914fb4e7 but the employee is 9.7m away (max 4.5m) — refusing to start receiving.
- day 2 17:57 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 6a12e672-27b8-43be-8a56-4bbb914fb4e7 but the employee is 9.7m away (max 4.5m) — refusing to start receiving.
- day 2 19:16 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (7.26, 1.16, 50.81) (path invalid) — abandoning the walk and handing the task back.
- day 2 19:16 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (7.26, 1.16, 50.81) (path invalid) — abandoning the walk and handing the task back.
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1E-1 bottom=1.81
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1E-1
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-3 bottom=1.46
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-3
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-1 bottom=1.41
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-1
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-4
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-2
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1E-2 bottom=1.43
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1E-2
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-2 bottom=2.44
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-4 bottom=2.37
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1E-1 bottom=1.81
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1E-1
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-3 bottom=1.46
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-3
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-1 bottom=1.41
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-1
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-4
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-2
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1E-2 bottom=1.43
- day 2 20:00 | **OVERLAP** | OVERLAP OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 & OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1E-2
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-2 bottom=2.44
- day 2 20:00 | **FLOATING** | FLOATING OutboundPallet_0cba0375-bb93-4696-93bd-fb562ea7b3fa_1 @1D-4 bottom=2.37
- day 2 22:10 | **WARN** | [AiNavigation] ReachTruck: stuck at (1.67, 1.11, 47.69) — walking out to (-10.22, 1.16, 41.22) (12.89m to mesh).
- day 2 22:10 | **WARN** | [AiNavigation] ReachTruck: stuck at (1.67, 1.11, 47.69) — walking out to (-10.22, 1.16, 41.22) (12.89m to mesh).
- day 2 22:26 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-27.15, 1.12, 48.76) — walking out to (-38.38, 1.12, 41.60) (12.32m to mesh).
- day 2 22:26 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-27.15, 1.12, 48.76) — walking out to (-38.38, 1.12, 41.60) (12.32m to mesh).
- day 2 23:48 | **WARN** | [AiNavigation] Employee_Justin Reyes cannot reach task position at (6.34, 1.16, 46.99) (path invalid) — abandoning the walk and handing the task back.
- day 2 23:48 | **WARN** | [AiNavigation] Employee_Justin Reyes cannot reach task position at (6.34, 1.16, 46.99) (path invalid) — abandoning the walk and handing the task back.
- day 2 23:51 | **WARN** | [AiNavigation] Dockstocker: stuck at (-37.17, 1.11, 45.46) — walking out to (-24.17, 1.16, 45.46) (12.00m to mesh).
- day 2 23:51 | **WARN** | [AiNavigation] Dockstocker: stuck at (-37.17, 1.11, 45.46) — walking out to (-24.17, 1.16, 45.46) (12.00m to mesh).
- day 3 00:49 | **WARN** | [AiNavigation] ReachTruck: stuck at (-40.46, 1.11, 46.59) — walking out to (-27.46, 1.16, 46.59) (12.00m to mesh).
- day 3 00:49 | **WARN** | [AiNavigation] ReachTruck: stuck at (-40.46, 1.11, 46.59) — walking out to (-27.46, 1.16, 46.59) (12.00m to mesh).
- day 3 00:53 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-3.82, 1.14, 49.79) — walking out to (-10.32, 1.20, 38.53) (12.00m to mesh).
- day 3 00:53 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-3.82, 1.14, 49.79) — walking out to (-10.32, 1.20, 38.53) (12.00m to mesh).
- day 3 01:11 | **WARN** | [OrderSelectionTaskDriver] Order dcdda027-7caa-402a-b642-d49a4f76851a (Blocky Bakeshop) has no reachable pick location for its remaining line item(s) — staging short at 347/630.
- day 3 01:11 | **WARN** | [OrderSelectionTaskDriver] Order dcdda027-7caa-402a-b642-d49a4f76851a (Blocky Bakeshop) has no reachable pick location for its remaining line item(s) — staging short at 347/630.
- day 3 03:32 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-15.60, 1.16, 39.36) — walking out to (-11.12, 1.63, 31.58) (8.01m to mesh).
- day 3 03:32 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-15.60, 1.16, 39.36) — walking out to (-11.12, 1.63, 31.58) (8.01m to mesh).
- day 3 04:49 | **WARN** | [OrderSelectionTaskDriver] Order dcdda027-7caa-402a-b642-d49a4f76851a (Blocky Bakeshop) has no reachable pick location for its remaining line item(s) — staging short at 503/630.
- day 3 04:49 | **WARN** | [OrderSelectionTaskDriver] Order dcdda027-7caa-402a-b642-d49a4f76851a (Blocky Bakeshop) has no reachable pick location for its remaining line item(s) — staging short at 503/630.
- day 3 06:27 | **WARN** | [AiNavigation] Dockstocker: stuck at (-18.56, 1.11, 41.89) — walking out to (-5.56, 1.16, 41.89) (12.00m to mesh).
- day 3 06:27 | **WARN** | [AiNavigation] Dockstocker: stuck at (-18.56, 1.11, 41.89) — walking out to (-5.56, 1.16, 41.89) (12.00m to mesh).
- day 3 06:48 | **WARN** | [OrderService] Order OG2434 closed with a IN TRANSIT pallet (56 cs) that never made the trailer - not billed, returning it to stock.
- day 3 06:48 | **WARN** | [OrderService] Order OG2434 closed with a IN TRANSIT pallet (56 cs) that never made the trailer - not billed, returning it to stock.
- day 3 06:52 | **WARN** | [AiNavigation] ReachTruck: stuck at (-19.84, 1.11, 41.89) — walking out to (-6.84, 1.16, 41.89) (12.00m to mesh).
- day 3 06:52 | **WARN** | [AiNavigation] ReachTruck: stuck at (-19.84, 1.11, 41.89) — walking out to (-6.84, 1.16, 41.89) (12.00m to mesh).
- day 3 07:30 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1D-1 (order 0cba0375-bb93-4696-93bd-fb562ea7b3fa gone)
- day 3 07:30 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1D-1 (order 0cba0375-bb93-4696-93bd-fb562ea7b3fa gone)
- day 3 07:30 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1D-1 (order 0cba0375-bb93-4696-93bd-fb562ea7b3fa gone)
- day 3 07:30 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1D-1 (order 0cba0375-bb93-4696-93bd-fb562ea7b3fa gone)
- day 3 08:07 | **WARN** | [AiNavigation] Dockstocker: stuck at (-8.21, 1.11, 44.34) — walking out to (-19.46, 1.16, 51.00) (12.22m to mesh).
- day 3 08:07 | **WARN** | [AiNavigation] Dockstocker: stuck at (-8.21, 1.11, 44.34) — walking out to (-19.46, 1.16, 51.00) (12.22m to mesh).
- day 3 08:30 | **WARN** | [AiNavigation] PalletJack: stuck at (-22.56, 1.11, 41.09) — walking out to (-9.56, 1.16, 41.09) (12.00m to mesh).
- day 3 08:30 | **WARN** | [AiNavigation] PalletJack: stuck at (-22.56, 1.11, 41.09) — walking out to (-9.56, 1.16, 41.09) (12.00m to mesh).
- day 3 08:41 | **WARN** | [AiNavigation] ReachTruck: stuck at (-40.35, 1.11, 42.50) — walking out to (-27.35, 1.16, 41.84) (12.02m to mesh).
- day 3 08:41 | **WARN** | [AiNavigation] ReachTruck: stuck at (-40.35, 1.11, 42.50) — walking out to (-27.35, 1.16, 41.84) (12.02m to mesh).
- day 3 09:03 | **WARN** | [OrderSelectionTaskDriver] Order dcdda027-7caa-402a-b642-d49a4f76851a (Blocky Bakeshop) has no reachable pick location for its remaining line item(s) — staging short at 571/630.
- day 3 09:03 | **WARN** | [OrderSelectionTaskDriver] Order dcdda027-7caa-402a-b642-d49a4f76851a (Blocky Bakeshop) has no reachable pick location for its remaining line item(s) — staging short at 571/630.
- day 3 10:27 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-16.53, 1.17, 38.79) — walking out to (-11.82, 1.50, 31.58) (8.11m to mesh).
- day 3 10:27 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-16.53, 1.17, 38.79) — walking out to (-11.82, 1.50, 31.58) (8.11m to mesh).
- day 3 10:37 | **WARN** | [AiNavigation] ReachTruck: stuck at (-33.04, 1.11, 45.46) — walking out to (-20.04, 1.16, 45.46) (12.00m to mesh).
- day 3 10:37 | **WARN** | [AiNavigation] ReachTruck: stuck at (-33.04, 1.11, 45.46) — walking out to (-20.04, 1.16, 45.46) (12.00m to mesh).
- day 3 10:42 | **WARN** | [AiNavigation] PalletJack: stuck at (-0.07, 1.11, 50.95) — walking out to (6.43, 1.16, 39.69) (12.00m to mesh).
- day 3 10:42 | **WARN** | [AiNavigation] PalletJack: stuck at (-0.07, 1.11, 50.95) — walking out to (6.43, 1.16, 39.69) (12.00m to mesh).
- day 3 10:44 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-16.61, 1.16, 40.84) — walking out to (-11.81, 1.50, 33.06) (8.15m to mesh).
- day 3 10:44 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-16.61, 1.16, 40.84) — walking out to (-11.81, 1.50, 33.06) (8.15m to mesh).
- day 3 11:37 | **WARN** | [AiNavigation] ReachTruck: stuck at (-40.45, 1.11, 45.81) — walking out to (-27.45, 1.16, 45.81) (12.00m to mesh).
- day 3 11:37 | **WARN** | [AiNavigation] ReachTruck: stuck at (-40.45, 1.11, 45.81) — walking out to (-27.45, 1.16, 45.81) (12.00m to mesh).
- day 3 12:33 | **WARN** | [OrderSelectionTaskDriver] Order dcdda027-7caa-402a-b642-d49a4f76851a (Blocky Bakeshop) has no reachable pick location for its remaining line item(s) — staging short at 615/630.
- day 3 12:33 | **WARN** | [OrderSelectionTaskDriver] Order dcdda027-7caa-402a-b642-d49a4f76851a (Blocky Bakeshop) has no reachable pick location for its remaining line item(s) — staging short at 615/630.
- day 3 16:21 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-36.24, 1.12, 50.51) — walking out to (-49.24, 0.04, 50.51) (12.00m to mesh).
- day 3 16:21 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-36.24, 1.12, 50.51) — walking out to (-49.24, 0.04, 50.51) (12.00m to mesh).
- day 3 16:23 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-40.72, 1.12, 50.60) — walking out to (-47.22, 1.08, 39.34) (12.00m to mesh).
- day 3 16:23 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-40.72, 1.12, 50.60) — walking out to (-47.22, 1.08, 39.34) (12.00m to mesh).
- day 3 17:06 | **WARN** | [AiNavigation] ReachTruck: stuck at (-10.87, 1.11, 51.00) — walking out to (-23.87, 1.16, 51.00) (12.00m to mesh).
- day 3 17:06 | **WARN** | [AiNavigation] ReachTruck: stuck at (-10.87, 1.11, 51.00) — walking out to (-23.87, 1.16, 51.00) (12.00m to mesh).
- day 3 18:35 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-16.61, 1.17, 39.34) — walking out to (-11.81, 1.50, 31.58) (8.15m to mesh).
- day 3 18:35 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-16.61, 1.17, 39.34) — walking out to (-11.81, 1.50, 31.58) (8.15m to mesh).
- day 3 18:36 | **WARN** | [AiNavigation] PalletJack: stuck at (-23.77, 1.11, 39.81) — walking out to (-30.25, 1.16, 51.00) (12.49m to mesh).
- day 3 18:36 | **WARN** | [AiNavigation] PalletJack: stuck at (-23.77, 1.11, 39.81) — walking out to (-30.25, 1.16, 51.00) (12.49m to mesh).
- day 3 18:47 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-16.61, 1.15, 41.39) — walking out to (-11.81, 1.50, 33.61) (8.15m to mesh).
- day 3 18:47 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-16.61, 1.15, 41.39) — walking out to (-11.81, 1.50, 33.61) (8.15m to mesh).
- day 3 19:34 | **WARN** | [AiNavigation] PalletJack: task target (-22.56, 1.14, 39.81) is not reachable — the partial path ends 18.24m short of it. Handing the task back.
- day 3 19:34 | **WARN** | [AiNavigation] PalletJack: task target (-22.56, 1.14, 39.81) is not reachable — the partial path ends 18.24m short of it. Handing the task back.
- day 3 19:40 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-03' (18.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__106:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1363)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 19:40 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-03' (18.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__106:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1363)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 19:55 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet e3bc5095-1e0d-47a6-83a0-76b106391555 but the employee is 11.7m away (max 4.5m) — refusing to start receiving.
- day 3 19:55 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet e3bc5095-1e0d-47a6-83a0-76b106391555 but the employee is 11.7m away (max 4.5m) — refusing to start receiving.
- day 3 19:59 | **WARN** | [AiNavigation] ReachTruck: stuck at (-35.12, 1.11, 45.46) — walking out to (-46.38, 1.20, 38.96) (12.00m to mesh).
- day 3 19:59 | **WARN** | [AiNavigation] ReachTruck: stuck at (-35.12, 1.11, 45.46) — walking out to (-46.38, 1.20, 38.96) (12.00m to mesh).
- day 3 20:09 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 47ab75b3-89cd-4331-9c10-f12aa54251e1 but the employee is 10.3m away (max 4.5m) — refusing to start receiving.
- day 3 20:09 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 47ab75b3-89cd-4331-9c10-f12aa54251e1 but the employee is 10.3m away (max 4.5m) — refusing to start receiving.
- day 3 20:28 | **WARN** | [OrderSelectionTaskDriver] Order feb33460-10b1-49d4-b846-40ad4f78a046 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 97/443 case(s).
- day 3 20:28 | **WARN** | [OrderSelectionTaskDriver] Order feb33460-10b1-49d4-b846-40ad4f78a046 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 97/443 case(s).
- day 3 21:35 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-33.97, 1.12, 48.76) — walking out to (-22.73, 1.12, 41.60) (12.32m to mesh).
- day 3 21:35 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-33.97, 1.12, 48.76) — walking out to (-22.73, 1.12, 41.60) (12.32m to mesh).
- day 4 03:43 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] 'Employee_Jonathan Cooper' made no progress on order OG3439 for 10 game minutes - handing the task back to the queue.
- day 4 03:43 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] 'Employee_Jonathan Cooper' made no progress on order OG3439 for 10 game minutes - handing the task back to the queue.
- day 4 04:05 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 69891473-8243-45f4-a1b0-1f24a1c48ec6 but the employee is 13.9m away (max 4.5m) — refusing to start receiving.
- day 4 04:05 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 69891473-8243-45f4-a1b0-1f24a1c48ec6 but the employee is 13.9m away (max 4.5m) — refusing to start receiving.
- day 4 04:06 | **REPEATING** | x25 and counting: [ReceivingTaskDriver] Employee_Amy Perez: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
- day 4 04:06 | **REPEATING** | x25 and counting: [ReceivingTaskDriver] Employee_Amy Perez: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
- day 4 04:13 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] 'Employee_Gregory Dixon' made no progress on order OG3439 for 10 game minutes - handing the task back to the queue.
- day 4 04:13 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] 'Employee_Gregory Dixon' made no progress on order OG3439 for 10 game minutes - handing the task back to the queue.
- day 4 04:29 | **REPEATING** | x25 and counting: [AiNavigation] PalletJack: task target (7.53, 1.16, 50.96) is not reachable — the partial path ends 19.80m short of it. Handing the task back.
- day 4 04:29 | **REPEATING** | x25 and counting: [AiNavigation] PalletJack: task target (7.53, 1.16, 50.96) is not reachable — the partial path ends 19.80m short of it. Handing the task back.
- day 4 05:19 | **WARN** | [AiNavigation] ReachTruck: stuck at (-23.54, 1.11, 40.71) — walking out to (-10.54, 1.16, 40.71) (12.00m to mesh).
- day 4 05:19 | **WARN** | [AiNavigation] ReachTruck: stuck at (-23.54, 1.11, 40.71) — walking out to (-10.54, 1.16, 40.71) (12.00m to mesh).
- day 4 05:40 | **WARN** | [AiNavigation] Employee_Gregory Dixon cannot reach equipment at (-26.56, 1.11, 40.91) (path invalid) — seeking next available
- day 4 05:40 | **WARN** | [AiNavigation] Employee_Gregory Dixon cannot reach equipment at (-26.56, 1.11, 40.91) (path invalid) — seeking next available
- day 4 06:18 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-33.22, 1.12, 47.38) — walking out to (-39.71, 1.08, 35.97) (12.13m to mesh).
- day 4 06:18 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-33.22, 1.12, 47.38) — walking out to (-39.71, 1.08, 35.97) (12.13m to mesh).
- day 4 06:55 | **WARN** | [AiNavigation] PalletJack: stuck at (-2.59, 1.11, 50.95) — walking out to (10.41, 1.16, 50.95) (12.00m to mesh).
- day 4 06:55 | **WARN** | [AiNavigation] PalletJack: stuck at (-2.59, 1.11, 50.95) — walking out to (10.41, 1.16, 50.95) (12.00m to mesh).
- day 4 07:22 | **WARN** | [AiNavigation] Dockstocker: stuck at (-47.27, 1.11, 36.39) — walking out to (-40.77, 1.16, 47.65) (12.00m to mesh).
- day 4 07:22 | **WARN** | [AiNavigation] Dockstocker: stuck at (-47.27, 1.11, 36.39) — walking out to (-40.77, 1.16, 47.65) (12.00m to mesh).
- day 4 08:48 | **WARN** | [AiNavigation] Dockstocker: stuck at (-36.20, 1.11, 39.01) — walking out to (-24.96, 1.16, 45.99) (12.23m to mesh).
- day 4 08:48 | **WARN** | [AiNavigation] Dockstocker: stuck at (-36.20, 1.11, 39.01) — walking out to (-24.96, 1.16, 45.99) (12.23m to mesh).
- day 4 09:50 | **WARN** | [AiNavigation] Dockstocker: stuck at (-37.59, 1.11, 45.46) — walking out to (-24.59, 1.16, 45.46) (12.00m to mesh).
- day 4 09:50 | **WARN** | [AiNavigation] Dockstocker: stuck at (-37.59, 1.11, 45.46) — walking out to (-24.59, 1.16, 45.46) (12.00m to mesh).
- day 4 09:57 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-34.11, 1.12, 48.76) — walking out to (-22.87, 1.12, 41.60) (12.32m to mesh).
- day 4 09:57 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-34.11, 1.12, 48.76) — walking out to (-22.87, 1.12, 41.60) (12.32m to mesh).
- day 4 10:09 | **WARN** | [AiNavigation] PalletJack: stuck at (-34.85, 1.11, 48.51) — walking out to (-23.61, 1.16, 41.35) (12.32m to mesh).
- day 4 10:09 | **WARN** | [AiNavigation] PalletJack: stuck at (-34.85, 1.11, 48.51) — walking out to (-23.61, 1.16, 41.35) (12.32m to mesh).
- day 4 11:43 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-40.81, 1.12, 51.23) — walking out to (-47.31, 1.08, 39.97) (12.00m to mesh).
- day 4 11:43 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (-40.81, 1.12, 51.23) — walking out to (-47.31, 1.08, 39.97) (12.00m to mesh).
- day 4 12:39 | **WARN** | [AiNavigation] ReachTruck: stuck at (-35.93, 1.11, 48.51) — walking out to (-24.69, 1.16, 41.35) (12.32m to mesh).
- day 4 12:39 | **WARN** | [AiNavigation] ReachTruck: stuck at (-35.93, 1.11, 48.51) — walking out to (-24.69, 1.16, 41.35) (12.32m to mesh).
- day 4 12:47 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-16.61, 1.16, 40.75) — walking out to (-11.81, 1.50, 32.97) (8.15m to mesh).
- day 4 12:47 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-16.61, 1.16, 40.75) — walking out to (-11.81, 1.50, 32.97) (8.15m to mesh).
- day 4 12:51 | **WARN** | [AiNavigation] Employee_Catherine Miller: stuck at (-34.02, 1.12, 48.76) — walking out to (-22.78, 1.12, 41.60) (12.32m to mesh).
- day 4 12:51 | **WARN** | [AiNavigation] Employee_Catherine Miller: stuck at (-34.02, 1.12, 48.76) — walking out to (-22.78, 1.12, 41.60) (12.32m to mesh).
- day 4 13:11 | **WARN** | [AiNavigation] PalletJack: stuck at (-13.21, 1.11, 40.61) — walking out to (-0.21, 1.16, 40.61) (12.00m to mesh).
- day 4 13:11 | **WARN** | [AiNavigation] PalletJack: stuck at (-13.21, 1.11, 40.61) — walking out to (-0.21, 1.16, 40.61) (12.00m to mesh).
- day 4 14:03 | **WARN** | [AiNavigation] PalletJack cannot reach task position at (-19.91, 1.15, 39.81) (path invalid) — abandoning the walk and handing the task back.
- day 4 14:03 | **WARN** | [AiNavigation] PalletJack cannot reach task position at (-19.91, 1.15, 39.81) (path invalid) — abandoning the walk and handing the task back.
- day 4 18:03 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] 'Employee_Catherine Miller' made no progress on order OG4441 for 10 game minutes - handing the task back to the queue.
- day 4 18:03 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] 'Employee_Catherine Miller' made no progress on order OG4441 for 10 game minutes - handing the task back to the queue.
- day 4 18:20 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-30.47, 1.11, 39.91) — walking out to (-36.96, 1.12, 51.33) (12.18m to mesh).
- day 4 18:20 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-30.47, 1.11, 39.91) — walking out to (-36.96, 1.12, 51.33) (12.18m to mesh).
- day 4 19:00 | **DOOR-DEADLOCK** | OG3435 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y1.5] [2: own/Staged/y1.2] 
- day 4 19:00 | **DOOR-DEADLOCK** | OG3435 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y1.5] [2: own/Staged/y1.2] 
- day 4 20:02 | **WARN** | [AiNavigation] ReachTruck: stuck at (-31.31, 1.11, 45.48) — walking out to (-44.31, 1.16, 45.48) (12.00m to mesh).
- day 4 20:02 | **WARN** | [AiNavigation] ReachTruck: stuck at (-31.31, 1.11, 45.48) — walking out to (-44.31, 1.16, 45.48) (12.00m to mesh).
- day 4 21:51 | **WARN** | [AiNavigation] PalletJack: stuck at (-15.12, 1.11, 51.00) — walking out to (-28.12, 1.16, 51.00) (12.00m to mesh).
- day 4 21:51 | **WARN** | [AiNavigation] PalletJack: stuck at (-15.12, 1.11, 51.00) — walking out to (-28.12, 1.16, 51.00) (12.00m to mesh).
- day 4 22:29 | **WARN** | [AiNavigation] Dockstocker: stuck at (7.94, 1.11, 49.64) — walking out to (-5.06, 1.16, 49.64) (12.00m to mesh).
- day 4 22:29 | **WARN** | [AiNavigation] Dockstocker: stuck at (7.94, 1.11, 49.64) — walking out to (-5.06, 1.16, 49.64) (12.00m to mesh).
- day 4 23:30 | **DOOR-DEADLOCK** | OG3435 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y1.5] [2: own/Staged/y1.2] 
- day 4 23:30 | **DOOR-DEADLOCK** | OG3435 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y1.5] [2: own/Staged/y1.2] 
- day 4 23:44 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-37.78, 1.12, 46.53) — walking out to (-44.28, 1.08, 35.27) (12.00m to mesh).
- day 4 23:44 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-37.78, 1.12, 46.53) — walking out to (-44.28, 1.08, 35.27) (12.00m to mesh).
- day 4 23:44 | **WARN** | [AiNavigation] Dockstocker: stuck at (-44.67, 1.11, 42.99) — walking out to (-33.43, 1.11, 36.06) (12.21m to mesh).
- day 4 23:44 | **WARN** | [AiNavigation] Dockstocker: stuck at (-44.67, 1.11, 42.99) — walking out to (-33.43, 1.11, 36.06) (12.21m to mesh).
- day 5 00:49 | **WARN** | [AiNavigation] Dockstocker: stuck at (5.22, 1.11, 50.96) — walking out to (-7.78, 1.16, 50.96) (12.00m to mesh).
- day 5 00:49 | **WARN** | [AiNavigation] Dockstocker: stuck at (5.22, 1.11, 50.96) — walking out to (-7.78, 1.16, 50.96) (12.00m to mesh).
- day 5 01:38 | **WARN** | [AiNavigation] Employee_Gregory Dixon: stuck at (-38.17, 1.12, 45.21) — walking out to (-26.91, 1.10, 38.71) (12.00m to mesh).
- day 5 01:38 | **WARN** | [AiNavigation] Employee_Gregory Dixon: stuck at (-38.17, 1.12, 45.21) — walking out to (-26.91, 1.10, 38.71) (12.00m to mesh).
- day 5 01:47 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-31.74, 1.12, 51.33) — walking out to (-31.74, 1.10, 38.59) (12.00m to mesh).
- day 5 01:47 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-31.74, 1.12, 51.33) — walking out to (-31.74, 1.10, 38.59) (12.00m to mesh).
- day 5 02:11 | **WARN** | [AiNavigation] PalletJack: stuck at (-36.00, 1.11, 45.46) — walking out to (-47.26, 1.19, 38.96) (12.00m to mesh).
- day 5 02:11 | **WARN** | [AiNavigation] PalletJack: stuck at (-36.00, 1.11, 45.46) — walking out to (-47.26, 1.19, 38.96) (12.00m to mesh).
- day 5 03:00 | **DOOR-DEADLOCK** | OG3435 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y1.5] [2: own/Staged/y1.2] 
- day 5 03:00 | **DOOR-DEADLOCK** | OG3435 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y1.5] [2: own/Staged/y1.2] 
- day 5 03:11 | **WARN** | [AiNavigation] Dockstocker: stuck at (-40.35, 1.11, 43.13) — walking out to (-29.11, 1.12, 36.05) (12.28m to mesh).
- day 5 03:11 | **WARN** | [AiNavigation] Dockstocker: stuck at (-40.35, 1.11, 43.13) — walking out to (-29.11, 1.12, 36.05) (12.28m to mesh).
- day 5 03:34 | **WARN** | [AiNavigation] PalletJack: stuck at (-34.43, 1.11, 48.51) — walking out to (-23.20, 1.16, 41.35) (12.32m to mesh).
- day 5 03:34 | **WARN** | [AiNavigation] PalletJack: stuck at (-34.43, 1.11, 48.51) — walking out to (-23.20, 1.16, 41.35) (12.32m to mesh).
- day 5 05:04 | **WARN** | [OrderService] Order OG4441 closed with a IN TRANSIT pallet (40 cs) that never made the trailer - not billed, returning it to stock.
- day 5 05:04 | **WARN** | [OrderService] Order OG4441 closed with a IN TRANSIT pallet (40 cs) that never made the trailer - not billed, returning it to stock.
- day 5 05:30 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1B-1 (order b9ce884d-b89b-4b83-a75d-be8142d89063 gone)
- day 5 05:30 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1B-1 (order b9ce884d-b89b-4b83-a75d-be8142d89063 gone)
- day 5 05:46 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (1.02, 1.12, 51.25) — walking out to (-10.24, 1.16, 44.75) (12.00m to mesh).
- day 5 05:46 | **WARN** | [AiNavigation] Employee_Jonathan Cooper: stuck at (1.02, 1.12, 51.25) — walking out to (-10.24, 1.16, 44.75) (12.00m to mesh).
- day 5 06:04 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack did not reach 'bay 01-02' ((-23.91, 1.16, 40.91)) after 8.4s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__110:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1546)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 5 06:04 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack did not reach 'bay 01-02' ((-23.91, 1.16, 40.91)) after 8.4s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__110:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1546)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 5 06:04 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-43.76, 1.12, 48.31) — walking out to (-32.51, 1.12, 41.63) (12.09m to mesh).
- day 5 06:04 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-43.76, 1.12, 48.31) — walking out to (-32.51, 1.12, 41.63) (12.09m to mesh).
- day 5 06:50 | **WARN** | [AiNavigation] Dockstocker: stuck at (-31.08, 1.11, 48.27) — walking out to (-19.84, 1.16, 41.36) (12.20m to mesh).
- day 5 06:50 | **WARN** | [AiNavigation] Dockstocker: stuck at (-31.08, 1.11, 48.27) — walking out to (-19.84, 1.16, 41.36) (12.20m to mesh).
- day 5 07:17 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-30.82, 1.18, 50.56) — walking out to (-37.32, 1.10, 39.30) (12.00m to mesh).
- day 5 07:17 | **WARN** | [AiNavigation] Employee_Brandon Thompson: stuck at (-30.82, 1.18, 50.56) — walking out to (-37.32, 1.10, 39.30) (12.00m to mesh).
- day 5 07:24 | **WARN** | [AiNavigation] Employee_Gregory Dixon: stuck at (-35.14, 1.14, 46.20) — walking out to (-41.64, 1.08, 35.17) (12.00m to mesh).
- day 5 07:24 | **WARN** | [AiNavigation] Employee_Gregory Dixon: stuck at (-35.14, 1.14, 46.20) — walking out to (-41.64, 1.08, 35.17) (12.00m to mesh).
- day 5 09:38 | **WARN** | [AiNavigation] PalletJack: stuck at (-41.16, 1.11, 46.83) — walking out to (-47.66, 1.09, 35.58) (12.00m to mesh).
- day 5 09:38 | **WARN** | [AiNavigation] PalletJack: stuck at (-41.16, 1.11, 46.83) — walking out to (-47.66, 1.09, 35.58) (12.00m to mesh).
- day 5 11:17 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-32.08, 1.18, 48.76) — walking out to (-20.85, 1.12, 41.60) (12.32m to mesh).
- day 5 11:17 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-32.08, 1.18, 48.76) — walking out to (-20.85, 1.12, 41.60) (12.32m to mesh).
- day 5 13:18 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (-41.79, 1.16, 50.96) (path invalid) — abandoning the walk and handing the task back.
- day 5 13:18 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (-41.79, 1.16, 50.96) (path invalid) — abandoning the walk and handing the task back.
- day 5 14:22 | **WARN** | [AiNavigation] Dockstocker: task target (-45.76, 1.18, 36.39) is not reachable — the partial path ends 32.01m short of it. Handing the task back.
- day 5 14:22 | **WARN** | [AiNavigation] Dockstocker: task target (-45.76, 1.18, 36.39) is not reachable — the partial path ends 32.01m short of it. Handing the task back.
- day 5 15:27 | **WARN** | [AiNavigation] ReachTruck: stuck at (-28.76, 1.11, 40.01) — walking out to (-35.25, 1.16, 51.00) (12.31m to mesh).
- day 5 15:27 | **WARN** | [AiNavigation] ReachTruck: stuck at (-28.76, 1.11, 40.01) — walking out to (-35.25, 1.16, 51.00) (12.31m to mesh).
- day 5 15:56 | **WARN** | [AiNavigation] ReachTruck cannot reach task position at (7.24, 1.16, 48.99) (path invalid) — abandoning the walk and handing the task back.
- day 5 15:56 | **WARN** | [AiNavigation] ReachTruck cannot reach task position at (7.24, 1.16, 48.99) (path invalid) — abandoning the walk and handing the task back.
- day 5 21:30 | **DOOR-DEADLOCK** | OG3435 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y1.5] [2: own/Staged/y1.2] 
- day 5 21:30 | **DOOR-DEADLOCK** | OG3435 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y1.5] [2: own/Staged/y1.2] 
- day 6 00:16 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (-44.44, 1.15, 36.39) (path invalid) — abandoning the walk and handing the task back.
- day 6 00:16 | **WARN** | [AiNavigation] Dockstocker cannot reach task position at (-44.44, 1.15, 36.39) (path invalid) — abandoning the walk and handing the task back.
- day 6 02:00 | **OVERLAP** | OVERLAP Pallet_04_Slot2_Tier0 & Pallet_08_Slot4_Tier0 @3B-4  <<HISTORY: d5 19:00 Putaway task created (STG3C-2->) by OrderService.RefileStrandedLanePallets | d5 21:02 moved (55, 82)->(-1, -1) by InventoryService.MovePallet | d5 21:10 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 21:10 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 21:10 record destroyed by InventoryService.DestroyPallet | d5 21:10 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d5 20:20 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 21:11 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 21:17 moved (56, 85)->(-1, -1) by InventoryService.MovePallet | d5 21:35 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 21:35 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 21:35 record destroyed by InventoryService.DestroyPallet>>
- day 6 02:00 | **FLOATING** | FLOATING Pallet_03_Slot1_Tier1 @3B-3 bottom=2.04  <<HISTORY: d5 18:20 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 19:05 moved (55, 84)->(-1, -1) by InventoryService.MovePallet | d5 19:13 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 19:13 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 19:13 record destroyed by InventoryService.DestroyPallet | d5 19:13 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 6 02:00 | **OVERLAP** | OVERLAP Pallet_03_Slot1_Tier1 & Pallet_02_Slot1_Tier0 @3B-3  <<HISTORY: d5 18:20 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 19:05 moved (55, 84)->(-1, -1) by InventoryService.MovePallet | d5 19:13 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 19:13 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 19:13 record destroyed by InventoryService.DestroyPallet | d5 19:13 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d5 18:33 Putaway task created (STG3C-3->) by OrderService.RefileStrandedLanePallets | d5 18:50 moved (55, 84)->(-1, -1) by InventoryService.MovePallet | d5 18:56 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 18:56 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 18:56 record destroyed by InventoryService.DestroyPallet | d5 18:56 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 6 02:00 | **FLOATING** | FLOATING Pallet_08_Slot4_Tier0 @3B-4 bottom=2.04  <<HISTORY: d5 20:20 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 21:11 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 21:17 moved (56, 85)->(-1, -1) by InventoryService.MovePallet | d5 21:35 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 21:35 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 21:35 record destroyed by InventoryService.DestroyPallet>>
- day 6 02:00 | **OVERLAP** | OVERLAP Pallet_04_Slot2_Tier0 & Pallet_08_Slot4_Tier0 @3B-4  <<HISTORY: d5 19:00 Putaway task created (STG3C-2->) by OrderService.RefileStrandedLanePallets | d5 21:02 moved (55, 82)->(-1, -1) by InventoryService.MovePallet | d5 21:10 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 21:10 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 21:10 record destroyed by InventoryService.DestroyPallet | d5 21:10 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d5 20:20 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 21:11 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 21:17 moved (56, 85)->(-1, -1) by InventoryService.MovePallet | d5 21:35 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 21:35 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 21:35 record destroyed by InventoryService.DestroyPallet>>
- day 6 02:00 | **FLOATING** | FLOATING Pallet_03_Slot1_Tier1 @3B-3 bottom=2.04  <<HISTORY: d5 18:20 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 19:05 moved (55, 84)->(-1, -1) by InventoryService.MovePallet | d5 19:13 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 19:13 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 19:13 record destroyed by InventoryService.DestroyPallet | d5 19:13 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 6 02:00 | **OVERLAP** | OVERLAP Pallet_03_Slot1_Tier1 & Pallet_02_Slot1_Tier0 @3B-3  <<HISTORY: d5 18:20 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 19:05 moved (55, 84)->(-1, -1) by InventoryService.MovePallet | d5 19:13 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 19:13 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 19:13 record destroyed by InventoryService.DestroyPallet | d5 19:13 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d5 18:33 Putaway task created (STG3C-3->) by OrderService.RefileStrandedLanePallets | d5 18:50 moved (55, 84)->(-1, -1) by InventoryService.MovePallet | d5 18:56 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 18:56 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 18:56 record destroyed by InventoryService.DestroyPallet | d5 18:56 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 6 02:00 | **FLOATING** | FLOATING Pallet_08_Slot4_Tier0 @3B-4 bottom=2.04  <<HISTORY: d5 20:20 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 21:11 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 21:17 moved (56, 85)->(-1, -1) by InventoryService.MovePallet | d5 21:35 adopted as outbound freight of order 9a81f68d-c0a6-49c1-83f1-a33f097a8da3 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 21:35 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 21:35 record destroyed by InventoryService.DestroyPallet>>
- day 6 03:41 | **WARN** | [AiNavigation] Dockstocker: stuck at (-2.29, 1.16, 46.54) — walking out to (4.96, 1.08, 35.50) (12.81m to mesh).
- day 6 03:41 | **WARN** | [AiNavigation] Dockstocker: stuck at (-2.29, 1.16, 46.54) — walking out to (4.96, 1.08, 35.50) (12.81m to mesh).
- day 6 05:00 | **DOOR-DEADLOCK** | OG4442 is Staged but door 3's truck has waited 90 min with no one loading it. | lane 3A: [1: own/Staged/y1.5, own/Staged/y1.2] [2: own/Staged/y2.0, own/Staged/y1.2] [3: own/Staged/y1.2] 
- day 6 05:00 | **DOOR-DEADLOCK** | OG4442 is Staged but door 3's truck has waited 90 min with no one loading it. | lane 3A: [1: own/Staged/y1.5, own/Staged/y1.2] [2: own/Staged/y2.0, own/Staged/y1.2] [3: own/Staged/y1.2] 
- day 6 06:50 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-24.45, 1.11, 40.30) — walking out to (-30.95, 1.12, 51.33) (12.00m to mesh).
- day 6 06:50 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-24.45, 1.11, 40.30) — walking out to (-30.95, 1.12, 51.33) (12.00m to mesh).
- day 6 07:50 | **WARN** | [AiNavigation] Employee_Catherine Miller: stuck at (-40.17, 1.13, 49.47) — walking out to (-28.96, 1.11, 41.56) (12.72m to mesh).
- day 6 07:50 | **WARN** | [AiNavigation] Employee_Catherine Miller: stuck at (-40.17, 1.13, 49.47) — walking out to (-28.96, 1.11, 41.56) (12.72m to mesh).
- day 6 10:11 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 19.1m short of 'bay 01-07' - re-issuing the drive (1/6).
- day 6 10:11 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 19.1m short of 'bay 01-07' - re-issuing the drive (1/6).
- day 6 11:06 | **WARN** | [AiNavigation] Dockstocker: task target (-3.71, 1.16, 50.96) is not reachable — the partial path ends 18.50m short of it. Handing the task back.
- day 6 11:06 | **WARN** | [AiNavigation] Dockstocker: task target (-3.71, 1.16, 50.96) is not reachable — the partial path ends 18.50m short of it. Handing the task back.
- day 6 11:55 | **WARN** | [AiNavigation] ReachTruck: stuck at (-23.46, 1.11, 40.01) — walking out to (-29.95, 1.16, 51.00) (12.31m to mesh).
- day 6 11:55 | **WARN** | [AiNavigation] ReachTruck: stuck at (-23.46, 1.11, 40.01) — walking out to (-29.95, 1.16, 51.00) (12.31m to mesh).
- day 6 12:30 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→PO_IG6735 & Truck→PO_IG6734 at (0.9, 0.0, 4.4)
- day 6 12:30 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→PO_IG6735 & Truck→PO_IG6734 at (0.9, 0.0, 4.4)
- day 6 12:46 | **WARN** | [AiNavigation] Dockstocker: stuck at (-34.06, 1.11, 45.99) — walking out to (-45.32, 1.21, 39.49) (12.00m to mesh).
- day 6 12:46 | **WARN** | [AiNavigation] Dockstocker: stuck at (-34.06, 1.11, 45.99) — walking out to (-45.32, 1.21, 39.49) (12.00m to mesh).
- day 6 13:33 | **WARN** | [OrderSelectionTaskDriver] Order 62406c1d-22ad-4824-bd6e-f929ef5af93a (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 89/96.
- day 6 13:33 | **WARN** | [OrderSelectionTaskDriver] Order 62406c1d-22ad-4824-bd6e-f929ef5af93a (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 89/96.
- day 6 13:56 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-03-B1 anchor: NO NAVMESH PATH. anchor=(-35.16, 5.11, 47.34) → driveTarget=(-35.16, 1.16, 47.34), from=(-38.17, 1.11, 43.61), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=False, navEnabled=True, seekingTask=True, pathStatus=n/a, hasPath=False, remaining=n/a, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x0024a] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:992 < ] 38.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 13:56 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-03-B1 anchor: NO NAVMESH PATH. anchor=(-35.16, 5.11, 47.34) → driveTarget=(-35.16, 1.16, 47.34), from=(-38.17, 1.11, 43.61), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=False, navEnabled=True, seekingTask=True, pathStatus=n/a, hasPath=False, remaining=n/a, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x0024a] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:992 < ] 38.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 15:55 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-03-B1 anchor: NO NAVMESH PATH. anchor=(-35.16, 5.11, 47.34) → driveTarget=(-35.16, 1.16, 47.34), from=(-27.84, 1.12, 45.75), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=False, navEnabled=True, seekingTask=True, pathStatus=n/a, hasPath=False, remaining=n/a, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x0024a] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:992 < ] 36.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 15:55 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-03-B1 anchor: NO NAVMESH PATH. anchor=(-35.16, 5.11, 47.34) → driveTarget=(-35.16, 1.16, 47.34), from=(-27.84, 1.12, 45.75), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=False, navEnabled=True, seekingTask=True, pathStatus=n/a, hasPath=False, remaining=n/a, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x0024a] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:992 < ] 36.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 15:57 | **WARN** | [AiNavigation] PalletJack: stuck at (-18.05, 1.11, 42.16) — walking out to (-13.54, 1.08, 35.50) (7.77m to mesh).
- day 6 15:57 | **WARN** | [AiNavigation] PalletJack: stuck at (-18.05, 1.11, 42.16) — walking out to (-13.54, 1.08, 35.50) (7.77m to mesh).
- day 6 16:07 | **WARN** | [OrderSelectionTaskDriver] Order 7682bab0-a330-4b61-8458-1a1ab87a96b8 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 35/37.
- day 6 16:07 | **WARN** | [OrderSelectionTaskDriver] Order 7682bab0-a330-4b61-8458-1a1ab87a96b8 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 35/37.
- day 6 17:33 | **WARN** | [AiNavigation] PalletJack: task target (-28.61, 1.16, 39.81) is not reachable — the partial path ends 15.13m short of it. Handing the task back.
- day 6 17:33 | **WARN** | [AiNavigation] PalletJack: task target (-28.61, 1.16, 39.81) is not reachable — the partial path ends 15.13m short of it. Handing the task back.
- day 6 17:42 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-04' (15.1m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__106:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1363)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 17:42 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-04' (15.1m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__106:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1363)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 18:30 | **DOOR-DEADLOCK** | OG3435 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y1.5] [2: own/Staged/y1.2] 
- day 6 18:30 | **DOOR-DEADLOCK** | OG3435 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y1.5] [2: own/Staged/y1.2] 
- day 6 19:00 | **DOOR-DEADLOCK** | OG4442 is Staged but door 3's truck has waited 90 min with no one loading it. | lane 3A: [1: own/Staged/y1.5, own/Staged/y1.2] [2: own/Staged/y2.0, own/Staged/y1.2] [3: own/Staged/y1.2] 
- day 6 19:00 | **DOOR-DEADLOCK** | OG4442 is Staged but door 3's truck has waited 90 min with no one loading it. | lane 3A: [1: own/Staged/y1.5, own/Staged/y1.2] [2: own/Staged/y2.0, own/Staged/y1.2] [3: own/Staged/y1.2] 
- day 6 19:05 | **ERROR** | [ReachTruckOperator] 'ReachTruck' replenish: → reserve 01-05-A0 anchor: NO NAVMESH PATH. anchor=(-24.56, 3.11, 40.71) → driveTarget=(-24.56, 1.16, 40.71), from=(-23.29, 1.11, 41.34), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=1.4, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at EquipmentBattery.ParkNav () [0x0000f] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\EquipmentBattery.cs:1174 < at EquipmentBattery+<NavTo>d__205.MoveNext () [0x00440] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\EquipmentBattery.cs:1163 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 92.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 19:05 | **ERROR** | [ReachTruckOperator] 'ReachTruck' replenish: → reserve 01-05-A0 anchor: NO NAVMESH PATH. anchor=(-24.56, 3.11, 40.71) → driveTarget=(-24.56, 1.16, 40.71), from=(-23.29, 1.11, 41.34), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=1.4, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at EquipmentBattery.ParkNav () [0x0000f] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\EquipmentBattery.cs:1174 < at EquipmentBattery+<NavTo>d__205.MoveNext () [0x00440] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\EquipmentBattery.cs:1163 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 92.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 21:19 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: no progress toward task target (-47.09, 1.18, 36.39) for 10s (stopped 15.01m short, path PathComplete) — handing the task back.
- day 6 21:19 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: no progress toward task target (-47.09, 1.18, 36.39) for 10s (stopped 15.01m short, path PathComplete) — handing the task back.
- day 6 22:34 | **WARN** | [AiNavigation] Employee_Catherine Miller: stuck at (-33.13, 1.15, 40.17) — walking out to (-46.13, 1.08, 40.17) (12.00m to mesh).
- day 6 22:34 | **WARN** | [AiNavigation] Employee_Catherine Miller: stuck at (-33.13, 1.15, 40.17) — walking out to (-46.13, 1.08, 40.17) (12.00m to mesh).
- day 6 23:54 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-27.05, 1.14, 40.66) — walking out to (-40.05, 1.18, 40.66) (12.00m to mesh).
- day 6 23:54 | **WARN** | [AiNavigation] Employee_Justin Reyes: stuck at (-27.05, 1.14, 40.66) — walking out to (-40.05, 1.18, 40.66) (12.00m to mesh).
- day 6 23:54 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-27.57, 1.14, 40.83) — walking out to (-40.57, 1.17, 40.83) (12.00m to mesh).
- day 6 23:54 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-27.57, 1.14, 40.83) — walking out to (-40.57, 1.17, 40.83) (12.00m to mesh).
- day 6 23:55 | **WARN** | [OrderSelectionTaskDriver] Order 52e4791d-d2a1-4a60-b8a6-e0c9c1d4cbd0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 155/441.
- day 6 23:55 | **WARN** | [OrderSelectionTaskDriver] Order 52e4791d-d2a1-4a60-b8a6-e0c9c1d4cbd0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 155/441.
- day 7 00:11 | **WARN** | [AiNavigation] Employee_Catherine Miller cannot reach task position at (-15.09, 1.08, 35.29) (path invalid) — abandoning the walk and handing the task back.
- day 7 00:18 | **WARN** | [AiNavigation] PltJack_FULL(Clone): task target (-31.86, 1.16, 40.91) is not reachable — the partial path ends 18.56m short of it. Handing the task back.
- day 7 00:20 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-20.44, 1.11, 41.89) — walking out to (-9.18, 1.23, 48.39) (12.00m to mesh).
- day 7 00:27 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-05' (18.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__106:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1363)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 7 00:48 | **WARN** | [OrderSelectionTaskDriver] Order 52e4791d-d2a1-4a60-b8a6-e0c9c1d4cbd0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 172/441 case(s).
- day 7 00:59 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-18.83, 1.11, 41.89) — walking out to (-13.54, 1.11, 35.50) (7.94m to mesh).
- day 7 00:59 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-26.21, 1.11, 41.89) — walking out to (-14.95, 1.23, 48.39) (12.00m to mesh).
- day 7 01:05 | **WARN** | [AiNavigation] DS_FULL(Clone): task target (-41.79, 1.09, 36.39) is not reachable — the partial path ends 28.51m short of it. Handing the task back.
- day 7 01:53 | **WARN** | [OrderSelectionTaskDriver] Order 52e4791d-d2a1-4a60-b8a6-e0c9c1d4cbd0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 185/441.
- day 7 03:11 | **DOOR-DEADLOCK** | OG3435 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.5, own/Staged/y1.2] [2: own/Staged/y1.2] 
- day 7 03:30 | **WARN** | [OrderSelectionTaskDriver] Order 52e4791d-d2a1-4a60-b8a6-e0c9c1d4cbd0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 196/441.
- day 7 06:28 | **WARN** | [OrderService] Order OG6446 closed with a IN TRANSIT pallet (8 cs) that never made the trailer - not billed, returning it to stock.
- day 7 06:55 | **WARN** | [AiNavigation] Employee_Justin Reyes cannot reach equipment at (-37.16, 1.11, 40.91) (path invalid) — seeking next available
- day 7 07:11 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1C-1 (order 52e4791d-d2a1-4a60-b8a6-e0c9c1d4cbd0 gone)
- day 7 07:22 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Justin Reyes' as its operator though nobody is on it or running it - clearing the stale link.
- day 7 07:23 | **WARN** | [AiNavigation] Employee_Catherine Miller cannot reach task position at (-22.14, 1.11, 40.75) (path invalid) — abandoning the walk and handing the task back.
- day 7 08:16 | **REPEATING** | x25 and counting: [AiNavigation] PltJack_FULL(Clone): task target (-3.07, 1.16, 50.96) is not reachable — the partial path ends 18.56m short of it. Handing the task back.
- day 7 08:59 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (-25.89, 1.16, 40.71) (path invalid) — abandoning the walk and handing the task back.
- day 7 11:22 | **WARN** | [OrderSelectionTaskDriver] Order a65a70e4-3687-43af-bce9-78a9508bb15d (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 315/385.
- day 7 12:05 | **WARN** | [AiNavigation] Employee_Gregory Dixon cannot reach task position at (-0.07, 1.11, 49.96) (path invalid) — abandoning the walk and handing the task back.
- day 7 13:33 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-37.36, 1.11, 38.84) — walking out to (-26.13, 1.16, 46.00) (12.32m to mesh).
- day 7 14:14 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane 3D exit: NO NAVMESH PATH. anchor=(8.56, 1.11, 48.99) → driveTarget=(8.56, 1.21, 48.99), from=(0.65, 1.11, 44.42), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=9.5, reissues=1, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__128.MoveNext () [0x00681] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2760 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 9.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 7 14:50 | **WARN** | [AiNavigation] DS_FULL(Clone) cannot reach task position at (-47.09, 1.12, 49.64) (path invalid) — abandoning the walk and handing the task back.
- day 7 15:31 | **WARN** | [AiNavigation] Employee_Amy Perez: stuck at (-31.36, 1.11, 40.50) — walking out to (-18.36, 1.19, 40.50) (12.00m to mesh).
- day 7 19:11 | **FLOATING** | FLOATING A Chep @2D-1 bottom=1.92  <<HISTORY: d7 02:18 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 04:31 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 04:47 moved (28, 82)->(-1, -1) by InventoryService.MovePallet | d7 05:06 adopted as outbound freight of order a65a70e4-3687-43af-bce9-78a9508bb15d (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 05:06 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 05:06 record destroyed by InventoryService.DestroyPallet>>
- day 7 19:11 | **OVERLAP** | OVERLAP A Chep & A Chep @2D-1  <<HISTORY: d7 02:18 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 04:31 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 04:47 moved (28, 82)->(-1, -1) by InventoryService.MovePallet | d7 05:06 adopted as outbound freight of order a65a70e4-3687-43af-bce9-78a9508bb15d (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 05:06 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 05:06 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d7 02:18 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 04:16 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 04:33 moved (28, 83)->(-1, -1) by InventoryService.MovePallet | d7 04:48 adopted as outbound freight of order a65a70e4-3687-43af-bce9-78a9508bb15d (24 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 04:48 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 04:48 record destroyed by InventoryService.DestroyPallet>>
- day 7 19:11 | **FLOATING** | FLOATING OutboundPallet_a65a70e4-3687-43af-bce9-78a9508bb15d_1 @2C-4 bottom=2.22
- day 7 19:11 | **OVERLAP** | OVERLAP OutboundPallet_a65a70e4-3687-43af-bce9-78a9508bb15d_1 & OutboundPallet_a65a70e4-3687-43af-bce9-78a9508bb15d_1 @2C-4
- day 7 20:11 | **FLOATING** | FLOATING A Chep @2D-1 bottom=1.92  <<HISTORY: d7 02:18 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 04:31 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 04:47 moved (28, 82)->(-1, -1) by InventoryService.MovePallet | d7 05:06 adopted as outbound freight of order a65a70e4-3687-43af-bce9-78a9508bb15d (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 05:06 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 05:06 record destroyed by InventoryService.DestroyPallet>>
- day 7 20:11 | **OVERLAP** | OVERLAP A Chep & A Chep @2D-1  <<HISTORY: d7 02:18 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 04:31 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 04:47 moved (28, 82)->(-1, -1) by InventoryService.MovePallet | d7 05:06 adopted as outbound freight of order a65a70e4-3687-43af-bce9-78a9508bb15d (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 05:06 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 05:06 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d7 02:18 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 04:16 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 04:33 moved (28, 83)->(-1, -1) by InventoryService.MovePallet | d7 04:48 adopted as outbound freight of order a65a70e4-3687-43af-bce9-78a9508bb15d (24 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 04:48 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 04:48 record destroyed by InventoryService.DestroyPallet>>
- day 7 20:22 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-18.05, 1.11, 49.15) — walking out to (-29.26, 1.16, 41.31) (12.68m to mesh).
- day 7 20:39 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-05-B0 anchor: NO NAVMESH PATH. anchor=(-33.84, 5.11, 47.34) → driveTarget=(-33.84, 1.16, 47.34), from=(-41.33, 1.11, 43.87), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=5.9, reissues=0, batteryNeedsCharge=True, lastSeekCancelledBy=[at AiNavigation.Update () [0x0024a] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:992 < ] 38.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 7 22:19 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-28.54, 1.11, 39.22) — walking out to (-39.79, 1.16, 45.98) (12.13m to mesh).
- day 7 22:34 | **WARN** | [OrderSelectionTaskDriver] Order 54c95f85-7cd0-4236-b604-3a9e379acae6 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 196/448.
- day 7 23:11 | **DOOR-DEADLOCK** | OG4440 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2A: [1: own/Staged/y1.6, own/Staged/y1.2] [2: own/Staged/y1.2] 
- day 7 23:38 | **WARN** | [OrderService] Order OG7450 closed with a IN TRANSIT pallet (8 cs) that never made the trailer - not billed, returning it to stock.
- day 7 23:49 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-30.18, 1.11, 41.89) — walking out to (-18.93, 1.23, 48.39) (12.00m to mesh).
