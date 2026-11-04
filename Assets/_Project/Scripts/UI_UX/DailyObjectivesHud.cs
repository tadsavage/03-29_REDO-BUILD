using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Diagnostics;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// The player's DAILY OBJECTIVES (Tad, 2026-11-03):
///  - an always-on checklist at the left of the screen ("OBJECTIVE"), one line per goal, ticking as the day goes;
///  - at the end of every day a RECAP modal: how the day went goal by goal, and the week so far next to it.
///
/// Everything is read from state that is already saved (shipped orders and their close-out day, dock appointments), so it survives save/load.
/// The only session-only numbers are fines, wage/upkeep spend, lowest cash and reputation at day start, sampled here once a second; finished days
/// are kept in PlayerPrefs so the week-so-far column survives a restart of the editor. Self-bootstrapping; draws on the fanfare layer.
/// Read-only: nothing here changes how anything is scored or paid. HAL 9000 driving a block gets the checklist but never the pausing modal.
/// </summary>
public static class DailyObjectivesHud
{
    // ── the goals (one day) ───────────────────────────────────────────────────
    public const float OnTimeGoal = 0.90f, FillGoal = 0.90f;
    public const int PalletsGoal = 12, CasesGoal = 500, RevenueGoal = 8500, ProfitGoal = 1500,
                     InboundGoal = 3, OutboundGoal = 2, SameDayGoal = 1, ReputationGoal = 8, CashFloor = 15000;
    const int DaysPerWeek = 7;

    enum St { Pending, Met, Failed }

    class DayStats
    {
        public int orders, onTime, pallets, cases, revenue, gross, picked, needed, sameDay, inbound, outbound;
        public int fines, hourlyCosts, repDelta, minCash;
        public int Profit => gross - hourlyCosts - fines;
    }

    class Row { public string label, goal, actual; public St state; }

    [Serializable] class DayRecord
    {
        public int day, orders, onTime, pallets, cases, revenue, profit, fines, picked, needed, sameDay, inbound, outbound, repDelta, minCash, met, total;
    }
    [Serializable] class SaveBlob { public List<DayRecord> days = new List<DayRecord>(); }

    static readonly Color Orange = new Color(1f, 0.58f, 0.10f), Ink = new Color(0.95f, 0.97f, 1f), Dim = new Color(0.62f, 0.72f, 0.82f),
                          Gold = new Color(1f, 0.82f, 0.25f), Green = new Color(0.40f, 0.92f, 0.50f), Red = new Color(1f, 0.38f, 0.33f),
                          Navy = new Color(0.06f, 0.08f, 0.12f, 0.90f);
    const string PrefsKey = "DailyObjectives.v1", CollapsedKey = "DailyObjectives.collapsed";

