# Ideas, Wave 4/5 refactor, TODO and bug lists (Jun-Jul 2026)

> Archived from CLAUDE.md on 2026-11-02 (verbatim). Historical detail — read only when working in this area. The lean CLAUDE.md holds the rules that always apply.

## IDEAS

Ideas and planned systems. These range from fully thought-out to early sparks.

### Lighting System (NEXT — starting 2026-07-03)
> **Status: on deck.** Tad's next feature, and "probably the last thing I need to add before we get into core game." Not started — no code yet.

**Real-time lighting (NOT baked)** delivered as purchasable, placeable **light fixtures**:
- Fixtures are normal placeable objects (`ObjDataSO` + prefab with a real-time `Light`, a `cost`, category, etc.) placed through the usual Build/FSM flow.
- Placed lights **illuminate the warehouse**; lit areas improve worker **safety** and **accuracy**, unlit areas degrade them.
- Enables the **night shift** to work safely — a dark warehouse at night without lights is unsafe/inaccurate.
- Should feed the employee stat/economy systems (safety & accuracy already exist as concepts). When building: confirm fixture types/costs, how "lit vs unlit" is measured per cell/area (light range/intensity → coverage), and how coverage maps to the safety/accuracy numbers.

### The Rat System
> **Status: Deferred.** Rats are intentionally being held until the core gameplay loop is solid. Do not implement until employee AI, inventory, and the basic simulation loop are all functional. The foundation has to come first.

Rats are a pest control challenge that layers chaos on top of the simulation. Left unaddressed, an infestation spirals out of control.

**Core behavior (implement first):**
- Rats hide inside pallets and boxes at rest
- When a worker passes nearby, a rat bolts from its hiding spot and scurries to a new location in the warehouse
- A green contamination cloud hangs around areas where rats have been present
- Rats can damage inventory — spoilage, contamination, loss

**Escalation (implement after core behavior is solid):**
- Rats procreate and multiply over time
- Individual rats grow larger and more aggressive as the infestation matures
- Cascading consequences: employee morale drops, employees quit, health inspectors arrive, fines accumulate, potential shutdown
- Endgame escalation: the player may have to physically fight a boss rat

**Testing checklist (for when this is built):**
- [ ] Rats spawn hidden inside pallets/boxes at scene load
- [ ] Rats flee to a new hiding spot when a worker comes within trigger range
- [ ] Green contamination cloud appears at rat locations
- [ ] Inventory items near rats take damage / show contamination state
- [ ] Rat population grows over time if unchecked
- [ ] Infestation consequences (morale, inspectors, fines) fire at correct thresholds

### Employee Simulation
Deep human element for warehouse workers:
- **Stats:** Speed, Safety awareness, Intelligence, Promotion potential
- **State:** Fatigue, Morale
- Behavior driven by stats + current conditions (a tired, low-morale employee is accident-prone)
- Career progression — employees can be promoted based on performance and potential

### Inventory & Ordering System
Core logistics loop:
- Place orders for goods
- Receive shipments, store inventory
- Fill outgoing orders from stock
- Stockouts, overstocking, and supplier delays create pressure

### Performance Optimizations (2026-06-11)

**Collider Architecture — Parent Colliders Instead of Per-Object**

Analyzed raycast system and discovered that individual Box Colliders on every placed object (floor tiles, foundations) causes massive performance overhead. Solution: one **parent collider per category**.

**Why This Works:**
- `RaycastController` performs two passes: ground raycast (gets grid cell) + object raycast (gets clicked object)
- The object raycast only needs to HIT something — it doesn't care if it's 1 collider or 3600
- Move/Delete logic identifies the exact object via BuildingData/PlacedObject components, not the collider itself

**Implementation:**
- **Floors:** Removed Box Colliders from FloorTile prefab. Added one large Box Collider to parent Ground GameObject. ✅ Complete, tested working.
- **Foundations:** Removing individual Box Colliders and adding one large Box Collider to parent Foundations GameObject. ⏳ In progress.
- **Walls:** Same pattern (TBD).

**Performance Gain:** 3600+ individual colliders → 1 parent collider per category. Ground optimization alone yielded +200 FPS. Foundations should see similar improvement.

**Code Impact:** Placement system needs to automatically parent placed objects to their category parent (Ground, Foundations, Walls) so the raycast hits the correct parent collider.

