# Bug log - block 11

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 20:38 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 690s while 1 task(s) wait
- day 2 00:53 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 5 task(s) wait
- day 2 05:08 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 10 task(s) wait
- day 2 09:23 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 15 task(s) wait
- day 2 13:38 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 19 task(s) wait
- day 2 16:54 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Joshua Myers' as its operator though nobody is on it or running it - clearing the stale link.
- day 3 07:16 | **REPEATING** | x25 and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per 30 s)
- day 3 22:04 | **WARN** | [OrderService] Order OG2384 closed with a STAGED pallet (7 cs) that never made the trailer - not billed, returning it to stock.
- day 3 22:08 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1A-1 (order dac418a5-bb78-4cbd-95b1-b69a9a367537 gone)
- day 4 00:10 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (-28.17, 1.11, 36.25) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- day 4 11:19 | **REPEATING** | x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-11.93, 1.11, 32.32) (no waypoint reachable for 8s) — warping to (3.26, 0.01, 31.09) to rejoin patrol.
- day 5 05:23 | **DOOR-DEADLOCK** | OG4388 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 5 13:30 | **WARN** | [AiNavigation] PalletJack: stuck at (18.73, 1.11, 32.89) — walking out to (27.73, 0.09, 32.89) (8.07m to mesh).
- day 5 14:09 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-02' (5.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__92:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1125)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 07:23 | **DOOR-DEADLOCK** | OG5391 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 6 16:18 | **REPEATING** | x25 and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.
- day 6 17:53 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 27 task(s) wait
- day 6 22:21 | **WARN** | [OrderService] Order OG6389 closed with a STAGED pallet (36 cs) that never made the trailer - not billed, returning it to stock.
- day 7 04:55 | **REPEATING** | x25 and counting: [ReceivingTaskDriver] Employee_Brian Jackson: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered).
- day 7 07:53 | **DOOR-DEADLOCK** | OG6392 is Loading but door 2's truck has waited 90 min with no one loading it.
