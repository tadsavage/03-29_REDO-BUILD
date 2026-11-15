# Bug log - block 55

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:12 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (12.88, 1.16, 36.88) — walking out to (-0.12, 1.11, 36.89) (12.00m to mesh).
- day 1 08:13 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.20, 1.22, 51.71) (12.00m to mesh).
- day 1 10:24 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-37.10, 1.11, 42.34).
- day 1 10:24 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-25.58, 1.11, 43.21) — walking out to (-37.10, 1.11, 42.34) (11.55m to mesh).
- day 1 10:59 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-17.09, 1.12, 59.25) — walking out to (-10.59, 1.18, 47.99) (12.00m to mesh).
- day 1 14:26 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 1 17:16 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (3.56, 1.12, 59.21) — walking out to (3.56, 1.21, 46.21) (12.00m to mesh).
- day 1 17:36 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-10.10, 1.12, 59.09) — walking out to (-10.10, 1.20, 46.09) (12.00m to mesh).
- day 1 18:27 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-6.45, 1.11, 54.36) — walking out to (-6.45, 1.25, 41.36) (12.00m to mesh).
- day 1 19:30 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (7.15, 1.11, 59.00) — walking out to (7.15, 1.25, 46.00) (12.00m to mesh).
- day 2 00:24 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.25, 1.14, 48.20) — walking out to (-16.25, 0.04, 34.46) (12.74m to mesh).
- day 2 00:58 | **REPEATING** | x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- day 2 03:41 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-5.88, 1.12, 59.25) — walking out to (-5.88, 1.20, 46.25) (12.00m to mesh).
- day 2 04:00 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-10.24, 1.11, 46.04) — walking out to (-10.24, 1.25, 44.65) (12.02m to mesh).
- day 2 06:37 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-10.54, 1.13, 57.95) — walking out to (-21.78, 1.12, 50.88) (12.28m to mesh).
- day 2 06:51 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (11.21, 1.11, 50.94) — walking out to (12.67, 1.16, 50.94) (12.00m to mesh).
- day 2 08:51 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-39.41, 1.11, 46.79) — walking out to (-28.17, 1.18, 53.95) (12.32m to mesh).
- day 2 09:09 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (4.97, 1.12, 58.92) — walking out to (4.97, 1.23, 45.92) (12.00m to mesh).
- day 2 11:00 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-18.98, 1.11, 47.66).
- day 2 11:00 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-30.50, 1.11, 46.79) — walking out to (-18.98, 1.11, 47.66) (11.55m to mesh).
- day 2 11:40 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (1.10, 1.16, 52.11) — walking out to (-6.05, 1.17, 40.87) (12.32m to mesh).
- day 2 11:46 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-2.98, 1.12, 59.25) — walking out to (-2.98, 1.24, 46.25) (12.00m to mesh).
- day 2 12:35 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-11.07, 1.11, 57.43) — walking out to (-10.50, 1.16, 58.81) (12.13m to mesh).
- day 2 12:59 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-09-01 anchor: NO NAVMESH PATH. anchor=(-31.19, 1.11, 40.71) → driveTarget=(-31.19, 1.18, 40.71), from=(-33.22, 1.17, 40.54), elapsed=32.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=2.0, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 189.4s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 13:28 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-0.89, 1.12, 58.43) — walking out to (-0.89, 1.23, 45.43) (12.00m to mesh).
- day 2 16:44 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-3.59, 1.11, 39.08) — walking out to (-3.02, 1.15, 37.70) (12.00m to mesh).
- day 2 16:51 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-40.81, 1.11, 42.34).
- day 2 16:51 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-38.06, 1.11, 43.21) — walking out to (-40.81, 1.11, 42.34) (2.89m to mesh).
- day 2 17:59 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-5.35, 1.15, 59.16) — walking out to (-5.35, 1.24, 46.16) (12.00m to mesh).
- day 2 18:07 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-41.35, 1.11, 45.16) runs through something solid - taking a clear way out to (-42.74, 1.16, 44.59) instead.
- day 2 20:16 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-38.87, 1.12, 47.57) — walking out to (-38.87, 0.04, 34.46) (12.11m to mesh).
- day 2 21:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 1 task(s) it could take wait
- day 2 21:59 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (1.10, 1.11, 38.20) — walking out to (0.52, 1.15, 39.58) (4.34m to mesh).
- day 2 23:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 12 task(s) it could take wait
- day 2 23:38 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-40.43, 1.11, 56.10) — walking out to (-39.37, 1.16, 56.33) (12.00m to mesh).
