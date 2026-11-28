# Playtest block 1091 — days 1–4

## Score: **52.3** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (10 shipped, 2 late) | 10% | 81.8 |
| Fill rate | 10% | 97.0 |
| Throughput (129 plt vs target 36) | 10% | 100.0 |
| Revenue ($246,055) | not scored | 100.0 |
| Net Profit (+99,156) | 10% | 100.0 |
| Cash health (floor $15,000) | 0% | 100.0 |
| Reliability (bugs) | 50% | 8.8 |
| Reputation (0 to 231) | 10% | 100.0 |

Orders shipped **10**, late/fined **2**, pallets shipped **129**. Cash $500,000 → $173,511 (-326,489). Stock at cost $0 → $98,693 (offsets stock bought in Net Profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went
*A save load zeroed the money ledger during this block, so this covers only the time since the load.*

Income **$141,757**. Expenses **$22,244** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 140,049 |
| Handling | 1,708 |

| Expense | $ |
|---|---|
| Inventory | 13,080 |
| Fines | 4,901 |
| Wages | 1,552 |
| Transportation | 1,076 |
| Maintenance | 803 |
| Lease / Mortgage | 420 |
| Groundskeeping | 402 |
| Loss Prevention | 10 |

## Late orders - state when it came due
- 2 x loaded but the truck had not left

- day 3: OG3014 (Barnacle Bill's Bait & Bite) was loaded but the truck had not left. Fined $2,772. Lines 21, 548 cs, released=True.
- day 3: OG3947 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $2,129. Lines 20, 518 cs, released=True.

## Bugs found (29 distinct, 165 incidents)

| Incidents | Bug |
|---|---|
| 98 | OVERLAP —  A Chep & A Chep @(#, #) |
| 20 | NO-RECORD —  lane pallet #E# A Chep |
| 10 | NO-RECORD —  lane pallet #D# A Chep |
| 9 | OVERLAP —  A Chep & A Chep @#E# |
| 3 | NO-RECORD —  lane pallet #C# Pallet_#_Slot#_Tier# |
| 1 | EXCEPTION — MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it. |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #A: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #B: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#] lane #C: [#: own/Staged/y#] lane #E: [#: own/Staged/y#] lane #D: [#: own/Staged/y#] lane #C: [#: own/Staged/y#]  |
| 2 | ORPHAN-OUTBOUND —  #D# (order ##b#c##f-# gone) |
| 1 | NO-RECORD —  lane pallet #D# Pallet_#_Slot#_Tier# |
| 1 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | ERROR — [MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | ERROR — [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot. |
| 1 | ERROR — [TrailerLoad] Could not reach lane #B (far end) — leaving Pallet_#_Slot#_Tier# staged. |
| 1 | ERROR — [TrailerLoad] Could not reach lane #C — leaving A Chep staged. |
| 1 | ERROR — [TrailerLoad] Could not reach lane #D — leaving A Chep staged. |
| 1 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | NO-TASK —  lane pallet #D# sku=# load=# |
| 1 | REPEATING — x# and counting: [MheTravel] 'RT_Full(Clone)' pallet pick: → lane #A: stalled # s, # m short of (#, #, #) - giving the leg up. [vel #, want #, stopped False, prio #, r #] near: agent Employee_Ryan Smith(off) #m r#; |
| 1 | REPEATING — x# and counting: [MheTravel] 'RT_Full(Clone)' pallet pick: → lane #A: did not arrive (status PathComplete) from (#, #, #) to (#, #, #); start on mesh: True, a static query says PathComplete, agent.CalculatePath says True/PathComplete, agent pos (#, #, #), destination (#, #, #), agent on mesh: True enabled: True. |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] PalletPick: no path to lane #A. Putting the pallet back and retrying in #s. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Martha Clark': could not reach the mouth of lane #A (try #/#) - waiting, then trying again. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###d##efa-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# (all held by other selectors / planned elsewhere) [###:# ] # x# (all held by other selectors / planned elsewhere) [###:# ] # x# (all held by other selectors / planned elsewhere) [###:# ] # x# (# held by other selectors / planned elsewhere) [###:#(behind) ###:# ] # x# [###:#(behind) ###:# ] # x# [###:#(behind) ] # x# (all held by other selectors / planned elsewhere) [###:#(behind) ] # x# (all held by other selectors / planned elsewhere) [###:# ] # x# (all held by other selectors / planned elsewhere) [###:#(behind) ] # x# (all held by other selectors / planned elsewhere) [###:# ] ... |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Betty Peterson': could not reach the mouth of lane #A (try #/#) - waiting, then trying again. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-bac##e##d### (Pony Express Oats) has no reachable pick location for any line item and nothing was picked — returning it to Open. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###d##efa-# (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Betty Peterson': could not reach the mouth of lane #B (try #/#) - waiting, then trying again. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##b#c##f-# (Salty Sailor Pretzel Co.) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Pamela Cartwright': could not reach the mouth of lane #A (try #/#) - waiting, then trying again. |

## First sightings (with stack traces)

- **OVERLAP** (day 3): OVERLAP A Chep & A Chep @(23, 83)  <<HISTORY: d3 04:50 restored from save at (23, 83) 14x347610 by InventoryService.RegisterPalletDirect | d3 04:50 restored from save at (23, 83) 14x347610 by InventoryService.RegisterPalletDirect | d3 04:50 restored from save at (23, 83) 14x347610 by InventoryService.RegisterPalletDirect>>  <<HISTORY: d3 04:50 restored from save at (23, 83) 14x347610 by InventoryService.RegisterPalletDirect | d3 04:50 restored from save at (23, 83) 14x347610 by InventoryService.RegisterPalletDirect | d3 04:50 restored from save at (23, 83) 14x347610 by InventoryService.RegisterPalletDirect>>
- **OVERLAP** (day 3): OVERLAP A Chep & A Chep @1E-2  <<HISTORY: d3 04:50 restored from save at (44, 82) 35x792146 by InventoryService.RegisterPalletDirect | d3 04:50 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 04:50 restored from save at (44, 82) 35x792146 by InventoryService.RegisterPalletDirect | d3 04:50 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 04:50 restored from save at (44, 82) 35x792146 by InventoryService.RegisterPalletDirect>>  <<HISTORY: d3 04:50 restored from save at (44, 82) 35x792146 by InventoryService.RegisterPalletDirect | d3 04:50 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 04:50 restored from save at (44, 82) 35x792146 by InventoryService.RegisterPalletDirect | d3 04:50 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 04:50 restored from save at (44, 82) 35x792146 by InventoryService.RegisterPalletDirect>>
- **WARN** (day 3): [OrderService] Order OG3016 closed with a STAGED pallet (5 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 3): ORPHAN-OUTBOUND 3D-1 (order f8be4c39-051b-4c39-964f-ab43f58c6afc gone)
- **ERROR** (day 3): [TrailerLoad] Could not reach lane 2C — leaving A Chep staged.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<LoadOnePallet>d__96:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:832)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 3): [TrailerLoad] Could not reach lane 2D — leaving A Chep staged.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<LoadOnePallet>d__96:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:832)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **NO-TASK** (day 3): NO-TASK lane pallet 1D-2 sku=148972 load=0947578984  <<HISTORY: d3 12:28 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 14:24 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 14:25 Putaway task created (STG1D-2->) by OrderService.RefileStrandedLanePallets | d3 14:40 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 14:40 PalletPick task created (STG1D-2->3A) by OrderService.RefilePalletPickShortfalls | d3 14:49 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **WARN** (day 3): [OrderSelectionTaskDriver] 'Employee_Pamela Cartwright': could not reach the mouth of lane 2A (try 1/4) - waiting, then trying again.
- **REPEATING** (day 3): x25 and counting: [MheTravel] 'RT_Full(Clone)' pallet pick: → lane 1A: stalled 15 s, 15.0 m short of (-12.64, 1.24, 48.99) - giving the leg up. [vel 4.00, want 4.00, stopped False, prio 52, r 1.00] near: agent Employee_Ryan Smith(off) 0.2m r0.25;
- **REPEATING** (day 3): x25 and counting: [MheTravel] 'RT_Full(Clone)' pallet pick: → lane 1A: did not arrive (status PathComplete) from (-26.17, 1.11, 42.57) to (-12.64, 1.24, 48.99); start on mesh: True, a static query says PathComplete, agent.CalculatePath says True/PathComplete, agent pos (-26.17, 1.13, 42.57), destination (-12.64, 1.24, 48.99), agent on mesh: True enabled: True.
- **REPEATING** (day 3): x25 and counting: [ReachTruckOperator] PalletPick: no path to lane 1A. Putting the pallet back and retrying in 15s.

## Bot actions (first 400)
