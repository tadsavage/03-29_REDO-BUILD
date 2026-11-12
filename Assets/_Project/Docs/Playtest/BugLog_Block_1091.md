# Bug log - block 1091

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:08 | **WARN** | [AiNavigation] Employee_Charles Edwards: stuck at (12.14, 1.13, 36.75) — walking out to (-0.86, 1.11, 36.90) (12.00m to mesh).
- day 1 08:10 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (12.41, 1.13, 36.75) — walking out to (-0.59, 1.11, 36.90) (12.00m to mesh).
- day 1 08:10 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (12.92, 1.12, 36.85) — walking out to (6.42, 1.17, 48.11) (12.00m to mesh).
- day 1 08:12 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (12.14, 1.13, 36.75) — walking out to (-0.86, 1.11, 36.90) (12.00m to mesh).
- day 1 08:12 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (12.59, 1.12, 36.75) — walking out to (-0.41, 1.11, 36.90) (12.00m to mesh).
- day 1 08:13 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (12.76, 1.12, 36.75) — walking out to (-0.24, 1.11, 36.90) (12.00m to mesh).
- day 1 08:13 | **WARN** | [AiNavigation] Employee_Kelly Harmon: stuck at (12.14, 1.13, 36.75) — walking out to (-0.86, 1.11, 36.90) (12.00m to mesh).
- day 1 08:15 | **WARN** | [AiNavigation] Employee_Justin Waller: stuck at (12.14, 1.13, 36.75) — walking out to (-0.86, 1.11, 36.90) (12.00m to mesh).
- day 1 09:14 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-10.72, 1.12, 59.16) — walking out to (-10.72, 1.21, 46.16) (12.00m to mesh).
- day 1 09:15 | **WARN** | [AiNavigation] Employee_Mark Peterson: stuck at (-10.72, 1.12, 59.16) — walking out to (-10.72, 1.21, 46.16) (12.00m to mesh).
- day 1 09:17 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (4.87, 1.11, 56.00) — walking out to (4.87, 1.25, 43.00) (12.00m to mesh).
- day 1 09:18 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-10.72, 1.12, 59.15) — walking out to (-10.72, 1.21, 46.15) (12.00m to mesh).
- day 1 09:20 | **WARN** | [AiNavigation] Employee_Larry Morgan: stuck at (-7.03, 1.12, 59.10) — walking out to (-20.03, 0.04, 58.81) (12.00m to mesh).
- day 1 09:55 | **WARN** | [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (-17.59, 1.23, 42.34) (path invalid) — abandoning the walk and handing the task back.
- day 1 10:16 | **WARN** | [AiNavigation] Employee_Shirley Edwards: stuck at (-14.26, 1.16, 44.10) — walking out to (-27.26, 1.12, 43.41) (12.02m to mesh).
- day 1 10:30 | **STALE-ASSIGNED** | STALE-ASSIGNED Putaway Putaway pallet [8210733071] staged at 2A-5 456s
- day 1 10:30 | **STALE-ASSIGNED** | STALE-ASSIGNED Putaway Putaway pallet [4587076266] staged at 1A-2 448s
- day 1 10:40 | **WARN** | [AiNavigation] Employee_Mary Young: stuck at (-11.42, 1.21, 45.66) — walking out to (-24.42, 1.12, 46.61) (12.03m to mesh).
- day 1 10:41 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet cef7ddfe-3ed3-4836-b5ac-e4cf5df34ecd but the employee is 4.7m away (max 4.5m) — refusing to start receiving.
- day 1 11:08 | **WARN** | [AiNavigation] Employee_Douglas Garcia: stuck at (5.12, 1.19, 47.81) — walking out to (-6.36, 1.16, 41.32) (12.18m to mesh).
- day 1 11:21 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-07 (aisle entry)' (8.1m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__197:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 1 11:21 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 01-07' - driving straight to the stop instead.
- day 1 11:22 | **WARN** | [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 18/248.
- day 1 11:46 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet eeec1e93-673f-44a7-8694-00f678c97d0e but the employee is 4.7m away (max 4.5m) — refusing to start receiving.
- day 1 11:53 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet c642f786-8c5c-40c3-8d4f-8629163d29ed but the employee is 4.8m away (max 4.5m) — refusing to start receiving.
- day 1 12:05 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-07' (19.8m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__197:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
