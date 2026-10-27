using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;

namespace GameCore.Diagnostics
{

    [Serializable] public class PlaytestBlockResult
    {
        public int block, startDay, endDay;
        public float score, onTime, fill, throughput, revenue, profit, cash, reliability, reputation;
        public int revenueTotal, repStart, repEnd;
        // what the planner reads and the Iterations tab graphs
        public float utilReceiver, utilReach, utilDock, utilSelector;
        public int headReceiver, headReach, headDock, headSelector, doors, machines;
        public float backlogReceive, backlogPutaway, backlogPick, backlogLoad, backlogOrders, waitingTrucks;
        public float shipFrac;
        public int ordersShipped, ordersLate, palletsShipped, bugIncidents, errors;
        public int cashStart, cashEnd;
        public int inventoryStart, inventoryEnd;   // stock on hand at cost: a liquid asset, counted as if it were cash in the Profit part (from block 1001's rescore on)
        public List<string> bugKeys = new();
        public List<int> bugCounts = new();
    }

    [Serializable] public class PlaytestScoreboard { public List<PlaytestBlockResult> blocks = new(); }

    /// <summary>
    /// The bot. Take the player's place and keep the warehouse running: sign contracts, book dock slots, release orders,
    /// buy stock. Meanwhile watch for bugs WITHOUT stopping (integrity checks + error log capture), and at the end of
    /// each N-day block pause and write a scored report so progress between blocks is measurable.
    /// Dev-only; started from Tools > Playtest Bot.
    /// </summary>
    public class PlaytestBot : MonoBehaviour
    {
        public static PlaytestBot Instance { get; private set; }
        public PlaytestBotSettings S;

        // ── run state ──
        public bool Running { get; private set; }
        public string Status => _status;
        public int BlockNo => _blockNo;
        public int BlockStartDay => _startDay;
        public int IncidentCount => _incidents.Values.Sum();
        public int ErrorCount => _errors;
        public int ActionsTaken => _actions.Count;
        public int ShippedThisBlock => _shipped;
        public int LateThisBlock => _late;
        int _blockNo, _startDay, _startCash, _startIncome, _startRep, _startInventory;

        /// <summary>Stock on hand valued at cost (cases x SKU cost). Counted with cash as the business's liquid worth.</summary>
        public static int InventoryValueNow()
        {
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return 0;
            double sum = 0;
            foreach (var rec in inv.AllPallets.Values)
            {
                if (rec == null || rec.Quantity <= 0) continue;
                var sku = inv.GetSkuData(rec.SkuId);
                if (sku != null) sum += rec.Quantity * (double)sku.BuyValue;
            }
            // Freight already paid for but still on the road is stock too - without it every purchase reads as money gone until the truck arrives.
            if (ServiceLocator.TryGet<ShipmentService>(out var ship) && ship != null)
                foreach (var sh in ship.PendingShipments)
                {
                    if (sh == null || !sh.PlayerOrdered) continue;
                    if (sh.Status != ShipmentData.ShipmentStatus.InTransit && sh.Status != ShipmentData.ShipmentStatus.Delayed && sh.Status != ShipmentData.ShipmentStatus.Receiving) continue;
                    foreach (var li in sh.LineItems)
                        if (!li.Dropped) sum += Mathf.Max(0, li.Quantity - li.ReceivedQuantity) * (double)li.UnitCost;
                }
            return (int)sum;
        }
        long _nextDecision, _nextHealth;
        int _lastBulkDay = -1, _bulkSignedToday, _spendDay = -1, _spentToday, _rushToday;
        string _status = "idle";
        bool _blockDoneAwaitingPlan;

        // ── block stats ──
        int _shipped, _shippedOnTime, _late, _palletsAtStart, _cashDipHours, _cashSamples, _purchases, _rushes, _signed, _booked, _released, _errors;
        readonly HashSet<string> _activeBugKeys = new();     // present at the last health check
        readonly Dictionary<string, int> _incidents = new(); // bug key -> episodes this block
        readonly Dictionary<string, string> _firstSeen = new();
        readonly List<string> _actions = new();
        readonly Dictionary<MHEOperatorSlot, (Vector3 pos, float movedAt)> _vehicleTrack = new();
        readonly List<PlaytestBlockResult> _history = new();
        readonly Dictionary<string, int> _repeatCounts = new();
        StringBuilder _bugLog = new();

        static readonly Regex Digits = new(@"[0-9a-fA-F]{8,}|-?\d+(\.\d+)?", RegexOptions.Compiled);
        static readonly string[] WatchWarnings = { "leaked", "stale link", "could not reach", "NO NAVMESH", "stuck", "Stuck", "never", "timed out" };
        static readonly Dictionary<string, float> Weight = new()
        {
            ["EXCEPTION"] = 4f, ["ERROR"] = 3f, ["WARN"] = 1f,
            ["MONEY-DRIFT"] = 4f, ["PARTNERSHIP-RANGE"] = 2f, ["DUPLINK"] = 2f, ["ORPHAN-OUTBOUND"] = 2f,
            ["NO-RECORD"] = 3f, ["NO-TASK"] = 2f, ["FLOATING"] = 3f, ["OVERLAP"] = 3f, ["GHOST-RECORD"] = 3f,
            ["STALE-ASSIGNED"] = 2f, ["REPEATING"] = 2f, ["TRUCK-TILT"] = 2f, ["TRUCK-SUNK"] = 3f, ["TRUCK-STALLED"] = 3f, ["TRUCK-OVERLAP"] = 2f,
            ["VEHICLE-STATIONARY"] = 2f, ["DOOR-DEADLOCK"] = 4f, ["GHOST-SLOT"] = 3f, ["PO-FAIL"] = 2f, ["BOT-ACTION-FAIL"] = 1f,
        };

        // ═════════════════════════════════════ lifecycle ═════════════════════════════════════

        public static PlaytestBot Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PlaytestBot") { hideFlags = HideFlags.DontSave };
            Instance = go.AddComponent<PlaytestBot>();
            Instance.S = LoadSettings();
            return Instance;
        }

        public static PlaytestBotSettings LoadSettings()
        {
            var s = Resources.Load<PlaytestBotSettings>("PlaytestBotSettings");
            return s != null ? s : ScriptableObject.CreateInstance<PlaytestBotSettings>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void OnEnable()
        {
            Application.logMessageReceived += OnLog;
            OrderService.OnOrderShipped += OnShipped;
            OrderService.OnOrderFined += OnFined;
        }

        void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            OrderService.OnOrderShipped -= OnShipped;
            OrderService.OnOrderFined -= OnFined;
        }

        /// <summary>True while a human is playing and the bot only WATCHES: it scores the block and logs bugs exactly as it does for itself, but makes no
        /// decisions (no contracts, bookings, releases, purchases, boarding) and the planner changes nothing. Player blocks are numbered 1001+ so their
        /// reports can never collide with HAL's, and are kept out of scoreboard.json (HAL's trend) in player_scoreboard.json instead.</summary>
        public bool ObserveOnly { get; private set; }

        /// <summary>Raised once when a block ends: (result, wasObserveOnly).</summary>
        public static event Action<PlaytestBlockResult, bool> BlockFinished;

        /// <summary>HAL's recorded blocks, read fresh from disk (never the player's).</summary>
        public List<PlaytestBlockResult> LoadHalHistory()
        {
            var l = new List<PlaytestBlockResult>();
            try { if (File.Exists(ScoreFile())) l.AddRange(JsonUtility.FromJson<PlaytestScoreboard>(File.ReadAllText(ScoreFile())).blocks); } catch { }
            return l;
        }

        string PlayerScoreFile() => Path.Combine(Folder(), "player_scoreboard.json");

        List<PlaytestBlockResult> LoadPlayerHistory()
        {
            var l = new List<PlaytestBlockResult>();
            try { if (File.Exists(PlayerScoreFile())) l.AddRange(JsonUtility.FromJson<PlaytestScoreboard>(File.ReadAllText(PlayerScoreFile())).blocks); } catch { }
            return l;
        }

        public void StartBlock(bool observeOnly = false)
        {
            ObserveOnly = observeOnly;
            if (!observeOnly) AutoObserveSuppressed = true;   // HAL is driving this session
            S = LoadSettings();   // always the live asset: a stale or blank copy ended a block the moment it started
            if (!ServiceLocator.TryGet<SimulationTimeService>(out var time) || !ServiceLocator.TryGet<MoneyService>(out var money))
            { Debug.LogWarning("[PlaytestBot] Services not ready - enter Play mode and load a save first."); return; }

            LoadHistory();
            // The next block is one past the HIGHEST recorded block, not one past the entry count: a scoreboard that lost rows
            // (block 7 once left only itself in the file) made the next run call itself "block 2" and overwrite the old block 2 reports.
            _blockNo = (_history.Count > 0 ? _history.Max(b => b.block) : 0) + 1;
            if (observeOnly) _blockNo = 1001 + LoadPlayerHistory().Count;               // a human's run: its own number series
            else if (_history.Count > 0) HalPlanner.PlanAndApply(S, _history, _blockNo);   // the one change for this block
            _startDay = time.Day; _startCash = money.CurrentCapital; _startIncome = money.LifetimeIncome.Values.Sum(); _startInventory = InventoryValueNow();
            _startRep = ServiceLocator.TryGet<ReputationService>(out var rs0) ? rs0.Score : 0;
            _shipped = _shippedOnTime = _late = _movedFines = _cashDipHours = _cashSamples = _purchases = _rushes = _signed = _booked = _released = _errors = 0;
            _palletsAtStart = PalletsShippedTotal(0, time.Day);
            HalMetrics.Reset();
            _repeatCounts.Clear();
            _recentScore.Clear(); _scoreInit = false; LiveScore = 0f; _startKey = Key(time);
            _lastLateWhy = _lastShortWhy = _lastDeadlockWhy = _lastRepeatWhy = null; _unclaimedSince.Clear(); _streakBlock = -1;
            _activeBugKeys.Clear(); _incidents.Clear(); _firstSeen.Clear(); _actions.Clear(); _vehicleTrack.Clear();
            _bugLog = new StringBuilder();
            _bugLines = 0;
            _nextDecision = 0; _nextHealth = 0;
            Running = true; _status = $"block {_blockNo} running";
            if (observeOnly)
            {
                // The player sets the speed. HAL only watches and scores.
                SystemsLogWindow.LogHal($"HAL 9000 is watching. Your block {_blockNo - 1000}, day {_startDay}, cash ${_startCash:N0}. {S.daysPerBlock} days. Good luck.");
                Debug.LogWarning($"[PlaytestBot] PLAYER block {_blockNo} started on day {_startDay}, cash ${_startCash:N0} (observe only).");
                return;
            }
            Time.timeScale = Mathf.Max(0.1f, S.gameSpeed);
            SystemsLogWindow.LogHal($"HAL 9000 initiated. Block {_blockNo}, day {_startDay}, cash ${_startCash:N0}.");
            Debug.LogWarning($"[PlaytestBot] Block {_blockNo} started on day {_startDay} at {S.gameSpeed}x, cash ${_startCash:N0}.");
        }

        public void StopBot() { Running = false; AutoObserveSuppressed = true; _status = "stopped"; Debug.LogWarning("[PlaytestBot] Stopped."); }

        /// <summary>True once HAL has been asked to drive (a self-propelled block started) or the bot was stopped by hand: the always-on observer then stays out of the way
        /// until the next scene load (PlayerScoreHud clears it).</summary>
        public static bool AutoObserveSuppressed;

