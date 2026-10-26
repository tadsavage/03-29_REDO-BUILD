using System;
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
    public enum Tab { Financial, Operational, Inventory, ItemListing }

    // ColBg/ColBorder come from FinanceUIKit (see the `using static` above) — window title text has
    // no equivalent there, so it's declared locally, same as ContractsPanel/SlotAssignmentPanel do.
    private static readonly Color ColTitleText = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);

    private readonly VisualElement _root;
    private readonly MoneyService _money;

    private class ReportWindow
    {
        public VisualElement Overlay;
        public VisualElement Modal;
        public VisualElement Content;   // a ScrollView for most reports; a plain column for Item Listing (it scrolls its own rows)
        public ResizableWindow Resizer;
        public Button ScaleButton;
        public bool Visible;
    }

    private readonly Dictionary<Tab, ReportWindow> _windows = new();

    /// <summary>True while any report window is on screen. Employee banners read this to stay hidden (they live in the HUD layer, which draws above these windows).</summary>
    public static bool AnyReportOpen { get; private set; }

    public ReportsPanelController(VisualElement root, MoneyService money)
    {
        _root = root;
        _money = money;
        AnyReportOpen = false;   // statics survive Play sessions (no domain reload)

        BuildWindow(Tab.Financial, "Financial Report");
        BuildWindow(Tab.Operational, "Operational Report");
        BuildWindow(Tab.Inventory, "Warehouse Capacity");
        BuildWindow(Tab.ItemListing, "Item Listing", scrollable: false);

        // Live charts: data twice a second, heartbeat pulse ~30 fps - only while the Warehouse Capacity window is showing.
        _root.schedule.Execute(OnSlowTick).Every(500);
        _root.schedule.Execute(OnPulseTick).Every(33);
    }

    /// <summary>Rebuilds whichever report window(s) are currently open with fresh data. Call whenever
    /// the Reports bar becomes visible again — a window left open while another HUD mode was up
    /// should show current numbers, not whatever was true when it was opened.</summary>
    public void Refresh()
    {
        foreach (var kvp in _windows)
            if (kvp.Value.Visible) RebuildContent(kvp.Key);
    }

    void OnSlowTick()
    {
        if (_windows.TryGetValue(Tab.Inventory, out var cap) && cap.Visible) UpdateCapacityCharts();
        if (_windows.TryGetValue(Tab.ItemListing, out var il) && il.Visible && Time.unscaledTime >= _ilRefreshAt)
        {
            _ilRefreshAt = Time.unscaledTime + 4f;
            RenderItemListing();
        }
    }

    void OnPulseTick()
    {
        if (_cap == null || !_windows.TryGetValue(Tab.Inventory, out var cap) || !cap.Visible) return;
        float t = Time.unscaledTime;
        _cap.ReservePie.Pulse(t);
        _cap.PickPie.Pulse(t);
    }

    // ── Window shell ─────────────────────────────────────────────────────────
    // Same programmatic shape as SlotAssignmentPanel/ContractsPanel: absolute-positioned modal,
    // draggable by its title bar (DraggableWindow), resizable edges (ResizableWindow), with
    // PanelTitleChrome's shared scale+close button pair in the corner.
    void BuildWindow(Tab tab, string title, bool scrollable = true)
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
        titleLabel.style.fontSize = tab == Tab.ItemListing ? 22f * 1.35f : 22f;   // Item Listing's title is 35% larger (Tad, 2026-10-23)
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.flexGrow = 1;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(titleLabel);

        // Item Listing is reached from Scheduler / Orders / Purchasing, so it links straight back to each of them.
        if (tab == Tab.ItemListing)
        {
            AddNavButton(titleBar, "SCHEDULER", NavButtonColors.Dest.Scheduler, 0);
            AddNavButton(titleBar, "ORDERS", NavButtonColors.Dest.Orders, 8);
            AddNavButton(titleBar, "PURCHASING", NavButtonColors.Dest.Purchasing, 9);
        }

        modal.Add(titleBar);

        VisualElement scroll = scrollable ? new ScrollView() : new VisualElement();
        scroll.style.flexGrow = 1;
        if (!scrollable) { scroll.style.minHeight = 0; scroll.style.flexDirection = FlexDirection.Column; }
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

