# Project Technical Documentation: Redo-Build Simulation

## 1. Project Description
This project is a high-fidelity warehouse management and construction simulation. It targets users interested in facility logistics, allowing them to design, build, and manage warehouse environments. The core pillars of the experience are precision grid-based placement, real-time economic management (capital and hourly operating costs), and a robust undo/redo system for design iteration.

## 2. Gameplay Flow / User Loop
1.  **Boot & Initialization**: The game starts in the `Main` scene where `GameContext` initializes services (Money, Time) and `UIBootstrapper` connects the UI Toolkit documents to the game logic.
2.  **Exploration**: The user navigates the environment using a `FreeLookCamera`, hovering over existing structures to see status popups (name, cost, hourly maintenance).
3.  **Construction/Management**:
    *   **Build Mode**: Users select assets (Racking, Barriers, MHE) from the `BuildMenuUI`. They place objects on a grid, with real-time cost previews.
    *   **Modification**: Users can move existing objects or delete them to optimize the layout.
    *   **Simulation**: Time progresses (`SimulationTimeService`), triggering hourly deductions from the `MoneyService` based on the facility's cumulative `hourlyCost`.
4.  **Persistence**: Users save their progress via the `SaveSystem`, which captures object registries and financial states into JSON metadata and thumbnails.

## 3. Architecture
The project follows a decoupled, service-oriented architecture with a heavy emphasis on the Command and State patterns for the construction systems.

*   **Service Layer**: `GameContext` acts as the service locator/provider for `MoneyService` and `SimulationTimeService`.
*   **State Machine**: `PlacementStateMachine` manages user interaction modes (Idle, Build, Move, Delete).
*   **Command Pattern**: Every modification to the game world (Place, Move, Delete) is encapsulated in a class implementing `ICommand`, stored in `CommandHistory` for undo/redo functionality.
*   **UI Binding**: `UIBootstrapper` links the UI Toolkit (UITK) documents to the underlying C# services, ensuring a clean separation between view and logic.

## 4. Game Systems & Domain Concepts

### Placement & Construction System
A state-driven system for grid-based object manipulation.
*   `PlacementStateMachine`: Manages transitions between build, move, delete, and idle states.
*   `PlacementGrid`: Handles the underlying spatial data and object stacking.
*   `PlacementValidator`: Checks for collisions or invalid placement rules before finalizing.
*   `PreviewController`: Manages the visual "ghost" of objects before they are placed.
*   `CommandHistory`: Stores `ICommand` objects for undo/redo.
`Location: Assets/1. Scripts/1. FSM/`

### Economics & Time System
Handles the simulation's progression and financial constraints.
*   `MoneyService`: Tracks capital, daily spending, and hourly maintenance costs.
*   `SimulationTimeService`: Drives the in-game clock (Minute, Hour, Day) and triggers economic events.
*   `TimeDriver`: Bridges Unity's `Update` loop to the `SimulationTimeService`.
`Location: Assets/1. Scripts/1. FSM/7. Math/TimeAndMoney/`

### Checklist & Task System
A data-driven system for tracking project milestones.
*   `ChecklistRoot`: Root container for sections, tasks, and subtasks.
*   `ChecklistTaskManager`: (Editor) Manages the JSON-based task lists.
`Location: Assets/1. Scripts/11. ChecklistSystem/`

## 5. Scene Overview
*   **Main**: The primary simulation scene. Contains the `GameContext`, the grid-based environment, and the UI root.
*   **_Recovery/**: Contains multiple backup scenes (`0.unity` through `0 (12).unity`) likely used for version recovery or iterative testing.
*   **SampleScene**: Default Unity scene, likely unused in the final build.

## 6. UI System
The project uses **UI Toolkit (UITK)** for its interface.
*   **UIBootstrapper**: The central injection point that finds `UIDocument` components and initializes UI controllers.
*   **BuildMenuUI**: A dynamic menu populated from `ObjDataRegistry` allowing users to select items for placement.
*   **TopBarUI**: Displays real-time money and time data, bound to their respective services.
*   **WorldHoverPopupUI**: A world-space/screen-space hybrid that follows the mouse to show object metadata using `RaycastController` data.
*   **PreviewCostUI**: Specifically shows the cost/refund impact of the current placement action.
`Location: Assets/3. UI/`

## 7. Asset & Data Model
*   **ScriptableObjects**:
    *   `ObjDataSO`: Defines individual object properties (name, mesh, cost, hourly cost, grid footprint).
    *   `ObjDataRegistry`: A collection of all placeable `ObjDataSO` items, used to populate build menus.
    *   `SoundDefinition`: Maps audio clips to specific game events.
*   **Persistence**:
    *   `SaveData`: JSON structure containing the state of the `PlacedObjectRegistry`, money, and time.
    *   `PlacedObject`: Stores the position, rotation, and data reference for every object in the grid.
`Location: Assets/1. Scripts/3. ScriptableObjects/`

## 8. Notes, Caveats & Gotchas
*   **Undo/Redo Lifecycle**: When adding a new placement command, ensure the `CommandHistory` is updated, or the financial state (`MoneyService`) will desync from the visual state.
*   **UI Injection**: `UIBootstrapper` must have references to both the `UIDocument` and the `GameContext` services. If the UI doesn't update, check the `Awake` initialization order in `UIBootstrapper`.
*   **Grid Footprint**: Objects with complex footprints (defined by `Vector2Int[] offsets` in `PlaceCommand`) must have their pivots correctly aligned in Blender to ensure `PlacementMath` calculates grid cells accurately.
*   **Floor Disabling**: The `PlaceCommand` automatically disables floor tiles underneath large objects to prevent Z-fighting and improves performance. These are re-enabled during `Undo`.