# Playtest block 72 — days 1–3

## Score: **35.6 (+1.4)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 1 late) | 10% | 66.7 (0.0) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (9 plt vs target 24) | 10% | 37.5 (0.0) |
| Revenue ($27,402) | not scored | 100.0 (0.0) |
| Net Profit (-25,489) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 10.4 (+2.8) |
| Reputation (19 to 59) | 10% | 100.0 (0.0) |

Orders shipped **2**, late/fined **1**, pallets shipped **9**. Cash $127,764 → $131,283 (+3,519). Stock at cost $200,106 → $171,098 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went

Income **$27,742**. Expenses **$24,223** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 26,610 |
| Handling | 792 |
| Floor | 340 |

| Expense | $ |
|---|---|
| General | 10,260 |
| Inventory | 4,224 |
| Fines | 3,068 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Floor | 380 |
| Transportation | 253 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x loaded but the truck had not left

- day 2: OG2821 (Neon Nectar Juice Co.) was loaded but the truck had not left. Fined $3,068. Lines 2, 479 cs, released=True.

## Change since last block
- Fixed / no longer seen: **20**
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#] <Load Available> 
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it. | lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##e##f#-a#bf-#/Staged/y#, OTHER ##e##f#-a#bf-#/Staged/y#] <Dig Available> lane #B: [#: OTHER #-a#f#a##b###/Staged/y#] <Load Available> 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##e##f#-a#bf-#/Staged/y#, OTHER ##e##f#-a#bf-#/Staged/y#] <Dig Available> lane #B: [#: OTHER #-a#f#a##b###/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] <Dig Available> 
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#] <Dig Available> 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##e##f#-a#bf-#/Staged/y#, OTHER ##e##f#-a#bf-#/Staged/y#] [#: own/Staged/y#] <Dig Available> lane #B: [#: OTHER #-a#f#a##b###/Staged/y#] 
  - ERROR|"SetDestination" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry, passing lane) (route)' (#m short) - giving up on this stop.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' replenish: → reserve ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__#.MoveNext () [#x#e] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane #D exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry, passing lane)' (#m short) - giving up on this stop.
  - ERROR|[MHEBoundary] 'PltJack_FULL(Clone)' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' cross-door load: to trailer: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s).