/// <summary>Title-bar link that closes this report and opens the numbered panel (same call its hotkey makes).</summary>
    void AddNavButton(VisualElement titleBar, string label, NavButtonColors.Dest dest, int panelKey)
    {
        var b = new Button(() =>
        {
            CloseAll();
            UIKeyBindingManager.Instance?.ToggleUI(panelKey);
        }) { text = label };
        b.style.height = 36;
        b.style.paddingLeft = b.style.paddingRight = 14;
        b.style.marginRight = 8;
        b.style.flexShrink = 0;
        b.style.fontSize = 15;
        b.style.unityFontStyleAndWeight = FontStyle.Bold;
        b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        NavButtonColors.Apply(b, dest);
        titleBar.Add(b);
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

        // Any other open panel (Purchasing, Contracts, Work Queue, Scheduler...) is closed first, exactly as opening one numbered
        // panel closes another. Without this a report opened from the Reports bar stayed hidden behind, or was blocked by, whatever
        // was already up (Item Listing "would not open").
        UIKeyBindingManager.Instance?.CloseAll();

        // The employee info card and the floating employee banners sit in layers ABOVE these windows and covered them; close them
        // for as long as a report is up (Tad, 2026-10-23).
        UnityEngine.Object.FindAnyObjectByType<EmployeeInfoUI>()?.Hide();
        EmployeeWorldBanners.Existing?.HideAll();

        RebuildContent(tab);
        win.Visible = true;
        AnyReportOpen = true;
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
        AnyReportOpen = _windows.Values.Any(w => w.Visible);
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
            case Tab.ItemListing: BuildItemListing(win.Content); break;
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

    // ── Warehouse Capacity ───────────────────────────────────────────────────
    // Left: the numbers. Right: live charts (pallets on hand over time, average inventory value by day, and two slot
    // utilisation donuts that pulse red like a heartbeat from 95% up). The charts refresh while the window is open.
    private class CapacityView
    {
        public LineChart Line;
        public BarChart Bars;
        public PieChart ReservePie, PickPie, ThroughputPie, FillPie;
        public Label ReserveCaption, PickCaption, ThroughputCaption, FillCaption;
    }
    private CapacityView _cap;

    void BuildInventory(VisualElement parent)
    {
        _cap = null;
        ServiceLocator.TryGet<InventoryService>(out var inv);

        var body = new VisualElement();
        body.style.flexDirection = FlexDirection.Row;
        body.style.flexShrink = 0;

        var left = new VisualElement();
        left.style.flexGrow = 4f; left.style.flexBasis = 0; left.style.minWidth = 380; left.style.marginRight = 16;
        var right = new VisualElement();
        right.style.flexGrow = 5f; right.style.flexBasis = 0; right.style.minWidth = 460;
        body.Add(left); body.Add(right);
        parent.Add(body);

        // ---- numbers ----
        left.Add(SectionHeader("Stock", ColBlueDark, ColBlueTint, LargeHeaderSize, LargeHeaderHeight));
        if (inv != null)
        {
            var rows = ItemAvailability.Build();
            InventoryHistory.Measure(inv, out int palletCount, out float totalValue);
            int totalUnits = inv.GetAllPallets().Where(p => inv.GetSkuData(p.SkuId) != null).Sum(p => p.Quantity);

            AddStatRow(left, "Total SKUs Available", rows.Count.ToString(), 0);
            AddStatRow(left, "Total SKUs With Balance", rows.Count(r => r.OnHand > 0).ToString(), 1);
            AddStatRow(left, "Total SKUs With Insufficient Balance", rows.Count(r => r.Status == AvailabilityStatus.Insufficient).ToString(), 0);
            AddStatRow(left, "Total Units On Hand", totalUnits.ToString("N0"), 1);
            AddStatRow(left, "Inventory Value (Cost)", FormatMoney(Mathf.RoundToInt(totalValue)), 0);
            AddStatRow(left, "Pallets Awaiting Putaway", inv.GetReceivingPallets().Count.ToString(), 1);
        }
        else
        {
            left.Add(EmptyMsg("Inventory service unavailable."));
        }

        left.Add(SectionHeader("Slot Utilization", ColBlueDark, ColBlueTint, LargeHeaderSize, LargeHeaderHeight));
        int all = LocationRegistry.All.Count();
        if (all > 0)
        {
            ReadSlotCounts(out int totalReserve, out int occupiedReserve, out int totalPick, out int assignedPick);
            AddStatRow(left, "Total Reserve Slots", totalReserve.ToString("N0"), 0);
            AddStatRow(left, "Occupied Reserve Slots", occupiedReserve.ToString("N0"), 1);
            AddStatRow(left, "Total Pick Slots", totalPick.ToString("N0"), 0);
            AddStatRow(left, "Total Assigned Pickslots", assignedPick.ToString("N0"), 1);
            AddStatRow(left, "Reserve Occupancy %", ToPercent(totalReserve > 0 ? (float)occupiedReserve / totalReserve : 0f), 0);
            AddStatRow(left, "Pick Occupancy %", ToPercent(totalPick > 0 ? (float)assignedPick / totalPick : 0f), 1);
        }
        else
        {
            left.Add(EmptyMsg("No rack locations registered yet."));
        }

        // ---- charts ----
        _cap = new CapacityView();

        // Service level donuts, under the numbers on the left: pallet throughput and fill rate.
        left.Add(SectionHeader("Throughput & Service", ColBlueDark, ColBlueTint, LargeHeaderSize, LargeHeaderHeight));
        var service = new VisualElement();
        service.style.flexDirection = FlexDirection.Row; service.style.justifyContent = Justify.SpaceAround; service.style.flexShrink = 0;
        service.style.paddingTop = 12; service.style.paddingBottom = 8;
        _cap.ThroughputPie = new PieChart { FillColor = new Color(0.30f, 0.72f, 0.45f, 1f), PulseWhenHot = false };
        _cap.FillPie = new PieChart { FillColor = new Color(0.95f, 0.62f, 0.20f, 1f), PulseWhenHot = false };
        _cap.ThroughputCaption = Lbl("Avg Pallets / Day", bold: true, size: 20f);
        _cap.FillCaption = Lbl("Fill Rate", bold: true, size: 20f);
        service.Add(PieColumn(_cap.ThroughputPie, _cap.ThroughputCaption));
        service.Add(PieColumn(_cap.FillPie, _cap.FillCaption));
        left.Add(service);

        right.Add(SectionHeader("Pallets On Hand", ColBlueDark, ColBlueTint, LargeHeaderSize, LargeHeaderHeight));
        _cap.Line = new LineChart { WindowHours = 96f };
        _cap.Line.style.height = 230; _cap.Line.style.marginBottom = 10;
        right.Add(_cap.Line);

        right.Add(SectionHeader("Average Inventory Value By Day", ColBlueDark, ColBlueTint, LargeHeaderSize, LargeHeaderHeight));
        _cap.Bars = new BarChart { Format = v => "$" + Mathf.RoundToInt(v).ToString("N0") };
        _cap.Bars.style.height = 230; _cap.Bars.style.marginBottom = 10;
        right.Add(_cap.Bars);

        right.Add(SectionHeader("Slot Utilization", ColBlueDark, ColBlueTint, LargeHeaderSize, LargeHeaderHeight));
        var pies = new VisualElement();
        pies.style.flexDirection = FlexDirection.Row; pies.style.justifyContent = Justify.SpaceAround; pies.style.flexShrink = 0;
        pies.style.paddingTop = 12; pies.style.paddingBottom = 8;
        _cap.ReservePie = new PieChart { FillColor = new Color(0.30f, 0.72f, 0.45f, 1f) };
        _cap.PickPie = new PieChart { FillColor = new Color(0.36f, 0.61f, 0.90f, 1f) };
        _cap.ReserveCaption = Lbl("Reserve Slots Used", bold: true, size: 20f);
        _cap.PickCaption = Lbl("Pick Slots Slotted", bold: true, size: 20f);
        pies.Add(PieColumn(_cap.ReservePie, _cap.ReserveCaption));
        pies.Add(PieColumn(_cap.PickPie, _cap.PickCaption));
        right.Add(pies);

        UpdateCapacityCharts();
    }

    static VisualElement PieColumn(PieChart pie, Label caption)
    {
        var col = new VisualElement();
        col.style.alignItems = Align.Center;
        col.style.flexShrink = 0;
        pie.style.width = 210; pie.style.height = 210;
        caption.style.unityTextAlign = TextAnchor.MiddleCenter;
        caption.style.marginTop = 6;
        col.Add(pie);
        col.Add(caption);
        return col;
    }

    static void ReadSlotCounts(out int totalReserve, out int occupiedReserve, out int totalPick, out int assignedPick)
    {
        var reserveLocations = LocationRegistry.ReserveLocations.ToList();
        var pickLocations = LocationRegistry.PickLocations.ToList();
        totalReserve = reserveLocations.Count;
        occupiedReserve = reserveLocations.Count(l => !l.IsAvailable);
        // Pick slots use "assigned" rather than "occupied": a pick face is dedicated to a SKU via the Slotting UI
        // (SlotAssignmentService) independently of whether it's currently holding product.
        totalPick = pickLocations.Count;
        assignedPick = pickLocations.Count(l => SlotAssignmentService.IsAssigned(l.Address));
    }

    /// <summary>Refreshes every chart from live data. Called on open and twice a second while the window is showing.</summary>
    void UpdateCapacityCharts()
    {
        if (_cap == null) return;
        if (!InventoryHistory.TryGetNowHours(out float now)) return;
        ServiceLocator.TryGet<InventoryService>(out var inv);
        InventoryHistory.Measure(inv, out int pallets, out float value);

        // Pallets on hand: the hourly samples, plus a live point so the line always ends at "now" and creeps left with the clock.
        var pts = new List<Vector2>();
        foreach (var s in InventoryHistory.Samples) if (s.Hours <= now) pts.Add(new Vector2(s.Hours, s.Pallets));
        pts.Add(new Vector2(now, pallets));
        _cap.Line.Points = pts;
        _cap.Line.NowHours = now;
        var firstSample = InventoryHistory.Samples.Count > 0 ? InventoryHistory.Samples[0].Hours : now;
        _cap.Line.MinHours = Mathf.Floor(Mathf.Min(firstSample, now) / 24f) * 24f;
        _cap.Line.Refresh();

        // Average inventory value per day (today's bar includes the live value).
        var days = InventoryHistory.AverageValueByDay(7);
        int today = Mathf.FloorToInt(now / 24f);
        if (days.Count == 0 || days[days.Count - 1].day != today) days.Add((today, value));
        var bars = new List<(string, float)>();
        foreach (var d in days) bars.Add(("Day " + d.day, d.avgValue));
        _cap.Bars.Bars = bars;
        _cap.Bars.Refresh();

        // Slot donuts.
        if (LocationRegistry.All.Any())
        {
            ReadSlotCounts(out int totalReserve, out int occupiedReserve, out int totalPick, out int assignedPick);
            _cap.ReservePie.Fraction = totalReserve > 0 ? (float)occupiedReserve / totalReserve : 0f;
            _cap.PickPie.Fraction = totalPick > 0 ? (float)assignedPick / totalPick : 0f;
            _cap.ReserveCaption.text = $"Reserve Slots Used ({occupiedReserve}/{totalReserve})";
            _cap.PickCaption.text = $"Pick Slots Slotted ({assignedPick}/{totalPick})";
        }
        _cap.ReservePie.Refresh();
        _cap.PickPie.Refresh();

        // Fill rate = cases shipped / cases ordered (lifetime). Pallet throughput = lifetime average per day, with today and yesterday.
        if (ServiceLocator.TryGet<FulfillmentStatsService>(out var fs) && fs != null)
        {
            _cap.FillPie.Fraction = Mathf.Clamp01(fs.AvgFillRate);
            _cap.FillCaption.text = "Fill Rate (cases shipped / ordered)";

            int day = Mathf.FloorToInt(now / 24f);
            int todayPallets = fs.PalletsShippedOn(day), yesterdayPallets = fs.PalletsShippedOn(day - 1);
            float avg = fs.AvgPalletsPerDay;
            _cap.ThroughputPie.Fraction = avg > 0.01f ? Mathf.Clamp01(todayPallets / avg) : 0f;   // ring = today as a share of the daily average
            _cap.ThroughputPie.CenterText = avg.ToString("0.0");
            _cap.ThroughputPie.CenterSub = "per day";
            _cap.ThroughputCaption.text = $"Avg Pallets / Day\nToday {todayPallets}  |  Yesterday {yesterdayPallets}";
        }
        _cap.FillPie.Refresh();
        _cap.ThroughputPie.Refresh();
        _cap.ReserveCaption.style.color = new StyleColor(_cap.ReservePie.Hot ? new Color(1f, 0.45f, 0.42f) : ColLabelNormal);
        _cap.PickCaption.style.color = new StyleColor(_cap.PickPie.Hot ? new Color(1f, 0.45f, 0.42f) : ColLabelNormal);
    }

    // ── Item Listing ─────────────────────────────────────────────────────────
    private enum ItemFilter { All, Ok, Scratch, Insufficient }
    private enum ItemSort { Item, Description, OnHand, Inbound, Pending, Net, Cost, Sale, Vendor }
    private ItemFilter _ilFilter = ItemFilter.All;
    private ItemSort _ilSort = ItemSort.Item;
    private bool _ilAscending = true;
    private ScrollView _ilRows;
    private VisualElement _ilChipRow, _ilHeaderRow;
    private float _ilRefreshAt;

    private static readonly Color ColStatusRed    = new Color(1f, 0.36f, 0.36f, 1f);
    private static readonly Color ColStatusYellow = new Color(1f, 0.88f, 0.30f, 1f);
    private static readonly Color ColStatusWhite  = Color.white;
    private static readonly Color ColPriceLightGreen = new Color(0.74f, 1f, 0.74f, 1f);     // purchase price
    private static readonly Color ColPriceForestGreen = new Color(0.16f, 0.58f, 0.22f, 1f); // sale price
    private static readonly Color ColVendorBlue = new Color(0.27f, 0.62f, 1f, 1f);

    private const float IlIconW = 46f, IlItemW = 84f, IlNumW = 118f, IlPriceW = 92f, IlVendorW = 210f, IlDescMin = 220f, IlRushW = 132f;

    // ── Rush orders ──────────────────────────────────────────────────────────
    // "Rush Order" on a row QUEUES that item (enough whole pallets to cover what's short, minimum one) with its cheapest vendor.
    // Nothing is bought until "Dispatch All Rush Orders!" - that groups the queue by vendor and raises one PO per vendor (more
    // than one if it won't fit a trailer), exactly like dispatching from the Purchasing screen. "Clear All" empties the queue.
    private readonly Dictionary<string, int> _rushPallets = new();      // skuId -> pallets queued
    private const int RushCostMultiplier = 2;                           // the price of last-ditch convenience
    private VisualElement _ilTruckFill;
    private Label _ilRushTotal;
    private Button _ilSchedulerBtn;
    private Button _ilDispatchBtn, _ilClearBtn;
    private VisualElement _ilTopRow;

    private static readonly Color ColRushGreen     = new Color(0.16f, 0.62f, 0.26f, 1f);
    private static readonly Color ColRushGreenDark = new Color(0.07f, 0.34f, 0.13f, 1f);
    private static readonly Color ColRushRed       = new Color(0.78f, 0.14f, 0.14f, 1f);
    private static readonly Color ColRushOrange    = new Color(0.90f, 0.47f, 0.10f, 1f);

    private static Font _lilitaFont;
    private static void ApplyLilita(VisualElement el, int size)
    {
        if (_lilitaFont == null)
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
            if (guids.Length > 0) _lilitaFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            _lilitaFont = Resources.Load<Font>("Resource_Fonts/LilitaOne-Regular");
#endif
        }
        if (_lilitaFont != null) el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_lilitaFont));
        el.style.fontSize = size;
    }

    static Button RushButton(string text, Color bg, int fontSize, float padV, float padH)
    {
        var b = new Button { text = text };
        ApplyLilita(b, fontSize);
        b.style.color = new StyleColor(Color.white);
        b.style.backgroundColor = new StyleColor(bg);
        b.style.unityFontStyleAndWeight = FontStyle.Normal;
        b.style.paddingTop = b.style.paddingBottom = padV;
        b.style.paddingLeft = b.style.paddingRight = padH;
        b.style.marginLeft = b.style.marginRight = b.style.marginTop = b.style.marginBottom = 0;
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.35f));
        return b;
    }

    /// <summary>Whole pallets worth ordering for a row: enough to cover what is short (after inbound), at least one.</summary>
    static int RushPalletsFor(ItemAvailabilityRow r)
    {
        int per = Mathf.Max(1, r.CasesPerPallet);
        int need = Mathf.Max(0, r.Pending - r.OnHand - r.Inbound);
        return Mathf.Max(1, Mathf.CeilToInt(need / (float)per));
    }

    void RefreshRushBar()
    {
        if (_ilDispatchBtn == null) return;
        bool any = _rushPallets.Count > 0;
        _ilDispatchBtn.style.opacity = any ? 1f : 0.45f;
        _ilClearBtn.style.opacity = any ? 1f : 0.45f;

        // The trailer fills as items are queued (same graphic as the Purchasing screen).
        var lines = RushLines();
        float fill01 = lines.Count > 0 ? Mathf.Clamp01(TrailerCapacity.Plan(lines).Fill01) : 0f;
        if (_ilTruckFill != null)
            _ilTruckFill.style.width = fill01 * (PurchasingPanel.TruckBoxRightFrac - PurchasingPanel.TruckBoxLeftFrac) * PurchasingPanel.TruckFillBarWidth;
        if (_ilRushTotal != null)
        {
            ServiceLocator.TryGet<InventoryService>(out var invForCost);
            int pallets = _rushPallets.Values.Sum();
            int cost = 0;
            if (invForCost != null && ServiceLocator.TryGet<OrderService>(out var ordersForCost) && ordersForCost != null)
            {
                var byId = ItemAvailability.Build().ToDictionary(r => r.SkuId);
                foreach (var kv in _rushPallets)
                    if (byId.TryGetValue(kv.Key, out var row) && row.Sku != null && !string.IsNullOrEmpty(row.VendorId))
                        cost += kv.Value * row.CasesPerPallet * RushUnitPrice(row.VendorId, row.Sku) * RushCostMultiplier;
            }
            _ilRushTotal.text = any ? $"{pallets} plt  |  {FormatMoney(cost)}" : "Rush trailer empty";
        }
    }

    void ClearRushQueue()
    {
        if (_rushPallets.Count == 0) return;
        _rushPallets.Clear();
        RenderItemListing();
    }

    static int RushUnitPrice(string vendorId, SkuData sku)
    {
        ServiceLocator.TryGet<MarketService>(out var market);
        ServiceLocator.TryGet<VendorEconomyService>(out var economy);
        int priced = economy != null ? Mathf.RoundToInt(economy.GetEffectiveCost(vendorId, sku, market)) : 0;
        return priced > 0 ? priced : Mathf.RoundToInt(market != null ? market.CurrentPrice(sku) : sku.BuyValue);
    }

    /// <summary>The queue as trailer lines (whole pallets only - a 4-case order is still a full pallet, and is paid for as one).</summary>
    List<(SkuData sku, int cases)> RushLines()
    {
        var lines = new List<(SkuData, int)>();
        if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return lines;
        ServiceLocator.TryGet<OrderService>(out var orders);
        foreach (var kv in _rushPallets)
        {
            var sku = inv.GetSkuData(kv.Key);
            int per = orders != null ? orders.FullPalletCases(kv.Key) : 0;
            if (sku != null && per > 0) lines.Add((sku, kv.Value * per));
        }
        return lines;
    }

    void DispatchRushOrders()
    {
        if (_rushPallets.Count == 0) { UIToast.Show("No rush orders queued - click Rush Order on an item first."); return; }
        if (!ServiceLocator.TryGet<ShipmentService>(out var shipments) || shipments == null)
        { UIToast.Show("Purchasing is unavailable - the shipment service isn't running."); return; }
        ServiceLocator.TryGet<SimulationTimeService>(out var time);
        int today = time != null ? time.Day : 1;

        // Today's cheapest vendor per item (prices move between the click and the dispatch); everything rides ONE trailer.
        var rows = ItemAvailability.Build().ToDictionary(r => r.SkuId);
        var lines = RushLines();
        if (lines.Count == 0) { UIToast.Show("Nothing in the rush queue can be ordered right now."); return; }

        var plan = TrailerCapacity.Plan(lines);
        if (plan.OverCapacity)
        { UIToast.Show($"The rush trailer is over capacity ({plan.FloorSlotsUsed} of {TrailerCapacity.FloorSlots} floor positions). Take something off the queue."); return; }

        var items = new List<ShipmentLineItem>();
        int cost = 0;
        foreach (var pallet in plan.Pallets)
        {
            if (!rows.TryGetValue(pallet.SkuId, out var r) || r.Sku == null || string.IsNullOrEmpty(r.VendorId)) continue;
            int unit = RushUnitPrice(r.VendorId, r.Sku) * RushCostMultiplier;
            items.Add(new ShipmentLineItem(pallet.SkuId, pallet.Cases, unit, r.Sku.ShelfLifeDays)
            { FloorSlotIndex = pallet.FloorSlot, PalletTier = pallet.Tier });
            cost += pallet.Cases * unit;
        }
        if (items.Count == 0) { UIToast.Show("No vendor currently sells anything in the rush queue."); return; }
        if (_money != null && cost > _money.CurrentCapital)
        { UIToast.Show($"Not enough money for the rush trailer: it costs {FormatMoney(cost)} (double), you have {FormatMoney(_money.CurrentCapital)}."); return; }

        var po = shipments.CreatePlayerPurchaseOrder(OrderNumberGenerator.GetNext('I', 'G', today), ShipmentService.RushSupplierId,
                                                     "Rush Courier", items, today, 0);
        if (po == null) { UIToast.Show("Couldn't raise the rush PO."); return; }

        _rushPallets.Clear();
        UIToast.Show($"RUSH PO {po.PONumber} raised - {po.TotalUnits:N0} case(s), {FormatMoney(po.TotalCost)} (2x cost), one trailer. Book it a door on the Scheduler.");
        RenderItemListing();
    }

    /// <summary>Closes the report windows and opens the standalone Scheduler (same path the Contracts panel's SCHEDULER button uses).</summary>
    void OpenSchedulerFromReport()
    {
        var topBar = UnityEngine.Object.FindAnyObjectByType<TopBarUI>();
        var scheduler = topBar != null ? topBar.SchedulerPanel : null;
        if (scheduler == null) { UIToast.Show("Couldn't open the scheduler - the panel isn't loaded."); return; }
        CloseAll();
        UIKeyBindingManager.Instance?.CloseAll();
        scheduler.Show();
    }

    void BuildItemListing(VisualElement parent)
    {
        _ilRefreshAt = Time.unscaledTime + 4f;
        parent.style.flexDirection = FlexDirection.Column;

        // Legend
        var legend = new VisualElement();
        legend.style.flexDirection = FlexDirection.Row; legend.style.alignItems = Align.Center;
        legend.style.flexShrink = 0; legend.style.marginBottom = 8;
        legend.Add(LegendItem(ColStatusWhite,  "OK - covered, or nothing owed"));
        legend.Add(LegendItem(ColStatusYellow, "Potential scratch - short on the shelf, but a PO lands before the order is due"));
        legend.Add(LegendItem(ColStatusRed,    "Insufficient balance - short, with nothing arriving in time"));
        parent.Add(legend);

        _ilTopRow = new VisualElement();
        _ilTopRow.style.flexDirection = FlexDirection.Row; _ilTopRow.style.alignItems = Align.Center; _ilTopRow.style.flexShrink = 0; _ilTopRow.style.marginBottom = 8;
        parent.Add(_ilTopRow);

        _ilChipRow = new VisualElement();
        _ilChipRow.style.flexDirection = FlexDirection.Row; _ilChipRow.style.flexGrow = 1;
        _ilTopRow.Add(_ilChipRow);

        // Right-hand end, above the Cheapest Vendor column.
        // The rush trailer: fills as items are queued. Everything ordered here travels on this one truck.
        var truckBox = new VisualElement();
        truckBox.style.alignItems = Align.Center; truckBox.style.flexShrink = 0; truckBox.style.marginRight = 14;
        truckBox.Add(PurchasingPanel.BuildTruckFillBar(out _ilTruckFill));
        _ilRushTotal = Lbl("Rush trailer empty", bold: true, size: 15f);
        _ilRushTotal.style.color = new StyleColor(ColBlueTint);
        truckBox.Add(_ilRushTotal);
        _ilTopRow.Add(truckBox);

        _ilDispatchBtn = RushButton("Dispatch All Rush Orders!", ColRushGreen, 24, 9, 22);
        _ilDispatchBtn.clicked += DispatchRushOrders;
        _ilDispatchBtn.style.marginRight = 10;
        _ilClearBtn = RushButton("Clear All", ColRushRed, 24, 9, 22);
        _ilClearBtn.clicked += ClearRushQueue;
        _ilClearBtn.style.marginRight = 10;
        _ilSchedulerBtn = RushButton("Scheduler", ColRushOrange, 24, 9, 22);
        _ilSchedulerBtn.clicked += OpenSchedulerFromReport;
        _ilTopRow.Add(_ilDispatchBtn);
        _ilTopRow.Add(_ilClearBtn);
        _ilTopRow.Add(_ilSchedulerBtn);
        RefreshRushBar();

        _ilHeaderRow = new VisualElement();
        _ilHeaderRow.style.flexDirection = FlexDirection.Row; _ilHeaderRow.style.alignItems = Align.Center;
        _ilHeaderRow.style.flexShrink = 0; _ilHeaderRow.style.height = 54;
        _ilHeaderRow.style.backgroundColor = new StyleColor(ColBlueDark);
        parent.Add(_ilHeaderRow);

        _ilRows = new ScrollView(ScrollViewMode.Vertical);
        _ilRows.style.flexGrow = 1;
        parent.Add(_ilRows);

        RenderItemListing();
    }

    static VisualElement LegendItem(Color c, string text)
    {
        var item = new VisualElement();
        item.style.flexDirection = FlexDirection.Row; item.style.alignItems = Align.Center; item.style.marginRight = 22;
        var sw = new VisualElement();
        sw.style.width = 16; sw.style.height = 16; sw.style.marginRight = 7;
        sw.style.backgroundColor = new StyleColor(c);
        sw.style.borderTopLeftRadius = sw.style.borderTopRightRadius = sw.style.borderBottomLeftRadius = sw.style.borderBottomRightRadius = 4;
        item.Add(sw);
        var l = Lbl(text, size: 15f);
        l.style.color = new StyleColor(ColLabelNormal);
        item.Add(l);
        return item;
    }

    void RenderItemListing()
    {
        if (_ilRows == null) return;
        var rows = ItemAvailability.Build();

        int nAll = rows.Count;
        int nOk = rows.Count(r => r.Status == AvailabilityStatus.Ok);
        int nScratch = rows.Count(r => r.Status == AvailabilityStatus.PotentialScratch);
        int nBad = rows.Count(r => r.Status == AvailabilityStatus.Insufficient);

        // Filter chips (rebuilt each time so the counts stay live and the active one is highlighted).
        _ilChipRow.Clear();
        _ilChipRow.Add(FilterChip("All", nAll, ItemFilter.All, ColStatusWhite));
        _ilChipRow.Add(FilterChip("Quantity OK", nOk, ItemFilter.Ok, ColStatusWhite));
        _ilChipRow.Add(FilterChip("Potential Scratch", nScratch, ItemFilter.Scratch, ColStatusYellow));
        _ilChipRow.Add(FilterChip("Insufficient Balance", nBad, ItemFilter.Insufficient, ColStatusRed));

        // Column headers (click to sort; click again to flip).
        _ilHeaderRow.Clear();
        _ilHeaderRow.Add(HeaderCell("", IlIconW, null));
        _ilHeaderRow.Add(HeaderCell("Item #", IlItemW, ItemSort.Item));
        _ilHeaderRow.Add(HeaderCell("Description", IlDescMin, ItemSort.Description, grow: true));
        _ilHeaderRow.Add(HeaderCell("Balance On Hand", IlNumW, ItemSort.OnHand, right: true));
        _ilHeaderRow.Add(HeaderCell("On Inbound Order", IlNumW, ItemSort.Inbound, right: true));
        _ilHeaderRow.Add(HeaderCell("Pending Shipment", IlNumW, ItemSort.Pending, right: true));
        _ilHeaderRow.Add(HeaderCell("Potential Scratches", IlNumW + 10f, ItemSort.Net, right: true));
        _ilHeaderRow.Add(HeaderCell("Purchase Price", IlPriceW, ItemSort.Cost, right: true));
        _ilHeaderRow.Add(HeaderCell("Sale Price", IlPriceW, ItemSort.Sale, right: true));
        _ilHeaderRow.Add(HeaderCell("Cheapest Vendor", IlVendorW, ItemSort.Vendor));
        var x2 = Lbl("x2 COST for RUSH!", size: 20f);
        ApplyLilita(x2, 20);
        x2.style.color = new StyleColor(new Color(1f, 0.22f, 0.22f, 1f));
        x2.style.width = IlRushW; x2.style.flexShrink = 0;
        x2.style.whiteSpace = WhiteSpace.Normal;
        x2.style.unityTextAlign = TextAnchor.MiddleCenter;
        _ilHeaderRow.Add(x2);
        RefreshRushBar();

        IEnumerable<ItemAvailabilityRow> shown = rows;
        if (_ilFilter == ItemFilter.Ok) shown = rows.Where(r => r.Status == AvailabilityStatus.Ok);
        else if (_ilFilter == ItemFilter.Scratch) shown = rows.Where(r => r.Status == AvailabilityStatus.PotentialScratch);
        else if (_ilFilter == ItemFilter.Insufficient) shown = rows.Where(r => r.Status == AvailabilityStatus.Insufficient);

        Func<ItemAvailabilityRow, IComparable> key = _ilSort switch
        {
            ItemSort.Description => r => r.Description ?? "",
            ItemSort.OnHand => r => r.OnHand,
            ItemSort.Inbound => r => r.Inbound,
            ItemSort.Pending => r => r.Pending,
            ItemSort.Net => r => Shortfall(r),
            ItemSort.Cost => r => r.PurchasePrice,
            ItemSort.Sale => r => r.SalePrice,
            ItemSort.Vendor => r => r.Vendor ?? "",
            _ => r => r.Sku != null ? r.Sku.ItemNumber : 0,
        };
        var list = (_ilAscending ? shown.OrderBy(key) : shown.OrderByDescending(key)).ThenBy(r => r.Sku != null ? r.Sku.ItemNumber : 0).ToList();

        var keep = _ilRows.scrollOffset;
        _ilRows.Clear();
        for (int i = 0; i < list.Count; i++) _ilRows.Add(ItemRowElement(list[i], i % 2 == 0 ? ColRowA : ColRowB));
        if (list.Count == 0) _ilRows.Add(EmptyMsg("No items match this filter."));
        _ilRows.schedule.Execute(() => _ilRows.scrollOffset = keep).ExecuteLater(1);
    }

    /// <summary>What the Potential Scratches column shows: 0 when the shelf covers the pending shipments, otherwise the
    /// (negative) number of cases short = on hand - pending. Cases still inbound do not change the number; they change its colour.</summary>
    static int Shortfall(ItemAvailabilityRow r) => Mathf.Min(0, r.OnHand - r.Pending);

    VisualElement FilterChip(string text, int count, ItemFilter filter, Color accent)
    {
        bool active = _ilFilter == filter;
        var chip = new Button(() => { _ilFilter = filter; RenderItemListing(); });
        chip.text = $"{text} ({count})";
        chip.style.marginRight = 8; chip.style.marginLeft = 0; chip.style.marginTop = 0; chip.style.marginBottom = 0;
        chip.style.paddingLeft = 14; chip.style.paddingRight = 14; chip.style.paddingTop = 6; chip.style.paddingBottom = 6;
        chip.style.fontSize = 17;
        chip.style.unityFontStyleAndWeight = FontStyle.Bold;
        chip.style.color = new StyleColor(active ? Color.black : accent);
        chip.style.backgroundColor = new StyleColor(active ? accent : ColRowB);
        chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius = chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 8;
        chip.style.borderTopWidth = chip.style.borderBottomWidth = chip.style.borderLeftWidth = chip.style.borderRightWidth = 2;
        chip.style.borderTopColor = chip.style.borderBottomColor = chip.style.borderLeftColor = chip.style.borderRightColor = new StyleColor(accent);
        return chip;
    }

    VisualElement HeaderCell(string text, float width, ItemSort? sort, bool right = false, bool grow = false)
    {
        bool active = sort.HasValue && _ilSort == sort.Value;
        var l = Lbl(text + (active ? (_ilAscending ? "  ▲" : "  ▼") : ""), bold: true, size: 15f);
        l.style.unityTextAlign = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        l.style.color = new StyleColor(active ? ColOrange : ColBlueTint);
        l.style.paddingLeft = 6; l.style.paddingRight = 8;
        l.style.flexShrink = 0;
        l.style.whiteSpace = WhiteSpace.Normal;   // long headers wrap onto two lines instead of running into the next column
        if (grow) { l.style.flexGrow = 1; l.style.minWidth = width; } else l.style.width = width;
        if (sort.HasValue)
        {
            var s = sort.Value;
            l.RegisterCallback<ClickEvent>(_ =>
            {
                if (_ilSort == s) _ilAscending = !_ilAscending; else { _ilSort = s; _ilAscending = true; }
                RenderItemListing();
            });
        }
        return l;
    }

    VisualElement ItemRowElement(ItemAvailabilityRow r, Color bg)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center;
        row.style.height = 46; row.style.flexShrink = 0;
        row.style.backgroundColor = new StyleColor(bg);

        var iconBox = new VisualElement();
        iconBox.style.width = IlIconW; iconBox.style.height = 46; iconBox.style.flexShrink = 0;
        iconBox.style.alignItems = Align.Center; iconBox.style.justifyContent = Justify.Center;
        if (r.Icon != null)
        {
            var img = new Image { sprite = r.Icon, scaleMode = ScaleMode.ScaleToFit };
            img.style.width = 38; img.style.height = 38;
            iconBox.Add(img);
        }
        row.Add(iconBox);

        row.Add(Cell(r.Sku != null ? r.Sku.ItemNumber.ToString() : r.SkuId, IlItemW, false, ColBlueTint));
        row.Add(Cell(r.Description, IlDescMin, false, Color.white, grow: true));
        row.Add(Cell(r.OnHand.ToString("N0"), IlNumW, true, Color.white, bold: true));
        row.Add(Cell(r.Inbound.ToString("N0"), IlNumW, true, Color.white));
        row.Add(Cell(r.Pending.ToString("N0"), IlNumW, true, Color.white));

        // Only a shortage is shown (negative); anything the shelf covers reads 0. Red = nothing arrives in time to cover it,
        // yellow = a PO lands before the order is due.
        int shortfall = Shortfall(r);
        Color netColor = shortfall >= 0 ? ColStatusWhite
                       : r.Status == AvailabilityStatus.PotentialScratch ? ColStatusYellow : ColStatusRed;
        row.Add(Cell(shortfall.ToString("N0"), IlNumW + 10f, true, netColor, bold: true));

        row.Add(Cell("$" + r.PurchasePrice.ToString("0.00"), IlPriceW, true, ColPriceLightGreen));
        row.Add(Cell("$" + r.SalePrice.ToString("0.00"), IlPriceW, true, ColPriceForestGreen));
        row.Add(Cell(r.Vendor, IlVendorW, false, ColVendorBlue));
        row.Add(RushCell(r));
        return row;
    }

    /// <summary>The green "Rush Order" button on a row. Click queues the item (click again to take it back off).</summary>
    VisualElement RushCell(ItemAvailabilityRow r)
    {
        var cell = new VisualElement();
        cell.style.width = IlRushW; cell.style.flexShrink = 0; cell.style.alignItems = Align.Center; cell.style.justifyContent = Justify.Center;

        bool canBuy = !string.IsNullOrEmpty(r.VendorId) && r.CasesPerPallet > 0;
        bool queued = _rushPallets.TryGetValue(r.SkuId, out int queuedPallets);
        var b = RushButton(queued ? $"Queued {queuedPallets} plt" : "Rush Order", queued ? ColRushGreenDark : ColRushGreen, 16, 4, 10);
        b.style.width = IlRushW - 14f;
        if (queued) b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(Color.white);
        if (!canBuy) b.style.opacity = 0.35f;
        string skuId = r.SkuId;
        b.clicked += () =>
        {
            if (!canBuy) { UIToast.Show("No vendor currently sells that item."); return; }
            if (_rushPallets.ContainsKey(skuId)) _rushPallets.Remove(skuId);
            else
            {
                // Everything rides ONE trailer: refuse an item that wouldn't fit alongside what's already queued.
                int pallets = RushPalletsFor(r);
                var lines = RushLines();
                if (TrailerCapacity.WouldOverflow(lines, r.Sku, pallets * r.CasesPerPallet, out _))
                {
                    UIToast.Show("The rush trailer is full - that won't fit. Dispatch what's queued, or take something off first.");
                    return;
                }
                _rushPallets[skuId] = pallets;
            }
            RenderItemListing();
        };
        cell.Add(b);
        return cell;
    }

    static Label Cell(string text, float width, bool right, Color color, bool grow = false, bool bold = false)
    {
        var l = Lbl(text, bold: bold, size: 18f);
        l.style.unityTextAlign = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        l.style.color = new StyleColor(color);
        l.style.paddingLeft = 6; l.style.paddingRight = 8;
        l.style.flexShrink = 0;
        l.style.overflow = Overflow.Hidden;
        if (grow) { l.style.flexGrow = 1; l.style.minWidth = width; } else l.style.width = width;
        return l;
    }

    // ── Shared row helper ────────────────────────────────────────────────────
    static void AddStatRow(VisualElement parent, string key, string value, int rowIndex)
    {
        var val = ValueLabel();
        val.text = value;
        parent.Add(DataRow(key, val, rowIndex % 2 == 0 ? ColRowA : ColRowB, false, LargeKeySize, LargeRowHeight, LargeValueWidth));
    }
}
