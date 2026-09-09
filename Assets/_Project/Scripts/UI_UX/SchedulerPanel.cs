using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Economy;
using GameCore.Events;
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
public class SchedulerPanel : IUIPanel
{
    private static readonly Color ColBg          = new Color(18f / 255f, 26f / 255f, 36f / 255f, 1f);
    private static readonly Color ColBorder      = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleText   = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColSubtleText  = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    private static readonly Color ColOrange      = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeEdge  = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    private static readonly Color ColOrangeText  = new Color(0xFD / 255f, 0xE8 / 255f, 0xCC / 255f, 1f);
    private static readonly Color ColOrangeHover = new Color(0xC6 / 255f, 0x7F / 255f, 0x42 / 255f, 1f);
    private static readonly Color ColCardEven = new Color(36f / 255f, 48f / 255f, 62f / 255f, .65f);
    private static readonly Color ColCardOdd = new Color(30f / 255f, 40f / 255f, 52f / 255f, .65f);
    private static readonly Color ColMoney       = new Color(0x7E / 255f, 0xD6 / 255f, 0x8A / 255f, 1f);
    private static readonly Color ColWholesale   = new Color(0xF3 / 255f, 0x9E / 255f, 0x47 / 255f, 1f); // warmed (more red, less yellow) and confirmed fully opaque per Tad's explicit call
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
    private const float ModalWidth  = 1366f;
    // Raised from 680 so the Schedule tab's grid — a ScrollView that just fills whatever's left after
    // the fixed-height header strip — shows more block rows without scrolling. minH on the
    // ResizableWindow below is tied to this same constant, so the player still can't drag it shorter
    // than the new default, same as before.
    private const float ModalHeight = 920f;
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

    private const float IconSize    = 72f;   // Offers cards

    /// <summary>Offer-card icons are drawn 10% wider than they are tall. The customer sprites are
    /// not square, so a square box squeezed them horizontally. Width only — the height stays on
    /// IconSize so every card on the board keeps the same baseline as the text beside it.</summary>
    private const float OfferIconWidthScale = 1.1f;
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
    private enum Tab { NewContracts, BulkOrders, Accounts, Schedule, NewScheduler }

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

    private static Font _nunito;

        public SchedulerPanel(VisualElement root)
    {
        _overlay = Build(out _modal, out _tabBar, out _tabHeader, out _content, out _footerMessage);
        root.Add(_overlay);
        // Added to the OVERLAY, not the modal: the modal is absolutely positioned and draggable, so a
        // dialog inside it would follow the window around and could sit half off-screen. The overlay
        // fills the panel's whole area, which is what a modal confirmation should darken and block.
        _overlay.Add(BuildOffSlotConfirm());

        // Live refresh: the chips' out-of-stock counts/colors and any currently-open hover tooltip
        // (short-shipped/needed-for-order highlighting) are all derived from live inventory, so they
        // go stale the instant a pallet is actually received anywhere — not just on the next
        // incidental Rebuild(). Per Tad's explicit call: wire this to the real receiving event.
        EventManager.Instance?.Subscribe<PalletMasterRecord>(GameEvents.Inventory.OnPalletReceived, OnPalletReceivedForLiveRefresh);

        Hide();
    }

    // ── Off-slot move confirmation ───────────────────────────────────────────
    //
    // Moving a trailer off the hour its customer asked for is allowed and always was — what was
    // missing is that the player had no way to know it cost anything until after they'd done it and
    // read a toast. This is the checkpoint: state the price, get a yes, and let anyone who's learned
    // it turn the prompt off for good.

    /// <summary>PlayerPrefs key for the "don't show this again" tick. PlayerPrefs rather than the save
    /// file on purpose — this is a preference about the UI belonging to the person playing, not world
    /// state belonging to one warehouse, so it should hold across every save and new game the way
    /// PlayerName and Difficulty already do.</summary>
    private const string OffSlotWarningPref = "Contracts.SuppressOffSlotWarning";

