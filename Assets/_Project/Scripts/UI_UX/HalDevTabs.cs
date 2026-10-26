using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Diagnostics;

/// <summary>
/// The two Dev Console tabs that drive the playtest bot: HAL 9000 (run it, tune what it does) and BALANCE (the scoring
/// system as a points pool - every weight is a whole-number slider in steps of 5 and the pool must total 100).
/// Built in code like the other panels. Big type and big hit targets on purpose: this is used for hours at a time.
/// Edits write straight into PlaytestBotSettings, which the bot reads live, so a change applies on its next decision.
/// </summary>
public static class HalDevTabs
{
    const int Body = 17, Small = 13, Head = 24, RowH = 46;
    static Font _font;
    public static Font HalFont => _font != null ? _font : (_font = Resources.Load<Font>("Resource_Fonts/HalOrbitron"));
    static readonly Color Ink = new(0.92f, 0.96f, 1f), Dim = new(0.62f, 0.74f, 0.84f), Blue = new(0.36f, 0.61f, 0.77f);
    static readonly Color Good = new(0.35f, 0.85f, 0.45f), Warn = new(1f, 0.75f, 0.25f), Bad = new(1f, 0.35f, 0.3f), Red = new(0.85f, 0.12f, 0.1f);
    static readonly Color Card = new(0.08f, 0.12f, 0.17f, 0.9f);

    static PlaytestBotSettings S => PlaytestBot.Ensure().S;

    // ══════════════════════════════════════════ HAL 9000 ══════════════════════════════════════════

