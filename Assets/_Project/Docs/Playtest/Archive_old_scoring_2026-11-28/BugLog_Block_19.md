# Bug log - block 19

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 09:33 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (9.69, 0.00, 43.03) (cell (57, 82)). Putting it back on its last inside spot (9.02, 1.11, 43.36). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 1 17:23 | **NO-TASK** | NO-TASK lane pallet 1A-5 sku=649320 load=0132345426  <<HISTORY: d1 15:22 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 17:08 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d1 17:08 PalletPick task created (STG1A-5->) by OrderService.FileReleasedOrderTasks | d1 17:08 PalletPick task Available->Open by OrderService.FileReleasedOrderTasks | d1 17:08 PalletPick task Open->Available by OrderService.AllocateHeldWork | d1 17:21 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 1 22:32 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (-24.57, 0.00, 45.67) (cell (31, 84)). Putting it back on its last inside spot (-24.33, 0.66, 44.95). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 2 09:38 | **NO-TASK** | NO-TASK lane pallet 2C-4 sku=248905 load=7215150387  <<HISTORY: d2 09:09 Receive task created (->STG2C-4) by ReceivingService.CreateReceiveTaskForPallet | d2 09:12 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 09:23 Putaway task created (STG2C-4->) by ReceiverReceivingWorkflow.CreatePutawayTask | d2 09:23 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 09:23 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 09:23 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask>>
- day 3 03:23 | **NO-TASK** | NO-TASK lane pallet 2B-2 sku=248905 load=4191242030  <<HISTORY: d2 08:22 Putaway task created (STG2B-2->) by ReceiverReceivingWorkflow.CreatePutawayTask | d2 08:22 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 08:22 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 08:22 PalletPick task Open->Available by OrderService.TryAssignInboundPalletToPendingPick | d2 08:22 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 03:21 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 3 07:53 | **NO-TASK** | NO-TASK lane pallet 3C-2 sku=649320 load=8125841483  <<HISTORY: d3 06:45 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 07:16 Putaway task created (STG3C-2->) by ReceiverReceivingWorkflow.CreatePutawayTask | d3 07:16 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 07:16 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 07:16 PalletPick task Open->Available by OrderService.TryAssignInboundPalletToPendingPick | d3 07:16 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask>>
- day 3 09:06 | **REPEATING** | x25 and counting: [EquipmentBattery] Dockstocker: not sliding 6.4 m by hand - that is a navigation job. Trying the path again.
- day 3 11:27 | **REPEATING** | x25 and counting: [AiNavigation] Employee_Carolyn Moore: stranded on an isolated NavMesh island at (0.38, 1.16, 39.94) (no waypoint reachable for 8s) — warping to (-12.64, 0.01, 43.01) to rejoin patrol.
- day 3 14:23 | **DOOR-DEADLOCK** | OG1651 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 3 15:23 | **DOOR-DEADLOCK** | OG4652 is Loading but door 2's truck has waited 90 min with no one loading it.
- day 5 10:44 | **EXCEPTION** | MissingReferenceException: The object of type 'UnityEngine.AI.NavMeshAgent' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.AI.NavMeshAgent.get_isOnNavMesh () (at <8b8737c539e44426a2faf31425268539>:0)
  GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__76.MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1141)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 1 08:00 | **EXCEPTION** | MissingReferenceException: The object of type 'UnityEngine.AI.NavMeshAgent' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.AI.NavMeshAgent.get_isOnNavMesh () (at <8b8737c539e44426a2faf31425268539>:0)
  GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__76.MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1141)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 1 08:00 | **EXCEPTION** | MissingReferenceException: The object of type 'UnityEngine.AI.NavMeshAgent' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.AI.NavMeshAgent.get_isOnNavMesh () (at <8b8737c539e44426a2faf31425268539>:0)
  GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__76.MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1141)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 1 10:58 | **EXCEPTION** | MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_rotation () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_forward () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  GameCore.Labor.TrailerOffloadController+<OffloadOnePallet>d__41.MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:586)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 1 10:58 | **EXCEPTION** | MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_rotation () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_forward () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  GameCore.Labor.TrailerOffloadController+<OffloadOnePallet>d__41.MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:586)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 1 10:58 | **EXCEPTION** | MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_rotation () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_forward () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  GameCore.Labor.TrailerOffloadController+<OffloadOnePallet>d__41.MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:586)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 1 08:00 | **REPEATING** | x25 and counting: Failed to create agent because it is not close enough to the NavMesh
