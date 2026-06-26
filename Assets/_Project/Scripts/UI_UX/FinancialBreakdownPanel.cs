using GameCore.Economy;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static FinanceUIKit;

/// Per-category expense breakdown with drill-down tooltips — triggered by the "Hourly" label in
/// TopBarUI. Revenue and Net Profit live on the Capital tab (CapitalSummaryPanel) instead. Hover
/// an expandable expense row for its sub-breakdown.
public class FinancialBreakdownPanel : ITopBarPanel
{
    // ── State ─────────────────────────────────────────────────────────────────
    readonly VisualElement _root;
    readonly VisualElement _panel;
    readonly MoneyService  _money;
    readonly Dictionary<string, Label> _expenseValues = new();
    readonly List<VisualElement>       _tooltips      = new();

    Label _expenseTotalLabel;
    bool  _visible;

    // Tracks each expandable row/tooltip pair's live hover state. A SINGLE poll (started once,
    // never re-created) drives closing — this replaced an earlier design that created a fresh
    // IVisualElementScheduledItem per mouse event and Pause()'d the previous one; that approach
    // broke down after repeated hover cycles (stale scheduled items, races between independent
    // per-tooltip timers) and tooltips would get permanently stuck open. One shared idle-timer
    // checked on a fixed interval has no per-interaction state to corrupt.
    class TooltipEntry
    {
        public VisualElement Row;
        public VisualElement Tooltip;
        public string Category;
        public bool Hovered;
    }
    readonly List<TooltipEntry> _entries = new();
    TooltipEntry _activeEntry;
    float _idleMs;

    // ── Construction ─────────────────────────────────────────────────────────
    public FinancialBreakdownPanel(VisualElement root, MoneyService money)
    {
        _root  = root;
        _money = money;
        _panel = Build();
        _panel.style.display = DisplayStyle.None;
        root.Add(_panel);
        // Add tooltips after the panel so they render on top.
        foreach (var tt in _tooltips)
            _root.Add(tt);
        _money.OnMoneyChanged += Refresh;
        _panel.schedule.Execute(PollIdle).Every(100);
        Refresh();
    }

