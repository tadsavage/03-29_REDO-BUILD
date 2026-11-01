# Bug log - block 21

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 13:14 | **WARN** | [AiNavigation] Employee_Judith Harmon: stuck at (-1.57, 1.16, 21.31) — walking out to (-12.79, 1.08, 14.00) (12.56m to mesh).
- day 1 14:04 | **WARN** | [AiNavigation] Dockstocker: stuck at (18.84, 1.11, 37.56) — walking out to (21.84, 0.08, 42.76) (5.11m to mesh).
- day 1 14:08 | **ERROR** | [MHEBoundary] 'Dockstocker' fell below the floor at (21.52, 0.41, 42.20) (cell (66, 81)). Putting it back on its last inside spot (21.19, 0.75, 41.63). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 1 14:10 | **ERROR** | [MHEBoundary] 'Dockstocker' keeps driving out of the building (6 times in 10s, at (21.64, 0.00, 42.43)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  ```
- day 2 19:46 | **ERROR** | [ReceiverReceivingWorkflow] No master record for pallet 1a9938d6-a12f-4a35-9ffc-cf21394ab376
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.ReceiverReceivingWorkflow:BeginReceivingAt (GameCore.Labor.WorkTask,UnityEngine.Transform,single) (at Assets/_Project/Scripts/1. FSM/Core/Labor/ReceiverReceivingWorkflow.cs:247)
  GameCore.Actors.ReceivingTaskDriver:OnArrivedAtStandPosition (GameCore.Labor.WorkTask,UnityEngine.Transform,int) (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:213)
  GameCore.Actors.ReceivingTaskDriver/<>c__DisplayClass22_0:<OnArrivedAtStandPosition>b__0 () (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:226)
  AiNavigation:Update () (at Assets/_Project/Scripts/Actors/AiNavigation.cs:966)
  ```
- day 3 08:26 | **DOOR-DEADLOCK** | OG2705 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 3 11:11 | **DOOR-DEADLOCK** | OG2705 is Staged but door 1's truck has waited 255 min with no one loading it.
- day 3 17:25 | **REPEATING** | x25 and counting: [EquipmentBattery] Dockstocker: not sliding 41.2 m by hand - that is a navigation job. Trying the path again.
- day 3 17:26 | **DOOR-DEADLOCK** | OG2705 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 3 17:59 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 3A and it never freed up — giving up and re-queueing.
- day 3 18:21 | **WARN** | [AiNavigation] ReachTruck: stuck at (15.49, 1.11, 33.60) — walking out to (15.49, 0.08, 42.67) (8.13m to mesh).
- day 3 18:27 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (15.49, 0.00, 42.67) (cell (61, 82)). Putting it back on its last inside spot (15.49, 0.69, 41.69). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 3 18:29 | **ERROR** | [MHEBoundary] 'ReachTruck' keeps driving out of the building (6 times in 10s, at (15.49, 0.08, 42.04)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  ```
- day 3 18:41 | **DOOR-DEADLOCK** | OG2705 is Loading but door 1's truck has waited 165 min with no one loading it.
- day 3 20:41 | **DOOR-DEADLOCK** | OG2705 is Staged but door 1's truck has waited 285 min with no one loading it.
- day 3 21:26 | **DOOR-DEADLOCK** | OG3707 is Loading but door 2's truck has waited 90 min with no one loading it.
- day 3 23:41 | **DOOR-DEADLOCK** | OG2705 is Staged but door 1's truck has waited 465 min with no one loading it.
- day 4 01:11 | **DOOR-DEADLOCK** | OG2705 is Loading but door 1's truck has waited 555 min with no one loading it.
- day 4 06:11 | **DOOR-DEADLOCK** | OG2705 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 4 07:41 | **DOOR-DEADLOCK** | OG2705 is Staged but door 1's truck has waited 180 min with no one loading it.
- day 4 08:26 | **DOOR-DEADLOCK** | OG2705 is Loading but door 1's truck has waited 225 min with no one loading it.
- day 4 10:41 | **DOOR-DEADLOCK** | OG2705 is Staged but door 1's truck has waited 360 min with no one loading it.
- day 4 13:56 | **DOOR-DEADLOCK** | OG2705 is Staged but door 1's truck has waited 555 min with no one loading it.
- day 5 03:56 | **DOOR-DEADLOCK** | OG2705 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 5 04:11 | **DOOR-DEADLOCK** | OG2705 is Loading but door 1's truck has waited 105 min with no one loading it.
