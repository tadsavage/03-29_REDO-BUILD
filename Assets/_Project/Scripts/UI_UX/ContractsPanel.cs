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
    /// <summary>Narrowest the window can be dragged. Below this the tab bar itself starts wrapping.
    /// The Completed tab can be narrowed past its own column total safely — it scrolls sideways, and
    /// its header tracks the scroll (see SyncCompletedHeader).</summary>
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

    /// <summary>How far either side of today the Schedule tab will page. Backwards is bounded by
    /// DockScheduleService's own purge of old appointments; forwards is just far enough to see the
    /// consequences of a lead time.</summary>
    private const int ScheduleDaysBack = 2;
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
    private enum Tab { NewContracts, BulkOrders, Accounts, Schedule, Completed }

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
        // Added to the OVERLAY, not the modal: the modal is absolutely positioned and draggable, so a
        // dialog inside it would follow the window around and could sit half off-screen. The overlay
        // fills the panel's whole area, which is what a modal confirmation should darken and block.
        _overlay.Add(BuildOffSlotConfirm());

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
        _resizeWindow?.ResetToNormal();
    }

    /// <summary>
    /// Opens this panel straight onto the Schedule tab, on a given day.
    ///
    /// Exists for cross-panel links — the Purchasing panel's "Scheduler" button, which sends the
    /// player from a PO that needs a door to the grid where doors are booked. Sets the tab BEFORE
    /// Show() so the panel never flashes whichever tab was last open on the way through.
    ///
    /// A day of 0 or less means "leave the view where it was", so a caller with no opinion doesn't
    /// have to invent one.
    /// </summary>
    public void ShowScheduleTab(int day = 0)
    {
        _tab = Tab.Schedule;
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
        var backToPurchasing = new Button(OpenPurchasing) { text = "Back to Purchasing" };
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

        _scaleBtn = new Button { text = string.Empty, tooltip = "Resize window (normal / large / fill screen)" };
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
        content.schedule.Execute(SyncFrozenTimeColumn).Every(16);
        content.schedule.Execute(SyncCompletedHeader).Every(16);
        content.schedule.Execute(SyncScheduleHeader).Every(16);
        // Coarser interval than the three above — this only ever displays whole minutes, so polling
        // faster than a few times a second buys nothing. Scheduled ONCE here for the same reason as
        // the others: doing it per Rebuild would stack a poller per refresh.
        content.schedule.Execute(SyncScheduleClock).Every(200);
        // A block only ever changes every 2 in-game hours — no need to poll as tightly as the clock.
        content.schedule.Execute(SyncScheduleElapsed).Every(1000);

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
        // The Accounts view is a forward work plan now: badge it with every unshipped recurring order,
        // not merely the number of customer contracts. One account can have seven scheduled manifests
        // through the planning horizon, and showing "1" there falsely suggests only one order exists.
        int accountCount = PendingRecurringOrders().Count;

        string scheduleBadge = "—";
        if (schedule != null)
        {
            int cap = schedule.CapacityPerBlock * DockScheduleService.BlocksPerDay;
            int booked = schedule.Appointments.Count(a => a.Day == _scheduleDay);
            scheduleBadge = cap > 0 ? $"{booked}/{cap}" : "no doors";
        }

        int bulkCount = LiveBulkOrders().Count;

        // Recurring before Bulk in the tab row, matching the New Contracts board's left-to-right
        // panel order (Recurring left, Bulk right) — the tab row used to run the opposite way.
        _tabBar.Add(MakeTab("New Contracts", offerCount.ToString(), Tab.NewContracts));
        _tabBar.Add(MakeTab("Recurring Orders", accountCount.ToString(), Tab.Accounts));
        _tabBar.Add(MakeTab("Bulk Orders", bulkCount.ToString(), Tab.BulkOrders));
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

    /// <summary>The Schedule tab's "Door 1 / Door 2 / …" column header, same shape as
    /// _completedHeaderRow — lives in the stationary strip above the grid and is slid sideways in
    /// step with the grid's horizontal scroll so it stays lined up over the right columns. Null on
    /// every other tab.</summary>
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

    /// <summary>The Schedule tab's mirror of SyncCompletedHeader — same reasoning, same mechanism,
    /// separate field because the two headers belong to different tabs and are cleared independently
    /// on Rebuild.</summary>
    private void SyncScheduleHeader()
    {
        if (_scheduleHeaderRow == null) return;

        float x = _content.scrollOffset.x;
        if (Mathf.Approximately(x, _lastScheduleHScroll)) return;
        _lastScheduleHScroll = x;
        _scheduleHeaderRow.style.left = -x;
    }

    /// <summary>Keeps the Schedule tab's rail clock ticking between rebuilds. Before this it only ever
    /// showed the time as of the last Rebuild() — which in practice meant it looked frozen unless the
    /// player did something that happened to trigger one (switching tabs, or the window losing and
    /// regaining focus), which read as a bug rather than the correct value just going stale. Guarded on
    /// change, same reasoning as SyncScheduleHeader/SyncCompletedHeader.</summary>
    private void SyncScheduleClock()
    {
        if (_scheduleClockLabel == null) return;

        string text = FormatScheduleClock();
        if (text == _lastScheduleClockText) return;
        _lastScheduleClockText = text;
        _scheduleClockLabel.text = text;
    }

    private static string FormatScheduleClock()
        => ServiceLocator.TryGet(out SimulationTimeService time) && time != null
            ? $"Day {time.Day}   ·   {time.Hour:00}:{time.Minute:00}"
            : $"Day {CurrentDay()}";

    /// <summary>
    /// Notices when the real clock crosses into a new 2-hour block and rebuilds the grid so its rows
    /// pick that up. Every row's past/future colouring (see BuildScheduleRow/BuildEmptySlot) is computed
    /// from schedule.CurrentBlock at the moment the row is BUILT — the clock label ticks live on its
    /// own (SyncScheduleClock), but the grid it sits above doesn't, so a slot that was still open at
    /// 03:59 kept reading as open past 04:00 until something unrelated forced a rebuild (a click, a tab
    /// switch). Gated to the Schedule tab: a rebuild is real work, and no other tab's content depends on
    /// this.
    /// </summary>
    private void SyncScheduleElapsed()
    {
        if (_tab != Tab.Schedule) return;
        var schedule = Schedule();
        if (schedule == null) return;

        int day = schedule.CurrentDay;
        int block = schedule.CurrentBlock;

        // Completion lands whenever a truck departs or an order ships — not on a block boundary — so
        // the block check alone would leave a finished trailer un-struck for up to two in-game hours.
        // A signature over just the displayed day's chips is cheap and catches it within the tick.
        string done = CompletedSignature(schedule);
        bool completionChanged = done != _lastCompletedSignature;

        if (day == _lastLiveScheduleDay && block == _lastLiveScheduleBlock && !completionChanged) return;

        _lastCompletedSignature = done;
        Rebuild(); // BuildSchedule re-stamps _lastLiveScheduleDay/_lastLiveScheduleBlock as it runs
    }

    /// <summary>Which of the displayed day's appointments currently read as finished. Compared as a
    /// string rather than diffed properly because it only has to answer "did anything change?" — the
    /// rebuild does the real work.</summary>
    private string CompletedSignature(DockScheduleService schedule)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var appt in schedule.Appointments)
        {
            if (appt == null || appt.Day != _scheduleDay) continue;
            if (schedule.IsComplete(appt)) sb.Append(appt.Id).Append(';');
        }
        return sb.ToString();
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
        => tab == Tab.Completed ? "COMPLETED ORDERS"
         : "OUTBOUND ORDER MANAGER";

    private void Rebuild()
    {
        BuildTabBar();
        if (_titleLabel != null) _titleLabel.text = TitleFor(_tab);
        _tabHeader.Clear();
        _content.Clear();
        _orderListScroll.Clear();
        _orderDetailsBody.Clear();
        _scheduleTimeCells.Clear(); // stale cells belong to elements that were just destroyed
        _lastHScroll = float.NaN;   // force the next poll to re-apply the offset to the new cells
        _completedHeaderRow = null; // the strip was just cleared; this pointed into it
        _lastCompletedHScroll = float.NaN;
        _scheduleHeaderRow = null;  // same reasoning, Schedule tab's own copy
        _lastScheduleHScroll = float.NaN;
        _scheduleClockLabel = null; // the rail was just cleared; this pointed into it
        _lastScheduleClockText = null;
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

        // Accounts AND Bulk Orders own the two-pane split (card list + ORDER DETAILS); every other tab
        // keeps the single full-width scroll view. Bulk was left out of the split originally and read
        // as a different screen for the same job — both tabs are "pick a trailer, see what's on it".
        bool splitTab = _tab == Tab.Accounts || _tab == Tab.BulkOrders;
        _content.style.display = splitTab ? DisplayStyle.None : DisplayStyle.Flex;
        _splitPane.style.display = splitTab ? DisplayStyle.Flex : DisplayStyle.None;

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
        column.style.backgroundColor = new StyleColor(new Color(accent.r, accent.g, accent.b, 0.06f));
        column.style.borderTopWidth = column.style.borderBottomWidth =
            column.style.borderLeftWidth = column.style.borderRightWidth = 2;
        column.style.borderTopColor = column.style.borderBottomColor =
            column.style.borderLeftColor = column.style.borderRightColor = new StyleColor(accent);
        column.style.borderTopLeftRadius = column.style.borderTopRightRadius =
            column.style.borderBottomLeftRadius = column.style.borderBottomRightRadius = 10;

        var titleLabel = MakeText(title, 24, ColTitleText, bold: true);
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.alignSelf = Align.Stretch;
        column.Add(titleLabel);

        var helper = MakeText(helperText, 13, ColSubtleText);
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

        return box;
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
        body.Add(MakeText(contract.Customer != null ? contract.Customer.CompanyName : contract.ContractId,
                          19, ColSubtleText, bold: true));
        body.Add(MakeText("WON'T DEAL WITH YOU YET", 14, ColSubtleText, bold: true));
        // "you have {rep}. {gap} to go" put a full stop between two numbers, so at rep 0 it rendered as
        // "you have 0. 250 to go" and read as the decimal 0.250. Comma-joined into one clause instead —
        // a separator that can never be mistaken for part of a number.
        var need = MakeText($"Needs {contract.ReputationRequired:N0} reputation " +
                            $"({ReputationService.BandLabel(ReputationService.BandFor(contract.ReputationRequired))}) " +
                            $"— you have {rep:N0}, so {contract.ReputationRequired - rep:N0} to go.",
                            14, ColSubtleText);
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
        // shipped" forever while the real money sat on the Completed tab. That tab is where finished
        // deals live now, per ORDER and with the actual revenue on them.
        _footerMessage.text = $"{running.Count} recurring account(s) running. Finished one-offs are on the " +
                              "Completed tab. Cancelling stops future orders; anything already on the " +
                              "board still has to ship.";
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
        cancel.tooltip = "Cancel this account's next order. Only possible while the work is still " +
                         "unreleased — once it's on the floor the order has to ship.";
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

            Cell($"Day {plannedOrder.CreatedDayNumber}:", 48, ColWholesale, bold: true);
            Cell(plannedOrder.OrderNumber ?? "—", 56, ColChipOutText, bold: true);
            Cell($"{plannedOrder.LineItems.Count} item(s)", 62, ColSubtleText);
            Cell($"{plannedOrder.TotalUnits:N0} cs", 46, ColSubtleText);
            // Same colour thresholds as the card's own Approximate Fill Rate below, so a red row here
            // and a red headline there mean the same thing.
            Cell($"{rowFill:P0} fill", 62,
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

        // ONE COLUMN, one variable per line, no bullets. Two columns split the five readings across a
        // reading order the eye doesn't follow, and the bullets bought nothing — every line already
        // starts with its own label. Font down to 15 and the gap to 1px so all five fit the card's
        // height without the card growing; NoWrap on every line so a long value clips rather than
        // silently becoming two lines and pushing the last reading out of view.
        var stats = new VisualElement();
        stats.style.flexGrow = 1;
        stats.style.justifyContent = Justify.Center;

        void Stat(string text, Color color, bool bold = false)
        {
            var line = MakeText(text, 15, color, bold: bold);
            line.style.marginBottom = 1;
            line.style.whiteSpace = WhiteSpace.NoWrap;
            line.style.overflow = Overflow.Hidden;
            stats.Add(line);
        }

        Stat($"Next Order: {nextDrop}", ColSubtleText);
        // No "Shipped:" line. The next order has not been released yet by definition, so it read 0%
        // on every account every time — a reading that never varies is not information.
        Stat($"Cases: {cases:N0}", ColSubtleText);
        Stat($"Pallets: {pallets:N0}", ColSubtleText);
        Stat($"Approximate Fill Rate: {approxFill:P0}",
             approxFill >= 0.95f ? ColMoney : approxFill >= 0.6f ? ColWholesale : ColDangerSoft,
             bold: true);

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

        var title = MakeText("ORDER DETAILS", 22, ColTitleText, bold: true);
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
                           18, ColTitleText, bold: true);
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
                14, ColWholesale, bold: true);
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
            var num = MakeText(li.SkuId, 18, ColTitleText, bold: true);
            num.style.whiteSpace = WhiteSpace.NoWrap;
            idBlock.Add(num);
            if (sku != null && !string.IsNullOrEmpty(sku.ItemDescription))
            {
                var desc = MakeText(sku.ItemDescription, 17, ColSubtleText);
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

        var title = MakeText("ORDER DETAILS", 22, ColTitleText, bold: true);
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.style.marginBottom = 14;
        _orderDetailsBody.Add(title);

        if (order == null)
        {
            _orderDetailsBody.Add(MakeText("Select a bulk order on the left to see what's on it.",
                                           15, ColSubtleText));
            return;
        }

        var name = MakeText(who ?? order.CustomerName, 18, ColTitleText, bold: true);
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

        var warn = MakeText("Product needs a pickslot assigned", 10, ColDangerSoft);
        warn.style.flexGrow = 0.9f; warn.style.flexBasis = 0;
        warn.style.whiteSpace = WhiteSpace.Normal;
        return warn;
    }

    private VisualElement DetailHeaderCell(string text, float flex)
    {
        var cell = MakeText(text, 12, ColSubtleText, bold: true);
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
        var cell = MakeText(text, 14, color, bold: bold);
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

    // ── Tab 3: Schedule ──────────────────────────────────────────────────────

    private void BuildSchedule(OrderArrivalService arrivals)
    {
        var schedule = Schedule();
        if (schedule == null)
        {
            _footerMessage.text = "Dock schedule service isn't running — appointments can't be shown.";
            return;
        }

        // Watermarks what "now" was as of THIS build, so SyncScheduleElapsed can tell when the real
        // clock has crossed into a new block since — every block row's past/future colouring below is
        // computed from schedule.CurrentBlock at build time and, without this, would otherwise only
        // ever refresh on some unrelated rebuild (a click, a tab switch), not as time actually passes.
        _lastLiveScheduleDay = schedule.CurrentDay;
        _lastLiveScheduleBlock = schedule.CurrentBlock;

        // A held selection can go stale between rebuilds — the trailer may have loaded out, or its block
        // elapsed, while it sat picked up. Drop it here rather than leave empty slots advertising a move
        // that TryMove will now refuse. FindById returns null for one that's gone, which locks too.
        if (_selectedAppointmentId != null && schedule.IsLocked(schedule.FindById(_selectedAppointmentId)))
            _selectedAppointmentId = null;

        int today = CurrentDay();
        // Day switcher and legend go in the STATIONARY strip, not the scroll view — they're the frame
        // you read the grid against, so they must not slide away when you scroll right to reach door 9.
        _tabHeader.Add(BuildScheduleHeader(today));

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

        // The frame you read the grid against — legend, the stranded-trailer pool, and the in-game
        // clock — goes ABOVE the grid and inside the STATIONARY strip: twelve block rows always
        // overflow the scroll view, so anything appended after them is permanently below the fold,
        // which is the one place none of the three is any use. You need all of them in view while
        // scrolling the grid hunting for somewhere to put a trailer.
        var unscheduled = schedule.UnscheduledGroups();
        var parked = schedule.ParkedAppointments.ToList();
        _tabHeader.Add(BuildScheduleStrip(unscheduled, parked, today, arrivals));

        // Column header goes LAST into the stationary strip so it ends up directly above row 1 — same
        // ordering reasoning as BuildCompletedSummary/BuildCompletedHeader. Without a label per column,
        // "which door is column 3" was only answerable by counting or reading a chip's own "· D3" tag.
        _scheduleHeaderRow = BuildScheduleColumnHeader(doors);
        _tabHeader.Add(_scheduleHeaderRow);

        for (int block = 0; block < DockScheduleService.BlocksPerDay; block++)
            _content.Add(BuildScheduleRow(schedule, arrivals, doors, block, today));

        // The rows exist again, so a reaction queued by the placement that triggered this rebuild can
        // finally find the cell it belongs over.
        PlayPendingUnhappyFx();

        // The rows were just rebuilt at offset 0 while the view may still be scrolled — re-apply
        // immediately so a rebuild (moving an appointment) doesn't flash the frozen column back to the
        // left of a scrolled grid for a frame before the poller catches up.
        SyncFrozenTimeColumn();

        int booked = schedule.Appointments.Count(a => a.Day == _scheduleDay);
        int capacity = doors.Count * DockScheduleService.BlocksPerDay;

        if (_selectedUnscheduledKey != null)
            _footerMessage.text = "Pick an empty slot to book this trailer in, or click it again to put it down.";
        else if (_selectedAppointmentId != null)
            _footerMessage.text = "Pick an empty slot to move the selected appointment, click it again to drop it, " +
                                  "or click the Unscheduled Trailers pool to send it back unbooked.";
        else
            _footerMessage.text = $"{booked} of {capacity} slots booked · recurring orders auto-book the first free " +
                                  $"block before their due day, bulk orders wait for you to place them — click an " +
                                  $"appointment to move it." +
                                  (unscheduled.Count > 0
                                      ? $"  ·  {unscheduled.Count} trailer(s) still need a door — see above."
                                      : string.Empty);
    }

    private void OnUnscheduledClicked(UnscheduledGroup group)
    {
        _selectedUnscheduledKey = _selectedUnscheduledKey == group.Key ? null : group.Key;
        _selectedAppointmentId = null; // one thing in hand at a time — see _selectedUnscheduledKey
        Rebuild();
    }

    private VisualElement BuildScheduleHeader(int today)
    {
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Column;
        header.style.marginBottom = 4;

        // ── Top row: legend + day switcher in the LEFT half, live clock in the RIGHT half ──
        // Deliberately mirrors BuildScheduleStrip's geometry below: two 50% halves that neither grow
        // nor shrink, inside 2px of horizontal padding standing in for the strip wrapper's 2px
        // border. That puts the boundary between these halves on exactly the same x as the divider
        // between "PO/ORDER DETAILS" and "UNSCHEDULED TRAILERS", and keeps it there at any width.
        //
        // It used to be justifyContent = SpaceBetween with the whole switcher block on the right,
        // which pinned it to the far edge of the panel — nowhere near that divider, and drifting
        // further from it the wider the panel got. Anchoring both to the shared 50% line is what
        // actually keeps the two partitions aligned.
        var topRow = new VisualElement();
        topRow.style.flexDirection = FlexDirection.Row;
        topRow.style.alignItems = Align.Center;
        topRow.style.marginBottom = 4;
        topRow.style.paddingLeft = 2; topRow.style.paddingRight = 2;

        var leftHalf = new VisualElement();
        leftHalf.style.flexBasis = Length.Percent(50);
        leftHalf.style.flexGrow = 0; leftHalf.style.flexShrink = 0;
        leftHalf.style.flexDirection = FlexDirection.Row;
        leftHalf.style.alignItems = Align.Center;
        leftHalf.style.overflow = Overflow.Hidden;
        leftHalf.Add(BuildCompactLegend());

        var daySwitcher = new VisualElement();
        daySwitcher.style.flexDirection = FlexDirection.Row;
        daySwitcher.style.alignItems = Align.Center;
        daySwitcher.style.flexShrink = 0;
        daySwitcher.style.marginLeft = 18;

        var prev = new Button(() => { _scheduleDay = Mathf.Max(today - ScheduleDaysBack, _scheduleDay - 1); Rebuild(); })
            { text = "◀" };
        StyleSquareButton(prev);
        prev.style.width = 39; prev.style.height = 39;
        daySwitcher.Add(prev);

        string when = _scheduleDay == today ? "today"
                    : _scheduleDay == today + 1 ? "tomorrow"
                    : _scheduleDay < today ? $"{today - _scheduleDay} day(s) ago"
                    : $"in {_scheduleDay - today} day(s)";
        var dayLabel = MakeText($"Day {_scheduleDay} · {when}", 30, ColChipOutText, bold: true);
        dayLabel.style.marginLeft = 12; dayLabel.style.marginRight = 12;
        daySwitcher.Add(dayLabel);

        var next = new Button(() => { _scheduleDay = Mathf.Min(today + ScheduleDaysAhead, _scheduleDay + 1); Rebuild(); })
            { text = "▶" };
        StyleSquareButton(next);
        next.style.width = 39; next.style.height = 39;
        daySwitcher.Add(next);
        leftHalf.Add(daySwitcher);

        // The partition. A real 2px bar rather than a "|" glyph, so it is the same width and colour
        // as the strip divider it has to line up with (that divider is the left hemisphere's 2px
        // borderRight in the same ColBorder). marginLeft:auto pins it to the right edge of this half;
        // marginRight:-1 pulls it back by half its own width so the 2px line straddles the boundary
        // and its CENTRE — not its left edge — sits on the divider's x.
        var clockSep = new VisualElement();
        clockSep.style.width = 2;
        clockSep.style.height = 28;
        clockSep.style.flexShrink = 0;
        clockSep.style.backgroundColor = new StyleColor(ColBorder);
        clockSep.style.marginLeft = StyleKeyword.Auto;
        clockSep.style.marginRight = -1;
        leftHalf.Add(clockSep);

        topRow.Add(leftHalf);

        // ── RIGHT half: the live clock, sitting just past the partition ──
        // Same font size as the day-switcher label so the two read as one continuous line of header
        // text rather than a heading with a footnote bolted on.
        var rightHalf = new VisualElement();
        rightHalf.style.flexBasis = Length.Percent(50);
        rightHalf.style.flexGrow = 0; rightHalf.style.flexShrink = 0;
        rightHalf.style.flexDirection = FlexDirection.Row;
        rightHalf.style.alignItems = Align.Center;
        rightHalf.style.overflow = Overflow.Hidden;

        _scheduleClockLabel = MakeText(FormatScheduleClock(), 30, ColWholesale, bold: true);
        _scheduleClockLabel.style.marginLeft = 14;
        _lastScheduleClockText = _scheduleClockLabel.text;
        rightHalf.Add(_scheduleClockLabel);

        topRow.Add(rightHalf);
        header.Add(topRow);

        return header;
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
    private VisualElement BuildScheduleStrip(List<UnscheduledGroup> unscheduled,
                                             List<DockAppointment> parked, int today,
                                             OrderArrivalService arrivals)
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

        // Parked trailers share the pool with never-booked freight and count toward everything the
        // caption reports — from the player's side they're the same problem ("this has no door"), and
        // a count that ignored the trailer they just pulled off would read as though it vanished,
        // which is the exact bug parking exists to fix.
        bool anyLate = unscheduled.Any(g => g.EarliestDueDay < today) || parked.Any(a => a.Day < today);
        int strandedOrders = unscheduled.Sum(g => g.OrderIds.Count) + parked.Sum(a => a.OrderIds.Count);
        int poolCount = unscheduled.Count + parked.Count;

        // ── Caption row: LEGEND / UNSCHEDULED TRAILERS, each centred over its half below ──
        // Sits above the bordered halves rather than as the first line inside them, so the two
        // captions read as headers over the box instead of body text at its top-left corner.
        var captions = new VisualElement();
        captions.style.flexDirection = FlexDirection.Row;
        captions.style.paddingTop = 6;
        captions.style.borderBottomWidth = 2;
        captions.style.borderBottomColor = new StyleColor(ColBorder);

        var legendCaptionHalf = new VisualElement();
        legendCaptionHalf.style.flexBasis = Length.Percent(50);
        legendCaptionHalf.style.flexGrow = 0; legendCaptionHalf.style.flexShrink = 0;
        legendCaptionHalf.style.alignItems = Align.Center;
        legendCaptionHalf.Add(MakeStripCaption("PO/ORDER DETAILS", ColSubtleText));
        captions.Add(legendCaptionHalf);

        var unscheduledCaptionHalf = new VisualElement();
        unscheduledCaptionHalf.style.flexBasis = Length.Percent(50);
        unscheduledCaptionHalf.style.flexGrow = 0; unscheduledCaptionHalf.style.flexShrink = 0;
        unscheduledCaptionHalf.style.alignItems = Align.Center;
        unscheduledCaptionHalf.Add(MakeStripCaption(
            poolCount == 0
                ? "UNSCHEDULED TRAILERS"
                : $"UNSCHEDULED TRAILERS — {poolCount} ({strandedOrders} order(s))",
            anyLate ? ColDangerSoft : ColSubtleText));
        captions.Add(unscheduledCaptionHalf);

        wrapper.Add(captions);

        // Align.Stretch, not Center: the divider between the halves is the left half's right border, so
        // it only runs the full height of the strip if that half is stretched to the taller sibling.
        var halves = new VisualElement();
        halves.style.flexDirection = FlexDirection.Row;
        halves.style.alignItems = Align.Stretch;

        // ── LEFT hemisphere: item numbers, descriptions and quantities for whatever's selected ──
        // Used to be the LEGEND (three swatch rows) — that moved up into BuildScheduleHeader, compacted,
        // to make room for this: click any inbound PO or outbound trailer (on the grid, parked, or
        // still in the pool) and its contents show up here instead of having to open a separate panel.
        var left = new VisualElement();
        left.style.flexBasis = Length.Percent(50);
        left.style.flexGrow = 0; left.style.flexShrink = 0;
        left.style.justifyContent = Justify.FlexStart;
        left.style.paddingTop = 6; left.style.paddingBottom = 6;
        left.style.paddingLeft = 14; left.style.paddingRight = 14;
        left.style.borderRightWidth = 2;
        left.style.borderRightColor = new StyleColor(ColBorder);
        left.Add(BuildPoOrderDetailsPanel(unscheduled));
        halves.Add(left);

        // ── RIGHT hemisphere: trailers with no door yet ──
        var right = new VisualElement();
        right.style.flexBasis = Length.Percent(50);
        right.style.flexGrow = 0; right.style.flexShrink = 0;
        right.style.justifyContent = Justify.Center;
        right.style.paddingTop = 6; right.style.paddingBottom = 6;
        right.style.paddingLeft = 14; right.style.paddingRight = 14;

        // Holding a booked chip turns the whole pool into a drop target — put it down anywhere in here
        // (not just on an empty grid slot) to unbook it and send it back to this pool. Individual pool
        // boxes stop their own click from bubbling up to this handler (see BuildPoolBox), so a click
        // that lands ON a box still means "select that box instead," never "also drop what I'm holding."
        // Only a trailer that's still ON the grid can be returned to the pool. A held trailer that is
        // already parked is in hand to be PUT BACK, so the pool must not offer to re-park it — that
        // click would be a no-op that reads as the trailer disappearing again.
        var heldAppt = _selectedAppointmentId != null ? Schedule()?.FindById(_selectedAppointmentId) : null;
        bool returningAppointment = heldAppt != null && !heldAppt.Parked;
        if (returningAppointment)
        {
            right.style.backgroundColor = new StyleColor(new Color(ColOrange.r, ColOrange.g, ColOrange.b, 0.12f));
            right.RegisterCallback<ClickEvent>(_ => OnReturnAppointmentToPoolClicked());
        }

        if (poolCount == 0)
        {
            var clear = MakeText(returningAppointment
                                     ? "Click here to pull that trailer off the grid — it'll wait here."
                                     : "Every trailer has a door. Nothing waiting.",
                                 16, returningAppointment ? ColOrangeText : ColSubtleText);
            clear.style.unityTextAlign = TextAnchor.MiddleCenter;
            clear.style.whiteSpace = WhiteSpace.Normal;
            right.Add(clear);
        }
        else
        {
            // Wrap rather than a horizontal scroller: the strip's height should be proportional to how
            // bad the backlog actually is, and a scroller hides exactly the thing it's reporting.
            var boxes = new VisualElement();
            boxes.style.flexDirection = FlexDirection.Row;
            boxes.style.flexWrap = Wrap.Wrap;
            boxes.style.alignItems = Align.Center;

            // Parked trailers FIRST: the player put them there a moment ago and is about to put them
            // back, so they're the ones being looked for. Never-booked freight is the standing backlog
            // and can wait its turn.
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
            right.Add(boxes);

            bool holdingParked = heldAppt != null && heldAppt.Parked;
            string hintText = returningAppointment
                ? "Click an empty spot here to pull that trailer off the grid — it'll wait here."
                : holdingParked
                    ? "Now click an open slot in the grid to put it back — or click the box again to let go."
                    : _selectedUnscheduledKey != null
                        ? "Now click an open slot in the grid — or click the box again to put it down."
                        : "Click a box, then click an open slot in the grid to book it.";
            var hint = MakeText(hintText, 15,
                                returningAppointment || holdingParked || _selectedUnscheduledKey != null
                                    ? ColOrangeText : ColSubtleText);
            hint.style.marginTop = 2;
            right.Add(hint);
        }

        halves.Add(right);
        wrapper.Add(halves);

        return wrapper;
    }

    private Label MakeStripCaption(string text, Color color)
    {
        var caption = MakeText(text, 14, color, bold: true);
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
        string dayText = late ? $"{today - group.EarliestDueDay}d LATE" : $"Day {group.EarliestDueDay}";

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
        string due = late ? $"{today - group.EarliestDueDay} day(s) LATE"
                   : group.EarliestDueDay == today ? "due today"
                   : $"due day {group.EarliestDueDay}";
        box.tooltip = $"{group.CustomerName} · {PalletLabel(pallets)} · {group.OrderIds.Count} order(s) with no dock appointment · {due}\n" +
                      (selected ? "Click again to put it down."
                                : "Click, then click an open slot to book it.");
        return box;
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
        textCol.Add(titleLabel);

        var subtitleLabel = MakeText(subtitle, 12, ink, bold: true);
        subtitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        subtitleLabel.style.marginTop = 1; subtitleLabel.style.marginBottom = 0;
        textCol.Add(subtitleLabel);

        var detailLabel = MakeText(detail, 11, ink);
        detailLabel.style.whiteSpace = WhiteSpace.NoWrap;
        detailLabel.style.overflow = Overflow.Hidden;
        detailLabel.style.opacity = 0.85f;
        detailLabel.style.marginTop = 1; detailLabel.style.marginBottom = 0;
        textCol.Add(detailLabel);

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

        string title, subtitle, detail;
        int pallets;
        if (isPo)
        {
            pallets = PalletCountForPO(appt.ShipmentPoNumber);
            title = $"PO {appt.ShipmentPoNumber}";
            subtitle = late ? $"{today - appt.Day}d LATE" : $"Day {appt.Day}";
            detail = $"{PalletLabel(pallets)} · {appt.CustomerName}";
        }
        else
        {
            pallets = 0; // a held outbound trailer carries no line items of its own to count
            title = appt.CustomerName;
            subtitle = late ? $"{today - appt.Day}d LATE" : $"Day {appt.Day}";
            detail = $"{DockScheduleService.BlockLabel(appt.BlockIndex)} · held";
        }

        var box = BuildFlagBox(
            icon: IconForCustomer(appt.CustomerId, appt.ContractId, Arrivals()),
            title: title, subtitle: subtitle, detail: detail,
            fill: fill, edge: edge, ink: ink,
            selected: selected);

        // Same reason as BuildPoolBox: the pool container is itself a drop target, and a click on a
        // specific box must mean "select this one", never "also drop what I'm holding".
        box.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); OnParkedClicked(appt); });
        box.tooltip = isPo
            ? $"Inbound PO {appt.ShipmentPoNumber} from {appt.CustomerName} · {PalletLabel(pallets)} · wanted day {appt.Day}\n" +
              $"No door booked — the truck won't leave the supplier until you give it one.\n" +
              (selected ? "Click an open slot to book it, or click again to let go."
                        : "Click, then click an open slot to book its door and time.")
            : $"{appt.CustomerName} · held off the grid by you · " +
              $"{DockScheduleService.BlockLabel(appt.BlockIndex)} on day {appt.Day}\n" +
              (selected ? "Click an open slot to put it back, or click again to let go."
                        : "Click, then click an open slot to put it back on the grid.");
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
    private VisualElement BuildScheduleColumnHeader(List<int> doors)
    {
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.marginBottom = 6;

        var spacer = new VisualElement();
        spacer.style.width = TimeColWidth;
        spacer.style.minWidth = TimeColWidth;
        spacer.style.flexShrink = 0;
        header.Add(spacer);

        foreach (int door in doors)
        {
            var label = MakeText($"Door {door}", 24, ColSubtleText, bold: true);
            label.style.width = SlotWidth;
            label.style.minWidth = SlotWidth;
            label.style.flexShrink = 0;
            label.style.marginRight = 5;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(label);
        }

        var flagSpacer = new VisualElement();
        flagSpacer.style.width = FullFlagWidth;
        flagSpacer.style.minWidth = FullFlagWidth;
        flagSpacer.style.flexShrink = 0;
        header.Add(flagSpacer);

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

        // Indexed by the door's OWN number, not by position among booked appointments — appts is
        // whichever doors happen to be taken in this block, not necessarily the first N doors, so
        // compacting them to the front (the old i < appts.Count approach) put a door-3 booking under
        // the "Door 1" header the instant door 1 or 2 was still open. This is also what makes explicit
        // door choice possible below: a click always names the exact door column it landed on.
        foreach (int doorNumber in doors)
        {
            var appt = appts.FirstOrDefault(a => a.DoorNumber == doorNumber);
            bool isChip = appt != null;
            var slot = isChip
                ? BuildAppointmentChip(schedule, appt, arrivals)
                : BuildEmptySlot(block, doorNumber, past);
            // Past blocks dim their SLOTS individually rather than the whole row. Row-level opacity
            // would also fade the frozen time cell, and a translucent frozen column lets the slots
            // show through it as they scroll under — exactly what freezing it was meant to prevent.
            //
            // Only booked chips fade via opacity here. A past EMPTY slot gets its own red tint instead
            // (see BuildEmptySlot) — a faded version of the same blue "open" box read as barely
            // different from a merely-quiet one; opacity on top of that tint would just wash it back
            // out, undoing the contrast this was added for.
            if (past && isChip) slot.style.opacity = 0.45f;
            // Named so a cell can be found again AFTER a rebuild. Every placement rebuilds the whole
            // grid, which destroys the element the player actually clicked — so an effect anchored to
            // that element would be anchored to a corpse. The name is the (block, door) coordinate,
            // which survives the rebuild because it describes the position rather than the object.
            slot.name = SlotElementName(block, doorNumber);
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

        // Struck through once the work is genuinely DONE — order shipped, or the inbound PO received
        // and its truck gone. Keyed on IsComplete rather than the `locked` flag above: locked also
        // covers "its block has elapsed", and a trailer that's late with freight still on it is the
        // opposite of finished. Dimming alone wasn't enough to tell those two apart at a glance.
        bool complete = schedule.IsComplete(appt);
        if (complete) AddStrikeThrough(chip, text);

        // An outbound trailer with nothing on it yet is a RESERVATION, not work. It looked identical
        // to a loaded one, which is how a booking at 06:00 for an order that doesn't arrive until
        // 17:00 reads as "the Work Queue is broken" rather than "there is nothing to pick yet".
        // Drawn hollow — the kind's edge colour kept, the fill dropped — so it reads as an outline
        // waiting to be filled in, without costing any of the ~110px the customer name has to live in.
        bool awaitingFreight = !complete && appt.Kind != AppointmentKind.Inbound && appt.OrderIds.Count == 0;
        if (awaitingFreight)
            chip.style.backgroundColor = new StyleColor(new Color(fill.r, fill.g, fill.b, 0.12f));

        // The tiny customer icon, immediately left of the name.
        chip.Add(MakeIcon(IconForAppointment(appt, arrivals), IconSizeTiny, 3, marginRight: 5));

        // Name flexes and is allowed to clip; the door number is a separate fixed-width element that
        // never can. Both in one label meant a long company name pushed "· D3" off the end of the
        // chip — and the door is the one thing on the chip you can't work out from anything else.
        //
        // AN INBOUND PO IS LABELLED "Inbound PO", NOT BY ITS SUPPLIER. Every purchase order comes from
        // the same wholesaler, so the supplier name is a constant — it filled the chip with the one
        // word that distinguishes nothing, and read as a customer, which is the opposite of what an
        // inbound trailer is. What the player needs off a glance at the grid is the DIRECTION of the
        // freight; the PO number is on the tooltip and in the Purchasing panel for when they need to
        // identify which one.
        var label = MakeText(ChipLabelFor(appt), 11, text);
        label.style.flexGrow = 1;
        label.style.flexShrink = 1;
        label.style.overflow = Overflow.Hidden;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        if (awaitingFreight) label.style.opacity = 0.75f;
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

        // The tooltip is where the specifics live, since the chip itself is only ~150px. An inbound
        // trailer carries a PO rather than orders, so counting OrderIds on it would always print
        // "0 order(s)" — true and useless.
        // "Done" outranks the lock reason: when a trailer is finished, that IS why it can't be moved,
        // and the generic lock text ("its block has passed") would name a lesser, less useful truth.
        string state = complete
            ? (IsInboundPo(appt) ? "received — this PO is done" : "shipped — this trailer is done")
            : locked ? lockReason : "click to move";

        // The single most useful thing to say about an empty trailer is WHEN it stops being empty.
        // Without it the player is left to work out for themselves that a 06:00 booking can't be
        // picked because the order it's for doesn't get raised until the contract's cutoff hour.
        if (awaitingFreight)
            state = ArrivalHintFor(appt, arrivals) + " · " + state;

        chip.tooltip = IsInboundPo(appt)
            ? $"Inbound PO {appt.ShipmentPoNumber} · {appt.CustomerName} · {appt.TimeLabel} · " +
              $"door {appt.DoorNumber} · " + state
            : $"{appt.CustomerName} · {appt.TimeLabel} · door {appt.DoorNumber} · " +
              $"{appt.OrderIds.Count} order(s) · " + state;
        return chip;
    }

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
    /// Draws a line straight through a chip to mark it finished.
    ///
    /// A real overlay element rather than a rich-text strikethrough tag on the label: the chip is an
    /// icon, a name and a door number, so a tag would only cross out the middle one and leave the rest
    /// standing. The line has to span the whole chip to read as "this trailer is done".
    ///
    /// Absolutely positioned and PickingMode.Ignore so it neither takes part in the chip's row layout
    /// nor swallows the click that selects/moves it. Added last so it draws over the contents.
    /// </summary>
    private static void AddStrikeThrough(VisualElement chip, Color ink)
    {
        var line = new VisualElement();
        line.pickingMode = PickingMode.Ignore;
        line.style.position = Position.Absolute;
        line.style.left = 4;
        line.style.right = 4;
        line.style.top = Length.Percent(50);
        line.style.height = 2;
        line.style.marginTop = -1;   // centre the 2px rule on the 50% line
        // Opaque even though the finished chip as a whole is dimmed — the strike is the signal, and a
        // faded line on a faded chip is what made "done" hard to spot in the first place.
        line.style.backgroundColor = new StyleColor(new Color(ink.r, ink.g, ink.b, 0.95f));
        chip.Add(line);
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
    private static string ChipLabelFor(DockAppointment appt)
        => IsInboundPo(appt) ? "Inbound PO" : appt.CustomerName;

    /// <summary>How a trailer is named in a TOAST. Same rule as the chip, but with room for the PO
    /// number — a message saying "Wholesale Supply's trailer is off the grid" names the one thing
    /// every purchase order has in common, and reads as though a customer were involved.</summary>
    private static string AppointmentDisplayName(DockAppointment appt)
        => IsInboundPo(appt) ? $"Inbound PO {appt.ShipmentPoNumber}" : appt.CustomerName;

    private VisualElement BuildEmptySlot(int block, int doorNumber, bool past)
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
        // Past reads as red, not just a fainter version of the normal blue box — a faded "open" slot
        // and a merely-quiet one were too close in contrast at a glance. Border, fill and label all
        // shift together so it reads as "gone," not "dim."
        slot.style.borderTopColor = slot.style.borderBottomColor =
            slot.style.borderLeftColor = slot.style.borderRightColor =
                new StyleColor(past ? ColDanger : ColBlueEdge);
        slot.style.borderTopLeftRadius = slot.style.borderTopRightRadius =
            slot.style.borderBottomLeftRadius = slot.style.borderBottomRightRadius = 5;

        if (past)
            slot.style.backgroundColor = new StyleColor(new Color(ColDanger.r, ColDanger.g, ColDanger.b, 0.16f));

        bool booking = _selectedUnscheduledKey != null;
        bool canDrop = (booking || _selectedAppointmentId != null) && !past;
        var label = MakeText(past ? "— past —" : !canDrop ? "— open —" : booking ? "book here" : "move here", 11,
                             past ? ColDangerSoft : canDrop ? ColOrangeText : ColEmptyText);
        label.style.height = IconSizeTiny;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        slot.Add(label);

        if (canDrop)
        {
            slot.style.backgroundColor = new StyleColor(new Color(ColOrange.r, ColOrange.g, ColOrange.b, 0.18f));
            slot.RegisterCallback<ClickEvent>(_ => OnSlotClicked(block, doorNumber));
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
        var orderNum = AddDoneCell(row, order.OrderNumber ?? ShortOrderId(order.OrderId), DoneOrderWidth, ColSubtleText, DoneColumn.Order);
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
