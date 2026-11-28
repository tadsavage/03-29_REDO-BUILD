# Playtest block 44 — days 1–3

## Score: **41.6 (+2.6)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (4 shipped, 0 late) | 10% | 100.0 (+20.0) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (15 plt vs target 24) | 10% | 62.5 (+8.3) |
| Revenue ($38,142) | not scored | 100.0 (0.0) |
| Net Profit (-40,207) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 19.0 (+0.1) |
| Reputation (19 to 24) | 10% | 58.3 (-3.3) |

Orders shipped **4**, late/fined **0**, pallets shipped **15**. Cash $127,764 → $154,508 (+26,744). Stock at cost $200,106 → $133,155 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 12 dock bookings, 1 orders released, 8 POs, 0 rush POs.

## Where the money went

Income **$38,142**. Expenses **$11,398** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 37,230 |
| Handling | 912 |

| Expense | $ |
|---|---|
| Inventory | 5,360 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Change since last block
- Fixed / no longer seen: **6**
  - VEHICLE-STATIONARY|PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - ERROR|[GridNav] 'Employee_Mark Peterson': no route for 'bay ## (loop in)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Mark Peterson cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
- New this block: **10**
  - WARN|[AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane #B: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s).
  - ERROR|[TrailerLoad] Could not reach lane #B — leaving OutboundPallet_##bc#ec##d-#_# staged.
  - ERROR|[TrailerLoad] 'DS_FULL(Clone)' cross-door load: to trailer: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s).
  - ERROR|[TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - WARN|[OrderSelectionTaskDriver] Order ##e##f#-a#bf-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] PltJack_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.

## Bugs found (21 distinct, 53 incidents)

| Incidents | Bug |
|---|---|
| 10 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 3 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane #B: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s). |
| 1 | ERROR — [TrailerLoad] Could not reach lane #B — leaving OutboundPallet_##bc#ec##d-#_# staged. |
| 1 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to trailer: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s). |
| 1 | ERROR — [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 2 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 2 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##e##f#-a#bf-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-8.71, 1.12, 58.48) — walking out to (-8.71, 1.20, 45.48) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-8.71, 1.12, 59.04) — walking out to (-8.71, 1.20, 46.04) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-26.33, 1.11, 50.19).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-37.85, 1.11, 51.02) — walking out to (-26.33, 1.11, 50.19) (11.55m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 105/248.
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-9.38, 1.14, 55.09) — walking out to (-9.38, 1.25, 42.09) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (1.55, 1.13, 56.87) — walking out to (-4.95, 1.23, 45.62) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone): stuck at (-38.05, 1.11, 43.21) — walking out to (-36.66, 1.18, 43.21) (12.00m to mesh).
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-19.33, 1.11, 56.33) runs through something solid - taking a clear way out to (-20.72, 1.16, 56.33) instead.
- **WARN** (day 2): [AiNavigation] Employee_Mark Peterson: stuck at (-31.90, 1.12, 49.02) — walking out to (-31.90, 0.04, 34.46) (13.56m to mesh).
- **ERROR** (day 2): [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to lane 1B: NO NAVMESH PATH to (-11.31, 1.19, 39.19) from (-8.41, 1.11, 39.18) (elapsed 30.0s).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__103:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1222)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] Could not reach lane 1B — leaving OutboundPallet_a99ac8a2-18bc-4ec5-861d-a92b572e82ae_1 staged.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<LoadOnePallet>d__96:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:832)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to trailer: NO NAVMESH PATH to (-1.44, 1.18, 38.89) from (-10.94, 1.19, 39.18) (elapsed 30.1s).
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
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-01 (aisle entry)' (7.4m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-26.04, 1.18, 38.92), elapsed=31.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=25.7, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__141.MoveNext () [0x00786] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2769 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 106.8s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 0f8ef8b0-2e70-43f8-a3bf-ff5c58588e0f (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at 39/51.
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (-17.59, 1.23, 42.34) (path invalid) — abandoning the walk and handing the task back.

## Bot actions (first 400)
- d1 signed bulk BULK_Grizzly Ridge Jerky_D1_0
- d1 PO IG1965 Anchor Point Foods: 1 plt $720
- d1 placed parked Inbound Anchor Point Foods day 1 block 25 door 1
- d1 placed parked Inbound Halloran Household day 1 block 26 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 2
- d1 placed parked Inbound Anchor Point Foods day 1 block 26 door 3
- d1 PO IG1966 Riverside Housewares: 1 plt $720
- d1 placed parked Inbound Riverside Housewares day 1 block 37 door 1
- d1 PO IG1967 Riverside Housewares: 1 plt $720
- d1 placed parked Inbound Riverside Housewares day 1 block 44 door 1
- d2 PO IG2968 Anchor Point Foods: 1 plt $768
- d2 placed parked Inbound Anchor Point Foods day 2 block 8 door 1
- d2 PO IG2969 Anchor Point Foods: 1 plt $768
- d2 placed parked Inbound Anchor Point Foods day 2 block 14 door 1
- d2 PO IG2970 Last Call Liquidators: 1 plt $768
- d2 placed parked Inbound Last Call Liquidators day 2 block 26 door 1
- d2 PO IG2971 Last Call Liquidators: 1 plt $768
- d2 placed parked Inbound Last Call Liquidators day 2 block 32 door 1
- d2 PO IG2972 Cornerstone Staples: 1 plt $128
- d2 placed parked Inbound Cornerstone Staples day 2 block 36 door 1
- d2 booked Grizzly Ridge Jerky day 3 block 11 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 5 pallet pick(s) / 2 reach truck(s): stock ready hour 42 + 8.8h of work)
- d2 released 1 order(s): OG4709 (trailer 10.8 h away; 8.8 h work + 2 h)
