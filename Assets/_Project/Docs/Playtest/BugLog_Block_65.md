# Bug log - block 65

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 10:56 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-6.91, 1.18, 43.01) — walking out to (-19.91, 1.13, 43.01) (12.00m to mesh).
- day 1 10:56 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-6.91, 1.18, 43.01) — walking out to (-19.91, 1.13, 43.01) (12.00m to mesh).
- day 1 10:57 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 80e63ccf-635d-43f4-8384-cc109ea9da32 but the employee is 4.9m away (max 4.5m) — refusing to start receiving.
- day 1 10:57 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 80e63ccf-635d-43f4-8384-cc109ea9da32 but the employee is 4.9m away (max 4.5m) — refusing to start receiving.
- day 1 11:28 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-1.45, 1.12, 37.95) — walking out to (10.26, 1.21, 44.44) (12.39m to mesh).
- day 1 11:28 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-1.45, 1.12, 37.95) — walking out to (10.26, 1.21, 44.44) (12.39m to mesh).
- day 1 11:54 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (8.56, 1.24, 48.99) (path invalid) — abandoning the walk and handing the task back.
- day 1 11:54 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (8.56, 1.24, 48.99) (path invalid) — abandoning the walk and handing the task back.
- day 1 13:08 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-18.04, 1.11, 51.48), elapsed=32.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=21.5, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x00339] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1274 < ] 222.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 13:08 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-18.04, 1.11, 51.48), elapsed=32.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=21.5, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x00339] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1274 < ] 222.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 13:38 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (7.24, 1.12, 58.91) — walking out to (6.34, 1.22, 45.91) (12.03m to mesh).
- day 1 13:38 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (7.24, 1.12, 58.91) — walking out to (6.34, 1.22, 45.91) (12.03m to mesh).
- day 1 13:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 44 task(s) it could take wait
- day 1 13:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 1 13:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 44 task(s) it could take wait
- day 1 13:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 1 14:40 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-18.63, 1.13, 50.75) — walking out to (-29.85, 1.12, 42.89) (12.69m to mesh).
- day 1 14:40 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-18.63, 1.13, 50.75) — walking out to (-29.85, 1.12, 42.89) (12.69m to mesh).
- day 1 14:47 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B0 anchor: NO NAVMESH PATH. anchor=(-28.54, 5.11, 49.29) → driveTarget=(-28.54, 1.20, 49.29), from=(-18.40, 1.19, 51.16), elapsed=32.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=10.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 225.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 14:47 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B0 anchor: NO NAVMESH PATH. anchor=(-28.54, 5.11, 49.29) → driveTarget=(-28.54, 1.20, 49.29), from=(-18.40, 1.19, 51.16), elapsed=32.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=10.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 225.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:14 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-20.08, 1.11, 51.16), elapsed=31.3s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=19.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 252.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:14 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-20.08, 1.11, 51.16), elapsed=31.3s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=19.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 252.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 17:35 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B0 anchor: NO NAVMESH PATH. anchor=(-28.54, 5.11, 49.29) → driveTarget=(-28.54, 1.20, 49.29), from=(-17.81, 1.11, 51.67), elapsed=31.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=11.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 229.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 17:35 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B0 anchor: NO NAVMESH PATH. anchor=(-28.54, 5.11, 49.29) → driveTarget=(-28.54, 1.20, 49.29), from=(-17.81, 1.11, 51.67), elapsed=31.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=11.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 229.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 18:39 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 51/248.
- day 1 18:39 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 51/248.
- day 1 20:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 1 20:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 1 21:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 47 task(s) it could take wait
- day 1 21:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 47 task(s) it could take wait
- day 1 22:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 1 22:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 47 task(s) it could take wait
- day 1 22:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 1 22:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 47 task(s) it could take wait
- day 2 00:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 47 task(s) it could take wait
- day 2 00:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 47 task(s) it could take wait
- day 2 00:50 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-16.67, 1.12, 58.91) — walking out to (-10.17, 1.19, 47.81) (12.00m to mesh).
- day 2 00:50 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-16.67, 1.12, 58.91) — walking out to (-10.17, 1.19, 47.81) (12.00m to mesh).
- day 2 01:26 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-16.03, 1.20, 51.54), elapsed=30.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=23.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 185.8s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 01:26 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-16.03, 1.20, 51.54), elapsed=30.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=23.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 185.8s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 08:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 08:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 08:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 08:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 10:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 10:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 10:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 10:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 11:08 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 56/248.
- day 2 11:08 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 56/248.
- day 2 12:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 12:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 12:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 12:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 14:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 14:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 14:32 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 56/248.
- day 2 14:32 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 56/248.
- day 2 14:36 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-19.73, 1.12, 49.97) — walking out to (-30.97, 1.12, 42.93) (12.26m to mesh).
- day 2 14:36 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-19.73, 1.12, 49.97) — walking out to (-30.97, 1.12, 42.93) (12.26m to mesh).
- day 2 14:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 14:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 15:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 450s while 30 task(s) it could take wait
- day 2 15:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 450s while 30 task(s) it could take wait
- day 2 16:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 16:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 16:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 16:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 16:57 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 56/248.
- day 2 16:57 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 56/248.
- day 2 17:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 17:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 18:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 18:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 18:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 18:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 49 task(s) it could take wait
- day 2 19:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 2 19:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 2 20:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 2 20:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 2 20:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 48 task(s) it could take wait
- day 2 20:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 48 task(s) it could take wait
- day 2 21:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 2 21:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 2 21:18 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 56/248.
- day 2 21:18 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 56/248.
- day 2 22:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 22:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 22:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 48 task(s) it could take wait
- day 2 22:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 48 task(s) it could take wait
- day 2 23:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 23:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 30 task(s) it could take wait
- day 2 23:33 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 56/248.
- day 2 23:33 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 56/248.
- day 2 23:59 | **REPEATING** | x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (-19.06, 1.11, 48.94) - the next move would violate the 1 m vehicle clearance from PltJack_FULL(Clone).
- day 2 23:59 | **REPEATING** | x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (-19.06, 1.11, 48.94) - the next move would violate the 1 m vehicle clearance from PltJack_FULL(Clone).
- day 3 00:00 | **TRUCK-STALLED** | TRUCK-STALLED Truck→Outbound_Door1 state=DepartToApproach 1408s at (-11.3, 0.0, 27.9)
- day 3 00:00 | **TRUCK-STALLED** | TRUCK-STALLED Truck→Outbound_Door1 state=DepartToApproach 1408s at (-11.3, 0.0, 27.9)
