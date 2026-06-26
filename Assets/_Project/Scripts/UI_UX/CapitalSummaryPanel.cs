using GameCore.Economy;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static FinanceUIKit;

/// Revenue breakdown + a single rolled-up Total Expenses line + Net Profit — triggered by the
/// "Capital" label in TopBarUI. For the per-category expense drill-down, see the "Hourly" label
/// (FinancialBreakdownPanel) instead.
public class CapitalSummaryPanel : ITopBarPanel
{
    readonly VisualElement _panel;
    readonly MoneyService  _money;
    readonly Dictionary<string, Label> _incomeValues = new();

    Label         _incomeTotalLabel;
    Label         _expenseTotalLabel;
    Label         _netLabel;
    VisualElement _netRow;
    bool          _visible;

    public CapitalSummaryPanel(VisualElement root, MoneyService money)
    {
        _money = money;
        _panel = Build();
        _panel.style.display = DisplayStyle.None;
        root.Add(_panel);
        _money.OnMoneyChanged += Refresh;
        Refresh();
    }

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
    }

    public void Dispose()
    {
        if (_money != null) _money.OnMoneyChanged -= Refresh;
        if (_panel.parent != null) _panel.RemoveFromHierarchy();
    }

    VisualElement Build()
    {
        var panel = Panel();

        panel.Add(SectionHeader("Revenue", ColRevenueGreen, ColRevenueYellow));
        for (int i = 0; i < FinanceCategory.IncomeOrder.Length; i++)
        {
            var cat = FinanceCategory.IncomeOrder[i];
            var val = ValueLabel();
            _incomeValues[cat] = val;
            panel.Add(DataRow(cat, val, i % 2 == 0 ? ColRowA : ColRowB, false));
        }
        _incomeTotalLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(TotalRow("Total Revenue", _incomeTotalLabel, ColTotalBg, ColOrange));

        _expenseTotalLabel = ValueLabel(bold: true, color: ColBlueTint);
        panel.Add(TotalRow("Total Expenses", _expenseTotalLabel, ColTotalBg, ColBlueTint));

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
            totalExpense += _money.LifetimeExpenses.TryGetValue(cat, out int x) ? x : 0;
        _expenseTotalLabel.text = FormatMoney(totalExpense);

        int net = totalIncome - totalExpense;
        _netLabel.text = FormatMoney(net);
        if (_netRow != null)
            _netRow.style.backgroundColor = new StyleColor(net >= 0 ? ColNetPos : ColNetNeg);
    }
}
