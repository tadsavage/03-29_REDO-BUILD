using UnityEngine;
using GameCore.Events;
using GameCore.Events.Payloads;
using GameCore.Services;

namespace GameCore.Build
{
    /// <summary>
    /// Abstract base class for all placement-related commands.
    /// Provides shared logic for event publishing and description handling.
    ///
    /// INHERITED BY:
    /// - PlaceCommand (place new object)
    /// - MoveCommand (relocate existing object)
    /// - DeleteCommand (remove object)
    /// - DragPlaceCommand (multi-cell placement)
    ///
    /// SHARED RESPONSIBILITIES:
    /// - Publish GameEvents.Build events on execute/undo/redo
    /// - Track command description for UI
    /// - Provide protected helper methods for common operations
    /// - Coordinate with EventManager singleton
    /// </summary>
    public abstract class PlacementCommandBase : IPlacementCommand
    {
        protected EventManager _eventManager;
        protected string _description;

        public string Description => _description;

        protected PlacementCommandBase(string description = "Build Command")
        {
            _description = description;
            _eventManager = EventManager.Instance;
        }

        public abstract void Execute();
        public abstract void Undo();
        public abstract void Redo();

        // ============ PROTECTED HELPERS ============

        /// <summary>
        /// Publish a build event via EventManager.
        /// Protected so subclasses can publish custom events.
        /// </summary>
        protected void PublishBuildEvent(string eventId, object payload = null)
        {
            if (_eventManager == null)
            {
                Debug.LogWarning("[PlacementCommandBase] EventManager not available for event publishing.");
                return;
            }

            if (payload == null)
            {
                _eventManager.Publish(eventId);
            }
            else if (payload is PlacedObject placedObj)
            {
                _eventManager.Publish<PlacedObject>(eventId, placedObj);
            }
            else if (payload is int intValue)
            {
                _eventManager.Publish<int>(eventId, intValue);
            }
            else if (payload is BuildingMoveData moveData)
            {
                _eventManager.Publish<BuildingMoveData>(eventId, moveData);
            }
        }

        /// <summary>
        /// Helper: Set GameObject active state (used by place/delete undo/redo).
        /// </summary>
        protected void SetObjectActive(GameObject obj, bool active)
        {
            if (obj != null)
            {
                obj.SetActive(active);
            }
        }

        /// <summary>
        /// Helper: Move an object's PlacedObject component to a new grid position.
        /// Used by MoveCommand and drag-place operations.
        /// </summary>
        protected void SetObjectPosition(PlacedObject placedObj, int gridX, int gridY, int rotation)
        {
            if (placedObj != null)
            {
                placedObj.gridX = gridX;
                placedObj.gridY = gridY;
                placedObj.rotation = rotation;
            }
        }

        /// <summary>
        /// Helper: Get world position from grid coordinates (used for visual feedback).
        /// Note: Actual conversion deferred to state layer where grid is available.
        /// </summary>
        protected Vector3 GetWorldPosition(int gridX, int gridY)
        {
            // Grid coordinate system conversion happens at the state/placement layer
            return Vector3.zero; // Placeholder
        }
    }
}
