using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static FinanceUIKit;

/// <summary>
/// Drives the Reports tab's three sub-panels (Financial / Operational / Inventory), each a static
/// snapshot rebuilt from live services on every show — not a live-bound dashboard. Built directly
/// with FinanceUIKit's row/label helpers rather than reusing CapitalSummaryPanel/FinancialBreakdown
/// Panel: those two build on FinanceUIKit.Panel(), which is a floating dropdown sized/positioned for
/// the top bar, not a fit for an embedded report pane.
///
/// Services beyond MoneyService (InventoryService, FulfillmentStatsService, ReputationService) are
/// resolved lazily via ServiceLocator on every Refresh() rather than cached at construction, since
/// this controller is built early (alongside the rest of the HUD) and those services may not have
/// registered yet at that point.
/// </summary>
public class ReportsPanelController
{
    public enum Tab { Financial, Operational, Inventory }

    readonly VisualElement _tabRow;
    readonly VisualElement _content;
    readonly MoneyService _money;

    readonly Dictionary<Tab, Button> _tabButtons = new();
    Tab _activeTab = Tab.Financial;

    public ReportsPanelController(VisualElement tabRow, VisualElement content, MoneyService money)
    {
        _tabRow = tabRow;
        _content = content;
        _money = money;

        BuildTabRow();
        ShowTab(Tab.Financial);
    }

    /// <summary>Rebuilds whichever sub-tab is currently active with fresh data. Call whenever the
    /// Reports mode becomes visible.</summary>
    public void Refresh() => ShowTab(_activeTab);

    void BuildTabRow()
    {
        _tabRow.Clear();
        _tabButtons.Clear();
        AddTabButton(Tab.Financial, "Financial");
        AddTabButton(Tab.Operational, "Operational");
        AddTabButton(Tab.Inventory, "Inventory");
    }

    void AddTabButton(Tab tab, string label)
    {
        var btn = new Button(() => ShowTab(tab)) { text = label };
        btn.AddToClassList("reports-subtab-button");
        _tabButtons[tab] = btn;
        _tabRow.Add(btn);
    }

    void ShowTab(Tab tab)
    {
        _activeTab = tab;
        foreach (var kvp in _tabButtons)
            kvp.Value.EnableInClassList("reports-subtab-active", kvp.Key == tab);

        _content.Clear();
        switch (tab)
        {
            case Tab.Financial:   BuildFinancial(_content);   break;
            case Tab.Operational: BuildOperational(_content); break;
            case Tab.Inventory:   BuildInventory(_content);   break;
        }
    }

    // ── Financial ────────────────────────────────────────────────────────────
    // Same underlying numbers as the top bar's Capital + Hourly dropdowns (LifetimeIncome/
    // LifetimeExpenses per FinanceCategory, plus MoneyService's rolling day/week snapshots) —
    // combined into one static report instead of two separate interactive dropdowns.
    void BuildFinancial(VisualElement parent)
    {
        if (_money == null) { parent.Add(EmptyMsg("Money service unavailable.")); return; }

        parent.Add(SectionHeader("Revenue", ColRevenueGreen, ColRevenueYellow, LargeHeaderSize, LargeHeaderHeight));
        int totalIncome = 0;
        for (int i = 0; i < FinanceCategory.IncomeOrder.Length; i++)
        {
            string cat = FinanceCategory.IncomeOrder[i];
            int v = _money.LifetimeIncome.TryGetValue(cat, out int x) ? x : 0;
            totalIncome += v;
            var val = ValueLabel();
            val.text = FormatMoney(v);
            parent.Add(DataRow(cat, val, i % 2 == 0 ? ColRowA : ColRowB, false, LargeKeySize, LargeRowHeight, LargeValueWidth));
        }
        var incomeTotal = ValueLabel(bold: true, color: ColOrange);
        incomeTotal.text = FormatMoney(totalIncome);
        parent.Add(TotalRow("Total Revenue", incomeTotal, ColTotalBg, ColOrange, LargeKeySize, LargeRowHeight + 2f, LargeValueWidth));

        parent.Add(SectionHeader("Expenses", ColExpenseRed, ColExpenseWhite, LargeHeaderSize, LargeHeaderHeight));
        int totalExpense = 0;
        for (int i = 0; i < FinanceCategory.ExpenseOrder.Length; i++)
        {
            string cat = FinanceCategory.ExpenseOrder[i];
            int v = _money.LifetimeExpenses.TryGetValue(cat, out int x) ? x : 0;
            totalExpense += v;
            var val = ValueLabel();
            val.text = FormatMoney(v);
            parent.Add(DataRow(cat, val, i % 2 == 0 ? ColRowA : ColRowB, false, LargeKeySize, LargeRowHeight, LargeValueWidth));
        }
        var expenseTotal = ValueLabel(bold: true, color: ColBlueTint);
        expenseTotal.text = FormatMoney(totalExpense);
        parent.Add(TotalRow("Total Expenses", expenseTotal, ColTotalBg, ColBlueTint, LargeKeySize, LargeRowHeight + 2f, LargeValueWidth));

        int net = totalIncome - totalExpense;
        parent.Add(NetProfitRow(net));

        parent.Add(SectionHeader("Snapshot", ColBlueDark, ColBlueTint, LargeHeaderSize, LargeHeaderHeight));
        AddStatRow(parent, "Revenue Today", FormatMoney(_money.RevenueToday), 0);
        AddStatRow(parent, "Expenses Today", FormatMoney(_money.ExpensesToday), 1);
        AddStatRow(parent, "Revenue This Week", FormatMoney(_money.RevenueThisWeek), 0);
        AddStatRow(parent, "Expenses This Week", FormatMoney(_money.ExpensesThisWeek), 1);
        AddStatRow(parent, "Current Capital", FormatMoney(_money.CurrentCapital), 0);
        AddStatRow(parent, "Hourly Upkeep", FormatMoney(_money.TotalHourlyCost), 1);
    }

