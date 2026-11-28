# Bug log - block 2

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
