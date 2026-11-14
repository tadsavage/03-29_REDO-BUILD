# Playtest block 41 — days 1–4

## Score: **26.9 (+5.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (4 shipped, 16 late) | 10% | 0.0 (-13.3) |
| Fill rate | 10% | 95.8 (-4.2) |
| Throughput (32 plt vs target 36) | 10% | 88.9 (+55.6) |
| Revenue ($59,044) | not scored | 100.0 (0.0) |
| Net Profit (-16,988) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 11.5 (+2.5) |
| Reputation (19 to 5) | 10% | 26.7 (+8.3) |

Orders shipped **4**, late/fined **16**, pallets shipped **32**. Cash $127,764 → $123,586 (-4,178). Stock at cost $200,106 → $187,296 (offsets stock bought in Net Profit).
Bot did: 3 contracts signed, 23 dock bookings, 17 orders released, 10 POs, 0 rush POs.

## Where the money went

Income **$59,019**. Expenses **$63,197** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 58,227 |
| Handling | 792 |

| Expense | $ |
|---|---|
| Inventory | 31,072 |
| Fines | 22,727 |
| Wages | 4,347 |
| Maintenance | 2,592 |
| Groundskeeping | 1,251 |
| Lease / Mortgage | 1,176 |
| Loss Prevention | 32 |

## Late orders - state when it came due
- 12 x still being picked
- 3 x loaded but the truck had not left
- 1 x never released

