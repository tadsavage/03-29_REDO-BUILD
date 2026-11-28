# Playtest block 23 — days 1–8

## Score: **58.0 (+4.5)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (14 shipped, 13 late) | 15% | 18.8 (+4.5) |
| Fill rate | 10% | 95.3 (+1.2) |
| Throughput (158 plt vs target 84) | 15% | 100.0 (0.0) |
| Revenue ($111,598) | 15% | 100.0 (0.0) |
| Profit (cash + stock -46,948) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 91.1 (+23.1) |
| Reliability (bugs) | 25% | 24.3 (+10.2) |
| Reputation (0 to 53) | 5% | 100.0 (0.0) |

Orders shipped **14**, late/fined **13**, pallets shipped **158**. Cash $97,855 → $22,051 (-75,804). Stock at cost $0 → $28,856; cash + stock -46,948.
Bot did: 12 contracts signed, 51 dock bookings, 17 orders released, 41 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **18**
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B#
  - FLOATING| Pallet_#_Slot#_Tier# @#B# bottom=#
  - FLOATING| OutboundPallet_#-a##d#-a#-#_# @#A# bottom=#
  - OVERLAP| OutboundPallet_#-a##d#-a#-#_# & OutboundPallet_#-a##d#-a#-#_# @#A#
  - TRUCK-OVERLAP| Truck→PO_IG# & Truck→Outbound_Door# at (#, #, #)
  - WARN|[AiNavigation] Employee_James Wallace: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|[ReceiverReceivingWorkflow] No master record for pallet ##d#c#f#fa#ec#a
  - ERROR|[MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|[MHEBoundary] 'ReachTruck' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside.
  - NO-TASK| lane pallet #A# sku=# load=#
  - NO-TASK| lane pallet #C# sku=# load=#
  - REPEATING|x# and counting: [DockSchedule] Caffeine Fiend Coffee's #:# slot on day # elapsed with nothing loaded — door freed for rebooking, kept on the grid struck through as missed.
  - REPEATING|x# and counting: [EquipmentBattery] Dockstocker: not sliding # m by hand - that is a navigation job. Trying the path again.
  - TRUCK-OVERLAP| Truck→Outbound_Door# & Truck→Outbound_Door# at (#, #, #)
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #A and it never freed up — giving up and re-queueing.
- New this block: **10**
  - EXCEPTION|MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  - ERROR|[ReceiverReceivingWorkflow] No master record for pallet ##b#db#-b##e#c#
  - ERROR|[ReceiverReceivingWorkflow] No master record for pallet ##a#d#c-ab#-#
  - ERROR|[MHEBoundary] 'Dockstocker' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C#
  - FLOATING| Pallet_#_Slot#_Tier# @#C# bottom=#
  - REPEATING|x# and counting: [JackStall] 'bay ##' at (#, #, #) (target (#, #, #)) remaining=# pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=False onMesh=True jackBlocked=False elapsed=#s reissues=#
  - REPEATING|x# and counting: [JackStall] 'bay ##' at (#, #, #) (target (#, #, #)) remaining=Infinity pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=False onMesh=True jackBlocked=False elapsed=#s reissues=#
  - WARN|[AiNavigation] Employee_Kathleen Lee: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #B and it never freed up — giving up and re-queueing.

## Bugs found (13 distinct, 15 incidents)

| Incidents | Bug |
|---|---|
| 3 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 1 | EXCEPTION — MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it. |
| 1 | ERROR — [ReceiverReceivingWorkflow] No master record for pallet ##b#db#-b##e#c# |
| 1 | ERROR — [ReceiverReceivingWorkflow] No master record for pallet ##a#d#c-ab#-# |
| 1 | ERROR — [MHEBoundary] 'Dockstocker' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 1 | REPEATING — x# and counting: [JackStall] 'bay ##' at (#, #, #) (target (#, #, #)) remaining=# pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=False onMesh=True jackBlocked=False elapsed=#s reissues=# |
| 1 | REPEATING — x# and counting: [JackStall] 'bay ##' at (#, #, #) (target (#, #, #)) remaining=Infinity pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=False onMesh=True jackBlocked=False elapsed=#s reissues=# |
| 1 | WARN — [AiNavigation] Employee_Kathleen Lee: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReachTruckOperator] 'ReachTruck' waited #s for lane #B and it never freed up — giving up and re-queueing. |
| 1 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReachTruckOperator] 'ReachTruck' waited #s for lane #C and it never freed up — giving up and re-queueing. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Kathleen Lee: stuck at (16.94, 1.16, 23.96) — walking out to (23.45, 0.04, 13.14) (11.63m to mesh).
- **DOOR-DEADLOCK** (day 3): OG2730 is Staged but door 1's truck has waited 90 min with no one loading it.
- **ERROR** (day 3): [ReceiverReceivingWorkflow] No master record for pallet d942cfa5-795b-4db6-b187-72225e9c0901
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.ReceiverReceivingWorkflow:BeginReceivingAt (GameCore.Labor.WorkTask,UnityEngine.Transform,single) (at Assets/_Project/Scripts/1. FSM/Core/Labor/ReceiverReceivingWorkflow.cs:247)
  GameCore.Actors.ReceivingTaskDriver:OnArrivedAtStandPosition (GameCore.Labor.WorkTask,UnityEngine.Transform,int) (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:213)
  GameCore.Actors.ReceivingTaskDriver/<>c__DisplayClass22_0:<OnArrivedAtStandPosition>b__0 () (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:226)
  AiNavigation:Update () (at Assets/_Project/Scripts/Actors/AiNavigation.cs:966)
  ```
- **ERROR** (day 3): [ReceiverReceivingWorkflow] No master record for pallet b00de7c7-7a4d-409c-ab75-e05abcf8b387
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.ReceiverReceivingWorkflow:BeginReceivingAt (GameCore.Labor.WorkTask,UnityEngine.Transform,single) (at Assets/_Project/Scripts/1. FSM/Core/Labor/ReceiverReceivingWorkflow.cs:247)
  GameCore.Actors.ReceivingTaskDriver:OnArrivedAtStandPosition (GameCore.Labor.WorkTask,UnityEngine.Transform,int) (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:213)
  GameCore.Actors.ReceivingTaskDriver/<>c__DisplayClass22_0:<OnArrivedAtStandPosition>b__0 () (at Assets/_Project/Scripts/Actors/Receivers/ReceivingTaskDriver.cs:226)
  AiNavigation:Update () (at Assets/_Project/Scripts/Actors/AiNavigation.cs:966)
  ```
