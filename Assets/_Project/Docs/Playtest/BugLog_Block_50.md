# Bug log - block 50

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:59 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-13.96, 1.11, 54.49) — walking out to (-7.46, 1.25, 43.23) (12.00m to mesh).
- day 1 09:23 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-0.88, 1.11, 53.63) — walking out to (-1.45, 1.19, 55.01) (12.00m to mesh).
- day 1 09:31 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-6.53, 1.12, 58.46) — walking out to (-13.03, 1.16, 47.20) (12.00m to mesh).
- day 1 12:16 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (3.21, 1.12, 59.22) — walking out to (3.21, 1.18, 46.22) (12.00m to mesh).
- day 1 12:50 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-1.22, 1.12, 59.25) — walking out to (-1.22, 1.24, 46.25) (12.00m to mesh).
- day 1 13:03 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.59, 1.11, 50.19).
- day 1 13:03 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-23.84, 1.11, 49.80) — walking out to (-17.59, 1.11, 50.19) (6.27m to mesh).
- day 1 14:12 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (4.14, 1.12, 58.88) — walking out to (4.14, 1.23, 45.88) (12.00m to mesh).
- day 1 14:24 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (4.56, 1.11, 55.44) — walking out to (4.56, 1.25, 42.44) (12.00m to mesh).
- day 1 15:15 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (0.61, 1.11, 56.31) — walking out to (-5.89, 1.25, 45.05) (12.00m to mesh).
- day 1 15:43 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-35.67, 1.12, 49.93) — walking out to (-24.42, 1.12, 42.93) (12.24m to mesh).
- day 1 16:00 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-11.70, 1.12, 58.94) — walking out to (-11.70, 1.22, 45.94) (12.00m to mesh).
- day 1 16:02 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-11.68, 1.12, 58.97) — walking out to (-11.68, 1.22, 45.97) (12.00m to mesh).
- day 1 16:33 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 227/248.
- day 1 17:23 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-40.35, 1.11, 45.89) — walking out to (-40.35, 1.17, 44.51) (12.03m to mesh).
- day 1 18:08 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-16.16, 1.12, 58.91) — walking out to (-9.66, 1.21, 47.65) (12.00m to mesh).
- day 1 18:19 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 243/248.
- day 1 19:21 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02 (loop in) (route)' from (-33.28, 1.11, 48.46) to (-17.59, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 19:21 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02 (loop in)' from (-33.28, 1.11, 48.46) to (-42.14, 1.11, 50.19) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 19:21 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02 (aisle entry)' from (-33.28, 1.11, 48.46) to (-16.26, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 19:21 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-02' - driving straight to the stop instead.
- day 1 19:22 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02' from (-33.28, 1.11, 48.46) to (-23.91, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 19:53 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-36.01, 1.12, 46.54) — walking out to (-42.48, 0.04, 34.58) (12.60m to mesh).
- day 1 20:03 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 243/248.
- day 1 20:15 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-1.35, 1.11, 51.51) — walking out to (-7.85, 1.22, 40.25) (12.00m to mesh).
- day 1 20:21 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-8.98, 1.12, 59.24) — walking out to (-8.98, 1.20, 46.24) (12.00m to mesh).
- day 1 20:29 | **REPEATING** | x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- day 1 20:42 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-22.17, 1.11, 50.19).
- day 1 20:42 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-33.69, 1.11, 51.16) — walking out to (-22.17, 1.11, 50.19) (11.56m to mesh).
- day 1 20:54 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-34.50, 1.12, 51.41) — walking out to (-34.50, 1.10, 38.59) (12.00m to mesh).
- day 1 21:00 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-36.46, 1.12, 48.34) — walking out to (-36.46, 0.04, 34.46) (12.88m to mesh).
- day 1 21:06 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 243/248.
- day 1 21:07 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02 (loop in) (route)' from (-32.54, 1.11, 48.32) to (-17.59, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 21:07 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02 (loop in)' from (-32.54, 1.11, 48.32) to (-42.14, 1.11, 50.19) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 21:07 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02 (aisle entry)' from (-32.54, 1.11, 48.32) to (-16.26, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 21:08 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-02' from (-32.54, 1.11, 48.32) to (-23.91, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 23:17 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.59, 1.11, 50.19).
- day 1 23:17 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-26.68, 1.11, 51.16) — walking out to (-17.59, 1.11, 50.19) (9.14m to mesh).
- day 1 23:27 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'staging lane 1B wait (route)' from (-24.16, 1.11, 47.28) to (-42.14, 1.11, 50.19) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 23:27 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'staging lane 1B wait' from (-24.16, 1.11, 47.28) to (-11.31, 1.11, 55.33) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 23:39 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-26.00, 1.12, 48.82) — walking out to (-26.00, 0.04, 34.46) (13.35m to mesh).
- day 2 00:12 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-01 (loop in) (route)' from (-24.16, 1.11, 47.28) to (-17.59, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 00:12 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-01 (loop in)' from (-24.16, 1.11, 47.28) to (-42.14, 1.11, 50.19) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 00:22 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-4.80, 1.12, 59.17) — walking out to (-4.80, 1.24, 46.17) (12.00m to mesh).
- day 2 00:28 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-01 (aisle entry)' from (-24.04, 1.11, 47.17) to (-16.26, 1.11, 39.81) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 00:28 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- day 2 00:28 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'bay 01-01' from (-24.04, 1.11, 47.17) to (-21.26, 1.11, 39.81) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 00:30 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-7.23, 1.12, 59.17) — walking out to (-13.73, 1.16, 47.91) (12.00m to mesh).
- day 2 00:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 21 task(s) it could take wait
- day 2 01:42 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-5.94, 1.11, 58.17) runs through something solid - taking a clear way out to (-6.52, 1.16, 59.00) instead.
- day 2 01:42 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-5.94, 1.11, 58.17) — walking out to (-6.52, 1.16, 59.00) (12.00m to mesh).
- day 2 02:12 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-25.85, 1.12, 48.23) — walking out to (-25.85, 0.04, 34.46) (12.77m to mesh).
- day 2 02:36 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.59, 1.11, 50.19).
- day 2 03:21 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-18.05, 1.11, 52.76) — walking out to (-16.67, 1.19, 53.33) (12.02m to mesh).
- day 2 04:35 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-14.32, 1.12, 59.25) — walking out to (-7.82, 1.20, 47.99) (12.00m to mesh).
- day 2 04:45 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-9.54, 1.11, 57.59) — walking out to (-9.54, 1.16, 59.00) (12.22m to mesh).
- day 2 05:17 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-26.35, 1.11, 50.19).
- day 2 06:21 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-2.92, 1.11, 56.26) — walking out to (-2.34, 1.17, 57.65) (12.00m to mesh).
- day 2 07:16 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-7.59, 1.11, 51.30) — walking out to (-8.16, 1.23, 49.92) (12.07m to mesh).
- day 2 09:03 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-45.76, 1.11, 44.90) — walking out to (-44.38, 1.14, 45.47) (12.12m to mesh).
- day 2 09:42 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (3.87, 1.14, 55.09) — walking out to (10.37, 1.21, 43.83) (12.00m to mesh).
- day 2 10:09 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (3.87, 1.14, 55.25) — walking out to (10.37, 1.21, 43.99) (12.00m to mesh).
- day 2 12:02 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-42.28, 1.12, 46.90) — walking out to (-35.81, 0.04, 34.58) (12.92m to mesh).
- day 2 12:20 | **WARN** | [OrderSelectionTaskDriver] Order b72defae-0fda-441b-9c33-e78561011633 (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at 46/48.
- day 2 14:04 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-37.64, 1.11, 46.79) — walking out to (-36.25, 1.18, 46.79) (12.32m to mesh).
- day 2 14:09 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (6.79, 1.12, 58.91) — walking out to (6.79, 1.21, 45.91) (12.00m to mesh).
- day 2 14:27 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-25.45, 1.11, 50.19).
- day 2 16:12 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-0.77, 1.11, 52.24) — walking out to (10.48, 1.25, 45.74) (12.00m to mesh).
- day 2 16:42 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-26.41, 1.11, 50.19).
- day 2 17:15 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-18.05, 1.11, 51.71) — walking out to (-11.03, 1.23, 40.47) (12.25m to mesh).
- day 2 18:38 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (3.87, 1.14, 55.09) — walking out to (10.37, 1.21, 43.83) (12.00m to mesh).
- day 2 18:42 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (7.11, 1.21, 45.38) — walking out to (7.11, 1.12, 58.38) (12.00m to mesh).
- day 2 22:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 4 task(s) it could take wait
- day 2 23:42 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-25.86, 1.11, 50.19).
- day 2 23:42 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-37.38, 1.11, 51.16) — walking out to (-25.86, 1.11, 50.19) (11.56m to mesh).
- day 2 23:55 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-40.37, 1.12, 51.02) — walking out to (-46.87, 1.08, 39.76) (12.00m to mesh).
