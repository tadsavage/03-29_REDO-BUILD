# Playtest block 20 — days 1–8

## Score: **33.4 (+22.3)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 9 late) | 15% | 10.0 (+10.0) |
| Fill rate | 10% | 100.0 (+100.0) |
| Throughput (20 plt vs target 84) | 15% | 23.8 (+23.8) |
| Revenue ($33,851) | 15% | 60.4 (+60.4) |
| Profit (cash + stock -92,351) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 37.4 (-41.2) |
| Reliability (bugs) | 25% | 13.0 (-5.8) |
| Reputation (0 to 20) | 5% | 83.3 (+33.3) |

Orders shipped **2**, late/fined **9**, pallets shipped **20**. Cash $97,856 → $-7,999 (-105,855). Stock at cost $0 → $13,504; cash + stock -92,351.
Bot did: 5 contracts signed, 123 dock bookings, 10 orders released, 18 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **8**
  - ERROR|[MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|[MHEBoundary] 'Dockstocker' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|Assertion failed
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - WARN|[AiNavigation] Employee_Steven Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_John Williams' as its operator though nobody is on it or running it - clearing the stale link.
- New this block: **18**
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C#
  - TRUCK-OVERLAP| Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #)
  - OVERLAP| OutboundPallet_##d##e####_# & OutboundPallet_##d##e####_# @#D#
  - WARN|[AiNavigation] Employee_Barbara Bennett: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - NO-TASK| lane pallet #B# sku=# load=#
  - FLOATING| Pallet_#_Slot#_Tier# @#A# bottom=#
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#D#
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_##d##e####_# @#D#
  - FLOATING| OutboundPallet_##d##e####_# @#D# bottom=#
  - FLOATING| Pallet_#_Slot#_Tier# @#D# bottom=#
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_###-b#fd##_# @#B#
  - FLOATING| Pallet_#_Slot#_Tier# @#C# bottom=#
  - OVERLAP| OutboundPallet_###-b#fd##_# & OutboundPallet_###-b#fd##_# @#A#
  - FLOATING| OutboundPallet_###-b#fd##_# @#A# bottom=#
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Barbara Bennett: receive workflow cancelled (out of range) — releasing the task and resuming patrol.

## Bugs found (22 distinct, 36 incidents)

| Incidents | Bug |
|---|---|
| 4 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 5 | TRUCK-OVERLAP —  Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #) |
| 3 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 2 | OVERLAP —  OutboundPallet_##d##e####_# & OutboundPallet_##d##e####_# @#D# |
| 4 | WARN — [AiNavigation] Employee_Barbara Bennett: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | NO-TASK —  lane pallet #B# sku=# load=# |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#D# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##d##e####_# @#D# |
| 1 | FLOATING —  OutboundPallet_##d##e####_# @#D# bottom=# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#D# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_###-b#fd##_# @#B# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 1 | OVERLAP —  OutboundPallet_###-b#fd##_# & OutboundPallet_###-b#fd##_# @#A# |
| 1 | FLOATING —  OutboundPallet_###-b#fd##_# @#A# bottom=# |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Barbara Bennett: receive workflow cancelled (out of range) — releasing the task and resuming patrol. |
| 1 | REPEATING — x# and counting: [DockSchedule] The Happy Herd Co.'s #:# slot on day # elapsed with nothing loaded — door freed for rebooking, kept on the grid struck through as missed. |
| 1 | REPEATING — x# and counting: [DockSchedule] Sneaky Pete's Seafood's #:# slot on day # elapsed with nothing loaded — door freed for rebooking, kept on the grid struck through as missed. |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Amanda Davis' as its operator though nobody is on it or running it - clearing the stale link. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Barbara Bennett: stuck at (-1.44, 1.16, 23.99) — walking out to (-12.68, 1.13, 31.18) (12.34m to mesh).
- **NO-TASK** (day 2): NO-TASK lane pallet 2B-5 sku=421698 load=3065519478  <<HISTORY: d2 22:04 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 22:18 Putaway task created (STG2B-5->) by ReceiverReceivingWorkflow.CreatePutawayTask | d2 22:18 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 22:18 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 22:18 PalletPick task Open->Available by OrderService.TryAssignInboundPalletToPendingPick | d2 22:18 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask>>
- **REPEATING** (day 3): x25 and counting: [ReceivingTaskDriver] Employee_Barbara Bennett: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
- **OVERLAP** (day 3): OVERLAP Pallet_03_Slot2_Tier1 & Pallet_05_Slot3_Tier1 @1A-1  <<HISTORY: d2 22:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d2 23:40 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 23:52 moved (53, 66)->(-1, -1) by InventoryService.MovePallet | d3 00:02 adopted as outbound freight of order 5e00d032-eacd-43d4-8fbb-63630a7b704d (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 00:02 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 00:02 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d2 22:43 Putaway task created (STG2B-5->) by OrderService.RefileStrandedLanePallets | d3 01:21 moved (54, 69)->(-1, -1) by InventoryService.MovePallet | d3 01:30 adopted as outbound freight of order 5e00d032-eacd-43d4-8fbb-63630a7b704d (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:30 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:30 record destroyed by InventoryService.DestroyPallet | d3 01:30 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **FLOATING** (day 3): FLOATING Pallet_05_Slot3_Tier1 @1A-1 bottom=1.30  <<HISTORY: d2 22:43 Putaway task created (STG2B-5->) by OrderService.RefileStrandedLanePallets | d3 01:21 moved (54, 69)->(-1, -1) by InventoryService.MovePallet | d3 01:30 adopted as outbound freight of order 5e00d032-eacd-43d4-8fbb-63630a7b704d (55 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 01:30 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 01:30 record destroyed by InventoryService.DestroyPallet | d3 01:30 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **DOOR-DEADLOCK** (day 3): OG5694 is Staged but door 1's truck has waited 90 min with no one loading it.
- **WARN** (day 5): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Amanda Davis' as its operator though nobody is on it or running it - clearing the stale link.
- **REPEATING** (day 5): x25 and counting: [DockSchedule] The Happy Herd Co.'s 09:30 slot on day 5 elapsed with nothing loaded — door freed for rebooking, kept on the grid struck through as missed.
- **TRUCK-OVERLAP** (day 6): TRUCK-OVERLAP Truck→Outbound_Door2 & Truck→Outbound_Door3 at (-7.6, 0.0, -52.8)
- **OVERLAP** (day 6): OVERLAP Pallet_03_Slot1_Tier1 & Pallet_00_Slot0_Tier0 @1D-1  <<HISTORY: d3 10:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 11:01 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 11:12 moved (38, 71)->(-1, -1) by InventoryService.MovePallet | d3 11:26 adopted as outbound freight of order 4e4a991e-4d76-4e41-8304-19f53c6f63b3 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 11:26 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 11:26 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d3 10:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 10:47 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 10:53 moved (32, 67)->(-1, -1) by InventoryService.MovePallet | d3 11:07 adopted as outbound freight of order 4e4a991e-4d76-4e41-8304-19f53c6f63b3 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 11:07 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 11:07 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 6): OVERLAP Pallet_03_Slot2_Tier0 & OutboundPallet_4e4a991e-4d76-4e41-8304-19f53c6f63b3_1 @1D-2  <<HISTORY: d3 10:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 11:08 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 11:20 moved (40, 67)->(-1, -1) by InventoryService.MovePallet | d3 11:35 adopted as outbound freight of order 4e4a991e-4d76-4e41-8304-19f53c6f63b3 (24 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 11:35 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 11:35 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 6): OVERLAP OutboundPallet_4e4a991e-4d76-4e41-8304-19f53c6f63b3_1 & OutboundPallet_4e4a991e-4d76-4e41-8304-19f53c6f63b3_2 @1D-3
- **FLOATING** (day 6): FLOATING OutboundPallet_4e4a991e-4d76-4e41-8304-19f53c6f63b3_1 @1D-4 bottom=2.35
- **FLOATING** (day 6): FLOATING Pallet_03_Slot1_Tier1 @1D-1 bottom=1.30  <<HISTORY: d3 10:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 11:01 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 11:12 moved (38, 71)->(-1, -1) by InventoryService.MovePallet | d3 11:26 adopted as outbound freight of order 4e4a991e-4d76-4e41-8304-19f53c6f63b3 (40 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 11:26 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 11:26 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 6): OVERLAP Pallet_09_Slot5_Tier1 & OutboundPallet_1f21d25e-4564-4088-b9fd-4b71f53e719b_1 @1B-4  <<HISTORY: d3 07:50 moved (54, 67)->(-1, -1) by InventoryService.MovePallet | d3 07:53 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d3 07:58 adopted as outbound freight of order 1f21d25e-4564-4088-b9fd-4b71f53e719b (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 07:58 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 07:58 record destroyed by InventoryService.DestroyPallet | d3 07:58 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **OVERLAP** (day 6): OVERLAP Pallet_16_Slot9_Tier0 & Pallet_14_Slot8_Tier0 @1C-1  <<HISTORY: d3 01:42 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 03:33 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:47 moved (34, 71)->(-1, -1) by InventoryService.MovePallet | d3 04:04 adopted as outbound freight of order 1f21d25e-4564-4088-b9fd-4b71f53e719b (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 04:04 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 04:04 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d3 01:42 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 03:47 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:57 moved (34, 71)->(-1, -1) by InventoryService.MovePallet | d3 04:11 adopted as outbound freight of order 1f21d25e-4564-4088-b9fd-4b71f53e719b (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 04:11 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 04:11 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 6): OVERLAP Pallet_04_Slot2_Tier0 & Pallet_02_Slot1_Tier0 @1B-3  <<HISTORY: d3 01:42 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 02:59 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:14 moved (34, 67)->(-1, -1) by InventoryService.MovePallet | d3 03:32 adopted as outbound freight of order 1f21d25e-4564-4088-b9fd-4b71f53e719b (44 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:32 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:32 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d3 01:42 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 03:19 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:32 moved (34, 67)->(-1, -1) by InventoryService.MovePallet | d3 03:45 adopted as outbound freight of order 1f21d25e-4564-4088-b9fd-4b71f53e719b (44 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:45 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:45 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 6): FLOATING Pallet_02_Slot1_Tier0 @1B-3 bottom=2.16  <<HISTORY: d3 01:42 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 03:19 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 03:32 moved (34, 67)->(-1, -1) by InventoryService.MovePallet | d3 03:45 adopted as outbound freight of order 1f21d25e-4564-4088-b9fd-4b71f53e719b (44 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 03:45 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 03:45 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 6): FLOATING Pallet_13_Slot7_Tier1 @1C-3 bottom=2.28  <<HISTORY: d3 01:42 PalletPick task Open->Available by OrderService.AllocateHeldWork | d3 04:43 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 04:54 moved (34, 76)->(-1, -1) by InventoryService.MovePallet | d3 05:09 adopted as outbound freight of order 1f21d25e-4564-4088-b9fd-4b71f53e719b (45 cs) by OutboundPalletBuilder.AdoptFullPallet | d3 05:09 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 05:09 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 6): OVERLAP OutboundPallet_1f21d25e-4564-4088-b9fd-4b71f53e719b_2 & OutboundPallet_1f21d25e-4564-4088-b9fd-4b71f53e719b_1 @1A-3
- **REPEATING** (day 7): x25 and counting: [DockSchedule] Sneaky Pete's Seafood's 21:30 slot on day 6 elapsed with nothing loaded — door freed for rebooking, kept on the grid struck through as missed.
- **FLOATING** (day 7): FLOATING OutboundPallet_1f21d25e-4564-4088-b9fd-4b71f53e719b_2 @1A-3 bottom=1.30

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1075 Bulk Basin Foods: 7 plt $4,396
- d1 placed parked Inbound Bulk Basin Foods day 1 block 18 door 1
- d1 PO IG1076 Homestead Market Co.: 2 plt $1,344
- d1 placed parked Inbound Homestead Market Co. day 1 block 19 door 2
- d1 PO IG1077 Bulk Basin Foods: 6 plt $3,400
- d1 placed parked Inbound Bulk Basin Foods day 1 block 20 door 2
- d1 signed bulk BULK_Cluck & Cover Poultry_D1_0
- d1 signed bulk BULK_Barnacle Bill's Bait & Bite_D1_1
- d1 PO IG1078 Golden Grove Gourmet: 10 plt $20,225
- d1 placed parked Inbound Golden Grove Gourmet day 1 block 22 door 1
- d1 PO IG1079 Anchor Point Foods: 2 plt $1,344
- d1 placed parked Inbound Anchor Point Foods day 1 block 24 door 2
- d1 PO IG1080 Bulk Basin Foods: 5 plt $3,200
- d1 placed parked Inbound Bulk Basin Foods day 1 block 25 door 2
- d1 PO IG1081 Homestead Market Co.: 5 plt $9,600
- d1 placed parked Inbound Homestead Market Co. day 1 block 26 door 3
- d1 booked Barnacle Bill's Bait & Bite day 2 block 0 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 9 pallet pick(s) / 2 reach truck(s): stock ready hour 18 + 3.1h of work)
- d1 PO IG1082 Backdoor Bill's Overstock: 3 plt $5,100
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 28 door 1
- d1 released 1 order(s)
- d1 booked Cluck & Cover Poultry day 1 block 42 door 1 (pallet-only, 10 pallet pick(s) / 2 reach truck(s): stock ready hour 17 + 3.0h of work)
- d1 released 1 order(s)
- d2 PO IG2083 Backdoor Bill's Overstock: 2 plt $3,600
- d2 PO IG2084 Homestead Market Co.: 1 plt $1,760
- d2 PO IG2085 Bulk Basin Foods: 1 plt $576
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 2 door 2
- d2 placed parked Inbound Homestead Market Co. day 2 block 2 door 3
- d2 placed parked Inbound Bulk Basin Foods day 2 block 3 door 2
- d2 released 1 order(s)
- d2 signed bulk BULK_The Happy Herd Co._D2_0
- d2 signed bulk BULK_Sprout Scout Foods_D2_1
- d2 PO IG2086 Coastal Cargo Exchange: 11 plt $11,244
- d2 placed parked Inbound Coastal Cargo Exchange day 2 block 34 door 2
- d2 PO IG2087 Fairweather Trading Co.: 17 plt $18,764
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 35 door 3
- d2 booked The Happy Herd Co. day 3 block 10 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 3 pallet pick(s) / 2 reach truck(s): stock ready hour 49 + 3.1h of work)
- d2 PO IG2088 Fairweather Trading Co.: 8 plt $5,826
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 39 door 1
- d2 PO IG2089 Homestead Market Co.: 2 plt $1,062
- d2 placed parked Inbound Homestead Market Co. day 2 block 40 door 2
- d2 PO IG2090 Bulk Basin Foods: 5 plt $3,128
- d2 placed parked Inbound Bulk Basin Foods day 2 block 41 door 2
- d2 PO IG2091 Backdoor Bill's Overstock: 1 plt $900
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 43 door 1
- d2 released 1 order(s)
- d3 PO IG3092 Bulk Basin Foods: 1 plt $672
- d3 placed parked Inbound Bulk Basin Foods day 3 block 2 door 1
- d3 booked Sprout Scout Foods day 3 block 12 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 18 pallet pick(s) / 2 reach truck(s): stock ready hour 49 + 4.7h of work)
- d3 released 1 order(s)
- d3 booked The Happy Herd Co. day 3 block 20 door 2 (case-pick, 1 picklist(s) / 3 selector(s), 1 pallet pick(s) / 2 reach truck(s): stock ready hour 54 + 3.1h of work)
- d3 released 1 order(s)
- d3 booked The Happy Herd Co. day 3 block 23 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d3 booked Sprout Scout Foods day 3 block 32 door 2 (case-pick, 1 picklist(s) / 3 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 60 + 3.1h of work)
- d3 booked The Happy Herd Co. day 3 block 27 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d3 booked The Happy Herd Co. day 3 block 31 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d3 booked The Happy Herd Co. day 3 block 35 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d3 booked The Happy Herd Co. day 3 block 39 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d3 booked Sneaky Pete's Seafood day 4 block 0 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 68 + 3.1h of work)
- d3 booked Sneaky Pete's Seafood day 4 block 2 door 2 (case-pick, 1 picklist(s) / 3 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 69 + 3.1h of work)
- d3 booked Sprout Scout Foods day 4 block 0 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d3 booked The Happy Herd Co. day 3 block 43 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d3 booked Sneaky Pete's Seafood day 4 block 7 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 70 + 3.1h of work)
- d3 booked Sneaky Pete's Seafood day 4 block 10 door 2 (case-pick, 1 picklist(s) / 3 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 71 + 3.1h of work)
- d3 booked The Happy Herd Co. day 4 block 0 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked The Happy Herd Co. day 4 block 3 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked The Happy Herd Co. day 4 block 7 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked Sprout Scout Foods day 4 block 11 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked The Happy Herd Co. day 4 block 11 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked The Happy Herd Co. day 4 block 15 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 released 1 order(s)
- d4 booked Sneaky Pete's Seafood day 4 block 17 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked The Happy Herd Co. day 4 block 19 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked Sneaky Pete's Seafood day 4 block 23 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked Sprout Scout Foods day 4 block 23 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked The Happy Herd Co. day 4 block 23 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked The Happy Herd Co. day 4 block 27 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked Sneaky Pete's Seafood day 4 block 29 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked The Happy Herd Co. day 4 block 31 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked Sneaky Pete's Seafood day 4 block 35 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked Sprout Scout Foods day 4 block 35 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked The Happy Herd Co. day 4 block 40 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked Sneaky Pete's Seafood day 4 block 41 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked The Happy Herd Co. day 4 block 43 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked Sprout Scout Foods day 5 block 0 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked The Happy Herd Co. day 5 block 0 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked Sneaky Pete's Seafood day 5 block 1 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 3 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked Sneaky Pete's Seafood day 5 block 7 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 7 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked Sprout Scout Foods day 5 block 11 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 11 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked Sneaky Pete's Seafood day 5 block 13 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 15 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 released 1 order(s)
- d5 booked Sneaky Pete's Seafood day 5 block 19 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 19 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked Sprout Scout Foods day 5 block 23 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 23 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked Sneaky Pete's Seafood day 5 block 25 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 27 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked Sneaky Pete's Seafood day 5 block 31 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 31 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked Sprout Scout Foods day 5 block 35 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 36 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked Sneaky Pete's Seafood day 5 block 38 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 39 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d5 booked The Happy Herd Co. day 5 block 43 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 1 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sprout Scout Foods day 6 block 1 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked The Happy Herd Co. day 6 block 1 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked The Happy Herd Co. day 6 block 5 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 7 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked The Happy Herd Co. day 6 block 9 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 13 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sprout Scout Foods day 6 block 13 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked The Happy Herd Co. day 6 block 13 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 released 1 order(s)
- d6 booked The Happy Herd Co. day 6 block 17 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 19 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked The Happy Herd Co. day 6 block 21 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 25 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sprout Scout Foods day 6 block 25 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked The Happy Herd Co. day 6 block 25 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked The Happy Herd Co. day 6 block 29 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 31 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked The Happy Herd Co. day 6 block 35 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 37 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sprout Scout Foods day 6 block 37 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked The Happy Herd Co. day 6 block 41 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 6 block 43 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d6 booked The Happy Herd Co. day 6 block 45 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 7 block 1 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sprout Scout Foods day 7 block 1 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked The Happy Herd Co. day 7 block 1 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked The Happy Herd Co. day 7 block 5 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 7 block 7 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked The Happy Herd Co. day 7 block 9 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 7 block 13 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sprout Scout Foods day 7 block 13 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked The Happy Herd Co. day 7 block 13 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 released 1 order(s)
- d7 booked The Happy Herd Co. day 7 block 17 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 7 block 19 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked The Happy Herd Co. day 7 block 21 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 7 block 25 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sprout Scout Foods day 7 block 25 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked The Happy Herd Co. day 7 block 25 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked The Happy Herd Co. day 7 block 29 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 7 block 31 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked The Happy Herd Co. day 7 block 35 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 7 block 37 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sprout Scout Foods day 7 block 37 door 3 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked The Happy Herd Co. day 7 block 40 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked Sneaky Pete's Seafood day 7 block 43 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 booked The Happy Herd Co. day 7 block 43 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
