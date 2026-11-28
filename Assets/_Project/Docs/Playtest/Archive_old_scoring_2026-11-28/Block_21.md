# Playtest block 21 — days 1–8

## Score: **30.2 (-3.3)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 9 late) | 15% | 10.0 (0.0) |
| Fill rate | 10% | 80.5 (-19.5) |
| Throughput (24 plt vs target 84) | 15% | 28.6 (+4.8) |
| Revenue ($12,865) | 15% | 23.0 (-37.5) |
| Profit (cash + stock -84,849) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 64.1 (+26.7) |
| Reliability (bugs) | 25% | 18.7 (+5.7) |
| Reputation (0 to 34) | 5% | 100.0 (+16.7) |

Orders shipped **2**, late/fined **9**, pallets shipped **24**. Cash $97,857 → $1,375 (-96,482). Stock at cost $0 → $11,633; cash + stock -84,849.
Bot did: 5 contracts signed, 35 dock bookings, 10 orders released, 26 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **21**
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C#
  - TRUCK-OVERLAP| Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #)
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B#
  - OVERLAP| OutboundPallet_##d##e####_# & OutboundPallet_##d##e####_# @#D#
  - WARN|[AiNavigation] Employee_Barbara Bennett: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - NO-TASK| lane pallet #B# sku=# load=#
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A#
  - FLOATING| Pallet_#_Slot#_Tier# @#A# bottom=#
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#D#
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_##d##e####_# @#D#
  - FLOATING| OutboundPallet_##d##e####_# @#D# bottom=#
  - FLOATING| Pallet_#_Slot#_Tier# @#D# bottom=#
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_###-b#fd##_# @#B#
  - FLOATING| Pallet_#_Slot#_Tier# @#B# bottom=#
  - FLOATING| Pallet_#_Slot#_Tier# @#C# bottom=#
