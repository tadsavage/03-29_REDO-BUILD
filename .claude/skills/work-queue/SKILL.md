---
description: Working on the Work Queue — the WorkQueueSystem task model and the WorkQueuePanel (key 7) modal, including columns, filters, sorting, row phases, priority steppers, release/close-out bottom bar, and the 250ms live-signature refresh. Use whenever the task mentions the Work Queue, work tasks, task priority, task claiming, Release Orders, row phases (Open/Available/Assigned/Staged/Loading/Loaded), queue columns/filters, or files WorkQueuePanel.cs / WorkQueueSystem.cs / OrderSelectionTaskDriver.cs / ReceivingTaskDriver.cs.
---

# Work Queue (task model + panel)

Pair this with the `unity:ui-uitk` skill for general UI Toolkit rules, but see "Project overrides" below, because this panel does NOT follow its UXML/USS-only guidance.

## Files
- `Assets/_Project/Scripts/1. FSM/Core/Labor/WorkQueueSystem.cs` (~435 lines) — `IService`, registered in `GameContext`. The task model and claim API.
- `Assets/_Project/Scripts/UI_UX/WorkQueuePanel.cs` (~2485 lines) — `IUIPanel`, bound to key **7** via `UIKeyBindingManager.ToggleUI`. Built entirely in C# (no UXML/USS).
- Consumers that claim/complete tasks: `OrderSelectionTaskDriver`, `ReceivingTaskDriver`, `ReachTruckOperator`, `TrailerOffloadController`, `TrailerLoadController`. Producers: `OrderService`, `ReceivingService`, `ReplenishmentService`, `PutawayLogic`.

## Task model (`WorkQueueSystem`)
- `WorkTaskType { Receive, Putaway, Replenish, OrderSelect, Load, PalletPick }`
- `WorkTaskStatus { Open, Available, Assigned, Complete, Cancelled }`. **Open = filed but not claimable** (unreleased, or waiting for a staging lane). Available = claimable.
- `WorkTask`: `TaskId`, `Type`, `RequiredRole`, `PalletId`, `Status`, `AssignedToEmployeeGuid`, `FromLocation`/`ToLocation` (private set; use `AssignFromLocation` / `AssignToLocation` / `RelocateStagingSource` / `RenameLocationReferences`), `Priority` (`SetPriority`), `OrderId`, `SkuId`, `Area`.
- API: `CreateTask`, `GetPendingTasksForRole`, `TryClaimNextTask(role, guid, out task)`, `TryClaimSpecificTask`, `CompleteTask`, `CancelTasksForPallet`, `ReleaseStaleAssignments(90s)`, `RegisterRestoredTask`, `ClearAllTasks`, `Tasks`.
- Events (static): `OnTaskCreated`, `OnTaskCompleted`.
- **Priority:** every task starts at **100**; the player raises it with the row steppers. Range 100–900, step 100 (`PriorityMin/Max/Step` in the panel). Do not reintroduce per-type default priorities (a Replenish at 250 starved putaways).
- **Role substitution:** `WorkQueueSystem.CanServe(employeeRole, requiredRole)`. A DockStockerOperator can serve Load tasks. Add substitutes there if a controller ever accepts one. `WarnAboutUnstaffedWork` keys off "role not hired".
- A task's `ToLocation` is stamped only once the pallet is on the forks (the save/load contract for `MHEOperatorPersistenceService`). Don't stamp it earlier.

