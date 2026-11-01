# Bug log - block 24

Every incident as it happened. Format: day hour:minute | category | what.

- day 2 12:12 | **DOOR-DEADLOCK** | OG2749 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 2 15:12 | **OVERLAP** | OVERLAP OutboundPallet_5d6bdcd2-7439-4ca6-9a55-d7fea9bb1635_2 & OutboundPallet_5d6bdcd2-7439-4ca6-9a55-d7fea9bb1635_1 @1E-1
- day 2 19:57 | **NO-TASK** | NO-TASK lane pallet 3A-3 sku=172495 load=6795962050  <<HISTORY: d2 19:39 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 19:53 Putaway task created (STG3A-3->) by ReceiverReceivingWorkflow.CreatePutawayTask | d2 19:53 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 19:53 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 19:53 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d2 19:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 2 20:44 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 3A and it never freed up — giving up and re-queueing.
- day 2 20:54 | **WARN** | [AiNavigation] ReachTruck: stuck at (18.22, 1.11, 34.88) — walking out to (22.72, 0.08, 42.67) (8.07m to mesh).
- day 2 20:59 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (22.48, 0.00, 42.27) (cell (67, 81)). Putting it back on its last inside spot (22.14, 0.70, 41.68). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 2 21:02 | **ERROR** | [MHEBoundary] 'ReachTruck' keeps driving out of the building (6 times in 10s, at (22.28, 0.00, 41.92)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  ```
- day 3 16:34 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 1E and it never freed up — giving up and re-queueing.
- day 3 18:23 | **WARN** | [AiNavigation] Dockstocker: stuck at (-2.33, 1.11, 38.33) — walking out to (-8.81, 1.16, 26.43) (12.55m to mesh).
- day 3 19:31 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 1E and it never freed up — giving up and re-queueing.
- day 3 20:16 | **WARN** | [AiNavigation] PalletJack: stuck at (18.75, 1.11, 32.93) — walking out to (5.75, 1.19, 32.93) (12.00m to mesh).
- day 5 03:42 | **NO-TASK** | NO-TASK lane pallet 2A-3 sku=164923 load=2695186831  <<HISTORY: d5 02:44 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 03:24 Putaway task created (STG2A-3->) by ReceiverReceivingWorkflow.CreatePutawayTask | d5 03:24 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 03:24 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 03:24 PalletPick task Open->Available by OrderService.TryAssignInboundPalletToPendingPick | d5 03:24 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask>>
- day 5 11:27 | **DOOR-DEADLOCK** | OG5757 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 5 12:11 | **WARN** | [AiNavigation] ReachTruck: stuck at (15.52, 1.11, 33.60) — walking out to (15.52, 0.08, 42.67) (8.13m to mesh).
- day 5 12:16 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (15.52, 0.00, 42.45) (cell (61, 82)). Putting it back on its last inside spot (15.52, 0.64, 41.76). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 5 17:57 | **DOOR-DEADLOCK** | OG5757 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 7 16:57 | **OVERLAP** | OVERLAP Pallet_03_Slot2_Tier1 & OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 @1A-1  <<HISTORY: d6 22:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d6 22:12 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d6 22:27 moved (36, 71)->(-1, -1) by InventoryService.MovePallet | d6 22:40 adopted as outbound freight of order b02a3e06-0258-43a5-ba72-7584aa5c5099 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d6 22:40 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d6 22:40 record destroyed by InventoryService.DestroyPallet>>
- day 7 16:57 | **FLOATING** | FLOATING OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 @1A-1 bottom=1.96
- day 7 16:57 | **FLOATING** | FLOATING OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 @1A-4 bottom=1.35
- day 7 16:57 | **OVERLAP** | OVERLAP OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 & OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 @1A-4
- day 7 16:57 | **OVERLAP** | OVERLAP OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 & OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 @1A-2
- day 7 16:57 | **FLOATING** | FLOATING OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 @1A-3 bottom=1.62
- day 7 16:57 | **OVERLAP** | OVERLAP OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 & OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_2 @1A-3
