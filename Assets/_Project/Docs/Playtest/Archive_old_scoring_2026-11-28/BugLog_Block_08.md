# Bug log - block 8

Every incident as it happened. Format: day hour:minute | category | what.

- day 3 07:07 | **WARN** | [OrderService] Order OG3325 closed with a STAGED pallet (40 cs) that never made the trailer - not billed, returning it to stock.
- day 3 07:07 | **WARN** | [OrderService] Order OG4326 closed with a IN TRANSIT pallet (38 cs) that never made the trailer - not billed, returning it to stock.
- day 3 07:10 | **NO-TASK** | NO-TASK lane pallet 2A-5 sku=148972 load=5468522926
- day 3 10:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 315s while 1 task(s) wait
- day 3 13:55 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1B-1 (order 1d7a2751-83f5-4a90-910c-6cf0e11dd123 gone)
- day 3 13:55 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1B-1 (order 1d7a2751-83f5-4a90-910c-6cf0e11dd123 gone)
- day 3 14:55 | **FLOATING** | FLOATING OutboundPallet_1d7a2751-83f5-4a90-910c-6cf0e11dd123_2 @1B-1 bottom=2.35
- day 4 05:25 | **NO-TASK** | NO-TASK lane pallet 2A-4 sku=903561 load=7932282862
- day 4 06:35 | **WARN** | [AiNavigation] ReachTruck: stuck at (15.49, 1.16, 33.60) — walking out to (15.49, 0.08, 42.67) (8.14m to mesh).
- day 4 07:05 | **WARN** | [AiNavigation] Dockstocker: stuck at (15.49, 1.16, 33.60) — walking out to (15.49, 0.08, 42.67) (8.14m to mesh).
- day 4 08:12 | **WARN** | [AiNavigation] Dockstocker: stuck at (15.49, 1.16, 33.60) — walking out to (15.49, 0.08, 42.67) (8.14m to mesh).
- day 4 10:03 | **WARN** | [OrderService] Order OG5328 closed with a STAGED pallet (51 cs) that never made the trailer - not billed, returning it to stock.
- day 4 10:10 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1A-1 (order f3eb1dec-d7ed-4ab8-b046-0b4627b011f2 gone)
- day 4 11:55 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 2B-1 (order 2d159039-1e2c-4df9-b433-7bd836a61d6e gone)
- day 5 18:03 | **ERROR** | [OrderSelectionTaskDriver] Pallet jack did not reach 'return pallet pickup' ((16.51, 1.13, 16.14)) after 7.9s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__96:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1269)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  ```
- day 6 12:34 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Mark Bennett' as its operator though nobody is on it or running it - clearing the stale link.
- day 7 17:25 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 405s while 1 task(s) wait
- day 1 11:42 | **EXCEPTION** | ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection.
  ```
  System.Collections.Generic.List`1[T].get_Item (System.Int32 index) (at <c5ae8a4eeb824526b5df2932bc2395e0>:0)
  GameCore.Actors.OrderSelectionTaskDriver+<PickRoutine>d__152.MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.cs:282)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 1 13:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 2 task(s) wait
- day 1 17:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 1 task(s) wait
- day 1 21:40 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 11 task(s) wait
- day 1 23:55 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 10 task(s) wait
- day 2 01:55 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 12 task(s) wait
- day 2 03:40 | **NO-TASK** | NO-TASK lane pallet 2A-5 sku=435689 load=0920000888
- day 2 03:55 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 19 task(s) wait
- day 2 04:25 | **NO-TASK** | NO-TASK lane pallet 1B-5 sku=975031 load=0593778602
- day 2 06:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 23 task(s) wait
- day 2 08:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 28 task(s) wait
- day 2 10:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 30 task(s) wait
- day 2 11:09 | **REPEATING** | x25 and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per 30 s)
- day 2 12:25 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 34 task(s) wait
- day 2 14:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 31 task(s) wait
- day 2 16:10 | **REPEATING** | x25 and counting: [OrderService] Order OG2177: no reserve or pick-face pallet of 903561 left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
- day 2 16:40 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 31 task(s) wait
- day 2 18:40 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 31 task(s) wait
- day 2 19:25 | **NO-TASK** | NO-TASK lane pallet 2E-1 sku=435689 load=2531998897
- day 2 20:40 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 30 task(s) wait
- day 2 22:40 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 30 task(s) wait
- day 3 00:55 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 30 task(s) wait
- day 3 02:55 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 35 task(s) wait
- day 3 05:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 43 task(s) wait
- day 3 06:55 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 43 task(s) wait
- day 3 07:21 | **REPEATING** | x25 and counting: [ReceivingTaskDriver] Employee_Jack Miller: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
- day 3 07:36 | **REPEATING** | x25 and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet e84dd4fd-0b7e-412d-9cfc-8a308858538b but the employee is 3.6m away (max 3m) — refusing to start receiving.
- day 3 07:36 | **REPEATING** | x25 and counting: [ReceivingTaskDriver] Employee_Jack Miller: released Receive task for pallet e84dd4fd-0b7e-412d-9cfc-8a308858538b back to the queue (employee strayed outside receiving range).
- day 3 09:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 43 task(s) wait
- day 3 10:55 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 43 task(s) wait
- day 3 13:25 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 40 task(s) wait
- day 3 13:25 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 40 task(s) wait
- day 3 15:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 40 task(s) wait
- day 3 17:25 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 40 task(s) wait
- day 3 17:25 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 40 task(s) wait
- day 3 19:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 40 task(s) wait
- day 3 21:40 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 40 task(s) wait
- day 3 21:40 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 40 task(s) wait
- day 3 23:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 40 task(s) wait
- day 4 01:40 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 40 task(s) wait
- day 4 01:40 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 40 task(s) wait
- day 4 03:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 40 task(s) wait
- day 4 05:55 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 40 task(s) wait
- day 4 05:55 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 40 task(s) wait
- day 4 07:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 40 task(s) wait
- day 4 10:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 40 task(s) wait
- day 4 10:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 40 task(s) wait
- day 4 11:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 40 task(s) wait
- day 4 14:25 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 39 task(s) wait
- day 4 14:25 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 39 task(s) wait
- day 4 15:40 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 39 task(s) wait
- day 4 18:25 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 38 task(s) wait
- day 4 18:25 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 38 task(s) wait
- day 4 19:40 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 38 task(s) wait
- day 4 22:40 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 4 22:40 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 4 23:40 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 38 task(s) wait
- day 5 02:40 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 38 task(s) wait
- day 5 02:40 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 38 task(s) wait
- day 5 03:40 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 38 task(s) wait
- day 5 06:55 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 5 06:55 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 5 07:55 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 38 task(s) wait
- day 5 11:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 5 11:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 5 12:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 38 task(s) wait
- day 5 15:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 38 task(s) wait
- day 5 15:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 38 task(s) wait
- day 5 16:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 38 task(s) wait
- day 5 19:25 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 5 19:25 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 5 20:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 38 task(s) wait
- day 5 23:40 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 5 23:40 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 6 00:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 38 task(s) wait
- day 6 03:55 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 6 03:55 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 6 04:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 38 task(s) wait
- day 6 07:55 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 38 task(s) wait
- day 6 07:55 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 38 task(s) wait
- day 6 08:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 38 task(s) wait
- day 6 12:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 6 12:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 6 12:40 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 38 task(s) wait
- day 6 16:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 38 task(s) wait
- day 6 16:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 38 task(s) wait
- day 6 16:55 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 38 task(s) wait
- day 6 20:25 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 6 20:25 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 6 21:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 38 task(s) wait
- day 7 00:40 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 7 00:40 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 7 01:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 38 task(s) wait
- day 7 04:40 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 38 task(s) wait
- day 7 04:40 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 38 task(s) wait
- day 7 05:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 38 task(s) wait
- day 7 08:55 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 7 08:55 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 7 09:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 38 task(s) wait
- day 7 13:10 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 7 13:10 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 7 13:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 38 task(s) wait
- day 7 17:25 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 255s while 38 task(s) wait
- day 7 17:25 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 255s while 38 task(s) wait
- day 7 17:40 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 38 task(s) wait
- day 7 21:25 | **VEHICLE-STATIONARY** | Dockstocker has a driver, hasn't moved in 240s while 38 task(s) wait
- day 7 21:25 | **VEHICLE-STATIONARY** | ReachTruck has a driver, hasn't moved in 240s while 38 task(s) wait
- day 7 21:55 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 38 task(s) wait