---

## Wave 4: FSM & Build State Refactoring (COMPLETED 2026-06-25)

### Architecture Refactoring Overview

Wave 4 introduced a standardized, event-driven architecture for FSM states and build operations. The work involved 3 major phases:

**Phase 1: Infrastructure Creation**
- `IBuildService` — Service contract for placement/move/delete operations
- `IPlacementCommand` — Unified command interface (Execute/Undo/Redo)
- `PlacementCommandBase` — Abstract base for command implementations
- `PlacementStateBase` — Abstract base implementing `IPlacementState` with shared services
- `BuildService` — Facade coordinating validation, command execution, and undo/redo
- `GameEvents.Build` — New event registry entries (OnValidationFailed, OnCommandUndone, OnCommandRedone)

**Phase 2: Service Integration**
- `GameContext` now instantiates and registers `BuildService` with `ServiceLocator`
- `CommandHistory` created fresh for each game session
- `EventManager` has runtime fallback instantiation if not in scene
- All services initialized in dependency order in `Awake()`

**Phase 3: State Refactoring (All 5 States Updated)**
All FSM states now inherit from `PlacementStateBase`:
- **IdleState** — Hover inspection and popup display
- **RaycastPlacementState** — Grid hover with cell indicators
- **BuildState** — Object placement with drag-place and validation
- **MoveState** — Object relocation with offset preservation
- **DeleteState** — Object removal with refunds and animations

### Service Access Pattern

All states now use `PlacementStateBase` to access services:
```csharp
public override void OnEnter()
{
    base.OnEnter();  // Initializes _eventManager, caches services
    // State-specific logic here
    // Access _moneyService, _timeService, _grid via protected members
}
```

### Build Operation Flow

```
User Input (Click/Drag)
    ↓
FSM State (BuildState, MoveState, DeleteState)
    ↓
BuildService.TryPlaceObject() / TryMoveObject() / TryDeleteObject()
    ↓
Validation: CanAffordCost? GridRulesOK? Etc.
    ↓
Create ICommand (PlaceCommand, MoveCommand, DeleteCommand)
    ↓
CommandHistory.Push(command) → Execute immediately
    ↓
Publish GameEvents.Build.OnObjectPlaced
    ↓
UI/Economy react to event
```

### Key Design Decisions

1. **Facade Pattern for BuildService** — Minimal implementation that delegates to existing commands and validators. Designed for future unification of command creation logic.

2. **PlacementStateBase as IPlacementState** — Base class implements the interface; concrete states override and call `base.OnEnter()` / `base.OnExit()` to ensure service initialization.

3. **Service Caching in Initialize()** — Services looked up once from ServiceLocator and cached in state instance, avoiding repeated registry queries.

4. **Protected Helpers** — `PublishBuildEvent()`, `CanAfford()`, `CurrentCapital`, `CurrentTime` provide quick access to common operations.

5. **Backward Compatibility** — Existing command classes (PlaceCommand, MoveCommand, DeleteCommand) remain unchanged. CommandHistory still owns undo/redo logic.

### Files Created

- `Assets/_Project/Scripts/1. FSM/1. Services/IBuildService.cs`
- `Assets/_Project/Scripts/1. FSM/1. Services/BuildService.cs`
- `Assets/_Project/Scripts/1. FSM/6. Commands/IPlacementCommand.cs`
- `Assets/_Project/Scripts/1. FSM/6. Commands/PlacementCommandBase.cs`
- `Assets/_Project/Scripts/1. FSM/2. States/PlacementStateBase.cs`

### Files Modified

- `GameContext.cs` — Registers BuildService and creates CommandHistory
- `IdleState.cs`, `RaycastPlacementState.cs`, `BuildState.cs`, `MoveState.cs`, `DeleteState.cs` — All now inherit PlacementStateBase
- `GameEvents.cs` — Added Build.OnValidationFailed, Build.OnCommandUndone, Build.OnCommandRedone

### Testing Checklist

- ✅ All 5 states refactored and compile clean
- ✅ Services accessible via ServiceLocator from all states
- ✅ EventManager initializes at runtime if not in scene
- ✅ BuildService facade works for validation queries
- ✅ Undo/redo via CommandHistory still functions
- ✅ No breaking changes to existing gameplay

### Next Steps (Future Waves)

