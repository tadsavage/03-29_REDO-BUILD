# Bug log - block 18

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 16:00 | **REPEATING** | x25 and counting: [NoWaypointIndicator] Alert emote 'emote_exclamations' not found in Resources/Emotes.
- day 1 18:53 | **NO-TASK** | NO-TASK lane pallet 3B-5 sku=172495 load=3615898260  <<HISTORY: d1 18:43 Putaway task created (STG3B-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 18:43 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d1 18:43 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d1 18:43 PalletPick task Open->Available by OrderService.TryAssignInboundPalletToPendingPick | d1 18:43 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d1 18:51 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 1 22:00 | **TRUCK-STALLED** | TRUCK-STALLED Truck→PO_IG1955 state=DepartToApproach 97s at (4.6, 0.0, 18.5)
- day 2 05:53 | **NO-TASK** | NO-TASK lane pallet 2C-1 sku=102435 load=5062486690  <<HISTORY: d2 03:07 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 03:17 Putaway task created (STG2C-1->) by ReceiverReceivingWorkflow.CreatePutawayTask | d2 03:17 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 03:17 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 03:17 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d2 05:52 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 2 07:51 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (9.71, 0.00, 43.02) (cell (57, 82)). Putting it back on its last inside spot (9.02, 1.11, 43.36). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 2 07:51 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (9.71, 0.00, 43.02) (cell (57, 82)). Putting it back on its last inside spot (9.02, 1.11, 43.36). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 2 10:26 | **WARN** | [AiNavigation] ReachTruck: stuck at (-24.00, 1.11, 31.29) — walking out to (-12.75, 0.08, 24.48) (12.19m to mesh).
- day 2 10:26 | **WARN** | [AiNavigation] ReachTruck: stuck at (-24.00, 1.11, 31.29) — walking out to (-12.75, 0.08, 24.48) (12.19m to mesh).
- day 2 13:29 | **ERROR** | [MHEBoundary] 'ReachTruck' drove OUT of the building at (-24.56, 0.00, 45.48) (cell (31, 84)). Putting it back on its last inside spot (-24.40, 1.11, 44.76). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 2 13:29 | **ERROR** | [MHEBoundary] 'ReachTruck' drove OUT of the building at (-24.56, 0.00, 45.48) (cell (31, 84)). Putting it back on its last inside spot (-24.40, 1.11, 44.76). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 2 19:00 | **REPEATING** | x25 and counting: [NoWaypointIndicator] Alert emote 'emote_exclamations' not found in Resources/Emotes.
- day 2 22:08 | **WARN** | [AiNavigation] Dockstocker: stuck at (-17.29, 1.11, 39.20) — walking out to (-30.28, 1.15, 37.81) (12.07m to mesh).
- day 2 22:08 | **WARN** | [AiNavigation] Dockstocker: stuck at (-17.29, 1.11, 39.20) — walking out to (-30.28, 1.15, 37.81) (12.07m to mesh).
- day 2 23:43 | **ERROR** | [MHEBoundary] 'PalletJack' drove OUT of the building at (-43.79, 1.11, 29.39) (cell (16, 72)). Putting it back on its last inside spot (-43.35, 1.11, 29.42). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 2 23:43 | **ERROR** | [MHEBoundary] 'PalletJack' drove OUT of the building at (-43.79, 1.11, 29.39) (cell (16, 72)). Putting it back on its last inside spot (-43.35, 1.11, 29.42). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 3 00:53 | **NO-TASK** | NO-TASK lane pallet 3A-1 sku=141256 load=4630855427  <<HISTORY: d2 21:30 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d2 21:39 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 21:40 Putaway task created (STG3A-1->) by OrderService.RefileStrandedLanePallets | d2 21:55 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 21:55 PalletPick task created (STG3A-1->1A) by OrderService.RefilePalletPickShortfalls | d3 00:53 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 3 01:30 | **NO-TASK** | NO-TASK lane pallet 2B-2 sku=615873 load=9355493019  <<HISTORY: d2 23:35 Receive task created (->STG2B-2) by ReceivingService.CreateReceiveTaskForPallet | d3 01:07 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 01:17 Putaway task created (STG2B-2->) by ReceiverReceivingWorkflow.CreatePutawayTask | d3 01:17 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 01:17 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 01:17 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask>>
- day 3 03:00 | **FLOATING** | FLOATING Pallet_00_Slot0_Tier0 @3A-1 bottom=2.33  <<HISTORY: d3 02:54 registered at (51, 74) 27x194831 by InventoryService.RegisterPhysicalPallet | d3 02:54 Receive task created (->STG3A-1) by ReceivingService.CreateReceiveTaskForPallet>>
- day 3 03:08 | **FLOATING** | FLOATING Pallet_00_Slot0_Tier0 @3A-1 bottom=2.33  <<HISTORY: d3 02:54 registered at (51, 74) 27x194831 by InventoryService.RegisterPhysicalPallet | d3 02:54 Receive task created (->STG3A-1) by ReceivingService.CreateReceiveTaskForPallet>>
- day 3 03:38 | **OVERLAP** | OVERLAP OutboundPallet_c832098b-4885-4232-b412-2a594a326e7f_2 & OutboundPallet_c832098b-4885-4232-b412-2a594a326e7f_1 @2E-1
- day 3 03:45 | **OVERLAP** | OVERLAP OutboundPallet_c832098b-4885-4232-b412-2a594a326e7f_2 & OutboundPallet_c832098b-4885-4232-b412-2a594a326e7f_1 @2E-1
- day 3 08:38 | **REPEATING** | x25 and counting: [AiNavigation] Employee_Tyler Hayes: stranded on an isolated NavMesh island at (-15.16, 1.13, 42.54) (no waypoint reachable for 8s) — warping to (-12.64, 0.01, 43.01) to rejoin patrol.
- day 3 08:38 | **REPEATING** | x25 and counting: [AiNavigation] Employee_Tyler Hayes: stranded on an isolated NavMesh island at (-15.16, 1.13, 42.54) (no waypoint reachable for 8s) — warping to (-12.64, 0.01, 43.01) to rejoin patrol.
- day 3 19:56 | **WARN** | [AiNavigation] Dockstocker: stuck at (-17.16, 1.11, 43.97) — walking out to (-9.68, 1.16, 32.74) (12.49m to mesh).
- day 3 19:56 | **WARN** | [AiNavigation] Dockstocker: stuck at (-17.16, 1.11, 43.97) — walking out to (-9.68, 1.16, 32.74) (12.49m to mesh).
- day 4 01:46 | **ERROR** | [MHEBoundary] 'ReachTruck' drove OUT of the building at (-21.95, 0.00, 45.68) (cell (33, 84)). Putting it back on its last inside spot (-21.42, 1.11, 43.39). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 4 01:46 | **ERROR** | [MHEBoundary] 'ReachTruck' drove OUT of the building at (-21.95, 0.00, 45.68) (cell (33, 84)). Putting it back on its last inside spot (-21.42, 1.11, 43.39). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 4 08:38 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 4 08:38 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (-26.43, 1.11, 40.13) (cell (30, 80)). Putting it back on its last inside spot (-27.33, 1.11, 39.56). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 4 08:38 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (-26.43, 1.11, 40.13) (cell (30, 80)). Putting it back on its last inside spot (-27.33, 1.11, 39.56). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 4 08:40 | **ERROR** | [MHEBoundary] 'Dockstocker' keeps driving out of the building (6 times in 10s, at (-26.58, 1.11, 40.03)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:85)
  ```
- day 4 08:40 | **ERROR** | [MHEBoundary] 'Dockstocker' keeps driving out of the building (6 times in 10s, at (-26.58, 1.11, 40.03)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:85)
  ```
- day 4 08:53 | **DOOR-DEADLOCK** | OG2629 is Loading but door 1's truck has waited 105 min with no one loading it.
- day 4 09:00 | **DOOR-DEADLOCK** | OG2629 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 4 09:00 | **DOOR-DEADLOCK** | OG2633 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 4 10:30 | **FLOATING** | FLOATING Pallet_08_Slot5_Tier1 @1A-3 bottom=2.75  <<HISTORY: d3 00:57 moved (51, 74)->(-1, -1) by InventoryService.MovePallet | d3 01:01 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d3 01:09 adopted as outbound freight of order 6dba98a2-67a5-4129-a08e-8ae17db26dc2 (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:09 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:09 record destroyed by InventoryService.DestroyPallet | d3 01:09 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 4 10:30 | **FLOATING** | FLOATING Pallet_11_Slot10_Tier0 @1A-5 bottom=1.34  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 4 10:30 | **OVERLAP** | OVERLAP Pallet_11_Slot10_Tier0 & OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_2 @1A-5  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 4 10:30 | **FLOATING** | FLOATING OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_1 @1A-4 bottom=2.15
- day 4 10:38 | **FLOATING** | FLOATING Pallet_08_Slot5_Tier1 @1A-3 bottom=2.75  <<HISTORY: d3 00:57 moved (51, 74)->(-1, -1) by InventoryService.MovePallet | d3 01:01 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d3 01:09 adopted as outbound freight of order 6dba98a2-67a5-4129-a08e-8ae17db26dc2 (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:09 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:09 record destroyed by InventoryService.DestroyPallet | d3 01:09 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 4 10:38 | **FLOATING** | FLOATING Pallet_11_Slot10_Tier0 @1A-5 bottom=1.34  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 4 10:38 | **OVERLAP** | OVERLAP Pallet_11_Slot10_Tier0 & OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_2 @1A-5  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 4 10:38 | **FLOATING** | FLOATING OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_1 @1A-4 bottom=2.15
- day 4 11:54 | **ERROR** | [MHEBoundary] 'ReachTruck' drove OUT of the building at (9.62, 0.00, 44.31) (cell (57, 83)). Putting it back on its last inside spot (8.95, 1.11, 44.10). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 4 11:54 | **ERROR** | [MHEBoundary] 'ReachTruck' drove OUT of the building at (9.62, 0.00, 44.31) (cell (57, 83)). Putting it back on its last inside spot (8.95, 1.11, 44.10). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 4 12:23 | **REPEATING** | x25 and counting: [AiNavigation] Employee_Jack Peterson: stranded on an isolated NavMesh island at (-22.63, 1.16, 42.01) (no waypoint reachable for 8s) — warping to (-19.26, 0.01, 41.69) to rejoin patrol.
- day 4 12:23 | **REPEATING** | x25 and counting: [AiNavigation] Employee_Jack Peterson: stranded on an isolated NavMesh island at (-22.63, 1.16, 42.01) (no waypoint reachable for 8s) — warping to (-19.26, 0.01, 41.69) to rejoin patrol.
- day 4 12:53 | **DOOR-DEADLOCK** | OG2629 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 4 12:53 | **DOOR-DEADLOCK** | OG2633 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 4 13:00 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 4 13:15 | **DOOR-DEADLOCK** | OG2629 is Loading but door 1's truck has waited 105 min with no one loading it.
- day 4 22:42 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2B and it never freed up — giving up and re-queueing.
- day 4 22:42 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2B and it never freed up — giving up and re-queueing.
- day 5 02:15 | **DOOR-DEADLOCK** | OG2629 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 5 02:15 | **DOOR-DEADLOCK** | OG2633 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 5 02:23 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 5 02:38 | **DOOR-DEADLOCK** | OG2629 is Loading but door 1's truck has waited 105 min with no one loading it.
- day 5 03:30 | **TRUCK-STALLED** | TRUCK-STALLED Truck→Outbound_Door3 state=DepartToApproach 105s at (4.6, 0.0, 18.7)
- day 5 05:38 | **FLOATING** | FLOATING OutboundPallet_cceaa103-b536-4d11-8549-185666e0bc5b_2 @1B-2 bottom=2.35
- day 5 05:45 | **FLOATING** | FLOATING OutboundPallet_cceaa103-b536-4d11-8549-185666e0bc5b_2 @1B-2 bottom=2.35
- day 5 07:08 | **DOOR-DEADLOCK** | OG2629 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 5 07:08 | **DOOR-DEADLOCK** | OG2633 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 5 07:30 | **DOOR-DEADLOCK** | OG2629 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 5 07:30 | **DOOR-DEADLOCK** | OG2633 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 5 07:46 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (9.83, 0.00, 44.36) (cell (57, 83)). Putting it back on its last inside spot (9.21, 0.59, 44.11). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 5 07:46 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (9.83, 0.00, 44.36) (cell (57, 83)). Putting it back on its last inside spot (9.21, 0.59, 44.11). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 5 09:38 | **FLOATING** | FLOATING OutboundPallet_6f408970-902d-4452-b598-33db5d4b7d68_1 @2C-2 bottom=2.19
- day 5 09:45 | **FLOATING** | FLOATING Pallet_08_Slot5_Tier1 @1A-3 bottom=2.75  <<HISTORY: d3 00:57 moved (51, 74)->(-1, -1) by InventoryService.MovePallet | d3 01:01 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d3 01:09 adopted as outbound freight of order 6dba98a2-67a5-4129-a08e-8ae17db26dc2 (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:09 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:09 record destroyed by InventoryService.DestroyPallet | d3 01:09 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 5 09:45 | **FLOATING** | FLOATING Pallet_11_Slot10_Tier0 @1A-5 bottom=1.34  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 09:45 | **OVERLAP** | OVERLAP Pallet_11_Slot10_Tier0 & OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_2 @1A-5  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 09:45 | **FLOATING** | FLOATING OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_1 @1A-4 bottom=2.15
- day 5 09:45 | **FLOATING** | FLOATING OutboundPallet_cceaa103-b536-4d11-8549-185666e0bc5b_2 @1B-2 bottom=2.35
- day 5 09:53 | **FLOATING** | FLOATING Pallet_08_Slot5_Tier1 @1A-3 bottom=2.75  <<HISTORY: d3 00:57 moved (51, 74)->(-1, -1) by InventoryService.MovePallet | d3 01:01 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d3 01:09 adopted as outbound freight of order 6dba98a2-67a5-4129-a08e-8ae17db26dc2 (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:09 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:09 record destroyed by InventoryService.DestroyPallet | d3 01:09 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 5 09:53 | **FLOATING** | FLOATING Pallet_11_Slot10_Tier0 @1A-5 bottom=1.34  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 09:53 | **OVERLAP** | OVERLAP Pallet_11_Slot10_Tier0 & OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_2 @1A-5  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 09:53 | **FLOATING** | FLOATING OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_1 @1A-4 bottom=2.15
- day 5 09:53 | **FLOATING** | FLOATING OutboundPallet_cceaa103-b536-4d11-8549-185666e0bc5b_2 @1B-2 bottom=2.35
- day 5 10:54 | **ERROR** | [MHEBoundary] 'ReachTruck' drove OUT of the building at (9.83, 0.00, 43.01) (cell (57, 82)). Putting it back on its last inside spot (8.67, 1.11, 43.12). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 5 10:54 | **ERROR** | [MHEBoundary] 'ReachTruck' drove OUT of the building at (9.83, 0.00, 43.01) (cell (57, 82)). Putting it back on its last inside spot (8.67, 1.11, 43.12). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 5 15:51 | **REPEATING** | x25 and counting: Parameter 'IsWaving' does not exist.
- day 5 15:51 | **REPEATING** | x25 and counting: Parameter 'IsWaving' does not exist.
- day 5 18:08 | **FLOATING** | FLOATING OutboundPallet_6f408970-902d-4452-b598-33db5d4b7d68_1 @2C-2 bottom=2.19
- day 5 18:15 | **FLOATING** | FLOATING OutboundPallet_6f408970-902d-4452-b598-33db5d4b7d68_1 @2C-2 bottom=2.19
- day 5 19:23 | **FLOATING** | FLOATING Pallet_08_Slot5_Tier1 @1A-3 bottom=2.75  <<HISTORY: d3 00:57 moved (51, 74)->(-1, -1) by InventoryService.MovePallet | d3 01:01 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d3 01:09 adopted as outbound freight of order 6dba98a2-67a5-4129-a08e-8ae17db26dc2 (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:09 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:09 record destroyed by InventoryService.DestroyPallet | d3 01:09 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 5 19:23 | **FLOATING** | FLOATING Pallet_11_Slot10_Tier0 @1A-5 bottom=1.34  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 19:23 | **OVERLAP** | OVERLAP Pallet_11_Slot10_Tier0 & OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_2 @1A-5  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 19:23 | **FLOATING** | FLOATING OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_1 @1A-4 bottom=2.15
- day 5 19:23 | **FLOATING** | FLOATING OutboundPallet_cceaa103-b536-4d11-8549-185666e0bc5b_2 @1B-2 bottom=2.35
- day 5 19:30 | **FLOATING** | FLOATING Pallet_08_Slot5_Tier1 @1A-3 bottom=2.75  <<HISTORY: d3 00:57 moved (51, 74)->(-1, -1) by InventoryService.MovePallet | d3 01:01 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d3 01:09 adopted as outbound freight of order 6dba98a2-67a5-4129-a08e-8ae17db26dc2 (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:09 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:09 record destroyed by InventoryService.DestroyPallet | d3 01:09 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 5 19:30 | **FLOATING** | FLOATING Pallet_11_Slot10_Tier0 @1A-5 bottom=1.34  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 19:30 | **OVERLAP** | OVERLAP Pallet_11_Slot10_Tier0 & OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_2 @1A-5  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 19:30 | **FLOATING** | FLOATING OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_1 @1A-4 bottom=2.15
- day 5 19:30 | **FLOATING** | FLOATING OutboundPallet_cceaa103-b536-4d11-8549-185666e0bc5b_2 @1B-2 bottom=2.35
- day 5 19:38 | **FLOATING** | FLOATING OutboundPallet_6f408970-902d-4452-b598-33db5d4b7d68_1 @2C-2 bottom=2.19
- day 5 19:45 | **FLOATING** | FLOATING OutboundPallet_6f408970-902d-4452-b598-33db5d4b7d68_1 @2C-2 bottom=2.19
- day 5 20:15 | **DOOR-DEADLOCK** | OG2629 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 5 20:15 | **DOOR-DEADLOCK** | OG2633 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 5 20:23 | **DOOR-DEADLOCK** | OG2629 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 5 20:23 | **DOOR-DEADLOCK** | OG2633 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 5 21:03 | **WARN** | [AiNavigation] Dockstocker: stuck at (-18.04, 1.11, 42.16) — walking out to (-24.53, 1.15, 30.41) (12.42m to mesh).
- day 5 21:03 | **WARN** | [AiNavigation] Dockstocker: stuck at (-18.04, 1.11, 42.16) — walking out to (-24.53, 1.15, 30.41) (12.42m to mesh).
- day 5 22:15 | **NO-TASK** | NO-TASK lane pallet 3B-2 sku=215678 load=1750826429  <<HISTORY: d5 18:53 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 19:04 Putaway task created (STG3B-2->) by ReceiverReceivingWorkflow.CreatePutawayTask | d5 19:04 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 19:04 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 19:04 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 22:14 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 5 22:53 | **FLOATING** | FLOATING OutboundPallet_6f408970-902d-4452-b598-33db5d4b7d68_1 @2C-2 bottom=2.19
- day 5 23:00 | **FLOATING** | FLOATING OutboundPallet_6f408970-902d-4452-b598-33db5d4b7d68_1 @2C-2 bottom=2.19
- day 5 23:15 | **FLOATING** | FLOATING Pallet_08_Slot5_Tier1 @1A-3 bottom=2.75  <<HISTORY: d3 00:57 moved (51, 74)->(-1, -1) by InventoryService.MovePallet | d3 01:01 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d3 01:09 adopted as outbound freight of order 6dba98a2-67a5-4129-a08e-8ae17db26dc2 (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:09 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:09 record destroyed by InventoryService.DestroyPallet | d3 01:09 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 5 23:15 | **FLOATING** | FLOATING Pallet_11_Slot10_Tier0 @1A-5 bottom=1.34  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 23:15 | **OVERLAP** | OVERLAP Pallet_11_Slot10_Tier0 & OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_2 @1A-5  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 23:15 | **FLOATING** | FLOATING OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_1 @1A-4 bottom=2.15
- day 5 23:15 | **FLOATING** | FLOATING OutboundPallet_cceaa103-b536-4d11-8549-185666e0bc5b_2 @1B-2 bottom=2.35
- day 5 23:23 | **FLOATING** | FLOATING Pallet_08_Slot5_Tier1 @1A-3 bottom=2.75  <<HISTORY: d3 00:57 moved (51, 74)->(-1, -1) by InventoryService.MovePallet | d3 01:01 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d3 01:09 adopted as outbound freight of order 6dba98a2-67a5-4129-a08e-8ae17db26dc2 (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:09 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:09 record destroyed by InventoryService.DestroyPallet | d3 01:09 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 5 23:23 | **FLOATING** | FLOATING Pallet_11_Slot10_Tier0 @1A-5 bottom=1.34  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 23:23 | **OVERLAP** | OVERLAP Pallet_11_Slot10_Tier0 & OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_2 @1A-5  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 5 23:23 | **FLOATING** | FLOATING OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_1 @1A-4 bottom=2.15
- day 5 23:23 | **FLOATING** | FLOATING OutboundPallet_cceaa103-b536-4d11-8549-185666e0bc5b_2 @1B-2 bottom=2.35
- day 5 23:31 | **ERROR** | [MHEBoundary] 'PalletJack' drove OUT of the building at (-43.93, 1.06, 36.14) (cell (16, 77)). Putting it back on its last inside spot (-43.76, 1.11, 35.02). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 5 23:31 | **ERROR** | [MHEBoundary] 'PalletJack' drove OUT of the building at (-43.93, 1.06, 36.14) (cell (16, 77)). Putting it back on its last inside spot (-43.76, 1.11, 35.02). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 5 23:31 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 3B and it never freed up — giving up and re-queueing.
- day 5 23:31 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 3B and it never freed up — giving up and re-queueing.
- day 6 01:28 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (-25.69, 0.00, 43.01) (cell (30, 82)). Putting it back on its last inside spot (-25.03, 1.11, 42.99). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 6 01:28 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (-25.69, 0.00, 43.01) (cell (30, 82)). Putting it back on its last inside spot (-25.03, 1.11, 42.99). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 6 02:00 | **DOOR-DEADLOCK** | OG7638 is Loading but door 2's truck has waited 435 min with no one loading it.
- day 6 02:38 | **FLOATING** | FLOATING Pallet_08_Slot5_Tier1 @1A-3 bottom=2.75  <<HISTORY: d3 00:57 moved (51, 74)->(-1, -1) by InventoryService.MovePallet | d3 01:01 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d3 01:09 adopted as outbound freight of order 6dba98a2-67a5-4129-a08e-8ae17db26dc2 (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:09 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:09 record destroyed by InventoryService.DestroyPallet | d3 01:09 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 6 02:38 | **FLOATING** | FLOATING Pallet_11_Slot10_Tier0 @1A-5 bottom=1.34  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 6 02:38 | **OVERLAP** | OVERLAP Pallet_11_Slot10_Tier0 & OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_2 @1A-5  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 6 02:38 | **FLOATING** | FLOATING OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_1 @1A-4 bottom=2.15
- day 6 02:38 | **FLOATING** | FLOATING OutboundPallet_cceaa103-b536-4d11-8549-185666e0bc5b_2 @1B-2 bottom=2.35
- day 6 02:45 | **FLOATING** | FLOATING Pallet_08_Slot5_Tier1 @1A-3 bottom=2.75  <<HISTORY: d3 00:57 moved (51, 74)->(-1, -1) by InventoryService.MovePallet | d3 01:01 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d3 01:09 adopted as outbound freight of order 6dba98a2-67a5-4129-a08e-8ae17db26dc2 (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:09 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:09 record destroyed by InventoryService.DestroyPallet | d3 01:09 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- day 6 02:45 | **FLOATING** | FLOATING Pallet_11_Slot10_Tier0 @1A-5 bottom=1.34  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 6 02:45 | **OVERLAP** | OVERLAP Pallet_11_Slot10_Tier0 & OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_2 @1A-5  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- day 6 02:45 | **FLOATING** | FLOATING OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_1 @1A-4 bottom=2.15
- day 6 02:45 | **FLOATING** | FLOATING OutboundPallet_cceaa103-b536-4d11-8549-185666e0bc5b_2 @1B-2 bottom=2.35
- day 6 03:22 | **ERROR** | [MHEBoundary] 'Dockstocker' keeps driving out of the building (6 times in 10s, at (-25.90, 1.11, 41.02)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:85)
  ```
- day 6 03:22 | **ERROR** | [MHEBoundary] 'Dockstocker' keeps driving out of the building (6 times in 10s, at (-25.90, 1.11, 41.02)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:85)
  ```
- day 6 05:00 | **TRUCK-STALLED** | TRUCK-STALLED Truck→Outbound_Door3 state=DepartToApproach 210s at (4.6, 0.0, 18.7)
- day 6 11:30 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 6 11:38 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 6 11:45 | **DOOR-DEADLOCK** | OG7638 is Loading but door 2's truck has waited 90 min with no one loading it.
- day 6 11:53 | **DOOR-DEADLOCK** | OG7638 is Loading but door 2's truck has waited 90 min with no one loading it.
- day 6 17:19 | **REPEATING** | x25 and counting: [JackStall] 'bay 02-01' at (-39.02, 1.14, 35.49) (target (-36.41, 1.11, 35.84)) remaining=2.63 pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(0.00, 0.00, 0.00) stopped=True onMesh=True jackBlocked=True elapsed=2.6s reissues=0
- day 6 17:19 | **REPEATING** | x25 and counting: [JackStall] 'bay 02-01' at (-39.02, 1.14, 35.49) (target (-36.41, 1.11, 35.84)) remaining=2.63 pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(0.00, 0.00, 0.00) stopped=True onMesh=True jackBlocked=True elapsed=2.6s reissues=0
- day 6 19:15 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (-1.91, 0.00, 45.67) (cell (48, 84)). Putting it back on its last inside spot (-1.88, 1.16, 44.33). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 6 19:15 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (-1.91, 0.00, 45.67) (cell (48, 84)). Putting it back on its last inside spot (-1.88, 1.16, 44.33). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 6 19:45 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 6 19:45 | **DOOR-DEADLOCK** | OG7638 is Loading but door 2's truck has waited 90 min with no one loading it.
- day 6 19:53 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 6 19:53 | **DOOR-DEADLOCK** | OG7638 is Loading but door 2's truck has waited 90 min with no one loading it.
- day 7 03:23 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 7 03:30 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 7 03:37 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (-25.65, 0.00, 44.34) (cell (30, 83)). Putting it back on its last inside spot (-25.03, 1.11, 44.34). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 7 03:37 | **ERROR** | [MHEBoundary] 'Dockstocker' drove OUT of the building at (-25.65, 0.00, 44.34) (cell (30, 83)). Putting it back on its last inside spot (-25.03, 1.11, 44.34). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  ```
- day 7 05:15 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 7 05:23 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 7 07:15 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→Outbound_Door2 & Truck→PO_IG7006 at (20.2, 0.0, -0.7)
- day 7 12:08 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 7 12:15 | **DOOR-DEADLOCK** | OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 7 17:12 | **EXCEPTION** | MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_position () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  GameCore.Actors.ReachTruckOperator+<DeliverPalletToRack>d__94.MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:1275)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 7 17:12 | **EXCEPTION** | MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_position () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  GameCore.Actors.ReachTruckOperator+<DeliverPalletToRack>d__94.MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:1275)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