    static VisualElement NetProfitRow(int net)
    {
        var row = new VisualElement();
        row.style.flexDirection   = FlexDirection.Row;
        row.style.backgroundColor = new StyleColor(net >= 0 ? ColNetPos : ColNetNeg);
        row.style.paddingTop      = 5f;
        row.style.paddingBottom   = 5f;
        row.style.borderTopWidth  = 1f;
        row.style.borderTopColor  = new StyleColor(new Color(1f, 1f, 1f, 0.12f));
        row.style.alignItems      = Align.Center;

        var key = Lbl("Net Profit", bold: true, size: LargeKeySize);
        key.style.flexGrow    = 1f;
        key.style.paddingLeft = 10f;

        var val = Lbl(FormatMoney(net), bold: true, size: LargeValueSize);
        val.style.width           = LargeValueWidth;
        val.style.unityTextAlign  = TextAnchor.MiddleRight;
        val.style.paddingRight    = 10f;

        row.Add(key);
        row.Add(val);
        return row;
    }

    // ── Operational ──────────────────────────────────────────────────────────
    void BuildOperational(VisualElement parent)
    {
        ServiceLocator.TryGet<FulfillmentStatsService>(out var stats);
        ServiceLocator.TryGet<ReputationService>(out var reputation);

        parent.Add(SectionHeader("Fulfillment", ColBlueDark, ColBlueTint, LargeHeaderSize, LargeHeaderHeight));
        if (stats != null)
        {
            AddStatRow(parent, "Avg Pallets / Day", stats.AvgPalletsPerDay.ToString("0.0"), 0);
            AddStatRow(parent, "Avg Cases / Day", stats.AvgCasesPerDay.ToString("0.0"), 1);
            AddStatRow(parent, "On-Time Rate", ToPercent(stats.AvgOnTimeRate), 0);
            AddStatRow(parent, "Fill Rate", ToPercent(stats.AvgFillRate), 1);
        }
        else
        {
            parent.Add(EmptyMsg("Fulfillment stats unavailable."));
        }

        parent.Add(SectionHeader("Reputation", ColBlueDark, ColBlueTint, LargeHeaderSize, LargeHeaderHeight));
        if (reputation != null)
        {
            AddStatRow(parent, "Score", $"{reputation.Score} / {ReputationService.MaxScore}", 0);
            AddStatRow(parent, "Band", ReputationService.BandLabel(reputation.Band), 1);
        }
        else
        {
            parent.Add(EmptyMsg("Reputation service unavailable."));
        }
    }

    static string ToPercent(float rate01) => Mathf.RoundToInt(rate01 * 100f) + "%";