    private static bool OffSlotWarningSuppressed
    {
        get => PlayerPrefs.GetInt(OffSlotWarningPref, 0) == 1;
        set { PlayerPrefs.SetInt(OffSlotWarningPref, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    private VisualElement _confirmBlocker;
    private Label _confirmMessage;
    private Toggle _confirmSuppress;
    private System.Action _confirmYesAction;

    /// <summary>
    /// The full-panel dim + centred Yes/No card. Built once and kept hidden rather than created per
    /// prompt, so the "don't show again" tick has somewhere to live between openings and there's no
    /// per-interaction allocation on a path the player may hit repeatedly while shuffling doors.
    /// </summary>
    private VisualElement BuildOffSlotConfirm()
    {
        _confirmBlocker = new VisualElement();
        _confirmBlocker.style.position = Position.Absolute;
        _confirmBlocker.style.left = 0; _confirmBlocker.style.right = 0;
        _confirmBlocker.style.top = 0; _confirmBlocker.style.bottom = 0;
        _confirmBlocker.style.alignItems = Align.Center;
        _confirmBlocker.style.justifyContent = Justify.Center;
        _confirmBlocker.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
        _confirmBlocker.style.display = DisplayStyle.None;
        // Swallows every click that isn't on the card, so the grid underneath can't be clicked while
        // an unanswered prompt is up — the whole point of a checkpoint is that it interrupts.
        _confirmBlocker.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

        var card = new VisualElement();
        card.style.width = 460;
        card.style.paddingTop = 18; card.style.paddingBottom = 16;
        card.style.paddingLeft = 22; card.style.paddingRight = 22;
        card.style.backgroundColor = new StyleColor(ColBg);
        card.style.borderTopWidth = card.style.borderBottomWidth =
            card.style.borderLeftWidth = card.style.borderRightWidth = 2;
        card.style.borderTopColor = card.style.borderBottomColor =
            card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(ColOrange);
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 10;

        var title = MakeText("ARE YOU SURE?", 22, ColOrangeText, bold: true);
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.marginBottom = 10;
        card.Add(title);

        _confirmMessage = MakeText("", 15, ColTitleText);
        _confirmMessage.style.whiteSpace = WhiteSpace.Normal;
        _confirmMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
        _confirmMessage.style.marginBottom = 14;
        card.Add(_confirmMessage);

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.justifyContent = Justify.Center;

        var yes = new Button(() => { var act = _confirmYesAction; HideOffSlotConfirm(); act?.Invoke(); })
            { text = "YES, MOVE IT" };
        StyleOrangeButton(yes);
        yes.style.height = OrangeButtonHeight * 1.25f;
        yes.style.marginRight = 10;
        buttons.Add(yes);

        var no = new Button(HideOffSlotConfirm) { text = "NO" };
        StyleOrangeButton(no);
        no.style.height = OrangeButtonHeight * 1.25f;
        // Muted so the destructive-ish option isn't the one the eye lands on first.
        no.style.backgroundColor = new StyleColor(ColStat);
        no.style.color = new StyleColor(ColSubtleText);
        buttons.Add(no);
        card.Add(buttons);

        // `text`, not the Toggle(label) constructor: a Toggle's LABEL renders to the left of the
        // checkbox, which reads backwards for an opt-out ("Don't show this again ☐"). `text` puts the
        // caption to the right of the tick where a checkbox caption belongs. Styling then has to go
        // through a Q over every Label in the control, since the element `text` creates isn't
        // labelElement — that one stays empty and would silently swallow the font change.
        _confirmSuppress = new Toggle { text = "Don't show this again" };
        foreach (var lbl in _confirmSuppress.Query<Label>().ToList())
        {
            ApplyFont(lbl, size: 13);
            lbl.style.color = new StyleColor(ColSubtleText);
        }
        _confirmSuppress.style.marginTop = 14;
        _confirmSuppress.style.alignSelf = Align.Center;
        // Written on CHANGE rather than when Yes is clicked: ticking it and then answering No is still
        // the player saying "I know what this costs, stop asking" — the tick is about the prompt, not
        // about this particular move.
        _confirmSuppress.RegisterValueChangedCallback(evt => OffSlotWarningSuppressed = evt.newValue);
        card.Add(_confirmSuppress);

        _confirmBlocker.Add(card);
        return _confirmBlocker;
    }

    /// <summary>Runs <paramref name="onYes"/> immediately if the player has turned the warning off,
    /// otherwise puts the prompt up and runs it only on Yes.</summary>
    private void ConfirmOffSlotMove(string customerName, string requestedLabel, string newLabel,
                                    System.Action onYes, int repCost = 0, string distance = null)
    {
        if (OffSlotWarningSuppressed) { onYes(); return; }

        // Quoting the ACTUAL number, not "a negative impact". The penalty scales with distance now, so
        // a warning that reads identically for a two-hour nudge and a three-day slip would hide the one
        // thing the player is deciding about. A dialog that can't tell you the price isn't a choice.
        string price = repCost > 0
            ? $"\n\nThis lands {distance} — reputation −{repCost}, plus a fine on any order already on " +
              $"the trailer, and this account's satisfaction drops."
            : "\n\nA fine will be imposed as well as a hit to this account's satisfaction.";

        _confirmMessage.text =
            $"Customer will be dissatisfied if you make an appointment earlier or later than the " +
            $"requested Appointment Timeslot.\n\n" +
            $"{customerName} asked for {requestedLabel}. You're moving them to {newLabel}." + price;
        _confirmYesAction = onYes;
        _confirmSuppress.SetValueWithoutNotify(false);
        _confirmBlocker.style.display = DisplayStyle.Flex;
        _confirmBlocker.BringToFront();
    }

    private void HideOffSlotConfirm()
    {
        _confirmYesAction = null;
        _confirmBlocker.style.display = DisplayStyle.None;
    }

    public bool IsVisible => _visible;
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
        if (_scheduleDay == int.MinValue) _scheduleDay = CurrentDay();
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

    /// <summary>
    /// Opens this panel on a given day.
    ///
    /// Exists for cross-panel links — the Purchasing panel's "Scheduler" button, which sends the
    /// player from a PO that needs a door to the grid where doors are booked.
    ///
    /// A day of 0 or less means "leave the view where it was", so a caller with no opinion doesn't
    /// have to invent one.
    /// </summary>
    public void ShowForDay(int day = 0)
    {
        if (day > 0) _scheduleDay = day;
        Show();
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
        _selectedAppointmentId = null;
        _selectedUnscheduledKey = null;
        _overlay.style.display = DisplayStyle.None;
    }

    public void Dispose()
    {
        EventManager.Instance?.Unsubscribe<PalletMasterRecord>(GameEvents.Inventory.OnPalletReceived, OnPalletReceivedForLiveRefresh);
        if (_overlay.parent != null) _overlay.RemoveFromHierarchy();
    }

    /// <summary>Fires on every pallet formally received anywhere in the warehouse. Refreshes the
    /// chips (if this panel is on screen) and any currently-open hover tooltip in place, so a live
    /// figure like an "out of stock" count or a short-shipped highlight can't sit stale while the
    /// player is looking right at it.</summary>
    private void OnPalletReceivedForLiveRefresh(string eventId, PalletMasterRecord pallet)
    {
        if (_visible) Rebuild();
        RefreshOpenTooltipContent();
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
        ApplyFont(title, bold: true, size: 44); // 35 * 1.25 — another 25% larger per Tad's explicit call
        title.style.color = new StyleColor(ColTitleText);
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        // Absolute + full-width-of-titleBar rather than flexGrow: with flexGrow the title only
        // centers in the space LEFT OVER after the ORDERS/PURCHASING/resize/close buttons (all of
        // which sit to its right), which visibly drags it left of true screen center. Overlaying it
        // across the whole title bar — independent of the button row's flow — centers it on the
        // modal itself, matching the Day switcher bar directly below. Ignore picking so it never
        // steals clicks from the buttons drawn on top of it.
        title.style.position = Position.Absolute;
        title.style.left = 0;
        title.style.right = 0;
        title.style.top = 0;
        title.style.bottom = 0;
        title.pickingMode = PickingMode.Ignore;
        titleBar.Add(title);

        // Cycles normal / large / fill-screen (see ResizableWindow.CycleScale below). Same size as the
        // close button and on the same title-bar row, so the two sit flush together.
        const float titleBtnSize = 48f; // 1.5x the base 32px square button

        // Cross-links to the other two outbound/inbound screens — the mirror of PurchasingPanel's own
        // "Scheduler" button, which is what sends the player here in the first place. Sits left of the
        // window buttons so the destructive ✕ keeps the far corner it always has.
        var toOrders = new Button(OpenOrders) { text = "ORDERS" };
        StyleCrossLinkButton(toOrders);
        // The title label above is no longer in the flex flow (it's an absolute overlay), so this
        // button group needs its own push to the right edge that the title's old flexGrow used to
        // provide as a side effect.
        toOrders.style.marginLeft = new StyleLength(StyleKeyword.Auto);
        titleBar.Add(toOrders);

        var backToPurchasing = new Button(OpenPurchasing) { text = "PURCHASING" };
        StyleCrossLinkButton(backToPurchasing);
        titleBar.Add(backToPurchasing);

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

        // Routed through CloseAll(), not a bare Hide() — this panel is registered on key 0, and only
        // UIKeyBindingManager.ToggleUI/CloseAll ever reset _currentOpenKey back to -1. A direct Hide()
        // left it stuck at 0 forever, and PlacementStateMachine.HandleIdleHover gates the world hover
        // popup on CurrentOpenKey == -1 — so clicking this ✕ silently killed every world tooltip for
        // the rest of the session even though the panel had visibly closed. CloseAll() calls Hide() on
        // every open registered panel (this one included) and THEN clears CurrentOpenKey, so it's a
        // safe superset of what the old direct call did.
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
        content.schedule.Execute(SyncNewSchedulerSweep).Every(150);

        footerMessage = new Label();
        ApplyFont(footerMessage, size: 15);
        footerMessage.style.color = new StyleColor(ColSubtleText);
        footerMessage.style.marginTop = 8;
        footerMessage.style.whiteSpace = WhiteSpace.Normal;

        // Rich hover card for New Scheduler timeline cells — lives directly on the modal (not
        // inside the ScrollView) so it isn't clipped by the grid's scroll viewport, and survives
        // every Rebuild() since Rebuild() only clears _tabHeader/_content, not the modal's other
        // fixed children.
        _newSchedulerTooltip = new VisualElement();
        _newSchedulerTooltip.style.position = Position.Absolute;
        _newSchedulerTooltip.style.display = DisplayStyle.None;
        // Was PickingMode.Ignore (pass-through) -- now Position so the tooltip itself can catch
        // MouseEnter/Leave (to stay open while the player's cursor is over it) and mouse-wheel
        // scroll events on the ScrollView below, per Tad's explicit call.
        _newSchedulerTooltip.pickingMode = PickingMode.Position;
        _newSchedulerTooltip.style.width = 460; // widened further per Tad's explicit call
        _newSchedulerTooltip.style.backgroundColor = new StyleColor(ColBg);
        _newSchedulerTooltip.style.borderTopWidth = _newSchedulerTooltip.style.borderBottomWidth =
            _newSchedulerTooltip.style.borderLeftWidth = _newSchedulerTooltip.style.borderRightWidth = 2;
        _newSchedulerTooltip.style.borderTopColor = _newSchedulerTooltip.style.borderBottomColor =
            _newSchedulerTooltip.style.borderLeftColor = _newSchedulerTooltip.style.borderRightColor =
                new StyleColor(ColBorder);
        _newSchedulerTooltip.style.borderTopLeftRadius = _newSchedulerTooltip.style.borderTopRightRadius =
            _newSchedulerTooltip.style.borderBottomLeftRadius = _newSchedulerTooltip.style.borderBottomRightRadius = 8;
        _newSchedulerTooltip.style.paddingTop = 10; _newSchedulerTooltip.style.paddingBottom = 10;
        _newSchedulerTooltip.style.paddingLeft = 12; _newSchedulerTooltip.style.paddingRight = 12;
        _newSchedulerTooltip.RegisterCallback<MouseLeaveEvent>(_ => HideNewSchedulerTooltip());

        // Content scrolls vertically once it's taller than the space left below the hovered cell,
        // rather than running off the bottom of the modal -- max-height is set per-show once we
        // know how much room is actually available (see ShowNewSchedulerTooltip).
        _newSchedulerTooltipScroll = new ScrollView(ScrollViewMode.Vertical);
        _newSchedulerTooltipScroll.style.overflow = Overflow.Hidden;
        _newSchedulerTooltip.Add(_newSchedulerTooltipScroll);

        modal.Add(_newSchedulerTooltip);
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





    /// <summary>
    /// Debug-only order triggers, moved here from the Tools window's "Outbound Simulator" section on
    /// 2026-08-01. Outbound debug belongs beside the outbound UI, and the Tools window's copy was
    /// misleading now that signed contracts are the real source of orders.
    ///
    /// Styled as a muted outline rather than a game action on purpose — it should never be mistaken
    /// for a thing the player is supposed to press. Hidden entirely when ToolsWindowController isn't
    /// in the scene, which is also what will hide it in a shipping build.
    /// </summary>


    /// <summary>
    /// Two-step, because it is not undoable and there is no confirmation dialog in this project.
    ///
    /// The first press arms the button and says what it will destroy; the second press inside the
    /// window actually does it. A dev control sitting one click away from deleting every account the
    /// player has built up is worth exactly one extra click.
    /// </summary>


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


    /// <summary>Last applied horizontal offset. Reset to NaN on rebuild so the next poll always
    /// re-applies — freshly built cells start at left 0 regardless of where the view is scrolled.</summary>
    private float _lastHScroll = float.NaN;

    /// <summary>The Schedule tab's "Door 1 / Door 2 / …" column header — lives in the stationary
    /// strip above the grid and is slid sideways in step with the grid's horizontal scroll so it stays
    /// lined up over the right columns. Null on every other tab.</summary>
    private VisualElement _scheduleHeaderRow;
    private float _lastScheduleHScroll = float.NaN;

    /// <summary>The Schedule tab's rail clock — kept live by SyncScheduleClock rather than only
    /// reflecting the moment of the last Rebuild(). Null on every other tab.</summary>
    private Label _scheduleClockLabel;
    private string _lastScheduleClockText;

    /// <summary>What schedule.CurrentDay/CurrentBlock were as of the Schedule tab's last build —
    /// watermarked in BuildSchedule, watched by SyncScheduleElapsed. int.MinValue means "never built
    /// yet," so the first poll after opening the tab can't mistake itself for a block change and force
    /// a redundant immediate rebuild.</summary>
    private int _lastLiveScheduleDay = int.MinValue;
    private int _lastLiveScheduleBlock = int.MinValue;
    // Which chips read as finished at the last check — see SyncScheduleElapsed/CompletedSignature.
    private string _lastCompletedSignature;

    /// <summary>
    /// Slides the Schedule tab's "Door 1 / Door 2 / …" header sideways by exactly what the grid is
    /// scrolled, so the two stay in step once the window is dragged narrower than the columns need.
    ///
    /// The mirror image of SyncFrozenTimeColumn, which holds the Schedule tab's time cells STILL
    /// against a scrolling grid; here the header is outside the scroller and has to be made to move
    /// WITH it. Same mechanism either way: `left` on a relative element is a visual offset that
    /// doesn't disturb siblings or the element's own width.
    /// </summary>


    /// <summary>Keeps the Schedule tab's rail clock ticking between rebuilds. Before this it only ever
    /// showed the time as of the last Rebuild() — which in practice meant it looked frozen unless the
    /// player did something that happened to trigger one (switching tabs, or the window losing and
    /// regaining focus), which read as a bug rather than the correct value just going stale. Guarded on
    /// change, same reasoning as SyncScheduleHeader/SyncCompletedHeader.</summary>




    /// <summary>
    /// Notices when the real clock crosses into a new 2-hour block and rebuilds the grid so its rows
    /// pick that up. Every row's past/future colouring (see BuildScheduleRow/BuildEmptySlot) is computed
    /// from schedule.CurrentBlock at the moment the row is BUILT — the clock label ticks live on its
    /// own (SyncScheduleClock), but the grid it sits above doesn't, so a slot that was still open at
    /// 03:59 kept reading as open past 04:00 until something unrelated forced a rebuild (a click, a tab
    /// switch). Gated to the Schedule tab: a rebuild is real work, and no other tab's content depends on
    /// this.
    /// </summary>


    /// <summary>Which of the displayed day's appointments currently read as finished. Compared as a
    /// string rather than diffed properly because it only has to answer "did anything change?" — the
    /// rebuild does the real work.</summary>




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


    private static string TitleFor(Tab tab) => "SCHEDULER";

    private void Rebuild()
    {
        if (_titleLabel != null) _titleLabel.text = TitleFor(_tab);
        _tabHeader.Clear();
        _content.Clear();
        _orderListScroll.Clear();
        _orderDetailsBody.Clear();
        _footerMessage.text = string.Empty;

        _content.mode = ScrollViewMode.Vertical;
        _content.style.display = DisplayStyle.Flex;
        _splitPane.style.display = DisplayStyle.None;

        var arrivals = Arrivals();
        if (arrivals == null)
        {
            _footerMessage.text = "Order arrival service isn't running — contracts can't be signed. " +
                                  "(OrderArrivalService is not registered in GameContext.)";
            return;
        }

        BuildNewScheduler(arrivals);
    }

    // ── Tab 1: New Contracts ─────────────────────────────────────────────────

    private const string RecurringHelperText =
        "Recurring Orders are ongoing contractual agreements to ship orders to customers on a " +
        "predetermined frequency. These will auto-schedule on the Schedule tab at their designated " +
        "time, although these can be modified at any given time up until the order is released.";

    private const string BulkHelperText =
        "Bulk Orders are one-time pickups, usually from customers that don't have a recurring order " +
        "contract. Treating these customers well could turn them into long-standing partners!";



    /// <summary>One hemisphere of the New Contracts board: a bordered panel dedicated to a single
    /// contract type, with a large title, an explanatory paragraph naming what the type actually
    /// commits the player to, and its own card list (offers first, then anything barred by a missed
    /// pickup). BuildOfferCard/BuildLostCard are unchanged and shared by both panels — only the
    /// grouping is new.</summary>


    /// <summary>Accent colour for a contract type — the same family used for its chips elsewhere, so
    /// a bulk order reads as the same thing on this tab and in the stranded strip.</summary>






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


    /// <summary>The BULK ORDER / RECURRING ORDER badge, sitting directly under the customer name.
    /// Colour AND words, not colour alone — the two types differ in what they commit the player to
    /// (and now in what their deadline even MEANS: a day versus a two-hour block), which is too
    /// important to encode only as a hue. Redundant with which panel the card is in now that New
    /// Contracts is split by type, but kept — the badge is also what the Bulk Orders and Completed
    /// tabs rely on to tell the two types apart in a single mixed list.</summary>


    /// <summary>A customer barred after a missed pickup. Shown rather than hidden so the board
    /// explains itself — a name that simply disappeared reads as a bug, not a consequence.</summary>


    /// <summary>
    /// A customer who won't deal with you yet. Same greyed treatment as a lost account, different
    /// reason and a different feeling: lost is a punishment with a timer, this is a target with a
    /// number. States the gap explicitly, because "needs 300 reputation" when you have 170 is a goal
    /// and "needs 300 reputation" on its own is just a wall.
    /// </summary>


    /// <summary>The run-on terms line. Bulk says "ship same day" rather than "due in 0 day(s)", which
    /// reads as missing data; recurring states its booked WINDOW rather than a number of days, because
    /// a standing account's deadline is the end of its pre-booked two-hour block, not the end of a day
    /// (see BuildDeadlineBadge). Either way the deadline also gets its own badge under the button —
    /// it's the term that decides whether the order is serviceable at all.</summary>


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


    // ── Tab 2: Bulk Orders ───────────────────────────────────────────────────

    /// <summary>Bulk orders still in the building — accepted and not yet shipped or cancelled.</summary>


    /// <summary>
    /// The live bulk orders and how far through the warehouse each one is.
    ///
    /// Deliberately not a second Work Queue. This answers the two questions the Work Queue can't:
    /// how a bulk order breaks down into pallets versus loose cases, and whether it has a door booked
    /// — which is what decides whether the account survives past its due day.
    /// </summary>




    /// <summary>Where this order has got to, in the player's words rather than OrderStatus's.</summary>




    // ── Tab 3: Accounts ──────────────────────────────────────────────────────




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




    /// <summary>Always a running recurring account — RunningAccounts excludes IsBulk (which now also
    /// covers the retired wholesale ordinal), so a bulk/wholesale contract never reaches this
    /// row.</summary>


    /// <summary>Reads the real SignedContract.SatisfactionPercent instead of the old hard-coded
    /// "Unhappy" placeholder.</summary>


    /// <summary>All unshipped recurring manifests currently planned for the named contract, earliest
    /// delivery slot first. These are real future orders (not a forecast): OrderArrivalService creates
    /// them across the ScheduleHorizonDays planning horizon so they can be released and picked early.</summary>


    /// <summary>All unshipped recurring manifests across every active account. Used for the Recurring
    /// Orders badge so the tab count represents actual available/plannable orders, not account count.</summary>


    /// <summary>The account's next planned order — the first unshipped manifest in delivery-slot order.
    /// Cancellation remains intentionally limited to this earliest order.</summary>


    /// <summary>
    /// "Approximate fill rate": per-SKU on-hand cases (capped at what's needed) summed over the whole
    /// order, divided by total cases needed. 1000 cases of mayo ordered with 500 on the shelf reads as
    /// 50% — exactly the number that's supposed to reward a player for carrying real inventory instead
    /// of ordering everything just-in-time. No order on the board reads as a perfect 100%, not a 0%.
    /// </summary>


    /// <summary>
    /// The right-hand "ORDER DETAILS" pane on the Accounts tab. Populated from whichever account card
    /// was last clicked (_selectedAccountContractId) — per-SKU breakdown of the account's CURRENT
    /// order: cases needed, pallets needed, cases on hand right now, and the anticipated fill rate for
    /// that one SKU. Passing null clears it back to the "nothing selected" placeholder.
    /// </summary>


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




    // ── Tab 3: Schedule ──────────────────────────────────────────────────────



    private void OnUnscheduledClicked(UnscheduledGroup group)
    {
        _selectedUnscheduledKey = _selectedUnscheduledKey == group.Key ? null : group.Key;
        _selectedAppointmentId = null; // one thing in hand at a time — see _selectedUnscheduledKey
        Rebuild();
    }



    /// <summary>
    /// Compact, single-row legend — swatch + label, three abbreviated entries — that sits to the left
    /// of the day switcher in BuildScheduleHeader. This replaced a full stacked LEGEND column that
    /// used to live inside BuildScheduleStrip; that space is now the "PO/ORDER DETAILS" breakdown for
    /// whatever's currently selected, so the legend had to shrink to fit above the grid instead.
    /// </summary>
    private VisualElement BuildCompactLegend()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.flexShrink = 1;
        row.Add(MakeCompactLegendItem(ColChipOut, ColBlueEdge, "recurring"));
        row.Add(MakeCompactLegendItem(ColChipBulk, ColBulkEdge, "bulk"));
        row.Add(MakeCompactLegendItem(ColChipIn, ColOrangeEdge, "inbound PO"));
        return row;
    }

    private VisualElement MakeCompactLegendItem(Color fill, Color edge, string text)
    {
        var item = new VisualElement();
        item.style.flexDirection = FlexDirection.Row;
        item.style.alignItems = Align.Center;
        item.style.marginRight = 14;

        var swatch = new VisualElement();
        swatch.style.width = 11; swatch.style.height = 11;
        swatch.style.flexShrink = 0;
        swatch.style.marginRight = 5;
        swatch.style.backgroundColor = new StyleColor(fill);
        swatch.style.borderTopWidth = swatch.style.borderBottomWidth =
            swatch.style.borderLeftWidth = swatch.style.borderRightWidth = 1;
        swatch.style.borderTopColor = swatch.style.borderBottomColor =
            swatch.style.borderLeftColor = swatch.style.borderRightColor = new StyleColor(edge);
        swatch.style.borderTopLeftRadius = swatch.style.borderTopRightRadius =
            swatch.style.borderBottomLeftRadius = swatch.style.borderBottomRightRadius = 3;
        item.Add(swatch);

        var label = MakeText(text, 11, ColSubtleText);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        item.Add(label);
        return item;
    }

    // ── Schedule strip geometry ───────────────────────────────────────────────────────────────────
    // Pool boxes read as a small pennant flag: a dark "pole" strip on the left, then a wider colored
    // "flag" rectangle carrying the icon and details. Still deliberately compact — this strip sits
    // between the day switcher and the grid, so every pixel it takes is a block row you can't see —
    // but wide enough for an icon and a pallet count, which a bare 78x70 square never had room for.
    private const float PoolBoxWidth  = 212f;
    private const float PoolBoxHeight = 68f;
    private const float PoolPoleWidth = 5f;
    private const float PoolIconSize  = 36f;
    private const int   PoolMaxBoxes  = 10;

    /// <summary>
    /// The bordered strip under the day switcher, split into two hemispheres by a vertical rule:
    /// LEGEND on the left, the stranded-trailer POOL on the right. The in-game clock used to live on a
    /// rail along the bottom of this strip — moved up next to the day switcher in BuildScheduleHeader
    /// instead, so this strip no longer has a bottom row at all once its two halves are built.
    ///
    /// The pool replaces a separate full-width strip that listed the same trailers as wide
    /// name-and-due-date chips: both were on screen at once carrying identical data, and the wide ones
    /// pushed the grid down. Same trailers, one small colour-coded box each, one place to look.
    ///
    /// A box is pick-up-then-place, not a literal pixel drag — the grid lives in a ScrollView, and a
    /// drag that has to auto-scroll to reach door 7 or block 11 is worse than two clicks. Clicking a
    /// box selects it exactly like clicking a booked chip does, and every open slot already lights up
    /// and accepts a click while something is held (see BuildEmptySlot's canDrop).
    /// </summary>


    private Label MakeStripCaption(string text, Color color)
    {
        var caption = MakeText(text, 18, color, bold: true); // 14 * 1.25 per Tad's explicit call
        caption.style.marginBottom = 6;
        caption.style.whiteSpace = WhiteSpace.NoWrap;
        return caption;
    }

    private VisualElement MakeLegendRow(Color fill, Color edge, string text)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 3;

        var swatch = new VisualElement();
        swatch.style.width = 22; swatch.style.height = 22;
        swatch.style.flexShrink = 0;
        swatch.style.marginRight = 10;
        swatch.style.backgroundColor = new StyleColor(fill);
        swatch.style.borderTopWidth = swatch.style.borderBottomWidth =
            swatch.style.borderLeftWidth = swatch.style.borderRightWidth = 2;
        swatch.style.borderTopColor = swatch.style.borderBottomColor =
            swatch.style.borderLeftColor = swatch.style.borderRightColor = new StyleColor(edge);
        swatch.style.borderTopLeftRadius = swatch.style.borderTopRightRadius =
            swatch.style.borderBottomLeftRadius = swatch.style.borderBottomRightRadius = 4;
        row.Add(swatch);

        var label = MakeText(text, 16, ColSubtleText);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        row.Add(label);
        return row;
    }

    /// <summary>
    /// Builds the "PO/ORDER DETAILS" pane that now occupies the left half of the schedule strip
    /// (where the LEGEND used to live — see BuildCompactLegend for where that went). Shows item
    /// number / description / quantity for whichever inbound PO or outbound trailer is currently
    /// selected on the grid, parked, or still sitting in the unscheduled pool. Compressed on purpose
    /// (11px rows) and capped to a scrollable height so a long manifest never pushes the grid down.
    /// </summary>
    private VisualElement BuildPoOrderDetailsPanel(List<UnscheduledGroup> unscheduled)
    {
        var container = new VisualElement();
        container.style.width = Length.Percent(100);

        var (title, lines) = ResolvePoOrderDetails(unscheduled);

        if (!string.IsNullOrEmpty(title))
        {
            var who = MakeText(title, 12, ColTitleText, bold: true);
            who.style.whiteSpace = WhiteSpace.NoWrap;
            who.style.overflow = Overflow.Hidden;
            who.style.marginBottom = 4;
            container.Add(who);
        }

        if (lines == null || lines.Count == 0)
        {
            var empty = MakeText(
                "Click an inbound PO or outbound order on the grid (or in the pool above) to see its " +
                "item numbers, descriptions and quantities here.", 11, ColSubtleText);
            empty.style.whiteSpace = WhiteSpace.Normal;
            container.Add(empty);
            return container;
        }

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.maxHeight = 96;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.marginBottom = 2;
        header.Add(PoDetailHeaderCell("ITEM #", 1.1f, 0));
        header.Add(PoDetailHeaderCell("DESCRIPTION", 1.6f, 0));
        header.Add(PoDetailHeaderCell("QTY", 0, PoDetailQtyColumnWidth));
        scroll.Add(header);

        foreach (var line in lines)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 1; row.style.paddingBottom = 1;

            var skuLabel = MakeText(line.sku, 11, ColTitleText, bold: true);
            skuLabel.style.flexBasis = 0; skuLabel.style.flexGrow = 1.1f;
            skuLabel.style.whiteSpace = WhiteSpace.NoWrap; skuLabel.style.overflow = Overflow.Hidden;
            row.Add(skuLabel);

            var descLabel = MakeText(string.IsNullOrEmpty(line.desc) ? "—" : line.desc, 11, ColSubtleText);
            descLabel.style.flexBasis = 0; descLabel.style.flexGrow = 1.6f;
            descLabel.style.whiteSpace = WhiteSpace.NoWrap; descLabel.style.overflow = Overflow.Hidden;
            row.Add(descLabel);

            // Fixed width, not a flex share — a flex-grown QTY cell drifts left/right of the QTY
            // header by however much room the ITEM#/DESCRIPTION text ahead of it actually used on
            // that particular row, so the numbers never lined up under their own header. A fixed
            // width matching the header cell's is what keeps every QTY value in one vertical column.
            var qtyLabel = MakeText($"{line.qty:N0}", 11, ColSubtleText);
            qtyLabel.style.width = PoDetailQtyColumnWidth; qtyLabel.style.flexShrink = 0; qtyLabel.style.flexGrow = 0;
            qtyLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(qtyLabel);

            scroll.Add(row);
        }
        container.Add(scroll);
        return container;
    }

    private const float PoDetailQtyColumnWidth = 44f;

    private Label PoDetailHeaderCell(string text, float flexGrow, float fixedWidth)
    {
        var label = MakeText(text, 10, ColSubtleText, bold: true);
        if (fixedWidth > 0)
        {
            label.style.width = fixedWidth; label.style.flexShrink = 0; label.style.flexGrow = 0;
            label.style.unityTextAlign = TextAnchor.MiddleRight;
        }
        else
        {
            label.style.flexBasis = 0; label.style.flexGrow = flexGrow;
        }
        label.style.whiteSpace = WhiteSpace.NoWrap;
        return label;
    }

    /// <summary>Figures out WHAT to show in the PO/Order Details pane: the selected grid/parked
    /// appointment takes priority (an inbound PO reads its ShipmentData line items, an outbound
    /// trailer reads its riding orders' line items), falling back to a selected-but-not-yet-booked
    /// pool group. Returns (null, empty) when nothing is selected.</summary>
    private (string title, List<(string sku, string desc, int qty)> lines) ResolvePoOrderDetails(
        List<UnscheduledGroup> unscheduled)
    {
        var schedule = Schedule();
        var appt = _selectedAppointmentId != null ? schedule?.FindById(_selectedAppointmentId) : null;
        if (appt != null)
        {
            if (IsInboundPo(appt))
            {
                ServiceLocator.TryGet<ShipmentService>(out var shipments);
                var shipment = shipments?.PendingShipments.FirstOrDefault(s => s.PONumber == appt.ShipmentPoNumber);
                if (shipment == null)
                    return ($"PO {appt.ShipmentPoNumber} — no longer on file", new List<(string, string, int)>());

                // One row PER SKU, not per line item — a player PO builds one ShipmentLineItem per
                // PALLET (see PalletCountForPO), so the same SKU can show up several times over. The
                // manifest here is meant to answer "what's on this truck and how much", not "how many
                // pallets", so totals it by SKU the same way BuildOrderLinesFor already does for orders.
                ServiceLocator.TryGet<InventoryService>(out var inv);
                var bySku = new Dictionary<string, int>();
                foreach (var li in shipment.LineItems)
                {
                    bySku.TryGetValue(li.SkuId, out int existing);
                    bySku[li.SkuId] = existing + li.Quantity;
                }
                var lines = bySku.Select(kv => (kv.Key, inv?.GetSkuData(kv.Key)?.ItemDescription, kv.Value)).ToList();
                return ($"PO {appt.ShipmentPoNumber} — {appt.CustomerName}", lines);
            }

            return BuildOrderLinesFor(appt.OrderIds, appt.CustomerName);
        }

        if (_selectedUnscheduledKey != null)
        {
            var group = unscheduled.FirstOrDefault(g => g.Key == _selectedUnscheduledKey);
            if (group != null) return BuildOrderLinesFor(group.OrderIds, group.CustomerName);
        }

        return (null, new List<(string, string, int)>());
    }

    /// <summary>Sums line-item quantities, by SKU, across every order riding one trailer — a
    /// multi-order recurring/bulk trailer shows one combined manifest rather than one block per order.</summary>
    private (string title, List<(string sku, string desc, int qty)> lines) BuildOrderLinesFor(
        List<string> orderIds, string customerName)
    {
        if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null
            || orderIds == null || orderIds.Count == 0)
            return ($"{customerName} — no items generated yet", new List<(string, string, int)>());

        ServiceLocator.TryGet<InventoryService>(out var inv);
        var bySku = new Dictionary<string, int>();
        foreach (var id in orderIds)
        {
            var order = orders.ActiveOrders.FirstOrDefault(o => o.OrderId == id);
            if (order == null) continue;
            foreach (var li in order.LineItems)
            {
                bySku.TryGetValue(li.SkuId, out int existing);
                bySku[li.SkuId] = existing + li.QuantityNeeded;
            }
        }

        if (bySku.Count == 0) return ($"{customerName} — no items generated yet", new List<(string, string, int)>());

        var lines = bySku.Select(kv => (kv.Key, inv?.GetSkuData(kv.Key)?.ItemDescription, kv.Value)).ToList();
        return (customerName, lines);
    }

