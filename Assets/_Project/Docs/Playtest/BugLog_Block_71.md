# Bug log - block 71

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:19 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (7.49, 1.16, 48.41) — walking out to (-3.77, 1.20, 41.91) (12.00m to mesh).
- day 1 09:10 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-10.83, 1.14, 55.88) — walking out to (-4.33, 1.22, 44.62) (12.00m to mesh).
- day 1 09:35 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (2.38, 1.16, 47.19) — walking out to (2.38, 0.04, 34.19) (12.00m to mesh).
- day 1 09:36 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet e4fd821a-e5f3-49f9-b3cd-666de3162ae6 but the employee is 6.2m away (max 4.5m) — refusing to start receiving.
- day 1 10:04 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (-39.14, 1.17, 49.29) (path invalid) — abandoning the walk and handing the task back.
- day 1 12:38 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-7.77, 1.19, 46.79) — walking out to (-14.27, 1.09, 35.53) (12.00m to mesh).
- day 1 13:00 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-8.39, 1.11, 40.61) — walking out to (-8.97, 1.19, 39.23) (12.00m to mesh).
- day 1 16:51 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (0.75, 1.11, 48.76) — walking out to (0.18, 1.23, 50.15) (12.08m to mesh).
- day 1 18:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 21 task(s) it could take wait
- day 1 18:19 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-5.96, 1.11, 51.40) — walking out to (-5.39, 1.21, 52.78) (12.34m to mesh).
- day 1 18:19 | **ERROR** | "GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_remainingDistance ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__238:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2189)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 18:19 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__238:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2189)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 19:11 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-11.51, 1.13, 58.16) — walking out to (-22.74, 1.12, 50.87) (12.39m to mesh).
- day 1 19:40 | **OVERLAP** | OVERLAP Pallet_01_Slot0_Tier1 & Pallet_03_Slot1_Tier1 @(34, 89)  <<HISTORY: d1 10:46 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 18:22 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 18:41 moved (56, 81)->(-1, -1) by InventoryService.MovePallet | d1 19:01 moved (-1, -1)->(35, 89) by InventoryService.MovePallet | d1 19:01 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:02 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>  <<HISTORY: d1 10:56 Putaway task created (STG3C-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 10:56 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:03 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 19:14 moved (55, 85)->(-1, -1) by InventoryService.MovePallet | d1 19:26 moved (-1, -1)->(35, 89) by InventoryService.MovePallet | d1 19:26 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 1 19:40 | **OVERLAP** | OVERLAP Pallet_02_Slot1_Tier0 & Pallet_02_Slot1_Tier1 @(34, 89)  <<HISTORY: d1 11:00 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 18:35 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 18:45 moved (55, 85)->(-1, -1) by InventoryService.MovePallet | d1 19:15 moved (-1, -1)->(35, 89) by InventoryService.MovePallet | d1 19:15 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:15 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>  <<HISTORY: d1 11:07 Putaway task created (STG1D-2->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 11:07 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:15 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 19:26 moved (43, 82)->(-1, -1) by InventoryService.MovePallet | d1 19:38 moved (-1, -1)->(35, 89) by InventoryService.MovePallet | d1 19:38 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 1 21:48 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-02 (loop in)' (7.1m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 21:56 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 2 02:02 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-04 (loop in)' (9.7m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 02:40 | **REPEATING** | x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- day 2 02:56 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-11.45, 1.11, 48.92) — walking out to (-10.87, 1.22, 50.30) (12.00m to mesh).
- day 2 02:56 | **ERROR** | "SetDestination" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:SetDestination (UnityEngine.Vector3)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__238:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2159)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 03:05 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-41.80, 1.17, 45.43) — walking out to (-41.22, 1.17, 46.81) (12.08m to mesh).
- day 2 03:08 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-04 (aisle entry, passing lane) (route)' (7.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 03:53 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-16.26, 1.11, 47.66).
- day 2 07:56 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-20.14, 1.11, 45.38) — walking out to (-20.70, 1.16, 46.76) (12.08m to mesh).
- day 2 07:56 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 08:08 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-16.26, 1.11, 47.66).
- day 2 08:17 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-11-A0 anchor: NO NAVMESH PATH. anchor=(-32.51, 3.11, 40.71) → driveTarget=(-32.51, 1.15, 40.71), from=(-20.33, 1.11, 46.27), elapsed=30.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=15.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 106.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 08:56 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' replenish: → reserve 01-14-B1 anchor: NO NAVMESH PATH. anchor=(-36.49, 5.11, 41.34) → driveTarget=(-36.49, 1.15, 41.34), from=(-19.92, 1.16, 45.57), elapsed=31.7s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=18.4, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [0x00025] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2375 < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__161.MoveNext () [0x0010e] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:3060 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 61.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 09:59 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-26.26, 1.16, 47.66).
- day 2 09:59 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-37.78, 1.16, 48.39) — walking out to (-26.26, 1.16, 47.66) (11.54m to mesh).
- day 2 09:59 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 10:05 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-27.05, 1.16, 41.10) — walking out to (-25.66, 1.16, 41.68) (12.31m to mesh).
- day 2 10:06 | **REPEATING** | x25 and counting: [OpsMetrics] save failed: Win32 IO returned 1224. Path: C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Metrics\bot_metrics.json
- day 2 10:23 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane 2D exit: NO NAVMESH PATH. anchor=(-0.71, 1.11, 48.99) → driveTarget=(-0.71, 1.24, 48.99), from=(-22.77, 1.11, 48.96), elapsed=31.6s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=22.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 113.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 11:01 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (7.37, 1.20, 58.91) — walking out to (-5.63, 1.12, 58.91) (12.00m to mesh).
- day 2 11:47 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (7.24, 1.20, 58.91) — walking out to (-5.76, 1.12, 58.91) (12.00m to mesh).
- day 2 12:08 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (3.79, 1.17, 57.44) — walking out to (4.36, 1.16, 58.82) (12.00m to mesh).
- day 2 12:22 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-13.96, 1.11, 57.59) — walking out to (-13.96, 1.16, 59.00) (12.22m to mesh).
- day 2 13:39 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-05 (aisle entry, passing lane)' (17.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 13:39 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 02-05' - driving straight to the stop instead.
- day 2 13:50 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-29.80, 1.11, 48.03) — walking out to (-16.80, 1.22, 48.03) (12.00m to mesh).
- day 2 14:13 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-40.81, 1.11, 42.34).
- day 2 14:13 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-37.31, 1.11, 41.99) — walking out to (-40.81, 1.11, 42.34) (3.52m to mesh).
- day 2 15:44 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-16.56, 1.12, 58.91) — walking out to (-10.06, 1.20, 47.65) (12.00m to mesh).
- day 2 17:11 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-16.69, 1.12, 58.91) — walking out to (-10.19, 1.20, 47.65) (12.00m to mesh).
- day 2 17:14 | **ERROR** | [MHEBoundary] 'PltJack_FULL(Clone)' drove OUT of the building at (-48.79, 1.11, 43.67) (cell (13, 82)). Putting it back on its last inside spot (-48.45, 1.11, 42.84). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 2 17:33 | **ERROR** | [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to trailer: NO NAVMESH PATH to (-2.79, 1.14, 38.89) from (-4.33, 1.14, 39.08) (elapsed 30.0s).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__103:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1222)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 17:33 | **ERROR** | [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<PlaceCarriedInTrailer>d__97:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:981)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 17:51 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-2.29, 1.11, 43.65) — walking out to (-2.86, 1.24, 45.04) (12.00m to mesh).
- day 2 19:00 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (3.90, 1.16, 55.09) — walking out to (3.65, 1.12, 42.09) (12.00m to mesh).
- day 2 19:03 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (2.51, 1.15, 58.96) — walking out to (13.77, 0.04, 52.46) (12.00m to mesh).
- day 2 19:23 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-10.47, 1.11, 54.49) — walking out to (-9.90, 1.19, 55.88) (12.45m to mesh).
- day 2 20:10 | **DOOR-DEADLOCK** | OG3675 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2B: [1: own/Staged/y1.2] <Load Available> 
- day 2 20:10 | **DOOR-DEADLOCK** | OG3678 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2C: [1: own/Staged/y1.2, own/Staged/y2.7] 
- day 2 20:10 | **DOOR-DEADLOCK** | OG3681 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2D: lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2, OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y2.2] [3: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2, OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y2.2] [4: own/Staged/y2.0, own/Staged/y1.2] <Dig Available> 
- day 2 20:10 | **DOOR-DEADLOCK** | OG1815 is Loading but door 2's truck has waited 90 min with no one loading it. | lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.2] [3: own/Staged/y1.2, own/Staged/y2.2] [4: OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y2.0, OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y1.2] <Dig Available> lane 2B: [1: OTHER acbd073d-a18f-48a7-9b84-7964fc8b3691/Staged/y1.2] <Load Available> 
- day 2 20:40 | **DOOR-DEADLOCK** | OG3675 is Staged but door 2's truck has waited 120 min with no one loading it. | lane 2B: [1: own/Staged/y1.2] 
- day 2 20:40 | **DOOR-DEADLOCK** | OG1815 is Staged but door 2's truck has waited 120 min with no one loading it. | lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.2] [3: own/Staged/y1.2, own/Staged/y2.2] [4: OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y2.0, OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y1.2] <Dig Available> lane 2B: [1: OTHER acbd073d-a18f-48a7-9b84-7964fc8b3691/Staged/y1.2] 
- day 2 21:10 | **DOOR-DEADLOCK** | OG3675 is Staged but door 2's truck has waited 150 min with no one loading it. | lane 2B: [1: own/Staged/y1.2] <Load Available> 
- day 2 21:10 | **DOOR-DEADLOCK** | OG1815 is Loading but door 2's truck has waited 150 min with no one loading it. | lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.2] [3: own/Staged/y1.2, own/Staged/y2.2] [4: OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y2.0, OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y1.2] <Dig Available> lane 2B: [1: OTHER acbd073d-a18f-48a7-9b84-7964fc8b3691/Staged/y1.2] <Load Available> 
- day 2 21:30 | **WARN** | [OrderSelectionTaskDriver] Order 6ef3c574-0350-45d1-bbe7-e773a49b0a4c (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at 513/592.
- day 2 21:40 | **DOOR-DEADLOCK** | OG3675 is Staged but door 2's truck has waited 180 min with no one loading it. | lane 2B: [1: own/Staged/y1.2] 
- day 2 21:40 | **DOOR-DEADLOCK** | OG1815 is Staged but door 2's truck has waited 180 min with no one loading it. | lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.2] [3: own/Staged/y1.2, own/Staged/y2.2] [4: OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y2.0, OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y1.2] <Dig Available> lane 2B: [1: OTHER acbd073d-a18f-48a7-9b84-7964fc8b3691/Staged/y1.2] 
- day 2 21:56 | **WARN** | [OrderSelectionTaskDriver] Order edc76943-da5c-4596-85b4-dfcdc6bb5d44 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 307/420.
- day 2 22:10 | **DOOR-DEADLOCK** | OG3675 is Staged but door 2's truck has waited 210 min with no one loading it. | lane 2B: [1: own/Staged/y1.2] <Load Available> 
- day 2 22:10 | **DOOR-DEADLOCK** | OG1815 is Loading but door 2's truck has waited 210 min with no one loading it. | lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.2] [3: own/Staged/y1.2, own/Staged/y2.2] [4: OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y2.0, OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y1.2] <Dig Available> lane 2B: [1: OTHER acbd073d-a18f-48a7-9b84-7964fc8b3691/Staged/y1.2] <Load Available> 
- day 2 22:16 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (5.22, 1.18, 56.56) — walking out to (-1.28, 1.23, 45.30) (12.00m to mesh).
- day 2 22:40 | **DOOR-DEADLOCK** | OG3675 is Staged but door 2's truck has waited 240 min with no one loading it. | lane 2B: [1: own/Staged/y1.2] 
- day 2 22:40 | **DOOR-DEADLOCK** | OG3681 is Staged but door 2's truck has waited 240 min with no one loading it. | lane 2D: lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2, OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y2.2] [3: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2, OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y2.2] [4: own/Staged/y2.0, own/Staged/y1.2] [5: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2] <Dig Available> 
- day 2 22:40 | **DOOR-DEADLOCK** | OG1815 is Staged but door 2's truck has waited 240 min with no one loading it. | lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.2] [3: own/Staged/y1.2, own/Staged/y2.2] [4: OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y2.0, OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y1.2] [5: own/Staged/y1.2] <Dig Available> lane 2B: [1: OTHER acbd073d-a18f-48a7-9b84-7964fc8b3691/Staged/y1.2] 
- day 2 23:10 | **DOOR-DEADLOCK** | OG3681 is Staged but door 2's truck has waited 270 min with no one loading it. | lane 2D: lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2, OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y2.2] [3: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2, OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y2.2] [4: own/Staged/y2.0, own/Staged/y1.2] <Dig Available> 
- day 2 23:10 | **DOOR-DEADLOCK** | OG1815 is Staged but door 2's truck has waited 270 min with no one loading it. | lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.2] [3: own/Staged/y1.2, own/Staged/y2.2] [4: OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y2.0, OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y1.2] <Dig Available> lane 2B: [1: OTHER acbd073d-a18f-48a7-9b84-7964fc8b3691/Staged/y1.2] 
- day 2 23:26 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (7.29, 1.20, 58.91) — walking out to (-5.71, 1.12, 58.91) (12.00m to mesh).
- day 2 23:38 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (6.30, 1.11, 56.28) — walking out to (-0.20, 1.23, 45.02) (12.00m to mesh).
- day 2 23:59 | **WARN** | [OrderSelectionTaskDriver] Order edc76943-da5c-4596-85b4-dfcdc6bb5d44 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 382/420.
