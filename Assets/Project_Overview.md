# Project Overview: Warehouse Simulation & Placement System

## 1. Project Description
This project is a 3D warehouse planning and management simulation built in Unity 6. It allows users to design warehouse layouts by placing racking, barriers, floors, and machinery on a grid-based system. The core experience focuses on spatial optimization, financial management (capital and hourly costs), and operational flow (AI navigation). It is designed for logistics planners or simulation enthusiasts to test warehouse configurations and operational efficiency.

**Core Pillars:**
- **Precise Grid Placement:** A robust FSM-driven system for placing, moving, and deleting warehouse assets.
- **Economic Simulation:** Tracking of placement costs and hourly operational expenses.
- **Spatial Logic:** Complex footprint validation including stacking, floor-layering, and "bulldozer" mechanics.
- **Operational Testing:** AI NavMesh integration to simulate worker and vehicle movement within the designed layout.

## 2. Gameplay Flow / User Loop
1.  **Boot & Setup:** The game initializes via a `UIBootstrapper`, setting up the `GameContext` and linking UI to the `PlacementStateMachine`.
2.  **Design Phase:** The user selects objects from the `BuildMenuUI`. They transition from `IdleState` to `BuildState`, using a 3D preview to position assets on the grid.
3.  **Validation & Placement:** The system validates the footprint (checking for level surfaces and stacking rules). Placing an object subtracts funds and updates the `PlacementGrid`.
4.  **Refinement:** Users use `MoveState` or `DeleteState` to optimize the layout. Actions can be undone/redone via the `CommandHistory`.
5.  **Simulation/Management:** The game tracks time and money via `GameContext`. NavMesh updates allow AI agents to navigate the new layout.
6.  **Persistence:** Users save their layouts to one of 8 slots, which includes a generated thumbnail for easy identification.

## 3. Architecture
The project follows a decoupled, state-driven architecture with a clear separation between data (ScriptableObjects), logic (FSM), and visuals.

### Placement FSM
The heart of the interaction logic, managing user input and state transitions.
- `PlacementStateMachine`: The central hub that switches between build, move, and delete modes.
- `IPlacementState`: Interface defining `OnEnter`, `Tick`, and `OnExit` for all states.
- `BuildState`, `MoveState`, `DeleteState`: Concrete implementations handling specific interaction logic.
- `CommandHistory`: Implements the Command pattern to support Undo/Redo for all placement actions.
- `PlacementActions`: C# wrapper for the New Input System.
`Location: Assets/1. Scripts/1. FSM`

### Execution & Validation
Separates the "intent" to place from the "rules" of placement.
- `PlacementValidator`: Logic for checking footprint overlaps, stack heights, and surface leveling.
- `PlacementFinalizer`: Handles the actual instantiation, parenting, and registration of objects.
- `PlacementGrid`: Data structure tracking what occupies every (x, y) cell, including stack heights.
`Location: Assets/1. Scripts/1. FSM/4. Validation & 5. Finalization`

### Global Context
Provides shared services to all systems without strict singletons where possible.
- `GameContext`: Holds references to `MoneyService`, `TimeService`, and other global simulation data.
`Location: Assets/1. Scripts/1. FSM/7. Math/TimeAndMoney`

## 4. Game Systems & Domain Concepts

### Grid & Stacking System
A multi-layered grid system that supports complex spatial rules.
- `ObjDataSO`: Defines the footprint (rectangular or custom), height, and stacking capabilities of an object.
- **Stacking Logic:** Objects marked as `isStackable` can be placed on top of each other, with the system calculating the cumulative `objHeight`.
- **Floor Logic:** Objects marked as `isFloor` (like lanes) can exist under other objects and do not block placement.
`Location: Assets/1. Scripts/3. ScriptableObjects/SO Scripts`

### AI & Navigation
Simulates warehouse operations using Unity's AI Navigation.
- `NavMeshManager`: Handles runtime NavMesh baking when the layout changes.
- `AiNavigation`: Controls agent behavior and pathfinding.
- `pathfindingClear`: A property in `ObjDataSO` that allows objects (like doors) to be ignored by NavMesh obstacles.
`Location: Assets/1. Scripts/6. Utility`

### Checklist System
A task-tracking system for guiding users through build phases or objectives.
- `ChecklistData`: Serialized structure for sections, tasks, and subtasks.
- `ChecklistTaskManager`: Handles the logic of task completion and progress tracking.
`Location: Assets/1. Scripts/11. ChecklistSystem`

## 5. Scene Overview
- **Main Scene:** The primary environment containing the `PlacementGrid`, `GameContext`, and UI Canvas. It serves as the sandbox for all gameplay.
- **ExampleScene (GuidanceLine):** A demonstration scene for the line rendering/guidance system used for AI or path visualization.
- **Scene Flow:** Currently, the project operates in a single-scene sandbox mode. Layouts are loaded into the `Main` scene via the Save/Load system rather than scene switching.

## 6. UI System
The project uses UGUI with a controller-based architecture.
- `UIBootstrapper`: Injects dependencies (like the `PlacementStateMachine`) into UI components at runtime.
- `BuildMenuUI`: A dynamic menu populated by the `ObjDataRegistry` to show available warehouse assets.
- `WorldHoverPopupUI`: A world-space/screen-space hybrid popup that displays object info (name, cost) when hovering in `IdleState`.
- `SaveLoadWindowController`: Manages the 8-slot save system, including thumbnail display and slot metadata.
`Location: Assets/3. UI`

## 7. Asset & Data Model
- **ObjDataSO:** The primary data definition for all placeable items. It contains cost, footprint, prefab references, and simulation rules.
- **ObjDataRegistry:** A ScriptableObject collection used to catalog all available `ObjDataSO` assets for the UI.
- **SaveData:** JSON-based persistence. It stores the ID, position, rotation, and stack level of every placed object.
- **SaveMetadata:** Stores high-level info about a save (timestamp, custom name, thumbnail path) to populate the Load menu without loading the full game state.
`Location: Assets/1. Scripts/3. ScriptableObjects`

## 8. Notes, Caveats & Gotchas
- **Rotational Logic:** The `ObjDataSO.GetFootprintOffsets` method handles 90-degree rotations. When adding custom shapes, ensure the `customShapeOffsets` are defined relative to a (0,0) root.
- **Layering:** "Ground" (foundations) and "Floors" (markings) are handled differently. Ground replaces existing ground, while Floors can coexist with any object.
- **NavMesh Baking:** High-frequency placement/deletion may cause performance spikes if the NavMesh re-bakes too often. The `NavMeshManager` should ideally batch these updates.
- **Save Folder:** Saves are stored in `Assets/_Saves`. This folder must exist or be created at runtime for the `SaveManager` to function correctly.