    public static void BuildHal(VisualElement root)
    {
        root.Clear();
        root.style.paddingLeft = root.style.paddingRight = 14; root.style.paddingTop = root.style.paddingBottom = 12;

        var title = Text("HAL 9000", 34, new Color(0.72f, 0.40f, 1f), true);
        root.Add(title);
        root.Add(Text("Playtest bot. It plays the warehouse so we can hunt bugs. Not an AI: a fixed set of rules.", Small, Dim));

        // status card
        var status = Section(root, "STATUS", Blue);
        var statusLine = Text("", 22, Ink, true); status.Add(statusLine);
        var stats = Text("", Body, Ink); stats.style.marginTop = 4; status.Add(stats);

        var btnRow = new VisualElement(); btnRow.style.flexDirection = FlexDirection.Row; btnRow.style.marginTop = 10;
        var start = BigButton("START BLOCK", new Color(0.15f, 0.55f, 0.25f), () =>
        {
            if (!Application.isPlaying) { UIToast.Show("Enter Play mode and load a save first."); return; }
            PlaytestBot.Ensure().StartBlock();
        });
        var stop = BigButton("STOP", new Color(0.65f, 0.18f, 0.15f), () => PlaytestBot.Ensure().StopBot());
        stop.style.marginLeft = 12;
        // Fresh game + build the selected layout + run, the same as INITIATE HAL 9000 on the main menu, without leaving the game.
        var fresh = BigButton("NEW COMPANY", new Color(0.15f, 0.4f, 0.65f), () =>
        {
            if (!Application.isPlaying) { UIToast.Show("Enter Play mode first."); return; }
            PlayerPrefs.SetString("PlayerName", PlayerPrefs.GetString("PlayerName", "Tug Dudley"));
            PlayerPrefs.SetInt("IsNewGame", 1); PlayerPrefs.SetInt("FromMainMenu", 1); PlayerPrefs.SetInt("HalAutoStart", 1); PlayerPrefs.Save();
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        });
        fresh.style.marginLeft = 12;
        // A human plays and HAL only scores it: no decisions, no planner, your own speed. Load YOUR save first, then press this.
        var mine = BigButton("I'LL PLAY (SCORE ME)", new Color(0.7f, 0.5f, 0.1f), () =>
        {
            if (!Application.isPlaying) { UIToast.Show("Enter Play mode and load your save first."); return; }
            PlaytestBot.Ensure().StartBlock(true);
            UIToast.Show("HAL 9000 is watching. Your score is at the top of the screen.");
        });
        mine.style.marginLeft = 12;
        btnRow.Add(start); btnRow.Add(stop); btnRow.Add(fresh); btnRow.Add(mine); status.Add(btnRow);

        statusLine.schedule.Execute(() =>
        {
            var bot = PlaytestBot.Instance;
            if (bot == null) { statusLine.text = "Idle - press START BLOCK"; stats.text = ""; return; }
            int day = GameCore.Services.ServiceLocator.TryGet<GameCore.Economy.SimulationTimeService>(out var t) ? t.Day : 0;
            statusLine.text = bot.Running
                ? (bot.ObserveOnly ? $"YOU are playing - day {Mathf.Max(0, day - bot.BlockStartDay) + 1} of {bot.S.daysPerBlock} (HAL is only scoring)"
                                   : $"Block {bot.BlockNo} running - day {Mathf.Max(0, day - bot.BlockStartDay) + 1} of {bot.S.daysPerBlock}")
                : (bot.BlockNo > 0 ? bot.Status : "Idle - press START BLOCK");
            statusLine.style.color = bot.Running ? Good : Ink;
            stats.text = $"Shipped {bot.ShippedThisBlock}   Late {bot.LateThisBlock}   Bug incidents {bot.IncidentCount}   Errors {bot.ErrorCount}   Bot actions {bot.ActionsTaken}";
        }).Every(500);

        // knobs
        var run = Section(root, "RUN", Blue);
        Slider(run, "Game speed", 1, 5, 1, () => Mathf.RoundToInt(S.gameSpeed), v => { S.gameSpeed = v; if (PlaytestBot.Instance != null && PlaytestBot.Instance.Running && !PlaytestBot.Instance.ObserveOnly) Time.timeScale = v; }, v => v + "x");
        Slider(run, "Days per block", 1, 14, 1, () => S.daysPerBlock, v => S.daysPerBlock = v, v => v + " days");
        Slider(run, "Decides every", 15, 120, 15, () => S.decisionIntervalMinutes, v => S.decisionIntervalMinutes = v, v => v + " min");
        Toggle(run, "Pause when a block ends", () => S.pauseAtBlockEnd, v => S.pauseAtBlockEnd = v);
        Toggle(run, "Save the game when a block ends", () => S.saveAtBlockEnd, v => S.saveAtBlockEnd = v);

        var crew = Section(root, "STARTING A NEW COMPANY", Blue);
        var layouts = HalLayout.Names();
        if (layouts.Count > 0)
        {
            var lrow = new VisualElement(); lrow.style.flexDirection = FlexDirection.Row; lrow.style.alignItems = Align.Center; lrow.style.minHeight = RowH;
            lrow.Add(Text("Building layout", Body, Ink));
            lrow.Children().First().style.width = Length.Percent(40);
            if (!layouts.Contains(S.layoutName)) S.layoutName = layouts[0];
            var dd = new DropdownField(layouts, layouts.IndexOf(S.layoutName));
            dd.style.flexGrow = 1; dd.style.fontSize = Body; dd.style.height = 40;
            if (HalFont != null) dd.style.unityFontDefinition = FontDefinition.FromFont(HalFont);
            dd.RegisterValueChangedCallback(e => { S.layoutName = e.newValue; Persist(dd); });
            lrow.Add(dd); crew.Add(lrow);
        }
        Slider(crew, "Dock stockers (machine + driver)", 0, 3, 1, () => S.startDockStockers, v => S.startDockStockers = v, v => v.ToString());
        Slider(crew, "Reach trucks (machine + driver)", 0, 3, 1, () => S.startReachTruckOps, v => S.startReachTruckOps = v, v => v.ToString());
        Slider(crew, "Receivers", 0, 3, 1, () => S.startReceivers, v => S.startReceivers = v, v => v.ToString());
        Slider(crew, "Order selectors", 0, 4, 1, () => S.startSelectors, v => S.startSelectors = v, v => v.ToString());
        Slider(crew, "Keep this much cash before building upper racks", 0, 60000, 5000, () => S.buildReserveCash, v => S.buildReserveCash = v, v => "$" + v.ToString("N0"));

        Toggle(crew, "Just build it: no bot, no records (for making layouts)", () => S.buildOnly, v => S.buildOnly = v);

        var lay = Section(root, "SAVE THE WAREHOUSE AS A LAYOUT", Blue);
        lay.Add(Text("Edit the building in the game as you like, then save it here. It appears in the layout list next time.", Small, Dim));
        var nameRow = new VisualElement(); nameRow.style.flexDirection = FlexDirection.Row; nameRow.style.alignItems = Align.Center; nameRow.style.minHeight = RowH;
        var nameLabel = Text("Layout name", Body, Ink); nameLabel.style.width = Length.Percent(40f);
        var nameField = new TextField { value = "Small Layout" };
        nameField.style.flexGrow = 1; nameField.style.fontSize = Body; nameField.style.height = 40;
        nameRow.Add(nameLabel); nameRow.Add(nameField); lay.Add(nameRow);
        var saveMsg = Text("", Small, Good); saveMsg.style.marginTop = 4;
        var saveBtn = BigButton("SAVE LAYOUT", new Color(0.15f, 0.45f, 0.7f), () =>
        {
            if (!Application.isPlaying) { saveMsg.text = "Enter Play mode with a warehouse first."; saveMsg.style.color = Bad; return; }
            string path = HalLayoutExporter.Export(nameField.value, out string msg);
            saveMsg.text = msg; saveMsg.style.color = path != null ? Good : Bad;
            if (path != null) SystemsLogWindow.LogHal("HAL 9000: " + msg);
        });
        saveBtn.style.marginTop = 6; lay.Add(saveBtn); lay.Add(saveMsg);

        var cust = Section(root, "CUSTOMERS", Blue);
        Slider(cust, "Recurring accounts to keep", 0, 8, 1, () => S.maxRecurringAccounts, v => S.maxRecurringAccounts = v, v => v.ToString());
        Slider(cust, "Bulk orders accepted per day", 0, 6, 1, () => S.bulkOffersPerDay, v => S.bulkOffersPerDay = v, v => v.ToString());

        var buy = Section(root, "MONEY AND BUYING", Blue);
        Slider(buy, "Never let cash fall below", 0, 100000, 5000, () => S.minCashBalance, v => S.minCashBalance = v, v => "$" + v.ToString("N0"));
        Slider(buy, "Spend at most this share of spare cash per day", 10, 100, 10, () => Mathf.RoundToInt(S.maxDailySpendFraction * 100), v => S.maxDailySpendFraction = v / 100f, v => v + "%");
        Slider(buy, "Pallets to keep of each active item", 1, 10, 1, () => S.palletsPerActiveSku, v => S.palletsPerActiveSku = v, v => v.ToString());
        Slider(buy, "...when cash is plentiful", 2, 12, 1, () => S.bulkUpPalletsPerSku, v => S.bulkUpPalletsPerSku = v, v => v.ToString());
        Slider(buy, "Purchase orders in transit at once", 1, 6, 1, () => S.maxPendingPOs, v => S.maxPendingPOs = v, v => v.ToString());
        Toggle(buy, "Cover shortages with double-cost rush orders", () => S.coverRushOrders, v => S.coverRushOrders = v);
        Slider(buy, "Rush orders per day", 0, 3, 1, () => S.maxRushPerDay, v => S.maxRushPerDay = v, v => v.ToString());

        var mon = Section(root, "BUG MONITOR", Blue);
        Slider(mon, "Integrity check every", 15, 180, 15, () => S.healthCheckIntervalMinutes, v => S.healthCheckIntervalMinutes = v, v => v + " min");
        Slider(mon, "Report a driven vehicle that sits still for", 60, 600, 60, () => Mathf.RoundToInt(S.stationarySeconds), v => S.stationarySeconds = v, v => v + " s");
        root.Add(Text("Reports are written to Assets/_Project/Docs/Playtest/ after each block.", Small, Dim));
    }