- day 2: OG2696 (Chubby Chipmunk Cookies) was loaded but the truck had not left. Fined $5,598. Lines 3, 1056 cs, released=True.
- day 2: OG2624 (Sneaky Pete's Seafood) was still being picked (248/248 cs). Fined $1,056. Lines 27, 248 cs, released=True.
- day 3: OG2699 (Pony Express Oats) was never released to the floor. Fined $8,469. Lines 3, 1517 cs, released=False.
- day 3: OG3672 (Fizz-Pop Soda Works) was still being picked (44/44 cs). Fined $387. Lines 9, 44 cs, released=True.
- day 3: OG3673 (Pony Express Oats) was loaded but the truck had not left. Fined $498. Lines 10, 59 cs, released=True.
- day 3: OG3674 (Mama Tomato Sauces) was still being picked (36/57 cs). Fined $458. Lines 10, 57 cs, released=True.
- day 3: OG3675 (Blocky Bakeshop) was still being picked (0/44 cs). Fined $307. Lines 10, 44 cs, released=True.
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
- Fixed / no longer seen: **17**
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: [#: own/Staged/y#, own/Staged/y#] 
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: [#: own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: own/Staged/y#, own/Staged/y#] 
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #C line-up' (#m short) - giving up on this stop.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #B: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' cleanup: → lane #D: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=#, reissues=#, lastSeekCancelledBy=[at GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\#. FSM\Core\Labor\TrailerLoadController.DigOut.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < at UnityEngine.MonoBehaviour.StartCoroutineManaged#_Injected (System.IntPtr , System.Collections.IEnumerator ) [#x#] in <#>:# < ] #s ago]
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #E wait' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #E line-up' (#m short) - giving up on this stop.
  - WARN|[InventoryService] Lane #B lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
- New this block: **17**
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'bay ## (loop in)' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'bay ## (aisle entry) (route)' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'bay ## (aisle entry)' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'bay ## (route)' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'bay ##' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': 'staging lane #D line-up' was interrupted # times - giving the leg up.
  - REPEATING|x# and counting: [AiNavigation] DS_FULL(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Douglas Garcia cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##fc#c#-b#c-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] Order ##bb##d#-bc### (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - WARN|[OrderSelectionTaskDriver] Order ##ba##c#f-a#fb-# (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#.

## Bugs found (34 distinct, 115 incidents)

| Incidents | Bug |
|---|---|
| 18 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 14 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 12 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 12 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 12 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 5 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #C: [#: own/Staged/y#]  |
| 3 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': 'bay ## (loop in)' was interrupted # times - giving the leg up. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': 'bay ## (aisle entry) (route)' was interrupted # times - giving the leg up. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': 'bay ## (aisle entry)' was interrupted # times - giving the leg up. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': 'bay ## (route)' was interrupted # times - giving the leg up. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': 'bay ##' was interrupted # times - giving the leg up. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': 'staging lane #D line-up' was interrupted # times - giving the leg up. |
| 1 | REPEATING — x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building. |
| 1 | REPEATING — x# and counting: [AiNavigation] DS_FULL(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 2 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 2 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Douglas Garcia cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##fc#c#-b#c-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##bb##d#-bc### (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##ba##c#f-a#fb-# (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##c##bd-ae#e## (Mama Tomato Sauces) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a##f##c#-# but the employee is #m away (max #m) — refusing to start receiving. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (8.87, 1.11, 57.95) — walking out to (8.87, 1.16, 59.00) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-4.63, 1.12, 59.13) — walking out to (-4.63, 1.24, 46.13) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-4.62, 1.12, 59.15) — walking out to (-4.62, 1.24, 46.15) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-2.10, 1.12, 59.15) — walking out to (-2.10, 1.24, 46.15) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-27.21, 1.17, 50.19).
- **ERROR** (day 1): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia cannot reach task position at (6.36, 1.22, 41.66) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-5.97, 1.17, 41.00) — walking out to (5.29, 1.22, 47.50) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-14.39, 1.16, 43.01) — walking out to (-25.59, 1.08, 35.17) (12.85m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet fc25b792-59fc-4c49-b10c-ebb33812876c but the employee is 30.2m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
- **ERROR** (day 1): [GridNav] 'Employee_Larry Morgan': 'bay 02-04 (loop in)' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': 'bay 02-04 (aisle entry) (route)' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': 'bay 02-04 (aisle entry)' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 02-04' - driving straight to the stop instead.
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': 'bay 02-04 (route)' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': 'bay 02-04' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': no route for 'staging lane 2C line-up (route)' from (-30.35, 1.11, 47.89) to (-42.14, 1.11, 50.19) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': no route for 'staging lane 2C line-up' from (-30.35, 1.11, 47.89) to (-2.04, 1.11, 51.33) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] Order b2d1889b-5bb8-4d46-bc45-378e4c29fb2b (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at 996/1056.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] DS_FULL(Clone): the straight way out of (5.82, 1.11, 48.32) runs through something solid - taking a clear way out to (6.40, 1.23, 49.70) instead.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 224/248.
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone): stuck at (-38.87, 1.11, 49.06) — walking out to (-25.87, 1.21, 49.06) (12.00m to mesh).
- **WARN** (day 2): [OrderService] Order OG2696 closed with a IN TRANSIT pallet (50 cs) that never made the trailer - not billed, returning it to stock.
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 2371db5b-4ba1-4c6f-a5fb-dbdcbd441229 (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at 49/59.
- **REPEATING** (day 3): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-40.36, 1.11, 45.90) runs through something solid - taking a clear way out to (-40.35, 1.17, 44.52) instead.
- **WARN** (day 3): [OrderSelectionTaskDriver] Order e0e75bca-1c45-42bd-ae3e-8518cfeb877a (Mama Tomato Sauces) has no reachable pick location for its remaining line item(s) — staging short at 35/57.
- **DOOR-DEADLOCK** (day 3): OG3675 is Staged but door 3's truck has waited 90 min with no one loading it. | lane 2C: [1: own/Staged/y1.2] 
- **WARN** (day 3): [AiNavigation] RT_Full(Clone) cannot reach task position at (-45.76, 1.11, 44.34) (path invalid) — abandoning the walk and handing the task back.
- **VEHICLE-STATIONARY** (day 3): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- **REPEATING** (day 3): x25 and counting: [AiNavigation] RT_Full(Clone): stuck at (-12.07, 1.11, 49.73) — walking out to (-11.50, 1.20, 51.12) (12.27m to mesh).
- **ERROR** (day 3): [GridNav] 'Employee_Mark Peterson': 'staging lane 2D line-up' was interrupted 7 times - giving the leg up.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:97)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 3): [ReceiverReceivingWorkflow] Arrival callback fired for pallet c592c087-a399-48f5-8c41-b4bd065d4733 but the employee is 9.5m away (max 4.5m) — refusing to start receiving.