## Panel structure (`WorkQueuePanel`)
- The table lists **orders and tasks together**. Order rows are `(OrderData, RowPhase, WorkTask)` tuples from `BuildVisibleOrderRows`; plain task rows come from `BuildTaskRow`.
- `RowPhase { Open, Available, Assigned, Staged, Loading, Loaded, NoStock }` is derived in `DeterminePhase(order, task)`. Order status wins over the task status. Read the comments there before touching it: PartiallyPicked with freight in a lane is offered as Staged, Loaded must have a row (it is the only route to close-out), `IsWaitingForLane` renders as Available with "Open" in the Status column.
- `SortColumn { PaletteId, ItemNumber, Area, Priority, Role, Task, Status, From, To, Operator, Customer, Order, DelDate }` drives sort, per-column filters (`ColumnFilter`, `BuildFilterPopup`) and header cells. A new column needs ALL of: a `SortColumn` entry, a width const, `HeaderCell`/`BuildFilterHeader`, the cell in both `BuildRow` and `BuildTaskRow`, and `GetTaskCellValue` + `GetOrderCellValue` (filter/sort values).
- Column widths are consts at the top (`PaletteIdWidth` … `DelDateWidth`, gaps `RoleTaskGap`, `TaskStatusGap`, `StatusFromGap`, `ToGap`, `CustomerOrderGap`). The header indent equals the row `paddingLeft` (`RowPaddingLeft = 6`). **Header and data rows must use the same widths or columns drift.**
- Bottom bar: `RebuildBottomBar` / `SetBottomBar(ActionMode, …)` / `OnSubmitClicked`. `ActionMode { None, ReleaseToLane, ReleaseToStagesAuto, ReleaseToLoading, ReleaseToLoadingAuto, CloseOut, Mixed, ReassignLane }`.
- **The Work Queue does NOT release orders (Tad, 2026-10-13).** Releasing is done only from the Scheduler tile's RELEASE ORDERS button. An unreleased order (phase Open) shows a message pointing at the Scheduler; `ReleaseToLane`/`ReleaseToStagesAuto` are no longer reachable (enum values kept, submit branches removed).
- **Lane control stays here.** Phase Available with held work (`HeldWorkCount > 0`: released orders whose lowest task is still Open; the Status column reads "Open") offers a lane dropdown (every lane with room, labelled `N free`) and the button **"Release Orders to Floor"** (`ActionMode.ReleaseHeld`, `ReleaseHeldWorkToLane`): it releases as much as that lane fits and toasts the "Releasing X of Y ... Free up some space!" note if work remains; pick a second lane the same way. Fully released single-lane orders get **Move** (`ReassignLane`, only lanes that fit the whole selection; not offered for multi-lane orders). One customer at a time. From column shows every lane (`2A+2B`).
- Picking ahead is allowed: picking is not tied to truck arrival. Release to e.g. 2A while the trailer will load at door 1 (`AssignedDoorNumber` = dock door, `StagingDoorNumber` = lane's door).
- Selection: ctrl/shift click and shift-drag range select (`HandleRowPointerDown`, `ApplyRangeSelection`), Select All, Cancel Selected (`IsCancellable`), priority stepper applies to the whole selection (`AdjustPriorityForSelected`).

## Live refresh — the part that bites
- A scheduled item runs `RefreshIfVisible` every **250 ms**; it rebuilds the rows only when `BuildLiveSignature()` changes. The signature is a string concatenation over every non-Complete task and every active order.
- **If you add a field to a row (a new column, a new status, a tooltip value), add it to the signature**, or the panel will not redraw when only that field changes.
- Keep it cheap: it runs 4x/second over all active orders. Terminal orders are archived out of `ActiveOrders` for exactly this reason.
- `Show()` sets `_liveSignature = null` and calls `FillScreenExact()` one frame later (deferred so there is a real layout on the first open).

## Project overrides to the `unity:ui-uitk` skill
- This panel is **programmatic**: styles are set inline in C# (`el.style.*`). That is the project's pattern (ShiftManagerPanel, ContractsPanel, SchedulerPanel). Match it. Do not move one panel to UXML/USS mid-change.
- Palette (house style, fully opaque navy): `ColBg #121A24`, border `#5C9BC4`, title text `#CFE2F0`, subtle `#7A99B0`, orange `#B5743A`, row even/odd `#24303E / #1E2834` @ 0.65. Font **Lilita One** via `LilitaFont()` / `ApplyFont`.
- Phase colours: Open grey, Available light blue, Assigned amber, Staged green, Loading teal, Loaded dark green (`ColStatus*`).
- Round-trip needed for any look change: the Editor must be in Play mode on a save with orders; screenshot it. Compiling mid-Play resets non-serialized fields, so stop Play first.

## UI Toolkit gotchas already paid for in this codebase
- `IVisualElementScheduledItem.ExecuteLater` reschedules an item that already fired; use `.StartingIn(ms)` to delay a first run.
- A fixed `height` on a flex child needs `flexShrink = 0` too, or the parent squeezes it (this crushed the Contracts title bar).
- Fixed widths on anything whose text changes per frame, or it forces a full re-layout.
- Rows inside a `ScrollView`: stop propagation on row left-click so the empty-space "clear selection" handler on `_rowScroll` does not fire.
- The panel must implement `IUIPanel` AND be registered (`RegisterUI`) AND be opened through `ToggleUI`, or Tab/exclusivity silently ignores it. Sub-popups register via `RegisterAuxiliary` (see `OrderShortsPopup`).
- UI feedback ticks on `Time.unscaledDeltaTime`; the game is often paused while the panel is open.

## Verification
1. Compile gate: wait for `Library/ScriptAssemblies/*.dll` newer than the edited `.cs`, then grep `Logs/Editor.log` for `error CS`. `Unity_ValidateScript` is syntax only.
2. Enter Play, press 7, check the new column/phase on a real save. Use the Dev cluster on the Contracts Customers tab (TEST CUSTOMER) to generate orders.
3. Drive state through `ServiceLocator.Get<WorkQueueSystem>()` / `OrderService` via `Unity_RunCommand`/`execute_code` and confirm the signature changes and the row redraws within 250 ms.

## Cancelling
Cancel Selected (whole orders, only while nothing is claimed) and Cancel Remaining (held work only; the order ships short) both open a `ConfirmationModal` built by `OrderService.DescribeCancel` that states the reputation and partnership cost before anything happens. Unreleased orders do not appear in the Work Queue (hidden in `BuildVisibleOrderRows`); they are cancelled from the Scheduler tile. See the `order-system` skill for the penalty scale.

## Per-pallet stage (2026-10-14)
Staged / Loading / Loaded orders draw ONE ROW PER PALLET, and each row's Status is that pallet's own `OutboundPalletBuilder.Stage` (`Staged, Moving, Loading, Loaded, Shipped`), not the order's. `TrailerLoadController` sets Loading when the dock stocker lifts a pallet (and at the top of `PlaceCarriedInTrailer`) and Loaded when it is set down in the trailer; `OrderService.ShipOrder` sets Shipped (pallets then drop out of the panel with the archived order). `BuildLiveSignature` includes per-order stage counts so a stage change redraws. `Moving` is reserved for the planned reach-truck MOVE task (relocate a staged pallet to another lane via the bottom dropdown) and currently displays as Staged. Stage is not persisted (staged pallets aren't either).

