using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Diagnostics;

/// <summary>
/// HAL 9000's SUMMARY window. Opaque white panel with a purple border, laid out like Tad's mock-up: a title row (block / day, score, close),
/// SUMMARY and ALL CHANGES tabs, a red close-out-streak banner, then a three-column table (what + why it is behind / goal / actual), and a
/// hover explanation on every row (and the full change list on the ALL CHANGES tab). Everything fits on one screen at the default size and the window stays small
/// enough to play the game beside it. Opened by double-clicking the HAL score box. Drag the title row to move it, drag the purple corner to
/// resize, X to close.
/// </summary>
public static class HalSummaryWindow
{
    const float DefaultW = 660f, DefaultH = 580f, IterW = 900f, IterH = 720f;

    static VisualElement _win, _head, _grip, _body, _tableHost, _logPanel;
    static Label _title, _score, _sub, _close, _tabSummary, _tabChanges;
    static VisualElement _table;
    static ScrollView _logScroll;
    static int _sizedForTab = -1;
    static int _tab;                      // 0 = summary, 1 = all changes, 2 = iterations
    static Label _tabIter, _tabMetrics, _curLabel, _highLabel, _highValue, _whoLabel;
    static ScrollView _iterScroll, _metricsScroll;
    static string _iterSig, _metricsSig;
    static string _tableSig, _logSig;
    static Font _lilita;

    static readonly Color Paper   = new Color(0.91f, 0.91f, 0.91f, 1f);
    static readonly Color Ink     = new Color(0.08f, 0.08f, 0.08f, 1f);
    static readonly Color InkSoft = new Color(0.22f, 0.22f, 0.22f, 1f);
    static readonly Color Grid    = new Color(0.55f, 0.55f, 0.55f, 1f);
    static readonly Color Band    = new Color(0.78f, 0.78f, 0.78f, 1f);
    static readonly Color Purple  = new Color(0.72f, 0.40f, 1f, 1f);
    static readonly Color ScorePurple = new Color(0.52f, 0.18f, 0.86f, 1f);   // a shade deeper so it reads on the pale panel
    static readonly Color Good    = new Color(0.10f, 0.58f, 0.20f, 1f);
    static readonly Color Bad     = new Color(0.70f, 0.08f, 0.08f, 1f);
    static readonly Color Pending = new Color(0.58f, 0.58f, 0.58f, 1f);
    static readonly Color LogBg   = new Color(0.09f, 0.09f, 0.13f, 1f);
    static readonly Color LogGreen  = new Color(0.30f, 0.90f, 0.35f, 1f);
    static readonly Color LogYellow = new Color(1f, 0.90f, 0.20f, 1f);

