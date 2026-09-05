using GameCore.Economy;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static FinanceUIKit;

/// Per-category expense breakdown with inline drill-down — triggered by the "Hourly" label in
/// TopBarUI. Revenue and Net Profit live on the Capital tab (CapitalSummaryPanel) instead. Click
/// an expandable expense row to expand its sub-breakdown directly below it, the same inline-list
/// pattern SpentTodayPanel uses for "Purchased Today" — no floating tooltip.
public class FinancialBreakdownPanel : ITopBarPanel
{
    // ── State ─────────────────────────────────────────────────────────────────
    readonly VisualElement _panel;
    readonly VisualElement _trigger;
    readonly MoneyService  _money;
    readonly Dictionary<string, Label> _expenseValues = new();

    // One entry per expandable category: its row, the inline detail list below it (initially
    // collapsed), and whether it's currently expanded — so Refresh() can keep an open detail's
    // numbers live without needing the row to be re-clicked.
    class ExpandableRow
    {
        public string Category;
        public VisualElement Row;
        public Label Arrow;
        public VisualElement Detail;
        public bool Expanded;
    }
    readonly List<ExpandableRow> _expandables = new();

    // Caps the scroll area so the 19-category list can't push the panel past the bottom bar —
    // the literal "spread every row out" version of this panel ran off the bottom of the screen
    // (see the 2026-09 TopBar font pass). The scrollbar lives on the LEFT per Tad's request.
    const float ScrollMaxHeight = 620f;

    Button _expandCollapseBtn;
    bool   _allExpanded;

    Label _expenseTotalLabel;
    bool  _visible;

    // ── Construction ─────────────────────────────────────────────────────────
    public FinancialBreakdownPanel(VisualElement root, MoneyService money, VisualElement trigger = null)
    {
        _money = money;
        _trigger = trigger;
        _panel = Build();
        _panel.style.display = DisplayStyle.None;
        root.Add(_panel);
        _money.OnMoneyChanged += Refresh;
        Refresh();
    }

