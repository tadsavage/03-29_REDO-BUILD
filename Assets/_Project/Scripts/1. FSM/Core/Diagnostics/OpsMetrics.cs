using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Time-study recorder: the floor reports how long each job really took, in GAME minutes (Time.time runs at 1 game minute per scaled second).
/// Samples are filed by WHO was running the warehouse (HAL the bot, or a human player), shown on the Bot Metrics / Player Metrics tabs, and
/// blended into labor standards on the Metrics Blended tab. They persist in their own folder (not the save files) so they keep building across
/// saves, loads and new games, and become the reference for balancing, difficulty levels, achievements and labor standards.
/// Hooks call <see cref="Record"/> when a job finishes; nothing here drives gameplay.
/// </summary>
public static class OpsMetrics
{
    // New kinds go on the end: samples are saved by number.
    public enum Kind { GateToDoor, Offload, LoadCycle, LoadTotal, PalletPick, CasePick, OffloadCycle, Receive }

    /// <summary>Who was running the warehouse when the sample was taken. Bot = HAL playing; Player = a human (including HAL only watching).</summary>
    public enum Source { Bot, Player }

    public struct Info { public Kind kind; public string title, what, unit; }

    /// <summary>Display order and wording for the metrics tabs.</summary>
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

    // t = real-world UTC ticks and d = difficulty, so the data can be sliced later (balance passes, difficulty levels, achievements, labor standards).
    [Serializable] class Sample { public int k; public float min; public int units; public long t; public int d; }
    [Serializable] class Store { public List<Sample> s = new(); }

    const int MaxPerKind = 300;           // kept per kind PER SOURCE
    const float Allowance = 1.15f;        // personal / fatigue / delay allowance on top of the observed average for a standard
    static readonly Dictionary<Source, Dictionary<Kind, List<Sample>>> _data = new()
    {
        { Source.Bot, new Dictionary<Kind, List<Sample>>() },
        { Source.Player, new Dictionary<Kind, List<Sample>>() },
    };
    static bool _loaded;
    static int _sinceSave;
    public static int Version { get; private set; }

