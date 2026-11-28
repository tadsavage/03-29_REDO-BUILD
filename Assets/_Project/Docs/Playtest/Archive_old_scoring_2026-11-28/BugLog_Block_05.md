# Bug log - block 5

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 10:10 | **REPEATING** | x25 and counting: [OrderService] Order OG1370: no reserve or pick-face pallet of 638412 left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
- day 1 13:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 255s while 1 task(s) wait
- day 1 14:25 | **NO-TASK** | NO-TASK lane pallet 1B-3 sku=193587 load=4671372066
- day 1 14:55 | **NO-TASK** | NO-TASK lane pallet 1B-2 sku=193587 load=5271060458
- day 1 16:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 9 task(s) wait
- day 1 17:25 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 14 task(s) wait
- day 1 18:40 | **WARN** | [AiNavigation] ReachTruck: stuck at (15.49, 1.16, 33.60) — walking out to (15.49, 0.08, 42.67) (8.14m to mesh).
- day 1 19:40 | **NO-TASK** | NO-TASK lane pallet 2B-2 sku=148972 load=5979410827
- day 1 20:40 | **NO-TASK** | NO-TASK lane pallet 2C-3 sku=193587 load=9594508000
- day 1 20:43 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nicholas Cartwright' as its operator though nobody is on it or running it - clearing the stale link.
- day 1 21:25 | **NO-TASK** | NO-TASK lane pallet 2C-2 sku=193587 load=5568382277
- day 2 08:18 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nicholas Cartwright' as its operator though nobody is on it or running it - clearing the stale link.
- day 2 13:52 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (-25.70, 1.11, 29.88) (no waypoint reachable for 8s) — warping to (3.26, 0.01, 31.09) to rejoin patrol.
- day 2 16:14 | **REPEATING** | x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-23.89, 1.11, 34.18) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- day 2 19:29 | **REPEATING** | x25 and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per 30 s)
- day 3 09:10 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 2 task(s) wait
- day 3 14:40 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 330s while 1 task(s) wait
- day 3 15:46 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Nicholas Cartwright' as its operator though nobody is on it or running it - clearing the stale link.
- day 4 02:08 | **WARN** | [OrderService] Order OG3368 closed with a STAGED pallet (35 cs) that never made the trailer - not billed, returning it to stock.
- day 4 02:08 | **WARN** | [OrderService] Order OG4372 closed with a IN TRANSIT pallet (23 cs) that never made the trailer - not billed, returning it to stock.
- day 4 02:10 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1B-5 (order f734d5b4-62f3-430a-ab98-95ee9169cf73 gone)
- day 4 02:21 | **REPEATING** | x25 and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.
- day 4 02:55 | **VEHICLE-STATIONARY** | PalletJack has a driver, hasn't moved in 240s while 4 task(s) wait
- day 4 06:25 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1B-1 (order 020890c4-cbfa-4f5f-8b24-408aa8115191 gone)
- day 4 06:25 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1B-1 (order 020890c4-cbfa-4f5f-8b24-408aa8115191 gone)
- day 4 09:24 | **WARN** | [OrderService] Order OG5375 closed with a IN TRANSIT pallet (8 cs) that never made the trailer - not billed, returning it to stock.
- day 4 09:24 | **WARN** | [OrderService] Order OG3369 closed with a STAGED pallet (8 cs) that never made the trailer - not billed, returning it to stock.
- day 4 09:25 | **TRUCK-STALLED** | TRUCK-STALLED Truck→Outbound_Door1 state=DepartToApproach 270s at (7.2, 0.0, 6.7)
- day 4 16:40 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1C-3 (order 2b5909a0-db55-47c4-96e4-6fa11cc928b5 gone)
- day 4 16:40 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1C-3 (order 2b5909a0-db55-47c4-96e4-6fa11cc928b5 gone)
- day 5 05:17 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] Return: no slot or pallet of 172495 left to put a case back into - the case is written off.
- day 5 09:10 | **FLOATING** | FLOATING OutboundPallet_2b5909a0-db55-47c4-96e4-6fa11cc928b5_2 @1C-3 bottom=2.20
- day 7 04:20 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: no progress toward task target (17.49, 1.13, 39.04) for 10s (stopped Infinitym short, path PathComplete) — treating as arrived.
- day 7 14:25 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→PO_IG7504 & Truck→PO_IG7505 at (-7.6, 0.0, -50.7)
- day 1 11:42 | **EXCEPTION** | ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection.
  ```
  System.Collections.Generic.List`1[T].get_Item (System.Int32 index) (at <c5ae8a4eeb824526b5df2932bc2395e0>:0)
  GameCore.Actors.OrderSelectionTaskDriver+<PickRoutine>d__152.MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.cs:282)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
