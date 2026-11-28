# Bug log - block 42

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 09:06 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 1 09:38 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-33.86, 1.11, 46.91) — walking out to (-32.47, 1.19, 47.48) (12.26m to mesh).
- day 1 11:39 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (1.86, 1.12, 59.25) — walking out to (1.86, 1.17, 46.25) (12.00m to mesh).
- day 1 11:47 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (0.64, 1.12, 59.00) — walking out to (0.64, 1.20, 46.00) (12.00m to mesh).
- day 1 12:29 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-17.46, 1.11, 50.97) — walking out to (-18.05, 1.19, 52.03) (12.97m to mesh).
- day 1 15:12 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-10.63, 1.11, 53.72) — walking out to (-10.06, 1.19, 55.11) (12.01m to mesh).
- day 1 15:17 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-11.74, 1.12, 59.25) — walking out to (-11.74, 1.22, 46.25) (12.00m to mesh).
- day 1 15:23 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-11.72, 1.15, 52.60) — walking out to (-24.72, 1.12, 53.21) (12.01m to mesh).
- day 1 15:43 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-3.68, 1.12, 59.13) — walking out to (-3.68, 1.24, 46.13) (12.00m to mesh).
- day 1 16:08 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-13.71, 1.16, 51.32) — walking out to (-2.45, 1.22, 44.82) (12.00m to mesh).
- day 1 16:31 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 177/248.
- day 1 17:15 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-6.92, 1.14, 55.17) — walking out to (-0.42, 1.22, 43.91) (12.00m to mesh).
- day 1 17:35 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (7.44, 1.12, 58.76) — walking out to (7.44, 1.21, 45.76) (12.00m to mesh).
- day 1 17:35 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 1 17:45 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-8.96, 1.11, 57.97) — walking out to (-8.96, 1.16, 59.00) (12.43m to mesh).
- day 1 18:15 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 182/248.
- day 1 19:51 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (11.02, 1.11, 56.89) — walking out to (11.59, 1.17, 58.28) (12.00m to mesh).
- day 1 20:27 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 246/248.
- day 1 21:42 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-9.29, 1.16, 50.27) — walking out to (1.97, 1.17, 43.77) (12.00m to mesh).
- day 1 22:10 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack did not reach 'bay 02-05 (loop in)' ((-17.59, 1.23, 42.34)) after 0.1s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__211:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2151)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 23:51 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-10.11, 1.15, 59.18) — walking out to (-10.11, 1.17, 46.18) (12.00m to mesh).
- day 2 00:09 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (12.67, 1.16, 59.00) — walking out to (12.67, 1.16, 57.61) (12.00m to mesh).
- day 2 00:28 | **REPEATING** | x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- day 2 00:41 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-05 (loop in)' (13.0m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 00:42 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-3.86, 1.13, 59.25) — walking out to (-3.86, 1.24, 46.25) (12.00m to mesh).
- day 2 00:51 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 3A line-up' (15.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 01:21 | **WARN** | [OrderSelectionTaskDriver] Order 2371db5b-4ba1-4c6f-a5fb-dbdcbd441229 (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at 44/59.
- day 2 01:36 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (5.36, 1.12, 58.38) — walking out to (5.36, 1.22, 45.38) (12.00m to mesh).
- day 2 01:42 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-2.41, 1.11, 52.87) — walking out to (8.85, 1.24, 46.37) (12.00m to mesh).
- day 2 02:24 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-6.02, 1.14, 59.25) — walking out to (0.48, 1.18, 47.99) (12.00m to mesh).
- day 2 03:46 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (2.48, 1.11, 51.54) — walking out to (-8.78, 1.21, 45.04) (12.00m to mesh).
- day 2 04:45 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-4.73, 1.13, 58.55) — walking out to (-4.73, 1.23, 45.55) (12.00m to mesh).
- day 2 04:54 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-11.13, 1.15, 59.25) — walking out to (-4.63, 1.18, 47.99) (12.00m to mesh).
- day 2 05:09 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (9.27, 1.11, 52.29) — walking out to (9.84, 1.20, 53.67) (12.00m to mesh).
- day 2 05:47 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-2.75, 1.15, 55.09) — walking out to (-2.54, 1.18, 42.09) (12.00m to mesh).
- day 2 06:03 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-40.35, 1.11, 46.12) runs through something solid - taking a clear way out to (-40.35, 1.17, 44.73) instead.
- day 2 06:31 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-24.48, 1.11, 50.19).
- day 2 07:09 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-40.37, 1.12, 47.01) — walking out to (-33.91, 0.04, 34.58) (13.01m to mesh).
- day 2 07:38 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-8.62, 1.14, 59.18) — walking out to (-8.62, 1.19, 46.18) (12.00m to mesh).
- day 2 08:08 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (3.87, 1.14, 55.09) — walking out to (10.37, 1.21, 43.83) (12.00m to mesh).
- day 2 08:15 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-1.94, 1.11, 56.95) — walking out to (-2.51, 1.17, 58.34) (12.00m to mesh).
- day 2 08:18 | **WARN** | [OrderSelectionTaskDriver] Order b72defae-0fda-441b-9c33-e78561011633 (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at 46/48.
- day 2 08:36 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-14.24, 1.16, 58.91) — walking out to (-20.74, 1.12, 47.65) (12.00m to mesh).
- day 2 09:15 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-5.39, 1.11, 49.64) — walking out to (-4.82, 1.22, 51.02) (12.00m to mesh).
- day 2 09:32 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-10.07, 1.15, 53.53) — walking out to (-10.07, 1.11, 39.86) (12.67m to mesh).
- day 2 09:40 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): stuck at (-9.85, 1.11, 53.32) — walking out to (-9.28, 1.19, 54.70) (12.70m to mesh).
- day 2 10:39 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-9.19, 1.15, 58.34) — walking out to (-9.19, 1.18, 45.34) (12.00m to mesh).
- day 2 14:13 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (10.02, 1.11, 54.50) — walking out to (10.60, 1.19, 55.88) (12.00m to mesh).
- day 2 15:03 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-9.38, 1.15, 55.09) — walking out to (-2.88, 1.22, 43.83) (12.00m to mesh).
- day 2 15:06 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-10.89, 1.15, 58.48) — walking out to (-10.42, 1.16, 45.48) (12.01m to mesh).
- day 2 15:34 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-0.97, 1.13, 57.94) — walking out to (-0.97, 1.23, 44.94) (12.00m to mesh).
- day 2 17:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 359s while 6 task(s) it could take wait
- day 2 17:51 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 5.9m short of 'bay 02-01 (aisle entry)' - re-issuing the drive (6/6).
- day 2 17:58 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-01 (aisle entry)' (5.9m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 17:58 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 02-01' - driving straight to the stop instead.
- day 2 19:27 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-11.27, 1.15, 59.19) — walking out to (-11.29, 1.20, 46.54) (12.00m to mesh).
- day 2 19:39 | **WARN** | [OrderSelectionTaskDriver] Order a73ffce7-e0a7-4563-8ce5-522a69ed33da (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at 48/55.
- day 2 19:46 | **ERROR** | [MHEBoundary] 'PltJack_FULL(Clone)' drove OUT of the building at (-48.71, 1.11, 47.66) (cell (13, 86)). Putting it back on its last inside spot (-48.28, 1.11, 47.66). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 2 21:31 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-26.90, 1.11, 53.41) — walking out to (-25.51, 1.19, 53.41) (12.00m to mesh).
- day 2 21:35 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-12.47, 1.11, 57.66) — walking out to (-12.47, 1.16, 59.00) (12.26m to mesh).
- day 2 22:53 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-43.00, 1.12, 47.61) — walking out to (-43.00, 0.04, 34.46) (12.15m to mesh).
- day 2 23:34 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-30.34, 1.11, 46.79) — walking out to (-28.95, 1.20, 46.79) (12.32m to mesh).