        /// <summary>A save was just loaded without a saved block: end any observer block begun before the load so the auto-observer starts a fresh one from the loaded state.</summary>
        public void DropObserverForLoad() { if (Running && ObserveOnly) { Running = false; _status = "reloaded"; } }

        // ── a human's block lives in the SAVE ──
        // The block's counters are plain fields, so stopping Play, reloading a save or an editor hiccup used to wipe a
        // player's score mid-run and the next "I'll play" started a fresh block from the loaded day. They now ride along in
        // every save (SaveData.playtestState) and a load of that save resumes the same block.
        [Serializable]
        class BotState
        {
            public int blockNo, startDay, startCash, startIncome, startRep, startInventory, shipped, shippedOnTime, late, palletsAtStart, cashDipHours, cashSamples, errors;
            public long startKey;
            public List<string> incKeys = new(); public List<int> incCounts = new();
            public List<string> seenKeys = new(); public List<string> seenVals = new();
            public List<string> activeKeys = new();
        }

        /// <summary>JSON of the running player block, or null when no player block is running (HAL's own blocks are not saved).</summary>
        public string ExportState()
        {
            if (!Running || !ObserveOnly) return null;
            var st = new BotState
            {
                blockNo = _blockNo, startDay = _startDay, startCash = _startCash, startIncome = _startIncome, startRep = _startRep, startInventory = _startInventory,
                shipped = _shipped, shippedOnTime = _shippedOnTime, late = _late, palletsAtStart = _palletsAtStart, cashDipHours = _cashDipHours, cashSamples = _cashSamples,
                errors = _errors, startKey = _startKey,
            };
            foreach (var kv in _incidents) { st.incKeys.Add(kv.Key); st.incCounts.Add(kv.Value); }
            foreach (var kv in _firstSeen) { st.seenKeys.Add(kv.Key); st.seenVals.Add(kv.Value); }
            st.activeKeys.AddRange(_activeBugKeys);
            return JsonUtility.ToJson(st);
        }

