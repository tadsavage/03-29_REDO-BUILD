# Playtest block 40 — days 1–4

## Score: **21.0 (-7.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (4 shipped, 13 late) | 10% | 13.3 (+0.8) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (12 plt vs target 36) | 10% | 33.3 (-52.8) |
| Revenue ($34,849) | not scored | 100.0 (0.0) |
| Net Profit (-6,749) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 8.9 (-5.3) |
| Reputation (19 to 0) | 10% | 18.3 (0.0) |

Orders shipped **4**, late/fined **13**, pallets shipped **12**. Cash $127,764 → $145,013 (+17,249). Stock at cost $200,106 → $176,108 (offsets stock bought in Net Profit).
Bot did: 2 contracts signed, 11 dock bookings, 15 orders released, 0 POs, 0 rush POs.

## Where the money went

Income **$34,849**. Expenses **$17,600** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 34,057 |
| Handling | 792 |

| Expense | $ |
|---|---|
| Fines | 8,202 |
| Wages | 4,347 |
| Maintenance | 2,592 |
| Groundskeeping | 1,251 |
| Lease / Mortgage | 1,176 |
| Loss Prevention | 32 |

## Late orders - state when it came due
- 10 x still being picked
- 3 x loaded but the truck had not left

- day 2: OG2624 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,056. Lines 27, 248 cs, released=True.
- day 3: OG3672 (Fizz-Pop Soda Works) was loaded but the truck had not left. Fined $387. Lines 9, 44 cs, released=True.
- day 3: OG3673 (Pony Express Oats) was still being picked (43/59 cs). Fined $498. Lines 10, 59 cs, released=True.
- day 3: OG3675 (Blocky Bakeshop) was still being picked (3/44 cs). Fined $307. Lines 10, 44 cs, released=True.
- day 3: OG3676 (The Big Cheese Inc.) was still being picked (0/53 cs). Fined $648. Lines 9, 53 cs, released=True.
- day 3: OG3677 (Chubby Chipmunk Cookies) was still being picked (0/48 cs). Fined $360. Lines 10, 48 cs, released=True.
- day 3: OG3678 (Neon Nectar Juice Co.) was still being picked (0/45 cs). Fined $414. Lines 10, 45 cs, released=True.
- day 3: OG3679 (The Daily Grind Tofu) was still being picked (0/55 cs). Fined $620. Lines 9, 55 cs, released=True.
- day 3: OG3680 (Crunch-Time Cereals) was still being picked (0/55 cs). Fined $763. Lines 10, 55 cs, released=True.
- day 3: OG3681 (Chubby Chipmunk Cookies) was still being picked (0/51 cs). Fined $467. Lines 9, 51 cs, released=True.
- day 3: OG3682 (Salty Sailor Pretzel Co.) was still being picked (0/47 cs). Fined $356. Lines 9, 47 cs, released=True.
- day 3: OG3683 (Bramble & Bite Berry Co.) was still being picked (0/47 cs). Fined $583. Lines 10, 47 cs, released=True.
- day 3: OG3625 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,743. Lines 19, 420 cs, released=True.

