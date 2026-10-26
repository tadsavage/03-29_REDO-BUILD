using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Diagnostics;

/// <summary>
/// HAL 9000's SUMMARY window: the close-out conditions (goal / actual / what is pulling it back, plus the pass streak) and the full list
/// of score changes. Opened by double-clicking the HAL score box (the purple one at the top of the screen while you play, or the one in
/// the bottom bar while HAL plays). Drag the title bar to move it, drag the bottom-right corner to resize it, X to close.
/// </summary>
public static class HalSummaryWindow
{
    const float HeaderH = 30f;
    static VisualElement _win, _head, _grip, _tabs;
    static Label _title, _score, _sub, _close, _tabChanges, _tabSummary;
    static ScrollView _list;
    static int _tab = 1;                   // 0 = every score change, 1 = summary
    static string _shownSig;
    static int _shownCount = -1; static float _shownTotal = -1f;
    static readonly List<Label> _rows = new();
    static readonly Color Blue = new Color(0.25f, 0.65f, 1f), Good = new Color(0.35f, 0.9f, 0.45f), Bad = new Color(1f, 0.4f, 0.35f), Dim = new Color(0.62f, 0.74f, 0.84f), Purple = new Color(0.72f, 0.40f, 1f);

    public static bool IsOpen => _win != null && _win.panel != null && _win.style.display == DisplayStyle.Flex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { _win = null; _shownSig = null; _shownCount = -1; }

    public static void Toggle() { if (IsOpen) Close(); else Open(); }

    public static void Open()
    {
        var layer = ShippedFanfare.LayerRoot();
        if (layer == null) return;
        if (_win == null || _win.panel == null) Build(layer);
        _win.style.display = DisplayStyle.Flex;
        _win.BringToFront();
        _shownSig = null; _shownCount = -1;
        Refresh();
    }

    public static void Close()
    {
        if (_win != null) _win.style.display = DisplayStyle.None;
    }

    static Label L(int size, Color col, bool bold = false)
    {
        var l = new Label();
        l.pickingMode = PickingMode.Ignore;
        l.style.fontSize = size; l.style.color = col; l.style.whiteSpace = WhiteSpace.NoWrap; l.style.overflow = Overflow.Hidden;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (HalDevTabs.HalFont != null) l.style.unityFontDefinition = FontDefinition.FromFont(HalDevTabs.HalFont);
        l.style.flexShrink = 0;
        l.style.paddingTop = l.style.paddingBottom = l.style.paddingLeft = l.style.paddingRight = 0;
        l.style.marginTop = l.style.marginBottom = 0;
        l.style.unityTextAlign = TextAnchor.MiddleLeft;
        return l;
    }

