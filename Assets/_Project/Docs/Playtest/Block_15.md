# Playtest block 15 — days 1–8

## Score: **55.6 (+16.3)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (11 shipped, 11 late) | 15% | 35.3 (+27.0) |
| Fill rate | 10% | 60.1 (-7.6) |
| Throughput (93 plt vs target 84) | 15% | 100.0 (+32.1) |
| Revenue ($55,290) | 15% | 98.7 (+36.5) |
| Profit (cash + stock -37,808) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (+17.6) |
| Reliability (bugs) | 25% | 28.1 (+7.4) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **11**, late/fined **11**, pallets shipped **93**. Cash $139,040 → $24,414 (-114,626). Stock at cost $0 → $76,818; cash + stock -37,808.
Bot did: 12 contracts signed, 44 dock bookings, 11 orders released, 39 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **6**
  - VEHICLE-STATIONARY|Dockstocker has a driver, hasn't moved in #s while # task(s) wait
  - DOOR-DEADLOCK|OG# is Loading but door #'s truck has waited # min with no one loading it.
  - TRUCK-OVERLAP| Truck→PO_IG# & Truck→PO_IG# at (#, #, #)
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it.
  - REPEATING|x# and counting: Parameter 'IsWaving' does not exist.
  - ORPHAN-OUTBOUND| #B# (order #-a#c#fe##a### gone)
