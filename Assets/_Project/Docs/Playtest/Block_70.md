# Playtest block 70 — days 1–3

## Score: **47.1 (+27.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 0 late) | 10% | 100.0 (+100.0) |
| Fill rate | 10% | 94.9 (+94.9) |
| Throughput (28 plt vs target 24) | 10% | 100.0 (+100.0) |
| Revenue ($41,510) | not scored | 100.0 (+100.0) |
| Net Profit (-139,899) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 15.3 (-3.1) |
| Reputation (19 to 128) | 10% | 100.0 (0.0) |

Orders shipped **2**, late/fined **0**, pallets shipped **28**. Cash $127,764 → $133,722 (+5,958). Stock at cost $200,106 → $54,249 (offsets stock bought in Net Profit).
Bot did: 2 contracts signed, 14 dock bookings, 0 orders released, 13 POs, 0 rush POs.

## Where the money went

Income **$41,510**. Expenses **$18,013** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 40,478 |
| Handling | 1,032 |

| Expense | $ |
|---|---|
| Inventory | 11,990 |
| Wages | 2,835 |
| Maintenance | 1,608 |
| Lease / Mortgage | 784 |
| Groundskeeping | 776 |
| Loss Prevention | 20 |

## Change since last block
- Fixed / no longer seen: **6**
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #E: [#: own/Staged/y#, own/Staged/y#] 
  - OVERLAP| A Chep & Pallet_#_Slot#_Tier# @(#, #)
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__#.MoveNext () [#x#e] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##f##f### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
- New this block: **20**
  - WARN|[AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - OVERLAP| Pallet_#_Slot#_Tier# & A Chep @(#, #)
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - WARN|[OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##ecc#-a#a-# (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - REPEATING|x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
  - REPEATING|x# and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.
  - ORPHAN-OUTBOUND| #A# (order ##ecc#-a#a-# gone)
  - REPEATING|x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (#, #, #) - the next move would violate the # m vehicle clearance from PltJack_FULL(Clone).
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a#a#b#d-be### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##c#e#d#-b#f#a#e# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[InventoryService] Lane #B lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
  - WARN|[OrderSelectionTaskDriver] Order ##ecc#-a#a-# (Rooted Robotics) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #.

## Bugs found (31 distinct, 63 incidents)

| Incidents | Bug |
|---|---|
| 9 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & A Chep @(#, #) |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #B wait' (#m short) - giving up on this stop. |
| 1 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##ecc#-a#a-# (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | REPEATING — x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building. |
| 2 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | REPEATING — x# and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway. |
| 1 | ORPHAN-OUTBOUND —  #A# (order ##ecc#-a#a-# gone) |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (#, #, #) - the next move would violate the # m vehicle clearance from PltJack_FULL(Clone). |
| 1 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a#a#b#d-be### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##c#e#d#-b#f#a#e# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [InventoryService] Lane #B lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##ecc#-a#a-# (Rooted Robotics) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [InventoryService] Lane #A lock was held for over #s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [InventoryService] Lane #A lock was held for over #s by reach truck RT_Full(Clone) (Amanda Dixon, PalletPick) - treating it as leaked and taking it over. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone): stuck at (-14.06, 1.11, 51.33) — walking out to (-13.49, 1.19, 52.71) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.16, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-7.42, 1.17, 46.16) — walking out to (-13.92, 1.08, 35.17) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (5.07, 1.22, 44.61) — walking out to (5.07, 1.13, 57.61) (12.00m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet ae4506ea-a93a-4b3d-be92-5aec025ba608 but the employee is 4.7m away (max 4.5m) — refusing to start receiving.
- **OVERLAP** (day 1): OVERLAP Pallet_01_Slot1_Tier0 & A Chep @(33, 83)  <<HISTORY: d1 10:04 Putaway task created (STG1D-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 10:04 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 12:20 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 12:36 moved (43, 85)->(-1, -1) by InventoryService.MovePallet | d1 12:51 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 12:51 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 11:00 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:40 Replenish task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 12:06 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 12:30 moved (33, 83)->(-1, -1) by InventoryService.MovePallet | d1 12:34 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 12:34 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **OVERLAP** (day 1): OVERLAP Pallet_00_Slot0_Tier0 & Pallet_01_Slot1_Tier0 @(33, 83)  <<HISTORY: d1 09:39 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 09:46 moved (50, 85)->(-1, -1) by InventoryService.MovePallet | d1 12:06 Putaway task Assigned->Available by WorkQueueSystem.ReleaseStaleAssignments | d1 12:18 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 12:18 Putaway task Available->Complete by WorkQueueSystem.CompleteTask | d1 12:20 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>  <<HISTORY: d1 10:02 Putaway task created (STG2E-3->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 10:02 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 13:14 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 13:25 moved (50, 83)->(-1, -1) by InventoryService.MovePallet | d1 13:36 moved (-1, -1)->(33, 83) by InventoryService.MovePallet | d1 13:36 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-16.26, 1.11, 47.66).
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 20 task(s) it could take wait
- **REPEATING** (day 1): x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (6.69, 1.16, 49.00) — walking out to (6.69, 1.09, 36.00) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (1.62, 1.16, 58.25) — walking out to (8.12, 1.23, 46.99) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (1.62, 1.16, 57.70) — walking out to (8.12, 1.23, 46.44) (12.00m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 20/248.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet fe91fdea-1c1e-4d21-b83f-625283a397e2 but the employee is 4.8m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] RT_Full(Clone) cannot reach task position at (-21.91, 1.22, 41.34) (path invalid) — abandoning the walk and handing the task back.
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 1B wait' (3.8m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 1): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [OrderSelectionTaskDriver] Order eb1433f2-2ecc-4529-a46a-a256833be6b7 (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at 463/728.
- **WARN** (day 1): [InventoryService] Lane 3B lock was held for over 120s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order eb1433f2-2ecc-4529-a46a-a256833be6b7 (Rooted Robotics) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 3.
- **WARN** (day 2): [OrderService] Order OG4790 closed with a STAGED pallet (23 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 2): ORPHAN-OUTBOUND 3A-1 (order eb1433f2-2ecc-4529-a46a-a256833be6b7 gone)
- **REPEATING** (day 2): x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (-11.00, 1.11, 49.27) - the next move would violate the 1 m vehicle clearance from PltJack_FULL(Clone).
- **WARN** (day 2): [InventoryService] Lane 1A lock was held for over 120s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over.
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-05 (aisle entry)' (10.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 01-05' - driving straight to the stop instead.
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- **WARN** (day 2): [InventoryService] Lane 1A lock was held for over 120s by reach truck RT_Full(Clone) (Amanda Dixon, PalletPick) - treating it as leaked and taking it over.
- **VEHICLE-STATIONARY** (day 2): RT_Full(Clone) has a driver, hasn't moved in 360s while 14 task(s) it could take wait

## Bot actions (first 400)
- d1 signed bulk BULK_Blocky Bakeshop_D1_0
- d1 PO IG1124 Anchor Point Foods: 4 plt $2,080
- d1 placed parked Inbound Anchor Point Foods day 1 block 26 door 3
- d1 PO IG1125 Last Call Liquidators: 4 plt $4,860
- d1 placed parked Inbound Last Call Liquidators day 1 block 27 door 2
- d1 PO IG1126 Homestead Market Co.: 1 plt $594
- d1 placed parked Inbound Homestead Market Co. day 1 block 29 door 3
- d1 booked Blocky Bakeshop day 3 block 10 door 2 (pallet-only, 16 pallet pick(s) / 2 reach truck(s): stock ready hour 18 + 25.2h of work)
- d1 PO IG1127 Bulk Basin Foods: 2 plt $240
- d2 placed parked Inbound Bulk Basin Foods day 2 block 1 door 1
- d2 PO IG2128 Anchor Point Foods: 2 plt $1,092
- d2 placed parked Inbound Anchor Point Foods day 2 block 36 door 2
- d2 PO IG2129 Backdoor Bill's Overstock: 1 plt $1,140
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 37 door 3
- d2 PO IG2130 Homestead Market Co.: 2 plt $2,744
- d2 placed parked Inbound Homestead Market Co. day 2 block 38 door 2
- d2 PO IG2131 Bulk Basin Foods: 1 plt $672
- d2 placed parked Inbound Bulk Basin Foods day 2 block 39 door 1
- d2 signed recurring Contract_HighVolume
- d2 PO IG2132 Bulk Basin Foods: 2 plt $808
- d2 placed parked Inbound Bulk Basin Foods day 2 block 41 door 1
- d2 PO IG2133 Homestead Market Co.: 2 plt $2,384
- d2 placed parked Inbound Homestead Market Co. day 2 block 42 door 2
- d2 PO IG2134 Steady Supply Grocers: 1 plt $648
- d2 placed parked Inbound Steady Supply Grocers day 2 block 43 door 1
- d2 PO IG2135 Anchor Point Foods: 2 plt $1,092
- d2 placed parked Inbound Anchor Point Foods day 2 block 45 door 1
- d2 PO IG2136 Last Call Liquidators: 1 plt $1,170
- d2 placed parked Inbound Last Call Liquidators day 2 block 46 door 2