## Planned case-pick rows (2026-10-26)
OrderSelect tasks of a planned order are one-per-pallet ("Select pallet i of N ..."). `WorkQueuePanel.AddUnifiedOrderRows` shows one row per planned pallet still to come; a task a selector is already building shows as the pallet itself (no double row). Plan data persists in `WorkTaskSnapshot.planText/planNumber/planTotal`.

## Pallet jack traffic + never drive through solids (2026-11-07)
Files: `Actors/OrderSelectionTaskDriver.JackTraffic.cs` (queue, right of way, step-off-and-walk), `Actors/MachineClearance.cs` (footprint guard, shared by AiNavigation's walk-out).
- **Guard:** every jack move (`JackFace`, `JackMoveStraight`, the NavMesh leg in `JackNavLeg`, AiNavigation `UnstickWalkOut` for any machine) refuses a step that would push the machine's outline further into a rack, wall, barrier, other machine or floor pallet. A turn that can't be made either way round is given up after 1 s real (the jack keeps its heading; an aisle is too narrow to spin a jack). A NavMesh leg held by a solid creeps 0.3 m straight on / back to swing wider (max 12), then gives up after 8 s real. The walk-out picks the nearest exit whose straight line is clear. Log tags: `[JackSolid]`, `"would drive it into"`, `"taking a clear way out"`.
- **Footprints** come from renderer `localBounds`, not colliders; shadow decals (`VehicleShadow_Wide` reaches 3.1 m back) and TMP labels are excluded. Pallet jack = x +/-0.61, z -2.54..+1.26 around the operator anchor.
- **Queue:** `JackAhead` = a MANNED jack within 3.6 m ahead and 1.2 m of the line of travel (further off sideways = the other lane is free, pass). Wait with no time limit. Mutual block (both driving, each in the other's path) → the one with more room ahead goes. `AiNavigation.HeldInQueue` pauses its 10 s no-progress backstop while held; `CheckNoProgress` is exempt while queued behind a jack that is itself progressing (cap 120 game minutes; not when the two wait on each other).
- **Step off and walk:** bay legs set `_queueProbe`; held 5 REAL seconds and `TryPlanQueueWalk` finds a route ≤ 10 m (walking NavMesh path from beside the pallet to the pick face, one detour round a jack/pallet in the way) → the leg ends with `_jackLegQueued`, the selector steps off (`BeginQueueStop`) and picks along the route. Next pick: still blocked and within 10 m → keep walking; else step on and re-park (a queue stop sets `_parkedBay = null` so it always re-parks).
- **Resume from save** must `SetJackDrive(false)`: it used to skip that, and the jack's own agent patrolled off with the selector aboard or walking behind it.
- Open: a true head-on in one aisle can still deadlock (both wait; the walk-pick and the no-progress watchdog free it); the in-aisle MHE NavMesh is missing, so bay legs end in scripted moves.

## Flat-battery jack riders (2026-11-07, Jose King / OG4466)
A selector sat for hours on a DEAD jack "picking" OG4466 with an empty pallet while the jack wandered its patrol route; the loaded
trailer was held for that pallet ("still has a pallet being moved") and the order was fined $2,414. Three guards in
`OrderSelectionTaskDriver.PalletJack.cs`:
- `CheckNoProgress`: jack/body movement only counts while the run drives the jack (jack `AiNavigation` off = scripted, or
  `IsSeekingTask`). A jack whose nav is on with no task is WANDERING, and that is never progress.
- `CheckDeadJackRider` (60 game min aboard a `NeedsCharge` jack that isn't charging): logs the full state ("sat on a flat pallet jack ...
  jackNav=WANDERING/seeking/off, claimed=, flatStop=, chargeTrip="), stops the jack, ends the run (`RecoverFromNoProgress`) or
  dismounts (`RecoverRiderFromJack`, shared with `CheckStrandedOnJack`).
- `FindFreeJack`: a selector's OWN jack is not returned when it is dead / `NeedsCharge` (returns null = wait; EquipmentBattery drives
  it to a charger with them aboard).
The first cause of the wandering was NOT pinned down. If the watchdog line fires, its fields say which path left the jack's nav on.

## Solid holds are queue holds too (2026-11-07, two empty jacks at the end of aisle 02)
`JackNavLeg` set `AiNavigation.HeldInQueue` only for `jackHold` (a MANNED jack ahead). Unmanned jacks are skipped by `JackAhead` and stop the jack via `MachineClearance` (solidHold) instead, so the AiNavigation 10 s no-progress backstop (GAME seconds = 2 real s at 5x) handed the leg back before the 5-real-s step-off-and-walk; after 6 re-issues "never got within 3m of 'bay 02-02'" and the selector abandoned this jack beside the first. Now `HeldInQueue = jackHold || solidHold`. Signature in logs: `[JackStall]` with `elapsed` ~0.1s despite 3 s still (= held), creeping 0.3 m steps, then `leaving the pallet jack (could not drive the jack to bay ..)`. Still open: how the FIRST unmanned jack ended up in the aisle.

## Queue stops park straight (2026-11-07)
A jack whose bay leg ends in a queue (`_jackLegQueued`, held 5 real s behind another jack) used to stop wherever it was, often
mid-turn and diagonal across an aisle mouth, and the selector walked off leaving it there. That blocked the jacks behind
(Tad: "parking at an angle in the aisles"). `DriveJackTo` now calls `SquareUpInAisle` on every queued exit: it turns in place to
`_aisleHeading` (set in `ComputeBayPark`, before the end-of-aisle 45-degree tweak), whichever end is nearer. `JackFace` sweep-checks
each step and keeps the heading if blocked. The end-of-aisle 45-degree park in `ComputeBayPark` (Tad, 2026-10-18) is untouched.

## Aisle traffic rules v2 (Tad, 2026-11-09)
Files: `Actors/AisleTraffic.cs` (shared: exit check, motion sampling, reach-truck pass planner), `OrderSelectionTaskDriver.PalletJack.cs`
(`DecideHeldInAisle`, `LaneDriveTo`/`LaneDriveCore`, `AisleExitStop`, `TryPlanPass`/`DrivePass`, `RightInAisle`), `ReachTruckOperator.cs`
(`TryPassJack`/`PassLeg`, `AisleExitStop`, both inside `SeekViaNavMesh`), `MachineClearance.ReachTruckBufferBlocked`, `AiNavigation.GoToRandomWaypoint`.
1. **Reach trucks have right of way.** Jacks hold any step that brings them within 1 m (`AisleTraffic.ReachTruckBuffer`) of a MANNED reach truck (moving away is always allowed). Log: "giving way to reach truck".
2. **Dock stockers stay out of aisles** except a player waypoint placed IN an aisle: patrol skips waypoints whose route runs through an aisle; task seeks still refuse aisle routes.
3. **Stop and look before leaving an aisle.** Jacks stop with the pivot 0.5 m past the last bay, reach trucks when 2.5 m ahead leaves the aisle; pause 1 game s; wait while another manned machine is MOVING within 5 m of the zone in front (machines in your own aisle and ones that started waiting after you don't count); cap 20 real s. Log: "stopped at the end of aisle".
4. **Jacks follow at 1 m** (`JackClearAhead` 3.6 -> 4.1 centre-to-centre). In an aisle a jack never faces more than 30 deg off the traffic direction (`InAisleMaxYaw`; wider requests are clamped, >100 deg faces traffic) and `RightInAisle` turns it back before every bay leg / exit. Held 0.4 real s behind a STOPPED jack: next pick > 4 m beyond its front -> pass; otherwise (or no clean pass) walk at once (no 5 s wait). The early 14 m look-ahead pass is gone. Pass = 25 deg out, left lane, up to 4 bays or until the pull-in ends at the stop (never before the tail clears), 25 deg back in. From 1 m a 25 deg pull-out clips the pallet ahead (needs ~2.3 m), so the jack reverses straight up to 2.5 m first.
5. **Reach trucks pass jacks** after 1 real s held, on the roomier side, unless another manned jack is within 2 bays (2 x 2.65 m) of that jack -> wait. Scripted pass checks solids AND jacks each step; stopped >2 s = abort, NavMesh leg re-issued.
Geometry (three doors, measured live): bay pitch 2.65 m, aisles 18.55 m = 7 bays, half-width 3.31 m, right-lane offset 0.9 m. "4-5 bays" of passing = most of an aisle, so the pass is capped at the stop.
Still open: the reach truck's 30 s "carrying on" after waiting on a jack still drives into it (pre-existing).