- New this block: **20**
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: [#: own/Staged/y#] lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#, OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#] 
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry) (route)' (#m short) - giving up on this stop.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__#.MoveNext () [#x#e] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (loop in)' ((#, #, #)) after #s.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (aisle entry)' ((#, #, #)) after #s.
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' blockers: back to source lane: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=#, reissues=#, lastSeekCancelledBy=[at AiNavigation.Update () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago]
  - WARN|[OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#).
  - REPEATING|x# and counting: [JackStall] 'bay ## (aisle entry)' at (#, #, #) (target (#, #, #)) remaining=# pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=False onMesh=True jackBlocked=False elapsed=#s reissues=# turnNeeded=#deg avoidPrio=# vel=# 
  - REPEATING|x# and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.
  - WARN|[AiNavigation] Employee_Justin Waller: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Charles Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##fe##d#-bbaa## but the employee is #m away (max #m) — refusing to start receiving.

## Bugs found (38 distinct, 69 incidents)

| Incidents | Bug |
|---|---|
| 5 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 7 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop. |
| 2 | ERROR — "GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh. |
| 2 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 5 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #B: [#: own/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #C: [#: own/Staged/y#, own/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #D: [#: own/Staged/y#] lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#, OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#]  |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry) (route)' (#m short) - giving up on this stop. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__#.MoveNext () [#x#e] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 3 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 3 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (loop in)' ((#, #, #)) after #s. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (aisle entry)' ((#, #, #)) after #s. |
| 1 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' blockers: back to source lane: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=#, reissues=#, lastSeekCancelledBy=[at AiNavigation.Update () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago] |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 2 | WARN — [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | REPEATING — x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#). |
| 1 | REPEATING — x# and counting: [JackStall] 'bay ## (aisle entry)' at (#, #, #) (target (#, #, #)) remaining=# pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=False onMesh=True jackBlocked=False elapsed=#s reissues=# turnNeeded=#deg avoidPrio=# vel=#  |
| 1 | REPEATING — x# and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway. |
| 2 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Justin Waller: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Charles Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##fe##d#-bbaa## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Douglas Garcia cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-d##a##d## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##e##ab#f## (Neon Nectar Juice Co.) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-3.64, 1.12, 59.14) — walking out to (-10.14, 1.19, 47.88) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-3.64, 1.12, 59.16) — walking out to (-10.14, 1.18, 47.90) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Justin Waller: stuck at (5.62, 1.16, 49.86) — walking out to (-6.30, 1.16, 43.38) (12.56m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Charles Edwards: stuck at (6.19, 1.16, 49.89) — walking out to (-6.30, 1.16, 43.43) (13.07m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-38.22, 1.11, 42.34).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-26.70, 1.11, 43.21) — walking out to (-38.22, 1.11, 42.34) (11.55m to mesh).
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-04 (aisle entry)' (13.0m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-04' - driving straight to the stop instead.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 6489311a-5fe0-42d3-bbaa-3d951a3a7831 but the employee is 4.6m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] RT_Full(Clone) cannot reach task position at (-28.54, 1.20, 49.29) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia cannot reach task position at (10.31, 2.40, 44.34) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet ec015986-d710-41a9-820d-5ccd910bc337 but the employee is 15.7m away (max 4.5m) — refusing to start receiving.
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-08 (aisle entry) (route)' (20.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-05-B0 anchor: NO NAVMESH PATH. anchor=(-24.56, 5.11, 40.71) → driveTarget=(-24.56, 1.20, 40.71), from=(-20.31, 1.11, 47.46), elapsed=31.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=8.9, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [0x00025] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2375 < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__161.MoveNext () [0x0010e] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:3060 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 65.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-10.84, 1.13, 56.59) — walking out to (0.41, 1.16, 50.09) (12.00m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 02-03' - driving straight to the stop instead.
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-30.57, 1.12, 46.54) — walking out to (-37.05, 0.04, 34.58) (12.60m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 232/248.
- **REPEATING** (day 1): x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- **VEHICLE-STATIONARY** (day 2): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 20 task(s) it could take wait
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-02 (loop in)' (8.8m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 9.0m short of 'bay 01-01 (aisle entry)' - re-issuing the drive (1/6).
- **REPEATING** (day 2): x25 and counting: [JackStall] 'bay 01-01 (aisle entry)' at (-7.53, 1.25, 45.66) (target (-16.26, 1.21, 39.81)) remaining=13.76 pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(-0.58, 0.00, -2.17) stopped=False onMesh=True jackBlocked=False elapsed=3.7s reissues=0 turnNeeded=0deg avoidPrio=40 vel=0.00 
- **WARN** (day 2): [OrderSelectionTaskDriver] Order e80b74d2-49e2-45ab-952f-7503ec4d9e76 (Neon Nectar Juice Co.) has no reachable pick location for its remaining line item(s) — staging short at 30/45.
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (-16.26, 1.24, 42.34) (path invalid) — abandoning the walk and handing the task back.
- **REPEATING** (day 2): x25 and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone): stuck at (-15.69, 1.11, 40.05) — walking out to (-17.08, 1.16, 40.62) (12.39m to mesh).
- **ERROR** (day 2): "GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_remainingDistance ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__238:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2189)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__238:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2189)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] DS_FULL(Clone): stuck at (3.57, 1.11, 39.97) — walking out to (3.00, 1.18, 41.35) (12.00m to mesh).
- **DOOR-DEADLOCK** (day 2): OG3675 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2B: [1: own/Staged/y1.2] 
- **DOOR-DEADLOCK** (day 2): OG3678 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2C: [1: own/Staged/y2.5, own/Staged/y1.2] 
- **DOOR-DEADLOCK** (day 2): OG3681 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2D: [1: own/Staged/y1.2] lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.6, OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: own/Staged/y1.2] 
- **WARN** (day 2): [AiNavigation] Employee_Mark Peterson: stuck at (-0.35, 1.13, 53.77) — walking out to (0.15, 1.16, 40.00) (12.78m to mesh).
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack did not reach 'bay 02-05 (loop in)' ((-16.39, 1.16, 42.25)) after 5.2s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__238:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2291)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack did not reach 'bay 01-02 (aisle entry)' ((-17.59, 1.16, 42.34)) after 3.3s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__238:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2291)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] 'DS_FULL(Clone)' blockers: back to source lane: never arrived at (4.59, 1.16, 39.19) (elapsed 30.0s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=5.9, reissues=0, lastSeekCancelledBy=[at AiNavigation.Update () [0x00352] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1284 < ] 37.5s ago]
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__32:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:756)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```

## Bot actions (first 400)
- d1 signed bulk BULK_Rooted Robotics_D1_0
