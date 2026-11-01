# Playtest block 26 — days 1–8

## Score: **55.9 (+12.8)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (6 shipped, 4 late) | 15% | 50.0 (+22.7) |
| Fill rate | 10% | 77.0 (-15.3) |
| Throughput (85 plt vs target 84) | 15% | 100.0 (+35.7) |
| Revenue ($38,329) | 15% | 68.4 (-9.2) |
| Profit (cash + stock -22,805) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (+45.1) |
| Reliability (bugs) | 25% | 21.8 (+8.8) |
| Reputation (0 to 85) | 5% | 100.0 (+50.0) |

Orders shipped **6**, late/fined **4**, pallets shipped **85**. Cash $97,855 → $31,485 (-66,370). Stock at cost $0 → $43,565; cash + stock -22,805.
Bot did: 4 contracts signed, 45 dock bookings, 7 orders released, 42 POs, 0 rush POs.

## Late orders - state when it came due
- 2 x still being picked
- 1 x loaded but the truck had not left
- 1 x never released

- day 2: OG2779 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,086. Lines 14, 319 cs, released=True.
- day 5: OG4784 (Neon Nectar Juice Co.) was never released to the floor. Fined $4,366. Lines 3, 648 cs, released=False.
- day 6: OG5785 (Salty Sailor Pretzel Co.) was still being picked (381/447 cs). Fined $1,686. Lines 3, 447 cs, released=True.
- day 6: OG6783 (Sneaky Pete's Seafood) was still being picked (232/383 cs). Fined $1,528. Lines 18, 383 cs, released=True.

## Change since last block
- Fixed / no longer seen: **16**
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it.
  - OVERLAP| OutboundPallet_#-aa##a#ed#ecbf#_# & OutboundPallet_#-aa##a#ed#ecbf#_# @#C#
  - WARN|[AiNavigation] Employee_Barbara Johnson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#E#
  - OVERLAP| OutboundPallet_#-e###-#_# & OutboundPallet_#-e###-#_# @#D#
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A#
  - FLOATING| Pallet_#_Slot#_Tier# @#A# bottom=#
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C#
  - FLOATING| Pallet_#_Slot#_Tier# @#C# bottom=#
  - FLOATING| OutboundPallet_#-aa##a#ed#ecbf#_# @#C# bottom=#
  - REPEATING|x# and counting: [EquipmentBattery] Dockstocker: not sliding # m by hand - that is a navigation job. Trying the path again.
  - REPEATING|x# and counting: [TrailerLoad] #B: door end blocked (the pallet at pos# belongs to order ##c#efb#a##, not one being loaded here); the far end gives # - stopping with # (the pallet at pos# belongs to order ##c#efb#a##, not one being loaded here).
  - REPEATING|x# and counting: [OrderService] Order ##a##d#-a#-# (Sneaky Pete's Seafood) was released to loading from #B but no staged pallet for it could be found there — reverted to Staged so it can be re-released. Its pallets are most likely sitting somewhere other than the lane.
  - REPEATING|x# and counting: [TrailerLoad] # order(s) at #B had no staged pallets to load — reverted to Staged.
- New this block: **15**
  - ERROR|[ReceiverReceivingWorkflow] No master record for pallet #-c##-a###
  - ERROR|[ReceiverReceivingWorkflow] No master record for pallet #-bfc##d##ba###
  - ERROR|[MHEBoundary] 'PalletJack' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|[MHEBoundary] 'PalletJack' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside.
  - ERROR|[MHEBoundary] 'ReachTruck' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - WARN|[AiNavigation] Employee_Madison Ford: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - TRUCK-OVERLAP| Truck→PO_IG# & Truck→PO_IG# at (#, #, #)
  - NO-TASK| lane pallet #A# sku=# load=#
  - ORPHAN-OUTBOUND| #A# (order ##f#### gone)
  - WARN|[AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #B and it never freed up — giving up and re-queueing.
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jean Stewart' as its operator though nobody is on it or running it - clearing the stale link.
  - WARN|[OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock.

## Bugs found (18 distinct, 21 incidents)

| Incidents | Bug |
|---|---|
| 2 | ERROR — [MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | ERROR — [ReceiverReceivingWorkflow] No master record for pallet #-c##-a### |
| 1 | ERROR — [ReceiverReceivingWorkflow] No master record for pallet #-bfc##d##ba### |
| 1 | ERROR — [MHEBoundary] 'PalletJack' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | ERROR — [MHEBoundary] 'PalletJack' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside. |
| 1 | ERROR — [MHEBoundary] 'ReachTruck' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 2 | WARN — [AiNavigation] Employee_Madison Ford: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 2 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | NO-TASK —  lane pallet #A# sku=# load=# |
| 1 | NO-TASK —  lane pallet #B# sku=# load=# |
| 1 | ORPHAN-OUTBOUND —  #A# (order ##f#### gone) |
| 1 | WARN — [AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReachTruckOperator] 'ReachTruck' waited #s for lane #B and it never freed up — giving up and re-queueing. |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jean Stewart' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Madison Ford: stuck at (6.34, 1.15, 23.96) — walking out to (6.34, 0.06, 10.96) (12.05m to mesh).
- **ERROR** (day 2): [ReceiverReceivingWorkflow] No master record for pallet b711160f-c506-4321-a171-7df43ef84daf
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.ReceiverReceivingWorkflow:BeginReceivingAt (GameCore.Labor.WorkTask,UnityEngine.Transform,single) (at Assets/_Project/Scripts/1. FSM/Core/Labor/ReceiverReceivingWorkflow.cs:247)
  GameCore.Actors.ReceivingTaskDriver:OnArrivedAtStandPosition (GameCore.Labor.WorkTask,UnityEngine.Transform,int) (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:213)
  GameCore.Actors.ReceivingTaskDriver/<>c__DisplayClass22_0:<OnArrivedAtStandPosition>b__0 () (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:226)
  AiNavigation:Update () (at Assets/_Project/Scripts/Actors/AiNavigation.cs:966)
  ```
- **ERROR** (day 2): [ReceiverReceivingWorkflow] No master record for pallet 170c4650-bfc7-42d8-8ba4-50d017405f4c
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.ReceiverReceivingWorkflow:BeginReceivingAt (GameCore.Labor.WorkTask,UnityEngine.Transform,single) (at Assets/_Project/Scripts/1. FSM/Core/Labor/ReceiverReceivingWorkflow.cs:247)
  GameCore.Actors.ReceivingTaskDriver:OnArrivedAtStandPosition (GameCore.Labor.WorkTask,UnityEngine.Transform,int) (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:213)
  GameCore.Actors.ReceivingTaskDriver/<>c__DisplayClass22_0:<OnArrivedAtStandPosition>b__0 () (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:226)
  AiNavigation:Update () (at Assets/_Project/Scripts/Actors/AiNavigation.cs:966)
  ```
- **TRUCK-OVERLAP** (day 3): TRUCK-OVERLAP Truck→PO_IG3277 & Truck→PO_IG3275 at (4.2, 0.0, -3.1)
- **WARN** (day 5): [AiNavigation] ReachTruck: stuck at (-6.31, 1.11, 24.26) — walking out to (-6.31, 0.08, 11.26) (12.04m to mesh).
- **ERROR** (day 5): [MHEBoundary] 'ReachTruck' fell below the floor at (-6.31, 0.45, 12.43) (cell (45, 59)). Putting it back on its last inside spot (-6.31, 0.65, 13.05). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **NO-TASK** (day 5): NO-TASK lane pallet 2A-1 sku=750426 load=6379372175  <<HISTORY: d5 15:46 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 16:12 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 16:12 PalletPick task created (STG2A-1->) by OrderService.FileReleasedOrderTasks | d5 16:12 PalletPick task Available->Open by OrderService.FileReleasedOrderTasks | d5 16:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d5 18:40 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **NO-TASK** (day 5): NO-TASK lane pallet 2B-4 sku=148972 load=1834940554  <<HISTORY: d5 16:40 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 16:50 Putaway task created (STG2B-4->) by ReceiverReceivingWorkflow.CreatePutawayTask | d5 16:50 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 16:50 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 16:50 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d5 18:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **WARN** (day 5): [AiNavigation] PalletJack: stuck at (19.44, 1.11, 37.44) — walking out to (25.44, 0.08, 37.44) (5.11m to mesh).
- **ERROR** (day 5): [MHEBoundary] 'PalletJack' fell below the floor at (24.85, 0.37, 37.44) (cell (68, 78)). Putting it back on its last inside spot (24.17, 0.71, 37.44). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **ERROR** (day 5): [MHEBoundary] 'PalletJack' keeps driving out of the building (6 times in 10s, at (25.39, 0.00, 37.44)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  
  ```
- **WARN** (day 5): [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2B and it never freed up — giving up and re-queueing.
- **ERROR** (day 5): [MHEBoundary] 'ReachTruck' keeps driving out of the building (6 times in 10s, at (6.56, 0.00, 41.67)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  
  ```
- **ERROR** (day 5): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-03' (13.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__92:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1140)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 5): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jean Stewart' as its operator though nobody is on it or running it - clearing the stale link.
- **OVERLAP** (day 5): OVERLAP Pallet_14_Slot9_Tier1 & Pallet_00_Slot0_Tier0 @(36, 71)  <<HISTORY: d5 19:44 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 21:44 Putaway task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d5 22:07 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d5 22:15 moved (53, 65)->(-1, -1) by InventoryService.MovePallet | d5 22:29 moved (-1, -1)->(36, 71) by InventoryService.MovePallet | d5 22:29 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d5 21:34 picked 1 (left 4) by InventoryService.PickFromPallet | d5 21:35 picked 1 (left 3) by InventoryService.PickFromPallet | d5 21:36 picked 1 (left 2) by InventoryService.PickFromPallet | d5 21:36 picked 1 (left 1) by InventoryService.PickFromPallet | d5 21:37 picked 1 (left 0) by InventoryService.PickFromPallet | d5 21:37 record destroyed by InventoryService.DestroyPallet>>
- **WARN** (day 7): [OrderService] Order OG5785 closed with a STAGED pallet (15 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 7): ORPHAN-OUTBOUND 1A-5 (order ceb8dcae-473f-4058-9618-0f88072cb0a6 gone)

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1259 Bulk Basin Foods: 6 plt $2,752
- d1 placed parked Inbound Bulk Basin Foods day 1 block 18 door 1
- d1 PO IG1260 Bulk Basin Foods: 4 plt $2,380
- d1 placed parked Inbound Bulk Basin Foods day 1 block 19 door 2
- d1 PO IG1261 Homestead Market Co.: 4 plt $5,392
- d1 placed parked Inbound Homestead Market Co. day 1 block 20 door 3
- d1 PO IG1262 Homestead Market Co.: 2 plt $1,216
- d1 placed parked Inbound Homestead Market Co. day 1 block 22 door 1
- d1 PO IG1263 Backdoor Bill's Overstock: 2 plt $2,520
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 24 door 1
- d1 PO IG1264 Anchor Point Foods: 3 plt $2,052
- d1 placed parked Inbound Anchor Point Foods day 1 block 25 door 1
- d1 PO IG1265 Bulk Basin Foods: 1 plt $128
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 2
- d1 released 1 order(s)
- d2 released 1 order(s)
- d2 PO IG2266 Anchor Point Foods: 1 plt $448
- d2 placed parked Inbound Anchor Point Foods day 2 block 28 door 1
- d2 PO IG2267 Fairweather Trading Co.: 9 plt $6,592
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 36 door 2
- d2 PO IG2268 Homestead Market Co.: 2 plt $2,624
- d2 placed parked Inbound Homestead Market Co. day 2 block 37 door 3
- d2 PO IG2269 Bulk Basin Foods: 4 plt $1,086
- d2 placed parked Inbound Bulk Basin Foods day 2 block 38 door 3
- d2 PO IG2270 Backdoor Bill's Overstock: 1 plt $1,300
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 40 door 1
- d2 PO IG2271 Bulk Basin Foods: 1 plt $128
- d2 placed parked Inbound Bulk Basin Foods day 2 block 42 door 1
- d2 PO IG2272 Blue Ridge Provisions: 2 plt $2,400
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 43 door 1
- d3 PO IG3273 Homestead Market Co.: 1 plt $544
- d3 placed parked Inbound Homestead Market Co. day 3 block 2 door 1
- d3 PO IG3274 Homestead Market Co.: 2 plt $966
- d3 placed parked Inbound Homestead Market Co. day 3 block 36 door 2
- d3 PO IG3275 Bulk Basin Foods: 4 plt $760
- d3 placed parked Inbound Bulk Basin Foods day 3 block 37 door 2
- d3 released 1 order(s)
- d3 PO IG3276 Backdoor Bill's Overstock: 1 plt $1,080
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 38 door 3
- d3 PO IG3277 Bulk Basin Foods: 1 plt $120
- d3 placed parked Inbound Bulk Basin Foods day 3 block 40 door 1
- d3 PO IG3278 Homestead Market Co.: 1 plt $480
- d3 placed parked Inbound Homestead Market Co. day 3 block 42 door 1
- d3 PO IG3279 Anchor Point Foods: 1 plt $816
- d3 placed parked Inbound Anchor Point Foods day 3 block 43 door 1
- d4 PO IG4280 Homestead Market Co.: 2 plt $1,259
- d4 placed parked Inbound Homestead Market Co. day 4 block 36 door 2
- d4 PO IG4281 Bulk Basin Foods: 5 plt $2,717
- d4 placed parked Inbound Bulk Basin Foods day 4 block 37 door 2
- d4 PO IG4282 Blue Ridge Provisions: 5 plt $2,872
- d4 placed parked Inbound Blue Ridge Provisions day 4 block 38 door 3
- d4 PO IG4283 Anchor Point Foods: 1 plt $420
- d4 placed parked Inbound Anchor Point Foods day 4 block 40 door 2
- d4 PO IG4284 Bulk Basin Foods: 2 plt $512
- d4 signed bulk BULK_Neon Nectar Juice Co._D4_0
- d4 placed parked Inbound Bulk Basin Foods day 4 block 42 door 1
- d4 PO IG4285 Blue Ridge Provisions: 8 plt $9,000
- d4 placed parked Inbound Blue Ridge Provisions day 4 block 43 door 1
- d4 PO IG4286 Homestead Market Co.: 7 plt $3,136
- d4 placed parked Inbound Homestead Market Co. day 4 block 44 door 2
- d5 PO IG5287 Homestead Market Co.: 1 plt $448
- d5 placed parked Inbound Homestead Market Co. day 5 block 2 door 1
- d5 released 1 order(s)
- d5 signed bulk BULK_Salty Sailor Pretzel Co._D5_0
- d5 PO IG5288 Bulk Basin Foods: 17 plt $4,880
- d5 placed parked Inbound Bulk Basin Foods day 5 block 24 door 1
- d5 booked Salty Sailor Pretzel Co. day 6 block 6 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 17 pallet pick(s) / 2 reach truck(s): stock ready hour 116 + 6.2h of work)
- d5 PO IG5289 Bulk Basin Foods: 2 plt $736
- d5 placed parked Inbound Bulk Basin Foods day 5 block 25 door 2
- d5 released 1 order(s)
- d5 released 1 order(s)
- d6 PO IG6290 Bulk Basin Foods: 3 plt $1,376
- d6 placed parked Inbound Bulk Basin Foods day 6 block 2 door 1
- d6 PO IG6291 Backdoor Bill's Overstock: 2 plt $480
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 3 door 2
- d6 PO IG6292 Homestead Market Co.: 1 plt $480
- d6 placed parked Inbound Homestead Market Co. day 6 block 4 door 1
- d6 PO IG6293 Anchor Point Foods: 1 plt $392
- d6 placed parked Inbound Anchor Point Foods day 6 block 6 door 2
- d6 PO IG6294 Bulk Basin Foods: 2 plt $736
- d6 placed parked Inbound Bulk Basin Foods day 6 block 8 door 2
- d6 PO IG6295 Homestead Market Co.: 1 plt $720
- d6 placed parked Inbound Homestead Market Co. day 6 block 10 door 2
- d6 PO IG6296 Backdoor Bill's Overstock: 1 plt $1,080
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 11 door 2
- d6 released 1 order(s)
- d6 booked Sneaky Pete's Seafood day 7 block 0 door 1 (ship-short: stock ready hour 142 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 7 block 2 door 2 (ship-short: stock ready hour 143 + 1.5h of work)
- d7 PO IG7297 Backdoor Bill's Overstock: 2 plt $448
- d7 PO IG7298 Bulk Basin Foods: 3 plt $1,571
- d7 placed parked Inbound Backdoor Bill's Overstock day 7 block 2 door 1
- d7 placed parked Inbound Bulk Basin Foods day 7 block 2 door 3
- d7 PO IG7299 Bulk Basin Foods: 8 plt $2,411
- d7 placed parked Inbound Bulk Basin Foods day 7 block 36 door 2
- d7 signed bulk BULK_Blocky Bakeshop_D7_0
- d7 PO IG7300 Backdoor Bill's Overstock: 1 plt $208
- d7 placed parked Inbound Backdoor Bill's Overstock day 7 block 41 door 1
