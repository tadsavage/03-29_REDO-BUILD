using System.IO;
using UnityEngine;

/// <summary>
/// Autosave preference + file helpers. The game writes to two rotating files (autosave_a / autosave_b) every
/// N in-game hours so one bad write or an accidental Stop can never cost more than that much play.
/// F5/F9 quicksave is untouched. The Settings panel sets the interval; 0 = off.
/// </summary>
public static class AutoSaveSettings
{
    public const string PrefKey = "AutoSaveHours";
    public const int DefaultHours = 12;
    public static readonly int[] Choices = { 0, 6, 12, 24 };
    public static readonly string[] FileNames = { "autosave_a", "autosave_b" };

    public static int Hours
    {
        get => PlayerPrefs.GetInt(PrefKey, DefaultHours);
        set { PlayerPrefs.SetInt(PrefKey, value); PlayerPrefs.Save(); }
    }

    private static string PathFor(string name) => Path.Combine(SaveSystem.SaveFolder, name + ".json");

    /// <summary>The file the next autosave should overwrite: the one that does not exist, else the older.</summary>
    public static string NextTarget()
    {
        string a = PathFor(FileNames[0]), b = PathFor(FileNames[1]);
        if (!File.Exists(a)) return FileNames[0];
        if (!File.Exists(b)) return FileNames[1];
        return File.GetLastWriteTimeUtc(a) <= File.GetLastWriteTimeUtc(b) ? FileNames[0] : FileNames[1];
    }

    /// <summary>The most recently written autosave, or null if none exists.</summary>
    public static string Newest()
    {
        string best = null; System.DateTime bestT = System.DateTime.MinValue;
        foreach (var n in FileNames)
        {
            string p = PathFor(n);
            if (!File.Exists(p)) continue;
            var t = File.GetLastWriteTimeUtc(p);
            if (best == null || t > bestT) { best = n; bestT = t; }
        }
        return best;
    }
}
