using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-lane operational settings that a future "Lane setup" UI will drive (Inbound/Outbound/Both,
/// FIFO vs LIFO, max stack height, …). For now these are not editable in-game — every lane uses
/// LaneConfig.Default until something calls LaneConfigRegistry.Set — but putaway already respects
/// them, so the UI can be layered on later without touching the inventory logic.
///
/// Keyed by lane KEY = "&lt;doorNumber&gt;&lt;letter&gt;" (e.g. "1A"), the same identity the lane
/// name uses, so a lane's config survives door renumbering only if the number is stable (it is —
/// door numbers never renumber). If a lane's door is deleted, its config just goes unused.
/// </summary>
public enum LaneUsage { Inbound, Outbound, Both }
public enum LaneStackOrder { FIFO, LIFO }

[System.Serializable]
public struct LaneConfig
{
    public LaneUsage Usage;
    public LaneStackOrder Order;
    public int MaxStackHeight; // pallets that may stack in one slot (cell); 1 = no stacking

    public static LaneConfig Default => new LaneConfig
    {
        Usage = LaneUsage.Both,
        Order = LaneStackOrder.FIFO,
        MaxStackHeight = 2
    };
}

/// <summary>Runtime store of per-lane configs. In-memory only for now (not persisted) — the Lane UI
/// and save integration come later; the important part today is that putaway reads MaxStackHeight
/// from here rather than a hard-coded constant.</summary>
public static class LaneConfigRegistry
{
    private static readonly Dictionary<string, LaneConfig> _byLane = new();

    public static string Key(int doorNumber, string lane) => $"{doorNumber}{lane}";

    public static LaneConfig Get(int doorNumber, string lane)
        => _byLane.TryGetValue(Key(doorNumber, lane), out var c) ? c : LaneConfig.Default;

    public static void Set(int doorNumber, string lane, LaneConfig config)
        => _byLane[Key(doorNumber, lane)] = config;

    /// <summary>True if this lane has been explicitly configured (vs. falling back to Default).</summary>
    public static bool Has(int doorNumber, string lane) => _byLane.ContainsKey(Key(doorNumber, lane));

    /// <summary>Flatten all configured lanes for saving. Keys are "&lt;door&gt;&lt;letter&gt;" so the
    /// door is the leading numeric run and the lane is the trailing letters.</summary>
    public static List<LaneConfigEntry> Export()
    {
        var list = new List<LaneConfigEntry>();
        foreach (var kv in _byLane)
        {
            // A zone's pseudo-door id is negative (see ZoneRegistry) — its key ("-3A") carries a
            // leading '-' the digit scan below must step over, or every zone lane's config would be
            // silently skipped as "malformed" and never saved.
            int split = kv.Key.Length > 0 && kv.Key[0] == '-' ? 1 : 0;
            while (split < kv.Key.Length && char.IsDigit(kv.Key[split])) split++;
            if (split == 0 || split >= kv.Key.Length) continue; // malformed key, skip
            var c = kv.Value;
            list.Add(new LaneConfigEntry
            {
                door = int.Parse(kv.Key.Substring(0, split)),
                lane = kv.Key.Substring(split),
                usage = (int)c.Usage,
                order = (int)c.Order,
                maxStack = c.MaxStackHeight
            });
        }
        return list;
    }

    /// <summary>Restore saved lane configs (replaces current state for those lanes).</summary>
    public static void Import(List<LaneConfigEntry> entries)
    {
        if (entries == null) return;
        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.lane)) continue;
            Set(e.door, e.lane, new LaneConfig
            {
                Usage = (LaneUsage)e.usage,
                Order = (LaneStackOrder)e.order,
                MaxStackHeight = Mathf.Max(1, e.maxStack)
            });
        }
    }
}

/// <summary>Serializable form of one lane's config, for JSON save/load.</summary>
[System.Serializable]
public class LaneConfigEntry
{
    public int door;
    public string lane;
    public int usage;    // (int)LaneUsage
    public int order;    // (int)LaneStackOrder
    public int maxStack;
}
