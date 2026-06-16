using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// Financial breakdown panel — toggled by clicking the Capital label in TopBarUI.
/// Hover over Wages, Maintenance, Electricity, or Groundskeeping rows for sub-breakdown.
public class FinancialBreakdownPanel
{
    // ── Palette (matches TopBar / HiringBoard) ────────────────────────────────
    static readonly Color ColBg          = new Color(0.078f, 0.110f, 0.149f, 0.97f);
    static readonly Color ColOrange      = new Color(0.941f, 0.494f, 0.176f, 1f);   // rgb(240,126,45)
    static readonly Color ColOrangeDark  = new Color(0.65f,  0.32f,  0.09f,  1f);
    static readonly Color ColBlueDark    = new Color(0.05f,  0.22f,  0.36f,  1f);
    static readonly Color ColBlueTint    = new Color(0.75f,  0.88f,  0.96f,  1f);
    static readonly Color ColRowA        = new Color(0.10f,  0.14f,  0.18f,  1f);
    static readonly Color ColRowB        = new Color(0.12f,  0.17f,  0.22f,  1f);
    static readonly Color ColValueBg     = new Color(0.06f,  0.13f,  0.22f,  1f);
    static readonly Color ColTotalBg     = new Color(0.05f,  0.19f,  0.31f,  1f);
    static readonly Color ColHoverRow    = new Color(0.18f,  0.26f,  0.35f,  1f);
    static readonly Color ColNetPos      = new Color(0.65f,  0.32f,  0.09f,  1f);
    static readonly Color ColNetNeg      = new Color(0.48f,  0.07f,  0.07f,  1f);
    static readonly Color ColTooltipBg   = new Color(0.05f,  0.08f,  0.12f,  0.98f);
    static readonly Color ColBorder      = new Color(0.36f,  0.61f,  0.77f,  0.5f);
    static readonly Color ColLabelNormal = new Color(0.75f,  0.80f,  0.85f,  1f);
    static readonly Color ColLabelHover  = new Color(0.88f,  0.92f,  0.96f,  1f);

    const float Width        = 360f;
    const float TooltipWidth = 230f;
    const float RowHeight    = 28f;
    const float HeaderHeight = 24f;
    const float ValueWidth   = 100f;
    const string FontClass   = "fin-lilita";

    // ── State ─────────────────────────────────────────────────────────────────
    readonly VisualElement _root;
    readonly VisualElement _panel;
    readonly MoneyService  _money;
    readonly Dictionary<string, Label> _incomeValues  = new();
    readonly Dictionary<string, Label> _expenseValues = new();
    readonly List<VisualElement>       _tooltips      = new();