    // ══════════════════════════════════════════ BALANCE ══════════════════════════════════════════

    public static void BuildBalance(VisualElement root)
    {
        root.Clear();
        root.style.paddingLeft = root.style.paddingRight = 14; root.style.paddingTop = root.style.paddingBottom = 12;

        root.Add(Text("BALANCE", 34, Blue, true));
        root.Add(Text("How a block is scored. You have 100 points to spend. Moving one slider up takes the points from the pool; to add more, lower another first.", Small, Dim));

        // pool card (kept at the top so it is always the first thing you see)
        var pool = Section(root, "POINT POOL", Warn);
        var poolBig = Text("", 40, Good, true); pool.Add(poolBig);
        var poolSub = Text("", Body, Ink); pool.Add(poolSub);
        var bar = new VisualElement(); bar.style.height = 16; bar.style.marginTop = 8; bar.style.backgroundColor = new Color(1, 1, 1, 0.08f);
        bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius = bar.style.borderBottomLeftRadius = bar.style.borderBottomRightRadius = 8;
        var fillBar = new VisualElement(); fillBar.style.height = Length.Percent(100); fillBar.style.borderTopLeftRadius = fillBar.style.borderBottomLeftRadius = 8;
        bar.Add(fillBar); pool.Add(bar);

        void RefreshPool()
        {
            int total = S.TotalWeight, left = 100 - total;
            poolBig.text = $"{total} / 100";
            Color c = total == 100 ? Good : (total < 100 ? Warn : Bad);
            poolBig.style.color = c; fillBar.style.backgroundColor = c;
            fillBar.style.width = Length.Percent(Mathf.Clamp(total, 0, 100));
            poolSub.text = total == 100 ? "All points spent." : (total < 100 ? $"{left} points left to spend." : $"{-left} points OVER. Lower something.");
        }

        var reset = BigButton("RESET TO BASELINE", new Color(0.25f, 0.4f, 0.55f), () => { S.ResetWeightsToBaseline(); BuildBalance(root); Persist(root); });
        reset.style.marginTop = 10; pool.Add(reset);
        RefreshPool();

        var w = Section(root, "WHAT MATTERS (points)", Blue);
        Weight(w, "On-time delivery", "Orders shipped without a late fine.", () => S.wOnTime, v => S.wOnTime = v, RefreshPool);
        Weight(w, "Fill rate", "How much of each order was actually picked.", () => S.wFill, v => S.wFill = v, RefreshPool);
        Weight(w, "Pallet throughput", "Pallets shipped against the target below.", () => S.wThroughput, v => S.wThroughput = v, RefreshPool);
        Weight(w, "Revenue", "Money earned against the target below.", () => S.wRevenue, v => S.wRevenue = v, RefreshPool);
        Weight(w, "Profit", "Cash gained over the block, net of every cost.", () => S.wProfit, v => S.wProfit = v, RefreshPool);
        Weight(w, "Cash health", "Share of the block spent above the cash floor.", () => S.wCash, v => S.wCash = v, RefreshPool);
        Weight(w, "Reliability", "Fewer bugs and errors. Game stability.", () => S.wReliability, v => S.wReliability = v, RefreshPool);
        Weight(w, "Reputation", "Reputation gained over the block.", () => S.wReputation, v => S.wReputation = v, RefreshPool);

        var t = Section(root, "TARGETS (100% of a score = hitting its target)", Blue);
        Slider(t, "Pallets shipped per day", 5, 60, 5, () => Mathf.RoundToInt(S.targetPalletsPerDay), v => S.targetPalletsPerDay = v, v => v.ToString());
        Slider(t, "Revenue per day", 1000, 30000, 1000, () => Mathf.RoundToInt(S.targetRevenuePerDay), v => S.targetRevenuePerDay = v, v => "$" + v.ToString("N0"));
        Slider(t, "Profit per day", 0, 10000, 500, () => Mathf.RoundToInt(S.targetProfitPerDay), v => S.targetProfitPerDay = v, v => "$" + v.ToString("N0"));
        Slider(t, "Reputation gain per block", 10, 100, 10, () => Mathf.RoundToInt(S.reputationGainPerBlock), v => S.reputationGainPerBlock = v, v => "+" + v);
        Slider(t, "Bug tolerance (higher = bugs hurt less)", 5, 30, 5, () => Mathf.RoundToInt(S.reliabilityTolerance), v => S.reliabilityTolerance = v, v => v.ToString());
        var g = Section(root, "GROWTH RULES (what HAL changes between blocks)", Blue);
        Toggle(g, "HAL may change the warehouse between blocks", () => S.plannerEnabled, v => S.plannerEnabled = v);
        Toggle(g, "...and may also remove people and doors it no longer needs", () => S.plannerMayRemove, v => S.plannerMayRemove = v);
        Slider(g, "Hire when a job is this busy", 50, 95, 5, () => S.addUtilPercent, v => S.addUtilPercent = v, v => v + "%");
        Slider(g, "...and this many jobs are waiting", 1, 10, 1, () => S.addBacklog, v => S.addBacklog = v, v => v.ToString());
        Slider(g, "Let someone go when a job is under this busy for two blocks", 5, 50, 5, () => S.removeUtilPercent, v => S.removeUtilPercent = v, v => v + "%");
        Slider(g, "Build another dock door when trucks waiting averages", 1, 6, 1, () => Mathf.RoundToInt(S.addWaitingTrucks), v => S.addWaitingTrucks = v, v => v.ToString());
        root.Add(Text("These targets are first guesses. Play a few blocks, then move them to match what a good run really looks like.", Small, Dim));
    }

