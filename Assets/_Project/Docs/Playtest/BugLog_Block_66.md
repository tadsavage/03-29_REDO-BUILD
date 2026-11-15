# Bug log - block 66

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:11 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.09, 1.21, 52.29) (12.00m to mesh).
- day 1 08:11 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (12.88, 1.16, 36.88) — walking out to (-0.12, 1.11, 36.89) (12.00m to mesh).
- day 1 08:22 | **WARN** | [AiNavigation] Employee_Kelly Harmon: stuck at (7.30, 1.15, 50.99) — walking out to (-3.96, 1.22, 44.49) (12.00m to mesh).
- day 1 09:47 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (3.15, 1.11, 48.06) — walking out to (3.72, 1.23, 49.45) (12.49m to mesh).
- day 1 10:58 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-10.41, 1.12, 37.95) — walking out to (-23.41, 1.10, 38.64) (12.02m to mesh).
- day 1 10:59 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B0 anchor: NO NAVMESH PATH. anchor=(-28.54, 5.11, 49.29) → driveTarget=(-28.54, 1.20, 49.29), from=(-26.16, 1.11, 49.53), elapsed=30.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=2.4, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 170.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 11:09 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-9.02, 1.19, 47.81) — walking out to (2.33, 1.17, 41.31) (12.08m to mesh).
- day 1 13:14 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-24.16, 1.11, 47.66).
- day 1 13:14 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-35.68, 1.11, 49.49) — walking out to (-24.16, 1.11, 47.66) (11.66m to mesh).
- day 1 13:14 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 13:32 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (2.32, 1.12, 59.25) — walking out to (2.32, 1.16, 46.25) (12.00m to mesh).
- day 1 14:35 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-1.76, 1.11, 48.06) — walking out to (-3.14, 1.24, 48.64) (12.21m to mesh).
- day 1 14:35 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 14:50 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-03-B1 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 40.71) → driveTarget=(-23.24, 1.21, 40.71), from=(-10.33, 1.11, 48.15), elapsed=30.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=15.0, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 168.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 15:26 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 105/248.
- day 1 16:26 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-04-B1 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 41.34) → driveTarget=(-23.24, 1.22, 41.34), from=(-5.24, 1.11, 48.58), elapsed=31.6s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=19.8, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 157.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:32 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (0.61, 1.11, 48.99) — walking out to (0.04, 1.23, 50.37) (12.56m to mesh).
- day 1 16:32 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 17:47 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-6.04, 1.11, 57.06) — walking out to (-4.65, 1.17, 57.64) (12.21m to mesh).
- day 1 17:47 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 17:48 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-13.47, 1.17, 49.04) — walking out to (-13.47, 1.09, 35.89) (12.00m to mesh).
- day 1 18:44 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-05-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 40.71) → driveTarget=(-24.56, 1.20, 40.71), from=(-20.51, 1.11, 42.26), elapsed=30.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=5.4, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 167.7s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 19:01 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 117/248.
- day 1 20:18 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 139/248.
- day 1 21:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 1 21:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 41 task(s) it could take wait
- day 1 23:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 1 23:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 41 task(s) it could take wait
- day 1 23:35 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-12-A0 anchor: NO NAVMESH PATH. anchor=(-25.89, 3.11, 49.29) → driveTarget=(-25.89, 1.21, 49.29), from=(-9.66, 1.11, 49.15), elapsed=30.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=16.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 234.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 00:03 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 139/248.
- day 2 00:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 00:38 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B0 anchor: NO NAVMESH PATH. anchor=(-28.54, 5.11, 49.29) → driveTarget=(-28.54, 1.20, 49.29), from=(-12.42, 1.11, 49.89), elapsed=32.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=16.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 185.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 02:02 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-05-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 40.71) → driveTarget=(-24.56, 1.20, 40.71), from=(-9.42, 1.23, 49.11), elapsed=32.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=17.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 374.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 02:11 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-5.59, 1.25, 44.41) — walking out to (-5.02, 1.25, 45.80) (12.00m to mesh).
- day 2 02:11 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 02:54 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 139/248.
- day 2 04:21 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 176/248.
- day 2 04:23 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-12-A0 anchor: NO NAVMESH PATH. anchor=(-25.89, 3.11, 49.29) → driveTarget=(-25.89, 1.21, 49.29), from=(-8.85, 1.23, 49.54), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=17.0, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 237.4s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 05:35 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 186/248.
- day 2 06:35 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-05-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 40.71) → driveTarget=(-24.56, 1.20, 40.71), from=(-20.42, 1.11, 43.07), elapsed=32.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=4.8, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 211.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 07:05 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-0.97, 1.12, 59.25) — walking out to (-7.47, 1.19, 47.99) (12.00m to mesh).
- day 2 07:06 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-15.23, 1.15, 40.04) — walking out to (-26.47, 1.12, 47.07) (12.26m to mesh).
- day 2 07:44 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-31.59, 1.11, 42.34).
- day 2 07:44 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-20.07, 1.11, 43.60) — walking out to (-31.59, 1.11, 42.34) (11.59m to mesh).
- day 2 07:44 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 09:20 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-05-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 40.71) → driveTarget=(-24.56, 1.20, 40.71), from=(-15.36, 1.24, 44.41), elapsed=32.3s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=9.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x00535] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1341 < ] 216.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 09:48 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-21.89, 1.12, 42.67) — walking out to (-33.11, 1.08, 35.17) (12.65m to mesh).
- day 2 09:57 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 186/248.
- day 2 10:56 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-05-B1 anchor: NO NAVMESH PATH. anchor=(-25.89, 5.11, 40.71) → driveTarget=(-25.89, 1.20, 40.71), from=(-14.46, 1.11, 46.87), elapsed=30.3s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=13.0, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 170.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 11:00 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-18.11, 1.14, 41.84) — walking out to (-29.36, 1.08, 35.17) (12.19m to mesh).
- day 2 11:00 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-17.39, 1.14, 42.40) — walking out to (-28.62, 1.08, 35.17) (12.49m to mesh).
- day 2 11:08 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 186/248.
- day 2 11:38 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-16.26, 1.11, 47.66).
- day 2 11:38 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-24.15, 1.11, 49.70) — walking out to (-16.26, 1.11, 47.66) (8.15m to mesh).
- day 2 11:38 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 12:33 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-14.99, 1.16, 41.31) — walking out to (-26.25, 1.08, 35.17) (12.00m to mesh).
- day 2 12:41 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-16.26, 1.11, 47.66).
- day 2 12:41 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.22, 1.11, 47.20) — walking out to (-16.26, 1.11, 47.66) (2.99m to mesh).
- day 2 12:41 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 13:12 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 186/248.
- day 2 13:14 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-11-A0 anchor: NO NAVMESH PATH. anchor=(-32.51, 3.11, 40.71) → driveTarget=(-32.51, 1.18, 40.71), from=(-16.37, 1.22, 47.64), elapsed=32.3s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=18.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 147.7s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 14:26 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-07-B0 anchor: NO NAVMESH PATH. anchor=(-27.21, 5.11, 40.71) → driveTarget=(-27.21, 1.19, 40.71), from=(-15.90, 1.11, 46.22), elapsed=31.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=12.6, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 202.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 15:47 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-11-A0 anchor: NO NAVMESH PATH. anchor=(-32.51, 3.11, 40.71) → driveTarget=(-32.51, 1.18, 40.71), from=(-20.51, 1.23, 45.39), elapsed=31.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=14.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 189.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 17:10 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-6.14, 1.12, 58.51) — walking out to (0.36, 1.20, 47.25) (12.00m to mesh).
- day 2 18:11 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-11.37, 1.25, 45.94) — walking out to (-11.06, 1.25, 44.55) (12.27m to mesh).
- day 2 18:11 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 19:37 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-7.96, 1.12, 59.06) — walking out to (-7.96, 1.20, 46.06) (12.00m to mesh).
- day 2 19:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 20:05 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 216/248.
- day 2 20:32 | **ERROR** | [GridNav] 'Employee_Larry Morgan': 'staging lane 3A line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:103)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 20:33 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-9.51, 1.16, 48.22) — walking out to (2.35, 1.16, 41.73) (12.52m to mesh).
- day 2 20:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 2 21:32 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-07-B0 anchor: NO NAVMESH PATH. anchor=(-27.21, 5.11, 40.71) → driveTarget=(-27.21, 1.19, 40.71), from=(-10.22, 1.11, 48.41), elapsed=31.7s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=18.7, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 750.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 23:55 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (0.55, 1.11, 56.78) — walking out to (-5.95, 1.25, 45.52) (12.00m to mesh).
- day 2 23:55 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