    // ── state (statics outlive Play sessions: domain reload is off) ───────────
    static GameObject _driver;
    static int _trackedDay;
    static int _fines, _hourlyCosts, _minCash, _repStart;
    static SaveBlob _save = new SaveBlob();
    static VisualElement _hud, _rowsHost, _modal;
    static Label _count, _dayLabel;
    static readonly List<(VisualElement row, VisualElement box, VisualElement mark, Label label, Label progress)> _rowViews = new();
    static St[] _prev; static int _prevDay;   // last tick's row states: a Pending -> Met change flashes
    static bool _collapsed;
    static GameContext _ctx;
    static float _nextCtxFind;
    static Action _pendingRecap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        _hud = null; _rowsHost = null; _modal = null; _prev = null; _trackedDay = 0; _pendingRecap = null; _ctx = null; _rowViews.Clear();
        try { _collapsed = PlayerPrefs.GetInt(CollapsedKey, 0) == 1; } catch { }
        LoadSave();
        OrderService.OnOrderFined -= OnFined;
        OrderService.OnOrderFined += OnFined;
        if (_driver != null) UnityEngine.Object.Destroy(_driver);
        _driver = new GameObject("[DailyObjectivesHud]") { hideFlags = HideFlags.HideInHierarchy };
        _driver.AddComponent<Ticker>();
        UnityEngine.Object.DontDestroyOnLoad(_driver);
    }

    static void OnFined(OrderData o, int amount) { if (_trackedDay > 0) _fines += Mathf.Max(0, amount); }

    class Ticker : MonoBehaviour
    {
        float _next;
        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            try { Tick(); } catch (Exception e) { Debug.LogWarning("[DailyObjectivesHud] " + e.Message); _next = Time.unscaledTime + 5f; }
        }
    }

    // ── tick ──────────────────────────────────────────────────────────────────

    static bool InGame()
    {
        if (!Application.isPlaying) return false;
        if (!ServiceLocator.TryGet<SimulationTimeService>(out _) || !ServiceLocator.TryGet<MoneyService>(out _) || !ServiceLocator.TryGet<OrderService>(out _)) return false;
        if (_ctx == null && Time.unscaledTime >= _nextCtxFind) { _ctx = UnityEngine.Object.FindFirstObjectByType<GameContext>(); _nextCtxFind = Time.unscaledTime + 3f; }
        return _ctx != null;
    }

    static void Tick()
    {
        if (!InGame()) { if (_hud != null) _hud.style.display = DisplayStyle.None; _trackedDay = 0; return; }
        ServiceLocator.TryGet<SimulationTimeService>(out var clock);
        ServiceLocator.TryGet<MoneyService>(out var money);
        int day = clock.Day;

        if (_trackedDay == 0) StartDay(day, true);
        else if (day != _trackedDay)
        {
            int finished = _trackedDay;
            bool normalRollover = day == finished + 1;
            if (normalRollover) FinishDay(finished);
            StartDay(day, !normalRollover);
        }

        _hourlyCosts = Mathf.Max(_hourlyCosts, money.SpentTodayHourlyOnly);   // the money service resets this at midnight: keep the day's peak
        _minCash = Mathf.Min(_minCash, money.CurrentCapital);

        if (_pendingRecap != null && ModalFree()) { var a = _pendingRecap; _pendingRecap = null; a(); }

        var layer = ShippedFanfare.LayerRoot();
        if (layer == null) return;
        if (_hud == null || _hud.panel == null) BuildHud(layer);
        _hud.style.display = DisplayStyle.Flex;
        RefreshHud(Rows(Live(day), false), day);
    }

    static bool ModalFree()
    {
        var layer = ShippedFanfare.LayerRoot();
        return _modal == null && (layer == null || layer.Q("PlayerResultsModal") == null);
    }

    static void StartDay(int day, bool pruneHistory)
    {
        _trackedDay = day;
        _fines = 0; _hourlyCosts = 0;
        _minCash = ServiceLocator.TryGet<MoneyService>(out var m) ? m.CurrentCapital : int.MaxValue;
        _repStart = ServiceLocator.TryGet<ReputationService>(out var r) ? r.Score : 0;
        if (pruneHistory) { _save.days.RemoveAll(d => d.day >= day); SaveNow(); }   // a new game or an earlier save: later days never happened
    }

    // ── numbers ───────────────────────────────────────────────────────────────

    static DayStats Live(int day)
    {
        var s = Compute(day);
        s.fines = _fines; s.hourlyCosts = _hourlyCosts; s.minCash = _minCash;
        s.repDelta = (ServiceLocator.TryGet<ReputationService>(out var r) ? r.Score : _repStart) - _repStart;
        return s;
    }

    static DayStats Compute(int day)
    {
        var s = new DayStats();
        if (!ServiceLocator.TryGet<OrderService>(out var orders)) return s;
        var seen = new HashSet<string>();
        void Add(OrderData o)
        {
            if (o == null || o.Status != OrderData.OrderStatus.Shipped || o.ClosedDayNumber != day || !seen.Add(o.OrderId)) return;
            s.orders++;
            if (!o.HasBeenFined) s.onTime++;
            int pal = o.PalletsShipped;
            if (pal <= 0)
                foreach (var li in o.LineItems) pal += Mathf.CeilToInt(li.QuantityPicked / (float)Mathf.Max(1, orders.FullPalletCases(li.SkuId)));
            s.pallets += pal;
            s.cases += o.TotalUnitsPicked; s.picked += o.TotalUnitsPicked; s.needed += o.TotalUnits;
            s.revenue += o.ShippedRevenue; s.gross += o.ShippedProfit;
            if (o.QualifiesForSameDayBonus(o.ClosedDayNumber)) s.sameDay++;
        }
        foreach (var o in orders.OrderHistory) Add(o);
        foreach (var o in orders.ActiveOrders) Add(o);

        if (ServiceLocator.TryGet<DockScheduleService>(out var ds))
            foreach (var a in ds.Appointments)
            {
                if (a == null || a.Day != day || a.Parked || a.IsHold || !ds.IsComplete(a)) continue;
                if (a.Kind == AppointmentKind.Inbound) s.inbound++;
                else if (a.OrderIds != null && a.OrderIds.Count > 0) s.outbound++;
            }
        return s;
    }

    static string Money(int v) => (v < 0 ? "-$" : "$") + Mathf.Abs(v).ToString("N0");

    /// <summary>The twelve goal lines for one day. While the day runs a goal is Pending until it is reached; "keep it at zero" goals fail the moment they break
    /// and only count as met once the day is over.</summary>
    static List<Row> Rows(DayStats s, bool over)
    {
        St Reach(bool reached) => reached ? St.Met : (over ? St.Failed : St.Pending);
        St Hold(bool ok) => !ok ? St.Failed : (over ? St.Met : St.Pending);
        float onTimePct = s.orders > 0 ? s.onTime / (float)s.orders : 0f;
        float fill = s.needed > 0 ? s.picked / (float)s.needed : 0f;
        return new List<Row>
        {
            new Row { label = "On-time orders", goal = ">= 90%", actual = s.orders > 0 ? $"{onTimePct * 100f:0}%" : "n/a", state = Reach(s.orders > 0 && onTimePct >= OnTimeGoal) },
            new Row { label = "No late fines", goal = "$0", actual = Money(s.fines), state = Hold(s.fines == 0) },
            new Row { label = "Ship pallets", goal = PalletsGoal.ToString(), actual = s.pallets.ToString(), state = Reach(s.pallets >= PalletsGoal) },
            new Row { label = "Ship cases", goal = CasesGoal.ToString(), actual = s.cases.ToString(), state = Reach(s.cases >= CasesGoal) },
            new Row { label = "Revenue", goal = Money(RevenueGoal), actual = Money(s.revenue), state = Reach(s.revenue >= RevenueGoal) },
            new Row { label = "Net profit", goal = Money(ProfitGoal), actual = Money(s.Profit), state = Reach(s.Profit >= ProfitGoal) },
            new Row { label = "Inbound trailers", goal = InboundGoal.ToString(), actual = s.inbound.ToString(), state = Reach(s.inbound >= InboundGoal) },
            new Row { label = "Outbound trailers", goal = OutboundGoal.ToString(), actual = s.outbound.ToString(), state = Reach(s.outbound >= OutboundGoal) },
            new Row { label = "Same-day order", goal = SameDayGoal.ToString(), actual = s.sameDay.ToString(), state = Reach(s.sameDay >= SameDayGoal) },
            new Row { label = "Fill rate", goal = ">= 90%", actual = s.needed > 0 ? $"{fill * 100f:0}%" : "n/a", state = Reach(s.needed > 0 && fill >= FillGoal) },
            new Row { label = "Reputation", goal = "+" + ReputationGoal, actual = (s.repDelta >= 0 ? "+" : "") + s.repDelta, state = Reach(s.repDelta >= ReputationGoal) },
            new Row { label = "Cash above", goal = Money(CashFloor), actual = Money(s.minCash == int.MaxValue ? 0 : s.minCash), state = Hold(s.minCash >= CashFloor) },
        };
    }

    // ── day end ───────────────────────────────────────────────────────────────

    static void FinishDay(int day)
    {
        var s = Live(day);
        var rows = Rows(s, true);
        var rec = new DayRecord
        {
            day = day, orders = s.orders, onTime = s.onTime, pallets = s.pallets, cases = s.cases, revenue = s.revenue, profit = s.Profit, fines = s.fines,
            picked = s.picked, needed = s.needed, sameDay = s.sameDay, inbound = s.inbound, outbound = s.outbound, repDelta = s.repDelta,
            minCash = s.minCash, met = rows.Count(r => r.state == St.Met), total = rows.Count,
        };
        _save.days.RemoveAll(d => d.day == day);
        _save.days.Add(rec);
        if (_save.days.Count > 120) _save.days.RemoveRange(0, _save.days.Count - 120);
        SaveNow();

        var bot = PlaytestBot.Instance;
        if (bot != null && bot.Running && !bot.ObserveOnly) return;   // HAL is driving an unattended block: record, never pause it
        _pendingRecap = () => ShowRecap(day, rows, rec);
    }

    static void LoadSave()
    {
        try
        {
            string j = PlayerPrefs.GetString(PrefsKey, "");
            _save = string.IsNullOrEmpty(j) ? new SaveBlob() : (JsonUtility.FromJson<SaveBlob>(j) ?? new SaveBlob());
            if (_save.days == null) _save.days = new List<DayRecord>();
        }
        catch { _save = new SaveBlob(); }
    }

    static void SaveNow()
    {
        try { PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_save)); PlayerPrefs.Save(); } catch { }
    }

    // ── the checklist on screen ───────────────────────────────────────────────

    static Label L(int size, Color col, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var l = new Label { pickingMode = PickingMode.Ignore };
        l.style.fontSize = size; l.style.color = col; l.style.whiteSpace = WhiteSpace.NoWrap; l.style.flexShrink = 0;
        l.style.unityTextAlign = align;
        l.style.marginTop = l.style.marginBottom = l.style.marginLeft = l.style.marginRight = 0;
        l.style.paddingTop = l.style.paddingBottom = l.style.paddingLeft = l.style.paddingRight = 0;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (HalDevTabs.HalFont != null) l.style.unityFontDefinition = FontDefinition.FromFont(HalDevTabs.HalFont);
        return l;
    }

    static void Border(VisualElement e, float w, Color c)
    {
        e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
        e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
    }

    static void Round(VisualElement e, float r) =>
        e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;

    static readonly Color Soft = new Color(0.84f, 0.91f, 0.97f);
    const string PosXKey = "DailyObjectives.x", PosYKey = "DailyObjectives.y", ScaleKey = "DailyObjectives.scale";
    const float HudWidth = 412f, MinScale = 0.7f, MaxScale = 1.6f;
    static float _scale = 1f;
    static bool _pinned;   // false until the player moves or resizes it: until then it hugs the right edge

    /// <summary>Dark outline so the text reads over any part of the warehouse with no box behind it (the reference look).</summary>
    static Label Out(Label l)
    {
        l.style.unityTextOutlineWidth = 0.22f;
        l.style.unityTextOutlineColor = new Color(0.02f, 0.03f, 0.06f, 0.95f);
        return l;
    }

    static Label HeadButton(string text, Action click)
    {
        var b = L(13, Ink, true, TextAnchor.MiddleCenter);
        b.pickingMode = PickingMode.Position; b.text = text;
        b.style.width = 30; b.style.height = 22; b.style.marginLeft = 4;
        b.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f); Border(b, 2, new Color(1f, 0.58f, 0.10f, 0.9f)); Round(b, 4);
        b.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) { click(); } e.StopPropagation(); });
        return Out(b);
    }

    static void BuildHud(VisualElement layer)
    {
        _rowViews.Clear();
        float sx = PlayerPrefs.GetFloat(ScaleKey, 1f);
        _scale = Mathf.Clamp(float.IsNaN(sx) ? 1f : sx, MinScale, MaxScale);
        _hud = new VisualElement { name = "DailyObjectivesHud", pickingMode = PickingMode.Position };
        _hud.style.position = Position.Absolute; _hud.style.top = 118; _hud.style.width = HudWidth;
        _hud.style.backgroundColor = Color.clear;   // see-through: outlined text over the game
        _hud.style.paddingTop = 4; _hud.style.paddingBottom = 6; _hud.style.paddingLeft = 8; _hud.style.paddingRight = 8;
        _hud.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0));
        _hud.style.scale = new Scale(new Vector2(_scale, _scale));
        if (PlayerPrefs.HasKey(PosXKey) && PlayerPrefs.HasKey(PosYKey))
        {
            _hud.style.left = PlayerPrefs.GetFloat(PosXKey); _hud.style.top = PlayerPrefs.GetFloat(PosYKey); _pinned = true;
        }
        else { _hud.style.right = 14; _pinned = false; }

        var head = new VisualElement { pickingMode = PickingMode.Position };
        head.style.flexDirection = FlexDirection.Row; head.style.alignItems = Align.Center; head.style.height = 34; head.style.flexShrink = 0;
        var title = Out(L(26, Orange, true)); title.text = "OBJECTIVE"; title.style.flexGrow = 1; title.style.height = 34;
        _dayLabel = Out(L(13, Soft, true, TextAnchor.MiddleRight)); _dayLabel.style.width = 62; _dayLabel.style.height = 34; _dayLabel.style.marginRight = 6;
        _count = Out(L(15, Gold, true, TextAnchor.MiddleRight)); _count.style.width = 52; _count.style.height = 34;
        head.Add(title); head.Add(_dayLabel); head.Add(_count);
        head.Add(HeadButton("A-", () => Resize(-0.1f)));
        head.Add(HeadButton("A+", () => Resize(+0.1f)));

        // Drag the box by ANY part of it to move it; a click on the header that doesn't move collapses / expands the list.
        Vector2 downAt = default, hudAt = default; bool dragging = false, moved = false, onHeader = false;
        _hud.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;
            dragging = true; moved = false; downAt = e.position;
            onHeader = head.worldBound.Contains((Vector2)e.position);
            Pin();
            hudAt = new Vector2(_hud.resolvedStyle.left, _hud.resolvedStyle.top);
            _hud.CapturePointer(e.pointerId); e.StopPropagation();
        });
        _hud.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!dragging || !_hud.HasPointerCapture(e.pointerId)) return;
            Vector2 d = (Vector2)e.position - downAt;
            if (!moved && d.magnitude < 5f) return;
            moved = true;
            _hud.style.left = hudAt.x + d.x; _hud.style.top = hudAt.y + d.y;
            ClampIntoLayer();
        });
        _hud.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!dragging) return;
            dragging = false; if (_hud.HasPointerCapture(e.pointerId)) _hud.ReleasePointer(e.pointerId);
            if (moved) SavePosition();
            else if (onHeader)
            {
                _collapsed = !_collapsed;
                try { PlayerPrefs.SetInt(CollapsedKey, _collapsed ? 1 : 0); } catch { }
                if (_rowsHost != null) _rowsHost.style.display = _collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            }
            e.StopPropagation();
        });
        _hud.Add(head);

        _rowsHost = new VisualElement { pickingMode = PickingMode.Ignore };
        _rowsHost.style.marginTop = 2;
        _rowsHost.style.display = _collapsed ? DisplayStyle.None : DisplayStyle.Flex;
        _hud.Add(_rowsHost);
        layer.Add(_hud);
        _hud.BringToFront();
    }

    /// <summary>Switches from "hug the right edge" to explicit left/top so it can be dragged or scaled without sliding off-screen.</summary>
    static void Pin()
    {
        if (_pinned || _hud == null) return;
        float l = _hud.resolvedStyle.left, t = _hud.resolvedStyle.top;
        if (float.IsNaN(l) || float.IsNaN(t)) return;
        _hud.style.left = l; _hud.style.top = t; _hud.style.right = StyleKeyword.Auto; _pinned = true;
    }

    static void Resize(float delta)
    {
        if (_hud == null) return;
        Pin();
        _scale = Mathf.Clamp(Mathf.Round((_scale + delta) * 10f) / 10f, MinScale, MaxScale);
        _hud.style.scale = new Scale(new Vector2(_scale, _scale));
        try { PlayerPrefs.SetFloat(ScaleKey, _scale); PlayerPrefs.Save(); } catch { }
        ClampIntoLayer();
        SavePosition();
    }

    /// <summary>Keeps the whole box on screen after a drag, a resize or a window change.</summary>
    static void ClampIntoLayer()
    {
        if (_hud == null || !_pinned || _hud.parent == null) return;
        float lw = _hud.parent.resolvedStyle.width, lh = _hud.parent.resolvedStyle.height;
        if (float.IsNaN(lw) || float.IsNaN(lh) || lw < 50f) return;
        float w = HudWidth * _scale, h = Mathf.Max(40f, _hud.resolvedStyle.height * _scale);
        // The STYLE value, not resolvedStyle: resolved lags a layout pass behind, so reading it here undid every drag step.
        float x = Mathf.Clamp(StyleOr(_hud.style.left, _hud.resolvedStyle.left), 0f, Mathf.Max(0f, lw - w - 4f));
        float y = Mathf.Clamp(StyleOr(_hud.style.top, _hud.resolvedStyle.top), 0f, Mathf.Max(0f, lh - Mathf.Min(h, 60f)));
        _hud.style.left = x; _hud.style.top = y;
    }

    static void SavePosition()
    {
        if (_hud == null || !_pinned) return;
        try { PlayerPrefs.SetFloat(PosXKey, StyleOr(_hud.style.left, _hud.resolvedStyle.left)); PlayerPrefs.SetFloat(PosYKey, StyleOr(_hud.style.top, _hud.resolvedStyle.top)); PlayerPrefs.Save(); } catch { }
    }

    static float StyleOr(StyleLength s, float fallback) => s.keyword == StyleKeyword.Undefined ? s.value.value : fallback;

    static (VisualElement row, VisualElement box, VisualElement mark, Label label, Label progress) AddRowView()
    {
        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.height = 28; row.style.flexShrink = 0;
        Round(row, 5);
        var box = new VisualElement { pickingMode = PickingMode.Ignore };
        box.style.width = 22; box.style.height = 22; box.style.flexShrink = 0; Border(box, 3, Ink); box.style.marginRight = 10; box.style.marginLeft = 3;
        box.style.backgroundColor = new Color(0f, 0f, 0f, 0.28f); Round(box, 3);
        var mark = new VisualElement { pickingMode = PickingMode.Ignore };
        mark.style.position = Position.Absolute; mark.style.display = DisplayStyle.None;
        box.Add(mark);
        var label = Out(L(16, Ink, true)); label.style.width = 205; label.style.height = 28; label.style.overflow = Overflow.Hidden;   // fixed columns: every value lines up
        var prog = Out(L(14, Soft, false, TextAnchor.MiddleRight)); prog.style.width = 150; prog.style.height = 28; prog.style.flexGrow = 1;
        row.Add(box); row.Add(label); row.Add(prog);
        _rowsHost.Add(row);
        return (row, box, mark, label, prog);
    }

    /// <summary>A goal just ticked over: its row flashes gold and a short toast says which one.</summary>
    static void Flash(VisualElement row, string label)
    {
        row.experimental.animation.Start(0f, 1f, 1300, (e, t) => e.style.backgroundColor = new Color(1f, 0.82f, 0.25f, 0.55f * (1f - t)));
        UIToast.Show("Goal met: " + label, 2.5f);
    }

    /// <summary>Met: gold box with a check mark. Failed: red box with a block. Pending: empty white box.</summary>
    static void Skin(VisualElement box, VisualElement mark, St st)
    {
        Color edge = st == St.Met ? Gold : st == St.Failed ? Red : Ink;
        Border(box, 3, edge);
        mark.style.display = st == St.Pending ? DisplayStyle.None : DisplayStyle.Flex;
        mark.style.backgroundColor = Color.clear;
        mark.style.borderTopWidth = mark.style.borderLeftWidth = 0;
        mark.style.rotate = new Rotate(new Angle(0f));
        if (st == St.Met)
        {
            mark.style.width = 6; mark.style.height = 13; mark.style.left = 5; mark.style.top = -4;
            mark.style.borderRightWidth = 3; mark.style.borderBottomWidth = 3; mark.style.borderRightColor = mark.style.borderBottomColor = Gold;
            mark.style.rotate = new Rotate(new Angle(40f));
        }
        else if (st == St.Failed)
        {
            mark.style.width = 8; mark.style.height = 8; mark.style.left = 3; mark.style.top = 3;
            mark.style.borderRightWidth = mark.style.borderBottomWidth = 0; mark.style.backgroundColor = Red;
        }
    }

    static void RefreshHud(List<Row> rows, int day)
    {
        if (_rowsHost == null) return;
        while (_rowViews.Count < rows.Count) _rowViews.Add(AddRowView());
        int met = 0;
        bool track = _prev != null && _prev.Length == rows.Count && _prevDay == day;
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i]; var v = _rowViews[i];
            if (r.state == St.Met) met++;
            if (track && _prev[i] != St.Met && r.state == St.Met) Flash(v.row, r.label);
            v.label.text = r.label;
            v.label.style.color = r.state == St.Met ? Gold : r.state == St.Failed ? Red : Ink;
            v.progress.text = r.actual + "  /  " + r.goal;
            Skin(v.box, v.mark, r.state);
        }
        _prev = rows.Select(x => x.state).ToArray(); _prevDay = day;
        _count.text = met + "/" + rows.Count;
        _dayLabel.text = "DAY " + day;
        ClampIntoLayer();
        _hud.BringToFront();
    }

    // ── the recap modal ───────────────────────────────────────────────────────

    static void ShowRecap(int day, List<Row> todayRows, DayRecord today)
    {
        var layer = ShippedFanfare.LayerRoot();
        if (layer == null) return;
        _modal?.RemoveFromHierarchy();

        float prevScale = Time.timeScale;
        var topBar = UnityEngine.Object.FindAnyObjectByType<TopBarUI>();
        if (topBar != null) topBar.SetGameSpeed(0f); else Time.timeScale = 0f;

        _modal = new VisualElement { name = "DailyRecapModal", pickingMode = PickingMode.Position };
        _modal.style.position = Position.Absolute;
        _modal.style.left = 0; _modal.style.right = 0; _modal.style.top = 0; _modal.style.bottom = 0;
        _modal.style.backgroundColor = new Color(0f, 0f, 0f, 0.66f);
        _modal.style.alignItems = Align.Center; _modal.style.justifyContent = Justify.Center;

        var card = new VisualElement();
        card.style.backgroundColor = new Color(0.07f, 0.09f, 0.14f, 0.98f); Border(card, 4, Orange); Round(card, 14);
        card.style.paddingTop = 18; card.style.paddingBottom = 18; card.style.paddingLeft = 26; card.style.paddingRight = 26;
        card.style.alignItems = Align.Center;
        _modal.Add(card);

        var title = L(34, Orange, true, TextAnchor.MiddleCenter); title.text = $"DAY {day} COMPLETE"; title.style.height = 44; card.Add(title);
        var sub = L(18, today.met >= today.total - 2 ? Green : today.met >= today.total / 2 ? Gold : Red, true, TextAnchor.MiddleCenter);
        sub.text = $"{today.met} of {today.total} goals met"; sub.style.height = 28; sub.style.marginBottom = 10; card.Add(sub);

        var cols = new VisualElement(); cols.style.flexDirection = FlexDirection.Row; cols.style.alignItems = Align.FlexStart; card.Add(cols);

        // today
        var left = new VisualElement(); left.style.marginRight = 34; cols.Add(left);
        left.Add(Heading("TODAY"));
        left.Add(TableRow("GOAL", "TARGET", "ACTUAL", "", Dim, true));
        foreach (var r in todayRows)
            left.Add(TableRow(r.label, r.goal, r.actual, r.state == St.Met ? "MET" : r.state == St.Failed ? "MISSED" : "-", r.state == St.Met ? Green : r.state == St.Failed ? Red : Ink, false));

        // week so far
        int weekStart = ((day - 1) / DaysPerWeek) * DaysPerWeek + 1;
        var week = _save.days.Where(d => d.day >= weekStart && d.day <= day).OrderBy(d => d.day).ToList();
        int n = week.Count;
        var right = new VisualElement(); cols.Add(right);
        right.Add(Heading($"WEEK {(day - 1) / DaysPerWeek + 1} SO FAR  -  {n} of {DaysPerWeek} days"));
        right.Add(TableRow("MEASURE", "PACE", "SO FAR", "", Dim, true));
        foreach (var r in WeekRows(week, n)) right.Add(TableRow(r.label, r.goal, r.actual, r.state == St.Met ? "ON PACE" : "BEHIND", r.state == St.Met ? Green : Red, false));

        var strip = L(13, Dim, false, TextAnchor.MiddleCenter);
        strip.text = "Goals met per day:   " + string.Join("   ", week.Select(d => $"D{d.day} {d.met}/{d.total}"));
        strip.style.height = 22; strip.style.marginTop = 10; card.Add(strip);

        var btn = new Button(() =>
        {
            _modal?.RemoveFromHierarchy(); _modal = null;
            if (prevScale > 0f) { if (topBar != null) topBar.SetGameSpeed(prevScale); else Time.timeScale = prevScale; }   // stays paused if the player had paused
        }) { text = "CONTINUE" };
        btn.style.height = 46; btn.style.width = 300; btn.style.marginTop = 12; btn.style.fontSize = 22; btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.backgroundColor = Orange; btn.style.color = Color.white; Round(btn, 8);
        if (HalDevTabs.HalFont != null) btn.style.unityFontDefinition = FontDefinition.FromFont(HalDevTabs.HalFont);
        card.Add(btn);

        layer.Add(_modal);
        _modal.BringToFront();
    }

    /// <summary>The week column: each daily goal times the days played so far, against what the saved days add up to. Ratios are weighted by volume.</summary>
    static List<Row> WeekRows(List<DayRecord> w, int n)
    {
        int orders = w.Sum(d => d.orders), onTime = w.Sum(d => d.onTime), picked = w.Sum(d => d.picked), needed = w.Sum(d => d.needed);
        int fines = w.Sum(d => d.fines), rep = w.Sum(d => d.repDelta);
        int lowCash = n > 0 ? w.Min(d => d.minCash) : 0;
        float onT = orders > 0 ? onTime / (float)orders : 0f, fill = needed > 0 ? picked / (float)needed : 0f;
        St Pace(int have, int goal) => have >= goal * n ? St.Met : St.Failed;
        return new List<Row>
        {
            new Row { label = "On-time orders", goal = ">= 90%", actual = orders > 0 ? $"{onT * 100f:0}%" : "n/a", state = orders > 0 && onT >= OnTimeGoal ? St.Met : St.Failed },
            new Row { label = "Late fines", goal = "$0", actual = Money(fines), state = fines == 0 ? St.Met : St.Failed },
            new Row { label = "Pallets", goal = (PalletsGoal * n).ToString(), actual = w.Sum(d => d.pallets).ToString(), state = Pace(w.Sum(d => d.pallets), PalletsGoal) },
            new Row { label = "Cases", goal = (CasesGoal * n).ToString(), actual = w.Sum(d => d.cases).ToString(), state = Pace(w.Sum(d => d.cases), CasesGoal) },
            new Row { label = "Revenue", goal = Money(RevenueGoal * n), actual = Money(w.Sum(d => d.revenue)), state = Pace(w.Sum(d => d.revenue), RevenueGoal) },
            new Row { label = "Net profit", goal = Money(ProfitGoal * n), actual = Money(w.Sum(d => d.profit)), state = Pace(w.Sum(d => d.profit), ProfitGoal) },
            new Row { label = "Inbound trailers", goal = (InboundGoal * n).ToString(), actual = w.Sum(d => d.inbound).ToString(), state = Pace(w.Sum(d => d.inbound), InboundGoal) },
            new Row { label = "Outbound trailers", goal = (OutboundGoal * n).ToString(), actual = w.Sum(d => d.outbound).ToString(), state = Pace(w.Sum(d => d.outbound), OutboundGoal) },
            new Row { label = "Same-day orders", goal = (SameDayGoal * n).ToString(), actual = w.Sum(d => d.sameDay).ToString(), state = Pace(w.Sum(d => d.sameDay), SameDayGoal) },
            new Row { label = "Fill rate", goal = ">= 90%", actual = needed > 0 ? $"{fill * 100f:0}%" : "n/a", state = needed > 0 && fill >= FillGoal ? St.Met : St.Failed },
            new Row { label = "Reputation", goal = "+" + (ReputationGoal * n), actual = (rep >= 0 ? "+" : "") + rep, state = Pace(rep, ReputationGoal) },
            new Row { label = "Lowest cash", goal = "> " + Money(CashFloor), actual = Money(lowCash), state = lowCash >= CashFloor ? St.Met : St.Failed },
        };
    }

    static VisualElement Heading(string text)
    {
        var l = L(19, Gold, true); l.text = text; l.style.height = 28; l.style.marginBottom = 4; return l;
    }

    static VisualElement TableRow(string a, string b, string c, string d, Color col, bool bold)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.height = 28; row.style.flexShrink = 0;
        Label Cell(string t, float w, TextAnchor al) { var l = L(bold ? 14 : 16, col, bold, al); l.text = t; l.style.width = w; l.style.height = 28; return l; }
        row.Add(Cell(a, 190, TextAnchor.MiddleLeft));
        row.Add(Cell(b, 100, TextAnchor.MiddleRight));
        row.Add(Cell(c, 120, TextAnchor.MiddleRight));
        row.Add(Cell(d, 90, TextAnchor.MiddleRight));
        return row;
    }
}