    Label         _incomeTotalLabel;
    Label         _expenseTotalLabel;
    Label         _netLabel;
    VisualElement _netRow;
    bool          _visible;

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
        Refresh();
    }

    // ── Public API ────────────────────────────────────────────────────────────
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
        foreach (var tt in _tooltips)
            tt.style.display = DisplayStyle.None;
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
        var panel = new VisualElement();
        panel.style.position                = Position.Absolute;
        panel.style.top                     = 44f;
        panel.style.left                    = 0f;
        panel.style.width                   = Width;
        panel.style.backgroundColor         = new StyleColor(ColBg);
        panel.style.borderBottomLeftRadius  = 6f;
        panel.style.borderBottomRightRadius = 6f;
        panel.style.borderBottomColor       = new StyleColor(ColBorder);
        panel.style.borderBottomWidth       = 1f;
        panel.style.borderLeftColor         = new StyleColor(ColBorder);
        panel.style.borderLeftWidth         = 1f;
        panel.style.borderRightColor        = new StyleColor(ColBorder);
        panel.style.borderRightWidth        = 1f;
        panel.pickingMode                   = PickingMode.Position;

        // ── Incoming Funds ─────────────────────────────────────────────────
        panel.Add(SectionHeader("Incoming Funds", ColOrangeDark, ColOrange));
        for (int i = 0; i < FinanceCategory.IncomeOrder.Length; i++)
        {
            var cat = FinanceCategory.IncomeOrder[i];
            var val = ValueLabel();
            _incomeValues[cat] = val;
            panel.Add(DataRow(cat, val, i % 2 == 0 ? ColRowA : ColRowB, false));
        }
        _incomeTotalLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(TotalRow("Total Income", _incomeTotalLabel, ColTotalBg, ColOrange));

        // ── Expenses ───────────────────────────────────────────────────────
        panel.Add(SectionHeader("Expenses", ColBlueDark, ColBlueTint));
        for (int i = 0; i < FinanceCategory.ExpenseOrder.Length; i++)
        {
            var cat = FinanceCategory.ExpenseOrder[i];
            var val = ValueLabel();
            _expenseValues[cat] = val;

            bool expandable = cat == FinanceCategory.Wages
                           || cat == FinanceCategory.Maintenance
                           || cat == FinanceCategory.Electricity
                           || cat == FinanceCategory.Groundskeeping;

            Color rowBg = i % 2 == 0 ? ColRowA : ColRowB;
            var row     = DataRow(cat, val, rowBg, expandable);
            panel.Add(row);

            if (expandable)
                AttachTooltip(row, cat, rowBg);
        }
        _expenseTotalLabel = ValueLabel(bold: true, color: ColBlueTint);
        panel.Add(TotalRow("Total Expenses", _expenseTotalLabel, ColTotalBg, ColBlueTint));

        // ── Net Profit ─────────────────────────────────────────────────────
        _netRow = new VisualElement();
        _netRow.style.flexDirection   = FlexDirection.Row;
        _netRow.style.backgroundColor = new StyleColor(ColNetPos);
        _netRow.style.paddingTop      = 5f;
        _netRow.style.paddingBottom   = 5f;
        _netRow.style.borderTopWidth  = 1f;
        _netRow.style.borderTopColor  = new StyleColor(new Color(1f, 1f, 1f, 0.12f));
        _netRow.style.alignItems      = Align.Center;

        var netKey = Lbl("Net Profit", bold: true, size: 14f);
        netKey.style.flexGrow    = 1f;
        netKey.style.paddingLeft = 10f;

        _netLabel = Lbl("$0", bold: true, size: 14f);
        _netLabel.style.width          = ValueWidth;
        _netLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        _netLabel.style.paddingRight   = 10f;

        _netRow.Add(netKey);
        _netRow.Add(_netLabel);
        panel.Add(_netRow);

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
        tt.pickingMode                      = PickingMode.Position;
        _tooltips.Add(tt);   // added to root AFTER panel in constructor

        bool overRow = false, overTip = false;

        void UpdateVis()
        {
            bool show = _visible && (overRow || overTip);
            if (show)
            {
                PopulateTooltip(tt, financeCategory);
                tt.style.top  = row.worldBound.y;
                tt.style.left = Width;
            }
            tt.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        row.RegisterCallback<MouseEnterEvent>(_ =>
        {
            overRow = true;
            row.style.backgroundColor = new StyleColor(ColHoverRow);
            UpdateVis();
        });
        row.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            overRow = false;
            row.style.backgroundColor = new StyleColor(normalBg);
            UpdateVis();
        });
        tt.RegisterCallback<MouseEnterEvent>(_ => { overTip = true;  UpdateVis(); });
        tt.RegisterCallback<MouseLeaveEvent>(_ => { overTip = false; UpdateVis(); });
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

        if (category == FinanceCategory.Wages)
        {
            bool any = false;
            int idx = 0;
            foreach (EmployeeRole role in Enum.GetValues(typeof(EmployeeRole)))
            {
                if (!_money.WagesByRole.TryGetValue(role.ToString(), out int amt) || amt <= 0) continue;
                tt.Add(TipRow(role.DisplayName(), amt, idx++ % 2 == 0));
                any = true;
            }
            if (!any) tt.Add(EmptyMsg("No wages paid yet"));
        }
        else
        {
            var detail = _money.GetLifetimeDetail(category);
            if (detail == null || detail.Count == 0)
            {
                tt.Add(EmptyMsg("No costs recorded yet"));
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
            if (!any) tt.Add(EmptyMsg("No costs recorded yet"));
        }
    }

    // ── Element factories ─────────────────────────────────────────────────────
    VisualElement SectionHeader(string text, Color bg, Color textColor)
    {
        var row = new VisualElement();
        row.style.backgroundColor = new StyleColor(bg);
        row.style.height          = HeaderHeight;
        row.style.justifyContent  = Justify.Center;
        row.style.alignItems      = Align.Center;
        row.style.borderTopWidth  = 1f;
        row.style.borderTopColor  = new StyleColor(new Color(1f, 1f, 1f, 0.08f));

        var lbl = Lbl(text, bold: true, size: 14f);
        lbl.style.color          = new StyleColor(textColor);
        lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
        row.Add(lbl);
        return row;
    }

    static VisualElement DataRow(string key, Label val, Color bg, bool expandable)
    {
        var row = new VisualElement();
        row.style.flexDirection   = FlexDirection.Row;
        row.style.backgroundColor = new StyleColor(bg);
        row.style.height          = RowHeight;
        row.style.alignItems      = Align.Center;

        var keyLbl = Lbl(expandable ? key + " ▾" : key, size: 13f);
        keyLbl.style.flexGrow    = 1f;
        keyLbl.style.paddingLeft = 10f;
        keyLbl.style.color       = new StyleColor(expandable ? ColLabelHover : ColLabelNormal);

        val.style.width           = ValueWidth;
        val.style.unityTextAlign  = TextAnchor.MiddleRight;
        val.style.paddingRight    = 10f;
        val.style.backgroundColor = new StyleColor(ColValueBg);

        row.Add(keyLbl);
        row.Add(val);
        return row;
    }

    static VisualElement TotalRow(string key, Label val, Color bg, Color keyColor)
    {
        var row = new VisualElement();
        row.style.flexDirection   = FlexDirection.Row;
        row.style.backgroundColor = new StyleColor(bg);
        row.style.height          = RowHeight + 2f;
        row.style.alignItems      = Align.Center;
        row.style.borderTopWidth  = 1f;
        row.style.borderTopColor  = new StyleColor(new Color(1f, 1f, 1f, 0.08f));

        var keyLbl = Lbl(key, bold: true, size: 13f);
        keyLbl.style.flexGrow    = 1f;
        keyLbl.style.paddingLeft = 10f;
        keyLbl.style.color       = new StyleColor(keyColor);

        val.style.width          = ValueWidth;
        val.style.unityTextAlign = TextAnchor.MiddleRight;
        val.style.paddingRight   = 10f;

        row.Add(keyLbl);
        row.Add(val);
        return row;
    }

    static VisualElement TipRow(string key, int value, bool alt)
    {
        var row = new VisualElement();
        row.style.flexDirection   = FlexDirection.Row;
        row.style.backgroundColor = new StyleColor(alt ? ColRowA : ColRowB);
        row.style.height          = 24f;
        row.style.alignItems      = Align.Center;

        var keyLbl = Lbl(key, size: 11f);
        keyLbl.style.flexGrow    = 1f;
        keyLbl.style.paddingLeft = 8f;
        keyLbl.style.color       = new StyleColor(ColLabelNormal);

        var valLbl = Lbl(FormatMoney(value), size: 11f);
        valLbl.style.width           = 82f;
        valLbl.style.unityTextAlign  = TextAnchor.MiddleRight;
        valLbl.style.paddingRight    = 8f;
        valLbl.style.backgroundColor = new StyleColor(ColValueBg);
        valLbl.style.color           = new StyleColor(ColOrange);

        row.Add(keyLbl);
        row.Add(valLbl);
        return row;
    }

    static VisualElement EmptyMsg(string msg)
    {
        var lbl = Lbl(msg, size: 11f);
        lbl.style.color         = new StyleColor(new Color(0.50f, 0.55f, 0.60f, 1f));
        lbl.style.paddingTop    = 6f;
        lbl.style.paddingBottom = 6f;
        lbl.style.paddingLeft   = 8f;
        return lbl;
    }

    static Label Lbl(string text = "", bool bold = false, float size = 13f)
    {
        var lbl = new Label(text);
        lbl.AddToClassList(FontClass);
        lbl.style.color                   = new StyleColor(Color.white);
        lbl.style.fontSize                = size;
        lbl.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        lbl.style.unityTextAlign          = TextAnchor.MiddleLeft;
        return lbl;
    }

    static Label ValueLabel(bool bold = false, Color? color = null)
    {
        var lbl = Lbl("$0", bold);
        if (color.HasValue) lbl.style.color = new StyleColor(color.Value);
        return lbl;
    }

    // ── Label helpers ─────────────────────────────────────────────────────────
    static string TooltipTitle(string cat) => cat switch
    {
        FinanceCategory.Wages          => "By Role",
        FinanceCategory.Maintenance    => "By Area",
        FinanceCategory.Electricity    => "By Type",
        FinanceCategory.Groundskeeping => "By Category",
        _                              => "Breakdown"
    };

    static string DetailLabel(string cat, string key) =>
        cat == FinanceCategory.Maintenance ? MaintenanceLabel(key) : key;

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
        int totalIncome = 0;
        foreach (var cat in FinanceCategory.IncomeOrder)
        {
            int v = _money.LifetimeIncome.TryGetValue(cat, out int x) ? x : 0;
            totalIncome += v;
            if (_incomeValues.TryGetValue(cat, out var lbl)) lbl.text = FormatMoney(v);
        }
        _incomeTotalLabel.text = FormatMoney(totalIncome);

        int totalExpense = 0;
        foreach (var cat in FinanceCategory.ExpenseOrder)
        {
            int v = _money.LifetimeExpenses.TryGetValue(cat, out int x) ? x : 0;
            totalExpense += v;
            if (_expenseValues.TryGetValue(cat, out var lbl)) lbl.text = FormatMoney(v);
        }
        _expenseTotalLabel.text = FormatMoney(totalExpense);

        int net = totalIncome - totalExpense;
        _netLabel.text = FormatMoney(net);
        if (_netRow != null)
            _netRow.style.backgroundColor = new StyleColor(net >= 0 ? ColNetPos : ColNetNeg);
    }

    static string FormatMoney(int amount)
        => (amount < 0 ? "-$" : "$") + Mathf.Abs(amount).ToString("N0");
}
