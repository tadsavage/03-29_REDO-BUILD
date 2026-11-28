# Bug log - block 51

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:32 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-37.90, 1.11, 42.34).
- day 1 08:32 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-26.38, 1.11, 43.21) — walking out to (-37.90, 1.11, 42.34) (11.55m to mesh).
- day 1 09:13 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-6.01, 1.11, 48.70) — walking out to (-5.43, 1.23, 50.08) (12.00m to mesh).
- day 1 09:43 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-1.30, 1.12, 59.15) — walking out to (-1.30, 1.24, 46.15) (12.00m to mesh).
- day 1 10:29 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-44.53, 1.12, 50.35) — walking out to (-33.30, 1.12, 42.91) (12.47m to mesh).
- day 1 10:41 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-42.86, 1.11, 46.33) — walking out to (-43.43, 1.16, 47.72) (12.57m to mesh).
- day 1 10:48 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-2.95, 1.12, 59.25) — walking out to (-2.95, 1.24, 46.25) (12.00m to mesh).
- day 1 11:39 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-28.43, 1.11, 50.19).
- day 1 11:47 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 91/248.
- day 1 12:05 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (7.31, 1.12, 59.25) — walking out to (7.31, 1.21, 46.25) (12.00m to mesh).
- day 1 12:06 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (5.96, 1.12, 58.91) — walking out to (5.96, 1.22, 45.91) (12.00m to mesh).
- day 1 12:37 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (7.82, 1.11, 40.36) — walking out to (-3.44, 1.25, 46.86) (12.00m to mesh).
- day 1 13:14 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (0.72, 1.11, 53.62) — walking out to (0.14, 1.19, 55.01) (12.00m to mesh).
- day 1 14:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 1 14:54 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-23.08, 1.12, 41.81) — walking out to (-9.49, 1.25, 41.81) (12.60m to mesh).
- day 1 15:11 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-46.71, 1.11, 46.99) — walking out to (-48.00, 1.08, 46.99) (12.22m to mesh).
- day 1 15:36 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (2.97, 1.22, 50.61) — walking out to (3.54, 1.21, 52.00) (12.00m to mesh).
- day 1 15:36 | **ERROR** | "GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_remainingDistance ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__211:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2049)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 15:36 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__211:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2049)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 15:43 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-7.63, 1.12, 59.25) — walking out to (-7.63, 1.18, 46.25) (12.00m to mesh).
- day 1 16:12 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (7.77, 1.12, 59.25) — walking out to (7.77, 1.21, 46.25) (12.00m to mesh).
- day 1 16:43 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-20.87, 1.12, 42.67) — walking out to (-32.08, 1.08, 35.17) (12.65m to mesh).
- day 1 17:14 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 139/248.
- day 1 17:33 | **WARN** | [AiNavigation] DS_FULL(Clone) cannot reach task position at (-29.86, 1.16, 56.26) (path invalid) — abandoning the walk and handing the task back.
- day 1 17:45 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-3.24, 1.12, 59.25) — walking out to (-3.24, 1.24, 46.25) (12.00m to mesh).
- day 1 18:20 | **REPEATING** | x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- day 1 18:21 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-40.35, 1.11, 44.11) — walking out to (-40.35, 1.17, 45.50) (12.03m to mesh).
- day 1 18:33 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.59, 1.11, 50.19).
- day 1 18:44 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-9.07, 1.11, 56.26) — walking out to (-8.49, 1.17, 57.65) (12.00m to mesh).
- day 1 19:44 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-32.92, 1.12, 49.19) — walking out to (-39.42, 1.10, 38.59) (12.00m to mesh).
- day 1 21:28 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-21.85, 1.11, 50.19).
- day 1 21:28 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-33.37, 1.11, 51.16) — walking out to (-21.85, 1.11, 50.19) (11.56m to mesh).
- day 1 21:37 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (3.06, 1.12, 58.44) — walking out to (9.56, 1.21, 47.18) (12.00m to mesh).
- day 1 22:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 22 task(s) it could take wait
- day 1 22:43 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-18.83, 1.14, 57.23) — walking out to (-18.83, 1.08, 58.50) (12.03m to mesh).
- day 1 22:59 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 240/248.
- day 1 23:38 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (7.24, 1.12, 58.32) — walking out to (7.24, 1.21, 45.32) (12.00m to mesh).
- day 1 23:42 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (7.24, 1.12, 58.32) — walking out to (7.24, 1.21, 45.32) (12.00m to mesh).
- day 2 00:03 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 2 00:55 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-24.73, 1.12, 47.75) — walking out to (-24.73, 0.04, 34.46) (12.29m to mesh).
- day 2 01:39 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-23.84, 1.11, 50.19).
- day 2 01:39 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-35.36, 1.11, 51.16) — walking out to (-23.84, 1.11, 50.19) (11.56m to mesh).
- day 2 02:59 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-7.30, 1.12, 58.43) — walking out to (-0.80, 1.25, 47.18) (12.00m to mesh).
- day 2 03:09 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-5.88, 1.12, 59.05) — walking out to (-5.88, 1.20, 46.05) (12.00m to mesh).
- day 2 03:25 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-18.05, 1.11, 51.96) — walking out to (-16.67, 1.19, 52.53) (12.03m to mesh).
- day 2 03:54 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-3.87, 1.11, 56.41) runs through something solid - taking a clear way out to (-3.30, 1.17, 57.79) instead.
- day 2 05:00 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-24.66, 1.11, 50.19).
- day 2 05:31 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-35.35, 1.12, 47.23) — walking out to (-28.90, 0.04, 34.57) (13.21m to mesh).
- day 2 06:06 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-38.61, 1.12, 46.54) — walking out to (-45.09, 0.04, 34.58) (12.60m to mesh).
- day 2 06:35 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (6.28, 1.11, 59.00) — walking out to (6.28, 1.25, 46.00) (12.00m to mesh).
- day 2 09:15 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): stuck at (-12.05, 1.11, 51.31) — walking out to (-11.47, 1.21, 52.70) (12.00m to mesh).
- day 2 09:15 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-12.05, 1.11, 51.31) — walking out to (-11.47, 1.21, 52.70) (12.00m to mesh).
- day 2 09:55 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-12.58, 1.15, 51.84) — walking out to (-25.58, 1.12, 51.38) (12.01m to mesh).
- day 2 13:39 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-4.11, 1.13, 56.59) — walking out to (-4.11, 1.21, 43.59) (12.00m to mesh).
- day 2 15:00 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-40.35, 1.12, 51.50) — walking out to (-27.35, 1.12, 51.41) (12.00m to mesh).
- day 2 16:24 | **ERROR** | [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane 1B: NO NAVMESH PATH to (-11.31, 1.19, 39.19) from (-9.64, 1.11, 39.13) (elapsed 30.0s).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__103:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1222)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 16:24 | **ERROR** | [TrailerLoad] Could not reach lane 1B — leaving OutboundPallet_a99ac8a2-18bc-4ec5-861d-a92b572e82ae_1 staged.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<LoadOnePallet>d__96:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:832)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 17:26 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.67, 1.11, 50.19).
- day 2 17:27 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-27.47, 1.12, 49.87) — walking out to (-38.71, 1.12, 42.94) (12.21m to mesh).
- day 2 21:11 | **WARN** | [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (-17.59, 1.23, 42.34) (path invalid) — abandoning the walk and handing the task back.
- day 2 21:51 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-03 (loop in)' (9.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 22:07 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-38.35, 1.11, 53.65) — walking out to (-37.29, 1.18, 53.41) (12.04m to mesh).
- day 2 22:27 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-22.09, 1.11, 41.45) — walking out to (-9.10, 1.22, 40.81) (12.01m to mesh).
- day 2 23:14 | **WARN** | [AiNavigation] Employee_Shirley Edwards cannot reach task position at (5.91, 1.14, 41.69) (path invalid) — abandoning the walk and handing the task back.
- day 2 23:20 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-17.59, 1.12, 59.25) — walking out to (-11.09, 1.18, 47.99) (12.00m to mesh).
- day 2 23:33 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-03 (aisle entry)' (3.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 23:33 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 02-03' - driving straight to the stop instead.
- day 3 00:00 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 329s while 7 task(s) it could take wait
- day 3 00:00 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 329s while 10 task(s) it could take wait
