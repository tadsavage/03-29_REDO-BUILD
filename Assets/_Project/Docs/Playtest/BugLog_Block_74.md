# Bug log - block 74

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:30 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-4.89, 1.12, 59.14) — walking out to (1.61, 1.19, 47.88) (12.00m to mesh).
- day 1 08:32 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-4.89, 1.12, 59.16) — walking out to (1.61, 1.19, 47.90) (12.00m to mesh).
- day 1 10:21 | **WARN** | [AiNavigation] RT_Full(Clone) cannot reach task position at (-37.81, 1.18, 49.29) (path invalid) — abandoning the walk and handing the task back.
- day 1 11:25 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 181s while 31 task(s) it could take wait
- day 1 11:25 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 181s while 28 task(s) it could take wait
- day 1 11:36 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 1B wait' (4.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__243:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 11:39 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-12.34, 1.19, 47.81) — walking out to (-1.11, 1.16, 40.45) (12.42m to mesh).
- day 1 12:06 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (-10.29, 1.15, 50.30) — walking out to (2.22, 1.16, 43.84) (13.08m to mesh).
- day 1 12:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 180s while 28 task(s) it could take wait
- day 1 12:47 | **WARN** | [AiNavigation] Employee_Mary Young cannot reach task position at (-3.79, 1.20, 47.67) (path invalid) — abandoning the walk and handing the task back.
- day 1 12:47 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet f263cc46-db60-4afb-afad-556981239d97 but the employee is 6.4m away (max 4.5m) — refusing to start receiving.
- day 1 13:55 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 225s while 28 task(s) it could take wait
- day 1 13:59 | **WARN** | [AiNavigation] PltJack_FULL(Clone): stuck at (-9.52, 1.11, 51.91) — walking out to (-8.95, 1.20, 53.30) (12.02m to mesh).
- day 1 15:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 180s while 26 task(s) it could take wait
- day 1 16:25 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 225s while 34 task(s) it could take wait
- day 1 16:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 180s while 26 task(s) it could take wait
- day 1 16:42 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-19.62, 1.14, 46.24) — walking out to (-26.11, 1.08, 35.17) (12.34m to mesh).
- day 1 17:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 180s while 25 task(s) it could take wait
- day 1 18:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 180s while 25 task(s) it could take wait
- day 1 19:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 180s while 36 task(s) it could take wait
- day 1 19:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 180s while 25 task(s) it could take wait
- day 1 20:55 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 225s while 36 task(s) it could take wait
- day 1 20:55 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 180s while 24 task(s) it could take wait
- day 1 21:49 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-9.05, 1.12, 59.25) — walking out to (3.95, 1.18, 59.25) (12.00m to mesh).
- day 1 22:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 225s while 36 task(s) it could take wait
- day 1 22:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 225s while 25 task(s) it could take wait
- day 1 22:19 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 177/248.
- day 1 23:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 180s while 36 task(s) it could take wait
- day 1 23:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 180s while 25 task(s) it could take wait
- day 2 00:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 180s while 36 task(s) it could take wait
- day 2 01:10 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 180s while 36 task(s) it could take wait
- day 2 01:40 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 225s while 25 task(s) it could take wait
- day 2 02:25 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 225s while 36 task(s) it could take wait
- day 2 03:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 225s while 36 task(s) it could take wait
- day 2 04:10 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 180s while 25 task(s) it could take wait
- day 2 04:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 180s while 36 task(s) it could take wait
- day 2 05:13 | **WARN** | [InventoryService] Lane 1B lock was held for over 120s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
- day 2 05:20 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-12.34, 1.12, 59.19) — walking out to (-12.34, 1.19, 46.19) (12.00m to mesh).
- day 2 05:36 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-2.75, 1.14, 55.35) — walking out to (-14.01, 1.16, 48.85) (12.00m to mesh).
- day 2 05:40 | **VEHICLE-STATIONARY** | RT_Full(Clone) has a driver, hasn't moved in 180s while 36 task(s) it could take wait
- day 2 05:47 | **REPEATING** | x25 and counting: [EquipmentTraffic] 'RT_Full(Clone)' is blocked at (-21.91, 1.11, 48.27): blocked by RT_Full(Clone) for 30s with no safe way round. No safe way to move - staying put.