    /// <summary>
    /// One stranded trailer as a small colour-coded flag. Fill and edge come from the same ChipFill/
    /// ChipEdge switch the booked chips in the grid use — a box in the pool and the chip it becomes
    /// once placed are the same trailer, so if they disagreed on colour the legend would be lying
    /// about one of them. Selection and lateness override the type colour: what you're holding and
    /// what's already overdue both matter more than what kind of freight it is.
    /// </summary>
    private VisualElement BuildPoolBox(UnscheduledGroup group, int today, OrderArrivalService arrivals)
    {
        bool selected = group.Key == _selectedUnscheduledKey;
        bool late = group.EarliestDueDay < today;

        Color fill = selected ? ColOrange : ChipFill(group.Kind);
        Color edge = selected ? ColOrangeText : late ? ColDanger : ChipEdge(group.Kind);
        Color ink  = selected ? ColOrangeText : late ? ColDangerSoft : ChipText(group.Kind);

        // The slot the customer actually contracted for: their contract's CutoffHour (the closest
        // thing to an "expected receiving time" on record) — falls back to an em dash for a Dev
        // Console order, which carries no ContractId and therefore no hour to show.
        var contract = arrivals?.GetContract(group.ContractId);
        string timeText = contract != null ? $"{contract.CutoffHour:00}:00" : "—";
        string dayText = late ? $"{today - group.EarliestDueDay}d LATE" : DeadlineLabel(group.EarliestDueDay);

        int pallets = PalletCountForGroup(group);
        string detail = $"{PalletLabel(pallets)} · {group.OrderIds.Count} order(s) · {timeText}";

        var box = BuildFlagBox(
            icon: IconForCustomer(group.CustomerId, group.ContractId, arrivals),
            title: group.CustomerName,
            subtitle: dayText,
            detail: detail,
            fill: fill, edge: edge, ink: ink,
            selected: selected);

        // Stopped here, not left to bubble: the pool container itself is a drop target for a held
        // appointment chip (see BuildScheduleStrip), and a click on one specific box means "select
        // this box" — never also "drop what I'm holding into the pool at large."
        box.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); OnUnscheduledClicked(group); });

        // Same rich hover card a booked grid chip shows (full line-item breakdown, fill rate, etc.)
        // rather than a plain text tooltip — per Tad's explicit call, built from a throwaway
        // DockAppointment standing in for this group since it has no real appointment yet.
        var syntheticAppt = new DockAppointment
        {
            Kind = group.Kind,
            CustomerId = group.CustomerId,
            CustomerName = group.CustomerName,
            ContractId = group.ContractId,
            OrderIds = group.OrderIds,
            Day = group.EarliestDueDay,
        };
        box.RegisterCallback<MouseEnterEvent>(_ =>
        {
            ShowNewSchedulerTooltip(syntheticAppt, box);
            CustomCursorService.SetHoveringInteractable(true);
        });
        box.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            CustomCursorService.SetHoveringInteractable(false);
            if (_newSchedulerTooltip != null && _newSchedulerTooltip.style.display == DisplayStyle.Flex &&
                _newSchedulerTooltip.worldBound.Contains(evt.mousePosition)) return;
            HideNewSchedulerTooltip();
        });
        return box;
    }

    /// <summary>"DEADLINE: D{day} {hh:00}" — the day and block-start hour after which a not-yet-shipped
    /// trailer starts costing a late penalty (the last bookable block of its due day). Replaces the old
    /// plain "Day N" subtitle on pool/parked boxes, per Tad's explicit call — the day alone didn't say
    /// how much of it was actually left.</summary>
    private static string DeadlineLabel(int dueDay)
    {
        int lastBlockHour = (DockScheduleService.BlocksPerDay - 1) * DockScheduleService.BlockHours;
        return $"DEADLINE: D{dueDay} {lastBlockHour:00}:00";
    }

    /// <summary>
    /// Shared pennant-flag layout for every pool box: a dark "pole" strip on the left, a colour-coded
    /// flag rectangle carrying the customer/vendor icon, then a title / subtitle / detail stack. Both
    /// BuildPoolBox and BuildParkedBox funnel through here so a stranded customer order and a parked
    /// trailer (including an inbound PO) look like the same family of thing, differing only in colour
    /// and text.
    /// </summary>
    private VisualElement BuildFlagBox(Sprite icon, string title, string subtitle, string detail,
                                       Color fill, Color edge, Color ink, bool selected)
        => BuildFlagBox(icon, title, subtitle, MakeFlagDetailText(detail, ink), fill, edge, ink, selected);

    private Label MakeFlagDetailText(string detail, Color ink)
    {
        var detailLabel = MakeText(detail, 11, ink);
        detailLabel.style.whiteSpace = WhiteSpace.NoWrap;
        detailLabel.style.overflow = Overflow.Hidden;
        detailLabel.style.opacity = 0.85f;
        detailLabel.style.marginTop = 1; detailLabel.style.marginBottom = 0;
        return detailLabel;
    }

    private VisualElement BuildFlagBox(Sprite icon, string title, string subtitle, VisualElement detailElement,
                                       Color fill, Color edge, Color ink, bool selected)
    {
        var box = new VisualElement();
        box.style.width = PoolBoxWidth; box.style.height = PoolBoxHeight;
        box.style.flexShrink = 0;
        box.style.flexDirection = FlexDirection.Row;
        box.style.marginRight = 8; box.style.marginBottom = 6;
        box.style.overflow = Overflow.Hidden;

        // The "pole" — a slim dark strip the flag hangs off of, which is what makes the rectangle
        // read as a pennant rather than just another chip.
        var pole = new VisualElement();
        pole.style.width = PoolPoleWidth;
        pole.style.flexShrink = 0;
        pole.style.backgroundColor = new StyleColor(ColBorder);
        pole.style.borderTopLeftRadius = pole.style.borderBottomLeftRadius = 3;
        box.Add(pole);

        // The "flag" — the colour-coded body carrying the icon and text.
        var flag = new VisualElement();
        flag.style.flexGrow = 1;
        flag.style.flexDirection = FlexDirection.Row;
        flag.style.alignItems = Align.Center;
        flag.style.paddingLeft = 8; flag.style.paddingRight = 8;
        flag.style.backgroundColor = new StyleColor(fill);
        flag.style.borderTopWidth = flag.style.borderBottomWidth = flag.style.borderRightWidth = selected ? 3 : 2;
        flag.style.borderTopColor = flag.style.borderBottomColor = flag.style.borderRightColor = new StyleColor(edge);
        flag.style.borderTopRightRadius = flag.style.borderBottomRightRadius = 5;
        box.Add(flag);

        var iconEl = MakeIcon(icon, PoolIconSize, 6, marginRight: 8);
        iconEl.style.borderTopWidth = iconEl.style.borderBottomWidth =
            iconEl.style.borderLeftWidth = iconEl.style.borderRightWidth = 2;
        iconEl.style.borderTopColor = iconEl.style.borderBottomColor =
            iconEl.style.borderLeftColor = iconEl.style.borderRightColor = new StyleColor(edge);
        flag.Add(iconEl);

        var textCol = new VisualElement();
        textCol.style.flexGrow = 1;
        textCol.style.flexShrink = 1;
        textCol.style.overflow = Overflow.Hidden;

        var titleLabel = MakeText(title, 15, ink, bold: true);
        titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        titleLabel.style.overflow = Overflow.Hidden;
        titleLabel.style.marginTop = 0; titleLabel.style.marginBottom = 0;
        titleLabel.style.paddingTop = 0; titleLabel.style.paddingBottom = 0;
        textCol.Add(titleLabel);

        var subtitleLabel = MakeText(subtitle, 12, ink, bold: true);
        subtitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        subtitleLabel.style.marginTop = 0; subtitleLabel.style.marginBottom = 0;
        subtitleLabel.style.paddingTop = 0; subtitleLabel.style.paddingBottom = 0;
        textCol.Add(subtitleLabel);

        textCol.Add(detailElement);

        flag.Add(textCol);
        return box;
    }

    /// <summary>
    /// A pool box for a trailer the player has PARKED — pulled off the grid but not given up on.
    ///
    /// Drawn like a stranded box so the pool reads as one row of "trailers with no door", but it's a
    /// different object underneath: a live DockAppointment rather than a derived group of orders. That
    /// difference is why it selects through _selectedAppointmentId and places through TryMoveToDoor,
    /// which is what keeps a parked recurring trailer bound to its promised time slot instead of
    /// letting the player launder it into a different one by unbooking and re-booking.
    ///
    /// Marked with a dashed-looking accent border and a "held" tooltip so it's distinguishable from
    /// freight that was never booked at all — the player put this one here on purpose.
    /// </summary>
    private VisualElement BuildParkedBox(DockAppointment appt, int today)
    {
        bool selected = appt.Id == _selectedAppointmentId;
        bool late = appt.Day < today;

        // An inbound PO reservation is identified by its PO NUMBER, not by initials of the supplier.
        // The supplier is the same wholesaler on every order; the number is what tells one delivery
        // from the next, and it's what the Purchasing panel showed the player when they raised it.
        bool isPo = !string.IsNullOrEmpty(appt.ShipmentPoNumber);

        Color fill = selected ? ColOrange : ChipFill(appt.Kind);
        Color edge = selected ? ColOrangeText : late ? ColDanger : ColOrange;
        Color ink  = selected ? ColOrangeText : late ? ColDangerSoft : ChipText(appt.Kind);

        string title, subtitle;
        VisualElement detailElement;
        int pallets;
        if (isPo)
        {
            pallets = PalletCountForPO(appt.ShipmentPoNumber);
            title = $"PO {appt.ShipmentPoNumber}";
            subtitle = late ? $"{today - appt.Day}d LATE" : DeadlineLabel(appt.Day);
            // Pallet count was unreadable at the shared 11px detail size, and the vendor name was
            // dead weight -- every PO parked here is the same wholesaler, so drop it and let the
            // number that actually matters (for judging door/lane capacity) be twice as big instead.
            var detailRow = new VisualElement();
            detailRow.style.flexDirection = FlexDirection.Row;
            detailRow.style.alignItems = Align.FlexEnd;
            detailRow.style.opacity = 0.85f;
            // Pulled up negative -- the 22px number's own line-height was pushing this row past the
            // box's fixed height, clipping the title above and the number below. Zeroing pad/margin on
            // both labels wasn't enough on its own since the taller line-height is baked into the font
            // metrics, not spacing this code controls.
            detailRow.style.marginTop = -5;
            var countLabel = MakeText(pallets.ToString(), 22, ink, bold: true);
            countLabel.style.marginRight = 4;
            countLabel.style.marginTop = 0; countLabel.style.marginBottom = 0;
            countLabel.style.paddingTop = 0; countLabel.style.paddingBottom = 0;
            detailRow.Add(countLabel);
            var unitLabel = MakeText(pallets == 1 ? "pallet" : "pallets", 19, ink); // 11 * 1.75 per Tad's explicit call
            unitLabel.style.marginTop = 0; unitLabel.style.marginBottom = 0;
            unitLabel.style.paddingTop = 0; unitLabel.style.paddingBottom = 0;
            detailRow.Add(unitLabel);
            detailElement = detailRow;
        }
        else
        {
            pallets = 0; // a held outbound trailer carries no line items of its own to count
            title = appt.CustomerName;
            subtitle = late ? $"{today - appt.Day}d LATE" : DeadlineLabel(appt.Day);
            detailElement = MakeFlagDetailText($"{DockScheduleService.BlockLabel(appt.BlockIndex)} · held", ink);
        }

        var box = BuildFlagBox(
            icon: IconForCustomer(appt.CustomerId, appt.ContractId, Arrivals()),
            title: title, subtitle: subtitle, detailElement: detailElement,
            fill: fill, edge: edge, ink: ink,
            selected: selected);

        // Same reason as BuildPoolBox: the pool container is itself a drop target, and a click on a
        // specific box must mean "select this one", never "also drop what I'm holding".
        box.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); OnParkedClicked(appt); });

        // Same rich hover card a booked grid chip shows — per Tad's explicit call. This box already
        // carries a real DockAppointment, so no throwaway stand-in is needed the way BuildPoolBox
        // needs one.
        box.RegisterCallback<MouseEnterEvent>(_ =>
        {
            ShowNewSchedulerTooltip(appt, box);
            CustomCursorService.SetHoveringInteractable(true);
        });
        box.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            CustomCursorService.SetHoveringInteractable(false);
            if (_newSchedulerTooltip != null && _newSchedulerTooltip.style.display == DisplayStyle.Flex &&
                _newSchedulerTooltip.worldBound.Contains(evt.mousePosition)) return;
            HideNewSchedulerTooltip();
        });
        return box;
    }

    /// <summary>Picks up (or puts down) a parked trailer. Shares _selectedAppointmentId with grid
    /// chips because a parked trailer IS an appointment — which means the existing "click a slot to
    /// move the held appointment" path in OnSlotClicked already places it, with no second code path
    /// and no chance of the two disagreeing about the rules.</summary>
    private void OnParkedClicked(DockAppointment appt)
    {
        _selectedAppointmentId = _selectedAppointmentId == appt.Id ? null : appt.Id;
        _selectedUnscheduledKey = null; // one thing in hand at a time
        Rebuild();
    }

    /// <summary>
    /// "Door 1 / Door 2 / …" labels, one per column, sitting directly above the grid. Column widths
    /// and the leading/trailing spacers mirror BuildScheduleRow exactly (TimeColWidth, SlotWidth per
    /// door with the same 5px marginRight the chips use, FullFlagWidth) so a header lines up with the
    /// slot underneath it without any per-cell math of its own.
    ///
    /// This row lives in the stationary strip (_tabHeader), not the scroll view, so SyncScheduleHeader
    /// has to hand it the grid's horizontal scroll offset every frame — see that method.
    /// </summary>


    /// <summary>
    /// One block row: time label, then one slot per outbound door, then a FULL flag.
    ///
    /// Every part is a FIXED width with flexShrink 0. Slots used to be flexGrow/flexBasis-0, which
    /// made them share whatever width was available — with enough doors they'd squeeze to nothing
    /// instead of overflowing, so the horizontal scrollbar could never appear. Fixed widths are what
    /// let the row grow past the viewport and give the scroller something to scroll.
    /// </summary>


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

    /// <summary>What each timeline chip colour means — Recurring / Bulk / Inbound PO — one small
    /// colour-coded chip per kind with its own name drawn INSIDE the coloured square rather than
    /// beside a separate swatch, per Tad's ask. Built from ChipFill/ChipEdge/ChipText directly so it
    /// can never disagree with what the real grid chips actually look like.</summary>
    private VisualElement BuildScheduleLegend()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.flexShrink = 0;

        row.Add(MakeText("Legend:", 17, Color.white, bold: true)); // was ColSubtleText -- too dim to read per Tad
        row.Add(BuildScheduleLegendChip(AppointmentKind.Outbound, "Recurring"));
        row.Add(BuildScheduleLegendChip(AppointmentKind.Bulk, "Bulk"));
        row.Add(BuildScheduleLegendChip(AppointmentKind.Inbound, "Inbound PO"));
        return row;
    }

    private VisualElement BuildScheduleLegendChip(AppointmentKind kind, string label)
    {
        var chip = MakeText(label, 11, ChipText(kind), bold: true);
        chip.style.unityTextAlign = TextAnchor.MiddleCenter;
        chip.style.whiteSpace = WhiteSpace.NoWrap;
        chip.style.marginLeft = 8;
        chip.style.paddingTop = 3; chip.style.paddingBottom = 3;
        chip.style.paddingLeft = 8; chip.style.paddingRight = 8;
        chip.style.backgroundColor = new StyleColor(ChipFill(kind));
        chip.style.borderTopWidth = chip.style.borderBottomWidth =
            chip.style.borderLeftWidth = chip.style.borderRightWidth = 1;
        chip.style.borderTopColor = chip.style.borderBottomColor =
            chip.style.borderLeftColor = chip.style.borderRightColor = new StyleColor(ChipEdge(kind));
        chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius =
            chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 4;
        return chip;
    }

    /// <summary>
    /// One booked trailer. Locked ones — elapsed, already loaded, or an inbound PO's note — render as
    /// flat history: dimmed, no selection border, no click handler at all. They used to look and behave
    /// exactly like a live booking, so a closed-out load could be picked up and rescheduled into the
    /// future, which both faked the calendar and consumed a door a real trailer needed.
    /// </summary>


    /// <summary>
    /// Why an outbound trailer is standing empty, in the player's terms.
    ///
    /// A recurring contract raises its order at its own CutoffHour, so a trailer booked earlier in the
    /// day genuinely has nothing to carry yet — and that is invisible from the grid, which is what
    /// makes an empty Work Queue look like a fault instead of a schedule. Naming the hour turns it
    /// into a fact the player can plan around.
    ///
    /// Falls back to a plain statement when the contract can't be resolved (a Dev Console order, or a
    /// runtime-generated offer that didn't survive a reload) rather than inventing an hour.
    /// </summary>
    private static string ArrivalHintFor(DockAppointment appt, OrderArrivalService arrivals)
    {
        var contract = arrivals != null && !string.IsNullOrEmpty(appt.ContractId)
            ? arrivals.GetContract(appt.ContractId)
            : null;

        if (contract == null) return "no orders on it yet";

        if (contract.Kind == ContractKind.Recurring)
            return $"no orders on it yet — {appt.CustomerName}'s order is raised at {contract.CutoffHour:00}:00";

        return "no orders on it yet — waiting on the order to be raised";
    }

    /// <summary>
    /// Draws a diagonal line corner-to-corner through a chip to mark it finished — per Tad, a plain
    /// horizontal strikethrough read too much like a text-editing mark; a diagonal reads as "closed
    /// out" the way a cancelled stamp or a crossed-off ticket does.
    ///
    /// A real overlay element rather than a rich-text strikethrough tag on the label: the chip is an
    /// icon, a name and a door number, so a tag would only cross out the middle one and leave the rest
    /// standing. The line has to span the whole chip to read as "this trailer is done".
    ///
    /// The exact length and angle depend on the chip's own resolved size, which isn't known until
    /// layout runs (chips are absolutely positioned at a percentage width of the lane), so both are
    /// computed from the chip's geometry once it resolves — via Pythagoras/atan2, so the line always
    /// reaches exactly corner-to-corner regardless of how wide a block happens to render — and
    /// reapplied on every later GeometryChangedEvent (panel resize, block-count zoom change).
    ///
    /// Absolutely positioned and PickingMode.Ignore so it neither takes part in the chip's row layout
    /// nor swallows the click that selects/moves it. Added last so it draws over the contents.
    /// </summary>
    private static void AddStrikeThrough(VisualElement chip, Color ink)
    {
        var line = new VisualElement();
        line.pickingMode = PickingMode.Ignore;
        line.style.position = Position.Absolute;
        line.style.height = 2;
        // Opaque even though the finished chip as a whole is dimmed — the strike is the signal, and a
        // faded line on a faded chip is what made "done" hard to spot in the first place.
        line.style.backgroundColor = new StyleColor(new Color(ink.r, ink.g, ink.b, 0.95f));
        chip.Add(line);

        void ApplyDiagonal(GeometryChangedEvent evt)
        {
            float w = chip.resolvedStyle.width;
            float h = chip.resolvedStyle.height;
            if (w <= 0f || h <= 0f) return;

            float length = Mathf.Sqrt(w * w + h * h);
            float angleDeg = Mathf.Atan2(h, w) * Mathf.Rad2Deg;

            line.style.width = length;
            // Centre the (now longer than the chip) line on the chip's own centre before rotating —
            // rotation pivots around the element's own centre by default, so this is what lands the
            // ends exactly on the chip's corners instead of somewhere off to one side.
            line.style.left = (w - length) / 2f;
            line.style.top = h / 2f - 1f;
            line.style.rotate = new Rotate(new Angle(angleDeg, AngleUnit.Degree));
        }
        chip.RegisterCallback<GeometryChangedEvent>(ApplyDiagonal);
    }

    /// <summary>
    /// Small square corner flag marking a chip as "this customer/vendor has already been burned once"
    /// — DockAppointment.WasLate. Deliberately NOT the diagonal strike (see AddStrikeThrough): the
    /// strike means "this slot's capacity is gone / nothing to do here", which is wrong the moment the
    /// trailer is back on a live future slot. This badge means the opposite kind of thing — a fact
    /// about the relationship, not the block — so it has to keep showing precisely when the strike
    /// stops.
    ///
    /// Pinned to the bottom-left corner (Position.Absolute, PickingMode.Ignore so it can't steal the
    /// chip's own click) rather than taking a line in the chip's own three-line text stack, which is
    /// already tight (see BuildNewSchedulerCell) and would have to fight for space on every chip
    /// instead of just the minority that are actually late.
    /// </summary>
    private void AddLateBadge(VisualElement chip)
    {
        var badge = new VisualElement();
        badge.pickingMode = PickingMode.Ignore;
        badge.style.position = Position.Absolute;
        badge.style.left = 2; badge.style.bottom = 2;
        badge.style.paddingLeft = 3; badge.style.paddingRight = 3;
        badge.style.paddingTop = 1; badge.style.paddingBottom = 1;
        badge.style.backgroundColor = new StyleColor(new Color(0.13f, 0.62f, 0.24f, 1f));
        badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius =
            badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 2;

        var label = MakeText("LATE", 9, ColDanger, bold: true);
        label.style.paddingTop = 0; label.style.paddingBottom = 0;
        label.style.marginTop = 0; label.style.marginBottom = 0;
        badge.Add(label);

        chip.Add(badge);
    }

    /// <summary>
    /// The tooltip half of the "LATE" corner badge — what the player sees when they actually hover the
    /// chip to find out what happened. Two completely different consequences share the one WasLate
    /// flag (see DockAppointment), so this picks whichever one is non-zero: a real dollar late fee for
    /// an outbound customer order, or a vendor-partnership hit for an inbound PO whose driver never
    /// showed. Per Tad's explicit call, the vendor case is reputation-only — it does NOT invent a
    /// dollar figure for a delivery that was never actually billed in money.
    /// </summary>
    private void AddLatePenaltyTooltipLine(VisualElement col, DockAppointment appt)
    {
        if (!appt.WasLate) return;

        string text = appt.LateFineAmount > 0
            ? $"Late Penalty: ${appt.LateFineAmount:N0}"
            : $"Late Penalty: -{appt.LateRelationshipPenalty} relationship";

        var label = MakeText(text, 14, ColDanger, bold: true);
        label.style.marginBottom = 4;
        col.Add(label);
    }

    /// <summary>Same live-status-override idea as PurchasingPanel.LiveDockStatus — a PO's trailer
    /// parked in the SideLot waiting for a door doesn't show up anywhere else on this tooltip
    /// (ShipmentData.Status stays InTransit/Receiving the whole time), so it's found the same way:
    /// by matching the live TruckController carrying this PO.</summary>
    private void AddSideLotTooltipLine(VisualElement col, string poNumber)
    {
        if (string.IsNullOrEmpty(poNumber)) return;

        var trucks = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None);
        foreach (var truck in trucks)
        {
            if (truck == null || truck.AssignedShipment == null) continue;
            if (truck.AssignedShipment.PONumber != poNumber) continue;
            if (!truck.IsInSideLot) continue;

            var label = MakeText("PARKED · Side Lot", 14, ColDanger, bold: true);
            label.style.marginBottom = 4;
            col.Add(label);
            return;
        }
    }

    /// <summary>True for a purchase order the player raised — an inbound appointment carrying a PO
    /// number, as opposed to BookInboundNow's note that a truck is currently at a door.</summary>
    private static bool IsInboundPo(DockAppointment appt)
        => appt != null && appt.Kind == AppointmentKind.Inbound
        && !string.IsNullOrEmpty(appt.ShipmentPoNumber);

    /// <summary>What a grid chip calls itself. Outbound trailers are identified by the CUSTOMER whose
    /// freight they carry; inbound ones by what they are, since the supplier is the same every time.
    /// A bare BookInboundNow note keeps the supplier name — that one really is "whose truck is at the
    /// door right now", and there's no PO to name it by.</summary>


    /// <summary>How a trailer is named in a TOAST. Same rule as the chip, but with room for the PO
    /// number — a message saying "Wholesale Supply's trailer is off the grid" names the one thing
    /// every purchase order has in common, and reads as though a customer were involved.</summary>
    private static string AppointmentDisplayName(DockAppointment appt)
        => IsInboundPo(appt) ? $"Inbound PO {appt.ShipmentPoNumber}" : appt.CustomerName;



    private void OnChipClicked(DockAppointment appt)
    {
        // Click the selected chip again to put it down — otherwise there's no way to abandon a move
        // without changing tabs.
        _selectedAppointmentId = _selectedAppointmentId == appt.Id ? null : appt.Id;
        _selectedUnscheduledKey = null; // one thing in hand at a time
        Rebuild();
    }

    /// <summary>
    /// Sends the held trailer back to the unscheduled pool — DockScheduleService.TryPark, which keeps
    /// the appointment alive rather than deleting it. The mirror image of picking a pool box up and
    /// placing it in the grid, and the thing that makes shuffling doors around possible: the trailer
    /// stays in the pool as its own box until the player puts it back down.
    ///
    /// This used to call Cancel and let the pool re-derive a box from the appointment's orders. That
    /// silently destroyed any trailer with no orders on it yet — every pre-booked recurring trailer —
    /// because the pool is derived from orders and there were none to derive from. See TryPark.
    ///
    /// The selection is deliberately KEPT when parking succeeds: the player almost always parks a
    /// trailer in order to put it somewhere else, so it stays in hand and the very next slot click
    /// places it. Clearing it would mean picking the same trailer up again for no reason.
    /// </summary>
    private void OnReturnAppointmentToPoolClicked()
    {
        var schedule = Schedule();
        if (schedule == null || _selectedAppointmentId == null) return;

        var appt = schedule.FindById(_selectedAppointmentId);
        if (appt == null) { _selectedAppointmentId = null; Rebuild(); return; }

        // Captured before parking, and phrased without a possessive: "Inbound PO 837194's trailer" is
        // clumsy, and plenty of company names already end in "s".
        string who = AppointmentDisplayName(appt);
        if (!schedule.TryPark(appt.Id, out string why))
        {
            _selectedAppointmentId = null;
            UIToast.Show(why);
            Rebuild();
            return;
        }

        UIToast.Show($"{who} is off the grid and waiting in the pool — " +
                     $"click an open slot to put it back.");
        Rebuild();
    }

    private void OnSlotClicked(int block, int doorNumber)
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

            // The warning goes BEFORE the booking, not after — a checkpoint the player passes having
            // already spent the money isn't a checkpoint. Everything past this point is deferred into
            // the callback, which runs immediately when the player has turned the prompt off.
            // No existing appointment to consult — booking a stranded group creates a fresh one, which
            // by definition hasn't been penalized yet.
            ConfirmOffSlotIfNeeded(schedule, group.ContractId, group.CustomerName, block, null, () =>
            {
                if (!schedule.TryBookGroupAtDoor(_scheduleDay, block, doorNumber, group, out var booked, out string bookWhy))
                {
                    UIToast.Show(bookWhy);
                    return;
                }

                AnnounceSlotResult(schedule, booked, block, doorNumber);
                _selectedUnscheduledKey = null;
                Rebuild();
            });
            return;
        }

        if (_selectedAppointmentId == null) return;

        // Captured now: the field is cleared inside the callback, which may run after the player has
        // answered a dialog, by which time reading it again would be reading null.
        string apptId = _selectedAppointmentId;
        var moving = schedule.FindById(apptId);
        if (moving == null) return;

        ConfirmOffSlotIfNeeded(schedule, moving.ContractId, moving.CustomerName, block, moving, () =>
        {
            if (!schedule.TryMoveToDoor(apptId, _scheduleDay, block, doorNumber, out string why))
            {
                UIToast.Show(why);
                return;
            }

            AnnounceSlotResult(schedule, schedule.FindById(apptId), block, doorNumber);
            _selectedAppointmentId = null;
            Rebuild();
        });
    }

    /// <summary>
    /// Puts the off-slot warning up only when this landing will ACTUALLY cost something; otherwise runs
    /// the action straight through. One place decides that, so the pool-booking path and the move path
    /// can't end up with different ideas about when a warning is warranted.
    ///
    /// Two conditions, and the second matters as much as the first: the destination has to miss the
    /// customer's hour, AND this trailer must not already have been penalized for that
    /// (DockAppointment.OffSlotPenaltyApplied). A dialog quoting a fine and a satisfaction hit that
    /// the code then declines to charge would train the player to distrust it — and since the penalty
    /// is once per trailer, every move after the first is exactly that case. Shuffling doors on an
    /// already-penalized trailer stays silent, which is the behaviour a player shuffling doors wants.
    /// </summary>
    private void ConfirmOffSlotIfNeeded(DockScheduleService schedule, string contractId,
                                        string customerName, int block, DockAppointment existing,
                                        System.Action onConfirmed)
    {
        bool alreadyPaid = existing != null && existing.OffSlotPenaltyApplied;
        if (alreadyPaid) { onConfirmed(); return; }

        // The DAY counts too, not just the hour. A trailer dragged to the right hour on the wrong day
        // used to slip through silently — WouldMissRequestedSlot only ever compared blocks — which is
        // the single biggest thing a customer would actually notice.
        int requestedDay = existing != null ? existing.RequestedDay : _scheduleDay;
        int cost = schedule.PredictOffSlotReputationCost(contractId, requestedDay, _scheduleDay, block);
        if (cost <= 0 && !schedule.WouldMissRequestedSlot(contractId, block)) { onConfirmed(); return; }

        schedule.TryGetRequestedBlock(contractId, out int wanted);
        string requestedLabel = requestedDay > 0 && requestedDay != _scheduleDay
            ? $"{DockScheduleService.BlockLabel(wanted)} on day {requestedDay}"
            : DockScheduleService.BlockLabel(wanted);

        ConfirmOffSlotMove(customerName, requestedLabel,
                           $"{DockScheduleService.BlockLabel(block)} on day {_scheduleDay}", onConfirmed,
                           cost, schedule.PredictDescribeOffSlot(contractId, requestedDay, _scheduleDay, block));
    }

    /// <summary>
    /// Applies and reports what a landing actually cost — same for a booking out of the pool and a move
    /// on the grid, because from the player's seat they're the same event: a trailer just landed
    /// somewhere. A plain confirmation when it landed on the customer's own requested hour; the price
    /// the confirmation dialog quoted when it didn't.
    ///
    /// BOTH halves of that price are charged here, which is the half that used to be missing: a
    /// satisfaction hit AND a fine, per the terms the player was just shown. The fine goes through
    /// OrderService.FineMovedOffRequestedSlot, which shares HasBeenFined with the deadline sweeps, so
    /// an order can't be billed twice for one broken promise about when its freight moves.
    ///
    /// An appointment with no orders on it yet — a pre-booked recurring trailer — takes the
    /// satisfaction hit but no fine: there is nothing to bill a percentage of. The order that lands on
    /// it later is then judged by its own deadline like any other, which is the honest outcome; the
    /// player was warned, and the account is already carrying the mark.
    /// </summary>
    private void AnnounceSlotResult(DockScheduleService schedule, DockAppointment appt, int block, int doorNumber)
    {
        if (appt == null) return;

        AudioManager.Play("UIClick");

        // ONE penalty per trailer, decided in one place. TryClaimOffSlotPenalty returns true only the
        // first time this trailer is found off its customer's hour, so shuffling it between doors — or
        // between two equally-wrong blocks — costs nothing further. Everything below it (satisfaction,
        // fine, floating reaction, toast wording) hangs off that one answer, so the three can't
        // disagree about whether this landing actually cost anything.
        if (!schedule.TryClaimOffSlotPenalty(appt))
        {
            UIToast.Show(schedule.MissedRequestedSlot(appt)
                ? $"{AppointmentDisplayName(appt)} moved to {DockScheduleService.BlockLabel(block)}, " +
                  $"door {doorNumber} — still off their slot, but already accounted for. No further charge."
                : $"{AppointmentDisplayName(appt)} booked into {DockScheduleService.BlockLabel(block)}, " +
                  $"door {doorNumber}, on day {_scheduleDay}.");
            return;
        }

        Arrivals()?.PenalizeSatisfaction(appt.ContractId);

        // Reputation, scaled by HOW FAR the trailer sits from what was promised — a one-block nudge is
        // a shrug, a day's slip is a real mark. Satisfaction above is per-account; this is the
        // building-wide score that gates which customers and vendors will deal with you at all, so a
        // trailer moved badly is felt in both places. See DockScheduleService.OffSlotReputationCost.
        int repCost = schedule.OffSlotReputationCost(appt);
        if (repCost > 0 && ServiceLocator.TryGet<ReputationService>(out var rep) && rep != null)
            rep.Add(-repCost, $"{appt.CustomerName} — {schedule.DescribeOffSlot(appt)}");

        int fined = 0;
        int fineTotal = 0;
        if (ServiceLocator.TryGet<OrderService>(out var orders) && orders != null)
        {
            foreach (var order in orders.ActiveOrders.Where(o => o != null && appt.OrderIds.Contains(o.OrderId)).ToList())
            {
                // The charge is a percentage of order value, computed inside OrderService — mirroring
                // that arithmetic here to show a number would be a second copy that eventually
                // disagrees with the money actually taken. Read capital across the call instead, so
                // what floats up is exactly what left the balance.
                long before = _moneyBeforeFine();
                if (!orders.FineMovedOffRequestedSlot(order, appt.StartHour)) continue;
                fined++;
                fineTotal += (int)Mathf.Max(0, before - _moneyBeforeFine());
            }
        }

        // Same "LATE" corner badge as a missed deadline — from the customer's seat, a trailer the
        // player themselves moved off the promised hour is no different from one that simply ran out
        // the clock. See DockScheduleService.SweepElapsedAppointments for the missed-deadline twin.
        if (fineTotal > 0)
        {
            appt.WasLate = true;
            appt.LateFineAmount += fineTotal;
        }

        // Queued rather than played now: the caller rebuilds the grid immediately after this, which
        // destroys the very cell the effect anchors to. See PlayPendingUnhappyFx.
        QueueUnhappyFx(block, doorNumber, fineTotal);

        // The message names the DISTANCE and the reputation cost, not just that something happened —
        // the whole point of scaling the penalty is lost if every landing reports the same sentence.
        string repPart = repCost > 0 ? $", reputation −{repCost}" : "";
        UIToast.Show(fined > 0
            ? $"{appt.CustomerName} — {schedule.DescribeOffSlot(appt)}. {fined} order(s) fined, " +
              $"satisfaction down{repPart}."
            : $"{appt.CustomerName} — {schedule.DescribeOffSlot(appt)}. Satisfaction down{repPart}.");
    }

    /// <summary>Current capital, or 0 when the money service isn't up. Used to measure a fine by what
    /// actually left the balance rather than recomputing the rate.</summary>
    private static long _moneyBeforeFine()
        => ServiceLocator.TryGet<MoneyService>(out var m) && m != null ? m.CurrentCapital : 0L;

    // ── Unhappy-customer reaction ────────────────────────────────────────────

    private int _pendingFxBlock = -1;
    private int _pendingFxDoor;
    private int _pendingFxFine;

    /// <summary>Stable per-cell element name — the grid coordinate, not the object, so it survives the
    /// rebuild that every placement triggers.</summary>
    private static string SlotElementName(int block, int doorNumber) => $"sched-slot-{block}-{doorNumber}";

    /// <summary>Records where the reaction should play. Deferred because AnnounceSlotResult runs
    /// BEFORE the caller's Rebuild(), and the rebuild throws away the cell being pointed at.</summary>
    private void QueueUnhappyFx(int block, int doorNumber, int fineAmount)
    {
        _pendingFxBlock = block;
        _pendingFxDoor = doorNumber;
        _pendingFxFine = fineAmount;
    }

    /// <summary>
    /// Plays any queued reaction over the freshly rebuilt grid cell.
    ///
    /// Called at the end of BuildSchedule, which is the first moment the new cell exists. The lookup
    /// is by name against the rebuilt tree, and the effect is parented to the OVERLAY rather than to
    /// the cell: the grid lives in a ScrollView that clips its children, and a face swelling to 150%
    /// out of a slot would be sliced off at the cell edge.
    ///
    /// Clears the queue unconditionally, hit or miss — a reaction that couldn't find its cell (the
    /// player switched day or tab in the same frame) must not lie in wait and fire over an unrelated
    /// slot the next time the grid is built.
    /// </summary>
    private void PlayPendingUnhappyFx()
    {
        if (_pendingFxBlock < 0) return;

        string name = SlotElementName(_pendingFxBlock, _pendingFxDoor);
        int fine = _pendingFxFine;
        _pendingFxBlock = -1;

        // One frame late: the rows were only just added, so nothing has a resolved worldBound yet and
        // the effect's own anchor read would come back empty.
        _overlay.schedule.Execute(() =>
        {
            var cell = _content?.Q(name);
            if (cell != null) UnhappyCustomerFx.Play(_overlay, cell, fine);
        }).ExecuteLater(16);
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

    /// <summary>Shared by booked chips and stranded ones — both identify a trailer the same way.
    /// Falls back to the VendorRegistry for inbound freight: an inbound appointment's CustomerId is
    /// actually the SUPPLIER's VendorId (see DockScheduleService.ParkInboundForPo), which never
    /// matches a CustomerData, so the customer-catalog lookups above always miss for it. Vendors carry
    /// their own icon for exactly this reason.</summary>
    private Sprite IconForCustomer(string customerId, string contractId, OrderArrivalService arrivals)
    {
        if (arrivals != null)
        {
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
        }

        if (!string.IsNullOrEmpty(customerId))
        {
            var vendor = VendorRegistry.Load()?.GetById(customerId);
            if (vendor != null) return vendor.Icon;
        }

        return null;
    }

    /// <summary>How many whole pallets a stranded customer trailer represents, summed across every
    /// line item of every order in the group. A part pallet still takes a whole one (see
    /// TrailerCapacity.PalletsFor) — this is "how many pallet positions this trailer needs", not a
    /// case count.</summary>
    private static int PalletCountForGroup(UnscheduledGroup group)
    {
        if (group == null || group.OrderIds.Count == 0) return 0;
        if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null) return 0;

        int pallets = 0;
        foreach (var orderId in group.OrderIds)
        {
            var order = orders.ActiveOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null) continue;

            foreach (var li in order.LineItems)
            {
                int fullPallet = orders.FullPalletCases(li.SkuId);
                if (fullPallet <= 0) continue;
                pallets += Mathf.CeilToInt(li.QuantityNeeded / (float)fullPallet);
            }
        }
        return pallets;
    }

    /// <summary>How many pallets an inbound PO's trailer is carrying. A player-raised PO builds one
    /// ShipmentLineItem per pallet (see PurchasingPanel's cart -> TrailerCapacity.PlanLoad path), so
    /// the line count IS the pallet count — no per-SKU maths needed the way a customer order needs.
    /// Returns -1 if the PO can no longer be found (e.g. already departed), so callers can show
    /// nothing rather than a misleading zero.</summary>
    private static int PalletCountForPO(string poNumber)
    {
        if (string.IsNullOrEmpty(poNumber)) return -1;
        if (!ServiceLocator.TryGet<ShipmentService>(out var shipments) || shipments == null) return -1;

        var shipment = shipments.PendingShipments.FirstOrDefault(s => s.PONumber == poNumber);
        return shipment?.LineItems.Count ?? -1;
    }

    /// <summary>Renders a whole-number pallet count as "N pallet(s)", or an em dash if it couldn't be
    /// determined (PO already departed, order missing) — never a misleading "0".</summary>
    private static string PalletLabel(int pallets)
        => pallets < 0 ? "—" : pallets == 1 ? "1 pallet" : $"{pallets} pallets";

    /// <summary>How many pallets a booked appointment's trailer represents — the same question
    /// PalletCountForGroup/PalletCountForPO answer for a pool box, asked of a live DockAppointment
    /// instead, for the grid chip's own "Pallets: N" line.</summary>
    private static int PalletCountForAppointment(DockAppointment appt)
    {
        if (appt == null) return 0;
        if (IsInboundPo(appt)) return PalletCountForPO(appt.ShipmentPoNumber);

        if (appt.OrderIds.Count == 0 || !ServiceLocator.TryGet<OrderService>(out var orders) || orders == null) return 0;

        int pallets = 0;
        foreach (var orderId in appt.OrderIds)
        {
            var order = orders.ActiveOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null) continue;

            foreach (var li in order.LineItems)
            {
                int fullPallet = orders.FullPalletCases(li.SkuId);
                if (fullPallet <= 0) continue;
                pallets += Mathf.CeilToInt(li.QuantityNeeded / (float)fullPallet);
            }
        }
        return pallets;
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

    /// <summary>Same trip as OpenPurchasing, to the outbound Orders/Contracts screen instead.</summary>
    private void OpenOrders()
    {
        Hide();

        var topBar = UnityEngine.Object.FindAnyObjectByType<TopBarUI>();
        var orders = topBar != null ? topBar.ContractsPanel : null;
        if (orders == null)
        {
            UIToast.Show("Couldn't open orders — the panel isn't loaded.");
            return;
        }

        UIKeyBindingManager.Instance?.CloseAll();
        orders.Show();
    }

    /// <summary>Shared styling for the title-bar cross-link buttons (ORDERS / PURCHASING).</summary>
    private static void StyleCrossLinkButton(Button b)
    {
        ApplyFont(b, bold: true, size: 16);
        b.style.height = 48f;
        b.style.paddingLeft = b.style.paddingRight = 18;
        b.style.marginRight = 10;
        b.style.flexShrink = 0;   // the title flexGrows; without this the label squeezes
        b.style.color = new StyleColor(ColOrangeText);
        b.style.backgroundColor = new StyleColor(ColOrange);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(ColOrangeEdge);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = new StyleColor(ColOrangeHover));
        b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(ColOrange));
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
    // Continuous 24h timeline mockup Tad supplied, built alongside (not replacing) the Schedule
    // tab above. Reuses that tab's data model and interactions wholesale (DockScheduleService,
    // the pool boxes, OnChipClicked/OnSlotClicked, ResolvePoOrderDetails) — only the grid's visual
    // treatment and the PO/Order Details card are new.

    private const float NewSchedulerDoorLabelWidth = 90f;
    private const float NewSchedulerRowHeight = 118f; // was 102 -- grown to fit the chip's new "Pallets: N" bottom line
    private const float NewSchedulerTickerHeight = 26f;
    private const float NewSchedulerBadgeHeight = 29f;
    private const float NewSchedulerBadgeWidth = 74f;

    private VisualElement _newSchedulerPastOverlay;
    private VisualElement _newSchedulerSweepLine;
    private Label _newSchedulerSweepBadge;
    private float _lastNewSchedulerSweepLeft = float.NaN;
    private string _lastNewSchedulerBadgeText;

    private void BuildNewScheduler(OrderArrivalService arrivals)
    {
        var schedule = Schedule();
        if (schedule == null)
        {
            _footerMessage.text = "Dock schedule service isn't running — appointments can't be shown.";
            return;
        }

        if (_selectedAppointmentId != null && schedule.IsLocked(schedule.FindById(_selectedAppointmentId)))
            _selectedAppointmentId = null;

        int today = CurrentDay();
        var doors = schedule.OutboundDoors();

        var unscheduled = schedule.UnscheduledGroups();
        var parked = schedule.ParkedAppointments.ToList();
        _tabHeader.Add(BuildNewSchedulerStrip(unscheduled, parked, today, arrivals));

        if (doors.Count == 0)
        {
            var none = new Label("No outbound doors yet. Place a door and a row of shipping lanes to see the timeline.");
            ApplyFont(none, size: 15);
            none.style.color = new StyleColor(ColSubtleText);
            none.style.whiteSpace = WhiteSpace.Normal;
            none.style.marginTop = 14;
            _content.Add(none);
            _footerMessage.text = "Capacity is 0 — arriving orders stay unscheduled until a door exists.";
            _newSchedulerSweepLine = null;
            _newSchedulerPastOverlay = null;
            _newSchedulerSweepBadge = null;
            return;
        }

        _content.Add(BuildNewSchedulerTimeline(schedule, arrivals, doors, today));
        SyncNewSchedulerSweep();

        if (_selectedUnscheduledKey != null)
            _footerMessage.text = "Pick an empty stretch of a door's row to book this trailer in, or click it again to put it down.";
        else if (_selectedAppointmentId != null)
            _footerMessage.text = "Pick an empty stretch to move the selected appointment, click it again to drop it, " +
                                  "or click the Unscheduled Trailers pool to send it back unbooked.";
        else
            _footerMessage.text = "Click a booked block or a pool trailer to see its load on the right.";
    }

    /// <summary>Mirrors BuildScheduleStrip but with the two halves swapped — pool on the LEFT, PO/
    /// Order Details on the RIGHT — per the mockup, and the details half redrawn after PurchasingPanel's
    /// multi-vendor header (icon, name, stats line, cost of load, truck fill bar) instead of the old
    /// tab's compact SKU table.</summary>
    private VisualElement BuildNewSchedulerStrip(List<UnscheduledGroup> unscheduled, List<DockAppointment> parked,
                                                 int today, OrderArrivalService arrivals)
    {
        var wrapper = new VisualElement();
        wrapper.style.flexDirection = FlexDirection.Column;
        wrapper.style.marginBottom = 6;
        wrapper.style.backgroundColor = new StyleColor(ColStat);
        wrapper.style.borderTopWidth = wrapper.style.borderBottomWidth =
            wrapper.style.borderLeftWidth = wrapper.style.borderRightWidth = 2;
        wrapper.style.borderTopColor = wrapper.style.borderBottomColor =
            wrapper.style.borderLeftColor = wrapper.style.borderRightColor = new StyleColor(ColBorder);
        wrapper.style.borderTopLeftRadius = wrapper.style.borderTopRightRadius =
            wrapper.style.borderBottomLeftRadius = wrapper.style.borderBottomRightRadius = 8;

        bool anyLate = unscheduled.Any(g => g.EarliestDueDay < today) || parked.Any(a => a.Day < today);
        int strandedOrders = unscheduled.Sum(g => g.OrderIds.Count) + parked.Sum(a => a.OrderIds.Count);
        int poolCount = unscheduled.Count + parked.Count;

        var dayRow = new VisualElement();
        dayRow.style.flexDirection = FlexDirection.Row;
        dayRow.style.alignItems = Align.Center;
        dayRow.style.justifyContent = Justify.Center;
        dayRow.style.paddingTop = 6; dayRow.style.paddingBottom = 4;

        var prev = new Button(() => { _scheduleDay = Mathf.Max(today - ScheduleDaysBack, _scheduleDay - 1); AudioManager.Play("SchedulerDayChange"); Rebuild(); }) { text = "◀" };
        StyleSquareButton(prev);
        prev.style.width = 30; prev.style.height = 30;
        dayRow.Add(prev);

        string when = _scheduleDay == today ? "today"
                    : _scheduleDay == today + 1 ? "tomorrow"
                    : _scheduleDay < today ? $"{today - _scheduleDay} day(s) ago"
                    : $"in {_scheduleDay - today} day(s)";
        var dayLabel = MakeText($"Day {_scheduleDay} · {when}", 27, ColChipOutText, bold: true); // 18 * 1.5 per Tad's explicit call
        dayLabel.style.marginLeft = 10; dayLabel.style.marginRight = 10;
        dayRow.Add(dayLabel);

        var next = new Button(() => { _scheduleDay = Mathf.Min(today + ScheduleDaysAhead, _scheduleDay + 1); AudioManager.Play("SchedulerDayChange"); Rebuild(); }) { text = "▶" };
        StyleSquareButton(next);
        next.style.width = 30; next.style.height = 30;
        dayRow.Add(next);

        wrapper.Add(dayRow);

        var captions = new VisualElement();
        captions.style.flexDirection = FlexDirection.Row;
        captions.style.paddingTop = 2;
        captions.style.borderTopWidth = 2;
        captions.style.borderTopColor = new StyleColor(ColBorder);
        captions.style.borderBottomWidth = 2;
        captions.style.borderBottomColor = new StyleColor(ColBorder);

        var poolCaptionHalf = new VisualElement();
        poolCaptionHalf.style.flexBasis = Length.Percent(50);
        poolCaptionHalf.style.flexGrow = 0; poolCaptionHalf.style.flexShrink = 0;
        poolCaptionHalf.style.alignItems = Align.Center;
        poolCaptionHalf.Add(MakeStripCaption(
            poolCount == 0 ? "UNSCHEDULED TRAILERS" : $"UNSCHEDULED TRAILERS — {poolCount} ({strandedOrders} order(s))",
            anyLate ? ColDangerSoft : Color.white));
        captions.Add(poolCaptionHalf);

        var detailsCaptionHalf = new VisualElement();
        detailsCaptionHalf.style.flexBasis = Length.Percent(50);
        detailsCaptionHalf.style.flexGrow = 0; detailsCaptionHalf.style.flexShrink = 0;
        detailsCaptionHalf.style.alignItems = Align.Center;
        detailsCaptionHalf.Add(MakeStripCaption("PO/ORDER DETAILS", Color.white));
        captions.Add(detailsCaptionHalf);

        wrapper.Add(captions);

        var halves = new VisualElement();
        halves.style.flexDirection = FlexDirection.Row;
        halves.style.alignItems = Align.Stretch;

        var left = new VisualElement();
        left.style.flexBasis = Length.Percent(50);
        left.style.flexGrow = 0; left.style.flexShrink = 0;
        left.style.justifyContent = Justify.Center;
        left.style.paddingTop = 6; left.style.paddingBottom = 6;
        left.style.paddingLeft = 14; left.style.paddingRight = 14;
        left.style.borderRightWidth = 2;
        left.style.borderRightColor = new StyleColor(ColBorder);

        var heldAppt = _selectedAppointmentId != null ? Schedule()?.FindById(_selectedAppointmentId) : null;
        bool returningAppointment = heldAppt != null && !heldAppt.Parked;
        if (returningAppointment)
        {
            left.style.backgroundColor = new StyleColor(new Color(ColOrange.r, ColOrange.g, ColOrange.b, 0.12f));
            left.RegisterCallback<ClickEvent>(_ => OnReturnAppointmentToPoolClicked());
            left.RegisterCallback<MouseEnterEvent>(_ => CustomCursorService.SetHoveringInteractable(true));
            left.RegisterCallback<MouseLeaveEvent>(_ => CustomCursorService.SetHoveringInteractable(false));
        }

        if (poolCount == 0)
        {
            var clear = MakeText(returningAppointment
                                     ? "Click here to pull that trailer off the grid — it'll wait here."
                                     : "Every trailer has a door. Nothing waiting.",
                                 23, returningAppointment ? ColOrangeText : Color.white); // 18 * 1.25 per Tad's explicit call
            clear.style.unityTextAlign = TextAnchor.MiddleCenter;
            clear.style.whiteSpace = WhiteSpace.Normal;
            left.Add(clear);
        }
        else
        {
            var boxes = new VisualElement();
            boxes.style.flexDirection = FlexDirection.Row;
            boxes.style.flexWrap = Wrap.Wrap;
            boxes.style.alignItems = Align.Center;

            int shown = 0;
            foreach (var appt in parked)
            {
                if (shown++ >= PoolMaxBoxes) break;
                boxes.Add(BuildParkedBox(appt, today));
            }
            foreach (var group in unscheduled)
            {
                if (shown++ >= PoolMaxBoxes) break;
                boxes.Add(BuildPoolBox(group, today, arrivals));
            }

            if (poolCount > PoolMaxBoxes)
            {
                var more = MakeText($"+{poolCount - PoolMaxBoxes} more", 15, ColSubtleText, bold: true);
                more.style.marginLeft = 2;
                more.style.marginBottom = 6;
                boxes.Add(more);
            }
            left.Add(boxes);

            bool holdingParked = heldAppt != null && heldAppt.Parked;
            string hintText = returningAppointment
                ? "Click an empty spot here to pull that trailer off the grid — it'll wait here."
                : holdingParked
                    ? "Now click an open stretch in the timeline to put it back — or click the box again to let go."
                    : _selectedUnscheduledKey != null
                        ? "Now click an open stretch in the timeline — or click the box again to put it down."
                        : "Click a box, then click an open stretch in the timeline to book it.";
            var hint = MakeText(hintText, 20,
                                returningAppointment || holdingParked || _selectedUnscheduledKey != null
                                    ? ColOrangeText : ColSubtleText);
            hint.style.marginTop = 2;
            left.Add(hint);
        }

        halves.Add(left);

        var right = new VisualElement();
        right.style.flexBasis = Length.Percent(50);
        right.style.flexGrow = 0; right.style.flexShrink = 0;
        right.style.justifyContent = Justify.Center;
        right.style.paddingTop = 6; right.style.paddingBottom = 6;
        right.style.paddingLeft = 14; right.style.paddingRight = 14;
        right.Add(BuildTruckOrderDetailsCard(unscheduled));

        halves.Add(right);
        wrapper.Add(halves);

        return wrapper;
    }

    /// <summary>The PO/Order Details card — copied from PurchasingPanel's multi-vendor group header
    /// (icon, name, case/pallet/critical stats line, cost of load, truck fill bar), compressed to a
    /// single row with no DISPATCH/DEALS buttons since this card is read-only. "Critical" here means
    /// a line item where current on-hand stock can't cover what's needed — the same shortage idea
    /// PurchasingPanel.CountCriticalItems uses for vendor SKUs, adapted to an order's line items.</summary>
    private VisualElement BuildTruckOrderDetailsCard(List<UnscheduledGroup> unscheduled)
    {
        var (title, lines) = ResolvePoOrderDetails(unscheduled);

        var card = new VisualElement();
        card.style.flexDirection = FlexDirection.Row;
        card.style.alignItems = Align.Center;
        card.style.width = Length.Percent(100);

        if (string.IsNullOrEmpty(title) || lines == null || lines.Count == 0)
        {
            var empty = MakeText("Click a trailer on the timeline, or a box in the pool, to see its load here.",
                                 18, Color.white); // set to 18 per Tad's explicit call
            empty.style.whiteSpace = WhiteSpace.Normal;
            card.Add(empty);

            // Fills the rest of this otherwise-empty row (only shown while nothing's selected) with
            // what each chip colour on the grid means. BuildCompactLegend/MakeLegendRow already existed
            // for a legend but were never actually wired into a row — see their doc comments — so this
            // is a fresh one built to read ChipFill/ChipEdge/ChipText directly rather than resurrecting
            // either, which keeps it impossible to drift out of sync with the real timeline chips.
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; card.Add(spacer);
            card.Add(BuildScheduleLegend());
            return card;
        }

        Sprite icon = null;
        var appt = _selectedAppointmentId != null ? Schedule()?.FindById(_selectedAppointmentId) : null;
        if (appt != null) icon = IconForAppointment(appt, Arrivals());
        else if (_selectedUnscheduledKey != null)
        {
            var group = unscheduled.FirstOrDefault(g => g.Key == _selectedUnscheduledKey);
            if (group != null) icon = IconForCustomer(group.CustomerId, group.ContractId, Arrivals());
        }

        card.Add(MakeIcon(icon, 48, 8, marginRight: 10));

        var textCol = new VisualElement();
        textCol.style.flexGrow = 1;
        textCol.style.flexShrink = 1;
        textCol.style.overflow = Overflow.Hidden;

        var nameLabel = MakeText(title, 20, ColTitleText, bold: true);
        nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
        nameLabel.style.overflow = Overflow.Hidden;
        textCol.Add(nameLabel);

        var (cases, pallets, critical, cost) = SummarizeOrderLines(lines);

        var statsLabel = MakeText($"{cases:N0} case(s) · {pallets:N0} pallet(s) · [{critical}] critical items",
                                  14, ColSubtleText);
        statsLabel.style.whiteSpace = WhiteSpace.NoWrap;
        textCol.Add(statsLabel);

        card.Add(textCol);

        var costBox = new VisualElement();
        costBox.style.alignItems = Align.Center;
        costBox.style.marginLeft = 8;
        costBox.style.flexShrink = 0;
        var costLabel = MakeText($"${cost:N0}", 23, ColMoney, bold: true);
        costLabel.style.whiteSpace = WhiteSpace.NoWrap;
        costBox.Add(costLabel);
        var costCaption = MakeText("Cost of Load", 12, ColMoney, bold: true);
        costCaption.style.whiteSpace = WhiteSpace.NoWrap;
        costBox.Add(costCaption);
        card.Add(costBox);

        var fillBar = PurchasingPanel.BuildTruckFillBar(out var fillElement);
        fillBar.style.marginLeft = 8;
        fillBar.style.flexShrink = 0;
        fillElement.style.width = Mathf.Clamp01(pallets / (float)TruckController.PalletSlotCount) *
            (PurchasingPanel.TruckBoxRightFrac - PurchasingPanel.TruckBoxLeftFrac) * PurchasingPanel.TruckFillBarWidth;
        card.Add(fillBar);

        return card;
    }

    /// <summary>Cases/pallets/critical-count/cost across a resolved PO/Order Details line list —
    /// shared by the details card and (for critical) nothing else yet, but kept general. Pallets use
    /// the same FullPalletCases packing PalletCountForGroup already uses; "critical" is a line where
    /// on-hand stock can't cover the quantity needed.</summary>
    private (int cases, int pallets, int critical, int cost) SummarizeOrderLines(
        List<(string sku, string desc, int qty)> lines)
    {
        if (lines == null || lines.Count == 0) return (0, 0, 0, 0);

        ServiceLocator.TryGet<InventoryService>(out var inv);
        ServiceLocator.TryGet<OrderService>(out var orders);

        int cases = 0, pallets = 0, critical = 0;
        float cost = 0f;
        foreach (var line in lines)
        {
            cases += line.qty;

            int onHand = inv != null ? inv.GetTotalUnitsBySku(line.sku) : 0;
            if (onHand < line.qty) critical++;

            int fullPallet = orders != null ? orders.FullPalletCases(line.sku) : 0;
            if (fullPallet > 0) pallets += Mathf.CeilToInt(line.qty / (float)fullPallet);

            var sku = inv != null ? inv.GetSkuData(line.sku) : null;
            if (sku != null) cost += line.qty * sku.SellValue;
        }
        return (cases, pallets, critical, Mathf.RoundToInt(cost));
    }

    /// <summary>The ticker + faint hour grid + door rows + sweep line. Absolutely-positioned over
    /// percentage-of-day (0–1440 minutes → 0–100%) rather than the old tab's per-block flex columns,
    /// so a 2-hour appointment reads as a block spanning 1/12th of the width at its true time of day.</summary>
