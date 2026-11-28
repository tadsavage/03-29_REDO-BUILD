# Bug log - block 26

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 11:30 | **WARN** | [AiNavigation] Employee_Madison Ford: stuck at (6.34, 1.15, 23.96) — walking out to (6.34, 0.06, 10.96) (12.05m to mesh).
- day 2 20:20 | **ERROR** | [ReceiverReceivingWorkflow] No master record for pallet b711160f-c506-4321-a171-7df43ef84daf
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.ReceiverReceivingWorkflow:BeginReceivingAt (GameCore.Labor.WorkTask,UnityEngine.Transform,single) (at Assets/_Project/Scripts/1. FSM/Core/Labor/ReceiverReceivingWorkflow.cs:247)
  GameCore.Actors.ReceivingTaskDriver:OnArrivedAtStandPosition (GameCore.Labor.WorkTask,UnityEngine.Transform,int) (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:213)
  GameCore.Actors.ReceivingTaskDriver/<>c__DisplayClass22_0:<OnArrivedAtStandPosition>b__0 () (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:226)
  AiNavigation:Update () (at Assets/_Project/Scripts/Actors/AiNavigation.cs:966)
  ```
- day 2 20:46 | **ERROR** | [ReceiverReceivingWorkflow] No master record for pallet 170c4650-bfc7-42d8-8ba4-50d017405f4c
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.ReceiverReceivingWorkflow:BeginReceivingAt (GameCore.Labor.WorkTask,UnityEngine.Transform,single) (at Assets/_Project/Scripts/1. FSM/Core/Labor/ReceiverReceivingWorkflow.cs:247)
  GameCore.Actors.ReceivingTaskDriver:OnArrivedAtStandPosition (GameCore.Labor.WorkTask,UnityEngine.Transform,int) (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:213)
  GameCore.Actors.ReceivingTaskDriver/<>c__DisplayClass22_0:<OnArrivedAtStandPosition>b__0 () (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:226)
  AiNavigation:Update () (at Assets/_Project/Scripts/Actors/AiNavigation.cs:966)
  ```
- day 3 21:27 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→PO_IG3277 & Truck→PO_IG3275 at (4.2, 0.0, -3.1)
- day 5 01:10 | **WARN** | [AiNavigation] ReachTruck: stuck at (-6.31, 1.11, 24.26) — walking out to (-6.31, 0.08, 11.26) (12.04m to mesh).
- day 5 01:17 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (-6.31, 0.45, 12.43) (cell (45, 59)). Putting it back on its last inside spot (-6.31, 0.65, 13.05). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 5 18:42 | **NO-TASK** | NO-TASK lane pallet 2A-1 sku=750426 load=6379372175  <<HISTORY: d5 15:46 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 16:12 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 16:12 PalletPick task created (STG2A-1->) by OrderService.FileReleasedOrderTasks | d5 16:12 PalletPick task Available->Open by OrderService.FileReleasedOrderTasks | d5 16:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d5 18:40 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 5 18:57 | **NO-TASK** | NO-TASK lane pallet 2B-4 sku=148972 load=1834940554  <<HISTORY: d5 16:40 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 16:50 Putaway task created (STG2B-4->) by ReceiverReceivingWorkflow.CreatePutawayTask | d5 16:50 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 16:50 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 16:50 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 18:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 5 19:34 | **WARN** | [AiNavigation] PalletJack: stuck at (19.44, 1.11, 37.44) — walking out to (25.44, 0.08, 37.44) (5.11m to mesh).
- day 5 19:38 | **ERROR** | [MHEBoundary] 'PalletJack' fell below the floor at (24.85, 0.37, 37.44) (cell (68, 78)). Putting it back on its last inside spot (24.17, 0.71, 37.44). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 5 19:40 | **ERROR** | [MHEBoundary] 'PalletJack' keeps driving out of the building (6 times in 10s, at (25.39, 0.00, 37.44)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  ```
- day 5 19:42 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2B and it never freed up — giving up and re-queueing.
- day 5 19:55 | **WARN** | [AiNavigation] ReachTruck: stuck at (16.75, 1.11, 35.54) — walking out to (3.75, 1.17, 35.54) (12.00m to mesh).
- day 5 20:19 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (5.79, 0.00, 41.87) (cell (54, 81)). Putting it back on its last inside spot (6.56, 0.60, 41.58). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 5 20:21 | **ERROR** | [MHEBoundary] 'ReachTruck' keeps driving out of the building (6 times in 10s, at (6.56, 0.00, 41.67)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  ```
- day 5 20:42 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-03' (13.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__92:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1140)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 5 22:29 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jean Stewart' as its operator though nobody is on it or running it - clearing the stale link.
- day 5 22:42 | **OVERLAP** | OVERLAP Pallet_14_Slot9_Tier1 & Pallet_00_Slot0_Tier0 @(36, 71)  <<HISTORY: d5 19:44 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 21:44 Putaway task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d5 22:07 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 22:15 moved (53, 65)->(-1, -1) by InventoryService.MovePallet | d5 22:29 moved (-1, -1)->(36, 71) by InventoryService.MovePallet | d5 22:29 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d5 21:34 picked 1 (left 4) by InventoryService.PickFromPallet | d5 21:35 picked 1 (left 3) by InventoryService.PickFromPallet | d5 21:36 picked 1 (left 2) by InventoryService.PickFromPallet | d5 21:36 picked 1 (left 1) by InventoryService.PickFromPallet | d5 21:37 picked 1 (left 0) by InventoryService.PickFromPallet | d5 21:37 record destroyed by InventoryService.DestroyPallet>>
- day 6 07:31 | **WARN** | [AiNavigation] Employee_Madison Ford: stuck at (17.27, 1.16, 25.41) — walking out to (23.46, 0.04, 13.37) (12.54m to mesh).
- day 7 15:21 | **WARN** | [OrderService] Order OG5785 closed with a STAGED pallet (15 cs) that never made the trailer - not billed, returning it to stock.
- day 7 15:27 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1A-5 (order ceb8dcae-473f-4058-9618-0f88072cb0a6 gone)
