# Playtest block 25 — days 1–8

## Score: **43.1 (-14.0)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (3 shipped, 8 late) | 15% | 27.3 (-6.1) |
| Fill rate | 10% | 92.2 (+1.7) |
| Throughput (54 plt vs target 84) | 15% | 64.3 (-35.7) |
| Revenue ($43,475) | 15% | 77.6 (-22.4) |
| Profit (cash + stock -67,726) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 54.9 (-45.1) |
| Reliability (bugs) | 25% | 13.0 (-5.8) |
| Reputation (0 to 0) | 5% | 50.0 (-16.7) |

Orders shipped **3**, late/fined **8**, pallets shipped **54**. Cash $97,855 → $-10,426 (-108,281). Stock at cost $0 → $40,555; cash + stock -67,726.
Bot did: 6 contracts signed, 27 dock bookings, 8 orders released, 26 POs, 0 rush POs.

## Late orders - state when it came due
- 4 x never released
- 2 x still being picked
- 1 x still loading
- 1 x staged but no trailer loaded it

- day 3: OG2770 (Crunch-Time Cereals) was never released to the floor. Fined $9,512. Lines 3, 728 cs, released=False.
- day 4: OG4771 (Sneaky Pete's Seafood) was still loading when it came due. Fined $1,498. Lines 15, 359 cs, released=True.
- day 5: OG4769 (Puff & Stuff Pastries) was never released to the floor. Fined $8,593. Lines 3, 672 cs, released=False.
- day 5: OG4773 (Barnacle Bill's Bait & Bite) was never released to the floor. Fined $2,016. Lines 3, 364 cs, released=False.
- day 5: OG5772 (Sneaky Pete's Seafood) was staged but no trailer loaded it (door 1). Fined $2,082. Lines 22, 483 cs, released=True.
- day 6: OG5774 (Pony Express Oats) was never released to the floor. Fined $2,458. Lines 2, 475 cs, released=False.
- day 6: OG6775 (Sneaky Pete's Seafood) was still being picked (136/322 cs). Fined $1,337. Lines 12, 322 cs, released=True.
- day 7: OG7776 (Sneaky Pete's Seafood) was still being picked (215/404 cs). Fined $1,634. Lines 16, 404 cs, released=True.

## Change since last block
- Fixed / no longer seen: **10**
  - FLOATING| OutboundPallet_###a#-ba###_# @#A# bottom=#
  - OVERLAP| OutboundPallet_###a#-ba###_# & OutboundPallet_###a#-ba###_# @#A#
  - NO-TASK| lane pallet #A# sku=# load=#
  - OVERLAP| OutboundPallet_###ca##a#-#_# & OutboundPallet_###ca##a#-#_# @#E#
  - ERROR|[MHEBoundary] 'ReachTruck' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside.
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_###a#-ba###_# @#A#
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #E and it never freed up — giving up and re-queueing.
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #A and it never freed up — giving up and re-queueing.
  - WARN|[AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **16**
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it.
  - OVERLAP| OutboundPallet_#-aa##a#ed#ecbf#_# & OutboundPallet_#-aa##a#ed#ecbf#_# @#C#
  - WARN|[AiNavigation] Employee_Barbara Johnson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#E#
  - OVERLAP| OutboundPallet_#-e###-#_# & OutboundPallet_#-e###-#_# @#D#
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A#
  - FLOATING| Pallet_#_Slot#_Tier# @#A# bottom=#
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C#
  - FLOATING| Pallet_#_Slot#_Tier# @#C# bottom=#
  - FLOATING| OutboundPallet_#-aa##a#ed#ecbf#_# @#C# bottom=#
  - NO-TASK| lane pallet #B# sku=# load=#
  - REPEATING|x# and counting: [EquipmentBattery] Dockstocker: not sliding # m by hand - that is a navigation job. Trying the path again.
  - REPEATING|x# and counting: [TrailerLoad] #B: door end blocked (the pallet at pos# belongs to order ##c#efb#a##, not one being loaded here); the far end gives # - stopping with # (the pallet at pos# belongs to order ##c#efb#a##, not one being loaded here).
  - REPEATING|x# and counting: [OrderService] Order ##a##d#-a#-# (Sneaky Pete's Seafood) was released to loading from #B but no staged pallet for it could be found there — reverted to Staged so it can be re-released. Its pallets are most likely sitting somewhere other than the lane.
  - REPEATING|x# and counting: [TrailerLoad] # order(s) at #B had no staged pallets to load — reverted to Staged.

## Bugs found (19 distinct, 54 incidents)

| Incidents | Bug |
|---|---|
| 29 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it. |
| 4 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 2 | OVERLAP —  OutboundPallet_#-aa##a#ed#ecbf#_# & OutboundPallet_#-aa##a#ed#ecbf#_# @#C# |
| 4 | WARN — [AiNavigation] Employee_Barbara Johnson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#E# |
| 1 | OVERLAP —  OutboundPallet_#-e###-#_# & OutboundPallet_#-e###-#_# @#D# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 1 | FLOATING —  OutboundPallet_#-aa##a#ed#ecbf#_# @#C# bottom=# |
| 1 | NO-TASK —  lane pallet #B# sku=# load=# |
| 1 | REPEATING — x# and counting: [EquipmentBattery] Dockstocker: not sliding # m by hand - that is a navigation job. Trying the path again. |
| 1 | REPEATING — x# and counting: [TrailerLoad] #B: door end blocked (the pallet at pos# belongs to order ##c#efb#a##, not one being loaded here); the far end gives # - stopping with # (the pallet at pos# belongs to order ##c#efb#a##, not one being loaded here). |
| 1 | REPEATING — x# and counting: [OrderService] Order ##a##d#-a#-# (Sneaky Pete's Seafood) was released to loading from #B but no staged pallet for it could be found there — reverted to Staged so it can be re-released. Its pallets are most likely sitting somewhere other than the lane. |
| 1 | REPEATING — x# and counting: [TrailerLoad] # order(s) at #B had no staged pallets to load — reverted to Staged. |
| 1 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReachTruckOperator] Replenish: could not reach pallet at ### within #m. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Barbara Johnson: stuck at (16.46, 1.15, 25.63) — walking out to (16.46, 0.04, 13.00) (11.63m to mesh).
- **WARN** (day 1): [AiNavigation] ReachTruck: stuck at (-0.95, 1.11, 19.45) — walking out to (-0.95, 0.08, 12.17) (6.36m to mesh).
- **ERROR** (day 1): [MHEBoundary] 'ReachTruck' fell below the floor at (-0.95, 0.36, 12.84) (cell (49, 59)). Putting it back on its last inside spot (-0.95, 0.64, 13.51). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **NO-TASK** (day 2): NO-TASK lane pallet 2B-2 sku=801236 load=4501819977  <<HISTORY: d1 18:47 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:42 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d1 19:42 PalletPick task created (STG2B-2->) by OrderService.FileReleasedOrderTasks | d1 19:42 PalletPick task Available->Open by OrderService.FileReleasedOrderTasks | d1 19:42 PalletPick task Open->Available by OrderService.AllocateHeldWork | d2 02:26 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **OVERLAP** (day 2): OVERLAP Pallet_10_Slot8_Tier0 & Pallet_00_Slot0_Tier0 @2E-1  <<HISTORY: d1 20:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d1 22:36 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 22:49 moved (53, 65)->(-1, -1) by InventoryService.MovePallet | d1 22:59 adopted as outbound freight of order 667e534d-e975-4912-9848-e11032ff5765 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d1 22:59 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 22:59 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d1 20:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d1 20:50 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 21:05 moved (34, 62)->(-1, -1) by InventoryService.MovePallet | d1 21:37 adopted as outbound freight of order 667e534d-e975-4912-9848-e11032ff5765 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d1 21:37 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 21:37 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 2): OVERLAP OutboundPallet_667e534d-e975-4912-9848-e11032ff5765_1 & OutboundPallet_667e534d-e975-4912-9848-e11032ff5765_2 @2D-5
- **OVERLAP** (day 3): OVERLAP Pallet_00_Slot0_Tier0 & Pallet_01_Slot1_Tier0 @3A-5  <<HISTORY: d3 02:37 registered at (61, 69) 16x172495 by InventoryService.RegisterPhysicalPallet | d3 02:37 Receive task created (->STG3A-5) by ReceivingService.CreateReceiveTaskForPallet | d3 02:37 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>  <<HISTORY: d3 02:52 registered at (61, 69) 16x172495 by InventoryService.RegisterPhysicalPallet | d3 02:52 Receive task created (->STG3A-5) by ReceivingService.CreateReceiveTaskForPallet | d3 02:52 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **FLOATING** (day 3): FLOATING Pallet_01_Slot1_Tier0 @3A-5 bottom=2.57  <<HISTORY: d3 02:52 registered at (61, 69) 16x172495 by InventoryService.RegisterPhysicalPallet | d3 02:52 Receive task created (->STG3A-5) by ReceivingService.CreateReceiveTaskForPallet | d3 02:52 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **WARN** (day 4): [ReachTruckOperator] Replenish: could not reach pallet at 02-09-01 within 0.15m.
- **DOOR-DEADLOCK** (day 4): OG4771 is Staged but door 1's truck has waited 90 min with no one loading it.
- **DOOR-DEADLOCK** (day 4): OG4771 is Loading but door 1's truck has waited 135 min with no one loading it.
- **REPEATING** (day 5): x25 and counting: [EquipmentBattery] Dockstocker: not sliding 9.9 m by hand - that is a navigation job. Trying the path again.
- **OVERLAP** (day 6): OVERLAP Pallet_00_Slot0_Tier0 & Pallet_06_Slot6_Tier0 @1C-1  <<HISTORY: d6 00:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d6 00:12 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d6 00:24 moved (42, 76)->(-1, -1) by InventoryService.MovePallet | d6 00:34 adopted as outbound freight of order e96882d6-aa45-489a-90ed-842465ecbf51 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d6 00:34 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d6 00:34 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d6 00:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d6 00:12 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d6 00:22 moved (34, 76)->(-1, -1) by InventoryService.MovePallet | d6 00:39 adopted as outbound freight of order e96882d6-aa45-489a-90ed-842465ecbf51 (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d6 00:39 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d6 00:39 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 6): FLOATING Pallet_06_Slot6_Tier0 @1C-1 bottom=2.08  <<HISTORY: d6 00:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d6 00:12 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d6 00:22 moved (34, 76)->(-1, -1) by InventoryService.MovePallet | d6 00:39 adopted as outbound freight of order e96882d6-aa45-489a-90ed-842465ecbf51 (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d6 00:39 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d6 00:39 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 6): FLOATING OutboundPallet_e96882d6-aa45-489a-90ed-842465ecbf51_1 @1C-2 bottom=1.81
- **OVERLAP** (day 6): OVERLAP OutboundPallet_e96882d6-aa45-489a-90ed-842465ecbf51_1 & OutboundPallet_e96882d6-aa45-489a-90ed-842465ecbf51_1 @1C-2
- **REPEATING** (day 7): x25 and counting: [TrailerLoad] 1B: door end blocked (the pallet at pos3 belongs to order 6ee03fae-659c-4efb-855a-6f475b24161d, not one being loaded here); the far end gives 0 - stopping with 0 (the pallet at pos3 belongs to order 6ee03fae-659c-4efb-855a-6f475b24161d, not one being loaded here).
- **REPEATING** (day 7): x25 and counting: [OrderService] Order b1d21933-8a18-4d87-a823-c439da435ce5 (Sneaky Pete's Seafood) was released to loading from 1B but no staged pallet for it could be found there — reverted to Staged so it can be re-released. Its pallets are most likely sitting somewhere other than the lane.
- **REPEATING** (day 7): x25 and counting: [TrailerLoad] 1 order(s) at 1B had no staged pallets to load — reverted to Staged.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1233 Backdoor Bill's Overstock: 4 plt $3,104
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 18 door 1
- d1 PO IG1234 Anchor Point Foods: 5 plt $3,424
- d1 placed parked Inbound Anchor Point Foods day 1 block 19 door 2
- d1 PO IG1235 Blue Ridge Provisions: 5 plt $6,143
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 20 door 1
- d1 signed bulk BULK_Blocky Bakeshop_D1_0
- d1 signed bulk BULK_Puff & Stuff Pastries_D1_1
- d1 PO IG1236 Bulk Basin Foods: 7 plt $2,693
- d1 placed parked Inbound Bulk Basin Foods day 1 block 22 door 2
- d1 PO IG1237 Fairweather Trading Co.: 15 plt $10,164
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 24 door 1
- d1 PO IG1238 Golden Grove Gourmet: 10 plt $20,115
- d1 placed parked Inbound Golden Grove Gourmet day 1 block 25 door 3
- d1 PO IG1239 Homestead Market Co.: 1 plt $1,000
- d1 placed parked Inbound Homestead Market Co. day 1 block 26 door 2
- d1 PO IG1240 Homestead Market Co.: 1 plt $2,000
- d1 placed parked Inbound Homestead Market Co. day 1 block 28 door 2
- d1 PO IG1241 Anchor Point Foods: 6 plt $3,120
- d1 placed parked Inbound Anchor Point Foods day 1 block 30 door 2
- d1 PO IG1242 Golden Grove Gourmet: 11 plt $21,855
- d1 placed parked Inbound Golden Grove Gourmet day 1 block 34 door 1
- d1 booked Blocky Bakeshop day 2 block 2 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 12 pallet pick(s) / 2 reach truck(s): stock ready hour 19 + 5.3h of work)
- d1 released 1 order(s)
- d1 released 1 order(s)
- d2 PO IG2243 Bulk Basin Foods: 2 plt $224
- d2 PO IG2244 Anchor Point Foods: 1 plt $520
- d2 placed parked Inbound Bulk Basin Foods day 2 block 2 door 2
- d2 placed parked Inbound Anchor Point Foods day 2 block 2 door 3
- d2 signed bulk BULK_Crunch-Time Cereals_D2_0
- d2 PO IG2245 Fairweather Trading Co.: 9 plt $4,432
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 34 door 2
- d2 PO IG2246 Backdoor Bill's Overstock: 2 plt $576
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 36 door 3
- d2 PO IG2247 Anchor Point Foods: 5 plt $2,490
- d2 placed parked Inbound Anchor Point Foods day 2 block 37 door 3
- d2 PO IG2248 Bulk Basin Foods: 5 plt $1,624
- d2 placed parked Inbound Bulk Basin Foods day 2 block 39 door 2
- d2 released 1 order(s)
- d3 PO IG3249 Backdoor Bill's Overstock: 2 plt $608
- d3 PO IG3250 Bulk Basin Foods: 1 plt $112
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 2 door 1
- d3 placed parked Inbound Bulk Basin Foods day 3 block 2 door 2
- d3 PO IG3251 Anchor Point Foods: 5 plt $2,578
- d3 PO IG3252 Bulk Basin Foods: 1 plt $160
- d3 PO IG3253 Homestead Market Co.: 1 plt $513
- d3 placed parked Inbound Anchor Point Foods day 3 block 36 door 2
- d3 placed parked Inbound Bulk Basin Foods day 3 block 36 door 3
- d3 placed parked Inbound Homestead Market Co. day 3 block 37 door 3
- d3 signed bulk BULK_Barnacle Bill's Bait & Bite_D3_0
- d3 signed bulk BULK_Pony Express Oats_D3_1
- d3 PO IG3254 Fairweather Trading Co.: 7 plt $6,796
- d3 placed parked Inbound Fairweather Trading Co. day 3 block 42 door 1
- d3 PO IG3255 Anchor Point Foods: 1 plt $672
- d3 placed parked Inbound Anchor Point Foods day 3 block 43 door 2
- d3 PO IG3256 Bulk Basin Foods: 3 plt $1,632
- d3 placed parked Inbound Bulk Basin Foods day 3 block 44 door 2
- d3 released 1 order(s)
- d4 PO IG4257 Homestead Market Co.: 1 plt $1,040
- d4 PO IG4258 Bulk Basin Foods: 2 plt $1,104
- d4 placed parked Inbound Homestead Market Co. day 4 block 2 door 1
- d4 placed parked Inbound Bulk Basin Foods day 4 block 2 door 2
- d4 released 1 order(s)
- d6 released 1 order(s)
- d6 released 1 order(s)
- d7 released 1 order(s)