    // ── Inventory ────────────────────────────────────────────────────────────
    void BuildInventory(VisualElement parent)
    {
        ServiceLocator.TryGet<InventoryService>(out var inv);

        parent.Add(SectionHeader("Stock", ColBlueDark, ColBlueTint, LargeHeaderSize, LargeHeaderHeight));
        if (inv != null)
        {
            var pallets = inv.GetAllPallets();

            // Not every tracked pallet is product: empty CHEP pallet carriers (SkuId like "[A Chep]")
            // are in the same list but have no matching SkuData — GetSkuData legitimately returns
            // null for them. Folding those into "units on hand"/"inventory value" would silently
            // undercount value (carriers cost nothing) while overcounting pallet/unit totals, so
            // product stats are scoped to pallets that actually resolve to a real SKU.
            var productPallets = pallets.Where(p => inv.GetSkuData(p.SkuId) != null).ToList();
            int emptyCarrierCount = pallets.Count - productPallets.Count;

            int totalUnits = productPallets.Sum(p => p.Quantity);
            float totalValue = productPallets.Sum(p => p.Quantity * inv.GetSkuData(p.SkuId).BuyValue);

            AddStatRow(parent, "Tracked SKUs", inv.AllSkus.Count().ToString(), 0);
            AddStatRow(parent, "Product Pallets On Hand", productPallets.Count.ToString(), 1);
            AddStatRow(parent, "Total Units On Hand", totalUnits.ToString("N0"), 0);
            AddStatRow(parent, "Inventory Value (Cost)", FormatMoney(Mathf.RoundToInt(totalValue)), 1);
            AddStatRow(parent, "Empty Pallet Carriers", emptyCarrierCount.ToString("N0"), 0);
            AddStatRow(parent, "Pallets Awaiting Putaway", inv.GetReceivingPallets().Count.ToString(), 1);

            var distinctSkuIds = productPallets.Select(p => p.SkuId).Distinct();
            int criticalLines = CriticalStockCheck.CountCriticalLines(distinctSkuIds);
            AddStatRow(parent, "Critical Stock Lines", criticalLines.ToString(), 0);
        }
        else
        {
            parent.Add(EmptyMsg("Inventory service unavailable."));
        }

        parent.Add(SectionHeader("Slot Utilization", ColBlueDark, ColBlueTint, LargeHeaderSize, LargeHeaderHeight));
        int all = LocationRegistry.All.Count();
        if (all > 0)
        {
            var reserveLocations = LocationRegistry.ReserveLocations.ToList();
            var pickLocations = LocationRegistry.PickLocations.ToList();

            int totalReserve = reserveLocations.Count;
            int occupiedReserve = reserveLocations.Count(l => !l.IsAvailable);

            // Pick slots use "assigned" rather than "occupied": a pick face is dedicated to a SKU via
            // the Slotting UI (SlotAssignmentService) independently of whether it's currently holding
            // product, unlike reserve slots where occupancy IS the only concept that applies.
            int totalPick = pickLocations.Count;
            int assignedPick = pickLocations.Count(l => SlotAssignmentService.IsAssigned(l.Address));

            AddStatRow(parent, "Total Reserve Slots", totalReserve.ToString("N0"), 0);
            AddStatRow(parent, "Occupied Reserve Slots", occupiedReserve.ToString("N0"), 1);
            AddStatRow(parent, "Total Pick Slots", totalPick.ToString("N0"), 0);
            AddStatRow(parent, "Total Assigned Pickslots", assignedPick.ToString("N0"), 1);
            AddStatRow(parent, "Reserve Occupancy %", ToPercent(totalReserve > 0 ? (float)occupiedReserve / totalReserve : 0f), 0);
            AddStatRow(parent, "Pick Occupancy %", ToPercent(totalPick > 0 ? (float)assignedPick / totalPick : 0f), 1);
        }
        else
        {
            parent.Add(EmptyMsg("No rack locations registered yet."));
        }
    }

    // ── Shared row helper ────────────────────────────────────────────────────
    static void AddStatRow(VisualElement parent, string key, string value, int rowIndex)
    {
        var val = ValueLabel();
        val.text = value;
        parent.Add(DataRow(key, val, rowIndex % 2 == 0 ? ColRowA : ColRowB, false, LargeKeySize, LargeRowHeight, LargeValueWidth));
    }
}
