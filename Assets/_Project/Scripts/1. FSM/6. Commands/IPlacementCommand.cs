namespace GameCore.Build
{
    /// <summary>
    /// Unified interface for all placement-related commands.
    /// Replaces ICommand for build-specific operations.
    ///
    /// All commands:
    /// - Execute immediately on Push (no deferred execution)
    /// - Support Undo() to reverse state
    /// - Support Redo() to restore state
    /// - Publish events to GameEvents.Build on state change
    ///
    /// IMPLEMENTATION NOTES:
    /// - Use PlacementCommandBase for shared logic
    /// - Undo DISABLES GameObjects, does not destroy (preserves instance for redo)
    /// - Redo RE-ENABLES disabled GameObjects
    /// - All event publishing goes through EventManager (see PlacementCommandBase)
    /// </summary>
    public interface IPlacementCommand
    {
        /// <summary>
        /// Execute the command immediately and publish events.
        /// Called by BuildService.TryXXX() and CommandHistory.Push().
        /// </summary>
        void Execute();

        /// <summary>
        /// Undo the command (reverse state changes).
        /// For placement: SetActive(false) the placed object.
        /// For deletion: SetActive(true) the deleted object.
        /// For movement: Move object back to original position.
        /// </summary>
        void Undo();

        /// <summary>
        /// Redo the command (restore state after undo).
        /// For placement: SetActive(true) the placed object.
        /// For deletion: SetActive(false) the deleted object.
        /// For movement: Move object to new position.
        /// </summary>
        void Redo();

        /// <summary>
        /// Get a human-readable description (used by UI for undo/redo labels).
        /// Examples: "Place Floor Tile", "Move Equipment", "Delete Wall"
        /// </summary>
        string Description { get; }
    }
}
