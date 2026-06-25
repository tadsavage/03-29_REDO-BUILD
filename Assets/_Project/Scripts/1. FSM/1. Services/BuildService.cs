using UnityEngine;
using GameCore.Services;
using GameCore.Events;
using GameCore.Economy;

namespace GameCore.Build
{
    /// <summary>
    /// Centralized service for all placement, movement, and deletion operations.
    /// Acts as a facade to the existing command system, providing a clean interface for FSM states.
    ///
    /// RESPONSIBILITIES:
    /// - Provide a unified BuildService interface (IBuildService) for FSM states
    /// - Expose CommandHistory for undo/redo operations
    /// - Publish GameEvents.Build events on state changes
    /// - Cache service references (PlacementGrid, MoneyService, etc.)
    /// - Track undo/redo capability
    ///
    /// INTEGRATION POINTS:
    /// - Publishes: GameEvents.Build.OnCommandUndone, OnCommandRedone
    /// - Uses: PlacementGrid, CommandHistory, MoneyService, ServiceLocator, EventManager
    /// - Used by: PlacementStateBase-derived states
    ///
    /// NOTE: This is a minimal facade that works with the existing command infrastructure.
    /// Complex validation (cost, grid rules) is handled by individual commands and states.
    /// Undo/redo logic is owned by individual ICommand implementations.
    /// </summary>
    public class BuildService : IBuildService
    {
        // ============ INTERNAL STATE ============
        private PlacementGrid _grid;
        private CommandHistory _commandHistory;
        private MoneyService _moneyService;
        private EventManager _eventManager;

        // ============ PROPERTIES ============
        public CommandHistory CommandHistory => _commandHistory;
        public PlacementGrid Grid => _grid;

        public bool CanUndo => _commandHistory != null && _commandHistory.UndoCount > 0;
        public bool CanRedo => _commandHistory != null && _commandHistory.RedoCount > 0;

        // ============ CONSTRUCTOR ============
        public BuildService(PlacementGrid grid, CommandHistory commandHistory)
        {
            _grid = grid;
            _commandHistory = commandHistory;
        }

        // ============ LIFECYCLE ============

        public void Initialize()
        {
            Debug.Log("[BuildService] Initializing...");

            _eventManager = EventManager.Instance;
            if (_eventManager == null)
            {
                Debug.LogError("[BuildService] EventManager not found.");
                return;
            }

            ServiceLocator.TryGet<MoneyService>(out _moneyService);
            if (_moneyService == null)
            {
                Debug.LogWarning("[BuildService] MoneyService not found.");
            }

            Debug.Log("[BuildService] Initialized.");
        }

        public void Shutdown()
        {
            Debug.Log("[BuildService] Shutting down...");
        }

        // ============ PLACEMENT (Deferred to states/commands) ============

        public bool TryPlaceObject(int gridX, int gridY, int rotation, ObjDataSO objData)
        {
            // States and BuildMenuUI handle placement directly for now
            // This interface exists for future unified command creation
            Debug.LogWarning("[BuildService] Direct placement not implemented. Use state-based placement.");
            return false;
        }

        public bool TryDragPlaceObjects(int startX, int startY, int endX, int endY, int rotation, ObjDataSO objData)
        {
            Debug.LogWarning("[BuildService] Drag placement not implemented. Use BuildState.");
            return false;
        }

        public bool CanPlaceObject(int gridX, int gridY, int rotation, ObjDataSO objData)
        {
            if (_grid == null || objData == null) return false;
            if (!_grid.IsInsideGrid(new Vector2Int(gridX, gridY))) return false;
            if (_moneyService != null && !_moneyService.CanAfford(objData.cost)) return false;
            // Grid-level validation deferred to states and existing command system
            return true;
        }

        // ============ MOVEMENT (Deferred to states/commands) ============

        public bool TryMoveObject(PlacedObject placedObject, int newGridX, int newGridY, int newRotation)
        {
            Debug.LogWarning("[BuildService] Direct movement not implemented. Use MoveState.");
            return false;
        }

        public bool CanMoveObject(PlacedObject placedObject, int newGridX, int newGridY, int newRotation)
        {
            if (_grid == null || placedObject == null) return false;

            int oldX = placedObject.gridX;
            int oldY = placedObject.gridY;
            int oldRotation = placedObject.rotation;

            // Moving to same position is valid (no-op)
            if (oldX == newGridX && oldY == newGridY && oldRotation == newRotation) return true;

            // Check target is inside grid bounds
            if (!_grid.IsInsideGrid(new Vector2Int(newGridX, newGridY))) return false;

            // Grid-level validation deferred to states and existing command system
            return true;
        }

        // ============ DELETION (Deferred to states/commands) ============

        public bool TryDeleteObject(PlacedObject placedObject)
        {
            Debug.LogWarning("[BuildService] Direct deletion not implemented. Use DeleteState.");
            return false;
        }

        public bool CanDeleteObject(PlacedObject placedObject)
        {
            return placedObject != null && placedObject.gameObject != null;
        }

        // ============ UNDO/REDO ============

        public bool Undo()
        {
            if (!CanUndo) return false;
            _commandHistory.Undo();
            _eventManager?.Publish(GameEvents.Build.OnCommandUndone);
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            _commandHistory.Redo();
            _eventManager?.Publish(GameEvents.Build.OnCommandRedone);
            return true;
        }
    }
}
