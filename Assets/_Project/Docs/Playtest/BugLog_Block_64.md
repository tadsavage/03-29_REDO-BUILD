# Bug log - block 64

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:11 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.09, 1.21, 52.29) (12.00m to mesh).
- day 1 08:12 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (12.88, 1.15, 36.88) — walking out to (-0.12, 1.11, 36.89) (12.00m to mesh).
- day 1 10:07 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (1.90, 1.18, 42.18) — walking out to (-9.35, 1.08, 35.37) (12.15m to mesh).
- day 1 10:08 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 4e64b88f-c1ee-4f10-a03d-a305d54141d6 but the employee is 4.5m away (max 4.5m) — refusing to start receiving.
- day 1 10:24 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-6.91, 1.16, 45.66) — walking out to (-13.41, 0.04, 34.40) (12.00m to mesh).
- day 1 10:24 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-22.85, 1.19, 47.66).
- day 1 10:24 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-34.37, 1.19, 49.88) — walking out to (-22.85, 1.19, 47.66) (11.73m to mesh).
- day 1 10:24 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 10:26 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 2ccd4c4b-900d-46b6-af5a-8e05290ac78e but the employee is 10.0m away (max 4.5m) — refusing to start receiving.
- day 1 11:38 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B1 anchor: NO NAVMESH PATH. anchor=(-27.21, 5.11, 49.29) → driveTarget=(-27.21, 1.21, 49.29), from=(-9.54, 1.11, 49.88), elapsed=31.8s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=17.7, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 143.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 12:06 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-10.54, 1.15, 51.58) — walking out to (-23.54, 1.12, 51.40) (12.00m to mesh).
- day 1 12:10 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-10.92, 1.15, 53.01) — walking out to (-10.93, 1.15, 39.86) (12.14m to mesh).
- day 1 13:06 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-22.03, 1.11, 41.40) — walking out to (-9.47, 1.25, 41.40) (12.00m to mesh).
- day 1 13:55 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 108/248.
- day 1 14:20 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-16.62, 1.12, 58.91) — walking out to (-10.12, 1.19, 47.81) (12.00m to mesh).
- day 1 15:17 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-03-B1 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 40.71) → driveTarget=(-23.24, 1.21, 40.71), from=(-7.53, 1.25, 46.91), elapsed=31.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 206.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 15:26 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-06-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 41.34) → driveTarget=(-24.56, 1.21, 41.34), from=(-9.01, 1.11, 50.28), elapsed=30.8s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=18.0, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 224.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:38 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 111/248.
- day 1 16:44 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-03-B1 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 40.71) → driveTarget=(-23.24, 1.21, 40.71), from=(-20.75, 1.11, 42.14), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=2.7, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 179.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:56 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.61, 1.14, 47.52) — walking out to (-16.61, 0.04, 34.46) (12.06m to mesh).
- day 1 17:05 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (-8.66, 1.24, 48.99) (path invalid) — abandoning the walk and handing the task back.
- day 1 17:10 | **OVERLAP** | OVERLAP Pallet_01_Slot1_Tier0 & Pallet_00_Slot0_Tier0 @(33, 83)  <<HISTORY: d1 15:21 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 16:01 Replenish task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 16:42 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 16:47 moved (31, 78)->(-1, -1) by InventoryService.MovePallet | d1 16:54 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 16:54 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 15:26 Putaway task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 15:28 moved (-1, -1)->(43, 85) by InventoryService.MovePallet | d1 16:53 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 16:56 moved (43, 85)->(-1, -1) by InventoryService.MovePallet | d1 17:02 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 17:02 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 1 17:54 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-03-B1 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 40.71) → driveTarget=(-23.24, 1.21, 40.71), from=(-11.18, 1.11, 49.31), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=14.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 156.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 21:00 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-05-B1 anchor: NO NAVMESH PATH. anchor=(-25.89, 5.11, 40.71) → driveTarget=(-25.89, 1.20, 40.71), from=(-20.53, 1.23, 45.66), elapsed=30.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=8.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 214.8s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 21:27 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-19.19, 1.13, 42.68) — walking out to (-30.40, 1.08, 35.17) (12.65m to mesh).
- day 1 22:21 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 136/248.
- day 2 01:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 2 01:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 26 task(s) it could take wait
- day 2 02:20 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-12.82, 1.11, 48.50) — walking out to (-12.25, 1.23, 49.88) (12.01m to mesh).
- day 2 02:20 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 03:20 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-13-B0 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 48.66) → driveTarget=(-23.24, 1.21, 48.66), from=(-13.46, 1.11, 49.12), elapsed=32.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=9.8, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 214.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 03:45 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 139/248.
- day 2 03:57 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-17.28, 1.14, 42.49) — walking out to (-28.50, 1.08, 35.17) (12.55m to mesh).
- day 2 04:21 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-14.97, 1.16, 40.71) — walking out to (-3.71, 1.25, 47.21) (12.00m to mesh).
- day 2 06:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 06:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 07:50 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-13-A1 anchor: NO NAVMESH PATH. anchor=(-21.91, 3.11, 48.66) → driveTarget=(-21.91, 1.21, 48.66), from=(-15.89, 1.23, 45.83), elapsed=30.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=6.7, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 175.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 08:02 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.13, 1.15, 47.88) — walking out to (-16.13, 0.04, 34.46) (12.42m to mesh).
- day 2 09:50 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.61, 1.14, 39.39) — walking out to (-27.83, 1.12, 47.10) (12.62m to mesh).
- day 2 10:20 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-9.56, 1.11, 49.22) — walking out to (-8.99, 1.22, 50.61) (12.75m to mesh).
- day 2 10:20 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 11:40 | **ERROR** | [GridNav] 'Employee_Mark Peterson': 'staging lane 3E line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:103)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 12:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 13:29 | **ERROR** | [GridNav] 'Employee_Larry Morgan': 'staging lane 3E wait' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:103)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 14:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 14:38 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-30.78, 1.11, 42.34).
- day 2 14:38 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.26, 1.11, 43.21) — walking out to (-30.78, 1.11, 42.34) (11.55m to mesh).
- day 2 14:39 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-11.06, 1.11, 46.52) — walking out to (-12.56, 1.25, 46.52) (12.54m to mesh).
- day 2 16:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 17:16 | **ERROR** | [GridNav] 'Employee_Larry Morgan': 'staging lane 3E line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:103)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 17:21 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 174/248.
- day 2 18:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 18:56 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-11-A1 anchor: NO NAVMESH PATH. anchor=(-33.84, 3.11, 40.71) → driveTarget=(-33.84, 1.17, 40.71), from=(-5.28, 1.23, 49.32), elapsed=32.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 204.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 19:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- day 2 20:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 20:35 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-14-A0 anchor: NO NAVMESH PATH. anchor=(-23.24, 3.11, 49.29) → driveTarget=(-23.24, 1.21, 49.29), from=(-18.94, 1.11, 49.51), elapsed=30.2s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=3.5, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 281.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 20:48 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 176/248.
- day 2 22:01 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-10.53, 1.20, 45.56) — walking out to (-23.52, 1.12, 46.62) (12.04m to mesh).
- day 2 22:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 22:26 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 176/248.
- day 2 22:55 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-12-A1 anchor: NO NAVMESH PATH. anchor=(-33.84, 3.11, 41.34) → driveTarget=(-33.84, 1.18, 41.34), from=(-25.83, 1.11, 42.64), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=7.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 351.4s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 23:47 | **REPEATING** | x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (-13.04, 1.11, 45.51) - the next move would violate the 1 m vehicle clearance from PltJack_FULL(Clone).
