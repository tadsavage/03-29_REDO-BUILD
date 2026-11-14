# Bug log - block 41

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 09:04 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 1 09:12 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (8.87, 1.11, 57.95) — walking out to (8.87, 1.16, 59.00) (12.00m to mesh).
- day 1 09:40 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-4.63, 1.12, 59.13) — walking out to (-4.63, 1.24, 46.13) (12.00m to mesh).
- day 1 09:42 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-4.62, 1.12, 59.15) — walking out to (-4.62, 1.24, 46.15) (12.00m to mesh).
- day 1 09:42 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-2.10, 1.12, 59.15) — walking out to (-2.10, 1.24, 46.15) (12.00m to mesh).
- day 1 10:44 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-27.21, 1.17, 50.19).
- day 1 10:44 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-38.73, 1.17, 49.94) — walking out to (-27.21, 1.17, 50.19) (11.52m to mesh).
- day 1 10:44 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 10:59 | **WARN** | [AiNavigation] Employee_Douglas Garcia cannot reach task position at (6.36, 1.22, 41.66) (path invalid) — abandoning the walk and handing the task back.
- day 1 11:03 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-5.97, 1.17, 41.00) — walking out to (5.29, 1.22, 47.50) (12.00m to mesh).
- day 1 12:05 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-14.39, 1.16, 43.01) — walking out to (-25.59, 1.08, 35.17) (12.85m to mesh).
- day 1 12:07 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet fc25b792-59fc-4c49-b10c-ebb33812876c but the employee is 30.2m away (max 4.5m) — refusing to start receiving.
- day 1 13:17 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-44.44, 1.11, 45.66) — walking out to (-43.05, 1.16, 46.24) (12.05m to mesh).
- day 1 13:28 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-14.07, 1.12, 59.25) — walking out to (-7.57, 1.20, 47.99) (12.00m to mesh).
- day 1 13:58 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-12.74, 1.12, 58.80) — walking out to (-12.74, 1.20, 45.80) (12.00m to mesh).
- day 1 14:08 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-30.81, 1.22, 42.34).
- day 1 14:08 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.29, 1.22, 40.83) — walking out to (-30.81, 1.22, 42.34) (11.62m to mesh).
- day 1 14:08 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 14:26 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.61, 1.12, 58.91) — walking out to (-10.11, 1.19, 47.81) (12.00m to mesh).
- day 1 14:37 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-8.32, 1.12, 59.23) — walking out to (-8.32, 1.20, 46.23) (12.00m to mesh).
- day 1 14:43 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-2.32, 1.12, 59.25) — walking out to (-2.32, 1.24, 46.25) (12.00m to mesh).
- day 1 15:23 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-31.41, 1.11, 56.25) — walking out to (-30.35, 1.16, 56.33) (13.04m to mesh).
- day 1 15:53 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-2.90, 1.12, 59.25) — walking out to (3.60, 1.19, 47.99) (12.00m to mesh).
- day 1 15:59 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-16.62, 1.12, 58.91) — walking out to (-10.12, 1.19, 47.81) (12.00m to mesh).
- day 1 17:16 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-12.78, 1.12, 59.25) — walking out to (-6.28, 1.18, 47.99) (12.00m to mesh).
- day 1 17:47 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-16.60, 1.12, 58.91) — walking out to (-10.10, 1.19, 47.81) (12.00m to mesh).
- day 1 18:11 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (5.85, 1.11, 55.43) — walking out to (7.24, 1.18, 56.01) (12.17m to mesh).
- day 1 18:18 | **REPEATING** | x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- day 1 18:20 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (7.27, 1.12, 58.91) — walking out to (7.27, 1.21, 45.91) (12.00m to mesh).
- day 1 18:38 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (6.66, 1.12, 59.21) — walking out to (6.75, 1.22, 46.21) (12.00m to mesh).
- day 1 19:44 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-16.66, 1.12, 58.90) — walking out to (-10.16, 1.19, 47.81) (12.00m to mesh).
- day 1 19:51 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-11.19, 1.12, 59.09) — walking out to (-11.19, 1.21, 46.09) (12.00m to mesh).
- day 1 20:40 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-1.02, 1.11, 54.13) — walking out to (-0.44, 1.19, 55.52) (12.00m to mesh).
- day 1 21:08 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (2.07, 1.12, 59.25) — walking out to (2.07, 1.16, 46.25) (12.00m to mesh).
- day 1 21:29 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-10.78, 1.11, 38.82) — walking out to (-10.21, 1.22, 40.21) (12.31m to mesh).
- day 1 21:29 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 21:41 | **ERROR** | [GridNav] 'Employee_Larry Morgan': 'bay 02-04 (loop in)' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 22:23 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-1.20, 1.12, 59.25) — walking out to (-1.20, 1.24, 46.25) (12.00m to mesh).
- day 1 22:55 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-11.30, 1.12, 59.25) — walking out to (-11.30, 1.22, 46.25) (12.00m to mesh).
- day 1 23:38 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (4.47, 1.20, 54.09) — walking out to (5.86, 1.19, 54.66) (12.13m to mesh).
- day 1 23:39 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (3.52, 1.12, 59.19) — walking out to (3.52, 1.21, 46.19) (12.00m to mesh).
- day 1 23:39 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-1.48, 1.12, 59.03) — walking out to (-1.48, 1.24, 46.03) (12.00m to mesh).
- day 2 00:17 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (7.07, 1.12, 58.91) — walking out to (7.07, 1.21, 45.91) (12.00m to mesh).
- day 2 00:17 | **ERROR** | [GridNav] 'Employee_Larry Morgan': 'bay 02-04 (aisle entry) (route)' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 01:07 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-45.76, 1.11, 41.69) — walking out to (-44.38, 1.15, 42.26) (12.00m to mesh).
- day 2 01:24 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (1.38, 1.12, 58.72) — walking out to (1.38, 1.20, 45.72) (12.00m to mesh).
- day 2 01:28 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-3.62, 1.12, 58.77) — walking out to (-4.23, 1.23, 45.77) (12.01m to mesh).
- day 2 01:37 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (1.02, 1.11, 53.18) — walking out to (2.41, 1.20, 53.75) (12.57m to mesh).
- day 2 02:28 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-8.85, 1.12, 58.60) — walking out to (-8.85, 1.20, 45.60) (12.00m to mesh).
- day 2 02:30 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-13.81, 1.12, 59.19) — walking out to (-7.31, 1.18, 47.93) (12.00m to mesh).
- day 2 03:20 | **ERROR** | [GridNav] 'Employee_Larry Morgan': 'bay 02-04 (aisle entry)' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 03:20 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 02-04' - driving straight to the stop instead.
- day 2 03:31 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-10.07, 1.12, 58.86) — walking out to (-10.07, 1.21, 46.49) (12.00m to mesh).
- day 2 03:46 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (4.13, 1.14, 55.24) — walking out to (-2.37, 1.22, 43.98) (12.00m to mesh).
- day 2 03:46 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-9.62, 1.11, 55.95) — walking out to (-8.23, 1.18, 56.52) (12.00m to mesh).
- day 2 04:37 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-5.08, 1.13, 57.93) — walking out to (-5.08, 1.23, 44.93) (12.00m to mesh).
- day 2 04:57 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (1.57, 1.13, 57.86) — walking out to (-4.93, 1.24, 46.60) (12.00m to mesh).
- day 2 05:01 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (1.57, 1.13, 57.89) — walking out to (-4.93, 1.24, 46.63) (12.00m to mesh).
- day 2 05:05 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-10.42, 1.11, 55.51) — walking out to (-10.99, 1.18, 56.90) (12.00m to mesh).
- day 2 05:58 | **ERROR** | [GridNav] 'Employee_Larry Morgan': 'bay 02-04 (route)' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 06:12 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-6.91, 1.12, 59.17) — walking out to (-19.91, 0.04, 58.81) (12.00m to mesh).
- day 2 07:16 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 2 08:46 | **ERROR** | [GridNav] 'Employee_Larry Morgan': 'bay 02-04' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 09:23 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-18.39, 1.11, 53.41) — walking out to (-18.96, 1.17, 54.80) (12.00m to mesh).
- day 2 11:53 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-17.59, 1.11, 50.19).
- day 2 11:53 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-23.18, 1.11, 51.16) — walking out to (-17.59, 1.11, 50.19) (5.68m to mesh).
- day 2 11:59 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-2.63, 1.11, 54.95) — walking out to (-2.06, 1.18, 56.33) (12.00m to mesh).
- day 2 12:37 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-15.53, 1.12, 59.25) — walking out to (-9.03, 1.18, 47.99) (12.00m to mesh).
- day 2 12:48 | **ERROR** | [GridNav] 'Employee_Larry Morgan': no route for 'staging lane 2C line-up (route)' from (-30.35, 1.11, 47.89) to (-42.14, 1.11, 50.19) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 12:48 | **ERROR** | [GridNav] 'Employee_Larry Morgan': no route for 'staging lane 2C line-up' from (-30.35, 1.11, 47.89) to (-2.04, 1.11, 51.33) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 14:17 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-11.06, 1.11, 45.39) — walking out to (-10.49, 1.25, 46.77) (12.08m to mesh).
- day 2 15:39 | **WARN** | [OrderSelectionTaskDriver] Order b2d1889b-5bb8-4d46-bc45-378e4c29fb2b (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at 996/1056.
- day 2 16:35 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (9.89, 1.11, 55.00) — walking out to (9.31, 1.20, 53.61) (12.00m to mesh).
- day 2 17:02 | **REPEATING** | x25 and counting: [AiNavigation] DS_FULL(Clone): the straight way out of (5.82, 1.11, 48.32) runs through something solid - taking a clear way out to (6.40, 1.23, 49.70) instead.
- day 2 17:02 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (5.82, 1.11, 48.32) — walking out to (6.40, 1.23, 49.70) (12.01m to mesh).
- day 2 17:09 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 224/248.
- day 2 17:42 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-38.87, 1.11, 49.06) — walking out to (-25.87, 1.21, 49.06) (12.00m to mesh).
- day 2 17:54 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-38.39, 1.12, 50.79) — walking out to (-27.18, 1.12, 42.89) (12.72m to mesh).
- day 2 18:02 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (3.83, 1.11, 45.05) — walking out to (4.41, 1.25, 46.44) (12.00m to mesh).
- day 2 18:13 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-40.19, 1.12, 49.22) — walking out to (-46.69, 1.08, 37.96) (12.00m to mesh).
- day 2 19:14 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 225/248.
- day 2 22:23 | **WARN** | [OrderService] Order OG2696 closed with a IN TRANSIT pallet (50 cs) that never made the trailer - not billed, returning it to stock.
- day 2 22:33 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-3.33, 1.11, 55.89) — walking out to (-3.90, 1.20, 54.50) (12.00m to mesh).
- day 2 23:18 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-23.95, 1.11, 56.33) — walking out to (-22.56, 1.16, 56.33) (13.12m to mesh).
- day 3 00:42 | **WARN** | [OrderSelectionTaskDriver] Order 2371db5b-4ba1-4c6f-a5fb-dbdcbd441229 (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at 49/59.
- day 3 03:22 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-9.49, 1.15, 51.97) — walking out to (-22.49, 1.12, 51.37) (12.01m to mesh).
- day 3 04:06 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (3.22, 1.12, 58.29) — walking out to (9.72, 1.20, 47.03) (12.00m to mesh).
- day 3 04:21 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-42.03, 1.12, 49.38) — walking out to (-48.33, 1.08, 38.12) (12.00m to mesh).
- day 3 07:14 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (12.54, 1.11, 52.06) — walking out to (12.67, 1.16, 53.12) (12.00m to mesh).
- day 3 08:54 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-37.52, 1.12, 51.29) — walking out to (-48.33, 1.08, 44.79) (12.00m to mesh).
- day 3 08:57 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-40.36, 1.11, 45.90) runs through something solid - taking a clear way out to (-40.35, 1.17, 44.52) instead.
- day 3 08:57 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-40.36, 1.11, 45.90) — walking out to (-40.35, 1.17, 44.52) (12.03m to mesh).
- day 3 09:56 | **WARN** | [OrderSelectionTaskDriver] Order e0e75bca-1c45-42bd-ae3e-8518cfeb877a (Mama Tomato Sauces) has no reachable pick location for its remaining line item(s) — staging short at 35/57.
- day 3 10:30 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-0.60, 1.13, 57.97) — walking out to (-0.60, 1.23, 44.97) (12.00m to mesh).
- day 3 10:45 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-3.22, 1.14, 53.96) — walking out to (-10.00, 1.24, 42.71) (12.14m to mesh).
- day 3 11:25 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (8.42, 1.12, 58.90) — walking out to (8.42, 1.21, 45.90) (12.00m to mesh).
- day 3 11:30 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (7.24, 1.13, 57.70) — walking out to (7.24, 1.21, 44.70) (12.00m to mesh).
- day 3 11:35 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (8.43, 1.12, 58.91) — walking out to (8.43, 1.21, 45.91) (12.00m to mesh).
- day 3 15:31 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-22.91, 1.11, 46.79) — walking out to (-21.52, 1.22, 46.79) (12.32m to mesh).
- day 3 16:10 | **DOOR-DEADLOCK** | OG3675 is Staged but door 3's truck has waited 90 min with no one loading it. | lane 2C: [1: own/Staged/y1.2] 
- day 3 17:07 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-2.08, 1.13, 58.17) — walking out to (-2.08, 1.23, 45.17) (12.00m to mesh).
- day 3 17:07 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (5.71, 1.11, 52.42) — walking out to (-0.79, 1.25, 41.16) (12.00m to mesh).
- day 3 17:21 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-7.70, 1.15, 52.72) — walking out to (-20.70, 1.12, 53.20) (12.01m to mesh).
- day 3 17:29 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-8.15, 1.14, 54.39) — walking out to (-7.81, 1.20, 41.39) (12.00m to mesh).
- day 3 17:56 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-37.66, 1.11, 46.79) — walking out to (-36.27, 1.18, 46.79) (12.32m to mesh).
- day 3 19:28 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (-45.76, 1.11, 44.34) (path invalid) — abandoning the walk and handing the task back.
- day 3 19:54 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-7.72, 1.13, 56.55) — walking out to (-1.22, 1.23, 45.29) (12.00m to mesh).
- day 3 20:56 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-8.64, 1.11, 54.37) — walking out to (-8.06, 1.19, 55.75) (12.18m to mesh).
- day 3 22:57 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (5.91, 1.11, 47.68) — walking out to (5.34, 1.25, 46.29) (12.04m to mesh).
- day 3 23:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 3 23:23 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): stuck at (-12.07, 1.11, 49.73) — walking out to (-11.50, 1.20, 51.12) (12.27m to mesh).
- day 3 23:31 | **ERROR** | [GridNav] 'Employee_Mark Peterson': 'staging lane 2D line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 23:53 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-2.53, 1.21, 43.00) — walking out to (-13.79, 1.10, 36.50) (12.00m to mesh).
- day 3 23:55 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (0.07, 1.23, 45.79) — walking out to (-6.43, 0.04, 34.53) (12.00m to mesh).
- day 3 23:55 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet c592c087-a399-48f5-8c41-b4bd065d4733 but the employee is 9.5m away (max 4.5m) — refusing to start receiving.
