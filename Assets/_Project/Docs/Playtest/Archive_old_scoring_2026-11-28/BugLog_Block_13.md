# Bug log - block 13

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 12:53 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 3 task(s) wait
- day 1 16:53 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 12 task(s) wait
- day 1 20:53 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 19 task(s) wait
- day 1 23:08 | **NO-TASK** | NO-TASK lane pallet 2A-3 sku=564219 load=7715743255
- day 2 00:08 | **NO-TASK** | NO-TASK lane pallet 2A-1 sku=564219 load=5282999646
- day 2 01:08 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 16 task(s) wait
- day 2 05:23 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 8 task(s) wait
- day 2 09:38 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 3 task(s) wait
- day 2 12:38 | **REPEATING** | x25 and counting: [ReceivingTaskDriver] Employee_Maria Edwards: task flagged in-progress but the workflow is Idle — clearing the stale flag and resuming (deadlock recovered).
- day 2 14:53 | **NO-TASK** | NO-TASK lane pallet 1C-5 sku=435689 load=8029595860
- day 2 15:38 | **NO-TASK** | NO-TASK lane pallet 1C-4 sku=435689 load=4682411950
- day 2 16:23 | **NO-TASK** | NO-TASK lane pallet 1C-3 sku=435689 load=7336494197
- day 2 17:10 | **WARN** | [OrderService] Order OG4428 closed with a STAGED pallet (45 cs) that never made the trailer - not billed, returning it to stock.
- day 2 17:10 | **WARN** | [OrderService] Order OG2426 closed with a IN TRANSIT pallet (39 cs) that never made the trailer - not billed, returning it to stock.
- day 2 22:08 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1A-1 (order 6db90509-1f3e-474a-a2d5-437b58fa42af gone)
- day 2 22:21 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Patricia Hamilton' as its operator though nobody is on it or running it - clearing the stale link.
- day 4 09:38 | **NO-TASK** | NO-TASK lane pallet 2B-5 sku=118432 load=6426321843
- day 4 10:38 | **NO-TASK** | NO-TASK lane pallet 2B-3 sku=118432 load=0594083258
- day 4 11:08 | **NO-TASK** | NO-TASK lane pallet 2B-2 sku=118432 load=2563586133
- day 4 11:38 | **NO-TASK** | NO-TASK lane pallet 2A-2 sku=999999 load=7658651867
- day 4 12:38 | **NO-TASK** | NO-TASK lane pallet 2C-5 sku=999999 load=7088893445
- day 4 15:30 | **WARN** | [OrderService] Order OG3427 closed with a IN TRANSIT pallet (30 cs) that never made the trailer - not billed, returning it to stock.
- day 4 15:30 | **WARN** | [OrderService] Order OG4431 closed with a STAGED pallet (16 cs) that never made the trailer - not billed, returning it to stock.
- day 4 16:08 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1C-1 (order a75c3580-3d69-4b9b-ae3d-9740b82fdc6a gone)
- day 5 00:03 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (10.35, 1.11, 39.80) (no waypoint reachable for 8s) — warping to (11.21, 0.01, 39.04) to rejoin patrol.
- day 6 16:12 | **WARN** | [OrderService] Order OG6436 closed with a STAGED pallet (8 cs) that never made the trailer - not billed, returning it to stock.
- day 6 16:12 | **WARN** | [OrderService] Order OG6436 closed with a IN TRANSIT pallet (28 cs) that never made the trailer - not billed, returning it to stock.
- day 6 19:08 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1C-1 (order 1bffc181-572b-4f66-b91c-d25865baa7fc gone)
- day 7 00:39 | **REPEATING** | x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-20.50, 1.11, 39.60) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
