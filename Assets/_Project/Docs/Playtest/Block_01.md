# Playtest block 1 — days 1–8

## Score: **9.8** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 11 late) | 15% | 0.0 |
| Fill rate | 10% | 0.0 |
| Throughput (0 plt vs target 84) | 15% | 0.0 |
| Revenue ($0) | 15% | 0.0 |
| Profit (cash -127,649) | 10% | 0.0 |
| Cash health (floor $10,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 9.4 |
| Reputation (0 to 0) | 5% | 50.0 |

Orders shipped **0**, late/fined **11**, pallets shipped **0**. Cash $139,038 → $11,389 (-127,649).
Bot did: 6 contracts signed, 36 dock bookings, 13 orders released, 17 POs, 5 rush POs.

## Bugs found (20 distinct, 64 incidents)

| Incidents | Bug |
|---|---|
| 10 | ERROR — [ReachTruckOperator] 'ReachTruck' deliver: → location ### anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 7 | ERROR — [ReachTruckOperator] 'ReachTruck' putaway: → lane #C exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 7 | ERROR — [ReachTruckOperator] 'ReachTruck' deliver: → location ### anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 7 | ERROR — [ReachTruckOperator] 'ReachTruck' putaway: → lane #B exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 5 | ERROR — [ReachTruckOperator] 'ReachTruck' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 3 | ERROR — [ReachTruckOperator] 'ReachTruck' pallet pick: → source lane #B exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 3 | ERROR — [TrailerLoad] 'Dockstocker' dig: → #C far end: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. |
| 4 | NO-TASK —  lane pallet #B# sku=# load=# |
| 2 | ERROR — [TrailerLoad] 'Dockstocker' dig: back to source lane: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. |
| 2 | ERROR — [ReachTruckOperator] 'ReachTruck' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 5 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [ReachTruckOperator] 'ReachTruck' pallet pick: → source lane #C exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [ReachTruckOperator] 'ReachTruck' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [TrailerLoad] 'Dockstocker' dig: return to source lane: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. |
| 1 | ERROR — [ReachTruckOperator] 'ReachTruck' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [ReachTruckOperator] 'ReachTruck' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) wait |
| 1 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 1 | VEHICLE-STATIONARY — Dockstocker has a driver, hasn't moved in #s while # task(s) wait |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Ruth Rodriguez' as its operator though nobody is on it or running it - clearing the stale link. |

## First sightings (with stack traces)

