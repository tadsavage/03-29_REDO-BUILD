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
///   NEW CONTRACTS  contracts on the table, split into two side-by-side panels so the player is
///                  choosing WITHIN a type, not across both: BULK ORDERS (a one-off full-pallet
///                  drop at cost + 5%, 1–3 rolled fresh daily, the rest expiring) and RECURRING
///                  ORDERS (a daily or weekly account).
///   BULK ORDERS    the bulk orders already accepted: how each breaks into pallets versus loose
///                  cases, and whether it has a door booked yet.
///   RECURRING ORDERS (Tab.Accounts)  what you're actually running, and how well. Per-contract
///                  delivered/late/earned, plus the only place a recurring account can be
///                  cancelled.
///   SCHEDULE       the dock appointment book — two-hour blocks, one row per block, as many slots
///                  per block as you have outbound doors.
///
/// RECURRING FREIGHT AUTO-BOOKS A DOOR; BULK FREIGHT WAITS FOR YOU TO PLACE IT. A recurring order
/// lands into the first open block before its due day the moment it arrives (DockScheduleService.
/// TryAutoPlace) — the Schedule tab is where you override that plan, not where you're required to
/// build it from scratch. A bulk order never auto-books: signing one is the player's own in-the-moment
/// choice, so it shows up as a box in the stranded strip's pool instead, same as recurring freight that
/// genuinely found no room. Either way, freight that's still unscheduled when its due day passes costs
/// the account for 30 days (OrderArrivalService.SweepMissedPickups).
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
    private static readonly Color ColBg          = new Color(18f / 255f, 26f / 255f, 36f / 255f, 0.97f);
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
    // Raised from 680 so the Schedule tab's grid — a ScrollView that just fills whatever's left after
    // the fixed-height header strip — shows more block rows without scrolling. minH on the
    // ResizableWindow below is tied to this same constant, so the player still can't drag it shorter
    // than the new default, same as before.
    private const float ModalHeight = 800f;
    /// <summary>Narrowest the window can be dragged. Below this the tab bar itself starts wrapping.</summary>
    private const float ModalMinWidth = 820f;
    /// <summary>Width of an offer card's right-hand action column. The commit button and the SHIP BY
    /// badge are both stretched to it, which is what makes them exactly the same width without either
    /// carrying a hard-coded number of its own.</summary>
    private const float OfferActionColWidth = 210f;

    /// <summary>Height StyleOrangeButton gives every orange button. Named so the offer card's
    /// deliberately taller commit button can be expressed as a multiple of it rather than as a magic
    /// number that silently stops relating to the others if the base ever changes.</summary>
    private const float OrangeButtonHeight = 30f;

    private const float IconSize    = 108f;   // Offers cards -- 72 * 1.5 per Tad's explicit call

    /// <summary>Offer-card icons are drawn wider than they are tall. The customer sprites are square
    /// on disk, so stretching them to fill a box this much wider than it is tall is what keeps them
    /// from reading as vertically squeezed/stretched — 1.1 wasn't enough of a correction. Width only —
    /// the height stays on IconSize so every card on the board keeps the same baseline as the text
    /// beside it.</summary>
    private const float OfferIconWidthScale = 1.4f;
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

    /// <summary>How far either side of today the timeline will page. Backwards is 0 —
    /// DockScheduleService purges a day's appointments the moment it stops being "today", so
    /// there's nothing left to page back to. Forwards is just far enough to see the
    /// consequences of a lead time.</summary>
    private const int ScheduleDaysBack = 0;
    private const int ScheduleDaysAhead = 7;

    /// <summary>
    /// NewContracts was "Offers" until 2026-08-01, then "Customers", and is now what it always meant:
    /// the board of contracts on the table. Two types share it — Bulk Orders (a one-off full-pallet
    /// drop priced off cost of goods, 1–3 rolled fresh every day) and Recurring Orders (a daily or
    /// weekly account, formerly labelled "Standing Order") — told apart by colour and a type pill AND
    /// (as of 2026-08-15) by two side-by-side panels, one per type, because the decision the player is
    /// making is "which of these do I want" WITHIN a type, not across both.
    ///
    /// BulkOrders is the opposite: not offers but the live bulk orders already accepted, and how far
    /// through the warehouse each one is.
    /// </summary>
    ///
    /// Completed is the ledger: every order that has actually been closed out and paid for, newest
    /// first. Distinct from the "COMPLETED DEALS" heading on Accounts (displayed as "Recurring
    /// Orders" — the enum name is unchanged), which lists finished wholesale CONTRACTS — this one is
    /// per ORDER, and it's the only place the money a shipment made (and what it cost to make it) is
    /// reported per deal.
    private enum Tab { NewContracts, BulkOrders, Accounts }

    private readonly VisualElement _overlay;
    private readonly VisualElement _modal;
    private readonly VisualElement _tabBar;
    /// <summary>Stationary strip between the tab bar and the scroll view. See Build.</summary>
    private readonly VisualElement _tabHeader;
    private readonly ScrollView _content;
    private readonly Label _footerMessage;

    /// <summary>
    /// Accounts-tab two-pane layout: a scrolling list of account cards on the left and a stationary
    /// "ORDER DETAILS" pane on the right that fills in for whichever card was last clicked. Sits
    /// alongside _content rather than inside it — nesting a second ScrollView inside _content fights
    /// Yoga's auto-height sizing, so this is a sibling that's shown/hidden in Rebuild() instead.
    /// </summary>
    private VisualElement _splitPane;
    private ScrollView _orderListScroll;
    private ScrollView _orderDetailsBody;

    /// <summary>ContractId of the account card selected on the Accounts tab, driving _orderDetailsBody.
    /// The bulk order whose contents the ORDER DETAILS pane is showing. Separate from the account
    /// selection because the two tabs select different things — an ACCOUNT on Recurring, a single
    /// ORDER on Bulk — and sharing one field made switching tabs show the wrong pane's contents.
    private string _selectedBulkOrderId;

    /// Null until the player clicks one; BuildAccounts defaults it to the first running account so the
    /// details pane is never empty while accounts exist.</summary>
    private string _selectedAccountContractId;

    /// <summary>The big heading in the title bar. Retitled per tab (see TitleFor) — a ledger of
    /// finished orders sitting under the words "OUTBOUND ORDER MANAGER" names the wrong thing.</summary>
    private Label _titleLabel;
    private Button _scaleBtn;
    private bool _visible;
    private ResizableWindow _resizeWindow;

    private Tab _tab = Tab.NewContracts;

    // Drag state. The modal is absolutely positioned so left/top can be written directly; the offset
    // is captured at pointer-down so the window doesn't jump to centre itself under the cursor.
    private bool _dragging;
    private Vector2 _dragOffset;
    private bool _placed; // false until the first Show centres it

    private static Font _nunito;

    public ContractsPanel(VisualElement root)
    {
        _overlay = Build(out _modal, out _tabBar, out _tabHeader, out _content, out _footerMessage);
        root.Add(_overlay);
        Hide();
    }

    public bool IsOpen => _visible;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        OrdersPauseGate.Push(this);
        _overlay.style.display = DisplayStyle.Flex;
        // Order screens sit above both bars. Raising on every Show — not once at build time — because
        // sibling order is decided by whoever raised LAST, so a panel opened after this one would
        // otherwise end up in front. KeepOnTop puts an on-screen toast back above us straight after.
        _overlay.BringToFront();
        UIToast.KeepOnTop();
        Rebuild();
        CentreOnce();

        // Always opens filled rather than normal size — same reasoning as PurchasingPanel.Show: the
        // Schedule grid alone is one column per door, and the Accounts/Bulk two-pane split wants all
        // the width it can get. Deferred one frame so FillScreen has a real layout to measure on the
        // very first Show() of a session — see ResizableWindow.FillScreen's own doc comment.
        _overlay.schedule.Execute(() =>
        {
            _resizeWindow?.FillScreenExact();
            if (_resizeWindow != null) _resizeWindow.UpdateScaleButtonIcon(_scaleBtn, 48f, ColTitleText);
        }).ExecuteLater(16);
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
        OrdersPauseGate.Pop(this);
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
        // FULL screen, deliberately including the bottom HUD strip. The order screens outrank both
        // bars (Tad's call) — the window may be dragged over the bar and the Build/Play tabs and will
        // draw on top of them. Other full-screen panels still stop at BuildMenuUI.
        // BottomHudReservedHeight; this is the documented exception, not a change of house rule.
        //
        // Covering the bar costs nothing in clickability: this overlay is PickingMode.Ignore with no
        // fill (see below), so only the modal's own rectangle is a hit target. The bar stays fully
        // usable everywhere the window isn't actually sitting on it.
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0;
        overlay.style.bottom = 0;

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
        // flexShrink 0 or the height above is a suggestion, not a rule. The modal is a fixed-height
        // column, and this was its ONLY shrinkable child with a fixed height — every other row is
        // flexShrink 0 or is the content that grows. So on a tab whose content overflows (Recurring
        // Orders with a long ORDER DETAILS list is the easy repro) flex took the entire shortfall out
        // of this row and crushed it 52 -> ~10px. The 48px buttons inside are flexShrink 0, so they
        // did NOT shrink with it — they overflowed the collapsed row and spilled ~19px out through
        // the top of the panel, which is what read as "the buttons are sitting too high".
        titleBar.style.flexShrink = 0;

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

        // Return trip for PurchasingPanel.OpenScheduler, which closes itself to get here. Without it
        // the only way back is to remember that purchasing lives on key 9 — a one-way hyperlink.
        // Sits left of the window buttons so the destructive ✕ keeps the far corner it always has.
        var backToPurchasing = new Button(OpenPurchasing) { text = "PURCHASING" };
        ApplyFont(backToPurchasing, bold: true, size: 16);
        backToPurchasing.style.height = titleBtnSize;
        backToPurchasing.style.paddingLeft = backToPurchasing.style.paddingRight = 18;
        backToPurchasing.style.marginRight = 10;
        backToPurchasing.style.flexShrink = 0;   // the title flexGrows; without this the label squeezes
        backToPurchasing.style.color = new StyleColor(ColOrangeText);
        backToPurchasing.style.backgroundColor = new StyleColor(ColOrange);
        backToPurchasing.style.borderTopWidth = backToPurchasing.style.borderBottomWidth =
            backToPurchasing.style.borderLeftWidth = backToPurchasing.style.borderRightWidth = 2;
        backToPurchasing.style.borderTopColor = backToPurchasing.style.borderBottomColor =
            backToPurchasing.style.borderLeftColor = backToPurchasing.style.borderRightColor = new StyleColor(ColOrangeEdge);
        backToPurchasing.style.borderTopLeftRadius = backToPurchasing.style.borderTopRightRadius =
            backToPurchasing.style.borderBottomLeftRadius = backToPurchasing.style.borderBottomRightRadius = 8;
        backToPurchasing.RegisterCallback<PointerEnterEvent>(_ =>
            backToPurchasing.style.backgroundColor = new StyleColor(ColOrangeHover));
        backToPurchasing.RegisterCallback<PointerLeaveEvent>(_ =>
            backToPurchasing.style.backgroundColor = new StyleColor(ColOrange));
        titleBar.Add(backToPurchasing);

        // Same trip, sideways instead of back — straight to the dock appointment grid (key 0's
        // standalone Scheduler), so a player looking at a contract doesn't have to remember the hotkey
        // to go check when a door's actually free. Same style/behaviour as PURCHASING, just its own
        // destination, per Tad's ask for a duplicate button next to it.
        var openScheduler = new Button(OpenScheduler) { text = "SCHEDULER" };
        ApplyFont(openScheduler, bold: true, size: 16);
        openScheduler.style.height = titleBtnSize;
        openScheduler.style.paddingLeft = openScheduler.style.paddingRight = 18;
        openScheduler.style.marginRight = 10;
        openScheduler.style.flexShrink = 0;
        openScheduler.style.color = new StyleColor(ColOrangeText);
        openScheduler.style.backgroundColor = new StyleColor(ColOrange);
        openScheduler.style.borderTopWidth = openScheduler.style.borderBottomWidth =
            openScheduler.style.borderLeftWidth = openScheduler.style.borderRightWidth = 2;
        openScheduler.style.borderTopColor = openScheduler.style.borderBottomColor =
            openScheduler.style.borderLeftColor = openScheduler.style.borderRightColor = new StyleColor(ColOrangeEdge);
        openScheduler.style.borderTopLeftRadius = openScheduler.style.borderTopRightRadius =
            openScheduler.style.borderBottomLeftRadius = openScheduler.style.borderBottomRightRadius = 8;
        openScheduler.RegisterCallback<PointerEnterEvent>(_ =>
            openScheduler.style.backgroundColor = new StyleColor(ColOrangeHover));
        openScheduler.RegisterCallback<PointerLeaveEvent>(_ =>
            openScheduler.style.backgroundColor = new StyleColor(ColOrange));
        titleBar.Add(openScheduler);

        _scaleBtn = new Button { text = string.Empty };
        RuntimeTooltip.Attach(_scaleBtn, "Resize window (normal / large / fill screen)");
        StyleSquareButton(_scaleBtn);
        _scaleBtn.style.width = titleBtnSize;
        _scaleBtn.style.height = titleBtnSize;
        _scaleBtn.style.marginRight = 6;
        ResizableWindow.AddStackedSquaresGlyph(_scaleBtn, titleBtnSize, ColTitleText, isFilled: false);
        _scaleBtn.RegisterCallback<PointerEnterEvent>(_ =>
            _scaleBtn.style.backgroundColor = new StyleColor(new Color(0.35f, 0.55f, 0.95f, 0.35f)));
        _scaleBtn.RegisterCallback<PointerLeaveEvent>(_ =>
            _scaleBtn.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f)));
        titleBar.Add(_scaleBtn);

        // Routed through CloseAll(), not a bare Hide() — this panel is registered on key 8, and only
        // UIKeyBindingManager.ToggleUI/CloseAll ever reset _currentOpenKey back to -1. A direct Hide()
        // left it stuck, and PlacementStateMachine.HandleIdleHover gates the world hover popup on
        // CurrentOpenKey == -1 — so clicking this ✕ silently killed every world tooltip afterward even
        // though the panel had visibly closed. CloseAll() calls Hide() on every open registered panel
        // (this one included) and THEN clears CurrentOpenKey, so it's a safe superset of the old call.
        var close = new Button(() => { UIKeyBindingManager.Instance?.CloseAll(); AudioManager.Play("UIClose"); }) { text = "✕" };
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

        // Accounts tab's two-pane layout — a sibling of `content`, shown instead of it (see Rebuild).
        var splitPane = new VisualElement();
        splitPane.style.flexGrow = 1;
        splitPane.style.flexDirection = FlexDirection.Row;
        splitPane.style.display = DisplayStyle.None;
        modal.Add(splitPane);

        var orderListScroll = new ScrollView(ScrollViewMode.Vertical);
        orderListScroll.style.flexGrow = 1;
        orderListScroll.style.flexBasis = 0;
        orderListScroll.style.marginRight = 12;
        splitPane.Add(orderListScroll);

        var orderDetailsBody = new ScrollView(ScrollViewMode.Vertical);
        orderDetailsBody.style.flexGrow = 1;
        orderDetailsBody.style.flexBasis = 0;
        orderDetailsBody.style.backgroundColor = new StyleColor(ColStat);
        orderDetailsBody.style.paddingTop = 14; orderDetailsBody.style.paddingBottom = 14;
        orderDetailsBody.style.paddingLeft = 16; orderDetailsBody.style.paddingRight = 16;
        orderDetailsBody.style.borderTopLeftRadius = orderDetailsBody.style.borderTopRightRadius =
            orderDetailsBody.style.borderBottomLeftRadius = orderDetailsBody.style.borderBottomRightRadius = 10;
        orderDetailsBody.style.borderTopWidth = orderDetailsBody.style.borderBottomWidth =
            orderDetailsBody.style.borderLeftWidth = orderDetailsBody.style.borderRightWidth = 2;
        orderDetailsBody.style.borderTopColor = orderDetailsBody.style.borderBottomColor =
            orderDetailsBody.style.borderLeftColor = orderDetailsBody.style.borderRightColor = new StyleColor(ColBorder);
        splitPane.Add(orderDetailsBody);

        _splitPane = splitPane;
        _orderListScroll = orderListScroll;
        _orderDetailsBody = orderDetailsBody;

        // Polled rather than driven by horizontalScroller.valueChanged: that event does NOT fire when
        // scrollOffset is set programmatically, so the frozen column silently desynced from any scroll
        // the panel itself performed. Reading scrollOffset every frame catches the wheel, a scrollbar
        // drag, and a programmatic jump identically. Guarded on change, so the usual cost is one float
        // comparison. Scheduled ONCE here — doing it per Rebuild would stack a poller per refresh.
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
        _scaleBtn.clicked += () =>
        {
            _resizeWindow.CycleScale();
            _resizeWindow.UpdateScaleButtonIcon(_scaleBtn, titleBtnSize, ColTitleText);
            AudioManager.Play(_resizeWindow.IsFilled ? "UIMax" : "UIMin");
        };

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
        _clearContractsArmed = false;  // nor should a half-finished wipe
        Rebuild();
    }

    private void BuildTabBar()
    {
        _tabBar.Clear();
        var arrivals = Arrivals();

        int offerCount = arrivals != null ? arrivals.AvailableOffers.Count() : 0;
        // The Accounts view is a forward work plan now: badge it with every unshipped recurring order,
        // not merely the number of customer contracts. One account can have seven scheduled manifests
        // through the planning horizon, and showing "1" there falsely suggests only one order exists.
        int accountCount = PendingRecurringOrders().Count;

        int bulkCount = LiveBulkOrders().Count;

        // Recurring before Bulk in the tab row, matching the New Contracts board's left-to-right
        // panel order (Recurring left, Bulk right) — the tab row used to run the opposite way.
        _tabBar.Add(MakeTab("New Contracts", offerCount.ToString(), Tab.NewContracts));
        _tabBar.Add(MakeTab("Recurring Orders", accountCount.ToString(), Tab.Accounts));
        _tabBar.Add(MakeTab("Bulk Orders", bulkCount.ToString(), Tab.BulkOrders));
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
        RuntimeTooltip.Attach(customer, "Debug: put one new signable customer offer on this tab. Stands in for " +
                           "reputation-driven arrival until that exists.");
        wrap.Add(customer);

        var truck = new Button(() => ToolsWindowController.Instance.SpawnOutboundTruckDebug()) { text = "OUTBOUND TRUCK" };
        StyleDevButton(truck);
        RuntimeTooltip.Attach(truck, "Debug: send an outbound truck to the first door with staged pallets.");
        wrap.Add(truck);

        var wipe = new Button(OnDevClearContracts) { text = "CLEAR CONTRACTS" };
        StyleDevButton(wipe);
        RuntimeTooltip.Attach(wipe, "Debug: wipe the contract board — drops every signed account and generated " +
                       "offer, and restores the authored offers. Orders already on the floor are NOT " +
                       "touched; clear those from the Work Queue.");
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

    private static string TitleFor(Tab tab) => "OUTBOUND ORDER MANAGER";

    private void Rebuild()
    {
        BuildTabBar();
        if (_titleLabel != null) _titleLabel.text = TitleFor(_tab);
        _tabHeader.Clear();
        _content.Clear();
        _orderListScroll.Clear();
        _orderDetailsBody.Clear();
        _footerMessage.text = string.Empty;

        _content.mode = ScrollViewMode.Vertical;

        // Accounts AND Bulk Orders own the two-pane split (card list + ORDER DETAILS); every other tab
        // keeps the single full-width scroll view. Bulk was left out of the split originally and read
        // as a different screen for the same job — both tabs are "pick a trailer, see what's on it".
        bool splitTab = _tab == Tab.Accounts || _tab == Tab.BulkOrders;
        _content.style.display = splitTab ? DisplayStyle.None : DisplayStyle.Flex;
        _splitPane.style.display = splitTab ? DisplayStyle.Flex : DisplayStyle.None;

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
        }
    }
    // ── Tab 1: New Contracts ─────────────────────────────────────────────────

    private const string RecurringHelperText =
        "Recurring Orders are ongoing contractual agreements to ship orders to customers on a " +
        "predetermined frequency. These will auto-schedule on the Schedule tab at their designated " +
        "time, although these can be modified at any given time up until the order is released.";

    private const string BulkHelperText =
        "Bulk Orders are one-time pickups, usually from customers that don't have a recurring order " +
        "contract. Treating these customers well could turn them into long-standing partners!";

    private void BuildNewContracts(OrderArrivalService arrivals)
    {
        // Intro on the left, dev triggers on the right. The dev cluster lives on THIS tab only — it
        // was in the tab row, which put it on screen while you were reading the Schedule, where it
        // means nothing. Kept short now that each panel below carries its own explanation.
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.FlexStart;
        header.style.marginBottom = 10;

        var intro = new Label("Take a contract to bring work in.  ·  Drag the title bar to move the window.");
        ApplyFont(intro, size: 21); // 14 * 1.5 per Tad's explicit call
        intro.style.color = new StyleColor(Color.white);
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
        // Anything barred after a missed pickup, so a customer who has vanished from the board is
        // explained rather than just gone.
        var lost = arrivals.LostOffers.ToList();
        // Customers who would deal with you if you were better regarded. Shown greyed for the same
        // reason locked vendors are on the Purchasing panel — the ladder has to be visible to be a goal.
        var repLocked = arrivals.ReputationLockedOffers.ToList();

        // Two panels, one per type — the decision the player is making is "which of these do I
        // want" WITHIN a type (compare two Bulk offers, or two Recurring ones), not across both, so
        // a single shuffled list was the wrong grouping once there were enough offers to compare.
        var columns = new VisualElement();
        columns.style.flexDirection = FlexDirection.Row;
        columns.style.alignItems = Align.FlexStart;

        columns.Add(BuildOfferColumn("RECURRING ORDERS", ColBorder, RecurringHelperText,
            offers.Where(c => !c.IsBulk).ToList(), lost.Where(c => !c.IsBulk).ToList(),
            repLocked.Where(c => !c.IsBulk).ToList(), arrivals, inv, marginRight: 12));

        columns.Add(BuildOfferColumn("BULK ORDERS", ColBulkEdge, BulkHelperText,
            offers.Where(c => c.IsBulk).ToList(), lost.Where(c => c.IsBulk).ToList(),
            repLocked.Where(c => c.IsBulk).ToList(), arrivals, inv, marginRight: 0));

        _content.Add(columns);

        _footerMessage.text = $"{offers.Count} contract(s) on the table · " +
                              $"{arrivals.RunningAccounts.Count()} recurring account(s) currently running" +
                              (lost.Count > 0 ? $" · {lost.Count} lost account(s) in cooldown." : ".");
    }

    /// <summary>One hemisphere of the New Contracts board: a bordered panel dedicated to a single
    /// contract type, with a large title, an explanatory paragraph naming what the type actually
    /// commits the player to, and its own card list (offers first, then anything barred by a missed
    /// pickup). BuildOfferCard/BuildLostCard are unchanged and shared by both panels — only the
    /// grouping is new.</summary>
    private VisualElement BuildOfferColumn(string title, Color accent, string helperText,
                                           List<ContractData> offers, List<ContractData> lostOffers,
                                           List<ContractData> repLockedOffers,
                                           OrderArrivalService arrivals, InventoryService inv,
                                           float marginRight)
    {
        var column = new VisualElement();
        column.style.flexGrow = 1;
        column.style.flexBasis = 0;
        column.style.marginRight = marginRight;
        column.style.paddingTop = 12; column.style.paddingBottom = 12;
        column.style.paddingLeft = 12; column.style.paddingRight = 12;
        column.style.backgroundColor = new StyleColor(new Color(ColBg.r, ColBg.g, ColBg.b, 1f)); // opaque per Tad's explicit call
        column.style.borderTopWidth = column.style.borderBottomWidth =
            column.style.borderLeftWidth = column.style.borderRightWidth = 2;
        column.style.borderTopColor = column.style.borderBottomColor =
            column.style.borderLeftColor = column.style.borderRightColor = new StyleColor(accent);
        column.style.borderTopLeftRadius = column.style.borderTopRightRadius =
            column.style.borderBottomLeftRadius = column.style.borderBottomRightRadius = 10;

        var titleLabel = MakeText(title, 36, Color.white, bold: true); // 24 * 1.5 per Tad's explicit call
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.alignSelf = Align.Stretch;
        column.Add(titleLabel);

        var helper = MakeText(helperText, 20, Color.white); // 13 * 1.5 per Tad's explicit call
        helper.style.marginTop = 4;
        helper.style.marginBottom = 10;
        column.Add(helper);

        if (offers.Count == 0 && lostOffers.Count == 0 && repLockedOffers.Count == 0)
        {
            var none = MakeText("Nothing on offer right now — check back after the next contract rolls.",
                                13, ColEmptyText);
            none.style.marginTop = 4;
            column.Add(none);
            return column;
        }

        int row = 0;
        foreach (var contract in offers)
            column.Add(BuildOfferCard(contract, inv, row++));
        foreach (var contract in lostOffers)
            column.Add(BuildLostCard(contract, arrivals, row++));
        foreach (var contract in repLockedOffers)
            column.Add(BuildReputationLockedCard(contract, row++));

        return column;
    }

    /// <summary>Accent colour for a contract type — the same family used for its chips elsewhere, so
    /// a bulk order reads as the same thing on this tab and in the stranded strip.</summary>
    private static Color AccentFor(ContractData c)
        => c.IsBulk ? ColBulkEdge : ColBorder;

    private static Color TypeTextFor(ContractData c)
        => c.IsBulk ? ColChipBulkTx : ColChipOutText;

    /// <summary>Rich stock-check tooltip for a bulk offer's ACCEPT ORDER button — per Tad's ask, "the
    /// regular order detail tooltip" for whether we have the product or not, laid out as a header plus
    /// one icon+text row per line (matching Tad's mockup) rather than a flat block of text. Reads
    /// ContractData.BulkPreviewLines (rolled once at offer creation, the same list GenerateBulk builds
    /// the real order from on Accept) against live on-hand inventory.</summary>
    private VisualElement BuildBulkStockTooltip(ContractData contract, InventoryService inv)
    {
        var body = new VisualElement();
        body.pickingMode = PickingMode.Ignore;

        var lines = contract?.BulkPreviewLines;
        if (lines == null || lines.Count == 0)
        {
            body.Add(MakeText("Product not determined yet.", 13, ColSubtleText));
            return body;
        }

        bool anyShort = false;
        var rows = new List<VisualElement>();
        foreach (var line in lines)
        {
            var sku = inv?.GetSkuData(line.SkuId);
            string name = sku != null ? sku.ItemDescription : line.SkuId;
            int onHand = inv != null ? inv.TotalOnHand(line.SkuId) : 0;
            bool have = onHand >= line.Quantity;
            if (!have) anyShort = true;

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 5;

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = 30; icon.style.height = 30;
            icon.style.flexShrink = 0;
            icon.style.marginRight = 8;
            icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius =
                icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = 4;
            if (sku != null && sku.Icon != null) icon.style.backgroundImage = new StyleBackground(sku.Icon);
            else icon.style.backgroundColor = new StyleColor(ColBlueEdge);
            row.Add(icon);

            var textCol = new VisualElement { pickingMode = PickingMode.Ignore };
            textCol.style.flexGrow = 1;
            textCol.style.flexShrink = 1;

            var nameLabel = MakeText(name, 13, ColTitleText, bold: true);
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            textCol.Add(nameLabel);

            string statusText = have
                ? $"On hand: {onHand:N0} — enough"
                : $"SHORT {line.Quantity - onHand:N0} — need {line.Quantity:N0}, have {onHand:N0}";
            var statusLabel = MakeText(statusText, 12, have ? ColMoney : ColDanger, bold: !have);
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            textCol.Add(statusLabel);

            row.Add(textCol);
            rows.Add(row);
        }

        var header = MakeText(anyShort ? "SOME PRODUCT NOT ON HAND" : "ALL PRODUCT ON HAND",
                              14, anyShort ? ColDanger : ColMoney, bold: true);
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = new StyleColor(ColBorder);
        header.style.paddingBottom = 5;
        body.Add(header);
        foreach (var row in rows) body.Add(row);
        return body;
    }

    private VisualElement BuildOfferCard(ContractData contract, InventoryService inv, int rowIndex)
    {
        var card = MakeRow(rowIndex, AccentFor(contract));
        card.style.position = Position.Relative;
        card.Add(MakeOfferCardIcon(contract.Customer != null ? contract.Customer.Icon : null));

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;

        var titleRow = new VisualElement();
        titleRow.style.flexDirection = FlexDirection.Row;
        titleRow.style.alignItems = Align.Center;
        titleRow.Add(MakeText(contract.Customer != null ? contract.Customer.CompanyName : "(no customer assigned)",
                              19, ColTitleText, bold: true));
        body.Add(titleRow);

        // The outlined type badge sits here, directly under the customer name — it used to float in
        // the card's top-right corner with a plain-text duplicate ("Bulk Order") sitting here instead.
        // Two renderings of one fact in two places, and the corner one overlapped the money figure.
        // One badge, in the reading order the player actually scans: who, then what kind of deal.
        var pill = MakeTypePill(contract);
        pill.style.marginTop = 3;
        pill.style.marginBottom = 1;
        body.Add(pill);

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
        right.style.width = OfferActionColWidth;
        right.style.flexShrink = 0;
        right.style.alignItems = Align.FlexEnd;

        // The commit button leads the column, in the corner the type badge used to occupy. The player
        // reads the offer left-to-right and lands on the action, with the money and the deadline
        // underneath it as the two things that qualify the decision.
        var sign = new Button(() => OnSign(contract)) { text = contract.IsBulk ? "ACCEPT ORDER" : "SIGN CONTRACT" };
        StyleOrangeButton(sign);
        // Bulk offers roll their SKUs once, at offer creation (OrderArrivalService.RollDailyBulkOffers)
        // — this is exactly that same list, so what's previewed here is what GenerateBulk builds on
        // Accept, never a second independently-rolled order. Recurring contracts don't have this
        // (their line items are rolled fresh each arrival day, not up front), so no tooltip for those.
        if (contract.IsBulk)
            RuntimeTooltip.AttachRich(sign, () => BuildBulkStockTooltip(contract, inv));
        // 25% taller than the shared 30px orange button, and stretched to the column rather than
        // sized to its own text. Both matter: the height makes it the obvious target on the card, and
        // the stretch is what lets the deadline badge below match its width WITHOUT measuring
        // resolvedStyle after layout — two elements stretched to the same parent are the same width
        // by construction, and can't drift when the button's label changes length between
        // "ACCEPT ORDER" and "SIGN CONTRACT".
        sign.style.height = OrangeButtonHeight * 1.25f;
        sign.style.alignSelf = Align.Stretch;
        sign.style.marginBottom = 8;
        right.Add(sign);

        right.Add(MakeText(EstimatedValueText(contract, inv), 21, ColMoney, bold: true));
        // The headline figure stays the BASE estimate even for a same-day rush — the double is
        // conditional on beating the deadline, and baking it into the number would advertise money
        // the player only earns if they pull it off. The caption says where the 2x comes from.
        var caption = MakeText(contract.IsBulk
                                   ? (contract.IsSameDayRush ? "est. one-off revenue · 2x if on time"
                                                             : "est. one-off revenue")
                                   : "est. revenue per day", 12, ColSubtleText);
        caption.style.marginBottom = 2;
        right.Add(caption);

        right.Add(BuildDeadlineBadge(contract));

        card.Add(right);
        return card;
    }

    /// <summary>
    /// The SHIP-BY badge under the commit button — the last moment this freight may leave the
    /// building, and what it costs to miss it.
    ///
    /// THE TWO CONTRACT TYPES HAVE GENUINELY DIFFERENT DEADLINES, and this badge is where the player
    /// finds that out before they commit rather than after:
    ///
    ///   BULK      a DAY. The freight may go any time up to the end of that day, and it's on the
    ///             player to find it a dock appointment (Contracts → Schedule) at all.
    ///   RECURRING a two-hour BLOCK, on the day the order lands. A standing account is pre-booked
    ///             out ScheduleHorizonDays in advance (OrderArrivalService.MaintainRecurringSchedule),
    ///             so there is always already an appointment — the deadline is the END of that block,
    ///             not the end of the day. Whatever hasn't shipped when the block elapses is late
    ///             (DockScheduleService.SweepElapsedAppointments).
    ///
    /// Missing either costs the same two things: a late fee at the contract's own advertised rate,
    /// and customer satisfaction. Bulk freight left with no appointment at all past its day is
    /// refused outright — SweepMissedPickups cancels the order and loses the account for
    /// ContractLossCooldownDays.
    ///
    /// Given its own boxed block rather than another clause on the terms line because it's the one
    /// term that decides whether the player can physically service the order at all. A 72-hour drop
    /// and a same-day rush are the same money and completely different decisions.
    ///
    /// A SAME-DAY rush (bulk only — recurring pays no rush premium) is drawn in the warning colour
    /// and states its double-revenue upside outright: the deadline is the whole risk, so the reward
    /// has to sit beside it. Late is late either way — a rush that slips is fined at the ordinary
    /// rate, it just forfeits the double (OrderData.QualifiesForSameDayBonus).
    /// </summary>
    private VisualElement BuildDeadlineBadge(ContractData contract)
    {
        if (!contract.IsBulk) return BuildRecurringDeadlineBadge(contract);

        bool rush = contract.IsSameDayRush;
        Color edge = rush ? ColWholesale : ColBlueEdge;
        Color text = rush ? ColWholesale : ColChipOutText;

        var box = new VisualElement();
        box.style.marginTop = 8;
        box.style.alignSelf = Align.Stretch;
        box.style.alignItems = Align.FlexEnd;
        box.style.paddingLeft = 8; box.style.paddingRight = 8;
        // Tighter than the 4 it was: with the lines themselves pulled together (DeadlineLine), the old
        // padding left the frame looking loose around a compact stack.
        box.style.paddingTop = 3; box.style.paddingBottom = 3;
        box.style.backgroundColor = new StyleColor(new Color(edge.r, edge.g, edge.b, 0.10f));
        box.style.borderTopWidth = box.style.borderBottomWidth =
            box.style.borderLeftWidth = box.style.borderRightWidth = 1;
        box.style.borderTopColor = box.style.borderBottomColor =
            box.style.borderLeftColor = box.style.borderRightColor = new StyleColor(edge);
        box.style.borderTopLeftRadius = box.style.borderTopRightRadius =
            box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 6;

        // "END OF DAY" spelled out, because the rule genuinely is end-of-day: OrderData.IsOverdue
        // is strictly currentDay > DueDay, so the whole of day N is still on time. "SHIP BY: DAY 4"
        // left it open whether day 4 was the last good day or the first late one.
        int dueDay = CurrentDay() + contract.LeadTimeDays;
        box.Add(DeadlineLine($"SHIP BY END OF DAY: {dueDay}", 13, text, bold: true));

        // The "48 HRS to book a door" line is gone — it was LeadTimeDays x 24, i.e. the same deadline
        // as the line above restated in hours, and two numbers for one deadline read as two deadlines.
        // The same-day line stays: it isn't a lead time, it's the 2x bonus, which exists nowhere else
        // on the card.
        if (rush) box.Add(DeadlineLine("SAME DAY — pays 2x on time", 11, ColWholesale));

        // Consequences, spelled out. WRAPS (DeadlineLine is deliberately NoWrap with a pinned height,
        // so this can't use it) and pinned to the badge's full width so it sits along the bottom.
        var consequence = MakeText(
            $"If loading isn't finished by midnight on day {dueDay} — a " +
            $"{contract.LateFeePercent:P0} fine is charged and customer's satisfaction drops.",
            10, ColDangerSoft);
        consequence.style.width = Length.Percent(100);
        consequence.style.whiteSpace = WhiteSpace.Normal;
        consequence.style.unityTextAlign = TextAnchor.UpperRight;
        consequence.style.marginTop = 2;
        consequence.style.marginBottom = 0;
        consequence.style.marginLeft = 0; consequence.style.marginRight = 0;
        box.Add(consequence);

        AttachHoverGrow(box);
        return box;
    }

    /// <summary>
    /// One line inside a SHIP BY badge, set tight.
    ///
    /// Unity's default runtime theme puts an unrequested ~2/4px margin AND ~1/2px padding on every
    /// Label (measured live; the same quirk is documented on MakeTypePill). Stacked four deep that's
    /// most of the air between these lines, and it read as four unrelated sentences rather than one
    /// block of terms. Zeroing it and pinning an explicit compact height roughly halves the gap.
    ///
    /// Height is fontSize + 5 rather than a fixed number so every line scales with its own text —
    /// enough to clear descenders at these sizes, tight enough that the lines group. The badge frame
    /// is sized by its children, so it shrinks to match with no separate number to keep in step.
    /// </summary>
    private Label DeadlineLine(string text, int size, Color color, bool bold = false)
    {
        var label = MakeText(text, size, color, bold);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.marginTop = 0; label.style.marginBottom = 0;
        label.style.marginLeft = 0; label.style.marginRight = 0;
        label.style.paddingTop = 0; label.style.paddingBottom = 0;
        label.style.paddingLeft = 0; label.style.paddingRight = 0;
        label.style.height = size + 5;
        label.style.unityTextAlign = TextAnchor.MiddleRight;
        return label;
    }

    /// <summary>
    /// The recurring half of the SHIP BY badge — a two-hour BLOCK rather than a day.
    ///
    /// A standing account never waits on the player to find it a door: MaintainRecurringSchedule
    /// pre-books its appointments a week ahead, so the promise the player is accepting is a specific
    /// window on each delivery day, and the deadline is that window's end hour. Shown as a clock time
    /// for that reason — "day 6" would be the wrong unit and would read as far more slack than there
    /// really is.
    ///
    /// The block is derived from CutoffHour, which is where MaintainRecurringSchedule aims its
    /// bookings, so the card and the Schedule tab agree by construction rather than by coincidence —
    /// and the slot is FIXED once booked (DockScheduleService.TryMoveToDoor lets the player change the
    /// door but not the time), so what this badge promises is what the trailer will actually get.
    ///
    /// The card does NOT show a first-delivery date, deliberately. A recurring account never delivers
    /// on the day it's signed (OrderArrivalService.DeliversOn), so a date here would be tomorrow's at
    /// the earliest and would compete with the thing that actually matters every single day after
    /// that: the window.
    /// </summary>
    private VisualElement BuildRecurringDeadlineBadge(ContractData contract)
    {
        int block = DockScheduleService.BlockForHour(contract.CutoffHour);
        int endHour = (block + 1) * DockScheduleService.BlockHours;

        var box = new VisualElement();
        box.style.marginTop = 8;
        box.style.alignSelf = Align.Stretch;
        box.style.alignItems = Align.FlexEnd;
        box.style.paddingLeft = 8; box.style.paddingRight = 8;
        // Tighter than the 4 it was: with the lines themselves pulled together (DeadlineLine), the old
        // padding left the frame looking loose around a compact stack.
        box.style.paddingTop = 3; box.style.paddingBottom = 3;
        box.style.backgroundColor = new StyleColor(new Color(ColBlueEdge.r, ColBlueEdge.g, ColBlueEdge.b, 0.10f));
        box.style.borderTopWidth = box.style.borderBottomWidth =
            box.style.borderLeftWidth = box.style.borderRightWidth = 1;
        box.style.borderTopColor = box.style.borderBottomColor =
            box.style.borderLeftColor = box.style.borderRightColor = new StyleColor(ColBlueEdge);
        box.style.borderTopLeftRadius = box.style.borderTopRightRadius =
            box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 6;

        box.Add(DeadlineLine($"SHIP BY: {endHour:00}:00", 13, ColChipOutText, bold: true));
        box.Add(DeadlineLine($"end of the {DockScheduleService.BlockLabel(block)} slot", 11, ColSubtleText));
        box.Add(DeadlineLine($"Auto-booked {OrderArrivalService.ScheduleHorizonDays} days out · fixed slot",
                             10, ColSubtleText));
        box.Add(DeadlineLine("Miss the slot: fee + satisfaction", 10, ColDangerSoft));

        AttachHoverGrow(box);
        return box;
    }

    /// <summary>Grows <paramref name="target"/> ~30% on hover and eases back to normal size on
    /// pointer-leave — the SHIP BY badge is dense, small-print text (a fine, a percentage, a day
    /// number), and this is a cheap way to let the player read it up close without a click. Scale is a
    /// pure paint-time transform and does NOT reflow the box's own layout rect — but it does NOT take
    /// the box out of document flow either, which is exactly what broke the first version of this:
    /// that version called BringToFront() on the enclosing card so the enlarged badge would draw over
    /// the next card in the scroll list. In UI Toolkit a flex container's child ORDER is both paint
    /// order and layout order — there is no separate z-index — so BringToFront() on a card inside the
    /// offer column physically moved that card to the END of the list. The card then jumped out from
    /// under the pointer, firing PointerLeave, which un-scaled it — but nothing ever moved it back,
    /// so every hover permanently shuffled the list, and hovering a badge whose card had just moved
    /// span the same feedback loop again. No reordering here now, on purpose: the badge simply grows
    /// in place from its TOP edge (so it doesn't also creep upward into the button above it) and may
    /// slightly overlap the card below while hovered — a fixed cosmetic trade-off, not a bug.</summary>
    private static void AttachHoverGrow(VisualElement target, float scaleAmount = 1.3f, int durationMs = 140)
    {
        target.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale") };
        target.style.transitionDuration = new List<TimeValue> { new TimeValue(durationMs, TimeUnit.Millisecond) };
        target.style.transitionTimingFunction =
            new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
        target.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(0));

        target.RegisterCallback<PointerEnterEvent>(_ =>
            target.style.scale = new Scale(new Vector3(scaleAmount, scaleAmount, 1f)));
        target.RegisterCallback<PointerLeaveEvent>(_ => target.style.scale = new Scale(Vector3.one));
    }

    /// <summary>The BULK ORDER / RECURRING ORDER badge, sitting directly under the customer name.
    /// Colour AND words, not colour alone — the two types differ in what they commit the player to
    /// (and now in what their deadline even MEANS: a day versus a two-hour block), which is too
    /// important to encode only as a hue. Redundant with which panel the card is in now that New
    /// Contracts is split by type, but kept — the badge is also what the Bulk Orders and Completed
    /// tabs rely on to tell the two types apart in a single mixed list.</summary>
    private VisualElement MakeTypePill(ContractData contract)
    {
        var pill = new VisualElement();
        // In normal flow now, not absolutely positioned in the card's top-right corner. Absolute
        // took it out of layout entirely, which is how it ended up overlapping the money figure —
        // and the corner is where the commit button belongs (see BuildOfferCard). alignSelf
        // FlexStart keeps it hugging its own text inside a stretch-aligned column parent instead of
        // spanning the full body width.
        pill.style.alignSelf = Align.FlexStart;
        pill.style.flexGrow = 0;
        pill.style.flexShrink = 0;
        pill.style.width = StyleKeyword.Auto;
        // 50% size increase: font 10->15, padding 6->9 and 1->2, border 1->2, radius 4->6
        pill.style.paddingLeft = 9; pill.style.paddingRight = 9;
        pill.style.paddingTop = 2; pill.style.paddingBottom = 2;
        pill.style.backgroundColor = new StyleColor(contract.IsBulk ? ColChipBulk : ColChipOut);
        pill.style.borderTopWidth = pill.style.borderBottomWidth =
            pill.style.borderLeftWidth = pill.style.borderRightWidth = 2;
        pill.style.borderTopColor = pill.style.borderBottomColor =
            pill.style.borderLeftColor = pill.style.borderRightColor = new StyleColor(AccentFor(contract));
        pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius =
            pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 6;

        var label = MakeText(contract.KindLabel, 15, TypeTextFor(contract), bold: true);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        // Unity's default runtime theme puts an unrequested margin/padding on every Label (measured
        // live: ~2/4px margin, ~1/2px padding, asymmetric) — invisible on loosely-laid-out text but
        // it's exactly what made this specific badge's border look loose around its own text, since
        // the badge is sized tightly off the label's box. Zeroed so the pill's own padding is the
        // only air around the word.
        label.style.marginLeft = 0; label.style.marginRight = 0;
        label.style.marginTop = 0; label.style.marginBottom = 0;
        label.style.paddingLeft = 0; label.style.paddingRight = 0;
        label.style.paddingTop = 0; label.style.paddingBottom = 0;
        pill.Add(label);
        return pill;
    }

    /// <summary>A customer barred after a missed pickup. Shown rather than hidden so the board
    /// explains itself — a name that simply disappeared reads as a bug, not a consequence.</summary>
    private VisualElement BuildLostCard(ContractData contract, OrderArrivalService arrivals, int rowIndex)
    {
        var card = MakeRow(rowIndex, ColDanger);
        card.style.opacity = 0.55f;
        card.Add(MakeOfferCardIcon(contract.Customer != null ? contract.Customer.Icon : null));

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

    /// <summary>
    /// A customer who won't deal with you yet. Same greyed treatment as a lost account, different
    /// reason and a different feeling: lost is a punishment with a timer, this is a target with a
    /// number. States the gap explicitly, because "needs 300 reputation" when you have 170 is a goal
    /// and "needs 300 reputation" on its own is just a wall.
    /// </summary>
    private VisualElement BuildReputationLockedCard(ContractData contract, int rowIndex)
    {
        int rep = ServiceLocator.TryGet<ReputationService>(out var r) && r != null ? r.Score : 0;

        var card = MakeRow(rowIndex, ColSubtleText);
        card.style.opacity = 0.55f;
        card.Add(MakeOfferCardIcon(contract.Customer != null ? contract.Customer.Icon : null));

        var body = new VisualElement();
        body.style.flexGrow = 1;
        // 19 * 1.5 per Tad's explicit call. FontStyle.Bold alone barely reads as bold with this
        // project's single-weight Nunito Sans (no dedicated bold face for Unity to synthesize from —
        // see the Scheduler sweep-badge fix), so a thin same-colour text outline fakes the extra
        // stroke weight Tad's after.
        var vendorName = MakeText(contract.Customer != null ? contract.Customer.CompanyName : contract.ContractId,
                          29, Color.white, bold: true);
        vendorName.style.unityTextOutlineWidth = 0.6f;
        vendorName.style.unityTextOutlineColor = new StyleColor(Color.white);
        body.Add(vendorName);
        body.Add(MakeText("WON'T DEAL WITH YOU YET", 21, ColDanger, bold: true)); // 14 * 1.5, red per Tad's explicit call
        // "you have {rep}. {gap} to go" put a full stop between two numbers, so at rep 0 it rendered as
        // "you have 0. 250 to go" and read as the decimal 0.250. Comma-joined into one clause instead —
        // a separator that can never be mistaken for part of a number.
        var need = MakeText($"Needs {contract.ReputationRequired:N0} reputation " +
                            $"({ReputationService.BandLabel(ReputationService.BandFor(contract.ReputationRequired))}) " +
                            $"— you have {rep:N0}, so {contract.ReputationRequired - rep:N0} to go.",
                            21, Color.white); // 14 * 1.5 per Tad's explicit call
        need.style.marginTop = 2;
        body.Add(need);
        card.Add(body);
        return card;
    }

    /// <summary>The run-on terms line. Bulk says "ship same day" rather than "due in 0 day(s)", which
    /// reads as missing data; recurring states its booked WINDOW rather than a number of days, because
    /// a standing account's deadline is the end of its pre-booked two-hour block, not the end of a day
    /// (see BuildDeadlineBadge). Either way the deadline also gets its own badge under the button —
    /// it's the term that decides whether the order is serviceable at all.</summary>
    private static string TermsLine(ContractData c)
    {
        if (c.IsBulk)
        {
            string deadline = c.IsSameDayRush
                ? "ship SAME DAY (2x pay)"
                : $"ship within {c.LeadTimeDays} day(s)";
            return $"{c.BulkLinesMin}–{c.BulkLinesMax} item(s) · full pallets out of reserve · " +
                   $"cost of goods + 5% · {deadline} · late fee {c.LateFeePercent:P0}";
        }

        int block = DockScheduleService.BlockForHour(c.CutoffHour);
        return $"{c.FrequencyLabel} · {c.OrdersPerDayMin}–{c.OrdersPerDayMax} orders/day · " +
               $"~{c.EstimatedCasesPerDay} cases/day · ships in the {DockScheduleService.BlockLabel(block)} " +
               $"slot · late fee {c.LateFeePercent:P0}";
    }

    /// <summary>
    /// Rough money the offer represents, for comparing cards. Always money COMING IN, so it's written
    /// with a leading "+".
    ///
    /// It used to lead with "~" for "approximately", which at this size read as a minus sign and made
    /// every contract look like a cost. The estimate caveat lives in the caption underneath instead,
    /// where it can't be mistaken for arithmetic.
    ///
    /// A bulk offer's line items are rolled once at offer creation (ContractData.BulkPreviewLines), so
    /// its figure is computed from those exact lines. A recurring contract's volume is rolled fresh on
    /// each arrival day instead, so there's nothing more specific to read yet — its figure is averaged
    /// over every sellable SKU. Good enough to rank two recurring offers against each other; not a
    /// forecast.
    /// </summary>
    private static string EstimatedValueText(ContractData c, InventoryService inv)
    {
        if (inv == null) return $"x{c.PayRateMultiplier:0.00}";

        var sellable = inv.AllSkus.Where(s => s != null && s.SellValue > 0f).ToList();
        if (sellable.Count == 0) return $"x{c.PayRateMultiplier:0.00}";

        if (c.IsBulk)
        {
            // Priced off BUY value, not sell — a bulk order pays cost of goods plus 5%, so estimating
            // it from SellValue like the others would overstate every card on the board. Computed from
            // THIS contract's own rolled BulkPreviewLines (exactly what GenerateBulk will build on
            // Accept) rather than an average over every SKU in the game — averaging ignored the actual
            // roll entirely, so every bulk card on the board showed the identical figure.
            var lines = c.BulkPreviewLines;
            if (lines != null && lines.Count > 0)
            {
                float total = 0f;
                foreach (var line in lines)
                {
                    var sku = inv.GetSkuData(line.SkuId);
                    if (sku == null || sku.BuyValue <= 0f) continue;
                    total += line.Quantity * sku.BuyValue;
                }
                if (total > 0f)
                    return $"+${Mathf.RoundToInt(total * OrderArrivalService.BulkSurchargeMultiplier):N0}";
            }

            // Fallback for a preview that hasn't been rolled yet (shouldn't happen once offers always
            // roll at creation) — an average estimate is better than nothing here.
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
        // Renders into the SPLIT pane's list, not _content — Bulk now shares the Accounts layout, and
        // _content is hidden on a split tab (see the splitTab gate in Rebuild).
        var intro = new Label("Bulk orders you've accepted. Each line ships as whole pallets out of reserve " +
                              "(a Pallet Pick for the Reach Trucks) plus any loose cases (a normal case pick). " +
                              "Anything still showing NO DOOR when its due day passes loses the account.");
        ApplyFont(intro, size: 14);
        intro.style.color = new StyleColor(ColSubtleText);
        intro.style.whiteSpace = WhiteSpace.Normal;
        intro.style.marginBottom = 8;
        _orderListScroll.Add(intro);

        var live = LiveBulkOrders();
        if (live.Count == 0)
        {
            var none = new Label("No bulk orders on the floor. Accept one on the New Contracts tab — " +
                                 "1–3 land there every day.");
            ApplyFont(none, size: 15);
            none.style.color = new StyleColor(ColSubtleText);
            none.style.whiteSpace = WhiteSpace.Normal;
            none.style.marginTop = 12;
            _orderListScroll.Add(none);
            RefreshOrderDetailsForOrder(null, null);
            _footerMessage.text = "No bulk orders in progress.";
            return;
        }

        var schedule = Schedule();
        int today = CurrentDay();
        int unbooked = 0;

        // Default to the first bulk order so the details pane is never blank on first open — same
        // reasoning as the Accounts tab.
        if (_selectedBulkOrderId == null || live.All(o => o.OrderId != _selectedBulkOrderId))
            _selectedBulkOrderId = live[0].OrderId;

        int row = 0;
        OrderData selectedBulk = null;
        foreach (var order in live)
        {
            bool booked = schedule != null && schedule.FindForOrder(order.OrderId) != null;
            if (!booked) unbooked++;

            var bulkRow = BuildBulkOrderRow(order, booked, today, row++);
            var captured = order;
            bulkRow.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;   // never let a row click swallow its own buttons
                _selectedBulkOrderId = captured.OrderId;
                Rebuild();
            });
            _orderListScroll.Add(bulkRow);

            if (order.OrderId == _selectedBulkOrderId) selectedBulk = order;
        }

        RefreshOrderDetailsForOrder(selectedBulk, selectedBulk?.CustomerName);

        _footerMessage.text = $"{live.Count} bulk order(s) in progress" +
                              (unbooked > 0
                                  ? $"  ·  {unbooked} with NO DOOR BOOKED — book them on the Schedule tab."
                                  : "  ·  all have a door booked.");
    }

    private VisualElement BuildBulkOrderRow(OrderData order, bool booked, int today, int rowIndex)
    {
        bool late = order.DueDay < today;
        // Selection uses the same orange-border treatment as the Accounts (Recurring Orders) tab —
        // the left accent stripe swaps to orange and a matching 2px border wraps the rest of the
        // card, so the selected bulk order visibly stands out from the unbooked/booked accent colors.
        bool selected = order.OrderId == _selectedBulkOrderId;
        Color leftAccent = selected ? ColOrange : (!booked ? ColDanger : ColBulkEdge);
        var card = MakeRow(rowIndex, leftAccent);
        if (selected)
        {
            card.style.borderTopWidth = card.style.borderRightWidth = card.style.borderBottomWidth = 2;
            card.style.borderTopColor = card.style.borderRightColor = card.style.borderBottomColor =
                new StyleColor(ColOrange);
        }

        // LEFT: Customer icon + name and order details
        var leftSection = new VisualElement();
        leftSection.style.flexDirection = FlexDirection.Row;
        leftSection.style.alignItems = Align.FlexStart;
        leftSection.style.flexGrow = 1;
        leftSection.style.flexShrink = 1;

        // Get customer icon from order
        var arrivals = Arrivals();
        Sprite customerIcon = null;
        if (arrivals != null)
        {
            var contract = arrivals.GetContract(order.ContractId);
            if (contract?.Customer != null)
                customerIcon = contract.Customer.Icon;
        }
        var iconElement = MakeIcon(customerIcon, 88, 0);
        iconElement.style.width = 88 * 1.1f; // ~10% wider than the icon's 88px height
        iconElement.style.marginRight = 12;
        leftSection.Add(iconElement);

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;
        body.style.alignItems = Align.FlexStart;

        body.Add(MakeText(order.CustomerName, 17, ColTitleText, bold: true));

        // The player-facing order number (see OrderData.OrderNumber) — the same code that identifies
        // this order's rows in the Work Queue, so a bulk order can be found in either place by the
        // same short string instead of the raw GUID.
        var orderNumberLabel = MakeText($"Order: {order.OrderNumber ?? "—"}", 13, ColChipOutText, bold: true);
        orderNumberLabel.style.marginTop = 2;
        body.Add(orderNumberLabel);

        // Status trio now sits neatly under the customer name instead of floating in an
        // absolutely-centered column of its own — and the per-SKU "520894 — 120 cs · 3 pallet(s)"
        // breakdown that used to live here is gone: the ORDER DETAILS pane on the right already
        // shows every line item, so repeating it on the card was redundant.
        var statusValue = MakeText(BulkPhaseLabel(order), 15, ColMoney, bold: true);
        statusValue.style.marginTop = 4;
        body.Add(statusValue);

        var doorStatus = MakeText(booked ? "Door Booked" : "NO DOOR BOOKED", 13,
                                  booked ? ColMoney : ColDangerSoft, bold: !booked);
        doorStatus.style.marginTop = 2;
        body.Add(doorStatus);

        // THE DEADLINE, restated on the live card. The offer card said what the player was agreeing
        // to; this says how much of it is left. A rush is called out by name while it's still winnable
        // — the 2x is only paid if it closes out before the day rolls (OrderService.ShipOrder), so
        // "today" is the last moment that label means anything.
        string due = late ? $"{today - order.DueDay}d LATE"
                   : order.DueDay == today
                       ? (order.IsSameDayRush ? "SHIP TODAY — 2x pay" : "ship by end of today")
                       : $"ship by day {order.DueDay}";
        var dueStatus = MakeText(due, 13,
                                 late ? ColDangerSoft : order.DueDay == today ? ColWholesale : ColSubtleText,
                                 bold: late || order.DueDay == today);
        dueStatus.style.marginTop = 2;
        body.Add(dueStatus);

        leftSection.Add(body);
        card.Add(leftSection);


        // RIGHT: Order Status header + Cases/Pallets
        var right = new VisualElement();
        right.style.width = 200;
        right.style.flexShrink = 0;
        right.style.alignItems = Align.FlexEnd;
        right.style.flexDirection = FlexDirection.Column;

        // Add "Order Status" header in light blue at top
        var statusHeader = MakeText("Order Status", 14, ColChipOutText);
        statusHeader.style.marginBottom = 6;
        right.Add(statusHeader);

        // Calculate expected pallets
        ServiceLocator.TryGet<OrderService>(out var orders);
        int expectedPallets = 0;
        if (orders != null)
        {
            foreach (var li in order.LineItems)
            {
                int fullPallet = orders.FullPalletCases(li.SkuId);
                if (fullPallet > 0)
                    expectedPallets += li.QuantityNeeded / fullPallet;
            }
        }

        // Add total cases expected (25% bigger font: 12 * 1.25 = 15)
        right.Add(MakeText($"{order.TotalUnits} cases expected", 16, ColTitleText, bold: true));
        right.Add(MakeText($"{expectedPallets} pallet(s) expected", 15, Color.white));

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
            var none = new Label("No accounts yet. Sign something on the New Contracts tab and orders will " +
                                 "start arriving on their own.");
            ApplyFont(none, size: 15);
            none.style.color = new StyleColor(ColSubtleText);
            none.style.whiteSpace = WhiteSpace.Normal;
            none.style.marginTop = 12;
            _orderListScroll.Add(none);
            RefreshOrderDetailsPane(null);
            return;
        }

        _orderListScroll.Add(BuildAccountsSummary(arrivals, running));

        // Default to the first running account so the details pane is never empty on first open.
        if (_selectedAccountContractId == null || running.All(s => s.ContractId != _selectedAccountContractId))
            _selectedAccountContractId = running[0].ContractId;

        int row = 0;
        ContractData selectedContract = null;
        foreach (var signed in running)
        {
            var contract = arrivals.GetContract(signed.ContractId);
            if (contract == null) continue;
            _orderListScroll.Add(BuildAccountRow(arrivals, signed, contract, row++));
            if (signed.ContractId == _selectedAccountContractId) selectedContract = contract;
        }

        RefreshOrderDetailsPane(selectedContract);

        // Spent one-offs are NOT listed here any more. A delivered wholesale drop is a finished deal,
        // not an account you're running — it has nothing left to arrive and nothing to cancel, and its
        // SignedContract carries no useful figures either (the per-contract counters only accrue for
        // orders stamped with a ContractId, which these aren't), so the row read "$0 earned · 0
        // shipped" forever while the real money sat elsewhere.
        _footerMessage.text = $"{running.Count} recurring account(s) running. Cancelling stops future " +
                              "orders; anything already on the board still has to ship.";
    }


    /// <summary>
    /// Four lifetime-average numbers at the top of Accounts: Avg. Plts. Dly., Avg. Cases Dly.,
    /// Avg. On-Time, Avg. Fill Rate %. All four come from FulfillmentStatsService, which tracks
    /// every order ever shipped (not just this tab's currently-running accounts) and divides by
    /// elapsed in-game days — so 240 pallets shipped by day 2 at noon (2.5 elapsed days) reads as
    /// 96 plts/day.
    ///
    /// FulfillmentStatsService accumulates raw totals the instant an order ships, but only
    /// recomputes these four averages once per in-game hour — reading its cached properties here
    /// is always cheap and always current as of the last hour tick.
    /// </summary>
    private VisualElement BuildAccountsSummary(OrderArrivalService arrivals, List<SignedContract> running)
    {
        var strip = new VisualElement();
        strip.style.flexDirection = FlexDirection.Row;
        strip.style.marginBottom = 8;

        if (!ServiceLocator.TryGet<FulfillmentStatsService>(out var stats) || stats == null)
        {
            strip.Add(MakeStatTile("AVG. PLTS. DLY.", "—", ColTitleText));
            strip.Add(MakeStatTile("AVG. CASES DLY.", "—", ColTitleText));
            strip.Add(MakeStatTile("AVG. ON-TIME", "—", ColTitleText));
            strip.Add(MakeStatTile("AVG. FILL RATE %", "—", ColTitleText));
            return strip;
        }

        strip.Add(MakeStatTile("AVG. PLTS. DLY.", $"{stats.AvgPalletsPerDay:N1}", ColTitleText));
        strip.Add(MakeStatTile("AVG. CASES DLY.", $"{stats.AvgCasesPerDay:N0}", ColTitleText));
        strip.Add(MakeStatTile("AVG. ON-TIME", $"{stats.AvgOnTimeRate:P0}",
                               stats.AvgOnTimeRate >= 0.9f ? ColMoney : stats.AvgOnTimeRate >= 0.7f ? ColWholesale : ColDangerSoft));
        strip.Add(MakeStatTile("AVG. FILL RATE %", $"{stats.AvgFillRate:P0}",
                               stats.AvgFillRate >= 0.95f ? ColMoney : stats.AvgFillRate >= 0.6f ? ColWholesale : ColDangerSoft));
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

    /// <summary>Always a running recurring account — RunningAccounts excludes IsBulk (which now also
    /// covers the retired wholesale ordinal), so a bulk/wholesale contract never reaches this
    /// row.</summary>
    private VisualElement BuildAccountRow(OrderArrivalService arrivals, SignedContract signed,
                                          ContractData contract, int rowIndex)
    {
        bool struggling = signed.OrdersLate > 0;
        bool selected = signed.ContractId == _selectedAccountContractId;
        Color accent = selected ? ColOrange : struggling ? ColDanger : ColMoney;

        var card = MakeRow(rowIndex, accent);
        // Top-align the icon/body/stats columns instead of the shared MakeRow default of vertical-
        // center: body's own content (name + satisfaction + pending-orders lookahead rows) is the
        // tallest of the three siblings, so centering pushed the icon and the stats column down into
        // the middle of that taller box instead of lining up with body's title at the top.
        card.style.alignItems = Align.FlexStart;
        // Stable name so the angry reaction can re-find this card after the Rebuild() that a cancel
        // triggers — same trick as SlotElementName on the schedule grid, and for the same reason: the
        // element the effect anchors to is destroyed between queuing and playing.
        card.name = AccountCardName(signed.ContractId);
        if (selected)
        {
            card.style.borderTopWidth = card.style.borderRightWidth = card.style.borderBottomWidth = 2;
            card.style.borderTopColor = card.style.borderRightColor = card.style.borderBottomColor =
                new StyleColor(ColOrange);
        }

        // Icon + CANCEL stacked in one column. Icon size/position is untouched (57px, same margin);
        // CANCEL now lives directly under it instead of off on the right beside the revenue figure.
        var iconColumn = new VisualElement();
        iconColumn.style.alignItems = Align.Center;
        iconColumn.style.flexShrink = 0;
        iconColumn.style.marginRight = 14;

        iconColumn.Add(MakeIcon(contract.Customer != null ? contract.Customer.Icon : null, 57, 6, marginRight: 0));

        var cancel = new Button(() => OnCancelNextOrder(contract, signed)) { text = "CANCEL" };
        StyleGhostButton(cancel);
        // Orange face, white text — this is the one destructive control on the card and it was reading
        // as just another quiet link next to the icon.
        cancel.style.backgroundColor = new StyleColor(ColOrange);
        cancel.style.color = new StyleColor(Color.white);
        cancel.style.borderTopColor = cancel.style.borderBottomColor =
            cancel.style.borderLeftColor = cancel.style.borderRightColor = new StyleColor(ColOrangeEdge);
        cancel.RegisterCallback<PointerEnterEvent>(_ => cancel.style.backgroundColor = new StyleColor(ColOrangeHover));
        cancel.RegisterCallback<PointerLeaveEvent>(_ => cancel.style.backgroundColor = new StyleColor(ColOrange));
        cancel.style.marginTop = 6;
        RuntimeTooltip.Attach(cancel, "Cancel this account's next order. Only possible while the work is still " +
                         "unreleased — once it's on the floor the order has to ship.");
        iconColumn.Add(cancel);

        card.Add(iconColumn);

        // Name + satisfaction — untouched.
        var body = new VisualElement();
        body.style.flexGrow = 0;
        body.style.flexShrink = 0;
        body.style.width = 230;
        body.style.marginRight = 20;

        body.Add(MakeText(contract.Customer != null ? contract.Customer.CompanyName : contract.ContractId,
                          21, ColTitleText, bold: true));

        var satisfaction = MakeText($"Customer Current Satisfaction: {SatisfactionLabel(signed.SatisfactionPercent)}",
                                    13, ColSubtleText);
        satisfaction.style.marginTop = 2;
        satisfaction.style.whiteSpace = WhiteSpace.Normal;
        body.Add(satisfaction);

        card.Add(body);

        // Every order through the planning horizon is real and can be picked early. Keep the next one
        // for the headline statistics/cancel action, but also make the rest explicit on the account card
        // so a single customer contract cannot look like it contains only one shipment.
        var pendingOrders = PendingOrdersFor(signed.ContractId);
        var nextOrder = pendingOrders.FirstOrDefault();
        var plannedLabel = MakeText($"Pending orders: {pendingOrders.Count}", 13, ColWholesale, bold: true);
        plannedLabel.style.marginTop = 5;
        body.Add(plannedLabel);

        // THREE days of lookahead, not the whole generated horizon. The full list ran to fourteen
        // rows, which pushed the card's own statistics off the bottom and buried the near-term work
        // the player can actually act on among orders a week out.
        //
        // Cut on distinct DAYS rather than a count of orders: an account can drop more than once on
        // the same day (day 3 and day 4 each have two here), so "the first three orders" would show
        // day 4 twice and day 5 not at all. The headline count above still reports every pending
        // order, so nothing is hidden — this is a horizon, not a filter on the total.
        const int LookaheadDays = 3;
        var horizonDays = pendingOrders.Select(o => o.CreatedDayNumber)
                                       .Distinct().OrderBy(d => d).Take(LookaheadDays).ToList();

        foreach (var plannedOrder in pendingOrders)
        {
            if (!horizonDays.Contains(plannedOrder.CreatedDayNumber)) continue;

            float rowFill = ApproxFillRate(plannedOrder);

            // Columns, not one run-on string. Fixed cell widths so the item counts, case counts and
            // fill percentages form readable vertical columns down the card — scanning "which of
            // these can I actually fill" is the whole reason this list is here, and that comparison
            // is impossible when every row starts its numbers at a different x.
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 1;

            void Cell(string text, float width, Color color, bool bold = false)
            {
                var c = MakeText(text, 12, color, bold: bold);
                c.style.width = width;
                c.style.flexShrink = 0;
                c.style.whiteSpace = WhiteSpace.NoWrap;
                c.style.overflow = Overflow.Hidden;
                c.style.marginTop = 0; c.style.marginBottom = 0;
                row.Add(c);
            }

                        // Widths trimmed to fit inside body's 230px column (was 274px total, spilling ~44px into
            // the stats block to the right and overlapping "Cases"/"Pallets"). NoWrap + Hidden overflow
            // on each cell means a long value clips rather than wrapping, same as before.
            Cell($"Day {plannedOrder.CreatedDayNumber}:", 42, ColWholesale, bold: true);
            Cell(plannedOrder.OrderNumber ?? "—", 40, ColChipOutText, bold: true);
            Cell($"{plannedOrder.LineItems.Count} item(s)", 52, ColSubtleText);
            Cell($"{plannedOrder.TotalUnits:N0} cs", 38, ColSubtleText);
            // Same colour thresholds as the card's own Approximate Fill Rate below, so a red row here
            // and a red headline there mean the same thing.
            Cell($"{rowFill:P0} fill", 52,
                 rowFill >= 0.95f ? ColMoney : rowFill >= 0.6f ? ColWholesale : ColDangerSoft);

            body.Add(row);
        }

        int today = CurrentDay();
        // TryGetNextArrival reports the first slot that has NOT been generated yet. With the whole
        // planning horizon now materialized, that would misleadingly point beyond the seven orders
        // already visible and workable. The first pending manifest is the actual next shipment.
        string nextDrop = nextOrder != null
            ? (nextOrder.CreatedDayNumber == today
                ? $"{contract.CutoffHour:00}:00 today"
                : $"{contract.CutoffHour:00}:00 on day {nextOrder.CreatedDayNumber}")
            : arrivals.TryGetNextArrival(signed.ContractId, out int day, out int hour)
                ? (day == today ? $"{hour:00}:00 today" : $"{hour:00}:00 on day {day}")
                : "unknown";

        int cases = nextOrder?.TotalUnits ?? 0;
        int pallets = Mathf.CeilToInt(cases / 24f);
        float approxFill = ApproxFillRate(nextOrder);

        // Swapped from the earlier layout per Tad's explicit call: Approximate Fill Rate and Average
        // Weekly Revenue move to the LEFT, left-justified against the stats block's own left edge (the
        // vertical line Tad drew, which lines up with the boundary right after the account's icon/name
        // column). Next Order/Cases/Pallets move to the RIGHT, right-justified against the card's own
        // right edge, and vertically centered ("dropped down") instead of pinned to the top row.
        var stats = new VisualElement();
        stats.style.flexDirection = FlexDirection.Row;
        stats.style.flexGrow = 1;
        // Stretch to match body's full height (the tallest of card's Row siblings).
        stats.style.alignSelf = Align.Stretch;
        stats.style.justifyContent = Justify.SpaceBetween;

        var leftGroup = new VisualElement();
        leftGroup.style.flexDirection = FlexDirection.Column;
        // Stretch spans the full row height so its own SpaceBetween below can pin Fill Rate to the
        // top and Revenue to the bottom, same top/bottom split as before -- just left-justified now.
        leftGroup.style.alignSelf = Align.Stretch;
        leftGroup.style.justifyContent = Justify.SpaceBetween;
        leftGroup.style.marginRight = 18;
        // Nudged right off the stats block's own left edge per Tad's "just a hair" call.
        leftGroup.style.marginLeft = 14;

        var rightGroup = new VisualElement();
        rightGroup.style.flexDirection = FlexDirection.Column;
        rightGroup.style.alignItems = Align.FlexEnd;
        // Center (not Stretch/FlexStart): drops this block down from the top row per Tad's call.
        rightGroup.style.alignSelf = Align.Center;
        // Pulled in from the card's right edge a little so the right-justified text has breathing room.
        rightGroup.style.marginRight = 10;

        void Stat(VisualElement target, string text, Color color, int size, bool bold = false)
        {
            var line = MakeText(text, size, color, bold: bold);
            line.style.marginBottom = 1;
            line.style.whiteSpace = WhiteSpace.NoWrap;
            line.style.overflow = Overflow.Hidden;
            // Without this, a tight row (leftGroup + rightGroup competing for the same stats width)
            // could flex-shrink a line below its text's natural width -- exactly what was clipping
            // "Average Weekly Revenue: $" mid-symbol. Never shrink; NoWrap+Hidden above stays only as
            // a safety net for a genuinely oversized value, not the normal sizing mechanism.
            line.style.flexShrink = 0;
            target.Add(line);
        }

        // ~15% smaller than the prior 23pt.
        const int StatsRightFontSize = 20;
        Stat(rightGroup, $"Next Order: {nextDrop}", ColSubtleText, StatsRightFontSize);
        // No "Shipped:" line. The next order has not been released yet by definition, so it read 0%
        // on every account every time — a reading that never varies is not information.
        Stat(rightGroup, $"Cases: {cases:N0}", ColSubtleText, StatsRightFontSize);
        Stat(rightGroup, $"Pallets: {pallets:N0}", ColSubtleText, StatsRightFontSize);

        // 35% larger than the original 15pt.
        const int StatsLeftFontSize = 20;
        Stat(leftGroup, $"Approximate Fill Rate: {approxFill:P0}",
             approxFill >= 0.95f ? ColMoney : approxFill >= 0.6f ? ColWholesale : ColDangerSoft,
             StatsLeftFontSize, bold: true);

        ServiceLocator.TryGet<ContractRevenueTracker>(out var revenueTracker);
        float avgWeeklyRevenue = revenueTracker?.GetAverageWeeklyRevenue(signed.ContractId) ?? 0f;
        Stat(leftGroup, $"Average Weekly Revenue: ${avgWeeklyRevenue:N0}", ColMoney, StatsLeftFontSize, bold: true);

        stats.Add(leftGroup);
        stats.Add(rightGroup);

        card.Add(stats);

        // Whole card selects the account for the ORDER DETAILS pane — except clicks that land on
        // CANCEL, which must not also select the row it just cancelled.
        card.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target is Button) return;
            _selectedAccountContractId = signed.ContractId;
            Rebuild();
        });

        return card;
    }

    /// <summary>Reads the real SignedContract.SatisfactionPercent instead of the old hard-coded
    /// "Unhappy" placeholder.</summary>
    private static string SatisfactionLabel(float satisfactionPercent) =>
        satisfactionPercent >= 80f ? "Happy" : satisfactionPercent >= 50f ? "Neutral" : "Unhappy";

    /// <summary>All unshipped recurring manifests currently planned for the named contract, earliest
    /// delivery slot first. These are real future orders (not a forecast): OrderArrivalService creates
    /// them across the ScheduleHorizonDays planning horizon so they can be released and picked early.</summary>
    private static List<OrderData> PendingOrdersFor(string contractId)
    {
        if (string.IsNullOrEmpty(contractId) || !ServiceLocator.TryGet<OrderService>(out var orders) || orders == null)
            return new List<OrderData>();

        return orders.ActiveOrders
            .Where(o => o != null && o.ContractId == contractId
                     && !o.IsBulk
                     && o.Status != OrderData.OrderStatus.Shipped
                     && o.Status != OrderData.OrderStatus.Cancelled)
            .OrderBy(o => o.CreatedDayNumber)
            .ThenBy(o => o.DueDay)
            .ThenBy(o => o.OrderId)
            .ToList();
    }

    /// <summary>All unshipped recurring manifests across every active account. Used for the Recurring
    /// Orders badge so the tab count represents actual available/plannable orders, not account count.</summary>
    private static List<OrderData> PendingRecurringOrders()
    {
        if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null)
            return new List<OrderData>();

        return orders.ActiveOrders
            .Where(o => o != null && !o.IsBulk && !string.IsNullOrEmpty(o.ContractId)
                     && o.Status != OrderData.OrderStatus.Shipped
                     && o.Status != OrderData.OrderStatus.Cancelled)
            .OrderBy(o => o.CreatedDayNumber)
            .ThenBy(o => o.DueDay)
            .ThenBy(o => o.OrderId)
            .ToList();
    }

    /// <summary>The account's next planned order — the first unshipped manifest in delivery-slot order.
    /// Cancellation remains intentionally limited to this earliest order.</summary>
    private static OrderData CurrentOrderFor(string contractId)
        => PendingOrdersFor(contractId).FirstOrDefault();

    /// <summary>
    /// "Approximate fill rate": per-SKU on-hand cases (capped at what's needed) summed over the whole
    /// order, divided by total cases needed. 1000 cases of mayo ordered with 500 on the shelf reads as
    /// 50% — exactly the number that's supposed to reward a player for carrying real inventory instead
    /// of ordering everything just-in-time. No order on the board reads as a perfect 100%, not a 0%.
    /// </summary>
    private static float ApproxFillRate(OrderData order)
    {
        if (order == null || order.LineItems.Count == 0) return 1f;
        if (!ServiceLocator.TryGet<InventoryService>(out var inventory) || inventory == null) return 0f;

        int totalNeeded = 0;
        int totalAvailable = 0;
        foreach (var li in order.LineItems)
        {
            int onHand = inventory.GetTotalUnitsBySku(li.SkuId);
            totalAvailable += Mathf.Min(onHand, li.QuantityNeeded);
            totalNeeded += li.QuantityNeeded;
        }
        return totalNeeded > 0 ? totalAvailable / (float)totalNeeded : 1f;
    }

    /// <summary>
    /// The right-hand "ORDER DETAILS" pane on the Accounts tab. Populated from whichever account card
    /// was last clicked (_selectedAccountContractId) — per-SKU breakdown of the account's CURRENT
    /// order: cases needed, pallets needed, cases on hand right now, and the anticipated fill rate for
    /// that one SKU. Passing null clears it back to the "nothing selected" placeholder.
    /// </summary>
    private void RefreshOrderDetailsPane(ContractData contract)
    {
        _orderDetailsBody.Clear();

        var title = MakeText("ORDER DETAILS", 33, ColTitleText, bold: true);
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.marginBottom = 14;
        _orderDetailsBody.Add(title);

        if (contract == null)
        {
            _orderDetailsBody.Add(MakeText("Select an account on the left to see its current order.",
                                          15, ColSubtleText));
            return;
        }

        var who = MakeText(contract.Customer != null ? contract.Customer.CompanyName : contract.ContractId,
                           27, ColTitleText, bold: true);
        who.style.marginBottom = 10;
        _orderDetailsBody.Add(who);

        var pendingOrders = PendingOrdersFor(contract.ContractId);
        if (pendingOrders.Count == 0)
        {
            // Say WHEN, not just "nothing here". An account between orders is the normal state for
            // most of the day, and a bare "no order" reads as a fault.
            string when = Arrivals() != null
                       && Arrivals().TryGetNextArrival(contract.ContractId, out int day, out int hour)
                ? (day == CurrentDay() ? $"Their next order is raised at {hour:00}:00 today."
                                       : $"Their next order is raised at {hour:00}:00 on day {day}.")
                : "Their next order time isn't known.";

            var none = MakeText($"Nothing on the board for this account right now. {when}", 15, ColSubtleText);
            none.style.whiteSpace = WhiteSpace.Normal;
            _orderDetailsBody.Add(none);
            return;
        }

        // Do not silently collapse an account to its earliest order. Every order in the recurring
        // planning horizon is a real, releasable manifest, so render each one with its slot day and
        // individual item lines. The body is already a ScrollView, which keeps a long future plan
        // navigable without hiding it or growing the modal off-screen.
        foreach (var order in pendingOrders)
        {
            var orderHeading = MakeText(
                $"ORDER {order.OrderNumber ?? "—"} · DAY {order.CreatedDayNumber} · {order.TotalUnits:N0} CASES · {order.LineItems.Count} ITEM(S)",
                21, ColWholesale, bold: true);
            orderHeading.style.marginTop = 12;
            orderHeading.style.marginBottom = 5;
            _orderDetailsBody.Add(orderHeading);
            AddOrderLineTable(order);
        }
    }

    /// <summary>
    /// Renders one order's contents as the ORDER DETAILS table. Shared by the Recurring tab (which
    /// selects an ACCOUNT and shows its current order) and the Bulk tab (which selects the ORDER
    /// directly) so the two can't drift into showing the same data differently.
    /// </summary>
    private void AddOrderLineTable(OrderData order)
    {
        ServiceLocator.TryGet<InventoryService>(out var inventory);
        ServiceLocator.TryGet<OrderService>(out var orders);

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.marginBottom = 6;
        header.Add(DetailSpacer(DetailIconSize + 8));           // sits over the item icons
        header.Add(DetailHeaderCell("ITEM", 1.5f));
        header.Add(DetailHeaderCell("ORDERED", 0.6f));
        header.Add(DetailHeaderCell("ON HAND", 0.6f));
        header.Add(DetailHeaderCell("FILL", 0.5f));
        header.Add(DetailHeaderCell("PICK FACE", 0.9f));
        _orderDetailsBody.Add(header);

        foreach (var li in order.LineItems)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 5; row.style.paddingBottom = 5;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(ColBlueEdge);

            var sku = inventory?.GetSkuData(li.SkuId);
            int onHand = inventory != null ? inventory.GetTotalUnitsBySku(li.SkuId) : 0;
            int fullPallet = orders?.FullPalletCases(li.SkuId) ?? 0;
            float lineFill = li.QuantityNeeded > 0
                ? Mathf.Min(onHand, li.QuantityNeeded) / (float)li.QuantityNeeded : 1f;

            row.Add(MakeIcon(sku != null ? sku.Icon : null, DetailIconSize, 4, marginRight: 8));

            // Item number over description — the number is what the player matches against a rack
            // label, the description is what tells them what it actually is.
            var idBlock = new VisualElement();
            idBlock.style.flexGrow = 1.5f; idBlock.style.flexBasis = 0;
            idBlock.style.overflow = Overflow.Hidden;
            var num = MakeText(li.SkuId, 27, ColTitleText, bold: true);
            num.style.whiteSpace = WhiteSpace.NoWrap;
            idBlock.Add(num);
            if (sku != null && !string.IsNullOrEmpty(sku.ItemDescription))
            {
                var desc = MakeText(sku.ItemDescription, 26, ColSubtleText);
                desc.style.whiteSpace = WhiteSpace.NoWrap;
                idBlock.Add(desc);
            }
            row.Add(idBlock);

            row.Add(DetailCell($"{li.QuantityNeeded:N0}", 0.6f, ColSubtleText, padLeft: NumericCellPadLeft));
            row.Add(DetailCell($"{onHand:N0}", 0.6f, onHand >= li.QuantityNeeded ? ColMoney : ColDangerSoft,
                               padLeft: NumericCellPadLeft));
            row.Add(DetailCell($"{lineFill:P0}", 0.5f,
                               lineFill >= 0.95f ? ColMoney : lineFill >= 0.6f ? ColWholesale : ColDangerSoft,
                               bold: true));

            row.Add(PickFaceCell(order, li, fullPallet));
            _orderDetailsBody.Add(row);
        }
    }

    /// <summary>Bulk's entry point to the details pane — it selects an ORDER rather than an account,
    /// so it can't go through RefreshOrderDetailsPane's contract lookup.</summary>
    private void RefreshOrderDetailsForOrder(OrderData order, string who)
    {
        _orderDetailsBody.Clear();

        var title = MakeText("ORDER DETAILS", 33, ColTitleText, bold: true);
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.marginBottom = 14;
        _orderDetailsBody.Add(title);

        if (order == null)
        {
            _orderDetailsBody.Add(MakeText("Select a bulk order on the left to see what's on it.",
                                           15, ColSubtleText));
            return;
        }

        var name = MakeText(who ?? order.CustomerName, 27, ColTitleText, bold: true);
        name.style.marginBottom = 2;
        _orderDetailsBody.Add(name);

        var orderNumberLabel = MakeText($"Order: {order.OrderNumber ?? "—"}", 13, ColChipOutText, bold: true);
        orderNumberLabel.style.marginBottom = 10;
        _orderDetailsBody.Add(orderNumberLabel);

        AddOrderLineTable(order);
    }

    private const float DetailIconSize = 60f;

    private VisualElement DetailSpacer(float width)
    {
        var e = new VisualElement();
        e.style.width = width; e.style.flexShrink = 0;
        return e;
    }

    /// <summary>
    /// Where a case picker will go for this line — or why they can't.
    ///
    /// A line the Reach Trucks take whole (a bulk order's full-pallet quantities) needs no pick face
    /// and is reported as such rather than as a problem: flagging "no pick slot" on freight that was
    /// never going to be hand-picked would send the player off assigning slots that change nothing.
    ///
    /// A case-pick line with no assigned slot IS a problem, and one the player can fix immediately —
    /// so it says so in the cell where the address would have been, in smaller text so a row of them
    /// reads as a to-do list rather than a wall of errors.
    /// </summary>
    private VisualElement PickFaceCell(OrderData order, OrderLineItem line, int fullPalletCases)
    {
        bool wholePallets = order.IsBulk && fullPalletCases > 0
                            && line.QuantityNeeded % fullPalletCases == 0;

        if (wholePallets)
            return DetailCell("Pallet Picks", 0.9f, ColSubtleText);

        var slots = SlotAssignmentService.GetSlotsForSku(line.SkuId);
        if (slots != null && slots.Count > 0)
        {
            string text = slots.Count == 1 ? slots[0] : $"{slots[0]} +{slots.Count - 1}";
            return DetailCell(text, 0.9f, ColChipOutText);
        }

        var warn = MakeText("Product needs a pickslot assigned", 15, ColDangerSoft);
        warn.style.flexGrow = 0.9f; warn.style.flexBasis = 0;
        warn.style.whiteSpace = WhiteSpace.Normal;
        return warn;
    }

    private VisualElement DetailHeaderCell(string text, float flex)
    {
        var cell = MakeText(text, 18, ColSubtleText, bold: true);
        cell.style.flexGrow = flex; cell.style.flexBasis = 0;
        return cell;
    }

    /// <summary>The ORDERED/ON HAND figures are short next to their own headers, so at flex-basis 0
    /// they sit visibly left of the header text above them. Nudged right rather than centred so the
    /// column still reads as a left-aligned number list.</summary>
    private const float NumericCellPadLeft = 12f;

    private VisualElement DetailCell(string text, float flex, Color color, bool bold = false,
                                     float padLeft = 0f)
    {
        var cell = MakeText(text, 21, color, bold: bold);
        cell.style.flexGrow = flex; cell.style.flexBasis = 0;
        if (padLeft > 0f) cell.style.paddingLeft = padLeft;
        return cell;
    }

    /// <summary>Calculate lifetime fill rate for a contract: (total cases shipped / total cases ordered) * 100%</summary>
    /// <remarks>Unused since the Accounts tab redesign switched the card's fill-rate readout to
    /// ApproxFillRate (per-SKU on-hand ÷ needed for the CURRENT order) — kept in case a lifetime figure
    /// is wanted again on the Completed tab or elsewhere.</remarks>
    private float CalculateLifetimeFillRate(string contractId)
    {
        if (!ServiceLocator.TryGet<OrderService>(out var orderService) || orderService == null)
            return 0f;

        int totalOrdered = 0;
        int totalShipped = 0;

        // Check active orders
        foreach (var order in orderService.ActiveOrders)
        {
            if (order != null && order.ContractId == contractId)
            {
                totalOrdered += order.TotalUnits;
                totalShipped += order.TotalUnitsPicked;
            }
        }

        // Check completed orders in history
        foreach (var order in orderService.OrderHistory)
        {
            if (order != null && order.ContractId == contractId)
            {
                totalOrdered += order.TotalUnits;
                totalShipped += order.TotalUnitsPicked;
            }
        }

        if (totalOrdered == 0) return 1f; // No orders = perfect fill rate
        return totalShipped / (float)totalOrdered;
    }

    private string StatusLine(OrderArrivalService arrivals, SignedContract signed, ContractData contract)
    {
        int today = CurrentDay();
        int daysHeld = Mathf.Max(0, today - signed.SignedOnDay);

        string next = arrivals.TryGetNextArrival(signed.ContractId, out int day, out int hour)
            ? (day == today ? $"next drop {hour:00}:00 today" : $"next drop {hour:00}:00 ON day {day}")
            : "next drop unknown";

        string lateBit = signed.OrdersLate > 0
            ? $" · {signed.OrdersLate} late · ${signed.LateFeesPaid:N0} in fees"
            : " · 0 late";

        return $"Days held: {daysHeld} · {next} · {signed.OrdersDelivered} shipped{lateBit} · " +
               $"on-time {signed.OnTimeRate:P0} · satisfaction {signed.SatisfactionPercent:F0}%";
    }

    /// <summary>
    /// Cancels this account's NEXT order — not the account itself.
    ///
    /// Only while the work is still unreleased. Once the player has pushed it to the floor there are
    /// selectors walking to pick faces and possibly pallets already standing in a lane; unwinding that
    /// from a button on a summary card would leave physical goods staged against an order that no
    /// longer exists. "Released" is read off the live tasks rather than the order's status, because the
    /// task list is what the floor is actually working from.
    ///
    /// Costs a large satisfaction hit and floats the angry reaction, same as a missed deadline —
    /// refusing to supply an order the customer placed is the worst thing this panel can do to an
    /// account short of dropping it.
    /// </summary>
    private void OnCancelNextOrder(ContractData contract, SignedContract signed)
    {
        string who = contract.Customer != null ? contract.Customer.CompanyName : contract.ContractId;

        var order = CurrentOrderFor(contract.ContractId);
        if (order == null)
        {
            UIToast.Show($"{who} has no order on the board to cancel.");
            return;
        }

        if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null) return;

        if (OrderWorkIsOnTheFloor(order))
        {
            UIToast.Show($"{who}'s order is already released to the floor — it has to ship now.");
            return;
        }

        int cancelled = orders.CancelOrders(new List<string> { order.OrderId });
        if (cancelled == 0)
        {
            UIToast.Show($"Couldn't cancel {who}'s order.");
            Rebuild();
            return;
        }

        Arrivals()?.PenalizeSatisfaction(contract.ContractId,
                                         OrderArrivalService.CancelledOrderSatisfactionPenalty);

        UIToast.Show($"{who}'s order cancelled — satisfaction down hard. Future orders still arrive.");
        PlayUnhappyOverAccountCard(contract.ContractId);
        Rebuild();
    }

    /// <summary>
    /// Has any of this order's work been handed to the floor yet?
    ///
    /// Anything past Open counts: Available means the player released it and a worker may already be
    /// walking to it. Read from the live task list rather than OrderData.Status because a partially
    /// picked order and a released-but-untouched one look the same from the status alone, and only one
    /// of them is safe to cancel from here.
    /// </summary>
    private static bool OrderWorkIsOnTheFloor(OrderData order)
    {
        if (order == null) return false;
        if (!ServiceLocator.TryGet<GameCore.Labor.WorkQueueSystem>(out var wq) || wq == null) return false;

        foreach (var t in wq.Tasks)
        {
            if (t == null || t.OrderId != order.OrderId) continue;
            if (t.Status == GameCore.Labor.WorkTaskStatus.Available
             || t.Status == GameCore.Labor.WorkTaskStatus.Assigned
             || t.Status == GameCore.Labor.WorkTaskStatus.Complete) return true;
        }
        return false;
    }

    private static string AccountCardName(string contractId) => $"acct-card-{contractId}";

    /// <summary>Floats the angry-customer reaction over an account's card, the same effect a missed
    /// slot plays over a schedule cell. Deferred a frame because the caller rebuilds immediately after
    /// and the card being pointed at doesn't exist yet at the moment this is asked for.</summary>
    private void PlayUnhappyOverAccountCard(string contractId)
    {
        string name = AccountCardName(contractId);
        _overlay.schedule.Execute(() =>
        {
            var card = _orderListScroll?.Q(name) ?? _content?.Q(name);
            if (card != null) UnhappyCustomerFx.Play(_overlay, card, 0);
        }).ExecuteLater(16);
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
        if (_selectedAccountContractId == contract.ContractId) _selectedAccountContractId = null;
        Rebuild();
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
    /// <summary>An offer-board card icon: IconSize tall, OfferIconWidthScale wider than that.</summary>
    private VisualElement MakeOfferCardIcon(Sprite sprite)
    {
        var icon = MakeIcon(sprite, IconSize, 8);
        icon.style.width = IconSize * OfferIconWidthScale;
        return icon;
    }

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

private static Font NunitoFont()
{
    if (_nunito != null) return _nunito;
#if UNITY_EDITOR
    // UI Toolkit's FontDefinition.FromSDFFont needs a UnityEngine.TextCore.Text.FontAsset, which is
    // a different type from TMPro.TMP_FontAsset in this project's TMP version (confirmed: TMP_FontAsset
    // derives from TMPro.TMP_Asset, not TextCore.Text.FontAsset, and no TextCore FontAsset for Nunito
    // exists anywhere in the project) — so this loads the plain-Font copy of the same Nunito Sans
    // typeface via FromFont instead, the same loading pattern every other panel's LilitaFont() uses.
    string[] guids = UnityEditor.AssetDatabase.FindAssets("NunitoSans t:Font");
    foreach (var guid in guids)
    {
        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
        if (path.IndexOf("Italic", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
        _nunito = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(path);
        break;
    }
#else
    _nunito = Resources.Load<Font>("NunitoSans-VariableFont_YTLC,opsz,wdth,wght");
#endif
    return _nunito;
}

private static void ApplyFont(VisualElement el, bool bold = false, int size = -1)
{
    var f = NunitoFont();
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
        b.style.height = OrangeButtonHeight;
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

    /// <summary>
    /// Closes this panel and opens the inbound Purchasing screen — the mirror of
    /// PurchasingPanel.OpenScheduler, which is what sends the player here in the first place.
    ///
    /// Closes rather than layering, for the same reason that one does: both are full-size draggable
    /// windows, and stacking them leaves two overlapping modals with no obvious way out. Routing
    /// through the key manager rather than calling Show directly keeps the exclusivity every other
    /// panel obeys, so Tab still closes it and opening a third panel still closes this.
    ///
    /// Says why if purchasing can't be reached, rather than reading as a dead button.
    /// </summary>
    private void OpenPurchasing()
    {
        Hide();

        var topBar = UnityEngine.Object.FindAnyObjectByType<TopBarUI>();
        var purchasing = topBar != null ? topBar.PurchasingPanel : null;
        if (purchasing == null)
        {
            UIToast.Show("Couldn't open purchasing — the panel isn't loaded.");
            return;
        }

        UIKeyBindingManager.Instance?.CloseAll();
        purchasing.Show();
    }

    /// <summary>Closes this panel and opens the standalone Scheduler (key 0's dock appointment grid) —
    /// same shape as OpenPurchasing, just a different destination for the SCHEDULER button.</summary>
    private void OpenScheduler()
    {
        Hide();

        var topBar = UnityEngine.Object.FindAnyObjectByType<TopBarUI>();
        var scheduler = topBar != null ? topBar.SchedulerPanel : null;
        if (scheduler == null)
        {
            UIToast.Show("Couldn't open the scheduler — the panel isn't loaded.");
            return;
        }

        UIKeyBindingManager.Instance?.CloseAll();
        scheduler.Show();
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
    // ── Tab 5: New Scheduler ─────────────────────────────────────────────────
}