- Refactor command classes to inherit from PlacementCommandBase and implement IPlacementCommand
- Unify command creation logic in BuildService (currently deferred to states/UI)
- Add more granular build events for UI/feedback systems
- Consider state-specific event publishers for cleaner decoupling

---

## Wave 5: Command Refactoring — Phase A COMPLETED (2026-06-25)

**Phase A done:** `PlaceCommand`, `MoveCommand`, `DeleteCommand`, `DragPlaceCommand` now inherit `PlacementCommandBase` and implement `IPlacementCommand` (which now extends `ICommand`, so `CommandHistory` needed no changes). Commands get real `Description` text and publish `GameEvents.Build.OnObjectPlaced/OnObjectMoved/OnObjectDeleted` through `EventManager`. Also fixed in the same pass: `PlacementStateMachine` previously constructed its **own** `CommandHistory` separate from the one `GameContext` handed to `BuildService` — `BuildService.Undo()/Redo()` were operating on a permanently-empty stack. `PlacementStateMachine.Initialize()` now adopts `BuildService.CommandHistory` via `ServiceLocator` instead of creating a fresh one.

**Side effect worth knowing:** this event-publishing is what made `EconomyService` start actually receiving `OnObjectPlaced`/`OnObjectDeleted` for the first time — hourly cost deduction for placed objects was dormant before this (see Economy section below).

**Phase B — deliberately deferred, not started:** centralizing command *construction* into `BuildService.TryPlaceObject/TryMoveObject/TryDeleteObject` instead of states constructing commands directly. User's call after weighing it: real benefit is only "a non-FSM caller could place objects programmatically" (nothing currently needs that), real cost is touching the placement hot path plus `DeleteState`'s `AddToBatch` has no equivalent in `IBuildService` today. Revisit only if a concrete feature needs headless placement.

**Secondary items, still open:**
- Fix ToolsWindow (non-functional UI currently)
- Migrate saves to `Application.persistentDataPath` (shipping blocker)
- Configure SimulationTimeService time scale (tuning needed)

---


## TODO

Things that need to be built, in rough priority order. Move items here as they come up and remove them when done.

**Detailed gameplay loop design:** See `GAMEPLAY_LOOP_DESIGN.md` for complete mechanics, phasing, and technical architecture.

