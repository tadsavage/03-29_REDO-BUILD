# Bug log - block 67

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:11 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (4.59, 1.11, 52.29) — walking out to (3.09, 1.21, 52.29) (12.00m to mesh).
- day 1 08:11 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (12.88, 1.16, 36.88) — walking out to (-0.12, 1.11, 36.89) (12.00m to mesh).
- day 1 08:36 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (3.33, 1.12, 59.19) — walking out to (3.33, 1.19, 46.19) (12.00m to mesh).
- day 1 08:38 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-15.37, 1.11, 51.16) — walking out to (-4.11, 1.25, 44.66) (12.00m to mesh).
- day 1 10:49 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-8.03, 1.20, 46.49) — walking out to (-14.52, 1.08, 35.17) (12.18m to mesh).
- day 1 10:50 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 74029820-c245-4f34-afb6-a404d475e0dc but the employee is 6.4m away (max 4.5m) — refusing to start receiving.
- day 1 10:56 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-14.02, 1.11, 46.47) — walking out to (-13.44, 1.24, 47.86) (12.50m to mesh).
- day 1 10:56 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 12:32 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B1 anchor: NO NAVMESH PATH. anchor=(-37.81, 5.11, 49.29) → driveTarget=(-37.81, 1.18, 49.29), from=(-35.28, 1.11, 49.06), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=2.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 205.7s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 12:44 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 97/248.
- day 1 12:55 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-1.98, 1.20, 40.93) — walking out to (-1.98, 1.14, 53.93) (12.00m to mesh).
- day 1 14:08 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-05-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 40.71) → driveTarget=(-24.56, 1.20, 40.71), from=(-20.52, 1.11, 43.81), elapsed=31.3s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=5.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 270.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 14:44 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-0.97, 1.12, 59.25) — walking out to (-7.47, 1.19, 47.99) (12.00m to mesh).
- day 1 15:59 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-22.31, 1.22, 43.21), elapsed=31.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 162.4s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 16:59 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-22.98, 1.11, 43.21), elapsed=31.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 231.4s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 17:35 | **ERROR** | [GridNav] 'Employee_Larry Morgan': 'staging lane 3A line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__8:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:109)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 18:08 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-24.25, 1.11, 42.70), elapsed=31.9s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 224.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 18:53 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-17.64, 1.23, 43.61), elapsed=30.3s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=14.4, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 124.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 19:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- day 1 20:05 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-33.43, 1.11, 42.34).
- day 1 20:05 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-21.91, 1.11, 41.34) — walking out to (-33.43, 1.11, 42.34) (11.56m to mesh).
- day 1 20:55 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 22:41 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane 1D exit: NO NAVMESH PATH. anchor=(-8.66, 1.11, 48.99) → driveTarget=(-8.66, 1.24, 48.99), from=(-39.93, 1.11, 43.21), elapsed=32.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 104.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 23:23 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-40.81, 1.11, 42.34).
- day 1 23:23 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-38.21, 1.11, 43.21) — walking out to (-40.81, 1.11, 42.34) (2.75m to mesh).
- day 1 23:23 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 00:05 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-16.69, 1.12, 58.91) — walking out to (-10.19, 1.19, 47.81) (12.00m to mesh).
- day 2 00:09 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-9.14, 1.12, 59.07) — walking out to (-9.14, 1.20, 46.07) (12.00m to mesh).
- day 2 00:54 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 137/248.
- day 2 04:12 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 153/248.
- day 2 05:17 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-27.22, 1.11, 40.81), elapsed=32.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 153.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 05:38 | **ERROR** | [GridNav] 'Employee_Larry Morgan': 'staging lane 3A line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__8:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:109)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 05:41 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 153/248.
- day 2 07:35 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 176/248.
- day 2 08:47 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-07-B0 anchor: NO NAVMESH PATH. anchor=(-27.21, 5.11, 40.71) → driveTarget=(-27.21, 1.19, 40.71), from=(-25.38, 1.11, 43.00), elapsed=32.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=2.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 310.8s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 09:10 | **OVERLAP** | OVERLAP Pallet_03_Slot2_Tier0 & Pallet_07_Slot4_Tier0 @(33, 84)  <<HISTORY: d2 06:16 Putaway task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d2 06:38 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 07:03 moved (50, 83)->(-1, -1) by InventoryService.MovePallet | d2 08:37 moved (-1, -1)->(33, 84) by InventoryService.MovePallet | d2 08:37 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 08:38 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>  <<HISTORY: d1 10:39 Putaway task created (STG2E-2->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 10:39 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 08:39 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 08:44 moved (50, 82)->(-1, -1) by InventoryService.MovePallet | d2 08:54 moved (-1, -1)->(33, 84) by InventoryService.MovePallet | d2 08:54 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 2 09:25 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-28.58, 1.11, 40.68) — walking out to (-28.58, 1.12, 54.16) (12.49m to mesh).
- day 2 09:36 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 181/248.
- day 2 09:37 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-7.58, 1.11, 40.73) — walking out to (-8.15, 1.20, 39.35) (12.00m to mesh).
- day 2 09:37 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 09:59 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-16-A1 anchor: NO NAVMESH PATH. anchor=(-39.14, 3.11, 41.34) → driveTarget=(-39.14, 1.16, 41.34), from=(-22.42, 1.22, 43.21), elapsed=31.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=16.8, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 171.4s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 11:05 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-40.81, 1.11, 42.34).
- day 2 11:05 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-29.86, 1.11, 40.71) — walking out to (-40.81, 1.11, 42.34) (11.07m to mesh).
- day 2 11:05 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 11:44 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-13.54, 1.11, 46.85), elapsed=30.7s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=9.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 135.4s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 12:27 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-10.29, 1.19, 47.89) — walking out to (-10.29, 1.08, 35.17) (12.00m to mesh).
- day 2 12:29 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 235/248.
- day 2 13:50 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-18.36, 1.11, 44.11) — walking out to (-16.97, 1.23, 44.68) (12.03m to mesh).
- day 2 13:50 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 14:32 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-09-A0 anchor: NO NAVMESH PATH. anchor=(-29.86, 3.11, 40.71) → driveTarget=(-29.86, 1.18, 40.71), from=(-26.15, 1.11, 41.79), elapsed=30.2s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=3.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 198.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 17:11 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (0.61, 1.11, 48.99) — walking out to (-12.39, 1.23, 48.99) (12.00m to mesh).
- day 2 17:11 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 17:38 | **WARN** | [AiNavigation] DS_FULL(Clone): stuck at (8.56, 1.11, 39.69) — walking out to (7.99, 1.20, 38.30) (12.00m to mesh).
- day 2 18:41 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-14.87, 1.22, 49.23), elapsed=32.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=10.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 207.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 19:10 | **DOOR-DEADLOCK** | OG3625 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3E: [1: stock 70745806-3662-4455-888f-64dd70d3621e] [2: stock a32230e3-43e9-4280-b05c-975061a2abb4, stock 7c1032a1-dc93-4e31-b5c8-d29b8b7a7382] lane 3A: [1: own/Staged/y1.2, own/Staged/y2.7] 
- day 2 19:38 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (7.28, 1.12, 58.91) — walking out to (7.28, 1.21, 45.91) (12.00m to mesh).
- day 2 19:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 21 task(s) it could take wait
- day 2 20:17 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-13-B0 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 48.66) → driveTarget=(-23.24, 1.21, 48.66), from=(-8.65, 1.11, 49.16), elapsed=30.2s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=14.6, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 282.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 20:23 | **ERROR** | [GridNav] 'Employee_Larry Morgan': 'staging lane 3A line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__8:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:109)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 20:35 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-19.22, 1.11, 48.95), elapsed=31.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=14.5, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 231.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 21:13 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-14.16, 1.15, 48.70) — walking out to (-14.16, 1.09, 35.70) (12.00m to mesh).
- day 2 21:24 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-10.99, 1.16, 48.45) — walking out to (-10.99, 1.08, 35.45) (12.00m to mesh).
- day 2 21:56 | **ERROR** | [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-A1 anchor: NO NAVMESH PATH. anchor=(-37.81, 3.11, 49.29) → driveTarget=(-37.81, 1.18, 49.29), from=(-36.22, 1.11, 49.34), elapsed=30.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=1.6, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 270.5s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 22:10 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-7.65, 1.13, 58.03) — walking out to (-1.54, 1.24, 46.77) (12.00m to mesh).
- day 2 23:10 | **DOOR-DEADLOCK** | OG2624 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1B: 
- day 2 23:40 | **DOOR-DEADLOCK** | OG3672 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2A: [2: own/Staged/y1.2] 
- day 2 23:57 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-10.17, 1.23, 47.34) — walking out to (-10.17, 0.04, 34.34) (12.00m to mesh).
