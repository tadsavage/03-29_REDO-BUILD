# Bug log - block 1024

Every incident as it happened. Format: day hour:minute | category | what.

- day 8 00:00 | **FLOATING** | FLOATING A Chep @1E-2 bottom=1.30
- day 8 00:00 | **OVERLAP** | OVERLAP A Chep & A Chep @1E-2
- day 8 00:00 | **OVERLAP** | OVERLAP A Chep & A Chep @2D-5
- day 8 00:00 | **FLOATING** | FLOATING A Chep @1E-4 bottom=1.30
- day 8 00:00 | **OVERLAP** | OVERLAP A Chep & A Chep @1E-4
- day 8 00:00 | **OVERLAP** | OVERLAP A Chep & A Chep @2B-4
- day 8 00:00 | **OVERLAP** | OVERLAP A Chep & A Chep @2D-2
- day 8 00:00 | **OVERLAP** | OVERLAP A Chep & A Chep @2D-4
- day 8 00:00 | **FLOATING** | FLOATING A Chep @1E-1 bottom=1.30
- day 8 00:00 | **OVERLAP** | OVERLAP A Chep & A Chep @1E-1
- day 8 00:00 | **OVERLAP** | OVERLAP A Chep & A Chep @2D-3
- day 8 00:06 | **WARN** | [AiNavigation] RT_Full(Clone): stuck at (16.36, 1.11, 38.02) — walking out to (9.86, 1.22, 26.77) (12.00m to mesh).
- day 1 15:35 | **WARN** | [AiNavigation] Employee_Sarah Rogers: stuck at (7.69, 1.15, 23.96) — walking out to (14.21, 0.04, 13.14) (11.63m to mesh).
- day 4 23:05 | **WARN** | [AiNavigation] Employee_Sarah Rogers: stuck at (-2.00, 1.15, 26.10) — walking out to (-8.45, 0.04, 13.37) (13.27m to mesh).
- day 5 01:11 | **WARN** | [AiNavigation] Employee_Sarah Rogers: stuck at (6.67, 1.14, 26.16) — walking out to (-5.04, 1.16, 19.69) (12.38m to mesh).
- day 5 04:34 | **WARN** | [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2A and it never freed up — giving up and re-queueing.
- day 5 04:44 | **WARN** | [AiNavigation] ReachTruck: stuck at (15.49, 1.11, 33.60) — walking out to (15.49, 0.08, 42.67) (8.13m to mesh).
- day 5 04:49 | **ERROR** | [MHEBoundary] 'ReachTruck' fell below the floor at (15.49, 0.61, 41.83) (cell (61, 81)). Putting it back on its last inside spot (15.49, 0.97, 41.25). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  ```
- day 5 04:52 | **ERROR** | [MHEBoundary] 'ReachTruck' keeps driving out of the building (6 times in 10s, at (15.49, 0.59, 41.86)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  ```
- day 5 14:50 | **DOOR-DEADLOCK** | OG5793 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 7 04:53 | **REPEATING** | x25 and counting: [JackStall] 'bay 02-04' at (21.76, 1.11, 32.68) (target (-14.61, 1.11, 25.24)) remaining=Infinity pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(-0.27, 0.00, -0.49) stopped=False onMesh=True jackBlocked=False elapsed=1.2s reissues=0
- day 7 08:05 | **DOOR-DEADLOCK** | OG7796 is Staged but door 1's truck has waited 90 min with no one loading it.
