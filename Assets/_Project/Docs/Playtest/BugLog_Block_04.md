# Bug log - block 4

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 10:40 | **REPEATING** | x25 and counting: [OrderService] Order OG3356: no reserve or pick-face pallet of 276945 left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
- day 1 13:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 1 task(s) wait
- day 1 17:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 3 task(s) wait
- day 1 18:05 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Martha Miller' as its operator though nobody is on it or running it - clearing the stale link.
- day 1 19:10 | **NO-TASK** | NO-TASK lane pallet 2B-4 sku=903561 load=7990160229
- day 1 19:40 | **NO-TASK** | NO-TASK lane pallet 2B-3 sku=903561 load=8381882419
- day 1 23:25 | **NO-TASK** | NO-TASK lane pallet 2B-3 sku=750426 load=8814789024
- day 2 08:44 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (4.23, 1.11, 37.91) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- day 2 09:25 | **REPEATING** | x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (10.30, 1.11, 32.27) (no waypoint reachable for 8s) — warping to (11.21, 0.01, 39.04) to rejoin patrol.
- day 2 14:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 1 task(s) wait
- day 2 18:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 1 task(s) wait
- day 2 22:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 1 task(s) wait
- day 3 02:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 1 task(s) wait
- day 3 06:40 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 1 task(s) wait
- day 3 10:55 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 1 task(s) wait
- day 3 12:25 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→PO_IG3463 & Truck→PO_IG3462 at (-7.6, 0.0, -51.9)
- day 3 15:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 1 task(s) wait
- day 3 17:10 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Martha Miller' as its operator though nobody is on it or running it - clearing the stale link.
- day 6 23:19 | **REPEATING** | x25 and counting: [TrailerLoad] 1 order(s) at 2A had no staged pallets to load — reverted to Staged.
- day 7 02:27 | **REPEATING** | x25 and counting: [OrderService] Order e05ac114-f1a9-41bb-8216-0d81a58310fd (Blocky Bakeshop) was released to loading from 2A but no staged pallet for it could be found there — reverted to Staged so it can be re-released. Its pallets are most likely sitting somewhere other than the lane.
- day 7 04:21 | **REPEATING** | x25 and counting: [TrailerLoad] 2A: pallet at pos1 belongs to order 74766301-3dc7-4b93-9b33-f1ab0fdb4b0f, not one being loaded here — stopping before it.
