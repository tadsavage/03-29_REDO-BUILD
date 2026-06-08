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

Plain C# classes (not MonoBehaviours) held by `GameContext`. `SimulationTimeService` runs at 1 real-second = 1 in-game-minute. `MoneyService` fires `OnMoneyChanged` on any balance change; hourly costs are applied via event subscription. `MoneyService` now takes a `sellBackRate` (0–1 float) that scales all deletion refunds — `DeleteCommand` uses `_money.SellBackRate` to compute the adjusted refund.

### Data Model

**ObjDataSO** (`Assets/1. Scripts/3. ScriptableObjects/SO Scripts/ObjDataSO.cs`)

ScriptableObject describing a placeable item: `id` (unique int), `prefab`, `cost`, `hourlyCost`, `footprint` (Vector2Int), optional `customShapeOffsets`, stacking rules, pathfinding flags, and special behavior flags (`isFloor`, `ClearsGridAfterPlacement`, `ignorePlacementRules`).

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

---

## TODO

Things that need to be built, in rough priority order. Move items here as they come up and remove them when done.

- [ ] Transition from build phase to live gameplay loop
- [ ] Employee AI — basic pathfinding and task assignment
- [ ] Inventory system — receiving, storing, and fulfilling orders
- [ ] Ordering system — suppliers, lead times, restock triggers
- [ ] Move save files out of `Assets/_Saves/` to `Application.persistentDataPath` (required before any real build/release)
- [ ] Replace `FindFirstObjectByType` calls in `PlacementStateMachine.Start()` with proper scene references
- [ ] Review and playtest difficulty balance (starting capital + sell-back rate per level)

---

## BUGS & ISSUES

Known problems that need fixing. Add to this list as issues are discovered.

- Save files currently write to `Assets/_Saves/` — this works in the Editor but will break in a built player. Must be moved to `Application.persistentDataPath` before shipping.
- `SimulationTimeService` time scale (1 real-second = 1 in-game-minute) is a placeholder — needs tuning/configurability before gameplay feels right.
- **ToolsWindow (`Assets/3. UI/7.ToolsWindow/`) is non-functional** — none of the following work: tilde toggle, X close button, tab switching, panel drag. Mouse scroll in the panel also bleeds through to the game camera. Root cause likely: PanelSettings misconfiguration, `Start()` silently failing before event wiring runs, or UIDocument not blocking input. Needs full debug pass.
