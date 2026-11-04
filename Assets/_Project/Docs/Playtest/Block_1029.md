# Playtest block 1029 — days 1–8

## Score: **61.4** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (21 shipped, 6 late) | 15% | 71.4 |
| Fill rate | 10% | 97.8 |
| Throughput (184 plt vs target 84) | 15% | 100.0 |
| Revenue ($309,223) | 15% | 100.0 |
| Profit (cash -261,765) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 3.8 |
| Reputation (0 to 231) | 5% | 100.0 |

Orders shipped **21**, late/fined **6**, pallets shipped **184**. Cash $500,000 → $238,235 (-261,765). Stock at cost $0 → $65,478 (not part of profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Late orders - state when it came due
- 3 x loaded but the truck had not left

- day 5: OG5841 (Barnacle Bill's Bait & Bite) was loaded but the truck had not left. Fined $680. Lines 14, 173 cs, released=True.
- day 5: OG5840 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,546. Lines 21, 360 cs, released=True.
- day 6: OG5845 (Barnacle Bill's Bait & Bite) was loaded but the truck had not left. Fined $5,258. Lines 3, 930 cs, released=True.

## Bugs found (79 distinct, 203 incidents)

| Incidents | Bug |
|---|---|
| 45 | OVERLAP —  A Chep & A Chep @(#, #) |
| 7 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 7 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 5 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 5 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 3 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 3 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 3 | GHOST-SLOT — RECORD # # x# cell=(#, #) lane= - no object |
| 3 | FLOATING —  OutboundPallet_###f####_# @#C# bottom=# |
| 3 | OVERLAP —  OutboundPallet_###f####_# & OutboundPallet_###f####_# @#C# |
| 3 | OVERLAP —  Pallet_#_Slot#_Tier# & A Chep @(#, #) |
| 3 | FLOATING —  A Chep @#E# bottom=# |
| 2 | OVERLAP —  OutboundPallet_###f####_restored & OutboundPallet_###f####_restored @#A# |
| 2 | FLOATING —  OutboundPallet_###f####_restored @#A# bottom=# |
| 6 | WARN — [OrderSelectionTaskDriver] Order ##ba##b-a#b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 2 | OVERLAP —  A Chep & A Chep @#E# |
| 2 | FLOATING —  A Chep @#D# bottom=# |
| 2 | OVERLAP —  A Chep & A Chep @#D# |
| 2 | FLOATING —  OutboundPallet_##a###a#a-#_# @#A# bottom=# |
| 2 | OVERLAP —  OutboundPallet_##a###a#a-#_# & OutboundPallet_##a###a#a-#_# @#A# |
| 5 | WARN — [OrderSelectionTaskDriver] Order ##b##c#-a### (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 5 | WARN — [OrderSelectionTaskDriver] Order ###e##e### (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | EXCEPTION — MissingReferenceException: The object of type 'UnityEngine.AI.NavMeshAgent' has been destroyed but you are still trying to access it. |
| 4 | WARN — [AiNavigation] Employee_Susan Mccarty: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | EXCEPTION — MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it. |
| 4 | WARN — [AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 4 | WARN — [OrderSelectionTaskDriver] Order #-eb##ab#-a### (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | NO-RECORD —  lane pallet #C# A Chep |
| 1 | GHOST-SLOT — SLOT ### names # - no object |
| 1 | GHOST-SLOT — SLOT ##-A# names # - no object |
| 3 | WARN — [OrderSelectionTaskDriver] Order ###f#### (Sneaky Pete's Seafood) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | OVERLAP —  OutboundPallet_###f####_restored & OutboundPallet_###f####_restored @#B# |
| 1 | FLOATING —  OutboundPallet_###f####_restored @#B# bottom=# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#E# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#E# |
| 3 | WARN — [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 3 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | FLOATING —  OutboundPallet_###f####_# @#B# bottom=# |
| 1 | OVERLAP —  OutboundPallet_###f####_# & OutboundPallet_###f####_# @#B# |
| 1 | FLOATING —  A Chep @#A# bottom=# |
| 1 | OVERLAP —  A Chep & A Chep @#A# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-b#e#feb-b#db-#_# @#C# |
| 1 | FLOATING —  OutboundPallet_#-b#e#feb-b#db-#_# @#C# bottom=# |
| 1 | OVERLAP —  A Chep & Pallet_#_Slot#_Tier# @(#, #) |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##ba##b-a#b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 1 | TRUCK-STALLED —  Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #) |
| 1 | OVERLAP —  A Chep & OutboundPallet_###e##e###_# @#D# |
| 1 | OVERLAP —  A Chep & OutboundPallet_###e##e###_restored @#E# |
| 1 | FLOATING —  OutboundPallet_###e##e###_# @#D# bottom=# |
| 1 | FLOATING —  OutboundPallet_###e##e###_restored @#D# bottom=# |
| 1 | OVERLAP —  OutboundPallet_###e##e###_restored & OutboundPallet_###e##e###_restored @#D# |
| 3 | WARN — [OrderSelectionTaskDriver] Order #-eb##ab#-a### (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##d#b#dcd#e#-#_# @#B# |
| 1 | FLOATING —  OutboundPallet_##d#b#dcd#e#-#_# @#B# bottom=# |
| 2 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 2 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [OrderSelectionTaskDriver] Order ###f#### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ORPHAN-OUTBOUND —  #A# (order ##b##ab#c### gone) |
| 2 | WARN — [AiNavigation] Employee_Andrea Scott cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 2 | WARN — [OrderSelectionTaskDriver] Order ##f#c#-a#b## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 2 | WARN — [OrderSelectionTaskDriver] Order ##b##c#-a### (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | ORPHAN-OUTBOUND —  #D# (order ###e##e### gone) |
| 1 | REPEATING — x# and counting: [PlaytestBot] PLAYER BLOCK # DONE: score #. Report: Assets/_Project/Docs/Playtest\Block_#.md |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##f##bdf#b### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##b##ab#c### (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##eec##c#f## (Happy Cow Creamery) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e#b#-a### (Sprout Scout Foods) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##f#c#-a#b## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##b##c#-a### (Barnacle Bill's Bait & Bite) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###e##e### (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [AiNavigation] Employee_Henry Robinson cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##a###a#a-# (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##b#b#f#### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##b#b#f#### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e#a###fd### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##d#b#dcd#e#-# (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **FLOATING** (day 5): FLOATING A Chep @3E-4 bottom=2.15  <<HISTORY: d5 12:54 Putaway task Available->Cancelled by PlacementSystem.ApplySaveData | d5 12:54 Putaway task Available->Cancelled by PlacementSystem.ApplySaveData | d5 12:54 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d5 12:54 adopted as outbound freight of order 44117fa8-1020-4e10-84e0-4eaa1619aae9 (80 cs) by OutboundPalletBuilder.AdoptFullPallet>>
- **OVERLAP** (day 5): OVERLAP A Chep & A Chep @3E-4  <<HISTORY: d5 12:54 Putaway task Available->Cancelled by PlacementSystem.ApplySaveData | d5 12:54 Putaway task Available->Cancelled by PlacementSystem.ApplySaveData | d5 12:54 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d5 12:54 adopted as outbound freight of order 44117fa8-1020-4e10-84e0-4eaa1619aae9 (80 cs) by OutboundPalletBuilder.AdoptFullPallet>>  <<HISTORY: d5 12:54 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d5 12:54 Putaway task Available->Cancelled by PlacementSystem.ApplySaveData | d5 12:54 adopted as outbound freight of order 44117fa8-1020-4e10-84e0-4eaa1619aae9 (80 cs) by OutboundPalletBuilder.AdoptFullPallet>>
- **FLOATING** (day 5): FLOATING A Chep @3D-3 bottom=2.15  <<HISTORY: d5 15:58 adopted as outbound freight of order 44117fa8-1020-4e10-84e0-4eaa1619aae9 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 15:58 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 15:58 record destroyed by InventoryService.DestroyPallet | d5 15:58 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 15:58 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 15:58 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **OVERLAP** (day 5): OVERLAP A Chep & A Chep @3D-3  <<HISTORY: d5 15:58 adopted as outbound freight of order 44117fa8-1020-4e10-84e0-4eaa1619aae9 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 15:58 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 15:58 record destroyed by InventoryService.DestroyPallet | d5 15:58 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 15:58 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 15:58 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>  <<HISTORY: d5 15:41 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d5 15:47 adopted as outbound freight of order 44117fa8-1020-4e10-84e0-4eaa1619aae9 (80 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 15:47 PalletPick task Available->Complete by WorkQueueSystem.CompleteTask | d5 15:47 record destroyed by InventoryService.DestroyPallet | d5 15:47 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 15:47 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **OVERLAP** (day 5): OVERLAP A Chep & OutboundPallet_44117fa8-1020-4e10-84e0-4eaa1619aae9_1 @3D-5  <<HISTORY: d5 13:08 moved (-1, -1)->(47, 81) by InventoryService.MovePallet | d5 15:48 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 16:01 moved (47, 81)->(-1, -1) by InventoryService.MovePallet | d5 16:14 adopted as outbound freight of order 44117fa8-1020-4e10-84e0-4eaa1619aae9 (48 cs) by OutboundPalletBuilder.AdoptFullPallet | d5 16:14 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 16:14 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 5): OVERLAP A Chep & OutboundPallet_44117fa8-1020-4e10-84e0-4eaa1619aae9_restored @3E-2  <<HISTORY: d5 12:54 PalletPick task Available->Cancelled by PlacementSystem.ApplySaveData | d5 12:54 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d5 12:54 Putaway task Available->Cancelled by PlacementSystem.ApplySaveData | d5 12:54 adopted as outbound freight of order 44117fa8-1020-4e10-84e0-4eaa1619aae9 (80 cs) by OutboundPalletBuilder.AdoptFullPallet>>
- **FLOATING** (day 5): FLOATING OutboundPallet_44117fa8-1020-4e10-84e0-4eaa1619aae9_1 @3D-5 bottom=2.04
- **FLOATING** (day 5): FLOATING OutboundPallet_44117fa8-1020-4e10-84e0-4eaa1619aae9_restored @3D-2 bottom=1.71
- **OVERLAP** (day 5): OVERLAP OutboundPallet_44117fa8-1020-4e10-84e0-4eaa1619aae9_restored & OutboundPallet_44117fa8-1020-4e10-84e0-4eaa1619aae9_restored @3D-2
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 4749ec70-28a5-4900-8a7a-a52f02fafb7a (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 89/117.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order c26f3199-8b5b-4f84-9930-8c4004a20ad6 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 35/142 case(s).
- **WARN** (day 6): [OrderSelectionTaskDriver] Order c26f3199-8b5b-4f84-9930-8c4004a20ad6 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 125/142.
- **WARN** (day 6): [OrderService] Order OG5845 closed with a STAGED pallet (48 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 6): ORPHAN-OUTBOUND 3D-1 (order 44117fa8-1020-4e10-84e0-4eaa1619aae9 gone)
- **FLOATING** (day 6): FLOATING OutboundPallet_4749ec70-28a5-4900-8a7a-a52f02fafb7a_1 @2A-1 bottom=2.22
- **OVERLAP** (day 6): OVERLAP OutboundPallet_4749ec70-28a5-4900-8a7a-a52f02fafb7a_1 & OutboundPallet_4749ec70-28a5-4900-8a7a-a52f02fafb7a_1 @2A-1
- **WARN** (day 6): [OrderSelectionTaskDriver] Order ab3af4e7-eb20-4ab6-a956-76e885a4a612 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 83/228 case(s).
- **WARN** (day 6): [OrderSelectionTaskDriver] Order ab3af4e7-eb20-4ab6-a956-76e885a4a612 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 118/228.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order c5f5cc1b-e2a4-4444-9fd8-8f8ee9704682 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 69/81.
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 89157185-0d5b-4dcd-80e0-a41bb2409352 (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at 243/472.
- **OVERLAP** (day 7): OVERLAP Pallet_02_Slot1_Tier0 & OutboundPallet_89157185-0d5b-4dcd-80e0-a41bb2409352_1 @3B-5  <<HISTORY: d7 14:51 Putaway task created (STG3A-4->) by OrderService.RefileStrandedLanePallets | d7 14:58 moved (53, 85)->(-1, -1) by InventoryService.MovePallet | d7 15:03 adopted as outbound freight of order 89157185-0d5b-4dcd-80e0-a41bb2409352 (60 cs) by OutboundPalletBuilder.AdoptFullPallet | d7 15:03 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d7 15:03 record destroyed by InventoryService.DestroyPallet | d7 15:03 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **FLOATING** (day 7): FLOATING OutboundPallet_89157185-0d5b-4dcd-80e0-a41bb2409352_1 @3B-5 bottom=2.06
- **REPEATING** (day 8): x25 and counting: [PlaytestBot] PLAYER BLOCK 1029 DONE: score 61.0. Report: Assets/_Project/Docs/Playtest\Block_1029.md

## Bot actions (first 400)
