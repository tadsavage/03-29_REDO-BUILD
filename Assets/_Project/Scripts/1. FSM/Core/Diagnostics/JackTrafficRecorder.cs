using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace GameCore.Diagnostics
{
    /// <summary>
    /// DEV TOOL (2026-11-10): watches pallet-jack traffic. Reloads the quicksave, runs the game until <see cref="EndHour"/>, writes every
    /// jack's state twice a real second plus every traffic log line to Logs/JackTraffic/, then reloads and repeats <see cref="Runs"/> times.
    /// Start it from code: JackTrafficRecorder.Begin(runs, speed). Reads the order selectors' private traffic state by reflection,
    /// so it changes nothing in the simulation.
    /// </summary>
    public class JackTrafficRecorder : MonoBehaviour
    {
        public int Runs = 8;
        public int EndHour = 12;
        public float Speed = 1f;
        public float SampleRealSeconds = 0.5f;

        public static JackTrafficRecorder Instance { get; private set; }
        public int CurrentRun { get; private set; }
        public bool Finished { get; private set; }
        public string Status = "idle";

        private string _dir;
        private StringBuilder _samples, _log;
        private readonly List<MHEOperatorSlot> _jacks = new();
        private float _jacksAt = -10f;

        private static readonly BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static System.Type DriverT => typeof(GameCore.Actors.OrderSelectionTaskDriver);
        private static readonly Dictionary<string, FieldInfo> s_fields = new();

        public bool HoldSpeed = true;       // keep Time.timeScale at Speed for the whole run
        public bool ReloadEachRun = true;   // false: record the game as already loaded (one run), numbered FirstRun
        public int FirstRun = 1;

        /// <summary>Quickloading on top of an already-loaded scene leaves every pick face reading empty (2026-11-10), so for clean runs
        /// start Play fresh (it loads the quicksave) and call Begin(1, speed, 12, reload: false, firstRun: N).</summary>
        public static JackTrafficRecorder Begin(int runs, float speed, int endHour = 12, bool reload = true, int firstRun = 1)
        {
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("JackTrafficRecorder");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<JackTrafficRecorder>();
            r.Runs = runs; r.Speed = speed; r.EndHour = endHour; r.ReloadEachRun = reload; r.FirstRun = firstRun;
            Instance = r;
            return r;
        }

        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }
        private void Start() { StartCoroutine(RunAll()); }

        private static object Get(object o, string field)
        {
            if (o == null) return null;
            if (!s_fields.TryGetValue(field, out var fi)) { fi = DriverT.GetField(field, F); s_fields[field] = fi; }
            return fi?.GetValue(o);
        }

        private static GameCore.Economy.SimulationTimeService Clock()
        {
            GameCore.Services.ServiceLocator.TryGet<GameCore.Economy.SimulationTimeService>(out var t);
            return t;
        }

        private string GameTime()
        {
            var t = Clock();
            return t != null ? $"D{t.Day} {t.TimeString}" : "?";
        }

        private IEnumerator RunAll()
        {
            _dir = Path.Combine(Application.dataPath, "..", "Logs", "JackTraffic");
            Directory.CreateDirectory(_dir);
            for (CurrentRun = FirstRun; CurrentRun < FirstRun + Runs; CurrentRun++)
            {
                Status = $"run {CurrentRun}: loading";
                Time.timeScale = 0f;
                if (ReloadEachRun)
                {
                    var ps = FindAnyObjectByType<PlacementSystem>();
                    if (ps == null) { Status = "no PlacementSystem"; yield break; }
                    ps.QuickLoad();
                    yield return new WaitForSecondsRealtime(4f);
                }
                _samples = new StringBuilder("real,game,jack,x,z,yaw,driver,offJack,travel,held,heldBehind,heldSolid,bufferYield,passing,legTarget,queued,order\n");
                _log = new StringBuilder();
                _jacksAt = -10f;
                Time.timeScale = Speed;
                Status = $"run {CurrentRun}: running";
                float start = Time.unscaledTime;
                while (true)
                {
                    var t = Clock();
                    if (t != null && t.Hour >= EndHour) break;
                    if (Time.unscaledTime - start > 60f * 30f) break;   // safety
                    if (HoldSpeed)   // the playtest block changes speed; runs must match
                    {
                        // The HUD's 5x button is Time.timeScale only; the clock's own multiplier stays 1 (setting it too compounded).
                        if (t != null && !Mathf.Approximately(t.TimeSpeed, 1f)) t.TimeSpeed = 1f;
                        if (!Mathf.Approximately(Time.timeScale, Speed)) Time.timeScale = Speed;
                    }
                    Sample(Time.unscaledTime - start);
                    yield return new WaitForSecondsRealtime(SampleRealSeconds);
                }
                Time.timeScale = 0f;
                File.WriteAllText(Path.Combine(_dir, $"run{CurrentRun}_samples.csv"), _samples.ToString());
                File.WriteAllText(Path.Combine(_dir, $"run{CurrentRun}_log.txt"), _log.ToString());
                Debug.LogWarning($"[JackTrafficRecorder] run {CurrentRun} done at {GameTime()} after {Time.unscaledTime - start:0}s real.");
            }
            Finished = true;
            Status = "finished";
        }

        private void Sample(float real)
        {
            if (Time.unscaledTime - _jacksAt > 3f)
            {
                _jacksAt = Time.unscaledTime;
                _jacks.Clear();
                foreach (var s in FindObjectsByType<MHEOperatorSlot>(FindObjectsSortMode.None))
                    if (s != null && s.isActiveAndEnabled && s.IsPalletJack && s.GetComponent<PlacedObject>() != null) _jacks.Add(s);
            }
            var claims = DriverT.GetField("s_jackClaims", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as IDictionary<MHEOperatorSlot, GameCore.Actors.OrderSelectionTaskDriver>;
            string gt = GameTime();
            foreach (var j in _jacks)
            {
                if (j == null) continue;
                Vector3 p = j.transform.position;
                GameCore.Actors.OrderSelectionTaskDriver d = null;
                claims?.TryGetValue(j, out d);
                string N(object o) => o is Object uo ? (uo != null ? uo.name : "") : (o?.ToString() ?? "");
                Vector3 travel = d != null ? (Vector3)(Get(d, "_travelDir") ?? Vector3.zero) : Vector3.zero;
                Vector3 leg = d != null ? (Vector3)(Get(d, "_legTarget") ?? Vector3.zero) : Vector3.zero;
                var order = d != null ? Get(d, "_currentOrder") : null;
                string orderNo = order != null ? (order.GetType().GetProperty("OrderNumber")?.GetValue(order) ?? order.GetType().GetField("OrderNumber")?.GetValue(order))?.ToString() : "";
                _samples.Append($"{real:0.0},{gt},{j.name}#{j.GetHashCode()},{p.x:0.00},{p.z:0.00},{j.transform.eulerAngles.y:0},")
                        .Append(d != null ? d.name.Replace("Employee_", "") : "").Append(',')
                        .Append(d != null ? Get(d, "_offJack") : "").Append(',')
                        .Append(travel.sqrMagnitude > 0.0001f ? "1" : "0").Append(',')
                        .Append(d != null ? $"{(float)(Get(d, "_heldRealSeconds") ?? 0f):0.0}" : "").Append(',')
                        .Append(N(d != null ? Get(d, "_heldBehindJack") : null)).Append(',')
                        .Append((d != null ? Get(d, "_heldBySolid") as string : "")?.Replace(',', ';')).Append(',')
                        .Append(N(d != null ? Get(d, "_bufferYieldTo") : null)).Append(',')
                        .Append(d != null ? Get(d, "_passing") : "").Append(',')
                        .Append(leg.sqrMagnitude > 0.0001f ? $"{leg.x:0.0}/{leg.z:0.0}" : "").Append(',')
                        .Append(d != null ? Get(d, "_queuedStop") : "").Append(',')
                        .Append(orderNo).Append('\n');
            }
        }

        private void OnLog(string msg, string stack, LogType type)
        {
            if (_log == null || msg == null) return;
            if (msg.StartsWith("[JackTrafficRecorder]") || msg.Contains("diag: busy=")) return;
            if (msg.Contains("OrderSelectionTaskDriver") || msg.Contains("[Jack") || msg.Contains("PltJack") || msg.Contains("pallet jack")
                || msg.Contains("[DISMOUNT]") || msg.Contains("ReachTruck") || msg.Contains("[AiNavigation]"))
                _log.Append($"{Time.unscaledTime:0.0} {GameTime()} [{type}] {msg.Replace('\n', ' ')}\n");
        }
    }
}
