using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Time-study recorder: the floor reports how long each job really took, in GAME minutes (Time.time runs at 1 game minute per scaled second),
/// and the HAL METRICS tab turns that into averages and suggested standards. Samples persist to disk so the standards keep building across
/// sessions. Hooks call <see cref="Record"/> when a job finishes; nothing here drives gameplay.
/// </summary>
public static class OpsMetrics
{
    // New kinds go on the end: samples are saved by number.
    public enum Kind { GateToDoor, Offload, LoadCycle, LoadTotal, PalletPick, CasePick, OffloadCycle, Receive }

    public struct Info { public Kind kind; public string title, what, unit; }

    /// <summary>Display order and wording for the METRICS tab.</summary>
    public static readonly Info[] All =
    {
        new Info { kind = Kind.GateToDoor, title = "GUARD SHACK TO DOOR", what = "Truck clears the gate until it is docked at its door (trucks sent to the side lot are not counted).", unit = "per truck" },
        new Info { kind = Kind.Offload, title = "OFFLOAD", what = "Dock stocker claims an inbound trailer until the last pallet is off.", unit = "per trailer" },
        new Info { kind = Kind.OffloadCycle, title = "OFFLOAD LAP", what = "One pallet, the full lap: picked up in the trailer until the next pallet is picked up (set-down and the drive back included).", unit = "per pallet" },
        new Info { kind = Kind.Receive, title = "RECEIVING", what = "Time per pallet received, counting the time between pallets while the receiver is working steadily (10 pallets in 100 min = 10 min each).", unit = "per pallet" },
        new Info { kind = Kind.LoadCycle, title = "LOAD CYCLE", what = "One pallet: pick-up in the staging lane, drive to the trailer, set down, ready for the next pick-up.", unit = "per pallet" },
        new Info { kind = Kind.LoadTotal, title = "LOAD TOTAL", what = "First pick-up at the dock until the final release of the dock stocker.", unit = "per trailer" },
        new Info { kind = Kind.PalletPick, title = "PALLET PICK", what = "Reach truck: pick-up, drop-off at the staging lane and the return trip.", unit = "per pallet" },
        new Info { kind = Kind.CasePick, title = "CASE PICK", what = "First pick face reached until the pallet is staged, divided by cases picked.", unit = "per trip" },
    };

    [Serializable] class Sample { public int k; public float min; public int units; }
    [Serializable] class Store { public List<Sample> s = new(); }

    const int MaxPerKind = 300;
    const float Allowance = 1.15f;   // personal / fatigue / delay allowance on top of the observed average for a standard
    static readonly Dictionary<Kind, List<(float min, int units)>> _data = new();
    static bool _loaded;
    static int _sinceSave;
    public static int Version { get; private set; }

    static string FilePath => Path.Combine(Application.persistentDataPath, "ops_metrics.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { _data.Clear(); _loaded = false; _sinceSave = 0; }

    /// <summary>Game-minute clock.</summary>
    public static float Now => Time.time;

    public static void Record(Kind kind, float minutes, int units = 1)
    {
        if (minutes <= 0.01f || minutes > 60f * 24f) return;
        Load();
        if (!_data.TryGetValue(kind, out var list)) _data[kind] = list = new List<(float, int)>();
        list.Add((minutes, Mathf.Max(1, units)));
        if (list.Count > MaxPerKind) list.RemoveAt(0);
        Version++;
        if (++_sinceSave >= 10) Save();
    }

    public struct Stats
    {
        public int n; public float avg, best, worst, standard, perUnitAvg, perHourAvg;
    }

    public static Stats Get(Kind kind)
    {
        Load();
        var st = new Stats();
        if (!_data.TryGetValue(kind, out var list) || list.Count == 0) return st;
        st.n = list.Count;
        st.avg = list.Average(x => x.min);
        st.best = list.Min(x => x.min);
        st.worst = list.Max(x => x.min);
        st.standard = st.avg * Allowance;
        float totalMin = list.Sum(x => x.min);
        int totalUnits = list.Sum(x => x.units);
        st.perUnitAvg = totalUnits > 0 ? totalMin / totalUnits : 0f;
        st.perHourAvg = totalMin > 0f ? totalUnits / (totalMin / 60f) : 0f;
        return st;
    }

    public static void Clear()
    {
        _data.Clear(); Version++;
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
    }

    static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (!File.Exists(FilePath)) return;
            var store = JsonUtility.FromJson<Store>(File.ReadAllText(FilePath));
            if (store?.s == null) return;
            foreach (var x in store.s)
            {
                var k = (Kind)x.k;
                if (!_data.TryGetValue(k, out var l)) _data[k] = l = new List<(float, int)>();
                l.Add((x.min, x.units));
            }
        }
        catch (Exception e) { Debug.LogWarning("[OpsMetrics] load failed: " + e.Message); }
    }

    static void Save()
    {
        _sinceSave = 0;
        try
        {
            var store = new Store();
            foreach (var kv in _data)
                foreach (var x in kv.Value) store.s.Add(new Sample { k = (int)kv.Key, min = x.min, units = x.units });
            File.WriteAllText(FilePath, JsonUtility.ToJson(store));
        }
        catch (Exception e) { Debug.LogWarning("[OpsMetrics] save failed: " + e.Message); }
    }
}
