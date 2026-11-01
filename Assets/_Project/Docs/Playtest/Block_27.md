# Playtest block 27 — days 1–8

## Score: **58.0 (+2.1)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (7 shipped, 7 late) | 15% | 12.5 (-37.5) |
| Fill rate | 10% | 87.3 (+10.3) |
| Throughput (116 plt vs target 84) | 15% | 100.0 (0.0) |
| Revenue ($63,365) | 15% | 100.0 (+31.6) |
| Profit (cash + stock -28,951) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 29.6 (+7.7) |
| Reputation (0 to 76) | 5% | 100.0 (0.0) |

Orders shipped **7**, late/fined **7**, pallets shipped **116**. Cash $97,857 → $33,982 (-63,875). Stock at cost $0 → $34,924; cash + stock -28,951.
Bot did: 3 contracts signed, 47 dock bookings, 7 orders released, 42 POs, 0 rush POs.

## Late orders - state when it came due
- 4 x loaded but the truck had not left
- 1 x staged but no trailer loaded it
- 1 x still being picked
- 1 x never released

- day 2: OG2790 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,662. Lines 21, 369 cs, released=True.
- day 4: OG4792 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,750. Lines 21, 421 cs, released=True.
- day 5: OG6795 (Salty Sailor Pretzel Co.) was loaded but the truck had not left. Fined $1,868. Lines 2, 367 cs, released=True.
- day 5: OG5793 (Sneaky Pete's Seafood) was staged but no trailer loaded it (door 1). Fined $1,545. Lines 13, 324 cs, released=True.
- day 6: OG6794 (Sneaky Pete's Seafood) was still being picked (239/311 cs). Fined $1,477. Lines 12, 311 cs, released=True.
- day 7: OG7796 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,635. Lines 18, 395 cs, released=True.
- day 8: OG7798 (Neon Nectar Juice Co.) was never released to the floor. Fined $11,460. Lines 3, 1761 cs, released=False.

## Change since last block
- Fixed / no longer seen: **14**
  - ERROR|[ReceiverReceivingWorkflow] No master record for pallet #-c##-a###
  - ERROR|[ReceiverReceivingWorkflow] No master record for pallet #-bfc##d##ba###
  - ERROR|[MHEBoundary] 'PalletJack' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - ERROR|[MHEBoundary] 'PalletJack' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - WARN|[AiNavigation] Employee_Madison Ford: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - TRUCK-OVERLAP| Truck→PO_IG# & Truck→PO_IG# at (#, #, #)
  - NO-TASK| lane pallet #B# sku=# load=#
  - ORPHAN-OUTBOUND| #A# (order ##f#### gone)
  - WARN|[AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #B and it never freed up — giving up and re-queueing.
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jean Stewart' as its operator though nobody is on it or running it - clearing the stale link.
  - WARN|[OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock.
- New this block: **5**
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it.
  - OVERLAP| OutboundPallet_#-f##a-a#f#e#e#_# & OutboundPallet_#-f##a-a#f#e#e#_# @#C#
  - WARN|[AiNavigation] Employee_Sarah Rogers: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [JackStall] 'bay ##' at (#, #, #) (target (#, #, #)) remaining=Infinity pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=False onMesh=True jackBlocked=False elapsed=#s reissues=#
  - WARN|[ReachTruckOperator] 'ReachTruck' waited #s for lane #A and it never freed up — giving up and re-queueing.

## Bugs found (9 distinct, 13 incidents)

| Incidents | Bug |
|---|---|
| 2 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 2 | OVERLAP —  OutboundPallet_#-f##a-a#f#e#e#_# & OutboundPallet_#-f##a-a#f#e#e#_# @#C# |
| 3 | WARN — [AiNavigation] Employee_Sarah Rogers: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [MHEBoundary] 'ReachTruck' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | ERROR — [MHEBoundary] 'ReachTruck' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside. |
| 1 | NO-TASK —  lane pallet #A# sku=# load=# |
| 1 | REPEATING — x# and counting: [JackStall] 'bay ##' at (#, #, #) (target (#, #, #)) remaining=Infinity pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=False onMesh=True jackBlocked=False elapsed=#s reissues=# |
| 1 | WARN — [ReachTruckOperator] 'ReachTruck' waited #s for lane #A and it never freed up — giving up and re-queueing. |
| 1 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Sarah Rogers: stuck at (7.69, 1.15, 23.96) — walking out to (14.21, 0.04, 13.14) (11.63m to mesh).
- **NO-TASK** (day 5): NO-TASK lane pallet 2A-3 sku=215678 load=0373388643  <<HISTORY: d5 02:00 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d5 02:12 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d5 02:12 PalletPick task created (STG2A-3->) by OrderService.FileReleasedOrderTasks | d5 02:12 PalletPick task Available->Open by OrderService.FileReleasedOrderTasks | d5 02:12 PalletPick task Open->Available by OrderService.AllocateHeldWork | d5 03:11 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **WARN** (day 5): [ReachTruckOperator] 'ReachTruck' waited 45s for lane 2A and it never freed up — giving up and re-queueing.
- **WARN** (day 5): [AiNavigation] ReachTruck: stuck at (15.49, 1.11, 33.60) — walking out to (15.49, 0.08, 42.67) (8.13m to mesh).
- **ERROR** (day 5): [MHEBoundary] 'ReachTruck' fell below the floor at (15.49, 0.61, 41.83) (cell (61, 81)). Putting it back on its last inside spot (15.49, 0.97, 41.25). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```
- **ERROR** (day 5): [MHEBoundary] 'ReachTruck' keeps driving out of the building (6 times in 10s, at (15.49, 0.59, 41.86)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:87)
  
  ```
- **DOOR-DEADLOCK** (day 5): OG5793 is Staged but door 1's truck has waited 90 min with no one loading it.
- **OVERLAP** (day 5): OVERLAP OutboundPallet_9e2ef5fb-f152-471a-a24f-57070e9374e6_2 & OutboundPallet_9e2ef5fb-f152-471a-a24f-57070e9374e6_1 @1C-1
- **REPEATING** (day 7): x25 and counting: [JackStall] 'bay 02-04' at (21.76, 1.11, 32.68) (target (-14.61, 1.11, 25.24)) remaining=Infinity pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(-0.27, 0.00, -0.49) stopped=False onMesh=True jackBlocked=False elapsed=1.2s reissues=0

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1301 Anchor Point Foods: 10 plt $6,538
- d1 placed parked Inbound Anchor Point Foods day 1 block 18 door 1
- d1 PO IG1302 Bulk Basin Foods: 11 plt $5,286
- d1 placed parked Inbound Bulk Basin Foods day 1 block 19 door 2
- d1 PO IG1303 Ambrose Fine Foods: 7 plt $12,168
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 20 door 3
- d1 PO IG1304 Homestead Market Co.: 1 plt $594
- d1 placed parked Inbound Homestead Market Co. day 1 block 23 door 1
- d1 PO IG1305 Backdoor Bill's Overstock: 5 plt $2,452
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 24 door 1
- d1 PO IG1306 Homestead Market Co.: 3 plt $2,016
- d1 placed parked Inbound Homestead Market Co. day 1 block 28 door 1
- d2 released 1 order(s)
- d2 PO IG2307 Anchor Point Foods: 2 plt $996
- d2 PO IG2308 Bulk Basin Foods: 1 plt $112
- d2 placed parked Inbound Anchor Point Foods day 2 block 2 door 1
- d2 placed parked Inbound Bulk Basin Foods day 2 block 2 door 2
- d2 PO IG2309 Coastal Cargo Exchange: 5 plt $5,656
- d2 placed parked Inbound Coastal Cargo Exchange day 2 block 36 door 2
- d2 PO IG2310 Bulk Basin Foods: 1 plt $200
- d2 placed parked Inbound Bulk Basin Foods day 2 block 37 door 3
- d2 PO IG2311 Homestead Market Co.: 4 plt $3,766
- d2 placed parked Inbound Homestead Market Co. day 2 block 38 door 3
- d2 PO IG2312 Anchor Point Foods: 1 plt $480
- d2 placed parked Inbound Anchor Point Foods day 2 block 40 door 2
- d2 PO IG2313 Bulk Basin Foods: 1 plt $665
- d2 PO IG2314 Anchor Point Foods: 1 plt $840
- d2 placed parked Inbound Bulk Basin Foods day 2 block 44 door 1
- d2 placed parked Inbound Anchor Point Foods day 2 block 44 door 2
- d2 released 1 order(s)
- d3 PO IG3315 Backdoor Bill's Overstock: 2 plt $608
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 36 door 2
- d3 PO IG3316 Anchor Point Foods: 1 plt $520
- d3 placed parked Inbound Anchor Point Foods day 3 block 37 door 2
- d3 PO IG3317 Anchor Point Foods: 2 plt $1,225
- d3 placed parked Inbound Anchor Point Foods day 3 block 38 door 2
- d3 PO IG3318 Backdoor Bill's Overstock: 1 plt $304
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 40 door 2
- d3 PO IG3319 Homestead Market Co.: 1 plt $621
- d3 placed parked Inbound Homestead Market Co. day 3 block 42 door 1
- d3 PO IG3320 Bulk Basin Foods: 1 plt $700
- d3 placed parked Inbound Bulk Basin Foods day 3 block 43 door 1
- d3 released 1 order(s)
- d4 PO IG4321 Blue Ridge Provisions: 4 plt $4,426
- d4 placed parked Inbound Blue Ridge Provisions day 4 block 36 door 2
- d4 PO IG4322 Bulk Basin Foods: 1 plt $480
- d4 placed parked Inbound Bulk Basin Foods day 4 block 37 door 3
- d4 PO IG4323 Homestead Market Co.: 1 plt $624
- d4 placed parked Inbound Homestead Market Co. day 4 block 38 door 2
- d4 PO IG4324 Homestead Market Co.: 1 plt $594
- d4 placed parked Inbound Homestead Market Co. day 4 block 40 door 1
- d4 PO IG4325 Fairweather Trading Co.: 11 plt $6,153
- d4 signed bulk BULK_Salty Sailor Pretzel Co._D4_0
- d4 placed parked Inbound Fairweather Trading Co. day 4 block 42 door 1
- d4 PO IG4326 Anchor Point Foods: 2 plt $784
- d4 placed parked Inbound Anchor Point Foods day 4 block 43 door 2
- d4 PO IG4327 Fairweather Trading Co.: 9 plt $5,760
- d4 placed parked Inbound Fairweather Trading Co. day 5 block 0 door 1
- d4 booked Salty Sailor Pretzel Co. day 5 block 22 door 1 (case-pick, 1 picklist(s) / 3 selector(s), 11 pallet pick(s) / 2 reach truck(s): stock ready hour 102 + 4.1h of work)
- d5 PO IG5328 Anchor Point Foods: 1 plt $420
- d5 PO IG5329 Bulk Basin Foods: 1 plt $595
- d5 placed parked Inbound Anchor Point Foods day 5 block 2 door 2
- d5 placed parked Inbound Bulk Basin Foods day 5 block 2 door 3
- d5 released 1 order(s)
- d5 released 1 order(s)
- d5 booked Sneaky Pete's Seafood day 6 block 0 door 1 (ship-short: stock ready hour 118 + 1.5h of work)
- d5 booked Sneaky Pete's Seafood day 6 block 2 door 2 (ship-short: stock ready hour 119 + 1.5h of work)
- d6 released 1 order(s)
- d6 PO IG6330 Bulk Basin Foods: 1 plt $360
- d6 placed parked Inbound Bulk Basin Foods day 6 block 2 door 1
- d6 PO IG6331 Backdoor Bill's Overstock: 1 plt $1,080
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 3 door 1
- d6 PO IG6332 Backdoor Bill's Overstock: 1 plt $288
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 4 door 1
- d6 PO IG6333 Homestead Market Co.: 1 plt $540
- d6 placed parked Inbound Homestead Market Co. day 6 block 6 door 1
- d6 PO IG6334 Anchor Point Foods: 1 plt $480
- d6 placed parked Inbound Anchor Point Foods day 6 block 8 door 1
- d6 PO IG6335 Bulk Basin Foods: 2 plt $885
- d6 placed parked Inbound Bulk Basin Foods day 6 block 9 door 1
- d6 released 1 order(s)
- d6 PO IG6336 Bulk Basin Foods: 3 plt $1,485
- d6 PO IG6337 Homestead Market Co.: 2 plt $2,152
- d6 placed parked Inbound Bulk Basin Foods day 6 block 36 door 2
- d6 placed parked Inbound Homestead Market Co. day 6 block 36 door 3
- d6 booked Sneaky Pete's Seafood day 7 block 0 door 1 (ship-short: stock ready hour 142 + 1.5h of work)
- d6 booked Sneaky Pete's Seafood day 7 block 2 door 2 (ship-short: stock ready hour 143 + 1.5h of work)
- d7 PO IG7338 Fairweather Trading Co.: 12 plt $5,568
- d7 placed parked Inbound Fairweather Trading Co. day 7 block 2 door 1
- d7 PO IG7339 Anchor Point Foods: 1 plt $576
- d7 placed parked Inbound Anchor Point Foods day 7 block 14 door 1
- d7 signed bulk BULK_Neon Nectar Juice Co._D7_0
- d7 PO IG7340 Homestead Market Co.: 2 plt $1,232
- d7 placed parked Inbound Homestead Market Co. day 7 block 36 door 2
- d7 PO IG7341 Anchor Point Foods: 2 plt $1,216
- d7 placed parked Inbound Anchor Point Foods day 7 block 37 door 2
- d7 PO IG7342 Bulk Basin Foods: 1 plt $416
- d7 placed parked Inbound Bulk Basin Foods day 7 block 42 door 1
