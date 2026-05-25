# Project Overview: Warehouse Simulation & Building System

This Unity project is a sophisticated warehouse simulation and layout planning tool. It features a robust grid-based building system with stacking mechanics, economy simulation (capital and hourly costs), and an undo/redo command architecture. The project is designed for users to design, optimize, and simulate warehouse operations, featuring specialized assets like pallet jacks, racking, and AI-driven workers/rats.

## 1. Project Description
The project is a professional-grade warehouse simulator focused on logistics, spatial planning, and financial management. It allows users to construct complex warehouse layouts using a variety of structural (walls, floors, foundations) and operational (racks, MHE - Material Handling Equipment) components.
- **Core Pillars:** Precision grid-based placement, vertical stacking, economic sustainability, and dynamic environment simulation (AI navigation, lighting flickers, and soundscapes).
- **Target Audience:** Logistical planners, warehouse managers, or simulation enthusiasts.

## 2. Gameplay Flow / User Loop
1. **Boot:** The game initializes via `UIBootstrapper`, setting up the `GameContext` and injecting dependencies into the UI and FSM.
2. **Construction:** Users select objects from the `BuildMenuUI`. The `PlacementStateMachine` transitions into `BuildState`, allowing for single-click or drag-placement.
3. **Management:** Placed objects incur an `hourlyCost`. Users must balance their `CurrentCapital` against operational expenses driven by the `TimeDriver` and `MoneyService`.
4. **Optimization:** Users can use `MoveState` to reorganize or `DeleteState` to remove inefficient structures. The Command pattern allows for frequent experimentation with Ctrl+Z/Ctrl+Y.
5. **Simulation:** AI entities (workers and rats) navigate the environment using a dynamically updated NavMesh managed by `NavMeshManager`.

## 3. Architecture
The project follows a decoupled, service-oriented architecture centered around a `GameContext` and a State Machine.

### Placement & State Management
* `PlacementStateMachine`: Central hub managing transitions between interaction states (Idle, Build, Move, Delete).
* `PlacementController`: Bridges the UI layer (`BuildMenuUI`) to the FSM logic.
* `GameContext`: A Service Locator/Dependency Injection hub providing access to `MoneyService`, `TimeService`, and other global logic.
* `CommandHistory`: Implements the Command Pattern, storing `ICommand` objects (e.g., `PlaceCommand`, `DeleteCommand`) to support multi-level undo/redo.
`Location: Assets/1. Scripts/1. FSM/`

### Data Flow & Registry
* `ObjDataRegistry`: A ScriptableObject-based database containing all buildable items.
* `ObjDataSO`: Defines the physical properties (footprint, height), economic costs, and prefabs for objects.
* `BuildingData`: A component attached to all placed objects in the scene, storing its runtime grid position, rotation, and original SO data.
`Location: Assets/1. Scripts/3. ScriptableObjects/SO Scripts/`

## 4. Game Systems & Domain Concepts

### Grid & Stacking System
* `PlacementGrid`: Manages a 2D array of cells. Each cell can hold a stack of objects (`List<PlacedObject>`).
* `PlacementValidator`: Enforces placement rules (inside grid, level surface, stack height limits).
* `PlacementMath`: Provides utility functions for world-to-grid conversions and footprint rotations.
* **Domain Concept - Stacking:** Objects have an `objHeight`. The grid tracks `currentStackHeight` to allow objects like boxes to be placed on top of racking or foundations.
`Location: Assets/1. Scripts/1. FSM/7. Math/`

### Economy & Time
* `MoneyService`: Handles capital, spending categories, and hourly cost deductions.
* `TimeDriver`: Drives the simulation clock, triggering "Hour Changed" events that invoke the `MoneyService` to deduct operating costs.
`Location: Assets/1. Scripts/1. FSM/7. Math/TimeAndMoney/`

### AI & Navigation
* `NavMeshManager`: Manages dynamic NavMesh baking. It debounces updates to ensure performance when many objects are placed rapidly.
* `AiNavigation`: Custom logic for agent movement and guidance.
* `RatBehavior`: Ambient AI that adds life (or pests) to the warehouse environment.
`Location: Assets/1. Scripts/6. Utility/`

## 5. Scene Overview
* **Main Scene (`Main.unity`):** The primary workspace. It contains the `PlacementController` and the main UI canvas. It relies on `UIBootstrapper` to connect scene-level managers to the UI.
* **Example Scenes:** Scenes like `ExampleScene.unity` under `GuidanceLine` are used for testing specific utility features.
* **Scene Rules:** Most systems are persistent or initialized via the `GameContext` found in the root of the Main scene.

## 6. UI System
The project uses UGUI with a custom "Bootstrapper" pattern.
* `BuildMenuUI`: The primary interface for selecting objects, categories, and triggering Delete/Move modes.
* `WorldHoverPopupUI`: A world-space UI that displays object details (cost, name) when hovering in `IdleState`.
* `TopBarUI`: Displays current money, time, and active tool information.
* `PreviewCostUI`: Floats near the cursor during placement to show the cost of the current operation.
* **Binding:** UI buttons call methods on the `PlacementController`, which then triggers the FSM.
`Location: Assets/3. UI/`

## 7. Asset & Data Model
* **ScriptableObjects:** Used for configuration (`ObjDataSO`), audio libraries (`SoundLibrary`), and data registries (`ObjDataRegistry`).
* **Prefabs:** Organized by category (Barriers, MHE, Racking, Workers). Each buildable prefab requires a `BuildingData` and `PlacedObject` component to interface with the grid.
* **Save Data:** Uses JSON serialization. Files like `slot_0_data.json` store the state of the warehouse, which is reconstructed using `PlacementGrid.RebuildFromRegistry()`.
`Location: Assets/2. Prefabs/` and `Assets/_Saves/`

## 8. Notes, Caveats & Gotchas
* **Grid Origin:** The grid uses an `Origin` Vector3. Ensure all scene foundations are aligned to this origin or placement will be offset.
* **NavMesh Updating:** The `NavMeshManager` uses a debounce timer. Frequent placement might feel like the NavMesh is "lagging" behind, but this is intentional to prevent frame drops.
* **Foundations vs. Floors:** Foundations have physical height and contribute to the stack; Floors are treated as zero-height overlaps that sit at the bottom of the stack.
* **Undo/Redo & Disabled Objects:** The `PlacementFinalizer` often disables objects (like replaced floors) instead of destroying them immediately to allow for `Undo` operations to restore them.