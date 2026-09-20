using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static FinanceUIKit;

/// <summary>
/// Drives the Reports tab. Clicking REPORTS shows a small popup with just three buttons —
/// Financial / Operational / Inventory — instead of an embedded content pane with sub-tabs. Each
/// button opens its OWN separate floating window: draggable by its title bar, resizable, with the
/// same maximize/close button pair in the upper-right corner as ContractsPanel/PurchasingPanel/
/// SlotAssignmentPanel (DraggableWindow + ResizableWindow + PanelTitleChrome), and — like those —
/// always opens maximized (FillScreenExact) rather than at some smaller default size.
///
/// Each window is a static snapshot rebuilt from live services every time it's opened — not a
/// live-bound dashboard. Services beyond MoneyService (InventoryService, FulfillmentStatsService,
/// ReputationService) are resolved lazily via ServiceLocator on every open rather than cached at
/// construction, since this controller is built early (alongside the rest of the HUD) and those
/// services may not have registered yet at that point.
/// </summary>
public class ReportsPanelController
{
    public enum Tab { Financial, Operational, Inventory }

    // ColBg/ColBorder come from FinanceUIKit (see the `using static` above) — window title text has
    // no equivalent there, so it's declared locally, same as ContractsPanel/SlotAssignmentPanel do.
    private static readonly Color ColTitleText = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);

    private readonly VisualElement _root;
    private readonly MoneyService _money;

    private class ReportWindow
    {
        public VisualElement Overlay;
        public VisualElement Modal;
        public VisualElement Content;
        public ResizableWindow Resizer;
        public Button ScaleButton;
        public bool Visible;
    }

    private readonly Dictionary<Tab, ReportWindow> _windows = new();

    public ReportsPanelController(VisualElement root, MoneyService money)
    {
        _root = root;
        _money = money;

        BuildWindow(Tab.Financial, "Financial Report");
        BuildWindow(Tab.Operational, "Operational Report");
        BuildWindow(Tab.Inventory, "Inventory Report");
    }

    /// <summary>Rebuilds whichever report window(s) are currently open with fresh data. Call whenever
    /// the Reports bar becomes visible again — a window left open while another HUD mode was up
    /// should show current numbers, not whatever was true when it was opened.</summary>
    public void Refresh()
    {
        foreach (var kvp in _windows)
            if (kvp.Value.Visible) RebuildContent(kvp.Key);
    }

    // ── Window shell ─────────────────────────────────────────────────────────
    // Same programmatic shape as SlotAssignmentPanel/ContractsPanel: absolute-positioned modal,
    // draggable by its title bar (DraggableWindow), resizable edges (ResizableWindow), with
    // PanelTitleChrome's shared scale+close button pair in the corner.
    void BuildWindow(Tab tab, string title)
    {
        var overlay = new VisualElement { name = $"reports-{tab}-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0; overlay.style.bottom = 0;
        overlay.style.display = DisplayStyle.None;
        overlay.pickingMode = PickingMode.Ignore;

        var modal = new VisualElement { name = $"reports-{tab}-modal" };
        modal.style.position = Position.Absolute;
        modal.style.left = 120; modal.style.top = 100;
        modal.style.width = 720; modal.style.height = 700;
        modal.style.backgroundColor = new StyleColor(ColBg);
        modal.style.borderTopWidth = modal.style.borderBottomWidth =
            modal.style.borderLeftWidth = modal.style.borderRightWidth = 3;
        modal.style.borderTopColor = modal.style.borderBottomColor =
            modal.style.borderLeftColor = modal.style.borderRightColor = new StyleColor(ColBorder);
        modal.style.borderTopLeftRadius = modal.style.borderTopRightRadius =
            modal.style.borderBottomLeftRadius = modal.style.borderBottomRightRadius = 16;
        modal.style.paddingTop = 14; modal.style.paddingBottom = 14;
        modal.style.paddingLeft = 16; modal.style.paddingRight = 16;

        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.height = 44;
        titleBar.style.marginBottom = 8;
        titleBar.style.flexShrink = 0;

        var titleLabel = new Label(title);
        titleLabel.style.color = new StyleColor(ColTitleText);
        titleLabel.style.fontSize = 22;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.flexGrow = 1;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(titleLabel);

        modal.Add(titleBar);

        var scroll = new ScrollView();
        scroll.style.flexGrow = 1;
        modal.Add(scroll);

        overlay.Add(modal);
        _root.Add(overlay);

        var resizer = new ResizableWindow(modal, minW: 480f, minH: 360f, grip: 10f, titleInset: 48f);
        var (scaleBtn, closeBtn) = PanelTitleChrome.Attach(titleBar, resizer, () => HideWindow(tab));
        new DraggableWindow(modal, titleBar, closeBtn);

        _windows[tab] = new ReportWindow
        {
            Overlay = overlay,
            Modal = modal,
            Content = scroll,
            Resizer = resizer,
            ScaleButton = scaleBtn,
        };
    }

public void Open(Tab tab)
    {
        if (!_windows.TryGetValue(tab, out var win)) return;

        // Clicking the button for the report that's already open closes it, same as clicking a
        // mode tab that's already active does elsewhere in this HUD — a toggle, not just an open.
        if (win.Visible) { HideWindow(tab); return; }

        // Only one report window on screen at a time — opening a different report flips straight to
        // it rather than piling windows up, so clicking Operational while Inventory is open closes
        // Inventory and opens Operational in its place.
        foreach (var kvp in _windows)
            if (kvp.Key != tab && kvp.Value.Visible) HideWindow(kvp.Key);

        RebuildContent(tab);
        win.Visible = true;
        win.Overlay.style.display = DisplayStyle.Flex;
        win.Overlay.BringToFront();

        // Always opens maximized — same reasoning/timing as ContractsPanel.Show()/PurchasingPanel.
        // Show(): deferred one frame so FillScreenExact has a real layout to measure on the very
        // first open of a session (see ResizableWindow.FillScreen's own doc comment).
        win.Modal.schedule.Execute(() =>
        {
            win.Resizer.FillScreenExact();
            win.Resizer.UpdateScaleButtonIcon(win.ScaleButton, PanelTitleChrome.ButtonSize, ColTitleText);
        }).ExecuteLater(16);
    }

    /// <summary>Closes every report window that's currently open. Called when the HUD switches away
    /// from Reports to another tab (Build/Orders/Staff) — a report window is a Reports-mode fixture,
    /// not something that should keep floating over an unrelated tab.</summary>
    public void CloseAll()
    {
        foreach (var kvp in _windows)
            if (kvp.Value.Visible) HideWindow(kvp.Key);
    }

    void HideWindow(Tab tab)
    {
        if (!_windows.TryGetValue(tab, out var win)) return;
        win.Visible = false;
        win.Overlay.style.display = DisplayStyle.None;
        win.Overlay.pickingMode = PickingMode.Ignore;
    }

    void RebuildContent(Tab tab)
    {
        var win = _windows[tab];
        win.Content.Clear();
        switch (tab)
        {
            case Tab.Financial:   BuildFinancial(win.Content);   break;
            case Tab.Operational: BuildOperational(win.Content); break;
            case Tab.Inventory:   BuildInventory(win.Content);   break;
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