- New this block: **7**
  - NO-TASK| lane pallet #A# sku=# load=#
  - EXCEPTION|MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  - ORPHAN-OUTBOUND| #B# (order #-dc##f#-bb### gone)
  - TRUCK-OVERLAP| Truck→Outbound_Door# & Truck→PO_IG# at (#, #, #)
  - ORPHAN-OUTBOUND| #C# (order ##ce#f##e### gone)
  - ORPHAN-OUTBOUND| #B# (order #-a#b#cc##a### gone)
  - WARN|[OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock.

## Bugs found (12 distinct, 19 incidents)

| Incidents | Bug |
|---|---|
| 6 | NO-TASK —  lane pallet #A# sku=# load=# |
| 1 | EXCEPTION — MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it. |
| 3 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | ORPHAN-OUTBOUND —  #B# (order #-dc##f#-bb### gone) |
| 1 | TRUCK-OVERLAP —  Truck→Outbound_Door# & Truck→PO_IG# at (#, #, #) |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | NO-TASK —  lane pallet #B# sku=# load=# |
| 1 | NO-TASK —  lane pallet #C# sku=# load=# |
| 1 | ORPHAN-OUTBOUND —  #C# (order ##ce#f##e### gone) |
| 1 | ORPHAN-OUTBOUND —  #B# (order #-a#b#cc##a### gone) |
| 1 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **NO-TASK** (day 1): NO-TASK lane pallet 1A-2 sku=975031 load=6852029725
- **WARN** (day 3): [OrderService] Order OG2461 closed with a STAGED pallet (14 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 3): ORPHAN-OUTBOUND 1B-1 (order d256afe3-dc10-4f67-bb37-19c3e4e5e270 gone)
- **TRUCK-OVERLAP** (day 3): TRUCK-OVERLAP Truck→Outbound_Door1 & Truck→PO_IG3671 at (-7.6, 0.0, -52.8)
- **REPEATING** (day 4): x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (13.86, 1.16, 27.11) (no waypoint reachable for 8s) — warping to (3.26, 0.01, 31.09) to rejoin patrol.
- **REPEATING** (day 4): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-25.01, 1.11, 32.38) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **EXCEPTION** (day 5): MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_position () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  GameCore.Actors.ReachTruckOperator+<DeliverPalletToRack>d__93.MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:1260)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  ```
- **NO-TASK** (day 5): NO-TASK lane pallet 2B-1 sku=304759 load=3675066330
- **NO-TASK** (day 5): NO-TASK lane pallet 2C-3 sku=193587 load=4084972791
- **WARN** (day 5): [OrderService] Order OG5469 closed with a IN TRANSIT pallet (19 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 5): ORPHAN-OUTBOUND 1C-1 (order 4c0b405e-79ce-47f1-9e77-1bcb174ef868 gone)
- **ORPHAN-OUTBOUND** (day 7): ORPHAN-OUTBOUND 1B-1 (order 3e99289a-a68b-4cc3-8a53-302d30b6e49b gone)

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1652 Bulk Basin Foods: 6 plt $3,354
- d1 placed parked Inbound Bulk Basin Foods day 1 block 5 door 1
- d1 PO IG1653 Bulk Basin Foods: 1 plt $520
- d1 placed parked Inbound Bulk Basin Foods day 1 block 5 door 2
- d1 PO IG1654 Blue Ridge Provisions: 4 plt $5,844
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 6 door 1
- d1 PO IG1655 Homestead Market Co.: 3 plt $3,332
- d1 signed bulk BULK_The Big Cheese Inc._D1_0
- d1 signed bulk BULK_Honest Harvest Rice_D1_1
- d1 placed parked Inbound Homestead Market Co. day 1 block 6 door 2
- d1 PO IG1656 Homestead Market Co.: 1 plt $1,840
- d1 placed parked Inbound Homestead Market Co. day 1 block 7 door 1
- d1 PO IG1657 Bulk Basin Foods: 4 plt $2,748
- d1 placed parked Inbound Bulk Basin Foods day 1 block 7 door 2
- d1 booked The Big Cheese Inc. day 1 block 11 door 1 (pallet-only: stock ready hour 19 + 1.5h of work)
- d1 PO IG1658 Fairweather Trading Co.: 14 plt $10,578
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 8 door 1
- d1 booked Honest Harvest Rice day 2 block 0 door 1 (pallet-only: stock ready hour 21 + 1.5h of work)
- d1 PO IG1659 Bulk Basin Foods: 2 plt $224
- d1 placed parked Inbound Bulk Basin Foods day 1 block 8 door 2
- d1 released 1 order(s)
- d1 released 1 order(s)
- d2 PO IG2660 Golden Grove Gourmet: 6 plt $11,555
- d2 placed parked Inbound Golden Grove Gourmet day 2 block 1 door 1
- d2 PO IG2661 Bulk Basin Foods: 5 plt $2,828
- d2 placed parked Inbound Bulk Basin Foods day 2 block 1 door 2
- d2 PO IG2662 Homestead Market Co.: 2 plt $1,116
- d2 placed parked Inbound Homestead Market Co. day 2 block 2 door 1
- d2 PO IG2663 Anchor Point Foods: 1 plt $720
- d2 placed parked Inbound Anchor Point Foods day 2 block 2 door 2
- d2 PO IG2664 Bulk Basin Foods: 5 plt $2,688
- d2 placed parked Inbound Bulk Basin Foods day 2 block 3 door 1
- d2 released 1 order(s)
- d2 PO IG2665 Homestead Market Co.: 1 plt $486
- d2 placed parked Inbound Homestead Market Co. day 2 block 4 door 1
- d2 signed bulk BULK_Honest Harvest Rice_D2_0
- d2 PO IG2666 Bulk Basin Foods: 7 plt $4,410
- d2 placed parked Inbound Bulk Basin Foods day 2 block 10 door 1
- d3 PO IG3667 Backdoor Bill's Overstock: 3 plt $816
- d3 PO IG3668 Homestead Market Co.: 3 plt $1,398
- d3 PO IG3669 Blue Ridge Provisions: 6 plt $4,804
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 1 door 1
- d3 placed parked Inbound Homestead Market Co. day 3 block 1 door 2
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 2 door 1
- d3 PO IG3670 Bulk Basin Foods: 3 plt $976
- d3 placed parked Inbound Bulk Basin Foods day 3 block 2 door 2
- d3 released 1 order(s)
- d3 signed bulk BULK_Mama Tomato Sauces_D3_0
- d3 signed bulk BULK_Crunch-Time Cereals_D3_1
- d3 PO IG3671 Bulk Basin Foods: 7 plt $2,800
- d3 placed parked Inbound Bulk Basin Foods day 3 block 6 door 1
- d3 booked Mama Tomato Sauces day 3 block 10 door 1 (pallet-only: stock ready hour 65 + 1.5h of work)
- d3 released 1 order(s)
- d3 PO IG3672 Bulk Basin Foods: 3 plt $352
- d3 PO IG3673 Blue Ridge Provisions: 5 plt $3,600
- d3 PO IG3674 Homestead Market Co.: 3 plt $1,659
- d3 placed parked Inbound Bulk Basin Foods day 3 block 10 door 2
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 11 door 1
- d3 placed parked Inbound Homestead Market Co. day 3 block 11 door 2
- d3 booked Crunch-Time Cereals day 4 block 3 door 1 (pallet-only: stock ready hour 75 + 1.5h of work)
- d4 released 1 order(s)
- d4 released 1 order(s)
- d4 signed bulk BULK_The Happy Herd Co._D4_0
- d4 signed bulk BULK_The Happy Herd Co._D4_1
- d4 PO IG4675 Bulk Basin Foods: 4 plt $1,456
- d4 placed parked Inbound Bulk Basin Foods day 4 block 10 door 1
- d4 booked The Happy Herd Co. day 5 block 3 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 97 + 3.3h of work)
- d4 PO IG4676 Homestead Market Co.: 8 plt $12,160
- d4 PO IG4677 Blue Ridge Provisions: 6 plt $5,760
- d4 placed parked Inbound Homestead Market Co. day 4 block 10 door 2
- d4 placed parked Inbound Blue Ridge Provisions day 4 block 11 door 1
- d4 released 1 order(s)
- d5 PO IG5678 Fairweather Trading Co.: 9 plt $6,530
- d5 PO IG5679 Bulk Basin Foods: 5 plt $1,040
- d5 PO IG5680 Backdoor Bill's Overstock: 2 plt $576
- d5 placed parked Inbound Fairweather Trading Co. day 5 block 1 door 1
- d5 placed parked Inbound Bulk Basin Foods day 5 block 1 door 2
- d5 placed parked Inbound Backdoor Bill's Overstock day 5 block 2 door 1
- d5 released 1 order(s)
- d5 PO IG5681 Homestead Market Co.: 3 plt $1,824
- d5 placed parked Inbound Homestead Market Co. day 5 block 5 door 1
- d5 signed bulk BULK_Rooted Robotics_D5_0
- d6 PO IG6682 Blue Ridge Provisions: 3 plt $2,308
- d6 PO IG6683 Anchor Point Foods: 1 plt $672
- d6 placed parked Inbound Blue Ridge Provisions day 6 block 1 door 1
- d6 placed parked Inbound Anchor Point Foods day 6 block 1 door 2
- d6 released 1 order(s)
- d6 signed bulk BULK_Puff & Stuff Pastries_D6_0
- d6 signed bulk BULK_Odd-Shape Orchards_D6_1
- d6 PO IG6684 Bulk Basin Foods: 6 plt $960
- d6 placed parked Inbound Bulk Basin Foods day 6 block 10 door 1
- d6 PO IG6685 Anchor Point Foods: 1 plt $476
- d6 placed parked Inbound Anchor Point Foods day 6 block 10 door 2
- d6 PO IG6686 Blue Ridge Provisions: 4 plt $2,176
- d6 placed parked Inbound Blue Ridge Provisions day 6 block 11 door 1
- d6 PO IG6687 Bulk Basin Foods: 5 plt $1,312
- d6 PO IG6688 Anchor Point Foods: 1 plt $624
- d6 placed parked Inbound Bulk Basin Foods day 6 block 11 door 2
- d6 placed parked Inbound Anchor Point Foods day 7 block 0 door 1
- d7 released 1 order(s)
- d7 PO IG7689 Fairweather Trading Co.: 13 plt $6,656
- d7 PO IG7690 Homestead Market Co.: 2 plt $896
- d7 placed parked Inbound Fairweather Trading Co. day 7 block 5 door 1
- d7 placed parked Inbound Homestead Market Co. day 7 block 5 door 2
- d7 signed bulk BULK_Neon Nectar Juice Co._D7_0
