# Bug log - block 69

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 09:33 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-6.47, 1.12, 59.25) — walking out to (-12.97, 1.17, 47.99) (12.00m to mesh).
- day 1 09:34 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 12273cb3-0f19-453f-9560-14d15e376b4b but the employee is 19.6m away (max 4.5m) — refusing to start receiving.
- day 1 09:34 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 12273cb3-0f19-453f-9560-14d15e376b4b but the employee is 19.6m away (max 4.5m) — refusing to start receiving.
- day 1 09:35 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-8.78, 1.14, 55.19) — walking out to (-2.28, 1.22, 43.93) (12.00m to mesh).
- day 1 09:35 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-8.78, 1.14, 55.19) — walking out to (-2.28, 1.22, 43.93) (12.00m to mesh).
- day 1 09:46 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (9.46, 1.20, 46.79) — walking out to (9.46, 1.12, 59.25) (12.00m to mesh).
- day 1 09:46 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (9.46, 1.20, 46.79) — walking out to (9.46, 1.12, 59.25) (12.00m to mesh).
- day 1 09:48 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (-37.81, 1.18, 49.29) (path invalid) — abandoning the walk and handing the task back.
- day 1 12:00 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 1B wait' (4.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__226:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 12:00 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 1B wait' (4.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__226:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 12:10 | **OVERLAP** | OVERLAP A Chep & Pallet_01_Slot1_Tier0 @(33, 83)  <<HISTORY: d1 10:57 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:37 Replenish task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 11:57 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:59 moved (33, 83)->(-1, -1) by InventoryService.MovePallet | d1 12:03 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 12:03 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 09:55 Putaway task created (STG1D-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 09:55 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 11:38 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:42 moved (43, 85)->(-1, -1) by InventoryService.MovePallet | d1 11:47 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 11:47 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 1 12:17 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-02 (aisle entry)' (11.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__226:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 12:17 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-02 (aisle entry)' (11.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__226:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 12:17 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-02' - driving straight to the stop instead.
- day 1 12:17 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-02' - driving straight to the stop instead.
- day 1 12:40 | **OVERLAP** | OVERLAP A Chep & Pallet_01_Slot1_Tier0 @(33, 83)  <<HISTORY: d1 10:57 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:37 Replenish task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 11:57 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:59 moved (33, 83)->(-1, -1) by InventoryService.MovePallet | d1 12:03 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 12:03 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 09:55 Putaway task created (STG1D-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 09:55 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 11:38 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:42 moved (43, 85)->(-1, -1) by InventoryService.MovePallet | d1 11:47 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 11:47 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 1 13:33 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-32.22, 1.11, 42.34).
- day 1 13:33 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-32.22, 1.11, 42.34).
- day 1 13:33 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-20.70, 1.11, 43.61) — walking out to (-32.22, 1.11, 42.34) (11.59m to mesh).
- day 1 13:33 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-20.70, 1.11, 43.61) — walking out to (-32.22, 1.11, 42.34) (11.59m to mesh).
- day 1 14:19 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 14:19 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 17:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 1 17:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 1 17:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 1 17:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 1 19:01 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-14-A1 anchor: NO NAVMESH PATH. anchor=(-21.91, 3.11, 49.29) → driveTarget=(-21.91, 1.21, 49.29), from=(-17.63, 1.11, 47.06), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=4.8, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [0x00025] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2375 < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__161.MoveNext () [0x0010e] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:3060 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 7.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 19:01 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-14-A1 anchor: NO NAVMESH PATH. anchor=(-21.91, 3.11, 49.29) → driveTarget=(-21.91, 1.21, 49.29), from=(-17.63, 1.11, 47.06), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=4.8, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [0x00025] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2375 < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__161.MoveNext () [0x0010e] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:3060 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 7.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 19:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 1 19:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 1 19:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 1 19:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 1 20:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 1 20:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 1 21:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 1 21:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 361s while 27 task(s) it could take wait
- day 1 22:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 1 22:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 361s while 23 task(s) it could take wait
- day 1 23:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 1 23:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 1 23:32 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (7.32, 1.12, 58.93) — walking out to (8.12, 1.21, 45.93) (12.02m to mesh).
- day 1 23:32 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (7.32, 1.12, 58.93) — walking out to (8.12, 1.21, 45.93) (12.02m to mesh).
- day 2 00:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 00:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 01:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 01:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 02:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 02:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 03:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 03:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 04:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 04:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 05:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 05:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 06:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 06:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 07:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 07:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 08:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 08:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 09:05 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-0.79, 1.12, 59.25) — walking out to (-7.29, 1.18, 47.99) (12.00m to mesh).
- day 2 09:05 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-0.79, 1.12, 59.25) — walking out to (-7.29, 1.18, 47.99) (12.00m to mesh).
- day 2 09:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 09:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 10:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 10:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 11:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 11:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 12:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 12:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 13:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 13:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 14:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 14:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 15:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 15:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 16:35 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-13.05, 1.12, 59.25) — walking out to (-6.55, 1.18, 47.99) (12.00m to mesh).
- day 2 16:35 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-13.05, 1.12, 59.25) — walking out to (-6.55, 1.18, 47.99) (12.00m to mesh).
- day 2 16:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 16:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 17:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 17:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 17:40 | **DOOR-DEADLOCK** | OG3625 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3E: [1: own/Staged/y2.6, own/Staged/y1.2] 
- day 2 17:40 | **DOOR-DEADLOCK** | OG3625 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3E: [1: own/Staged/y2.6, own/Staged/y1.2] 
- day 2 18:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 18:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 19:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 19:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 20:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 20:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 21:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 21:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 22:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 22:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 23:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 23:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 3 00:00 | **TRUCK-STALLED** | TRUCK-STALLED Truck→Outbound_Door1 state=DepartToApproach 1410s at (-11.3, 0.0, 27.9)
