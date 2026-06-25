using UnityEngine;
using GameCore.Services;

namespace GameCore.Build
{
    /// <summary>
    /// Service contract for all placement, movement, and deletion operations.
    /// Abstracts away command creation and FSM state management from individual states.
    ///
    /// RESPONSIBILITY:
    /// - Validate placement/move/delete before execution
    /// - Execute commands through CommandHistory (undo/redo support)
    /// - Publish build events for UI/economy feedback
    /// - Coordinate with PlacedObjectRegistry and PlacementGrid
    ///
    /// USAGE:
    /// - BuildState calls: TryPlaceObject() for single-placement
    /// - DragPlace calls: TryDragPlaceObjects() for multi-cell drag placement
    /// - MoveState calls: TryMoveObject() for object relocation
    /// - DeleteState calls: TryDeleteObject() for object removal
    /// - All methods return bool: success = command executed and added to history
    ///
    /// INTEGRATION POINTS:
    /// - Publishes: GameEvents.Build.OnObjectPlaced, OnObjectMoved, OnObjectDeleted
    /// - Uses: PlacementGrid, PlacedObjectRegistry, CommandHistory, MoneyService
    /// - Used by: All FSM states
    /// </summary>
    public interface IBuildService : IService
    {
        // ============ PLACEMENT ============

        /// <summary>
        /// Attempt to place a single object at the given grid cell.
        /// Returns true if placement succeeded and was added to command history.
        /// </summary>
        bool TryPlaceObject(int gridX, int gridY, int rotation, ObjDataSO objData);

        /// <summary>
        /// Attempt to place multiple objects via drag placement (multi-cell).
        /// Returns true if at least one object was placed successfully.
        /// </summary>
        bool TryDragPlaceObjects(int startX, int startY, int endX, int endY, int rotation, ObjDataSO objData);

        // ============ MOVEMENT ============

        /// <summary>
        /// Attempt to move an existing object from its current position to a new grid cell.
        /// Returns true if move succeeded and was added to command history.
        /// </summary>
        bool TryMoveObject(PlacedObject placedObject, int newGridX, int newGridY, int newRotation);

        // ============ DELETION ============

        /// <summary>
        /// Attempt to delete an object from the grid.
        /// Returns true if deletion succeeded, charged cost, and was added to command history.
        /// </summary>
        bool TryDeleteObject(PlacedObject placedObject);

        // ============ VALIDATION ============

        /// <summary>
        /// Check if placement is valid without executing (used for preview/hovering).
        /// </summary>
        bool CanPlaceObject(int gridX, int gridY, int rotation, ObjDataSO objData);

        /// <summary>
        /// Check if an object can be moved to the target location (used for preview).
        /// </summary>
        bool CanMoveObject(PlacedObject placedObject, int newGridX, int newGridY, int newRotation);

        /// <summary>
        /// Check if an object can be deleted (typically always true, unless special rules exist).
        /// </summary>
        bool CanDeleteObject(PlacedObject placedObject);

        // ============ UNDO/REDO ============

        /// <summary>
        /// Undo the last command (keyboard: Ctrl+Z or UI button).
        /// Returns true if undo succeeded.
        /// </summary>
        bool Undo();

        /// <summary>
        /// Redo the last undone command (keyboard: Ctrl+Y or UI button).
        /// Returns true if redo succeeded.
        /// </summary>
        bool Redo();

        /// <summary>
        /// Check if undo is available.
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// Check if redo is available.
        /// </summary>
        bool CanRedo { get; }

        // ============ STATE ACCESS ============

        /// <summary>
        /// Get the underlying CommandHistory (used by PlacementStateMachine for undo/redo UI).
        /// </summary>
        CommandHistory CommandHistory { get; }

        /// <summary>
        /// Get the underlying PlacementGrid (used by states for coordinate checks).
        /// </summary>
        PlacementGrid Grid { get; }
    }
}
