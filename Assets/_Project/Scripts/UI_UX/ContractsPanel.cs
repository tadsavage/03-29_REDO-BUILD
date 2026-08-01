using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// The Outbound Order Manager. Three tabs over one question — where work comes from, how the work
/// you already took is going, and when each trailer is getting a door.
///
///   OFFERS    contracts available to sign. Signing is where demand enters the game in a real build;
///             the Dev Console's "Create Test Order" button stays as a debug override.
///   ACCOUNTS  what you're actually running, and how well. Per-contract delivered/late/earned, plus
///             the only place a standing account can be cancelled.
///   SCHEDULE  the dock appointment book — two-hour blocks, one row per block, as many slots per
///             block as you have outbound doors.
///
/// Why Offers and Accounts are separate rather than one greyed-out list: a delivered wholesale deal
/// stays signed forever (that's what keeps the Sign button off it), so under the old single list it
/// sat in the offers permanently at 45% opacity, taking a slot and telling the player nothing. An
/// offer you can act on and an account you're being judged on are different objects.
///
/// Palette and font are taken from WorkQueuePanel deliberately, so the two read as one family. Each
/// panel in this project declares its own colour constants rather than sharing a theme class — that's
/// the existing convention here, not an oversight.
/// </summary>
public class ContractsPanel : IUIPanel
{
    private static readonly Color ColBg          = new Color(20f / 255f, 28f / 255f, 38f / 255f, 0.92f);
    private static readonly Color ColBorder      = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleText   = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColSubtleText  = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    private static readonly Color ColOrange      = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeEdge  = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    private static readonly Color ColOrangeText  = new Color(0xFD / 255f, 0xE8 / 255f, 0xCC / 255f, 1f);
    private static readonly Color ColOrangeHover = new Color(0xC6 / 255f, 0x7F / 255f, 0x42 / 255f, 1f);
    private static readonly Color ColCardEven    = new Color(36f / 255f, 48f / 255f, 62f / 255f, 0.65f);
    private static readonly Color ColCardOdd     = new Color(30f / 255f, 40f / 255f, 52f / 255f, 0.65f);
    private static readonly Color ColMoney       = new Color(0x7E / 255f, 0xD6 / 255f, 0x8A / 255f, 1f);
    private static readonly Color ColWholesale   = new Color(0xF2 / 255f, 0xC2 / 255f, 0x5A / 255f, 1f);
    private static readonly Color ColBlueEdge    = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColDanger      = new Color(0xE2 / 255f, 0x4B / 255f, 0x4A / 255f, 1f);
    private static readonly Color ColDangerSoft  = new Color(0xF0 / 255f, 0x95 / 255f, 0x95 / 255f, 1f);
    private static readonly Color ColStat        = new Color(30f / 255f, 40f / 255f, 52f / 255f, 1f);

    // Schedule chip fills — one per AppointmentKind, matching the legend at the foot of that tab.
    private static readonly Color ColChipOut     = new Color(26f / 255f, 58f / 255f, 74f / 255f, 1f);
    private static readonly Color ColChipOutText = new Color(0x9F / 255f, 0xCB / 255f, 0xE4 / 255f, 1f);
    private static readonly Color ColChipIn      = new Color(58f / 255f, 42f / 255f, 26f / 255f, 1f);
    private static readonly Color ColChipWhole   = new Color(42f / 255f, 32f / 255f, 51f / 255f, 1f);
    private static readonly Color ColChipWholeTx = new Color(0xAF / 255f, 0xA9 / 255f, 0xEC / 255f, 1f);
    private static readonly Color ColChipPurple  = new Color(0x53 / 255f, 0x4A / 255f, 0xB7 / 255f, 1f);
    private static readonly Color ColEmptyText   = new Color(0x4D / 255f, 0x65 / 255f, 0x77 / 255f, 1f);
    private static readonly Color ColTabIdle     = new Color(28f / 255f, 38f / 255f, 50f / 255f, 1f);
    private static readonly Color ColTabHover    = new Color(40f / 255f, 54f / 255f, 70f / 255f, 1f);

    private const float ModalWidth  = 1040f;
    private const float ModalHeight = 680f;
    private const float IconSize    = 72f;   // Offers cards
    private const float IconSizeSm  = 44f;   // Accounts rows
    private const float IconSizeTiny = 18f;  // Schedule chips

    /// <summary>Inactive tab height. The active tab is taller and bottom-aligned against the divider,
    /// so the selected one physically stands proud of the others.</summary>
    private const float TabHeight = 32f;
    private const float TabHeightActive = 38f;

    /// <summary>Schedule grid column widths. Fixed, not flexible — see BuildScheduleRow for why that's
    /// what makes horizontal scrolling possible at all.</summary>
    private const float TimeColWidth = 92f;
    private const float SlotWidth = 168f;
    private const float FullFlagWidth = 44f;
    /// <summary>Time cells are given an explicit height so the frozen column's opaque background
    /// covers the full row rather than just the text's own line box.</summary>
    private const float ScheduleRowHeight = 30f;

    /// <summary>How far either side of today the Schedule tab will page. Backwards is bounded by
    /// DockScheduleService's own purge of old appointments; forwards is just far enough to see the
    /// consequences of a lead time.</summary>
    private const int ScheduleDaysBack = 2;
    private const int ScheduleDaysAhead = 7;

    /// <summary>Customers was called "Offers" until 2026-08-01. Renamed because contract offers will
    /// eventually appear on their own over time, driven by the business's reputation — at which point
    /// the tab is a customer board you watch, not a static list of offers you shop.</summary>
    private enum Tab { Customers, Accounts, Schedule }

    private readonly VisualElement _overlay;
    private readonly VisualElement _modal;
    private readonly VisualElement _tabBar;
    /// <summary>Stationary strip between the tab bar and the scroll view. See Build.</summary>
    private readonly VisualElement _tabHeader;
    private readonly ScrollView _content;
    private readonly Label _footerMessage;
    private bool _visible;

    private Tab _tab = Tab.Customers;

    /// <summary>Day the Schedule tab is looking at. int.MinValue means "not set yet" — resolved to
    /// the live day on first Show so a panel built at startup doesn't pin itself to day 0.</summary>
    private int _scheduleDay = int.MinValue;

    /// <summary>
    /// The Schedule grid's time cells, so horizontal scrolling can hold them still.
    ///
    /// Frozen by COUNTER-TRANSLATION rather than by splitting the grid into a fixed left pane and a
    /// scrolling right pane. Two panes means two scroll views whose vertical offsets have to be kept
    /// in sync and whose row heights have to match exactly — and a chip row is not necessarily the
    /// same height as an empty-slot row, so any divergence shows up as the times sliding out of
    /// alignment with their own rows. Offsetting the cells inside one scroll view can't desync,
    /// because they're still in the rows they belong to.
    /// </summary>
    private readonly List<VisualElement> _scheduleTimeCells = new();