- **WARN** (day 4): [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2B and it never freed up — giving up and re-queueing.
- **EXCEPTION** (day 4): MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_position () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  GameCore.Actors.ReachTruckOperator+<DeliverPalletToRack>d__94.MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:1275)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  ```
- **WARN** (day 6): [AiNavigation] Dockstocker: stuck at (15.57, 1.11, 33.60) — walking out to (15.57, 0.08, 42.67) (8.13m to mesh).
- **WARN** (day 6): [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2C and it never freed up — giving up and re-queueing.
- **ERROR** (day 6): [MHEBoundary] 'Dockstocker' fell below the floor at (15.57, 0.00, 42.64) (cell (61, 82)). Putting it back on its last inside spot (15.57, 0.64, 41.72). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **OVERLAP** (day 6): OVERLAP Pallet_02_Slot1_Tier1 & Pallet_01_Slot1_Tier0 @2C-2  <<HISTORY: d6 05:19 registered at (55, 66) 32x482913 by InventoryService.RegisterPhysicalPallet | d6 05:19 Receive task created (->STG2C-2) by ReceivingService.CreateReceiveTaskForPallet | d6 05:20 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>  <<HISTORY: d6 05:34 registered at (55, 66) 8x193587 by InventoryService.RegisterPhysicalPallet | d6 05:34 Receive task created (->STG2C-2) by ReceivingService.CreateReceiveTaskForPallet | d6 05:39 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **FLOATING** (day 6): FLOATING Pallet_03_Slot2_Tier0 @2C-1 bottom=1.30  <<HISTORY: d6 06:08 registered at (55, 65) 32x750426 by InventoryService.RegisterPhysicalPallet | d6 06:08 Receive task created (->STG2C-1) by ReceivingService.CreateReceiveTaskForPallet | d6 06:09 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d6 06:14 Receive task Assigned->Available by ReceivingTaskDriver.ReleaseClaimedTask | d6 06:15 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **REPEATING** (day 6): x25 and counting: [JackStall] 'bay 01-05' at (11.81, 1.11, 32.85) (target (-19.94, 1.11, 18.61)) remaining=36.55 pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(-2.32, 0.00, -0.93) stopped=False onMesh=True jackBlocked=False elapsed=6.8s reissues=0
- **REPEATING** (day 7): x25 and counting: [JackStall] 'bay 01-03' at (15.67, 1.11, 34.50) (target (-14.64, 1.11, 18.61)) remaining=Infinity pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(-1.87, 0.00, -1.37) stopped=False onMesh=True jackBlocked=False elapsed=4.9s reissues=0

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1156 Bulk Basin Foods: 6 plt $2,358
- d1 placed parked Inbound Bulk Basin Foods day 1 block 18 door 1
- d1 PO IG1157 Homestead Market Co.: 4 plt $5,568
- d1 placed parked Inbound Homestead Market Co. day 1 block 19 door 2
- d1 PO IG1158 Bulk Basin Foods: 8 plt $4,688
- d1 placed parked Inbound Bulk Basin Foods day 1 block 20 door 3
- d1 signed bulk BULK_Barnacle Bill's Bait & Bite_D1_0
- d1 signed bulk BULK_Caffeine Fiend Coffee_D1_1
- d1 PO IG1159 Fairweather Trading Co.: 15 plt $17,804
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 22 door 1
- d1 PO IG1160 Fairweather Trading Co.: 10 plt $6,624
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 24 door 2
- d1 PO IG1161 Blue Ridge Provisions: 4 plt $5,505
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 25 door 3
- d1 PO IG1162 Anchor Point Foods: 6 plt $3,780
- d1 placed parked Inbound Anchor Point Foods day 1 block 27 door 3
- d1 PO IG1163 Coastal Cargo Exchange: 8 plt $6,192
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 29 door 2
- d1 PO IG1164 Bulk Basin Foods: 5 plt $2,200
- d1 placed parked Inbound Bulk Basin Foods day 1 block 30 door 1
- d1 released 1 order(s)
- d1 booked Barnacle Bill's Bait & Bite day 2 block 26 door 1 (pallet-only, 15 pallet pick(s) / 2 reach truck(s): stock ready hour 19 + 17.8h of work)
- d1 released 1 order(s)
- d1 booked Caffeine Fiend Coffee day 2 block 24 door 2 (case-pick, 1 picklist(s) / 3 selector(s), 8 pallet pick(s) / 2 reach truck(s): stock ready hour 20 + 15.8h of work)
- d1 released 1 order(s)
- d2 PO IG2165 Anchor Point Foods: 2 plt $896
- d2 PO IG2166 Bulk Basin Foods: 2 plt $504
- d2 placed parked Inbound Anchor Point Foods day 2 block 2 door 1
- d2 placed parked Inbound Bulk Basin Foods day 2 block 2 door 2
- d2 signed bulk BULK_Sneaky Pete's Seafood_D2_0
- d2 signed bulk BULK_Chubby Chipmunk Cookies_D2_1
- d2 PO IG2167 Bulk Basin Foods: 5 plt $520
- d2 placed parked Inbound Bulk Basin Foods day 2 block 34 door 2
- d2 PO IG2168 Bulk Basin Foods: 9 plt $3,848
- d2 placed parked Inbound Bulk Basin Foods day 2 block 35 door 3
- d2 PO IG2169 Anchor Point Foods: 8 plt $6,144
- d2 placed parked Inbound Anchor Point Foods day 2 block 37 door 2
- d2 PO IG2170 Anchor Point Foods: 1 plt $720
- d2 placed parked Inbound Anchor Point Foods day 2 block 43 door 1
- d3 PO IG3171 Backdoor Bill's Overstock: 4 plt $1,088
- d3 PO IG3172 Anchor Point Foods: 2 plt $1,344
- d3 PO IG3173 Bulk Basin Foods: 7 plt $4,143
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 2 door 1
- d3 placed parked Inbound Anchor Point Foods day 3 block 2 door 2
- d3 placed parked Inbound Bulk Basin Foods day 3 block 2 door 3
- d3 booked Sneaky Pete's Seafood day 3 block 20 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 15 pallet pick(s) / 2 reach truck(s): stock ready hour 53 + 4.3h of work)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 released 1 order(s)
- d3 PO IG3174 Homestead Market Co.: 1 plt $720
- d3 placed parked Inbound Homestead Market Co. day 3 block 30 door 1
- d3 signed bulk BULK_The Happy Herd Co._D3_0
- d3 signed bulk BULK_Twisted Twine Pasta_D3_1
- d3 PO IG3175 Golden Grove Gourmet: 6 plt $12,425
- d3 placed parked Inbound Golden Grove Gourmet day 3 block 38 door 2
- d3 PO IG3176 Golden Grove Gourmet: 5 plt $6,600
- d3 placed parked Inbound Golden Grove Gourmet day 3 block 39 door 3
- d3 booked The Happy Herd Co. day 4 block 30 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 6 pallet pick(s) / 2 reach truck(s): stock ready hour 72 + 15.0h of work)
- d3 released 1 order(s)
- d4 PO IG4177 Bulk Basin Foods: 9 plt $4,340
- d4 placed parked Inbound Bulk Basin Foods day 4 block 25 door 1
- d4 booked Twisted Twine Pasta day 5 block 0 door 1 (pallet-only, 6 pallet pick(s) / 2 reach truck(s): stock ready hour 90 + 5.8h of work)
- d4 released 1 order(s)
- d4 signed bulk BULK_Barnacle Bill's Bait & Bite_D4_0
- d4 signed bulk BULK_Grizzly Ridge Jerky_D4_1
- d4 PO IG4178 Fairweather Trading Co.: 11 plt $6,129
- d4 placed parked Inbound Fairweather Trading Co. day 5 block 0 door 2
- d4 booked Chubby Chipmunk Cookies day 5 block 18 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 7 pallet pick(s) / 2 reach truck(s): stock ready hour 102 + 2.9h of work)
- d4 booked Barnacle Bill's Bait & Bite day 5 block 16 door 1 (pallet-only, 4 pallet pick(s) / 2 reach truck(s): stock ready hour 102 + 1.8h of work)
- d4 PO IG4179 Homestead Market Co.: 7 plt $6,140
- d4 placed parked Inbound Homestead Market Co. day 5 block 0 door 3
- d4 booked Grizzly Ridge Jerky day 5 block 16 door 2 (pallet-only, 5 pallet pick(s) / 2 reach truck(s): stock ready hour 101 + 2.2h of work)
- d4 PO IG4180 Bulk Basin Foods: 4 plt $1,824
- d4 placed parked Inbound Bulk Basin Foods day 5 block 3 door 1
- d5 PO IG5181 Backdoor Bill's Overstock: 1 plt $1,300
- d5 placed parked Inbound Backdoor Bill's Overstock day 5 block 4 door 3
- d5 PO IG5182 Bulk Basin Foods: 3 plt $1,200
- d5 placed parked Inbound Bulk Basin Foods day 5 block 5 door 1
- d5 PO IG5183 Bulk Basin Foods: 1 plt $512
- d5 placed parked Inbound Bulk Basin Foods day 5 block 5 door 3
- d5 released 4 order(s)
- d5 PO IG5184 Homestead Market Co.: 1 plt $624
- d5 PO IG5185 Bulk Basin Foods: 1 plt $400
- d5 placed parked Inbound Homestead Market Co. day 5 block 22 door 1
- d5 placed parked Inbound Bulk Basin Foods day 5 block 22 door 2
- d5 released 1 order(s)
- d5 signed bulk BULK_Odd-Shape Orchards_D5_0
- d5 signed bulk BULK_Blocky Bakeshop_D5_1
- d5 PO IG5186 Bulk Basin Foods: 6 plt $528
- d5 placed parked Inbound Bulk Basin Foods day 5 block 40 door 1
- d5 booked Blocky Bakeshop day 6 block 28 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 3 pallet pick(s) / 2 reach truck(s): stock ready hour 121 + 12.1h of work)
- d5 released 1 order(s)
- d5 PO IG5187 Homestead Market Co.: 4 plt $5,776
- d5 placed parked Inbound Homestead Market Co. day 5 block 41 door 2
- d5 booked Odd-Shape Orchards day 6 block 30 door 2 (case-pick, 1 picklist(s) / 3 selector(s), 8 pallet pick(s) / 2 reach truck(s): stock ready hour 121 + 13.6h of work)
- d5 released 1 order(s)
- d6 PO IG6188 Anchor Point Foods: 1 plt $624
- d6 PO IG6189 Bulk Basin Foods: 5 plt $2,243
- d6 placed parked Inbound Anchor Point Foods day 6 block 2 door 1
- d6 placed parked Inbound Bulk Basin Foods day 6 block 2 door 2
- d6 PO IG6190 Bulk Basin Foods: 4 plt $1,242
- d6 placed parked Inbound Bulk Basin Foods day 6 block 44 door 1
- d6 signed bulk BULK_Twisted Twine Pasta_D6_0
- d6 PO IG6191 Homestead Market Co.: 4 plt $2,929
- d6 placed parked Inbound Homestead Market Co. day 6 block 46 door 1
- d6 PO IG6192 Bulk Basin Foods: 1 plt $672
- d6 placed parked Inbound Bulk Basin Foods day 6 block 47 door 2
- d7 released 1 order(s)
- d7 PO IG7193 Fairweather Trading Co.: 8 plt $5,176
- d7 PO IG7194 Homestead Market Co.: 1 plt $486
- d7 placed parked Inbound Fairweather Trading Co. day 7 block 2 door 1
- d7 placed parked Inbound Homestead Market Co. day 7 block 2 door 2
- d7 released 1 order(s)
- d7 PO IG7195 Bulk Basin Foods: 1 plt $160
- d7 placed parked Inbound Bulk Basin Foods day 7 block 36 door 2
- d7 PO IG7196 Backdoor Bill's Overstock: 1 plt $1,080
- d7 placed parked Inbound Backdoor Bill's Overstock day 7 block 37 door 2
