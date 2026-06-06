# Project Overview: Warehouse Management & Logistics Simulation

## 1. Project Description
This project is a high-fidelity warehouse logistics simulation where players design, build, and manage a distribution center. Players balance capital, layout efficiency, and operational hazards (such as rat infestations) to create a profitable shipping hub. The experience focuses on realistic warehouse components—racking, MHE (Material Handling Equipment), and docking systems—integrated with a dynamic economic and AI-driven environment.

## 2. Gameplay Flow / User Loop
1.  **Boot & Main Menu:** The user starts at the `MainMenu` scene, where they can choose a difficulty (Clerk, Supervisor, Manager) which dictates starting capital and refund rates.
2.  **Initialization:** Upon entering the `Main` scene, `GameContext` initializes the `MoneyService` and `SimulationTimeService`. If it is a new game, the entire grid is auto-populated with "Yard Floor" tiles.
3.  **Build Phase:** Users use the grid-based building system to place walls, floors, racks, and functional equipment. Every object has an upfront cost and often an hourly maintenance cost.
4.  **Operational Phase:** Time progresses via the `TimeDriver`. Workers navigate the facility, trucks arrive at docks, and pests (rats) may spawn and contaminate inventory.
5.  **Management Loop:** The player must optimize the layout to handle more volume while managing cash flow. Hourly costs are deducted automatically, and the player can save/load their progress at any time.

## 3. Architecture
The project follows a decoupled, service-oriented architecture centered around a "Context" pattern and a State Machine for user interaction.

### Core Architecture Components
*   `GameContext`: The central hub that instantiates and holds references to global services like `MoneyService` and `TimeService`. It manages the transition between new game setup and loading existing saves.
*   `PlacementStateMachine`: Manages all player-to-world interactions. It toggles between states like `BuildState`, `MoveState`, and `DeleteState` to handle world modification.
*   `PlacedObjectRegistry`: A static or singleton registry that tracks every built object in the scene, facilitating easy lookup for AI (like rats finding food) and the Save/Load system.
*   `Command Pattern`: Used within the placement system (`PlaceCommand`, `DeleteCommand`, etc.) to support multi-level Undo/Redo functionality via `CommandHistory`.

`Location: Assets/1. Scripts/1. FSM/1. Controllers`

## 4. Game Systems & Domain Concepts

### Placement & Grid System
The core engine for world modification, utilizing a discrete grid for object alignment and footprint validation.
*   `PlacementGrid`: Defines the world bounds and tracks occupied cells.
*   `PlacementValidator`: Checks for overlaps and valid placement rules based on object footprints.
*   `PreviewController`: Handles the visual "ghost" of an object before it is permanently placed.
*   `PlacementFinalizer`: The actor that actually instantiates the prefab and registers it with the world.

`Location: Assets/1. Scripts/1. FSM`

### Economy & Time System
A ticking simulation that drives the "business" side of the warehouse.
*   `MoneyService`: Tracks current balance, applies refunds, and handles hourly maintenance deductions.
*   `SimulationTimeService`: Managed by `TimeDriver`, it converts real-time to game-time, triggering daily and hourly events.

`Location: Assets/1. Scripts/1. FSM/7. Math/TimeAndMoney`

### AI & Agent System
Simulates life within the warehouse, including workers and pests.
*   `NavMeshManager`: Handles synchronous and asynchronous baking of the NavMesh as the player modifies the environment.
*   `RatBehavior`: A complex AI that scavenges, hides under pallets, breeds based on population caps, and contaminates inventory via `ContaminationState`.
*   `AgentTypeTag`: An enum-based tagging system used by AI to identify targets (e.g., Rats identifying "Human" or "Exterminator").

`Location: Assets/1. Scripts/6. Utility`

## 5. Scene Overview
*   **MainMenu:** The entry point. Handles player profile creation, difficulty selection, and global settings.
*   **Main:** The primary simulation environment. It is a persistent space where the `PlacementGrid` is located.
*   **Recovery Scenes:** Located in `_Recovery`, these appear to be auto-generated or backup versions of scene states, likely for development safety.

## 6. UI System
The project uses **Unity UI Toolkit (UITK)** for complex menus and **UGUI** for world-space overlays.
*   `UIBootstrapper`: The main entry point for UI; it finds `UIDocument` components and injects service references into specific controllers.
*   `BuildMenuUI`: A UITK-based bottom bar that generates categories of buildable items from the `ObjDataRegistry`.
*   `TopBarUI`: Displays the clock and money, and provides access to the Save/Load menu.
*   `WorldHoverPopupUI`: A dynamic overlay that follows the mouse to show object details (name, cost, maintenance) in the 3D view.
*   **Extension:** To add a new screen, create a UXML file, add it to a `UIDocument`, and create a controller class that inherits from a base UI script, then register it in `UIBootstrapper`.

`Location: Assets/3. UI`

## 7. Asset & Data Model
*   **ScriptableObjects (`ObjDataSO`):** The primary data container for every buildable object. Defines the prefab, UI icon, name, cost, hourly cost, and grid footprint.
*   **JSON Save System:** Game state is serialized into JSON files (found in `_Saves`). It stores the position, rotation, and `ObjDataSO` ID of every `PlacedObject`.
*   **Registry Pattern:** `ObjDataRegistry` is a ScriptableObject that maintains a master list of all available `ObjDataSO` assets to ensure consistent ID mapping during serialization.
*   **Checklist System:** `BuildPhaseTasks.json` stores externalized task data, allowing for dynamic objective updates without code changes.

`Location: Assets/1. Scripts/3. ScriptableObjects`

## 8. Notes, Caveats & Gotchas
*   **NavMesh Baking:** Since the game is grid-based and highly modifiable, the `NavMeshManager` must be triggered after major placement batches. Be aware that `BakeSynchronous` can cause frame hitches on very large grids.
*   **Object Origin:** Placement logic assumes the origin of prefabs is at the bottom-center/corner to align correctly with the `PlacementGrid`.
*   **Rat Population:** Rats are not "saved" in the standard JSON save file (they are bred dynamically). Only rats placed via the build menu are persistent across sessions.
*   **UI Input Blocking:** Ensure `UIInputGuard` or `pickingMode` is correctly set on new UITK documents, otherwise, you may find yourself unable to click objects in the world through "invisible" UI layers.