    /// <summary>Appointment picked up for a move. The Schedule tab is select-then-place rather than
    /// drag-and-drop: pointer capture inside a ScrollView fights the scroller, and a two-click move
    /// is also the only interaction that works if the source and target blocks aren't both on screen.</summary>
    private string _selectedAppointmentId;

    // Drag state. The modal is absolutely positioned so left/top can be written directly; the offset
    // is captured at pointer-down so the window doesn't jump to centre itself under the cursor.
    private bool _dragging;
    private Vector2 _dragOffset;
    private bool _placed; // false until the first Show centres it

    private static Font _lilita;

    public ContractsPanel(VisualElement root)
    {
        _overlay = Build(out _modal, out _tabBar, out _tabHeader, out _content, out _footerMessage);
        root.Add(_overlay);
        Hide();
    }

    public bool IsVisible => _visible;
    public bool IsOpen => _visible;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.pickingMode = PickingMode.Position;
        if (_scheduleDay == int.MinValue) _scheduleDay = CurrentDay();
        Rebuild();
        CentreOnce();
    }

    /// <summary>Centres the modal the FIRST time it's shown and never again — reopening should return
    /// it to wherever the player dragged it, not yank it back to the middle.</summary>
    private void CentreOnce()
    {
        if (_placed) return;
        _overlay.schedule.Execute(() =>
        {
            if (_placed) return;
            Rect r = _overlay.worldBound;
            if (r.width < 1f) return; // no layout yet — try again next Show
            _modal.style.left = Mathf.Max(0f, (r.width - ModalWidth) * 0.5f);
            _modal.style.top = Mathf.Max(0f, (r.height - ModalHeight) * 0.5f);
            _placed = true;
        }).ExecuteLater(16);
    }

    public void Hide()
    {
        _visible = false;
        _selectedAppointmentId = null;
        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
    }

    public void Dispose()
    {
        if (_overlay.parent != null) _overlay.RemoveFromHierarchy();
    }

    // ── Shell ────────────────────────────────────────────────────────────────

    private VisualElement Build(out VisualElement modalOut, out VisualElement tabBarOut,
                                out VisualElement tabHeaderOut, out ScrollView contentOut,
                                out Label footerMessage)
    {
        var overlay = new VisualElement { name = "contracts-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0; overlay.style.bottom = 0;
        overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));

        var modal = new VisualElement { name = "contracts-modal" };
        // Absolute rather than centred by the overlay's flex: a dragged window needs left/top it can
        // own, and flex centring would fight every frame with whatever the drag writes.
        modal.style.position = Position.Absolute;
        modal.style.width = ModalWidth;
        modal.style.height = ModalHeight;
        modal.style.backgroundColor = new StyleColor(ColBg);
        modal.style.borderTopWidth = modal.style.borderBottomWidth =
            modal.style.borderLeftWidth = modal.style.borderRightWidth = 3;
        modal.style.borderTopColor = modal.style.borderBottomColor =
            modal.style.borderLeftColor = modal.style.borderRightColor = new StyleColor(ColBorder);
        modal.style.borderTopLeftRadius = modal.style.borderTopRightRadius =
            modal.style.borderBottomLeftRadius = modal.style.borderBottomRightRadius = 16;
        modal.style.paddingTop = 14; modal.style.paddingBottom = 14;
        modal.style.paddingLeft = 16; modal.style.paddingRight = 16;

        // Title bar
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.height = 52;
        titleBar.style.marginBottom = 6;

        var title = new Label("OUTBOUND CONTRACTS");
        ApplyFont(title, bold: true, size: 28);
        title.style.color = new StyleColor(ColTitleText);
        title.style.flexGrow = 1;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(title);

        var close = new Button(Hide) { text = "✕" };
        StyleSquareButton(close);
        titleBar.Add(close);
        modal.Add(titleBar);

        // The title bar is the drag handle. Registered on the BAR, not the modal, so dragging can't
        // start from a card or swallow a click meant for a Sign button.
        titleBar.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            if (evt.target is Button) return; // let ✕ do its job
            _dragging = true;
            _dragOffset = (Vector2)evt.position - new Vector2(modal.worldBound.x, modal.worldBound.y);
            titleBar.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });
        titleBar.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!_dragging) return;
            Rect bounds = _overlay.worldBound;
            Vector2 target = (Vector2)evt.position - _dragOffset - new Vector2(bounds.x, bounds.y);
            // Keep at least a strip of the title bar on screen so it can always be grabbed back.
            float maxX = Mathf.Max(0f, bounds.width - 120f);
            float maxY = Mathf.Max(0f, bounds.height - 60f);
            modal.style.left = Mathf.Clamp(target.x, -(ModalWidth - 120f), maxX);
            modal.style.top = Mathf.Clamp(target.y, 0f, maxY);
            _placed = true;
            evt.StopPropagation();
        });
        titleBar.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!_dragging) return;
            _dragging = false;
            titleBar.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        });

        var tabBar = new VisualElement();
        tabBar.style.flexDirection = FlexDirection.Row;
        // FlexEnd so tabs sit ON the divider regardless of their individual heights — that's what lets
        // the active tab be taller and still line up along the bottom edge.
        tabBar.style.alignItems = Align.FlexEnd;
        tabBar.style.flexShrink = 0;
        tabBar.style.borderBottomWidth = 2;
        tabBar.style.borderBottomColor = new StyleColor(ColBlueEdge);
        tabBar.style.marginBottom = 10;
        modal.Add(tabBar);

        // Fixed header strip, OUTSIDE the scroll view. The Schedule tab's day switcher and colour
        // legend have to stay put while the grid scrolls under them — anything inside _content would
        // scroll away both vertically and (once horizontal scrolling is on) sideways. Other tabs leave
        // it empty and it collapses to nothing.
        var tabHeader = new VisualElement();
        tabHeader.style.flexShrink = 0;
        modal.Add(tabHeader);

        var content = new ScrollView(ScrollViewMode.Vertical);
        content.style.flexGrow = 1;
        modal.Add(content);

        // Polled rather than driven by horizontalScroller.valueChanged: that event does NOT fire when
        // scrollOffset is set programmatically, so the frozen column silently desynced from any scroll
        // the panel itself performed. Reading scrollOffset every frame catches the wheel, a scrollbar
        // drag, and a programmatic jump identically. Guarded on change, so the usual cost is one float
        // comparison. Scheduled ONCE here — doing it per Rebuild would stack a poller per refresh.
        content.schedule.Execute(SyncFrozenTimeColumn).Every(16);

        footerMessage = new Label();
        ApplyFont(footerMessage, size: 15);
        footerMessage.style.color = new StyleColor(ColSubtleText);
        footerMessage.style.marginTop = 8;
        footerMessage.style.whiteSpace = WhiteSpace.Normal;
        modal.Add(footerMessage);

        overlay.Add(modal);
        modalOut = modal;
        tabBarOut = tabBar;
        tabHeaderOut = tabHeader;
        contentOut = content;
        return overlay;
    }

    private void SetTab(Tab tab)
    {
        if (_tab == tab) return;
        _tab = tab;
        _selectedAppointmentId = null; // a half-finished move shouldn't survive a tab change
        Rebuild();
    }

    private void BuildTabBar()
    {
        _tabBar.Clear();
        var arrivals = Arrivals();
        var schedule = Schedule();

        int offerCount = arrivals != null ? arrivals.AvailableOffers.Count() : 0;
        int accountCount = arrivals != null
            ? arrivals.RunningAccounts.Count() + arrivals.DeliveredWholesale.Count()
            : 0;

        string scheduleBadge = "—";
        if (schedule != null)
        {
            int cap = schedule.CapacityPerBlock * DockScheduleService.BlocksPerDay;
            int booked = schedule.Appointments.Count(a => a.Day == _scheduleDay);
            scheduleBadge = cap > 0 ? $"{booked}/{cap}" : "no doors";
        }

        _tabBar.Add(MakeTab("Customers", offerCount.ToString(), Tab.Customers));
        _tabBar.Add(MakeTab("Accounts", accountCount.ToString(), Tab.Accounts));
        _tabBar.Add(MakeTab("Schedule", scheduleBadge, Tab.Schedule));
    }

    /// <summary>
    /// Debug-only order triggers, moved here from the Tools window's "Outbound Simulator" section on
    /// 2026-08-01. Outbound debug belongs beside the outbound UI, and the Tools window's copy was
    /// misleading now that signed contracts are the real source of orders.
    ///
    /// Styled as a muted outline rather than a game action on purpose — it should never be mistaken
    /// for a thing the player is supposed to press. Hidden entirely when ToolsWindowController isn't
    /// in the scene, which is also what will hide it in a shipping build.
    /// </summary>
    private VisualElement BuildDevCluster()
    {
        var wrap = new VisualElement();
        wrap.style.flexDirection = FlexDirection.Row;
        wrap.style.alignItems = Align.Center;
        wrap.style.marginBottom = 2;

        if (ToolsWindowController.Instance == null) return wrap;

        var tag = MakeText("DEV", 10, ColSubtleText, bold: true);
        tag.style.marginRight = 6;
        wrap.Add(tag);

        var customer = new Button(OnDevAddCustomer) { text = "TEST CUSTOMER" };
        StyleDevButton(customer);
        customer.tooltip = "Debug: put one new signable customer offer on this tab. Stands in for " +
                           "reputation-driven arrival until that exists.";
        wrap.Add(customer);

        var truck = new Button(() => ToolsWindowController.Instance.SpawnOutboundTruckDebug()) { text = "OUTBOUND TRUCK" };
        StyleDevButton(truck);
        truck.tooltip = "Debug: send an outbound truck to the first door with staged pallets.";
        wrap.Add(truck);

        return wrap;
    }

    /// <summary>
    /// Slides every time cell right by exactly the amount the grid scrolled left, so it lands back
    /// where it started and the column reads as pinned.
    ///
    /// `left` on a Position.Relative element is a visual offset only — it does NOT move siblings — so
    /// the slot columns keep their true positions and the row's width is unaffected. The cells are
    /// also raised above their siblings (see BuildScheduleRow) and painted opaque, otherwise the slots
    /// would slide over the top of them instead of underneath.
    /// </summary>
    private void SyncFrozenTimeColumn()
    {
        if (_tab != Tab.Schedule || _scheduleTimeCells.Count == 0) return;

        float x = _content.scrollOffset.x;
        if (Mathf.Approximately(x, _lastHScroll)) return;
        _lastHScroll = x;

        foreach (var cell in _scheduleTimeCells)
            if (cell != null) cell.style.left = x;
    }

    /// <summary>Last applied horizontal offset. Reset to NaN on rebuild so the next poll always
    /// re-applies — freshly built cells start at left 0 regardless of where the view is scrolled.</summary>
    private float _lastHScroll = float.NaN;

    private void OnDevAddCustomer()
    {
        string who = ToolsWindowController.Instance != null
            ? ToolsWindowController.Instance.CreateTestCustomerOffer()
            : null;

        UIToast.Show(who != null
            ? $"{who} is looking for a warehouse."
            : "Couldn't generate an offer — check the console.");
        Rebuild(); // the new card has to appear on the tab you're already looking at
    }

    /// <summary>
    /// One folder tab. Built to look like a physical tab rather than a row of words: every tab has a
    /// raised card face, its own outline and rounded top corners, so an inactive tab still reads as
    /// something you can press. The first pass drew inactive tabs as bare transparent text and they
    /// looked like a subtitle — a player had no reason to think there was anything behind them.
    ///
    /// The active tab is filled orange AND sits 2px lower with no bottom border, so it breaks through
    /// the divider line under the row and joins the content below it. That break is what actually
    /// sells the metaphor; colour alone just looks like a highlighted word.
    /// </summary>
    private VisualElement MakeTab(string label, string badge, Tab tab)
    {
        bool active = _tab == tab;

        var btn = new Button(() => SetTab(tab));
        btn.style.height = active ? TabHeightActive : TabHeight;
        btn.style.marginLeft = 0; btn.style.marginRight = 3;
        // Bottom-align every tab against the divider, so the taller active one grows downward over
        // the line instead of upward away from it.
        btn.style.marginTop = active ? 0 : TabHeightActive - TabHeight;
        btn.style.marginBottom = active ? -2 : 0;
        btn.style.paddingLeft = 18; btn.style.paddingRight = 18;
        btn.style.paddingTop = 0; btn.style.paddingBottom = 0;
        btn.style.flexDirection = FlexDirection.Row;
        btn.style.alignItems = Align.Center;

        btn.style.borderTopWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 2;
        btn.style.borderBottomWidth = active ? 0 : 2;
        var edge = active ? ColOrangeEdge : ColBlueEdge;
        btn.style.borderTopColor = btn.style.borderLeftColor =
            btn.style.borderRightColor = btn.style.borderBottomColor = new StyleColor(edge);
        btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 8;
        btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 0;
        btn.style.backgroundColor = new StyleColor(active ? ColOrange : ColTabIdle);

        var text = MakeText(label, active ? 15 : 14, active ? ColOrangeText : ColSubtleText, bold: true);
        btn.Add(text);

        // Count in its own pill so it reads as data about the tab, not part of its name. Without this
        // "Schedule 4/60" scans as a four-word title.
        if (!string.IsNullOrEmpty(badge))
        {
            var pill = MakeText(badge, 11, active ? ColOrangeText : ColTitleText, bold: true);
            pill.style.marginLeft = 7;
            pill.style.paddingLeft = 6; pill.style.paddingRight = 6;
            pill.style.paddingTop = 1; pill.style.paddingBottom = 1;
            pill.style.backgroundColor = new StyleColor(active
                ? new Color(0f, 0f, 0f, 0.22f)
                : new Color(0f, 0f, 0f, 0.30f));
            pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius =
                pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 7;
            pill.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.Add(pill);
        }

        if (!active)
        {
            btn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                btn.style.backgroundColor = new StyleColor(ColTabHover);
                text.style.color = new StyleColor(ColTitleText);
            });
            btn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                btn.style.backgroundColor = new StyleColor(ColTabIdle);
                text.style.color = new StyleColor(ColSubtleText);
            });
        }
        return btn;
    }

    private void Rebuild()
    {
        BuildTabBar();
        _tabHeader.Clear();
        _content.Clear();
        _scheduleTimeCells.Clear(); // stale cells belong to elements that were just destroyed
        _lastHScroll = float.NaN;   // force the next poll to re-apply the offset to the new cells
        _footerMessage.text = string.Empty;

        // Only the Schedule grid can outgrow the modal sideways — a row is one column per door, and
        // door count is unbounded. The card tabs stay vertical-only so their text can't be pushed
        // off-screen horizontally by a stray wide element.
        _content.mode = _tab == Tab.Schedule
            ? ScrollViewMode.VerticalAndHorizontal
            : ScrollViewMode.Vertical;

        var arrivals = Arrivals();
        if (arrivals == null)
        {
            _footerMessage.text = "Order arrival service isn't running — contracts can't be signed. " +
                                  "(OrderArrivalService is not registered in GameContext.)";
            return;
        }

        switch (_tab)
        {
            case Tab.Customers: BuildCustomers(arrivals); break;
            case Tab.Accounts: BuildAccounts(arrivals); break;
            case Tab.Schedule: BuildSchedule(arrivals); break;
        }
    }

    // ── Tab 1: Customers ─────────────────────────────────────────────────────

    private void BuildCustomers(OrderArrivalService arrivals)
    {
        // Intro on the left, dev triggers on the right. The dev cluster lives on THIS tab only — it
        // was in the tab row, which put it on screen while you were reading the Schedule, where it
        // means nothing.
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.FlexStart;
        header.style.marginBottom = 8;

        var intro = new Label("Sign an account to bring work in. Standing accounts send orders every day; " +
                              "wholesale deals drop a full trailer once.  ·  Drag the title bar to move the window.");
        ApplyFont(intro, size: 14);
        intro.style.color = new StyleColor(ColSubtleText);
        intro.style.whiteSpace = WhiteSpace.Normal;
        intro.style.flexGrow = 1;
        intro.style.flexShrink = 1;
        intro.style.marginRight = 12;
        header.Add(intro);

        var dev = BuildDevCluster();
        dev.style.flexShrink = 0;
        header.Add(dev);
        _content.Add(header);

        if (arrivals.Catalog.Count == 0)
        {
            _footerMessage.text = "No contract offers authored yet. Create ContractData assets and list them " +
                                  "on a ContractRegistry asset under a Resources folder.";
            return;
        }

        ServiceLocator.TryGet<InventoryService>(out var inv);

        // AvailableOffers only — a signed contract has moved to Accounts. Showing it here greyed out
        // is what let a spent wholesale deal squat in the list forever.
        var offers = arrivals.AvailableOffers.ToList();
        if (offers.Count == 0)
        {
            var none = new Label("Every customer on the board is signed. Cancel an account to free one up, " +
                                 "or check the Accounts tab to see how the ones you have are doing.");
            ApplyFont(none, size: 15);
            none.style.color = new StyleColor(ColSubtleText);
            none.style.whiteSpace = WhiteSpace.Normal;
            none.style.marginTop = 12;
            _content.Add(none);
        }

        int row = 0;
        foreach (var contract in offers)
            _content.Add(BuildOfferCard(contract, inv, row++));

        _footerMessage.text = $"{offers.Count} customer(s) looking for a warehouse · " +
                              $"{arrivals.RunningAccounts.Count()} standing account(s) currently running.";
    }

    private VisualElement BuildOfferCard(ContractData contract, InventoryService inv, int rowIndex)
    {
        var card = MakeRow(rowIndex, contract.IsWholesale ? ColWholesale : ColBorder);
        card.Add(MakeIcon(contract.Customer != null ? contract.Customer.Icon : null, IconSize, 8));

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;

        body.Add(MakeText(contract.Customer != null ? contract.Customer.CompanyName : "(no customer assigned)",
                          19, ColTitleText, bold: true));
        body.Add(MakeText(contract.Title, 14, contract.IsWholesale ? ColWholesale : ColSubtleText, bold: true));

        if (!string.IsNullOrWhiteSpace(contract.Pitch))
        {
            var pitch = MakeText(contract.Pitch, 14, ColSubtleText);
            pitch.style.marginTop = 2;
            body.Add(pitch);
        }

        var terms = MakeText(TermsLine(contract), 14, ColSubtleText);
        terms.style.marginTop = 4;
        body.Add(terms);
        card.Add(body);

        var right = new VisualElement();
        right.style.width = 210;
        right.style.flexShrink = 0;
        right.style.alignItems = Align.FlexEnd;

        right.Add(MakeText(EstimatedValueText(contract, inv), 21, ColMoney, bold: true));
        var caption = MakeText(contract.IsWholesale ? "est. one-off revenue" : "est. revenue per day", 12, ColSubtleText);
        caption.style.marginBottom = 6;
        right.Add(caption);

        var sign = new Button(() => OnSign(contract)) { text = "SIGN CONTRACT" };
        StyleOrangeButton(sign);
        right.Add(sign);

        card.Add(right);
        return card;
    }

    private static string TermsLine(ContractData c)
    {
        if (c.IsWholesale)
            return $"{c.PalletCount} full pallets · due in {c.LeadTimeDays} day(s) · " +
                   $"late fee {c.LateFeePercent:P0} · full-pallet quantities only";

        return $"{c.OrdersPerDayMin}–{c.OrdersPerDayMax} orders/day · " +
               $"~{c.EstimatedCasesPerDay} cases/day · cutoff {c.CutoffHour:00}:00 · " +
               $"due in {c.LeadTimeDays} day(s) · late fee {c.LateFeePercent:P0}";
    }

    /// <summary>
    /// Rough money the offer represents, for comparing cards. Always money COMING IN, so it's written
    /// with a leading "+".
    ///
    /// It used to lead with "~" for "approximately", which at this size read as a minus sign and made
    /// every contract look like a cost. The estimate caveat lives in the caption underneath instead,
    /// where it can't be mistaken for arithmetic.
    ///
    /// Averaged over every sellable SKU rather than the ones this contract will actually roll — the
    /// roll happens at arrival, so there's nothing more specific to read. Good enough to rank two
    /// offers against each other; not a forecast.
    /// </summary>
    private static string EstimatedValueText(ContractData c, InventoryService inv)
    {
        if (inv == null) return $"x{c.PayRateMultiplier:0.00}";

        var sellable = inv.AllSkus.Where(s => s != null && s.SellValue > 0f).ToList();
        if (sellable.Count == 0) return $"x{c.PayRateMultiplier:0.00}";

        if (c.IsWholesale)
        {
            var palletCapable = sellable.Where(s => s.Ti > 0 && s.Hi > 0).ToList();
            if (palletCapable.Count == 0) return $"x{c.PayRateMultiplier:0.00}";
            float avgPalletValue = palletCapable.Average(s => s.Ti * s.Hi * s.SellValue);
            return $"+${Mathf.RoundToInt(avgPalletValue * c.PalletCount * c.PayRateMultiplier):N0}";
        }

        float avgCase = sellable.Average(s => s.SellValue);
        return $"+${Mathf.RoundToInt(avgCase * c.EstimatedCasesPerDay * c.PayRateMultiplier):N0}";
    }

    private void OnSign(ContractData contract)
    {
        var arrivals = Arrivals();
        if (arrivals == null) return;

        if (!arrivals.Sign(contract.ContractId))
        {
            UIToast.Show("Couldn't sign that contract — it may already be taken.");
            Rebuild();
            return;
        }

        string who = contract.Customer != null ? contract.Customer.CompanyName : contract.ContractId;
        UIToast.Show(contract.IsWholesale
            ? $"{who}: {contract.PalletCount} pallets inbound — check the Work Queue."
            : $"{who} signed — orders start arriving at {contract.CutoffHour:00}:00.");

        Rebuild();
    }

    // ── Tab 2: Accounts ──────────────────────────────────────────────────────

    private void BuildAccounts(OrderArrivalService arrivals)
    {
        var running = arrivals.RunningAccounts.ToList();
        var delivered = arrivals.DeliveredWholesale.ToList();

        if (running.Count == 0 && delivered.Count == 0)
        {
            var none = new Label("No accounts yet. Sign something on the Offers tab and orders will start " +
                                 "arriving on their own.");
            ApplyFont(none, size: 15);
            none.style.color = new StyleColor(ColSubtleText);
            none.style.whiteSpace = WhiteSpace.Normal;
            none.style.marginTop = 12;
            _content.Add(none);
            return;
        }

        _content.Add(BuildAccountsSummary(arrivals, running));

        int row = 0;
        foreach (var signed in running)
        {
            var contract = arrivals.GetContract(signed.ContractId);
            if (contract == null) continue;
            _content.Add(BuildAccountRow(arrivals, signed, contract, row++));
        }

        if (delivered.Count > 0)
        {
            var heading = MakeText("COMPLETED DEALS", 13, ColSubtleText, bold: true);
            heading.style.marginTop = 10;
            heading.style.marginBottom = 4;
            _content.Add(heading);

            foreach (var signed in delivered)
            {
                var contract = arrivals.GetContract(signed.ContractId);
                if (contract == null) continue;
                _content.Add(BuildAccountRow(arrivals, signed, contract, row++));
            }
        }

        _footerMessage.text = $"{running.Count} standing account(s) running · {delivered.Count} completed deal(s). " +
                              "Cancelling stops future orders; anything already on the board still has to ship.";
    }

    /// <summary>
    /// Three numbers at the top of Accounts, and the middle one is the point of the tab: committed
    /// volume against what the floor actually picked yesterday. A player can read every card on the
    /// Offers tab and still have no idea whether one more account will break them — this is the
    /// comparison that answers it.
    /// </summary>
    private VisualElement BuildAccountsSummary(OrderArrivalService arrivals, List<SignedContract> running)
    {
        int committed = running.Sum(s => arrivals.GetContract(s.ContractId)?.EstimatedCasesPerDay ?? 0);
        int delivered = running.Sum(s => s.OrdersDelivered);
        int late = running.Sum(s => s.OrdersLate);
        float onTime = delivered <= 0 ? 1f : (delivered - late) / (float)delivered;

        var strip = new VisualElement();
        strip.style.flexDirection = FlexDirection.Row;
        strip.style.marginBottom = 8;

        strip.Add(MakeStatTile("COMMITTED / DAY", $"{committed:N0} cases", ColTitleText));
        strip.Add(MakeStatTile("ORDERS DELIVERED", $"{delivered:N0}", ColTitleText));
        strip.Add(MakeStatTile("ON-TIME", $"{onTime:P0}",
                               onTime >= 0.9f ? ColMoney : onTime >= 0.7f ? ColWholesale : ColDangerSoft));
        return strip;
    }

    private VisualElement MakeStatTile(string label, string value, Color valueColor)
    {
        var tile = new VisualElement();
        tile.style.flexGrow = 1;
        tile.style.flexBasis = 0;
        tile.style.marginRight = 8;
        tile.style.backgroundColor = new StyleColor(ColStat);
        tile.style.borderTopLeftRadius = tile.style.borderTopRightRadius =
            tile.style.borderBottomLeftRadius = tile.style.borderBottomRightRadius = 8;
        tile.style.paddingTop = 8; tile.style.paddingBottom = 8;
        tile.style.paddingLeft = 10; tile.style.paddingRight = 10;
        tile.Add(MakeText(label, 11, ColSubtleText, bold: true));
        tile.Add(MakeText(value, 20, valueColor, bold: true));
        return tile;
    }

    private VisualElement BuildAccountRow(OrderArrivalService arrivals, SignedContract signed,
                                          ContractData contract, int rowIndex)
    {
        bool isWholesale = contract.IsWholesale;
        bool struggling = !isWholesale && signed.OrdersLate > 0;
        Color accent = isWholesale ? ColChipPurple : struggling ? ColDanger : ColMoney;

        var card = MakeRow(rowIndex, accent);
        card.Add(MakeIcon(contract.Customer != null ? contract.Customer.Icon : null, IconSizeSm, 6));

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;
        body.Add(MakeText(contract.Customer != null ? contract.Customer.CompanyName : contract.ContractId,
                          17, ColTitleText, bold: true));
        body.Add(MakeText(StatusLine(arrivals, signed, contract), 13,
                          struggling ? ColDangerSoft : ColSubtleText));
        card.Add(body);

        var right = new VisualElement();
        right.style.width = 190;
        right.style.flexShrink = 0;
        right.style.alignItems = Align.FlexEnd;

        right.Add(MakeText($"${signed.RevenueEarned:N0}", 17, ColMoney, bold: true));
        var caption = MakeText("earned to date", 11, ColSubtleText);
        caption.style.marginBottom = 5;
        right.Add(caption);

        if (isWholesale)
        {
            right.Add(MakeText("DELIVERED", 12, ColSubtleText, bold: true));
        }
        else
        {
            var cancel = new Button(() => OnCancel(contract, signed)) { text = "CANCEL" };
            StyleGhostButton(cancel);
            right.Add(cancel);
        }

        card.Add(right);
        return card;
    }

    private string StatusLine(OrderArrivalService arrivals, SignedContract signed, ContractData contract)
    {
        int today = CurrentDay();
        int daysHeld = Mathf.Max(0, today - signed.SignedOnDay);

        if (contract.IsWholesale)
        {
            return $"Wholesale — {contract.PalletCount} pallets · signed day {signed.SignedOnDay} · " +
                   $"{signed.OrdersDelivered} order(s) shipped" +
                   (signed.LateFeesPaid > 0 ? $" · ${signed.LateFeesPaid:N0} in late fees" : "");
        }

        string next = arrivals.TryGetNextArrival(signed.ContractId, out int day, out int hour)
            ? (day == today ? $"next drop {hour:00}:00 today" : $"next drop {hour:00}:00 day {day}")
            : "next drop unknown";

        string lateBit = signed.OrdersLate > 0
            ? $" · {signed.OrdersLate} late · ${signed.LateFeesPaid:N0} in fees"
            : " · 0 late";

        return $"Day {daysHeld} · {next} · {signed.OrdersDelivered} shipped{lateBit} · " +
               $"on-time {signed.OnTimeRate:P0}";
    }

    private void OnCancel(ContractData contract, SignedContract signed)
    {
        var arrivals = Arrivals();
        if (arrivals == null) return;

        string who = contract.Customer != null ? contract.Customer.CompanyName : contract.ContractId;
        if (!arrivals.Cancel(contract.ContractId))
        {
            UIToast.Show($"Couldn't cancel {who}.");
            Rebuild();
            return;
        }

        // Deliberately does NOT touch orders already on the board. Cancelling ends FUTURE arrivals;
        // work you already accepted still has a due date and still fines you for missing it.
        UIToast.Show($"{who} cancelled — no new orders. Anything already open still has to ship.");
        Rebuild();
    }

    // ── Tab 3: Schedule ──────────────────────────────────────────────────────

    private void BuildSchedule(OrderArrivalService arrivals)
    {
        var schedule = Schedule();
        if (schedule == null)
        {
            _footerMessage.text = "Dock schedule service isn't running — appointments can't be shown.";
            return;
        }

        int today = CurrentDay();
        // Day switcher and legend go in the STATIONARY strip, not the scroll view — they're the frame
        // you read the grid against, so they must not slide away when you scroll right to reach door 9.
        _tabHeader.Add(BuildScheduleHeader(schedule, today));

        var doors = schedule.OutboundDoors();
        if (doors.Count == 0)
        {
            var none = new Label("No outbound doors yet. A block can only hold as many trailers as you have " +
                                 "dock doors with shipping lanes — place a door and a row of lane tiles, and " +
                                 "the grid fills in.");
            ApplyFont(none, size: 15);
            none.style.color = new StyleColor(ColSubtleText);
            none.style.whiteSpace = WhiteSpace.Normal;
            none.style.marginTop = 14;
            _content.Add(none);
            _footerMessage.text = "Capacity is 0 — arriving orders stay unscheduled until a door exists.";
            return;
        }

        // Legend ABOVE the grid and in the stationary strip: twelve block rows always overflow the
        // scroll view, so a legend appended at the end is permanently below the fold — the one place
        // it's useless, since the colours it explains are all on screen.
        _tabHeader.Add(BuildScheduleLegend());

        for (int block = 0; block < DockScheduleService.BlocksPerDay; block++)
            _content.Add(BuildScheduleRow(schedule, arrivals, doors, block, today));

        // The rows were just rebuilt at offset 0 while the view may still be scrolled — re-apply
        // immediately so a rebuild (moving an appointment) doesn't flash the frozen column back to the
        // left of a scrolled grid for a frame before the poller catches up.
        SyncFrozenTimeColumn();

        int booked = schedule.Appointments.Count(a => a.Day == _scheduleDay);
        int capacity = doors.Count * DockScheduleService.BlocksPerDay;
        _footerMessage.text = _selectedAppointmentId != null
            ? "Pick an empty slot to move the selected appointment, or click it again to drop it."
            : $"{booked} of {capacity} slots booked · arriving orders auto-book the first free block " +
              $"after their cutoff — click an appointment to move it.";
    }

    private VisualElement BuildScheduleHeader(DockScheduleService schedule, int today)
    {
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 8;

        var prev = new Button(() => { _scheduleDay = Mathf.Max(today - ScheduleDaysBack, _scheduleDay - 1); Rebuild(); })
            { text = "◀" };
        StyleSquareButton(prev);
        prev.style.width = 26; prev.style.height = 26;
        header.Add(prev);

        string when = _scheduleDay == today ? "today"
                    : _scheduleDay == today + 1 ? "tomorrow"
                    : _scheduleDay < today ? $"{today - _scheduleDay} day(s) ago"
                    : $"in {_scheduleDay - today} day(s)";
        var dayLabel = MakeText($"Day {_scheduleDay} · {when}", 15, ColTitleText, bold: true);
        dayLabel.style.marginLeft = 8; dayLabel.style.marginRight = 8;
        header.Add(dayLabel);

        var next = new Button(() => { _scheduleDay = Mathf.Min(today + ScheduleDaysAhead, _scheduleDay + 1); Rebuild(); })
            { text = "▶" };
        StyleSquareButton(next);
        next.style.width = 26; next.style.height = 26;
        header.Add(next);

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        header.Add(spacer);

        int doorCount = schedule.CapacityPerBlock;
        header.Add(MakeText($"{doorCount} outbound door(s) → {doorCount} appointment(s) per block",
                            12, ColSubtleText));
        return header;
    }

    /// <summary>
    /// One block row: time label, then one slot per outbound door, then a FULL flag.
    ///
    /// Every part is a FIXED width with flexShrink 0. Slots used to be flexGrow/flexBasis-0, which
    /// made them share whatever width was available — with enough doors they'd squeeze to nothing
    /// instead of overflowing, so the horizontal scrollbar could never appear. Fixed widths are what
    /// let the row grow past the viewport and give the scroller something to scroll.
    /// </summary>
    private VisualElement BuildScheduleRow(DockScheduleService schedule, OrderArrivalService arrivals,
                                           List<int> doors, int block, int today)
    {
        var appts = schedule.GetBlock(_scheduleDay, block).ToList();
        int capacity = doors.Count;
        bool full = appts.Count >= capacity;
        // Only the live day has a "past" — a future day's early blocks are perfectly bookable.
        bool past = _scheduleDay < today || (_scheduleDay == today && block < schedule.CurrentBlock);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 4;
        row.style.flexShrink = 0;

        // An empty in-flow spacer reserves the time column's width so the slots start in the right
        // place. The visible time cell is added LAST and positioned absolutely over this.
        var spacer = new VisualElement();
        spacer.style.width = TimeColWidth;
        spacer.style.minWidth = TimeColWidth;
        spacer.style.flexShrink = 0;
        spacer.style.height = ScheduleRowHeight;
        row.Add(spacer);

        for (int i = 0; i < capacity; i++)
        {
            var slot = i < appts.Count
                ? BuildAppointmentChip(appts[i], arrivals)
                : BuildEmptySlot(block, past);
            // Past blocks dim their SLOTS individually rather than the whole row. Row-level opacity
            // would also fade the frozen time cell, and a translucent frozen column lets the slots
            // show through it as they scroll under — exactly what freezing it was meant to prevent.
            if (past) slot.style.opacity = 0.45f;
            row.Add(slot);
        }

        var flag = MakeText(full ? "FULL" : string.Empty, 11, ColDangerSoft, bold: true);
        flag.style.width = FullFlagWidth;
        flag.style.minWidth = FullFlagWidth;
        flag.style.flexShrink = 0;
        flag.style.unityTextAlign = TextAnchor.MiddleRight;
        if (past) flag.style.opacity = 0.45f;
        row.Add(flag);

        // LAST child, so it paints over every slot — and ABSOLUTE, so being last doesn't also put it
        // last in the layout. UI Toolkit has no z-index: paint order is sibling order, and in a flex
        // row sibling order is ALSO horizontal order. The first attempt used BringToFront() on an
        // in-flow cell, which duly raised it and shoved the whole time column to the right-hand end of
        // the row. Taking it out of flow is what lets the two orders differ.
        var time = MakeText(DockScheduleService.BlockLabel(block), 12,
                            full ? ColDangerSoft : past ? ColEmptyText : ColSubtleText);
        time.style.position = Position.Absolute;
        time.style.left = 0;
        time.style.top = 0;
        time.style.width = TimeColWidth;
        time.style.height = ScheduleRowHeight;
        time.style.paddingLeft = 2;
        // Opaque, so slots scrolling under it are hidden rather than showing through.
        time.style.backgroundColor = new StyleColor(ColBg);
        time.style.borderRightWidth = 1;
        time.style.borderRightColor = new StyleColor(ColBlueEdge);
        time.style.unityTextAlign = TextAnchor.MiddleLeft;
        row.Add(time);
        _scheduleTimeCells.Add(time);

        return row;
    }

    private VisualElement BuildAppointmentChip(DockAppointment appt, OrderArrivalService arrivals)
    {
        bool selected = appt.Id == _selectedAppointmentId;
        Color fill = appt.Kind switch
        {
            AppointmentKind.Inbound => ColChipIn,
            AppointmentKind.Wholesale => ColChipWhole,
            _ => ColChipOut
        };
        Color edge = appt.Kind switch
        {
            AppointmentKind.Inbound => ColOrangeEdge,
            AppointmentKind.Wholesale => ColChipPurple,
            _ => ColBlueEdge
        };
        Color text = appt.Kind switch
        {
            AppointmentKind.Inbound => ColWholesale,
            AppointmentKind.Wholesale => ColChipWholeTx,
            _ => ColChipOutText
        };

        var chip = new VisualElement();
        chip.style.width = SlotWidth;
        chip.style.minWidth = SlotWidth;
        chip.style.flexShrink = 0;
        chip.style.flexDirection = FlexDirection.Row;
        chip.style.alignItems = Align.Center;
        chip.style.marginRight = 5;
        chip.style.paddingLeft = 6; chip.style.paddingRight = 6;
        chip.style.paddingTop = 4; chip.style.paddingBottom = 4;
        chip.style.backgroundColor = new StyleColor(fill);
        chip.style.borderTopWidth = chip.style.borderBottomWidth =
            chip.style.borderLeftWidth = chip.style.borderRightWidth = selected ? 2 : 1;
        chip.style.borderTopColor = chip.style.borderBottomColor =
            chip.style.borderLeftColor = chip.style.borderRightColor =
            new StyleColor(selected ? ColOrange : edge);
        chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius =
            chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 5;
        chip.style.overflow = Overflow.Hidden;

        // The tiny customer icon, immediately left of the name.
        chip.Add(MakeIcon(IconForAppointment(appt, arrivals), IconSizeTiny, 3, marginRight: 5));

        // Name flexes and is allowed to clip; the door number is a separate fixed-width element that
        // never can. Both in one label meant a long company name pushed "· D3" off the end of the
        // chip — and the door is the one thing on the chip you can't work out from anything else.
        var label = MakeText(appt.CustomerName, 11, text);
        label.style.flexGrow = 1;
        label.style.flexShrink = 1;
        label.style.overflow = Overflow.Hidden;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        chip.Add(label);

        var door = MakeText($"D{appt.DoorNumber}", 11, text, bold: true);
        door.style.flexShrink = 0;
        door.style.marginLeft = 4;
        door.style.unityTextAlign = TextAnchor.MiddleRight;
        chip.Add(door);

        // Whole chip is the hit target for select/move — a chip is only ~150px wide, so a separate
        // grab handle would eat most of the label.
        chip.RegisterCallback<ClickEvent>(_ => OnChipClicked(appt));
        chip.tooltip = $"{appt.CustomerName} · {appt.TimeLabel} · door {appt.DoorNumber} · " +
                       $"{appt.OrderIds.Count} order(s) · click to move";
        return chip;
    }

    private VisualElement BuildEmptySlot(int block, bool past)
    {
        var slot = new VisualElement();
        slot.style.width = SlotWidth;
        slot.style.minWidth = SlotWidth;
        slot.style.flexShrink = 0;
        slot.style.marginRight = 5;
        slot.style.paddingLeft = 6; slot.style.paddingRight = 6;
        slot.style.paddingTop = 4; slot.style.paddingBottom = 4;
        slot.style.borderTopWidth = slot.style.borderBottomWidth =
            slot.style.borderLeftWidth = slot.style.borderRightWidth = 1;
        slot.style.borderTopColor = slot.style.borderBottomColor =
            slot.style.borderLeftColor = slot.style.borderRightColor = new StyleColor(ColBlueEdge);
        slot.style.borderTopLeftRadius = slot.style.borderTopRightRadius =
            slot.style.borderBottomLeftRadius = slot.style.borderBottomRightRadius = 5;

        bool canDrop = _selectedAppointmentId != null && !past;
        var label = MakeText(canDrop ? "move here" : "— open —", 11,
                             canDrop ? ColOrangeText : ColEmptyText);
        label.style.height = IconSizeTiny;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        slot.Add(label);

        if (canDrop)
        {
            slot.style.backgroundColor = new StyleColor(new Color(ColOrange.r, ColOrange.g, ColOrange.b, 0.18f));
            slot.RegisterCallback<ClickEvent>(_ => OnSlotClicked(block));
        }
        return slot;
    }

    private void OnChipClicked(DockAppointment appt)
    {
        // Click the selected chip again to put it down — otherwise there's no way to abandon a move
        // without changing tabs.
        _selectedAppointmentId = _selectedAppointmentId == appt.Id ? null : appt.Id;
        Rebuild();
    }

    private void OnSlotClicked(int block)
    {
        var schedule = Schedule();
        if (schedule == null || _selectedAppointmentId == null) return;

        if (!schedule.TryMove(_selectedAppointmentId, _scheduleDay, block, out string why))
        {
            UIToast.Show(why);
            return;
        }

        _selectedAppointmentId = null;
        Rebuild();
    }

    private VisualElement BuildScheduleLegend()
    {
        var legend = new VisualElement();
        legend.style.flexDirection = FlexDirection.Row;
        legend.style.marginBottom = 6;
        legend.Add(MakeLegendSwatch(ColChipOut, ColBlueEdge, "outbound order"));
        legend.Add(MakeLegendSwatch(ColChipIn, ColOrangeEdge, "inbound PO (same doors)"));
        legend.Add(MakeLegendSwatch(ColChipWhole, ColChipPurple, "wholesale pallet drop"));
        return legend;
    }

    private VisualElement MakeLegendSwatch(Color fill, Color edge, string text)
    {
        var wrap = new VisualElement();
        wrap.style.flexDirection = FlexDirection.Row;
        wrap.style.alignItems = Align.Center;
        wrap.style.marginRight = 16;

        var swatch = new VisualElement();
        swatch.style.width = 10; swatch.style.height = 10;
        swatch.style.marginRight = 5;
        swatch.style.backgroundColor = new StyleColor(fill);
        swatch.style.borderTopWidth = swatch.style.borderBottomWidth =
            swatch.style.borderLeftWidth = swatch.style.borderRightWidth = 1;
        swatch.style.borderTopColor = swatch.style.borderBottomColor =
            swatch.style.borderLeftColor = swatch.style.borderRightColor = new StyleColor(edge);
        wrap.Add(swatch);
        wrap.Add(MakeText(text, 11, ColSubtleText));
        return wrap;
    }

    /// <summary>
    /// Customer icon for a scheduled trailer. Resolved off the CONTRACT first (an appointment carries
    /// its ContractId, which is exact), falling back to a scan of the catalog by CustomerId for
    /// hand-made Dev Console orders that never had a contract.
    ///
    /// Goes through the contract catalog rather than CustomerRegistry on purpose: the registry asset
    /// doesn't live under a Resources folder, so it isn't loadable in a built player, and the catalog
    /// is already in memory here.
    /// </summary>
    private Sprite IconForAppointment(DockAppointment appt, OrderArrivalService arrivals)
    {
        if (arrivals == null) return null;

        if (!string.IsNullOrEmpty(appt.ContractId))
        {
            var byContract = arrivals.GetContract(appt.ContractId);
            if (byContract?.Customer != null) return byContract.Customer.Icon;
        }

        if (!string.IsNullOrEmpty(appt.CustomerId))
        {
            foreach (var c in arrivals.Catalog)
                if (c?.Customer != null && c.Customer.CustomerId == appt.CustomerId)
                    return c.Customer.Icon;
        }
        return null; // inbound suppliers have no CustomerData; the chip's tint carries the meaning
    }

    // ── Shared builders ──────────────────────────────────────────────────────

    private VisualElement MakeRow(int rowIndex, Color leftAccent)
    {
        var card = new VisualElement();
        card.style.flexDirection = FlexDirection.Row;
        card.style.alignItems = Align.Center;
        card.style.paddingTop = 10; card.style.paddingBottom = 10;
        card.style.paddingLeft = 12; card.style.paddingRight = 12;
        card.style.marginBottom = 6;
        card.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColCardEven : ColCardOdd);
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 10;
        card.style.borderLeftWidth = 3;
        card.style.borderLeftColor = new StyleColor(leftAccent);
        return card;
    }

    /// <summary>Customer icon block. A null sprite falls back to a flat plate rather than collapsing,
    /// so rows stay aligned whether or not every CustomerData has art assigned.</summary>
    private VisualElement MakeIcon(Sprite sprite, float size, int radius, float marginRight = 14f)
    {
        var icon = new VisualElement();
        icon.style.width = size;
        icon.style.height = size;
        icon.style.marginRight = marginRight;
        icon.style.flexShrink = 0;
        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius =
            icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = radius;
        if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
        else icon.style.backgroundColor = new StyleColor(ColBlueEdge);
        return icon;
    }

    private Label MakeText(string text, int size, Color color, bool bold = false)
    {
        var label = new Label(text);
        ApplyFont(label, bold, size);
        label.style.color = new StyleColor(color);
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private static OrderArrivalService Arrivals()
        => ServiceLocator.TryGet<OrderArrivalService>(out var s) ? s : null;

    private static DockScheduleService Schedule()
        => ServiceLocator.TryGet<DockScheduleService>(out var s) ? s : null;

    private static int CurrentDay()
        => ServiceLocator.TryGet<SimulationTimeService>(out var t) && t != null ? t.Day : 0;

    // ── Styling helpers (mirrors WorkQueuePanel's) ───────────────────────────

    private static Font LilitaFont()
    {
        if (_lilita != null) return _lilita;
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
        if (guids.Length > 0)
            _lilita = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
        _lilita = Resources.Load<Font>("LilitaOne-Regular");
#endif
        return _lilita;
    }

    private static void ApplyFont(VisualElement el, bool bold = false, int size = -1)
    {
        var f = LilitaFont();
        if (f != null) el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
        if (bold) el.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (size > 0) el.style.fontSize = size;
    }

    private static void StyleOrangeButton(Button b)
    {
        ApplyFont(b, bold: true, size: 15);
        b.style.backgroundColor = new StyleColor(ColOrange);
        b.style.color = new StyleColor(ColOrangeText);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(ColOrangeEdge);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;
        b.style.paddingLeft = 12; b.style.paddingRight = 12;
        b.style.height = 30;
        b.style.marginLeft = 0; b.style.marginRight = 0;
        b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = new StyleColor(ColOrangeHover));
        b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(ColOrange));
    }

    /// <summary>Small, muted, deliberately unappealing — a debug trigger sitting inside a real panel
    /// must not look like something the player is meant to press.</summary>
    private static void StyleDevButton(Button b)
    {
        ApplyFont(b, bold: true, size: 10);
        b.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.25f));
        b.style.color = new StyleColor(ColSubtleText);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 1;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(ColBlueEdge);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 4;
        b.style.paddingLeft = 8; b.style.paddingRight = 8;
        b.style.paddingTop = 0; b.style.paddingBottom = 0;
        b.style.height = 20;
        b.style.marginLeft = 0; b.style.marginRight = 4;
        b.style.marginTop = 0; b.style.marginBottom = 0;
        b.RegisterCallback<MouseEnterEvent>(_ => b.style.color = new StyleColor(ColWholesale));
        b.RegisterCallback<MouseLeaveEvent>(_ => b.style.color = new StyleColor(ColSubtleText));
    }

    /// <summary>Outlined, low-emphasis button — Cancel shouldn't compete with Sign for attention.</summary>
    private static void StyleGhostButton(Button b)
    {
        ApplyFont(b, bold: true, size: 12);
        b.style.backgroundColor = new StyleColor(Color.clear);
        b.style.color = new StyleColor(ColSubtleText);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(ColBlueEdge);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;
        b.style.paddingLeft = 10; b.style.paddingRight = 10;
        b.style.height = 24;
        b.style.marginLeft = 0; b.style.marginRight = 0;
        b.RegisterCallback<MouseEnterEvent>(_ => b.style.color = new StyleColor(ColDangerSoft));
        b.RegisterCallback<MouseLeaveEvent>(_ => b.style.color = new StyleColor(ColSubtleText));
    }

    private static void StyleSquareButton(Button b)
    {
        ApplyFont(b, bold: true, size: 15);
        b.style.width = 32; b.style.height = 32;
        b.style.backgroundColor = new StyleColor(new Color(0.16f, 0.22f, 0.29f, 1f));
        b.style.color = new StyleColor(ColTitleText);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(ColBlueEdge);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;
        b.style.marginLeft = 0; b.style.marginRight = 0;
    }
}
