# Playtest block 1029 — days 1–8

## Score: **60.1** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (18 shipped, 7 late) | 15% | 61.1 |
| Fill rate | 10% | 94.5 |
| Throughput (147 plt vs target 84) | 15% | 100.0 |
| Revenue ($232,043) | 15% | 100.0 |
| Profit (cash -307,932) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 6.1 |
| Reputation (0 to 145) | 5% | 100.0 |

Orders shipped **18**, late/fined **7**, pallets shipped **147**. Cash $500,000 → $192,068 (-307,932). Stock at cost $0 → $56,782 (not part of profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Late orders - state when it came due
- 5 x loaded but the truck had not left
- 2 x still loading

- day 4: OG5859 (Chubby Chipmunk Cookies) was loaded but the truck had not left. Fined $3,111. Lines 3, 470 cs, released=True.
- day 4: OG6858 (Happy Cow Creamery) was loaded but the truck had not left. Fined $2,626. Lines 2, 621 cs, released=True.
- day 4: OG4856 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $581. Lines 13, 104 cs, released=True.
- day 5: OG6863 (Cluck & Cover Poultry) was still loading when it came due. Fined $5,141. Lines 2, 484 cs, released=True.
- day 5: OG6862 (Happy Cow Creamery) was loaded but the truck had not left. Fined $3,589. Lines 1, 495 cs, released=True.
- day 6: OG6866 (Barnacle Bill's Bait & Bite) was still loading when it came due. Fined $559. Lines 21, 140 cs, released=True.
- day 7: OG8871 (Odd-Shape Orchards) was loaded but the truck had not left. Fined $3,035. Lines 2, 380 cs, released=True.

## Bugs found (48 distinct, 117 incidents)

| Incidents | Bug |
|---|---|
| 19 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 18 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 4 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 4 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 3 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 3 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 3 | OVERLAP —  OutboundPallet_###f####_# & OutboundPallet_###f####_# @#B# |
| 3 | FLOATING —  OutboundPallet_###f####_# @#B# bottom=# |
| 3 | OVERLAP —  OutboundPallet_#-b#ae#ad#-b#a##_# & OutboundPallet_#-b#ae#ad#-b#a##_# @#A# |
| 3 | FLOATING —  OutboundPallet_#-b#ae#ad#-b#a##_# @#A# bottom=# |
| 2 | FLOATING —  OutboundPallet_#-fef##-a#c##_# @#A# bottom=# |
| 2 | OVERLAP —  OutboundPallet_#-fef##-a#c##_# & OutboundPallet_#-fef##-a#c##_# @#A# |
| 5 | WARN — [OrderSelectionTaskDriver] Order ###fb#-a#fc## (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | EXCEPTION — MissingReferenceException: The object of type 'UnityEngine.AI.NavMeshAgent' has been destroyed but you are still trying to access it. |
| 1 | NO-RECORD —  lane pallet #C# A Chep |
| 1 | GHOST-SLOT — SLOT ### names # - no object |
| 1 | GHOST-SLOT — SLOT ##-A# names # - no object |
| 3 | WARN — [AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 3 | WARN — [AiNavigation] Employee_Andrea Scott: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack did not reach 'bay ##' ((#, #, #)) after #s. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#D# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#D# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-ce##c-aa#c##_# @#A# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-c#ad#fb-bab#-#_# @#B# |
| 1 | FLOATING —  OutboundPallet_#-c#ad#fb-bab#-#_# @#B# bottom=# |
| 1 | OVERLAP —  OutboundPallet_#-c#ad#fb-bab#-#_# & OutboundPallet_#-c#ad#fb-bab#-#_# @#A# |
| 1 | FLOATING —  OutboundPallet_#-c#ad#fb-bab#-#_# @#A# bottom=# |
| 2 | WARN — [OrderSelectionTaskDriver] Order ###f#### (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 2 | WARN — [OrderSelectionTaskDriver] Order ###f#### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Order OG# pallet # of #: every pick face is empty - handing the task back until replenishment stocks them. |
| 1 | ORPHAN-OUTBOUND —  #A# (order #-d#d###f### gone) |
| 2 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 2 | WARN — [OrderSelectionTaskDriver] Order #-c#ad#fb-bab#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 2 | WARN — [OrderSelectionTaskDriver] Order ###fb#-a#fc## (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 2 | WARN — [OrderSelectionTaskDriver] Order #-b#ae#ad#-b#a## (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##d##ee##e## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-ba#c#### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Susan Mccarty: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###dea-be#f## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-fef##-a#c## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-ce##c-aa#c## (Cluck & Cover Poultry) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###a#b-b#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-b#ae#ad#-b#a## (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |

## First sightings (with stack traces)

- **OVERLAP** (day 1): OVERLAP Pallet_02_Slot2_Tier0 & Pallet_04_Slot3_Tier1 @3A-2  <<HISTORY: d1 10:58 registered at (53, 82) 40x148972 by InventoryService.RegisterPhysicalPallet | d1 10:58 Receive task created (->STG3A-2) by ReceivingService.CreateReceiveTaskForPallet | d1 11:02 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:14 Putaway task created (STG3A-2->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 11:14 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 11:14 registered at (53, 82) 48x209875 by InventoryService.RegisterPhysicalPallet | d1 11:14 Receive task created (->STG3A-2) by ReceivingService.CreateReceiveTaskForPallet | d1 11:15 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:25 Putaway task created (STG3A-2->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 11:25 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **FLOATING** (day 1): FLOATING Pallet_01_Slot1_Tier0 @3A-3 bottom=2.79  <<HISTORY: d1 10:44 registered at (53, 85) 35x792146 by InventoryService.RegisterPhysicalPallet | d1 10:44 Receive task created (->STG3A-3) by ReceivingService.CreateReceiveTaskForPallet | d1 10:44 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 11:30 Putaway task created (STG3A-3->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 11:30 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 11:31 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **OVERLAP** (day 1): OVERLAP Pallet_05_Slot4_Tier0 & Pallet_07_Slot5_Tier0 @3B-3  <<HISTORY: d1 11:57 registered at (54, 85) 32x999999 by InventoryService.RegisterPhysicalPallet | d1 11:57 Receive task created (->STG3B-3) by ReceivingService.CreateReceiveTaskForPallet | d1 11:58 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 12:12 Putaway task created (STG3B-3->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 12:12 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d1 12:14 registered at (54, 85) 32x482913 by InventoryService.RegisterPhysicalPallet | d1 12:14 Receive task created (->STG3B-3) by ReceivingService.CreateReceiveTaskForPallet | d1 12:14 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **FLOATING** (day 1): FLOATING Pallet_07_Slot5_Tier0 @3B-3 bottom=2.06  <<HISTORY: d1 12:14 registered at (54, 85) 32x482913 by InventoryService.RegisterPhysicalPallet | d1 12:14 Receive task created (->STG3B-3) by ReceivingService.CreateReceiveTaskForPallet | d1 12:14 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **WARN** (day 1): [AiNavigation] DS_FULL(Clone) cannot reach task position at (-47.09, 1.18, 36.39) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 25c1a31d-6210-45f3-8066-228c6211bcee (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 247/266 case(s).
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 0301f4cd-9d37-4ee9-978e-24dee4aeccc8 but the employee is 29.7m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet d31783a0-ba7c-4708-9744-5c1ad664032e but the employee is 13.5m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone): task target (-12.64, 0.08, 52.66) only partially reachable — stopped 1.98m short. Treating as arrived.
- **WARN** (day 2): [AiNavigation] DS_FULL(Clone): stuck at (-33.93, 1.11, 45.46) — walking out to (-45.19, 1.21, 38.96) (12.00m to mesh).
- **OVERLAP** (day 2): OVERLAP OutboundPallet_c116f24e-7565-4f78-9212-6a67533ff5e8_1 & OutboundPallet_c116f24e-7565-4f78-9212-6a67533ff5e8_1 @1B-1
- **FLOATING** (day 2): FLOATING OutboundPallet_c116f24e-7565-4f78-9212-6a67533ff5e8_1 @1B-1 bottom=2.47
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] Order OG3855 pallet 1 of 1: every pick face is empty - handing the task back until replenishment stocks them.
- **WARN** (day 3): [AiNavigation] Employee_Susan Mccarty: task target (-10.89, 1.34, 48.31) only partially reachable — stopped 0.41m short. Treating as arrived.
- **WARN** (day 3): [AiNavigation] Employee_Andrea Scott: stuck at (-7.41, 1.16, 48.38) — walking out to (-7.41, 0.04, 34.46) (12.92m to mesh).
- **WARN** (day 3): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 98fa4f53-3156-4dea-be6f-0ff0ea2d83ec but the employee is 4.5m away (max 4.5m) — refusing to start receiving.
- **OVERLAP** (day 4): OVERLAP Pallet_00_Slot0_Tier0 & Pallet_01_Slot0_Tier1 @3C-1  <<HISTORY: d3 17:37 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 18:39 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 18:42 moved (48, 86)->(-1, -1) by InventoryService.MovePallet | d3 18:51 adopted as outbound freight of order 01ec0457-5677-40db-957b-71fae73b24e2 (8 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 18:51 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 18:51 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d3 18:31 Putaway task created (STG2C-4->) by OrderService.RefileStrandedLanePallets | d3 18:57 moved (48, 86)->(-1, -1) by InventoryService.MovePallet | d3 19:07 adopted as outbound freight of order 01ec0457-5677-40db-957b-71fae73b24e2 (8 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 19:07 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 19:07 record destroyed by InventoryService.DestroyPallet | d3 19:07 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **FLOATING** (day 4): FLOATING Pallet_01_Slot0_Tier1 @3C-1 bottom=2.16  <<HISTORY: d3 18:31 Putaway task created (STG2C-4->) by OrderService.RefileStrandedLanePallets | d3 18:57 moved (48, 86)->(-1, -1) by InventoryService.MovePallet | d3 19:07 adopted as outbound freight of order 01ec0457-5677-40db-957b-71fae73b24e2 (8 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 19:07 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 19:07 record destroyed by InventoryService.DestroyPallet | d3 19:07 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **WARN** (day 4): [OrderService] Order OG4856 closed with a IN TRANSIT pallet (36 cs) that never made the trailer - not billed, returning it to stock.
- **ERROR** (day 4): [OrderSelectionTaskDriver] Pallet jack did not reach 'bay 01-06' ((-34.51, 1.15, 40.91)) after 9.2s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__108:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1520)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 2A-1 (order 235c41af-d7d6-4979-90f4-2a3e3ae220a2 gone)
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 536e5dea-fef7-4260-a87c-8e95fe41b7f0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 73/172 case(s).
- **WARN** (day 5): [AiNavigation] RT_Full(Clone) cannot reach task position at (-33.84, 1.14, 40.01) (path invalid) — abandoning the walk and handing the task back.
- **OVERLAP** (day 5): OVERLAP Pallet_01_Slot1_Tier0 & Pallet_00_Slot0_Tier0 @(24, 82)  <<HISTORY: d2 11:28 picked 1 (left 4) by InventoryService.PickFromPallet | d2 11:32 picked 1 (left 3) by InventoryService.PickFromPallet | d2 11:35 picked 1 (left 2) by InventoryService.PickFromPallet | d2 11:39 picked 1 (left 1) by InventoryService.PickFromPallet | d4 23:42 picked 1 (left 0) by InventoryService.PickFromPallet | d4 23:42 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d2 11:02 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 23:44 Replenish task created (01-12-A1->01-12-01) by ReplenishmentService.CreateReplenishTask | d5 00:15 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 00:33 moved (25, 82)->(-1, -1) by InventoryService.MovePallet | d5 00:38 moved (-1, -1)->(25, 82) by InventoryService.MovePallet | d5 00:38 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **FLOATING** (day 5): FLOATING Pallet_08_Slot4_Tier0 @2D-1 bottom=1.96  <<HISTORY: d5 03:42 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 04:47 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 04:51 moved (55, 86)->(-1, -1) by InventoryService.MovePallet | d5 05:00 adopted as outbound freight of order 31ea9963-ce57-406c-aa5c-1947ad60bb38 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 05:00 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 05:00 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 5): OVERLAP Pallet_08_Slot4_Tier0 & Pallet_05_Slot2_Tier1 @2D-1  <<HISTORY: d5 03:42 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 04:47 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 04:51 moved (55, 86)->(-1, -1) by InventoryService.MovePallet | d5 05:00 adopted as outbound freight of order 31ea9963-ce57-406c-aa5c-1947ad60bb38 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 05:00 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 05:00 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d5 02:34 Putaway task created (STG3B-2->) by OrderService.RefileStrandedLanePallets | d5 03:13 moved (54, 82)->(-1, -1) by InventoryService.MovePallet | d5 03:23 adopted as outbound freight of order 31ea9963-ce57-406c-aa5c-1947ad60bb38 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 03:23 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 03:23 record destroyed by InventoryService.DestroyPallet | d5 03:23 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **OVERLAP** (day 5): OVERLAP Pallet_02_Slot1_Tier0 & OutboundPallet_31ea9963-ce57-406c-aa5c-1947ad60bb38_1 @2A-3  <<HISTORY: d5 02:14 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 02:20 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 02:25 moved (54, 85)->(-1, -1) by InventoryService.MovePallet | d5 02:32 adopted as outbound freight of order 31ea9963-ce57-406c-aa5c-1947ad60bb38 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 02:32 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 02:32 record destroyed by InventoryService.DestroyPallet>>
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 31ea9963-ce57-406c-aa5c-1947ad60bb38 (Cluck & Cover Poultry) has no reachable pick location for its remaining line item(s) — staging short at 468/484.
- **WARN** (day 5): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 4cdf9d33-9000-4a2b-b675-f986a0c2f930 but the employee is 5.5m away (max 4.5m) — refusing to start receiving.
- **FLOATING** (day 5): FLOATING OutboundPallet_536e5dea-fef7-4260-a87c-8e95fe41b7f0_1 @1A-3 bottom=2.27
- **OVERLAP** (day 5): OVERLAP OutboundPallet_536e5dea-fef7-4260-a87c-8e95fe41b7f0_1 & OutboundPallet_536e5dea-fef7-4260-a87c-8e95fe41b7f0_1 @1A-3
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 71d64cde-c7ad-46fb-bab8-f89c4f1d02fa (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 16/240 case(s).
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 09b5ab23-5161-4fb2-a9fc-16dc55864fa2 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 28/140 case(s).
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 09b5ab23-5161-4fb2-a9fc-16dc55864fa2 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 76/140.
- **OVERLAP** (day 6): OVERLAP Pallet_09_Slot4_Tier1 & OutboundPallet_71d64cde-c7ad-46fb-bab8-f89c4f1d02fa_1 @1B-1  <<HISTORY: d5 20:14 PalletPick task Open->Available by OrderService.AllocateHeldWork | d5 20:49 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 20:59 moved (21, 83)->(-1, -1) by InventoryService.MovePallet | d5 21:12 adopted as outbound freight of order 71d64cde-c7ad-46fb-bab8-f89c4f1d02fa (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 21:12 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 21:12 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 6): FLOATING OutboundPallet_71d64cde-c7ad-46fb-bab8-f89c4f1d02fa_1 @1B-1 bottom=2.26
- **OVERLAP** (day 6): OVERLAP OutboundPallet_71d64cde-c7ad-46fb-bab8-f89c4f1d02fa_1 & OutboundPallet_71d64cde-c7ad-46fb-bab8-f89c4f1d02fa_1 @1A-4
- **FLOATING** (day 6): FLOATING OutboundPallet_71d64cde-c7ad-46fb-bab8-f89c4f1d02fa_1 @1A-4 bottom=2.35
- **WARN** (day 6): [AiNavigation] RT_Full(Clone): stuck at (-12.89, 1.18, 48.02) — walking out to (-12.89, 1.08, 35.50) (12.39m to mesh).
- **WARN** (day 7): [OrderSelectionTaskDriver] Order bb9dab38-b0ae-4ad7-b21a-90bd616259e0 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 81/135 case(s).
- **WARN** (day 7): [OrderSelectionTaskDriver] Order bb9dab38-b0ae-4ad7-b21a-90bd616259e0 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 131/135.
- **OVERLAP** (day 7): OVERLAP OutboundPallet_bb9dab38-b0ae-4ad7-b21a-90bd616259e0_1 & OutboundPallet_bb9dab38-b0ae-4ad7-b21a-90bd616259e0_1 @2A-1
- **FLOATING** (day 7): FLOATING OutboundPallet_bb9dab38-b0ae-4ad7-b21a-90bd616259e0_1 @2A-2 bottom=1.41

## Bot actions (first 400)
