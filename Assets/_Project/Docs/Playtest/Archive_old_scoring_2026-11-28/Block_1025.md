# Playtest block 1025 — days 1–8

## Score: **59.5** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (11 shipped, 4 late) | 15% | 63.6 |
| Fill rate | 10% | 94.4 |
| Throughput (79 plt vs target 84) | 15% | 94.0 |
| Revenue ($88,194) | 15% | 100.0 |
| Profit (cash -350,401) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 5.5 |
| Reputation (0 to 111) | 5% | 100.0 |

Orders shipped **11**, late/fined **4**, pallets shipped **79**. Cash $500,000 → $149,599 (-350,401). Stock at cost $0 → $43,463 (not part of profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Late orders - state when it came due
- 2 x still loading
- 1 x still being picked

- day 5: OG5381 (Neon Nectar Juice Co.) was still loading when it came due. Fined $4,758. Lines 10, 335 cs, released=True.
- day 5: OG5383 (Sneaky Pete's Seafood) was still being picked (11/18 cs). Fined $73. Lines 18, 18 cs, released=True.
- day 6: OG6385 (Caffeine Fiend Coffee) was still loading when it came due. Fined $1,518. Lines 1, 276 cs, released=True.

## Bugs found (47 distinct, 113 incidents)

| Incidents | Bug |
|---|---|
| 11 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 11 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 9 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 9 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 4 | OVERLAP —  OutboundPallet_##a#d#bf#-a#-#_# & OutboundPallet_##a#d#bf#-a#-#_# @#B# |
| 4 | FLOATING —  OutboundPallet_##a#d#bf#-a#-#_# @#B# bottom=# |
| 2 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 2 | FLOATING —  A Chep @#A# bottom=# |
| 2 | FLOATING —  OutboundPallet_###e#-a#ff-#_# @#A# bottom=# |
| 2 | OVERLAP —  OutboundPallet_###e#-a#ff-#_# & OutboundPallet_###e#-a#ff-#_# @#A# |
| 2 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##af##f#f-bf#c-#_# @#B# |
| 2 | FLOATING —  A Chep @#C# bottom=# |
| 2 | OVERLAP —  A Chep & Pallet_#_Slot#_Tier# @#C# |
| 2 | FLOATING —  Pallet_#_Slot#_Tier# @#D# bottom=# |
| 2 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#D# |
| 2 | FLOATING —  OutboundPallet_##af##f#f-bf#c-#_# @#B# bottom=# |
| 2 | FLOATING —  OutboundPallet_#-df#a#-bb###_# @#A# bottom=# |
| 2 | OVERLAP —  OutboundPallet_#-df#a#-bb###_# & OutboundPallet_#-df#a#-bb###_# @#A# |
| 4 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 1 | OVERLAP —  A Chep & A Chep @(#, #) |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane #A exit: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < at UnityEngine.MonoBehaviour.StartCoroutineManaged#_Injected (System.IntPtr , System.Collections.IEnumerator ) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | OVERLAP —  OutboundPallet_##a#d#bf#-a#-#_# & OutboundPallet_##a#d#bf#-a#-#_# @#C# |
| 1 | FLOATING —  OutboundPallet_##a#d#bf#-a#-#_# @#C# bottom=# |
| 1 | OVERLAP —  A Chep & Pallet_#_Slot#_Tier# @(#, #) |
| 1 | OVERLAP —  A Chep & Pallet_#_Slot#_Tier# @#A# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & A Chep @#A# |
| 1 | OVERLAP —  OutboundPallet_##af##f#f-bf#c-#_# & OutboundPallet_##af##f#f-bf#c-#_# @#B# |
| 3 | WARN — [OrderSelectionTaskDriver] Order #-df#a#-bb### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 3 | WARN — [OrderSelectionTaskDriver] Order ###e-b#d#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##f#a##d#a## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##e##a##ee##_# @#A# |
| 1 | FLOATING —  OutboundPallet_##e##a##ee##_# @#A# bottom=# |
| 1 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 2 | WARN — [AiNavigation] Employee_Emma Clark: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: Attempting to render to a depth only surface with no dummy color attachment |
| 2 | WARN — [OrderSelectionTaskDriver] Order ##a#d#bf#-a#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 2 | WARN — [OrderSelectionTaskDriver] Order ###e#-a#ff-# (Neon Nectar Juice Co.) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##e##fa-b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Carol Morris' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | WARN — [AiNavigation] Employee_Hannah Phillips: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###e-b#d#-# (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###e-b#d#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##f#a##d#a## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |

## First sightings (with stack traces)

- **OVERLAP** (day 2): OVERLAP A Chep & A Chep @(27, 82)  <<HISTORY: d2 21:25 restored from save at (27, 82) 27x194831 by InventoryService.RegisterPalletDirect | d2 22:49 Replenish task created (01-10-A0->01-10-00) by ReplenishmentService.CreateReplenishTask | d2 22:50 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 23:00 moved (27, 82)->(-1, -1) by InventoryService.MovePallet | d2 23:05 moved (-1, -1)->(27, 82) by InventoryService.MovePallet | d2 23:05 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d2 22:45 picked 1 (left 4) by InventoryService.PickFromPallet | d2 22:45 picked 1 (left 3) by InventoryService.PickFromPallet | d2 22:46 picked 1 (left 2) by InventoryService.PickFromPallet | d2 22:47 picked 1 (left 1) by InventoryService.PickFromPallet | d2 22:47 picked 1 (left 0) by InventoryService.PickFromPallet | d2 22:47 record destroyed by InventoryService.DestroyPallet>>
- **WARN** (day 3): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Carol Morris' as its operator though nobody is on it or running it - clearing the stale link.
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 246fa0f0-6a6d-4bf9-a189-cdbcbbb65ed9 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 338/370.
- **ERROR** (day 3): [ReachTruckOperator] 'RT_Full(Clone)' putaway: → lane 2A exit: NO NAVMESH PATH. anchor=(-4.69, 1.11, 48.99) → driveTarget=(-4.69, 1.12, 48.99), from=(-14.86, 1.11, 50.38), elapsed=30.1s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=10.0, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator+<SeekViaNavMesh>d__128.MoveNext () [0x00357] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:2651 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < at UnityEngine.MonoBehaviour.StartCoroutineManaged2_Injected (System.IntPtr , System.Collections.IEnumerator ) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 30.1s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__128:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2767)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **OVERLAP** (day 3): OVERLAP OutboundPallet_246fa0f0-6a6d-4bf9-a189-cdbcbbb65ed9_1 & OutboundPallet_246fa0f0-6a6d-4bf9-a189-cdbcbbb65ed9_2 @1B-4
- **OVERLAP** (day 3): OVERLAP OutboundPallet_246fa0f0-6a6d-4bf9-a189-cdbcbbb65ed9_1 & OutboundPallet_246fa0f0-6a6d-4bf9-a189-cdbcbbb65ed9_2 @1C-1
- **FLOATING** (day 3): FLOATING OutboundPallet_246fa0f0-6a6d-4bf9-a189-cdbcbbb65ed9_2 @1B-2 bottom=2.24
- **FLOATING** (day 3): FLOATING OutboundPallet_246fa0f0-6a6d-4bf9-a189-cdbcbbb65ed9_2 @1C-1 bottom=2.27
- **WARN** (day 4): [AiNavigation] DS_FULL(Clone): stuck at (-17.72, 1.11, 51.00) — walking out to (-6.47, 1.25, 44.50) (12.00m to mesh).
- **WARN** (day 4): [AiNavigation] Employee_Hannah Phillips: stuck at (-18.52, 1.13, 50.51) — walking out to (-7.26, 1.16, 44.01) (12.00m to mesh).
- **WARN** (day 4): [OrderSelectionTaskDriver] Order 48423227-7930-4e84-a6ff-dc8148aede2c (Neon Nectar Juice Co.) has no reachable pick location for its remaining line item(s) — staging short at 54/335 case(s).
- **OVERLAP** (day 4): OVERLAP A Chep & Pallet_03_Slot1_Tier1 @(28, 82)  <<HISTORY: d4 08:33 picked 1 (left 4) by InventoryService.PickFromPallet | d4 08:34 picked 1 (left 3) by InventoryService.PickFromPallet | d4 08:34 picked 1 (left 2) by InventoryService.PickFromPallet | d4 08:35 picked 1 (left 1) by InventoryService.PickFromPallet | d4 08:36 picked 1 (left 0) by InventoryService.PickFromPallet | d4 08:36 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d3 17:43 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 08:36 Replenish task created (01-08-A1->01-08-01) by ReplenishmentService.CreateReplenishTask | d4 12:46 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 13:03 moved (29, 82)->(-1, -1) by InventoryService.MovePallet | d4 13:11 moved (-1, -1)->(29, 82) by InventoryService.MovePallet | d4 13:11 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **WARN** (day 5): [AiNavigation] DS_FULL(Clone) cannot reach task position at (12.54, 1.12, 46.99) (path invalid) — abandoning the walk and handing the task back.
- **FLOATING** (day 5): FLOATING A Chep @3A-3 bottom=2.47  <<HISTORY: d4 06:52 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 11:43 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 12:00 moved (21, 87)->(-1, -1) by InventoryService.MovePallet | d4 12:19 adopted as outbound freight of order 48423227-7930-4e84-a6ff-dc8148aede2c (60 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 12:19 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 12:19 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 5): OVERLAP A Chep & Pallet_03_Slot3_Tier0 @3A-3  <<HISTORY: d4 06:52 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 11:43 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 12:00 moved (21, 87)->(-1, -1) by InventoryService.MovePallet | d4 12:19 adopted as outbound freight of order 48423227-7930-4e84-a6ff-dc8148aede2c (60 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 12:19 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 12:19 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d4 06:52 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 11:22 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 11:39 moved (25, 82)->(-1, -1) by InventoryService.MovePallet | d4 11:58 adopted as outbound freight of order 48423227-7930-4e84-a6ff-dc8148aede2c (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 11:58 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 11:58 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 5): OVERLAP Pallet_04_Slot4_Tier0 & A Chep @3A-4  <<HISTORY: d4 06:52 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 11:59 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 12:15 moved (21, 87)->(-1, -1) by InventoryService.MovePallet | d4 12:31 adopted as outbound freight of order 48423227-7930-4e84-a6ff-dc8148aede2c (91 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 12:31 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 12:31 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d4 06:52 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 12:20 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 12:33 moved (33, 82)->(-1, -1) by InventoryService.MovePallet | d4 12:45 adopted as outbound freight of order 48423227-7930-4e84-a6ff-dc8148aede2c (11 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 12:45 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 12:45 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 5): FLOATING OutboundPallet_48423227-7930-4e84-a6ff-dc8148aede2c_1 @3A-5 bottom=2.21
- **OVERLAP** (day 5): OVERLAP OutboundPallet_48423227-7930-4e84-a6ff-dc8148aede2c_1 & OutboundPallet_48423227-7930-4e84-a6ff-dc8148aede2c_1 @3A-5
- **OVERLAP** (day 5): OVERLAP Pallet_07_Slot5_Tier0 & OutboundPallet_da2e0246-2af8-4f6f-bf1c-c4af89beaa33_1 @1B-3  <<HISTORY: d4 06:43 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 07:30 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 07:43 moved (21, 78)->(-1, -1) by InventoryService.MovePallet | d4 07:59 adopted as outbound freight of order da2e0246-2af8-4f6f-bf1c-c4af89beaa33 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 07:59 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 07:59 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 5): FLOATING A Chep @1C-5 bottom=2.19  <<HISTORY: d4 06:43 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 09:57 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 10:10 moved (24, 82)->(-1, -1) by InventoryService.MovePallet | d4 10:22 adopted as outbound freight of order da2e0246-2af8-4f6f-bf1c-c4af89beaa33 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 10:22 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 10:22 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 5): OVERLAP A Chep & Pallet_00_Slot0_Tier0 @1C-5  <<HISTORY: d4 06:43 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 09:57 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 10:10 moved (24, 82)->(-1, -1) by InventoryService.MovePallet | d4 10:22 adopted as outbound freight of order da2e0246-2af8-4f6f-bf1c-c4af89beaa33 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 10:22 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 10:22 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d4 06:43 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 09:36 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 09:45 moved (29, 82)->(-1, -1) by InventoryService.MovePallet | d4 09:58 adopted as outbound freight of order da2e0246-2af8-4f6f-bf1c-c4af89beaa33 (60 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 09:58 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 09:58 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 5): OVERLAP Pallet_05_Slot4_Tier0 & Pallet_08_Slot6_Tier0 @1C-1  <<HISTORY: d4 06:43 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 08:00 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 08:10 moved (25, 87)->(-1, -1) by InventoryService.MovePallet | d4 08:22 adopted as outbound freight of order da2e0246-2af8-4f6f-bf1c-c4af89beaa33 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 08:22 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 08:22 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d4 06:43 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 08:18 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 08:28 moved (23, 83)->(-1, -1) by InventoryService.MovePallet | d4 08:41 adopted as outbound freight of order da2e0246-2af8-4f6f-bf1c-c4af89beaa33 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 08:41 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 08:41 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 5): FLOATING Pallet_02_Slot2_Tier0 @1D-1 bottom=2.15  <<HISTORY: d4 06:43 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 10:24 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 10:36 moved (21, 82)->(-1, -1) by InventoryService.MovePallet | d4 10:51 adopted as outbound freight of order da2e0246-2af8-4f6f-bf1c-c4af89beaa33 (36 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 10:51 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 10:51 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 5): OVERLAP Pallet_02_Slot2_Tier0 & Pallet_06_Slot5_Tier0 @1D-1  <<HISTORY: d4 06:43 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 10:24 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 10:36 moved (21, 82)->(-1, -1) by InventoryService.MovePallet | d4 10:51 adopted as outbound freight of order da2e0246-2af8-4f6f-bf1c-c4af89beaa33 (36 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 10:51 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 10:51 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d4 06:43 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 09:59 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 10:11 moved (23, 82)->(-1, -1) by InventoryService.MovePallet | d4 10:26 adopted as outbound freight of order da2e0246-2af8-4f6f-bf1c-c4af89beaa33 (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 10:26 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 10:26 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 5): FLOATING Pallet_06_Slot4_Tier1 @1C-2 bottom=2.79  <<HISTORY: d4 06:43 PalletPick task Open->Available by OrderService.AllocateHeldWork | d4 08:42 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 08:50 moved (29, 83)->(-1, -1) by InventoryService.MovePallet | d4 09:01 adopted as outbound freight of order da2e0246-2af8-4f6f-bf1c-c4af89beaa33 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 09:01 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 09:01 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 5): FLOATING OutboundPallet_da2e0246-2af8-4f6f-bf1c-c4af89beaa33_1 @1B-5 bottom=2.09
- **OVERLAP** (day 5): OVERLAP OutboundPallet_da2e0246-2af8-4f6f-bf1c-c4af89beaa33_1 & OutboundPallet_da2e0246-2af8-4f6f-bf1c-c4af89beaa33_1 @1B-5
- **WARN** (day 5): [OrderService] Order OG5382 closed with a STAGED pallet (60 cs) that never made the trailer - not billed, returning it to stock.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order e9c39b61-df4a-4379-bb70-5844e0325efd (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 14/18.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 99515f76-6520-429e-b7d0-b60adc786f5a (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 16/20.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 5cf21503-792f-4a93-9d3a-89a7c60c8695 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 21/27.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 99515f76-6520-429e-b7d0-b60adc786f5a (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 2.
- **WARN** (day 6): [AiNavigation] RT_Full(Clone) cannot reach task position at (-35.16, 1.12, 40.71) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 99515f76-6520-429e-b7d0-b60adc786f5a (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 19/20 case(s).
- **OVERLAP** (day 6): OVERLAP Pallet_07_Slot3_Tier1 & OutboundPallet_8d8a5b52-49e8-4a43-82ee-6c6f9ee91cb6_1 @3A-5  <<HISTORY: d6 05:59 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d6 06:03 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d6 06:11 moved (46, 82)->(-1, -1) by InventoryService.MovePallet | d6 06:19 adopted as outbound freight of order 8d8a5b52-49e8-4a43-82ee-6c6f9ee91cb6 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d6 06:19 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d6 06:19 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 6): FLOATING OutboundPallet_8d8a5b52-49e8-4a43-82ee-6c6f9ee91cb6_1 @3A-5 bottom=2.26
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 5cf21503-792f-4a93-9d3a-89a7c60c8695 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 25/27 case(s).
- **FLOATING** (day 7): FLOATING OutboundPallet_e9c39b61-df4a-4379-bb70-5844e0325efd_1 @1A-1 bottom=1.66
- **OVERLAP** (day 7): OVERLAP OutboundPallet_e9c39b61-df4a-4379-bb70-5844e0325efd_1 & OutboundPallet_e9c39b61-df4a-4379-bb70-5844e0325efd_1 @1A-1

## Bot actions (first 400)