## Change since last block
- Fixed / no longer seen: **5**
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #D: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - REPEATING|x# and counting: [AiNavigation] DS_FULL(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - REPEATING|x# and counting: [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##dd##b#-a#-# but the employee is #m away (max #m) — refusing to start receiving.
- New this block: **19**
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: [#: own/Staged/y#, own/Staged/y#] 
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: [#: own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: own/Staged/y#, own/Staged/y#] 
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - WARN|[OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #C line-up' (#m short) - giving up on this stop.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #B: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' cleanup: → lane #D: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=#, reissues=#, lastSeekCancelledBy=[at GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\#. FSM\Core\Labor\TrailerLoadController.DigOut.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < at UnityEngine.MonoBehaviour.StartCoroutineManaged#_Injected (System.IntPtr , System.Collections.IEnumerator ) [#x#] in <#>:# < ] #s ago]
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #E wait' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #E line-up' (#m short) - giving up on this stop.
  - WARN|[InventoryService] Lane #B lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.

## Bugs found (34 distinct, 112 incidents)

| Incidents | Bug |
|---|---|
| 17 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 13 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 10 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 8 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #C: [#: own/Staged/y#]  |
| 2 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #D: [#: own/Staged/y#, own/Staged/y#]  |
| 6 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop. |
| 4 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #A:  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #D: [#: own/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #C: [#: own/Staged/y#, own/Staged/y#]  |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #C line-up' (#m short) - giving up on this stop. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #B: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' cleanup: → lane #D: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=#, reissues=#, lastSeekCancelledBy=[at GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\#. FSM\Core\Labor\TrailerLoadController.DigOut.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < at UnityEngine.MonoBehaviour.StartCoroutineManaged#_Injected (System.IntPtr , System.Collections.IEnumerator ) [#x#] in <#>:# < ] #s ago] |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #E wait' (#m short) - giving up on this stop. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #E line-up' (#m short) - giving up on this stop. |
| 1 | REPEATING — x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building. |
| 2 | WARN — [InventoryService] Lane #B lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 2 | WARN — [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#). |
| 1 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-15.80, 1.13, 58.96) — walking out to (-9.30, 1.20, 47.70) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (0.60, 1.11, 52.19) — walking out to (-5.90, 1.24, 40.93) (12.00m to mesh).
- **ERROR** (day 1): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] RT_Full(Clone) cannot reach task position at (-24.56, 1.20, 40.71) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (-2.04, 1.11, 52.29) — walking out to (-1.46, 1.20, 53.67) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-4.04, 1.15, 59.25) — walking out to (-17.04, 1.12, 59.25) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (6.06, 1.14, 59.25) — walking out to (6.06, 1.22, 46.25) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (6.74, 1.22, 47.46) — walking out to (6.74, 0.04, 34.46) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (7.24, 1.14, 56.56) — walking out to (7.24, 1.22, 43.56) (12.00m to mesh).
- **OVERLAP** (day 1): OVERLAP Pallet_01_Slot1_Tier0 & Pallet_01_Slot1_Tier0 @(31, 78)  <<HISTORY: d1 10:15 Putaway task created (STG3A-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 10:15 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 11:56 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 12:14 moved (53, 85)->(-1, -1) by InventoryService.MovePallet | d1 12:24 moved (-1, -1)->(31, 78) by InventoryService.MovePallet | d1 12:24 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 09:59 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 11:08 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:20 moved (46, 83)->(-1, -1) by InventoryService.MovePallet | d1 11:46 moved (-1, -1)->(31, 78) by InventoryService.MovePallet | d1 11:46 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 11:48 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>
- **REPEATING** (day 2): x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- **WARN** (day 2): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-38.46, 1.11, 42.34).
- **WARN** (day 2): [InventoryService] Lane 1B lock was held for over 120s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 135/248.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (5.32, 1.11, 56.26) runs through something solid - taking a clear way out to (5.89, 1.17, 57.65) instead.
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone): stuck at (-5.53, 1.11, 58.32) — walking out to (-5.53, 1.16, 59.00) (12.00m to mesh).
- **ERROR** (day 3): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 3C line-up' (20.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 3): [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 1B: NO NAVMESH PATH. anchor=(-11.31, 1.14, 48.99) → driveTarget=(-11.31, 1.23, 48.99), from=(-9.06, 1.11, 51.55), elapsed=31.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=3.4, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 295.8s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 3): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-02 (loop in)' (13.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 3): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-02 (aisle entry)' (10.7m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 3): [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 02-02' - driving straight to the stop instead.
- **ERROR** (day 3): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-04-A0 anchor: NO NAVMESH PATH. anchor=(-21.91, 3.11, 41.34) → driveTarget=(-21.91, 1.22, 41.34), from=(-23.66, 1.20, 40.24), elapsed=30.5s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=2.1, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 216.2s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 3): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 19 task(s) it could take wait
- **WARN** (day 3): [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- **ERROR** (day 3): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-04-B1 anchor: NO NAVMESH PATH. anchor=(-23.24, 5.11, 41.34) → driveTarget=(-23.24, 1.22, 41.34), from=(-20.10, 1.11, 44.10), elapsed=31.4s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=4.2, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 206.0s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **DOOR-DEADLOCK** (day 3): OG3673 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3C: [1: own/Staged/y1.2] 
- **DOOR-DEADLOCK** (day 3): OG3672 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2A: 
- **DOOR-DEADLOCK** (day 3): OG3675 is Staged but door 3's truck has waited 90 min with no one loading it. | lane 2D: [1: own/Staged/y1.2] 
- **DOOR-DEADLOCK** (day 3): OG3676 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3D: [1: own/Staged/y1.2, own/Staged/y2.5] 
- **ERROR** (day 3): [TrailerLoad] 'DS_FULL(Clone)' cleanup: → lane 2D: never arrived at (-0.71, 1.19, 39.19) (elapsed 31.6s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=17.6, reissues=0, lastSeekCancelledBy=[at GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__32.MoveNext () [0x00249] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\1. FSM\Core\Labor\TrailerLoadController.DigOut.cs:701 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < at UnityEngine.MonoBehaviour.StartCoroutineManaged2_Injected (System.IntPtr , System.Collections.IEnumerator ) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 32.1s ago]
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__32:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:756)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 3): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 2E wait' (14.8m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 3): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 2E line-up' (10.3m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 3): x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 13.7m short of 'bay 01-01 (aisle entry)' - re-issuing the drive (3/6).
- **DOOR-DEADLOCK** (day 3): OG3673 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3C: [1: own/Staged/y1.2, own/Staged/y1.9] 

