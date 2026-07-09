# Project Overview
- Game Title: Warehouse Logistics Simulation
- High-Level Concept: High-fidelity warehouse management and logistics simulation involving spatial planning, resource management, and inventory flow.
- Players: Single player
- Target Platform: PC (StandaloneWindows64)
- Render Pipeline: PC_RPAsset (URP)

# Game Mechanics
## Core Gameplay Loop
- Building warehouse infrastructure (floors, racking, equipment).
- Managing inventory (receiving pallets, putaway, picking).
- Managing staff (hiring, scheduling, task assignment).
- Economic management (capital, upkeep, wages).

# UI
- UI Toolkit (UITK) based menus and HUDs.
- `SaveLoadWindowController` for managing game saves.
- `BuildMenuUI` for infrastructure construction.

# Key Asset & Context
- `PlacementSystem.cs`: Main controller for saving and loading the world state.
- `InventoryService.cs`: Manages `PalletMasterRecord` data.
- `InventoryPersistenceService.cs`: Intended to handle the visual restoration of pallets (currently empty).
- `PalletBuilder.cs`: Component on the pallet prefab that builds the 3D cases.
- `PalletData.cs`: Component on received pallets holding license plate (Load ID) and SKU info.
- `PalletMasterLink.cs`: Component on unreceived (ghosted) pallets linking them to their master record GUID.
- `ChepStack.prefab`: The standard pallet visual prefab.

# Implementation Steps
## Step 1: Implement Visual Restoration in InventoryPersistenceService
- **Description**: Update `InventoryPersistenceService.InstantiateRestoredPalletVisuals()` to iterate over all `PalletMasterRecord` objects in `InventoryService` and recreate their physical GameObjects.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No
- **Details**:
    - Load the `ChepStack` prefab from `Resources/Inventory/ChepStack`.
    - Iterate through `InventoryService.AllPallets`.
    - Instantiate the prefab at the saved world position (Grid X/Y converted to world + `WorldHeightY`).
    - Configure `PalletBuilder`:
        - Assign the `casePrefab` from the `SkuData`.
        - Set `manualTi`/`manualHi` from the `SkuData` (Ti/Hi).
        - Call `Build(deductMoney: false)`.
    - Restore State Logic:
        - **Unreceived (Ghosted)**: If `PalletMasterRecord.LoadId` is null or empty, attach `PalletMasterLink` with the `PalletId` (GUID). Apply the ghost material to `PalletBuilder`.
        - **Received (Solid)**: If `PalletMasterRecord.LoadId` is present, add a `PalletData` component and initialize it with the record's data.
    - Ensure `InventoryService.RecalculateAllPalletHeights()` is called if needed, or rely on the saved `WorldHeightY`.

## Step 2: Update PlacementSystem to Ensure Correct Sequencing
- **Description**: Verify and ensure `PlacementSystem.ApplySaveData` calls the restoration at the correct time (after grid/racking is restored).
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

# Verification & Testing
- **Manual Check**:
    - Start a game, receive several pallets at the dock.
    - Save the game.
    - Load the game and verify that pallets at the dock (unreceived) are visible and ghosted.
    - Move some pallets to racking (receive them), save, and load again to verify they are visible and solid.
- **Edge Cases**:
    - Loading a save with no pallets.
    - Loading a save where some pallets are on the ground and some are in racking.
    - Verify that `PalletMasterLink` correctly re-registers pallets so the `ReceivingTaskDriver` can find them.
