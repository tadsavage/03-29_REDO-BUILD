using System;
using System.Collections.Generic;

/// <summary>
/// Global list of all placed objects in the world.
/// Used by save/load, delete, undo/redo, etc.
/// </summary>
public static class PlacedObjectRegistry
{
    // Use HashSet for O(1) Add/Remove. This is critical for performance
    // when stopping Play Mode with thousands of objects.
    private static readonly HashSet<PlacedObject> _all = new();

    /// <summary>
    /// Expose the collection for iteration.
    /// Note: Foreach over a HashSet is efficient, but you cannot modify it while iterating.
    /// </summary>
    public static IEnumerable<PlacedObject> All => _all;

    public static int Count => _all.Count;

    /// <summary>Fired after an object is added. Lets listeners (e.g. NavMeshManager's per-cell
    /// floor cache) maintain incremental state instead of re-scanning the whole registry.</summary>
    public static event Action<PlacedObject> OnRegistered;

    /// <summary>Fired after an object is removed. See <see cref="OnRegistered"/>.</summary>
    public static event Action<PlacedObject> OnUnregistered;

    /// <summary>Fired by Clear(). PlacementSystem.ClearAll() wipes the registry synchronously
    /// BEFORE each object's queued Destroy() actually runs its deferred OnDestroy/Unregister
    /// at end-of-frame — by then _all is already empty, so Unregister()'s HashSet.Remove
    /// returns false and OnUnregistered never fires for any of them. Listeners that maintain
    /// their own incremental state keyed off individual (Un)Registered events (e.g.
    /// NavMeshManager's per-cell floor cache) must also listen here to avoid going stale.</summary>
    public static event Action OnCleared;

    public static void Register(PlacedObject obj)
    {
        if (obj == null) return;
        if (_all.Add(obj))
            OnRegistered?.Invoke(obj);
    }

    public static void Unregister(PlacedObject obj)
    {
        if (obj == null) return;
        if (_all.Remove(obj))
            OnUnregistered?.Invoke(obj);
    }

    public static void Clear()
    {
        _all.Clear();
        OnCleared?.Invoke();
    }

    /// <summary>
    /// Returns a snapshot of the registry as an array. 
    /// Safe for iterating when you might be modifying the registry (e.g. destroying objects).
    /// </summary>
    public static PlacedObject[] GetSnapshot()
    {
        PlacedObject[] snapshot = new PlacedObject[_all.Count];
        _all.CopyTo(snapshot);
        return snapshot;
    }
}
