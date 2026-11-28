# Bug log - block 58

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 09:58 | **ERROR** | [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → 3E: NO NAVMESH PATH. anchor=(9.89, 1.11, 39.69) → driveTarget=(9.89, 1.22, 39.69), from=(-12.72, 1.15, 37.87), elapsed=30.1s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerOffloadController/<SeekViaNavMesh>d__60:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:1224)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 15:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 1 15:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 1 15:28 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B0 anchor: NO NAVMESH PATH. anchor=(-28.54, 5.11, 49.29) → driveTarget=(-28.54, 1.20, 49.29), from=(-19.63, 1.20, 49.89), elapsed=30.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=8.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [0x00025] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2375 < at GameCore.Actors.ReachTruckOperator+<TryGoAround>d__155.MoveNext () [0x0029f] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:3020 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 462.8s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 8 task(s) it could take wait
- day 1 16:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 1 17:25 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 108/248.
- day 1 19:07 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 111/248.
- day 1 20:59 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 136/248.
- day 1 22:04 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 173/248.
- day 1 22:09 | **ERROR** | [GridNav] 'Employee_Mark Peterson': no route for 'staging lane 1B wait' from (-16.26, 1.11, 47.66) to (-11.31, 1.11, 33.34) (NoPath, 1777 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:70)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 22:16 | **REPEATING** | x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- day 1 22:31 | **ERROR** | [GridNav] 'Employee_Shirley Edwards': no route for 'staging lane 1B wait' from (-16.26, 1.11, 47.66) to (-11.31, 1.11, 33.34) (NoPath, 1777 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:70)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 23:53 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.16, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 2 00:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 26 task(s) it could take wait
- day 2 00:21 | **ERROR** | [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → 3E: NO NAVMESH PATH. anchor=(9.89, 1.11, 39.69) → driveTarget=(9.89, 1.22, 39.69), from=(5.66, 1.11, 39.27), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerOffloadController/<SeekViaNavMesh>d__60:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:1224)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 01:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 1 task(s) it could take wait
- day 2 01:12 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 176/248.
- day 2 02:23 | **ERROR** | [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → 3E: NO NAVMESH PATH. anchor=(9.89, 1.11, 39.69) → driveTarget=(9.89, 1.22, 39.69), from=(7.84, 1.23, 40.51), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerOffloadController/<SeekViaNavMesh>d__60:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:1224)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 02:28 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-9.38, 1.14, 55.09) — walking out to (-9.38, 1.25, 42.09) (12.00m to mesh).
- day 2 02:33 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-6.30, 1.12, 59.12) — walking out to (0.20, 1.19, 47.86) (12.00m to mesh).
- day 2 02:55 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-37.90, 1.11, 42.34).
- day 2 02:55 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-26.38, 1.11, 43.20) — walking out to (-37.90, 1.11, 42.34) (11.55m to mesh).
- day 2 03:02 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 236/248.
- day 2 03:30 | **ERROR** | [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → 3E: NO NAVMESH PATH. anchor=(9.89, 1.11, 39.69) → driveTarget=(9.89, 1.22, 39.69), from=(-2.35, 1.09, 35.94), elapsed=30.2s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerOffloadController/<SeekViaNavMesh>d__60:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:1224)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 04:38 | **ERROR** | [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → 3E: NO NAVMESH PATH. anchor=(9.89, 1.11, 39.69) → driveTarget=(9.89, 1.22, 39.69), from=(8.29, 1.11, 40.36), elapsed=30.1s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerOffloadController/<SeekViaNavMesh>d__60:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:1224)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 05:28 | **WARN** | [OrderSelectionTaskDriver] Order 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a (Fizz-Pop Soda Works) has no reachable pick location for its remaining line item(s) — staging short at 41/44.
- day 2 06:13 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-09-B0 anchor: NO NAVMESH PATH. anchor=(-29.86, 5.11, 40.71) → driveTarget=(-29.86, 1.18, 40.71), from=(-20.67, 1.23, 44.17), elapsed=30.2s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=10.5, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 297.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 07:09 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-13.96, 1.11, 55.67) — walking out to (-13.39, 1.18, 57.05) (12.00m to mesh).
- day 2 07:10 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-8.05, 1.12, 58.44) — walking out to (-8.05, 1.20, 45.44) (12.00m to mesh).
- day 2 07:16 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-8.05, 1.12, 58.76) — walking out to (-8.05, 1.20, 45.76) (12.00m to mesh).
- day 2 07:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 21 task(s) it could take wait
- day 2 08:12 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.68, 1.14, 46.59) — walking out to (-23.16, 0.04, 34.58) (12.64m to mesh).
- day 2 09:10 | **OVERLAP** | OVERLAP Pallet_01_Slot0_Tier1 & Pallet_11_Slot7_Tier1 @(27, 78)  <<HISTORY: d2 06:48 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 07:28 Putaway task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d2 07:28 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 07:45 moved (57, 85)->(-1, -1) by InventoryService.MovePallet | d2 08:50 moved (-1, -1)->(27, 78) by InventoryService.MovePallet | d2 08:50 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 09:57 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 22:06 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 22:34 moved (57, 82)->(-1, -1) by InventoryService.MovePallet | d2 01:25 moved (-1, -1)->(27, 78) by InventoryService.MovePallet | d2 01:25 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 01:26 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>
- day 2 09:33 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-1.01, 1.12, 59.15) — walking out to (-1.01, 1.24, 46.15) (12.00m to mesh).
- day 2 11:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 19 task(s) it could take wait
- day 2 12:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 12 task(s) it could take wait
- day 2 13:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 21 task(s) it could take wait
- day 2 13:28 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-04-B1 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 41.34) → driveTarget=(-23.24, 1.22, 41.34), from=(-9.99, 1.24, 48.10), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=15.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__141.MoveNext () [0x00786] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2769 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 171.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 13:53 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-10.74, 1.19, 47.86) — walking out to (-10.74, 1.08, 35.17) (12.00m to mesh).
- day 2 14:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 12 task(s) it could take wait
- day 2 14:16 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-12.64, 1.20, 46.50) — walking out to (-6.17, 1.08, 35.17) (12.57m to mesh).
- day 2 15:08 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-40.89, 1.11, 40.95) — walking out to (-40.89, 1.12, 53.95) (12.00m to mesh).
- day 2 16:24 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-41.51, 1.12, 44.68) — walking out to (-28.52, 1.12, 43.36) (12.06m to mesh).
- day 2 17:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 18 task(s) it could take wait
- day 2 18:39 | **WARN** | [OrderSelectionTaskDriver] Order 368110a7-be45-4027-af00-3c57e3568069 (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at 41/53 case(s).
- day 2 19:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 17 task(s) it could take wait
- day 2 19:35 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-31.61, 1.12, 43.46) — walking out to (-42.87, 1.09, 37.31) (12.00m to mesh).
- day 2 20:31 | **ERROR** | [GridNav] 'Employee_Larry Morgan': no route for 'bay 02-01 (aisle entry)' from (-35.12, 1.11, 42.34) to (-40.81, 1.11, 47.66) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:70)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 20:31 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 02-01' - driving straight to the stop instead.
- day 2 20:53 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-40.03, 1.11, 40.63) — walking out to (-40.11, 1.12, 53.63) (12.00m to mesh).
- day 2 21:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 15 task(s) it could take wait
- day 2 22:07 | **ERROR** | [GridNav] 'Employee_Larry Morgan': no route for 'bay 02-01' from (-35.12, 1.11, 42.34) to (-37.14, 1.11, 47.66) (GoalBlocked, 0 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__5:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:70)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 22:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 4 task(s) it could take wait
- day 2 22:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 14 task(s) it could take wait
- day 2 23:29 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-24.90, 1.12, 42.28) — walking out to (-36.13, 1.08, 35.17) (12.43m to mesh).