## Bot actions (first 400)
- d1 signed bulk BULK_Salty Sailor Pretzel Co._D1_0
- d1 booked Salty Sailor Pretzel Co. day 1 block 25 door 1 (pallet-only, 1 pallet pick(s) / 2 reach truck(s): stock ready hour 10 + 1.5h of work)
- d1 released 1 order(s): OG1691 (trailer 2.3 h away; 1.5 h work + 2 h)
- d2 released 1 order(s): OG2624 (trailer 7.3 h away; 5.5 h work + 2 h)
- d2 signed bulk BULK_Cluck & Cover Poultry_D2_0
- d2 released 2 order(s): OG3673 (trailer 8.3 h away; 6.7 h work + 2 h); OG3674 (trailer 8.3 h away; 6.6 h work + 2 h)
- d3 released 10 order(s): OG3672 (trailer 7.8 h away; 11.1 h work + 2 h); OG3675 (trailer 8.8 h away; 11.3 h work + 2 h); OG3676 (trailer 8.8 h away; 11.5 h work + 2 h); OG3677 (trailer 8.8 h away; 11.5 h work + 2 h); OG3678 (trailer 9.3 h away; 11.4 h work + 2 h); OG3679 (trailer 9.8 h away; 11.5 h work + 2 h); OG3680 (trailer 9.8 h away; 11.8 h work + 2 h); OG3681 (trailer 10.3 h away; 11.4 h work + 2 h); OG3682 (trailer 10.8 h away; 11.2 h work + 2 h); OG3683 (trailer 10.8 h away; 11.5 h work + 2 h)
- d3 released 1 order(s): OG3625 (trailer 16.3 h away; 20.2 h work + 2 h)
- d3 booked Blocky Bakeshop day 4 block 10 door 3 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 58 + 18.2h of work)
- d3 booked Pony Express Oats day 4 block 6 door 1 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 58 + 16.4h of work)
- d3 booked Chubby Chipmunk Cookies day 4 block 10 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 59 + 17.9h of work)
- d3 booked Neon Nectar Juice Co. day 4 block 11 door 3 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 59 + 17.8h of work)
- d3 booked The Big Cheese Inc. day 4 block 12 door 1 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 59 + 17.8h of work)
- d3 booked Chubby Chipmunk Cookies day 4 block 13 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 60 + 16.9h of work)
- d3 booked Crunch-Time Cereals day 4 block 14 door 3 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 60 + 17.3h of work)
- d3 booked The Daily Grind Tofu day 4 block 15 door 1 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 60 + 17.1h of work)
- d3 booked Bramble & Bite Berry Co. day 4 block 16 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 61 + 16.4h of work)
- d3 booked Salty Sailor Pretzel Co. day 4 block 17 door 3 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 61 + 16.2h of work)
