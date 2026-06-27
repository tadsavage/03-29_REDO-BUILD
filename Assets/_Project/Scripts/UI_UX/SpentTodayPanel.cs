using GameCore.Economy;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static FinanceUIKit;

/// <summary>
/// Today's spending breakdown: Purchases (one-time), Upkeep (hourly maintenance), and Wages.
///
/// Structure:
/// - "Purchased Today:" — one-time costs (buying objects from build menu), broken out by category
///   - If no purchases today, shows "No purchases recorded yet today"
/// - "Upkeep Costs:" — total hourly maintenance costs (object operating costs) for the day
/// - "Wages:" — total wage costs for all employees for the day
/// - Total spent = sum of all purchases + upkeep + wages
/// </summary>
public class SpentTodayPanel : ITopBarPanel
{
    readonly VisualElement _panel;
    readonly VisualElement _purchasesListContainer;
    readonly MoneyService _money;

    Label _purchasesTotalLabel;
    Label _upkeepCostLabel;
    Label _wageCostLabel;
    Label _totalSpentLabel;

    bool _visible;

    public SpentTodayPanel(VisualElement root, MoneyService money)
    {
        _money = money;
        _panel = Build(out _purchasesListContainer);
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

    VisualElement Build(out VisualElement purchasesListContainer)
    {
        var panel = Panel();

        panel.Add(SectionHeader("What We've Spent Today", ColBlueDark, ColBlueTint));

        // Purchases section
        panel.Add(SectionHeader("Purchased Today", ColOrangeDark, ColOrange));
        purchasesListContainer = new VisualElement();
        panel.Add(purchasesListContainer);

        _purchasesTotalLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(TotalRow("Purchases", _purchasesTotalLabel, ColTotalBg, ColOrange));

        // Upkeep costs section
        _upkeepCostLabel = ValueLabel(bold: true, color: ColBlueTint);
        panel.Add(TotalRow("Upkeep Costs", _upkeepCostLabel, ColTotalBg, ColBlueTint));

        // Wages section
        _wageCostLabel = ValueLabel(bold: true, color: ColBlueTint);
        panel.Add(TotalRow("Wages", _wageCostLabel, ColTotalBg, ColBlueTint));

        // Total
        panel.Add(new VisualElement { style = { height = 4 } }); // spacer
        _totalSpentLabel = ValueLabel(bold: true, color: ColRevenueYellow);
        panel.Add(TotalRow("Total Spent Today", _totalSpentLabel, ColTotalBg, ColRevenueYellow));

        return panel;
    }

    void Refresh()
    {
        // Purchases breakdown
        _purchasesListContainer.Clear();

        var entries = _money.SpentTodayByObjectCategory
                          .Where(kvp => kvp.Value > 0)
                          .OrderByDescending(kvp => kvp.Value)
                          .ToList();

        int purchasesTotal = 0;
        if (entries.Count == 0)
        {
            _purchasesListContainer.Add(EmptyMsg("No purchases recorded yet today"));
        }
        else
        {
            int idx = 0;
            foreach (var kvp in entries)
            {
                _purchasesListContainer.Add(TipRow(kvp.Key, kvp.Value, idx++ % 2 == 0));
                purchasesTotal += kvp.Value;
            }
        }

        _purchasesTotalLabel.text = FormatMoney(purchasesTotal);

        // Upkeep and Wages
        int upkeepTotal = _money.SpentTodayUpkeep;
        int wagesTotal = _money.SpentTodayWages;
        int totalSpent = purchasesTotal + upkeepTotal + wagesTotal;

        _upkeepCostLabel.text = FormatMoney(upkeepTotal);
        _wageCostLabel.text = FormatMoney(wagesTotal);
        _totalSpentLabel.text = FormatMoney(totalSpent);
    }
}
