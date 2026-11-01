# Playtest block 24 — days 1–8

## Score: **57.1 (-0.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (12 shipped, 10 late) | 15% | 33.3 (+14.6) |
| Fill rate | 10% | 90.6 (-4.8) |
| Throughput (139 plt vs target 84) | 15% | 100.0 (0.0) |
| Revenue ($90,518) | 15% | 100.0 (0.0) |
| Profit (cash + stock -48,202) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (+8.9) |
| Reliability (bugs) | 25% | 18.8 (-5.5) |
| Reputation (0 to 10) | 5% | 66.7 (-33.3) |

Orders shipped **12**, late/fined **10**, pallets shipped **139**. Cash $97,855 → $20,278 (-77,577). Stock at cost $0 → $29,375; cash + stock -48,202.
Bot did: 10 contracts signed, 43 dock bookings, 14 orders released, 36 POs, 0 rush POs.

## Late orders - state when it came due
- 5 x loaded but the truck had not left
- 2 x still loading
- 2 x never released
- 1 x staged but no trailer loaded it

- day 2: OG4752 (Cluck & Cover Poultry) was loaded but the truck had not left. Fined $1,082. Lines 2, 203 cs, released=True.
- day 2: OG3751 (Fizz-Pop Soda Works) was loaded but the truck had not left. Fined $2,487. Lines 3, 585 cs, released=True.
- day 3: OG5753 (Pony Express Oats) was still loading when it came due. Fined $400. Lines 1, 80 cs, released=True.
- day 3: OG3750 (Sneaky Pete's Seafood) was still loading when it came due. Fined $1,684. Lines 13, 343 cs, released=True.
- day 5: OG7759 (Bramble & Bite Berry Co.) was loaded but the truck had not left. Fined $3,810. Lines 2, 598 cs, released=True.
- day 5: OG5757 (Sneaky Pete's Seafood) was staged but no trailer loaded it (door 1). Fined $1,941. Lines 14, 367 cs, released=True.
- day 6: OG5761 (Sprout Scout Foods) was never released to the floor. Fined $7,026. Lines 3, 828 cs, released=False.
- day 6: OG5762 (Happy Cow Creamery) was never released to the floor. Fined $10,539. Lines 3, 988 cs, released=False.
- day 6: OG6760 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,561. Lines 15, 383 cs, released=True.
- day 7: OG7763 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,821. Lines 14, 409 cs, released=True.

## Change since last block
- Fixed / no longer seen: **11**
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
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #C and it never freed up — giving up and re-queueing.
- New this block: **11**
  - FLOATING| OutboundPallet_###a#-ba###_# @#A# bottom=#
  - OVERLAP| OutboundPallet_###a#-ba###_# & OutboundPallet_###a#-ba###_# @#A#
  - ERROR|[MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - NO-TASK| lane pallet #A# sku=# load=#
  - OVERLAP| OutboundPallet_###ca##a#-#_# & OutboundPallet_###ca##a#-#_# @#E#
  - ERROR|[MHEBoundary] 'ReachTruck' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside.
  - OVERLAP| Pallet_#_Slot#_Tier# & OutboundPallet_###a#-ba###_# @#A#
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #E and it never freed up — giving up and re-queueing.
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #A and it never freed up — giving up and re-queueing.
  - WARN|[AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).

## Bugs found (13 distinct, 23 incidents)

| Incidents | Bug |
|---|---|
| 3 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 3 | FLOATING —  OutboundPallet_###a#-ba###_# @#A# bottom=# |
| 3 | OVERLAP —  OutboundPallet_###a#-ba###_# & OutboundPallet_###a#-ba###_# @#A# |
| 2 | ERROR — [MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 2 | NO-TASK —  lane pallet #A# sku=# load=# |
| 1 | OVERLAP —  OutboundPallet_###ca##a#-#_# & OutboundPallet_###ca##a#-#_# @#E# |
| 1 | ERROR — [MHEBoundary] 'ReachTruck' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_###a#-ba###_# @#A# |
| 2 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [ReachTruckOperator] 'ReachTruck' waited #s for lane #E and it never freed up — giving up and re-queueing. |
| 1 | WARN — [ReachTruckOperator] 'ReachTruck' waited #s for lane #A and it never freed up — giving up and re-queueing. |
| 1 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **DOOR-DEADLOCK** (day 2): OG2749 is Staged but door 1's truck has waited 90 min with no one loading it.
- **OVERLAP** (day 2): OVERLAP OutboundPallet_5d6bdcd2-7439-4ca6-9a55-d7fea9bb1635_2 & OutboundPallet_5d6bdcd2-7439-4ca6-9a55-d7fea9bb1635_1 @1E-1
- **NO-TASK** (day 2): NO-TASK lane pallet 3A-3 sku=172495 load=6795962050  <<HISTORY: d2 19:39 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 19:53 Putaway task created (STG3A-3->) by ReceiverReceivingWorkflow.CreatePutawayTask | d2 19:53 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 19:53 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d2 19:53 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d2 19:55 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **WARN** (day 2): [ReachTruckOperator] 'ReachTruck' waited 45s for lane 3A and it never freed up — giving up and re-queueing.
- **WARN** (day 2): [AiNavigation] ReachTruck: stuck at (18.22, 1.11, 34.88) — walking out to (22.72, 0.08, 42.67) (8.07m to mesh).
- **ERROR** (day 2): [MHEBoundary] 'ReachTruck' fell below the floor at (22.48, 0.00, 42.27) (cell (67, 81)). Putting it back on its last inside spot (22.14, 0.70, 41.68). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **ERROR** (day 2): [MHEBoundary] 'ReachTruck' keeps driving out of the building (6 times in 10s, at (22.28, 0.00, 41.92)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  
  ```
- **WARN** (day 3): [ReachTruckOperator] 'ReachTruck' waited 45s for lane 1E and it never freed up — giving up and re-queueing.
- **WARN** (day 3): [AiNavigation] Dockstocker: stuck at (-2.33, 1.11, 38.33) — walking out to (-8.81, 1.16, 26.43) (12.55m to mesh).
- **WARN** (day 3): [AiNavigation] PalletJack: stuck at (18.75, 1.11, 32.93) — walking out to (5.75, 1.19, 32.93) (12.00m to mesh).
- **OVERLAP** (day 7): OVERLAP Pallet_03_Slot2_Tier1 & OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 @1A-1  <<HISTORY: d6 22:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d6 22:12 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d6 22:27 moved (36, 71)->(-1, -1) by InventoryService.MovePallet | d6 22:40 adopted as outbound freight of order b02a3e06-0258-43a5-ba72-7584aa5c5099 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d6 22:40 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d6 22:40 record destroyed by InventoryService.DestroyPallet>>
- **FLOATING** (day 7): FLOATING OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 @1A-1 bottom=1.96
- **OVERLAP** (day 7): OVERLAP OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 & OutboundPallet_b02a3e06-0258-43a5-ba72-7584aa5c5099_1 @1A-4

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1197 Bulk Basin Foods: 8 plt $2,446
- d1 placed parked Inbound Bulk Basin Foods day 1 block 18 door 1
- d1 PO IG1198 Fairweather Trading Co.: 7 plt $6,344
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 19 door 2
- d1 PO IG1199 Backdoor Bill's Overstock: 3 plt $2,076
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 20 door 3
- d1 signed bulk BULK_Fizz-Pop Soda Works_D1_0
- d1 signed bulk BULK_Cluck & Cover Poultry_D1_1
- d1 PO IG1200 Fairweather Trading Co.: 13 plt $6,920
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 22 door 1
- d1 PO IG1201 Bulk Basin Foods: 11 plt $6,408
- d1 placed parked Inbound Bulk Basin Foods day 1 block 24 door 2
- d1 PO IG1202 Homestead Market Co.: 7 plt $4,595
- d1 placed parked Inbound Homestead Market Co. day 1 block 25 door 3
- d1 PO IG1203 Anchor Point Foods: 7 plt $3,588
- d1 placed parked Inbound Anchor Point Foods day 1 block 29 door 1
- d1 booked Cluck & Cover Poultry day 2 block 0 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 7 pallet pick(s) / 2 reach truck(s): stock ready hour 20 + 3.1h of work)
- d1 booked Fizz-Pop Soda Works day 2 block 0 door 2 (case-pick, 1 picklist(s) / 3 selector(s), 17 pallet pick(s) / 2 reach truck(s): stock ready hour 16 + 6.2h of work)
- d1 released 1 order(s)
- d1 released 2 order(s)
- d2 PO IG2204 Homestead Market Co.: 2 plt $1,134
- d2 PO IG2205 Bulk Basin Foods: 3 plt $1,320
- d2 placed parked Inbound Homestead Market Co. day 2 block 2 door 3
- d2 placed parked Inbound Bulk Basin Foods day 2 block 3 door 3
- d2 signed bulk BULK_Pony Express Oats_D2_0
- d2 signed bulk BULK_Caffeine Fiend Coffee_D2_1
- d2 PO IG2206 Backdoor Bill's Overstock: 5 plt $1,360
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 34 door 2
- d2 booked Pony Express Oats day 3 block 2 door 1 (pallet-only, 5 pallet pick(s) / 2 reach truck(s): stock ready hour 46 + 2.2h of work)
- d2 PO IG2207 Bulk Basin Foods: 3 plt $2,160
- d2 placed parked Inbound Bulk Basin Foods day 2 block 35 door 3
- d2 PO IG2208 Bulk Basin Foods: 5 plt $2,710
- d2 placed parked Inbound Bulk Basin Foods day 2 block 37 door 2
- d2 PO IG2209 Fairweather Trading Co.: 15 plt $12,952
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 38 door 3
- d2 released 1 order(s)
- d2 PO IG2210 Anchor Point Foods: 1 plt $768
- d2 placed parked Inbound Anchor Point Foods day 2 block 40 door 2
- d2 PO IG2211 Homestead Market Co.: 1 plt $1,920
- d2 placed parked Inbound Homestead Market Co. day 2 block 41 door 1
- d2 released 1 order(s)
- d3 PO IG3212 Bulk Basin Foods: 2 plt $1,344
- d3 PO IG3213 Backdoor Bill's Overstock: 1 plt $256
- d3 placed parked Inbound Bulk Basin Foods day 3 block 2 door 2
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 2 door 3
- d3 released 1 order(s)
- d3 signed bulk BULK_Mama Tomato Sauces_D3_0
- d3 PO IG3214 Bulk Basin Foods: 10 plt $5,440
- d3 placed parked Inbound Bulk Basin Foods day 3 block 24 door 1
- d3 booked Mama Tomato Sauces day 4 block 22 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 10 pallet pick(s) / 2 reach truck(s): stock ready hour 66 + 16.4h of work)
- d3 released 1 order(s)
- d3 PO IG3215 Bulk Basin Foods: 1 plt $544
- d3 placed parked Inbound Bulk Basin Foods day 3 block 25 door 2
- d3 PO IG3216 Golden Grove Gourmet: 6 plt $9,750
- d3 placed parked Inbound Golden Grove Gourmet day 3 block 36 door 2
- d3 PO IG3217 Bulk Basin Foods: 3 plt $640
- d3 placed parked Inbound Bulk Basin Foods day 3 block 37 door 3
- d4 PO IG4218 Backdoor Bill's Overstock: 1 plt $256
- d4 PO IG4219 Bulk Basin Foods: 2 plt $536
- d4 placed parked Inbound Backdoor Bill's Overstock day 4 block 2 door 1
- d4 placed parked Inbound Bulk Basin Foods day 4 block 2 door 2
- d4 signed bulk BULK_Puff & Stuff Pastries_D4_0
- d4 signed bulk BULK_Bramble & Bite Berry Co._D4_1
- d4 booked Puff & Stuff Pastries day 4 block 36 door 2 (pallet-only, 1 pallet pick(s) / 2 reach truck(s): stock ready hour 88 + 1.5h of work)
- d4 released 1 order(s)
- d4 PO IG4220 Fairweather Trading Co.: 9 plt $10,440
- d4 placed parked Inbound Fairweather Trading Co. day 4 block 34 door 3
- d4 PO IG4221 Homestead Market Co.: 2 plt $1,134
- d4 placed parked Inbound Homestead Market Co. day 4 block 35 door 2
- d4 PO IG4222 Homestead Market Co.: 2 plt $2,239
- d4 placed parked Inbound Homestead Market Co. day 4 block 42 door 1
- d4 PO IG4223 Bulk Basin Foods: 1 plt $360
- d4 placed parked Inbound Bulk Basin Foods day 4 block 43 door 1
- d4 PO IG4224 Backdoor Bill's Overstock: 1 plt $960
- d4 placed parked Inbound Backdoor Bill's Overstock day 4 block 44 door 1
- d4 PO IG4225 Anchor Point Foods: 1 plt $816
- d4 placed parked Inbound Anchor Point Foods day 4 block 46 door 1
- d4 booked Bramble & Bite Berry Co. day 5 block 6 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 12 pallet pick(s) / 2 reach truck(s): stock ready hour 94 + 4.6h of work)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d5 PO IG5226 Coastal Cargo Exchange: 5 plt $5,220
- d5 PO IG5227 Backdoor Bill's Overstock: 1 plt $240
- d5 PO IG5228 Homestead Market Co.: 1 plt $486
- d5 placed parked Inbound Coastal Cargo Exchange day 5 block 2 door 1
- d5 placed parked Inbound Backdoor Bill's Overstock day 5 block 2 door 2
- d5 placed parked Inbound Homestead Market Co. day 5 block 2 door 3
- d5 booked Caffeine Fiend Coffee day 5 block 40 door 2 (case-pick, 1 picklist(s) / 3 selector(s), 8 pallet pick(s) / 2 reach truck(s): stock ready hour 102 + 13.9h of work)
- d5 released 1 order(s)
- d5 released 1 order(s)
- d5 signed bulk BULK_Sprout Scout Foods_D5_0
- d5 signed bulk BULK_Happy Cow Creamery_D5_1
- d6 PO IG6229 Bulk Basin Foods: 6 plt $2,960
- d6 placed parked Inbound Bulk Basin Foods day 6 block 2 door 1
- d6 PO IG6230 Bulk Basin Foods: 4 plt $1,309
- d6 placed parked Inbound Bulk Basin Foods day 6 block 42 door 1
- d6 released 1 order(s)
- d7 PO IG7231 Blue Ridge Provisions: 3 plt $3,600
- d7 placed parked Inbound Blue Ridge Provisions day 7 block 2 door 1
- d7 released 1 order(s)
- d7 PO IG7232 Fairweather Trading Co.: 7 plt $6,201
- d7 placed parked Inbound Fairweather Trading Co. day 7 block 42 door 1