    // ── Public API ────────────────────────────────────────────────────────────
    public bool IsVisible => _visible;
    public VisualElement Root => _panel;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        PositionUnderTrigger(_panel, _trigger);
        _panel.style.display = DisplayStyle.Flex;
        Refresh();
    }

    public void Hide()
    {
        _visible = false;
        _panel.style.display = DisplayStyle.None;
    }

    public void Dispose()
    {
        if (_money != null) _money.OnMoneyChanged -= Refresh;
        if (_panel.parent != null) _panel.RemoveFromHierarchy();
    }

    // ── Build ─────────────────────────────────────────────────────────────────
    VisualElement Build()
    {
        var panel = Panel();
        panel.style.width = LargeWidth;

        // ── Expenses only — Revenue/Net Profit live on the Capital tab now ──
        // Built directly (not via the shared SectionHeader) so the Expand/Collapse All button can
        // sit in the same row as the title without touching the shared helper every other panel uses.
        var header = new VisualElement();
        header.style.flexDirection  = FlexDirection.Row;
        header.style.backgroundColor = new StyleColor(ColExpenseRed);
        header.style.height         = LargeHeaderHeight;
        header.style.alignItems     = Align.Center;
        header.style.borderTopWidth = 1f;
        header.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));

        var titleLbl = Lbl("Expenses", bold: true, size: LargeHeaderSize);
        titleLbl.style.color          = new StyleColor(ColExpenseWhite);
        titleLbl.style.flexGrow       = 1f;
        titleLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
        header.Add(titleLbl);

        _expandCollapseBtn = new Button(ToggleExpandCollapseAll) { text = "Expand All" };
        _expandCollapseBtn.style.backgroundColor    = new StyleColor(ColOrange);
        _expandCollapseBtn.style.color              = new StyleColor(Color.white);
        _expandCollapseBtn.style.fontSize           = 15f;
        _expandCollapseBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        _expandCollapseBtn.style.unityTextAlign     = TextAnchor.MiddleCenter;
        _expandCollapseBtn.style.whiteSpace         = WhiteSpace.Normal; // wrap "Collapse All" at this width rather than clip
        _expandCollapseBtn.style.width              = 128f;
        _expandCollapseBtn.style.height              = LargeHeaderHeight - 8f;
        _expandCollapseBtn.style.marginRight        = 8f;
        _expandCollapseBtn.style.marginTop          = 0f;
        _expandCollapseBtn.style.marginBottom       = 0f;
        _expandCollapseBtn.style.borderTopWidth     = 0f;
        _expandCollapseBtn.style.borderBottomWidth  = 0f;
        _expandCollapseBtn.style.borderLeftWidth    = 0f;
        _expandCollapseBtn.style.borderRightWidth   = 0f;
        _expandCollapseBtn.style.borderTopLeftRadius     = 4f;
        _expandCollapseBtn.style.borderTopRightRadius    = 4f;
        _expandCollapseBtn.style.borderBottomLeftRadius  = 4f;
        _expandCollapseBtn.style.borderBottomRightRadius = 4f;
        header.Add(_expandCollapseBtn);

        panel.Add(header);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.maxHeight = ScrollMaxHeight;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; // no horizontal scrollbar
        PutScrollbarOnLeft(scroll);
        StyleScrollbarToBlendIn(scroll);
        panel.Add(scroll);

        for (int i = 0; i < FinanceCategory.ExpenseOrder.Length; i++)
        {
            var cat = FinanceCategory.ExpenseOrder[i];
            var val = ValueLabel();
            _expenseValues[cat] = val;

            bool expandable = cat == FinanceCategory.Wages
                           || cat == FinanceCategory.Maintenance
                           || cat == FinanceCategory.Electricity
                           || cat == FinanceCategory.Groundskeeping
                           || cat == FinanceCategory.ContractLabor
                           || cat == FinanceCategory.LossPrevention
                           || cat == FinanceCategory.Sanitation
                           || cat == FinanceCategory.MHECosts
                           || cat == FinanceCategory.PalletLeaseRepair
                           || cat == FinanceCategory.Transportation;

            Color rowBg = i % 2 == 0 ? ColRowA : ColRowB;

            // Built directly (not via the shared DataRow's static " ▾" suffix) so the arrow can
            // flip between collapsed/expanded and the row can carry a click handler.
            var row = new VisualElement();
            row.style.flexDirection   = FlexDirection.Row;
            row.style.backgroundColor = new StyleColor(rowBg);
            row.style.height          = LargeRowHeight;
            row.style.alignItems      = Align.Center;

            Label arrow = null;
            if (expandable)
            {
                arrow = Lbl("▸", size: LargeKeySize); // ▸ collapsed
                arrow.style.color        = new StyleColor(ColLabelHover);
                arrow.style.paddingLeft  = 10f;
                arrow.style.paddingRight = 4f;
                row.Add(arrow);
            }

            var keyLbl = Lbl(cat, size: LargeKeySize);
            keyLbl.style.flexGrow    = 1f;
            keyLbl.style.paddingLeft = expandable ? 0f : 10f;
            keyLbl.style.color       = new StyleColor(expandable ? ColLabelHover : ColLabelNormal);
            row.Add(keyLbl);

            val.style.fontSize        = LargeValueSize;
            val.style.width           = LargeValueWidth;
            val.style.unityTextAlign  = TextAnchor.MiddleRight;
            val.style.paddingRight    = 10f;
            val.style.backgroundColor = new StyleColor(ColValueBg);
            row.Add(val);

            scroll.Add(row);

            if (expandable)
            {
                var detail = new VisualElement();
                detail.style.display = DisplayStyle.None;
                scroll.Add(detail);

                var entry = new ExpandableRow { Category = cat, Row = row, Arrow = arrow, Detail = detail };
                _expandables.Add(entry);

                row.pickingMode = PickingMode.Position;
                row.RegisterCallback<ClickEvent>(_ => ToggleExpanded(entry));
            }
        }
        _expenseTotalLabel = ValueLabel(bold: true, color: ColExpenseWhite);
        panel.Add(TotalRow("Total Expenses", _expenseTotalLabel, ColTotalBg, ColExpenseWhite, LargeKeySize, LargeRowHeight + 2f, LargeValueWidth));

        return panel;
    }

    /// <summary>Reverses the ScrollView's internal content/scrollbar order so the vertical
    /// scroller renders on the LEFT edge instead of Unity's default right — per Tad's request.</summary>
    static void PutScrollbarOnLeft(ScrollView scroll)
    {
        var container = scroll.Q(className: "unity-scroll-view__content-and-vertical-scroll-container");
        if (container != null) container.style.flexDirection = FlexDirection.RowReverse;
    }

    /// <summary>Unity's default scroller is a light grey that reads as a bright line against this
    /// panel's dark navy — recolor track/thumb/buttons to the panel's own palette so it blends in
    /// instead of standing out. Scoped to the vertical scroller specifically (the horizontal one is
    /// hidden). Verified against this Unity version's actual runtime hierarchy — the thumb's real
    /// class is "unity-base-slider__dragger", NOT "unity-scroller__thumb" (that name doesn't exist
    /// here, which is why the first pass at this had no visible effect).</summary>
    static void StyleScrollbarToBlendIn(ScrollView scroll)
    {
        var scroller = scroll.Q(className: "unity-scroll-view__vertical-scroller");
        if (scroller == null) return;

        scroller.style.backgroundColor = new StyleColor(ColBg);

        var slider = scroller.Q(className: "unity-scroller__slider");
        if (slider != null) slider.style.backgroundColor = new StyleColor(ColBg);

        var tracker = scroller.Q(className: "unity-base-slider__tracker");
        if (tracker != null) tracker.style.backgroundColor = new StyleColor(ColBg);

        var thumb = scroller.Q(className: "unity-base-slider__dragger");
        if (thumb != null) thumb.style.backgroundColor = new StyleColor(ColBorder);

        var lowBtn = scroller.Q(className: "unity-scroller__low-button");
        if (lowBtn != null) lowBtn.style.backgroundColor = new StyleColor(ColBg);

        var highBtn = scroller.Q(className: "unity-scroller__high-button");
        if (highBtn != null) highBtn.style.backgroundColor = new StyleColor(ColBg);
    }

    void ToggleExpanded(ExpandableRow entry) => SetExpanded(entry, !entry.Expanded);

    void SetExpanded(ExpandableRow entry, bool expanded)
    {
        entry.Expanded = expanded;
        entry.Arrow.text = expanded ? "▾" : "▸"; // ▾ expanded / ▸ collapsed
        entry.Detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
        if (expanded) PopulateDetail(entry);
    }

    /// <summary>The orange header button — expands every category on the first click, collapses
    /// them all on the next, flipping its own label to match.</summary>
    // Noticeably lighter than ColOrange for the button's "Collapse All" (expanded) state — NOT
    // blue. Without the explicit re-assign + Blur() below, Unity's runtime theme paints a pressed
    // Button with its own blue focus fill that outlives the click, the same issue already worked
    // around for the category buttons in buildmenuNEW.uss.
    static readonly Color LighterOrange = new Color(0.98f, 0.68f, 0.35f);

    void ToggleExpandCollapseAll()
    {
        _allExpanded = !_allExpanded;
        _expandCollapseBtn.text = _allExpanded ? "Collapse All" : "Expand All";
        _expandCollapseBtn.style.backgroundColor = new StyleColor(_allExpanded ? LighterOrange : ColOrange);
        _expandCollapseBtn.Blur();
        foreach (var entry in _expandables)
            SetExpanded(entry, _allExpanded);
    }

    // ── Inline detail list (replaces the old floating tooltip) ─────────────────
    void PopulateDetail(ExpandableRow entry)
    {
        var detail = entry.Detail;
        detail.Clear();

        var hdr = Lbl(TooltipTitle(entry.Category), bold: true, size: DetailTextSize);
        hdr.style.backgroundColor = new StyleColor(ColBlueDark);
        hdr.style.color           = new StyleColor(ColBlueTint);
        hdr.style.paddingTop      = 4f;
        hdr.style.paddingBottom   = 4f;
        hdr.style.paddingLeft     = 18f;
        detail.Add(hdr);

        // Every expandable category shares the same generic per-category detail bucket
        // (MoneyService._lifetimeDetail, populated by RemoveCapital(amount, category, detailKey)
        // from PayrollService — wages — and EconomyService — ObjDataSO.GL_Line hourly costs).
        // Wages keys its detail by wage-tier or role name; everything else keys by GL_Line.
        var detailData = _money.GetLifetimeDetail(entry.Category);
        string emptyMsg = entry.Category == FinanceCategory.Wages ? "No wages paid yet" : "No costs recorded yet";
        if (detailData == null || detailData.Count == 0)
        {
            detail.Add(BigEmptyMsg(emptyMsg));
            return;
        }
        int idx = 0;
        bool any = false;
        foreach (var kvp in detailData)
        {
            if (kvp.Value <= 0) continue;
            var tip = BigTipRow(DetailLabel(entry.Category, kvp.Key), kvp.Value, idx++ % 2 == 0);
            detail.Add(tip);
            any = true;
        }
        if (!any) detail.Add(BigEmptyMsg(emptyMsg));

        // Indent the whole list slightly so it visibly nests under its parent category row.
        detail.style.paddingLeft = 8f;
    }

    // Sub-text size for the detail list (header, empty message, line items) — "almost as big as"
    // the LargeKeySize row font per Tad's request, not identical (still needs to read as nested).
    const float DetailTextSize = 22f;

    // Same "yellow with a hint of orange" used for the Reputation dropdown's "Known" band text.
    static readonly Color KnownYellowOrange = new Color(0.95f, 0.75f, 0.25f);

    static VisualElement BigEmptyMsg(string msg)
    {
        var lbl = Lbl(msg, size: DetailTextSize);
        lbl.style.color         = new StyleColor(KnownYellowOrange);
        lbl.style.paddingTop    = 6f;
        lbl.style.paddingBottom = 6f;
        lbl.style.paddingLeft   = 26f; // nudged past the "By X" sub-header's paddingLeft to read as aligned
        return lbl;
    }

    static VisualElement BigTipRow(string key, int value, bool alt)
    {
        var row = new VisualElement();
        row.style.flexDirection   = FlexDirection.Row;
        row.style.backgroundColor = new StyleColor(alt ? ColRowA : ColRowB);
        row.style.height          = DetailTextSize + 14f;
        row.style.alignItems      = Align.Center;

        var keyLbl = Lbl(key, size: DetailTextSize);
        keyLbl.style.flexGrow    = 1f;
        keyLbl.style.paddingLeft = 8f;
        keyLbl.style.color       = new StyleColor(ColLabelNormal);

        var valLbl = Lbl(FormatMoney(value), size: DetailTextSize);
        valLbl.style.width           = LargeValueWidth;
        valLbl.style.unityTextAlign  = TextAnchor.MiddleRight;
        valLbl.style.paddingRight    = 8f;
        valLbl.style.backgroundColor = new StyleColor(ColValueBg);
        valLbl.style.color           = new StyleColor(ColOrange);

        row.Add(keyLbl);
        row.Add(valLbl);
        return row;
    }

    // ── Label helpers ─────────────────────────────────────────────────────────
    static string TooltipTitle(string cat) => cat switch
    {
        FinanceCategory.Wages             => "By Tier / Role",
        FinanceCategory.Maintenance       => "By Area",
        FinanceCategory.Electricity       => "By Type",
        FinanceCategory.Groundskeeping    => "By Category",
        FinanceCategory.ContractLabor     => "By Role",
        FinanceCategory.LossPrevention    => "By Source",
        FinanceCategory.Sanitation        => "By Source",
        FinanceCategory.MHECosts          => "By Vehicle",
        FinanceCategory.PalletLeaseRepair => "By Item",
        FinanceCategory.Transportation    => "By Item",
        _                                 => "Breakdown"
    };

    static string DetailLabel(string cat, string key)
    {
        if ((cat == FinanceCategory.Wages || cat == FinanceCategory.ContractLabor || cat == FinanceCategory.LossPrevention
             || cat == FinanceCategory.Sanitation || cat == FinanceCategory.Transportation) && Enum.TryParse(key, out EmployeeRole role))
            return role.DisplayName();
        return cat == FinanceCategory.Maintenance ? MaintenanceLabel(key) : key;
    }

    static string MaintenanceLabel(string key) => key switch
    {
        "Foundation" => "Building Maintenance",
        "Wall"       => "Walls",
        "Floor"      => "Floors",
        _            => key
    };

    // ── Refresh ───────────────────────────────────────────────────────────────
    void Refresh()
    {
        int totalExpense = 0;
        foreach (var cat in FinanceCategory.ExpenseOrder)
        {
            int v = _money.LifetimeExpenses.TryGetValue(cat, out int x) ? x : 0;
            totalExpense += v;
            if (_expenseValues.TryGetValue(cat, out var lbl)) lbl.text = FormatMoney(v);
        }
        _expenseTotalLabel.text = FormatMoney(totalExpense);

        // Keep any currently-open detail list's numbers live rather than only refreshing on click.
        foreach (var entry in _expandables)
            if (entry.Expanded) PopulateDetail(entry);
    }
}