- day 1 08:00 | **REPEATING** | x25 and counting: Failed to create agent because it is not close enough to the NavMesh
- day 1 23:26 | **REPEATING** | x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (7.59, 1.11, 38.97) (no waypoint reachable for 8s) — warping to (11.21, 0.01, 39.04) to rejoin patrol.
- day 1 23:26 | **REPEATING** | x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (7.59, 1.11, 38.97) (no waypoint reachable for 8s) — warping to (11.21, 0.01, 39.04) to rejoin patrol.
- day 1 08:48 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (1.62, 1.11, 36.00) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- day 1 08:48 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (1.62, 1.11, 36.00) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- day 2 05:11 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (11.21, 1.26, 35.06) (no waypoint reachable for 8s) — warping to (11.21, 0.01, 39.04) to rejoin patrol.
- day 2 05:45 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (3.26, 0.00, 31.09) (cell (52, 73)). Putting it back on its last inside spot (-26.11, 1.11, 21.27). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 2 05:45 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (3.26, 0.00, 31.09) (cell (52, 73)). Putting it back on its last inside spot (-26.11, 1.11, 21.27). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 2 05:45 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (3.26, 0.00, 31.09) (cell (52, 73)). Putting it back on its last inside spot (-26.11, 1.11, 21.27). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 2 06:27 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (3.26, 0.00, 31.09) (cell (52, 73)). Putting it back on its last inside spot (3.59, 1.11, 32.51). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 2 06:27 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (3.26, 0.00, 31.09) (cell (52, 73)). Putting it back on its last inside spot (3.59, 1.11, 32.51). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 2 06:27 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (3.26, 0.00, 31.09) (cell (52, 73)). Putting it back on its last inside spot (3.59, 1.11, 32.51). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 2 07:20 | **ERROR** | Assertion failed
  ```
  UnityEngine.UIElements.UIElementsRuntimeUtilityNative:RepaintPanels (bool)
  ```
- day 2 07:20 | **ERROR** | Assertion failed
  ```
  UnityEngine.UIElements.UIElementsRuntimeUtilityNative:RepaintPanels (bool)
  ```
- day 2 07:20 | **ERROR** | Assertion failed
  ```
  UnityEngine.UIElements.UIElementsRuntimeUtilityNative:RepaintPanels (bool)
  ```
