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

    /// <summary>Total reachable levels (index 0/1) the setup UI exposes — mirrors
    /// RackSetupUI.REACHABLE_LEVELS/TOTAL_LEVELS so a partially-known designation array can be
    /// synthesized one level at a time as save data streams in (see RegisterLevel).</summary>
    private const int TOTAL_LEVELS = 6;

    /// <summary>
    /// Marks <paramref name="aisleNumber"/> as used and records a single level's Pick/Reserve
    /// designation without clobbering the levels already known. Used when restoring racks from a
    /// save (RackSaveCodec.RestoreOnto) — each PlacedObject only knows its own level, so the full
    /// scheme is rebuilt incrementally as every rack in the aisle loads. Levels never seen default
    /// to "Pick" (reachable) or "Reserve" (above the reachable ones), matching RackSetupUI's own
    /// default-building for non-reachable levels.
    /// </summary>
    public static void RegisterLevel(int aisleNumber, int levelIndex, string designation)
    {
        _used.Add(aisleNumber);
        if (levelIndex < 0 || levelIndex >= TOTAL_LEVELS) return;

        if (!_designations.TryGetValue(aisleNumber, out var d) || d == null || d.Length != TOTAL_LEVELS)
        {
            d = new string[TOTAL_LEVELS];
            for (int i = 0; i < TOTAL_LEVELS; i++)
                d[i] = i < 2 ? "Pick" : "Reserve";
            _designations[aisleNumber] = d;
        }
        d[levelIndex] = designation;
    }

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
