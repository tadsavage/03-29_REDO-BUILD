# Bug log - block 47

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:33 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-1.76, 1.11, 52.10) — walking out to (-1.19, 1.22, 50.72) (12.00m to mesh).
- day 1 10:47 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-6.36, 1.12, 58.59) — walking out to (-12.86, 1.17, 47.34) (12.00m to mesh).
- day 1 10:50 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-3.47, 1.12, 59.12) — walking out to (-3.47, 1.24, 46.12) (12.00m to mesh).
- day 1 11:49 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-38.47, 1.11, 42.34).
- day 1 11:49 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-26.95, 1.11, 43.21) — walking out to (-38.47, 1.11, 42.34) (11.55m to mesh).
- day 1 12:53 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-12.51, 1.11, 59.00) — walking out to (-12.51, 1.25, 46.00) (12.00m to mesh).
- day 1 12:57 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-8.31, 1.12, 59.25) — walking out to (-8.31, 1.20, 46.25) (12.00m to mesh).
- day 1 13:09 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-8.77, 1.12, 59.08) — walking out to (-8.77, 1.20, 46.08) (12.00m to mesh).
- day 1 14:00 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (12.92, 1.12, 57.67) — walking out to (6.42, 1.22, 46.41) (12.00m to mesh).
- day 1 14:41 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-4.18, 1.12, 59.14) — walking out to (-4.18, 1.24, 46.14) (12.00m to mesh).
- day 1 14:53 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-18.05, 1.11, 51.96) — walking out to (-16.67, 1.20, 52.53) (12.03m to mesh).
- day 1 15:21 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (5.96, 1.12, 58.91) — walking out to (5.96, 1.22, 45.91) (12.00m to mesh).
- day 1 15:30 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-3.21, 1.11, 57.57) — walking out to (-3.21, 1.16, 59.00) (12.00m to mesh).
- day 1 16:51 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-12.52, 1.12, 59.19) — walking out to (-12.52, 1.19, 46.19) (12.00m to mesh).
- day 1 17:27 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (6.76, 1.12, 58.91) — walking out to (6.76, 1.21, 45.91) (12.00m to mesh).
- day 1 18:43 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-33.80, 1.12, 46.54) — walking out to (-40.28, 0.04, 34.58) (12.60m to mesh).
- day 1 19:09 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-5.83, 1.11, 52.27) — walking out to (-4.44, 1.21, 52.84) (12.29m to mesh).
- day 1 20:05 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-32.32, 1.12, 49.73) — walking out to (-21.07, 1.12, 42.95) (12.14m to mesh).
- day 1 20:14 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-37.06, 1.12, 47.66) — walking out to (-37.06, 0.04, 34.46) (12.20m to mesh).
- day 1 20:42 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-6.59, 1.11, 54.76) — walking out to (-6.59, 1.25, 41.76) (12.00m to mesh).
- day 1 22:31 | **REPEATING** | x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- day 1 23:09 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (4.90, 1.11, 57.73) — walking out to (4.90, 1.16, 59.00) (12.00m to mesh).
- day 2 01:03 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-9.68, 1.11, 44.98) — walking out to (-9.10, 1.25, 46.37) (12.00m to mesh).
- day 2 01:03 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-08 (aisle entry)' (8.4m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 01:03 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 01-08' - driving straight to the stop instead.
- day 2 01:26 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-10.34, 1.11, 40.61) — walking out to (-11.72, 1.23, 40.61) (12.00m to mesh).
- day 2 01:41 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- day 2 01:53 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-15.01, 1.12, 59.20) — walking out to (-8.51, 1.18, 47.94) (12.00m to mesh).
- day 2 02:07 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-08' (10.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 02:45 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (3.26, 1.11, 52.39) — walking out to (-3.24, 1.25, 41.13) (12.00m to mesh).
- day 2 02:51 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (3.20, 1.12, 58.46) — walking out to (3.20, 1.18, 45.46) (12.00m to mesh).
- day 2 03:52 | **WARN** | [OrderSelectionTaskDriver] Order e0e75bca-1c45-42bd-ae3e-8518cfeb877a (Mama Tomato Sauces) has no reachable pick location for its remaining line item(s) — staging short at 54/57.
- day 2 03:56 | **WARN** | [OrderSelectionTaskDriver] Order 2371db5b-4ba1-4c6f-a5fb-dbdcbd441229 (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at 34/59.
- day 2 04:24 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (8.31, 1.12, 58.64) — walking out to (8.31, 1.21, 45.64) (12.00m to mesh).
- day 2 07:07 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-32.73, 1.17, 40.25), elapsed=31.3s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=32.7, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 392.4s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 07:36 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 2 08:24 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-39.87, 1.12, 48.01) — walking out to (-39.87, 0.04, 34.46) (12.54m to mesh).
- day 2 08:36 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-39.16, 1.11, 46.79) — walking out to (-27.93, 1.18, 53.95) (12.32m to mesh).
- day 2 09:42 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-6.69, 1.11, 44.77), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=4.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 350.8s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 09:45 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (2.88, 1.11, 50.16) — walking out to (2.31, 1.22, 51.55) (12.00m to mesh).
- day 2 09:53 | **WARN** | [OrderSelectionTaskDriver] Order b72defae-0fda-441b-9c33-e78561011633 (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at 40/48.
- day 2 10:03 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (4.58, 1.14, 54.15) — walking out to (3.69, 1.18, 41.15) (12.03m to mesh).
- day 2 10:20 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack did not reach 'bay 01-02 (aisle entry)' ((-16.26, 1.24, 42.34)) after 8.8s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__211:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2151)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 10:20 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 01-02' - driving straight to the stop instead.
- day 2 10:21 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-0.20, 1.12, 59.09) — walking out to (-0.20, 1.24, 46.09) (12.00m to mesh).
- day 2 11:48 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (2.18, 1.15, 58.95) — walking out to (12.92, 1.12, 52.45) (12.00m to mesh).
- day 2 11:50 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (3.87, 1.16, 55.09) — walking out to (3.65, 1.12, 42.09) (12.00m to mesh).
- day 2 11:55 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (6.62, 1.11, 54.42) — walking out to (0.12, 1.25, 43.16) (12.00m to mesh).
- day 2 11:57 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (2.18, 1.15, 58.99) — walking out to (12.92, 1.12, 52.49) (12.00m to mesh).
- day 2 12:14 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 21.3m short of 'bay 01-01 (aisle entry)' - re-issuing the drive (2/6).
- day 2 13:32 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (0.35, 1.13, 58.14) — walking out to (0.35, 1.23, 45.14) (12.00m to mesh).
- day 2 13:34 | **WARN** | [OrderSelectionTaskDriver] Order e80b74d2-49e2-45ab-952f-7503ec4d9e76 (Neon Nectar Juice Co.) has no reachable pick location for its remaining line item(s) — staging short at 11/45.
- day 2 13:39 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-20.46, 1.13, 42.67) — walking out to (-31.67, 1.08, 35.17) (12.65m to mesh).
- day 2 13:58 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-5.38, 1.11, 58.20) runs through something solid - taking a clear way out to (-5.38, 1.16, 59.00) instead.
- day 2 13:58 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-5.38, 1.11, 58.20) — walking out to (-5.38, 1.16, 59.00) (12.00m to mesh).
- day 2 13:59 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-2.75, 1.15, 55.26) — walking out to (-2.75, 1.20, 42.51) (12.00m to mesh).
- day 2 19:18 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (0.72, 1.20, 47.67) — walking out to (0.72, 1.08, 35.17) (12.00m to mesh).
- day 2 19:45 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-4.69, 1.11, 53.96) — walking out to (-5.26, 1.19, 55.34) (12.00m to mesh).
- day 2 21:03 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (7.24, 1.19, 57.69) — walking out to (-5.76, 1.14, 57.69) (12.00m to mesh).
- day 2 21:06 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (0.72, 1.13, 59.01) — walking out to (0.72, 1.20, 46.01) (12.00m to mesh).
- day 2 21:10 | **DOOR-DEADLOCK** | OG3672 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2A: [1: own/Staged/y1.2] 
- day 2 21:10 | **DOOR-DEADLOCK** | OG3675 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2B: [1: own/Staged/y1.2] 
- day 2 22:10 | **DOOR-DEADLOCK** | OG3678 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2C: [1: own/Staged/y1.2] 
- day 2 22:18 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-12.26, 1.16, 59.00) — walking out to (-13.64, 1.16, 59.00) (12.00m to mesh).
- day 2 22:23 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-37.80, 1.12, 48.39) — walking out to (-37.80, 0.04, 34.46) (12.92m to mesh).
- day 2 22:23 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-32.93, 1.11, 46.79) — walking out to (-31.54, 1.19, 46.79) (12.32m to mesh).
- day 2 23:20 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-35.48, 1.12, 46.58) — walking out to (-41.95, 0.04, 34.58) (12.64m to mesh).
- day 2 23:54 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.59, 1.11, 50.19).
- day 2 23:54 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-27.36, 1.11, 51.16) — walking out to (-17.59, 1.11, 50.19) (9.82m to mesh).
