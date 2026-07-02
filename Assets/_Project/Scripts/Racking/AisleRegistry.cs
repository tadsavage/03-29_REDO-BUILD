using System.Collections.Generic;

/// <summary>
/// Tracks which aisle numbers are already in use so no two collections can be named
/// the same aisle. Registered by AisleInitializer on a successful setup; queried by
/// RackSetupUI before it accepts a submission.
/// </summary>
public static class AisleRegistry
{
    private static readonly HashSet<int> _used = new();
    // Per-aisle level designations ("Pick"/"Reserve" per level index, length 6). Needed so a rack
    // stacked onto an aisle AFTER setup can compute its level char with the SAME scheme the setup
    // UI chose — otherwise post-init stacked levels wouldn't know which levels were reserves.
    private static readonly Dictionary<int, string[]> _designations = new();

    public static bool IsUsed(int aisleNumber) => _used.Contains(aisleNumber);

    public static void Register(int aisleNumber) => _used.Add(aisleNumber);

    /// <summary>Register a used aisle number along with its level designation scheme.</summary>
    public static void Register(int aisleNumber, string[] designations)
    {
        _used.Add(aisleNumber);
        if (designations != null) _designations[aisleNumber] = designations;
    }

    /// <summary>The level designation array for an aisle, or null if unknown (caller falls back).</summary>
    public static string[] GetDesignations(int aisleNumber)
        => _designations.TryGetValue(aisleNumber, out var d) ? d : null;

    public static void Unregister(int aisleNumber)
    {
        _used.Remove(aisleNumber);
        _designations.Remove(aisleNumber);
    }

    public static void Clear()
    {
        _used.Clear();
        _designations.Clear();
    }
}
