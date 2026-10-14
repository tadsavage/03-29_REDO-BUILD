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