    public static bool IsOpen => _win != null && _win.panel != null && _win.style.display == DisplayStyle.Flex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _win = null; _tableSig = _logSig = null; }

    public static void Toggle() { if (IsOpen) Close(); else Open(); }

    public static void Open()
    {
        var layer = ShippedFanfare.LayerRoot();
        if (layer == null) return;
        if (_win == null || _win.panel == null) Build(layer);
        _win.style.display = DisplayStyle.Flex;
        _win.BringToFront();
        _tableSig = _logSig = null;
        Refresh();
    }

    public static void Close()
    {
        HideExplain();
        if (_win != null) _win.style.display = DisplayStyle.None;
    }

    // ── building blocks ───────────────────────────────────────────────────────

    static Font Lilita => _lilita != null ? _lilita : (_lilita = Resources.Load<Font>("Resource_Fonts/LilitaOne-Regular"));

    static Label L(float size, Color col, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var l = new Label();
        l.pickingMode = PickingMode.Ignore;
        l.style.fontSize = size; l.style.color = col; l.style.unityTextAlign = align;
        l.style.whiteSpace = WhiteSpace.NoWrap; l.style.flexShrink = 0;
        l.style.paddingTop = l.style.paddingBottom = l.style.paddingLeft = l.style.paddingRight = 0;
        l.style.marginTop = l.style.marginBottom = l.style.marginLeft = l.style.marginRight = 0;
        if (Lilita != null) l.style.unityFontDefinition = FontDefinition.FromFont(Lilita);
        return l;
    }

    static void Border(VisualElement e, float w, Color c)
    {
        e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
        e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
    }

    static void Radius(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
    }

    static void Build(VisualElement layer)
    {
        _win = new VisualElement { name = "HalSummaryWindow" };
        _win.pickingMode = PickingMode.Position;
        _win.style.position = Position.Absolute;
        _win.style.left = 40; _win.style.top = 60; _win.style.width = DefaultW;
        _win.style.backgroundColor = Paper;
        _win.style.flexDirection = FlexDirection.Column;
        _win.style.paddingLeft = _win.style.paddingRight = 10; _win.style.paddingTop = 8; _win.style.paddingBottom = 8;
        Border(_win, 3, Purple); Radius(_win, 8);
        _win.style.display = DisplayStyle.None;

        // title row
        _head = new VisualElement();
        _head.style.flexDirection = FlexDirection.Row; _head.style.alignItems = Align.Center; _head.style.flexShrink = 0; _head.style.minHeight = 40;
        _title = L(24, Ink); _title.text = "HAL 9000 SUMMARY";
        _sub = L(12, InkSoft); _sub.style.marginLeft = 12; _sub.style.flexGrow = 1; _sub.style.flexShrink = 1; _sub.style.overflow = Overflow.Hidden;
        // Two aligned rows, labels on the left and the big numbers on the right:
        //   CURRENT SCORE   37.5
        //   HIGH SCORE:     55.1   (purple)
        //                   by YOU (purple)
        var scoreBox = new VisualElement(); scoreBox.style.flexShrink = 0; scoreBox.style.marginRight = 6; scoreBox.style.marginTop = 24; scoreBox.style.alignItems = Align.FlexEnd;
        VisualElement ScoreRow(Label label, Label value)
        {
            var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.justifyContent = Justify.FlexEnd;
            label.style.marginRight = 8;
            value.style.minWidth = 84;
            r.Add(label); r.Add(value);
            return r;
        }
        _curLabel = L(12, Ink, TextAnchor.MiddleRight); _curLabel.text = "CURRENT SCORE";
        _score = L(30, Ink, TextAnchor.MiddleRight);
        _highLabel = L(12, ScorePurple, TextAnchor.MiddleRight); _highLabel.text = "HIGH SCORE:";
        _highValue = L(30, ScorePurple, TextAnchor.MiddleRight);
        _whoLabel = L(12, ScorePurple, TextAnchor.MiddleRight); _whoLabel.text = "";
        scoreBox.Add(ScoreRow(_curLabel, _score));
        scoreBox.Add(ScoreRow(_highLabel, _highValue));
        scoreBox.Add(_whoLabel);
        _close = L(18, Color.white, TextAnchor.MiddleCenter); _close.text = "X"; _close.pickingMode = PickingMode.Position;
        _close.style.width = 30; _close.style.height = 28; _close.style.backgroundColor = new Color(0.72f, 0.12f, 0.12f, 1f); Radius(_close, 4);
        _close.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; Close(); e.StopPropagation(); });
        _head.Add(_title); _head.Add(_sub); _head.Add(scoreBox);
        _win.Add(_head);
        _close.style.position = Position.Absolute; _close.style.top = 4; _close.style.right = 4;   // back up in the corner
        _win.Add(_close);

        // tabs
        var tabs = new VisualElement();
        tabs.style.flexDirection = FlexDirection.Row; tabs.style.flexShrink = 0; tabs.style.height = 28; tabs.style.marginTop = 2; tabs.style.marginBottom = 6;
        _tabSummary = Tab("SUMMARY", 0); _tabIter = Tab("ITERATIONS", 2); _tabChanges = Tab("ALL CHANGES", 1); _tabMetrics = Tab("METRICS", 3);
        tabs.Add(_tabSummary); tabs.Add(_tabIter); tabs.Add(_tabChanges); tabs.Add(_tabMetrics);
        _win.Add(tabs);

        _metricsScroll = new ScrollView(ScrollViewMode.Vertical);
        _metricsScroll.style.flexGrow = 1; _metricsScroll.style.display = DisplayStyle.None;
        _metricsScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        _win.Add(_metricsScroll);

        // the table (summary tab)
        _tableHost = new VisualElement();
        _tableHost.style.flexGrow = 0; _tableHost.style.flexShrink = 0;
        _table = new VisualElement();   // no scroll bar: on the summary tab the window grows to fit the whole table
        _tableHost.Add(_table);
        _win.Add(_tableHost);

        _iterScroll = new ScrollView(ScrollViewMode.Vertical);
        _iterScroll.style.flexGrow = 1; _iterScroll.style.display = DisplayStyle.None;
        _iterScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        _win.Add(_iterScroll);

        // the dark change-log panel (under the table; fills the window on the ALL CHANGES tab)
        _logPanel = new VisualElement();
        _logPanel.style.flexShrink = 0; _logPanel.style.height = 112; _logPanel.style.marginTop = 8;
        _logPanel.style.backgroundColor = LogBg;
        Border(_logPanel, 2, Purple); Radius(_logPanel, 6);
        _logPanel.style.paddingLeft = 8; _logPanel.style.paddingTop = 4; _logPanel.style.paddingBottom = 4; _logPanel.style.paddingRight = 2;
        _logScroll = new ScrollView(ScrollViewMode.Vertical);
        _logScroll.style.flexGrow = 1;
        _logScroll.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
        _logPanel.Add(_logScroll);
        _win.Add(_logPanel);

        // resize corner
        _grip = new VisualElement();
        _grip.style.position = Position.Absolute; _grip.style.right = 0; _grip.style.bottom = 0; _grip.style.width = 18; _grip.style.height = 18;
        _grip.style.backgroundColor = Purple; _grip.style.borderTopLeftRadius = 6;
        _win.Add(_grip);

        WireDrag(); WireResize();
        _win.schedule.Execute(Refresh).Every(400);
        layer.Add(_win);
    }

    static Label Tab(string text, int index)
    {
        var t = L(13, Ink, TextAnchor.MiddleCenter);
        t.text = text; t.pickingMode = PickingMode.Position;
        t.style.height = 28; t.style.paddingLeft = t.style.paddingRight = 12; t.style.marginRight = 8;
        Border(t, 2, Ink); Radius(t, 4);
        t.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; _tab = index; HideExplain(); _tableSig = _logSig = _iterSig = _metricsSig = null; e.StopPropagation(); Refresh(); });
        return t;
    }

    static void StyleTabs()
    {
        _tabSummary.style.backgroundColor = _tab == 0 ? Band : Color.white;
        _tabChanges.style.backgroundColor = _tab == 1 ? Band : Color.white;
        _tabIter.style.backgroundColor = _tab == 2 ? Band : Color.white;
        _tabMetrics.style.backgroundColor = _tab == 3 ? Band : Color.white;
    }

    // ── drag / resize ─────────────────────────────────────────────────────────

    static void WireDrag()
    {
        Vector2 startPointer = default, startPos = default; bool down = false;
        _head.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;
            down = true; startPointer = e.position; startPos = new Vector2(_win.resolvedStyle.left, _win.resolvedStyle.top);
            _head.CapturePointer(e.pointerId);
        });
        _head.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!down || !_head.HasPointerCapture(e.pointerId)) return;
            Vector2 d = (Vector2)e.position - startPointer;
            _win.style.left = Mathf.Max(0f, startPos.x + d.x);
            _win.style.top = Mathf.Max(0f, startPos.y + d.y);
        });
        _head.RegisterCallback<PointerUpEvent>(e => { down = false; if (_head.HasPointerCapture(e.pointerId)) _head.ReleasePointer(e.pointerId); });
    }

    static void WireResize()
    {
        Vector2 startPointer = default, startSize = default; bool down = false;
        _grip.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;
            down = true; startPointer = e.position; startSize = new Vector2(_win.resolvedStyle.width, _win.resolvedStyle.height);
            _grip.CapturePointer(e.pointerId); e.StopPropagation();
        });
        _grip.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!down || !_grip.HasPointerCapture(e.pointerId)) return;
            Vector2 d = (Vector2)e.position - startPointer;
            _win.style.width = Mathf.Clamp(startSize.x + d.x, 520f, 1400f);
            if (_tab != 0) _win.style.height = Mathf.Clamp(startSize.y + d.y, 320f, 1000f);
        });
        _grip.RegisterCallback<PointerUpEvent>(e => { down = false; if (_grip.HasPointerCapture(e.pointerId)) _grip.ReleasePointer(e.pointerId); });
    }

    // ── content ───────────────────────────────────────────────────────────────

    static void Refresh()
    {
        if (!IsOpen) return;
        var bot = PlaytestBot.Instance;
        StyleTabs();

        _metricsScroll.style.display = _tab == 3 ? DisplayStyle.Flex : DisplayStyle.None;
        if (_tab == 3)
        {
            _tableHost.style.display = DisplayStyle.None; _iterScroll.style.display = DisplayStyle.None; _logPanel.style.display = DisplayStyle.None;
            if (_sizedForTab != 3)
            {
                _sizedForTab = 3;
                _win.style.height = IterH;
                if (_win.resolvedStyle.width < IterW) _win.style.width = IterW;
            }
            _sub.text = "time studies, in game minutes";
            RenderMetrics();
            return;
        }

        bool haveBlock = bot != null && (bot.Running || bot.BlockNo > 0);
        if (!haveBlock && _tab == 2) { _score.text = "-"; _sub.text = "no block running"; _tableHost.style.display = DisplayStyle.None; _logPanel.style.display = DisplayStyle.None; _iterScroll.style.display = DisplayStyle.Flex; RenderIterations(); return; }
        if (!haveBlock)
        {
            _score.text = "-"; _sub.text = "no block running";
            if (_tableSig != "none")
            {
                _tableSig = "none"; _table.Clear();
                var n = L(16, Ink); n.text = "No block is running yet."; n.style.marginTop = 10; _table.Add(n);
                _logScroll.Clear(); _logSig = null;
            }
            return;
        }

        _score.text = bot.LiveScore.ToString("0.0");
        bot.GetHighScore(out float high, out string highWho);
        _highValue.text = high >= 0f ? high.ToString("0.0") : "-";
        _whoLabel.text = high >= 0f ? "by " + highWho : "";
        _sub.text = (bot.ObserveOnly ? $"YOUR BLOCK {bot.BlockNo - 1000}" : $"BLOCK {bot.BlockNo}")
                    + (bot.Running ? $"  DAY {Mathf.Min(bot.BlockDay, bot.S.daysPerBlock)}/{bot.S.daysPerBlock}" : "  DONE");

        bool summary = _tab == 0, iter = _tab == 2;
        if (_sizedForTab != _tab)
        {
            _sizedForTab = _tab;
            _win.style.height = summary ? new StyleLength(StyleKeyword.Auto) : new StyleLength(iter ? IterH : DefaultH);
            if (iter && _win.resolvedStyle.width < IterW) _win.style.width = IterW;   // three cards across need the room
        }
        _tableHost.style.display = summary ? DisplayStyle.Flex : DisplayStyle.None;
        _iterScroll.style.display = iter ? DisplayStyle.Flex : DisplayStyle.None;
        // The summary tab is the table alone (hover a row for what it means). The dark change list is the ALL CHANGES tab.
        _logPanel.style.display = (summary || iter) ? DisplayStyle.None : DisplayStyle.Flex;
        if (!summary && !iter) { _logPanel.style.height = StyleKeyword.Auto; _logPanel.style.flexGrow = 1; _logPanel.style.marginTop = 0; }

        if (summary) RenderTable(bot);
        else if (iter) RenderIterations();
        else RenderLog(bot, int.MaxValue);
    }

    static void RenderLog(PlaytestBot bot, int maxRows)
    {
        var changes = bot.RecentScoreChanges;
        int n = Mathf.Min(changes.Count, maxRows);
        string sig = _tab + "|" + n + "|" + bot.LiveScore.ToString("0.0");
        if (sig == _logSig) return;
        _logSig = sig;

        var keep = _logScroll.scrollOffset;
        _logScroll.Clear();
        if (changes.Count == 0)
        {
            var none = L(13, Pending); none.text = "No score changes yet"; none.style.height = 20; _logScroll.Add(none);
            return;
        }
        const float Gap = 24f;   // about a quarter inch between columns
        for (int i = 0; i < n; i++)
        {
            var c = changes[i];
            Color tone = c.delta >= 0 ? LogGreen : LogYellow;
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.height = 24; row.style.flexShrink = 0;

            var when = L(13, LogGreen); when.text = c.when; when.style.width = 120; when.style.marginRight = Gap;
            var amount = L(13, tone, TextAnchor.MiddleRight); amount.text = (c.delta >= 0 ? "+" : "") + c.delta.ToString("0.0"); amount.style.width = 52; amount.style.marginRight = Gap;
            var cause = L(13, tone); cause.text = c.cause; cause.style.flexGrow = 1; cause.style.flexShrink = 1; cause.style.overflow = Overflow.Hidden; cause.style.marginRight = Gap;
            var score = L(13, tone, TextAnchor.MiddleRight); score.text = "score " + c.total.ToString("0.0"); score.style.width = 92;
            row.Add(when); row.Add(amount); row.Add(cause); row.Add(score);
            _logScroll.Add(row);
        }
        _logScroll.scrollOffset = keep;
    }

    static void RenderTable(PlaytestBot bot)
    {
        var rows = bot.GetGoalRows();
        int streak = bot.PassStreak();
        bool exitOk = rows.Count > 0, anyPending = false;
        foreach (var r in rows)
            if (r.exit) { if (r.state == PlaytestBot.GoalState.Missed) exitOk = false; if (r.state == PlaytestBot.GoalState.Pending) anyPending = true; }

        var sb = new StringBuilder();
        sb.Append(streak).Append('|').Append(exitOk).Append(anyPending);
        foreach (var r in rows) sb.Append(r.name).Append(r.actual).Append((int)r.state).Append(r.why).Append(r.goal);
        string sig = sb.ToString();
        if (sig == _tableSig) return;
        _tableSig = sig;

        _table.Clear();

        string status = anyPending ? "too early" : exitOk ? "PASSING" : "FAILING";
        var banner = new VisualElement();
        banner.style.flexShrink = 0; banner.style.height = 34; banner.style.justifyContent = Justify.Center; banner.style.marginBottom = 4;
        bool ok = !anyPending && exitOk;
        banner.style.backgroundColor = ok ? new Color(0.10f, 0.35f, 0.14f, 1f) : new Color(0.46f, 0.07f, 0.07f, 1f);
        var bt = L(20, ok ? new Color(0.45f, 1f, 0.5f, 1f) : new Color(1f, 0.36f, 0.36f, 1f), TextAnchor.MiddleCenter);
        bt.text = $"CLOSE-OUT STREAK {streak} / {PlaytestBot.StreakGoal} blocks   this block: {status}";
        banner.Add(bt);
        _table.Add(banner);

        var rule = L(10, InkSoft); rule.text = "A block passes when ALL four hold: on-time >= 80%, no door deadlocks, no repeating bugs, profit > $0.";
        rule.style.whiteSpace = WhiteSpace.Normal; rule.style.marginBottom = 4; _table.Add(rule);

        _table.Add(HeaderRow());

        bool otherHeader = false;
        float totalPts = 0f, totalMax = 0f;
        foreach (var r0 in rows) if (r0.scored) { totalPts += r0.points; totalMax += r0.maxPoints; }
        foreach (var r in rows)
        {
            if (!r.exit && !otherHeader)
            {
                otherHeader = true;
                var band = new VisualElement(); band.style.flexShrink = 0; band.style.height = 24; band.style.justifyContent = Justify.Center;
                band.style.backgroundColor = Band; band.style.paddingLeft = 6; band.style.marginTop = 2;
                var bl = L(13, Ink); bl.text = "OTHER PARTS OF THE SCORE"; band.Add(bl);
                _table.Add(band);
            }
            _table.Add(Row(r));
        }

        // the total: the points above add up to the score at the top of the window
        var total = new VisualElement();
        total.style.flexDirection = FlexDirection.Row; total.style.flexShrink = 0; total.style.height = 34; total.style.alignItems = Align.Center;
        total.style.backgroundColor = Band; total.style.marginTop = 2;
        var tl = L(15, Ink); tl.text = "TOTAL SCORE"; tl.style.marginLeft = 6; tl.style.flexGrow = 1;
        var tv = L(18, Ink, TextAnchor.MiddleRight); tv.text = totalPts.ToString("0.0") + " / " + totalMax.ToString("0"); tv.style.marginRight = 14;
        total.Add(tl); total.Add(tv);
        _table.Add(total);
    }

    static VisualElement Row(PlaytestBot.GoalRow r)
    {
        Color state = r.state == PlaytestBot.GoalState.Met ? Good : r.state == PlaytestBot.GoalState.Missed ? Bad : Pending;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row; row.style.flexShrink = 0; row.style.minHeight = 42;
        if (!string.IsNullOrEmpty(r.explain)) RuntimeTooltip.Attach(row, r.explain, ignoreGlobalToggle: true);
        row.style.borderBottomWidth = 1; row.style.borderBottomColor = Grid;

        // what it is + why it is behind
        var left = new VisualElement();
        left.style.flexGrow = 5; left.style.flexBasis = 0; left.style.flexShrink = 1; left.style.justifyContent = Justify.Center;
        left.style.paddingLeft = 6; left.style.paddingTop = 2; left.style.paddingBottom = 2;
        var top = new VisualElement(); top.style.flexDirection = FlexDirection.Row; top.style.alignItems = Align.Center;
        if (r.state != PlaytestBot.GoalState.Pending)
        {
            var mark = L(15, state); mark.text = r.state == PlaytestBot.GoalState.Met ? "OK " : "X "; mark.style.marginRight = 4;
            top.Add(mark);
        }
        var name = L(16, Ink); name.text = r.name; top.Add(name);
        left.Add(top);
        var why = L(10, InkSoft); why.text = r.why ?? ""; why.style.whiteSpace = WhiteSpace.Normal;
        left.Add(why);
        row.Add(left);

        // goal
        var mid = Cell(3f);
        mid.style.borderLeftWidth = 1; mid.style.borderLeftColor = Grid;
        var goal = L(14, Ink, TextAnchor.MiddleCenter); goal.text = "goal " + r.goal; goal.style.whiteSpace = WhiteSpace.Normal;
        mid.Add(goal);
        row.Add(mid);

        // actual
        var right = Cell(3.4f);
        right.style.borderLeftWidth = 1; right.style.borderLeftColor = Grid;
        var act = L(16, state, TextAnchor.MiddleCenter); act.text = r.actual; act.style.whiteSpace = WhiteSpace.Normal;
        right.Add(act);
        row.Add(right);

        // score points this row is earning (of the most it can earn)
        var pts = Cell(2.4f);
        pts.style.borderLeftWidth = 1; pts.style.borderLeftColor = Grid;
        if (r.scored)
        {
            float frac = r.maxPoints > 0.01f ? r.points / r.maxPoints : 0f;
            Color pc = frac >= 0.8f ? Good : frac < 0.4f ? Bad : new Color(0.80f, 0.48f, 0.02f, 1f);
            var pl = L(16, pc, TextAnchor.MiddleCenter); pl.text = r.points.ToString("0.0") + " / " + r.maxPoints.ToString("0.#");
            pts.Add(pl);
        }
        else
        {
            var pl = L(10, Pending, TextAnchor.MiddleCenter); pl.text = "feeds Reliability"; pl.style.whiteSpace = WhiteSpace.Normal;
            pts.Add(pl);
        }
        row.Add(pts);
        return row;
    }

    static VisualElement HeaderRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row; row.style.flexShrink = 0; row.style.height = 24;
        row.style.backgroundColor = Band; row.style.borderBottomWidth = 1; row.style.borderBottomColor = Grid;

        VisualElement Head(string text, float grow, TextAnchor align, string tip)
        {
            var c = Cell(grow);
            c.style.borderLeftWidth = grow == 5f ? 0 : 1; c.style.borderLeftColor = Grid;
            var l = L(11, Ink, align); l.text = text;
            if (grow == 5f) { c.style.alignItems = Align.FlexStart; c.style.paddingLeft = 6; }
            c.Add(l);
            if (!string.IsNullOrEmpty(tip)) RuntimeTooltip.Attach(c, tip, ignoreGlobalToggle: true);
            return c;
        }

        row.Add(Head("WHAT IT MEASURES", 5f, TextAnchor.MiddleLeft, null));
        row.Add(Head("GOAL", 3f, TextAnchor.MiddleCenter, null));
        row.Add(Head("ACTUAL", 3.4f, TextAnchor.MiddleCenter, null));
        row.Add(Head("SCORE POINTS", 2.4f, TextAnchor.MiddleCenter,
            "SCORE POINTS" + (char)10 + "How many of the 100 score points each row is earning right now, out of the most it can earn. " +
            "A row that is fully on target earns its maximum; one that is behind earns part of it. " +
            "The rows add up to the TOTAL SCORE at the bottom, which is the score shown at the top of the window. " +
            "Door deadlocks and repeating bugs have no points of their own: they pull down Reliability."));
        return row;
    }

    static VisualElement Cell(float grow)
    {
        var c = new VisualElement();
        c.style.flexGrow = grow; c.style.flexBasis = 0; c.style.flexShrink = 1;
        c.style.justifyContent = Justify.Center; c.style.alignItems = Align.Center;
        c.style.paddingLeft = c.style.paddingRight = 4;
        return c;
    }


    // -- hover explanations for the ITERATIONS cards: shown in a panel just outside the window's right edge --
    static VisualElement _explain;
    static Label _explainTitle, _explainBody;

    static readonly Dictionary<string, string> CardText = new Dictionary<string, string>
    {
        ["SCORE"] = "The block's overall HAL score out of 100. It is eight weighted parts added together: on-time delivery, fill rate, pallets shipped, revenue, profit, cash health, reliability and reputation. The SUMMARY tab shows exactly how many points each part earned. Higher is better.",
        ["ON-TIME DELIVERY"] = "The share of orders that went out on time. On-time % = orders shipped on time / (orders shipped on time + orders fined for being late). An order is late only when a fine is charged: a recurring order's 4-hour dock block ends with the freight still here, or a bulk order passes its due day. A late order that ships afterwards counts once, as late. Moving a trailer off the customer's requested hour is a separate fine and is not counted as late. Higher is better.",
        ["PALLETS SHIPPED"] = "How many pallets left on outbound trailers during the block. The goal is a daily rate (set in the HAL settings) times the days in the block. Higher is better.",
        ["REVENUE"] = "Income earned during the block: the goods you sold plus handling fees. It does not subtract anything you spent. Higher is better.",
        ["CASH CHANGE"] = "Cash at the end of the block minus cash at the start. It can be very negative when you spend on stock: that money is now inventory on your shelves, not lost. The score judges cash plus stock on hand together, so read this next to the profit row on the SUMMARY tab. Higher is better.",
        ["BUG INCIDENTS"] = "How many separate bug episodes HAL logged during the block (stuck vehicles, pallets with no task, repeating errors, deadlocks and so on). The same bug coming back later counts again. Lower is better.",
        ["RELIABILITY"] = "A 0 to 100 grade for how clean the block was. It starts at 100 and drops with every bug incident; the same bug repeating costs more each time. It is worth 25 of the 100 score points. Higher is better.",
        ["REPUTATION"] = "Your standing with customers and vendors at the end of the block (0 to 1000). Late and cancelled orders lower it, clean deliveries raise it, and it decides which customers and vendors will deal with you. Higher is better.",
        ["HEADCOUNT"] = "How many receivers, reach truck operators, dock stockers and order selectors were on the crew. Shown for context: if everything else is equal, a smaller crew is cheaper to run, so down is counted as better.",
        ["DOCK DOORS"] = "How many shipping doors the warehouse had. More doors means more trailers can be handled at once, but each door and its lanes cost money and floor space.",
        ["JOBS WAITING (AVG)"] = "The average number of tasks sitting in the Work Queue waiting for someone (receiving, putaway and replenishment, picking, loading and orders), sampled all through the block. A high number means the crew cannot keep up. Lower is better.",
        ["CREW BUSY (AVG)"] = "The average share of time the crew spent actually working. Very high means people are stretched and work will back up; very low means there is spare capacity. A middle-to-high number with few jobs waiting is the healthy picture.",
    };

    static void BuildExplainPanel(VisualElement layer)
    {
        _explain = new VisualElement { pickingMode = PickingMode.Ignore };
        _explain.style.position = Position.Absolute;
        _explain.style.width = 300;
        _explain.style.backgroundColor = Paper;
        _explain.style.paddingLeft = _explain.style.paddingRight = 14; _explain.style.paddingTop = 10; _explain.style.paddingBottom = 12;
        Border(_explain, 3, Purple); Radius(_explain, 8);
        _explain.style.display = DisplayStyle.None;
        _explainTitle = L(18, Ink); _explainTitle.style.whiteSpace = WhiteSpace.Normal;
        _explainBody = L(14, InkSoft); _explainBody.style.whiteSpace = WhiteSpace.Normal; _explainBody.style.marginTop = 6;
        _explain.Add(_explainTitle); _explain.Add(_explainBody);
        layer.Add(_explain);
    }

    static void ShowExplain(string title)
    {
        if (_win == null || _win.panel == null) return;
        if (_explain == null || _explain.panel == null) BuildExplainPanel(_win.parent);
        string body;
        if (!CardText.TryGetValue(title, out body)) return;
        _explainTitle.text = title;
        _explainBody.text = body + "  " + "Each dot is one finished block (the number underneath is the block). Green means better than the block before, red means worse.";
        float left = _win.resolvedStyle.left + _win.resolvedStyle.width + 10f;
        float top = _win.resolvedStyle.top + 170f;
        _explain.style.left = left; _explain.style.top = top;
        _explain.style.display = DisplayStyle.Flex;
        _explain.BringToFront();
    }

    static void HideExplain()
    {
        if (_explain != null) _explain.style.display = DisplayStyle.None;
    }

    // -- METRICS tab: time studies from the floor, with a suggested standard for each job ------

    static string Mins(float m) => m <= 0f ? "-" : (m < 10f ? m.ToString("0.0") : m.ToString("0")) + " min";

    static string RateText(OpsMetrics.Kind k, OpsMetrics.Stats s)
    {
        if (s.n == 0) return "-";
        switch (k)
        {
            case OpsMetrics.Kind.CasePick: return s.perHourAvg.ToString("0") + " cases/hr";
            case OpsMetrics.Kind.PalletPick: return s.perHourAvg.ToString("0.0") + " picks/hr";
            case OpsMetrics.Kind.OffloadCycle:
            case OpsMetrics.Kind.Receive:
            case OpsMetrics.Kind.LoadCycle: return s.perHourAvg.ToString("0.0") + " pallets/hr";
            case OpsMetrics.Kind.LoadTotal:
            case OpsMetrics.Kind.Offload: return s.perHourAvg.ToString("0.0") + " pallets/hr";
            default: return "-";
        }
    }

    static void RenderMetrics()
    {
        string sig = OpsMetrics.Version.ToString();
        if (sig == _metricsSig) return;
        _metricsSig = sig;

        var keep = _metricsScroll.scrollOffset;
        _metricsScroll.Clear();

        var title = L(30, Ink); title.text = "METRICS"; title.style.marginTop = 2;
        _metricsScroll.Add(title);
        var sub = L(12, InkSoft); sub.style.whiteSpace = WhiteSpace.Normal;
        sub.text = "Time studies as an industrial engineer would take them: every finished job is timed in game minutes. STANDARD = average plus a 15% allowance for personal time, fatigue and delay.";
        sub.style.marginBottom = 8;
        _metricsScroll.Add(sub);

        float[] widths = { 0f, 52f, 74f, 74f, 74f, 84f, 118f };   // first column flexes
        string[] heads = { "JOB", "N", "AVG", "BEST", "WORST", "STANDARD", "RATE" };
        VisualElement Row(Color bg, bool header)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0;
            r.style.backgroundColor = bg; r.style.paddingTop = r.style.paddingBottom = header ? 4 : 6; r.style.paddingLeft = r.style.paddingRight = 6;
            return r;
        }
        Label Cell(string text, int col, Color c, float size, bool header = false)
        {
            var l = L(size, c, col == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            l.text = text;
            if (col == 0) { l.style.flexGrow = 1; l.style.flexShrink = 1; l.style.whiteSpace = WhiteSpace.Normal; }
            else l.style.width = widths[col];
            return l;
        }

        var hr = Row(Band, true);
        for (int i = 0; i < heads.Length; i++) hr.Add(Cell(heads[i], i, Ink, 12, true));
        _metricsScroll.Add(hr);

        bool alt = false;
        foreach (var info in OpsMetrics.All)
        {
            var s = OpsMetrics.Get(info.kind);
            var row = Row(alt ? new Color(0.96f, 0.96f, 0.96f, 1f) : new Color(1f, 1f, 1f, 1f), false); alt = !alt;
            var name = new VisualElement(); name.style.flexGrow = 1; name.style.flexShrink = 1;
            var t = L(15, Ink); t.text = info.title; name.Add(t);
            var d = L(11, InkSoft); d.text = info.what; d.style.whiteSpace = WhiteSpace.Normal; name.Add(d);
            row.Add(name);
            row.Add(Cell(s.n.ToString(), 1, InkSoft, 14));
            row.Add(Cell(s.n > 0 ? Mins(s.avg) : "-", 2, Ink, 14));
            row.Add(Cell(s.n > 0 ? Mins(s.best) : "-", 3, Good, 14));
            row.Add(Cell(s.n > 0 ? Mins(s.worst) : "-", 4, Bad, 14));
            row.Add(Cell(s.n > 0 ? Mins(s.standard) : "-", 5, ScorePurple, 15));
            row.Add(Cell(RateText(info.kind, s), 6, Ink, 13));
            _metricsScroll.Add(row);
        }

        var foot = L(12, InkSoft); foot.style.whiteSpace = WhiteSpace.Normal; foot.style.marginTop = 10;
        foot.text = "Once these settle they become the labor standards, and the goals that partnership, reputation and the Reliability score are measured against. Samples are kept between sessions (last 300 per job).";
        _metricsScroll.Add(foot);

        var clear = L(13, Color.white, TextAnchor.MiddleCenter); clear.text = "CLEAR ALL SAMPLES"; clear.pickingMode = PickingMode.Position;
        clear.style.height = 28; clear.style.width = 170; clear.style.marginTop = 8; clear.style.backgroundColor = new Color(0.72f, 0.12f, 0.12f, 1f); Radius(clear, 4);
        clear.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; OpsMetrics.Clear(); _metricsSig = null; e.StopPropagation(); });
        _metricsScroll.Add(clear);

        _metricsScroll.scrollOffset = keep;
    }

    // -- ITERATIONS tab: one card per measure, one point per finished block ------

    static void RenderIterations()
    {
        var blocks = HalDevTabs.LoadBlocks();
        var ledger = HalPlanner.LoadLedger(PlaytestBot.Ensure().S);
        string sig = blocks.Count + "|" + (blocks.Count > 0 ? blocks[blocks.Count - 1].score.ToString("0.00") : "") + "|" + ledger.changes.Count + "|" + _tab;
        if (sig == _iterSig) return;
        _iterSig = sig;

        var keep = _iterScroll.scrollOffset;
        _iterScroll.Clear();

        var title = L(30, Ink); title.text = "ITERATIONS"; title.style.marginTop = 2;
        _iterScroll.Add(title);
        var sub = L(12, InkSoft); sub.text = "One point per finished block. Green means better than the block before, red worse."; sub.style.marginBottom = 8;
        _iterScroll.Add(sub);

        if (blocks.Count == 0)
        {
            var none = L(16, InkSoft); none.text = "NO BLOCKS YET. Finish a block with HAL 9000 and its results will be drawn here."; none.style.marginTop = 10;
            _iterScroll.Add(none);
            return;
        }

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = Wrap.Wrap;
        foreach (var m in HalDevTabs.Metrics())
        {
            var vals = new List<float>();
            foreach (var b in blocks) vals.Add(m.get(b));
            float last = vals[vals.Count - 1];
            Color accent = Color.Lerp(m.color, Color.black, 0.28f);

            var card = new VisualElement();
            string cardTitle = m.title;
            card.RegisterCallback<PointerEnterEvent>(_ => ShowExplain(cardTitle));
            card.RegisterCallback<PointerLeaveEvent>(_ => HideExplain());
            card.style.width = Length.Percent(32.6f); card.style.marginRight = Length.Percent(0.7f); card.style.marginBottom = 8;
            card.style.backgroundColor = new Color(0.97f, 0.97f, 0.97f, 1f);
            card.style.paddingLeft = card.style.paddingRight = 8; card.style.paddingTop = 4; card.style.paddingBottom = 6;
            card.style.borderTopWidth = 3; card.style.borderTopColor = accent;
            card.style.borderBottomWidth = 1; card.style.borderLeftWidth = 1; card.style.borderRightWidth = 1;
            card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = new Color(0.8f, 0.8f, 0.8f, 1f);

            var head = new VisualElement(); head.style.flexDirection = FlexDirection.Row; head.style.justifyContent = Justify.SpaceBetween; head.style.alignItems = Align.Center;
            var t = L(13, Ink); t.text = m.title; t.style.flexShrink = 1;
            var v = L(20, Ink, TextAnchor.MiddleRight); v.text = m.fmt(last);
            head.Add(t); head.Add(v); card.Add(head);

            string delta = "no change"; Color dc = InkSoft;
            if (vals.Count > 1)
            {
                float d = last - vals[vals.Count - 2];
                bool better = m.higherIsBetter ? d > 0 : d < 0;
                if (Mathf.Abs(d) > 0.0001f) { delta = (d > 0 ? "up " : "down ") + m.fmt(Mathf.Abs(d)); dc = better ? Good : Bad; }
            }
            else delta = "first block";
            var dl = L(11, dc); dl.text = delta; card.Add(dl);

            var chart = new IterChart(vals, accent, m.fmt);
            chart.style.height = 92; chart.style.marginTop = 2;
            card.Add(chart);
            grid.Add(card);
        }
        _iterScroll.Add(grid);

        // what HAL changed between blocks, and what happened to the score afterwards
        var band = new VisualElement(); band.style.height = 24; band.style.justifyContent = Justify.Center; band.style.backgroundColor = Band; band.style.paddingLeft = 6; band.style.marginTop = 4;
        var bl = L(13, Ink); bl.text = "WHAT HAL CHANGED"; band.Add(bl);
        _iterScroll.Add(band);
        if (ledger.changes.Count == 0)
        {
            var nl = L(12, InkSoft); nl.text = "Nothing yet. HAL makes one change before each block after the first."; nl.style.marginTop = 4; nl.style.whiteSpace = WhiteSpace.Normal;
            _iterScroll.Add(nl);
        }
        int shown = 0;
        for (int i = ledger.changes.Count - 1; i >= 0 && shown < 12; i--, shown++)
        {
            var c = ledger.changes[i];
            string outcome = c.measured ? $"score {c.scoreBefore:0.0} to {c.scoreAfter:0.0} ({c.scoreAfter - c.scoreBefore:+0.0;-0.0;0.0})" : "waiting for that block to finish";
            Color col = c.measured ? (c.scoreAfter > c.scoreBefore ? Good : Bad) : Ink;
            var line = L(12, col); line.text = $"Block {c.block}: {c.action}. {c.reason}. {outcome}.";
            line.style.whiteSpace = WhiteSpace.Normal; line.style.marginTop = 4;
            _iterScroll.Add(line);
        }
        _iterScroll.scrollOffset = keep;
    }

    /// <summary>A small line chart on a light card: a dot on every block, the high and low labelled, block numbers underneath.</summary>
    class IterChart : VisualElement
    {
        const float PadFrac = 22f;
        readonly List<float> _v; readonly Color _col; readonly float _min, _max;

        public IterChart(List<float> values, Color col, System.Func<float, string> fmt)
        {
            _v = values; _col = col;
            float max = float.MinValue, min = float.MaxValue;
            foreach (var x in values) { if (x > max) max = x; if (x < min) min = x; }
            if (Mathf.Approximately(max, min)) { max += 1f; min -= Mathf.Approximately(min, 0f) ? 0f : 1f; }
            _min = min; _max = max;
            generateVisualContent += Draw;

            Label Small() { var l = L(10, Ink); l.pickingMode = PickingMode.Ignore; l.style.position = Position.Absolute; return l; }
            var top = Small(); top.text = fmt(max); top.style.left = 2; top.style.top = 0; Add(top);
            var bot = Small(); bot.text = fmt(min); bot.style.left = 2; bot.style.bottom = 14; Add(bot);
            for (int i = 0; i < values.Count; i++)
            {
                var n = Small(); n.text = (i + 1).ToString();
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
            p.lineWidth = 1f; p.strokeColor = new Color(0f, 0f, 0f, 0.18f);
            for (int i = 0; i <= 2; i++) { float y = Mathf.Lerp(y1, y0, i / 2f); p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.Stroke(); }
            Vector2 Pt(int i) => new Vector2(_v.Count == 1 ? (x0 + x1) * 0.5f : Mathf.Lerp(x0, x1, i / (float)(_v.Count - 1)), Mathf.Lerp(y1, y0, Mathf.InverseLerp(_min, _max, _v[i])));
            p.lineWidth = 2.5f; p.strokeColor = _col; p.lineJoin = LineJoin.Round;
            if (_v.Count > 1) { p.BeginPath(); p.MoveTo(Pt(0)); for (int i = 1; i < _v.Count; i++) p.LineTo(Pt(i)); p.Stroke(); }
            p.fillColor = _col;
            for (int i = 0; i < _v.Count; i++) { p.BeginPath(); p.Arc(Pt(i), i == _v.Count - 1 ? 5f : 3.5f, 0f, 360f); p.Fill(); }
        }
    }
}
