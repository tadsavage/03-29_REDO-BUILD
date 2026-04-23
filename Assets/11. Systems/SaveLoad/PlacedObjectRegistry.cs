using System.Collections.Generic;

/// <summary>
/// Global list of all placed objects in the world.
/// Used by save/load, delete, undo/redo, etc.
/// </summary>
public static class PlacedObjectRegistry
{
    public static readonly List<PlacedObject> All = new();

    public static void Register(PlacedObject obj)
    {
        if (!All.Contains(obj))
            All.Add(obj);
    }

    public static void Unregister(PlacedObject obj)
    {
        All.Remove(obj);
    }

    public static void Clear()
    {
        All.Clear();
    }
}
