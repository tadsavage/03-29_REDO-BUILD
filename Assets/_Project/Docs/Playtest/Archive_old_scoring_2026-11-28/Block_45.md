# Playtest block 45 — days 1–3

## Score: **26.3 (-15.2)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 1 late) | 10% | 66.7 (-33.3) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (8 plt vs target 24) | 10% | 33.3 (-29.2) |
| Revenue ($26,496) | not scored | 100.0 (0.0) |
| Net Profit (-56,206) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 9.0 (-10.0) |
| Reputation (19 to 0) | 10% | 18.3 (-40.0) |

Orders shipped **2**, late/fined **1**, pallets shipped **8**. Cash $127,764 → $124,531 (-3,233). Stock at cost $200,106 → $147,133 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 10 dock bookings, 0 orders released, 7 POs, 0 rush POs.

## Where the money went

Income **$26,496**. Expenses **$29,729** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 25,764 |
| Handling | 732 |

| Expense | $ |
|---|---|
| Inventory | 21,152 |
| Wages | 2,835 |
| Fines | 2,539 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x never released

- day 3: OG2712 (Bramble & Bite Berry Co.) was never released to the floor. Fined $2,539. Lines 2, 479 cs, released=False.

## Change since last block
- Fixed / no longer seen: **8**
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane #B: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s).
  - ERROR|[TrailerLoad] Could not reach lane #B — leaving OutboundPallet_##bc#ec##d-#_# staged.
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' cross-door load: to trailer: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s).
  - ERROR|[TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[OrderSelectionTaskDriver] Order ##e##f#-a#bf-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] PltJack_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
- New this block: **17**
  - VEHICLE-STATIONARY|PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (aisle entry) (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (aisle entry) (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)

## Bugs found (30 distinct, 110 incidents)

| Incidents | Bug |
|---|---|
| 13 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 5 | ERROR — [GridNav] 'Employee_Larry Morgan': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 5 | ERROR — [GridNav] 'Employee_Larry Morgan': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 12 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 10 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 2 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 2 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (aisle entry) (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 2 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 2 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'bay ## (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 2 | ERROR — [GridNav] 'Employee_Shirley Edwards': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 5 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 4 | WARN — [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 3 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': no route for 'bay ## (aisle entry) (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': no route for 'bay ## (aisle entry)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': no route for 'bay ## (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | ERROR — [GridNav] 'Employee_Mark Peterson': no route for 'bay ##' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 2 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach bay ## after # tries with nothing picked - leaving the jack and handing the task back. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-16.77, 1.13, 38.79) — walking out to (-29.77, 1.10, 38.79) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-19.33, 1.16, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-8.96, 1.12, 58.74) — walking out to (-8.96, 1.20, 45.74) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-8.97, 1.12, 58.99) — walking out to (-8.97, 1.20, 45.99) (12.00m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 79/248.
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-36.63, 1.12, 48.22) — walking out to (-36.63, 0.04, 34.46) (12.75m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-36.40, 1.11, 42.34).
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 27 task(s) it could take wait
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-36.18, 1.11, 46.79) runs through something solid - taking a clear way out to (-34.80, 1.17, 46.79) instead.
- **WARN** (day 2): [AiNavigation] Employee_Larry Morgan: stuck at (-26.07, 1.13, 46.91) — walking out to (-19.61, 0.04, 34.58) (12.93m to mesh).
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone): stuck at (-9.72, 1.11, 57.63) — walking out to (-9.72, 1.16, 59.00) (12.24m to mesh).
- **ERROR** (day 2): [GridNav] 'Employee_Mark Peterson': no route for 'bay 02-02 (loop in)' from (-10.65, 1.11, 57.59) to (-17.59, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Mark Peterson': no route for 'bay 02-02 (aisle entry) (route)' from (-10.65, 1.11, 57.59) to (-17.59, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Mark Peterson': no route for 'bay 02-02 (aisle entry)' from (-10.65, 1.11, 57.59) to (-42.14, 1.11, 47.66) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 02-02' - driving straight to the stop instead.
- **ERROR** (day 2): [GridNav] 'Employee_Mark Peterson': no route for 'bay 02-02 (route)' from (-10.65, 1.11, 57.59) to (-17.59, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Mark Peterson': no route for 'bay 02-02' from (-10.65, 1.11, 57.59) to (-34.49, 1.11, 47.66) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': no route for 'bay 01-01 (aisle entry)' from (-9.30, 1.11, 57.59) to (-16.26, 1.11, 39.81) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- **ERROR** (day 2): [GridNav] 'Employee_Larry Morgan': no route for 'bay 01-01' from (-9.30, 1.11, 57.59) to (-21.26, 1.11, 39.81) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Shirley Edwards': no route for 'bay 02-02 (loop in)' from (-10.65, 1.11, 57.59) to (-17.59, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Shirley Edwards': no route for 'bay 02-02 (aisle entry) (route)' from (-10.65, 1.11, 57.59) to (-17.59, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Shirley Edwards': no route for 'bay 02-02 (aisle entry)' from (-10.65, 1.11, 57.59) to (-42.14, 1.11, 47.66) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 02-02' - driving straight to the stop instead.
- **ERROR** (day 2): [GridNav] 'Employee_Shirley Edwards': no route for 'bay 02-02 (route)' from (-10.65, 1.11, 57.59) to (-17.59, 1.11, 42.34) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [GridNav] 'Employee_Shirley Edwards': no route for 'bay 02-02' from (-10.65, 1.11, 57.59) to (-34.49, 1.11, 47.66) (NoPath, 1 expanded) - the jack will not drive it.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackGridLeg>d__4:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.GridLeg.cs:64)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **OVERLAP** (day 2): OVERLAP Pallet_03_Slot1_Tier1 & Pallet_01_Slot0_Tier1 @(28, 89)  <<HISTORY: d2 18:02 Putaway task created (STG1B-4->) by ReceiverReceivingWorkflow.CreatePutawayTask | d2 18:02 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 18:48 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 18:52 moved (41, 84)->(-1, -1) by InventoryService.MovePallet | d2 19:00 moved (-1, -1)->(29, 89) by InventoryService.MovePallet | d2 19:00 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d2 17:46 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 17:47 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 18:19 moved (41, 85)->(-1, -1) by InventoryService.MovePallet | d2 18:26 moved (-1, -1)->(29, 89) by InventoryService.MovePallet | d2 18:26 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 18:27 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach bay 01-08 after 5 tries with nothing picked - leaving the jack and handing the task back.

## Bot actions (first 400)
- d1 signed bulk BULK_Bramble & Bite Berry Co._D1_0
- d1 PO IG1973 Fairweather Trading Co.: 9 plt $4,992
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 26 door 3
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 26 door 2
- d1 PO IG1974 Halloran Household: 2 plt $1,120
- d1 placed parked Inbound Halloran Household day 1 block 36 door 1
- d1 PO IG1975 Halloran Household: 9 plt $4,992
- d1 placed parked Inbound Halloran Household day 2 block 0 door 1
- d2 PO IG2976 Meridian Provisions: 2 plt $1,152
- d2 placed parked Inbound Meridian Provisions day 2 block 6 door 1
- d2 PO IG2977 Meridian Provisions: 9 plt $5,088
- d2 placed parked Inbound Meridian Provisions day 2 block 14 door 1
- d2 PO IG2978 Millbrook Family Foods: 1 plt $544
- d2 placed parked Inbound Millbrook Family Foods day 2 block 24 door 1
- d2 PO IG2979 Millbrook Family Foods: 6 plt $3,264
- d2 placed parked Inbound Millbrook Family Foods day 2 block 32 door 1
