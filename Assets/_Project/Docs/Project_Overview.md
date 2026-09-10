# Project Overview: Warehouse Logistics Simulation

## 1. Project Description
This project is a high-fidelity Warehouse Logistics Simulation designed for management training and operational optimization. It allows users to design warehouse layouts, manage inventory (pallets and SKUs), hire and schedule employees, and oversee the financial health of the operation. The core pillars of the experience are **Spatial Planning** (grid-based construction), **Resource Management** (staffing and payroll), and **Logistical Flow** (inventory tracking and movement).

## 2. Gameplay Flow / User Loop
1.  **Boot & Setup:** Users start at the `MainMenu`, where they can begin a "New Game" (choosing difficulty) or "Load" an existing warehouse.
2.  **Warehouse Construction:** Using the `BuildMenuUI`, players place floors, racking, and equipment. A Finite State Machine (`PlacementStateMachine`) handles the transition between building, moving, and deleting objects.
3.  **Operations Management:**
    *   **Inventory:** Shipments arrive as `PalletData` and are staged at receiving.
    *   **Labor:** Players hire employees through the `EmployeeSystem`, managing their shifts and roles (Workers, Drivers, MHE Operators).
4.  **Simulation Loop:** As in-game time passes (`SimulationTimeService`), employees move goods, inventory may spoil, and expenses (wages/upkeep) are deducted.
5.  **Persistence:** Players save their progress via the `SaveManager`, which captures the grid state, economy, and world thumbnails.

## 3. Architecture
The project follows a **Service-Oriented Architecture** managed by a central **Service Locator**.

### Core Services
*   `ServiceLocator`: A static registry that enables dependency injection for decoupling logic from Unity's `MonoBehaviour` lifecycle.
*   `GameContext`: The main entry point for the simulation. It initializes all C# services, registers them with the locator, and manages the initial world population (yard floors and NavMesh).
*   `EventManager`: A global event bus (`GameEvents`) used for inter-system communication (e.g., Time -> Economy -> UI).
*   `BuildService`: Manages the high-level build state and `CommandHistory` for undo/redo functionality.

`Location: Assets/_Project/Scripts/1. FSM/1. Services/`

## 4. Game Systems & Domain Concepts

### Placement & Grid System
*   `PlacementGrid`: Manages the logical 2D grid where all objects are stored.
*   `PlacementStateMachine`: Controls user interaction modes (`BuildState`, `MoveState`, `DeleteState`, `IdleState`).
*   `CommandHistory`: Implements the **Command Pattern** to provide multi-level undo/redo for all construction actions.
*   `PlacementValidator`: Ensures objects are only placed in valid locations according to footprint and collision rules.

`Location: Assets/_Project/Scripts/Core/FSM/`

### Economy & Time System
*   `SimulationTimeService`: Advances the in-game clock (Minute/Hour/Day) and scales simulation speed (Pause, 1x, 2x, 3x).
*   `MoneyService`: Tracks capital, daily spending, and historical financial data across categories (Wages, Construction, Upkeep).
*   `PayrollService`: Automatically deducts hourly wages for all active employees based on their role and overtime status.
*   `EconomyService`: Tracks recurring hourly costs of placed infrastructure (e.g., equipment maintenance).

`Location: Assets/_Project/Scripts/Core/Managers/TimeAndMoney/`

### Inventory & Logistics
*   `InventoryService`: Manages the lifecycle of `PalletData`. It handles receiving shipments, putaway logic, and FIFO/LIFO picking from storage lanes.
*   `LaneNamingService`: Derives warehouse addresses (e.g., "Door 1, Lane A, Slot 3") based on the spatial grid layout.
*   `PalletData`: A data-only class representing a unit of stock, including SKU ID, quantity, and expiration date.

`Location: Assets/_Project/Scripts/Core/Inventory/`

### Employee System
*   `EmployeeRegistry`: A central database of all hired staff.
*   `EmployeeGenerator`: Procedurally generates new employees with unique identities and traits.
*   `AiNavigation`: Interfaces with `NavMeshSurface` to handle agent pathfinding within the warehouse.

`Location: Assets/_Project/Scripts/Actors/EmployeeSystem/`

## 5. Scene Overview
*   **MainMenu:** Handles initial configuration, difficulty selection, and slot-based loading.
*   **Main:** The primary simulation scene. It contains the `PlacementGrid`, `GameContext` for service initialization, and the UI root. It is designed to be persistent, with the environment being rebuilt dynamically from save data.

`Location: Assets/_Project/Scenes/`

## 6. UI System
The project primarily uses **UI Toolkit (UITK)** for its interface.
*   `BuildMenuUI`: The primary interaction hub for construction. It uses `VisualTreeAsset` (UXML) templates to dynamically build category buttons and item lists.
*   `WorldHoverPopupUI`: A floating info card that displays object details (cost/upkeep) when hovering over the warehouse in `IdleState`.
*   `SaveLoadWindowController`: Manages the visual interface for the 8-slot save system, including thumbnail previews.
*   **Binding:** UI elements subscribe to events from the `ServiceLocator` (e.g., `MoneyService.OnCapitalChanged`) to update labels.

`Location: Assets/_Project/Scripts/UI_UX/`

## 7. Asset & Data Model
*   **ScriptableObjects:** `ObjDataSO` defines building metadata (prefabs, cost, names). `SkuData` defines inventory items.
*   **Prefabs:** Located in `_Project/Prefabs`, these contain the 3D models and `BuildingData` components required for grid placement.
*   **Save Data:** Uses JSON serialization. `SaveManager` stores files in `Assets/_Saves/`.
    *   `metadata.json`: Tracks slot info (timestamps, save names).
    *   `slot_X_data.json`: Contains the serialized grid state and economy values.
    *   `slot_X_thumb.png`: Screen capture of the warehouse at the time of saving.

`Location: Assets/_Project/ScriptableObjects/`

## 8. Notes, Caveats & Gotchas
*   **Service Initialization:** Ensure all `IService` classes are registered in `GameContext.Awake`. Accessing them in other `Awake` methods will likely fail; use `Start` or the event system instead.
*   **Grid Consistency:** The "Yard Floor" is regenerated at runtime and is **not** saved to disk to save space. If you modify floor generation logic, check `GameContext.PopulateYardFloors`.
*   **Undo/Redo:** Only actions implemented as `PlacementCommandBase` will be tracked by the `CommandHistory`.
*   **NavMesh:** The NavMesh is baked dynamically (`NavMeshManager.BakeImmediate`) after loading or major grid changes. Large warehouses may experience a brief frame drop during this process.