- New this block: **11**
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it.
  - ERROR|[MHEBoundary] 'Dockstocker' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|[MHEBoundary] 'Dockstocker' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside.
  - ERROR|[ReceiverReceivingWorkflow] No master record for pallet #-a#f#a##ffc-#
  - ERROR|[MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|[MHEBoundary] 'ReachTruck' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside.
  - REPEATING|x# and counting: [EquipmentBattery] Dockstocker: not sliding # m by hand - that is a navigation job. Trying the path again.
  - WARN|[AiNavigation] Employee_Judith Harmon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #A and it never freed up — giving up and re-queueing.
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (12 distinct, 25 incidents)

| Incidents | Bug |
|---|---|
| 8 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 7 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it. |
| 1 | ERROR — [MHEBoundary] 'Dockstocker' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | ERROR — [MHEBoundary] 'Dockstocker' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside. |
| 1 | ERROR — [ReceiverReceivingWorkflow] No master record for pallet #-a#f#a##ffc-# |
| 1 | ERROR — [MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | ERROR — [MHEBoundary] 'ReachTruck' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside. |
| 1 | REPEATING — x# and counting: [EquipmentBattery] Dockstocker: not sliding # m by hand - that is a navigation job. Trying the path again. |
| 1 | WARN — [AiNavigation] Employee_Judith Harmon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReachTruckOperator] 'ReachTruck' waited #s for lane #A and it never freed up — giving up and re-queueing. |
| 1 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Judith Harmon: stuck at (-1.57, 1.16, 21.31) — walking out to (-12.79, 1.08, 14.00) (12.56m to mesh).
- **WARN** (day 1): [AiNavigation] Dockstocker: stuck at (18.84, 1.11, 37.56) — walking out to (21.84, 0.08, 42.76) (5.11m to mesh).
- **ERROR** (day 1): [MHEBoundary] 'Dockstocker' fell below the floor at (21.52, 0.41, 42.20) (cell (66, 81)). Putting it back on its last inside spot (21.19, 0.75, 41.63). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **ERROR** (day 1): [MHEBoundary] 'Dockstocker' keeps driving out of the building (6 times in 10s, at (21.64, 0.00, 42.43)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  
  ```
- **ERROR** (day 2): [ReceiverReceivingWorkflow] No master record for pallet 1a9938d6-a12f-4a35-9ffc-cf21394ab376
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.ReceiverReceivingWorkflow:BeginReceivingAt (GameCore.Labor.WorkTask,UnityEngine.Transform,single) (at Assets/_Project/Scripts/1. FSM/Core/Labor/ReceiverReceivingWorkflow.cs:247)
  GameCore.Actors.ReceivingTaskDriver:OnArrivedAtStandPosition (GameCore.Labor.WorkTask,UnityEngine.Transform,int) (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:213)
  GameCore.Actors.ReceivingTaskDriver/<>c__DisplayClass22_0:<OnArrivedAtStandPosition>b__0 () (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:226)
  AiNavigation:Update () (at Assets/_Project/Scripts/Actors/AiNavigation.cs:966)
  ```
- **DOOR-DEADLOCK** (day 3): OG2705 is Loading but door 1's truck has waited 90 min with no one loading it.
- **DOOR-DEADLOCK** (day 3): OG2705 is Staged but door 1's truck has waited 255 min with no one loading it.
- **REPEATING** (day 3): x25 and counting: [EquipmentBattery] Dockstocker: not sliding 41.2 m by hand - that is a navigation job. Trying the path again.
- **WARN** (day 3): [ReachTruckOperator] 'ReachTruck' waited 45s for lane 3A and it never freed up — giving up and re-queueing.
- **WARN** (day 3): [AiNavigation] ReachTruck: stuck at (15.49, 1.11, 33.60) — walking out to (15.49, 0.08, 42.67) (8.13m to mesh).
- **ERROR** (day 3): [MHEBoundary] 'ReachTruck' fell below the floor at (15.49, 0.00, 42.67) (cell (61, 82)). Putting it back on its last inside spot (15.49, 0.69, 41.69). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **ERROR** (day 3): [MHEBoundary] 'ReachTruck' keeps driving out of the building (6 times in 10s, at (15.49, 0.08, 42.04)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  
  ```

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1093 Backdoor Bill's Overstock: 3 plt $1,596
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 18 door 1
- d1 PO IG1094 Bulk Basin Foods: 10 plt $3,316
- d1 placed parked Inbound Bulk Basin Foods day 1 block 19 door 2
- d1 PO IG1095 Anchor Point Foods: 2 plt $1,196
- d1 placed parked Inbound Anchor Point Foods day 1 block 20 door 1
- d1 signed bulk BULK_Barnacle Bill's Bait & Bite_D1_0
- d1 PO IG1096 Homestead Market Co.: 2 plt $2,828
- d1 placed parked Inbound Homestead Market Co. day 1 block 22 door 1
- d1 PO IG1097 Homestead Market Co.: 1 plt $576
- d1 placed parked Inbound Homestead Market Co. day 1 block 24 door 1
- d1 PO IG1098 Anchor Point Foods: 11 plt $7,680
- d1 placed parked Inbound Anchor Point Foods day 1 block 25 door 1
- d1 PO IG1099 Bulk Basin Foods: 6 plt $1,200
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 2
- d1 PO IG1100 Bulk Basin Foods: 1 plt $200
- d1 placed parked Inbound Bulk Basin Foods day 1 block 28 door 3
- d1 booked Barnacle Bill's Bait & Bite day 2 block 0 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 16 pallet pick(s) / 2 reach truck(s): stock ready hour 19 + 4.3h of work)
- d1 PO IG1101 Bulk Basin Foods: 1 plt $200
- d1 placed parked Inbound Bulk Basin Foods day 1 block 30 door 2
- d1 released 1 order(s)
- d2 PO IG2102 Anchor Point Foods: 1 plt $672
- d2 PO IG2103 Bulk Basin Foods: 1 plt $112
- d2 placed parked Inbound Anchor Point Foods day 2 block 2 door 2
- d2 placed parked Inbound Bulk Basin Foods day 2 block 2 door 3
- d2 released 1 order(s)
- d2 signed bulk BULK_Fizz-Pop Soda Works_D2_0
- d2 PO IG2104 Blue Ridge Provisions: 7 plt $8,820
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 34 door 2
- d2 PO IG2105 Bulk Basin Foods: 5 plt $2,080
- d2 placed parked Inbound Bulk Basin Foods day 2 block 35 door 3
- d2 booked Fizz-Pop Soda Works day 3 block 8 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 10 pallet pick(s) / 2 reach truck(s): stock ready hour 46 + 5.8h of work)
- d2 released 1 order(s)
- d2 PO IG2106 Ambrose Fine Foods: 4 plt $9,972
- d2 placed parked Inbound Ambrose Fine Foods day 2 block 38 door 2
- d2 PO IG2107 Anchor Point Foods: 1 plt $448
- d2 placed parked Inbound Anchor Point Foods day 2 block 38 door 3
- d2 PO IG2108 Homestead Market Co.: 3 plt $3,872
- d2 placed parked Inbound Homestead Market Co. day 2 block 40 door 1
- d2 PO IG2109 Bulk Basin Foods: 4 plt $1,497
- d2 placed parked Inbound Bulk Basin Foods day 2 block 41 door 2
- d2 PO IG2110 Blue Ridge Provisions: 3 plt $4,677
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 42 door 1
- d2 released 1 order(s)
- d3 PO IG3111 Bulk Basin Foods: 4 plt $1,104
- d3 PO IG3112 Anchor Point Foods: 1 plt $392
- d3 placed parked Inbound Bulk Basin Foods day 3 block 2 door 1
- d3 placed parked Inbound Anchor Point Foods day 3 block 2 door 2
- d3 signed bulk BULK_The Daily Grind Tofu_D3_0
- d3 signed bulk BULK_Honest Harvest Rice_D3_1
- d3 PO IG3113 Coastal Cargo Exchange: 10 plt $10,708
- d3 placed parked Inbound Coastal Cargo Exchange day 3 block 24 door 1
- d3 booked Honest Harvest Rice day 3 block 42 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 7 pallet pick(s) / 2 reach truck(s): stock ready hour 66 + 3.0h of work)
- d3 PO IG3114 Backdoor Bill's Overstock: 6 plt $1,920
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 25 door 2
- d3 booked The Daily Grind Tofu day 3 block 40 door 2 (pallet-only, 6 pallet pick(s) / 2 reach truck(s): stock ready hour 65 + 2.2h of work)
- d3 released 2 order(s)
- d3 released 1 order(s)
- d3 booked Honest Harvest Rice day 4 block 4 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 71 + 2.8h of work)
- d4 PO IG4115 Backdoor Bill's Overstock: 2 plt $608
- d4 PO IG4116 Bulk Basin Foods: 2 plt $1,190
- d4 PO IG4117 Homestead Market Co.: 2 plt $1,168
- d4 placed parked Inbound Backdoor Bill's Overstock day 4 block 2 door 1
- d4 placed parked Inbound Bulk Basin Foods day 4 block 2 door 2
- d4 placed parked Inbound Homestead Market Co. day 4 block 2 door 3
- d4 booked Honest Harvest Rice day 4 block 14 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 76 + 2.9h of work)
- d4 booked Honest Harvest Rice day 4 block 24 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 81 + 3.0h of work)
- d4 booked Honest Harvest Rice day 4 block 29 door 1 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 booked Honest Harvest Rice day 4 block 35 door 2 (pallet-only: stock ready hour 0 + 1.5h of work)
- d4 released 1 order(s)
- d5 PO IG5118 Anchor Point Foods: 1 plt $392
- d5 placed parked Inbound Anchor Point Foods day 5 block 2 door 1
- d6 released 1 order(s)
- d6 released 1 order(s)