private VisualElement BuildNewSchedulerTimeline(DockScheduleService schedule, OrderArrivalService arrivals,
                                                 List<int> doors, int today)
{
    var root = new VisualElement();
    root.style.position = Position.Relative;
    root.style.marginTop = NewSchedulerBadgeHeight + 4;

    var ticker = new VisualElement();
    ticker.style.flexDirection = FlexDirection.Row;
    ticker.style.height = NewSchedulerTickerHeight;
    ticker.style.marginLeft = NewSchedulerDoorLabelWidth;
    ticker.style.backgroundColor = new StyleColor(ColStat);
    ticker.style.borderTopWidth = 2; ticker.style.borderTopColor = new StyleColor(ColBorder);
    ticker.style.borderBottomWidth = 2; ticker.style.borderBottomColor = new StyleColor(ColBorder);

    for (int h = 0; h < 24; h++)
    {
        var cell = new VisualElement();
        cell.style.flexBasis = 0; cell.style.flexGrow = 1;
        cell.style.position = Position.Relative;
        cell.style.justifyContent = Justify.Center;
        cell.style.overflow = Overflow.Hidden;

        var hourTick = new VisualElement();
        hourTick.style.position = Position.Absolute;
        hourTick.style.left = 0; hourTick.style.top = 0; hourTick.style.bottom = 0;
        hourTick.style.width = 1;
        hourTick.style.backgroundColor = new StyleColor(ColBlueEdge);
        cell.Add(hourTick);

        var halfTick = new VisualElement();
        halfTick.style.position = Position.Absolute;
        halfTick.style.left = Length.Percent(50);
        halfTick.style.top = Length.Percent(55);
        halfTick.style.bottom = 0;
        halfTick.style.width = 1;
        halfTick.style.backgroundColor = new StyleColor(new Color(ColBlueEdge.r, ColBlueEdge.g, ColBlueEdge.b, 0.5f));
        cell.Add(halfTick);

        var label = MakeText($"{h:00}:00", 15, Color.white, bold: true); // 10 * 1.5 per Tad's explicit call
        label.style.marginLeft = 3;
        // Default Label padding/margin pushed this past the ticker's 26px band and clipped the bottom
        // border once the font grew -- zero it out so justifyContent:Center on the cell actually
        // centers the text between the top/bottom blue border lines instead of overflowing past them.
        label.style.marginTop = 0; label.style.marginBottom = 0;
        label.style.paddingTop = 0; label.style.paddingBottom = 0;
        cell.Add(label);

        ticker.Add(cell);
    }
    root.Add(ticker);

    var gridWrap = new VisualElement();
    gridWrap.style.position = Position.Relative;
    // Trying opaque per Tad's request -- the schedule grid used to have no background at all here,
    // letting the live warehouse view show straight through behind every door row. ColBg at full
    // alpha (vs. the panel chrome's near-opaque 0.97) makes it a solid dark rectangle instead.
    gridWrap.style.backgroundColor = new StyleColor(new Color(ColBg.r, ColBg.g, ColBg.b, 1f));
    root.Add(gridWrap);

    var laneOverlay = new VisualElement();
    laneOverlay.style.position = Position.Absolute;
    laneOverlay.style.left = NewSchedulerDoorLabelWidth; laneOverlay.style.right = 0;
    laneOverlay.style.top = 0; laneOverlay.style.bottom = 0;
    laneOverlay.pickingMode = PickingMode.Ignore;
    gridWrap.Add(laneOverlay);
    for (int h = 1; h < 24; h++)
    {
        var line = new VisualElement();
        line.style.position = Position.Absolute;
        line.style.left = Length.Percent(h / 24f * 100f);
        line.style.top = 0; line.style.bottom = 0;
        line.style.width = 1;
        line.style.backgroundColor = new StyleColor(new Color(ColBlueEdge.r, ColBlueEdge.g, ColBlueEdge.b, 0.28f));
        laneOverlay.Add(line);
    }

    foreach (int doorNumber in doors)
        gridWrap.Add(BuildNewSchedulerDoorRow(schedule, arrivals, doorNumber, today));

    // The sweep line/badge only mean anything on the day the in-game clock is actually inside of —
    // paging to a future or past day and still showing "now" at some x position would be lying about
    // where the clock is. Only built when the tab is looking at today; otherwise left null, which is
    // what SyncNewSchedulerSweep already treats as "nothing to sync" (same guard the no-doors branch
    // of BuildNewScheduler uses).
    if (_scheduleDay == today)
    {
        // Everything BEFORE now on today's timeline, tinted red so a glance says "that time is
        // gone" — lives in laneOverlay (created above, in scope here) so it sits behind the door
        // rows' own appointment cells rather than covering them.
        var pastOverlay = new VisualElement();
        pastOverlay.style.position = Position.Absolute;
        pastOverlay.style.left = 0;
        pastOverlay.style.top = 0; pastOverlay.style.bottom = 0;
        pastOverlay.style.width = 0; // set below by SyncNewSchedulerSweep, same % as the sweep line
        pastOverlay.style.backgroundColor = new StyleColor(new Color(ColDanger.r, ColDanger.g, ColDanger.b, 0.25f));
        pastOverlay.pickingMode = PickingMode.Ignore;
        laneOverlay.Add(pastOverlay);
        _newSchedulerPastOverlay = pastOverlay;

        var sweepHost = new VisualElement();
        sweepHost.style.position = Position.Absolute;
        sweepHost.style.left = NewSchedulerDoorLabelWidth; sweepHost.style.right = 0;
        sweepHost.style.top = -(NewSchedulerBadgeHeight + 4); sweepHost.style.bottom = 0;
        sweepHost.pickingMode = PickingMode.Ignore;
        root.Add(sweepHost);

        var sweepColor = new Color(0xF5 / 255f, 0xC7 / 255f, 0x3C / 255f, 1f);

        var line2 = new VisualElement();
        line2.style.position = Position.Absolute;
        line2.style.top = NewSchedulerBadgeHeight; line2.style.bottom = 0;
        line2.style.width = 2;
        line2.style.marginLeft = -1;
        line2.style.backgroundColor = new StyleColor(sweepColor);
        sweepHost.Add(line2);
        _newSchedulerSweepLine = line2;

        var badge = new Label();
        ApplyFont(badge, bold: true, size: 23); // 18 * 1.25 per Tad's explicit call
        badge.style.position = Position.Absolute;
        badge.style.top = 0;
        badge.style.height = NewSchedulerBadgeHeight;
        badge.style.width = NewSchedulerBadgeWidth;
        badge.style.marginLeft = -(NewSchedulerBadgeWidth / 2f);
        badge.style.backgroundColor = new StyleColor(sweepColor);
        badge.style.color = new StyleColor(new Color(0.14f, 0.10f, 0.02f, 1f));
        badge.style.unityTextAlign = TextAnchor.MiddleCenter;
        // FontStyle.Bold alone doesn't thicken this label -- Nunito Sans is loaded as a single-weight
        // variable TTF with no dedicated bold face, so Unity has nothing heavier to synthesize. A thin
        // text outline in the same ink colour fakes the extra stroke weight Tad's after.
        badge.style.unityTextOutlineWidth = 0.6f;
        badge.style.unityTextOutlineColor = new StyleColor(new Color(0.14f, 0.10f, 0.02f, 1f));
        badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius =
            badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 3;
        sweepHost.Add(badge);
        _newSchedulerSweepBadge = badge;
        // Force the next SyncNewSchedulerSweep() to write into these freshly-created elements even
        // if the computed time string/position happens to match what the LAST (now-destroyed) badge
        // already showed — the stale-value guards below exist to skip redundant style writes on the
        // SAME element across repeated polls, not to skip the first write on a brand new one.
        _lastNewSchedulerSweepLeft = float.NaN;
        _lastNewSchedulerBadgeText = null;
    }
    else
    {
        _newSchedulerSweepLine = null;
        _newSchedulerPastOverlay = null;
        _newSchedulerSweepBadge = null;
    }

    return root;
}

    private void SyncNewSchedulerSweep()
    {
        if (_newSchedulerSweepLine == null || _newSchedulerSweepBadge == null) return;

        float hour = 0f, minute = 0f;
        if (ServiceLocator.TryGet(out SimulationTimeService time) && time != null)
        {
            hour = time.Hour;
            minute = time.Minute;
        }

        float pct = Mathf.Clamp01((hour * 60f + minute) / 1440f) * 100f;
        if (!Mathf.Approximately(pct, _lastNewSchedulerSweepLeft))
        {
            _lastNewSchedulerSweepLeft = pct;
            var leftLen = Length.Percent(pct);
            _newSchedulerSweepLine.style.left = leftLen;
            _newSchedulerSweepBadge.style.left = leftLen;
            if (_newSchedulerPastOverlay != null) _newSchedulerPastOverlay.style.width = leftLen;
        }

        string text = FormatNewSchedulerClock((int)hour, (int)minute);
        if (text != _lastNewSchedulerBadgeText)
        {
            _lastNewSchedulerBadgeText = text;
            _newSchedulerSweepBadge.text = text;
        }
    }

    private static string FormatNewSchedulerClock(int hour, int minute)
    {
        string suffix = hour < 12 ? "a" : "p";
        int h12 = hour % 12;
        if (h12 == 0) h12 = 12;
        return $"{h12}:{minute:00}{suffix}";
    }

    private VisualElement BuildNewSchedulerDoorRow(DockScheduleService schedule, OrderArrivalService arrivals,
                                                    int doorNumber, int today)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.height = NewSchedulerRowHeight;
        row.style.borderBottomWidth = 1;
        row.style.borderBottomColor = new StyleColor(new Color(ColBlueEdge.r, ColBlueEdge.g, ColBlueEdge.b, 0.4f));

        var label = MakeText($"Door {doorNumber}", 16, ColSubtleText, bold: true);
        label.style.width = NewSchedulerDoorLabelWidth;
        label.style.minWidth = NewSchedulerDoorLabelWidth;
        label.style.flexShrink = 0;
        label.style.paddingLeft = 8;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        row.Add(label);

        var lane = new VisualElement();
        lane.style.flexGrow = 1;
        lane.style.position = Position.Relative;
        row.Add(lane);

        bool booking = _selectedUnscheduledKey != null;
        bool holding = booking || _selectedAppointmentId != null;

        for (int block = 0; block < DockScheduleService.BlocksPerDay; block++)
        {
            // includeClosedOut so a block whose window has elapsed still renders (struck through)
            // instead of going blank — see DockAppointment.ClosedOut. Ordered so an active rebooking
            // into the same now-freed slot is shown in preference to the stale closed-out record.
            var appt = schedule.GetBlock(_scheduleDay, block, includeClosedOut: true)
                .Where(a => a.DoorNumber == doorNumber)
                .OrderBy(a => a.ClosedOut)
                .FirstOrDefault();
            bool past = _scheduleDay < today || (_scheduleDay == today && block < schedule.CurrentBlock);
            VisualElement cell = appt != null
                ? BuildNewSchedulerCell(schedule, appt, arrivals, past)
                : (holding && !past ? BuildNewSchedulerEmptyCell(block, doorNumber, booking) : null);
            if (cell == null) continue;
            cell.style.left = Length.Percent(block / (float)DockScheduleService.BlocksPerDay * 100f);
            cell.style.width = Length.Percent(100f / DockScheduleService.BlocksPerDay);
            lane.Add(cell);
        }

        return row;
    }

    private VisualElement BuildNewSchedulerCell(DockScheduleService schedule, DockAppointment appt,
                                                OrderArrivalService arrivals, bool past)
    {
        bool locked = schedule.IsLocked(appt, out _);
        bool selected = !locked && appt.Id == _selectedAppointmentId;
        Color fill = ChipFill(appt.Kind);
        Color edge = ChipEdge(appt.Kind);
        Color text = ChipText(appt.Kind);

        var cell = new VisualElement();
        cell.style.position = Position.Absolute;
        cell.style.top = 5; cell.style.bottom = 5;
        cell.style.paddingLeft = 4; cell.style.paddingRight = 4; cell.style.paddingTop = 3;
        cell.style.backgroundColor = new StyleColor(fill);
        cell.style.borderTopWidth = cell.style.borderBottomWidth =
            cell.style.borderLeftWidth = cell.style.borderRightWidth = selected ? 2 : 1;
        cell.style.borderTopColor = cell.style.borderBottomColor =
            cell.style.borderLeftColor = cell.style.borderRightColor = new StyleColor(selected ? ColOrange : edge);
        cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius =
            cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 4;
        cell.style.overflow = Overflow.Hidden;

        if (locked || past) cell.style.opacity = 0.45f;

        // IsComplete is computed independently from live state and takes priority: a trailer that's
        // both ClosedOut (swept because its block elapsed) AND IsComplete (its freight actually went
        // out/came in fine before that happened) reads as finished, not missed. ClosedOut on its own —
        // swept with nothing to show for it — is the "block passed, nothing loaded/no truck showed"
        // case and gets the red miss strike instead, per Tad: these should stay on the grid rather
        // than vanish once their block passes.
        bool complete = schedule.IsComplete(appt);
        if (complete) AddStrikeThrough(cell, text);
        else if (appt.ClosedOut) AddStrikeThrough(cell, ColDanger);

        // Separate from the strike above on purpose. ClosedOut clears the moment the trailer is
        // re-placed (TryMoveToDoor), which is what stops it reading as missed forever — but WasLate
        // never clears, so a trailer that already burned its customer/vendor once still carries the
        // warning after landing on a brand-new future slot, instead of looking like nothing happened.
        if (appt.WasLate) AddLateBadge(cell);

        bool awaitingFreight = !complete && appt.Kind != AppointmentKind.Inbound && appt.OrderIds.Count == 0;
        if (awaitingFreight)
            cell.style.backgroundColor = new StyleColor(new Color(fill.r, fill.g, fill.b, 0.12f));

        // Three lines: who it's for, its order/PO number, then a compact summary -- out of stock items
        // for an inbound PO, fill rate for an outbound trailer. The raw time range used to be line 1
        // but per Tad's explicit call was dropped — the chip's own position on the timeline already
        // conveys when it's booked — in favor of the order number. Everything else (full item
        // breakdown, revenue) still lives in the hover tooltip now that the taller row has room for it.
        // Inbound POs run ~25% smaller than outbound -- per Tad, outbound sizing is exactly right and
        // must not change, but inbound's longer vendor/PO text was reading oversized at the same size.
        bool isInbound = IsInboundPo(appt);
        int ChipFontSize = isInbound ? 13 : 14; // was 11 for inbound -- too small to read per Tad
        // Inbound chips render on a dark brown fill; ColWholesale (the standard chip text colour for
        // this kind) reads too dim at chip size, even bold -- brighten just the label lines to white
        // instead of dulling the meaning of the orange/red highlight colours used elsewhere.
        Color labelColor = isInbound ? Color.white : text;
        // Zero out the default Label's built-in padding/margin (4px padding top+bottom, 4px/2px
        // margin) before applying our own tight spacing -- three lines at the enlarged font size
        // otherwise overflow the cell's own height with dead space, clipping the third line entirely.
        void TightenLine(VisualElement line, int marginTop)
        {
            line.style.paddingTop = 0; line.style.paddingBottom = 0;
            line.style.marginTop = marginTop; line.style.marginBottom = 0;
        }

        var who = MakeText(appt.CustomerName, ChipFontSize, labelColor, bold: true);
        who.style.whiteSpace = WhiteSpace.NoWrap; who.style.overflow = Overflow.Hidden;
        TightenLine(who, 0);
        cell.Add(who);

        var orderLine = BuildChipOrderLine(appt, ChipFontSize, labelColor);
        orderLine.style.whiteSpace = WhiteSpace.NoWrap; orderLine.style.overflow = Overflow.Hidden;
        TightenLine(orderLine, 1);
        cell.Add(orderLine);

        int SummaryFontSize = isInbound ? 12 : 14; // was 18/21 -- shrunk ~35% per Tad's request
        var summary = BuildChipSummaryLine(appt, SummaryFontSize);
        if (summary != null)
        {
            // Word-wrap rather than clip -- "2 out of stock item(s)" was truncating to "2 out of stock
            // ite" at the cell's width. The outbound "X% In-Stock" text is short enough it never wraps
            // anyway, so this is safe for both.
            summary.style.whiteSpace = WhiteSpace.Normal; summary.style.overflow = Overflow.Visible;
            TightenLine(summary, 1);
            cell.Add(summary);
        }

        // Bottom line: how many pallets this trailer represents — per Tad's explicit call, so a
        // player scanning the grid doesn't have to open the hover tooltip just to gauge trailer size.
        int palletCount = PalletCountForAppointment(appt);
        var palletsLine = MakeText($"Pallets: {(palletCount < 0 ? "—" : palletCount.ToString())}", SummaryFontSize, labelColor);
        palletsLine.style.whiteSpace = WhiteSpace.NoWrap; palletsLine.style.overflow = Overflow.Hidden;
        TightenLine(palletsLine, 1);
        cell.Add(palletsLine);

        if (!locked) cell.RegisterCallback<ClickEvent>(_ => OnChipClicked(appt));

        cell.RegisterCallback<MouseEnterEvent>(_ =>
        {
            ShowNewSchedulerTooltip(appt, cell);
            // Only a genuinely clickable chip gets the select cursor -- a locked one still shows its
            // tooltip (informational) but a click does nothing, so promising "select" would be a lie.
            if (!locked) CustomCursorService.SetHoveringInteractable(true);
        });
        // Don't hide if the cursor is heading straight into the tooltip (now interactive, for
        // mouse-wheel scrolling on long item lists) -- the tooltip's own MouseLeaveEvent covers
        // hiding once the cursor actually leaves it.
        cell.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            if (!locked) CustomCursorService.SetHoveringInteractable(false);
            if (_newSchedulerTooltip != null && _newSchedulerTooltip.style.display == DisplayStyle.Flex &&
                _newSchedulerTooltip.worldBound.Contains(evt.mousePosition)) return;
            HideNewSchedulerTooltip();
        });

        return cell;
    }
    /// <summary>Second chip line: this trailer's player-facing order number(s) for an outbound/bulk
    /// appointment, or its PO number for an inbound one -- replaces the old raw time range now that
    /// the chip's position on the timeline already conveys when it's scheduled.</summary>
    private VisualElement BuildChipOrderLine(DockAppointment appt, int fontSize, Color color)
    {
        if (IsInboundPo(appt))
            return MakeText($"Order#: {appt.ShipmentPoNumber}", fontSize, color, bold: true);

        if (appt.OrderIds.Count == 0 || !ServiceLocator.TryGet<OrderService>(out var orders) || orders == null)
            return MakeText("No order yet", fontSize, color, bold: true);

        var allOrders = orders.ActiveOrders.Concat(orders.OrderHistory).ToList();
        var numbers = appt.OrderIds
            .Select(id => allOrders.FirstOrDefault(o => o.OrderId == id)?.OrderNumber)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .ToList();

        string label = numbers.Count > 0 ? $"Ordered: {string.Join(" + ", numbers)}" : "No order yet";
        return MakeText(label, fontSize, color, bold: true);
    }

    /// <summary>Third chip line: how many SKUs on an inbound PO are OUT OF STOCK -- in demand from
    /// pending outbound orders beyond what's actually received/on-hand (LIVE inventory only; stock
    /// still in transit on another PO does NOT count, even though it's coming -- see
    /// CountInboundOutOfStockItems) -- or the blended In-Stock coverage across every order riding an
    /// outbound trailer -- same metric and color scale (<see cref="OutsColor"/>) as the per-SKU lines
    /// in the hover tooltip, just rolled up to one trailer-wide number. Null for a bare "truck at
    /// door, no PO on file" note -- there's no shipment/order data yet to summarize.</summary>
    private VisualElement BuildChipSummaryLine(DockAppointment appt, int fontSize)
    {
        if (IsInboundPo(appt))
        {
            int outOfStock = CountInboundOutOfStockItems(appt);
            string label = outOfStock > 0
                ? $"{outOfStock} out of stock item{(outOfStock == 1 ? "" : "s")}"
                : "Nothing out of stock";
            // Neutral case brightened to match the label lines above (see BuildNewSchedulerCell) --
            // ChipText's dim orange was hard to read at chip size even bold.
            Color neutral = new Color(1f, 0.92f, 0.78f);
            return MakeText(label, fontSize, outOfStock > 0 ? ColDangerSoft : neutral, bold: true);
        }

        if (appt.Kind == AppointmentKind.Inbound) return null; // bare note, nothing to summarize yet

        int inStockPercent = ComputeOutboundInStockPercent(appt);
        return MakeText($"{inStockPercent}% In-Stock", fontSize, OutsColor(inStockPercent), bold: true);
    }

    /// <summary>Per Tad: "out of stock" is judged purely against LIVE inventory (InventoryService.
    /// TotalOnHand -- a pallet becomes part of the balance the moment it's received), never netted
    /// against quantity still in transit on some other PO. Stock on order doesn't help the floor until
    /// it's actually on the shelf, so it must not mask a real shortage here -- that's what the
    /// separate "Pending Receipt" line on each item row is for instead.</summary>
    private static int CountInboundOutOfStockItems(DockAppointment appt)
    {
        if (!ServiceLocator.TryGet<ShipmentService>(out var shipments) || shipments == null) return 0;
        var shipment = shipments.PendingShipments.FirstOrDefault(s => s.PONumber == appt.ShipmentPoNumber);
        if (shipment == null) return 0;
        if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return 0;
        if (!ServiceLocator.TryGet<VendorEconomyService>(out var econ) || econ == null) return 0;

        int count = 0;
        foreach (var skuId in shipment.LineItems.Select(li => li.SkuId).Distinct())
        {
            int outOfStock = Mathf.Max(0, econ.GetTotalInDemand(skuId) - inv.TotalOnHand(skuId));
            if (outOfStock > 0) count++;
        }
        return count;
    }

    /// <summary>Blended on-hand coverage across every distinct SKU riding this trailer: how much of
    /// everything ordered could be covered by what's currently on the shelf, capped per-SKU at 100% so
    /// a surplus of one item can't paper over a shortage of another. Same ActiveOrders+OrderHistory
    /// lookup as BuildOutboundTooltipContent, so the chip agrees with its own hover tooltip even after
    /// an order archives out of ActiveOrders on close-out.</summary>
    private static int ComputeOutboundInStockPercent(DockAppointment appt)
    {
        if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null || appt.OrderIds.Count == 0)
            return 0;
        if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return 0;

        var allOrders = orders.ActiveOrders.Concat(orders.OrderHistory);
        var neededBySku = new Dictionary<string, int>();
        foreach (var id in appt.OrderIds)
        {
            var order = allOrders.FirstOrDefault(o => o.OrderId == id);
            if (order == null) continue;
            foreach (var li in order.LineItems)
            {
                neededBySku.TryGetValue(li.SkuId, out int needed);
                neededBySku[li.SkuId] = needed + li.QuantityNeeded;
            }
        }
        if (neededBySku.Count == 0) return 0;

        int totalNeeded = 0, totalCovered = 0;
        foreach (var kv in neededBySku)
        {
            totalNeeded += kv.Value;
            totalCovered += Mathf.Min(inv.TotalOnHand(kv.Key), kv.Value);
        }
        return totalNeeded > 0 ? Mathf.Clamp(Mathf.RoundToInt(totalCovered / (float)totalNeeded * 100f), 0, 100) : 0;
    }


    private VisualElement BuildNewSchedulerEmptyCell(int block, int doorNumber, bool booking)
    {
        var cell = new VisualElement();
        cell.style.position = Position.Absolute;
        cell.style.top = 5; cell.style.bottom = 5;
        cell.style.backgroundColor = new StyleColor(new Color(ColOrange.r, ColOrange.g, ColOrange.b, 0.18f));
        cell.style.borderTopWidth = cell.style.borderBottomWidth =
            cell.style.borderLeftWidth = cell.style.borderRightWidth = 1;
        cell.style.borderTopColor = cell.style.borderBottomColor =
            cell.style.borderLeftColor = cell.style.borderRightColor = new StyleColor(ColOrangeEdge);
        cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius =
            cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 4;

        var label = MakeText(booking ? "book here" : "move here", 10, ColOrangeText);
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.marginTop = NewSchedulerRowHeight / 2f - 20f;
        cell.Add(label);

        cell.RegisterCallback<ClickEvent>(_ => OnSlotClicked(block, doorNumber));
        cell.RegisterCallback<MouseEnterEvent>(_ => CustomCursorService.SetHoveringInteractable(true));
        cell.RegisterCallback<MouseLeaveEvent>(_ => CustomCursorService.SetHoveringInteractable(false));
        return cell;
    }


    // ── New Scheduler hover tooltip ───────────────────────────────────────────────
    private VisualElement _newSchedulerTooltip;
    private ScrollView _newSchedulerTooltipScroll;

    /// <summary>Appointment the open hover tooltip is currently showing, if any — lets a live
    /// inventory event (see OnPalletReceivedForLiveRefresh) re-render the SAME tooltip's content in
    /// place without needing the mouse to leave and re-enter the chip.</summary>
    private DockAppointment _hoveredTooltipAppt;

    /// <summary>Builds the rich hover card for a timeline cell — an inbound PO's manifest or an
    /// outbound trailer's items — and positions it just below the hovered cell, clamped so it can't
    /// run off the modal's right/bottom edge.</summary>
    private void ShowNewSchedulerTooltip(DockAppointment appt, VisualElement cell)
    {
        if (_newSchedulerTooltip == null) return;
        _hoveredTooltipAppt = appt;
        _newSchedulerTooltipScroll.Clear();
        _newSchedulerTooltipScroll.scrollOffset = Vector2.zero;

        VisualElement content = IsInboundPo(appt) ? BuildInboundTooltipContent(appt)
            : appt.Kind == AppointmentKind.Inbound ? BuildInboundNoteTooltipContent(appt)
            : BuildOutboundTooltipContent(appt);
        _newSchedulerTooltipScroll.Add(content);

        // Overlaps the cell by a few px instead of sitting just below it — a real gap there let the
        // mouse cross empty space between the two and lose the tooltip before it reached it (a
        // problem now that the tooltip needs to be hovered to mouse-wheel scroll it), per Tad's
        // explicit call.
        Vector2 local = _modal.WorldToLocal(new Vector2(cell.worldBound.x, cell.worldBound.yMax - 4));
        float maxLeft = Mathf.Max(4f, _modal.resolvedStyle.width - 476f);
        float maxTop = Mathf.Max(4f, _modal.resolvedStyle.height - 60f);
        float top = Mathf.Clamp(local.y, 4f, maxTop);
        _newSchedulerTooltip.style.left = Mathf.Clamp(local.x, 4f, maxLeft);
        _newSchedulerTooltip.style.top = top;
        // Cap the scroll area to whatever room is left below it in the modal, rather than letting
        // tall item lists (see BuildOutboundTooltipContent) push the tooltip off the bottom edge --
        // it scrolls internally instead, per Tad's explicit call.
        _newSchedulerTooltipScroll.style.maxHeight =
            Mathf.Max(120f, _modal.resolvedStyle.height - top - 36f);
        _newSchedulerTooltip.style.display = DisplayStyle.Flex;
        _newSchedulerTooltip.BringToFront();
    }

    private void HideNewSchedulerTooltip()
    {
        _hoveredTooltipAppt = null;
        if (_newSchedulerTooltip != null) _newSchedulerTooltip.style.display = DisplayStyle.None;
    }

    /// <summary>Re-renders the currently-open hover tooltip's content in place — same position, fresh
    /// numbers — without needing ShowNewSchedulerTooltip's full re-entry (which needs a `cell`
    /// reference this doesn't have). No-op if no tooltip is actually open right now.</summary>
    private void RefreshOpenTooltipContent()
    {
        if (_newSchedulerTooltip == null || _hoveredTooltipAppt == null) return;
        if (_newSchedulerTooltip.style.display != DisplayStyle.Flex) return;

        _newSchedulerTooltipScroll.Clear();
        VisualElement content = IsInboundPo(_hoveredTooltipAppt) ? BuildInboundTooltipContent(_hoveredTooltipAppt)
            : _hoveredTooltipAppt.Kind == AppointmentKind.Inbound ? BuildInboundNoteTooltipContent(_hoveredTooltipAppt)
            : BuildOutboundTooltipContent(_hoveredTooltipAppt);
        _newSchedulerTooltipScroll.Add(content);
    }

    private VisualElement BuildInboundNoteTooltipContent(DockAppointment appt)
    {
        var col = new VisualElement();
        col.Add(MakeText(appt.CustomerName, 14, ColTitleText, bold: true));
        col.Add(MakeText("Truck currently at the door — no PO on file for it.", 12, ColSubtleText));
        return col;
    }

    /// <summary>PO number, vendor, then one row per SKU on the PO — cases, pallets (one
    /// ShipmentLineItem IS one pallet, so pallet count is a real line count, not a derived estimate),
    /// and how much of that SKU is currently in net demand across outbound orders (gross in-demand
    /// minus on-hand minus on-order — the same shortage definition PurchasingPanel.CountCriticalItems
    /// uses for vendor SKUs). Any item in net demand marks the whole load CRITICAL — this PO is
    /// carrying something the floor actually needs.</summary>
