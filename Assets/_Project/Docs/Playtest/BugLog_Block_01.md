# Bug log - block 1

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:00 | **WARN** | To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.
- day 1 08:02 | **ERROR** | RuntimeNavMeshBuilder: Source mesh garbage_container does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 08:02 | **ERROR** | RuntimeNavMeshBuilder: Source mesh garbage_container_lid does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 08:02 | **ERROR** | RuntimeNavMeshBuilder: Source mesh garbage_container_trash does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 08:02 | **ERROR** | RuntimeNavMeshBuilder: Source mesh Jersey barrier does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 08:02 | **ERROR** | RuntimeNavMeshBuilder: Source mesh ParkBanch does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 08:03 | **ERROR** | RuntimeNavMeshBuilder: Source mesh flakes_2 does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 08:03 | **ERROR** | RuntimeNavMeshBuilder: Source mesh orange does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 08:03 | **ERROR** | RuntimeNavMeshBuilder: Source mesh flakes does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 08:03 | **ERROR** | RuntimeNavMeshBuilder: Source mesh cola does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 08:03 | **ERROR** | RuntimeNavMeshBuilder: Source mesh vending_machine_button_3 does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 08:03 | **ERROR** | RuntimeNavMeshBuilder: Source mesh vending_machine does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 08:03 | **ERROR** | RuntimeNavMeshBuilder: Source mesh vending_machine_2 does not allow read access. This will work in playmode in the editor but not in player
  ```
  UnityEngine.AI.NavMeshBuilder:UpdateNavMeshDataAsync (UnityEngine.AI.NavMeshData,UnityEngine.AI.NavMeshBuildSettings,System.Collections.Generic.List`1<UnityEngine.AI.NavMeshBuildSource>,UnityEngine.Bounds)
  NavMeshManager/<UpdateRoutine>d__49:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Managers/NavMeshManager.cs:691)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 12:19 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 01-05-01 anchor: NO NAVMESH PATH. anchor=(-8.66, 1.11, 19.51) → driveTarget=(-8.66, 1.11, 19.51), from=(5.64, 1.16, 24.80), elapsed=1.5s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 13:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 5 task(s) wait
