# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity 6 (URP) warehouse/facility builder simulation game. Players place objects on a 2D grid in a 3D world, with a simulated economy (money + time) and full save/load support.

**Unity Version:** 6 (project name: `03-29-26 REDO`)
**Render Pipeline:** Universal Render Pipeline (URP) 17.3.0
**Input System:** Unity Input System 1.18.0

## Development Commands

All development is done through the **Unity Editor** — open the project in Unity 6, then:

- **Play/Test:** Press Play in the Editor
- **Build:** File → Build Settings → Build
- **Run Tests:** Window → General → Test Runner (uses `com.unity.test-framework`)
- **Editor Tools (custom menus):**
  - `Tools/ObjData/Auto‑Assign Unique IDs` — assigns unique IDs to all `ObjDataSO` assets after creating new ones
  - `Tools/Checklist/Open Checklist JSON` — opens the dev checklist file
  - Window → `Build Phase Checklist` — shows the in-Editor checklist window

**Quicksave/Quickload (in Play Mode):**
- `F5` — quicksave to `autosave`
- `F9` — quickload from `autosave`
- `F6` — open the slot-based save/load window

**UI hotkeys (in Play Mode, number row — rebound from F1–F4 on 2026-06-26 to make room for 5):**
- `1` — Dev Console (`ToolsWindowController` — currently non-functional, see BUGS & ISSUES)
- `2` — Hiring Board
- `3` — Employee Roster
- `4` — Employee List
- `5` — Shift Manager (first draft, UI-only — see Economy & Financial Reporting System below)

## Architecture

### Core Systems

**Placement FSM** (`Assets/1. Scripts/1. FSM/`)

The heart of the game. `PlacementStateMachine` drives a stack-based FSM with five states:
- `IdleState` — hover inspection, popup display
- `RaycastPlacementState` — grid hover without placement
- `BuildState` — place new objects (click or click-drag for multi-place)
- `MoveState` — pick up and re-place existing objects
- `DeleteState` — remove objects with destruction animation

`PlacementController` translates UI button events (from `BuildMenuUI`) into FSM transitions. The FSM owns a `CommandHistory` for undo/redo (Ctrl+Z / Ctrl+Y, also available via UI buttons). ESC or RMB pops the state stack back to the previous state.

**Key rule:** Only `IdleState` drives the hover popup (`WorldHoverPopupUI`). All other states must not show it.

**PlacementGrid** (`Assets/1. Scripts/1. FSM/7. Math/PlacementGrid.cs`)

2D array of cell stacks (`List<PlacedObject>[,]`). Manages object stacking, height tracking, and world↔cell coordinate conversion. Default cell size is `1.33f` units. Object ordering within a cell is: Foundations/Grounds → Floors → normal objects.

After a save is loaded or objects are manually placed in the Editor, call `grid.RebuildFromRegistry()` to sync the internal grid state with `PlacedObjectRegistry`.

**Command Pattern** (`Assets/1. Scripts/1. FSM/6. Commands/`)

Every placement/delete/move action is wrapped in an `ICommand` and pushed to `CommandHistory`. Multi-cell drag-place uses `DragPlaceCommand`; batching uses `CommandBatch`. Commands execute immediately on `Push()` and support `Undo()`/`Redo()`.

**GameContext** (`Assets/1. Scripts/1. FSM/7. Math/TimeAndMoney/GameContext.cs`)

Scene singleton (`[DefaultExecutionOrder(-100)]`) that owns and initializes `MoneyService` and `SimulationTimeService`. Wires hourly cost deduction and daily spending resets. Reads `PlayerPrefs` for difficulty and save-slot routing in `Start()`, then bakes the NavMesh synchronously.

Difficulty levels (set via Main Menu, stored in `PlayerPrefs("Difficulty")`):
- `0` — Clerk (easy): $120k starting capital, 100% sell-back
- `1` — Supervisor (normal): $100k starting capital, 75% sell-back
- `2` — Manager (hard): $80k starting capital, 50% sell-back

**⚠️ Difficulty balance needs review** — numbers are placeholder, not playtested.

**MoneyService / SimulationTimeService**

Plain C# classes (not MonoBehaviours) held by `GameContext`, registered with `ServiceLocator`. `SimulationTimeService` runs at 1 real-second = 1 in-game-minute. `MoneyService` fires `OnMoneyChanged` on any balance change; hourly costs are applied via event subscription. `MoneyService` now takes a `sellBackRate` (0–1 float) that scales all deletion refunds — `DeleteCommand` uses `_money.SellBackRate` to compute the adjusted refund.