        /// <summary>Resumes a player block from <see cref="ExportState"/> output (a save load).</summary>
        public void ImportState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            BotState st;
            try { st = JsonUtility.FromJson<BotState>(json); } catch (Exception e) { Debug.LogWarning("[PlaytestBot] Couldn't read saved block state: " + e.Message); return; }
            if (st == null) return;
            S = LoadSettings();
            ObserveOnly = true;
            _blockNo = st.blockNo; _startDay = st.startDay; _startCash = st.startCash; _startIncome = st.startIncome; _startRep = st.startRep; _startInventory = st.startInventory;
            _shipped = st.shipped; _late = st.late; _shippedOnTime = st.shippedOnTime > 0 || st.shipped == 0 ? st.shippedOnTime : Mathf.Max(0, st.shipped - st.late); _palletsAtStart = st.palletsAtStart; _cashDipHours = st.cashDipHours; _cashSamples = st.cashSamples;
            _errors = st.errors; _startKey = st.startKey;
            _purchases = _rushes = _signed = _booked = _released = 0;
            _incidents.Clear(); for (int i = 0; i < st.incKeys.Count && i < st.incCounts.Count; i++) _incidents[st.incKeys[i]] = st.incCounts[i];
            _firstSeen.Clear(); for (int i = 0; i < st.seenKeys.Count && i < st.seenVals.Count; i++) _firstSeen[st.seenKeys[i]] = st.seenVals[i];
            _activeBugKeys.Clear(); foreach (var k in st.activeKeys) _activeBugKeys.Add(k);
            _actions.Clear(); _vehicleTrack.Clear(); _repeatCounts.Clear();
            HalMetrics.Reset();
            _recentScore.Clear(); _scoreInit = false;
            _bugLog = new StringBuilder(); _bugLines = 0;
            _nextDecision = 0; _nextHealth = 0;
            Running = true; _status = $"block {_blockNo} running";
            Debug.LogWarning($"[PlaytestBot] PLAYER block {_blockNo} RESUMED from the save (started day {_startDay}, {_shipped} shipped, {_late} late, {_incidents.Values.Sum()} incidents).");
        }

        static long Key(SimulationTimeService t) => (long)t.Day * 1440 + t.Hour * 60 + t.Minute;

        void Update()
        {
            if (!Running) return;
            if (!ServiceLocator.TryGet<SimulationTimeService>(out var time)) return;
            long now = Key(time);

            if (now >= _nextHealth) { _nextHealth = now + Mathf.Max(5, S.healthCheckIntervalMinutes); SafeRun(HealthCheck, "health"); }
            if (now >= _nextDecision) { _nextDecision = now + Mathf.Max(5, S.decisionIntervalMinutes); SafeRun(Decide, "decide"); }
            if (S == null || S.daysPerBlock < 1) S = LoadSettings();
            if (time.Day >= _startDay + Mathf.Max(1, S.daysPerBlock)) FinishBlock();
        }

        void SafeRun(Action a, string what)
        {
            try { a(); }
            catch (Exception e) { Note("BOT-ACTION-FAIL", $"{what}: {e.GetType().Name} {e.Message}"); }
        }


        // ═════════════════════════════════════ running score ═════════════════════════════════════
        // The same eight weighted parts as the end-of-block score, evaluated on the numbers so far (targets are prorated by days elapsed).
        // Every time something moves it by 0.05 or more, the change and its cause go into a list of the last five - shown in the bottom-bar
        // modal (HalScoreModal).

        public class ScoreChange { public string when; public float delta; public string cause; public float total; }

        /// <summary>Raised the moment a score change is recorded (the falling text under the player's score box listens).</summary>
        public static event System.Action<ScoreChange> ScoreChanged;

        readonly List<ScoreChange> _recentScore = new();
        public IReadOnlyList<ScoreChange> RecentScoreChanges => _recentScore;
        public float LiveScore { get; private set; }
        public int BlockDay { get { return ServiceLocator.TryGet<SimulationTimeService>(out var t) ? Mathf.Max(1, t.Day - _startDay + 1) : 1; } }
        long _startKey;
        bool _scoreInit;
        float[] _lastComps = new float[8];
        static readonly string[] CompNames = { "On-time delivery", "Fill rate", "Throughput", "Revenue", "Profit", "Cash health", "Reliability", "Reputation" };

        float ScoreNow(float[] c)
        {
            if (!ServiceLocator.TryGet<SimulationTimeService>(out var time) || !ServiceLocator.TryGet<MoneyService>(out var money)) return LiveScore;
            ServiceLocator.TryGet<FulfillmentStatsService>(out var stats);
            float elapsed = Mathf.Max(0.25f, (Key(time) - _startKey) / 1440f);
            int pallets = PalletsShippedTotal(0, time.Day + 1) - _palletsAtStart;
            int revenue = money.LifetimeIncome.Values.Sum() - _startIncome;
            int net = (money.CurrentCapital + InventoryValueNow()) - (_startCash + _startInventory);   // stock counts as money in the pocket
            float penalty = _incidents.Sum(kv => WeightOf(kv.Key) * (1f + Mathf.Log(kv.Value, 2f)));
            int rep = ServiceLocator.TryGet<ReputationService>(out var rs) ? rs.Score : _startRep;
            c[0] = OnTimeRatio();
            c[1] = _shipped > 0 && stats != null ? Mathf.Clamp01(stats.AvgFillRate) : 0f;
            c[2] = Mathf.Clamp01(pallets / Mathf.Max(1f, S.targetPalletsPerDay * elapsed));
            c[3] = Mathf.Clamp01(revenue / Mathf.Max(1f, S.targetRevenuePerDay * elapsed));
            c[4] = Mathf.Clamp01(0.5f + 0.5f * net / Mathf.Max(1f, S.targetProfitPerDay * elapsed));
            c[5] = _cashSamples > 0 ? 1f - (float)_cashDipHours / _cashSamples : 1f;
            c[6] = 1f / (1f + penalty / Mathf.Max(1f, S.reliabilityTolerance));
            c[7] = Mathf.Clamp01(0.5f + 0.5f * (rep - _startRep) / Mathf.Max(1f, S.reputationGainPerBlock * elapsed / Mathf.Max(1, S.daysPerBlock)));
            float[] w = { S.wOnTime, S.wFill, S.wThroughput, S.wRevenue, S.wProfit, S.wCash, S.wReliability, S.wReputation };
            float tot = 0f; for (int i = 0; i < 8; i++) tot += w[i] * c[i];
            return 100f * tot / Mathf.Max(1, S.TotalWeight);
        }

        /// <summary>Re-score; if it moved, remember by how much and why. A null cause names whichever part moved the most.</summary>
        void EvaluateScore(string cause)
        {
            if (!Running || S == null) return;
            var c = new float[8];
            float total = ScoreNow(c);
            if (!_scoreInit) { _scoreInit = true; LiveScore = total; System.Array.Copy(c, _lastComps, 8); return; }
            float d = total - LiveScore;
            if (Mathf.Abs(d) >= 0.05f)
            {
                if (string.IsNullOrEmpty(cause))
                {
                    float[] w = { S.wOnTime, S.wFill, S.wThroughput, S.wRevenue, S.wProfit, S.wCash, S.wReliability, S.wReputation };
                    int best = 0; float bestMove = 0f;
                    for (int i = 0; i < 8; i++) { float m = Mathf.Abs(w[i] * (c[i] - _lastComps[i])); if (m > bestMove) { bestMove = m; best = i; } }
                    cause = $"{CompNames[best]} {(c[best] >= _lastComps[best] ? "up" : "down")}";
                }
                string when = ServiceLocator.TryGet<SimulationTimeService>(out var t) ? $"Day {t.Day} {t.Hour:00}:{t.Minute:00}" : "";
                _recentScore.Insert(0, new ScoreChange { when = when, delta = d, cause = cause, total = total });
                while (_recentScore.Count > 300) _recentScore.RemoveAt(300);
                try { ScoreChanged?.Invoke(_recentScore[0]); } catch (System.Exception e) { Debug.LogWarning("[PlaytestBot] ScoreChanged listener: " + e.Message); }
            }
            LiveScore = total;
            System.Array.Copy(c, _lastComps, 8);
        }

        // ═════════════════════════════════════ decisions ═════════════════════════════════════

        void Decide()
        {
            if (ObserveOnly) return;   // a human is playing: the bot makes no decisions
            if (!ServiceLocator.TryGet<MoneyService>(out var money) || !ServiceLocator.TryGet<SimulationTimeService>(out var time)) return;
            ServiceLocator.TryGet<OrderArrivalService>(out var arrivals);
            ServiceLocator.TryGet<OrderService>(out var orders);
            ServiceLocator.TryGet<DockScheduleService>(out var schedule);

            if (money.CurrentCapital < S.minCashBalance && S.bailoutCash > 0 && money.CurrentCapital < 2000)
            { money.AddCapital(S.bailoutCash, "Bot Bailout"); Act($"BAILOUT +${S.bailoutCash}"); }

            EnsureOperatorsBoarded();
            if (arrivals != null) SignContracts(arrivals, money, time);
            if (schedule != null) BookDock(schedule, time);
            if (orders != null && schedule != null) ReleaseOrders(orders, schedule, time);
            Purchase(money, time, schedule);
        }

        // The walk-to-machine order given at hire time sometimes never takes (observed 2026-10-24: both operators patrolled while their
        // machines sat empty, so nothing was received or put away). Re-issue it every tick for anyone not aboard or already walking.
        void EnsureOperatorsBoarded()
        {
            MHEOperatorSlot[] slots = null;
            foreach (var id in FindObjectsByType<EmployeeIdentity>())
            {
                if (id == null || id.Record == null) continue;
                var role = id.Record.role;
                if (role != EmployeeRole.DockStockerOperator && role != EmployeeRole.ReachTruckOperator) continue;
                var nav = id.GetComponent<AiNavigation>();
                if (nav == null || !nav.enabled || nav.IsSeekingEquipment || id.GetComponentInParent<MHEOperatorSlot>() != null) continue;
                slots ??= FindObjectsByType<MHEOperatorSlot>();
                string kind = role == EmployeeRole.DockStockerOperator ? "Dockstocker" : "ReachTruck";
                var slot = slots.FirstOrDefault(m => m != null && m.name.StartsWith(kind) && !m.IsOccupied && m.AcceptsOperator);
                if (slot == null) continue;
                nav.SeekEquipment(slot);
                if (nav.IsSeekingEquipment) Act($"sent {id.Record.employeeName} to the {kind}");
            }
        }

        void SignContracts(OrderArrivalService arrivals, MoneyService money, SimulationTimeService time)
        {
            int running = arrivals.RunningAccounts.Count();
            foreach (var c in arrivals.AvailableOffers.Where(c => !c.IsBulk).ToList())
            {
                if (running >= S.maxRecurringAccounts) break;
                if (arrivals.Sign(c.ContractId)) { running++; _signed++; Act($"signed recurring {c.ContractId}"); }
            }

            if (time.Day != _lastBulkDay) { _lastBulkDay = time.Day; _bulkSignedToday = 0; }
            if (money.CurrentCapital >= S.minCashBalance * S.bulkCashMultiple && CoveredPallets() >= S.minPalletsBeforeBulk)
                foreach (var c in arrivals.AvailableOffers.Where(c => c.IsBulk).ToList())
                {
                    if (_bulkSignedToday >= S.bulkOffersPerDay) break;
                    if (arrivals.Sign(c.ContractId)) { _bulkSignedToday++; _signed++; Act($"signed bulk {c.ContractId}"); }
                }
        }

        // Pallets of product physically here or already on an open purchase order. A bulk order is only accepted when this is enough
        // to ship it: HAL must not promise stock it has not bought.
        int CoveredPallets()
        {
            int pallets = 0;
            foreach (var r in ItemAvailability.Build())
                if (r.CasesPerPallet > 0) pallets += (r.OnHand + r.Inbound) / r.CasesPerPallet;
            return pallets;
        }

        IEnumerable<(int day, int block)> Slots(SimulationTimeService time, int lastDay)
        {
            for (int d = time.Day; d <= Mathf.Max(lastDay, time.Day); d++)
                for (int b = (d == time.Day ? Mathf.Min(11, time.Hour / 2 + 1) : 0); b < 12; b++)
                    yield return (d, b);
        }

        // One trailer only carries so much: a group of orders that is too big is booked as several trailers.
        static IEnumerable<UnscheduledGroup> SplitToFit(DockScheduleService schedule, UnscheduledGroup g)
        {
            var cur = new List<string>();
            foreach (var id in g.OrderIds)
            {
                var trial = new List<string>(cur) { id };
                if (cur.Count > 0 && !schedule.FitsOneTrailer(trial)) { yield return Part(g, cur); cur = new List<string>(); }
                cur.Add(id);
            }
            if (cur.Count > 0) yield return Part(g, cur);
        }

        static UnscheduledGroup Part(UnscheduledGroup g, List<string> ids)
            => new UnscheduledGroup { CustomerId = g.CustomerId, CustomerName = g.CustomerName, ContractId = g.ContractId, IsBulk = g.IsBulk, OrderIds = ids, EarliestDueDay = g.EarliestDueDay };

        // Cases of each item that customers ordered over the last three days (open orders count in full).
        static Dictionary<string, int> RecentDemandCases(int today)
        {
            var d = new Dictionary<string, int>();
            if (!ServiceLocator.TryGet<OrderService>(out var orders)) return d;
            void Add(OrderData o) { if (o == null || o.ReturnsMode) return; foreach (var li in o.LineItems) { if (li == null || string.IsNullOrEmpty(li.SkuId)) continue; d.TryGetValue(li.SkuId, out int n); d[li.SkuId] = n + li.QuantityNeeded; } }
            foreach (var o in orders.ActiveOrders) Add(o);
            foreach (var o in orders.OrderHistory) if (o != null && o.DueDay >= today - 3) Add(o);
            return d;
        }

        void BookDock(DockScheduleService schedule, SimulationTimeService time)
        {
            var doors = schedule.OutboundDoors();
            if (doors.Count == 0) return;

            // 1. Inbound first, in the earliest free slots, so that the outbound bookings below can count on what it will bring.
            foreach (var appt in schedule.ParkedAppointments.ToList())
            {
                bool done = false;
                foreach (var (d, b) in Slots(time, Mathf.Max(appt.Day, time.Day) + 2))
                {
                    if (d < appt.Day || !schedule.HasRoom(d, b)) continue;
                    foreach (int door in doors)
                        if (schedule.TryMoveToDoor(appt.Id, d, b, door, out _)) { done = true; _booked++; Act($"placed parked {appt.Kind} {appt.CustomerName} day {d} block {b} door {door}"); break; }
                    if (done) break;
                }
            }

            // 2. Outbound. A trailer is booked only when everything on its orders is in the building or on an inbound PO that is already on the
            // calendar, and never sooner than the lead time from now. Otherwise the order simply waits unscheduled and the buying rule goes
            // and gets the stock. A trailer parked at a door with nothing to ship holds the door for days and blocks every inbound behind it.
            if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null) return;
            var timeline = BuildStockTimeline(schedule, time, orders);
            var cpp = new Dictionary<string, int>();
            foreach (var r in ItemAvailability.Build()) if (r.CasesPerPallet > 0) cpp[r.SkuId] = r.CasesPerPallet;
            int nowHours = (time.Day - 1) * 24 + time.Hour;
            foreach (var g in schedule.UnscheduledGroups().ToList().SelectMany(grp => SplitToFit(schedule, grp)).ToList())
            {
                if (!TryStockReadyHour(g, timeline, orders, out int readyHour)) continue;   // not coverable yet: no trailer
                // The trailer must not arrive before the work of getting this order onto it can be done, counted from when the stock
                // is in the building. Pallet-only orders are a straight roll-on / roll-off (stage + load); case-pick orders take as long
                // as their selector trips do; rush orders are off, so there is no last-minute scratch run to catch up with.
                float turnHours = TurnHoursFor(g, orders, cpp, out string kind);
                int earliestStart = Mathf.Max(nowHours + Mathf.Max(0, S.outboundLeadHours), Mathf.CeilToInt(readyHour + turnHours));
                int earliestDay = earliestStart / 24 + 1;
                bool done = false;
                foreach (var (d, b) in Slots(time, Mathf.Max(g.EarliestDueDay, earliestDay)))
                {
                    if ((d - 1) * 24 + b * DockScheduleService.BlockHours < earliestStart) continue;
                    if (!schedule.HasRoom(d, b)) continue;
                    foreach (int door in doors)
                        if (schedule.TryBookGroupAtDoor(d, b, door, g, out _, out _)) { done = true; _booked++; Act($"booked {g.CustomerName} day {d} block {b} door {door} ({kind}: stock ready hour {readyHour} + {turnHours:F1}h of work)"); break; }
                    if (done) break;
                }
            }
        }

        // When each item is available, as (hour, cases) entries: what is on the shelves now, plus every inbound PO that already has a slot on
        // the calendar (available a buffer after that slot ends: offload, receive, putaway). Cases already promised to orders that HAVE a
        // trailer booked are taken out first, so two orders never count the same pallet.
        Dictionary<string, List<(int hour, int cases)>> BuildStockTimeline(DockScheduleService schedule, SimulationTimeService time, OrderService orders)
        {
            int nowHours = (time.Day - 1) * 24 + time.Hour;
            var t = new Dictionary<string, List<(int hour, int cases)>>();
            foreach (var r in ItemAvailability.Build())
                if (r.OnHand > 0) t[r.SkuId] = new List<(int hour, int cases)> { (nowHours, r.OnHand) };

            if (ServiceLocator.TryGet<ShipmentService>(out var ship) && ship != null)
                foreach (var po in ship.PendingShipments)
                {
                    if (po == null || po.Status != ShipmentData.ShipmentStatus.InTransit) continue;
                    var appt = schedule.FindForPo(po.PONumber);
                    if (appt == null || appt.Parked) continue;                           // not on the calendar: do not count on it
                    int ready = (appt.Day - 1) * 24 + appt.EndHour + Mathf.Max(0, S.inboundBufferHours);
                    foreach (var li in po.LineItems)
                    {
                        if (li == null || li.Dropped || string.IsNullOrEmpty(li.SkuId)) continue;
                        int cases = li.Quantity - li.ReceivedQuantity;
                        if (cases <= 0) continue;
                        if (!t.TryGetValue(li.SkuId, out var list)) t[li.SkuId] = list = new List<(int hour, int cases)>();
                        list.Add((Mathf.Max(ready, nowHours), cases));
                    }
                }
            foreach (var list in t.Values) list.Sort((a, b) => a.hour.CompareTo(b.hour));

            foreach (var o in orders.ActiveOrders)
            {
                if (o == null || o.ReturnsMode) continue;
                if (o.Status != OrderData.OrderStatus.Pending && o.Status != OrderData.OrderStatus.PartiallyPicked && o.Status != OrderData.OrderStatus.Backorder) continue;
                if (schedule.FindForOrder(o.OrderId) == null) continue;                   // only the orders that already hold a trailer
                foreach (var li in o.LineItems) ConsumeStock(t, li.SkuId, li.QuantityRemaining, out _);
            }
            return t;
        }

        // How long, in game hours, the floor needs once the stock is in the building before the trailer can leave.
        //  - Pallet-only order ("straight roll-on / roll-off"): the reach truck pulls whole pallets to the lane and the loader takes them: short.
        //  - Case-pick order: every selector trip is a picklist (~2-2.5 h, two pallets a trip); the selectors work in parallel, so the time is
        //    the number of rounds the picklists need. Pallet picks run on another role and always finish first, so they add nothing.
        // The order is a case-pick order when any line leaves loose cases (a quantity that is not a whole number of pallets).
        float TurnHoursFor(UnscheduledGroup g, OrderService orders, Dictionary<string, int> cpp, out string kind)
        {
            float loosePalletEquivalents = 0f;
            foreach (var id in g.OrderIds)
            {
                var o = orders.ActiveOrders.FirstOrDefault(x => x != null && x.OrderId == id);
                if (o == null) continue;
                foreach (var li in o.LineItems)
                {
                    if (li == null || li.QuantityRemaining <= 0) continue;
                    if (!cpp.TryGetValue(li.SkuId, out int full) || full <= 0) { loosePalletEquivalents += li.QuantityRemaining / 48f; continue; }
                    int loose = li.QuantityRemaining % full;
                    if (loose > 0) loosePalletEquivalents += loose / (float)full;
                }
            }
            if (loosePalletEquivalents < 0.0001f) { kind = "pallet-only"; return S.palletTurnHours; }

            int looseTrips = Mathf.Max(1, Mathf.CeilToInt(Mathf.CeilToInt(loosePalletEquivalents - 0.0001f) / (float)OrderService.SelectorPalletsPerTrip));
            int selectors = Mathf.Max(1, HalMetrics.Headcount(EmployeeRole.OrderSelector));
            int rounds = Mathf.CeilToInt(looseTrips / (float)selectors);
            kind = $"case-pick, {looseTrips} picklist(s) / {selectors} selector(s)";
            return rounds * S.pickHoursPerPicklist + S.loadBufferHours;
        }

        static bool ConsumeStock(Dictionary<string, List<(int hour, int cases)>> t, string sku, int need, out int maxHour)
        {
            maxHour = 0;
            if (need <= 0) return true;
            if (string.IsNullOrEmpty(sku) || !t.TryGetValue(sku, out var list)) return false;
            int left = need;
            for (int i = 0; i < list.Count && left > 0; i++)
            {
                int take = Mathf.Min(left, list[i].cases);
                left -= take;
                maxHour = Mathf.Max(maxHour, list[i].hour);
                list[i] = (list[i].hour, list[i].cases - take);
            }
            list.RemoveAll(e => e.cases <= 0);
            return left == 0;
        }

        // True when the whole group's orders can be filled from the timeline; readyHour is the latest hour any of it arrives.
        // On success the cases are taken out of the timeline for good; on failure it is left exactly as it was.
        static bool TryStockReadyHour(UnscheduledGroup g, Dictionary<string, List<(int hour, int cases)>> timeline, OrderService orders, out int readyHour)
        {
            readyHour = 0;
            var need = new Dictionary<string, int>();
            foreach (var id in g.OrderIds)
            {
                var o = orders.ActiveOrders.FirstOrDefault(x => x != null && x.OrderId == id);
                if (o == null) continue;
                foreach (var li in o.LineItems)
                    if (li != null && li.QuantityRemaining > 0 && !string.IsNullOrEmpty(li.SkuId))
                        need[li.SkuId] = (need.TryGetValue(li.SkuId, out var n) ? n : 0) + li.QuantityRemaining;
            }
            if (need.Count == 0) return true;

            var trial = new Dictionary<string, List<(int hour, int cases)>>();
            foreach (var sku in need.Keys)
            {
                if (!timeline.TryGetValue(sku, out var l)) return false;
                trial[sku] = new List<(int hour, int cases)>(l);
            }
            foreach (var kv in need)
            {
                if (!ConsumeStock(trial, kv.Key, kv.Value, out int h)) return false;
                readyHour = Mathf.Max(readyHour, h);
            }
            foreach (var kv in trial) timeline[kv.Key] = kv.Value;
            return true;
        }

        void ReleaseOrders(OrderService orders, DockScheduleService schedule, SimulationTimeService time)
        {
            // Only orders whose trailer arrives within the lead window (or is already due): the rest wait, so lanes stay free.
            int nowHours = (time.Day - 1) * 24 + time.Hour;
            int lead = Mathf.Max(0, S.releaseLeadHours);
            var ready = orders.ActiveOrders.Where(o =>
            {
                if (o == null || !orders.IsAwaitingRelease(o)) return false;
                var appt = schedule.FindForOrder(o.OrderId);
                if (appt == null) return false;
                int startHours = (appt.Day - 1) * 24 + appt.StartHour;
                return startHours - nowHours <= lead;
            }).ToList();
            if (ready.Count == 0) return;
            if (orders.ReleaseOrdersToStages(ready.Select(o => o.OrderId).ToList(), out string why, out bool waiting, out _))
            { _released += ready.Count; Act($"released {ready.Count} order(s){(waiting ? " (waiting for lane)" : "")}"); }
            else Note("BOT-ACTION-FAIL", $"release failed: {why}");
        }

        void Purchase(MoneyService money, SimulationTimeService time, DockScheduleService schedule)
        {
            if (!ServiceLocator.TryGet<ShipmentService>(out var ship) || !ServiceLocator.TryGet<InventoryService>(out _)) return;
            int pending = ship.PendingShipments.Count(s => s.Status == ShipmentData.ShipmentStatus.InTransit);
            if (pending >= S.maxPendingPOs) return;

            var rows = ItemAvailability.Build();
            var active = new HashSet<string>(SlotAssignmentService.AllAssignments.Values);
            foreach (var r in rows) if (r.Pending > 0) active.Add(r.SkuId);

            int cash = money.CurrentCapital;
            bool bulkUp = cash > S.minCashBalance * S.bulkUpCashMultiple;
            int wantPallets = bulkUp ? S.bulkUpPalletsPerSku : S.palletsPerActiveSku;
            if (_spendDay != time.Day) { _spendDay = time.Day; _spentToday = 0; _rushToday = 0; }
            int budget = Mathf.Min(cash - S.minCashBalance, Mathf.RoundToInt((cash - S.minCashBalance) * S.maxDailySpendFraction) + 0) - _spentToday;
            if (budget <= 0) return;

            // Rush first: an order is owed, nothing covers it, and a vendor sells it.
            if (S.coverRushOrders && _rushToday < S.maxRushPerDay)
            {
                var rush = rows.Where(r => r.Status == AvailabilityStatus.Insufficient && r.CasesPerPallet > 0 && r.VendorId != null && r.Inbound == 0).Take(4).ToList();
                if (rush.Count > 0 && TryRaisePO(ship, time, rush.Select(r => (r, 1)).ToList(), ShipmentService.RushSupplierId, "Rush Courier", 2, ref budget, rushPo: true))
                { _rushes++; _rushToday++; return; }
            }

            // Normal restock, one vendor per PO.
            // Orders already promised come first, and they are bought ONE ORDER AT A TIME, soonest due first, only when the whole shortfall
            // of that order fits the budget. (Block 6 spread $123k as a pallet or two of ~45 different SKUs, so no order was ever
            // fully covered and nothing shipped: shipping needs every line of an order, not a little of every line of every order.)
            var owed = PlanOrderCoverage(rows, budget);
            foreach (var g in owed.GroupBy(x => x.row.VendorId))
            {
                if (pending >= S.maxPendingPOs) break;
                var vendor = VendorRegistry.Load()?.GetById(g.Key);
                if (TryRaisePO(ship, time, g.Select(x => (x.row, x.pallets)).ToList(), g.Key, vendor != null ? vendor.DisplayName : g.Key, 1, ref budget, false, vendor != null ? vendor.MinimumOrderCases : 0))
                { pending++; _purchases++; return; }   // one PO per decision; the next tick re-reads what is now on order
            }

            // Restock only items customers are actually ordering, and only up to a few days of that demand. (Block 2 stocked every
            // assigned pick slot to three pallets whatever was selling, and spent $123k in a week against $47k of revenue.)
            float stockValue = 0f;
            foreach (var r in rows) stockValue += (r.OnHand + r.Inbound) * r.PurchasePrice;
            if (stockValue >= S.inventoryValueCap) return;
            var demand = RecentDemandCases(time.Day);
            var needs = rows.Where(r => demand.ContainsKey(r.SkuId) && r.CasesPerPallet > 0 && r.VendorId != null)
                            .Select(r =>
                            {
                                float perDay = demand[r.SkuId] / 3f;
                                int target = Mathf.Clamp(Mathf.CeilToInt(perDay * Mathf.Max(1, S.stockCoverDays) / r.CasesPerPallet), 1, Mathf.Max(1, S.palletsPerActiveSku));
                                int have = Mathf.CeilToInt((r.OnHand + r.Inbound - r.Pending) / (float)r.CasesPerPallet);
                                return (row: r, pallets: Mathf.Max(0, target - have));
                            })
                            .Where(x => x.pallets > 0)
                            .OrderByDescending(x => x.pallets).ToList();
            foreach (var g in needs.GroupBy(x => x.row.VendorId))
            {
                if (pending >= S.maxPendingPOs) break;
                var vendor = VendorRegistry.Load()?.GetById(g.Key);
                if (TryRaisePO(ship, time, g.Select(x => (x.row, x.pallets)).ToList(), g.Key, vendor != null ? vendor.DisplayName : g.Key, 1, ref budget, false, vendor != null ? vendor.MinimumOrderCases : 0))
                { pending++; _purchases++; }
            }
        }

        /// <summary>
        /// Picks what to buy so that whole orders become fillable. Walks the open orders soonest-due first, hands each one stock that is
        /// already on hand or on its way (a running pool, so two orders never count the same pallet), and for the part still short asks for
        /// whole pallets. An order is bought for only if EVERY line has a vendor and its full cost fits what is left of the budget;
        /// otherwise it is skipped and the pool is left untouched, so a smaller or cheaper order behind it can still be covered.
        /// </summary>
        List<(ItemAvailabilityRow row, int pallets)> PlanOrderCoverage(List<ItemAvailabilityRow> rows, int budget)
        {
            var result = new List<(ItemAvailabilityRow row, int pallets)>();
            if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null) return result;
            var bySku = rows.ToDictionary(r => r.SkuId, r => r);
            var pool = rows.ToDictionary(r => r.SkuId, r => r.OnHand + r.Inbound);   // cases promised to nobody yet
            var palletsToBuy = new Dictionary<string, int>();
            int left = budget;

            var open = orders.ActiveOrders
                .Where(o => o != null && !o.ReturnsMode &&
                            (o.Status == OrderData.OrderStatus.Pending || o.Status == OrderData.OrderStatus.PartiallyPicked || o.Status == OrderData.OrderStatus.Backorder))
                .OrderBy(o => o.DueDay).ThenBy(o => o.CreatedDayNumber).ToList();

            foreach (var o in open)
            {
                var take = new Dictionary<string, int>();      // cases this order takes from the pool
                var buy = new Dictionary<string, int>();       // pallets this order needs bought
                int cost = 0; bool buyable = true;
                foreach (var line in o.LineItems)
                {
                    int need = line.QuantityRemaining;
                    if (need <= 0) continue;
                    if (!bySku.TryGetValue(line.SkuId, out var r)) { buyable = false; break; }
                    int have = Mathf.Max(0, pool.TryGetValue(line.SkuId, out var p) ? p : 0) - (take.TryGetValue(line.SkuId, out var t) ? t : 0);
                    int fromPool = Mathf.Min(need, Mathf.Max(0, have));
                    take[line.SkuId] = (take.TryGetValue(line.SkuId, out var t2) ? t2 : 0) + fromPool;
                    int short_ = need - fromPool;
                    if (short_ <= 0) continue;
                    if (r.CasesPerPallet <= 0 || r.VendorId == null) { buyable = false; break; }
                    int plt = Mathf.CeilToInt(short_ / (float)r.CasesPerPallet);
                    buy[line.SkuId] = (buy.TryGetValue(line.SkuId, out var b) ? b : 0) + plt;
                    cost += Mathf.RoundToInt(r.PurchasePrice) * r.CasesPerPallet * plt;
                }
                if (!buyable || cost > left) continue;           // cannot be covered completely: leave the pool alone, try the next order
                foreach (var kv in take) pool[kv.Key] = Mathf.Max(0, pool[kv.Key] - kv.Value);
                foreach (var kv in buy)
                {
                    palletsToBuy[kv.Key] = (palletsToBuy.TryGetValue(kv.Key, out var cur) ? cur : 0) + kv.Value;
                    // the pallets bought land in the pool as cases and are consumed by this order, so nothing is left over to double count
                }
                left -= cost;
                if (left <= 0) break;
            }

            foreach (var kv in palletsToBuy) result.Add((bySku[kv.Key], kv.Value));
            return result;
        }

        bool TryRaisePO(ShipmentService ship, SimulationTimeService time, List<(ItemAvailabilityRow row, int pallets)> want, string vendorId, string vendorName,
                        int costMultiple, ref int budget, bool rushPo, int minCases = 0)
        {
            var lines = new List<(SkuData sku, int cases)>();
            foreach (var (row, pallets) in want) lines.Add((row.Sku, row.CasesPerPallet * pallets));
            // Fit one trailer, then fit the budget.
            lines = TrailerCapacity.TrimToFit(lines);
            int Cost(List<(SkuData sku, int cases)> l) => l.Sum(x => Mathf.RoundToInt(want.First(w => w.row.SkuId == x.sku.SkuId).row.PurchasePrice) * costMultiple * x.cases);
            while (lines.Count > 0 && Cost(lines) > budget) lines.RemoveAt(lines.Count - 1);
            if (lines.Count == 0 || lines.Sum(l => l.cases) < minCases) return false;

            var plan = TrailerCapacity.Plan(lines);
            if (plan.OverCapacity) return false;
            var items = new List<ShipmentLineItem>();
            foreach (var p in plan.Pallets)
            {
                var row = want.First(w => w.row.SkuId == p.SkuId).row;
                int unit = Mathf.Max(1, Mathf.RoundToInt(row.PurchasePrice)) * costMultiple;
                items.Add(new ShipmentLineItem(p.SkuId, p.Cases, unit, row.Sku.ShelfLifeDays) { FloorSlotIndex = p.FloorSlot, PalletTier = p.Tier });
            }
            if (items.Count == 0) return false;

            var po = ship.CreatePlayerPurchaseOrder(OrderNumberGenerator.GetNext('I', 'G', time.Day), vendorId, vendorName, items, time.Day, 0);
            if (po == null) { Note("PO-FAIL", $"CreatePlayerPurchaseOrder returned null for {vendorName}"); return false; }
            budget -= po.TotalCost; _spentToday += po.TotalCost;
            Act($"{(rushPo ? "RUSH " : "")}PO {po.PONumber} {vendorName}: {items.Count} plt ${po.TotalCost:N0}");
            return true;
        }

        void Act(string s)
        {
            if (_actions.Count < 400) _actions.Add($"d{CurrentDay()} {s}");
            SystemsLogWindow.LogHal("HAL 9000: " + s);
        }

        static int CurrentDay() => ServiceLocator.TryGet<SimulationTimeService>(out var t) ? t.Day : 0;

        // ═════════════════════════════════════ bug monitor ═════════════════════════════════════

        void OnShipped(OrderData o)
        {
            if (!Running) return;
            _shipped++;
            if (o == null || !o.HasBeenFined || IsMovedSlotFine(o)) _shippedOnTime++;   // shipped and never fined for lateness
            if (o != null)
            {
                OrderLineItem worst = null;
                foreach (var li in o.LineItems) if (li.QuantityRemaining > 0 && (worst == null || li.QuantityRemaining > worst.QuantityRemaining)) worst = li;
                if (worst != null)
                    _lastShortWhy = $"{o.OrderNumber ?? o.OrderId} ({o.CustomerName}) shipped short: {o.LineItems.Sum(l => l.QuantityRemaining)} cs cut, most on {worst.SkuId} ({worst.QuantityRemaining}).";
            }
            EvaluateScore("Order " + (o != null ? (o.OrderNumber ?? o.OrderId) : "") + " shipped");
        }
        int _movedFines;
        static bool IsMovedSlotFine(OrderData o) => o != null && o.LastFineWhy != null && o.LastFineWhy.StartsWith("was moved off");

        /// <summary>On-time orders / (on-time orders + orders fined for lateness). A late order that ships afterwards is counted ONCE, as late.</summary>
        float OnTimeRatio()
        {
            int total = _shippedOnTime + _late;
            return total > 0 ? Mathf.Clamp01((float)_shippedOnTime / total) : 0f;
        }

        readonly Dictionary<string, float> _lastPop = new();
        /// <summary>Purple on-screen headline for an event HAL noticed. One per category per 30 real seconds, so a repeating bug cannot flood the screen.</summary>
        void HalPop(string category, string text)
        {
            if (!Running) return;
            float now = Time.unscaledTime;
            if (_lastPop.TryGetValue(category, out float t) && now - t < 30f) return;
            _lastPop[category] = now;
            ShippedFanfare.ShowHalEvent(text.Length > 90 ? text.Substring(0, 90) + "..." : text);
        }

        void OnFined(OrderData o, int amount)
        {
            if (!Running) return;
            if (o != null && IsMovedSlotFine(o)) { _movedFines++; EvaluateScore("Moved-slot fine, order " + (o.OrderNumber ?? o.OrderId)); return; }   // a penalty for breaking the requested hour, not a late order
            _late++;
            if (o != null) { _lastLateWhy = WhyLate(o); HalPop("LATE", $"LATE ORDER {o.OrderNumber ?? o.OrderId}: fined ${amount:N0}"); }
            EvaluateScore("Late fine, order " + (o != null ? (o.OrderNumber ?? o.OrderId) : ""));
        }

        // ═════════════════════════════════════ exit criteria (the HAL window's SUMMARY tab) ═════════════════════════════════════
        // Step 1 of the roadmap closes when StreakGoal blocks in a row each pass ALL of: on-time >= 80%, no door deadlock,
        // no REPEATING bug, profit > 0 (cash + stock). Everything else on the tab is shown because it explains those four.

        public const int StreakGoal = 5;
        public const float OnTimeGoal = 0.80f, FillGoal = 0.90f;
        public enum GoalState { Pending, Met, Missed }
        public class GoalRow { public string name, goal, actual, why, explain; public GoalState state; public bool exit; public bool scored; public float points, maxPoints; }

        string _lastLateWhy, _lastShortWhy, _lastDeadlockWhy, _lastRepeatWhy;
        readonly Dictionary<string, long> _unclaimedSince = new();
        int _streakBlock = -1, _streakCache;

        static string WhyLate(OrderData o)
        {
            string s;
            switch (o.Status)
            {
                case OrderData.OrderStatus.Pending when !o.ReleasedToFloor: s = "was never released to the floor"; break;
                case OrderData.OrderStatus.Pending when o.IsWaitingForLane: s = "sat waiting for a free staging lane"; break;
                case OrderData.OrderStatus.Pending:
                case OrderData.OrderStatus.PartiallyPicked:
                case OrderData.OrderStatus.FullyPicked: s = $"was still being picked ({o.TotalUnitsPicked}/{o.TotalUnits} cs)"; break;
                case OrderData.OrderStatus.Staged: s = $"was staged but no trailer loaded it (door {o.AssignedDoorNumber})"; break;
                case OrderData.OrderStatus.Loading: s = "was still loading when it came due"; break;
                case OrderData.OrderStatus.Loaded: s = "was loaded but the truck had not left"; break;
                default: s = "came due unfinished"; break;
            }
            return $"{o.OrderNumber ?? o.OrderId} ({o.CustomerName}) {s}.";
        }

        /// <summary>A staged/loading order whose outbound truck has sat docked and unclaimed for 90 game minutes: the block-5 deadlock (staged product, empty truck at the door, no Load task).</summary>
        void CheckDoorDeadlocks()
        {
            if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null || !ServiceLocator.TryGet<SimulationTimeService>(out var time)) return;
            long now = Key(time);
            var trucks = FindObjectsByType<TruckController>();
            var seen = new HashSet<string>();
            foreach (var o in orders.ActiveOrders)
            {
                if (o == null || (o.Status != OrderData.OrderStatus.Staged && o.Status != OrderData.OrderStatus.Loading) || o.AssignedDoorNumber <= 0) continue;
                var tr = trucks.FirstOrDefault(t => t != null && t.IsOutbound && t.AwaitingLoad && t.OutboundDoorNumber == o.AssignedDoorNumber);
                if (tr == null) continue;
                seen.Add(o.OrderId);
                if (!_unclaimedSince.TryGetValue(o.OrderId, out long since)) { _unclaimedSince[o.OrderId] = now; continue; }
                if (now - since < 90) continue;
                _lastDeadlockWhy = $"{o.OrderNumber ?? o.OrderId} is {o.Status} but door {o.AssignedDoorNumber}'s truck has waited {now - since} min with no one loading it.";
                Note("DOOR-DEADLOCK", _lastDeadlockWhy);
            }
            foreach (var k in _unclaimedSince.Keys.Where(k => !seen.Contains(k)).ToList()) _unclaimedSince.Remove(k);
        }

        static bool BlockPassed(PlaytestBlockResult r) =>
            r != null && r.ordersShipped > 0 && r.onTime >= OnTimeGoal * 100f
            && !r.bugKeys.Any(k => k.StartsWith("DOOR-DEADLOCK") || k.StartsWith("REPEATING"))
            && (r.cashEnd + r.inventoryEnd) - (r.cashStart + r.inventoryStart) > 0;

        /// <summary>Consecutive passing blocks, newest first, in this series (HAL's blocks, or the player's).</summary>
        public int PassStreak()
        {
            if (_streakBlock != _blockNo)
            {
                var list = ObserveOnly ? LoadPlayerHistory() : LoadHalHistory();
                int n = 0;
                for (int i = list.Count - 1; i >= 0 && BlockPassed(list[i]); i--) n++;
                _streakCache = n; _streakBlock = _blockNo;
            }
            return _streakCache;
        }

        string BiggestQueue()
        {
            if (!ServiceLocator.TryGet<WorkQueueSystem>(out var wq) || wq == null) return null;
            var g = wq.Tasks.Where(t => t != null && t.Status == WorkTaskStatus.Available && t.Type != WorkTaskType.ChargeMHE)
                            .GroupBy(t => t.Type).OrderByDescending(x => x.Count()).FirstOrDefault();
            return g == null ? null : $"{g.Count()} {g.Key} task(s) waiting";
        }

        /// <summary>"Biggest offending bug is VEHICLE-STATIONARY - ... with 4 incidents." Ranked by the same weight x count the score uses.</summary>
        string BiggestOffenderSentence()
        {
            var top = _incidents.OrderByDescending(k => WeightOf(k.Key) * k.Value).FirstOrDefault();
            if (top.Key == null) return "No bugs logged this block.";
            string[] p = top.Key.Split('|');
            string detail = p.Length > 1 ? p[1].Trim() : "";
            if (detail.Length > 80) detail = detail.Substring(0, 80) + "...";
            string what = detail.Length > 0 ? p[0] + " - " + detail : p[0];
            return $"Biggest offending bug is {what} with {top.Value} incident{(top.Value == 1 ? "" : "s")}.";
        }

        string TopBug()
        {
            var top = _incidents.OrderByDescending(k => WeightOf(k.Key) * k.Value).FirstOrDefault();
            if (top.Key == null) return null;
            string[] p = top.Key.Split('|');
            string detail = p.Length > 1 ? p[1].Trim() : "";
            if (detail.Length > 70) detail = detail.Substring(0, 70) + "...";
            return $"{p[0]} x{top.Value}: {detail}";
        }

        // ── high score ──
        float _highCache = -1f; string _highWho = ""; int _highCacheBlocks = -1; float _highCacheAt = -100f;

        /// <summary>The best block score on record, who earned it ("HAL" or "YOU"), and which block. The block still running counts too
        /// once it passes every finished one. Read from the two scoreboards at most every few seconds.</summary>
        public void GetHighScore(out float score, out string who)
        {
            if (Time.unscaledTime - _highCacheAt > 5f)
            {
                _highCacheAt = Time.unscaledTime;
                _highCache = -1f; _highWho = "";
                foreach (var b in LoadHalHistory()) if (b != null && b.score > _highCache) { _highCache = b.score; _highWho = "HAL"; }
                foreach (var b in LoadPlayerHistory()) if (b != null && b.score > _highCache) { _highCache = b.score; _highWho = "YOU"; }
            }
            score = _highCache; who = _highWho;
            if (Running && LiveScore > score) { score = LiveScore; who = ObserveOnly ? "YOU" : "HAL"; }
        }

        /// <summary>The hover explanation for a summary row: what it measures and exactly when it moves.</summary>
        string ExplainRow(string name)
        {
            switch (name)
            {
                case "On-time orders":
                    return "ON-TIME ORDERS\nOn-time % = orders shipped on time / (orders shipped on time + orders fined for being late).\n\nAn order only counts as late when a fine is charged against it, and each order is fined at most once:\n1. A recurring order's 4-hour dock block ends with the freight still in the building. The fine fires the moment the block ends.\n2. A bulk order passes its due day. The fine fires at midnight.\n\nA late order that ships afterwards still counts once, as late (it is not counted twice)." +
                           $"Moving a trailer off the hour the customer asked for also costs a fine, but that is a penalty for breaking the promise, not lateness, so it is not counted here ({_movedFines} so far this block).\n\n" +
                           "Fairness: an outbound truck reaches the gate 90 minutes before its block opens so it can be at the door on time. " +
                           "If everything the building has is loaded and the rest is scratched, the game pauses and asks whether to release the trailer (no lateness fine for cases that were never in stock).";
                case "Door deadlocks":
                    return "DOOR DEADLOCKS\nCounted when a staged or loading order's outbound truck has sat docked for 90 game minutes with nobody loading it. Goal: zero.";
                case "Repeating bugs":
                    return "REPEATING BUGS\nCounted when the same warning or error line repeats 25 times in a block, which means something is stuck in a loop. Goal: zero.";
                case "Profit (cash + stock)":
                    return "PROFIT\nCash plus stock on hand (valued at cost) compared with where the block started. Stock you buy counts as money in your pocket until it sells, so buying inventory is not a loss. Goal: above zero.";
                case "Fill rate":
                    return "FILL RATE\nThe average share of each shipped order's cases that actually went out. Scratched cases (not in stock) pull it down.";
                case "Pallets shipped":
                    return "PALLETS SHIPPED\nThe goal is a daily rate (" + S.targetPalletsPerDay.ToString("0") + " pallets per day, set in the HAL settings) multiplied by the days that have passed in this block.\n\n" +
                           "Example: at the start of day 3, two days have passed, so the goal reads " + S.targetPalletsPerDay.ToString("0") + " x 2 days = " + Mathf.CeilToInt(S.targetPalletsPerDay * 2f) + ".\n" +
                           "The right-hand number is pallets that have left on outbound trailers this block. One pallet under the goal shows red; at or above it shows green.\n" +
                           "The goal grows by " + (S.targetPalletsPerDay / 24f).ToString("0.##") + " pallets every game hour, so it keeps moving up during the day.\n\n" +
                           "For the first 6 hours of a block the row stays gray, because there is not enough time passed to judge.";
                case "Revenue":
                    return "REVENUE\nThe goal is a daily rate ($" + S.targetRevenuePerDay.ToString("N0") + " per day, set in the HAL settings) multiplied by the days that have passed in this block.\n\n" +
                           "Example: at the start of day 3, two days have passed, so the goal reads $" + S.targetRevenuePerDay.ToString("N0") + " x 2 days = $" + (S.targetRevenuePerDay * 2f).ToString("N0") + ".\n" +
                           "The right-hand number is income earned this block (goods plus handling fees). Under the goal shows red; at or above it shows green.\n" +
                           "The goal grows continuously through the day.\n\n" +
                           "For the first 6 hours of a block the row stays gray, because there is not enough time passed to judge.";
                case "Cash health":
                    return "CASH HEALTH\nChecked through the block: it falls whenever your cash is under the safety floor. Goal: never below the floor.";
                case "Reliability":
                    return "RELIABILITY\nStarts at 100 and drops with every bug incident the bot logs. The same bug repeating costs more each time it comes back.\n" + BiggestOffenderSentence();
                case "Reputation":
                    return "REPUTATION\nYour standing with customers and vendors compared with the start of the block. Late orders and cancellations lower it; clean on-time deliveries raise it.";
                default: return null;
            }
        }

        /// <summary>Goal vs actual for every part of the score, plus a one-line reason for each miss.</summary>
        public List<GoalRow> GetGoalRows()
        {
            var rows = new List<GoalRow>();
            if (S == null || !ServiceLocator.TryGet<SimulationTimeService>(out var time) || !ServiceLocator.TryGet<MoneyService>(out var money)) return rows;
            ServiceLocator.TryGet<FulfillmentStatsService>(out var stats);
            ServiceLocator.TryGet<OrderService>(out var orders);
            float elapsed = Mathf.Max(0.25f, (Key(time) - _startKey) / 1440f);
            int done = _shippedOnTime + _late;

            // 1. on-time
            float onT = OnTimeRatio();
            rows.Add(new GoalRow
            {
                exit = true, name = "On-time orders", goal = ">= 80%",
                actual = done > 0 ? $"{onT * 100f:0}%  ({_shippedOnTime} on time, {_late} late)" : "no orders finished yet",
                state = done == 0 ? GoalState.Pending : onT >= OnTimeGoal ? GoalState.Met : GoalState.Missed,
                why = _late == 0 ? "Nothing late so far." : "Last late: " + (_lastLateWhy ?? "unknown")
            });

            // 2. deadlocks
            int dead = _incidents.Where(k => k.Key.StartsWith("DOOR-DEADLOCK")).Sum(k => k.Value);
            rows.Add(new GoalRow
            {
                exit = true, name = "Door deadlocks", goal = "0", actual = dead.ToString(),
                state = dead == 0 ? GoalState.Met : GoalState.Missed,
                why = dead == 0 ? "No door held by an unloaded truck." : _lastDeadlockWhy
            });

            // 3. repeating bugs
            int rep = _incidents.Count(k => k.Key.StartsWith("REPEATING"));
            rows.Add(new GoalRow
            {
                exit = true, name = "Repeating bugs", goal = "0", actual = rep.ToString(),
                state = rep == 0 ? GoalState.Met : GoalState.Missed,
                why = rep == 0 ? "No log line has looped 25 times." : (_lastRepeatWhy ?? "unknown")
            });

            // 4. profit
            int net = (money.CurrentCapital + InventoryValueNow()) - (_startCash + _startInventory);
            rows.Add(new GoalRow
            {
                exit = true, name = "Profit (cash + stock)", goal = "> $0", actual = (net >= 0 ? "+$" : "-$") + Mathf.Abs(net).ToString("N0"),
                state = net > 0 ? GoalState.Met : GoalState.Missed,
                why = net > 0 ? "Ahead of where the block started."
                              : $"Cash ${money.CurrentCapital:N0} + stock ${InventoryValueNow():N0} is below the start; revenue so far ${money.LifetimeIncome.Values.Sum() - _startIncome:N0}."
            });

            // supporting parts of the score
            float fill = _shipped > 0 && stats != null ? Mathf.Clamp01(stats.AvgFillRate) : 0f;
            rows.Add(new GoalRow
            {
                name = "Fill rate", goal = ">= 90%", actual = _shipped > 0 ? $"{fill * 100f:0}%" : "n/a",
                state = _shipped == 0 ? GoalState.Pending : fill >= FillGoal ? GoalState.Met : GoalState.Missed,
                why = fill >= FillGoal || _shipped == 0 ? "Orders are shipping complete." : (_lastShortWhy ?? "unknown")
            });

            int pallets = PalletsShippedTotal(0, time.Day + 1) - _palletsAtStart;
            // The goal is a RATE (settings: targetPalletsPerDay). What is shown is that rate and the pace it implies for the time that has
            // really passed in the block; for the first quarter-day there is nothing meaningful to judge, so it stays pending.
            float daysIn = Mathf.Max(0f, (Key(time) - _startKey) / 1440f);
            // Whole pallets, rounded UP, and the same number is both shown and compared: 12/day x 2 days = 24, so 23 is red and 24 is green.
            int palTarget = Mathf.CeilToInt(S.targetPalletsPerDay * daysIn - 0.001f);
            string daysText = daysIn.ToString("0.#") + (Mathf.Approximately(daysIn, 1f) ? " day" : " days");
            rows.Add(new GoalRow
            {
                name = "Pallets shipped", goal = $"{S.targetPalletsPerDay:0} x {daysText} = {palTarget}",
                actual = pallets.ToString(),
                state = daysIn < 0.25f ? GoalState.Pending : pallets >= palTarget ? GoalState.Met : GoalState.Missed,
                why = daysIn < 0.25f ? "Too early in the block to judge." : pallets >= palTarget ? "At or above the goal." : $"{palTarget - pallets} short. Biggest queue: " + (BiggestQueue() ?? "none waiting")
            });

            int revenue = money.LifetimeIncome.Values.Sum() - _startIncome;
            int revTarget = Mathf.CeilToInt(S.targetRevenuePerDay * daysIn - 0.001f);
            int open = orders != null ? orders.ActiveOrders.Count : 0;
            int overdue = orders != null ? orders.ActiveOrders.Count(x => x != null && x.IsOverdue(time.Day)) : 0;
            rows.Add(new GoalRow
            {
                name = "Revenue", goal = $"${S.targetRevenuePerDay:N0} x {daysText} = ${revTarget:N0}",
                actual = $"${revenue:N0}",
                state = daysIn < 0.25f ? GoalState.Pending : revenue >= revTarget ? GoalState.Met : GoalState.Missed,
                why = daysIn < 0.25f ? "Too early in the block to judge." : revenue >= revTarget ? "At or above the goal." : $"${revTarget - revenue:N0} short. {open} order(s) open, {overdue} overdue."
            });

            rows.Add(new GoalRow
            {
                name = "Cash health", goal = $"never under ${S.minCashBalance:N0}", actual = "$" + money.CurrentCapital.ToString("N0"),
                state = _cashDipHours == 0 ? GoalState.Met : GoalState.Missed,
                why = _cashDipHours == 0 ? "Cash has stayed above the floor." : $"Cash was under the floor for {_cashDipHours} of {_cashSamples} checks."
            });

            var comps = new float[8]; ScoreNow(comps);
            rows.Add(new GoalRow
            {
                name = "Reliability", goal = ">= 80", actual = $"{comps[6] * 100f:0}  ({IncidentCount} incidents)",
                state = comps[6] >= 0.8f ? GoalState.Met : GoalState.Missed,
                why = comps[6] >= 0.8f ? "Few bugs." : "Worst: " + (TopBug() ?? "unknown")
            });

            int repNow = ServiceLocator.TryGet<ReputationService>(out var rs) ? rs.Score : _startRep;
            rows.Add(new GoalRow
            {
                name = "Reputation", goal = "rising", actual = $"{(repNow >= _startRep ? "+" : "")}{repNow - _startRep}  ({_startRep} to {repNow})",
                state = repNow >= _startRep ? GoalState.Met : GoalState.Missed,
                why = repNow >= _startRep ? "Holding or climbing." : "Late and cancelled orders cost reputation."
            });

            // Where the score comes from: each row that is a part of the 0-100 score earns (weight x how well it is doing) of the total.
            float[] w = { S.wOnTime, S.wFill, S.wThroughput, S.wRevenue, S.wProfit, S.wCash, S.wReliability, S.wReputation };
            float wSum = Mathf.Max(1, S.TotalWeight);
            var compIndex = new Dictionary<string, int>
            {
                ["On-time orders"] = 0, ["Fill rate"] = 1, ["Pallets shipped"] = 2, ["Revenue"] = 3,
                ["Profit (cash + stock)"] = 4, ["Cash health"] = 5, ["Reliability"] = 6, ["Reputation"] = 7,
            };
            foreach (var r in rows)
            {
                r.explain = ExplainRow(r.name);
                if (!compIndex.TryGetValue(r.name, out int ci)) continue;
                r.scored = true;
                r.maxPoints = 100f * w[ci] / wSum;
                r.points = 100f * w[ci] * comps[ci] / wSum;
            }
            return rows;
        }

        void OnLog(string msg, string stack, LogType type)
        {
            if (!Running) return;
            // Unity logs this at every NavMesh rebuild for meshes that are not marked readable. It does nothing in play and is not a bug.
            if (type == LogType.Error && msg.Contains("RuntimeNavMeshBuilder: Source mesh")) return;
            // The same line over and over is a bug by itself (a stuck loop), even when each copy is only a warning or an info line.
            if (type != LogType.Log)
            {
                string rk = Digits.Replace(msg.Split((char)10)[0], "#");
                if (rk.Length > 140) rk = rk.Substring(0, 140);
                _repeatCounts.TryGetValue(rk, out int rc);
                _repeatCounts[rk] = ++rc;
                if (rc == 25) { _lastRepeatWhy = msg.Split((char)10)[0]; Note("REPEATING", "x25 and counting: " + _lastRepeatWhy); }
            }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors++;
                string first = msg.Split('\n')[0];
                Note(type == LogType.Exception ? "EXCEPTION" : "ERROR", first, stack);
            }
            else if (type == LogType.Warning && WatchWarnings.Any(w => msg.Contains(w)))
                Note("WARN", msg.Split('\n')[0]);
        }

        // Called from any thread-agnostic context; incidents are keyed by category + normalised text so a bug counts once
        // per episode (while it keeps repeating inside one health-check window it is the same incident).
        readonly Dictionary<string, long> _lastLogSeen = new();
        // Every incident goes straight to disk as it happens (full, un-normalised text with the real pallet / door / slot numbers),
        // so a crash or a stopped run loses nothing and the whole block can be worked through afterwards.
        static readonly string NL = System.Environment.NewLine;
        int _bugLines;
        void WriteBug(string category, string text, string stack = null)
        {
            if (_bugLines >= 3000) return;
            try
            {
                string f = Path.Combine(Folder(), $"BugLog_Block_{_blockNo:00}.md");
                if (!File.Exists(f)) File.WriteAllText(f, $"# Bug log - block {_blockNo}" + NL + NL + "Every incident as it happened. Format: day hour:minute | category | what." + NL + NL);
                string when = ServiceLocator.TryGet<SimulationTimeService>(out var t) ? $"day {t.Day} {t.Hour:00}:{t.Minute:00}" : "?";
                var sb = new StringBuilder();
                sb.Append($"- {when} | **{category}** | {text.Replace(NL, " ")}").Append(NL);
                if (!string.IsNullOrEmpty(stack))
                {
                    sb.Append("  ```").Append(NL);
                    foreach (var line in stack.Split((char)10).Select(x => x.Trim()).Where(x => x.Length > 0).Take(8)) sb.Append("  ").Append(line).Append(NL);
                    sb.Append("  ```").Append(NL);
                }
                File.AppendAllText(f, sb.ToString());
                _bugLines++;
            }
            catch { }
        }

        void Note(string category, string text, string stack = null)
        {
            string key = category + "|" + Digits.Replace(text, "#");
            long nowKey = ServiceLocator.TryGet<SimulationTimeService>(out var t) ? Key(t) : 0;
            // log-sourced bugs: a new episode begins after 60 game minutes of silence
            if (_lastLogSeen.TryGetValue(key, out long last) && nowKey - last < 60) { _lastLogSeen[key] = nowKey; return; }
            _lastLogSeen[key] = nowKey;
            _incidents[key] = _incidents.TryGetValue(key, out int n) ? n + 1 : 1;
            WriteBug(category, text, stack);
            if (category != "WARN") HalPop(category, $"BUG: {category}  {text}");
            EvaluateScore("Bug: " + category);
            if (!_firstSeen.ContainsKey(key))
            {
                _firstSeen[key] = $"day {CurrentDay()}";
                _bugLog.AppendLine($"- **{category}** (day {CurrentDay()}): {text}");
                if (!string.IsNullOrEmpty(stack)) _bugLog.AppendLine("  ```\n  " + string.Join("\n  ", stack.Split('\n').Take(5)) + "\n  ```");
            }
        }

        void HealthCheck()
        {
            // 1) the existing integrity scan
            string report = QAHealthCheck.Run();
            var present = new HashSet<string>();
            string newTag = null;
            foreach (var raw in report.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string tag = line.Split(' ')[0];
                if (!Weight.ContainsKey(tag)) continue;
                string key = tag + "|" + Digits.Replace(line.Substring(tag.Length), "#");
                present.Add(key);
                if (_activeBugKeys.Contains(key)) continue;           // still the same episode
                newTag ??= tag;
                _incidents[key] = _incidents.TryGetValue(key, out int n) ? n + 1 : 1;
                WriteBug(tag, line);
                HalPop(tag, $"BUG: {line}");
                if (!_firstSeen.ContainsKey(key))
                {
                    _firstSeen[key] = $"day {CurrentDay()}";
                    _bugLog.AppendLine($"- **{tag}** (day {CurrentDay()}): {line}");
                }
            }
            _activeBugKeys.Clear(); foreach (var k in present) _activeBugKeys.Add(k);
            if (newTag != null) EvaluateScore("Bug: " + newTag);
            CheckDoorDeadlocks();

            // 2) ghost rack slots / records
            string ghosts = QAHealthCheck.Ghosts();
            if (!ghosts.StartsWith("no ghosts"))
                foreach (var l in ghosts.Split('\n').Where(x => x.Trim().Length > 0).Take(8)) Note("GHOST-SLOT", l.Trim());

            // 3) vehicles that have a driver, are not moving, while work is waiting
            if (ServiceLocator.TryGet<WorkQueueSystem>(out var wq))
            {
                int waiting = wq.Tasks.Count(t => t.Status == WorkTaskStatus.Available && (t.Type == WorkTaskType.Putaway || t.Type == WorkTaskType.PalletPick || t.Type == WorkTaskType.Load || t.Type == WorkTaskType.Replenish));
                foreach (var slot in FindObjectsByType<MHEOperatorSlot>())
                {
                    if (slot == null || !slot.IsOccupied) continue;
                    var p = slot.transform.position;
                    if (_vehicleTrack.TryGetValue(slot, out var tr) && Vector3.Distance(tr.pos, p) < 0.3f)
                    {
                        if (waiting > 0 && Time.time - tr.movedAt > S.stationarySeconds)
                        { Note("VEHICLE-STATIONARY", $"{slot.name} has a driver, hasn't moved in {(Time.time - tr.movedAt):F0}s while {waiting} task(s) wait"); _vehicleTrack[slot] = (p, Time.time); }
                    }
                    else _vehicleTrack[slot] = (p, Time.time);
                }
            }

            HalMetrics.Sample();

            // 4) finance sample
            if (ServiceLocator.TryGet<MoneyService>(out var money))
            { _cashSamples++; if (money.CurrentCapital < S.minCashBalance) _cashDipHours++; }
            EvaluateScore(null);
        }

        // ═════════════════════════════════════ scoring + reports ═════════════════════════════════════

        static int PalletsShippedTotal(int fromDay, int toDayExclusive)
        {
            if (!ServiceLocator.TryGet<FulfillmentStatsService>(out var f)) return 0;
            int sum = 0;
            for (int d = fromDay; d < toDayExclusive; d++) sum += f.PalletsShippedOn(d);
            return sum;
        }

        string Folder()
        {
            string root = S.ResolveReportDir();
            Directory.CreateDirectory(root);
            return root;
        }

        string ScoreFile() => Path.Combine(Folder(), "scoreboard.json");

        void LoadHistory()
        {
            _history.Clear();
            try
            {
                string f = ScoreFile();
                if (File.Exists(f)) _history.AddRange(JsonUtility.FromJson<PlaytestScoreboard>(File.ReadAllText(f)).blocks);
            }
            catch (Exception e) { Debug.LogWarning("[PlaytestBot] Couldn't read scoreboard: " + e.Message); }
        }

        void FinishBlock()
        {
            Running = false;
            HealthCheck();   // one last sample so an open bug still counts
            ServiceLocator.TryGet<MoneyService>(out var money);
            ServiceLocator.TryGet<SimulationTimeService>(out var time);
            ServiceLocator.TryGet<FulfillmentStatsService>(out var stats);
            int endDay = time != null ? time.Day : _startDay + S.daysPerBlock;
            int cashEnd = money != null ? money.CurrentCapital : 0;

            int pallets = PalletsShippedTotal(0, endDay) - _palletsAtStart;
            int days = Mathf.Max(1, endDay - _startDay);

            // components, each 0..1
            float onTime = OnTimeRatio();
            float fill = _shipped > 0 && stats != null ? Mathf.Clamp01(stats.AvgFillRate) : 0f;
            float throughput = Mathf.Clamp01(pallets / Mathf.Max(1f, S.targetPalletsPerDay * days));
            int revenueTotal = (money != null ? money.LifetimeIncome.Values.Sum() : 0) - _startIncome;
            float revenue = Mathf.Clamp01(revenueTotal / Mathf.Max(1f, S.targetRevenuePerDay * days));
            int inventoryEnd = InventoryValueNow();
            int net = (cashEnd + inventoryEnd) - (_startCash + _startInventory);   // stock counts as money in the pocket
            float profit = Mathf.Clamp01(0.5f + 0.5f * net / Mathf.Max(1f, S.targetProfitPerDay * days));
            float cash = _cashSamples > 0 ? 1f - (float)_cashDipHours / _cashSamples : 1f;
            float penalty = _incidents.Sum(kv => WeightOf(kv.Key) * (1f + Mathf.Log(kv.Value, 2f)));
            float reliability = 1f / (1f + penalty / Mathf.Max(1f, S.reliabilityTolerance));
            int repEnd = ServiceLocator.TryGet<ReputationService>(out var rs1) ? rs1.Score : _startRep;
            float reputation = Mathf.Clamp01(0.5f + 0.5f * (repEnd - _startRep) / Mathf.Max(1f, S.reputationGainPerBlock));

            float wSum = Mathf.Max(1, S.TotalWeight);   // normalised, so a total that isn't exactly 100 still gives a 0-100 score
            float total = 100f * (S.wOnTime * onTime + S.wFill * fill + S.wThroughput * throughput + S.wRevenue * revenue + S.wProfit * profit
                                  + S.wCash * cash + S.wReliability * reliability + S.wReputation * reputation) / wSum;

            var res = new PlaytestBlockResult
            {
                block = _blockNo, startDay = _startDay, endDay = endDay, score = total,
                onTime = onTime * 100f, fill = fill * 100f, throughput = throughput * 100f, revenue = revenue * 100f, profit = profit * 100f,
                cash = cash * 100f, reliability = reliability * 100f, reputation = reputation * 100f,
                revenueTotal = revenueTotal, repStart = _startRep, repEnd = repEnd,
                utilReceiver = HalMetrics.Avg(HalMetrics.UtilReceiver), utilReach = HalMetrics.Avg(HalMetrics.UtilReach),
                utilDock = HalMetrics.Avg(HalMetrics.UtilDock), utilSelector = HalMetrics.Avg(HalMetrics.UtilSelector),
                headReceiver = HalMetrics.Headcount(EmployeeRole.Receiver), headReach = HalMetrics.Headcount(EmployeeRole.ReachTruckOperator),
                headDock = HalMetrics.Headcount(EmployeeRole.DockStockerOperator), headSelector = HalMetrics.Headcount(EmployeeRole.OrderSelector),
                doors = PlacedObjectRegistry.All.Count(p => p != null && p.data != null && p.data.objName == "ShippingDoor"),
                machines = FindObjectsByType<MHEOperatorSlot>().Length,
                backlogReceive = HalMetrics.Avg(HalMetrics.BacklogReceive), backlogPutaway = HalMetrics.Avg(HalMetrics.BacklogPutaway),
                backlogPick = HalMetrics.Avg(HalMetrics.BacklogPick), backlogLoad = HalMetrics.Avg(HalMetrics.BacklogLoad),
                backlogOrders = HalMetrics.Avg(HalMetrics.BacklogOrders), waitingTrucks = HalMetrics.Avg(HalMetrics.WaitingTrucks),
                ordersShipped = _shipped, ordersLate = _late, palletsShipped = pallets, bugIncidents = _incidents.Values.Sum(), errors = _errors,
                cashStart = _startCash, cashEnd = cashEnd, inventoryStart = _startInventory, inventoryEnd = inventoryEnd,
            };
            foreach (var kv in _incidents.OrderByDescending(k => WeightOf(k.Key) * k.Value)) { res.bugKeys.Add(kv.Key); res.bugCounts.Add(kv.Value); }

            if (ObserveOnly)
            {
                // A human's block: its own scoreboard and report, nothing added to HAL's trend, no planner, no restart.
                try
                {
                    var mine = LoadPlayerHistory(); mine.Add(res);
                    File.WriteAllText(PlayerScoreFile(), JsonUtility.ToJson(new PlaytestScoreboard { blocks = mine }, true));
                    File.WriteAllText(Path.Combine(Folder(), $"Block_{_blockNo:00}.md"), BuildReport(res, null));
                }
                catch (Exception e) { Debug.LogError("[PlaytestBot] Couldn't write the player report: " + e.Message); }
                SystemsLogWindow.LogHal($"HAL 9000: your block is complete. Score {total:F1}.");
                Debug.LogWarning($"[PlaytestBot] PLAYER BLOCK {_blockNo} DONE: score {total:F1}. Report: {Path.Combine(S.ReportDir, $"Block_{_blockNo:00}.md")}");
                _status = $"your block done: {total:F1}";
                Time.timeScale = 0f;                       // hold the moment while the results come up
                BlockFinished?.Invoke(res, true);
                return;
            }

            var prev = _history.LastOrDefault();
            _history.Add(res);
            HalPlanner.RecordOutcome(S, res);
            try
            {
                // Merge with what is already on disk so a short in-memory history can never erase recorded blocks.
                var onDisk = new List<PlaytestBlockResult>();
                try { if (File.Exists(ScoreFile())) onDisk.AddRange(JsonUtility.FromJson<PlaytestScoreboard>(File.ReadAllText(ScoreFile())).blocks); } catch { }
                var merged = onDisk.Where(d => _history.All(h => h.block != d.block)).Concat(_history).OrderBy(b => b.block).ToList();
                File.WriteAllText(ScoreFile(), JsonUtility.ToJson(new PlaytestScoreboard { blocks = merged }, true));
                File.WriteAllText(Path.Combine(Folder(), $"Block_{_blockNo:00}.md"), BuildReport(res, prev));
                File.WriteAllText(Path.Combine(Folder(), "Scoreboard.md"), BuildScoreboard());
            }
            catch (Exception e) { Debug.LogError("[PlaytestBot] Couldn't write report: " + e.Message); }

            if (S.saveAtBlockEnd)
            {
                var ps = FindAnyObjectByType<PlacementSystem>();
                if (ps != null) { try { ps.SaveGame($"bot_block_{_blockNo}"); } catch (Exception e) { Debug.LogWarning("[PlaytestBot] save failed: " + e.Message); } }
            }

            SystemsLogWindow.LogHal($"HAL 9000: block {_blockNo} complete. Score {total:F1}.");
            string delta = prev != null ? $" ({total - prev.score:+0.0;-0.0;0.0} vs block {prev.block})" : "";
            Debug.LogWarning($"[PlaytestBot] BLOCK {_blockNo} DONE: score {total:F1}{delta}. Report: {Path.Combine(S.ReportDir, $"Block_{_blockNo:00}.md")}");
            _status = $"block {_blockNo} done: {total:F1}";
            _blockDoneAwaitingPlan = true;

            if (S.pauseAtBlockEnd) Time.timeScale = 0f;
            else StartBlock();
        }

        static float WeightOf(string key)
        {
            string cat = key.Split('|')[0];
            return Weight.TryGetValue(cat, out float w) ? w : 1f;
        }

        string BuildReport(PlaytestBlockResult r, PlaytestBlockResult prev)
        {
            string D(float cur, float? old) => old.HasValue ? $"{cur:F1} ({cur - old.Value:+0.0;-0.0;0.0})" : $"{cur:F1}";
            var sb = new StringBuilder();
            sb.AppendLine($"# Playtest block {r.block} — days {r.startDay}–{r.endDay}");
            sb.AppendLine();
            sb.AppendLine($"## Score: **{D(r.score, prev?.score)}** / 100");
            sb.AppendLine();
            sb.AppendLine("| Component | Weight | This block |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine($"| On-time delivery ({r.ordersShipped} shipped, {r.ordersLate} late) | {S.wOnTime}% | {D(r.onTime, prev?.onTime)} |");
            sb.AppendLine($"| Fill rate | {S.wFill}% | {D(r.fill, prev?.fill)} |");
            sb.AppendLine($"| Throughput ({r.palletsShipped} plt vs target {S.targetPalletsPerDay * Mathf.Max(1, r.endDay - r.startDay):F0}) | {S.wThroughput}% | {D(r.throughput, prev?.throughput)} |");
            sb.AppendLine($"| Revenue (${r.revenueTotal:N0}) | {S.wRevenue}% | {D(r.revenue, prev?.revenue)} |");
            sb.AppendLine($"| Profit (cash + stock {(r.cashEnd + r.inventoryEnd) - (r.cashStart + r.inventoryStart):+#,0;-#,0;0}) | {S.wProfit}% | {D(r.profit, prev?.profit)} |");
            sb.AppendLine($"| Cash health (floor ${S.minCashBalance:N0}) | {S.wCash}% | {D(r.cash, prev?.cash)} |");
            sb.AppendLine($"| Reliability (bugs) | {S.wReliability}% | {D(r.reliability, prev?.reliability)} |");
            sb.AppendLine($"| Reputation ({r.repStart} to {r.repEnd}) | {S.wReputation}% | {D(r.reputation, prev?.reputation)} |");
            sb.AppendLine();
            sb.AppendLine($"Orders shipped **{r.ordersShipped}**, late/fined **{r.ordersLate}**, pallets shipped **{r.palletsShipped}**. Cash ${r.cashStart:N0} → ${r.cashEnd:N0} ({r.cashEnd - r.cashStart:+#,0;-#,0;0}). Stock at cost ${r.inventoryStart:N0} → ${r.inventoryEnd:N0}; cash + stock {(r.cashEnd + r.inventoryEnd) - (r.cashStart + r.inventoryStart):+#,0;-#,0;0}.");
            sb.AppendLine($"Bot did: {_signed} contracts signed, {_booked} dock bookings, {_released} orders released, {_purchases} POs, {_rushes} rush POs.");
            sb.AppendLine();

            if (prev != null)
            {
                var gone = prev.bugKeys.Except(r.bugKeys).ToList();
                var fresh = r.bugKeys.Except(prev.bugKeys).ToList();
                sb.AppendLine("## Change since last block");
                sb.AppendLine($"- Fixed / no longer seen: **{gone.Count}**" + (gone.Count > 0 ? "\n" + string.Join("\n", gone.Take(15).Select(g => "  - " + g)) : ""));
                sb.AppendLine($"- New this block: **{fresh.Count}**" + (fresh.Count > 0 ? "\n" + string.Join("\n", fresh.Take(15).Select(g => "  - " + g)) : ""));
                sb.AppendLine();
            }

            sb.AppendLine($"## Bugs found ({r.bugKeys.Count} distinct, {r.bugIncidents} incidents)");
            sb.AppendLine();
            sb.AppendLine("| Incidents | Bug |");
            sb.AppendLine("|---|---|");
            for (int i = 0; i < r.bugKeys.Count; i++) sb.AppendLine($"| {r.bugCounts[i]} | {r.bugKeys[i].Replace("|", " — ")} |");
            sb.AppendLine();
            sb.AppendLine("## First sightings (with stack traces)");
            sb.AppendLine();
            sb.Append(_bugLog.Length > 0 ? _bugLog.ToString() : "_none_\n");
            sb.AppendLine();
            sb.AppendLine("## Bot actions (first 400)");
            foreach (var a in _actions) sb.AppendLine("- " + a);
            return sb.ToString();
        }

        string BuildScoreboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Playtest scoreboard");
            sb.AppendLine();
            sb.AppendLine("| Block | Days | Score | Δ | On-time | Fill | Thru | Rev | Profit | Cash | Reliab | Rep | Bugs (distinct/incidents) | Shipped / late |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            PlaytestBlockResult p = null;
            foreach (var r in _history)
            {
                string d = p == null ? "—" : $"{r.score - p.score:+0.0;-0.0;0.0}";
                sb.AppendLine($"| {r.block} | {r.startDay}–{r.endDay} | **{r.score:F1}** | {d} | {r.onTime:F0} | {r.fill:F0} | {r.throughput:F0} | {r.revenue:F0} | {r.profit:F0} | {r.cash:F0} | {r.reliability:F0} | {r.reputation:F0} | {r.bugKeys.Count}/{r.bugIncidents} | {r.ordersShipped} / {r.ordersLate} |");
                p = r;
            }
            return sb.ToString();
        }

        void OnGUI()
        {
            if (S == null) return;
            GUI.Label(new Rect(8, Screen.height - 24, 600, 20), $"[PlaytestBot] {_status}  day {CurrentDay()}  incidents {_incidents.Values.Sum()}  errors {_errors}");
        }
    }

#if UNITY_EDITOR
    static class PlaytestBotMenu
    {
        const string AssetPath = "Assets/_Project/Resources/PlaytestBotSettings.asset";

        [UnityEditor.MenuItem("Tools/Playtest Bot/Select Settings")]
        static void SelectSettings()
        {
            var s = UnityEditor.AssetDatabase.LoadAssetAtPath<PlaytestBotSettings>(AssetPath);
            if (s == null)
            {
                s = ScriptableObject.CreateInstance<PlaytestBotSettings>();
                UnityEditor.AssetDatabase.CreateAsset(s, AssetPath);
                UnityEditor.AssetDatabase.SaveAssets();
            }
            UnityEditor.Selection.activeObject = s;
            UnityEditor.EditorGUIUtility.PingObject(s);
        }

        [UnityEditor.MenuItem("Tools/Playtest Bot/Start Block")]
        static void StartBlock()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[PlaytestBot] Enter Play mode and load a save first."); return; }
            PlaytestBot.Ensure().StartBlock();
        }

        [UnityEditor.MenuItem("Tools/Playtest Bot/Stop")]
        static void Stop() { if (PlaytestBot.Instance != null) PlaytestBot.Instance.StopBot(); }
    }
#endif
}
