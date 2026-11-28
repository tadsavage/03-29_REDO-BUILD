# Playtest block 22 — days 1–8

## Score: **53.5 (+23.3)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (7 shipped, 12 late) | 15% | 14.3 (+4.3) |
| Fill rate | 10% | 94.2 (+13.6) |
| Throughput (95 plt vs target 84) | 15% | 100.0 (+71.4) |
| Revenue ($88,458) | 15% | 100.0 (+77.0) |
| Profit (cash + stock -67,170) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 68.0 (+3.9) |
| Reliability (bugs) | 25% | 14.1 (-4.6) |
| Reputation (0 to 30) | 5% | 100.0 (0.0) |

Orders shipped **7**, late/fined **12**, pallets shipped **95**. Cash $97,857 → $1,997 (-95,860). Stock at cost $0 → $28,690; cash + stock -67,170.
Bot did: 9 contracts signed, 95 dock bookings, 15 orders released, 37 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **5**
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it.
  - ERROR|[MHEBoundary] 'Dockstocker' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|[MHEBoundary] 'Dockstocker' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside.
  - ERROR|[ReceiverReceivingWorkflow] No master record for pallet #-a#f#a##ffc-#
  - WARN|[AiNavigation] Employee_Judith Harmon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **14**
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B#
  - FLOATING| Pallet_#_Slot#_Tier# @#B# bottom=#
  - FLOATING| OutboundPallet_#-a##d#-a#-#_# @#A# bottom=#
  - OVERLAP| OutboundPallet_#-a##d#-a#-#_# & OutboundPallet_#-a##d#-a#-#_# @#A#
  - TRUCK-OVERLAP| Truck→PO_IG# & Truck→Outbound_Door# at (#, #, #)
  - WARN|[AiNavigation] Employee_James Wallace: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #C and it never freed up — giving up and re-queueing.
  - ERROR|[ReceiverReceivingWorkflow] No master record for pallet ##d#c#f#fa#ec#a
  - NO-TASK| lane pallet #A# sku=# load=#
  - NO-TASK| lane pallet #C# sku=# load=#
  - REPEATING|x# and counting: [DockSchedule] Caffeine Fiend Coffee's #:# slot on day # elapsed with nothing loaded — door freed for rebooking, kept on the grid struck through as missed.
  - TRUCK-OVERLAP| Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #)
  - WARN|[OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock.
  - WARN|[AiNavigation] Employee_Joshua Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (21 distinct, 36 incidents)

| Incidents | Bug |
|---|---|
| 4 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 5 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 2 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 2 | FLOATING —  OutboundPallet_#-a##d#-a#-#_# @#A# bottom=# |
| 2 | OVERLAP —  OutboundPallet_#-a##d#-a#-#_# & OutboundPallet_#-a##d#-a#-#_# @#A# |
| 2 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→Outbound_Door# at (#, #, #) |
| 3 | WARN — [AiNavigation] Employee_James Wallace: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [ReachTruckOperator] 'ReachTruck' waited #s for lane #C and it never freed up — giving up and re-queueing. |
| 1 | ERROR — [ReceiverReceivingWorkflow] No master record for pallet ##d#c#f#fa#ec#a |
| 1 | ERROR — [MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | ERROR — [MHEBoundary] 'ReachTruck' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside. |
| 1 | NO-TASK —  lane pallet #A# sku=# load=# |
| 1 | NO-TASK —  lane pallet #C# sku=# load=# |
| 1 | REPEATING — x# and counting: [DockSchedule] Caffeine Fiend Coffee's #:# slot on day # elapsed with nothing loaded — door freed for rebooking, kept on the grid struck through as missed. |
| 1 | REPEATING — x# and counting: [EquipmentBattery] Dockstocker: not sliding # m by hand - that is a navigation job. Trying the path again. |
| 1 | TRUCK-OVERLAP —  Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #) |
| 1 | WARN — [ReachTruckOperator] 'ReachTruck' waited #s for lane #A and it never freed up — giving up and re-queueing. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Joshua Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_James Wallace: stuck at (-2.07, 1.15, 24.66) — walking out to (-2.07, 0.04, 11.66) (12.05m to mesh).
- **TRUCK-OVERLAP** (day 2): TRUCK-OVERLAP Truck→PO_IG2128 & Truck→Outbound_Door1 at (-7.6, 0.0, -40.0)
- **NO-TASK** (day 3): NO-TASK lane pallet 2A-2 sku=615873 load=6735920615  <<HISTORY: d3 03:59 Receive task created (->STG2A-2) by ReceivingService.CreateReceiveTaskForPallet | d3 04:01 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 04:12 Putaway task created (STG2A-2->) by ReceiverReceivingWorkflow.CreatePutawayTask | d3 04:12 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 04:12 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 04:12 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask>>
- **WARN** (day 3): [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2A and it never freed up — giving up and re-queueing.
- **WARN** (day 3): [ReachTruckOperator] 'ReachTruck' waited 45s for lane 1C and it never freed up — giving up and re-queueing.
- **ERROR** (day 3): [ReceiverReceivingWorkflow] No master record for pallet 06df891e-208d-4c2f-87fa-81423ec0781a
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.ReceiverReceivingWorkflow:BeginReceivingAt (GameCore.Labor.WorkTask,UnityEngine.Transform,single) (at Assets/_Project/Scripts/1. FSM/Core/Labor/ReceiverReceivingWorkflow.cs:247)
  GameCore.Actors.ReceivingTaskDriver:OnArrivedAtStandPosition (GameCore.Labor.WorkTask,UnityEngine.Transform,int) (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:213)
  GameCore.Actors.ReceivingTaskDriver/<>c__DisplayClass22_0:<OnArrivedAtStandPosition>b__0 () (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:226)
  AiNavigation:Update () (at Assets/_Project/Scripts/Actors/AiNavigation.cs:966)
  ```
- **NO-TASK** (day 3): NO-TASK lane pallet 1C-1 sku=903561 load=8896818536  <<HISTORY: d3 20:12 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d3 20:34 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 20:37 Putaway task created (STG1C-1->) by OrderService.RefileStrandedLanePallets | d3 20:49 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d3 20:49 PalletPick task created (STG1C-1->1B) by OrderService.RefilePalletPickShortfalls | d3 22:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **OVERLAP** (day 4): OVERLAP Pallet_04_Slot4_Tier0 & Pallet_00_Slot0_Tier0 @1B-1  <<HISTORY: d3 13:11 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 13:14 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 13:23 moved (38, 66)->(-1, -1) by InventoryService.MovePallet | d3 13:38 adopted as outbound freight of order f7155818-aba1-4d6a-b874-2ec07a097743 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 13:38 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 13:38 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d3 13:11 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 13:11 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 13:22 moved (38, 66)->(-1, -1) by InventoryService.MovePallet | d3 13:34 adopted as outbound freight of order f7155818-aba1-4d6a-b874-2ec07a097743 (16 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 13:34 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 13:34 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 4): FLOATING Pallet_00_Slot0_Tier0 @1B-2 bottom=2.43  <<HISTORY: d3 13:50 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 14:14 moved (49, 69)->(-1, -1) by InventoryService.MovePallet | d3 14:16 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 14:21 adopted as outbound freight of order f7155818-aba1-4d6a-b874-2ec07a097743 (24 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 14:21 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 14:21 record destroyed by InventoryService.DestroyPallet>>
- **WARN** (day 4): [OrderService] Order OG3720 closed with a STAGED pallet (24 cs) that never made the trailer - not billed, returning it to stock.
- **WARN** (day 4): [AiNavigation] Dockstocker: stuck at (-2.33, 1.11, 38.33) — walking out to (-8.81, 1.16, 26.43) (12.55m to mesh).
- **WARN** (day 4): [AiNavigation] ReachTruck: stuck at (15.50, 1.11, 33.60) — walking out to (15.50, 0.08, 42.67) (8.13m to mesh).
- **ERROR** (day 4): [MHEBoundary] 'ReachTruck' fell below the floor at (15.50, 0.38, 42.19) (cell (61, 81)). Putting it back on its last inside spot (15.50, 0.71, 41.64). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **ERROR** (day 4): [MHEBoundary] 'ReachTruck' keeps driving out of the building (6 times in 10s, at (15.50, 0.00, 42.10)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  
  ```
- **DOOR-DEADLOCK** (day 5): OG3715 is Staged but door 1's truck has waited 90 min with no one loading it.
- **WARN** (day 5): [AiNavigation] Employee_Joshua Garcia: stuck at (-2.08, 1.12, 38.67) — walking out to (-8.56, 1.14, 26.68) (12.62m to mesh).
- **FLOATING** (day 6): FLOATING OutboundPallet_b40924d7-a237-4d93-a380-f5717a3e2ccc_2 @1A-3 bottom=2.47
- **OVERLAP** (day 6): OVERLAP OutboundPallet_b40924d7-a237-4d93-a380-f5717a3e2ccc_2 & OutboundPallet_b40924d7-a237-4d93-a380-f5717a3e2ccc_1 @1A-3
- **REPEATING** (day 7): x25 and counting: [DockSchedule] Caffeine Fiend Coffee's 01:30 slot on day 7 elapsed with nothing loaded — door freed for rebooking, kept on the grid struck through as missed.
- **REPEATING** (day 7): x25 and counting: [EquipmentBattery] Dockstocker: not sliding 32.9 m by hand - that is a navigation job. Trying the path again.
- **TRUCK-OVERLAP** (day 7): TRUCK-OVERLAP Truck→Outbound_Door1 & Truck→Outbound_Door2 at (-7.6, 0.0, -52.8)

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1119 Anchor Point Foods: 7 plt $3,895
- d1 placed parked Inbound Anchor Point Foods day 1 block 18 door 1
- d1 PO IG1120 Ambrose Fine Foods: 4 plt $12,816
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 19 door 2
- d1 PO IG1121 Bulk Basin Foods: 4 plt $2,153
- d1 placed parked Inbound Bulk Basin Foods day 1 block 20 door 3
- d1 signed bulk BULK_The Big Cheese Inc._D1_0
- d1 PO IG1122 Backdoor Bill's Overstock: 10 plt $4,036
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 22 door 1
- d1 PO IG1123 Coastal Cargo Exchange: 10 plt $16,128
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 24 door 2
- d1 PO IG1124 Homestead Market Co.: 5 plt $6,336
- d1 placed parked Inbound Homestead Market Co. day 1 block 25 door 3
- d1 booked The Big Cheese Inc. day 2 block 0 door 1 (pallet-only, 16 pallet pick(s) / 2 reach truck(s): stock ready hour 19 + 4.3h of work)
- d1 released 1 order(s)
- d1 released 1 order(s)
- d2 PO IG2125 Backdoor Bill's Overstock: 1 plt $320
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 2 door 2
- d2 signed bulk BULK_Salty Sailor Pretzel Co._D2_0
- d2 PO IG2126 Coastal Cargo Exchange: 10 plt $13,800
- d2 placed parked Inbound Coastal Cargo Exchange day 2 block 34 door 2
- d2 PO IG2127 Backdoor Bill's Overstock: 1 plt $960
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 35 door 3
- d2 PO IG2128 Bulk Basin Foods: 2 plt $768
- d2 placed parked Inbound Bulk Basin Foods day 2 block 36 door 3
- d2 PO IG2129 Homestead Market Co.: 2 plt $1,152
- d2 placed parked Inbound Homestead Market Co. day 2 block 38 door 3
- d2 PO IG2130 Anchor Point Foods: 2 plt $1,408
- d2 placed parked Inbound Anchor Point Foods day 2 block 40 door 2
- d2 PO IG2131 Backdoor Bill's Overstock: 1 plt $960
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 41 door 1
- d3 PO IG3132 Fairweather Trading Co.: 13 plt $6,234
- d3 PO IG3133 Bulk Basin Foods: 1 plt $160
- d3 PO IG3134 Backdoor Bill's Overstock: 1 plt $256
- d3 placed parked Inbound Fairweather Trading Co. day 3 block 2 door 1
- d3 placed parked Inbound Bulk Basin Foods day 3 block 2 door 2
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 2 door 3
- d3 booked Salty Sailor Pretzel Co. day 3 block 30 door 2 (case-pick, 1 picklist(s) / 3 selector(s), 14 pallet pick(s) / 2 reach truck(s): stock ready hour 56 + 6.6h of work)
- d3 PO IG3135 Homestead Market Co.: 2 plt $1,424
- d3 placed parked Inbound Homestead Market Co. day 3 block 6 door 2
- d3 released 2 order(s)
- d3 signed bulk BULK_Mama Tomato Sauces_D3_0
- d3 signed bulk BULK_Cluck & Cover Poultry_D3_1
- d3 PO IG3136 Fairweather Trading Co.: 10 plt $5,760
- d3 placed parked Inbound Fairweather Trading Co. day 3 block 24 door 1
- d3 PO IG3137 Backdoor Bill's Overstock: 4 plt $1,024
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 25 door 2
- d3 booked Cluck & Cover Poultry day 4 block 4 door 1 (pallet-only, 14 pallet pick(s) / 2 reach truck(s): stock ready hour 66 + 7.6h of work)
- d3 released 1 order(s)
- d3 PO IG3138 Homestead Market Co.: 7 plt $4,368
- d3 placed parked Inbound Homestead Market Co. day 3 block 30 door 1
- d3 PO IG3139 Backdoor Bill's Overstock: 6 plt $7,800
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 31 door 3
- d3 booked Mama Tomato Sauces day 4 block 4 door 2 (pallet-only, 13 pallet pick(s) / 2 reach truck(s): stock ready hour 68 + 5.9h of work)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d4 PO IG4140 Bulk Basin Foods: 7 plt $2,377
- d4 PO IG4141 Anchor Point Foods: 2 plt $896
- d4 PO IG4142 Homestead Market Co.: 1 plt $624
- d4 placed parked Inbound Bulk Basin Foods day 4 block 2 door 3
- d4 placed parked Inbound Anchor Point Foods day 4 block 2 door 1
- d4 placed parked Inbound Homestead Market Co. day 4 block 2 door 2
- d4 PO IG4143 Backdoor Bill's Overstock: 2 plt $1,456
- d4 PO IG4144 Homestead Market Co.: 1 plt $1,748
- d4 placed parked Inbound Backdoor Bill's Overstock day 4 block 31 door 1
- d4 placed parked Inbound Homestead Market Co. day 4 block 31 door 2
- d4 signed bulk BULK_Cluck & Cover Poultry_D4_0
- d4 signed bulk BULK_Bramble & Bite Berry Co._D4_1
- d4 PO IG4145 Ambrose Fine Foods: 6 plt $8,688
- d4 placed parked Inbound Ambrose Fine Foods day 4 block 34 door 2
- d4 PO IG4146 Backdoor Bill's Overstock: 2 plt $1,680
- d4 placed parked Inbound Backdoor Bill's Overstock day 4 block 36 door 3
- d4 booked Cluck & Cover Poultry day 5 block 0 door 1 (pallet-only, 2 pallet pick(s) / 2 reach truck(s): stock ready hour 94 + 1.5h of work)
- d4 PO IG4147 Homestead Market Co.: 1 plt $432
- d4 placed parked Inbound Homestead Market Co. day 4 block 37 door 2
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 PO IG4148 Homestead Market Co.: 3 plt $1,440
- d4 placed parked Inbound Homestead Market Co. day 4 block 42 door 1
- d4 PO IG4149 Anchor Point Foods: 1 plt $720
- d4 placed parked Inbound Anchor Point Foods day 4 block 43 door 2
- d4 booked Bramble & Bite Berry Co. day 5 block 32 door 2 (pallet-only, 6 pallet pick(s) / 2 reach truck(s): stock ready hour 97 + 14.7h of work)
- d4 released 1 order(s)
- d4 PO IG4150 Bulk Basin Foods: 1 plt $480
- d4 placed parked Inbound Bulk Basin Foods day 4 block 44 door 1
- d5 PO IG5151 Homestead Market Co.: 2 plt $906
- d5 PO IG5152 Backdoor Bill's Overstock: 1 plt $840
- d5 PO IG5153 Anchor Point Foods: 1 plt $440
- d5 placed parked Inbound Homestead Market Co. day 5 block 2 door 1
- d5 placed parked Inbound Backdoor Bill's Overstock day 5 block 2 door 2
- d5 placed parked Inbound Anchor Point Foods day 5 block 2 door 3
- d5 booked Cluck & Cover Poultry day 5 block 21 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 signed bulk BULK_The Cracker Barrel Caravan_D5_0
- d5 signed bulk BULK_Caffeine Fiend Coffee_D5_1
- d5 PO IG5154 Golden Grove Gourmet: 8 plt $16,885
- d5 placed parked Inbound Golden Grove Gourmet day 5 block 38 door 2
- d5 PO IG5155 Homestead Market Co.: 1 plt $378
- d5 placed parked Inbound Homestead Market Co. day 5 block 39 door 3
- d5 booked Caffeine Fiend Coffee day 6 block 2 door 1 (pallet-only, 2 pallet pick(s) / 2 reach truck(s): stock ready hour 119 + 1.5h of work)
- d5 released 1 order(s)
- d6 released 1 order(s)
- d6 booked Caffeine Fiend Coffee day 6 block 5 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 7 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 9 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 11 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 13 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 released 1 order(s)
- d6 booked Caffeine Fiend Coffee day 6 block 15 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 17 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 17 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 19 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 21 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 23 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 23 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 25 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 27 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 29 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 29 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 31 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 33 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 35 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 35 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 37 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 39 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 41 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 43 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 45 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Caffeine Fiend Coffee day 6 block 47 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 1 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 3 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 5 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 7 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 9 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 11 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 13 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 15 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 17 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 19 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 21 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 23 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 25 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 released 1 order(s)
- d7 booked Caffeine Fiend Coffee day 7 block 27 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 29 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 31 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 33 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 35 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 37 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 39 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 41 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 43 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Caffeine Fiend Coffee day 7 block 45 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 released 1 order(s)
- d7 booked Caffeine Fiend Coffee day 7 block 47 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
