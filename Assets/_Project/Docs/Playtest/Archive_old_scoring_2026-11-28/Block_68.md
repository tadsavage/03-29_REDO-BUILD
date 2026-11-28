# Playtest block 68 — days 1–3

## Score: **33.9 (+18.6)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 2 late) | 10% | 50.0 (+50.0) |
| Fill rate | 10% | 100.0 (+100.0) |
| Throughput (4 plt vs target 24) | 10% | 16.7 (+16.7) |
| Revenue ($10,620) | not scored | 66.4 (+66.4) |
| Net Profit (-21,522) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 14.5 (+3.8) |
| Reputation (19 to 59) | 10% | 100.0 (0.0) |

Orders shipped **2**, late/fined **2**, pallets shipped **4**. Cash $127,764 → $125,386 (-2,378). Stock at cost $200,106 → $180,962 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 3 dock bookings, 0 orders released, 3 POs, 0 rush POs.

## Where the money went

Income **$10,620**. Expenses **$12,998** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 10,620 |

| Expense | $ |
|---|---|
| Inventory | 4,152 |
| Wages | 2,835 |
| Fines | 2,808 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x loaded but the truck had not left
- 1 x still being picked

- day 2: OG2624 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,056. Lines 27, 248 cs, released=True.
- day 3: OG2802 (Sneaky Pete's Seafood) was still being picked (367/432 cs). Fined $1,752. Lines 3, 432 cs, released=True.

## Change since last block
- Fixed / no longer seen: **14**
  - ERROR|"IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'staging lane #A line-up' was interrupted # times - giving the leg up.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #E: [#: stock ####f##] [#: stock ##e##-b#c#a#abb#, stock #-dc##e#-b#c#-#] lane #A: [#: own/Staged/y#, own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: [#: own/Staged/y#] 
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane #D exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=Infinity, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - WARN|[AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-c##f#-afb#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **22**
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #E: [#: own/Staged/y#, own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##fda#b#c#-#/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##fda#b#c#-#/Staged/y#, OTHER ##fda#b#c#-#/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: stock #-a#e#ca-b##e#cde#] [#: stock #-c#d##-afab##, stock ##fe#cb-a#-#] [#: stock #-e#d#-a#d###, stock ###b#f#-#] lane #A: [#: OTHER ##c##bd-ae#e##/Staged/y#, OTHER ##c##bd-ae#e##/Staged/y#] [#: own/Staged/y#, own/Staged/y#] 
  - WARN|[AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #B wait' (#m short) - giving up on this stop.
  - ERROR|"SetDestination" can only be called on an active agent that has been placed on a NavMesh.
  - REPEATING|x# and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - REPEATING|x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress staging order OG# for # game minutes - staying aboard the loaded jack (StagingHold).
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' made no progress staging order OG# for # game minutes - staying aboard the loaded jack (StagingHold).
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] 'Employee_Shirley Edwards' made no progress staging order OG# for # game minutes - staying aboard the loaded jack (StagingHold).
  - WARN|[InventoryService] Lane #A lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.

## Bugs found (30 distinct, 56 incidents)

| Incidents | Bug |
|---|---|
| 8 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 6 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #E: [#: own/Staged/y#, own/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #A: [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##fda#b#c#-#/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #A: [#: own/Staged/y#, own/Staged/y#] [#: OTHER ##fda#b#c#-#/Staged/y#, OTHER ##fda#b#c#-#/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #C: [#: stock #-a#e#ca-b##e#cde#] [#: stock #-c#d##-afab##, stock ##fe#cb-a#-#] [#: stock #-e#d#-a#d###, stock ###b#f#-#] lane #A: [#: OTHER ##c##bd-ae#e##/Staged/y#, OTHER ##c##bd-ae#e##/Staged/y#] [#: own/Staged/y#, own/Staged/y#]  |
| 3 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #B wait' (#m short) - giving up on this stop. |
| 1 | ERROR — "SetDestination" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | REPEATING — x# and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway. |
| 1 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | REPEATING — x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress staging order OG# for # game minutes - staying aboard the loaded jack (StagingHold). |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' made no progress staging order OG# for # game minutes - staying aboard the loaded jack (StagingHold). |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Shirley Edwards' made no progress staging order OG# for # game minutes - staying aboard the loaded jack (StagingHold). |
| 2 | WARN — [InventoryService] Lane #A lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over. |
| 1 | REPEATING — x# and counting: "SetDestination" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | WARN — [AiNavigation] Employee_Douglas Garcia cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Mary Young cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Justin Waller: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ab##-a#e### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [InventoryService] Lane #B lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over. |
| 1 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia cannot reach task position at (10.31, 1.21, 46.99) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (1.90, 1.19, 41.00) — walking out to (12.92, 1.12, 47.50) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young cannot reach task position at (5.49, 1.21, 44.34) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] Employee_Justin Waller: stuck at (-7.91, 1.20, 45.94) — walking out to (-14.41, 1.08, 35.17) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-8.86, 1.12, 59.13) — walking out to (-8.86, 1.20, 46.13) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone) cannot reach task position at (8.56, 1.24, 48.99) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (9.89, 1.22, 51.33) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 489930c6-7ab3-4317-a9e5-315e7437a198 but the employee is 13.6m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.
- **VEHICLE-STATIONARY** (day 1): RT_Full(Clone) has a driver, hasn't moved in 360s while 38 task(s) it could take wait
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 1B wait' (4.4m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__223:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1803)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 26 task(s) it could take wait
- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone): stuck at (-6.30, 1.11, 59.00) — walking out to (-4.91, 1.16, 59.00) (12.00m to mesh).
- **REPEATING** (day 1): x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 232/248.
- **WARN** (day 2): [InventoryService] Lane 1B lock was held for over 120s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] 'Employee_Larry Morgan' made no progress staging order OG2624 for 10 game minutes - staying aboard the loaded jack (StagingHold).
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] 'Employee_Mark Peterson' made no progress staging order OG3674 for 10 game minutes - staying aboard the loaded jack (StagingHold).
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] 'Employee_Shirley Edwards' made no progress staging order OG3674 for 10 game minutes - staying aboard the loaded jack (StagingHold).
- **WARN** (day 2): [InventoryService] Lane 1A lock was held for over 120s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
- **WARN** (day 2): [AiNavigation] Employee_Shirley Edwards: stuck at (-4.08, 1.14, 56.59) — walking out to (-5.63, 1.20, 43.59) (12.09m to mesh).
- **ERROR** (day 2): "SetDestination" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:SetDestination (UnityEngine.Vector3)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__234:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2150)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 2): x25 and counting: "SetDestination" can only be called on an active agent that has been placed on a NavMesh.
- **DOOR-DEADLOCK** (day 2): OG3625 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 3E: [1: own/Staged/y2.6, own/Staged/y1.2] 
- **DOOR-DEADLOCK** (day 2): OG3674 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y2.5] [2: OTHER b72defae-0fda-441b-9c33-e78561011633/Staged/y1.2] 
- **WARN** (day 2): [AiNavigation] Employee_Mark Peterson: stuck at (-12.64, 1.19, 44.71) — walking out to (-25.63, 1.12, 43.36) (12.07m to mesh).
- **DOOR-DEADLOCK** (day 2): OG3674 is Staged but door 1's truck has waited 180 min with no one loading it. | lane 1A: [1: own/Staged/y1.2, own/Staged/y2.5] [2: OTHER b72defae-0fda-441b-9c33-e78561011633/Staged/y1.5, OTHER b72defae-0fda-441b-9c33-e78561011633/Staged/y1.2] 
- **WARN** (day 2): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-16.26, 1.11, 47.66).
- **DOOR-DEADLOCK** (day 2): OG3677 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1C: [2: stock 5c69be3a-a28e-41ca-b523-45093e04cde5] [3: stock 72e7e311-c6d8-4236-afab-954ede14f041, stock 35f01d91-08fe-47cb-a383-f39d5f315d22] [4: stock d711fab8-e40d-4363-a3d5-196f298c2375, stock 331bc22d-5616-442b-8f62-ca12df0b1e67] lane 1A: [1: OTHER e0e75bca-1c45-42bd-ae3e-8518cfeb877a/Staged/y1.2, OTHER e0e75bca-1c45-42bd-ae3e-8518cfeb877a/Staged/y2.5] [2: own/Staged/y1.5, own/Staged/y1.2] 

## Bot actions (first 400)
- d1 signed bulk BULK_Grizzly Ridge Jerky_D1_0
- d1 PO IG1116 Anchor Point Foods: 2 plt $1,470
- d1 placed parked Inbound Anchor Point Foods day 1 block 26 door 3
- d1 PO IG1117 Anchor Point Foods: 1 plt $735
- d1 placed parked Inbound Anchor Point Foods day 1 block 25 door 1
- d1 PO IG1118 Fairweather Trading Co.: 5 plt $6,240
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 27 door 2
