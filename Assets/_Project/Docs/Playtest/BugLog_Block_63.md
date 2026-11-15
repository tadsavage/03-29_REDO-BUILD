# Bug log - block 63

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:13 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (12.88, 1.16, 36.88) — walking out to (-0.12, 1.11, 36.89) (12.00m to mesh).
- day 1 08:15 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (12.14, 1.20, 36.17) — walking out to (12.14, 1.14, 49.17) (12.00m to mesh).
- day 1 08:18 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (9.89, 1.11, 56.26) — walking out to (9.89, 1.25, 43.26) (12.00m to mesh).
- day 1 08:19 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-13.96, 1.11, 57.59) — walking out to (-13.96, 1.16, 59.00) (12.22m to mesh).
- day 1 09:04 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-8.15, 1.13, 56.22) — walking out to (-1.65, 1.23, 44.96) (12.00m to mesh).
- day 1 09:18 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-36.43, 1.11, 42.34).
- day 1 10:08 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 10:15 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (7.28, 1.23, 42.15) — walking out to (-3.97, 1.09, 35.65) (12.00m to mesh).
- day 1 10:50 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-9.99, 1.11, 39.69) — walking out to (-10.56, 1.16, 38.30) (12.00m to mesh).
- day 1 10:59 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-23.88, 1.11, 48.13), elapsed=32.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=15.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [0x00025] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2375 < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__160.MoveNext () [0x000f1] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:3058 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 35.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 13:00 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (4.93, 1.12, 59.19) — walking out to (4.93, 1.23, 46.19) (12.00m to mesh).
- day 1 13:03 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (4.93, 1.12, 59.13) — walking out to (4.93, 1.23, 46.13) (12.00m to mesh).
- day 1 13:17 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-32.56, 1.11, 42.34).
- day 1 13:17 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-21.04, 1.11, 43.21) — walking out to (-32.56, 1.11, 42.34) (11.55m to mesh).
- day 1 13:17 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 13:41 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-28.50, 1.20, 49.51), elapsed=30.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=10.6, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 215.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 14:36 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 119/248.
- day 1 15:20 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B1 anchor: NO NAVMESH PATH. anchor=(-37.81, 5.11, 49.29) → driveTarget=(-37.81, 1.18, 49.29), from=(-16.69, 1.11, 49.48), elapsed=30.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=21.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 143.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:04 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-38.78, 1.12, 41.73) — walking out to (-27.53, 1.08, 35.17) (12.14m to mesh).
- day 1 16:13 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 119/248.
- day 1 16:38 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-38.24, 1.11, 40.38), elapsed=30.6s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 219.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 18:26 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-39.77, 1.12, 42.34) — walking out to (-28.54, 1.08, 35.17) (12.46m to mesh).
- day 1 19:41 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-04-B0 anchor: NO NAVMESH PATH. anchor=(-21.91, 5.11, 41.34) → driveTarget=(-21.91, 1.22, 41.34), from=(-20.70, 1.11, 44.21), elapsed=31.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=3.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 130.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 20:20 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-21.82, 1.22, 43.21), elapsed=30.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__141.MoveNext () [0x00786] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2769 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 203.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 20:33 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 155/248.
- day 1 21:39 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-15.42, 1.13, 57.14) — walking out to (-8.65, 1.20, 45.89) (12.13m to mesh).
- day 1 21:56 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-11-B1 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 48.66) → driveTarget=(-24.56, 1.21, 48.66), from=(-13.76, 1.11, 49.86), elapsed=32.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=10.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 166.4s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 22:53 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (0.61, 1.11, 48.99) — walking out to (0.04, 1.23, 50.37) (12.00m to mesh).
- day 1 22:53 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 23:10 | **OVERLAP** | OVERLAP Pallet_00_Slot0_Tier0 & Pallet_00_Slot0_Tier0 @(33, 83)  <<HISTORY: d1 09:46 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 09:46 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 10:20 moved (43, 85)->(-1, -1) by InventoryService.MovePallet | d1 11:44 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 11:44 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 11:46 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>  <<HISTORY: d1 21:16 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 21:56 Putaway task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 22:48 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 22:51 moved (50, 85)->(-1, -1) by InventoryService.MovePallet | d1 23:02 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 23:02 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 1 23:59 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 212/248.
- day 2 00:20 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-40.81, 1.11, 42.34).
- day 2 00:20 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-29.98, 1.11, 40.57) — walking out to (-40.81, 1.11, 42.34) (10.98m to mesh).
- day 2 00:20 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 01:44 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-13-A1 anchor: NO NAVMESH PATH. anchor=(-21.91, 3.11, 48.66) → driveTarget=(-21.91, 1.21, 48.66), from=(-18.09, 1.11, 49.50), elapsed=30.3s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=3.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 242.7s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 02:59 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-04-B1 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 41.34) → driveTarget=(-23.24, 1.22, 41.34), from=(-19.29, 1.11, 42.13), elapsed=32.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=4.0, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 216.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 03:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 03:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 03:41 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-11.06, 1.11, 45.87) — walking out to (-12.45, 1.25, 46.44) (12.09m to mesh).
- day 2 03:41 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 04:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 26 task(s) it could take wait
- day 2 05:36 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 218/248.
- day 2 07:11 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-35.31, 1.11, 42.34).
- day 2 07:11 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 07:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 26 task(s) it could take wait
- day 2 09:10 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-28.56, 1.12, 42.42) — walking out to (-39.78, 1.08, 35.17) (12.51m to mesh).
- day 2 09:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 26 task(s) it could take wait
- day 2 09:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 26 task(s) it could take wait
- day 2 11:11 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-05-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 40.71) → driveTarget=(-24.56, 1.20, 40.71), from=(-23.43, 1.20, 40.29), elapsed=31.2s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=1.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 143.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 11:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 26 task(s) it could take wait
- day 2 11:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 26 task(s) it could take wait
- day 2 12:26 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-05-B1 anchor: NO NAVMESH PATH. anchor=(-25.89, 5.11, 40.71) → driveTarget=(-25.89, 1.20, 40.71), from=(-24.10, 1.11, 40.34), elapsed=32.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=1.8, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 214.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 13:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 24 task(s) it could take wait
- day 2 13:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 26 task(s) it could take wait
- day 2 15:23 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-15-A1 anchor: NO NAVMESH PATH. anchor=(-39.14, 3.11, 40.71) → driveTarget=(-39.14, 1.15, 40.71), from=(-33.58, 1.17, 40.55), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=5.6, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 177.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 17:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 21 task(s) it could take wait
- day 2 17:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 25 task(s) it could take wait
- day 2 17:54 | **ERROR** | [GridNav] 'Employee_Mark Peterson': 'staging lane 3E line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:103)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 18:54 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-28.43, 1.12, 42.62) — walking out to (-39.64, 1.08, 35.17) (12.62m to mesh).
- day 2 21:41 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-16.61, 1.12, 58.91) — walking out to (-10.11, 1.21, 47.65) (12.00m to mesh).
- day 2 23:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 22 task(s) it could take wait
