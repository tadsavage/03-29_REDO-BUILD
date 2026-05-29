This technical overview provides a comprehensive analysis of the Unity project, focusing on its architecture, gameplay systems, and data structures.

# 1. Project Description
This project is a grid-based warehouse/industrial simulation and construction toolkit. It is designed for users to plan, build, and manage warehouse layouts. The core pillars of the experience are precision grid-based placement, financial management (capital and hourly costs), and persistent state management. Key features include a multi-state construction system (FSM), a robust undo/redo history, and a specialized stacking system for vertical storage simulation.

# 2. Gameplay Flow / User Loop
1.  **Boot & Initialization**: The game starts in the `Main` scene. `UIBootStrapper` initializes the core services and UI components.
2.  **Construction Loop**:
    *   **Selection**: User selects an industrial object (Racking, MHE, Floors) from the `BuildMenuUI`.
    *   **Preview & Validation**: A ghost object tracks the mouse on the grid. `PlacementValidator` checks for collisions and affordability.
    *   **Execution**: User places objects individually or via "Drag Placement" for bulk construction.
3.  **Management Loop**:
    *   **Time Progression**: `SimulationTimeService` drives the in-game clock.
    *   **Financial Impact**: Hourly costs are deducted from `CurrentCapital` every in-game hour.
    *   **Modification**: User can move or delete existing structures, utilizing `MoveState` and `DeleteState`.
4.  **Persistence**: User saves progress into slots, capturing both the grid state and a visual thumbnail via `SaveManager`.

# 3. Architecture
The project follows a decoupled, service-oriented architecture with a Finite State Machine (FSM) governing the primary interaction mode.
*   **GameContext**: Acting as a Service Locator, it holds references to `MoneyService` and `SimulationTimeService`, injecting them into the FSM.
*   **Placement FSM**: The `PlacementStateMachine` manages transitions between `IdleState`, `BuildState`, `MoveState`, and `DeleteState`.
*   **Command Pattern**: Every grid modification is encapsulated in a command (e.g., `PlaceCommand`, `MoveCommand`). This enables a robust Undo/Redo system managed by `CommandHistory`.
*   **UI Binding**: The UI (managed via `UIBootStrapper`) communicates with the FSM and Services through events, ensuring the logic remains independent of the presentation.

`Location: Assets/1. Scripts/1. FSM`

# 4. Game Systems & Domain Concepts

### Grid & Stacking System
Manages the physical world representation. It uses a 2D array of lists to support multiple objects in a single cell (Stacking).
*   `PlacementGrid`: Core data structure managing cell occupancy and height calculations.
*   `PlacementMath`: Utility for coordinate conversion and footprint rotation.
*   `BuildingData`: Component attached to prefabs storing their root cell, rotation, and current offsets.
`Location: Assets/1. Scripts/1. FSM/7. Math`

### Financial & Time Simulation
Simulates the economic constraints of warehouse management.
*   `MoneyService`: Tracks capital, hourly operational costs, and daily spending categories.
*   `SimulationTimeService`: Drives the game clock and triggers hourly/daily financial events.
*   `TimeDriver`: MonoBehavior that ticks the simulation services.
`Location: Assets/1. Scripts/1. FSM/7. Math/TimeAndMoney`

### Construction & Validation
Handles the logic of placing objects according to warehouse rules.
*   `PlacementValidator`: Evaluates if a footprint is valid based on stacking rules, ground requirements, and "bulldozer" clears.
*   `PlacementFinalizer`: Handles the actual instantiation, NavMesh warping, and grid registration.
*   `PreviewController`: Manages the "Ghost" objects and cell indicators during placement.
`Location: Assets/1. Scripts/1. FSM/4. Validation` | `5. Finalization`

# 5. Scene Overview
*   **Main Scene**: The primary workspace where all construction and simulation occur. It contains the `PlacementGrid`, `GameContext`, and UI hierarchies.
*   **ExampleScene (GuidanceLine)**: A specialized scene for testing the Guidance Line utility.
*   **Scene Flow**: The project is primarily single-scene focused. Transitions are handled via UI overlays (Save/Load/Splash) rather than scene loading, with the exception of the `SaveManager` which re-deserializes the grid state into the active scene.

`Location: Assets/8. Scenes`

# 6. UI System
The project uses UGUI with a controller-based pattern for complex windows.
*   `BuildMenuUI`: The primary interaction hub for selecting objects to build.
*   `TopBarUI`: Displays current money, time, and active tool state.
*   `WorldHoverPopupUI`: A world-to-screen UI that shows object details when hovering in `IdleState`.
*   `SaveLoadWindowController`: Manages the save slot interface, thumbnails, and metadata display.
*   `UIBootStrapper`: Orchestrates the initialization and dependency injection for all UI components.

`Location: Assets/3. UI`

# 7. Asset & Data Model
*   **ObjDataSO**: ScriptableObject defining an object's cost, prefab, footprint shape, and special rules (e.g., `isFloor`, `isStackable`).
*   **ObjDataRegistry**: A registry asset that maps IDs to `ObjDataSO` for serialization.
*   **SaveData**: A JSON-based model that stores `PlacedObject` arrays (ID, Position, Rotation) and `MoneyService` states.
*   **ChecklistData**: A data model for the internal development task tracker.
*   **Organization**: Assets are strictly categorized by type (Prefabs, Scripts, Models) with a numerical prefix for core system scripts.

`Location: Assets/1. Scripts/3. ScriptableObjects` | `Assets/_Saves`

# 8. Notes, Caveats & Gotchas
*   **Coordinate Convention**: The grid uses `Vector2Int` for logic, but the world uses Y-up. `PlacementGrid` handles the conversion. Always use `GetCellCenter()` to avoid z-fighting.
*   **Ground vs. Floor**: "Ground" (Foundations) objects are always at Y=0. "Floors" sit on top of Grounds. Normal objects stack on top of both.
*   **NavMesh Warping**: When moving or placing objects with `NavMeshAgent`, you must use `agent.Warp()` instead of `transform.position` to ensure the agent doesn't "snap" back to a previous valid position.
*   **Undo/Redo Stability**: Commands must store sufficient state (like `disabledObjects` list) to restore the grid exactly, especially when using the "Bulldozer" (clear) feature.
*   **ID Persistence**: Never change the `id` field in an `ObjDataSO` once it has been used in a save file, as the `SaveManager` relies on these IDs to reconstruct the scene.