    static void Build(VisualElement layer)
    {
        _win = new VisualElement { name = "HalSummaryWindow" };
        _win.pickingMode = PickingMode.Position;
        _win.style.position = Position.Absolute;
        _win.style.left = 380; _win.style.top = 110; _win.style.width = 600; _win.style.height = 620;
        _win.style.overflow = Overflow.Hidden;
        _win.style.backgroundColor = new Color(0.08f, 0.06f, 0.14f, 0.97f);
        _win.style.borderTopWidth = _win.style.borderBottomWidth = _win.style.borderLeftWidth = _win.style.borderRightWidth = 3;
        _win.style.borderTopColor = _win.style.borderBottomColor = _win.style.borderLeftColor = _win.style.borderRightColor = Purple;
        _win.style.borderTopLeftRadius = _win.style.borderTopRightRadius = _win.style.borderBottomLeftRadius = _win.style.borderBottomRightRadius = 8;
        _win.style.paddingLeft = _win.style.paddingRight = 12; _win.style.paddingTop = 4; _win.style.paddingBottom = 4;
        _win.style.display = DisplayStyle.None;

        _head = new VisualElement();
        _head.style.flexDirection = FlexDirection.Row; _head.style.alignItems = Align.Center;
        _head.style.height = HeaderH; _head.style.flexShrink = 0;
        _title = L(13, Purple, true); _title.text = "HAL 9000 SUMMARY"; _title.style.height = HeaderH;
        _sub = L(10, Dim); _sub.style.flexGrow = 1; _sub.style.marginLeft = 12; _sub.style.height = HeaderH;
        _score = L(24, Color.white, true); _score.style.height = HeaderH; _score.style.unityTextAlign = TextAnchor.MiddleRight; _score.style.marginRight = 12;
        _close = L(18, Color.white, true); _close.text = "X"; _close.pickingMode = PickingMode.Position;
        _close.style.width = 28; _close.style.height = 26; _close.style.unityTextAlign = TextAnchor.MiddleCenter;
        _close.style.backgroundColor = new Color(0.55f, 0.15f, 0.15f, 1f);
        _close.style.borderTopLeftRadius = _close.style.borderTopRightRadius = _close.style.borderBottomLeftRadius = _close.style.borderBottomRightRadius = 5;
        _close.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; Close(); e.StopPropagation(); });
        _head.Add(_title); _head.Add(_sub); _head.Add(_score); _head.Add(_close);
        _win.Add(_head);

        _tabs = new VisualElement();
        _tabs.style.flexDirection = FlexDirection.Row; _tabs.style.flexShrink = 0; _tabs.style.height = 22; _tabs.style.marginBottom = 4;
        _tabSummary = MakeTab("SUMMARY", 1); _tabChanges = MakeTab("ALL CHANGES", 0);
        _tabs.Add(_tabSummary); _tabs.Add(_tabChanges);
        _win.Add(_tabs);

        _list = new ScrollView(ScrollViewMode.Vertical);
        _list.style.flexGrow = 1; _list.style.flexShrink = 1;
        _list.verticalScrollerVisibility = ScrollerVisibility.Auto;
        _win.Add(_list);

        _grip = new VisualElement();
        _grip.style.position = Position.Absolute; _grip.style.right = 0; _grip.style.bottom = 0; _grip.style.width = 18; _grip.style.height = 18;
        _grip.style.backgroundColor = new Color(0.72f, 0.40f, 1f, 0.55f);
        _grip.style.borderTopLeftRadius = 6;
        _win.Add(_grip);

        WireDrag();
        WireResize();
        _win.schedule.Execute(Refresh).Every(400);
        layer.Add(_win);
    }

    static Label MakeTab(string text, int index)
    {
        var t = L(10, Dim, true);
        t.text = text; t.pickingMode = PickingMode.Position;
        t.style.height = 22; t.style.paddingLeft = t.style.paddingRight = 12; t.style.marginRight = 4;
        t.style.borderTopLeftRadius = t.style.borderTopRightRadius = 4;
        t.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; _tab = index; _shownSig = null; _shownCount = -1; e.StopPropagation(); Refresh(); });
        return t;
    }

    static void StyleTabs()
    {
        foreach (var (lab, idx) in new[] { (_tabChanges, 0), (_tabSummary, 1) })
        {
            bool on = _tab == idx;
            lab.style.backgroundColor = on ? new Color(0.72f, 0.40f, 1f, 0.35f) : new Color(1f, 1f, 1f, 0.06f);
            lab.style.color = on ? Color.white : Dim;
        }
    }

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
            _win.style.width = Mathf.Clamp(startSize.x + d.x, 380f, 1400f);
            _win.style.height = Mathf.Clamp(startSize.y + d.y, 240f, 1000f);
        });
        _grip.RegisterCallback<PointerUpEvent>(e => { down = false; if (_grip.HasPointerCapture(e.pointerId)) _grip.ReleasePointer(e.pointerId); });
    }

    static void Refresh()
    {
        if (!IsOpen) return;
        var bot = PlaytestBot.Instance;
        StyleTabs();
        if (bot == null || (!bot.Running && bot.BlockNo == 0))
        {
            _score.text = "-"; _sub.text = "no block running";
            if (_shownSig != "none") { _shownSig = "none"; _list.Clear(); var n = L(12, Dim); n.text = "No block is running yet."; n.style.height = 22; _list.Add(n); }
            return;
        }

        _score.text = bot.LiveScore.ToString("0.0");
        _sub.text = (bot.ObserveOnly ? $"YOUR BLOCK {bot.BlockNo - 1000}" : $"BLOCK {bot.BlockNo}") + (bot.Running ? $"   DAY {Mathf.Min(bot.BlockDay, bot.S.daysPerBlock)}/{bot.S.daysPerBlock}" : "   DONE");

        if (_tab == 1) { RenderSummary(bot); return; }
        _shownSig = null;

        var changes = bot.RecentScoreChanges;
        int want = changes.Count;
        if (want == _shownCount && Mathf.Approximately(_shownTotal, bot.LiveScore)) return;
        _shownCount = want; _shownTotal = bot.LiveScore;

        _list.Clear();
        if (changes.Count == 0) { var none = L(11, Dim); none.text = "No changes yet"; none.style.height = 18; _list.Add(none); return; }
        for (int i = 0; i < want; i++)
        {
            var c = changes[i];
            var r = L(11, c.delta >= 0 ? Good : Bad);
            r.style.height = 18;
            r.text = $"{c.when}   {(c.delta >= 0 ? "+" : "")}{c.delta:0.0}   {c.cause}   (score {c.total:0.0})";
            _list.Add(r);
        }
    }

    // ── SUMMARY tab: goal / actual / what is pulling it back ──────────────────

    static void RenderSummary(PlaytestBot bot)
    {
        var rows = bot.GetGoalRows();
        int streak = bot.PassStreak();
        bool exitOk = rows.Count > 0; bool anyPending = false;
        foreach (var r in rows) if (r.exit) { if (r.state == PlaytestBot.GoalState.Missed) exitOk = false; if (r.state == PlaytestBot.GoalState.Pending) anyPending = true; }

        var sb = new System.Text.StringBuilder();
        sb.Append(streak).Append('|').Append(exitOk).Append(anyPending);
        foreach (var r in rows) sb.Append(r.name).Append(r.actual).Append((int)r.state).Append(r.why);
        string sig = sb.ToString();
        if (sig == _shownSig) return;
        _shownSig = sig;

        var keep = _list.scrollOffset;
        _list.Clear();

        var banner = L(12, exitOk ? Good : Bad, true);
        banner.text = $"CLOSE-OUT STREAK  {streak} / {PlaytestBot.StreakGoal} blocks     this block: {(anyPending ? "too early" : exitOk ? "PASSING" : "FAILING")}";
        banner.style.height = 22; _list.Add(banner);
        var rule = L(9, Dim);
        rule.text = "A block passes when ALL four hold: on-time >= 80%, no door deadlocks, no repeating bugs, profit > $0.";
        rule.style.height = 16; rule.style.whiteSpace = WhiteSpace.Normal; _list.Add(rule);

        bool supportHeader = false;
        foreach (var r in rows)
        {
            if (!r.exit && !supportHeader)
            {
                supportHeader = true;
                var h = L(9, Purple, true); h.text = "OTHER PARTS OF THE SCORE"; h.style.height = 20; h.style.marginTop = 6; _list.Add(h);
            }
            Color c = r.state == PlaytestBot.GoalState.Met ? Good : r.state == PlaytestBot.GoalState.Missed ? Bad : Dim;
            var line = new VisualElement(); line.style.flexDirection = FlexDirection.Row; line.style.flexShrink = 0; line.style.height = 18; line.style.marginTop = 3;
            var dot = L(11, c, true); dot.text = r.state == PlaytestBot.GoalState.Met ? "OK " : r.state == PlaytestBot.GoalState.Missed ? "X  " : "-  "; dot.style.width = 28; dot.style.height = 18;
            var name = L(11, Color.white, true); name.text = r.name; name.style.width = 150; name.style.height = 18;
            var goal = L(10, Dim); goal.text = "goal " + r.goal; goal.style.width = 150; goal.style.height = 18;
            var act = L(11, c, true); act.text = r.actual; act.style.height = 18; act.style.flexShrink = 1;
            line.Add(dot); line.Add(name); line.Add(goal); line.Add(act);
            _list.Add(line);

            var why = L(10, Dim); why.text = "    " + (r.why ?? "");
            why.style.whiteSpace = WhiteSpace.Normal; why.style.height = StyleKeyword.Auto; why.style.minHeight = 14; why.style.marginLeft = 28;
            why.style.unityTextAlign = TextAnchor.UpperLeft;
            _list.Add(why);
        }
        _list.scrollOffset = keep;
    }
}