    // ══════════════════════════════════════════ widgets ══════════════════════════════════════════

    // ══════════════════════════════════════════ ITERATIONS ══════════════════════════════════════════

    struct Metric { public string title; public Func<PlaytestBlockResult, float> get; public Func<float, string> fmt; public bool higherIsBetter; public Color color; }

    static List<Metric> Metrics() => new()
    {
        new Metric { title = "SCORE", get = r => r.score, fmt = v => v.ToString("0.0"), higherIsBetter = true, color = new Color(1f, 0.82f, 0.3f) },
        new Metric { title = "ON-TIME DELIVERY", get = r => r.onTime, fmt = v => v.ToString("0") + "%", higherIsBetter = true, color = new Color(0.35f, 0.85f, 0.45f) },
        new Metric { title = "PALLETS SHIPPED", get = r => r.palletsShipped, fmt = v => v.ToString("0"), higherIsBetter = true, color = new Color(0.35f, 0.7f, 1f) },
        new Metric { title = "REVENUE", get = r => r.revenueTotal, fmt = v => "$" + v.ToString("N0"), higherIsBetter = true, color = new Color(0.35f, 0.85f, 0.45f) },
        new Metric { title = "CASH CHANGE", get = r => r.cashEnd - r.cashStart, fmt = v => (v < 0 ? "-$" : "+$") + Mathf.Abs(v).ToString("N0"), higherIsBetter = true, color = new Color(0.7f, 0.9f, 0.4f) },
        new Metric { title = "BUG INCIDENTS", get = r => r.bugIncidents, fmt = v => v.ToString("0"), higherIsBetter = false, color = new Color(1f, 0.4f, 0.35f) },
        new Metric { title = "RELIABILITY", get = r => r.reliability, fmt = v => v.ToString("0") + "%", higherIsBetter = true, color = new Color(1f, 0.6f, 0.3f) },
        new Metric { title = "REPUTATION", get = r => r.repEnd, fmt = v => v.ToString("0"), higherIsBetter = true, color = new Color(0.8f, 0.6f, 1f) },
        new Metric { title = "HEADCOUNT", get = r => r.headReceiver + r.headReach + r.headDock + r.headSelector, fmt = v => v.ToString("0"), higherIsBetter = false, color = new Color(0.6f, 0.8f, 0.95f) },
        new Metric { title = "DOCK DOORS", get = r => r.doors, fmt = v => v.ToString("0"), higherIsBetter = false, color = new Color(0.6f, 0.8f, 0.95f) },
        new Metric { title = "JOBS WAITING (AVG)", get = r => r.backlogReceive + r.backlogPutaway + r.backlogPick + r.backlogLoad + r.backlogOrders, fmt = v => v.ToString("0.0"), higherIsBetter = false, color = new Color(1f, 0.75f, 0.25f) },
        new Metric { title = "CREW BUSY (AVG)", get = r => (r.utilReceiver + r.utilReach + r.utilDock + r.utilSelector) / 4f * 100f, fmt = v => v.ToString("0") + "%", higherIsBetter = true, color = new Color(0.4f, 0.9f, 0.85f) },
    };