    // ── Public API ────────────────────────────────────────────────────────────
    public bool IsVisible => _visible;
    public VisualElement Root => _panel;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        _panel.style.display = DisplayStyle.Flex;
        Refresh();
    }

    public void Hide()
    {
        _visible = false;
        _panel.style.display = DisplayStyle.None;
        _activeEntry = null;
        _idleMs = 0f;
        foreach (var tt in _tooltips)
            tt.style.display = DisplayStyle.None;
    }

    // Runs every 100ms for the lifetime of the panel. Closes the active tooltip once it's been
    // unhovered for ~2s. Hovering (row OR tooltip) resets the idle clock back to zero.
    void PollIdle()
    {
        if (_activeEntry == null) return;

        if (!_visible || _activeEntry.Hovered)
        {
            _idleMs = 0f;
            if (!_visible) { HideTooltip(_activeEntry.Tooltip); _activeEntry = null; }
            return;
        }

        _idleMs += 100f;
        if (_idleMs < 1500f) return;

        HideTooltip(_activeEntry.Tooltip);
        _activeEntry = null;
        _idleMs = 0f;
    }

    void ActivateEntry(TooltipEntry entry)
    {
        if (_activeEntry == entry) return;
        if (_activeEntry != null) HideTooltip(_activeEntry.Tooltip);

        _activeEntry = entry;
        _idleMs = 0f;

        var tt = entry.Tooltip;
        PopulateTooltip(tt, entry.Category);
        tt.style.top       = entry.Row.worldBound.y;
        tt.style.left      = Width;
        tt.style.display   = DisplayStyle.Flex;
        tt.style.translate = new Translate(0f, 0f);
        tt.style.opacity   = 1f;
    }

    // Fades + slides a tooltip back toward the panel, then sets display:None once the
    // transition finishes (display can't itself be animated in UI Toolkit).
    static void HideTooltip(VisualElement tt)
    {
        tt.style.opacity   = 0f;
        tt.style.translate = new Translate(-16f, 0f);
        tt.schedule.Execute(() =>
        {
            if (tt.resolvedStyle.opacity <= 0.01f)
                tt.style.display = DisplayStyle.None;
        }).StartingIn(150);
    }

    public void Dispose()
    {
        if (_money != null) _money.OnMoneyChanged -= Refresh;
        if (_panel.parent != null) _panel.RemoveFromHierarchy();
        foreach (var tt in _tooltips)
            if (tt.parent != null) tt.RemoveFromHierarchy();
    }

    // ── Build ─────────────────────────────────────────────────────────────────
    VisualElement Build()
    {
        var panel = Panel();

        // ── Expenses only — Revenue/Net Profit live on the Capital tab now ──
        panel.Add(SectionHeader("Expenses", ColExpenseRed, ColExpenseWhite));
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
            var row     = DataRow(cat, val, rowBg, expandable);
            panel.Add(row);

            if (expandable)
                AttachTooltip(row, cat, rowBg);
        }
        _expenseTotalLabel = ValueLabel(bold: true, color: ColExpenseWhite);
        panel.Add(TotalRow("Total Expenses", _expenseTotalLabel, ColTotalBg, ColExpenseWhite));

        return panel;
    }

    // ── Tooltip ───────────────────────────────────────────────────────────────
    void AttachTooltip(VisualElement row, string financeCategory, Color normalBg)
    {
        var tt = new VisualElement();
        tt.style.position                   = Position.Absolute;
        tt.style.width                      = TooltipWidth;
        tt.style.backgroundColor            = new StyleColor(ColTooltipBg);
        tt.style.borderTopLeftRadius        = 5f;
        tt.style.borderTopRightRadius       = 5f;
        tt.style.borderBottomLeftRadius     = 5f;
        tt.style.borderBottomRightRadius    = 5f;
        tt.style.borderTopWidth             = 1f;
        tt.style.borderBottomWidth          = 1f;
        tt.style.borderLeftWidth            = 1f;
        tt.style.borderRightWidth           = 1f;
        tt.style.borderTopColor             = new StyleColor(ColBorder);
        tt.style.borderBottomColor          = new StyleColor(ColBorder);
        tt.style.borderLeftColor            = new StyleColor(ColBorder);
        tt.style.borderRightColor           = new StyleColor(ColBorder);
        tt.style.display                    = DisplayStyle.None;
        tt.style.opacity                     = 0f;
        tt.style.translate                  = new Translate(-16f, 0f);
        tt.style.transitionProperty          = new List<StylePropertyName> { new("opacity"), new("translate") };
        tt.style.transitionDuration          = new List<TimeValue> { new(140, TimeUnit.Millisecond) };
        tt.pickingMode                      = PickingMode.Position;
        _tooltips.Add(tt);   // added to root AFTER panel in constructor

        var entry = new TooltipEntry { Row = row, Tooltip = tt, Category = financeCategory };
        _entries.Add(entry);

        row.RegisterCallback<MouseEnterEvent>(_ =>
        {
            entry.Hovered = true;
            row.style.backgroundColor = new StyleColor(ColHoverRow);
            if (_visible) ActivateEntry(entry);
        });
        row.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            entry.Hovered = false;
            row.style.backgroundColor = new StyleColor(normalBg);
        });
        tt.RegisterCallback<MouseEnterEvent>(_ => entry.Hovered = true);
        tt.RegisterCallback<MouseLeaveEvent>(_ => entry.Hovered = false);
    }

    void PopulateTooltip(VisualElement tt, string category)
    {
        tt.Clear();

        var hdr = Lbl(TooltipTitle(category), bold: true, size: 12f);
        hdr.style.backgroundColor       = new StyleColor(ColBlueDark);
        hdr.style.color                 = new StyleColor(ColBlueTint);
        hdr.style.paddingTop            = 5f;
        hdr.style.paddingBottom         = 5f;
        hdr.style.paddingLeft           = 8f;
        hdr.style.borderTopLeftRadius   = 5f;
        hdr.style.borderTopRightRadius  = 5f;
        tt.Add(hdr);

        // Every expandable category shares the same generic per-category detail bucket
        // (MoneyService._lifetimeDetail, populated by RemoveCapital(amount, category, detailKey)
        // from PayrollService — wages — and EconomyService — ObjDataSO.GL_Line hourly costs).
        // Wages keys its detail by wage-tier or role name; everything else keys by GL_Line.
        var detail = _money.GetLifetimeDetail(category);
        string emptyMsg = category == FinanceCategory.Wages ? "No wages paid yet" : "No costs recorded yet";
        if (detail == null || detail.Count == 0)
        {
            tt.Add(EmptyMsg(emptyMsg));
            return;
        }
        int idx = 0;
        bool any = false;
        foreach (var kvp in detail)
        {
            if (kvp.Value <= 0) continue;
            tt.Add(TipRow(DetailLabel(category, kvp.Key), kvp.Value, idx++ % 2 == 0));
            any = true;
        }
        if (!any) tt.Add(EmptyMsg(emptyMsg));
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
    }
}
