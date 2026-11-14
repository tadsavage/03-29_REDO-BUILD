# Bug log - block 40

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 09:00 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-15.80, 1.13, 58.96) — walking out to (-9.30, 1.20, 47.70) (12.00m to mesh).
- day 1 09:07 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (0.60, 1.11, 52.19) — walking out to (-5.90, 1.24, 40.93) (12.00m to mesh).
- day 1 11:10 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-4.69, 1.11, 48.99) — walking out to (-4.44, 1.25, 47.60) (12.00m to mesh).
- day 1 11:10 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 11:33 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (-24.56, 1.20, 40.71) (path invalid) — abandoning the walk and handing the task back.
- day 1 11:33 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-2.04, 1.11, 52.29) — walking out to (-1.46, 1.20, 53.67) (12.00m to mesh).
- day 1 11:46 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-4.04, 1.15, 59.25) — walking out to (-17.04, 1.12, 59.25) (12.00m to mesh).
- day 1 11:59 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (6.06, 1.14, 59.25) — walking out to (6.06, 1.22, 46.25) (12.00m to mesh).
- day 1 12:07 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (6.74, 1.22, 47.46) — walking out to (6.74, 0.04, 34.46) (12.00m to mesh).
- day 1 12:09 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (7.24, 1.14, 56.56) — walking out to (7.24, 1.22, 43.56) (12.00m to mesh).
- day 1 12:40 | **OVERLAP** | OVERLAP Pallet_01_Slot1_Tier0 & Pallet_01_Slot1_Tier0 @(31, 78)  <<HISTORY: d1 10:15 Putaway task created (STG3A-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 10:15 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 11:56 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 12:14 moved (53, 85)->(-1, -1) by InventoryService.MovePallet | d1 12:24 moved (-1, -1)->(31, 78) by InventoryService.MovePallet | d1 12:24 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 09:59 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 11:08 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:20 moved (46, 83)->(-1, -1) by InventoryService.MovePallet | d1 11:46 moved (-1, -1)->(31, 78) by InventoryService.MovePallet | d1 11:46 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 11:48 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>
- day 1 13:07 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-9.90, 1.14, 59.25) — walking out to (-9.90, 1.20, 46.25) (12.00m to mesh).
- day 1 13:09 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-10.99, 1.14, 58.07) — walking out to (-22.22, 1.12, 50.87) (12.34m to mesh).
- day 1 13:53 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-15.79, 1.13, 59.25) — walking out to (-9.29, 1.18, 47.99) (12.00m to mesh).
- day 1 14:41 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (7.10, 1.14, 58.89) — walking out to (7.10, 1.21, 45.89) (12.00m to mesh).
- day 1 14:50 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-16.58, 1.13, 58.91) — walking out to (-10.08, 1.21, 47.65) (12.00m to mesh).
- day 1 15:55 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (1.57, 1.16, 57.45) — walking out to (-4.93, 1.24, 46.19) (12.00m to mesh).
- day 1 15:58 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (0.75, 1.16, 58.66) — walking out to (0.75, 1.20, 45.66) (12.00m to mesh).
- day 1 15:59 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (0.75, 1.16, 58.79) — walking out to (0.75, 1.20, 45.79) (12.00m to mesh).
- day 1 16:13 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-11.10, 1.11, 53.87) — walking out to (-11.67, 1.21, 52.49) (12.00m to mesh).
- day 1 17:45 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-11.76, 1.14, 58.94) — walking out to (-11.76, 1.22, 45.94) (12.00m to mesh).
- day 1 17:46 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.75, 1.13, 58.93) — walking out to (-10.25, 1.20, 47.67) (12.00m to mesh).
- day 1 17:49 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-11.76, 1.14, 58.84) — walking out to (-11.76, 1.22, 45.84) (12.00m to mesh).
- day 1 17:52 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-11.76, 1.14, 58.87) — walking out to (-11.76, 1.22, 45.87) (12.00m to mesh).
- day 1 19:05 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (12.54, 1.11, 45.73) — walking out to (12.67, 1.16, 46.79) (12.00m to mesh).
- day 1 21:05 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 1 21:34 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-12.60, 1.13, 59.15) — walking out to (-12.60, 1.20, 46.15) (12.00m to mesh).
- day 1 21:34 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-10.03, 1.14, 59.03) — walking out to (-10.03, 1.20, 46.03) (12.00m to mesh).
- day 1 21:39 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-10.03, 1.14, 59.03) — walking out to (-10.03, 1.20, 46.03) (12.00m to mesh).
- day 1 21:43 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-5.86, 1.15, 58.98) — walking out to (-5.86, 1.20, 45.98) (12.00m to mesh).
- day 1 22:45 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-4.68, 1.11, 57.43) — walking out to (-4.11, 1.16, 58.81) (12.00m to mesh).
- day 1 23:35 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (2.76, 1.11, 53.52) — walking out to (3.34, 1.19, 54.90) (12.00m to mesh).
- day 2 01:48 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-4.35, 1.15, 59.06) — walking out to (-4.35, 1.24, 46.06) (12.00m to mesh).
- day 2 01:49 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-4.35, 1.15, 58.73) — walking out to (-4.35, 1.23, 45.73) (12.00m to mesh).
- day 2 02:53 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-2.66, 1.11, 55.38) — walking out to (-3.23, 1.20, 53.99) (12.01m to mesh).
- day 2 03:26 | **REPEATING** | x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- day 2 04:03 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-12.45, 1.13, 59.18) — walking out to (-12.45, 1.19, 46.18) (12.00m to mesh).
- day 2 06:12 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (6.96, 1.11, 59.00) — walking out to (8.34, 1.16, 59.00) (12.00m to mesh).
- day 2 06:53 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-6.87, 1.15, 58.68) — walking out to (-19.87, 0.04, 58.68) (12.00m to mesh).
- day 2 07:08 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-7.89, 1.15, 59.17) — walking out to (-7.89, 1.20, 46.17) (12.00m to mesh).
- day 2 07:09 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-2.90, 1.16, 59.15) — walking out to (-2.90, 1.24, 46.49) (12.00m to mesh).
- day 2 07:36 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (0.90, 1.11, 55.79) — walking out to (0.90, 1.25, 42.79) (12.00m to mesh).
- day 2 10:29 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-38.46, 1.11, 42.34).
- day 2 10:29 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-26.94, 1.11, 43.21) — walking out to (-38.46, 1.11, 42.34) (11.55m to mesh).
- day 2 10:59 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-7.23, 1.15, 59.16) — walking out to (-13.73, 1.16, 47.90) (12.00m to mesh).
- day 2 11:27 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 2 13:57 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-11.31, 1.16, 52.52) — walking out to (-24.31, 1.12, 53.22) (12.02m to mesh).
- day 2 14:41 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-6.77, 1.11, 52.43) — walking out to (-6.20, 1.20, 53.82) (12.00m to mesh).
- day 2 14:54 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-18.05, 1.11, 51.55) — walking out to (-17.48, 1.19, 52.93) (12.01m to mesh).
- day 2 16:02 | **WARN** | [InventoryService] Lane 1B lock was held for over 120s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
- day 2 16:08 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-8.96, 1.11, 59.00) — walking out to (-7.57, 1.16, 59.00) (12.00m to mesh).
- day 2 16:44 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-3.11, 1.11, 47.51) — walking out to (-2.54, 1.24, 48.89) (12.00m to mesh).
- day 2 17:03 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 135/248.
- day 2 17:29 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (11.19, 1.11, 56.26) — walking out to (12.67, 1.16, 56.26) (12.00m to mesh).
- day 2 18:04 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-12.28, 1.16, 40.49) — walking out to (-23.53, 1.12, 47.04) (12.02m to mesh).
- day 2 18:36 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 141/248.
- day 2 18:54 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-13.80, 1.11, 47.64) — walking out to (-13.22, 1.23, 49.02) (12.01m to mesh).
- day 2 19:06 | **REPEATING** | x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (5.32, 1.11, 56.26) runs through something solid - taking a clear way out to (5.89, 1.17, 57.65) instead.
- day 2 19:50 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 168/248.
- day 2 20:42 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (2.34, 1.16, 59.04) — walking out to (13.60, 0.04, 52.54) (12.00m to mesh).
- day 2 21:22 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-4.74, 1.11, 55.01) — walking out to (-5.32, 1.20, 53.63) (12.00m to mesh).
- day 2 21:35 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-5.53, 1.11, 58.32) — walking out to (-5.53, 1.16, 59.00) (12.00m to mesh).
- day 2 22:05 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-11.28, 1.11, 45.66) — walking out to (-10.70, 1.25, 47.05) (12.05m to mesh).
- day 2 22:12 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-14.29, 1.13, 58.91) — walking out to (-3.03, 1.18, 52.41) (12.00m to mesh).
- day 2 22:19 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-14.29, 1.13, 58.91) — walking out to (-7.79, 1.20, 47.65) (12.00m to mesh).
- day 2 22:24 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (3.88, 1.15, 59.19) — walking out to (3.88, 1.23, 46.19) (12.00m to mesh).
- day 3 00:56 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (8.96, 1.13, 58.91) — walking out to (8.96, 1.21, 45.91) (12.00m to mesh).
- day 3 01:59 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-36.72, 1.11, 51.16) — walking out to (-38.11, 1.18, 51.16) (12.00m to mesh).
- day 3 02:46 | **WARN** | [InventoryService] Lane 1B lock was held for over 120s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
- day 3 02:58 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 3C line-up' (20.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 03:04 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 1B: NO NAVMESH PATH. anchor=(-11.31, 1.14, 48.99) → driveTarget=(-11.31, 1.23, 48.99), from=(-9.06, 1.11, 51.55), elapsed=31.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=3.4, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 295.8s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 03:17 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-9.42, 1.15, 55.09) — walking out to (-9.42, 1.25, 42.09) (12.00m to mesh).
- day 3 03:43 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-02 (loop in)' (13.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 04:12 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-32.10, 1.11, 48.24) — walking out to (-30.72, 1.20, 48.81) (12.00m to mesh).
- day 3 06:00 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-0.96, 1.11, 47.25) — walking out to (-0.39, 1.24, 48.64) (12.33m to mesh).
- day 3 06:30 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-02 (aisle entry)' (10.7m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 06:30 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 02-02' - driving straight to the stop instead.
- day 3 07:05 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-9.48, 1.11, 47.84) — walking out to (-8.91, 1.23, 49.22) (12.20m to mesh).
- day 3 07:05 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 07:46 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-04-A0 anchor: NO NAVMESH PATH. anchor=(-21.91, 3.11, 41.34) → driveTarget=(-21.91, 1.22, 41.34), from=(-23.66, 1.20, 40.24), elapsed=30.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=2.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 216.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 08:36 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-2.75, 1.17, 55.09) — walking out to (-2.51, 1.20, 42.09) (12.00m to mesh).
- day 3 08:46 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (3.88, 1.15, 58.33) — walking out to (3.88, 1.23, 45.33) (12.00m to mesh).
- day 3 09:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 19 task(s) it could take wait
- day 3 09:41 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-01 (aisle entry)' (7.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 09:41 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- day 3 10:49 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (0.58, 1.11, 51.46) — walking out to (1.15, 1.21, 52.84) (12.21m to mesh).
- day 3 11:19 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-04-B1 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 41.34) → driveTarget=(-23.24, 1.22, 41.34), from=(-20.10, 1.11, 44.10), elapsed=31.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=4.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 206.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 11:55 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-1.84, 1.11, 57.58) — walking out to (-1.84, 1.16, 59.00) (12.00m to mesh).
- day 3 12:40 | **DOOR-DEADLOCK** | OG3673 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3C: [1: own/Staged/y1.2] 
- day 3 13:23 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-44.09, 1.11, 41.69) — walking out to (-31.09, 1.19, 41.69) (12.00m to mesh).
- day 3 13:24 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-4.96, 1.17, 52.35) — walking out to (6.30, 1.22, 45.85) (12.00m to mesh).
- day 3 14:38 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-02 (loop in)' (10.4m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 15:10 | **DOOR-DEADLOCK** | OG3672 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2A: 
- day 3 15:10 | **DOOR-DEADLOCK** | OG3675 is Staged but door 3's truck has waited 90 min with no one loading it. | lane 2D: [1: own/Staged/y1.2] 
- day 3 16:10 | **DOOR-DEADLOCK** | OG3673 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3C: [1: own/Staged/y1.2] 
- day 3 17:10 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (3.96, 1.11, 57.22) — walking out to (5.35, 1.17, 57.79) (12.80m to mesh).
- day 3 17:40 | **DOOR-DEADLOCK** | OG3676 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3D: [1: own/Staged/y1.2, own/Staged/y2.5] 
- day 3 18:00 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-31.75, 1.12, 48.95) — walking out to (-31.75, 0.04, 34.46) (13.49m to mesh).
- day 3 18:11 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-29.90, 1.11, 47.53) — walking out to (-28.52, 1.20, 48.10) (12.00m to mesh).
- day 3 18:39 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (12.25, 1.11, 56.26) — walking out to (5.75, 1.25, 45.00) (12.00m to mesh).
- day 3 19:46 | **ERROR** | [TrailerLoad] 'DS_FULL(Clone)' cleanup: → lane 2D: never arrived at (-0.71, 1.19, 39.19) (elapsed 31.6s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=17.6, reissues=0, lastSeekCancelledBy=[at GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__32.MoveNext () [0x00249] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\1. FSM\Core\Labor\TrailerLoadController.DigOut.cs:701 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < at UnityEngine.MonoBehaviour.StartCoroutineManaged2_Injected (System.IntPtr , System.Collections.IEnumerator ) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 32.1s ago]
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__32:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:756)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 19:47 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (10.06, 1.13, 58.91) — walking out to (10.06, 1.22, 45.91) (12.00m to mesh).
- day 3 21:20 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 2E wait' (14.8m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 21:25 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (8.27, 1.11, 44.34) — walking out to (-4.73, 1.25, 44.34) (12.00m to mesh).
- day 3 21:35 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 2E line-up' (10.3m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 21:47 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 13.7m short of 'bay 01-01 (aisle entry)' - re-issuing the drive (3/6).
- day 3 21:51 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-11.19, 1.16, 50.58) — walking out to (0.07, 1.22, 44.08) (12.00m to mesh).
- day 3 22:09 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-01 (aisle entry)' (14.0m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 22:09 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- day 3 22:40 | **DOOR-DEADLOCK** | OG3673 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3C: [1: own/Staged/y1.2, own/Staged/y1.9] 
- day 3 22:40 | **DOOR-DEADLOCK** | OG3676 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3D: [1: own/Staged/y1.2, own/Staged/y2.5] 
- day 3 23:27 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (-2.95, 1.11, 53.61) — walking out to (-2.38, 1.19, 55.00) (12.03m to mesh).