**NEXT UP — Lighting System (real-time fixtures)** — see [IDEAS → Lighting System](#lighting-system-next--starting-2026-07-03). Purchasable/placeable light fixtures with real-time lights that illuminate the warehouse and affect worker safety/accuracy; enables safe night-shift work. Tad's last building-block feature before the core loop.

**Phase 1: Receiving & Putaway (Data layer DONE — UI/Services NEXT)**
- [x] Pallet data structure and InventoryService — COMPLETE
- [x] 99 SKU assets with cross-referenced Excel data — COMPLETE
- [x] Test data generator (shipments + orders) — COMPLETE
- [ ] ShipmentService — Manage inbound queue, trigger receiving tasks
- [ ] Receiving task UI — Display incoming shipments, "Scan & Receive" workflow
- [ ] Putaway mechanics — Task assignment, employee moves pallets to storage
- [ ] Storage location tracking — Cell-based capacity, pallet validation

**Phase 2: Order Selection & Fulfillment**
- [ ] Customer order generator
- [ ] Order queue UI and prioritization
- [ ] Picking task assignment to employees
- [ ] Inventory decrement on item pick
- [ ] Partial-order handling (backorder, split, cancel)

**Phase 3: Staging, Loading & Shipping**
- [ ] Staging area with capacity constraints
- [ ] Truck scheduling and assignment UI
- [ ] Load planning (assign orders to trucks)
- [ ] Truck departure trigger and visual feedback

**Phase 4: Revenue & Financial Integration**
- [ ] Revenue calculation on shipment departure
- [ ] Invoice UI showing order → revenue detail
- [ ] Daily profit/loss reporting integration with FinanceCategory

**Phase 5: Perishable Spoilage**
- [ ] Expiration date tracking per pallet
- [ ] Daily spoilage check and contamination state
- [ ] Write-off loss calculation

**Phase 6: Employee AI for Warehouse Tasks**
- [ ] Task assignment system (Receive, Putaway, Pick, Load tasks)
- [ ] Pathfinding to task locations in warehouse
- [ ] Task priority queuing
- [ ] Task completion detection (pallet moved, order picked, truck loaded)

**Post-MVP (Deferred until core loop is solid)**
- [ ] Ordering system — suppliers, lead times, restock triggers
- [ ] Move save files out of `Assets/_Saves/` to `Application.persistentDataPath` (required before any real build/release)
- [ ] Replace `FindFirstObjectByType` calls in `PlacementStateMachine.Start()` with proper scene references
- [ ] Review and playtest difficulty balance (starting capital + sell-back rate per level)
- [ ] Wire `ShiftManagerPanel` schedules into actual gameplay (replace `ShiftSchedule.cs`'s fixed windows), add persistence
- [ ] Reconcile day-of-week conventions: `ShiftManagerPanel` uses Sun=0..Sat=6, `EmployeeWorkSchedule` uses Mon=0..Sun=6
- [ ] Wire `EmployeeStatSystem.TickDay` to day-change event (currently dormant)

---

## BUGS & ISSUES

Known problems that need fixing. Add to this list as issues are discovered.

### CRITICAL — Post-Refactor Audit (2026-06-26) — FIXED ✅

- **EVENT UNSUBSCRIPTION IN BUILDSTATE** — ✅ CONFIRMED ALREADY FIXED in refactor. Input callbacks are properly unsubscribed in BuildState.OnExit() (line 138-139).

- **STATIC EVENT MEMORY LEAK IN EMPLOYEESPAWNER** — ✅ FIXED (2026-06-26). `MHEPlacementEvent.OnMHEEquipmentPlaced` subscriptions are now tracked in a dictionary and unsubscribed when the employee is removed via `OnEmployeeRemoved()` handler. This prevents dead closures and memory leaks from accumulating over playtime.

- **SERVICELOCATOR RETURNS NULL INSTEAD OF THROWING** — ✅ FIXED (2026-06-26). `ServiceLocator.Get<T>()` now throws `InvalidOperationException` when a service isn't found, making initialization failures fail fast and clearly instead of silently returning null.

### IMPORTANT — Post-Refactor Audit (2026-06-26)

- **FindAnyObjectByType in Hot Path** (`EmployeeAssignmentService.cs` lines 71-76) — O(n) search called on every employee assignment. Should cache spawner reference at initialization.

- **Try-Catch Silent Swallow** (`DeleteCommand.cs` line 237) — Bare catch block with no logging swallows all exceptions. Add exception logging to aid debugging.

- **HiringBoardUI Event Unsubscription Missing** — UI callbacks registered in `OnEnable()` but not unregistered in `OnDisable()`. Toggling panel causes duplicate callbacks. Add `OnDisable()` with callback unregistration.

### EXISTING ISSUES

- Save files currently write to `Assets/_Saves/` — this works in the Editor but will break in a built player. Must be moved to `Application.persistentDataPath` before shipping.
- `SimulationTimeService` time scale (1 real-second = 1 in-game-minute) is a placeholder — needs tuning/configurability before gameplay feels right.
- **ToolsWindow (`Assets/3. UI/7.ToolsWindow/`) is non-functional** — none of the following work: tilde toggle, X close button, tab switching, panel drag. Mouse scroll in the panel also bleeds through to the game camera. Root cause likely: PanelSettings misconfiguration, `Start()` silently failing before event wiring runs, or UIDocument not blocking input. Needs full debug pass.
- **One-time placement costs aren't `FinanceCategory`-categorized for lifetime totals** — `PlaceCommand`'s `_money.Deduct(_data.cost, _data.category)` tags the deduction with the raw `ObjDataSO.category` string into `_lifetimeExpenses`/`_lifetimeDetail`, which doesn't match any `FinanceCategory.ExpenseOrder` entry. The dollars are real and now correctly shown in the **Spent Today → Purchases** list (fixed 2026-06-25/26), but the **Hourly tab's lifetime per-category breakdown still won't include them**. See [Economy & Financial Reporting System](#economy--financial-reporting-system).
- **`EmployeeStatSystem`'s daily fatigue/morale tick is dormant** — `TickDay`/`TickDayForAll` are never called except from their own Editor debug context menu. The new overtime-doubles-fatigue rule is implemented correctly inside it but has zero effect in actual play until it's wired to a real day-change event. `GetCurrentDayOfWeek()` is also still a real-world-`DateTime.Now` placeholder, not tied to the in-game calendar.

---


