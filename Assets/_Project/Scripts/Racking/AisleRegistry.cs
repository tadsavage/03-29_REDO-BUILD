using System.Collections.Generic;

/// <summary>
/// Tracks which aisle numbers are already in use so no two collections can be named
/// the same aisle. Registered by AisleInitializer on a successful setup; queried by
/// RackSetupUI before it accepts a submission.
/// </summary>
public static class AisleRegistry
{
    private static readonly HashSet<int> _used = new();

    public static bool IsUsed(int aisleNumber) => _used.Contains(aisleNumber);

    public static void Register(int aisleNumber) => _used.Add(aisleNumber);

    public static void Unregister(int aisleNumber) => _used.Remove(aisleNumber);

    public static void Clear() => _used.Clear();
}
