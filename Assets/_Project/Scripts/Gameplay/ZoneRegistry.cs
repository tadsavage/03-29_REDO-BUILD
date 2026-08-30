using System.Collections.Generic;

/// <summary>
/// Assigns a stable, negative "pseudo door number" to each standalone lane Zone (Pick-and-Deliver,
/// Limbo/Penalty Box, QA, Overflow, …) — a named group of shipping lanes that belongs to no dock
/// door. Every existing lane-keyed system (InventoryService, LaneConfigRegistry,
/// StagingLaneAssignmentService, TrailerOffloadController, ReachTruckOperator,
/// OrderSelectionTaskDriver, WorkQueuePanel) already keys lanes by a plain `int doorNumber` — real
/// doors are always positive (see DockSlot.AssignDoorNumbers), so a negative int is a value none of
/// them can ever collide with, and none of their signatures need to change. Only LaneNamingService
/// (which owns the identity-vs-geometry split — see its Recompute()) and anything that formats a
/// door number into PLAYER-FACING TEXT need to know a given int might actually be a zone.
///
/// IDs must stay stable across a session/save — assigned once per zone name, never renumbered, so a
/// persisted OrderData.AssignedDoorNumber or WorkTask.ToLocation pointing at a zone still resolves
/// correctly after a reload (same reasoning DockSlot door numbers are never reassigned).
/// </summary>
public static class ZoneRegistry
{
    private static readonly Dictionary<string, int> _pseudoDoorByZone = new(); // normalized name -> id
    private static readonly Dictionary<int, string> _zoneByPseudoDoor = new(); // id -> display name
    private static int _nextId = -1; // decrements: -1, -2, -3, …

    private static string Normalize(string zoneName) => zoneName?.Trim().ToUpperInvariant();

    public static bool IsZone(int doorNumber) => doorNumber < 0;

    /// <summary>Returns this zone's pseudo door number, allocating a fresh one (the next unused
    /// negative id) the first time this name is seen. Case/whitespace-insensitive.</summary>
    public static int GetOrCreate(string zoneName)
    {
        string key = Normalize(zoneName);
        if (string.IsNullOrEmpty(key)) return 0;

        if (_pseudoDoorByZone.TryGetValue(key, out int id)) return id;

        id = _nextId--;
        _pseudoDoorByZone[key] = id;
        _zoneByPseudoDoor[id] = key;
        return id;
    }

    /// <summary>Looks up a zone by name WITHOUT creating one — for parsing untrusted/typed-in text
    /// (e.g. an address string) where an unrecognized name must fail, not silently mint a new zone.</summary>
    public static bool TryGetPseudoDoor(string zoneName, out int pseudoDoor)
        => _pseudoDoorByZone.TryGetValue(Normalize(zoneName), out pseudoDoor);

    public static bool TryGetZoneName(int pseudoDoor, out string zoneName)
        => _zoneByPseudoDoor.TryGetValue(pseudoDoor, out zoneName);

    /// <summary>The door-number-or-zone-name text a player should see for this identity.</summary>
    public static string DisplayPrefix(int doorNumber)
        => IsZone(doorNumber) && TryGetZoneName(doorNumber, out var name) ? name : doorNumber.ToString();

    public static void ClearAll()
    {
        _pseudoDoorByZone.Clear();
        _zoneByPseudoDoor.Clear();
        _nextId = -1;
    }

    /// <summary>Flatten every known zone for saving.</summary>
    public static List<ZoneEntry> Export()
    {
        var list = new List<ZoneEntry>();
        foreach (var kv in _zoneByPseudoDoor)
            list.Add(new ZoneEntry { zoneName = kv.Value, pseudoDoor = kv.Key });
        return list;
    }

    /// <summary>Restores saved zone identities EXACTLY (same name -> same id) — never re-derived via
    /// GetOrCreate, which could hand out a different id than the one already baked into saved
    /// OrderData/WorkTask addresses. Also advances _nextId past every restored id so a brand-new
    /// zone created after loading can't collide with one restored from the save.</summary>
    public static void Import(List<ZoneEntry> entries)
    {
        ClearAll();
        if (entries == null) return;
        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.zoneName)) continue;
            string key = Normalize(e.zoneName);
            _pseudoDoorByZone[key] = e.pseudoDoor;
            _zoneByPseudoDoor[e.pseudoDoor] = key;
            if (e.pseudoDoor <= _nextId) _nextId = e.pseudoDoor - 1;
        }
    }
}

/// <summary>Serializable form of one zone identity, for JSON save/load.</summary>
[System.Serializable]
public class ZoneEntry
{
    public string zoneName;
    public int pseudoDoor;
}
