# Playtest block 71 — days 1–3

## Score: **34.2 (-12.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 1 late) | 10% | 66.7 (-33.3) |
| Fill rate | 10% | 100.0 (+5.1) |
| Throughput (9 plt vs target 24) | 10% | 37.5 (-62.5) |
| Revenue ($27,402) | not scored | 100.0 (0.0) |
| Net Profit (-43,788) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 7.6 (-7.7) |
| Reputation (19 to 71) | 10% | 100.0 (0.0) |

Orders shipped **2**, late/fined **1**, pallets shipped **9**. Cash $127,764 → $124,852 (-2,912). Stock at cost $200,106 → $159,230 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 2 dock bookings, 1 orders released, 1 POs, 0 rush POs.

## Where the money went

Income **$27,402**. Expenses **$30,314** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 26,610 |
| Handling | 792 |

| Expense | $ |
|---|---|
| Inventory | 16,500 |
| Fines | 7,776 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x never released

- day 2: OG1815 (Rooted Robotics) was never released to the floor. Fined $7,776. Lines 3, 592 cs, released=False.

## Change since last block
- Fixed / no longer seen: **18**
  - OVERLAP| Pallet_#_Slot#_Tier# & A Chep @(#, #)
  - WARN|[OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #B wait' (#m short) - giving up on this stop.
  - WARN|[OrderSelectionTaskDriver] Order ##ecc#-a#a-# (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - REPEATING|x# and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.
  - ORPHAN-OUTBOUND| #A# (order ##ecc#-a#a-# gone)
  - REPEATING|x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (#, #, #) - the next move would violate the # m vehicle clearance from PltJack_FULL(Clone).
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a#a#b#d-be### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##c#e#d#-b#f#a#e# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[InventoryService] Lane #B lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
  - WARN|[OrderSelectionTaskDriver] Order ##ecc#-a#a-# (Rooted Robotics) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #.
  - WARN|[OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - WARN|[InventoryService] Lane #A lock was held for over #s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over.
- New this block: **25**
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#] <Load Available> 
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it. | lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##e##f#-a#bf-#/Staged/y#, OTHER ##e##f#-a#bf-#/Staged/y#] <Dig Available> lane #B: [#: OTHER #-a#f#a##b###/Staged/y#] <Load Available> 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##e##f#-a#bf-#/Staged/y#, OTHER ##e##f#-a#bf-#/Staged/y#] <Dig Available> lane #B: [#: OTHER #-a#f#a##b###/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] <Dig Available> 
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: own/Staged/y#, own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#] <Dig Available> 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##e##f#-a#bf-#/Staged/y#, OTHER ##e##f#-a#bf-#/Staged/y#] [#: own/Staged/y#] <Dig Available> lane #B: [#: OTHER #-a#f#a##b###/Staged/y#] 
  - ERROR|"GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|"SetDestination" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry, passing lane) (route)' (#m short) - giving up on this stop.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' replenish: → reserve ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__#.MoveNext () [#x#e] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane #D exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.

## Bugs found (38 distinct, 76 incidents)

| Incidents | Bug |
|---|---|
| 3 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #B: [#: own/Staged/y#] <Load Available>  |
| 3 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it.  —  lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##e##f#-a#bf-#/Staged/y#, OTHER ##e##f#-a#bf-#/Staged/y#] <Dig Available> lane #B: [#: OTHER #-a#f#a##b###/Staged/y#] <Load Available>  |
| 3 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #B: [#: own/Staged/y#]  |
| 3 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##e##f#-a#bf-#/Staged/y#, OTHER ##e##f#-a#bf-#/Staged/y#] <Dig Available> lane #B: [#: OTHER #-a#f#a##b###/Staged/y#]  |
| 9 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 2 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #D: lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] <Dig Available>  |
| 6 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop. |
| 5 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #C: [#: own/Staged/y#, own/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #D: lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#, OTHER ###d#-bbe#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ###d#-bbe#-#/Staged/y#] <Dig Available>  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #A: [#: OTHER #-d#cd#e#e#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##e##f#-a#bf-#/Staged/y#, OTHER ##e##f#-a#bf-#/Staged/y#] [#: own/Staged/y#] <Dig Available> lane #B: [#: OTHER #-a#f#a##b###/Staged/y#]  |
| 1 | ERROR — "GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | ERROR — "SetDestination" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry, passing lane) (route)' (#m short) - giving up on this stop. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' replenish: → reserve ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__#.MoveNext () [#x#e] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane #D exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 3 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry, passing lane)' (#m short) - giving up on this stop. |
| 1 | ERROR — [MHEBoundary] 'PltJack_FULL(Clone)' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to trailer: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s). |
| 1 | ERROR — [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot. |
| 2 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | REPEATING — x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building. |
| 1 | REPEATING — x# and counting: [OpsMetrics] save failed: Win# IO returned #. Path: C:\Users\MURILLO\Documents\GitHub\### REDO\Metrics\bot_metrics.json |
| 2 | WARN — [OrderSelectionTaskDriver] Order #-da#c##b#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-e#f##f#-b#cd## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###d#-bbe#-# (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (7.49, 1.16, 48.41) — walking out to (-3.77, 1.20, 41.91) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-10.83, 1.14, 55.88) — walking out to (-4.33, 1.22, 44.62) (12.00m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet e4fd821a-e5f3-49f9-b3cd-666de3162ae6 but the employee is 6.2m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] RT_Full(Clone) cannot reach task position at (-39.14, 1.17, 49.29) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (-8.39, 1.11, 40.61) — walking out to (-8.97, 1.19, 39.23) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (0.75, 1.11, 48.76) — walking out to (0.18, 1.23, 50.15) (12.08m to mesh).
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 21 task(s) it could take wait
- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone): stuck at (-5.96, 1.11, 51.40) — walking out to (-5.39, 1.21, 52.78) (12.34m to mesh).
- **ERROR** (day 1): "GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_remainingDistance ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__238:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2189)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__238:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2189)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **OVERLAP** (day 1): OVERLAP Pallet_01_Slot0_Tier1 & Pallet_03_Slot1_Tier1 @(34, 89)  <<HISTORY: d1 10:46 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 18:22 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 18:41 moved (56, 81)->(-1, -1) by InventoryService.MovePallet | d1 19:01 moved (-1, -1)->(35, 89) by InventoryService.MovePallet | d1 19:01 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:02 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>  <<HISTORY: d1 10:56 Putaway task created (STG3C-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 10:56 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:03 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 19:14 moved (55, 85)->(-1, -1) by InventoryService.MovePallet | d1 19:26 moved (-1, -1)->(35, 89) by InventoryService.MovePallet | d1 19:26 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-02 (loop in)' (7.1m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 2): x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- **ERROR** (day 2): "SetDestination" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:SetDestination (UnityEngine.Vector3)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__238:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2159)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-04 (aisle entry, passing lane) (route)' (7.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-16.26, 1.11, 47.66).
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-11-A0 anchor: NO NAVMESH PATH. anchor=(-32.51, 3.11, 40.71) → driveTarget=(-32.51, 1.15, 40.71), from=(-20.33, 1.11, 46.27), elapsed=30.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=15.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 106.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' replenish: → reserve 01-14-B1 anchor: NO NAVMESH PATH. anchor=(-36.49, 5.11, 41.34) → driveTarget=(-36.49, 1.15, 41.34), from=(-19.92, 1.16, 45.57), elapsed=31.7s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=18.4, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [0x00025] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2375 < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__161.MoveNext () [0x0010e] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:3060 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 61.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 2): x25 and counting: [OpsMetrics] save failed: Win32 IO returned 1224. Path: C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Metrics\bot_metrics.json
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane 2D exit: NO NAVMESH PATH. anchor=(-0.71, 1.11, 48.99) → driveTarget=(-0.71, 1.24, 48.99), from=(-22.77, 1.11, 48.96), elapsed=31.6s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=22.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1263 < ] 113.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] Employee_Douglas Garcia: stuck at (7.24, 1.20, 58.91) — walking out to (-5.76, 1.12, 58.91) (12.00m to mesh).
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-05 (aisle entry, passing lane)' (17.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 02-05' - driving straight to the stop instead.
- **ERROR** (day 2): [MHEBoundary] 'PltJack_FULL(Clone)' drove OUT of the building at (-48.79, 1.11, 43.67) (cell (13, 82)). Putting it back on its last inside spot (-48.45, 1.11, 42.84). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **ERROR** (day 2): [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to trailer: NO NAVMESH PATH to (-2.79, 1.14, 38.89) from (-4.33, 1.14, 39.08) (elapsed 30.0s).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__103:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1222)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<PlaceCarriedInTrailer>d__97:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:981)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] Employee_Larry Morgan: stuck at (3.90, 1.16, 55.09) — walking out to (3.65, 1.12, 42.09) (12.00m to mesh).
- **DOOR-DEADLOCK** (day 2): OG3675 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2B: [1: own/Staged/y1.2] <Load Available> 
- **DOOR-DEADLOCK** (day 2): OG3678 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2C: [1: own/Staged/y1.2, own/Staged/y2.7] 
- **DOOR-DEADLOCK** (day 2): OG3681 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2D: lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2, OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y2.2] [3: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2, OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y2.2] [4: own/Staged/y2.0, own/Staged/y1.2] <Dig Available> 
- **DOOR-DEADLOCK** (day 2): OG1815 is Loading but door 2's truck has waited 90 min with no one loading it. | lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.2] [3: own/Staged/y1.2, own/Staged/y2.2] [4: OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y2.0, OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y1.2] <Dig Available> lane 2B: [1: OTHER acbd073d-a18f-48a7-9b84-7964fc8b3691/Staged/y1.2] <Load Available> 
- **DOOR-DEADLOCK** (day 2): OG3675 is Staged but door 2's truck has waited 120 min with no one loading it. | lane 2B: [1: own/Staged/y1.2] 
- **DOOR-DEADLOCK** (day 2): OG1815 is Staged but door 2's truck has waited 120 min with no one loading it. | lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.2] [3: own/Staged/y1.2, own/Staged/y2.2] [4: OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y2.0, OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y1.2] <Dig Available> lane 2B: [1: OTHER acbd073d-a18f-48a7-9b84-7964fc8b3691/Staged/y1.2] 
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 6ef3c574-0350-45d1-bbe7-e773a49b0a4c (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at 513/592.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order edc76943-da5c-4596-85b4-dfcdc6bb5d44 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 307/420.
- **WARN** (day 2): [AiNavigation] Employee_Shirley Edwards: stuck at (5.22, 1.18, 56.56) — walking out to (-1.28, 1.23, 45.30) (12.00m to mesh).
- **DOOR-DEADLOCK** (day 2): OG3681 is Staged but door 2's truck has waited 240 min with no one loading it. | lane 2D: lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2, OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y2.2] [3: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2, OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y2.2] [4: own/Staged/y2.0, own/Staged/y1.2] [5: OTHER 6ef3c574-0350-45d1-bbe7-e773a49b0a4c/Staged/y1.2] <Dig Available> 
- **DOOR-DEADLOCK** (day 2): OG1815 is Staged but door 2's truck has waited 240 min with no one loading it. | lane 2A: [1: OTHER 1ecd998c-d8cd-4e5e-83f8-b4015bd11b7a/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.2] [3: own/Staged/y1.2, own/Staged/y2.2] [4: OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y2.0, OTHER 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f/Staged/y1.2] [5: own/Staged/y1.2] <Dig Available> lane 2B: [1: OTHER acbd073d-a18f-48a7-9b84-7964fc8b3691/Staged/y1.2] 

## Bot actions (first 400)
- d1 signed bulk BULK_Rooted Robotics_D1_0
- d1 PO IG1137 Ambrose Fine Foods: 5 plt $16,500
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 3
- d1 booked Rooted Robotics day 2 block 36 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 11 pallet pick(s) / 2 reach truck(s): stock ready hour 18 + 23.1h of work)
- d2 released 1 order(s): OG1815 (trailer 12.8 h away; 11.5 h work + 2 h)