    public static void BuildIterations(VisualElement root)
    {
        root.Clear();
        root.style.paddingLeft = root.style.paddingRight = 14; root.style.paddingTop = root.style.paddingBottom = 12;
        root.Add(Text("ITERATIONS", 34, new Color(1f, 0.82f, 0.3f), true));
        root.Add(Text("One point per finished block. Green means better than the block before, red worse.", Small, Dim));

        var blocks = LoadBlocks();
        if (blocks.Count == 0)
        {
            var empty = Section(root, "NO BLOCKS YET", Warn);
            empty.Add(Text("Finish a block with HAL 9000 and its results will be drawn here.", Body, Ink));
            return;
        }

        var grid = new VisualElement(); grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = Wrap.Wrap; grid.style.marginTop = 10;
        foreach (var m in Metrics())
        {
            var vals = blocks.Select(b => m.get(b)).ToList();
            var card = new VisualElement();
            card.style.width = Length.Percent(32.5f); card.style.marginRight = Length.Percent(0.5f); card.style.marginBottom = 8;
            card.style.backgroundColor = Card; card.style.paddingLeft = card.style.paddingRight = 8; card.style.paddingTop = card.style.paddingBottom = 6;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;
            card.style.borderTopWidth = 3; card.style.borderTopColor = m.color;

            var head = new VisualElement(); head.style.flexDirection = FlexDirection.Row; head.style.justifyContent = Justify.SpaceBetween; head.style.alignItems = Align.Center;
            var title = Text(m.title, 12, Dim); title.style.flexShrink = 1;
            var last = vals[vals.Count - 1];
            var value = Text(m.fmt(last), 20, Ink); value.style.flexShrink = 0;
            head.Add(title); head.Add(value); card.Add(head);

            string delta = ""; Color dc = Dim;
            if (vals.Count > 1)
            {
                float d = last - vals[vals.Count - 2];
                bool better = m.higherIsBetter ? d > 0 : d < 0;
                if (Mathf.Abs(d) > 0.0001f) { delta = (d > 0 ? "up " : "down ") + m.fmt(Mathf.Abs(d)); dc = better ? Good : Bad; }
                else delta = "no change";
            }
            card.Add(Text(delta, 12, dc));

            var chart = new IterationChart(vals, m.color, m.fmt);
            chart.style.height = 90; chart.style.marginTop = 4;
            card.Add(chart);
            grid.Add(card);
        }
        root.Add(grid);

        // what changed between blocks, and what happened to the score afterwards
        var ledger = HalPlanner.LoadLedger(S);
        var log = Section(root, "WHAT HAL CHANGED", Blue);
        if (ledger.changes.Count == 0) log.Add(Text("Nothing yet. HAL makes one change before each block after the first.", Body, Dim));
        foreach (var c in Enumerable.Reverse(ledger.changes).Take(12))
        {
            string outcome = c.measured ? $"score {c.scoreBefore:0.0} to {c.scoreAfter:0.0} ({c.scoreAfter - c.scoreBefore:+0.0;-0.0;0.0})" : "waiting for that block to finish";
            var line = Text($"Block {c.block}: {c.action}. {c.reason}. {outcome}.", 14, c.measured && c.scoreAfter > c.scoreBefore ? Good : (c.measured ? Warn : Ink));
            line.style.marginTop = 4; log.Add(line);
        }
    }

