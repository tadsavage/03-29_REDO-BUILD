# Appointment isolation review and fix plan (2026-10-31)

Golden rule (Tad): ONE order per appointment block. An appointment (door + day + block) is its own island. Nothing from the
12:00 block at Door 2 may touch the 10:00 block at Door 2, and nothing at Door 3 may touch Door 2. The linking key everywhere
must be the **appointment id**, never a door number, a customer, a contract or "whatever is at this door".

## Findings (all verified in code)

### A. Orders get attached to the wrong appointment, or several to one (DockScheduleService.cs)
- A1 `HandleOrderArrived` (~1410): an arriving order is appended to an existing appointment (`existing.OrderIds.Add`) when it
  "fits" (`FitsWith`). Several orders per appointment, matched by customer + contract, not by one-to-one booking.
- A2 Same method, bulk branch: `candidates.FirstOrDefault(a => FitsWith(a, order))` takes ANY appointment of that contract on
  ANY day/block. Pure bleed.
- A3 `ReconcileFutureRecurringAppointments` (~1572): `target = sameDay.FirstOrDefault(block matches) ?? sameDay[0]`. When the
  requested block has no appointment it falls back to the first appointment that day (the 10:00 trailer takes the 12:00 order),
  and it strips the order from every other appointment.
- A4 `TryBookGroupAtDoor` (~1724): `booked.OrderIds.AddRange(group.OrderIds)` and `UnscheduledGroups` groups by customer|contract,
  so one pool box books several orders onto one appointment.
- A5 `TryPlanStageSpread` (OrderService ~1060): groups by (customer, dock door). Two appointments at the same door for the same
  customer (10:00 and 12:00) are merged into one stage plan sharing lanes.
- A6 `AssignWaitingLanes` (OrderService ~1660): groups by (customer, AssignedDoorNumber); same merge across blocks.

### B. A staging door becomes a dock door (OrderService.cs) - ROOT CAUSE of the OG4586 / Door 3 case
- B1 `TryPlanStageSpread`: `if (group.DockDoor <= 0) group.DockDoor = found[0].door; // unscheduled: dock where it stages`.
- B2 `ReleaseOrdersToLanes` (~1219): `AssignedDoorNumber = dockDoor > 0 ? dockDoor : lanes[0].door`.
- B3 `ReleaseOrdersWaitingForLane` (~1380): `AssignedDoorNumber = dockDoor` (can be 0).
- B4 `AllocateHeldWork` (~613), (~1331) and `AssignLaneToOrders` (~1401): `if (AssignedDoorNumber <= 0) AssignedDoorNumber = <staging door>`.
- Effect: an order with no appointment is silently given the door its freight happens to stage at, then every door-keyed check
  treats it as belonging to whichever trailer is at that door.

### C. Everything downstream is keyed by DOOR NUMBER, not appointment
- C1 Load tasks: `ToLocation = doorNumber.ToString()` (OrderService ~2290). No appointment id. `TrailerLoadController`
  (~149-157) finds the truck by `FindDockedOutboundTruck(dockDoor)`. Back-to-back appointments at one door share the same key.
- C2 `TrailerLoadController.AutoFileLoadTasks` (~332-373): groups orders by `AssignedDoorNumber`; my 2026-10-31 change now limits
  it to the truck's appointment, but still keyed through the door lookup.
- C3 `FindStagedPalletsInLane` (~669): `loadingIds` = every Loading/Loaded order at the dock door, so a second appointment's
  pallets in the same lane count as "ours".
- C4 `TrailerLoadController.DigOut` (~77): `FindDockedOutboundTruck(dstDoor)`.
- C5 `OutboundTruckDispatchService.ScanStagedPallets`: dispatches by `order.AssignedDoorNumber`; `SpawnOutboundTruck(door)` with
  no appointment id (also from `OrderService.ReleaseOrdersToLoading`) creates an "unattributed" truck.
