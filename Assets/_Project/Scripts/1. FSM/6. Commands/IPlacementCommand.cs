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
    public interface IPlacementCommand : ICommand
    {
        /// <summary>
        /// Get a human-readable description (used by UI for undo/redo labels).
        /// Examples: "Place Floor Tile", "Move Equipment", "Delete Wall"
        /// </summary>
        string Description { get; }
    }
}
