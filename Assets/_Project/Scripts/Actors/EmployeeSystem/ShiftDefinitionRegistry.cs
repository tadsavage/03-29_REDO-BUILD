using System.Collections.Generic;

/// <summary>
/// Backing data store for ShiftManagerPanel's player-defined shift templates (name + per-day
/// Start/End windows, Sun=0..Sat=6 — matches ShiftManagerPanel's own day-index convention, NOT
/// EmployeeWorkSchedule's Mon=0..Sun=6). Extracted out of the panel so PlacementSystem can
/// snapshot/restore it without needing a live UI instance — the panel is a thin UI over this
/// registry, seeding itself from it on construction and committing to it on a successful
/// Save & Close / Enter Info.
///
/// Sentinel encoding for Start/End values matches ShiftManagerPanel's private Closed(-1)/NotSet(-2)
/// constants — duplicated here rather than shared since they're pure data sentinels, not behavior.
///
/// Still UI-only / not wired to PayrollService or ShiftSchedule.cs — see ShiftManagerPanel's own
/// scope note. This registry only makes the data survive save/load, nothing more.
/// </summary>
public static class ShiftDefinitionRegistry
{
    public const int Closed = -1;
    public const int NotSet = -2;

    public class ShiftDefinition
    {
        public string Name;
        public readonly int[] Start = new int[7];
        public readonly int[] End = new int[7];
    }

    private static readonly List<ShiftDefinition> _shifts = new();

    public static IReadOnlyList<ShiftDefinition> All => _shifts;

    /// <summary>Replaces the entire shift-template list (called by the panel on commit).</summary>
    public static void ReplaceAll(List<ShiftDefinition> shifts)
    {
        _shifts.Clear();
        if (shifts != null) _shifts.AddRange(shifts);
    }

    public static List<ShiftDefinitionSnapshot> Export()
    {
        var list = new List<ShiftDefinitionSnapshot>();
        foreach (var s in _shifts)
        {
            list.Add(new ShiftDefinitionSnapshot
            {
                name = s.Name,
                start = (int[])s.Start.Clone(),
                end = (int[])s.End.Clone()
            });
        }
        return list;
    }

    /// <summary>Restore saved shift templates (replaces current state).</summary>
    public static void Import(List<ShiftDefinitionSnapshot> entries)
    {
        _shifts.Clear();
        if (entries == null) return;
        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.name)) continue;
            var def = new ShiftDefinition { Name = e.name };
            for (int d = 0; d < 7; d++)
            {
                def.Start[d] = (e.start != null && d < e.start.Length) ? e.start[d] : NotSet;
                def.End[d]   = (e.end   != null && d < e.end.Length)   ? e.end[d]   : NotSet;
            }
            _shifts.Add(def);
        }
    }
}

/// <summary>Serializable form of one shift template, for JSON save/load.</summary>
[System.Serializable]
public class ShiftDefinitionSnapshot
{
    public string name;
    public int[] start = new int[7];
    public int[] end = new int[7];
}
