# Bug log - block 39

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 09:04 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 1 09:39 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (-47.09, 1.19, 39.04) (path invalid) — abandoning the walk and handing the task back.
- day 1 09:39 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (-47.09, 1.19, 39.04) (path invalid) — abandoning the walk and handing the task back.
- day 1 09:48 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-14.74, 1.16, 43.43) — walking out to (-1.74, 1.21, 43.43) (12.00m to mesh).
- day 1 09:50 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet a4074603-9dd4-49b1-a861-f926a6747989 but the employee is 6.6m away (max 4.5m) — refusing to start receiving.
- day 1 09:50 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet a4074603-9dd4-49b1-a861-f926a6747989 but the employee is 6.6m away (max 4.5m) — refusing to start receiving.
- day 1 11:36 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (7.26, 1.13, 56.56) — walking out to (7.26, 1.22, 43.56) (12.00m to mesh).
- day 1 11:36 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (7.26, 1.13, 56.56) — walking out to (7.26, 1.22, 43.56) (12.00m to mesh).
- day 1 12:26 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (5.91, 1.11, 39.69) — walking out to (7.30, 1.23, 40.26) (12.00m to mesh).
- day 1 12:26 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (5.91, 1.11, 39.69) — walking out to (7.30, 1.23, 40.26) (12.00m to mesh).
- day 1 12:35 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-16.43, 1.12, 58.92) — walking out to (-9.93, 1.21, 47.66) (12.00m to mesh).
- day 1 12:46 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (5.04, 1.12, 59.25) — walking out to (11.54, 1.16, 47.99) (12.00m to mesh).
- day 1 12:46 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (5.04, 1.12, 59.25) — walking out to (11.54, 1.16, 47.99) (12.00m to mesh).
- day 1 13:13 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-11.70, 1.20, 40.85) — walking out to (-24.70, 1.11, 40.85) (12.00m to mesh).
- day 1 13:13 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-11.70, 1.20, 40.85) — walking out to (-24.70, 1.11, 40.85) (12.00m to mesh).
- day 1 13:20 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.46, 1.12, 58.81) — walking out to (-9.96, 1.22, 47.55) (12.00m to mesh).
- day 1 13:20 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.46, 1.12, 58.81) — walking out to (-9.96, 1.22, 47.55) (12.00m to mesh).
- day 1 13:58 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-1.70, 1.12, 59.25) — walking out to (-1.70, 1.24, 46.25) (12.00m to mesh).
- day 1 13:59 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-06-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 41.34) → driveTarget=(-24.56, 1.21, 41.34), from=(-12.89, 1.11, 38.89), elapsed=30.2s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=11.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 171.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 13:59 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-06-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 41.34) → driveTarget=(-24.56, 1.21, 41.34), from=(-12.89, 1.11, 38.89), elapsed=30.2s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=11.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 171.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 14:22 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-7.99, 1.12, 59.17) — walking out to (-7.99, 1.20, 46.17) (12.00m to mesh).
- day 1 14:22 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-7.99, 1.12, 59.17) — walking out to (-7.99, 1.20, 46.17) (12.00m to mesh).
- day 1 14:24 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-7.99, 1.12, 59.17) — walking out to (-7.99, 1.20, 46.17) (12.00m to mesh).
- day 1 14:24 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-7.99, 1.12, 59.17) — walking out to (-7.99, 1.20, 46.17) (12.00m to mesh).
- day 1 14:25 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-7.99, 1.12, 59.20) — walking out to (-7.99, 1.20, 46.20) (12.00m to mesh).
- day 1 14:25 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-7.99, 1.12, 59.20) — walking out to (-7.99, 1.20, 46.20) (12.00m to mesh).
- day 1 14:27 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-2.99, 1.12, 59.13) — walking out to (-2.50, 1.24, 46.13) (12.01m to mesh).
- day 1 14:27 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-2.99, 1.12, 59.13) — walking out to (-2.50, 1.24, 46.13) (12.01m to mesh).
- day 1 15:26 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-6.27, 1.11, 54.80) — walking out to (-5.69, 1.18, 56.19) (12.00m to mesh).
- day 1 15:26 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-6.27, 1.11, 54.80) — walking out to (-5.69, 1.18, 56.19) (12.00m to mesh).
- day 1 15:35 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (5.61, 1.11, 53.27) — walking out to (6.99, 1.20, 53.85) (12.00m to mesh).
- day 1 15:35 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (5.61, 1.11, 53.27) — walking out to (6.99, 1.20, 53.85) (12.00m to mesh).
- day 1 15:35 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-34.10, 1.11, 42.34).
- day 1 15:35 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-34.10, 1.11, 42.34).
- day 1 15:35 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-22.58, 1.11, 40.72) — walking out to (-34.10, 1.11, 42.34) (11.63m to mesh).
- day 1 15:35 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-22.58, 1.11, 40.72) — walking out to (-34.10, 1.11, 42.34) (11.63m to mesh).
- day 1 15:35 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 15:35 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:08 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-16.60, 1.12, 58.91) — walking out to (-10.10, 1.21, 47.66) (12.00m to mesh).
- day 1 16:08 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-16.60, 1.12, 58.91) — walking out to (-10.10, 1.21, 47.66) (12.00m to mesh).
- day 1 16:19 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (6.15, 1.12, 58.53) — walking out to (6.79, 1.21, 45.53) (12.02m to mesh).
- day 1 16:19 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (6.15, 1.12, 58.53) — walking out to (6.79, 1.21, 45.53) (12.02m to mesh).
- day 1 16:41 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:41 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:50 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-47.09, 1.11, 40.36) — walking out to (-48.00, 1.08, 40.36) (12.22m to mesh).
- day 1 16:50 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-47.09, 1.11, 40.36) — walking out to (-48.00, 1.08, 40.36) (12.22m to mesh).
- day 1 17:24 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (7.22, 1.13, 56.56) — walking out to (7.22, 1.22, 43.56) (12.00m to mesh).
- day 1 17:24 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (7.22, 1.13, 56.56) — walking out to (7.22, 1.22, 43.56) (12.00m to mesh).
- day 1 17:49 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.59, 1.11, 50.19).
- day 1 17:49 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.59, 1.11, 50.19).
- day 1 17:49 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-25.86, 1.11, 48.69) — walking out to (-17.59, 1.11, 50.19) (8.41m to mesh).
- day 1 17:49 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-25.86, 1.11, 48.69) — walking out to (-17.59, 1.11, 50.19) (8.41m to mesh).
- day 1 17:49 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 17:49 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 18:08 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-14.43, 1.11, 52.06) — walking out to (-13.85, 1.16, 53.45) (12.03m to mesh).
- day 1 18:08 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-14.43, 1.11, 52.06) — walking out to (-13.85, 1.16, 53.45) (12.03m to mesh).
- day 1 19:49 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-5.04, 1.12, 59.25) — walking out to (-5.04, 1.24, 46.25) (12.00m to mesh).
- day 1 19:49 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-5.04, 1.12, 59.25) — walking out to (-5.04, 1.24, 46.25) (12.00m to mesh).
- day 1 19:59 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (9.41, 1.11, 56.25) — walking out to (9.41, 1.25, 43.25) (12.00m to mesh).
- day 1 19:59 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (9.41, 1.11, 56.25) — walking out to (9.41, 1.25, 43.25) (12.00m to mesh).
- day 1 20:03 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (0.69, 1.13, 56.57) — walking out to (7.24, 1.21, 45.31) (12.02m to mesh).
- day 1 20:03 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (0.69, 1.13, 56.57) — walking out to (7.24, 1.21, 45.31) (12.02m to mesh).
- day 1 20:53 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (7.14, 1.12, 58.93) — walking out to (7.14, 1.21, 45.93) (12.00m to mesh).
- day 1 20:53 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (7.14, 1.12, 58.93) — walking out to (7.14, 1.21, 45.93) (12.00m to mesh).
- day 1 21:23 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-11.67, 1.11, 55.83) — walking out to (-5.17, 1.25, 44.58) (12.00m to mesh).
- day 1 21:23 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-11.67, 1.11, 55.83) — walking out to (-5.17, 1.25, 44.58) (12.00m to mesh).
- day 1 21:29 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (7.24, 1.12, 58.91) — walking out to (7.24, 1.21, 45.91) (12.00m to mesh).
- day 1 21:29 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (7.24, 1.12, 58.91) — walking out to (7.24, 1.21, 45.91) (12.00m to mesh).
- day 1 23:01 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-16-A1 anchor: NO NAVMESH PATH. anchor=(-19.26, 3.11, 49.29) → driveTarget=(-19.26, 1.21, 49.29), from=(-11.94, 1.23, 49.19), elapsed=31.7s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=7.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 188.7s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 23:01 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-16-A1 anchor: NO NAVMESH PATH. anchor=(-19.26, 3.11, 49.29) → driveTarget=(-19.26, 1.21, 49.29), from=(-11.94, 1.23, 49.19), elapsed=31.7s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=7.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 188.7s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 23:02 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-19.33, 1.16, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 2 00:31 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (3.26, 1.12, 59.20) — walking out to (3.26, 1.19, 46.20) (12.00m to mesh).
- day 2 00:33 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (1.05, 1.12, 58.69) — walking out to (1.05, 1.20, 45.69) (12.00m to mesh).
- day 2 00:33 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (1.05, 1.12, 58.69) — walking out to (1.05, 1.20, 45.69) (12.00m to mesh).
- day 2 03:11 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-16.61, 1.12, 58.91) — walking out to (-10.11, 1.21, 47.65) (12.00m to mesh).
- day 2 03:11 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-16.61, 1.12, 58.91) — walking out to (-10.11, 1.21, 47.65) (12.00m to mesh).
- day 2 03:21 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-1.92, 1.12, 59.00) — walking out to (-1.92, 1.23, 46.00) (12.00m to mesh).
- day 2 03:21 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-1.92, 1.12, 59.00) — walking out to (-1.92, 1.23, 46.00) (12.00m to mesh).
- day 2 03:33 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-3.60, 1.12, 58.98) — walking out to (-3.60, 1.24, 46.49) (12.00m to mesh).
- day 2 03:33 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-3.60, 1.12, 58.98) — walking out to (-3.60, 1.24, 46.49) (12.00m to mesh).
- day 2 05:25 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (4.02, 1.12, 59.25) — walking out to (4.02, 1.24, 46.25) (12.00m to mesh).
- day 2 05:25 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (4.02, 1.12, 59.25) — walking out to (4.02, 1.24, 46.25) (12.00m to mesh).
- day 2 06:03 | **REPEATING** | x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- day 2 06:03 | **REPEATING** | x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- day 2 06:23 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (1.21, 1.12, 58.67) — walking out to (1.21, 1.20, 45.67) (12.00m to mesh).
- day 2 06:23 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (1.21, 1.12, 58.67) — walking out to (1.21, 1.20, 45.67) (12.00m to mesh).
- day 2 06:24 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-3.78, 1.12, 58.71) — walking out to (-4.19, 1.23, 45.71) (12.00m to mesh).
- day 2 06:24 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-3.78, 1.12, 58.71) — walking out to (-4.19, 1.23, 45.71) (12.00m to mesh).
- day 2 08:12 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-14.77, 1.12, 58.74) — walking out to (-3.51, 1.16, 52.24) (12.00m to mesh).
- day 2 08:12 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-14.77, 1.12, 58.74) — walking out to (-3.51, 1.16, 52.24) (12.00m to mesh).
- day 2 12:07 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (2.08, 1.12, 58.93) — walking out to (2.08, 1.16, 45.93) (12.00m to mesh).
- day 2 12:07 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (2.08, 1.12, 58.93) — walking out to (2.08, 1.16, 45.93) (12.00m to mesh).
- day 2 14:30 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (6.18, 1.11, 51.77) — walking out to (6.75, 1.21, 53.15) (12.19m to mesh).
- day 2 14:30 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (6.18, 1.11, 51.77) — walking out to (6.75, 1.21, 53.15) (12.19m to mesh).
- day 2 15:04 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-12.10, 1.12, 59.21) — walking out to (-12.10, 1.22, 46.21) (12.00m to mesh).
- day 2 15:04 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-12.10, 1.12, 59.21) — walking out to (-12.10, 1.22, 46.21) (12.00m to mesh).
- day 2 16:35 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-14.61, 1.16, 59.15) — walking out to (-21.11, 1.12, 47.89) (12.00m to mesh).
- day 2 16:35 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-14.61, 1.16, 59.15) — walking out to (-21.11, 1.12, 47.89) (12.00m to mesh).
- day 2 16:35 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-3.85, 1.11, 53.84) — walking out to (-3.28, 1.19, 55.22) (12.00m to mesh).
- day 2 16:35 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-3.85, 1.11, 53.84) — walking out to (-3.28, 1.19, 55.22) (12.00m to mesh).
- day 2 17:47 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-47.09, 1.11, 44.34) — walking out to (-48.00, 1.08, 44.34) (12.05m to mesh).
- day 2 17:47 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-47.09, 1.11, 44.34) — walking out to (-48.00, 1.08, 44.34) (12.05m to mesh).
- day 2 18:23 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.57, 1.16, 58.91) — walking out to (-23.07, 1.12, 47.66) (12.00m to mesh).
- day 2 18:23 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.57, 1.16, 58.91) — walking out to (-23.07, 1.12, 47.66) (12.00m to mesh).
- day 2 18:29 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (0.43, 1.11, 54.46) — walking out to (-0.15, 1.19, 55.85) (12.28m to mesh).
- day 2 18:29 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (0.43, 1.11, 54.46) — walking out to (-0.15, 1.19, 55.85) (12.28m to mesh).
- day 2 19:06 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (3.69, 1.11, 55.71) runs through something solid - taking a clear way out to (3.12, 1.20, 54.32) instead.
- day 2 19:06 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (3.69, 1.11, 55.71) — walking out to (3.12, 1.20, 54.32) (12.00m to mesh).
- day 2 19:06 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (3.69, 1.11, 55.71) — walking out to (3.12, 1.20, 54.32) (12.00m to mesh).
- day 2 19:40 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.59, 1.11, 50.19).
- day 2 19:40 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.59, 1.11, 50.19).
- day 2 19:50 | **REPEATING** | x25 and counting: [AiNavigation] DS_FULL(Clone): the straight way out of (-13.85, 1.11, 47.49) runs through something solid - taking a clear way out to (-14.42, 1.22, 48.88) instead.
- day 2 20:00 | **REPEATING** | x25 and counting: [AiNavigation] DS_FULL(Clone): the straight way out of (-19.33, 1.11, 56.33) runs through something solid - taking a clear way out to (-20.72, 1.16, 56.33) instead.
- day 2 20:41 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-10.68, 1.11, 55.44) runs through something solid - taking a clear way out to (-11.25, 1.20, 54.06) instead.
- day 2 21:20 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (0.73, 1.15, 59.01) — walking out to (0.73, 1.20, 46.01) (12.00m to mesh).
- day 2 21:20 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (0.73, 1.15, 59.01) — walking out to (0.73, 1.20, 46.01) (12.00m to mesh).
- day 2 21:22 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (2.30, 1.19, 57.38) — walking out to (13.55, 0.04, 50.88) (12.00m to mesh).
- day 2 21:22 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (2.30, 1.19, 57.38) — walking out to (13.55, 0.04, 50.88) (12.00m to mesh).
- day 2 21:29 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (1.57, 1.16, 57.26) — walking out to (8.59, 1.21, 46.02) (12.26m to mesh).
- day 2 21:29 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (1.57, 1.16, 57.26) — walking out to (8.59, 1.21, 46.02) (12.26m to mesh).
- day 2 21:56 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (12.67, 1.11, 59.00) — walking out to (11.28, 1.16, 59.00) (12.00m to mesh).
- day 2 21:56 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (12.67, 1.11, 59.00) — walking out to (11.28, 1.16, 59.00) (12.00m to mesh).
- day 2 23:02 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-0.16, 1.19, 55.91) — walking out to (1.22, 1.18, 56.48) (12.00m to mesh).
- day 2 23:02 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-0.16, 1.19, 55.91) — walking out to (1.22, 1.18, 56.48) (12.00m to mesh).
- day 2 23:08 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-16.57, 1.16, 58.91) — walking out to (-23.07, 1.12, 47.65) (12.00m to mesh).
- day 2 23:08 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-16.57, 1.16, 58.91) — walking out to (-23.07, 1.12, 47.65) (12.00m to mesh).
- day 2 23:32 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-16.61, 1.16, 58.91) — walking out to (-23.11, 1.12, 47.65) (12.00m to mesh).
- day 2 23:32 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-16.61, 1.16, 58.91) — walking out to (-23.11, 1.12, 47.65) (12.00m to mesh).
- day 3 01:07 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-8.84, 1.14, 58.87) — walking out to (-8.84, 1.20, 45.87) (12.00m to mesh).
- day 3 01:07 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-8.84, 1.14, 58.87) — walking out to (-8.84, 1.20, 45.87) (12.00m to mesh).
- day 3 02:13 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2D: NO NAVMESH PATH. anchor=(-0.71, 1.14, 48.99) → driveTarget=(-0.71, 1.24, 48.99), from=(-1.93, 1.11, 48.74), elapsed=31.7s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=1.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 187.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 02:13 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2D: NO NAVMESH PATH. anchor=(-0.71, 1.14, 48.99) → driveTarget=(-0.71, 1.24, 48.99), from=(-1.93, 1.11, 48.74), elapsed=31.7s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=1.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 187.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 03:41 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (2.71, 1.19, 54.95) — walking out to (3.28, 1.18, 56.34) (12.00m to mesh).
- day 3 03:41 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (2.71, 1.19, 54.95) — walking out to (3.28, 1.18, 56.34) (12.00m to mesh).
- day 3 03:53 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2D: NO NAVMESH PATH. anchor=(-0.71, 1.14, 48.99) → driveTarget=(-0.71, 1.24, 48.99), from=(-4.58, 1.11, 48.78), elapsed=30.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=3.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 122.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 03:53 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2D: NO NAVMESH PATH. anchor=(-0.71, 1.14, 48.99) → driveTarget=(-0.71, 1.24, 48.99), from=(-4.58, 1.11, 48.78), elapsed=30.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=3.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 122.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 04:31 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (1.66, 1.19, 59.21) — walking out to (-11.34, 1.15, 59.21) (12.00m to mesh).
- day 3 04:31 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (1.66, 1.19, 59.21) — walking out to (-11.34, 1.15, 59.21) (12.00m to mesh).
- day 3 04:42 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-41.61, 1.11, 42.34).
- day 3 04:42 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-41.61, 1.11, 42.34).
- day 3 04:42 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-30.09, 1.11, 42.54) — walking out to (-41.61, 1.11, 42.34) (11.52m to mesh).
- day 3 04:42 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-30.09, 1.11, 42.54) — walking out to (-41.61, 1.11, 42.34) (11.52m to mesh).
- day 3 04:42 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 04:42 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 05:01 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-4.61, 1.13, 59.25) — walking out to (-4.61, 1.24, 46.25) (12.00m to mesh).
- day 3 05:01 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-4.61, 1.13, 59.25) — walking out to (-4.61, 1.24, 46.25) (12.00m to mesh).
- day 3 05:07 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-7.47, 1.14, 59.25) — walking out to (5.53, 1.18, 59.25) (12.00m to mesh).
- day 3 05:14 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-5.66, 1.11, 58.91) — walking out to (-4.60, 1.16, 59.00) (12.00m to mesh).
- day 3 05:14 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-5.66, 1.11, 58.91) — walking out to (-4.60, 1.16, 59.00) (12.00m to mesh).
- day 3 06:16 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (6.74, 1.22, 47.54) — walking out to (6.74, 0.04, 34.54) (12.00m to mesh).
- day 3 06:16 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (6.74, 1.22, 47.54) — walking out to (6.74, 0.04, 34.54) (12.00m to mesh).
- day 3 06:41 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-7.46, 1.11, 48.32) — walking out to (-8.03, 1.23, 49.71) (12.00m to mesh).
- day 3 06:41 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-7.46, 1.11, 48.32) — walking out to (-8.03, 1.23, 49.71) (12.00m to mesh).
- day 3 08:28 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (4.07, 1.19, 59.25) — walking out to (4.07, 1.24, 46.25) (12.00m to mesh).
- day 3 09:02 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (7.85, 1.11, 55.33) — walking out to (9.24, 1.19, 55.90) (12.00m to mesh).
- day 3 09:02 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (7.85, 1.11, 55.33) — walking out to (9.24, 1.19, 55.90) (12.00m to mesh).
- day 3 09:11 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-16.97, 1.11, 45.37) — walking out to (-17.54, 1.22, 46.75) (12.08m to mesh).
- day 3 09:11 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-16.97, 1.11, 45.37) — walking out to (-17.54, 1.22, 46.75) (12.08m to mesh).
- day 3 09:37 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-4.37, 1.13, 57.85) — walking out to (-4.37, 1.22, 44.85) (12.00m to mesh).
- day 3 09:37 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-4.37, 1.13, 57.85) — walking out to (-4.37, 1.22, 44.85) (12.00m to mesh).
- day 3 10:27 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (8.79, 1.11, 58.01) — walking out to (8.79, 1.16, 59.00) (12.00m to mesh).
- day 3 10:27 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (8.79, 1.11, 58.01) — walking out to (8.79, 1.16, 59.00) (12.00m to mesh).
- day 3 10:35 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (7.20, 1.17, 55.35) — walking out to (7.20, 1.23, 42.35) (12.00m to mesh).
- day 3 10:35 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (7.20, 1.17, 55.35) — walking out to (7.20, 1.23, 42.35) (12.00m to mesh).
- day 3 11:23 | **REPEATING** | x25 and counting: [AiNavigation] DS_FULL(Clone): stuck at (-5.23, 1.11, 57.92) — walking out to (-5.23, 1.16, 59.00) (12.00m to mesh).
- day 3 11:23 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-5.23, 1.11, 57.92) — walking out to (-5.23, 1.16, 59.00) (12.00m to mesh).
- day 3 11:23 | **REPEATING** | x25 and counting: [AiNavigation] DS_FULL(Clone): stuck at (-5.23, 1.11, 57.92) — walking out to (-5.23, 1.16, 59.00) (12.00m to mesh).
- day 3 11:23 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-5.23, 1.11, 57.92) — walking out to (-5.23, 1.16, 59.00) (12.00m to mesh).
- day 3 11:33 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-8.96, 1.15, 58.39) — walking out to (-8.96, 1.20, 45.39) (12.00m to mesh).
- day 3 11:33 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-8.96, 1.15, 58.39) — walking out to (-8.96, 1.20, 45.39) (12.00m to mesh).
- day 3 12:47 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (11.98, 1.11, 56.58) — walking out to (12.67, 1.16, 56.58) (12.00m to mesh).
- day 3 12:47 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (11.98, 1.11, 56.58) — walking out to (12.67, 1.16, 56.58) (12.00m to mesh).
- day 3 14:41 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-16.61, 1.11, 40.36) — walking out to (-17.19, 1.24, 41.75) (12.22m to mesh).
- day 3 14:41 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-16.61, 1.11, 40.36) — walking out to (-17.19, 1.24, 41.75) (12.22m to mesh).
- day 3 16:23 | **REPEATING** | x25 and counting: [AiNavigation] dock stocker DS_FULL(Clone): refused to drive from (5.83, 1.11, 51.87) to (-45.76, 1.11, 46.99) - the only route runs down aisle 02. Dock stockers stay out of the aisles; whatever is over there needs a lane the dock stockers can reach.
- day 3 17:33 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-6.22, 1.14, 59.11) — walking out to (0.28, 1.19, 47.85) (12.00m to mesh).
- day 3 17:46 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-4.38, 1.13, 58.27) — walking out to (-4.38, 1.23, 45.27) (12.00m to mesh).
- day 3 17:46 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-4.38, 1.13, 58.27) — walking out to (-4.38, 1.23, 45.27) (12.00m to mesh).
- day 3 18:11 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (2.43, 1.11, 52.37) — walking out to (1.86, 1.22, 50.99) (12.00m to mesh).
- day 3 18:11 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (2.43, 1.11, 52.37) — walking out to (1.86, 1.22, 50.99) (12.00m to mesh).
- day 3 19:10 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-6.90, 1.11, 58.56) — walking out to (-6.90, 1.25, 45.56) (12.00m to mesh).
- day 3 19:10 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-6.90, 1.11, 58.56) — walking out to (-6.90, 1.25, 45.56) (12.00m to mesh).
- day 3 20:18 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-6.02, 1.11, 53.61) — walking out to (-6.02, 1.24, 40.61) (12.00m to mesh).
- day 3 20:18 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-6.02, 1.11, 53.61) — walking out to (-6.02, 1.24, 40.61) (12.00m to mesh).
- day 3 20:42 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-7.83, 1.11, 56.50) — walking out to (-20.83, 1.16, 56.32) (12.00m to mesh).
- day 3 20:42 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-7.83, 1.11, 56.50) — walking out to (-20.83, 1.16, 56.32) (12.00m to mesh).
- day 3 23:55 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (7.59, 1.16, 59.25) — walking out to (7.59, 1.21, 46.25) (12.00m to mesh).
- day 3 23:55 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (7.59, 1.16, 59.25) — walking out to (7.59, 1.21, 46.25) (12.00m to mesh).
- day 4 00:00 | **TRUCK-STALLED** | TRUCK-STALLED Truck→Outbound_Door2 state=DepartToApproach 420s at (-2.0, 0.0, 27.9)
- day 4 00:00 | **TRUCK-STALLED** | TRUCK-STALLED Truck→Outbound_Door3 state=DepartToApproach 689s at (7.2, 0.0, 27.9)
- day 4 00:00 | **TRUCK-STALLED** | TRUCK-STALLED Truck→Outbound_Door2 state=DepartToApproach 420s at (-2.0, 0.0, 27.9)
- day 4 00:00 | **TRUCK-STALLED** | TRUCK-STALLED Truck→Outbound_Door3 state=DepartToApproach 689s at (7.2, 0.0, 27.9)