- day 2 08:04 | **REPEATING** | x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (4.83, 1.11, 38.61) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- day 2 20:15 | **WARN** | [AiNavigation] Employee_Steven Peterson: stuck at (6.44, 1.16, 22.65) — walking out to (-0.06, 0.04, 11.39) (12.05m to mesh).
- day 2 20:15 | **WARN** | [AiNavigation] Employee_Steven Peterson: stuck at (6.44, 1.16, 22.65) — walking out to (-0.06, 0.04, 11.39) (12.05m to mesh).
- day 2 20:15 | **WARN** | [AiNavigation] Employee_Steven Peterson: stuck at (6.44, 1.16, 22.65) — walking out to (-0.06, 0.04, 11.39) (12.05m to mesh).
- day 3 07:58 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (3.04, 1.11, 38.37). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 3 07:58 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (3.04, 1.11, 38.37). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 3 07:58 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (3.04, 1.11, 38.37). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 3 19:43 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (12.15, 1.11, 34.21). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 3 19:43 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (12.15, 1.11, 34.21). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 3 19:43 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (12.15, 1.11, 34.21). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 3 21:26 | **DOOR-DEADLOCK** | OG3678 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 4 05:11 | **OVERLAP** | OVERLAP Pallet_08_Slot4_Tier0 & Pallet_09_Slot4_Tier1 @2B-4  <<HISTORY: d4 04:55 registered at (54, 68) 36x189432 by InventoryService.RegisterPhysicalPallet | d4 04:55 Receive task created (->STG2B-4) by ReceivingService.CreateReceiveTaskForPallet | d4 04:55 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 05:05 Putaway task created (STG2B-4->) by ReceiverReceivingWorkflow.CreatePutawayTask | d4 05:05 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d4 04:40 registered at (54, 68) 36x189432 by InventoryService.RegisterPhysicalPallet | d4 04:40 Receive task created (->STG2B-4) by ReceivingService.CreateReceiveTaskForPallet | d4 04:40 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 04:51 Putaway task created (STG2B-4->) by ReceiverReceivingWorkflow.CreatePutawayTask | d4 04:51 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 4 05:26 | **OVERLAP** | OVERLAP Pallet_11_Slot5_Tier1 & Pallet_05_Slot2_Tier1 @2A-1  <<HISTORY: d4 05:15 registered at (53, 65) 36x189432 by InventoryService.RegisterPhysicalPallet | d4 05:15 Receive task created (->STG2A-1) by ReceivingService.CreateReceiveTaskForPallet | d4 05:15 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>  <<HISTORY: d4 03:36 registered at (53, 65) 36x189432 by InventoryService.RegisterPhysicalPallet | d4 03:36 Receive task created (->STG2A-1) by ReceivingService.CreateReceiveTaskForPallet | d4 03:36 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 03:47 Putaway task created (STG2A-1->) by ReceiverReceivingWorkflow.CreatePutawayTask | d4 03:47 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 4 05:41 | **FLOATING** | FLOATING Pallet_08_Slot4_Tier0 @2B-4 bottom=1.30  <<HISTORY: d4 04:55 registered at (54, 68) 36x189432 by InventoryService.RegisterPhysicalPallet | d4 04:55 Receive task created (->STG2B-4) by ReceivingService.CreateReceiveTaskForPallet | d4 04:55 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 05:05 Putaway task created (STG2B-4->) by ReceiverReceivingWorkflow.CreatePutawayTask | d4 05:05 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 4 06:56 | **DOOR-DEADLOCK** | OG3678 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 4 13:01 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (12.21, 1.11, 32.95). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 4 13:01 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (12.21, 1.11, 32.95). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 4 13:01 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (12.21, 1.11, 32.95). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 5 06:11 | **DOOR-DEADLOCK** | OG3678 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 5 12:29 | **DOOR-DEADLOCK** | OG3678 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 5 12:29 | **DOOR-DEADLOCK** | OG3678 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 5 12:36 | **WARN** | [AiNavigation] Dockstocker: stuck at (-2.33, 1.11, 38.33) — walking out to (-8.81, 1.16, 26.43) (12.55m to mesh).
- day 5 12:36 | **WARN** | [AiNavigation] Dockstocker: stuck at (-2.33, 1.11, 38.33) — walking out to (-8.81, 1.16, 26.43) (12.55m to mesh).
- day 5 12:36 | **WARN** | [AiNavigation] Dockstocker: stuck at (-2.33, 1.11, 38.33) — walking out to (-8.81, 1.16, 26.43) (12.55m to mesh).
- day 5 19:04 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (9.41, 1.11, 37.07). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 5 19:04 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (9.41, 1.11, 37.07). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 5 19:04 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (9.41, 1.11, 37.07). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 5 20:32 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (3.12, 1.11, 37.67). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 5 20:32 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (3.12, 1.11, 37.67). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 5 20:32 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (3.12, 1.11, 37.67). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 6 05:58 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (6.31, 1.11, 38.85). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 6 05:58 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (6.31, 1.11, 38.85). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 6 05:58 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (6.31, 1.11, 38.85). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 6 06:11 | **DOOR-DEADLOCK** | OG3678 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 6 06:14 | **DOOR-DEADLOCK** | OG3678 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 6 06:14 | **DOOR-DEADLOCK** | OG3678 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 6 08:12 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_John Williams' as its operator though nobody is on it or running it - clearing the stale link.
- day 6 08:12 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_John Williams' as its operator though nobody is on it or running it - clearing the stale link.
- day 6 08:12 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_John Williams' as its operator though nobody is on it or running it - clearing the stale link.
- day 6 20:18 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (3.65, 1.11, 33.48). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 6 20:18 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (3.65, 1.11, 33.48). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 6 20:18 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (3.65, 1.11, 33.48). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 7 17:13 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (9.31, 1.11, 38.86). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 7 17:13 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (9.31, 1.11, 38.86). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 7 17:13 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (11.21, 0.00, 39.04) (cell (58, 79)). Putting it back on its last inside spot (9.31, 1.11, 38.86). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 7 23:15 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (2.81, 1.11, 37.37). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 7 23:15 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (2.81, 1.11, 37.37). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 7 23:15 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (1.94, 0.00, 39.04) (cell (51, 79)). Putting it back on its last inside spot (2.81, 1.11, 37.37). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
