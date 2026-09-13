using UnityEngine;

/// <summary>
/// Implemented by build commands that cache a direct GameObject reference to a Walls-category
/// placed object (Wall / Corner / T-Wall).
///
/// WallConnectivityManager's auto-shape swap (see its class doc) destroys the old GameObject and
/// instantiates a new one whenever a neighboring wall placement/deletion changes what shape a
/// segment needs to be. If an EARLIER command still on the undo/redo stacks cached a reference to
/// the object that just got destroyed, that command's own later Undo()/Redo() would silently
/// no-op on a dead object instead of doing anything.
///
/// WallConnectivityManager closes that gap by calling CommandHistory.RelocateWallInstance(old,
/// new) immediately after every swap. CommandHistory walks every command still reachable from its
/// undo stack, redo stack, and any in-progress batch, and calls RelocateWallInstance on each one
/// that implements this interface — giving it a chance to swap out the stale reference for the
/// live one before it's ever asked to act on it again.
/// </summary>
public interface IWallInstanceRelocatable
{
    /// <summary>
    /// If this command holds a cached reference equal to <paramref name="oldInstance"/>, replace
    /// it with <paramref name="newInstance"/>. Implementations should use plain reference identity
    /// (e.g. ReferenceEquals), not Unity's overloaded == operator, since oldInstance may already be
    /// a destroyed object by the time this is called.
    /// </summary>
    void RelocateWallInstance(GameObject oldInstance, GameObject newInstance);
}