    /// <summary>Own folder, separate from the saves so it survives every save, load and new game. In the editor it is also mirrored to
    /// the project's Metrics folder (outside Assets, so no reimport churn) so the data is git-trackable and travels between machines.</summary>
    public static string FolderPath => Path.Combine(Application.persistentDataPath, "Metrics");
    static string FileFor(Source src) => Path.Combine(FolderPath, src == Source.Bot ? "bot_metrics.json" : "player_metrics.json");
    static string LegacyFile => Path.Combine(Application.persistentDataPath, "ops_metrics.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { foreach (var d in _data.Values) d.Clear(); _loaded = false; _sinceSave = 0; }

    /// <summary>Game-minute clock.</summary>
    public static float Now => Time.time;

    /// <summary>The source new samples are filed under right now: Bot while HAL is actually playing, otherwise Player.</summary>
    public static Source CurrentSource
    {
        get
        {
            var bot = GameCore.Diagnostics.PlaytestBot.Instance;
            return bot != null && bot.Running && !bot.ObserveOnly ? Source.Bot : Source.Player;
        }
    }

    public static void Record(Kind kind, float minutes, int units = 1)
    {
        if (minutes <= 0.01f || minutes > 60f * 24f) return;
        Load();
        var map = _data[CurrentSource];
        if (!map.TryGetValue(kind, out var list)) map[kind] = list = new List<Sample>();
        list.Add(new Sample { k = (int)kind, min = minutes, units = Mathf.Max(1, units), t = DateTime.UtcNow.Ticks, d = PlayerPrefs.GetInt("Difficulty", 0) });
        if (list.Count > MaxPerKind) list.RemoveAt(0);
        Version++;
        if (++_sinceSave >= 10) Save();
    }

    public struct Stats
    {
        public int n; public float avg, best, worst, standard, perUnitAvg, perHourAvg;
    }

    /// <summary>Stats for one source (Bot or Player).</summary>
    public static Stats Get(Kind kind, Source src) => Compute(Samples(kind, src, false));

    /// <summary>Bot + Player samples together: the labor-standard view.</summary>
    public static Stats GetBlended(Kind kind) => Compute(Samples(kind, Source.Bot, true));

    static List<Sample> Samples(Kind kind, Source src, bool both)
    {
        Load();
        var all = new List<Sample>();
        if (both || src == Source.Bot) { if (_data[Source.Bot].TryGetValue(kind, out var b)) all.AddRange(b); }
        if (both || src == Source.Player) { if (_data[Source.Player].TryGetValue(kind, out var p)) all.AddRange(p); }
        return all;
    }

    static Stats Compute(List<Sample> list)
    {
        var st = new Stats();
        if (list.Count == 0) return st;
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

    public static void Clear(Source src)
    {
        Load();
        _data[src].Clear(); Version++;
        try { if (File.Exists(FileFor(src))) File.Delete(FileFor(src)); } catch { }
        try { var m = MirrorFor(src); if (m != null && File.Exists(m)) File.Delete(m); } catch { }
    }

    static string MirrorFor(Source src)
    {
#if UNITY_EDITOR
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Metrics", src == Source.Bot ? "bot_metrics.json" : "player_metrics.json"));
#else
        return null;
#endif
    }

    static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            foreach (Source src in Enum.GetValues(typeof(Source)))
            {
                string f = FileFor(src);
#if UNITY_EDITOR
                // A fresh machine has no persistent copy yet: seed from the tracked mirror so the data travels with the repo.
                if (!File.Exists(f)) { var m = MirrorFor(src); if (m != null && File.Exists(m)) { Directory.CreateDirectory(FolderPath); File.Copy(m, f); } }
#endif
                if (!File.Exists(f)) continue;
                var store = JsonUtility.FromJson<Store>(File.ReadAllText(f));
                if (store?.s == null) continue;
                foreach (var x in store.s)
                {
                    var k = (Kind)x.k;
                    if (!_data[src].TryGetValue(k, out var l)) _data[src][k] = l = new List<Sample>();
                    l.Add(x);
                }
            }
            // One-time migration of the old single file (written while HAL was doing the playing) into the Bot file.
            if (File.Exists(LegacyFile) && !File.Exists(FileFor(Source.Bot)))
            {
                var store = JsonUtility.FromJson<Store>(File.ReadAllText(LegacyFile));
                if (store?.s != null)
                {
                    foreach (var x in store.s)
                    {
                        var k = (Kind)x.k;
                        if (!_data[Source.Bot].TryGetValue(k, out var l)) _data[Source.Bot][k] = l = new List<Sample>();
                        l.Add(x);
                    }
                    Save();
                }
            }
        }
        catch (Exception e) { Debug.LogWarning("[OpsMetrics] load failed: " + e.Message); }
    }

    /// <summary>Flush to disk now (also called when the game quits so the last few samples are not lost).</summary>
    public static void Save()
    {
        _sinceSave = 0;
        try
        {
            Directory.CreateDirectory(FolderPath);
            foreach (Source src in Enum.GetValues(typeof(Source)))
            {
                var store = new Store();
                foreach (var kv in _data[src]) store.s.AddRange(kv.Value);
                string json = JsonUtility.ToJson(store, true);
                File.WriteAllText(FileFor(src), json);
                var m = MirrorFor(src);
                if (m != null) { Directory.CreateDirectory(Path.GetDirectoryName(m)); File.WriteAllText(m, json); }
            }
        }
        catch (Exception e) { Debug.LogWarning("[OpsMetrics] save failed: " + e.Message); }
    }

    /// <summary>Saves on quit so no samples are lost between the every-10-samples flushes.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookQuit()
    {
        Application.quitting -= OnQuit;
        Application.quitting += OnQuit;
    }
    static void OnQuit() { if (_loaded) Save(); }
}
