# Bug log - block 60

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:13 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (12.14, 1.17, 36.10) — walking out to (5.64, 1.23, 47.36) (12.00m to mesh).
- day 1 08:13 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.20, 1.22, 51.71) (12.00m to mesh).
- day 1 09:22 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-12.00, 1.12, 58.93) — walking out to (-12.00, 1.22, 45.93) (12.00m to mesh).
- day 1 09:43 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (9.88, 1.23, 48.62) — walking out to (3.38, 0.04, 59.88) (12.00m to mesh).
- day 1 09:51 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 98c3775e-16ec-4529-9b1d-66e6a432e2d4 but the employee is 14.6m away (max 4.5m) — refusing to start receiving.
- day 1 09:53 | **ERROR** | [TrailerOffload] 'DS_FULL(Clone)' cross-door offload → 3E: NO NAVMESH PATH. anchor=(9.89, 1.11, 39.69) → driveTarget=(9.89, 1.25, 39.69), from=(5.85, 1.13, 35.51), elapsed=30.2s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerOffloadController/<SeekViaNavMesh>d__60:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerOffloadController.cs:1224)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 15:00 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 145/248.
- day 1 15:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 1 17:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 1 17:40 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→PO_IG1916 & Truck→PO_IG1915 at (-6.4, 0.0, 17.9)
- day 1 17:59 | **REPEATING** | x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- day 1 20:07 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 151/248.
- day 1 22:20 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 151/248.
- day 1 23:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 2 01:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 6 task(s) it could take wait
- day 2 02:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 02:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 6 task(s) it could take wait
- day 2 03:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 05:12 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 176/248.
- day 2 06:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 6 task(s) it could take wait
- day 2 07:27 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 176/248.
- day 2 07:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 28 task(s) it could take wait
- day 2 11:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 6 task(s) it could take wait
- day 2 12:16 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-15.79, 1.15, 46.96) — walking out to (-22.25, 0.04, 34.58) (12.97m to mesh).
- day 2 12:24 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 176/248.
- day 2 12:35 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-16.42, 1.11, 44.20), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=23.5, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__141.MoveNext () [0x00786] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2769 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 354.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 18:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 18:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 6 task(s) it could take wait
- day 2 19:12 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 181/248.
- day 2 20:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 20:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 6 task(s) it could take wait
- day 2 21:46 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 181/248.
- day 2 22:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- day 2 22:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 6 task(s) it could take wait
