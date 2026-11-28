# Playtest block 19 — days 1–8

## Score: **11.1 (-35.6)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 11 late) | 15% | 0.0 (0.0) |
| Fill rate | 10% | 0.0 (-100.0) |
| Throughput (0 plt vs target 84) | 15% | 0.0 (-82.1) |
| Revenue ($0) | 15% | 0.0 (-100.0) |
| Profit (cash + stock -71,560) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 78.6 (-21.4) |
| Reliability (bugs) | 25% | 18.8 (+11.2) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **0**, late/fined **11**, pallets shipped **0**. Cash $97,857 → $1,441 (-96,416). Stock at cost $0 → $24,856; cash + stock -71,560.
Bot did: 6 contracts signed, 21 dock bookings, 7 orders released, 21 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **22**
  - FLOATING| Pallet_#_Slot#_Tier# @#A# bottom=#
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it.
  - ERROR|[MHEBoundary] 'Dockstocker' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_##c##b#-b#e-#_# @#A#
  - FLOATING| OutboundPallet_##c##b#-b#e-#_# @#A# bottom=#
  - FLOATING| OutboundPallet_#-b##d###e#bc#b_# @#B# bottom=#
  - ERROR|[MHEBoundary] 'ReachTruck' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - FLOATING| OutboundPallet_##d#-b###_# @#C# bottom=#
  - ERROR|[MHEBoundary] 'PalletJack' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|[MHEBoundary] 'Dockstocker' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside.
  - EXCEPTION|MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  - OVERLAP| OutboundPallet_###-b###_# & OutboundPallet_###-b###_# @#E#
  - NO-TASK| lane pallet #B# sku=# load=#
  - NO-TASK| lane pallet #C# sku=# load=#
  - REPEATING|x# and counting: [NoWaypointIndicator] Alert emote 'emote_exclamations' not found in Resources/Emotes.