`MoneyService`, `EconomyService`, and `PayrollService` together drive the full economy — see [Economy & Financial Reporting System](#economy--financial-reporting-system) below for the complete picture (GL_Line cost tracking, wage tiers, overtime, the 4 TopBar reporting panels).

### Data Model

**ObjDataSO** (`Assets/1. Scripts/3. ScriptableObjects/SO Scripts/ObjDataSO.cs`)

ScriptableObject describing a placeable item: `id` (unique int), `prefab`, `cost`, `hourlyCost`, `category`, `GL_Line`, `footprint` (Vector2Int), optional `customShapeOffsets`, stacking rules, pathfinding flags, and special behavior flags (`isFloor`, `ClearsGridAfterPlacement`, `ignorePlacementRules`).

`GL_Line` is the General Ledger line for hourly-cost reporting — see [Economy & Financial Reporting System](#economy--financial-reporting-system) below. It's a plain hand-editable string per asset (no runtime default-computation); the `Tools/ObjData/Auto-Assign GL Lines` one-time batch (run via `Unity_RunCommand`, not an Editor menu item yet) populated it across all 105 assets from `category` with named overrides.

`GetFootprintOffsets(rotation)` returns the rotated cell offsets for a placement. The convention is: pass `-currentRotation` to get footprint offsets (negated because world rotation and grid rotation are inversed).

**ObjDataRegistry** (`Assets/1. Scripts/3. ScriptableObjects/SO Scripts/ObjDataRegistry.cs`)

ScriptableObject list of all `ObjDataSO` assets. Lookup by `id` via `GetByID(int)`. After creating new `ObjDataSO` assets, run `Tools/ObjData/Auto‑Assign Unique IDs` to ensure no ID collisions.

**PlacedObject** (`Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts/PlacedObject.cs`)

MonoBehaviour on every placed prefab. Stores `gridX`, `gridY`, `rotation` (in 90° increments), and a reference to its `ObjDataSO`. Automatically registers/unregisters with the static `PlacedObjectRegistry` via `OnEnable`/`OnDisable`/`OnDestroy`. Nested `PlacedObject` children (e.g. items on a pallet) skip registration to avoid being saved at (0,0).

**BuildingData** (`Assets/1. Scripts/1. FSM/8. Data/BuildingData.cs`)

Stores the root cell, rotation (degrees), footprint offsets, and data reference for a placed object. Used by `MoveState` and `PlacementGrid` to know where an object's root and footprint cells are.

### Save System

**PlacedObjectRegistry** (static) — global `HashSet<PlacedObject>` of all active placed objects.

**PlacementSystem** (`Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts/PlacementSystem.cs`) — serializes/deserializes the world state to/from JSON via `ObjDataRegistry` IDs. The quicksave (F5/F9) path writes to `Assets/_Saves/autosave.json` using the legacy `SaveSystem` static class.

**SaveManager** (`Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts/SaveManager.cs`) — manages up to 8 named save slots. Each slot has a game data JSON file and a thumbnail PNG. Metadata (names, timestamps, file references) is persisted to `Assets/_Saves/metadata.json`.

Save files are stored at `Application.dataPath + "/_Saves/"` (inside the project's `Assets` folder — intentional for this prototype).

### Audio

**AudioManager** — singleton MonoBehaviour that persists across scenes. Sound effects are defined in a `SoundDefinition` ScriptableObject and played by name: `AudioManager.Play("soundName")`. Music plays probabilistically on an interval with fade-in/fade-out.

### Racking/Aisle Initialization System (`Assets/_Project/Scripts/Racking/`)

> **📎 Skill:** When working on this system, use the **`racking-system`** skill (`.claude/skills/racking-system/`) — it captures the load-bearing conventions (grid-cell adjacency, chevron facing = `Vector3.forward` reference, side-teams, ghost lifecycle, delete cleanup) and the known deferred gaps. Auto-triggers on racking/chevron/aisle tasks, or invoke `/racking-system`.

**Status: BUILT 2026-06-29** — Core system complete; chevrons spawning, positioning/rotation fixed, adjacency detection working.

**Overview:** Player places rack prefabs → system auto-detects adjacent racks (via local X-axis) → groups into collections → spawns 4 chevrons per aisle (one at each corner: first/last bay, left/right side) → player right-clicks chevron to rotate (set aisle travel direction) → double-clicks to open setup UI → enters aisle #/level designations → system instantiates real racks with location labels (AA-BB-LC format) → chevrons deleted.

**Key Components:**

- **RackPlacedEvent** — Static event fired when rack placed in build mode. Listened to by RackCollectionDetector.
- **RackCollectionDetector** — Detects adjacent racks by projecting distance vector onto existing rack's local X-axis (transform.right). Groups into `RackCollection` objects. Fires `OnCollectionCreated`/`OnCollectionAdded` events. ADJACENCY_THRESHOLD = 1.5f.
- **RackCollection** — MonoBehaviour grouping adjacent racks. Tracks `Racks` list, `Initialized` state, collection center/bounds for chevron placement.
- **ChevronSpawner** — Listens to `OnCollectionCreated`, spawns exactly 4 chevrons per aisle (Left_Front, Left_Rear, Right_Front, Right_Rear) at first and last rack Z positions. Position: Y=1.15f (on ground), Rotation: 90° on X-axis (Quaternion.Euler(90,0,0) to make horizontal). Uses _chevronSprite and _chevronMaterial from RackingSystemManager.
- **ChevronController** — MonoBehaviour on each chevron. Right-click rotates 180° (toggles _currentRotation between 0/180). Double-click fires `OnChevronSelected`, calls `RackSetupUI.SetSelectedChevron(this)`.
- **RackSetupUI** — Modal UI receiving selected chevron. Collects aisle # (01-99) and level designations (Pick/Reserve by height ≥80"). On submit, calls `AisleInitializer.HandleSetupSubmit()`.
- **LocationNameGenerator** — Static utility. `GenerateAisleLocations()` creates AA-BB-LC names (Aisle-Bay-Level-Column) for all racks in collections. Determines even/odd sides from chevron rotation: rotation < 90° = first collection even bays (02,04,06...), other side odd (01,03,05...). Level indices 0-1 → "0","1"; 2-5 → "A"-"D" (pick/reserve tiers).
- **AisleInitializer** — Orchestrates initialization. On RackSetupUI submit: generates location names, instantiates real racks from `PlacedObject.data.prefab` (NOT a serialized field), assigns locations to TMP labels on each rack, disables non-aisle-facing label groups (determined by chevron rotation), marks collections initialized, deletes all chevrons.
- **RackingSystemManager** — Scene orchestrator. In Awake(), adds RackCollectionDetector/ChevronSpawner/AisleInitializer components and configures them via `SetSprite()`/`SetMaterial()`/`SetHeight()` setters with values from Inspector fields (Real Rack Material, Chevron Sprite, Chevron Material).

**Modifications to Existing Code:**
- **PlaceCommand.Execute()** — Added `if (_data != null && _data.category == "Racking") RackPlacedEvent.Fire(_instance);`
- **DragPlaceCommand.Execute()** — Added same RackPlacedEvent.Fire() inside `if (placed != null)` loop for multi-place.

**Setup Checklist:**
- [ ] RackingSystemManager GameObject in scene
- [ ] Configure Inspector fields: Real Rack Material, Chevron Sprite, Chevron Material
- [ ] Rack ObjDataSO must have category == "Racking" and prefab reference
- [ ] Test workflow: place racks → collections detected → chevrons spawn at first/last bays → right-click rotate → double-click open UI → submit → racks instantiate with labels

**Known Issues:**
- Adjacency detection uses local X-axis via dot product projection — works for any global orientation but threshold may need tuning if rack spacing varies.
- Two-sided aisle detection (SpawnChevronPair) logic may need refinement based on gameplay testing.
- Location naming + label assignment not yet end-to-end tested in full workflow.

### Editor Tools (`Assets/10. Editor/`)

- **ObjDataRegistryEditor** — custom Inspector for `ObjDataRegistry`
- **ObjDataIDAssigner** — menu item to auto-assign unique IDs to all `ObjDataSO` assets
- **Build PhaseChecklistWindow** — in-Editor task tracker reading from a JSON file

### Input

Input is handled via the Unity Input System. `PlacementActions` is the generated C# wrapper for `InputSystem_Actions.inputactions`. States subscribe/unsubscribe their action bindings in `OnEnter`/`OnExit`. Global shortcuts (Ctrl+Z, Ctrl+Y, ESC, RMB) are handled directly in `PlacementStateMachine.Update()`.

### Key Conventions

- **Rotation convention:** rotations are stored in 90° increments as integers on `PlacedObject.rotation`. In `BuildingData` and state logic, rotation is stored as float degrees. Pass `-currentRotation` to `GetFootprintOffsets()`.
- **Cell stacking order matters:** Foundations and Grounds go at index 0; Floors go after grounds; normal objects append to the top. `PlacementGrid` enforces this in `AddStackObject`.
- **Undo disables objects, not destroys:** Undo for placement calls `SetActive(false)` on the GameObject so it can be re-enabled on redo, keeping the same instance alive.
- **`FindFirstObjectByType` usage:** `PlacementStateMachine.Start()` uses `FindFirstObjectByType` to locate shared systems — these must all be present in the scene at startup.

---

## Game Vision

This is a warehouse simulation game with a serious logistics core and a whimsical/absurdist chaos layer on top — think Sims 4 build mode meets real operations management meets escalating insanity.

**Current phase:** Build mode only. The placement system, grid, economy, and save/load are the foundation. The actual gameplay loop comes next.

**Player character:** The player is **Tug Dudley**, the warehouse manager / "The Boss." This name appears in the welcome overlay and is stored via `PlayerPrefs("PlayerName")`.

**Design principle:** The chaos should *emerge from* the simulation, not be bolted on. Serious systems create the conditions for absurd outcomes.

---

## IDEAS

Ideas and planned systems. These range from fully thought-out to early sparks.

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

## Economy & Financial Reporting System

Built out 2026-06-25, actively being extended. This is the active focus area for the foreseeable future — keep this section current as it evolves rather than letting it drift like the Wave 4/5 docs did.

**⚠️ Testing gotcha, confirmed 2026-06-25: restart Play Mode after any code change before re-testing this system.** Recompiling scripts mid-Play-session triggers a Unity domain reload, which silently nulls `EventManager.Instance`, `ServiceLocator`'s entire registry, and `GameContext.MoneyService`/`TimeService` (plain C# objects don't survive domain reload the way the `GameContext` GameObject itself does). The result: `SimulationTimeService` stops ticking, `EconomyService`'s hourly accrual stops, `PayrollService` stops paying wages — but the TopBar UI keeps showing whatever numbers were last rendered, so it LOOKS like the game is still running when it's actually frozen. This reads exactly like a UI/data-wiring bug (and cost real debugging time before the actual cause was found via `Unity_RunCommand`: `EventManager.Instance == null` + `ServiceLocator.TryGet<MoneyService>()` failing while `GameContext` itself is still findable in-scene). There's no automatic recovery — stop and re-enter Play Mode to get a clean `GameContext.Awake()`.

### Core data flow

Two **separate, parallel** cost pipelines feed `MoneyService`, both ultimately routed through `FinanceCategory`:

1. **Object hourly costs** (`EconomyService`) — driven by `ObjDataSO.hourlyCost` for placed buildings/vehicles/decor. Tracked per `GL_Line`, NOT per individual object.
2. **Employee wages** (`PayrollService`) — driven by `EmployeeRecord.hourlyWage`, paid once per in-game hour. Completely separate from (1) — see the "Worker/Staff exclusion" gotcha below.

**`ObjDataSO.GL_Line`** — a plain string field, hand-set per asset (Inspector-editable, no runtime default-computation). Defaults were populated by a one-time batch (`Unity_RunCommand`, not a menu item) mapping `category` → `GL_Line`:

| category | default GL_Line | top-level bucket |
|---|---|---|
| Barrier/Barriers | Barriers | Maintenance |
| Flavor | Flavor | Maintenance |
| Floor | Floor | Maintenance |
| Grounds | Groundskeeping | Groundskeeping |
| Inventory | Pallet Lease and Repair | Pallet Lease and Repair |
| Loss Prevention | Loss Prevention | Loss Prevention |
| Racking | Racking | Maintenance |
| Staff | Wages | Wages |
| Vehicles | (per-vehicle, see below) | MHE Costs |
| Walls | Walls | Maintenance |
| Waypoints | Waypoints | Maintenance |
| Worker | Worker | Maintenance |

**Per-asset overrides on top of the table above** (set directly on the asset, not derivable from category):
- `ManDoor`, `RollupDoor`, `ShippingDoor`, `Wall-Win-Entrance` → GL_Line `"Doors"` (still Maintenance)
- `Truck`, `TruckSavageDev`, `TruckDriver` → GL_Line `"Transportation"` (own top-level bucket, pairs the vehicle + its driver's wage)
- `DS`/`PJ`/`RT` (Dockstocker/PalletJack/ReachTruck) → GL_Line `"Dock Stocker"`/`"Pallet Jack"`/`"Reach Truck"` respectively — each vehicle type gets its **own** GL_Line string so the MHE Costs tooltip can break them out individually, all three routed to the same `MHECosts` top bucket via `FinanceCategory.ForGLLine`
- `Dumpster`, `Garbage Can`, `Trash Can` → GL_Line `"Sanitation"` (own top-level bucket, pairs with the Sanitation employee tier)
- `Exterminator` (the Staff body model) → GL_Line `"Contract Labor"` (pairs with the Exterminator employee's wage)

**`FinanceCategory.cs`** (`Assets/_Project/Scripts/Core/Managers/TimeAndMoney/FinanceCategory.cs`) is the single source of truth for category strings and routing:
- `IncomeOrder`/`ExpenseOrder` — fixed arrays that drive row order in every breakdown panel.
- `ForGLLine(string glLine)` — maps a GL_Line value to its top-level bucket. **Whenever you give an object/category a new distinct GL_Line value, you must add a case here too**, or it silently falls through to the `Maintenance` default (this has bitten us twice already — Sanitation and Contract Labor GL_Line values were missing cases and got miscategorized).
- `ForWageGLLine(EmployeeRole role)` — maps an employee role to `(topCategory, detailKey)`. Exterminator→Contract Labor, Security→Loss Prevention, Sanitation→Sanitation (own line), TruckDriver→Transportation — these four are pulled OUT of Wages entirely. Everything else stays under Wages, tiered into `FloorWages`/`HourlyWages`/`SalaryWages` (OrderSelector/Loader/DockStockerOperator/ReachTruckOperator/Receiver = Floor; InventoryControl/Admin = Hourly; HR/Boss/Supervisor = Salary).

### EconomyService (`Assets/_Project/Scripts/1. FSM/1. Services/EconomyService.cs`)

- Subscribes to `GameEvents.Build.OnObjectPlaced/OnObjectDeleted` and pools hourly cost **per GL_Line** in `_hourlyByGLLine` (NOT a single flat total — that was the old, dormant-until-2026-06-25 design).
- **Fractional-carry accumulator (`_fractionalByGLLine`) is load-bearing, do not remove.** Without it, `Mathf.CeilToInt(hourlyAmount/60f)` rounds every GL_Line up to a **$1/minute floor** regardless of its real rate — several cheap categories (a few dollars/hour each) all hit the same floor and accumulate *identical* lifetime totals, which looks exactly like a data bug but is a rounding bug.
- **`IsWageTracked()` excludes `category == "Worker" || "Staff"`.** Employee prefabs carry a `PlacedObject` component too (for the hover-popup), so they self-register with `PlacedObjectRegistry` just like a placed building even though they never go through `PlaceCommand`. Without this exclusion, every hired employee's body double-charges its legacy `ObjDataSO.hourlyCost` on top of `PayrollService`'s own wage — confirmed empirically (the math matched to the dollar) before this was understood.
- **`RebuildFromRegistry()` must be called after every save load / scene start / manual rebuild** — call it everywhere `PlacementGrid.RebuildFromRegistry()` is called (currently: `GameContext.SyncAndBake`, `PlacementSystem`'s two load call sites, `ToolsWindowController`'s rebuild button and Clear-All). Objects restored from a save or placed by hand in the Editor scene are instantiated directly, bypassing `PlaceCommand` — `OnObjectPlaced` never fires for them, so without an explicit rescan they're invisible to hourly-cost tracking even though they're sitting right there costing money.
- **`OnMinutePassed` also calls `moneyService.RecordHourlySpend(wholeDollars)`** for every real per-GL_Line deduction — bookkeeping only (the dollar was already removed by `RemoveCapital` on the line above), tags it as a *recurring* hourly cost for the Spent Today panel. `PayrollService` does the same for wages. See MoneyService below.
- **Known gap, still not fixed:** one-time placement costs (`PlaceCommand`'s `_money.Deduct(_data.cost, _data.category)`) are tagged with the raw `ObjDataSO.category` string directly in `_lifetimeExpenses`/`_lifetimeDetail` — completely bypassing `FinanceCategory`/`GL_Line` for the **lifetime** totals. The Hourly tab's per-category breakdown still won't show one-time purchase costs (category strings like `"Grounds"`/`"Walls"` don't match any `FinanceCategory.ExpenseOrder` entry). This WAS fixed for the **Spent Today** panel specifically (see `MoneyService.Deduct()` below) but not for the lifetime/Hourly-tab view — would need the same `FinanceCategory`/`GL_Line` routing `Deduct()` callers don't currently use.

### PayrollService (`Assets/_Project/Scripts/1. FSM/1. Services/PayrollService.cs`)

Pays every `Active`, non-`SystemManaged` employee their `EmployeeRecord.hourlyWage` once per in-game hour, routed through `FinanceCategory.ForWageGLLine`. Increments `EmployeeRecord.totalWagesPaid`.

**Overtime rule (added 2026-06-25):** if `ShiftSchedule.IsOvertime(record.shift, currentHour)` is true, that hour is paid at **1.5×** the base rate. `Flexible`-shift employees have no fixed window and are never overtime.

### ShiftSchedule (`Assets/_Project/Scripts/Actors/EmployeeSystem/ShiftSchedule.cs`)

Single source of truth for shift windows, shared by `PayrollService` (overtime pay) and `ShiftStatusPanel` (the Time dropdown). Fixed windows, not yet configurable:
- Day: 08:00–16:00
- Evening: 16:00–00:00
- Night: 00:00–08:00
- Flexible: always "in shift," never overtime

This is a reasonable first-pass interpretation, not a confirmed design spec — revisit if the windows or overtime definition need to change.

### MoneyService (`Assets/_Project/Scripts/Core/Managers/TimeAndMoney/MoneyService.cs`)

Lifetime tracking (`_lifetimeExpenses`/`_lifetimeIncome` top-level totals, `_lifetimeDetail` = `category → detailKey → amount` powering the Hourly tab's tooltips) all flow through `RemoveCapital`'s two overloads sharing a private `ApplyRemoval()` core: the 2-arg version records under `reason`; the 3-arg version (`amount, category, detailKey`) records under the finer `detailKey` instead of `category`, to avoid double-recording the same dollar under two granularities.

**Two SEPARATE "today" counters power the Spent Today panel** — these used to be one conflated number and that was a real, user-reported bug (2026-06-25/26): buying 56 foundations showed as "$25,200 Total Hourly Expenses" with an empty Purchases list, because the old single `SpentToday`/`_spentTodayDetail` lumped one-time purchases in with recurring hourly costs, and the by-category list was only ever fed by the hourly loop. Fixed by splitting into:
- **`_spentTodayHourlyOnly`** — RECURRING costs only: `EconomyService.OnMinutePassed` (object upkeep) and `PayrollService` (wages) both call `RecordHourlySpend(amount)` as bookkeeping alongside their real `RemoveCapital` call. Powers Spent Today's "Total Hourly Expenses" line.
- **`_spentTodayByObjectCategory`** — ONE-TIME purchase costs only, keyed by raw `ObjDataSO.category`. Populated exclusively by **`Deduct(int amount, string category)`** — every `PlaceCommand`/`MoveCommand`/`DeleteCommand` one-time cost adjustment goes through this method, and it always carries a real category string, so it's the correct/only hook point. Powers Spent Today's "Purchases" section + "Total Purchases" row.

Both reset in `ResetDailySpending()` alongside the legacy `_spentToday` int (which still exists and still means "everything," just isn't displayed as a single number anywhere anymore). If you add a NEW one-time-cost call site, route it through `Deduct()` (not raw `RemoveCapital`) so it's correctly counted as a purchase, not an hourly cost.

**Lease/Mortgage** — `EconomyService.ChargeLease()`, fired on `OnDayChanged`: `(PlacementGrid.Width × Height) / 10` dollars, once per in-game **day** (not hourly, unlike everything else — rent is conventionally billed per period). Explicitly a quick stand-in per the user's request ("may not be a permanent feature") — easy to rip out or replace with a real property/lease system later.

### The four TopBar reporting panels

All implement `ITopBarPanel` (`IsVisible`/`Show()`/`Hide()`/`Root` — `Root` exposes the panel's own VisualElement) and share visual building blocks from `FinanceUIKit.cs`. `TopBarUI.ToggleExclusive()` ensures only one is ever open at a time, and `TopBarUI.PollPanelAutoClose()` (checked every 100ms) auto-closes whichever panel is open once it's been unhovered — neither its trigger label nor its own `Root` — for 1500ms. Hovering either the label or the panel resets that idle clock.

| TopBar label | Panel class | Shows |
|---|---|---|
| Capital | `CapitalSummaryPanel` | Revenue breakdown (green header / light-yellow text) + **one** rolled-up Total Expenses line + Net Profit. No per-category expense drill-down here. |
| Hourly | `FinancialBreakdownPanel` | **Expenses only** (red header / white text) — full per-category breakdown with hover-to-expand tooltips (Wages tiers, Maintenance by area, MHE Costs by vehicle, etc). The Income section and Net Profit row were removed entirely — Revenue/Net Profit live on Capital instead. |
| Spent Today | `SpentTodayPanel` | "Total Hourly Expenses" — recurring costs only, one number, not broken out. Below it, a "Purchases" section: one-time costs broken out by raw `ObjDataSO.category`, with a "Total Purchases" row. See MoneyService above for why these are two genuinely separate counters now. |
| Time | `ShiftStatusPanel` | Hours left in whichever shift window currently contains the in-game time, + live count of `Active` employees currently working outside their own shift (overtime). |

**Tooltip retract mechanism (FinancialBreakdownPanel's per-category hover tooltips, NOT the same thing as the panel auto-close above)** — rewritten 2026-06-25 after two patch attempts both failed under sustained use. The original design created a fresh `IVisualElementScheduledItem` per mouse-enter/leave event and called `Pause()` on the previous one each time; this degraded after repeated hover cycles into tooltips getting permanently stuck open. **Do not reintroduce that pattern.** Current design: a `TooltipEntry` (`Row`, `Tooltip`, `Category`, `Hovered` bool) per expandable row, tracked in a `List<TooltipEntry>`, with **one** shared idle-timer (`_idleMs`) polled by **one** scheduled item created once in the constructor (`_panel.schedule.Execute(PollIdle).Every(100)`) that runs for the panel's entire lifetime, closing after 1500ms unhovered. No per-interaction timer creation/cancellation.

**UI Toolkit scheduling gotcha, learned the hard way (twice):** `IVisualElementScheduledItem.ExecuteLater(ms)` reschedules an item that has **already fired once** — it does NOT delay the first execution. To delay a first run, use `.StartingIn(ms)` instead. Both the tooltip retract above AND the panel auto-close (`TopBarUI.PollPanelAutoClose`) initially used `ExecuteLater` and silently didn't work — the panels stayed open until manually re-clicked, which read as a missing feature rather than a one-word API mistake.

### Overtime actions — "Ask to Work OT" / "Send Home" (added 2026-06-25)

Two new options in the Employee Info card's Actions dropdown (`EmployeeInfoUI`), handled by a new static `EmployeeOvertimeService`:
- **Ask to Work OT** — applies an instant morale penalty (5 points normally, 12 if morale is already below 40 — "low morale takes a bigger hit"), and marks today as an overtime day on the employee's `EmployeeWorkSchedule` (`SetOvertime`). Does NOT gate whether overtime pay/fatigue actually happens — `PayrollService` already pays 1.5x automatically by hour regardless of consent (see ShiftSchedule above); this button is the morale-cost flavor action of formally asking, not a gameplay gate.
- **Send Home** — flat 10-point morale penalty (`// TODO: scale by difficulty` — explicitly a placeholder per Tad's note, not implemented yet). Does nothing else right now (no pay/status change) — kept deliberately minimal to match the literal ask.

**`EmployeeStatSystem.cs`'s daily fatigue/morale tick (`TickDay`/`TickDayForAll`) is NOT wired to run automatically anywhere** — confirmed by grep, it's only ever called from its own Editor debug context menu ("Tick Day for All Employees"). Its overtime fatigue gain is now `_fatiguePerWorkDay * _overtimeFatigueMultiplier` (multiplier defaults to 2 — an exact "doubled" relationship, replacing an old unrelated flat constant), and `GetCurrentDayOfWeek()` is now `public static` (still uses real-world `DateTime.Now`, not in-game day — a known placeholder) so `EmployeeOvertimeService` can share it instead of duplicating the bug. Net effect: the "doubled fatigue during overtime" rule is implemented correctly but has **zero visible effect in actual play** until this daily tick gets wired to a real day-change event — flagged to Tad, not silently fixed (wiring it up is a separate decision: should it fire on `GameEvents.Time.OnDayChanged`, and should `GetCurrentDayOfWeek` be fixed to use `SimulationTimeService.Day` at the same time, since they're related bugs).

### ShiftManagerPanel — first draft (2026-06-25/26, UI ONLY, not wired to gameplay; visual pass + polish 2026-06-26)

`Assets/_Project/Scripts/UI_UX/ShiftManagerPanel.cs`, bound to the **5** key (built/owned by `TopBarUI` like the four panels above, but NOT one of them — it's a full-screen modal, not a TopBar dropdown, so it's outside `ToggleExclusive`/`PollPanelAutoClose`). Lets the player define named shifts (free-text name, e.g. "Day Shift", "Clean Shift", "Night Shift") with a Start/End time dropdown per day of week (Sun=0..Sat=6 — **note this differs from `EmployeeWorkSchedule`'s Mon=0..Sun=6 convention**, unreconciled since this isn't wired to that system yet), in 30-minute increments 00:00–23:30, or "Closed". Constructor now takes `ITimeService` (`new ShiftManagerPanel(root, _timeService)` in `TopBarUI`) so the header row can show absolute day numbers (see below).

**Cell state model:** each Start/End cell is one of `NotSet` (blank, never touched — red background, .65 alpha), `Closed`, or a real time. A day where **both** Start and End are still `NotSet` silently resolves to `Closed` on Save & Close / a row's own "Enter Info" (the "simplify the process" default) — no error. A day left **half-filled** (one side set, the other blank) blocks submission. Setting Start to a real time **auto-populates End as +8.5 hours later** (a standard no-OT shift, clamped to 23:30 — no midnight wraparound modeled), which the player can still override manually. A Start-after-End on a day where both sides are real times, or any cell still `NotSet`, renders both that day's Start and End boxes in red (.66 alpha) and blocks submission with a toast.

**Per-row actions:** Name field placeholder reads "Shift Name (req.)"; typed name also mirrors into that row's "Start:"/"End:" labels. **Enter Info** (blue, matches the Start:/End: label color) validates/commits just that one row — "Shift Name is Required" / "Shift Name in Use" / "Please correct any errors before submitting the Schedule" toasts as appropriate. **Remove** (orange, far right, flush with the grid's right edge) now opens a small "Confirmation" / "Are you sure?" Yes/No sub-modal before deleting — no more instant delete. **Save & Close** (fire-hydrant red `#C1272D`) re-validates everything. The **✕** button is a separate **cancel/discard** path — "Confirmation" / "Exit? Changes may be lost." — Yes closes immediately with no validation (throws away unsaved edits), No cancels; it no longer silently behaves like Save & Close.

**Visuals (2026-06-26 pass):** restyled to match the Employee Roster / Hiring Board navy-and-blue aesthetic with Lilita One throughout (loaded via `AssetDatabase.FindAssets` in editor / `Resources.Load` in builds, same pattern as `LoadingScreenManager`); day headers show the full day name plus an absolute "Day NN" line underneath (computed from `ITimeService.Day` offset by each column's distance from "today"); title is centered; panel anchors **top-center** (not vertically centered) specifically so it can grow downward as shifts are added without running out of screen room — modal/shiftsContainer/each shift block all set `flexShrink = 0` and there's deliberately no `maxHeight`/scroll cap.

**Column-width gotchas worth knowing if you touch this file again:** the header banner row, the name/Enter-Info/Remove row, and each Start/End row are all pinned to the *same explicit* width (`LabelColWidth + DayColWidth*7 + SeparatorWidth*6`) because a plain `VisualElement` row otherwise defaults to stretching to fill its parent's cross-axis width, which silently let rows drift out of alignment with each other. Separately, `DropdownField` has its own content-driven min-width (popup arrow + longest time string) that can win over an explicit `width` and quietly grow a "fixed" column — fixed by wrapping each dropdown in a fixed-width `cellWrapper` (`overflow: Hidden`) with the dropdown's own `minWidth = 0`; the per-cell border was also moved off the dropdown onto a decorative `position: Absolute` overlay with zero layout weight so a border can't reintroduce the same drift. A related one: a column-flow `VisualElement`'s child does **not** auto-stretch along the vertical/main axis by default (only the horizontal/cross axis does) — the dropdown was leaving a gap at the bottom of its wrapper whenever the row stretched the wrapper taller than the dropdown's own natural height; fixed with `dropdown.style.flexGrow = 1`. That last fix compiled clean but has not yet been visually re-confirmed in-editor.

**Explicitly scoped as UI-only per Tad:** nothing here drives actual employee arrival/departure/overtime/attendance — `EmployeeRecord.shift`/`ShiftSchedule.cs`/`PayrollService` are completely unchanged and still what actually runs. Schedules built here are also **not persisted** — in-memory only, reset every Play session. A "Remove" button per shift and a "+ Add Shift" button were added as minimal necessary usability (not explicitly requested, but the UI is unusable without them).

**Planned follow-up, not built yet:** a second "side UI" for assigning individual employees to one of these named shifts (the Shift Manager only defines shift *templates* — Sun-Sat windows under a name — it doesn't touch any specific employee). When this and the gameplay wiring above both land, this whole system would replace `ShiftSchedule.cs`'s fixed Day/Evening/Night/Flexible windows with the player-defined ones.

**Confirmed, not changed:** Employee Roster (`EmployeeRosterUI.cs`) dragging was checked during this pass — it's already wired via the same shared `DraggableWindow` helper used here and by the Hiring Board (`DraggableWindow.cs`'s own doc comment specifically cites the roster's stretched top+bottom anchoring as the motivating case it was built to handle). No code change was needed there.

### Keybindings (rebound 2026-06-26)

F1–F4 were rebound to the number row to free up F-keys and make room for **5** (Shift Manager): **1** = Dev Console (`ToolsWindowController`, itself non-functional — see BUGS), **2** = Hiring Board, **3** = Employee Roster, **4** = Employee List, **5** = Shift Manager. All via `Keyboard.current.digitNKey`, not the numpad. Quicksave/load (F5/F6/F9, see Development Commands above) are unrelated F-keys, untouched.

### Inventory System — Milestone 1 (CORE COMPLETE — 2026-06-27)

**✅ COMPLETED: Core Data Structures**
- `PalletData.cs` — Pallet entity (SKU, qty, location, age, expiration); expiration detection
- `SkuData.cs` — Master product data (SO): pricing, shelf-life, Ti/Hi/CaseWeight, stacking rules, demand
- `SkuImporter.cs` — CSV-based importer (no COM deps); reads Days Supply + ItemSetup; cross-references by SKU; auto-generates pricing/shelf-life/stacking rules
- `InventoryService.cs` — Pallet lifecycle: create, move, pick, destroy; location indexing; daily spoilage checks; event publishing
- `ShipmentData.cs` + `ShipmentLineItem.cs` — Inbound tracking (supplier, ETA, line items, costs)
- `OrderData.cs` + `OrderLineItem.cs` — Order tracking (fulfillment, SLA, revenue, profit calc)
- `TestDataGenerator.cs` — Runtime test shipment/order generation from SKU database

**✅ COMPLETED: Excel Integration**
- **99 SKU assets** created from ItemFilesForForkIT.xlsx (Days Supply + ItemSetup sheets)
- Cross-reference validated: matched all Days Supply items with ItemSetup dimension data
- All assets populate Ti, Hi, CaseWeight, pricing, shelf-life categories
- Assets saved to: `Assets/_Project/Data/Inventory/SKUs/SKU_*.asset`

**✅ COMPLETED: Service Integration**
- InventoryService registered in GameContext.Awake(), initialized at startup
- Subscribes to OnDayChanged for daily spoilage detection
- Event system: OnPalletReceived, OnPalletMoved, OnPalletPartialPicked, OnPalletDestroyed, OnSpoilageDetected
- SkuDatabase loaded at runtime via `inventoryService.LoadSkuDatabase(skus)`

**⚠️ KNOWN ISSUE**
- Shadow atlas set to 16384×16384 to accommodate 240+ shadow maps; source unclear (no lights visible in Main.unity)
- Need to investigate on next session if warning persists

**📚 Documentation**
- `GAMEPLAY_LOOP_DESIGN.md` — 6-phase loop with mechanics, roles, 6-milestone roadmap, success metrics
- `MILESTONE_1_TECHNICAL_DESIGN.md` — Architecture, API, UI/UX specs, testing strategy, acceptance criteria
- `EXCEL_DATA_IMPORT_GUIDE.md` — Step-by-step workflow for CSV export and import

---

## TODO

Things that need to be built, in rough priority order. Move items here as they come up and remove them when done.

**Detailed gameplay loop design:** See `GAMEPLAY_LOOP_DESIGN.md` for complete mechanics, phasing, and technical architecture.

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