- **ERROR** (day 1): [ReachTruckOperator] 'ReachTruck' deliver: → location 01-05-01 anchor: NO NAVMESH PATH. anchor=(-8.66, 1.11, 19.51) → driveTarget=(-8.66, 1.11, 19.51), from=(5.64, 1.16, 24.80), elapsed=1.5s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 1): PalletJack has a driver, hasn't moved in 255s while 5 task(s) wait
- **ERROR** (day 1): [ReachTruckOperator] 'ReachTruck' pallet pick: → source lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 1.11, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(12.05, 1.16, 27.44), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **NO-TASK** (day 1): NO-TASK lane pallet 1B-3 sku=182491 load=5594936484
- **WARN** (day 1): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Ruth Rodriguez' as its operator though nobody is on it or running it - clearing the stale link.
- **ERROR** (day 1): [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 1A: NO NAVMESH PATH. anchor=(4.59, 1.09, 27.79) → driveTarget=(4.59, 1.13, 27.79), from=(12.83, 1.16, 27.79), elapsed=1.1s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): [ReachTruckOperator] 'ReachTruck' pallet pick: → source lane 2C exit: NO NAVMESH PATH. anchor=(17.84, 1.11, 27.79) → driveTarget=(17.84, 1.12, 27.79), from=(13.88, 1.11, 30.95), elapsed=7.9s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): [TrailerLoad] 'Dockstocker' dig: → 1C far end: never arrived at (7.24, 1.12, 27.79) (elapsed 30.0s) — refusing to straight-line through pallets.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__20:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:713)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(15.19, 1.09, 27.79) → driveTarget=(15.19, 1.13, 27.79), from=(19.39, 1.16, 26.53), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): [ReachTruckOperator] 'ReachTruck' putaway: → lane 1C exit: NO NAVMESH PATH. anchor=(7.24, 1.11, 27.79) → driveTarget=(7.24, 1.12, 27.79), from=(6.09, 1.11, 31.28), elapsed=1.9s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] ReachTruck: stuck at (8.18, 1.11, 29.55) — walking out to (8.18, 0.08, 42.67) (12.16m to mesh).
- **ERROR** (day 1): [TrailerLoad] 'Dockstocker' dig: back to source lane: never arrived at (5.91, 1.12, 17.99) (elapsed 30.0s) — refusing to straight-line through pallets.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__20:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:713)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [ReachTruckOperator] 'ReachTruck' deliver: → location 01-02-A1 anchor: NO NAVMESH PATH. anchor=(-3.36, 3.11, 18.81) → driveTarget=(-3.36, 1.11, 18.81), from=(4.84, 1.16, 21.78), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] 'Dockstocker' dig: return to source lane: never arrived at (5.91, 1.12, 17.99) (elapsed 1.3s) — refusing to straight-line through pallets.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__20:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:713)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [ReachTruckOperator] 'ReachTruck' deliver: → location 01-02-B1 anchor: NO NAVMESH PATH. anchor=(-3.36, 5.11, 18.81) → driveTarget=(-3.36, 1.11, 18.81), from=(8.91, 1.11, 27.78), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [ReachTruckOperator] 'ReachTruck' deliver: → location 02-04-00 anchor: NO NAVMESH PATH. anchor=(-16.61, 1.11, 25.44) → driveTarget=(-16.61, 1.11, 25.44), from=(7.24, 1.11, 27.79), elapsed=30.0s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [ReachTruckOperator] 'ReachTruck' putaway: → lane 2B exit: NO NAVMESH PATH. anchor=(16.51, 1.11, 27.79) → driveTarget=(16.51, 1.12, 27.79), from=(14.86, 1.16, 26.44), elapsed=7.0s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 3): [ReachTruckOperator] 'ReachTruck' deliver: → location 01-10-A0 anchor: NO NAVMESH PATH. anchor=(-12.64, 3.11, 18.81) → driveTarget=(-12.64, 1.11, 18.81), from=(-10.69, 1.11, 18.83), elapsed=7.9s, timedOut=False, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__117:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2560)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **TRUCK-OVERLAP** (day 5): TRUCK-OVERLAP Truck→PO_IG5390 & Truck→PO_IG5389 at (-7.6, 0.0, -51.1)
- **VEHICLE-STATIONARY** (day 6): Dockstocker has a driver, hasn't moved in 270s while 2 task(s) wait

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 released 2 order(s)
- d1 PO IG1372 Homestead Market Co.: 5 plt $4,752
- d1 sent Robert Myers to the ReachTruck
- d1 placed parked Inbound Homestead Market Co. day 1 block 5 door 1
- d1 PO IG1373 Bulk Basin Foods: 4 plt $2,306
- d1 sent Robert Myers to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 placed parked Inbound Bulk Basin Foods day 1 block 5 door 2
- d1 PO IG1374 Blue Ridge Provisions: 4 plt $5,884
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 6 door 1
- d1 sent Robert Myers to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 signed bulk BULK_Honest Harvest Rice_D1_0
- d1 booked Honest Harvest Rice day 1 block 6 door 2
- d1 released 1 order(s)
- d1 PO IG1375 Homestead Market Co.: 4 plt $2,944
- d1 sent Robert Myers to the ReachTruck
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 placed parked Inbound Homestead Market Co. day 1 block 7 door 1
- d1 sent Robert Myers to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 PO IG1376 Bulk Basin Foods: 3 plt $1,944
- d1 sent Robert Myers to the ReachTruck
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 placed parked Inbound Bulk Basin Foods day 1 block 7 door 2
- d1 PO IG1377 Blue Ridge Provisions: 3 plt $3,060
- d1 sent Robert Myers to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 8 door 1
- d1 sent Robert Myers to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Robert Myers to the ReachTruck
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Robert Myers to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Robert Myers to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Robert Myers to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Robert Myers to the ReachTruck
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Robert Myers to the ReachTruck
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Robert Myers to the ReachTruck
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Robert Myers to the ReachTruck
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Robert Myers to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Robert Myers to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Robert Myers to the ReachTruck
- d1 sent Julie Sanchez to the ReachTruck
- d1 sent Robert Myers to the ReachTruck
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Patrick Gutierrez to the Dockstocker
- d1 sent Deborah Harmon to the Dockstocker
- d1 sent Julie Sanchez to the ReachTruck
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 RUSH PO IG2378 Rush Courier: 4 plt $12,266
- d2 sent Robert Myers to the ReachTruck
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 placed parked Inbound Rush Courier day 2 block 1 door 1
- d2 PO IG2379 Fairweather Trading Co.: 8 plt $8,495
- d2 sent Robert Myers to the ReachTruck
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 1 door 2
- d2 PO IG2380 Anchor Point Foods: 6 plt $2,352
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 placed parked Inbound Anchor Point Foods day 2 block 2 door 1
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 PO IG2381 Bulk Basin Foods: 2 plt $560
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 placed parked Inbound Bulk Basin Foods day 2 block 2 door 2
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Robert Myers to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Deborah Harmon to the Dockstocker
- d2 signed bulk BULK_Fizz-Pop Soda Works_D2_0
- d2 signed bulk BULK_Crunch-Time Cereals_D2_1
- d2 booked Crunch-Time Cereals day 2 block 9 door 1
- d2 booked Fizz-Pop Soda Works day 2 block 9 door 2
- d2 released 2 order(s)
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Deborah Harmon to the Dockstocker
- d2 released 1 order(s) (waiting for lane)
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Julie Sanchez to the ReachTruck
- d2 sent Patrick Gutierrez to the Dockstocker
- d2 sent Robert Myers to the ReachTruck
- d2 sent Deborah Harmon to the Dockstocker
- d2 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 RUSH PO IG3382 Rush Courier: 4 plt $10,360
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 placed parked Inbound Rush Courier day 3 block 1 door 1
- d3 PO IG3383 Backdoor Bill's Overstock: 2 plt $672
- d3 sent Robert Myers to the ReachTruck
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 1 door 2
- d3 PO IG3384 Cornerstone Staples: 11 plt $2,378
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 placed parked Inbound Cornerstone Staples day 3 block 2 door 1
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 PO IG3385 Homestead Market Co.: 2 plt $1,701
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 placed parked Inbound Homestead Market Co. day 3 block 2 door 2
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 PO IG3386 Homestead Market Co.: 1 plt $608
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 placed parked Inbound Homestead Market Co. day 3 block 3 door 1
- d3 sent Robert Myers to the ReachTruck
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 signed bulk BULK_The Cracker Barrel Caravan_D3_0
- d3 signed bulk BULK_Rooted Robotics_D3_1
- d3 booked Rooted Robotics day 3 block 6 door 1
- d3 booked The Cracker Barrel Caravan day 3 block 6 door 2
- d3 released 2 order(s) (waiting for lane)
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Robert Myers to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Robert Myers to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 released 1 order(s)
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Patrick Gutierrez to the Dockstocker
- d3 sent Robert Myers to the ReachTruck
- d3 sent Deborah Harmon to the Dockstocker
- d3 sent Julie Sanchez to the ReachTruck