- day 1 14:06 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-07-00 anchor: NO NAVMESH PATH. anchor=(-11.31, 1.11, 26.14) → driveTarget=(-11.31, 1.11, 26.14), from=(3.91, 1.11, 27.10), elapsed=0.7s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 14:27 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → source lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 1.11, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(12.05, 1.16, 27.44), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 15:25 | **NO-TASK** | NO-TASK lane pallet 1B-3 sku=182491 load=5594936484
- day 1 15:52 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Ruth Rodriguez' as its operator though nobody is on it or running it - clearing the stale link.
- day 1 17:36 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-07-00 anchor: NO NAVMESH PATH. anchor=(-11.31, 1.11, 26.14) → driveTarget=(-11.31, 1.11, 26.14), from=(-4.65, 1.11, 25.96), elapsed=2.9s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 17:43 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → source lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 1.11, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(15.51, 1.11, 27.93), elapsed=7.9s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 18:40 | **NO-TASK** | NO-TASK lane pallet 2B-1 sku=102435 load=0599550126
- day 1 19:02 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 1A: NO NAVMESH PATH. anchor=(4.59, 1.09, 27.79) → driveTarget=(4.59, 1.13, 27.79), from=(12.83, 1.16, 27.79), elapsed=1.1s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 19:55 | **NO-TASK** | NO-TASK lane pallet 2B-1 sku=102435 load=0959248994
- day 1 20:04 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → source lane 2C exit: NO NAVMESH PATH. anchor=(17.84, 1.11, 27.79) → driveTarget=(17.84, 1.12, 27.79), from=(13.88, 1.11, 30.95), elapsed=7.9s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 20:55 | **ERROR** | [TrailerLoad] 'Dockstocker' dig: → 1C far end: never arrived at (7.24, 1.12, 27.79) (elapsed 30.0s) — refusing to straight-line through pallets.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__20:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:713)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 21:03 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-05-01 anchor: NO NAVMESH PATH. anchor=(-12.64, 1.11, 26.14) → driveTarget=(-12.64, 1.11, 26.14), from=(14.99, 1.11, 27.58), elapsed=0.9s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 21:13 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(15.19, 1.09, 27.79) → driveTarget=(15.19, 1.13, 27.79), from=(19.39, 1.16, 26.53), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 21:23 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(15.19, 1.09, 27.79) → driveTarget=(15.19, 1.13, 27.79), from=(17.73, 1.11, 27.79), elapsed=0.2s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 22:38 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 1A: NO NAVMESH PATH. anchor=(4.59, 1.09, 27.79) → driveTarget=(4.59, 1.13, 27.79), from=(15.68, 1.11, 27.79), elapsed=0.4s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 22:49 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 1C exit: NO NAVMESH PATH. anchor=(7.24, 1.11, 27.79) → driveTarget=(7.24, 1.12, 27.79), from=(6.09, 1.11, 31.28), elapsed=1.9s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 23:14 | **WARN** | [AiNavigation] ReachTruck: stuck at (8.18, 1.11, 29.55) — walking out to (8.18, 0.08, 42.67) (12.16m to mesh).
- day 1 23:16 | **ERROR** | [TrailerLoad] 'Dockstocker' dig: back to source lane: never arrived at (5.91, 1.12, 17.99) (elapsed 30.0s) — refusing to straight-line through pallets.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__20:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:713)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 00:12 | **ERROR** | [TrailerLoad] 'Dockstocker' dig: → 1C far end: never arrived at (7.24, 1.12, 27.79) (elapsed 30.0s) — refusing to straight-line through pallets.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__20:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:713)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 00:25 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 2C exit: NO NAVMESH PATH. anchor=(17.84, 0.00, 27.79) → driveTarget=(17.84, 1.12, 27.79), from=(24.87, 0.00, 35.49), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 01:02 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → source lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 1.11, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(14.54, 1.16, 26.17), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 01:23 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-05-01 anchor: NO NAVMESH PATH. anchor=(-12.64, 1.11, 26.14) → driveTarget=(-12.64, 1.11, 26.14), from=(12.68, 1.16, 27.42), elapsed=1.5s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 02:22 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 01-02-A1 anchor: NO NAVMESH PATH. anchor=(-3.36, 3.11, 18.81) → driveTarget=(-3.36, 1.11, 18.81), from=(4.84, 1.16, 21.78), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 02:37 | **WARN** | [AiNavigation] ReachTruck: stuck at (5.48, 1.11, 26.86) — walking out to (5.48, 1.08, 14.33) (12.43m to mesh).
- day 2 02:41 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 1C exit: NO NAVMESH PATH. anchor=(7.24, 1.11, 27.79) → driveTarget=(7.24, 1.12, 27.79), from=(5.48, 1.10, 20.40), elapsed=8.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 03:29 | **ERROR** | [TrailerLoad] 'Dockstocker' dig: return to source lane: never arrived at (5.91, 1.12, 17.99) (elapsed 1.3s) — refusing to straight-line through pallets.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__20:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:713)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 04:21 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 01-02-B1 anchor: NO NAVMESH PATH. anchor=(-3.36, 5.11, 18.81) → driveTarget=(-3.36, 1.11, 18.81), from=(8.91, 1.11, 27.78), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 04:30 | **ERROR** | [TrailerLoad] 'Dockstocker' dig: → 1C far end: never arrived at (7.24, 1.12, 27.79) (elapsed 30.0s) — refusing to straight-line through pallets.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__20:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:713)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 04:36 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-05-01 anchor: NO NAVMESH PATH. anchor=(-12.64, 1.11, 26.14) → driveTarget=(-12.64, 1.11, 26.14), from=(17.83, 1.11, 27.79), elapsed=0.1s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 07:15 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 1A: NO NAVMESH PATH. anchor=(4.59, 1.09, 27.79) → driveTarget=(4.59, 1.13, 27.79), from=(12.45, 1.16, 27.82), elapsed=1.2s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 07:51 | **WARN** | [AiNavigation] ReachTruck: stuck at (8.19, 1.11, 29.54) — walking out to (8.19, 0.08, 42.67) (12.17m to mesh).
- day 2 07:54 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 1C exit: NO NAVMESH PATH. anchor=(7.24, 1.16, 27.79) → driveTarget=(7.24, 1.16, 27.79), from=(8.19, 1.11, 33.34), elapsed=7.8s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 08:04 | **ERROR** | [TrailerLoad] 'Dockstocker' dig: back to source lane: never arrived at (5.91, 1.12, 17.99) (elapsed 30.0s) — refusing to straight-line through pallets.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__20:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:713)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 08:43 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-04-00 anchor: NO NAVMESH PATH. anchor=(-16.61, 1.11, 25.44) → driveTarget=(-16.61, 1.11, 25.44), from=(7.24, 1.11, 27.79), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 09:55 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 2C exit: NO NAVMESH PATH. anchor=(17.84, 1.11, 27.79) → driveTarget=(17.84, 1.12, 27.79), from=(13.46, 1.11, 27.48), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 13:05 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-07-00 anchor: NO NAVMESH PATH. anchor=(-11.31, 1.11, 26.14) → driveTarget=(-11.31, 1.11, 26.14), from=(3.69, 1.11, 27.77), elapsed=1.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 16:10 | **ERROR** | [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 1A: NO NAVMESH PATH. anchor=(4.59, 1.09, 27.79) → driveTarget=(4.59, 1.13, 27.79), from=(15.95, 1.11, 27.79), elapsed=0.3s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 17:14 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-07-00 anchor: NO NAVMESH PATH. anchor=(-11.31, 1.11, 26.14) → driveTarget=(-11.31, 1.11, 26.14), from=(3.66, 1.11, 27.02), elapsed=0.8s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 20:41 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-05-01 anchor: NO NAVMESH PATH. anchor=(-12.64, 1.11, 26.14) → driveTarget=(-12.64, 1.11, 26.14), from=(17.78, 1.11, 27.65), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 23:30 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 1.11, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(14.86, 1.16, 26.44), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 00:23 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 01-10-A0 anchor: NO NAVMESH PATH. anchor=(-12.64, 3.11, 18.81) → driveTarget=(-12.64, 1.11, 18.81), from=(15.11, 1.16, 25.40), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 01:18 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 01-10-A0 anchor: NO NAVMESH PATH. anchor=(-12.64, 3.11, 18.81) → driveTarget=(-12.64, 1.11, 18.81), from=(-10.69, 1.11, 18.83), elapsed=7.9s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 02:08 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 2C exit: NO NAVMESH PATH. anchor=(17.84, 1.11, 27.79) → driveTarget=(17.84, 1.12, 27.79), from=(16.73, 1.11, 26.99), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 02:25 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 1.11, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(8.45, 1.11, 31.62), elapsed=1.7s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 04:29 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-07-00 anchor: NO NAVMESH PATH. anchor=(-11.31, 1.11, 26.14) → driveTarget=(-11.31, 1.11, 26.14), from=(-1.98, 1.11, 25.89), elapsed=2.3s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 05:30 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 1.11, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(14.53, 1.16, 27.55), elapsed=4.9s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 06:33 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 1.11, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(14.75, 1.16, 26.35), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 08:12 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 1.11, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(12.83, 1.16, 24.15), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 08:29 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-10-01 anchor: NO NAVMESH PATH. anchor=(-7.34, 1.11, 25.44) → driveTarget=(-7.34, 1.11, 25.44), from=(13.43, 1.11, 30.94), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 09:13 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-05-00 anchor: NO NAVMESH PATH. anchor=(-13.96, 1.11, 26.14) → driveTarget=(-13.96, 1.11, 26.14), from=(5.53, 1.11, 27.66), elapsed=0.3s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 10:39 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-02-01 anchor: NO NAVMESH PATH. anchor=(-17.94, 1.11, 25.44) → driveTarget=(-17.94, 1.12, 25.44), from=(18.50, 1.16, 28.02), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 10:40 | **NO-TASK** | NO-TASK lane pallet 2B-2 sku=520894 load=0393415491
- day 3 10:47 | **WARN** | [AiNavigation] ReachTruck: stuck at (17.82, 1.11, 29.30) — walking out to (26.82, 0.08, 29.30) (8.07m to mesh).
- day 3 10:51 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 1.11, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(24.14, 1.00, 29.30), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 12:11 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 0.00, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(24.73, 0.00, 32.85), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 13:22 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-05-00 anchor: NO NAVMESH PATH. anchor=(-13.96, 1.11, 26.14) → driveTarget=(-13.96, 1.11, 26.14), from=(5.78, 1.11, 28.24), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 15:13 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-12-00 anchor: NO NAVMESH PATH. anchor=(-6.01, 1.11, 25.44) → driveTarget=(-6.01, 1.11, 25.44), from=(-2.04, 1.11, 19.51), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 3 15:42 | **WARN** | [AiNavigation] ReachTruck: stuck at (-1.89, 1.11, 17.64) — walking out to (-1.89, 0.08, 12.17) (4.59m to mesh).
- day 3 15:46 | **ERROR** | [ReachTruckOperator] 'ReachTruck' putaway: → lane 2C exit: NO NAVMESH PATH. anchor=(17.84, 1.11, 27.79) → driveTarget=(17.84, 1.12, 27.79), from=(-1.45, 0.00, 12.21), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 4 01:11 | **ERROR** | [ReachTruckOperator] 'ReachTruck' deliver: → location 02-05-00 anchor: NO NAVMESH PATH. anchor=(-13.96, 1.11, 26.14) → driveTarget=(-13.96, 1.11, 26.14), from=(5.24, 1.16, 27.76), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 5 14:25 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→PO_IG5390 & Truck→PO_IG5389 at (-7.6, 0.0, -51.1)
- day 6 02:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 270s while 2 task(s) wait
- day 1 11:42 | **EXCEPTION** | ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection.
  ```
  System.Collections.Generic.List`1[T].get_Item (System.Int32 index) (at <c5ae8a4eeb824526b5df2932bc2395e0>:0)
  GameCore.Actors.OrderSelectionTaskDriver+<PickRoutine>d__152.MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.cs:282)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
