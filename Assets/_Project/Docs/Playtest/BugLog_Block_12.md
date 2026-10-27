# Bug log - block 12

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 12:53 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 3 task(s) wait
- day 1 13:08 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 3 task(s) wait
- day 1 17:23 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 8 task(s) wait
- day 1 21:23 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 24 task(s) wait
- day 2 01:38 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 16 task(s) wait
- day 2 05:38 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 21 task(s) wait
- day 2 11:23 | **NO-TASK** | NO-TASK lane pallet 2B-3 sku=198432 load=3805796857
- day 2 15:00 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nicole Barton' as its operator though nobody is on it or running it - clearing the stale link.
- day 3 05:15 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nicole Barton' as its operator though nobody is on it or running it - clearing the stale link.
- day 3 15:23 | **NO-TASK** | NO-TASK lane pallet 2B-1 sku=193587 load=0093292567
- day 3 17:38 | **DOOR-DEADLOCK** | OG2405 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 3 23:08 | **DOOR-DEADLOCK** | OG2405 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 4 09:16 | **WARN** | [OrderService] Order OG3406 closed with a IN TRANSIT pallet (23 cs) that never made the trailer - not billed, returning it to stock.
- day 4 09:16 | **WARN** | [OrderService] Order OG3406 closed with a STAGED pallet (29 cs) that never made the trailer - not billed, returning it to stock.
- day 4 09:23 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1A-1 (order c37ce15e-3a15-49ac-ac33-3da07cb03596 gone)
- day 4 09:23 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→Outbound_Door1 & Truck→PO_IG4558 at (-7.0, 0.0, -15.8)
- day 4 11:08 | **FLOATING** | FLOATING OutboundPallet_c37ce15e-3a15-49ac-ac33-3da07cb03596_1 @1A-1 bottom=2.12
- day 4 17:38 | **NO-TASK** | NO-TASK lane pallet 2B-2 sku=172495 load=3010957582
- day 5 07:00 | **REPEATING** | x25 and counting: [DockSchedule] Door 2's 04:00–06:00 slot on day 5 elapsed for PO IG5566 (status=not found) — door freed for rebooking, kept on the grid (IsComplete decides finished vs missed).
- day 5 07:54 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (-19.69, 1.11, 33.02) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- day 5 14:10 | **WARN** | [OrderService] Order OG4411 closed with a IN TRANSIT pallet (31 cs) that never made the trailer - not billed, returning it to stock.
- day 5 14:10 | **WARN** | [OrderService] Order OG7415 closed with a STAGED pallet (32 cs) that never made the trailer - not billed, returning it to stock.
- day 5 14:12 | **ERROR** | [OrderSelectionTaskDriver] No free slot in lane 1C for pallet 1/1 of order 798d3bc1-511f-4b75-b259-43d447f797de (Sneaky Pete's Seafood) — dropping it beside the last one, where the loader will NOT find it. The lane was verified to have room before placement began, so something claimed a slot mid-drop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver:PlacePalletsAtStagingSlot (GameCore.Inventory.OrderData,int,string,UnityEngine.Vector3,UnityEngine.Vector3) (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.cs:603)
  GameCore.Actors.OrderSelectionTaskDriver/<JackStageRoutine>d__61:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:592)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 5 14:23 | **OVERLAP** | OVERLAP Pallet_05_Slot5_Tier0 & OutboundPallet_798d3bc1-511f-4b75-b259-43d447f797de_1 @1C-1
- day 5 14:23 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1C-1 (order 798d3bc1-511f-4b75-b259-43d447f797de gone)
- day 5 17:38 | **NO-TASK** | NO-TASK lane pallet 2A-5 sku=750426 load=5055978417
- day 5 21:09 | **REPEATING** | x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-9.99, 1.11, 26.14) (no waypoint reachable for 8s) — warping to (3.26, 0.01, 31.09) to rejoin patrol.
- day 6 03:08 | **OVERLAP** | OVERLAP Pallet_01_Slot0_Tier1 & Pallet_01_Slot0_Tier1 @(40, 71)
- day 6 14:35 | **WARN** | [OrderService] Order OG5414 closed with a IN TRANSIT pallet (7 cs) that never made the trailer - not billed, returning it to stock.
- day 6 14:35 | **WARN** | [OrderService] Order OG6417 closed with a STAGED pallet (15 cs) that never made the trailer - not billed, returning it to stock.
- day 6 15:38 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1B-1 (order b5aade4c-60f6-487f-9f89-826abf673cba gone)
- day 1 14:10 | **REPEATING** | x25 and counting: Parameter 'IsWaving' does not exist.
- day 1 20:23 | **NO-TASK** | NO-TASK lane pallet 2B-3 sku=864209 load=7173993645
- day 2 09:38 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 615s while 31 task(s) wait
- day 2 11:08 | **NO-TASK** | NO-TASK lane pallet 1C-5 sku=659083 load=8273443677
- day 3 04:23 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→PO_IG2619 & Truck→PO_IG2618 at (-7.6, 0.0, -42.0)
- day 3 12:38 | **DOOR-DEADLOCK** | OG2443 is Staged but door 1's truck has waited 90 min with no one loading it.
- day 3 16:23 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→PO_IG3627 & Truck→PO_IG3626 at (-7.6, 0.0, -41.7)
- day 3 23:11 | **REPEATING** | x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-23.71, 1.11, 34.16) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- day 4 06:19 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (-8.61, 1.11, 40.10) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- day 4 16:23 | **DOOR-DEADLOCK** | OG3444 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 4 17:16 | **WARN** | [OrderService] Order OG3444 closed with a STAGED pallet (26 cs) that never made the trailer - not billed, returning it to stock.
- day 4 17:23 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1B-1 (order 78321068-a89c-4fe6-98a0-322d23810771 gone)
- day 5 03:08 | **NO-TASK** | NO-TASK lane pallet 1B-5 sku=482913 load=3229705121
- day 5 04:08 | **NO-TASK** | NO-TASK lane pallet 1B-3 sku=482913 load=6054209082
- day 5 15:23 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 10 task(s) wait
- day 5 15:53 | **NO-TASK** | NO-TASK lane pallet 1C-2 sku=304759 load=5466466056
- day 5 17:23 | **DOOR-DEADLOCK** | OG5447 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 5 19:23 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 5 task(s) wait
- day 5 23:23 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 3 task(s) wait
- day 6 01:08 | **DOOR-DEADLOCK** | OG5447 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 6 03:23 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 3 task(s) wait
- day 6 07:38 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 3 task(s) wait
- day 6 11:53 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 3 task(s) wait
- day 6 15:53 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 5 task(s) wait
- day 6 19:53 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 5 task(s) wait
- day 7 00:08 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 5 task(s) wait
- day 7 04:23 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 5 task(s) wait
- day 7 08:38 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 5 task(s) wait
- day 7 12:38 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 5 task(s) wait
- day 7 16:53 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 7 task(s) wait
- day 7 20:53 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 7 task(s) wait
