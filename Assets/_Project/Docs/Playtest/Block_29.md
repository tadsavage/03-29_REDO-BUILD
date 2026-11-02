# Playtest block 29 — days 1–8

## Score: **36.7 (-22.7)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (3 shipped, 7 late) | 15% | 0.0 (-66.7) |
| Fill rate | 10% | 92.0 (+12.1) |
| Throughput (45 plt vs target 84) | 15% | 53.6 (-46.4) |
| Revenue ($35,588) | 15% | 63.6 (-28.1) |
| Profit (cash -185,006) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 9.9 (-0.9) |
| Reputation (0 to 0) | 5% | 50.0 (-50.0) |

Orders shipped **3**, late/fined **7**, pallets shipped **45**. Cash $210,868 → $25,862 (-185,006). Stock at cost $0 → $42,788 (not part of profit).
Bot did: 2 contracts signed, 72 dock bookings, 9 orders released, 57 POs, 0 rush POs.

## Late orders - state when it came due
- 5 x staged but no trailer loaded it
- 1 x loaded but the truck had not left
- 1 x still loading

- day 2: OG2327 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,707. Lines 16, 401 cs, released=True.
- day 3: OG3328 (Sneaky Pete's Seafood) was staged but no trailer loaded it (door 1). Fined $2,015. Lines 19, 462 cs, released=True.
- day 4: OG4329 (Sneaky Pete's Seafood) was staged but no trailer loaded it (door 1). Fined $1,740. Lines 17, 416 cs, released=True.
- day 5: OG5330 (Sneaky Pete's Seafood) was staged but no trailer loaded it (door 1). Fined $2,223. Lines 14, 520 cs, released=True.
- day 6: OG7332 (Fizz-Pop Soda Works) was still loading when it came due. Fined $4,894. Lines 3, 733 cs, released=True.
- day 6: OG6331 (Sneaky Pete's Seafood) was staged but no trailer loaded it (door 1). Fined $1,717. Lines 14, 353 cs, released=True.
- day 7: OG7333 (Sneaky Pete's Seafood) was staged but no trailer loaded it (door 1). Fined $1,918. Lines 19, 418 cs, released=True.

## Change since last block
- Fixed / no longer seen: **17**
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A#
  - FLOATING| Pallet_#_Slot#_Tier# @#A# bottom=#
  - OVERLAP| OutboundPallet_#-dcc##a-a#b##_# & OutboundPallet_#-dcc##a-a#b##_# @#C#
  - OVERLAP| OutboundPallet_#-e##a#-a#c#-#_# & OutboundPallet_#-e##a#-a#c#-#_# @#A#
  - FLOATING| OutboundPallet_#-dcc##a-a#b##_# @#C# bottom=#
  - FLOATING| OutboundPallet_#-e##a#-a#c#-#_# @#A# bottom=#
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_#-e##e#-a#b##_# @#B#
  - FLOATING| Pallet_#_Slot#_Tier# @#D# bottom=#
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_#-e##e#-a#b##_# @#D#
  - FLOATING| OutboundPallet_#-e##e#-a#b##_# @#B# bottom=#
  - OVERLAP| OutboundPallet_#-e##e#-a#b##_# & OutboundPallet_#-e##e#-a#b##_# @#B#
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_##ea###ea#cda#_# @#A#
  - OVERLAP| OutboundPallet_##ea###ea#cda#_# & OutboundPallet_##ea###ea#cda#_# @#A#
  - NO-TASK| lane pallet #B# sku=# load=#
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #A and it never freed up — giving up and re-queueing.
- New this block: **27**
  - ERROR|[ReachTruckOperator] 'ReachTruck' pallet pick: → lane #D: destination did not take — no path and none pending for driveTarget=(#, #, #) (anchor=(#, #, #), from=(#, #, #), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  - ERROR|[TrailerLoad] 'Dockstocker' cross-door load: to lane #C: destination did not take (no path) for (#, #, #) from (#, #, #).
  - ERROR|[TrailerLoad] Could not reach lane #C — leaving OutboundPallet_#-f#e##b#-bd###_# staged.
  - ERROR|[ReachTruckOperator] 'ReachTruck' deliver: → location ### anchor: destination did not take — no path and none pending for driveTarget=(#, #, #) (anchor=(#, #, #), from=(#, #, #), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  - VEHICLE-STATIONARY|Dockstocker has a driver, hasn't moved in #s while # task(s) it could take wait
  - FLOATING| OutboundPallet_###bda#f#-#_# @#A# bottom=#
  - OVERLAP| OutboundPallet_###bda#f#-#_# & OutboundPallet_###bda#f#-#_# @#A#
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #D: [#: own/Staged/y#, own/Staged/y#] lane #A: [#: own/Staged/y#, own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #D: [#: own/Staged/y#, own/Staged/y#] lane #A: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #D: [#: own/Staged/y#, own/Staged/y#] lane #A: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #E: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #E: [#: own/Staged/y#, own/Staged/y#] lane #B: [#: OTHER ###bda#f#-#/Staged/y#, OTHER ###bda#f#-#/Staged/y#] [#: OTHER ###bda#f#-#/Staged/y#, OTHER ###bda#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#] 
  - ERROR|[OrderSelectionTaskDriver] Pallet jack: no path to (#, #, #) for 'pallet jack parking'.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack: no path to (#, #, #) for 'bay ##'.

## Bugs found (31 distinct, 43 incidents)

| Incidents | Bug |
|---|---|
| 2 | ERROR — [ReachTruckOperator] 'ReachTruck' pallet pick: → lane #D: destination did not take — no path and none pending for driveTarget=(#, #, #) (anchor=(#, #, #), from=(#, #, #), onMesh=True). Aborting leg immediately instead of waiting out the timeout. |
| 2 | ERROR — [TrailerLoad] 'Dockstocker' cross-door load: to lane #C: destination did not take (no path) for (#, #, #) from (#, #, #). |
| 2 | ERROR — [TrailerLoad] Could not reach lane #C — leaving OutboundPallet_#-f#e##b#-bd###_# staged. |
| 2 | ERROR — [ReachTruckOperator] 'ReachTruck' deliver: → location ### anchor: destination did not take — no path and none pending for driveTarget=(#, #, #) (anchor=(#, #, #), from=(#, #, #), onMesh=True). Aborting leg immediately instead of waiting out the timeout. |
| 3 | VEHICLE-STATIONARY — Dockstocker has a driver, hasn't moved in #s while # task(s) it could take wait |
| 2 | FLOATING —  OutboundPallet_###bda#f#-#_# @#A# bottom=# |
| 2 | OVERLAP —  OutboundPallet_###bda#f#-#_# & OutboundPallet_###bda#f#-#_# @#A# |
| 5 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #B: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #D: [#: own/Staged/y#, own/Staged/y#] lane #A: [#: own/Staged/y#, own/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #B: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #D: [#: own/Staged/y#, own/Staged/y#] lane #A: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #B: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #D: [#: own/Staged/y#, own/Staged/y#] lane #A: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #C: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #E: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #D: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #E: [#: own/Staged/y#, own/Staged/y#] lane #B: [#: OTHER ###bda#f#-#/Staged/y#, OTHER ###bda#f#-#/Staged/y#] [#: OTHER ###bda#f#-#/Staged/y#, OTHER ###bda#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#]  |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack: no path to (#, #, #) for 'pallet jack parking'. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack: no path to (#, #, #) for 'bay ##'. |
| 1 | ERROR — [ReachTruckOperator] 'ReachTruck' deliver: → location ##-A# anchor: destination did not take — no path and none pending for driveTarget=(#, #, #) (anchor=(#, #, #), from=(#, #, #), onMesh=True). Aborting leg immediately instead of waiting out the timeout. |
| 1 | ERROR — [TrailerLoad] 'Dockstocker' cross-door load: to trailer: destination did not take (no path) for (#, #, #) from (#, #, #). |
| 1 | ERROR — [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 1 | FLOATING —  OutboundPallet_###bda#f#-#_# @#B# bottom=# |
| 1 | OVERLAP —  OutboundPallet_###bda#f#-#_# & OutboundPallet_###bda#f#-#_# @#B# |
| 1 | ERROR — [TrailerLoad] 'Dockstocker' cross-door load: to lane #A: destination did not take (no path) for (#, #, #) from (#, #, #). |
| 1 | ERROR — [TrailerLoad] Could not reach lane #A — leaving OutboundPallet_###bda#f#-#_# staged. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#E# |
| 1 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jonathan Moore' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReachTruckOperator] Replenish: could not reach pallet at ##-A# within #m. |
| 1 | WARN — [AiNavigation] Employee_Pamela Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack: no path to (-0.07, 1.11, 50.96) for 'pallet jack parking'.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__96:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1256)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jonathan Moore' as its operator though nobody is on it or running it - clearing the stale link.
- **ERROR** (day 2): [ReachTruckOperator] 'ReachTruck' pallet pick: → lane 1D: destination did not take — no path and none pending for driveTarget=(-9.99, 1.12, 48.99) (anchor=(-9.99, 1.09, 48.99), from=(-24.56, 1.15, 40.71), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2683)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **OVERLAP** (day 2): OVERLAP Pallet_04_Slot4_Tier0 & Pallet_05_Slot4_Tier1 @(25, 82)  <<HISTORY: d1 22:12 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 01:18 Replenish task created (01-12-A0->01-12-00) by ReplenishmentService.CreateReplenishTask | d2 02:08 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 02:23 moved (25, 82)->(-1, -1) by InventoryService.MovePallet | d2 02:37 moved (-1, -1)->(25, 82) by InventoryService.MovePallet | d2 02:37 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d2 01:13 picked 1 (left 4) by InventoryService.PickFromPallet | d2 01:14 picked 1 (left 3) by InventoryService.PickFromPallet | d2 01:14 picked 1 (left 2) by InventoryService.PickFromPallet | d2 01:15 picked 1 (left 1) by InventoryService.PickFromPallet | d2 01:16 picked 1 (left 0) by InventoryService.PickFromPallet | d2 01:16 record destroyed by InventoryService.DestroyPallet>>
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack: no path to (-26.54, 1.11, 46.44) for 'bay 02-05'.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__96:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1256)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [ReachTruckOperator] 'ReachTruck' deliver: → location 02-09-A1 anchor: destination did not take — no path and none pending for driveTarget=(-27.21, 1.13, 47.34) (anchor=(-27.21, 3.11, 47.34), from=(-4.69, 1.16, 48.99), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2683)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **DOOR-DEADLOCK** (day 3): OG3328 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1B: [1: own/Staged/y2.2, own/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.3] [3: own/Staged/y2.2, own/Staged/y1.2] [4: own/Staged/y2.0, own/Staged/y1.2] [5: own/Staged/y1.2, own/Staged/y1.9] lane 1D: [1: own/Staged/y1.2, own/Staged/y2.8] lane 1A: [1: own/Staged/y2.6, own/Staged/y1.2] 
- **DOOR-DEADLOCK** (day 3): OG3328 is Staged but door 1's truck has waited 105 min with no one loading it. | lane 1B: [1: own/Staged/y2.2, own/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.3] [3: own/Staged/y2.2, own/Staged/y1.2] [4: own/Staged/y2.0, own/Staged/y1.2] [5: own/Staged/y1.2, own/Staged/y1.9] lane 1D: [1: own/Staged/y1.2, own/Staged/y2.8] lane 1A: [1: own/Staged/y2.6, own/Staged/y1.2] [2: own/Staged/y1.2] 
- **DOOR-DEADLOCK** (day 3): OG3328 is Staged but door 1's truck has waited 435 min with no one loading it. | lane 1B: [1: own/Staged/y2.2, own/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y2.3] [3: own/Staged/y2.2, own/Staged/y1.2] [4: own/Staged/y2.0, own/Staged/y1.2] [5: own/Staged/y1.2, own/Staged/y1.9] lane 1D: [1: own/Staged/y1.2, own/Staged/y2.8] lane 1A: [1: own/Staged/y2.6, own/Staged/y1.2] [2: own/Staged/y1.2, own/Staged/y1.6] 
- **WARN** (day 3): [AiNavigation] Dockstocker: stuck at (-2.04, 1.11, 40.36) — walking out to (4.46, 1.25, 51.00) (12.00m to mesh).
- **ERROR** (day 4): [TrailerLoad] 'Dockstocker' cross-door load: to trailer: destination did not take (no path) for (-2.64, 1.12, 38.89) from (-9.99, 1.16, 39.19).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__100:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1145)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 4): [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<PlaceCarriedInTrailer>d__96:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:977)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **DOOR-DEADLOCK** (day 4): OG4329 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1C: [1: own/Staged/y1.2, own/Staged/y2.4] [2: own/Staged/y1.2, own/Staged/y2.5] [3: own/Staged/y1.2, own/Staged/y2.1] [4: own/Staged/y2.2, own/Staged/y1.2] [5: own/Staged/y1.2, own/Staged/y1.5] lane 1E: [1: own/Staged/y1.2, own/Staged/y2.3] [2: own/Staged/y1.2, own/Staged/y2.3] 
- **WARN** (day 5): [AiNavigation] ReachTruck: stuck at (-35.16, 1.11, 40.01) — walking out to (-28.68, 1.16, 51.00) (12.31m to mesh).
- **ERROR** (day 5): [TrailerLoad] 'Dockstocker' cross-door load: to lane 1C: destination did not take (no path) for (-11.31, 1.12, 39.19) from (7.84, 1.16, 38.89).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__100:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1145)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 5): [TrailerLoad] Could not reach lane 1C — leaving OutboundPallet_c23261e7-f4e9-4b11-bd33-236b8f6bf425_1 staged.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<LoadOnePallet>d__95:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:830)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 5): [ReachTruckOperator] Replenish: could not reach pallet at 02-03-A0 within 0.15m.
- **TRUCK-OVERLAP** (day 6): TRUCK-OVERLAP Truck→PO_IG6514 & Truck→PO_IG6513 at (-2.3, 0.0, 18.5)
- **ERROR** (day 6): [ReachTruckOperator] 'ReachTruck' deliver: → location 01-04-00 anchor: destination did not take — no path and none pending for driveTarget=(-21.91, 1.12, 40.01) (anchor=(-21.91, 1.11, 40.01), from=(-3.36, 1.16, 48.99), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2683)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 6): Dockstocker has a driver, hasn't moved in 243s while 6 task(s) it could take wait
- **WARN** (day 7): [AiNavigation] Employee_Pamela Garcia: stuck at (-27.20, 1.16, 42.13) — walking out to (-38.46, 1.09, 35.63) (12.00m to mesh).
- **DOOR-DEADLOCK** (day 7): OG7333 is Staged but door 1's truck has waited 90 min with no one loading it. | lane 1D: [1: own/Staged/y1.2, own/Staged/y2.6] [2: own/Staged/y1.2, own/Staged/y2.3] [3: own/Staged/y1.2, own/Staged/y1.9] [4: own/Staged/y2.0, own/Staged/y1.2] [5: own/Staged/y1.5, own/Staged/y1.2] lane 1E: [1: own/Staged/y1.2, own/Staged/y2.6] lane 1B: [1: OTHER d369c64a-6779-4bda-85f7-c7bf4b0a91e3/Staged/y2.7, OTHER d369c64a-6779-4bda-85f7-c7bf4b0a91e3/Staged/y1.2] [2: OTHER d369c64a-6779-4bda-85f7-c7bf4b0a91e3/Staged/y1.8, OTHER d369c64a-6779-4bda-85f7-c7bf4b0a91e3/Staged/y1.2] [3: own/Staged/y2.3, own/Staged/y1.2] [4: own/Staged/y1.2, own/Staged/y1.8] [5: own/Staged/y1.2] 
- **OVERLAP** (day 7): OVERLAP Pallet_00_Slot0_Tier0 & Pallet_01_Slot1_Tier0 @1B-1  <<HISTORY: d5 05:10 Putaway task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 06:04 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 06:22 moved (29, 78)->(-1, -1) by InventoryService.MovePallet | d5 06:41 adopted as outbound freight of order d369c64a-6779-4bda-85f7-c7bf4b0a91e3 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 06:41 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 06:41 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d5 04:14 PalletPick task Open->Available by OrderService.AllocateHeldWork | d5 06:15 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 06:35 moved (25, 82)->(-1, -1) by InventoryService.MovePallet | d5 06:59 adopted as outbound freight of order d369c64a-6779-4bda-85f7-c7bf4b0a91e3 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 06:59 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 06:59 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 7): FLOATING Pallet_01_Slot1_Tier0 @1B-1 bottom=2.57  <<HISTORY: d5 04:14 PalletPick task Open->Available by OrderService.AllocateHeldWork | d5 06:15 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 06:35 moved (25, 82)->(-1, -1) by InventoryService.MovePallet | d5 06:59 adopted as outbound freight of order d369c64a-6779-4bda-85f7-c7bf4b0a91e3 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 06:59 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 06:59 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 7): FLOATING OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 @1A-5 bottom=2.17
- **OVERLAP** (day 7): OVERLAP OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 & OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 @1A-5
- **FLOATING** (day 7): FLOATING OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 @1B-2 bottom=1.66
- **OVERLAP** (day 7): OVERLAP OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 & OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 @1B-2
- **ERROR** (day 7): [TrailerLoad] 'Dockstocker' cross-door load: to lane 1A: destination did not take (no path) for (-13.96, 1.12, 39.19) from (-2.64, 1.16, 38.89).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__100:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1145)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 7): [TrailerLoad] Could not reach lane 1A — leaving OutboundPallet_d369c64a-6779-4bda-85f7-c7bf4b0a91e3_1 staged.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<LoadOnePallet>d__95:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:830)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **OVERLAP** (day 7): OVERLAP Pallet_01_Slot1_Tier0 & Pallet_02_Slot2_Tier0 @3E-1  <<HISTORY: d7 20:22 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 20:23 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 21:03 moved (25, 82)->(-1, -1) by InventoryService.MovePallet | d7 21:46 adopted as outbound freight of order 8f8bf42e-61d4-46d9-88f2-463b3642a5dd (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 21:46 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 21:46 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d7 20:22 PalletPick task Open->Available by OrderService.AllocateHeldWork | d7 20:37 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d7 21:06 moved (23, 78)->(-1, -1) by InventoryService.MovePallet | d7 21:46 adopted as outbound freight of order 8f8bf42e-61d4-46d9-88f2-463b3642a5dd (8 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 21:46 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 21:46 record destroyed by InventoryService.DestroyPallet>>

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1473 Blue Ridge Provisions: 4 plt $8,178
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 18 door 1
- d1 PO IG1474 Bulk Basin Foods: 10 plt $3,678
- d1 placed parked Inbound Bulk Basin Foods day 1 block 19 door 2
- d1 PO IG1475 Fairweather Trading Co.: 9 plt $7,490
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 20 door 3
- d1 PO IG1476 Bulk Basin Foods: 3 plt $2,012
- d1 placed parked Inbound Bulk Basin Foods day 1 block 22 door 1
- d1 PO IG1477 Homestead Market Co.: 3 plt $1,968
- d1 placed parked Inbound Homestead Market Co. day 1 block 24 door 1
- d1 PO IG1478 Blue Ridge Provisions: 2 plt $2,360
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 26 door 1
- d1 released 1 order(s)
- d1 PO IG1479 Anchor Point Foods: 2 plt $952
- d1 placed parked Inbound Anchor Point Foods day 1 block 26 door 2
- d1 PO IG1480 Homestead Market Co.: 1 plt $2,080
- d1 placed parked Inbound Homestead Market Co. day 1 block 28 door 1
- d1 PO IG1481 Bulk Basin Foods: 2 plt $715
- d1 placed parked Inbound Bulk Basin Foods day 1 block 30 door 1
- d1 PO IG1482 Millbrook Family Foods: 3 plt $1,968
- d1 placed parked Inbound Millbrook Family Foods day 1 block 42 door 1
- d1 released 1 order(s)
- d2 PO IG2483 Millbrook Family Foods: 1 plt $476
- d2 placed parked Inbound Millbrook Family Foods day 2 block 6 door 1
- d2 PO IG2484 Bulk Basin Foods: 4 plt $1,732
- d2 placed parked Inbound Bulk Basin Foods day 2 block 36 door 2
- d2 PO IG2485 Millbrook Family Foods: 1 plt $1,672
- d2 placed parked Inbound Millbrook Family Foods day 2 block 37 door 3
- d2 PO IG2486 Backdoor Bill's Overstock: 2 plt $512
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 38 door 3
- d2 PO IG2487 Ambrose Fine Foods: 6 plt $10,056
- d2 placed parked Inbound Ambrose Fine Foods day 2 block 39 door 2
- d2 PO IG2488 Fairweather Trading Co.: 9 plt $7,486
- d2 PO IG2489 Millbrook Family Foods: 1 plt $624
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 41 door 3
- d2 placed parked Inbound Millbrook Family Foods day 2 block 43 door 1
- d2 PO IG2490 Bulk Basin Foods: 3 plt $1,379
- d2 placed parked Inbound Bulk Basin Foods day 2 block 43 door 2
- d3 released 1 order(s)
- d3 PO IG3491 Last Call Liquidators: 1 plt $720
- d3 placed parked Inbound Last Call Liquidators day 3 block 36 door 2
- d3 PO IG3492 Bulk Basin Foods: 6 plt $2,760
- d3 placed parked Inbound Bulk Basin Foods day 3 block 37 door 2
- d3 booked Sneaky Pete's Seafood day 4 block 0 door 1 (ship-short: stock ready hour 70 + 1.5h of work)
- d3 booked Sneaky Pete's Seafood day 4 block 2 door 2 (ship-short: stock ready hour 71 + 1.5h of work)
- d4 released 1 order(s)
- d4 PO IG4493 Millbrook Family Foods: 1 plt $576
- d4 placed parked Inbound Millbrook Family Foods day 4 block 36 door 2
- d4 PO IG4494 Bulk Basin Foods: 1 plt $520
- d4 placed parked Inbound Bulk Basin Foods day 4 block 37 door 2
- d4 PO IG4495 Backdoor Bill's Overstock: 2 plt $512
- d4 placed parked Inbound Backdoor Bill's Overstock day 4 block 38 door 2
- d4 PO IG4496 Salvage Row Traders: 1 plt $920
- d4 placed parked Inbound Salvage Row Traders day 4 block 39 door 3
- d4 PO IG4497 Fairweather Trading Co.: 10 plt $7,808
- d4 PO IG4498 Blue Ridge Provisions: 4 plt $3,270
- d4 placed parked Inbound Fairweather Trading Co. day 4 block 41 door 2
- d4 placed parked Inbound Blue Ridge Provisions day 4 block 41 door 3
- d4 PO IG4499 Backdoor Bill's Overstock: 1 plt $256
- d4 signed bulk BULK_Fizz-Pop Soda Works_D4_0
- d4 placed parked Inbound Backdoor Bill's Overstock day 4 block 43 door 1
- d4 PO IG4500 Golden Grove Gourmet: 10 plt $16,240
- d4 placed parked Inbound Golden Grove Gourmet day 5 block 0 door 1
- d4 booked Sneaky Pete's Seafood day 5 block 0 door 2 (ship-short: stock ready hour 94 + 1.5h of work)
- d4 PO IG4501 Millbrook Family Foods: 7 plt $3,780
- d4 placed parked Inbound Millbrook Family Foods day 5 block 0 door 3
- d4 booked Sneaky Pete's Seafood day 5 block 5 door 3 (ship-short: stock ready hour 95 + 1.5h of work)
- d5 PO IG5502 Coastal Cargo Exchange: 5 plt $6,096
- d5 PO IG5503 Millbrook Family Foods: 4 plt $1,996
- d5 PO IG5504 Bulk Basin Foods: 1 plt $704
- d5 placed parked Inbound Coastal Cargo Exchange day 5 block 2 door 2
- d5 placed parked Inbound Millbrook Family Foods day 5 block 6 door 2
- d5 placed parked Inbound Bulk Basin Foods day 5 block 7 door 1
- d5 placed parked Inbound Millbrook Family Foods day 5 block 6 door 2
- d5 placed parked Inbound Bulk Basin Foods day 5 block 7 door 1
- d5 released 1 order(s)
- d5 PO IG5505 Summit Peak Specialty: 9 plt $14,875
- d5 placed parked Inbound Summit Peak Specialty day 5 block 16 door 1
- d5 PO IG5506 Millbrook Family Foods: 5 plt $2,700
- d5 placed parked Inbound Millbrook Family Foods day 5 block 17 door 2
- d5 PO IG5507 Dockside Closeouts: 4 plt $4,896
- d5 placed parked Inbound Dockside Closeouts day 5 block 21 door 2
- d5 PO IG5508 Millbrook Family Foods: 2 plt $1,080
- d5 placed parked Inbound Millbrook Family Foods day 5 block 24 door 1
- d5 PO IG5509 Golden Grove Gourmet: 8 plt $13,200
- d5 placed parked Inbound Golden Grove Gourmet day 5 block 34 door 2
- d5 booked Sneaky Pete's Seafood day 6 block 0 door 1 (ship-short: stock ready hour 118 + 1.5h of work)
- d5 booked Sneaky Pete's Seafood day 6 block 2 door 2 (ship-short: stock ready hour 119 + 1.5h of work)
- d6 booked Fizz-Pop Soda Works day 6 block 28 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 16 pallet pick(s) / 2 reach truck(s): stock ready hour 120 + 13.9h of work)
- d6 released 1 order(s)
- d6 PO IG6510 Bulk Basin Foods: 4 plt $1,752
- d6 placed parked Inbound Bulk Basin Foods day 6 block 2 door 1
- d6 PO IG6511 Golden Grove Gourmet: 6 plt $9,485
- d6 PO IG6512 Millbrook Family Foods: 1 plt $672
- d6 placed parked Inbound Golden Grove Gourmet day 6 block 3 door 3
- d6 placed parked Inbound Millbrook Family Foods day 6 block 5 door 1
- d6 PO IG6513 Bulk Basin Foods: 5 plt $1,816
- d6 placed parked Inbound Bulk Basin Foods day 6 block 10 door 1
- d6 released 1 order(s)
- d6 PO IG6514 Steady Supply Grocers: 1 plt $1,200
- d6 PO IG6515 Bulk Basin Foods: 2 plt $1,480
- d6 placed parked Inbound Steady Supply Grocers day 6 block 16 door 1
- d6 placed parked Inbound Bulk Basin Foods day 6 block 16 door 2
- d6 PO IG6516 Bulk Basin Foods: 1 plt $112
- d6 placed parked Inbound Bulk Basin Foods day 6 block 28 door 1
- d6 PO IG6517 Anchor Point Foods: 2 plt $896
- d6 placed parked Inbound Anchor Point Foods day 6 block 36 door 3
- d6 PO IG6518 Homestead Market Co.: 1 plt $720
- d6 placed parked Inbound Homestead Market Co. day 6 block 38 door 3
- d6 PO IG6519 Cornerstone Staples: 1 plt $816
- d6 placed parked Inbound Cornerstone Staples day 6 block 39 door 3
- d6 PO IG6520 Iron Gate Staples: 1 plt $112
- d6 placed parked Inbound Iron Gate Staples day 6 block 47 door 1
- d7 booked Sneaky Pete's Seafood day 7 block 1 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 PO IG7521 Fairweather Trading Co.: 13 plt $10,776
- d7 PO IG7522 Homestead Market Co.: 1 plt $768
- d7 placed parked Inbound Fairweather Trading Co. day 7 block 2 door 2
- d7 placed parked Inbound Homestead Market Co. day 7 block 2 door 3
- d7 PO IG7523 Anchor Point Foods: 2 plt $1,064
- d7 placed parked Inbound Anchor Point Foods day 7 block 6 door 3
- d7 PO IG7524 Iron Gate Staples: 1 plt $768
- d7 placed parked Inbound Iron Gate Staples day 7 block 10 door 1
- d7 booked Sneaky Pete's Seafood day 7 block 11 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 7 block 21 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 PO IG7525 Millbrook Family Foods: 1 plt $768
- d7 placed parked Inbound Millbrook Family Foods day 7 block 22 door 2
- d7 released 1 order(s)
- d7 PO IG7526 Millbrook Family Foods: 2 plt $1,064
- d7 placed parked Inbound Millbrook Family Foods day 7 block 24 door 2
- d7 booked Sneaky Pete's Seafood day 7 block 31 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 PO IG7527 Ambrose Fine Foods: 5 plt $9,864
- d7 placed parked Inbound Ambrose Fine Foods day 7 block 36 door 3
- d7 PO IG7528 Riverside Housewares: 1 plt $480
- d7 placed parked Inbound Riverside Housewares day 7 block 39 door 2
- d7 PO IG7529 Cornerstone Staples: 3 plt $1,528
- d7 placed parked Inbound Cornerstone Staples day 7 block 40 door 2
- d7 released 1 order(s)
- d7 booked Sneaky Pete's Seafood day 8 block 0 door 1 (ship-short: stock ready hour 166 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 8 block 2 door 2 (ship-short: stock ready hour 167 + 1.5h of work)
