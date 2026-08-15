using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// The Outbound Order Manager. Four tabs over one question — where work comes from, how the work you
/// already took is going, and when each trailer is getting a door.
///
///   NEW CONTRACTS  contracts on the table. Two types share the board: BULK ORDERS (a one-off
///                  full-pallet drop at cost + 5%, 1–3 rolled fresh daily, the rest expiring) and
///                  STANDING ORDERS (a daily or weekly account). Told apart by a type pill and a
///                  colour family, not by separate tabs — the player is choosing across both.
///   BULK ORDERS    the bulk orders already accepted: how each breaks into pallets versus loose
///                  cases, and whether it has a door booked yet.
///   ACCOUNTS       what you're actually running, and how well. Per-contract delivered/late/earned,
///                  plus the only place a standing account can be cancelled.
///   SCHEDULE       the dock appointment book — two-hour blocks, one row per block, as many slots
///                  per block as you have outbound doors.
///
/// ARRIVING FREIGHT AUTO-BOOKS A DOOR, BUT STAYS THE PLAYER'S TO MOVE. An order lands into the first
/// open block before its due day the moment it arrives (DockScheduleService.TryAutoPlace) — the
/// Schedule tab is where you override that plan, not where you're required to build it from scratch.
/// Freight that genuinely found no room shows in the stranded strip instead, and if it passes its due
/// day still stranded, the account is lost for 30 days (OrderArrivalService.SweepMissedPickups).
///
/// Why New Contracts and Accounts are separate rather than one greyed-out list: a delivered one-off
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

    // Bulk orders — a teal/green family, deliberately distant from both the blue standing-order
    // chips and the purple wholesale ones. The three types have to be tellable apart at chip size in
    // the stranded strip, where they sit side by side and the only difference is colour.
    private static readonly Color ColChipBulk    = new Color(22f / 255f, 58f / 255f, 52f / 255f, 1f);
    private static readonly Color ColChipBulkTx  = new Color(0x7C / 255f, 0xD9 / 255f, 0xC0 / 255f, 1f);
    private static readonly Color ColBulkEdge    = new Color(0x2E / 255f, 0x8B / 255f, 0x76 / 255f, 1f);
    private static readonly Color ColEmptyText   = new Color(0x4D / 255f, 0x65 / 255f, 0x77 / 255f, 1f);
    private static readonly Color ColTabIdle     = new Color(28f / 255f, 38f / 255f, 50f / 255f, 1f);
    private static readonly Color ColTabHover    = new Color(40f / 255f, 54f / 255f, 70f / 255f, 1f);

    /// <summary>Starting size. Width is only the DEFAULT now — the modal's side edges are draggable
    /// (see ResizableWindow in Build), so the player can pull it wider or narrower and it stays that
    /// way for the session. Widened from 1040 when the Completed tab's text went up to 16pt (needs
    /// ~1170), then again to fit the Order # column (adds ~100) — a default that clips its own ledger
    /// would make resizing a repair rather than a preference.</summary>
    private const float ModalWidth  = 1340f;
    private const float ModalHeight = 680f;
    /// <summary>Narrowest the window can be dragged. Below this the tab bar itself starts wrapping.
    /// The Completed tab can be narrowed past its own column total safely — it scrolls sideways, and
    /// its header tracks the scroll (see SyncCompletedHeader).</summary>
    private const float ModalMinWidth = 820f;
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

    /// <summary>
    /// NewContracts was "Offers" until 2026-08-01, then "Customers", and is now what it always meant:
    /// the board of contracts on the table. Two types share it — Bulk Orders (a one-off full-pallet
    /// drop priced off cost of goods, 1–3 rolled fresh every day) and Standing Orders (a daily or
    /// weekly account) — told apart by colour and a type pill rather than by separate tabs, because
    /// the decision the player is making is "which of these do I want", across both.
    ///
    /// BulkOrders is the opposite: not offers but the live bulk orders already accepted, and how far
    /// through the warehouse each one is.
    /// </summary>
    ///
    /// Completed is the ledger: every order that has actually been closed out and paid for, newest
    /// first. Distinct from the "COMPLETED DEALS" heading on Accounts, which lists finished wholesale
    /// CONTRACTS — this one is per ORDER, and it's the only place the money a shipment made (and what
    /// it cost to make it) is reported per deal.
    private enum Tab { NewContracts, BulkOrders, Accounts, Schedule, Completed }

    private readonly VisualElement _overlay;
    private readonly VisualElement _modal;
    private readonly VisualElement _tabBar;
    /// <summary>Stationary strip between the tab bar and the scroll view. See Build.</summary>
    private readonly VisualElement _tabHeader;
    private readonly ScrollView _content;
    private readonly Label _footerMessage;
    /// <summary>The big heading in the title bar. Retitled per tab (see TitleFor) — a ledger of
    /// finished orders sitting under the words "OUTBOUND CONTRACTS" names the wrong thing.</summary>
    private Label _titleLabel;
    private bool _visible;
    private ResizableWindow _resizeWindow;

    private Tab _tab = Tab.NewContracts;

    /// <summary>Body text size on the Completed tab. Everything on that tab is sized off this, so the
    /// whole ledger moves together — it was 12 and read as fine print next to the rest of the panel.
    /// Column widths below are cut for THIS size; raising it again means widening them too.</summary>
    private const int DoneFontSize = 16;

    /// <summary>Completed-tab column widths. Fixed, like the Schedule grid's — a ledger only reads as
    /// a ledger if the digits line up in a column, which flexible widths can't guarantee. Widening the
    /// window therefore adds empty space on the right rather than stretching the columns; that's the
    /// deliberate trade, because the header lives OUTSIDE the scroll view and any column that stretched
    /// would drift from its rows by exactly the width of the vertical scrollbar.</summary>
    private const float DoneDateWidth    = 150f;
    /// <summary>Just the first 8 characters of OrderData.OrderId (a GUID) — enough to tell rows apart
    /// at a glance without eating the width a full GUID would need. The full id is in the tooltip for
    /// whenever "which exact order was this" actually matters (e.g. cross-checking a console log).</summary>
    private const float DoneOrderWidth   = 100f;
    private const float DoneAccountWidth = 260f;
    private const float DoneTypeWidth    = 118f;
    private const float DoneRevenueWidth = 128f;
    private const float DoneProfitWidth  = 132f;
    private const float DonePalletsWidth = 92f;
    private const float DoneCasesWidth   = 92f;
    private const float DoneFillWidth    = 178f;
    /// <summary>Left indent shared by the Completed header and its rows, so the two line up.</summary>
    private const float DoneRowIndent    = 10f;
    /// <summary>Rows carry a 3px status stripe down their left edge, which pushes their content across
    /// by 3px. The header wears the same border in a transparent colour purely so the two agree —
    /// without it every heading sat 3px left of the column it names.</summary>
    private const float DoneStripeWidth  = 3f;

    private enum DoneColumn { Date, Order, Account, Type, Revenue, Profit, Pallets, Cases, FillRate }

    /// <summary>Which column the Completed tab is sorted on. Date descending by default — the deal
    /// that just left the dock is the one you opened the tab to find.</summary>
    private DoneColumn _doneSort = DoneColumn.Date;
    private bool _doneAscending;

    /// <summary>Excel-style multi-select filter on the Shipped column — the only column that has one
    /// today. Selecting specific day(s) narrows the ledger rows AND the Revenue/Net Profit/Fill Rate
    /// stat strip to just those days, same as an Excel AutoFilter narrows a whole sheet by one column's
    /// checked values. Inactive (every day implicitly included) until the player opens the dropdown and
    /// unchecks something — see DoneDateFilterValue for what a "day" bucket is.</summary>
    private readonly HashSet<string> _doneDateFilterSelection = new();
    private bool _doneDateFilterActive;
    private string _doneDateFilterSearch = "";
    private VisualElement _doneDateFilterPopup;

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

    /// <summary>UnscheduledGroup.Key of a stranded trailer picked up for booking. Mutually exclusive
    /// with _selectedAppointmentId — the two are the same gesture from different sources (pick a thing
    /// up, click a slot to put it down), and holding both at once would make the next slot click
    /// ambiguous. Selecting either clears the other.</summary>
    private string _selectedUnscheduledKey;

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
        if (_scheduleDay == int.MinValue) _scheduleDay = CurrentDay();
        Rebuild();
        CentreOnce();
        _resizeWindow?.ResetToNormal();
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
        _selectedUnscheduledKey = null;
        CloseDoneDateFilterPopup();
        _overlay.style.display = DisplayStyle.None;
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
        // Stops above the bottom HUD so it can't cover the bar or the Build/Play tabs — see the note
        // on workqueue-overlay in WorkQueuePanel.Build.
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0;
        overlay.style.bottom = BuildMenuUI.BottomHudReservedHeight;

        // NOT a modal scrim. This element is a positioning frame for the window and nothing else: no
        // dimming fill, and Ignore so it never becomes a hit target. PickingMode.Ignore on a parent
        // does not stop its children being picked, so the modal below still takes its own clicks —
        // what it stops is the full-screen rect between them eating every click aimed at the world or
        // at another panel. UIInputGuard.IsPointerOverUIToolkit explicitly skips Ignore elements, so
        // this is also what hands camera drag and object selection back to the player.
        overlay.pickingMode = PickingMode.Ignore;

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

        var title = new Label(TitleFor(_tab));
        _titleLabel = title;
        ApplyFont(title, bold: true, size: 28);
        title.style.color = new StyleColor(ColTitleText);
        title.style.flexGrow = 1;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(title);

        // Cycles normal / large / fill-screen (see ResizableWindow.CycleScale below). Same size as the
        // close button and on the same title-bar row, so the two sit flush together.
        const float titleBtnSize = 48f; // 1.5x the base 32px square button
        var scaleBtn = new Button { text = string.Empty, tooltip = "Resize window (normal / large / fill screen)" };
        StyleSquareButton(scaleBtn);
        scaleBtn.style.width = titleBtnSize;
        scaleBtn.style.height = titleBtnSize;
        scaleBtn.style.marginRight = 6;
        ResizableWindow.AddStackedSquaresGlyph(scaleBtn, titleBtnSize, ColTitleText);
        titleBar.Add(scaleBtn);

        var close = new Button(Hide) { text = "✕" };
        StyleSquareButton(close);
        close.style.width = titleBtnSize;
        close.style.height = titleBtnSize;
        ApplyFont(close, bold: true, size: 22);
        var closeNormalBg = close.style.backgroundColor;
        var closeHoverBg = new StyleColor(new Color(0.8f, 0.3f, 0.2f, 1f));
        var closeActiveBg = new StyleColor(new Color(0.6f, 0.16f, 0.12f, 1f));
        close.RegisterCallback<PointerEnterEvent>(_ => close.style.backgroundColor = closeHoverBg);
        close.RegisterCallback<PointerLeaveEvent>(_ => close.style.backgroundColor = closeNormalBg);
        close.RegisterCallback<PointerDownEvent>(_ => close.style.backgroundColor = closeActiveBg);
        close.RegisterCallback<PointerUpEvent>(_ => close.style.backgroundColor = closeHoverBg);
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
        // Clipped, because the Completed tab's column header is slid sideways inside it to track that
        // tab's horizontal scroll — without this it would draw straight out past the modal's edge.
        tabHeader.style.overflow = Overflow.Hidden;
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
        content.schedule.Execute(SyncCompletedHeader).Every(16);

        footerMessage = new Label();
        ApplyFont(footerMessage, size: 15);
        footerMessage.style.color = new StyleColor(ColSubtleText);
        footerMessage.style.marginTop = 8;
        footerMessage.style.whiteSpace = WhiteSpace.Normal;
        modal.Add(footerMessage);

        // Side edges only. Height stays fixed on purpose: this modal's tabs each manage their own
        // vertical scrolling and none of them gains anything from a taller window, whereas WIDTH is
        // the axis that actually decides how much of the Completed ledger and the Schedule grid you
        // can see at once. titleInset keeps the grips clear of the drag handle above them.
        _resizeWindow = new ResizableWindow(modal, minW: ModalMinWidth, minH: ModalHeight, grip: 10f,
                            titleInset: 48f, allowVerticalResize: false);
        scaleBtn.clicked += _resizeWindow.CycleScale;

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
        _selectedUnscheduledKey = null;
        _clearContractsArmed = false;  // nor should a half-finished wipe
        CloseDoneDateFilterPopup();    // it's a direct child of _modal, so it survives Rebuild() on
                                        // its own tab, but not walking away to a different tab
        Rebuild();
    }

    private void BuildTabBar()
    {
        _tabBar.Clear();
        var arrivals = Arrivals();
        var schedule = Schedule();

        int offerCount = arrivals != null ? arrivals.AvailableOffers.Count() : 0;
        // Running accounts only — spent one-offs moved to the Completed tab, and a badge that still
        // counted them would promise rows the tab no longer has.
        int accountCount = arrivals != null ? arrivals.RunningAccounts.Count() : 0;

        string scheduleBadge = "—";
        if (schedule != null)
        {
            int cap = schedule.CapacityPerBlock * DockScheduleService.BlocksPerDay;
            int booked = schedule.Appointments.Count(a => a.Day == _scheduleDay);
            scheduleBadge = cap > 0 ? $"{booked}/{cap}" : "no doors";
        }

        int bulkCount = LiveBulkOrders().Count;

        _tabBar.Add(MakeTab("New Contracts", offerCount.ToString(), Tab.NewContracts));
        _tabBar.Add(MakeTab("Bulk Orders", bulkCount.ToString(), Tab.BulkOrders));
        _tabBar.Add(MakeTab("Accounts", accountCount.ToString(), Tab.Accounts));
        _tabBar.Add(MakeTab("Schedule", scheduleBadge, Tab.Schedule));
        _tabBar.Add(MakeTab("Completed", CompletedOrders().Count.ToString(), Tab.Completed));
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

        var wipe = new Button(OnDevClearContracts) { text = "CLEAR CONTRACTS" };
        StyleDevButton(wipe);
        wipe.tooltip = "Debug: wipe the contract board — drops every signed account and generated " +
                       "offer, and restores the authored offers. Orders already on the floor are NOT " +
                       "touched; clear those from the Work Queue.";
        wrap.Add(wipe);

        return wrap;
    }

    /// <summary>
    /// Two-step, because it is not undoable and there is no confirmation dialog in this project.
    ///
    /// The first press arms the button and says what it will destroy; the second press inside the
    /// window actually does it. A dev control sitting one click away from deleting every account the
    /// player has built up is worth exactly one extra click.
    /// </summary>
    private void OnDevClearContracts()
    {
        var arrivals = Arrivals();
        if (arrivals == null) { UIToast.Show("Order arrival service isn't running."); return; }

        int accounts = arrivals.Signed.Count(s => s.Active);
        if (!_clearContractsArmed)
        {
            _clearContractsArmed = true;
            UIToast.Show($"Press CLEAR CONTRACTS again to wipe {accounts} account(s) and the offer board. " +
                         "Orders already on the floor are not affected.");
            // Disarms itself, so a press left forgotten on screen can't be completed ten minutes later
            // by someone who has lost the context of the first one. Long enough to actually read the
            // toast and decide — a window that expires while you're still reading it just produces a
            // second "are you sure?" that looks like the button is broken.
            _modal.schedule.Execute(() => _clearContractsArmed = false).ExecuteLater(12000);
            return;
        }

        _clearContractsArmed = false;
        int dropped = arrivals.ClearAllContracts();
        UIToast.Show(dropped > 0
            ? $"Contract board wiped — {dropped} signed record(s) dropped."
            : "Contract board wiped — there were no signed records.");
        Rebuild();
    }

    /// <summary>See OnDevClearContracts. Reset on a tab change too, so arming it and wandering off to
    /// another tab doesn't leave a live trigger behind.</summary>
    private bool _clearContractsArmed;

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

    /// <summary>The Completed tab's column header, which lives in the stationary strip ABOVE the
    /// scroll view so it can't scroll away vertically. Null on every other tab.</summary>
    private VisualElement _completedHeaderRow;
    private float _lastCompletedHScroll = float.NaN;

    /// <summary>
    /// Slides the Completed header sideways by exactly what the ledger is scrolled, so the two stay
    /// in step once the window is dragged narrower than the columns need.
    ///
    /// The mirror image of SyncFrozenTimeColumn, which holds the Schedule tab's time cells STILL
    /// against a scrolling grid; here the header is outside the scroller and has to be made to move
    /// WITH it. Same mechanism either way: `left` on a relative element is a visual offset that
    /// doesn't disturb siblings or the element's own width.
    /// </summary>
    private void SyncCompletedHeader()
    {
        if (_completedHeaderRow == null) return;

        float x = _content.scrollOffset.x;
        if (Mathf.Approximately(x, _lastCompletedHScroll)) return;
        _lastCompletedHScroll = x;
        _completedHeaderRow.style.left = -x;
    }

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

    /// <summary>Heading for a tab. Only Completed differs today: the other three are all views of the
    /// contract board, which is what the panel is called.</summary>
    private static string TitleFor(Tab tab)
        => tab == Tab.Completed ? "COMPLETED ORDERS" : "OUTBOUND CONTRACTS";

    private void Rebuild()
    {
        BuildTabBar();
        if (_titleLabel != null) _titleLabel.text = TitleFor(_tab);
        _tabHeader.Clear();
        _content.Clear();
        _scheduleTimeCells.Clear(); // stale cells belong to elements that were just destroyed
        _lastHScroll = float.NaN;   // force the next poll to re-apply the offset to the new cells
        _completedHeaderRow = null; // the strip was just cleared; this pointed into it
        _lastCompletedHScroll = float.NaN;
        _footerMessage.text = string.Empty;

        // Only the Schedule grid can outgrow the modal sideways — a row is one column per door, and
        // door count is unbounded. The card tabs stay vertical-only so their text can't be pushed
        // off-screen horizontally by a stray wide element.
        // Schedule and Completed are the two grids that can outgrow the modal sideways — Schedule
        // because a row is one column per door, Completed because the window can now be dragged
        // narrower than its fixed column set. The card tabs stay vertical-only so their text can't be
        // pushed off-screen horizontally by a stray wide element.
        _content.mode = _tab == Tab.Schedule || _tab == Tab.Completed
            ? ScrollViewMode.VerticalAndHorizontal
            : ScrollViewMode.Vertical;

        // Completed reads the ORDER archive, not the contract system, so it's answered before the
        // arrivals guard below — a session with no OrderArrivalService can still have shipped freight,
        // and hiding the ledger behind a service it doesn't use would be wrong.
        if (_tab == Tab.Completed)
        {
            BuildCompleted();
            return;
        }

        var arrivals = Arrivals();
        if (arrivals == null)
        {
            _footerMessage.text = "Order arrival service isn't running — contracts can't be signed. " +
                                  "(OrderArrivalService is not registered in GameContext.)";
            return;
        }

        switch (_tab)
        {
            case Tab.NewContracts: BuildNewContracts(arrivals); break;
            case Tab.BulkOrders: BuildBulkOrders(arrivals); break;
            case Tab.Accounts: BuildAccounts(arrivals); break;
            case Tab.Schedule: BuildSchedule(arrivals); break;
        }
    }

    // ── Tab 1: New Contracts ─────────────────────────────────────────────────

    private void BuildNewContracts(OrderArrivalService arrivals)
    {
        // Intro on the left, dev triggers on the right. The dev cluster lives on THIS tab only — it
        // was in the tab row, which put it on screen while you were reading the Schedule, where it
        // means nothing.
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.FlexStart;
        header.style.marginBottom = 8;

        var intro = new Label("Take a contract to bring work in. BULK ORDERS are a one-off full-pallet drop " +
                              "priced at cost plus 5% — a fresh 1–3 of them land here every day and the rest " +
                              "expire. STANDING ORDERS are an account that sends work daily or weekly.  ·  " +
                              "Drag the title bar to move the window.");
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

        // Bulk first, then standing. Bulk offers expire tonight and standing ones don't, so the
        // perishable decision goes at the top where it will actually be read.
        int row = 0;
        foreach (var contract in offers.OrderByDescending(c => c.IsBulk))
            _content.Add(BuildOfferCard(contract, inv, row++));

        // Anything barred after a missed pickup, so a customer who has vanished from the board is
        // explained rather than just gone.
        var lost = arrivals.LostOffers.ToList();
        foreach (var contract in lost)
            _content.Add(BuildLostCard(contract, arrivals, row++));

        _footerMessage.text = $"{offers.Count} contract(s) on the table · " +
                              $"{arrivals.RunningAccounts.Count()} standing account(s) currently running" +
                              (lost.Count > 0 ? $" · {lost.Count} lost account(s) in cooldown." : ".");
    }

    /// <summary>Accent colour for a contract type — the same family used for its chips elsewhere, so
    /// a bulk order reads as the same thing on this tab and in the stranded strip.</summary>
    private static Color AccentFor(ContractData c)
        => c.IsBulk ? ColBulkEdge : ColBorder;

    private static Color TypeTextFor(ContractData c)
        => c.IsBulk ? ColChipBulkTx : ColChipOutText;

    private VisualElement BuildOfferCard(ContractData contract, InventoryService inv, int rowIndex)
    {
        var card = MakeRow(rowIndex, AccentFor(contract));
        card.Add(MakeIcon(contract.Customer != null ? contract.Customer.Icon : null, IconSize, 8));

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;

        var titleRow = new VisualElement();
        titleRow.style.flexDirection = FlexDirection.Row;
        titleRow.style.alignItems = Align.Center;
        titleRow.Add(MakeText(contract.Customer != null ? contract.Customer.CompanyName : "(no customer assigned)",
                              19, ColTitleText, bold: true));
        titleRow.Add(MakeTypePill(contract));
        body.Add(titleRow);

        body.Add(MakeText(contract.Title, 14, TypeTextFor(contract), bold: true));

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
        var caption = MakeText(contract.IsBulk ? "est. one-off revenue"
                                                : "est. revenue per day", 12, ColSubtleText);
        caption.style.marginBottom = 6;
        right.Add(caption);

        var sign = new Button(() => OnSign(contract)) { text = contract.IsBulk ? "ACCEPT ORDER" : "SIGN CONTRACT" };
        StyleOrangeButton(sign);
        right.Add(sign);

        card.Add(right);
        return card;
    }

    /// <summary>The BULK ORDER / STANDING ORDER badge beside the customer name. Colour AND words, not
    /// colour alone — the two types differ in what they commit the player to, which is too important
    /// to encode only as a hue.</summary>
    private VisualElement MakeTypePill(ContractData contract)
    {
        var pill = new VisualElement();
        pill.style.marginLeft = 8;
        pill.style.paddingLeft = 6; pill.style.paddingRight = 6;
        pill.style.paddingTop = 1; pill.style.paddingBottom = 1;
        pill.style.backgroundColor = new StyleColor(contract.IsBulk ? ColChipBulk : ColChipOut);
        pill.style.borderTopWidth = pill.style.borderBottomWidth =
            pill.style.borderLeftWidth = pill.style.borderRightWidth = 1;
        pill.style.borderTopColor = pill.style.borderBottomColor =
            pill.style.borderLeftColor = pill.style.borderRightColor = new StyleColor(AccentFor(contract));
        pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius =
            pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 4;

        var label = MakeText(contract.KindLabel, 10, TypeTextFor(contract), bold: true);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        pill.Add(label);
        return pill;
    }

    /// <summary>A customer barred after a missed pickup. Shown rather than hidden so the board
    /// explains itself — a name that simply disappeared reads as a bug, not a consequence.</summary>
    private VisualElement BuildLostCard(ContractData contract, OrderArrivalService arrivals, int rowIndex)
    {
        var card = MakeRow(rowIndex, ColDanger);
        card.style.opacity = 0.55f;
        card.Add(MakeIcon(contract.Customer != null ? contract.Customer.Icon : null, IconSize, 8));

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.Add(MakeText(contract.Customer != null ? contract.Customer.CompanyName : contract.ContractId,
                          19, ColDangerSoft, bold: true));
        body.Add(MakeText("ACCOUNT LOST — pickup was never scheduled", 14, ColDangerSoft, bold: true));
        var wait = MakeText($"They'll consider you again in {arrivals.LossCooldownDaysRemaining(contract.ContractId)} day(s).",
                            14, ColSubtleText);
        wait.style.marginTop = 2;
        body.Add(wait);
        card.Add(body);
        return card;
    }

    private static string TermsLine(ContractData c)
    {
        if (c.IsBulk)
            return $"{c.BulkLinesMin}–{c.BulkLinesMax} item(s) · full pallets out of reserve · " +
                   $"cost of goods + 5% · due in {c.LeadTimeDays} day(s) · late fee {c.LateFeePercent:P0}";

        return $"{c.FrequencyLabel} · {c.OrdersPerDayMin}–{c.OrdersPerDayMax} orders/day · " +
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

        if (c.IsBulk)
        {
            // Priced off BUY value, not sell — a bulk order pays cost of goods plus 5%, so estimating
            // it from SellValue like the others would overstate every card on the board.
            var palletCapable = sellable.Where(s => s.Ti > 0 && s.Hi > 0 && s.BuyValue > 0f).ToList();
            if (palletCapable.Count == 0) return $"x{OrderArrivalService.BulkSurchargeMultiplier:0.00}";
            float avgPalletCost = palletCapable.Average(s => s.Ti * s.Hi * s.BuyValue);
            float midLines = (c.BulkLinesMin + c.BulkLinesMax) / 2f;
            float midPallets = (c.BulkPalletsPerLineMin + c.BulkPalletsPerLineMax) / 2f;
            return $"+${Mathf.RoundToInt(avgPalletCost * midLines * midPallets * OrderArrivalService.BulkSurchargeMultiplier):N0}";
        }

        float avgCase = sellable.Average(s => s.SellValue);
        return $"+${Mathf.RoundToInt(avgCase * c.EstimatedCasesPerDay * c.PayRateMultiplier):N0}";
    }

    // ── Tab 2: Bulk Orders ───────────────────────────────────────────────────

    /// <summary>Bulk orders still in the building — accepted and not yet shipped or cancelled.</summary>
    private static List<OrderData> LiveBulkOrders()
    {
        if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null) return new List<OrderData>();
        return orders.ActiveOrders
            .Where(o => o != null && o.IsBulk
                     && o.Status != OrderData.OrderStatus.Shipped
                     && o.Status != OrderData.OrderStatus.Cancelled)
            .OrderBy(o => o.DueDay)
            .ThenBy(o => o.CustomerName, System.StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The live bulk orders and how far through the warehouse each one is.
    ///
    /// Deliberately not a second Work Queue. This answers the two questions the Work Queue can't:
    /// how a bulk order breaks down into pallets versus loose cases, and whether it has a door booked
    /// — which is what decides whether the account survives past its due day.
    /// </summary>
    private void BuildBulkOrders(OrderArrivalService arrivals)
    {
        var intro = new Label("Bulk orders you've accepted. Each line ships as whole pallets out of reserve " +
                              "(a Pallet Pick for the Reach Trucks) plus any loose cases (a normal case pick). " +
                              "Anything still showing NO DOOR when its due day passes loses the account.");
        ApplyFont(intro, size: 14);
        intro.style.color = new StyleColor(ColSubtleText);
        intro.style.whiteSpace = WhiteSpace.Normal;
        intro.style.marginBottom = 8;
        _content.Add(intro);

        var live = LiveBulkOrders();
        if (live.Count == 0)
        {
            var none = new Label("No bulk orders on the floor. Accept one on the New Contracts tab — " +
                                 "1–3 land there every day.");
            ApplyFont(none, size: 15);
            none.style.color = new StyleColor(ColSubtleText);
            none.style.whiteSpace = WhiteSpace.Normal;
            none.style.marginTop = 12;
            _content.Add(none);
            _footerMessage.text = "No bulk orders in progress.";
            return;
        }

        var schedule = Schedule();
        int today = CurrentDay();
        int unbooked = 0;

        int row = 0;
        foreach (var order in live)
        {
            bool booked = schedule != null && schedule.FindForOrder(order.OrderId) != null;
            if (!booked) unbooked++;
            _content.Add(BuildBulkOrderRow(order, booked, today, row++));
        }

        _footerMessage.text = $"{live.Count} bulk order(s) in progress" +
                              (unbooked > 0
                                  ? $"  ·  {unbooked} with NO DOOR BOOKED — book them on the Schedule tab."
                                  : "  ·  all have a door booked.");
    }

    private VisualElement BuildBulkOrderRow(OrderData order, bool booked, int today, int rowIndex)
    {
        bool late = order.DueDay < today;
        var card = MakeRow(rowIndex, !booked ? ColDanger : ColBulkEdge);

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;

        body.Add(MakeText(order.CustomerName, 17, ColTitleText, bold: true));

        // One line per SKU, spelled out as the split the warehouse will actually work: N pallets plus
        // whatever part case is left. That breakdown is the whole reason this tab exists.
        ServiceLocator.TryGet<OrderService>(out var orders);
        foreach (var li in order.LineItems)
        {
            int fullPallet = orders?.FullPalletCases(li.SkuId) ?? 0;
            string split = fullPallet > 0
                ? $"{li.QuantityNeeded / fullPallet} pallet(s)" +
                  (li.QuantityNeeded % fullPallet > 0 ? $" + {li.QuantityNeeded % fullPallet} cs" : "")
                : $"{li.QuantityNeeded} cs (no pallet maths)";

            var lineText = MakeText($"{li.SkuId} — {li.QuantityNeeded} cs  ·  {split}", 13, ColSubtleText);
            lineText.style.marginTop = 1;
            body.Add(lineText);
        }
        card.Add(body);

        var right = new VisualElement();
        right.style.width = 230;
        right.style.flexShrink = 0;
        right.style.alignItems = Align.FlexEnd;

        right.Add(MakeText(BulkPhaseLabel(order), 14, ColTitleText, bold: true));

        string due = late ? $"{today - order.DueDay}d LATE" : order.DueDay == today ? "due today" : $"due day {order.DueDay}";
        var dueLabel = MakeText(due, 13, late ? ColDangerSoft : ColSubtleText, bold: late);
        dueLabel.style.marginTop = 2;
        right.Add(dueLabel);

        var dock = MakeText(booked ? "door booked" : "NO DOOR BOOKED", 13,
                            booked ? ColMoney : ColDangerSoft, bold: !booked);
        dock.style.marginTop = 2;
        right.Add(dock);

        right.Add(MakeText($"{order.TotalUnitsPicked}/{order.TotalUnits} cs picked", 12, ColSubtleText));

        card.Add(right);
        return card;
    }

    /// <summary>Where this order has got to, in the player's words rather than OrderStatus's.</summary>
    private static string BulkPhaseLabel(OrderData order) => order.Status switch
    {
        OrderData.OrderStatus.Pending => "Awaiting release",
        OrderData.OrderStatus.PartiallyPicked => "Being picked",
        OrderData.OrderStatus.FullyPicked => "Picked",
        OrderData.OrderStatus.Staged => "Staged — ready to load",
        OrderData.OrderStatus.Loading => "Loading",
        OrderData.OrderStatus.Loaded => "Loaded — awaiting close-out",
        _ => order.Status.ToString()
    };

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
        UIToast.Show(contract.IsBulk
            ? $"{who}: bulk order accepted — book it a door on the Schedule tab."
            : $"{who} signed — orders start arriving at {contract.CutoffHour:00}:00.");

        Rebuild();
    }

    // ── Tab 3: Accounts ──────────────────────────────────────────────────────

    private void BuildAccounts(OrderArrivalService arrivals)
    {
        var running = arrivals.RunningAccounts.ToList();

        if (running.Count == 0)
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

        // Spent one-offs are NOT listed here any more. A delivered wholesale drop is a finished deal,
        // not an account you're running — it has nothing left to arrive and nothing to cancel, and its
        // SignedContract carries no useful figures either (the per-contract counters only accrue for
        // orders stamped with a ContractId, which these aren't), so the row read "$0 earned · 0
        // shipped" forever while the real money sat on the Completed tab. That tab is where finished
        // deals live now, per ORDER and with the actual revenue on them.
        _footerMessage.text = $"{running.Count} standing account(s) running. Finished one-offs are on the " +
                              "Completed tab. Cancelling stops future orders; anything already on the " +
                              "board still has to ship.";
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

    /// <summary>Always a running standing account — RunningAccounts excludes IsBulk (which now also
    /// covers the retired wholesale ordinal), so a bulk/wholesale contract never reaches this
    /// row.</summary>
    private VisualElement BuildAccountRow(OrderArrivalService arrivals, SignedContract signed,
                                          ContractData contract, int rowIndex)
    {
        bool struggling = signed.OrdersLate > 0;
        Color accent = struggling ? ColDanger : ColMoney;

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

        var cancel = new Button(() => OnCancel(contract, signed)) { text = "CANCEL" };
        StyleGhostButton(cancel);
        right.Add(cancel);

        card.Add(right);
        return card;
    }

    private string StatusLine(OrderArrivalService arrivals, SignedContract signed, ContractData contract)
    {
        int today = CurrentDay();
        int daysHeld = Mathf.Max(0, today - signed.SignedOnDay);

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

        // A held selection can go stale between rebuilds — the trailer may have loaded out, or its block
        // elapsed, while it sat picked up. Drop it here rather than leave empty slots advertising a move
        // that TryMove will now refuse. FindById returns null for one that's gone, which locks too.
        if (_selectedAppointmentId != null && schedule.IsLocked(schedule.FindById(_selectedAppointmentId)))
            _selectedAppointmentId = null;

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

        // Stranded freight goes ABOVE the grid and inside the stationary strip for the same reason the
        // legend does — you need it in view while you scroll the grid hunting for somewhere to put it.
        var unscheduled = schedule.UnscheduledGroups();
        if (unscheduled.Count > 0) _tabHeader.Add(BuildUnscheduledStrip(unscheduled, arrivals));

        for (int block = 0; block < DockScheduleService.BlocksPerDay; block++)
            _content.Add(BuildScheduleRow(schedule, arrivals, doors, block, today));

        // The rows were just rebuilt at offset 0 while the view may still be scrolled — re-apply
        // immediately so a rebuild (moving an appointment) doesn't flash the frozen column back to the
        // left of a scrolled grid for a frame before the poller catches up.
        SyncFrozenTimeColumn();

        int booked = schedule.Appointments.Count(a => a.Day == _scheduleDay);
        int capacity = doors.Count * DockScheduleService.BlocksPerDay;

        if (_selectedUnscheduledKey != null)
            _footerMessage.text = "Pick an empty slot to book this trailer in, or click it again to put it down.";
        else if (_selectedAppointmentId != null)
            _footerMessage.text = "Pick an empty slot to move the selected appointment, or click it again to drop it.";
        else
            _footerMessage.text = $"{booked} of {capacity} slots booked · arriving orders auto-book the first free " +
                                  $"block before their due day — click an appointment to move it." +
                                  (unscheduled.Count > 0
                                      ? $"  ·  {unscheduled.Count} trailer(s) still need a door — see above."
                                      : string.Empty);
    }

    /// <summary>
    /// The stranded-freight strip: live orders no appointment is holding a door for.
    ///
    /// Exists because these orders were previously invisible on this tab AND unbookable — auto-placement
    /// gets one attempt when the order arrives and never retries, so anything it missed simply never
    /// appeared, while the Accounts tab happily reported the customer as having freight due. A chip here
    /// is picked up and dropped into a slot with exactly the same two clicks as moving a booked one.
    /// </summary>
    private VisualElement BuildUnscheduledStrip(List<UnscheduledGroup> groups,
                                                OrderArrivalService arrivals)
    {
        int today = CurrentDay();

        var wrap = new VisualElement();
        wrap.style.marginBottom = 8;
        wrap.style.paddingTop = 6; wrap.style.paddingBottom = 6;
        wrap.style.paddingLeft = 8; wrap.style.paddingRight = 8;
        wrap.style.backgroundColor = new StyleColor(new Color(ColDanger.r, ColDanger.g, ColDanger.b, 0.10f));
        wrap.style.borderLeftWidth = 3;
        wrap.style.borderLeftColor = new StyleColor(ColDanger);
        wrap.style.borderTopLeftRadius = wrap.style.borderTopRightRadius =
            wrap.style.borderBottomLeftRadius = wrap.style.borderBottomRightRadius = 6;

        int orderCount = groups.Sum(g => g.OrderIds.Count);
        var heading = MakeText($"NO DOOR BOOKED — {orderCount} order(s) on {groups.Count} trailer(s). " +
                               "Click one, then click an open slot.", 12, ColDangerSoft, bold: true);
        heading.style.marginBottom = 5;
        wrap.Add(heading);

        // Wrapping row rather than a horizontal scroller: this strip eats vertical space from the grid,
        // and a wrap keeps that cost proportional to how bad the backlog actually is.
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        foreach (var group in groups)
            row.Add(BuildUnscheduledChip(group, arrivals, today));
        wrap.Add(row);
        return wrap;
    }

    private VisualElement BuildUnscheduledChip(UnscheduledGroup group,
                                               OrderArrivalService arrivals, int today)
    {
        bool selected = group.Key == _selectedUnscheduledKey;
        bool late = group.EarliestDueDay < today;

        var chip = new VisualElement();
        chip.style.flexDirection = FlexDirection.Row;
        chip.style.alignItems = Align.Center;
        chip.style.flexShrink = 0;
        chip.style.marginRight = 5; chip.style.marginBottom = 4;
        chip.style.paddingLeft = 6; chip.style.paddingRight = 8;
        chip.style.paddingTop = 3; chip.style.paddingBottom = 3;
        // Coloured by TYPE, matching the booked chips in the grid — the strip is where the three
        // types sit side by side, so it's the one place the distinction has to survive at chip size.
        // Selection and lateness still override: what you're holding and what's overdue matter more
        // than what kind it is.
        chip.style.backgroundColor = new StyleColor(selected ? ColOrange : ChipFill(group.Kind));
        chip.style.borderTopWidth = chip.style.borderBottomWidth =
            chip.style.borderLeftWidth = chip.style.borderRightWidth = 1;
        chip.style.borderTopColor = chip.style.borderBottomColor =
            chip.style.borderLeftColor = chip.style.borderRightColor =
                new StyleColor(selected ? ColOrangeText : late ? ColDanger : ChipEdge(group.Kind));
        chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius =
            chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 5;

        Color text = selected ? ColOrangeText : late ? ColDangerSoft : ChipText(group.Kind);
        chip.Add(MakeIcon(IconForCustomer(group.CustomerId, group.ContractId, arrivals), IconSizeTiny, 3,
                          marginRight: 5));

        var name = MakeText(group.CustomerName, 11, text);
        name.style.whiteSpace = WhiteSpace.NoWrap;
        chip.Add(name);

        string due = late ? $"{today - group.EarliestDueDay}d LATE"
                   : group.EarliestDueDay == today ? "due today"
                   : $"due d{group.EarliestDueDay}";
        var meta = MakeText($"· {group.OrderIds.Count} · {due}", 11,
                            selected ? ColOrangeText : late ? ColDangerSoft : ColSubtleText, bold: late);
        meta.style.marginLeft = 4;
        meta.style.whiteSpace = WhiteSpace.NoWrap;
        chip.Add(meta);

        chip.RegisterCallback<ClickEvent>(_ => OnUnscheduledClicked(group));
        chip.tooltip = $"{group.CustomerName} · {group.OrderIds.Count} order(s) with no dock appointment · " +
                       $"earliest due day {group.EarliestDueDay} · click, then click an open slot to book";
        return chip;
    }

    private void OnUnscheduledClicked(UnscheduledGroup group)
    {
        _selectedUnscheduledKey = _selectedUnscheduledKey == group.Key ? null : group.Key;
        _selectedAppointmentId = null; // one thing in hand at a time — see _selectedUnscheduledKey
        Rebuild();
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
                ? BuildAppointmentChip(schedule, appts[i], arrivals)
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

    // One palette for every appointment chip, wherever it's drawn. Booked chips in the grid and
    // stranded chips in the strip are the same trailer at different stages, so they have to be the
    // same colour — three separate switch blocks is how they drifted apart the first time.
    private static Color ChipFill(AppointmentKind kind) => kind switch
    {
        AppointmentKind.Inbound => ColChipIn,
        // Wholesale is retired (folded into Bulk) but ordinal-persisted — a not-yet-purged saved
        // appointment from before the merge can still carry it, so it renders identically to Bulk
        // rather than falling through to the generic Outbound style.
        AppointmentKind.Bulk or AppointmentKind.Wholesale => ColChipBulk,
        _ => ColChipOut
    };

    private static Color ChipEdge(AppointmentKind kind) => kind switch
    {
        AppointmentKind.Inbound => ColOrangeEdge,
        AppointmentKind.Bulk or AppointmentKind.Wholesale => ColBulkEdge,
        _ => ColBlueEdge
    };

    private static Color ChipText(AppointmentKind kind) => kind switch
    {
        AppointmentKind.Inbound => ColWholesale,
        AppointmentKind.Bulk or AppointmentKind.Wholesale => ColChipBulkTx,
        _ => ColChipOutText
    };

    /// <summary>
    /// One booked trailer. Locked ones — elapsed, already loaded, or an inbound PO's note — render as
    /// flat history: dimmed, no selection border, no click handler at all. They used to look and behave
    /// exactly like a live booking, so a closed-out load could be picked up and rescheduled into the
    /// future, which both faked the calendar and consumed a door a real trailer needed.
    /// </summary>
    private VisualElement BuildAppointmentChip(DockScheduleService schedule, DockAppointment appt,
                                               OrderArrivalService arrivals)
    {
        bool locked = schedule.IsLocked(appt, out string lockReason);
        bool selected = !locked && appt.Id == _selectedAppointmentId;
        Color fill = ChipFill(appt.Kind);
        Color edge = ChipEdge(appt.Kind);
        Color text = ChipText(appt.Kind);

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

        // Dimmed as a whole rather than by swapping every colour for a grey one: it keeps the kind's
        // tint readable (you can still tell an inbound from an outbound at a glance) while reading
        // unmistakably as finished next to a live chip in the row above.
        if (locked) chip.style.opacity = 0.45f;

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
        // grab handle would eat most of the label. A locked chip registers NO handler, so it can't be
        // picked up at all; the tooltip still explains why rather than leaving it inertly unresponsive.
        if (!locked) chip.RegisterCallback<ClickEvent>(_ => OnChipClicked(appt));

        chip.tooltip = $"{appt.CustomerName} · {appt.TimeLabel} · door {appt.DoorNumber} · " +
                       $"{appt.OrderIds.Count} order(s) · " + (locked ? lockReason : "click to move");
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

        bool booking = _selectedUnscheduledKey != null;
        bool canDrop = (booking || _selectedAppointmentId != null) && !past;
        var label = MakeText(!canDrop ? "— open —" : booking ? "book here" : "move here", 11,
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
        _selectedUnscheduledKey = null; // one thing in hand at a time
        Rebuild();
    }

    private void OnSlotClicked(int block)
    {
        var schedule = Schedule();
        if (schedule == null) return;

        // Booking a stranded trailer and moving a booked one land on the same slot click; which one is
        // meant is decided by what's in hand, and only one ever is.
        if (_selectedUnscheduledKey != null)
        {
            var group = schedule.UnscheduledGroups()
                                .FirstOrDefault(g => g.Key == _selectedUnscheduledKey);
            if (group == null)
            {
                // Something shipped or was cancelled while it sat selected. Rebuild rather than book a
                // trailer for freight that no longer needs one.
                UIToast.Show("Those orders are no longer waiting on a door.");
                _selectedUnscheduledKey = null;
                Rebuild();
                return;
            }

            if (!schedule.TryBookGroup(_scheduleDay, block, group, out string bookWhy))
            {
                UIToast.Show(bookWhy);
                return;
            }

            UIToast.Show($"{group.CustomerName} booked into {DockScheduleService.BlockLabel(block)} " +
                         $"on day {_scheduleDay}.");
            _selectedUnscheduledKey = null;
            Rebuild();
            return;
        }

        if (_selectedAppointmentId == null) return;

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
        legend.Add(MakeLegendSwatch(ColChipOut, ColBlueEdge, "standing order"));
        legend.Add(MakeLegendSwatch(ColChipBulk, ColBulkEdge, "bulk order"));
        legend.Add(MakeLegendSwatch(ColChipIn, ColOrangeEdge, "inbound PO (same doors)"));
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
        => IconForCustomer(appt.CustomerId, appt.ContractId, arrivals);

    /// <summary>Shared by booked chips and stranded ones — both identify a trailer the same way.</summary>
    private Sprite IconForCustomer(string customerId, string contractId, OrderArrivalService arrivals)
    {
        if (arrivals == null) return null;

        if (!string.IsNullOrEmpty(contractId))
        {
            var byContract = arrivals.GetContract(contractId);
            if (byContract?.Customer != null) return byContract.Customer.Icon;
        }

        if (!string.IsNullOrEmpty(customerId))
        {
            foreach (var c in arrivals.Catalog)
                if (c?.Customer != null && c.Customer.CustomerId == customerId)
                    return c.Customer.Icon;
        }
        return null; // inbound suppliers have no CustomerData; the chip's tint carries the meaning
    }

    // ── Tab 5: Completed ─────────────────────────────────────────────────────

    /// <summary>
    /// The deals this tab reports: orders that were closed out and paid for.
    ///
    /// Cancelled orders share the same archive (OrderService.OrderHistory holds both) but are
    /// deliberately excluded — a called-off order isn't a completed deal, it made no money and shipped
    /// no freight, and it would sit in the ledger as a row of zeroes dragging every total down with it.
    ///
    /// The archive is capped at OrderService.MaxArchivedOrders and trims oldest-first, so a long game
    /// shows the most recent N deals rather than all of them. Lifetime per-account totals live on the
    /// Accounts tab, which accumulates them and never forgets.
    /// </summary>
    private static List<OrderData> CompletedOrders()
    {
        if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null)
            return new List<OrderData>();
        return orders.OrderHistory
            .Where(o => o != null && o.Status == OrderData.OrderStatus.Shipped)
            .ToList();
    }

    /// <summary>CompletedOrders narrowed by the Shipped column's date filter, if active. This is what
    /// every downstream reader of the tab (ledger rows, the Revenue/Net Profit/Fill Rate stat strip,
    /// the footer summary) actually consumes — narrowing them all from one shared filtered list is what
    /// makes picking a date range recompute everything together instead of just hiding rows.</summary>
    private List<OrderData> FilteredCompletedOrders()
    {
        var deals = CompletedOrders();
        if (!_doneDateFilterActive) return deals;
        return deals.Where(o => _doneDateFilterSelection.Contains(DoneDateFilterValue(o))).ToList();
    }

    /// <summary>The bucket a deal's Shipped date filters into — one per in-game day, "Unknown" for the
    /// rare pre-migration order with no date stamp at all. Deliberately day-granular (drops the HH:MM
    /// ClosedDateText shows on the row itself) since a per-minute filter list would be unusable to scan
    /// or check off.</summary>
    private static string DoneDateFilterValue(OrderData order)
    {
        if (order == null) return "Unknown";
        int day = HasClosedStamp(order) ? order.ClosedDayNumber
                 : order.CreatedDayNumber > 0 ? order.CreatedDayNumber : -1;
        return day >= 0 ? $"Day {day}" : "Unknown";
    }

    /// <summary>Numeric key behind a DoneDateFilterValue bucket, so the filter's checkbox list can sort
    /// newest-first instead of "Day 10" alphabetically outranking "Day 2". Unknown sorts last.</summary>
    private static int DoneDateFilterSortKey(string bucket)
        => bucket.StartsWith("Day ") && int.TryParse(bucket.Substring(4), out int d) ? d : int.MinValue;

    /// <summary>Every distinct Shipped-date bucket across the WHOLE archive, not just what's currently
    /// filtered in — an Excel filter's own option list doesn't shrink as you narrow it, only the rows do.</summary>
    private static List<string> CollectDoneDateBuckets()
    {
        var buckets = CompletedOrders().Select(DoneDateFilterValue).Distinct().ToList();
        buckets.Sort((a, b) => DoneDateFilterSortKey(b).CompareTo(DoneDateFilterSortKey(a)));
        return buckets;
    }

    private void BuildCompleted()
    {
        var allDeals = CompletedOrders();

        if (allDeals.Count == 0)
        {
            var none = MakeText("Nothing shipped yet. Close a loaded trailer out on the Work Queue " +
                                "(key 7) and the deal is recorded here.", 15, ColSubtleText);
            none.style.marginTop = 12;
            _content.Add(none);
            _footerMessage.text = "No completed deals on record.";
            return;
        }

        var deals = SortedCompleted();

        // Both the stat strip and the column headings go in the STATIONARY strip above the scroll
        // view, not in _content — a ledger whose headings (or totals) scroll off the top stops being
        // readable the moment it's longer than the panel. Stats are added FIRST so the header ends up
        // directly above row 1, the same "heading sits right on top of its data" layout every other
        // tab uses — they used to be swapped, putting a gap of stat tiles between the header and what
        // it names.
        //
        // Built off allDeals.Count > 0 rather than deals.Count > 0 — the header carries the Shipped
        // filter dropdown, so it has to stay reachable even when the current filter selection hides
        // every row, or narrowing to an empty date range would strand the player with no way back in.
        _tabHeader.Add(BuildCompletedSummary(deals));

        // Being outside the scroller is also why SyncCompletedHeader has to hand the header the
        // horizontal scroll offset: vertical stillness is wanted here, horizontal stillness would be
        // a bug — the header still has to slide with the ledger when it's scrolled sideways.
        _completedHeaderRow = BuildCompletedHeader();
        _tabHeader.Add(_completedHeaderRow);

        if (deals.Count == 0)
        {
            var none = MakeText("No completed deals match the selected Shipped date(s). Open the " +
                                "Shipped column's filter and check more days, or Clear Filter to see " +
                                "everything.", 15, ColSubtleText);
            none.style.marginTop = 12;
            _content.Add(none);
            _footerMessage.text = "No completed deals match the selected date(s).";
            return;
        }

        for (int i = 0; i < deals.Count; i++)
            _content.Add(BuildCompletedRow(deals[i], i));

        int cases = deals.Sum(o => o.TotalUnitsPicked);
        int ordered = deals.Sum(o => o.TotalUnits);
        int short_ = deals.Count(o => o.TotalUnitsPicked < o.TotalUnits);
        float pallets = deals.Sum(EffectivePallets);
        // Spelled "about" rather than prefixed "~", for the same reason the cells dropped their tilde:
        // in front of a number it reads as a minus sign. One decimal — summing scores of modelled
        // fractions to two would be false precision.
        string palletTotal = deals.All(HasPalletCount)
            ? $"{pallets.ToString("N0")} pallet(s)"
            : $"about {pallets.ToString("0.#")} pallet(s)";
        _footerMessage.text = $"{deals.Count} completed deal(s) on record · {palletTotal}, " +
                              $"{cases:N0} of {ordered:N0} case(s) shipped" +
                              (short_ > 0 ? $" · {short_} shipped short" : " · none shipped short") +
                              ". Click a column heading to re-sort.";
    }

    /// <summary>Three totals across everything on the tab. Net profit is the middle number on purpose:
    /// revenue alone flatters a deal that cost almost as much to buy as it sold for.</summary>
    private VisualElement BuildCompletedSummary(List<OrderData> deals)
    {
        int revenue = deals.Sum(o => o.ShippedRevenue);
        int profit = deals.Sum(o => o.ShippedProfit);
        int cases = deals.Sum(o => o.TotalUnitsPicked);
        int ordered = deals.Sum(o => o.TotalUnits);
        // Weighted by cases rather than averaging the per-order percentages: a 4-case order shipping
        // short shouldn't move the service level as far as a 400-case one does.
        float fill = ordered > 0 ? (float)cases / ordered : 1f;

        var strip = new VisualElement();
        strip.style.flexDirection = FlexDirection.Row;
        strip.style.marginBottom = 8;
        strip.Add(MakeStatTile("REVENUE", Money(revenue), ColMoney));
        strip.Add(MakeStatTile("NET PROFIT", Money(profit), profit >= 0 ? ColMoney : ColDangerSoft));
        strip.Add(MakeStatTile("FILL RATE", $"{fill:P0}",
                               fill >= 0.99f ? ColMoney : fill >= 0.9f ? ColWholesale : ColDangerSoft));
        return strip;
    }

    private List<OrderData> SortedCompleted()
    {
        var deals = FilteredCompletedOrders();

        IEnumerable<OrderData> sorted = _doneSort switch
        {
            DoneColumn.Order    => deals.OrderBy(o => o.OrderId),
            DoneColumn.Account  => deals.OrderBy(o => o.CustomerName),
            DoneColumn.Type     => deals.OrderBy(OrderTypeLabel),
            DoneColumn.Revenue  => deals.OrderBy(o => o.ShippedRevenue),
            DoneColumn.Profit   => deals.OrderBy(o => o.ShippedProfit),
            // Sorts on the same number the column shows, estimate included — a column that sorted on
            // something other than its own visible values would read as broken.
            DoneColumn.Pallets  => deals.OrderBy(EffectivePallets),
            DoneColumn.Cases    => deals.OrderBy(o => o.TotalUnitsPicked),
            DoneColumn.FillRate => deals.OrderBy(FillRatio),
            _                   => deals.OrderBy(ClosedSortKey),
        };

        // Built ascending, then reversed — same shape the Work Queue's sorts use.
        return (_doneAscending ? sorted : sorted.Reverse()).ToList();
    }

    /// <summary>
    /// One comparable moment for "when this deal closed".
    ///
    /// Sorts on the SAME value the Shipped column displays, including the approximate arrival day a
    /// legacy deal falls back to — a list that sorts by something other than the dates you can read
    /// in it just looks broken. An order with neither stamp nor arrival day sorts to the very bottom
    /// of a newest-first list rather than jumping to the top as day 0.
    /// </summary>
    private static long ClosedSortKey(OrderData order)
    {
        if (order == null) return long.MinValue;
        if (HasClosedStamp(order))
            return (long)order.ClosedDayNumber * 1440L + Mathf.Max(0, order.ClosedMinuteOfDay);
        // Arrival day only — no time of day to go with it, so it lands at the start of that day and
        // therefore below any real pickup stamped on the same day. That's the right way round: a
        // guess should never outrank a fact.
        return order.CreatedDayNumber > 0 ? (long)order.CreatedDayNumber * 1440L : long.MinValue;
    }

    private static float FillRatio(OrderData order)
        => order == null || order.TotalUnits <= 0 ? 0f : (float)order.TotalUnitsPicked / order.TotalUnits;

    private VisualElement BuildCompletedHeader()
    {
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.paddingLeft = DoneRowIndent;
        header.style.paddingBottom = 4;
        header.style.marginBottom = 2;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = new StyleColor(ColBlueEdge);
        // Invisible counterpart to a row's status stripe — see DoneStripeWidth.
        header.style.borderLeftWidth = DoneStripeWidth;
        header.style.borderLeftColor = new StyleColor(Color.clear);

        header.Add(DoneDateHeaderCell());
        header.Add(DoneHeaderCell("Order #", DoneOrderWidth, DoneColumn.Order));
        header.Add(DoneHeaderCell("Account", DoneAccountWidth, DoneColumn.Account));
        header.Add(DoneHeaderCell("Type", DoneTypeWidth, DoneColumn.Type));
        header.Add(DoneHeaderCell("Revenue", DoneRevenueWidth, DoneColumn.Revenue));
        header.Add(DoneHeaderCell("Net Profit", DoneProfitWidth, DoneColumn.Profit));
        header.Add(DoneHeaderCell("Pallets", DonePalletsWidth, DoneColumn.Pallets));
        header.Add(DoneHeaderCell("Cases", DoneCasesWidth, DoneColumn.Cases));
        header.Add(DoneHeaderCell("Fill Rate", DoneFillWidth, DoneColumn.FillRate));
        return header;
    }

    /// <summary>A sortable column heading: click to sort by it, click again to reverse.</summary>
    private Button DoneHeaderCell(string text, float width, DoneColumn column)
    {
        bool active = _doneSort == column;

        var head = new Button();
        ApplyFont(head, bold: true, size: DoneFontSize);
        head.text = active ? text + (_doneAscending ? " ▲" : " ▼") : text;
        head.style.color = new StyleColor(active ? ColOrange : ColSubtleText);
        head.style.width = width;
        head.style.minWidth = width;
        head.style.flexShrink = 0;
        head.style.backgroundColor = new StyleColor(Color.clear);
        head.style.borderTopWidth = head.style.borderBottomWidth =
            head.style.borderLeftWidth = head.style.borderRightWidth = 0;
        head.style.marginLeft = 0; head.style.marginRight = 0;
        head.style.paddingLeft = 0; head.style.paddingRight = IsNumericDone(column) ? 10 : 0;
        head.style.paddingTop = 0; head.style.paddingBottom = 0;
        head.style.unityTextAlign = IsNumericDone(column) ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;

        head.clicked += () =>
        {
            if (_doneSort == column) _doneAscending = !_doneAscending;
            else
            {
                _doneSort = column;
                // Money, counts, fill and dates all read most-useful biggest-first; text reads A–Z.
                _doneAscending = column == DoneColumn.Account || column == DoneColumn.Type
                               || column == DoneColumn.Order;
            }
            Rebuild();
        };
        if (!active)
        {
            head.RegisterCallback<MouseEnterEvent>(_ => head.style.color = new StyleColor(ColTitleText));
            head.RegisterCallback<MouseLeaveEvent>(_ => head.style.color = new StyleColor(ColSubtleText));
        }
        return head;
    }

    // ── Shipped column: Excel-style multi-select date filter ───────────────────

    /// <summary>The Shipped column's header. Still click-to-sort like every other column (sort state
    /// is unaffected by the filter and the popup's own sort buttons drive the same _doneSort field),
    /// but also carries the dropdown arrow that opens the date filter popup — clicking the label
    /// re-sorts, clicking the arrow (or anywhere else in the cell) opens the filter.</summary>
    private VisualElement DoneDateHeaderCell()
    {
        bool sortActive = _doneSort == DoneColumn.Date;
        Color idle = _doneDateFilterActive || sortActive ? ColOrange : ColSubtleText;

        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.alignItems = Align.Center;
        container.style.width = DoneDateWidth;
        container.style.minWidth = DoneDateWidth;
        container.style.flexShrink = 0;

        var label = new Button { text = sortActive ? "Shipped" + (_doneAscending ? " ▲" : " ▼") : "Shipped" };
        ApplyFont(label, bold: true, size: DoneFontSize);
        label.style.color = new StyleColor(idle);
        label.style.flexShrink = 0;
        label.style.backgroundColor = new StyleColor(Color.clear);
        label.style.borderTopWidth = label.style.borderBottomWidth =
            label.style.borderLeftWidth = label.style.borderRightWidth = 0;
        label.style.marginLeft = 0; label.style.marginRight = 0;
        label.style.paddingLeft = 0; label.style.paddingRight = 0;
        label.style.paddingTop = 0; label.style.paddingBottom = 0;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.clicked += () =>
        {
            if (_doneSort == DoneColumn.Date) _doneAscending = !_doneAscending;
            else { _doneSort = DoneColumn.Date; _doneAscending = false; }
            Rebuild();
        };
        container.Add(label);

        var icon = new Label("▼");
        ApplyFont(icon, size: 10);
        icon.style.color = new StyleColor(idle);
        icon.style.marginLeft = 4;
        icon.RegisterCallback<ClickEvent>(e =>
        {
            e.StopPropagation();
            ToggleDoneDateFilterPopup(container);
        });
        container.Add(icon);

        if (!sortActive && !_doneDateFilterActive)
        {
            container.RegisterCallback<PointerEnterEvent>(_ =>
            {
                label.style.color = new StyleColor(ColTitleText);
                icon.style.color = new StyleColor(ColTitleText);
            });
            container.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                label.style.color = new StyleColor(ColSubtleText);
                icon.style.color = new StyleColor(ColSubtleText);
            });
        }

        return container;
    }

    private void ToggleDoneDateFilterPopup(VisualElement anchor)
    {
        if (_doneDateFilterPopup != null) { CloseDoneDateFilterPopup(); return; }
        OpenDoneDateFilterPopup(anchor);
    }

    private void OpenDoneDateFilterPopup(VisualElement anchor)
    {
        var allBuckets = CollectDoneDateBuckets();

        // First open with nothing unchecked yet: start from "everything selected" so the popup reads
        // as a full list with all boxes ticked, matching Excel's own first-open state.
        if (!_doneDateFilterActive)
        {
            _doneDateFilterSelection.Clear();
            foreach (var b in allBuckets) _doneDateFilterSelection.Add(b);
        }

        var anchorBounds = anchor.worldBound;
        var modalBounds = _modal.worldBound;
        float popupX = anchorBounds.x - modalBounds.x;
        float popupY = anchorBounds.y + anchorBounds.height - modalBounds.y;

        _doneDateFilterPopup = BuildDoneDateFilterPopup(allBuckets, popupX, popupY);
        _modal.Add(_doneDateFilterPopup);
    }

    private void CloseDoneDateFilterPopup()
    {
        if (_doneDateFilterPopup != null)
        {
            _doneDateFilterPopup.RemoveFromHierarchy();
            _doneDateFilterPopup = null;
        }
        _doneDateFilterSearch = "";
    }

    private VisualElement BuildDoneDateFilterPopup(List<string> allBuckets, float x, float y)
    {
        const float PopupWidth = 240f;
        const float PopupMaxHeight = 380f;
        const float ListMaxHeight = 220f;

        var clickCatcher = new VisualElement();
        clickCatcher.style.position = Position.Absolute;
        clickCatcher.style.left = 0; clickCatcher.style.top = 0;
        clickCatcher.style.right = 0; clickCatcher.style.bottom = 0;
        clickCatcher.style.backgroundColor = new StyleColor(Color.clear);
        clickCatcher.RegisterCallback<ClickEvent>(e =>
        {
            e.StopPropagation();
            CloseDoneDateFilterPopup();
        });

        var panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.left = x;
        panel.style.top = y;
        panel.style.width = PopupWidth;
        panel.style.maxHeight = PopupMaxHeight;
        panel.style.backgroundColor = new StyleColor(new Color(16f / 255f, 22f / 255f, 30f / 255f, 0.98f));
        panel.style.borderTopWidth = panel.style.borderBottomWidth =
            panel.style.borderLeftWidth = panel.style.borderRightWidth = 2;
        panel.style.borderTopColor = panel.style.borderBottomColor =
            panel.style.borderLeftColor = panel.style.borderRightColor = new StyleColor(ColBorder);
        panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius =
            panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 8;
        panel.style.paddingTop = 8; panel.style.paddingBottom = 8;
        panel.style.paddingLeft = 8; panel.style.paddingRight = 8;
        panel.RegisterCallback<ClickEvent>(e => e.StopPropagation());
        clickCatcher.Add(panel);

        var searchField = new TextField { value = _doneDateFilterSearch };
        ApplyFont(searchField, size: 12);
        searchField.style.width = StyleKeyword.Auto;
        searchField.style.marginBottom = 6;
        searchField.style.color = new StyleColor(ColTitleText);
        searchField.style.backgroundColor = new StyleColor(new Color(0x1A / 255f, 0x24 / 255f, 0x32 / 255f, 1f));
        searchField.style.borderBottomWidth = 1;
        searchField.style.borderBottomColor = new StyleColor(ColBorder);
        searchField.style.borderTopWidth = searchField.style.borderLeftWidth = searchField.style.borderRightWidth = 0;
        searchField.style.borderTopLeftRadius = searchField.style.borderTopRightRadius =
            searchField.style.borderBottomLeftRadius = searchField.style.borderBottomRightRadius = 4;
        searchField.RegisterValueChangedCallback(evt =>
        {
            _doneDateFilterSearch = evt.newValue ?? "";
            RefreshDoneDateFilterList(panel, allBuckets);
        });
        panel.Add(searchField);

        var selectAllToggle = new Toggle { label = "Select All", value = true };
        selectAllToggle.name = "select-all-toggle";
        ApplyFont(selectAllToggle, size: 12);
        selectAllToggle.style.color = new StyleColor(ColTitleText);
        selectAllToggle.style.marginBottom = 4;
        selectAllToggle.RegisterValueChangedCallback(evt =>
        {
            var visible = FilterDoneBucketsBySearch(allBuckets, _doneDateFilterSearch);
            if (evt.newValue) foreach (var v in visible) _doneDateFilterSelection.Add(v);
            else foreach (var v in visible) _doneDateFilterSelection.Remove(v);
            _doneDateFilterActive = _doneDateFilterSelection.Count < allBuckets.Count;
            RefreshDoneDateFilterList(panel, allBuckets);
            Rebuild();
        });
        panel.Add(selectAllToggle);

        var itemScroll = new ScrollView
        {
            verticalScrollerVisibility = ScrollerVisibility.Auto,
            horizontalScrollerVisibility = ScrollerVisibility.Hidden
        };
        itemScroll.style.maxHeight = ListMaxHeight;
        itemScroll.style.marginBottom = 6;
        itemScroll.name = "filter-list";
        panel.Add(itemScroll);

        PopulateDoneDateFilterList(itemScroll, allBuckets);

        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new StyleColor(new Color(ColBorder.r, ColBorder.g, ColBorder.b, 0.4f));
        sep.style.marginTop = 4; sep.style.marginBottom = 4;
        panel.Add(sep);

        var bottomRow = new VisualElement();
        bottomRow.style.flexDirection = FlexDirection.Row;
        bottomRow.style.alignItems = Align.Center;

        var clearBtn = new Button(() =>
        {
            _doneDateFilterActive = false;
            _doneDateFilterSelection.Clear();
            foreach (var v in allBuckets) _doneDateFilterSelection.Add(v);
            RefreshDoneDateFilterList(panel, allBuckets);
            Rebuild();
        }) { text = "Clear Filter" };
        ApplyFont(clearBtn, size: 11);
        clearBtn.style.backgroundColor = new StyleColor(Color.clear);
        clearBtn.style.color = new StyleColor(ColTitleText);
        clearBtn.style.borderTopWidth = clearBtn.style.borderBottomWidth =
            clearBtn.style.borderLeftWidth = clearBtn.style.borderRightWidth = 0;
        clearBtn.style.paddingLeft = 8; clearBtn.style.paddingRight = 8;
        clearBtn.style.paddingTop = 4; clearBtn.style.paddingBottom = 4;
        clearBtn.RegisterCallback<PointerEnterEvent>(_ => clearBtn.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f)));
        clearBtn.RegisterCallback<PointerLeaveEvent>(_ => clearBtn.style.backgroundColor = new StyleColor(Color.clear));
        bottomRow.Add(clearBtn);

        var countLabel = new Label();
        ApplyFont(countLabel, size: 11);
        countLabel.style.color = new StyleColor(ColSubtleText);
        countLabel.style.marginLeft = 8;
        countLabel.style.flexGrow = 1;
        countLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        countLabel.name = "filter-count";
        bottomRow.Add(countLabel);
        panel.Add(bottomRow);

        UpdateDoneDateFilterCount(panel, allBuckets);

        return clickCatcher;
    }

    private static List<string> FilterDoneBucketsBySearch(List<string> allBuckets, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return allBuckets;
        return allBuckets.Where(v => v.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
    }

    private void RefreshDoneDateFilterList(VisualElement popupPanel, List<string> allBuckets)
    {
        var scroll = popupPanel.Q<ScrollView>("filter-list");
        if (scroll != null)
        {
            scroll.Clear();
            PopulateDoneDateFilterList(scroll, allBuckets);
        }

        var selectAll = popupPanel.Q<Toggle>("select-all-toggle");
        if (selectAll != null)
        {
            var visible = FilterDoneBucketsBySearch(allBuckets, _doneDateFilterSearch);
            bool allVisibleSelected = visible.Count > 0 && visible.All(v => _doneDateFilterSelection.Contains(v));
            selectAll.SetValueWithoutNotify(allVisibleSelected);
        }

        UpdateDoneDateFilterCount(popupPanel, allBuckets);
    }

    private void PopulateDoneDateFilterList(ScrollView scroll, List<string> allBuckets)
    {
        var visible = FilterDoneBucketsBySearch(allBuckets, _doneDateFilterSearch);

        if (visible.Count == 0)
        {
            var empty = new Label("No dates match search.");
            ApplyFont(empty, size: 11);
            empty.style.color = new StyleColor(ColSubtleText);
            empty.style.paddingLeft = 4;
            empty.style.paddingTop = 4;
            scroll.Add(empty);
            return;
        }

        foreach (var bucket in visible)
        {
            var toggle = new Toggle { label = bucket, value = _doneDateFilterSelection.Contains(bucket) };
            ApplyFont(toggle, size: 12);
            toggle.style.color = new StyleColor(ColTitleText);
            toggle.style.paddingLeft = 4;
            toggle.style.marginBottom = 2;
            toggle.style.whiteSpace = WhiteSpace.NoWrap;

            string captured = bucket;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) _doneDateFilterSelection.Add(captured);
                else _doneDateFilterSelection.Remove(captured);
                _doneDateFilterActive = _doneDateFilterSelection.Count < allBuckets.Count;

                var selectAll = scroll.parent?.Q<Toggle>("select-all-toggle");
                if (selectAll != null)
                {
                    var visItems = FilterDoneBucketsBySearch(allBuckets, _doneDateFilterSearch);
                    bool allVis = visItems.Count > 0 && visItems.All(v => _doneDateFilterSelection.Contains(v));
                    selectAll.SetValueWithoutNotify(allVis);
                }

                UpdateDoneDateFilterCount(scroll.parent, allBuckets);
                Rebuild();
            });

            scroll.Add(toggle);
        }
    }

    private void UpdateDoneDateFilterCount(VisualElement popupPanel, List<string> allBuckets)
    {
        var label = popupPanel?.Q<Label>("filter-count");
        if (label == null) return;

        int selected = _doneDateFilterSelection.Count;
        int total = allBuckets.Count;
        label.text = _doneDateFilterActive ? $"{selected} of {total} selected" : $"{total} dates";
    }

    /// <summary>Columns whose cells are right-aligned so their digits stack. Heading and cell both read
    /// this, so the two can't drift apart.</summary>
    private static bool IsNumericDone(DoneColumn column) =>
        column == DoneColumn.Revenue || column == DoneColumn.Profit ||
        column == DoneColumn.Pallets || column == DoneColumn.Cases;

    /// <summary>
    /// One closed deal, as a table row rather than one of this panel's cards.
    ///
    /// Deliberately compressed — 3px of padding against a card's 10, no icon, no rounded plate — because
    /// this tab is a ledger you scan down rather than a board you pick from. Fitting several times as
    /// many deals in the same window is the whole point of the view. The left accent stripe is kept, so
    /// a short-shipped deal is still findable at a glance without reading the Fill Rate column.
    /// </summary>
    private VisualElement BuildCompletedRow(OrderData order, int rowIndex)
    {
        bool shippedShort = order.TotalUnits > 0 && order.TotalUnitsPicked < order.TotalUnits;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 3; row.style.paddingBottom = 3;
        row.style.paddingLeft = DoneRowIndent; row.style.paddingRight = 8;
        row.style.marginBottom = 1;
        row.style.flexShrink = 0;
        row.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColCardEven : ColCardOdd);
        row.style.borderLeftWidth = 3;
        row.style.borderLeftColor = new StyleColor(shippedShort ? ColWholesale : ColMoney);

        var date = AddDoneCell(row, ClosedDateText(order), DoneDateWidth,
                               HasClosedStamp(order) ? ColTitleText : ColSubtleText, DoneColumn.Date);
        if (!HasClosedStamp(order))
            date.tooltip = "Ship date wasn't recorded for this deal — showing the day the order " +
                           "arrived instead. Deals closed out from now on carry their real pickup time.";
        var orderNum = AddDoneCell(row, ShortOrderId(order.OrderId), DoneOrderWidth, ColSubtleText, DoneColumn.Order);
        orderNum.tooltip = order.OrderId;
        AddDoneCell(row, order.CustomerName, DoneAccountWidth, ColTitleText, DoneColumn.Account, bold: true);
        AddDoneCell(row, OrderTypeLabel(order), DoneTypeWidth, OrderTypeColor(order), DoneColumn.Type);
        AddDoneCell(row, Money(order.ShippedRevenue), DoneRevenueWidth, ColMoney, DoneColumn.Revenue, bold: true);
        AddDoneCell(row, Money(order.ShippedProfit), DoneProfitWidth,
                    order.ShippedProfit >= 0 ? ColMoney : ColDangerSoft, DoneColumn.Profit, bold: true);
        // Recorded counts read firmer than modelled ones — that's now the only visual difference
        // between the two, since neither carries a prefix.
        var pallets = AddDoneCell(row, PalletCountText(order), DonePalletsWidth,
                                  HasPalletCount(order) ? ColTitleText : ColSubtleText, DoneColumn.Pallets);
        if (!HasPalletCount(order))
            pallets.tooltip = "Pallet-equivalents: cases picked ÷ (Ti × Hi). Under 1 means the deal " +
                              "didn't fill a whole pallet. Estimated because this deal shipped before " +
                              "pallet counts were recorded — deals closed out from now on carry the " +
                              "loader's real count.";
        AddDoneCell(row, order.TotalUnitsPicked.ToString("N0"), DoneCasesWidth, ColTitleText, DoneColumn.Cases);
        AddDoneCell(row, FillText(order), DoneFillWidth,
                    shippedShort ? ColWholesale : ColMoney, DoneColumn.FillRate, bold: true);
        return row;
    }

    private Label AddDoneCell(VisualElement row, string text, float width, Color color,
                              DoneColumn column, bool bold = false)
    {
        var label = new Label(string.IsNullOrEmpty(text) ? "—" : text);
        ApplyFont(label, bold, DoneFontSize);
        label.style.color = new StyleColor(color);
        label.style.width = width;
        label.style.minWidth = width;
        label.style.flexShrink = 0;
        // Zeroed explicitly. The theme's default Label margin is a few px, and the header cells are
        // Buttons that zero their own — so leaving these alone made every column sit a little wider
        // than its heading and the two drifted apart cumulatively, 43px out by the last column.
        label.style.marginLeft = 0;
        label.style.marginRight = 0;
        label.style.marginTop = 0;
        label.style.marginBottom = 0;
        // NoWrap, unlike MakeText's rows — a wrapped account name would make one row taller than the
        // rest and break the ledger's alignment.
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.overflow = Overflow.Hidden;
        label.style.unityTextAlign = IsNumericDone(column) ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        if (IsNumericDone(column)) label.style.paddingRight = 10;
        row.Add(label);
        return label;
    }

    /// <summary>True if this deal carries a real recorded pickup moment. False for every order shipped
    /// before OrderData.ClosedDayNumber existed — which is the whole archive of any save made before
    /// this tab was built.</summary>
    private static bool HasClosedStamp(OrderData order) => order != null && order.ClosedDayNumber > 0;

    /// <summary>
    /// "Day 14 · 15:20" — the in-game moment the trailer was closed out and picked up. There's no
    /// calendar in this game, so the day number IS the date, shown the way the top bar shows it.
    ///
    /// A deal with no recorded pickup falls back to "~Day 12", the day the ORDER ARRIVED, which is
    /// persisted on every order ever written. The tilde and the dimmer colour are load-bearing: it is
    /// not the ship date and must never be read as one — it's only ever earlier, sometimes by days.
    /// Shown anyway because a whole archive of "—" tells the player nothing at all, and an order's
    /// arrival day still puts it in roughly the right place in a list sorted newest-first.
    /// </summary>
    private static string ClosedDateText(OrderData order)
    {
        if (order == null) return "—";
        if (!HasClosedStamp(order))
            return order.CreatedDayNumber > 0 ? $"~Day {order.CreatedDayNumber}" : "—";
        if (order.ClosedMinuteOfDay < 0) return $"Day {order.ClosedDayNumber}";
        return $"Day {order.ClosedDayNumber} · {order.ClosedMinuteOfDay / 60:00}:{order.ClosedMinuteOfDay % 60:00}";
    }

    /// <summary>How the deal reached the dock. Bulk is a real flag on the order (also covers the
    /// retired wholesale concept, folded into it); anything else came from a contract, unless it has
    /// no contract at all — which only a Dev Console order does.</summary>
    private static string OrderTypeLabel(OrderData order)
    {
        if (order == null) return "—";
        if (order.IsBulk) return "Bulk";
        return string.IsNullOrEmpty(order.ContractId) ? "Manual" : "Contract";
    }

    /// <summary>Type colours reuse the families the rest of the panel already assigns these:
    /// teal for bulk, blue for a standing contract.</summary>
    private static Color OrderTypeColor(OrderData order) => OrderTypeLabel(order) switch
    {
        "Bulk" => ColChipBulkTx,
        "Contract" => ColChipOutText,
        _ => ColSubtleText,
    };

    /// <summary>
    /// Pallets on the deal — the real count where we have it, an estimate where we don't.
    ///
    /// The real one is counted by the loader as it puts each pallet aboard (OrderData.PalletsShipped).
    /// Deals shipped before that was recorded have nothing stored, so rather than a column of dashes
    /// they get "~2", reconstructed from what IS persisted: the line items' picked quantities and each
    /// SKU's Ti/Hi. Tilde-marked, exactly like the approximate ship dates, because it is a model of
    /// what the selector would have built rather than a record of what it did.
    /// </summary>
    private string PalletCountText(OrderData order)
    {
        if (order == null) return "—";
        if (order.PalletsShipped > 0) return order.PalletsShipped.ToString();
        float est = EstimatePalletFraction(order);
        // "0.##" so a whole pallet reads "2" rather than "2.00", while a part load keeps its precision.
        //
        // NO "~" prefix, unlike the approximate ship dates. A tilde in front of a bare decimal reads as
        // a MINUS SIGN at this size — "~0.65" was being read as "-0.65", i.e. as a bug. "~Day 28" is
        // safe because a word follows it; a number is not. Estimated rows are marked by the dimmer
        // colour and the tooltip instead, which can't be misread as arithmetic.
        return est > 0f ? est.ToString("0.##") : "—";
    }

    /// <summary>True when the row's pallet figure is a real recorded count rather than an estimate.</summary>
    private static bool HasPalletCount(OrderData order) => order != null && order.PalletsShipped > 0;

    /// <summary>The pallet figure this deal actually reports — the loader's recorded whole-pallet
    /// count where we have it, the fractional estimate otherwise. What the column sorts on and what
    /// the footer total sums. Clamped at zero so no row can ever pull the total downward.</summary>
    private float EffectivePallets(OrderData order)
        => HasPalletCount(order)
         ? order.PalletsShipped
         : Mathf.Max(0f, EstimatePalletFraction(order));

    /// <summary>
    /// How much pallet this order's picked cases actually amount to: cases / (Ti x Hi), summed across
    /// the lines.
    ///
    /// Reported as a FRACTION, not rounded up to a whole pallet. 26 cases of a 40-per-pallet SKU is
    /// 0.65 of a pallet, and saying "1" hides exactly the thing the number is for — how much of a
    /// pallet the deal was really worth. A part load and a full one are different deals.
    ///
    /// Uses the SAME arithmetic OutboundPalletBuilder accumulates as it builds (one case occupies
    /// 1/(Ti x Hi) of a reference pallet), so this is the selector's own cubing model rather than a
    /// second, quietly different idea of how big a pallet is.
    ///
    /// Never negative: quantities shouldn't go below zero, but a bad line item shouldn't be able to
    /// subtract freight off the rest of the order either. Returns 0 — rendered "—" — if any line's SKU
    /// can't be resolved or carries no Ti/Hi, rather than silently reporting a figure that's missing a
    /// line's worth of goods.
    /// </summary>
    private float EstimatePalletFraction(OrderData order)
    {
        if (order?.LineItems == null || order.LineItems.Count == 0) return 0f;

        float pallets = 0f;
        foreach (var li in order.LineItems)
        {
            if (li == null || li.QuantityPicked <= 0) continue;
            int perPallet = CasesPerPallet(li.SkuId);
            if (perPallet <= 0) return 0f;
            pallets += li.QuantityPicked / (float)perPallet;
        }
        return Mathf.Max(0f, pallets);
    }

    /// <summary>
    /// Ti x Hi for a SKU, or 0 if it can't be resolved.
    ///
    /// Cached because this is asked once per line item per row and the archive holds up to 250 deals —
    /// AllSkus is an IEnumerable, so resolving each one by scanning it turns a tab switch into tens of
    /// thousands of string compares. Filled on first use rather than at construction: the panel is
    /// built at startup and InventoryService may not have loaded its catalog yet.
    ///
    /// An INSTANCE field, not a static one. The panel is rebuilt per play session, so the cache dies
    /// with it — a static would survive domain reloads and hand the next session a catalog it never
    /// loaded.
    /// </summary>
    private readonly Dictionary<string, int> _casesPerPallet = new();

    private int CasesPerPallet(string skuId)
    {
        if (string.IsNullOrEmpty(skuId)) return 0;

        if (_casesPerPallet.Count == 0)
        {
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv?.AllSkus == null) return 0;
            foreach (var s in inv.AllSkus)
                if (s != null && !string.IsNullOrEmpty(s.SkuId))
                    _casesPerPallet[s.SkuId] = Mathf.Max(0, s.Ti * s.Hi);
        }
        return _casesPerPallet.TryGetValue(skuId, out int n) ? n : 0;
    }

    /// <summary>"85% · 17 / 20" — the percentage first because that's the KPI, the raw counts behind it
    /// so a short is legible as cases rather than just as a number that isn't 100.</summary>
    private static string FillText(OrderData order)
    {
        if (order == null || order.TotalUnits <= 0) return "—";
        return $"{Mathf.RoundToInt(FillRatio(order) * 100f)}%  ·  {order.TotalUnitsPicked} / {order.TotalUnits}";
    }

    private static string Money(int amount)
        => amount < 0 ? $"-${Mathf.Abs(amount):N0}" : $"${amount:N0}";

    /// <summary>First 8 characters of a GUID order id — enough to tell rows apart at a glance and to
    /// cross-reference a console log line (which also truncates ids this way — see WorkQueuePanel's
    /// own ShortId), without spending the width a full GUID would need. The full id is on the cell's
    /// tooltip for whenever the short form isn't enough.</summary>
    private static string ShortOrderId(string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return "—";
        return orderId.Length > 8 ? orderId.Substring(0, 8) : orderId;
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