private VisualElement BuildInboundTooltipContent(DockAppointment appt)
{
    var col = new VisualElement();

    if (!ServiceLocator.TryGet<ShipmentService>(out var shipments) || shipments == null)
    {
        col.Add(MakeText("Shipment data unavailable.", 12, ColSubtleText));
        return col;
    }
    // PurgeCompleted (ShipmentService) moves a finished PO out of PendingShipments the moment its
    // truck departs — it isn't deleted, it's ARCHIVED, so a completed PO's tooltip can keep showing
    // what actually arrived for the rest of that day. The appointment itself is what actually goes
    // away at day rollover (DockScheduleService purges every appointment once its day stops being
    // "today"), so falling back to the archive here is what makes this survive until 23:59 — no
    // separate day-cutoff bookkeeping needed.
    var shipment = shipments.PendingShipments.FirstOrDefault(s => s.PONumber == appt.ShipmentPoNumber)
                 ?? shipments.ArchivedShipments.FirstOrDefault(s => s.PONumber == appt.ShipmentPoNumber);
    col.Add(MakeText($"PO {appt.ShipmentPoNumber}", 38, ColTitleText, bold: true)); // 19 * 2 per Tad's explicit call
    AddLatePenaltyTooltipLine(col, appt);
    AddSideLotTooltipLine(col, appt.ShipmentPoNumber);
    if (shipment == null)
    {
        col.Add(MakeText("No longer on file.", 12, ColSubtleText));
        return col;
    }

    // Once the truck is done, show what was actually RECEIVED rather than what was ordered — a
    // supplier shortage or a broker write-off means those can differ, and "received" is what the
    // player actually needs to know once the trailer has already left.
    bool received = shipment.Status == ShipmentData.ShipmentStatus.Received
                 || shipment.Status == ShipmentData.ShipmentStatus.Departed;

    ServiceLocator.TryGet<InventoryService>(out var inv);
    ServiceLocator.TryGet<VendorEconomyService>(out var econ);

    var bySku = new Dictionary<string, (int ordered, int received, int pallets, int droppedPallets, int droppedCases)>();
    foreach (var li in shipment.LineItems)
    {
        bySku.TryGetValue(li.SkuId, out var agg);
        agg.ordered += li.Quantity;
        agg.received += li.ReceivedQuantity;
        agg.pallets += 1;
        // Dropped means the supplier's short-shipment roll (ShipmentService.ApplySupplierVariance)
        // picked this exact pallet — it will NEVER arrive, distinct from a pallet that's simply
        // sitting in the lane waiting for a Receiver to walk over and process it. Tracked separately
        // so the two states don't render identically (see the row loop below).
        if (li.Dropped) { agg.droppedPallets += 1; agg.droppedCases += li.Quantity; }
        bySku[li.SkuId] = agg;
    }

    // "Out of stock" is judged against LIVE inventory only (TotalOnHand — a pallet counts the moment
    // it's received) — quantity still in transit on some OTHER PO does not cover a shortage here, per
    // Tad, since it isn't actually on the shelf yet. That other-PO quantity is surfaced separately as
    // each row's "Pending Receipt" instead, so the player can see relief is coming without it quietly
    // erasing a real shortage from view.
    var rows = new List<(SkuData sku, int ordered, int received, int pallets, int outOfStock, int pendingReceipt, int droppedPallets, int droppedCases)>();
    bool anyOutOfStock = false;
    foreach (var kv in bySku)
    {
        var sku = inv?.GetSkuData(kv.Key);
        int outOfStock = econ != null && inv != null
            ? Mathf.Max(0, econ.GetTotalInDemand(kv.Key) - inv.TotalOnHand(kv.Key))
            : 0;
        int pendingReceipt = OtherPendingReceipt(shipments, shipment, kv.Key);
        if (outOfStock > 0) anyOutOfStock = true;
        rows.Add((sku, kv.Value.ordered, kv.Value.received, kv.Value.pallets, outOfStock, pendingReceipt,
                 kv.Value.droppedPallets, kv.Value.droppedCases));
    }

    if (!received && anyOutOfStock)
    {
        var badge = MakeText("OUT OF STOCK LOAD — CARRYING ITEM(S) THE FLOOR NEEDS", 14, ColDangerSoft, bold: true); // 11 * 1.25, all-caps per Tad's explicit call
        badge.style.marginTop = 2; badge.style.marginBottom = 2;
        col.Add(badge);
    }

    int totalPallets = rows.Sum(r => r.pallets);
    var vendor = VendorRegistry.Load()?.GetById(shipment.SupplierId);
    var vendorRow = new VisualElement();
    vendorRow.style.flexDirection = FlexDirection.Row;
    vendorRow.style.alignItems = Align.Center;
    vendorRow.style.marginTop = 6; vendorRow.style.marginBottom = 6;
    var vendorIcon = MakeIcon(vendor?.Icon, 73, 5, marginRight: 12);
    vendorIcon.style.width = 73 * 0.9f; // squeezed 10% on X only per Tad's explicit call -- height stays 73
    vendorRow.Add(vendorIcon);
    var vendorNameLabel = MakeText(vendor != null ? vendor.DisplayName : (shipment.SupplierId ?? "Unknown vendor"),
                           18, new Color(0xD0 / 255f, 0xEC / 255f, 0xFC / 255f, 1f), bold: true); // 26 * 0.7, even lighter blue, fully opaque per Tad's explicit call
    // Wrapped to ~2 lines rather than one long line running past the badge, per Tad's explicit call --
    // MakeText already sets WhiteSpace.Normal, this just gives it a width narrow enough to actually wrap.
    // Narrowed from 170 -- that box's own dead space past the wrapped text was what stood between the
    // name and the badge, so shrinking it (not just the gap after it) is what actually pulls the
    // badge further left, per Tad's explicit call.
    vendorNameLabel.style.width = 120;
    vendorRow.Add(vendorNameLabel);
    // Small fixed gap instead of a flexGrow spacer -- pulls the pallet badge in snug against the
    // vendor name instead of pinning it to the row's far right edge, per Tad's explicit call to move
    // it as far left as possible without clipping the icon/name.
    var vendorRowSpacer = new VisualElement(); vendorRowSpacer.style.width = 4;
    vendorRow.Add(vendorRowSpacer);
    // Total pallet count, to help the player judge door/lane capacity while booking this PO onto the
    // Scheduler — per Tad's explicit call. Big number over a "pallet(s)" caption, no "Total:" label,
    // sitting on a solid blue badge circle per Tad's follow-up ask for visual appeal.
    var palletCountBadge = new VisualElement();
    palletCountBadge.style.width = 90; palletCountBadge.style.height = 90;
    palletCountBadge.style.flexShrink = 0;
    palletCountBadge.style.borderTopLeftRadius = palletCountBadge.style.borderTopRightRadius =
        palletCountBadge.style.borderBottomLeftRadius = palletCountBadge.style.borderBottomRightRadius = 45;
    palletCountBadge.style.backgroundColor = new StyleColor(ColBorder);
    palletCountBadge.style.justifyContent = Justify.Center;
    palletCountBadge.style.alignItems = Align.Center;
    var palletCountBlock = new VisualElement();
    palletCountBlock.style.alignItems = Align.Center;
    var palletCountNumber = MakeText(totalPallets.ToString(), 44, ColMoney, bold: true); // 46 * 0.95; reverted back to money-green -- red didn't look good, per Tad's explicit call
    // Zeroed out -- the default Label's own top/bottom padding was the real gap here, not the
    // margin; a -4 margin on top of that padding still left "pallets" hanging visibly below the
    // number instead of tucked right under it, per Tad's explicit call.
    palletCountNumber.style.paddingTop = 0; palletCountNumber.style.paddingBottom = 0;
    palletCountNumber.style.marginTop = 0; palletCountNumber.style.marginBottom = -6;
    palletCountBlock.Add(palletCountNumber);
    // Back down to 13 (the +4pt bump was reverted) but staying all-caps, per Tad's explicit call.
    var palletCountWord = MakeText(totalPallets == 1 ? "PALLET" : "PALLETS", 12, ColTitleText, bold: true); // 13 * 0.95; reverted back -- red didn't look good, per Tad's explicit call
    palletCountWord.style.paddingTop = 0; palletCountWord.style.paddingBottom = 0;
    palletCountWord.style.marginTop = -6; palletCountWord.style.marginBottom = 0;
    palletCountBlock.Add(palletCountWord);
    palletCountBadge.Add(palletCountBlock);
    vendorRow.Add(palletCountBadge);
    col.Add(vendorRow);

    // Per-item red now means "short-shipped" specifically (see the row loop below) — an item being
    // out of stock on the floor no longer colors its own row, only the trailer-level chip/badge above.
    var shortShipLegend = MakeText("SHORT-SHIPPED IN RED", 14, ColDanger, bold: true); // 10 * 1.35, all-caps per Tad's explicit call
    shortShipLegend.style.marginBottom = 2;
    col.Add(shortShipLegend);

    // Still not enough live inventory to cover pending outbound orders — orange, whether or not this
    // PO has arrived yet. Unlike a short-shipment (unknowable until departure/receipt), this is a
    // fact we already know today, so per Tad it should show in advance rather than waiting.
    var neededLegend = MakeText("PRODUCT NEEDED FOR ORDER IN ORANGE", 14, ColWholesale, bold: true); // 10 * 1.35, all-caps per Tad's explicit call
    neededLegend.style.marginBottom = 4;
    col.Add(neededLegend);

    if (received)
    {
        int shortfall = rows.Sum(r => r.ordered - r.received);
        var costLabel = MakeText(
            $"Received cost: ${shipment.TotalReceivedCost:N0}" +
            (shortfall > 0 ? $" · {shortfall:N0} case(s) short" : string.Empty),
            12, shortfall > 0 ? ColDangerSoft : ColOrangeText, bold: true);
        costLabel.style.marginBottom = 6;
        col.Add(costLabel);
    }
    else
    {
        int totalCost = Mathf.RoundToInt(rows.Sum(r => r.ordered * (r.sku?.BuyValue ?? 0f)));
        var costLabel = MakeText($"Expected cost: ${totalCost:N0}", 16, ColOrangeText, bold: true); // 12 * 1.35 per Tad's explicit call
        costLabel.style.marginBottom = 6;
        col.Add(costLabel);
    }

    col.Add(BuildTooltipDivider());

    foreach (var row in rows.OrderBy(r => r.sku?.ItemNumber ?? 0))
    {
        // Short-shipped can only be KNOWN once the trailer has actually departed/been received — see
        // ShipmentLineItem.Dropped. "Needed for order" (outOfStock) is different: it's a fact we
        // already know today regardless of whether this PO has arrived, so — per Tad's explicit
        // correction — it's allowed to flag orange in advance, while red stays strictly gated on
        // `received`.
        bool shortShipped = received && row.droppedPallets > 0;
        string caseLine = received
            ? (row.received < row.ordered
                ? $"{row.received:N0}/{row.ordered:N0} case(s) received · {PalletLabel(row.pallets)}"
                : $"{row.received:N0} case(s) received · {PalletLabel(row.pallets)}")
            : $"Expected: {row.ordered:N0} case(s) · Received: 0 · {PalletLabel(row.pallets)}";

        Color? highlight = null;
        if (shortShipped)
        {
            // Short-shipped is a PERMANENT discrepancy (the supplier never put it on the truck) — RED,
            // and takes priority over the orange case below.
            caseLine += $" · SHORT-SHIPPED: {row.droppedCases:N0} case(s) never arrived";
            highlight = ColDanger;
        }
        else if (row.outOfStock > 0)
        {
            // Nothing WRONG here — just a fact worth flagging either way: the floor doesn't have
            // enough live inventory of this SKU to cover pending outbound orders yet. ORANGE, whether
            // this PO is still inbound or already landed. See "Pending Receipt" below for whether more
            // relief is already on its way from elsewhere.
            caseLine += " · needed for order";
            highlight = ColWholesale;
        }
        // Neither: fully covers demand (once received) or nothing to flag yet — stays neutral/white.

        var detailElement = BuildInboundItemDetail(caseLine, highlight != null ? row.pendingReceipt : 0, highlight);
        col.Add(BuildTooltipItemRow(row.sku, detailElement, highlight));
    }

    return col;
}

    /// <summary>Builds an inbound item row's detail column: the existing case/pallet line, plus a
    /// "Pending Receipt" line underneath when pendingReceipt > 0 -- quantity of this SKU already in
    /// transit on some OTHER PO. Still counts as out of stock (it isn't on the shelf yet), but tells
    /// the player relief is coming so they know to get it received before the outbound order that
    /// needs it comes up on the Scheduler.</summary>
    private VisualElement BuildInboundItemDetail(string caseLine, int pendingReceipt, Color? highlightColor = null)
    {
        var col = new VisualElement();

        // White regardless of state — the red/orange highlight now lives on the item name line above
        // (see BuildTooltipItemRow) instead of here, per Tad's explicit call that this detail text was
        // hard to read. Still bold for a genuine discrepancy (red short-shipped / orange still out of
        // stock) so the emphasis isn't lost, just not carried in the colour anymore.
        var caseLabel = MakeText(caseLine, 14, Color.white, bold: highlightColor != null);
        col.Add(caseLabel);

        if (pendingReceipt > 0)
        {
            var pendingLabel = MakeText($"Pending Receipt: {pendingReceipt:N0}", 14, ColWholesale, bold: true);
            col.Add(pendingLabel);
        }

        return col;
    }

    /// <summary>Quantity of this SKU already in transit on OTHER pending shipments — deliberately
    /// excludes <paramref name="current"/> so a PO's own line doesn't echo its own quantity back at
    /// itself as "Pending Receipt". Same status filter as VendorEconomyService.GetTotalOnOrder.</summary>
    private static int OtherPendingReceipt(ShipmentService shipments, ShipmentData current, string skuId)
    {
        if (shipments == null || string.IsNullOrEmpty(skuId)) return 0;

        int total = 0;
        foreach (var s in shipments.PendingShipments)
        {
            if (s == null || s == current) continue;
            if (s.Status != ShipmentData.ShipmentStatus.InTransit &&
                s.Status != ShipmentData.ShipmentStatus.Receiving &&
                s.Status != ShipmentData.ShipmentStatus.Delayed) continue;

            foreach (var li in s.LineItems)
            {
                if (li == null || li.SkuId != skuId) continue;
                total += Mathf.Max(0, li.Quantity - li.ReceivedQuantity);
            }
        }
        return total;
    }

    /// <summary>Customer, total expected revenue across every order riding this trailer, then one row
    /// per SKU — cases actually picked so far and the resulting fill rate.</summary>
    private VisualElement BuildOutboundTooltipContent(DockAppointment appt)
    {
        var col = new VisualElement();

        if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null || appt.OrderIds.Count == 0)
        {
            col.Add(MakeText(appt.CustomerName, 27, ColTitleText, bold: true));
            col.Add(MakeText("No items generated yet.", 24, ColSubtleText));
            return col;
        }

        // Order stats stay visible through departure (looked up from OrderHistory too, since a
        // shipped/cancelled order archives out of ActiveOrders the moment it closes out) but only for
        // the day of this appointment — per Tad's request, once the in-game day rolls past appt.Day
        // the numbers purge instead of showing stale data from a day that's already over.
        int currentDay = Schedule()?.CurrentDay ?? appt.Day;
        if (currentDay > appt.Day)
        {
            col.Add(MakeText(appt.CustomerName, 27, ColTitleText, bold: true));
            col.Add(MakeText("Order data cleared — day has ended.", 24, ColSubtleText));
            return col;
        }

        var allOrders = orders.ActiveOrders.Concat(orders.OrderHistory).ToList();
        var orderList = appt.OrderIds
            .Select(id => allOrders.FirstOrDefault(o => o.OrderId == id))
            .Where(o => o != null).ToList();
        int totalRevenue = orderList.Sum(o => o.TotalRevenue);

        col.Add(MakeText(appt.CustomerName, 30, ColTitleText, bold: true));
        AddLatePenaltyTooltipLine(col, appt);
        var revenueLabel = MakeText($"Expected revenue: ${totalRevenue:N0}", 24, ColMoney, bold: true);
        revenueLabel.style.marginBottom = 6;
        col.Add(revenueLabel);
        col.Add(BuildTooltipDivider());

        if (orderList.Count == 0)
        {
            col.Add(MakeText("No items generated yet.", 24, ColSubtleText));
            return col;
        }

        ServiceLocator.TryGet<InventoryService>(out var inv);
        ServiceLocator.TryGet<VendorEconomyService>(out var economy);
        ServiceLocator.TryGet<ShipmentService>(out var shipments);
        var dockSchedule = Schedule();
        var bySku = new Dictionary<string, int>();
        foreach (var order in orderList)
            foreach (var li in order.LineItems)
            {
                bySku.TryGetValue(li.SkuId, out var needed);
                bySku[li.SkuId] = needed + li.QuantityNeeded;
            }

        foreach (var kv in bySku.OrderBy(k => inv?.GetSkuData(k.Key)?.ItemNumber ?? 0))
        {
            var sku = inv?.GetSkuData(kv.Key);
            int onHand = inv?.TotalOnHand(kv.Key) ?? 0;
            int onPo = economy?.GetTotalOnOrder(kv.Key) ?? 0;
            string nextPo = NextPoLabel(kv.Key, shipments, dockSchedule);
            // On-hand alone already covers what this trailer needs -- item reads green, per Tad's
            // explicit call, same as everywhere else "good coverage" is called out (see OutsColor).
            Color? itemColor = onHand >= kv.Value ? ColMoney : (Color?)null;
            col.Add(BuildTooltipItemRow(sku, BuildOutboundItemDetail(kv.Value, onHand, onPo, nextPo), itemColor));
        }

        return col;
    }

    /// <summary>Soonest inbound PO still carrying this SKU, formatted as "Next PO: Day D HH:00" — or
    /// "Nothing On Order" if nothing pending covers it. Mirrors OtherPendingReceipt's pending-status
    /// filter (InTransit/Receiving/Delayed) so a line only counts freight that's genuinely still
    /// coming, not something already fully received or long departed.</summary>
    private static string NextPoLabel(string skuId, ShipmentService shipments, DockScheduleService dockSchedule)
    {
        if (shipments == null || dockSchedule == null || string.IsNullOrEmpty(skuId)) return "Nothing On Order";

        DockAppointment earliest = null;
        foreach (var s in shipments.PendingShipments)
        {
            if (s == null) continue;
            if (s.Status != ShipmentData.ShipmentStatus.InTransit &&
                s.Status != ShipmentData.ShipmentStatus.Receiving &&
                s.Status != ShipmentData.ShipmentStatus.Delayed) continue;
            if (!s.LineItems.Any(li => li != null && li.SkuId == skuId && li.Quantity > li.ReceivedQuantity)) continue;

            var appt = dockSchedule.FindForPo(s.PONumber);
            if (appt == null) continue;
            if (earliest == null || (appt.Day * 24 + appt.StartHour) < (earliest.Day * 24 + earliest.StartHour))
                earliest = appt;
        }

        return earliest == null ? "Nothing On Order" : $"Next PO: Day {earliest.Day} {earliest.StartHour:00}:00";
    }

    private VisualElement BuildTooltipDivider()
    {
        var divider = new VisualElement();
        divider.style.height = 1;
        divider.style.backgroundColor = new StyleColor(ColBorder);
        divider.style.marginBottom = 6;
        return divider;
    }

    /// <summary>Standard color for an "Outs" (on-hand stock coverage) percentage, anywhere one is
    /// shown: 0% is fully out (red), 1-99% is partial coverage (orange), 100% is fully covered
    /// (bright green). Per Tad, this is the house standard going forward for any stock-coverage
    /// percentage, not just the order tooltip it was introduced for.</summary>
    private static Color OutsColor(int pct)
    {
        if (pct <= 0) return ColDanger;
        if (pct >= 100) return ColMoney;
        return ColWholesale;
    }

    /// <summary>Builds the two-line "Ordered: X | On-Hand: X | On PO: X" + "Next PO: Day D HH:00"
    /// detail block for an outbound order's item row. "Shipped" was dropped per Tad — useless info,
    /// this order hasn't picked yet. The second line calls out whether the shortfall is actually
    /// covered: red for "Nothing On Order" (nothing coming, still short), orange for "Next PO: ..."
    /// (fill qty is on an order, just not here yet), or green "Sufficient BOH" when nothing's on
    /// order because nothing needs to be — on-hand alone already covers what's needed — per Tad's
    /// explicit call.</summary>
    private VisualElement BuildOutboundItemDetail(int ordered, int onHand, int onPo, string nextPoLabel)
    {
        var col = new VisualElement();
        col.style.flexDirection = FlexDirection.Column;

        var line1 = MakeText($"Ordered: {ordered:N0} | On-Hand: {onHand:N0} | On PO: {onPo:N0}", 19, ColTitleText);
        line1.style.flexWrap = Wrap.Wrap;
        line1.style.marginBottom = 0;
        col.Add(line1);

        bool nothingOnOrder = nextPoLabel == "Nothing On Order";
        bool sufficientBoh = nothingOnOrder && onHand >= ordered;
        string line2Text = sufficientBoh ? "Sufficient BOH" : nextPoLabel;
        Color line2Color = sufficientBoh ? ColMoney : nothingOnOrder ? ColDanger : ColWholesale;
        var line2 = MakeText(line2Text, 19, line2Color, bold: true);
        line2.style.marginTop = -2; // butt up against line1 per Tad's explicit call
        col.Add(line2);

        return col;
    }

    private VisualElement BuildTooltipItemRow(SkuData sku, VisualElement detailElement, Color? highlightColor = null)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 2; // was 4 -- compressed per Tad's request to fit more item lines vertically

        row.Add(MakeIcon(sku?.Icon, 57, 4, marginRight: 8)); // was 44 -- upsized 30% more per Tad's explicit call

        var textCol = new VisualElement();
        textCol.style.flexGrow = 1;
        textCol.style.overflow = Overflow.Hidden;

        // Zero out the default Label's built-in padding/margin (same treatment BuildNewSchedulerCell's
        // TightenLine gives the chip lines) -- without it the name and detail lines each carry the
        // project's default Label spacing on top of our own, which is most of why item rows read so
        // tall and loose to begin with.
        // Item number + description also takes the highlight color (red = short-shipped, orange =
        // still needed for pending orders) -- per Tad, not just the detail line below it.
        var nameLabel = MakeText(sku != null ? $"#{sku.ItemNumber} — {sku.ItemDescription}" : "Unknown item",
                                 24, highlightColor ?? ColTitleText, bold: true);
        nameLabel.style.paddingTop = 0; nameLabel.style.paddingBottom = 0;
        nameLabel.style.marginTop = 0; nameLabel.style.marginBottom = 0;
        textCol.Add(nameLabel);

        detailElement.style.paddingTop = 0; detailElement.style.paddingBottom = 0;
        detailElement.style.marginTop = 0; detailElement.style.marginBottom = 0;
        textCol.Add(detailElement);

        row.Add(textCol);

        return row;
    }

}
