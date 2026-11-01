# Bug log - block 23

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 13:09 | **WARN** | [AiNavigation] Employee_Kathleen Lee: stuck at (16.94, 1.16, 23.96) — walking out to (23.45, 0.04, 13.14) (11.63m to mesh).
- day 3 15:57 | **DOOR-DEADLOCK** | OG2730 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 3 21:32 | **ERROR** | [ReceiverReceivingWorkflow] No master record for pallet d942cfa5-795b-4db6-b187-72225e9c0901
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.ReceiverReceivingWorkflow:BeginReceivingAt (GameCore.Labor.WorkTask,UnityEngine.Transform,single) (at Assets/_Project/Scripts/1. FSM/Core/Labor/ReceiverReceivingWorkflow.cs:247)
  GameCore.Actors.ReceivingTaskDriver:OnArrivedAtStandPosition (GameCore.Labor.WorkTask,UnityEngine.Transform,int) (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:213)
  GameCore.Actors.ReceivingTaskDriver/<>c__DisplayClass22_0:<OnArrivedAtStandPosition>b__0 () (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:226)
  AiNavigation:Update () (at Assets/_Project/Scripts/Actors/AiNavigation.cs:966)
  ```
- day 3 23:01 | **ERROR** | [ReceiverReceivingWorkflow] No master record for pallet b00de7c7-7a4d-409c-ab75-e05abcf8b387
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.ReceiverReceivingWorkflow:BeginReceivingAt (GameCore.Labor.WorkTask,UnityEngine.Transform,single) (at Assets/_Project/Scripts/1. FSM/Core/Labor/ReceiverReceivingWorkflow.cs:247)
  GameCore.Actors.ReceivingTaskDriver:OnArrivedAtStandPosition (GameCore.Labor.WorkTask,UnityEngine.Transform,int) (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:213)
  GameCore.Actors.ReceivingTaskDriver/<>c__DisplayClass22_0:<OnArrivedAtStandPosition>b__0 () (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:226)
  AiNavigation:Update () (at Assets/_Project/Scripts/Actors/AiNavigation.cs:966)
  ```
- day 4 00:38 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2B and it never freed up — giving up and re-queueing.
- day 4 00:42 | **DOOR-DEADLOCK** | OG2730 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 4 01:35 | **EXCEPTION** | MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_position () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  GameCore.Actors.ReachTruckOperator+<DeliverPalletToRack>d__94.MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:1275)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 6 01:42 | **WARN** | [AiNavigation] Dockstocker: stuck at (15.57, 1.11, 33.60) — walking out to (15.57, 0.08, 42.67) (8.13m to mesh).
- day 6 01:43 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2C and it never freed up — giving up and re-queueing.
- day 6 01:48 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (15.57, 0.00, 42.64) (cell (61, 82)). Putting it back on its last inside spot (15.57, 0.64, 41.72). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 6 05:42 | **OVERLAP** | OVERLAP Pallet_02_Slot1_Tier1 & Pallet_01_Slot1_Tier0 @2C-2  <<HISTORY: d6 05:19 registered at (55, 66) 32x482913 by InventoryService.RegisterPhysicalPallet | d6 05:19 Receive task created (->STG2C-2) by ReceivingService.CreateReceiveTaskForPallet | d6 05:20 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>  <<HISTORY: d6 05:34 registered at (55, 66) 8x193587 by InventoryService.RegisterPhysicalPallet | d6 05:34 Receive task created (->STG2C-2) by ReceivingService.CreateReceiveTaskForPallet | d6 05:39 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 6 06:27 | **FLOATING** | FLOATING Pallet_03_Slot2_Tier0 @2C-1 bottom=1.30  <<HISTORY: d6 06:08 registered at (55, 65) 32x750426 by InventoryService.RegisterPhysicalPallet | d6 06:08 Receive task created (->STG2C-1) by ReceivingService.CreateReceiveTaskForPallet | d6 06:09 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d6 06:14 Receive task Assigned->Available by ReceivingTaskDriver.ReleaseClaimedTask | d6 06:15 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- day 6 18:18 | **REPEATING** | x25 and counting: [JackStall] 'bay 01-05' at (11.81, 1.11, 32.85) (target (-19.94, 1.11, 18.61)) remaining=36.55 pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(-2.32, 0.00, -0.93) stopped=False onMesh=True jackBlocked=False elapsed=6.8s reissues=0
- day 7 15:13 | **REPEATING** | x25 and counting: [JackStall] 'bay 01-03' at (15.67, 1.11, 34.50) (target (-14.64, 1.11, 18.61)) remaining=Infinity pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(-1.87, 0.00, -1.37) stopped=False onMesh=True jackBlocked=False elapsed=4.9s reissues=0
- day 7 21:57 | **DOOR-DEADLOCK** | OG2730 is Staged but door 1's truck has waited 90 min with no one loading it.
