# Playtest block 18 — days 1–8

## Score: **46.7 (+17.3)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (4 shipped, 17 late) | 15% | 0.0 (0.0) |
| Fill rate | 10% | 100.0 (+36.7) |
| Throughput (69 plt vs target 84) | 15% | 82.1 (+47.6) |
| Revenue ($62,270) | 15% | 100.0 (+62.9) |
| Profit (cash + stock -163,374) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (+15.1) |
| Reliability (bugs) | 25% | 7.6 (-14.7) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **4**, late/fined **17**, pallets shipped **69**. Cash $228,497 → $27,737 (-200,760). Stock at cost $0 → $37,386; cash + stock -163,374.
Bot did: 4 contracts signed, 53 dock bookings, 1 orders released, 25 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **9**
  - VEHICLE-STATIONARY|Dockstocker has a driver, hasn't moved in #s while # task(s) it could take wait
  - NO-TASK| lane pallet #D# sku=# load=#
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - ORPHAN-OUTBOUND| #B# (order ##b#-b#d## gone)
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Order #-df##e-b#aa-# (Sneaky Pete's Seafood) can't reach staging — no lane at Stage # or any nearby door has room for # pallet(s) (every lane full, held by another customer, or an inbound trailer is docked). Waiting with the pallet(s), retrying in #s.
  - VEHICLE-STATIONARY|PalletJack has a driver, hasn't moved in #s while # task(s) it could take wait
  - WARN|[OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jack Torres' as its operator though nobody is on it or running it - clearing the stale link.
- New this block: **19**
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it.
  - FLOATING| Pallet_#_Slot#_Tier# @#A# bottom=#
  - ERROR|[MHEBoundary] 'Dockstocker' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_##c##b#-b#e-#_# @#A#
  - FLOATING| OutboundPallet_##c##b#-b#e-#_# @#A# bottom=#
  - FLOATING| OutboundPallet_#-b##d###e#bc#b_# @#B# bottom=#
  - ERROR|[MHEBoundary] 'ReachTruck' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - FLOATING| OutboundPallet_##d#-b###_# @#C# bottom=#
  - ERROR|[MHEBoundary] 'PalletJack' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|[MHEBoundary] 'Dockstocker' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside.
  - EXCEPTION|MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  - WARN|[AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - OVERLAP| OutboundPallet_###-b###_# & OutboundPallet_###-b###_# @#E#
  - REPEATING|x# and counting: [AiNavigation] Employee_Tyler Hayes: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [AiNavigation] Employee_Jack Peterson: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.

## Bugs found (24 distinct, 77 incidents)

| Incidents | Bug |
|---|---|
| 10 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 11 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 7 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it. |
| 6 | ERROR — [MHEBoundary] 'Dockstocker' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 5 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##c##b#-b#e-#_# @#A# |
| 5 | FLOATING —  OutboundPallet_##c##b#-b#e-#_# @#A# bottom=# |
| 5 | FLOATING —  OutboundPallet_#-b##d###e#bc#b_# @#B# bottom=# |
| 4 | ERROR — [MHEBoundary] 'ReachTruck' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 4 | FLOATING —  OutboundPallet_##d#-b###_# @#C# bottom=# |
| 2 | ERROR — [MHEBoundary] 'PalletJack' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 2 | ERROR — [MHEBoundary] 'Dockstocker' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside. |
| 1 | EXCEPTION — MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it. |
| 3 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | OVERLAP —  OutboundPallet_###-b###_# & OutboundPallet_###-b###_# @#E# |
| 1 | NO-TASK —  lane pallet #B# sku=# load=# |
| 1 | NO-TASK —  lane pallet #C# sku=# load=# |
| 1 | REPEATING — x# and counting: [NoWaypointIndicator] Alert emote 'emote_exclamations' not found in Resources/Emotes. |
| 1 | NO-TASK —  lane pallet #A# sku=# load=# |
| 1 | REPEATING — x# and counting: [AiNavigation] Employee_Tyler Hayes: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] Employee_Jack Peterson: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 2 | WARN — [ReachTruckOperator] 'ReachTruck' waited #s for lane #B and it never freed up — giving up and re-queueing. |
| 1 | REPEATING — x# and counting: Parameter 'IsWaving' does not exist. |
| 1 | REPEATING — x# and counting: [JackStall] 'bay ##' at (#, #, #) (target (#, #, #)) remaining=# pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=True onMesh=True jackBlocked=True elapsed=#s reissues=# |
| 1 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **NO-TASK** (day 1): NO-TASK lane pallet 3B-5 sku=172495 load=3615898260  <<HISTORY: d1 18:43 Putaway task created (STG3B-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 18:43 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d1 18:43 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d1 18:43 PalletPick task Open->Available by OrderService.TryAssignInboundPalletToPendingPick | d1 18:43 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d1 18:51 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **NO-TASK** (day 2): NO-TASK lane pallet 2C-1 sku=102435 load=5062486690  <<HISTORY: d2 03:07 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 03:17 Putaway task created (STG2C-1->) by ReceiverReceivingWorkflow.CreatePutawayTask | d2 03:17 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 03:17 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 03:17 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d2 05:52 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **ERROR** (day 2): [MHEBoundary] 'Dockstocker' drove OUT of the building at (9.71, 0.00, 43.02) (cell (57, 82)). Putting it back on its last inside spot (9.02, 1.11, 43.36). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  
  ```
- **WARN** (day 2): [AiNavigation] ReachTruck: stuck at (-24.00, 1.11, 31.29) — walking out to (-12.75, 0.08, 24.48) (12.19m to mesh).
- **ERROR** (day 2): [MHEBoundary] 'ReachTruck' drove OUT of the building at (-24.56, 0.00, 45.48) (cell (31, 84)). Putting it back on its last inside spot (-24.40, 1.11, 44.76). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  
  ```
- **REPEATING** (day 2): x25 and counting: [NoWaypointIndicator] Alert emote 'emote_exclamations' not found in Resources/Emotes.
- **WARN** (day 2): [AiNavigation] Dockstocker: stuck at (-17.29, 1.11, 39.20) — walking out to (-30.28, 1.15, 37.81) (12.07m to mesh).
- **ERROR** (day 2): [MHEBoundary] 'PalletJack' drove OUT of the building at (-43.79, 1.11, 29.39) (cell (16, 72)). Putting it back on its last inside spot (-43.35, 1.11, 29.42). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:89)
  
  ```
- **NO-TASK** (day 3): NO-TASK lane pallet 3A-1 sku=141256 load=4630855427  <<HISTORY: d2 21:30 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d2 21:39 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 21:40 Putaway task created (STG3A-1->) by OrderService.RefileStrandedLanePallets | d2 21:55 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 21:55 PalletPick task created (STG3A-1->1A) by OrderService.RefilePalletPickShortfalls | d3 00:53 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **FLOATING** (day 3): FLOATING Pallet_00_Slot0_Tier0 @3A-1 bottom=2.33  <<HISTORY: d3 02:54 registered at (51, 74) 27x194831 by InventoryService.RegisterPhysicalPallet | d3 02:54 Receive task created (->STG3A-1) by ReceivingService.CreateReceiveTaskForPallet>>
- **OVERLAP** (day 3): OVERLAP OutboundPallet_c832098b-4885-4232-b412-2a594a326e7f_2 & OutboundPallet_c832098b-4885-4232-b412-2a594a326e7f_1 @2E-1
- **REPEATING** (day 3): x25 and counting: [AiNavigation] Employee_Tyler Hayes: stranded on an isolated NavMesh island at (-15.16, 1.13, 42.54) (no waypoint reachable for 8s) — warping to (-12.64, 0.01, 43.01) to rejoin patrol.
- **DOOR-DEADLOCK** (day 4): OG2629 is Staged but door 1's truck has waited 90 min with no one loading it.
- **ERROR** (day 4): [MHEBoundary] 'Dockstocker' keeps driving out of the building (6 times in 10s, at (-26.58, 1.11, 40.03)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:85)
  
  ```
- **DOOR-DEADLOCK** (day 4): OG2629 is Loading but door 1's truck has waited 105 min with no one loading it.
- **OVERLAP** (day 4): OVERLAP Pallet_11_Slot10_Tier0 & OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_2 @1A-5  <<HISTORY: d3 02:15 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 02:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:08 moved (47, 78)->(-1, -1) by InventoryService.MovePallet | d3 03:16 adopted as outbound freight of order 7a4975db-70c2-4b36-b15e-b4272be103ea (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:16 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:16 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 4): FLOATING OutboundPallet_7a4975db-70c2-4b36-b15e-b4272be103ea_1 @1A-4 bottom=2.15
- **REPEATING** (day 4): x25 and counting: [AiNavigation] Employee_Jack Peterson: stranded on an isolated NavMesh island at (-22.63, 1.16, 42.01) (no waypoint reachable for 8s) — warping to (-19.26, 0.01, 41.69) to rejoin patrol.
- **WARN** (day 4): [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2B and it never freed up — giving up and re-queueing.
- **FLOATING** (day 5): FLOATING OutboundPallet_cceaa103-b536-4d11-8549-185666e0bc5b_2 @1B-2 bottom=2.35
- **FLOATING** (day 5): FLOATING OutboundPallet_6f408970-902d-4452-b598-33db5d4b7d68_1 @2C-2 bottom=2.19
- **REPEATING** (day 5): x25 and counting: Parameter 'IsWaving' does not exist.
- **REPEATING** (day 6): x25 and counting: [JackStall] 'bay 02-01' at (-39.02, 1.14, 35.49) (target (-36.41, 1.11, 35.84)) remaining=2.63 pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(0.00, 0.00, 0.00) stopped=True onMesh=True jackBlocked=True elapsed=2.6s reissues=0
- **EXCEPTION** (day 7): MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_position () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  GameCore.Actors.ReachTruckOperator+<DeliverPalletToRack>d__94.MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:1275)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  ```

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1948 Coastal Cargo Exchange: 4 plt $6,604
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 18 door 1
- d1 PO IG1949 Backdoor Bill's Overstock: 4 plt $2,432
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 19 door 2
- d1 PO IG1950 Anchor Point Foods: 6 plt $3,961
- d1 placed parked Inbound Anchor Point Foods day 1 block 20 door 1
- d1 signed bulk BULK_Pony Express Oats_D1_0
- d1 signed bulk BULK_Grizzly Ridge Jerky_D1_1
- d1 PO IG1951 Bulk Basin Foods: 3 plt $1,744
- d1 placed parked Inbound Bulk Basin Foods day 1 block 22 door 2
- d1 PO IG1952 Homestead Market Co.: 10 plt $5,670
- d1 placed parked Inbound Homestead Market Co. day 1 block 24 door 1
- d1 PO IG1953 Homestead Market Co.: 1 plt $544
- d1 placed parked Inbound Homestead Market Co. day 1 block 25 door 2
- d1 PO IG1954 Backdoor Bill's Overstock: 9 plt $2,304
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 26 door 2
- d1 booked Pony Express Oats day 2 block 0 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 19 + 3.3h of work)
- d1 PO IG1955 Homestead Market Co.: 10 plt $6,720
- d1 placed parked Inbound Homestead Market Co. day 1 block 28 door 3
- d1 PO IG1956 Coastal Cargo Exchange: 10 plt $13,520
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 30 door 1
- d1 PO IG1957 Backdoor Bill's Overstock: 1 plt $256
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 31 door 2
- d1 released 1 order(s)
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 44 door 1
- d1 placed parked Inbound Homestead Market Co. day 2 block 0 door 2
- d1 booked Grizzly Ridge Jerky day 2 block 20 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 30 + 3.3h of work)
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 33 door 2
- d2 PO IG2961 Golden Grove Gourmet: 11 plt $17,955
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 38 door 2
- d2 placed parked Inbound Anchor Point Foods day 2 block 40 door 3
- d2 placed parked Inbound Bulk Basin Foods day 2 block 41 door 1
- d2 PO IG2965 Backdoor Bill's Overstock: 1 plt $1,600
- d2 placed parked Inbound Homestead Market Co. day 2 block 43 door 1
- d2 placed parked Inbound Homestead Market Co. day 2 block 43 door 3
- d2 placed parked Inbound Anchor Point Foods day 2 block 44 door 1
- d2 placed parked Inbound Bulk Basin Foods day 2 block 45 door 1
- d3 placed parked Inbound Bulk Basin Foods day 3 block 9 door 1
- d3 placed parked Inbound Bulk Basin Foods day 3 block 23 door 1
- d3 PO IG3972 Ambrose Fine Foods: 5 plt $10,320
- d3 placed parked Inbound Fairweather Trading Co. day 3 block 24 door 3
- d3 booked Barnacle Bill's Bait & Bite day 3 block 40 door 2 (pallet-only: stock ready hour 66 + 1.5h of work)
- d3 placed parked Inbound Bulk Basin Foods day 3 block 27 door 1
- d3 placed parked Inbound Anchor Point Foods day 3 block 35 door 2
- d3 PO IG3976 Homestead Market Co.: 1 plt $1,080
- d3 placed parked Inbound Bulk Basin Foods day 3 block 36 door 3
- d3 placed parked Inbound Anchor Point Foods day 3 block 39 door 2
- d3 placed parked Inbound Bulk Basin Foods day 3 block 40 door 3
- d4 PO IG4980 Bulk Basin Foods: 1 plt $160
- d4 placed parked Inbound Bulk Basin Foods day 4 block 33 door 2
- d4 booked Mama Tomato Sauces day 5 block 4 door 1 (case-pick, 1 picklist(s) / 3 selector(s): stock ready hour 94 + 3.3h of work)
- d4 PO IG4982 Homestead Market Co.: 8 plt $5,368
- d4 placed parked Inbound Homestead Market Co. day 4 block 37 door 2
- d4 placed parked Inbound Bulk Basin Foods day 4 block 38 door 2
- d4 placed parked Inbound Homestead Market Co. day 4 block 38 door 3
- d4 placed parked Inbound Bulk Basin Foods day 4 block 40 door 2
- d4 placed parked Inbound Bulk Basin Foods day 4 block 41 door 1
- d4 placed parked Inbound Homestead Market Co. day 4 block 44 door 1
- d5 placed parked Inbound Coastal Cargo Exchange day 5 block 23 door 1
- d5 PO IG5990 Fairweather Trading Co.: 10 plt $5,344
- d6 placed parked Inbound Bulk Basin Foods day 6 block 1 door 2
- d6 booked Sneaky Pete's Seafood day 6 block 1 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 PO IG6992 Homestead Market Co.: 1 plt $448
- d6 placed parked Inbound Anchor Point Foods day 6 block 3 door 2
- d6 placed parked Inbound Blue Ridge Provisions day 6 block 5 door 2
- d6 placed parked Inbound Fairweather Trading Co. day 6 block 7 door 2
- d6 PO IG6996 Bulk Basin Foods: 1 plt $400
- d6 placed parked Inbound Fairweather Trading Co. day 6 block 10 door 1
- d6 placed parked Inbound Bulk Basin Foods day 6 block 25 door 1
- d6 placed parked Inbound Fairweather Trading Co. day 6 block 27 door 1
- d6 signed bulk BULK_Sneaky Pete's Seafood_D6_2
- d6 PO IG6999 Bulk Basin Foods: 4 plt $2,520
- d6 PO IG6001 Anchor Point Foods: 11 plt $5,236
- d6 PO IG6002 Fairweather Trading Co.: 15 plt $7,226
- d7 placed parked Inbound Golden Grove Gourmet day 7 block 3 door 1
- d7 placed parked Inbound Homestead Market Co. day 7 block 3 door 3
- d7 placed parked Inbound Bulk Basin Foods day 7 block 4 door 3
- d7 booked Sneaky Pete's Seafood day 7 block 20 door 1 (case-pick, 1 picklist(s) / 3 selector(s): stock ready hour 150 + 3.3h of work)
- d7 booked Odd-Shape Orchards day 7 block 5 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 PO IG7006 Bulk Basin Foods: 4 plt $2,800
- d7 PO IG7007 Anchor Point Foods: 1 plt $504
- d7 PO IG7008 Homestead Market Co.: 3 plt $2,652
