# Bug log - block 59

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:16 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (10.59, 1.20, 45.94) — walking out to (4.09, 1.09, 35.17) (12.00m to mesh).
- day 1 08:55 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-36.40, 1.11, 42.34).
- day 1 08:55 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-24.88, 1.11, 43.21) — walking out to (-36.40, 1.11, 42.34) (11.55m to mesh).
- day 1 09:59 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (11.17, 1.19, 40.85) — walking out to (11.17, 1.14, 53.85) (12.00m to mesh).
- day 1 10:03 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (11.17, 1.19, 40.84) — walking out to (11.17, 1.14, 53.84) (12.00m to mesh).
- day 1 11:32 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-26.86, 1.12, 47.82) — walking out to (-26.86, 0.04, 34.46) (12.36m to mesh).
- day 1 11:51 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 91/248.
- day 1 14:16 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 95/248.
- day 1 16:04 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-24.05, 1.22, 46.90), elapsed=30.8s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=15.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__141.MoveNext () [0x00786] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2769 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 428.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 1 16:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 1 16:31 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-25.06, 1.11, 47.66).
- day 1 16:31 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-36.58, 1.11, 46.79) — walking out to (-25.06, 1.11, 47.66) (11.55m to mesh).
- day 1 16:54 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-25.28, 1.12, 48.12) — walking out to (-25.28, 0.04, 34.46) (12.66m to mesh).
- day 1 17:03 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 105/248.
- day 1 18:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 8 task(s) it could take wait
- day 1 18:32 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-1.99, 1.12, 58.71) — walking out to (-1.99, 1.23, 45.71) (12.00m to mesh).
- day 1 18:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 450s while 29 task(s) it could take wait
- day 1 19:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 8 task(s) it could take wait
- day 1 20:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 8 task(s) it could take wait
- day 1 20:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 450s while 27 task(s) it could take wait
- day 1 20:55 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 1 21:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 1 22:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 1 22:25 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 1 22:25 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-23.73, 1.22, 46.94), elapsed=30.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=15.6, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__141.MoveNext () [0x00786] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2769 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 452.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 22:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 1 23:55 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 2 00:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 8 task(s) it could take wait
- day 2 00:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 01:10 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 2 02:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 8 task(s) it could take wait
- day 2 02:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 04:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 04:48 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 2 05:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 06:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 06:15 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 2 07:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 08:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 08:23 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 2 08:58 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress on order OG3625 for 10 game minutes - handing the task back to the queue.
- day 2 09:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 10:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 11:08 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 2 11:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 11:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 12:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 12:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 12:15 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 2 13:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 13:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 14:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 14:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 14:49 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' made no progress on order OG2624 for 10 game minutes - staging the 14 case(s) picked and freeing the rest.
- day 2 14:49 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' leaving the pallet jack (no progress) - its 14 case(s) stay on the jack for the next free selector to take over (order OG2624).
- day 2 14:51 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 2 15:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 15:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 16:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 16:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 16:22 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 2 17:07 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-23.56, 1.11, 46.93), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=15.8, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__141.MoveNext () [0x00786] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2769 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 871.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 17:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 17:21 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-16.60, 1.12, 58.91) — walking out to (-10.10, 1.21, 47.65) (12.00m to mesh).
- day 2 18:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 18:28 | **REPEATING** | x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- day 2 19:08 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 2 19:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 20:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 8 task(s) it could take wait
- day 2 21:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 21:25 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 114/248.
- day 2 22:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 9 task(s) it could take wait
- day 2 23:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 3 00:00 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 330s while 9 task(s) it could take wait
