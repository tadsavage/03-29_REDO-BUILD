# Bug log - block 70

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 09:02 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-14.06, 1.11, 51.33) — walking out to (-13.49, 1.19, 52.71) (12.00m to mesh).
- day 1 09:37 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.16, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- day 1 09:49 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-7.42, 1.17, 46.16) — walking out to (-13.92, 1.08, 35.17) (12.00m to mesh).
- day 1 12:41 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (5.07, 1.22, 44.61) — walking out to (5.07, 1.13, 57.61) (12.00m to mesh).
- day 1 12:42 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet ae4506ea-a93a-4b3d-be92-5aec025ba608 but the employee is 4.7m away (max 4.5m) — refusing to start receiving.
- day 1 13:10 | **OVERLAP** | OVERLAP Pallet_01_Slot1_Tier0 & A Chep @(33, 83)  <<HISTORY: d1 10:04 Putaway task created (STG1D-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 10:04 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 12:20 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 12:36 moved (43, 85)->(-1, -1) by InventoryService.MovePallet | d1 12:51 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 12:51 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 11:00 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:40 Replenish task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 12:06 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 12:30 moved (33, 83)->(-1, -1) by InventoryService.MovePallet | d1 12:34 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 12:34 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 1 13:40 | **OVERLAP** | OVERLAP Pallet_00_Slot0_Tier0 & Pallet_01_Slot1_Tier0 @(33, 83)  <<HISTORY: d1 09:39 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 09:46 moved (50, 85)->(-1, -1) by InventoryService.MovePallet | d1 12:06 Putaway task Assigned->Available by WorkQueueSystem.ReleaseStaleAssignments | d1 12:18 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 12:18 Putaway task Available->Complete by WorkQueueSystem.CompleteTask | d1 12:20 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>  <<HISTORY: d1 10:02 Putaway task created (STG2E-3->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 10:02 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 13:14 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 13:25 moved (50, 83)->(-1, -1) by InventoryService.MovePallet | d1 13:36 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 13:36 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- day 1 13:49 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (5.18, 1.13, 38.01) — walking out to (-6.08, 1.17, 44.51) (12.00m to mesh).
- day 1 19:16 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-16.26, 1.11, 47.66).
- day 1 19:16 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-21.13, 1.11, 51.16) — walking out to (-16.26, 1.11, 47.66) (5.99m to mesh).
- day 1 20:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 20 task(s) it could take wait
- day 1 20:21 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (1.85, 1.12, 59.25) — walking out to (1.85, 1.17, 46.25) (12.00m to mesh).
- day 1 20:30 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (1.78, 1.12, 59.00) — walking out to (1.78, 1.18, 46.00) (12.00m to mesh).
- day 1 20:54 | **REPEATING** | x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- day 1 22:27 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-2.33, 1.12, 59.00) — walking out to (-2.33, 1.23, 46.00) (12.00m to mesh).
- day 1 22:29 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-2.33, 1.12, 59.13) — walking out to (-2.33, 1.24, 46.13) (12.00m to mesh).
- day 1 08:26 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (6.69, 1.16, 49.00) — walking out to (6.69, 1.09, 36.00) (12.00m to mesh).
- day 1 08:33 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (1.62, 1.16, 58.25) — walking out to (8.12, 1.23, 46.99) (12.00m to mesh).
- day 1 08:50 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (1.62, 1.16, 57.70) — walking out to (8.12, 1.23, 46.44) (12.00m to mesh).
- day 1 10:35 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 20/248.
- day 1 11:22 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-10.95, 1.16, 48.78) — walking out to (-10.95, 1.09, 35.78) (12.00m to mesh).
- day 1 11:32 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-11.59, 1.16, 58.09) — walking out to (-22.83, 1.12, 50.87) (12.35m to mesh).
- day 1 11:57 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet fe91fdea-1c1e-4d21-b83f-625283a397e2 but the employee is 4.8m away (max 4.5m) — refusing to start receiving.
- day 1 12:02 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (-21.91, 1.22, 41.34) (path invalid) — abandoning the walk and handing the task back.
- day 1 12:26 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 33/248.
- day 1 12:36 | **REPEATING** | x25 and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.
- day 1 14:06 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 1B wait' (3.8m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 14:27 | **ERROR** | "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 15:37 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 68/248.
- day 1 16:14 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-1.66, 1.13, 58.61) — walking out to (4.84, 1.32, 47.36) (12.00m to mesh).
- day 1 16:42 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (0.93, 1.16, 57.90) — walking out to (7.43, 1.22, 46.64) (12.00m to mesh).
- day 1 20:30 | **WARN** | [OrderSelectionTaskDriver] Order eb1433f2-2ecc-4529-a46a-a256833be6b7 (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at 463/728.
- day 1 21:24 | **WARN** | [InventoryService] Lane 3B lock was held for over 120s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
- day 2 00:14 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-41.26, 1.12, 44.05) — walking out to (-28.26, 1.12, 43.41) (12.01m to mesh).
- day 2 02:23 | **WARN** | [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-40.81, 1.11, 42.34).
- day 2 02:23 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (-37.03, 1.11, 42.99) — walking out to (-40.81, 1.11, 42.34) (3.83m to mesh).
- day 2 03:36 | **WARN** | [OrderSelectionTaskDriver] Order eb1433f2-2ecc-4529-a46a-a256833be6b7 (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at 701/728.
- day 2 04:05 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-16.19, 1.16, 58.32) — walking out to (-22.69, 1.12, 47.06) (12.00m to mesh).
- day 2 04:05 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-16.56, 1.16, 58.43) — walking out to (-23.06, 1.12, 47.18) (12.00m to mesh).
- day 2 04:35 | **WARN** | [OrderSelectionTaskDriver] Order eb1433f2-2ecc-4529-a46a-a256833be6b7 (Rooted Robotics) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 3.
- day 2 04:35 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (1.62, 1.16, 58.25) — walking out to (8.12, 1.23, 46.99) (12.00m to mesh).
- day 2 05:11 | **WARN** | [OrderSelectionTaskDriver] Order eb1433f2-2ecc-4529-a46a-a256833be6b7 (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at 701/728.
- day 2 05:15 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (7.79, 1.11, 56.94) — walking out to (7.79, 1.25, 43.94) (12.00m to mesh).
- day 2 05:29 | **WARN** | [OrderService] Order OG4790 closed with a STAGED pallet (23 cs) that never made the trailer - not billed, returning it to stock.
- day 2 05:40 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 3A-1 (order eb1433f2-2ecc-4529-a46a-a256833be6b7 gone)
- day 2 06:14 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-12.94, 1.16, 58.26) — walking out to (-24.17, 1.12, 50.86) (12.44m to mesh).
- day 2 06:49 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (0.21, 1.13, 59.16) — walking out to (0.21, 1.24, 46.16) (12.00m to mesh).
- day 2 07:38 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-16.20, 1.16, 58.34) — walking out to (-22.70, 1.12, 47.08) (12.00m to mesh).
- day 2 08:17 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (6.71, 1.14, 58.62) — walking out to (6.71, 1.21, 45.62) (12.00m to mesh).
- day 2 13:03 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-12.93, 1.16, 58.39) — walking out to (0.07, 1.13, 58.39) (12.00m to mesh).
- day 2 13:09 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-12.95, 1.16, 58.38) — walking out to (0.05, 1.13, 58.38) (12.00m to mesh).
- day 2 15:14 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-0.44, 1.12, 58.89) — walking out to (-0.44, 1.23, 45.89) (12.00m to mesh).
- day 2 15:24 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-0.44, 1.13, 59.25) — walking out to (-0.44, 1.24, 46.25) (12.00m to mesh).
- day 2 15:24 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-0.44, 1.12, 58.78) — walking out to (-0.44, 1.23, 45.78) (12.00m to mesh).
- day 2 15:33 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (12.20, 1.13, 36.94) — walking out to (5.70, 1.16, 48.20) (12.00m to mesh).
- day 2 19:46 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (-31.19, 1.16, 41.34) (path invalid) — abandoning the walk and handing the task back.
- day 2 20:40 | **REPEATING** | x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (-11.00, 1.11, 49.27) - the next move would violate the 1 m vehicle clearance from PltJack_FULL(Clone).
- day 2 21:30 | **WARN** | [InventoryService] Lane 1A lock was held for over 120s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over.
- day 2 21:33 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-05 (aisle entry)' (10.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 2 21:33 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 01-05' - driving straight to the stop instead.
- day 2 21:35 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- day 2 22:37 | **WARN** | [InventoryService] Lane 1A lock was held for over 120s by reach truck RT_Full(Clone) (Amanda Dixon, PalletPick) - treating it as leaked and taking it over.
- day 2 23:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 360s while 14 task(s) it could take wait