    static List<PlaytestBlockResult> LoadBlocks()
    {
        try
        {
            string dir = S.ResolveReportDir();
            string f = System.IO.Path.Combine(dir, "scoreboard.json");
            if (System.IO.File.Exists(f)) return JsonUtility.FromJson<PlaytestScoreboard>(System.IO.File.ReadAllText(f)).blocks.OrderBy(b => b.block).ToList();
        }
        catch (Exception e) { Debug.LogWarning("[Iterations] " + e.Message); }
        return new List<PlaytestBlockResult>();
    }

    /// <summary>A small line chart: one point per block, a dot on every point, min and max labelled, block numbers underneath.</summary>
    class IterationChart : VisualElement
    {
        const float PadFrac = 22f;
        readonly List<float> _v; readonly Color _col; readonly float _min, _max;

        public IterationChart(List<float> values, Color col, Func<float, string> fmt)
        {
            _v = values; _col = col;
            float max = values.Max(), min = values.Min();
            if (Mathf.Approximately(max, min)) { max += 1f; min -= Mathf.Approximately(min, 0f) ? 0f : 1f; }
            _min = min; _max = max;
            generateVisualContent += Draw;

            Label L(string s) { var l = Text(s, 11, Dim); l.pickingMode = PickingMode.Ignore; l.style.position = Position.Absolute; return l; }
            var top = L(fmt(max)); top.style.left = 2; top.style.top = 0; Add(top);
            var bot = L(fmt(min)); bot.style.left = 2; bot.style.bottom = 14; Add(bot);
            for (int i = 0; i < values.Count; i++)
            {
                var n = L((i + 1).ToString());
                n.style.bottom = 0;
                float f = values.Count == 1 ? 0.5f : i / (float)(values.Count - 1);
                n.style.left = Length.Percent(PadFrac + f * (96f - PadFrac));
                Add(n);
            }
        }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect; if (r.width < 10 || r.height < 10) return;
            var p = mgc.painter2D;
            float x0 = r.width * PadFrac / 100f, x1 = r.width * 0.96f, y0 = 14f, y1 = r.height - 18f;
            p.lineWidth = 1f; p.strokeColor = new Color(1, 1, 1, 0.1f);
            for (int i = 0; i <= 2; i++) { float y = Mathf.Lerp(y1, y0, i / 2f); p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.Stroke(); }
            Vector2 Pt(int i) => new Vector2(_v.Count == 1 ? (x0 + x1) * 0.5f : Mathf.Lerp(x0, x1, i / (float)(_v.Count - 1)), Mathf.Lerp(y1, y0, Mathf.InverseLerp(_min, _max, _v[i])));
            p.lineWidth = 2.5f; p.strokeColor = _col; p.lineJoin = LineJoin.Round;
            if (_v.Count > 1) { p.BeginPath(); p.MoveTo(Pt(0)); for (int i = 1; i < _v.Count; i++) p.LineTo(Pt(i)); p.Stroke(); }
            p.fillColor = _col;
            for (int i = 0; i < _v.Count; i++) { p.BeginPath(); p.Arc(Pt(i), i == _v.Count - 1 ? 5f : 3.5f, 0f, 360f); p.Fill(); }
        }
    }

    /// <summary>A slider in the points pool: whole numbers in steps of 5, and it cannot take more than the pool still holds.</summary>
    static void Weight(VisualElement parent, string label, string hint, Func<int> get, Action<int> set, Action onChange)
    {
        var holder = new VisualElement();
        var s = Slider(holder, label, 0, 100, 5, get, v =>
        {
            int others = S.TotalWeight - get();
            int allowed = Mathf.Max(0, (100 - others) / 5 * 5);
            set(Mathf.Min(v, allowed));
            onChange();
        }, v => v + " pts", clampTo: () => Mathf.Max(0, (100 - (S.TotalWeight - get())) / 5 * 5));
        var h = Text(hint, Small, Dim); h.style.marginLeft = 4; h.style.marginBottom = 6; holder.Add(h);
        parent.Add(holder);
    }

    static SliderInt Slider(VisualElement parent, string label, int min, int max, int step, Func<int> get, Action<int> set,
                            Func<int, string> fmt, Func<int> clampTo = null)
    {
        var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.minHeight = RowH;
        var name = Text(label, Body, Ink); name.style.width = Length.Percent(40); name.style.flexShrink = 0; name.style.whiteSpace = WhiteSpace.Normal;
        var slider = new SliderInt(min, max) { value = Mathf.Clamp(get(), min, max) };
        slider.style.flexGrow = 1; slider.style.marginLeft = slider.style.marginRight = 10;
        var drag = slider.Q(className: "unity-base-slider__dragger");
        if (drag != null) { drag.style.width = 30; drag.style.height = 30; drag.style.top = -10; drag.style.backgroundColor = Blue; drag.style.borderTopLeftRadius = drag.style.borderTopRightRadius = drag.style.borderBottomLeftRadius = drag.style.borderBottomRightRadius = 15; }
        var track = slider.Q(className: "unity-base-slider__tracker");
        if (track != null) { track.style.height = 10; track.style.backgroundColor = new Color(1, 1, 1, 0.18f); }
        var val = Text(fmt(slider.value), Body, Warn, true); val.style.width = 130; val.style.flexShrink = 0; val.style.unityTextAlign = TextAnchor.MiddleRight;

        slider.RegisterValueChangedCallback(e =>
        {
            int v = Mathf.RoundToInt(e.newValue / (float)step) * step;
            if (clampTo != null) v = Mathf.Min(v, clampTo());
            v = Mathf.Clamp(v, min, max);
            if (v != slider.value) slider.SetValueWithoutNotify(v);
            set(v);
            val.text = fmt(v);
            Persist(slider);
        });
        row.Add(name); row.Add(slider); row.Add(val); parent.Add(row);
        return slider;
    }

    static void Toggle(VisualElement parent, string label, Func<bool> get, Action<bool> set)
    {
        var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.minHeight = RowH;
        var name = Text(label, Body, Ink); name.style.flexGrow = 1;
        var btn = new Button();
        void Paint() { btn.text = get() ? "ON" : "OFF"; btn.style.backgroundColor = get() ? new Color(0.15f, 0.55f, 0.25f) : new Color(0.35f, 0.38f, 0.42f); }
        btn.clicked += () => { set(!get()); Paint(); Persist(btn); };
        btn.style.width = 110; btn.style.height = 38; btn.style.fontSize = Body; btn.style.color = Color.white;
        if (HalFont != null) btn.style.unityFontDefinition = FontDefinition.FromFont(HalFont);
        Paint();
        row.Add(name); row.Add(btn); parent.Add(row);
    }

    static Button BigButton(string text, Color bg, Action click)
    {
        var b = new Button(click) { text = text };
        b.style.height = 56; b.style.minWidth = 200; b.style.fontSize = 20; b.style.color = Color.white; b.style.backgroundColor = bg;
        if (HalFont != null) b.style.unityFontDefinition = FontDefinition.FromFont(HalFont);
        b.style.paddingLeft = b.style.paddingRight = 18;
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 10;
        return b;
    }

    static VisualElement Section(VisualElement parent, string title, Color accent)
    {
        var s = new VisualElement();
        s.style.backgroundColor = Card; s.style.marginTop = 14; s.style.paddingLeft = s.style.paddingRight = 14; s.style.paddingTop = 10; s.style.paddingBottom = 10;
        s.style.borderLeftWidth = 5; s.style.borderLeftColor = accent;
        s.style.borderTopLeftRadius = s.style.borderTopRightRadius = s.style.borderBottomLeftRadius = s.style.borderBottomRightRadius = 8;
        s.Add(Text(title, Head - 4, accent, true));
        parent.Add(s);
        return s;
    }

    static Label Text(string text, int size, Color color, bool bold = false)
    {
        var l = new Label(text);
        l.style.fontSize = size; l.style.color = color; l.style.whiteSpace = WhiteSpace.Normal;
        if (HalFont != null) l.style.unityFontDefinition = FontDefinition.FromFont(HalFont);
        return l;
    }

    // Debounced: save the settings asset a moment after the last edit so tuning survives a restart.
    static IVisualElementScheduledItem _saveJob;
    static void Persist(VisualElement anchor)
    {
#if UNITY_EDITOR
        var s = S; if (s == null) return;
        UnityEditor.EditorUtility.SetDirty(s);
        _saveJob?.Pause();
        _saveJob = anchor.schedule.Execute(() => UnityEditor.AssetDatabase.SaveAssetIfDirty(s)).StartingIn(1500);
#endif
    }
}