## Bot actions (first 400)
- d1 signed bulk BULK_Chubby Chipmunk Cookies_D1_0
- d1 PO IG1944 Last Call Liquidators: 2 plt $2,160
- d1 placed parked Inbound Last Call Liquidators day 1 block 26 door 3
- d1 PO IG1945 Fairweather Trading Co.: 8 plt $4,352
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 27 door 2
- d1 PO IG1946 Backdoor Bill's Overstock: 5 plt $5,400
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 28 door 3
- d1 booked Chubby Chipmunk Cookies day 2 block 10 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 22 pallet pick(s) / 2 reach truck(s): stock ready hour 20 + 8.4h of work)
- d1 released 1 order(s): OG2696 (trailer 10.3 h away; 8.4 h work + 2 h)
- d2 released 1 order(s): OG2624 (trailer 7.8 h away; 6.3 h work + 2 h)
- d2 signed bulk BULK_Pony Express Oats_D2_0
- d2 PO IG2947 Backdoor Bill's Overstock: 7 plt $9,800
- d2 placed parked Inbound Backdoor Bill's Overstock day 3 block 0 door 1
- d2 released 2 order(s): OG3673 (trailer 8.8 h away; 7.2 h work + 2 h); OG3674 (trailer 8.8 h away; 7.1 h work + 2 h)
- d2 released 10 order(s): OG3672 (trailer 8.3 h away; 12.0 h work + 2 h); OG3675 (trailer 9.3 h away; 12.3 h work + 2 h); OG3676 (trailer 9.3 h away; 12.4 h work + 2 h); OG3677 (trailer 9.3 h away; 12.5 h work + 2 h); OG3678 (trailer 9.8 h away; 12.3 h work + 2 h); OG3679 (trailer 10.3 h away; 12.5 h work + 2 h); OG3680 (trailer 10.3 h away; 12.8 h work + 2 h); OG3681 (trailer 10.8 h away; 12.3 h work + 2 h); OG3682 (trailer 11.3 h away; 12.1 h work + 2 h); OG3683 (trailer 11.3 h away; 12.4 h work + 2 h)
- d3 released 1 order(s): OG3625 (trailer 16.8 h away; 21.5 h work + 2 h)
- d3 booked Blocky Bakeshop day 4 block 14 door 3 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 58 + 20.2h of work)
- d3 booked Mama Tomato Sauces day 4 block 8 door 1 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 58 + 17.9h of work)
- d3 signed bulk BULK_The Big Cheese Inc._D3_0
- d3 booked Chubby Chipmunk Cookies day 4 block 14 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 59 + 20.0h of work)
- d3 booked The Big Cheese Inc. day 4 block 15 door 3 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 59 + 19.9h of work)
- d3 PO IG3948 Anchor Point Foods: 2 plt $1,040
- d3 placed parked Inbound Anchor Point Foods day 3 block 24 door 1
- d3 booked Neon Nectar Juice Co. day 4 block 16 door 1 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 59 + 19.7h of work)
- d3 PO IG3949 Homestead Market Co.: 4 plt $2,880
- d3 placed parked Inbound Homestead Market Co. day 3 block 25 door 2
- d3 booked Chubby Chipmunk Cookies day 4 block 17 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 60 + 19.3h of work)
- d3 booked Crunch-Time Cereals day 4 block 18 door 3 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 60 + 19.7h of work)
- d3 booked The Daily Grind Tofu day 4 block 19 door 1 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 60 + 19.4h of work)
- d3 placed parked Inbound Anchor Point Foods day 3 block 27 door 1
- d3 booked Bramble & Bite Berry Co. day 4 block 20 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 61 + 18.3h of work)
- d3 placed parked Inbound Homestead Market Co. day 3 block 28 door 2
- d3 released 1 order(s): OG4697 (trailer 23.8 h away; 22.3 h work + 2 h)
- d3 PO IG3950 Last Call Liquidators: 1 plt $1,080
- d3 placed parked Inbound Last Call Liquidators day 3 block 36 door 2
- d3 booked Salty Sailor Pretzel Co. day 5 block 0 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 70 + 25.9h of work)
- d3 released 1 order(s): OG4698 (trailer 23.3 h away; 26.8 h work + 2 h)
- d3 PO IG3951 Anchor Point Foods: 1 plt $520
- d3 placed parked Inbound Anchor Point Foods day 3 block 37 door 2
- d3 PO IG3952 Anchor Point Foods: 2 plt $960
- d3 placed parked Inbound Anchor Point Foods day 3 block 44 door 1
- d3 PO IG3953 Homestead Market Co.: 4 plt $2,880
- d3 placed parked Inbound Homestead Market Co. day 4 block 0 door 1
