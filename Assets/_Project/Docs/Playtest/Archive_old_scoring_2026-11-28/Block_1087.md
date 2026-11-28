# Playtest block 1087 — days 1–8

## Score: **49.5** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (15 shipped, 16 late) | 15% | 11.1 |
| Fill rate | 10% | 75.6 |
| Throughput (79 plt vs target 84) | 15% | 94.0 |
| Revenue ($147,303) | not scored | 100.0 |
| Net Profit (+17,093) | 10% | 100.0 |
| Cash health (floor $15,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 4.9 |
| Reputation (0 to 0) | 5% | 50.0 |

Orders shipped **15**, late/fined **16**, pallets shipped **79**. Cash $500,000 → $153,651 (-346,349). Stock at cost $0 → $69,485 (offsets stock bought in Net Profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went
*A save load zeroed the money ledger during this block, so this covers only the time since the load.*

Income **$74,036**. Expenses **$45,477** (stock bought shows as Inventory). Loan principal repaid **$5,000** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 70,770 |
| Handling | 3,266 |

| Expense | $ |
|---|---|
| Inventory | 28,522 |
| Lease / Mortgage | 7,776 |
| Loan Principal | 5,000 |
| Fines | 3,558 |
| Transportation | 2,532 |
| Wages | 1,768 |
| Maintenance | 981 |
| Groundskeeping | 328 |
| Loss Prevention | 12 |

## Late orders - state when it came due
- 2 x loaded but the truck had not left

- day 7: OG7450 (Barnacle Bill's Bait & Bite) was loaded but the truck had not left. Fined $2,011. Lines 50, 448 cs, released=True.
- day 7: OG7451 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,547. Lines 21, 385 cs, released=True.

## Bugs found (74 distinct, 201 incidents)

| Incidents | Bug |
|---|---|
| 7 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #A: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#]  |
| 16 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 15 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 5 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 12 | WARN — [AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | FLOATING —  OutboundPallet_#-bb###bd-#_# @#D# bottom=# |
| 4 | OVERLAP —  OutboundPallet_#-bb###bd-#_# & OutboundPallet_#-bb###bd-#_# @#D# |
| 9 | WARN — [AiNavigation] Employee_Amy Perez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] Employee_Jonathan Cooper: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop. |
| 8 | WARN — [AiNavigation] Employee_Justin Reyes: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #A: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#]  |
| 7 | WARN — [AiNavigation] Employee_Brandon Thompson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 2 | FLOATING —  OutboundPallet_#-bb###bd-#_# @#E# bottom=# |
| 2 | OVERLAP —  OutboundPallet_#-bb###bd-#_# & OutboundPallet_#-bb###bd-#_# @#E# |
| 2 | ERROR — [ReachTruckOperator] 'ReachTruck' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=False, navEnabled=True, seekingTask=True, pathStatus=n/a, hasPath=False, remaining=n/a, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#a] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 2 | FLOATING —  A Chep @#D# bottom=# |
| 2 | OVERLAP —  A Chep & A Chep @#D# |
| 5 | WARN — [OrderSelectionTaskDriver] Order #-bb###bd-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 4 | WARN — [AiNavigation] Employee_Catherine Miller: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [OrderSelectionTaskDriver] Order ##caa#a-b#-# (Blocky Bakeshop) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 4 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 2 | ORPHAN-OUTBOUND —  #D# (order #-bb###bd-# gone) |
| 4 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [ReachTruckOperator] 'ReachTruck' pallet pick: → lane #B: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#a] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack did not reach 'bay ##' ((#, #, #)) after #s. |
| 1 | ERROR — [ReachTruckOperator] 'ReachTruck' replenish: → reserve ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at EquipmentBattery.ParkNav () [#x#f] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\EquipmentBattery.cs:# < at EquipmentBattery+<NavTo>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\EquipmentBattery.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 3 | WARN — [OrderSelectionTaskDriver] Order #-d#a##a#-b#a#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane #D exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | FLOATING —  OutboundPallet_###af-bce###_# @#C# bottom=# |
| 1 | OVERLAP —  OutboundPallet_###af-bce###_# & OutboundPallet_###af-bce###_# @#C# |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-B# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=True, lastSeekCancelledBy=[at AiNavigation.Update () [#x#a] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 2 | WARN — [AiNavigation] PalletJack cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Order OG# pallet # of #: every pick face is empty - handing the task back until replenishment stocks them. |
| 2 | WARN — [AiNavigation] PalletJack: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Jonathan Cooper' made no progress on order OG# for # game minutes - handing the task back to the queue. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Amy Perez: receive workflow cancelled (out of range) — releasing the task and resuming patrol. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Gregory Dixon' made no progress on order OG# for # game minutes - handing the task back to the queue. |
| 1 | REPEATING — x# and counting: [AiNavigation] PalletJack: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] 'Employee_Catherine Miller' made no progress on order OG# for # game minutes - handing the task back to the queue. |
| 2 | WARN — [AiNavigation] Employee_Gregory Dixon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ORPHAN-OUTBOUND —  #B# (order #-b#b#b#-a#d-# gone) |
| 2 | WARN — [AiNavigation] Dockstocker: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ##' - re-issuing the drive (#/#). |
| 1 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: no progress toward task target (#, #, #) for #s (stopped #m short, path PathComplete) — handing the task back. |
| 2 | WARN — [AiNavigation] Employee_Catherine Miller cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ORPHAN-OUTBOUND —  #C# (order #-d#a##a#-b#a#-# gone) |
| 1 | REPEATING — x# and counting: [AiNavigation] PltJack_FULL(Clone): task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Catherine Miller cannot reach equipment at (#, #, #) (path invalid) — seeking next available |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##b##be#a### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Justin Reyes cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##e#d#a##a### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##cd##c#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##b##d#-b### (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###f#-a#b### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Gregory Dixon cannot reach equipment at (#, #, #) (path invalid) — seeking next available |
| 1 | WARN — [AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##ad#-bd#e-# (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-a##b#### (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-d#a##a#-b#a#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [AiNavigation] DS_FULL(Clone): task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Justin Reyes cannot reach equipment at (#, #, #) (path invalid) — seeking next available |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Justin Reyes' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###af-bce### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Employee_Gregory Dixon cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##cd##-b### (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **WARN** (day 7): [AiNavigation] Employee_Catherine Miller cannot reach task position at (-15.09, 1.08, 35.29) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 7): [AiNavigation] PltJack_FULL(Clone): task target (-31.86, 1.16, 40.91) is not reachable — the partial path ends 18.56m short of it. Handing the task back.
- **WARN** (day 7): [AiNavigation] PltJack_FULL(Clone): stuck at (-20.44, 1.11, 41.89) — walking out to (-9.18, 1.23, 48.39) (12.00m to mesh).
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 52e4791d-d2a1-4a60-b8a6-e0c9c1d4cbd0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 172/441 case(s).
- **WARN** (day 7): [AiNavigation] DS_FULL(Clone): stuck at (-18.83, 1.11, 41.89) — walking out to (-13.54, 1.11, 35.50) (7.94m to mesh).
- **WARN** (day 7): [AiNavigation] RT_Full(Clone): stuck at (-26.21, 1.11, 41.89) — walking out to (-14.95, 1.23, 48.39) (12.00m to mesh).
- **WARN** (day 7): [AiNavigation] DS_FULL(Clone): task target (-41.79, 1.09, 36.39) is not reachable — the partial path ends 28.51m short of it. Handing the task back.
- **WARN** (day 7): [AiNavigation] Employee_Justin Reyes cannot reach equipment at (-37.16, 1.11, 40.91) (path invalid) — seeking next available
- **ORPHAN-OUTBOUND** (day 7): ORPHAN-OUTBOUND 1C-1 (order 52e4791d-d2a1-4a60-b8a6-e0c9c1d4cbd0 gone)
- **WARN** (day 7): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Justin Reyes' as its operator though nobody is on it or running it - clearing the stale link.
- **REPEATING** (day 7): x25 and counting: [AiNavigation] PltJack_FULL(Clone): task target (-3.07, 1.16, 50.96) is not reachable — the partial path ends 18.56m short of it. Handing the task back.
- **WARN** (day 7): [AiNavigation] RT_Full(Clone) cannot reach task position at (-25.89, 1.16, 40.71) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 7): [OrderSelectionTaskDriver] Order a65a70e4-3687-43af-bce9-78a9508bb15d (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 315/385.
- **WARN** (day 7): [AiNavigation] Employee_Gregory Dixon cannot reach task position at (-0.07, 1.11, 49.96) (path invalid) — abandoning the walk and handing the task back.
- **ERROR** (day 7): [ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane 3D exit: NO NAVMESH PATH. anchor=(8.56, 1.11, 48.99) → driveTarget=(8.56, 1.21, 48.99), from=(0.65, 1.11, 44.42), elapsed=30.0s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=9.5, reissues=1, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__128.MoveNext () [0x00681] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2760 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 9.6s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 7): [AiNavigation] DS_FULL(Clone) cannot reach task position at (-47.09, 1.12, 49.64) (path invalid) — abandoning the walk and handing the task back.
- **FLOATING** (day 7): FLOATING A Chep @2D-1 bottom=1.92  <<HISTORY: d7 02:18 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 04:31 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 04:47 moved (28, 82)->(-1, -1) by InventoryService.MovePallet | d7 05:06 adopted as outbound freight of order a65a70e4-3687-43af-bce9-78a9508bb15d (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 05:06 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 05:06 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 7): OVERLAP A Chep & A Chep @2D-1  <<HISTORY: d7 02:18 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 04:31 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 04:47 moved (28, 82)->(-1, -1) by InventoryService.MovePallet | d7 05:06 adopted as outbound freight of order a65a70e4-3687-43af-bce9-78a9508bb15d (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 05:06 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 05:06 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d7 02:18 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 04:16 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 04:33 moved (28, 83)->(-1, -1) by InventoryService.MovePallet | d7 04:48 adopted as outbound freight of order a65a70e4-3687-43af-bce9-78a9508bb15d (24 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 04:48 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 04:48 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 7): FLOATING OutboundPallet_a65a70e4-3687-43af-bce9-78a9508bb15d_1 @2C-4 bottom=2.22
- **OVERLAP** (day 7): OVERLAP OutboundPallet_a65a70e4-3687-43af-bce9-78a9508bb15d_1 & OutboundPallet_a65a70e4-3687-43af-bce9-78a9508bb15d_1 @2C-4
- **ERROR** (day 7): [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-05-B0 anchor: NO NAVMESH PATH. anchor=(-33.84, 5.11, 47.34) → driveTarget=(-33.84, 1.16, 47.34), from=(-41.33, 1.11, 43.87), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=5.9, reissues=0, batteryNeedsCharge=True, lastSeekCancelledBy=[at AiNavigation.Update () [0x0024a] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:992 < ] 38.9s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2788)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 54c95f85-7cd0-4236-b604-3a9e379acae6 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 196/448.

## Bot actions (first 400)
