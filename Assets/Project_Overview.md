# Technical Project Overview: Warehouse Simulation & Grid-Based Builder

## 1. Project Description
This project is a high-fidelity **Warehouse Simulation and Grid-Based Building System**. It allows users to design warehouse layouts using a specialized placement system, manage logistics through AI-driven workers and material handling equipment (MHE), and persist their progress through a robust save/load framework. The experience is defined by precision grid placement, real-time cost management, and dynamic AI pathfinding that responds to layout changes.

**Core Pillars:**
- **Precise Construction:** Grid-based placement with multi-place (drag) capabilities and footprint validation.
- **Logistics Simulation:** AI agents (Workers and Forklifts) with role-specific pathfinding costs and area preferences.
- **State Management:** A robust Command pattern for Undo/Redo and a State Machine for handling complex placement interactions.

## 2. Gameplay Flow / User Loop
1.  **Boot & Initialization:** The game starts in the `Main` scene. `UIBootstrapper` initializes the UI Toolkit (UITK) documents and connects them to the underlying services.
2.  **Building Mode:** Players select items (Racks, Barriers, Walls) from the `BuildMenuUI`. This transitions the `PlacementStateMachine` from `IdleState` to `BuildState`.
3.  **Validation & Costing:** Players preview placements on the grid. The `PlacementValidator` checks for overlaps and stacking rules, while the `MoneyService` validates affordability in real-time.
4.  **Simulation & Logistics:** Once objects are placed, `NavMeshManager` triggers rebaking. AI agents (`AiNavigation`) begin moving between `Waypoints`, preferring specific lanes based on their `AgentRole`.
5.  **Persistence:** Players can save their custom layouts to one of 8 slots via the `SaveManager`. This captures the grid state, object metadata, and a visual thumbnail.

## 3. Architecture
The project follows a decoupled, service-oriented architecture centered around a **Finite State Machine (FSM)** for user interaction and a **Command Pattern** for world modification.

-   **State Management:** `PlacementStateMachine` manages the flow between `Idle`, `Build`, `Move`, and `Delete` states. It centralizes shared dependencies like `RaycastController` and `PreviewController`.
-   **Execution Layer:** Changes to the world are encapsulated in `PlaceCommand`, `DeleteCommand`, and `DragPlaceCommand`. This allows for native Undo/Redo functionality through a `CommandHistory` stack.
-   **Dependency Injection:** A `GameContext` object serves as a service locator, providing states with access to `MoneyService`, `TimeService`, and other global utilities.
-   **UI Binding:** `UIBootstrapper` connects UITK VisualElements to C# logic, ensuring the UI remains decoupled from the core simulation logic.

`Location: Assets/1. Scripts/1. FSM`

## 4. Game Systems & Domain Concepts

### Placement System
-   `PlacementGrid`: Manages the underlying coordinate system and occupancy data.
-   `PlacementValidator`: Validates footprints and stacking rules (e.g., objects with `isStackable` flag).
-   `PreviewController`: Handles the "ghost" visual of objects before they are finalized.
-   `CellIndicatorController`: Provides visual feedback on the grid cells (Valid/Invalid/Hover).
-   **Extension:** To add new placement rules, implement a new validation check in `PlacementValidator`.

`Location: Assets/1. Scripts/1. FSM/1. Controllers`

### AI & Navigation System
-   `AiNavigation`: Controls agent movement using `NavMeshAgent`. It dynamically adjusts `AreaCost` based on `AgentRole` (e.g., Forklifts prefer "MHE Lanes").
-   `NavMeshManager`: Handles real-time NavMesh updates and surface initialization to ensure AI reacts to newly placed barriers or racks.
-   `Waypoint`: Defines destination nodes for the AI "Scurry" behavior.
-   **Extension:** New agent types can be added by extending the `AgentRole` enum and defining specific cost weights in `ApplyAgentCosts`.

`Location: Assets/1. Scripts/6. Utility`

### Save & Load System
-   `SaveManager`: Orchestrates the saving process, including JSON serialization and thumbnail capture.
-   `PlacementSystem`: Handles the actual conversion of placed GameObjects into `PlacedObject` data models.
-   `SaveThumbnailCapture`: Captures a screenshot of the current view to provide visual context in the save menu.

`Location: Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts`

## 5. Scene Overview
-   **SampleScene (Startup):** Typically used for testing or as a splash entry point.
-   **Main (Active):** The primary simulation environment. Contains the grid, UI hierarchy, and global managers.
-   **_Recovery Scenes:** A series of backup scenes (`0 (1).unity` through `0 (46).unity`) likely used for version recovery or automated backups.
-   **Scene Flow:** The project uses a single-scene simulation model where layout changes happen dynamically on the grid rather than via scene transitions.

`Location: Assets/8. Scenes`

## 6. UI System
The project uses **UI Toolkit (UITK)** for its primary interface.
-   **Structure:** The UI is split into `HUD` (TopBar, CostPreview) and `BuildMenu` (BottomBar).
-   **Binding:** `UIBootstrapper` queries the `UIDocument` using `rootVisualElement.Q<T>()` and injects dependencies into UI controllers.
-   **Popups:** `WorldHoverPopupUI` provides contextual information when hovering over buildings, controlled exclusively by the `IdleState` to prevent UI clutter during construction.
-   **Feedback:** `Toast` and `PreviewCostUI` provide immediate feedback on actions and costs.

`Location: Assets/3. UI`

## 7. Asset & Data Model
-   **ScriptableObjects:**
    -   `ObjDataSO`: Defines object metadata (name, cost, footprint, rotation rules, stackability).
    -   `ObjDataRegistry`: A central repository of all placeable objects for the `SaveManager` to reference by ID.
-   **Prefabs:** Organized by category (Barriers, MHE, Racking, Workers). Prefabs for placeable objects must contain a `BuildingData` component.
-   **Data Storage:** Saves are stored in `Assets/_Saves/` as JSON files (`slot_X_data.json`) with corresponding PNG thumbnails.

`Location: Assets/1. Scripts/3. ScriptableObjects/SO Scripts`

## 8. Notes, Caveats & Gotchas
-   **NavMesh Baking:** AI agents wait for the NavMesh to be ready (`agent.isOnNavMesh`) during load. If an agent is spawned off-mesh, the `AiNavigation` script attempts to warp it to the nearest valid surface.
-   **UI Input Blocking:** The `UIInputGuard` or `RaycastController.IsPointerOverUI` check is critical; ensure it is called before processing grid clicks to prevent "clicking through" the UI.
-   **Rotation Logic:** Footprint offsets are calculated dynamically based on the current rotation. When adding new large objects, ensure the `ObjDataSO` footprint matches the visual model's bounds.
-   **Undo/Redo:** The `CommandHistory` is volatile and is not persisted in save files. It only tracks actions within the current session.