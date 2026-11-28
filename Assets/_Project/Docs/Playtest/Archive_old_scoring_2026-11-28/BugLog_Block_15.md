# Bug log - block 15

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 08:00 | **WARN** | To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.
- day 1 08:00 | **REPEATING** | x25 and counting: Internal: JobTempAlloc has allocations that are more than the maximum lifespan of 4 frames old - this is not allowed and likely a leak
- day 1 08:00 | **REPEATING** | x25 and counting: To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.
- day 1 16:28 | **NO-TASK** | NO-TASK lane pallet 2A-2 sku=152431 load=9183167166
- day 1 17:43 | **NO-TASK** | NO-TASK lane pallet 2B-5 sku=152431 load=0572025675
- day 1 17:47 | **NO-TASK** | NO-TASK lane pallet 2B-5 sku=152431 load=0572025675
- day 1 18:38 | **NO-TASK** | NO-TASK lane pallet 1A-4 sku=482913 load=8058858671
- day 1 17:53 | **NO-TASK** | NO-TASK lane pallet 1A-2 sku=975031 load=6852029725
- day 1 19:23 | **NO-TASK** | NO-TASK lane pallet 2A-4 sku=517634 load=1367117030
- day 1 22:53 | **NO-TASK** | NO-TASK lane pallet 2A-2 sku=517634 load=8229575358
- day 1 23:53 | **NO-TASK** | NO-TASK lane pallet 2A-1 sku=517634 load=6312255919
- day 3 14:54 | **WARN** | [OrderService] Order OG2461 closed with a STAGED pallet (14 cs) that never made the trailer - not billed, returning it to stock.
- day 3 15:08 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1B-1 (order d256afe3-dc10-4f67-bb37-19c3e4e5e270 gone)
- day 3 15:08 | **TRUCK-OVERLAP** | TRUCK-OVERLAP Truck→Outbound_Door1 & Truck→PO_IG3671 at (-7.6, 0.0, -52.8)
- day 3 15:23 | **NO-TASK** | NO-TASK lane pallet 2A-5 sku=856320 load=9503904529
- day 4 08:52 | **REPEATING** | x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (13.86, 1.16, 27.11) (no waypoint reachable for 8s) — warping to (3.26, 0.01, 31.09) to rejoin patrol.
- day 4 13:30 | **REPEATING** | x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-25.01, 1.11, 32.38) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- day 5 01:04 | **EXCEPTION** | MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it.
  ```
  UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.Transform.get_position () (at <d5ea965f39d74501b56f4006ef80da34>:0)
  GameCore.Actors.ReachTruckOperator+<DeliverPalletToRack>d__93.MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:1260)
  UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <d5ea965f39d74501b56f4006ef80da34>:0)
  UnityEngine.StackTraceUtility:ExtractStringFromExceptionInternal(Object, String&, String&)
  ```
- day 5 06:53 | **NO-TASK** | NO-TASK lane pallet 2B-1 sku=304759 load=3675066330
- day 5 08:53 | **NO-TASK** | NO-TASK lane pallet 2A-1 sku=193587 load=4542814119
- day 5 09:08 | **NO-TASK** | NO-TASK lane pallet 2C-3 sku=193587 load=4084972791
- day 5 09:13 | **WARN** | [OrderService] Order OG5469 closed with a STAGED pallet (60 cs) that never made the trailer - not billed, returning it to stock.
- day 5 09:13 | **WARN** | [OrderService] Order OG5469 closed with a IN TRANSIT pallet (19 cs) that never made the trailer - not billed, returning it to stock.
- day 5 10:23 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1C-1 (order 4c0b405e-79ce-47f1-9e77-1bcb174ef868 gone)
- day 7 15:45 | **WARN** | [OrderService] Order OG7474 closed with a STAGED pallet (44 cs) that never made the trailer - not billed, returning it to stock.
- day 7 15:53 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1B-1 (order 3e99289a-a68b-4cc3-8a53-302d30b6e49b gone)
- day 1 20:08 | **NO-TASK** | NO-TASK lane pallet 1B-5 sku=113489 load=0245169499
- day 1 20:08 | **NO-TASK** | NO-TASK lane pallet 1A-1 sku=113489 load=5660366934
- day 1 21:38 | **NO-TASK** | NO-TASK lane pallet 2B-5 sku=792146 load=8870436857
- day 1 22:53 | **NO-TASK** | NO-TASK lane pallet 1A-5 sku=102435 load=1610967378
- day 1 23:27 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Daniel Sanders' as its operator though nobody is on it or running it - clearing the stale link.
- day 2 05:38 | **NO-TASK** | NO-TASK lane pallet 2A-4 sku=248905 load=0746061575
- day 2 06:38 | **NO-TASK** | NO-TASK lane pallet 2B-5 sku=304759 load=4119558700
- day 2 06:48 | **WARN** | [OrderService] Order OG1496 closed with a STAGED pallet (34 cs) that never made the trailer - not billed, returning it to stock.
- day 2 06:53 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1D-1 (order 7e94fd20-ada5-42cf-b294-4134ddbdd87c gone)
- day 2 06:53 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1D-1 (order 7e94fd20-ada5-42cf-b294-4134ddbdd87c gone)
- day 2 09:23 | **ORPHAN-OUTBOUND** | ORPHAN-OUTBOUND 1C-1 (order b5946e3f-220b-4129-b6c4-6f2bc3d6e1bd gone)
- day 2 19:55 | **WARN** | [AiNavigation] ReachTruck: stuck at (5.69, 1.16, 20.76) — walking out to (1.22, 0.08, 12.28) (8.65m to mesh).
- day 3 01:56 | **WARN** | [AiNavigation] ReachTruck: stuck at (8.31, 1.16, 25.50) — walking out to (8.31, 0.08, 12.17) (12.39m to mesh).
- day 3 08:49 | **WARN** | [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Daniel Sanders' as its operator though nobody is on it or running it - clearing the stale link.
- day 4 17:38 | **DOOR-DEADLOCK** | OG4499 is Loading but door 1's truck has waited 90 min with no one loading it.
- day 4 23:38 | **DOOR-DEADLOCK** | OG4499 is Loading but door 1's truck has waited 90 min with no one loading it.