- C6 `TruckOrderColors.ResolveOutboundAppointment` (~75-84): an unattributed truck guesses "today's appointment at this door".
- C7 `WorldHoverPopupUI` (~1693): no-appointment fallback lists every order at the door; this is why the card shows a different order than the trailer holds.
- C8 `OrderService`: `TryReleaseDoorIfClear`, `ReleaseFullyShippedTrucks`, `OrdersRidingTruck`, `FindScratchReadyDoors`, AutoClose
  "driver waiting" message all matched on door number. 2026-10-31 edits made them appointment-first with a door fallback; the
  fallback is still the old bug when a truck has no appointment id.
- C9 `ReleaseOrdersToLoading` (~2214) requires one customer + one door per batch, not one appointment.
- C10 Work Queue (`WorkQueuePanel` ~2160-2212) batches and labels by `AssignedDoorNumber`; row "To" shows "Door N" from the order, not the appointment.

### D. The 15 min/pallet allowance is only partly in place
- Done 2026-10-31: `DockScheduleService.AllowedMinutes` (max(block span, pallets x 15)) now decides when an appointment is
  "elapsed" in `SweepElapsedAppointments` (outbound fines; inbound no-show). Inbound offload Standard is 60 + 15/pallet
  (`TruckController`).
- NOT using it (still block end): 
  - `TruckController.TryMinutesToWindowEnd` (~3499): drives the outbound dwell bar ("X minutes until $fine"), AND the
    "driver leaves" cutoff (`toEnd <= -OutboundGraceMinutes`, ~3493). Trucks still leave at block end + grace regardless of pallets.
  - `DockScheduleService.IsLocked`-style check (~648) uses `LastBlockIndex`.
  - `SchedulerPanel` ~4450 ("considered late" / late fee time) and `WorldHoverPopupUI` ~1548 ("@ EndHour") display block end.
  - `OrderService` day-end late fee (`OnDayChanged`) is by due day (fine).
  - `FineMissedDeadline(order, appt.EndHour)` message text prints the block end.
- Scoring: `PlaytestBot` / HAL read `OrderService.OnOrderFined`, so the score follows whatever the fines say. Once the fines and
  the truck cutoff use one deadline the score is right. Needs one shared helper so every consumer agrees.

## Fix plan (in order)

1. **One deadline function.** `DockScheduleService.DeadlineMinute(appt)` = day x 1440 + StartHour x 60 + AllowedMinutes (one order, its
   pallets x 15, at least the block). Use it in: sweep, `TruckController.TryMinutesToWindowEnd` (bar + departure cutoff), IsLocked,
   SchedulerPanel late label, WorldHoverPopupUI countdown, `FineMissedDeadline` text.
2. **Never borrow a door.** Remove B1-B4. An order with no appointment (no dock door) cannot be released to the floor; it waits in
   the pending pool. Release requires `FindForOrder(order)` to exist.
3. **One order per appointment.** A1-A4: an arriving order books ITS OWN appointment (exact day + block, or the next free
   block/door); never `OrderIds.Add` onto another order's appointment; never fall back to `sameDay[0]`; pool books one order per
   appointment. Enforce with an assert in `DockAppointment` add paths (log + refuse if OrderIds.Count would exceed 1).
4. **Appointment id as the key.** Load tasks carry the appointment id (new field on `WorkTask`, e.g. `AppointmentId`, saved);
   `TrailerLoadController`, DigOut, `FindStagedPalletsInLane`, dispatch, popup and Work Queue resolve the truck/orders from
   that id. Trucks are always spawned with an appointment id; delete the door-guess fallbacks (C6, C7, C8 fallback).
5. **Stage planning per appointment.** A5/A6: group by appointment, not (customer, door).
6. **Heal old saves.** On load, for each order: if it is on more than one appointment or on a different door than its appointment,
   re-home it to its own appointment's door; un-stick trucks holding orders that are not theirs.
7. **Verify** with `QAHealthCheck`: add checks for (orders per appointment > 1), (order.AssignedDoorNumber != appointment door),
   (Load task door != appointment door), (truck holding pallets of an order not on its appointment). Play a block at speed 5.

## Open question for Tad
- Orders from one customer that arrive together (same contract and block): confirm each gets its own appointment/trailer
  (that is the golden rule), even though it uses more door capacity.
