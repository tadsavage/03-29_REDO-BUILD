# Playtest block 65 — days 1–3

## Score: **19.8 (-13.5)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 2 late) | 10% | 0.0 (-50.0) |
| Fill rate | 10% | 0.0 (-100.0) |
| Throughput (0 plt vs target 24) | 10% | 0.0 (-20.8) |
| Revenue ($0) | not scored | 0.0 (-5.0) |
| Net Profit (-15,636) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 20.2 (+7.8) |
| Reputation (19 to 47) | 10% | 96.7 (-3.3) |

Orders shipped **0**, late/fined **2**, pallets shipped **0**. Cash $127,764 → $116,354 (-11,410). Stock at cost $200,106 → $195,880 (offsets stock bought in Net Profit).
Bot did: 0 contracts signed, 3 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went

Income **$0**. Expenses **$11,410** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|

| Expense | $ |
|---|---|
| Fines | 2,972 |
| Wages | 2,835 |
| Inventory | 2,400 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 2 x still being picked

- day 2: OG1779 (Puff & Stuff Pastries) was still being picked (0/417 cs). Fined $1,916. Lines 2, 417 cs, released=True.
- day 2: OG2624 (Sneaky Pete's Seafood) was still being picked (56/248 cs). Fined $1,056. Lines 27, 248 cs, released=True.

## Change since last block
- Fixed / no longer seen: **14**
  - ERROR|"IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - ERROR|[GridNav] 'Employee_Mark Peterson': 'staging lane #E line-up' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'staging lane #E wait' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'staging lane #E line-up' was interrupted # times - giving the leg up.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #).
  - WARN|[AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-c#ee#f#-a#d-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##d#b#-af#a## but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **3**
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##d#f##-# but the employee is #m away (max #m) — refusing to start receiving.

## Bugs found (13 distinct, 49 incidents)

| Incidents | Bug |
|---|---|
| 16 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 12 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 4 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 6 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 3 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | TRUCK-STALLED —  Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #) |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (#, #, #) - the next move would violate the # m vehicle clearance from PltJack_FULL(Clone). |
| 1 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##d#f##-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-6.91, 1.18, 43.01) — walking out to (-19.91, 1.13, 43.01) (12.00m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 80e63ccf-635d-43f4-8384-cc109ea9da32 but the employee is 4.9m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-1.45, 1.12, 37.95) — walking out to (10.26, 1.21, 44.44) (12.39m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone) cannot reach task position at (8.56, 1.24, 48.99) (path invalid) — abandoning the walk and handing the task back.
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-02-B0 anchor: NO NAVMESH PATH. anchor=(-39.14, 5.11, 49.29) → driveTarget=(-39.14, 1.17, 49.29), from=(-18.04, 1.11, 51.48), elapsed=32.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=21.5, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x00339] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1274 < ] 222.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 1): RT_Full(Clone) has a driver, hasn't moved in 360s while 44 task(s) it could take wait
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-18.63, 1.13, 50.75) — walking out to (-29.85, 1.12, 42.89) (12.69m to mesh).
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-10-B0 anchor: NO NAVMESH PATH. anchor=(-28.54, 5.11, 49.29) → driveTarget=(-28.54, 1.20, 49.29), from=(-18.40, 1.19, 51.16), elapsed=32.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=10.3, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 225.3s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 51/248.
- **WARN** (day 2): [AiNavigation] Employee_Shirley Edwards: stuck at (-19.73, 1.12, 49.97) — walking out to (-30.97, 1.12, 42.93) (12.26m to mesh).
- **REPEATING** (day 2): x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (-19.06, 1.11, 48.94) - the next move would violate the 1 m vehicle clearance from PltJack_FULL(Clone).
- **TRUCK-STALLED** (day 3): TRUCK-STALLED Truck→Outbound_Door1 state=DepartToApproach 1408s at (-11.3, 0.0, 27.9)

## Bot actions (first 400)
- d1 placed parked Inbound Anchor Point Foods day 1 block 26 door 3
- d1 booked Puff & Stuff Pastries day 2 block 42 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 8 pallet pick(s) / 2 reach truck(s): stock ready hour 18 + 26.5h of work)
- d2 placed parked Inbound Anchor Point Foods day 2 block 35 door 2
