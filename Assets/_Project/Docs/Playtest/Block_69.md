# Playtest block 69 — days 1–3

## Score: **19.2 (-14.7)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 1 late) | 10% | 0.0 (-50.0) |
| Fill rate | 10% | 0.0 (-100.0) |
| Throughput (0 plt vs target 24) | 10% | 0.0 (-16.7) |
| Revenue ($0) | not scored | 0.0 (-66.4) |
| Net Profit (-16,379) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 18.5 (+4.0) |
| Reputation (19 to 57) | 10% | 100.0 (0.0) |

Orders shipped **0**, late/fined **1**, pallets shipped **0**. Cash $127,764 → $119,230 (-8,534). Stock at cost $200,106 → $192,261 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 1 dock bookings, 1 orders released, 0 POs, 0 rush POs.

## Where the money went

Income **$0**. Expenses **$8,534** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|

| Expense | $ |
|---|---|
| Wages | 2,835 |
| Maintenance | 1,618 |
| Inventory | 1,440 |
| Fines | 1,056 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x still being picked

- day 2: OG2624 (Sneaky Pete's Seafood) was still being picked (248/248 cs). Fined $1,056. Lines 27, 248 cs, released=True.

## Change since last block
- Fixed / no longer seen: **20**
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##fda#b#c#-#/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##fda#b#c#-#/Staged/y#, OTHER ##fda#b#c#-#/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: stock #-a#e#ca-b##e#cde#] [#: stock #-c#d##-afab##, stock ##fe#cb-a#-#] [#: stock #-e#d#-a#d###, stock ###b#f#-#] lane #A: [#: OTHER ##c##bd-ae#e##/Staged/y#, OTHER ##c##bd-ae#e##/Staged/y#] [#: own/Staged/y#, own/Staged/y#] 
  - ERROR|"SetDestination" can only be called on an active agent that has been placed on a NavMesh.
  - REPEATING|x# and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.
  - REPEATING|x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress staging order OG# for # game minutes - staying aboard the loaded jack (StagingHold).
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' made no progress staging order OG# for # game minutes - staying aboard the loaded jack (StagingHold).
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Shirley Edwards' made no progress staging order OG# for # game minutes - staying aboard the loaded jack (StagingHold).
  - WARN|[InventoryService] Lane #A lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
  - REPEATING|x# and counting: "SetDestination" can only be called on an active agent that has been placed on a NavMesh.
  - WARN|[AiNavigation] Employee_Douglas Garcia cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] Employee_Mary Young cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] Employee_Justin Waller: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **7**
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - OVERLAP| A Chep & Pallet_#_Slot#_Tier# @(#, #)
  - ERROR|"IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__#.MoveNext () [#x#e] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##f##f### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.

## Bugs found (17 distinct, 51 incidents)

| Incidents | Bug |
|---|---|
| 16 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 16 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 4 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #E: [#: own/Staged/y#, own/Staged/y#]  |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #B wait' (#m short) - giving up on this stop. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 1 | OVERLAP —  A Chep & Pallet_#_Slot#_Tier# @(#, #) |
| 1 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__#.MoveNext () [#x#e] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | TRUCK-STALLED —  Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #) |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##f##f### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-8.89, 1.11, 59.00) — walking out to (-7.50, 1.16, 59.00) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-8.12, 1.12, 59.25) — walking out to (-8.12, 1.20, 46.25) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone) cannot reach task position at (-4.69, 1.24, 48.99) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 12273cb3-0f19-453f-9560-14d15e376b4b but the employee is 19.6m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-8.78, 1.14, 55.19) — walking out to (-2.28, 1.22, 43.93) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (9.46, 1.20, 46.79) — walking out to (9.46, 1.12, 59.25) (12.00m to mesh).
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 1B wait' (4.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__226:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-02 (aisle entry)' (11.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__226:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-02' - driving straight to the stop instead.
- **OVERLAP** (day 1): OVERLAP A Chep & Pallet_01_Slot1_Tier0 @(33, 83)  <<HISTORY: d1 10:57 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:37 Replenish task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 11:57 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:59 moved (33, 83)->(-1, -1) by InventoryService.MovePallet | d1 12:03 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 12:03 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 09:55 Putaway task created (STG1D-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 09:55 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 11:38 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:42 moved (43, 85)->(-1, -1) by InventoryService.MovePallet | d1 11:47 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 11:47 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-32.22, 1.11, 42.34).
- **ERROR** (day 1): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 1): RT_Full(Clone) has a driver, hasn't moved in 360s while 29 task(s) it could take wait
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 23 task(s) it could take wait
- **ERROR** (day 1): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-14-A1 anchor: NO NAVMESH PATH. anchor=(-21.91, 3.11, 49.29) → driveTarget=(-21.91, 1.21, 49.29), from=(-17.63, 1.11, 47.06), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=4.8, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [0x00025] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2375 < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__161.MoveNext () [0x0010e] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:3060 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 7.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **DOOR-DEADLOCK** (day 2): OG3625 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3E: [1: own/Staged/y2.6, own/Staged/y1.2] 
- **TRUCK-STALLED** (day 3): TRUCK-STALLED Truck→Outbound_Door1 state=DepartToApproach 1410s at (-11.3, 0.0, 27.9)

## Bot actions (first 400)
- d1 signed bulk BULK_Crunch-Time Cereals_D1_0
- d1 booked Crunch-Time Cereals day 2 block 24 door 2 (pallet-only, 1 pallet pick(s) / 2 reach truck(s): stock ready hour 10 + 25.5h of work)
- d1 released 1 order(s): OG1806 (trailer 25.8 h away; 25.5 h work + 2 h)