- New this block: **10**
  - ERROR|[MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|[MHEBoundary] 'Dockstocker' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|Assertion failed
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B#
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A#
  - FLOATING| Pallet_#_Slot#_Tier# @#B# bottom=#
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - WARN|[AiNavigation] Employee_Steven Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_John Williams' as its operator though nobody is on it or running it - clearing the stale link.

## Bugs found (12 distinct, 24 incidents)

| Incidents | Bug |
|---|---|
| 6 | ERROR — [MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 4 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 5 | ERROR — [MHEBoundary] 'Dockstocker' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | ERROR — Assertion failed |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | WARN — [AiNavigation] Employee_Steven Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_John Williams' as its operator though nobody is on it or running it - clearing the stale link. |

## First sightings (with stack traces)

- **REPEATING** (day 2): x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (11.21, 1.26, 35.06) (no waypoint reachable for 8s) — warping to (11.21, 0.01, 39.04) to rejoin patrol.
- **ERROR** (day 2): [MHEBoundary] 'Dockstocker' fell below the floor at (3.26, 0.00, 31.09) (cell (52, 73)). Putting it back on its last inside spot (-26.11, 1.11, 21.27). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **ERROR** (day 2): [MHEBoundary] 'ReachTruck' fell below the floor at (3.26, 0.00, 31.09) (cell (52, 73)). Putting it back on its last inside spot (3.59, 1.11, 32.51). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **ERROR** (day 2): Assertion failed
  ```
  UnityEngine.UIElements.UIElementsRuntimeUtilityNative:RepaintPanels (bool)
  
  ```
- **REPEATING** (day 2): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (4.83, 1.11, 38.61) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **WARN** (day 2): [AiNavigation] Employee_Steven Peterson: stuck at (6.44, 1.16, 22.65) — walking out to (-0.06, 0.04, 11.39) (12.05m to mesh).
- **DOOR-DEADLOCK** (day 3): OG3678 is Staged but door 1's truck has waited 90 min with no one loading it.
- **OVERLAP** (day 4): OVERLAP Pallet_08_Slot4_Tier0 & Pallet_09_Slot4_Tier1 @2B-4  <<HISTORY: d4 04:55 registered at (54, 68) 36x189432 by InventoryService.RegisterPhysicalPallet | d4 04:55 Receive task created (->STG2B-4) by ReceivingService.CreateReceiveTaskForPallet | d4 04:55 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 05:05 Putaway task created (STG2B-4->) by ReceiverReceivingWorkflow.CreatePutawayTask | d4 05:05 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>  <<HISTORY: d4 04:40 registered at (54, 68) 36x189432 by InventoryService.RegisterPhysicalPallet | d4 04:40 Receive task created (->STG2B-4) by ReceivingService.CreateReceiveTaskForPallet | d4 04:40 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 04:51 Putaway task created (STG2B-4->) by ReceiverReceivingWorkflow.CreatePutawayTask | d4 04:51 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **OVERLAP** (day 4): OVERLAP Pallet_11_Slot5_Tier1 & Pallet_05_Slot2_Tier1 @2A-1  <<HISTORY: d4 05:15 registered at (53, 65) 36x189432 by InventoryService.RegisterPhysicalPallet | d4 05:15 Receive task created (->STG2A-1) by ReceivingService.CreateReceiveTaskForPallet | d4 05:15 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>  <<HISTORY: d4 03:36 registered at (53, 65) 36x189432 by InventoryService.RegisterPhysicalPallet | d4 03:36 Receive task created (->STG2A-1) by ReceivingService.CreateReceiveTaskForPallet | d4 03:36 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 03:47 Putaway task created (STG2A-1->) by ReceiverReceivingWorkflow.CreatePutawayTask | d4 03:47 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **FLOATING** (day 4): FLOATING Pallet_08_Slot4_Tier0 @2B-4 bottom=1.30  <<HISTORY: d4 04:55 registered at (54, 68) 36x189432 by InventoryService.RegisterPhysicalPallet | d4 04:55 Receive task created (->STG2B-4) by ReceivingService.CreateReceiveTaskForPallet | d4 04:55 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 05:05 Putaway task created (STG2B-4->) by ReceiverReceivingWorkflow.CreatePutawayTask | d4 05:05 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **WARN** (day 5): [AiNavigation] Dockstocker: stuck at (-2.33, 1.11, 38.33) — walking out to (-8.81, 1.16, 26.43) (12.55m to mesh).
- **WARN** (day 6): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_John Williams' as its operator though nobody is on it or running it - clearing the stale link.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1053 Bulk Basin Foods: 9 plt $2,584
- d1 PO IG1054 Blue Ridge Provisions: 5 plt $2,520
- d1 PO IG1055 Backdoor Bill's Overstock: 2 plt $576
- d1 placed parked Inbound Bulk Basin Foods day 1 block 18 door 1
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 18 door 2
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 18 door 3
- d2 PO IG2056 Homestead Market Co.: 1 plt $1,672
- d2 placed parked Inbound Homestead Market Co. day 2 block 2 door 1
- d2 PO IG2057 Bulk Basin Foods: 4 plt $2,348
- d2 placed parked Inbound Bulk Basin Foods day 2 block 3 door 1
- d2 PO IG2058 Homestead Market Co.: 1 plt $576
- d2 placed parked Inbound Homestead Market Co. day 2 block 4 door 2
- d2 PO IG2059 Homestead Market Co.: 2 plt $2,416
- d2 placed parked Inbound Homestead Market Co. day 2 block 6 door 1
- d2 PO IG2060 Anchor Point Foods: 1 plt $768
- d2 placed parked Inbound Anchor Point Foods day 2 block 8 door 1
- d2 released 1 order(s)
- d2 PO IG2061 Bulk Basin Foods: 5 plt $1,888
- d2 PO IG2062 Blue Ridge Provisions: 4 plt $3,882
- d2 PO IG2063 Homestead Market Co.: 4 plt $2,252
- d2 signed bulk BULK_Cluck & Cover Poultry_D2_0
- d2 signed bulk BULK_Caffeine Fiend Coffee_D2_1
- d2 placed parked Inbound Bulk Basin Foods day 2 block 36 door 2
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 36 door 3
- d2 placed parked Inbound Homestead Market Co. day 2 block 38 door 3
- d2 PO IG2064 Backdoor Bill's Overstock: 2 plt $576
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 40 door 2
- d3 PO IG3065 Homestead Market Co.: 8 plt $4,320
- d3 placed parked Inbound Homestead Market Co. day 3 block 2 door 1
- d3 PO IG3066 Bulk Basin Foods: 11 plt $6,930
- d3 PO IG3067 Blue Ridge Provisions: 4 plt $2,048
- d3 placed parked Inbound Bulk Basin Foods day 3 block 3 door 2
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 3 door 3
- d3 released 1 order(s)
- d3 signed bulk BULK_Chubby Chipmunk Cookies_D3_0
- d3 signed bulk BULK_Mama Tomato Sauces_D3_1
- d4 PO IG4068 Coastal Cargo Exchange: 13 plt $9,096
- d4 PO IG4069 Anchor Point Foods: 1 plt $420
- d4 placed parked Inbound Coastal Cargo Exchange day 4 block 2 door 1
- d4 placed parked Inbound Anchor Point Foods day 4 block 2 door 2
- d4 released 1 order(s)
- d4 signed bulk BULK_The Big Cheese Inc._D4_0
- d5 PO IG5070 Homestead Market Co.: 1 plt $513
- d5 placed parked Inbound Homestead Market Co. day 5 block 2 door 1
- d5 PO IG5071 Homestead Market Co.: 6 plt $3,933
- d5 placed parked Inbound Homestead Market Co. day 5 block 3 door 1
- d5 booked Caffeine Fiend Coffee day 5 block 16 door 1 (pallet-only: stock ready hour 102 + 1.5h of work)
- d5 released 1 order(s)
- d5 released 1 order(s)
- d6 PO IG6072 Bulk Basin Foods: 3 plt $336
- d6 PO IG6073 Backdoor Bill's Overstock: 1 plt $304
- d6 released 1 order(s)
- d7 booked Caffeine Fiend Coffee day 7 block 1 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d7 released 1 order(s)
