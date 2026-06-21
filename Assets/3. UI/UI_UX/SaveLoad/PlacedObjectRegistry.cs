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

    public static void Register(PlacedObject obj)
    {
        if (obj == null) return;
        _all.Add(obj);
    }

    public static void Unregister(PlacedObject obj)
    {
        if (obj == null) return;
        _all.Remove(obj);
    }

    public static void Clear()
    {
        _all.Clear();
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
