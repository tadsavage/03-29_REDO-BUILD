using GameCore.Economy;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static FinanceUIKit;

/// Today's spending only — triggered by the "Spent Today" label in TopBarUI.
///
/// Top line: "Total Hourly Expenses" — the single summed RECURRING hourly cost (object upkeep
/// + wages) only. Does NOT include one-time purchase costs or the daily Lease charge.
///
/// Below: "Purchases" — one-time costs (buying foundations/walls/vehicles etc. from the build
/// menu), broken out by raw ObjDataSO.category, with a Total Purchases row at the bottom.
///
/// These two numbers were originally conflated (the top line read MoneyService.SpentToday, the
/// all-up total) — e.g. buying 56 foundations showed as "$25,200 Total Hourly Expenses" when
/// that was a one-time purchase, not an hourly cost. Now tracked as two separate MoneyService
/// counters (SpentTodayHourlyOnly vs SpentTodayByObjectCategory) so they can't bleed into each
/// other. Both reset every in-game day (MoneyService.ResetDailySpending).
public class SpentTodayPanel : ITopBarPanel
{
    readonly VisualElement _panel;
    readonly VisualElement _listContainer;
    readonly MoneyService  _money;
    Label _hourlyTotalLabel;
    Label _purchasesTotalLabel;
    bool  _visible;

    public SpentTodayPanel(VisualElement root, MoneyService money)
    {
        _money = money;
        _panel = Build(out _listContainer);
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

    VisualElement Build(out VisualElement listContainer)
    {
        var panel = Panel();

        panel.Add(SectionHeader("Today's Spending", ColBlueDark, ColBlueTint));

        _hourlyTotalLabel = ValueLabel(bold: true, color: ColBlueTint);
        panel.Add(TotalRow("Total Hourly Expenses", _hourlyTotalLabel, ColTotalBg, ColBlueTint));

        panel.Add(SectionHeader("Purchases", ColOrangeDark, ColOrange));

        listContainer = new VisualElement();
        panel.Add(listContainer);

        _purchasesTotalLabel = ValueLabel(bold: true, color: ColOrange);
        panel.Add(TotalRow("Total Purchases", _purchasesTotalLabel, ColTotalBg, ColOrange));

        return panel;
    }

    void Refresh()
    {
        _hourlyTotalLabel.text = FormatMoney(_money.SpentTodayHourlyOnly);

        _listContainer.Clear();

        var entries = _money.SpentTodayByObjectCategory.Where(kvp => kvp.Value > 0)
                                                         .OrderByDescending(kvp => kvp.Value)
                                                         .ToList();
        if (entries.Count == 0)
        {
            _listContainer.Add(EmptyMsg("No purchases recorded yet today"));
            _purchasesTotalLabel.text = FormatMoney(0);
            return;
        }

        int idx = 0;
        int sum = 0;
        foreach (var kvp in entries)
        {
            _listContainer.Add(TipRow(kvp.Key, kvp.Value, idx++ % 2 == 0));
            sum += kvp.Value;
        }
        _purchasesTotalLabel.text = FormatMoney(sum);
    }
}
