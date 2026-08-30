using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Economy;
using GameCore.Events;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// PURCHASING — where raw stock comes into the building. Play-bar key 9.
///
/// The inbound counterpart to the Contracts panel: that one is demand the player accepts, this is
/// supply the player commits to. Three tabs, mirroring the life of a purchase order:
///
///   INBOUND ORDER CREATION  comparison shopping across every vendor at once — a collapsible group
///                           per house, each with its own independent load, so the same SKU can be
///                           priced and bought from several vendors side by side before any of them
///                           dispatch. Replaced the old single-vendor "pick one house, build one
///                           basket" Create tab entirely, taking over both its name and position.
///   VENDORS                 the supplier roster — partnership level, fill rate, catalogue — plus The
///                           Broker's salvage loads and the Spot Deals board (moved here once the old
///                           Create tab that used to host them was removed), and the "Order from
///                           Vendor" shortcut into Inbound Order Creation pre-filtered to one house.
///                           Moved here from ContractsPanel: vendors are suppliers, so this is the
///                           inbound side's job.
///   PO LIST                 orders raised and not yet finished — what's coming and when.
///
/// BUILT PROGRAMMATICALLY, like ShiftManagerPanel / WorkQueuePanel / ContractsPanel, rather than from
/// a UXML+USS pair. This panel is a data list with per-row controls, not bespoke art, and the UXML
/// route carries three documented footguns in this project (a parse error that silently yields an
/// empty tree, UIDocument cloning before its visualTreeAsset is set, and SetActive-based visibility
/// that doesn't actually detach from the panel). See RackSetupUI's notes in CLAUDE.md.
///
/// The look is deliberately the ContractsPanel palette — navy card, blue rules, orange for the
/// actions that commit — because the two panels are the same job pointed in opposite directions and
/// should not look like they came from different games.
/// </summary>
public class PurchasingPanel : IUIPanel
{
    // ── Palette (matches ContractsPanel exactly) ─────────────────────────────
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
    private static readonly Color ColBlueEdge    = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColDanger      = new Color(0xE2 / 255f, 0x4B / 255f, 0x4A / 255f, 1f);
    private static readonly Color ColDangerSoft  = new Color(0xF0 / 255f, 0x95 / 255f, 0x95 / 255f, 1f);
    private static readonly Color ColStat        = new Color(30f / 255f, 40f / 255f, 52f / 255f, 1f);
    private static readonly Color ColEmptyText   = new Color(0x4D / 255f, 0x65 / 255f, 0x77 / 255f, 1f);
    private static readonly Color ColTabIdle     = new Color(28f / 255f, 38f / 255f, 50f / 255f, 1f);
    private static readonly Color ColTabHover    = new Color(40f / 255f, 54f / 255f, 70f / 255f, 1f);
    private static readonly Color ColChipOutText = new Color(0x9F / 255f, 0xCB / 255f, 0xE4 / 255f, 1f);

    // The two big action buttons. Red for the one that throws the order away, green for the one that
    // commits it — the only place on this panel where colour carries meaning beyond emphasis.
    private static readonly Color ColCancelRed        = new Color(0xC4 / 255f, 0x3D / 255f, 0x35 / 255f, 1f);
    private static readonly Color ColCancelRedEdge    = new Color(0x7E / 255f, 0x24 / 255f, 0x1E / 255f, 1f);
    private static readonly Color ColCancelRedHover   = new Color(0xD8 / 255f, 0x4C / 255f, 0x42 / 255f, 1f);
    private static readonly Color ColCreateGreen      = new Color(0x3E / 255f, 0xA1 / 255f, 0x55 / 255f, 1f);
    private static readonly Color ColCreateGreenEdge  = new Color(0x24 / 255f, 0x66 / 255f, 0x33 / 255f, 1f);
    private static readonly Color ColCreateGreenHover = new Color(0x4C / 255f, 0xB8 / 255f, 0x65 / 255f, 1f);
    /// <summary>BUY price text — matches the orange Tad circled in his mockup.</summary>
    private static readonly Color ColBuyPrice         = new Color(0xE0 / 255f, 0x8E / 255f, 0x30 / 255f, 1f);
    /// <summary>SELL price text — matches the yellow Tad circled in his mockup.</summary>
    private static readonly Color ColSellPrice        = new Color(0xE8 / 255f, 0xD4 / 255f, 0x3C / 255f, 1f);

    // Multi-vendor tab's DEALS button — same red family as VendorRow's deal bar (ColDealRed there),
    // reusing ColDanger as the fill so it also matches this tab's own truck-fill-bar red.
    private static readonly Color ColDealRedEdge  = new Color(0x6E / 255f, 0x1C / 255f, 0x19 / 255f, 1f);
    private static readonly Color ColDealRedHover = new Color(0xF0 / 255f, 0x6A / 255f, 0x69 / 255f, 1f);

    /// <summary>Line-cost plate colours, lifted from the mock: a near-black plate with a muted caption
    /// over a warm tan figure. Its own palette on purpose — it's the one number that changes as you
    /// press the steppers, and it has to pop off a card that's already blue-on-navy.</summary>
    private static readonly Color ColPlateBg      = new Color(0.04f, 0.05f, 0.07f, 1f);
    private static readonly Color ColPlateCaption = new Color(0x9A / 255f, 0xA6 / 255f, 0xB2 / 255f, 1f);
    private static readonly Color ColPlateValue   = new Color(0xF0 / 255f, 0xC2 / 255f, 0x7A / 255f, 1f);

    // Widened from the original 1180x760 once the VENDORS grid picked up six full-size data columns
    // plus the wide "ORDER FROM VENDOR" button — at the old width that content needed an internal
    // horizontal scrollbar to reach the button even at fill-screen scale (a transform scale zooms the
    // whole panel, it doesn't add usable internal width). This is wide enough for every VENDORS column
    // plus the button with no horizontal scroll on a normal desktop resolution.
    private const float ModalWidth  = 1600f;
    private const float ModalHeight = 820f;
    /// <summary>Narrowest the window can be dragged before the two item columns stop being readable.</summary>
    private const float ModalMinWidth = 1300f;
    /// <summary>Title-bar chrome buttons (resize, close). Also referenced from Show(), which is why
    /// it's a field rather than the local const it used to be — Build()'s closures aren't reachable
    /// from there.</summary>
    private const float TitleButtonSize = 48f;

    private const float QtyFieldWidth = 82f;
    private const float StepButtonSize = 30f;

    /// <summary>Compact commit controls: the prominent order total remains readable without taking a
    /// full card-height away from the item catalogue.</summary>
    private const float ActionButtonHeight = 42f;
    private const float ActionButtonWidth  = 180f;

    /// <summary>Line-cost plate stays legible but no longer steals item-description width in a
    /// two-column catalogue.</summary>
    private const float LineCostPlateWidth = 118f;

    /// <summary>Shared width for the stacked CANCEL PO / SCHEDULER buttons on a PO card, so they line
    /// up as one block rather than two ragged ends.</summary>
    private const float ActionLinkWidth = 132f;


    // MultiVendor is what's shown as "Inbound Order Creation" — the old single-vendor Create tab
    // (which used to own that name) is gone; MultiVendor took over both its name and its position as
    // the default/first tab.
    private enum Tab { Vendors, PoList, MultiVendor }
    private Tab _tab = Tab.MultiVendor;

    private readonly VisualElement _overlay;
    private readonly VisualElement _modal;
    private readonly VisualElement _tabBar;
    private readonly VisualElement _tabHeader;
    private readonly ScrollView _content;
    private readonly Label _footerMessage;
    private Label _titleLabel;
    private Button _scaleBtn;
    private ResizableWindow _resizeWindow;
    private bool _visible;
    private bool _placed;   // false until the first Show centres it
    private bool _dragging;
    private Vector2 _dragOffset;

    /// <summary>Inbound Order Creation's baskets — one independent in-progress load PER VENDOR
    /// (vendorId -> (skuId -> cases)), so comparing/building several vendors' loads side by side never
    /// has one clobbering another.</summary>
    private readonly Dictionary<string, Dictionary<string, int>> _multiBaskets = new();

    /// <summary>Which vendor groups are expanded on Inbound Order Creation. Survives Rebuild() (that
    /// tab rebuilds its content fresh every time, unlike VendorsTabView's cached root) so opening a
    /// vendor's list doesn't collapse the moment a quantity change triggers a repaint.</summary>
    private readonly HashSet<string> _expandedMultiVendors = new();

    /// <summary>Snapshot of _expandedMultiVendors taken the moment the Item filter goes from "All
    /// Items" to a specific SKU — null whenever no item filter is active. Every vendor carrying the
    /// selected item is force-expanded while searching (see AddItemOption's click handler below), and
    /// this is what gets restored once the filter clears back to "All Items", so searching for an item
    /// doesn't permanently blow away whatever the player had manually expanded before.</summary>
    private HashSet<string> _expandedBeforeItemFilter = null;

    /// <summary>Vendor-filter state for Inbound Order Creation's header filter dropdown. Empty
    /// `_multiVendorFilterVendorIds` means "no specific vendors chosen" (show all, subject to the
    /// Critical Items toggle below) — a dropdown replacing the old free-text search box, per Tad's
    /// request, and supporting more than one vendor selected at once.</summary>
    private readonly HashSet<string> _multiVendorFilterVendorIds = new();

    /// <summary>"Critical Items" special filter entry — shows only vendors currently carrying at
    /// least one item in net demand (see CountCriticalItems), combined (AND) with any selected
    /// vendors above.</summary>
    private bool _multiVendorFilterCriticalOnly = false;

    /// <summary>Whether the filter dropdown's popout panel is currently open.</summary>
    private bool _multiVendorFilterOpen = false;

    private enum MultiVendorSortMode { NameAZ, PartnershipHighToLow, PartnershipLowToHigh }

    /// <summary>Sort order for the vendor groups on Inbound Order Creation. Defaults to alphabetical
    /// so the list reads the same way it always has until the player deliberately asks for a
    /// partnership-driven order.</summary>
    private MultiVendorSortMode _multiVendorSortMode = MultiVendorSortMode.NameAZ;

    /// <summary>Whether the sort dropdown's popout panel is currently open.</summary>
    private bool _multiVendorSortOpen = false;

    /// <summary>Item filter for Inbound Order Creation — narrows every expanded vendor group down to
    /// one SKU, so "the same item can appear under several vendors at different prices" (see the hint
    /// label) becomes something the player can actually line up side by side instead of expanding
    /// every vendor and hunting for it. Null = "All Items".</summary>
    private string _multiVendorItemFilterSkuId = null;

    /// <summary>Whether the item filter dropdown's popout panel is currently open.</summary>
    private bool _multiVendorItemFilterOpen = false;

    /// <summary>Root passed into the constructor, kept for the lifetime of the panel — this is the
    /// only ancestor common to every click on screen (including clicks that land on nothing and get
    /// dispatched to the document root itself), so the global "close the open dropdown" capture
    /// handler below is registered on it rather than on _overlay (whose own pickingMode is Ignore).</summary>
    private readonly VisualElement _root;

    /// <summary>Pending auto-close timers for the three Inbound Order Creation header dropdowns
    /// (vendor filter, sort, item filter) — each one restarts on every interaction (open, or a
    /// Rebuild() that happens while still open) and fires the popout closed after 3s of being left
    /// alone, per Tad's request that the dropdown not require hunting for its own arrow to dismiss.</summary>
    private IVisualElementScheduledItem _multiVendorFilterAutoClose;
    private IVisualElementScheduledItem _multiVendorSortAutoClose;
    private IVisualElementScheduledItem _multiVendorItemAutoClose;

    /// <summary>Deal discounts claimed on Inbound Order Creation, keyed by DealKey(vendorId, skuId) —
    /// this tab prices several vendors' loads at once, so the same SKU can carry a claimed discount
    /// under one vendor while carrying none (or a different one) under another. Cleared per vendor
    /// when that vendor's load dispatches (see CommitDispatchVendorOrder).</summary>
    private readonly Dictionary<string, float> _multiDealDiscountByKey = new();

    private static Font _lilita;

    /// <summary>The VENDORS tab's builder — constructed once alongside every other tab's state so
    /// its own rows survive across Rebuild() calls the same way the Create tab's basket does. Moved
    /// here from ContractsPanel: vendors are suppliers, so browsing them belongs on the inbound
    /// (purchasing) side, not the outbound (contracts) side.</summary>
    private VendorsTabView _vendorsTabView;
    private VisualElement _vendorsTabRoot;
    private VisualElement _vendorsPane;

    /// <summary>Root scroll view for the INBOUND ORDER CREATION tab (the multi-vendor one — the old
    /// single-vendor Create tab that used to own that name is gone). Unlike _vendorsPane, this is
    /// cleared and rebuilt every Rebuild() rather than built once and cached — its content changes
    /// with every qty-stepper press.</summary>
    private ScrollView _multiVendorPane;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public PurchasingPanel(VisualElement root)
    {
        _root = root;
        _overlay = Build(out _modal, out _tabBar, out _tabHeader, out _content, out _footerMessage);
        root.Add(_overlay);
        _overlay.Add(BuildConfirmDialog());

        // ROOT-LEVEL capture, same pattern SaveLoadWindowController uses for its own outside-click
        // close — fires before any child can intercept, and worldBound hit-testing means it doesn't
        // matter what's visually drawn on top. Registered once here (not rebuilt with the bar) so it
        // always finds whichever popout/button instance is currently live via name lookup.
        _root.RegisterCallback<PointerDownEvent>(OnGlobalPointerDownForDropdowns, TrickleDown.TrickleDown);

        ServiceLocator.TryGet<VendorEconomyService>(out var vendorEconomy);
        ServiceLocator.TryGet<VendorPerformanceTracker>(out var vendorTracker);
        var vendorSfx = Resources.Load<VendorUiSfxConfig>("VendorUiSfx");
        _vendorsTabView = new VendorsTabView(vendorEconomy, vendorTracker, vendorSfx);

        EventManager.Instance?.Subscribe<string>(GameEvents.Vendor.OnOrderFromVendorRequested, OnOrderFromVendorRequested);

        // The PO List's docked/UNLOADING status (see LiveDockStatus) only gets recomputed on a
        // Rebuild(), and nothing else fires one while a truck rolls up to a door and starts unloading
        // mid-view — so it would sit on "InTransit" the whole time the player was watching it happen.
        // A light poll only while that tab is actually open and visible fixes it without adding a
        // general-purpose refresh loop the rest of this panel doesn't need.
        _overlay.schedule.Execute(() =>
        {
            if (_visible && _tab == Tab.PoList) Rebuild();
        }).Every(1000);

        Hide();
    }

    public void Dispose()
    {
        EventManager.Instance?.Unsubscribe<string>(GameEvents.Vendor.OnOrderFromVendorRequested, OnOrderFromVendorRequested);
        _root.UnregisterCallback<PointerDownEvent>(OnGlobalPointerDownForDropdowns, TrickleDown.TrickleDown);
        if (_overlay.parent != null) _overlay.RemoveFromHierarchy();
    }

    /// <summary>Closes whichever of the three Inbound Order Creation header dropdowns is open the
    /// moment a left- or right-click lands outside both its popout and its own trigger button — the
    /// trigger button itself is excluded so its own click handler keeps sole ownership of the
    /// open/close toggle instead of this handler fighting it (see CloseMultiVendorPopoutIfOutside).</summary>
    private void OnGlobalPointerDownForDropdowns(PointerDownEvent evt)
    {
        if (evt.button != 0 && evt.button != 1) return; // left or right only
        Vector2 pos = evt.position;
        CloseMultiVendorPopoutIfOutside(ref _multiVendorFilterOpen, "MultiVendorFilterPopout",
            "MultiVendorFilterButton", pos, ref _multiVendorFilterAutoClose);
        CloseMultiVendorPopoutIfOutside(ref _multiVendorSortOpen, "MultiVendorSortPopout",
            "MultiVendorSortButton", pos, ref _multiVendorSortAutoClose);
        CloseMultiVendorPopoutIfOutside(ref _multiVendorItemFilterOpen, "MultiVendorItemPopout",
            "MultiVendorItemButton", pos, ref _multiVendorItemAutoClose);
    }

    private void CloseMultiVendorPopoutIfOutside(ref bool openFlag, string popoutName, string buttonName,
        Vector2 pointerPos, ref IVisualElementScheduledItem autoClose)
    {
        if (!openFlag) return;
        var popout = _modal.Q<VisualElement>(popoutName);
        var button = _modal.Q<Button>(buttonName);
        if ((popout != null && popout.worldBound.Contains(pointerPos)) ||
            (button != null && button.worldBound.Contains(pointerPos)))
            return; // click landed on the popout or its own trigger — the button's toggle handles it

        openFlag = false;
        if (popout != null) popout.style.display = DisplayStyle.None;
        autoClose?.Pause();
        autoClose = null;
    }

    /// <summary>(Re)starts the vendor filter popout's 3-second auto-close countdown — called both the
    /// moment it opens and again on every Rebuild() that happens while it's still open (e.g. ticking a
    /// vendor checkbox), so an active session keeps getting a fresh 3s rather than closing mid-pick.</summary>
    private void ScheduleFilterAutoClose(VisualElement popout)
    {
        _multiVendorFilterAutoClose?.Pause();
        IVisualElementScheduledItem filterScheduled = _modal.schedule.Execute(() =>
        {
            _multiVendorFilterOpen = false;
            popout.style.display = DisplayStyle.None;
        });
        _multiVendorFilterAutoClose = filterScheduled;
        _multiVendorFilterAutoClose.ExecuteLater(3000);
    }

    /// <summary>Same as ScheduleFilterAutoClose, for the sort popout.</summary>
    private void ScheduleSortAutoClose(VisualElement popout)
    {
        _multiVendorSortAutoClose?.Pause();
        _multiVendorSortAutoClose = _modal.schedule.Execute(() =>
        {
            _multiVendorSortOpen = false;
            popout.style.display = DisplayStyle.None;
        });
        _multiVendorSortAutoClose.ExecuteLater(3000);
    }

    /// <summary>Same as ScheduleFilterAutoClose, for the item filter popout.</summary>
    private void ScheduleItemAutoClose(VisualElement popout)
    {
        _multiVendorItemAutoClose?.Pause();
        _multiVendorItemAutoClose = _modal.schedule.Execute(() =>
        {
            _multiVendorItemFilterOpen = false;
            popout.style.display = DisplayStyle.None;
        });
        _multiVendorItemAutoClose.ExecuteLater(3000);
    }

    /// <summary>Routed here from this panel's own VENDORS tab's "Order from Vendor" button, via the
    /// central EventManager rather than a direct call — VendorsTabView doesn't know which panel hosts
    /// it. Switches to Inbound Order Creation (the multi-vendor tab — the old single-vendor Create tab
    /// this used to point at is gone) with the filter dropdown narrowed to this vendor and its group
    /// pre-expanded, so the player lands looking at exactly the house they asked for.</summary>
    private void OnOrderFromVendorRequested(string eventId, string vendorId)
    {
        if (string.IsNullOrEmpty(vendorId)) return;
        _multiVendorFilterVendorIds.Clear();
        _multiVendorFilterVendorIds.Add(vendorId);
        _multiVendorFilterCriticalOnly = false;
        _expandedMultiVendors.Add(vendorId);
        _tab = Tab.MultiVendor;
        Show();
    }

    public bool IsVisible => _visible;
    public bool IsOpen => _visible;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        OrdersPauseGate.Push(this);
        _overlay.style.display = DisplayStyle.Flex;
        // Order screens sit above both bars — same reasoning as ContractsPanel.Show. This overlay was
        // already full-screen (bottom = 0); the raise is what guarantees it beats the top bar and any
        // panel opened before it, and KeepOnTop hands the top slot back to a visible toast.
        _overlay.BringToFront();
        UIToast.KeepOnTop();
        Rebuild();
        CentreOnce();

        // Always opens filled rather than normal size — the VENDORS tab alone now carries six data
        // columns plus Partnership/Travel Time/buttons, and Inbound Order Creation/PO List have plenty
        // of their own rows too. Deferred one frame, same reason CentreOnce is: on the very first Show()
        // of a session the panel hasn't been through a layout pass yet, so FillScreen's size math has
        // nothing real to measure — see FillScreen's own doc comment for why it's safe to just call
        // this again rather than needing a retry loop.
        _overlay.schedule.Execute(() =>
        {
            _resizeWindow?.FillScreen();
            if (_resizeWindow != null) _resizeWindow.UpdateScaleButtonIcon(_scaleBtn, TitleButtonSize, ColTitleText);
        }).ExecuteLater(16);
    }

    public void Hide()
    {
        _visible = false;
        OrdersPauseGate.Pop(this);
        HideConfirm();
        _overlay.style.display = DisplayStyle.None;
    }

    private void CentreOnce()
    {
        if (_placed) return;
        _overlay.schedule.Execute(() =>
        {
            if (_placed) return;
            Rect r = _overlay.worldBound;
            if (r.width < 1f) return;
            _modal.style.left = Mathf.Max(0f, (r.width - ModalWidth) * 0.5f);
            _modal.style.top = Mathf.Max(0f, (r.height - ModalHeight) * 0.5f);
            _placed = true;
        }).ExecuteLater(16);
    }

    // ── Shell ────────────────────────────────────────────────────────────────

    private VisualElement Build(out VisualElement modalOut, out VisualElement tabBarOut,
                                out VisualElement tabHeaderOut, out ScrollView contentOut,
                                out Label footerOut)
    {
        var overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0; overlay.style.right = 0;
        overlay.style.top = 0; overlay.style.bottom = 0;
        // Ignore, not Position: the panel is a floating window, so clicks outside the modal belong to
        // whatever is underneath. The modal itself picks normally.
        overlay.pickingMode = PickingMode.Ignore;

        var modal = new VisualElement();
        modal.style.position = Position.Absolute;
        modal.style.width = ModalWidth;
        modal.style.height = ModalHeight;
        modal.style.backgroundColor = new StyleColor(ColBg);
        modal.style.borderTopWidth = modal.style.borderBottomWidth =
            modal.style.borderLeftWidth = modal.style.borderRightWidth = 2;
        modal.style.borderTopColor = modal.style.borderBottomColor =
            modal.style.borderLeftColor = modal.style.borderRightColor = new StyleColor(ColBorder);
        modal.style.borderTopLeftRadius = modal.style.borderTopRightRadius =
            modal.style.borderBottomLeftRadius = modal.style.borderBottomRightRadius = 12;
        modal.style.paddingLeft = 16; modal.style.paddingRight = 16;
        modal.style.paddingTop = 10; modal.style.paddingBottom = 12;
        overlay.Add(modal);

        // ── Title bar (drag handle + close) ──
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.justifyContent = Justify.Center;
        titleBar.style.marginBottom = 8;
        // Same guard as ContractsPanel's title bar: without it, a full modal makes flex shrink this
        // row while the 48px chrome buttons inside refuse to shrink, so they spill out over the top
        // edge of the panel.
        titleBar.style.flexShrink = 0;

        // A left spacer that mirrors the right-side button cluster's width, so the flexGrow title in
        // the middle is centred against the WHOLE title bar rather than just the room left of the
        // buttons — without this the text sits visibly left of true-centre by half the button
        // cluster's width, since a flexGrow element's own MiddleCenter text only centres within its
        // own box, not the full row. Width is set below once the button cluster (rightGroup) has
        // actually been laid out, since the button cluster is text-sized rather than fixed-width.
        var leftSpacer = new VisualElement();
        leftSpacer.style.flexShrink = 0;
        titleBar.Add(leftSpacer);

        _titleLabel = MakeText("PURCHASING", 39, ColTitleText, bold: true);
        _titleLabel.style.flexGrow = 1;
        _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(_titleLabel);

        var rightGroup = new VisualElement();
        rightGroup.style.flexDirection = FlexDirection.Row;
        rightGroup.style.alignItems = Align.Center;
        rightGroup.style.flexShrink = 0;
        rightGroup.RegisterCallback<GeometryChangedEvent>(_ =>
            leftSpacer.style.width = rightGroup.resolvedStyle.width);

        // Return leg of the trip the Inbound Order Creation hint describes: a raised PO still needs a
        // day, time block and door, and that happens on the Scheduler. It used to sit down in that
        // tab's body beside the PO pill, which meant it only existed on one of the three tabs and sat
        // nowhere near the other navigation. Up here it is reachable from every tab, and it mirrors
        // ContractsPanel, which carries its "Back to Purchasing" counterpart in exactly this slot.
        // Left of the window buttons, so the destructive ✕ keeps the far corner it always has.
        var toScheduler = new Button(() => OpenScheduler(null)) { text = "SCHEDULER" };
        StyleOrangeButton(toScheduler);
        ApplyFont(toScheduler, bold: true, size: 16);
        toScheduler.style.height = TitleButtonSize;   // clears the helper's fixed 30px
        toScheduler.style.paddingLeft = toScheduler.style.paddingRight = 18;
        toScheduler.style.marginRight = 10;
        toScheduler.style.flexShrink = 0;          // the title flexGrows; without this the label squeezes
        toScheduler.style.borderTopLeftRadius = toScheduler.style.borderTopRightRadius =
            toScheduler.style.borderBottomLeftRadius = toScheduler.style.borderBottomRightRadius = 8;
        RuntimeTooltip.Attach(toScheduler, "Close purchasing and open the Scheduler, where POs are given a day, " +
                              "time block and door.");

        // Sideways trip to the Outbound panel (key 8, "Orders" on the play bar) — same pattern as
        // toScheduler above, just the other destination. Per Tad's ask, sitting right next to it.
        var toOrders = new Button(OpenOrders) { text = "ORDERS" };
        StyleOrangeButton(toOrders);
        ApplyFont(toOrders, bold: true, size: 16);
        toOrders.style.height = TitleButtonSize;
        toOrders.style.paddingLeft = toOrders.style.paddingRight = 18;
        toOrders.style.marginRight = 10;
        toOrders.style.flexShrink = 0;
        toOrders.style.borderTopLeftRadius = toOrders.style.borderTopRightRadius =
            toOrders.style.borderBottomLeftRadius = toOrders.style.borderBottomRightRadius = 8;
        RuntimeTooltip.Attach(toOrders, "Close purchasing and open Orders, where customer contracts are signed.");

        // ORDERS then SCHEDULER — swapped from creation order per Tad's request, so ORDERS sits
        // leftmost (closer to the title) and SCHEDULER sits to its right, closer to the window chrome.
        rightGroup.Add(toOrders);
        rightGroup.Add(toScheduler);

        // Resize + close, the same pair ContractsPanel carries — square, blue-edged, flush together.
        // Deliberately NOT the orange treatment: that is this panel's accent (active tab, PO pill and
        // the Back button above); window chrome stays blue so a chrome button never reads as an action.
        _scaleBtn = new Button();
        RuntimeTooltip.Attach(_scaleBtn, "Resize window (normal / large / fill screen)");
        StyleSquareButton(_scaleBtn);
        _scaleBtn.style.width = TitleButtonSize;
        _scaleBtn.style.height = TitleButtonSize;
        _scaleBtn.style.marginRight = 6;
        _scaleBtn.style.flexShrink = 0;
        ResizableWindow.AddStackedSquaresGlyph(_scaleBtn, TitleButtonSize, ColTitleText, isFilled: false);
        _scaleBtn.RegisterCallback<PointerEnterEvent>(_ =>
            _scaleBtn.style.backgroundColor = new StyleColor(new Color(0.35f, 0.55f, 0.95f, 0.35f)));
        _scaleBtn.RegisterCallback<PointerLeaveEvent>(_ =>
            _scaleBtn.style.backgroundColor = new StyleColor(new Color(0.16f, 0.22f, 0.29f, 1f)));
        rightGroup.Add(_scaleBtn);

        // Routed through CloseAll(), not a bare Hide() — this panel is registered on key 9, and only
        // UIKeyBindingManager.ToggleUI/CloseAll ever reset _currentOpenKey back to -1. A direct Hide()
        // left it stuck, and PlacementStateMachine.HandleIdleHover gates the world hover popup on
        // CurrentOpenKey == -1 — so clicking this ✕ silently killed every world tooltip afterward even
        // though the panel had visibly closed. CloseAll() calls Hide() on every open registered panel
        // (this one included) and THEN clears CurrentOpenKey, so it's a safe superset of the old call.
        var close = new Button(() => { UIKeyBindingManager.Instance?.CloseAll(); AudioManager.Play("UIClose"); }) { text = "✕" };
        StyleSquareButton(close);
        close.style.width = TitleButtonSize;
        close.style.height = TitleButtonSize;
        close.style.flexShrink = 0;
        ApplyFont(close, bold: true, size: 22);
        // Red on hover, darker red while held — the same three states ContractsPanel's close button
        // uses, and the same values, so the two windows behave identically. Pointer events rather than
        // Mouse ones for the same reason it does: a PointerDown/Up pair is what gives the press its
        // own shade instead of the button looking inert while it's actually being clicked.
        var closeNormalBg = close.style.backgroundColor;
        var closeHoverBg = new StyleColor(new Color(0.8f, 0.3f, 0.2f, 1f));
        var closeActiveBg = new StyleColor(new Color(0.6f, 0.16f, 0.12f, 1f));
        close.RegisterCallback<PointerEnterEvent>(_ => close.style.backgroundColor = closeHoverBg);
        close.RegisterCallback<PointerLeaveEvent>(_ => close.style.backgroundColor = closeNormalBg);
        close.RegisterCallback<PointerDownEvent>(_ => close.style.backgroundColor = closeActiveBg);
        close.RegisterCallback<PointerUpEvent>(_ => close.style.backgroundColor = closeHoverBg);
        rightGroup.Add(close);
        titleBar.Add(rightGroup);
        modal.Add(titleBar);

        // Drag by the title bar. Implemented inline rather than through the DraggableWindow helper for
        // the same reason ContractsPanel does it this way: that helper is built around a UXML-authored
        // window, and this modal is absolutely positioned in code, so left/top can just be written.
        // The offset is captured at pointer-down so the window doesn't jump to centre under the cursor.
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
            Rect bounds = overlay.worldBound;
            Vector2 target = (Vector2)evt.position - _dragOffset - new Vector2(bounds.x, bounds.y);
            // Keep a strip of the title bar on screen so the window can always be grabbed back.
            float maxX = Mathf.Max(0f, bounds.width - 120f);
            float maxY = Mathf.Max(0f, bounds.height - 60f);
            modal.style.left = Mathf.Clamp(target.x, 0f, maxX);
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

        // ── Tabs ──
        var tabBar = new VisualElement();
        tabBar.style.flexDirection = FlexDirection.Row;
        tabBar.style.alignItems = Align.FlexEnd;
        tabBar.style.borderBottomWidth = 2;
        tabBar.style.borderBottomColor = new StyleColor(ColBorder);
        tabBar.style.marginBottom = 8;
        modal.Add(tabBar);

        var tabHeader = new VisualElement();
        tabHeader.style.flexShrink = 0;
        modal.Add(tabHeader);

        var content = new ScrollView(ScrollViewMode.Vertical);
        content.style.flexGrow = 1;
        modal.Add(content);

        // VENDORS tab layout — a sibling of `content`, shown instead of it (see Rebuild).
        // VendorsTabView nests its own ScrollViews for the vendor list and the data grid body, and
        // nesting THOSE inside `content` (itself a ScrollView) fights Yoga's auto-height sizing.
        var vendorsPane = new VisualElement();
        vendorsPane.style.flexGrow = 1;
        vendorsPane.style.display = DisplayStyle.None;
        modal.Add(vendorsPane);
        _vendorsPane = vendorsPane;

        // MULTI-VENDOR tab layout — same "sibling pane, own scroll view" reasoning as VENDORS above:
        // each vendor group nests its own detail ScrollView, and nesting that inside `content` (itself
        // a ScrollView) fights Yoga's auto-height sizing the same way.
        var multiVendorPane = new ScrollView(ScrollViewMode.Vertical);
        multiVendorPane.style.flexGrow = 1;
        multiVendorPane.style.display = DisplayStyle.None;
        modal.Add(multiVendorPane);
        _multiVendorPane = multiVendorPane;

        var footer = MakeText(string.Empty, 14, ColSubtleText);
        footer.style.marginTop = 6;
        footer.style.flexShrink = 0;
        modal.Add(footer);

        // Side edges draggable + the title-bar button cycling normal/large/fill-screen, same as the
        // Contracts window. titleInset keeps the grips clear of the drag handle above them.
        _resizeWindow = new ResizableWindow(modal, minW: ModalMinWidth, minH: ModalHeight, grip: 10f,
                            titleInset: 48f, allowVerticalResize: false);
        _scaleBtn.clicked += () =>
        {
            _resizeWindow.CycleScale();
            _resizeWindow.UpdateScaleButtonIcon(_scaleBtn, TitleButtonSize, ColTitleText);
            AudioManager.Play(_resizeWindow.IsFilled ? "UIMax" : "UIMin");
        };

        modalOut = modal; tabBarOut = tabBar; tabHeaderOut = tabHeader;
        contentOut = content; footerOut = footer;
        return overlay;
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

    private void Rebuild()
    {
        _tabBar.Clear();
        _tabHeader.Clear();
        _content.Clear();
        _footerMessage.text = string.Empty;
        _titleLabel.text = TitleFor(_tab);

        var shipments = Shipments();
        int live = shipments?.PendingShipments.Count(s => s != null) ?? 0;

        _tabBar.Add(MakeTab("Order", Tab.MultiVendor,
                             _multiBaskets.Count(kv => kv.Value.Count > 0)));
        _tabBar.Add(MakeTab("Vendors", Tab.Vendors, 0));
        _tabBar.Add(MakeTab("PO List", Tab.PoList, live));

        // VENDORS and MULTI-VENDOR are sibling panes shown instead of `content`, same as
        // ContractsPanel's old Accounts/Bulk split — both own their own scroll views nested inside,
        // which fights Yoga's auto-height sizing if nested inside `content` (itself a ScrollView).
        _content.style.display = (_tab == Tab.Vendors || _tab == Tab.MultiVendor) ? DisplayStyle.None : DisplayStyle.Flex;
        _vendorsPane.style.display = _tab == Tab.Vendors ? DisplayStyle.Flex : DisplayStyle.None;
        _multiVendorPane.style.display = _tab == Tab.MultiVendor ? DisplayStyle.Flex : DisplayStyle.None;

        if (_tab == Tab.Vendors)
        {
            // Built once and cached rather than torn down and rebuilt on every Rebuild() — its own
            // rows already know how to refresh themselves.
            if (_vendorsTabRoot == null)
            {
                _vendorsTabRoot = _vendorsTabView.Build();
                _vendorsPane.Add(_vendorsTabRoot);
            }
            _vendorsTabView.Refresh();
            return;
        }

        if (_tab == Tab.MultiVendor)
        {
            BuildMultiVendorTab();
            return;
        }

        BuildShipmentList(shipments?.PendingShipments, live: true);
    }

    private static string TitleFor(Tab tab) => tab switch
    {
        Tab.Vendors => "VENDORS",
        Tab.MultiVendor => "INBOUND ORDER CREATION",
        _ => "PURCHASE ORDERS — IN FLIGHT"
    };

    /// <summary>Folder-style tab, same construction as ContractsPanel's: the active one is taller,
    /// sits 2px lower and drops its bottom border so it breaks through the divider and joins the
    /// content. Colour alone reads as a highlighted word rather than a selected tab.</summary>
    private VisualElement MakeTab(string text, Tab tab, int count)
    {
        bool active = _tab == tab;

        var button = new VisualElement();
        button.style.flexDirection = FlexDirection.Row;
        button.style.alignItems = Align.Center;
        button.style.height = active ? 38 : 32;
        button.style.marginRight = 4;
        button.style.marginBottom = active ? -2 : 0;
        button.style.paddingLeft = 14; button.style.paddingRight = 14;
        button.style.backgroundColor = new StyleColor(active ? ColOrange : ColTabIdle);
        button.style.borderTopWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 2;
        button.style.borderBottomWidth = active ? 0 : 2;
        button.style.borderTopColor = button.style.borderBottomColor =
            button.style.borderLeftColor = button.style.borderRightColor =
                new StyleColor(active ? ColOrangeEdge : ColBlueEdge);
        button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 8;

        var label = MakeText(text, 15, active ? ColOrangeText : ColSubtleText, bold: true);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        button.Add(label);

        if (count > 0)
        {
            var pill = new VisualElement();
            pill.style.marginLeft = 8;
            pill.style.paddingLeft = 7; pill.style.paddingRight = 7;
            pill.style.backgroundColor = new StyleColor(ColStat);
            pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius =
                pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = 8;
            var pl = MakeText(count.ToString(), 13, ColChipOutText, bold: true);
            pl.style.marginTop = 0; pl.style.marginBottom = 0;
            pill.Add(pl);
            button.Add(pill);
        }

        if (!active)
        {
            button.RegisterCallback<MouseEnterEvent>(_ => button.style.backgroundColor = new StyleColor(ColTabHover));
            button.RegisterCallback<MouseLeaveEvent>(_ => button.style.backgroundColor = new StyleColor(ColTabIdle));
        }
        button.RegisterCallback<ClickEvent>(_ => { _tab = tab; Rebuild(); });
        return button;
    }

    // ── The broker: salvage loads ────────────────────────────────────────────

    private static readonly Color ColSalvageBg   = new Color(0.10f, 0.09f, 0.08f, 0.95f);
    private static readonly Color ColSalvageEdge = new Color(0x8A / 255f, 0x6B / 255f, 0x2E / 255f, 1f);
    private static readonly Color ColSalvageText = new Color(0xD8 / 255f, 0xC6 / 255f, 0xA0 / 255f, 1f);

    private static BrokerService Broker()
        => ServiceLocator.TryGet<BrokerService>(out var b) ? b : null;

    /// <summary>
    /// An unmanifested trailer, sold as is.
    ///
    /// Deliberately styled AGAINST the rest of the panel — near-black with a dull brass edge instead
    /// of the house navy-and-blue. Everything else on this screen is a catalogue with known contents
    /// and a known price; this is neither, and it should not look like it belongs next to them.
    /// </summary>
    private VisualElement BuildSalvageStrip()
    {
        var wrap = new VisualElement();

        var broker = Broker();
        var offers = broker?.LiveOffers.ToList() ?? new List<SalvageOffer>();
        if (offers.Count == 0) return wrap;   // collapses to nothing when the broker has nothing

        wrap.style.marginBottom = 12;
        foreach (var offer in offers) wrap.Add(BuildSalvageCard(offer));
        return wrap;
    }

    private VisualElement BuildSalvageCard(SalvageOffer offer)
    {
        var card = new VisualElement();
        card.style.flexDirection = FlexDirection.Row;
        card.style.alignItems = Align.Center;
        card.style.paddingTop = 10; card.style.paddingBottom = 10;
        card.style.paddingLeft = 14; card.style.paddingRight = 14;
        card.style.backgroundColor = new StyleColor(ColSalvageBg);
        card.style.borderTopWidth = card.style.borderBottomWidth =
            card.style.borderLeftWidth = card.style.borderRightWidth = 2;
        card.style.borderTopColor = card.style.borderBottomColor =
            card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(ColSalvageEdge);
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;
        body.style.overflow = Overflow.Hidden;

        var head = MakeText("THE BROKER · CLOSE-OUT LOAD", 16, ColSalvageEdge, bold: true);
        head.style.marginTop = 0; head.style.marginBottom = 0;
        head.style.whiteSpace = WhiteSpace.NoWrap;
        body.Add(head);

        // The manifest is the whole product. It says how many pallets and nothing whatsoever about
        // what's on them.
        var manifest = MakeText(offer.ManifestLine, 24, ColSalvageText, bold: true);
        manifest.style.marginTop = 2; manifest.style.marginBottom = 0;
        manifest.style.whiteSpace = WhiteSpace.Normal;
        body.Add(manifest);

        var terms = MakeText("Contents not guaranteed. No returns. You find out on the dock.",
                             13, ColSubtleText);
        terms.style.marginTop = 3; terms.style.marginBottom = 0;
        terms.style.whiteSpace = WhiteSpace.Normal;
        body.Add(terms);
        card.Add(body);

        var take = new Button(() => OnTakeSalvage(offer)) { text = $"BUY AS IS · {offer.PriceLabel}" };
        StyleActionButton(take, ColSalvageEdge, new Color(0.35f, 0.26f, 0.10f), new Color(0.62f, 0.48f, 0.22f));
        take.style.width = StyleKeyword.Auto;
        take.style.height = 48;
        take.style.marginLeft = 14; take.style.marginRight = 0;
        take.style.flexShrink = 0;
        take.style.fontSize = 17;
        card.Add(take);

        return card;
    }

    private void OnTakeSalvage(SalvageOffer offer)
    {
        var broker = Broker();
        if (broker == null) return;

        if (!ServiceLocator.TryGet<ShipmentService>(out var shipments) || shipments == null)
        {
            UIToast.Show("Purchasing is unavailable — the shipment service isn't running.");
            return;
        }

        if (ServiceLocator.TryGet<MoneyService>(out var money) && money != null &&
            money.CurrentCapital < offer.Price)
        {
            ShowNotice($"Not enough capital for this load.\n\nIt costs {Money(offer.Price)} and you " +
                       $"have {Money(money.CurrentCapital)}.");
            return;
        }

        // Confirmed, unlike a spot deal. A spot deal is a known item at a known discount; this is a
        // four-figure bet on a trailer nobody has opened, and it deserves one deliberate press.
        ShowConfirm($"Buy this load as is?\n\n{offer.ManifestLine}\n{Money(offer.Price)}, paid now.\n\n" +
                    $"The manifest is all you get. Some of it will be good, some of it will be " +
                    $"short-dated, and some of it may be junk you paid for and can't sell.",
                    () => CommitSalvage(offer, shipments, broker));
    }

    private void CommitSalvage(SalvageOffer offer, ShipmentService shipments, BrokerService broker)
    {
        // Claim BEFORE building, so a card left on screen across an expiry can't produce two trailers.
        if (!broker.ClaimOffer(offer.Id))
        {
            UIToast.Show("That load has already gone.");
            Rebuild();
            return;
        }

        var items = broker.BuildLineItems(offer);
        if (items.Count == 0)
        {
            UIToast.Show("Couldn't raise that PO — the load didn't resolve to any real SKUs.");
            return;
        }

        var po = shipments.CreatePlayerPurchaseOrder(PONumberGenerator.GetRandomPONumber(),
                                                     BrokerService.BrokerVendorId, "The Broker",
                                                     items, Today());
        if (po == null)
        {
            UIToast.Show("Couldn't raise that PO.");
            return;
        }

        po.IsSalvage = true;

        UIToast.Show($"Load bought — PO {po.PONumber}, {Money(po.TotalCost)}. Book it a door. " +
                     $"You'll find out what's on it when it's broken down.");

        _tab = Tab.PoList;
        Rebuild();
    }

    // ── Spot deals ───────────────────────────────────────────────────────────

    /// <summary>
    /// The offer board: three discounted, whole-pallet loads that expire tonight.
    ///
    /// Sits at the TOP OF THE SCROLL VIEW rather than in the stationary header. It's the first thing
    /// you see when the panel opens and then it scrolls away as you get to work, which is exactly its
    /// weight in the decision — glance, decide, move on. Pinning it would cost the catalogue a
    /// permanent strip of height for something you consider once.
    /// </summary>
    private VisualElement BuildSpotDealsStrip()
    {
        var wrap = new VisualElement();
        wrap.style.marginBottom = 12;

        var market = Market();
        var deals = market?.LiveDeals.ToList() ?? new List<SpotDeal>();
        if (deals.Count == 0) return wrap;   // collapses to nothing when the board is empty

        var head = new VisualElement();
        head.style.flexDirection = FlexDirection.Row;
        head.style.alignItems = Align.Center;
        head.style.justifyContent = Justify.SpaceBetween;
        head.style.marginBottom = 6;

        var title = MakeText("TODAY'S SPOT DEALS", 18, ColOrangeText, bold: true);
        title.style.whiteSpace = WhiteSpace.NoWrap;
        head.Add(title);

        var expiry = MakeText("Gone at midnight", 14, ColSubtleText);
        expiry.style.whiteSpace = WhiteSpace.NoWrap;
        head.Add(expiry);
        wrap.Add(head);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Stretch;
        foreach (var deal in deals) row.Add(BuildDealCard(deal));
        wrap.Add(row);

        return wrap;
    }

    private VisualElement BuildDealCard(SpotDeal deal)
    {
        var sku = FindSku(deal.SkuId);

        var card = new VisualElement();
        card.style.flexGrow = 1;
        card.style.flexBasis = 0;
        card.style.marginRight = 8;
        card.style.paddingTop = 8; card.style.paddingBottom = 8;
        card.style.paddingLeft = 10; card.style.paddingRight = 10;
        card.style.backgroundColor = new StyleColor(new Color(ColOrange.r, ColOrange.g, ColOrange.b, 0.12f));
        card.style.borderTopWidth = card.style.borderBottomWidth =
            card.style.borderLeftWidth = card.style.borderRightWidth = 2;
        card.style.borderTopColor = card.style.borderBottomColor =
            card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(ColOrange);
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;

        var top = new VisualElement();
        top.style.flexDirection = FlexDirection.Row;
        top.style.alignItems = Align.Center;

        if (sku != null && sku.Icon != null)
        {
            var icon = new VisualElement();
            icon.style.width = 40; icon.style.height = 40;
            icon.style.flexShrink = 0;
            icon.style.marginRight = 8;
            icon.style.backgroundImage = new StyleBackground(sku.Icon);
            top.Add(icon);
        }

        var namePart = new VisualElement();
        namePart.style.flexGrow = 1;
        namePart.style.flexShrink = 1;
        namePart.style.overflow = Overflow.Hidden;

        var name = MakeText(sku != null ? sku.ItemDescription : deal.SkuId, 17, ColTitleText, bold: true);
        name.style.marginTop = 0; name.style.marginBottom = 0;
        name.style.whiteSpace = WhiteSpace.NoWrap;
        namePart.Add(name);

        var qty = MakeText($"{deal.Pallets} pallet(s) · {deal.TotalCases:N0} cases", 13, ColSubtleText);
        qty.style.marginTop = 0; qty.style.marginBottom = 0;
        qty.style.whiteSpace = WhiteSpace.NoWrap;
        namePart.Add(qty);
        top.Add(namePart);

        // The discount is the headline — it's the reason to look at this card at all.
        var pct = MakeText($"-{deal.DiscountPercent}%", 24, ColMoney, bold: true);
        pct.style.marginTop = 0; pct.style.marginBottom = 0;
        pct.style.flexShrink = 0;
        pct.style.whiteSpace = WhiteSpace.NoWrap;
        top.Add(pct);
        card.Add(top);

        var price = MakeText($"{Money(deal.UnitPrice)}/case (list {Money(deal.ListPriceWhenOffered)})",
                             13, ColChipOutText);
        price.style.marginTop = 4;
        price.style.whiteSpace = WhiteSpace.NoWrap;
        card.Add(price);
        var dealVendor = VendorRegistry.Load()?.GetById(deal.VendorId);
        if (dealVendor != null)
        {
            int dealVendorPartnership = Economy()?.GetState(deal.VendorId)?.PartnershipLevel ?? 0;
            var vendorLine = MakeText($"{dealVendor.DisplayName} [{dealVendorPartnership:+0;-0;0}]",
                                      17, Color.white, bold: true);
            vendorLine.style.marginTop = 4;
            vendorLine.style.whiteSpace = WhiteSpace.NoWrap;
            card.Add(vendorLine);
        }


        var take = new Button(() => OnTakeDeal(deal)) { text = $"TAKE · {Money(deal.TotalCost)}" };
        StyleActionButton(take, ColOrange, ColOrangeEdge, ColOrangeHover);
        // Overrides the shared action-button sizing: these three sit side by side inside a strip, not
        // on the footer row where that fixed 240x56 belongs.
        take.style.width = StyleKeyword.Auto;
        take.style.height = 36;
        take.style.marginTop = 6;
        take.style.marginLeft = 0; take.style.marginRight = 0;
        take.style.fontSize = 15;
        card.Add(take);

        return card;
    }

    /// <summary>
    /// Takes a spot deal: claims it, then raises a PO for it immediately at the deal price.
    ///
    /// Straight to a PO rather than into the basket. A spot deal is a fixed load somebody else has
    /// already built — letting the player edit the quantity would make it an ordinary catalogue line
    /// with a discount, and the take-it-or-leave-it shape is the entire mechanic.
    /// </summary>
    private void OnTakeDeal(SpotDeal deal)
    {
        var market = Market();
        var sku = FindSku(deal.SkuId);
        if (market == null || sku == null)
        {
            UIToast.Show("That offer can't be filled — its item is no longer in the catalogue.");
            return;
        }

        if (!ServiceLocator.TryGet<ShipmentService>(out var shipments) || shipments == null)
        {
            UIToast.Show("Purchasing is unavailable — the shipment service isn't running.");
            return;
        }

        // Checked here and NOT on the basket path, deliberately: this is one click that commits the
        // whole amount, with no running total to watch on the way. Being put into the red by a single
        // button press is a different experience from spending down a number you were staring at.
        if (ServiceLocator.TryGet<MoneyService>(out var money) && money != null &&
            money.CurrentCapital < deal.TotalCost)
        {
            ShowNotice($"Not enough capital for this deal.\n\nIt costs {Money(deal.TotalCost)} and you " +
                       $"have {Money(money.CurrentCapital)}.");
            return;
        }

        var plan = TrailerCapacity.Plan(new List<(SkuData sku, int cases)> { (sku, deal.TotalCases) });
        if (plan.OverCapacity)
        {
            ShowNotice("That deal is more than one trailer will hold.");
            return;
        }

        // Claim BEFORE building the PO: the panel can sit open across midnight, and two clicks on a
        // card that expired while it was on screen must not produce two trailers.
        if (!market.ClaimDeal(deal.Id))
        {
            UIToast.Show("That offer has already gone.");
            Rebuild();
            return;
        }

        var items = plan.Pallets
            .Select(pallet => new ShipmentLineItem(pallet.SkuId, pallet.Cases,
                                                   deal.UnitPrice, sku.ShelfLifeDays)
            {
                FloorSlotIndex = pallet.FloorSlot,
                PalletTier = pallet.Tier
            })
            .ToList();

        var po = shipments.CreatePlayerPurchaseOrder(PONumberGenerator.GetRandomPONumber(),
                                                     "SPOT_BROKER", "Spot Market", items, Today());
        if (po == null)
        {
            UIToast.Show("Couldn't raise that PO — the deal didn't resolve to a real SKU.");
            return;
        }

        UIToast.Show($"Deal taken — PO {po.PONumber}, {po.TotalUnits:N0} case(s) of " +
                     $"{sku.ItemDescription} for {Money(po.TotalCost)}. Book it a door on the Scheduler.");

        _tab = Tab.PoList;
        Rebuild();
    }

    // ── Tabs 2 & 3: PO list and archive ──────────────────────────────────────

    /// <summary>
    /// The PO list, in this panel's own palette rather than the Dev Console's green — same information
    /// and the same expand-to-see-lines behaviour, restyled to match. Shared by the live and archived
    /// tabs because they show the same object at different points in its life; only the accent colour
    /// and whether a Cancel button appears differ.
    /// </summary>
    private void BuildShipmentList(IReadOnlyList<ShipmentData> shipments, bool live)
    {
        var intro = MakeText(live
            ? "Purchase orders raised and not yet received. No truck leaves until you book the order a door and time on the Scheduler."
            : "Finished purchase orders — received, departed or cancelled. A record of what you've bought.",
            14, ColSubtleText);
        intro.style.marginBottom = 8;
        intro.style.whiteSpace = WhiteSpace.Normal;
        _tabHeader.Add(intro);

        if (shipments == null || shipments.Count == 0)
        {
            var none = MakeText(live ? "No purchase orders in flight."
                                     : "Nothing archived yet.", 16, ColEmptyText);
            none.style.unityTextAlign = TextAnchor.MiddleCenter;
            none.style.marginTop = 30;
            _content.Add(none);
            return;
        }

        int row = 0;
        foreach (var s in shipments)
        {
            if (s == null) continue;
            _content.Add(BuildShipmentCard(s, row++, live));
        }

        _footerMessage.text = live
            ? $"{shipments.Count} PO(s) in flight · {shipments.Sum(s => s?.TotalUnits ?? 0):N0} case(s) inbound."
            : $"{shipments.Count} archived PO(s).";
    }

    /// <summary>The PO list's Status column reads straight off ShipmentData.Status, which never
    /// actually transitions into "Receiving" today — a trailer sitting at a door unloading still just
    /// says "[InTransit]", which is what the player was seeing and asking to fix. There's no PO-to-door
    /// registry to read this off directly, so it's answered by finding the live TruckController
    /// carrying this PO and asking IT whether it's currently docked (TruckController.DockedAt is only
    /// non-null while docked — see TruckController.cs). Returns null (fall back to the raw status
    /// label) for anything not actively docked right now.</summary>
    private static string LiveDockStatus(ShipmentData shipment)
    {
        if (shipment.Status != ShipmentData.ShipmentStatus.InTransit &&
            shipment.Status != ShipmentData.ShipmentStatus.Receiving)
            return null;

        var trucks = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None);
        foreach (var truck in trucks)
        {
            if (truck == null || truck.AssignedShipment == null) continue;
            if (truck.AssignedShipment.PONumber != shipment.PONumber) continue;
            if (truck.DockedAt != null) return $"UNLOADING · Door {truck.DockedAt.DoorNumber}";
        }
        return null;
    }

    private VisualElement BuildShipmentCard(ShipmentData shipment, int rowIndex, bool live)
    {
        Color accent = shipment.Status == ShipmentData.ShipmentStatus.Cancelled ? ColDanger
                     : live ? ColBorder : ColBlueEdge;

        var card = new VisualElement();
        card.style.marginBottom = 6;
        card.style.paddingTop = 8; card.style.paddingBottom = 8;
        card.style.paddingLeft = 12; card.style.paddingRight = 12;
        card.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColCardEven : ColCardOdd);
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;
        card.style.borderLeftWidth = 3;
        card.style.borderLeftColor = new StyleColor(accent);

        bool expanded = !_collapsedPos.Contains(shipment.PONumber);

        // ── Header row: disclosure arrow, [PO number / customer name] stacked, status, totals, actions ──
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;

        var arrow = MakeText(expanded ? "▼" : "▶", 14, ColChipOutText, bold: true);
        arrow.style.width = 18;
        arrow.style.flexShrink = 0;
        arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
        arrow.style.marginTop = 0; arrow.style.marginBottom = 0;
        header.Add(arrow);

        // PO number leads alone on its own line; the seller name and load status sit together on
        // the line underneath it — status belongs next to WHO shipped it, not next to the PO number.
        var titleCol = new VisualElement();
        titleCol.style.marginRight = 12;
        header.Add(titleCol);

        var po = MakeText($"PO {shipment.PONumber}", 18, ColTitleText, bold: true);
        po.style.whiteSpace = WhiteSpace.NoWrap;
        // This custom font renders taller than a Label's auto-computed layout box (the same mismatch
        // that clipped the quantity field digits), so a plain auto-height stack overlaps the line
        // below it. Reserving real height plus a margin gap is what actually separates the two lines.
        po.style.height = 26;
        po.style.marginBottom = 6;
        titleCol.Add(po);

        var subRow = new VisualElement();
        subRow.style.flexDirection = FlexDirection.Row;
        subRow.style.alignItems = Align.Center;
        subRow.style.height = 20;
        titleCol.Add(subRow);

        var supplier = MakeText(shipment.SupplierName, 14, ColSubtleText);
        supplier.style.marginRight = 8;
        supplier.style.whiteSpace = WhiteSpace.NoWrap;
        subRow.Add(supplier);

        string dockedLabel = LiveDockStatus(shipment);
        var status = MakeText(dockedLabel ?? $"[{shipment.Status}]", 13,
                              dockedLabel != null ? ColMoney :
                              shipment.Status == ShipmentData.ShipmentStatus.Cancelled ? ColDangerSoft : ColChipOutText,
                              bold: true);
        status.style.whiteSpace = WhiteSpace.NoWrap;
        subRow.Add(status);

        // Arrival data — when this PO's truck is actually coming and which door it's booked at, or
        // that it isn't booked yet. ScheduledFor/ScheduleTextFor already existed for exactly this but
        // were never wired into a row — this was a bare flexGrow spacer before.
        bool scheduled = ScheduledFor(shipment) != null;
        var arrival = MakeText(ScheduleTextFor(shipment), 13, scheduled ? ColSubtleText : ColDangerSoft, bold: !scheduled);
        arrival.style.flexGrow = 1;
        arrival.style.unityTextAlign = TextAnchor.MiddleCenter;
        arrival.style.whiteSpace = WhiteSpace.NoWrap;
        header.Add(arrival);

        // Pallets, not "lines" — each line item IS one physical pallet (see SubmitPurchaseOrder), so
        // this is the trailer's actual pallet count, the number the player cares about at a glance.
        var summary = MakeText($"{shipment.LineItems.Count} pallet(s) · {shipment.TotalUnits:N0} case(s)",
                               14, ColSubtleText);
        summary.style.marginRight = 14;
        summary.style.whiteSpace = WhiteSpace.NoWrap;
        header.Add(summary);

        var cost = MakeText($"${shipment.TotalCost:N0}", 18, ColMoney, bold: true);
        cost.style.marginRight = 12;
        cost.style.whiteSpace = WhiteSpace.NoWrap;
        header.Add(cost);

        if (live)
        {
            // Stacked, not side by side: the header row is already full, and these two are the only
            // things on the card you can act on, so they belong together in one column at its end.
            var actions = new VisualElement();
            actions.style.flexShrink = 0;
            actions.style.alignItems = Align.FlexEnd;

            var cancel = new Button(() => OnCancelPo(shipment.PONumber)) { text = "CANCEL PO" };
            StyleOrangeButton(cancel);
            cancel.style.height = 28;
            cancel.style.width = ActionLinkWidth;
            cancel.style.flexShrink = 0;
            actions.Add(cancel);

            // Takes the player from a PO that needs a door to the grid where doors are booked. A
            // purchase order lands in the unscheduled pool and does NOT dispatch a truck until it has
            // a slot, so "where do I do that?" is the obvious next question this card raises — and the
            // answer was a different panel behind a different number key.
            var scheduler = new Button(() => OpenScheduler(shipment)) { text = "SCHEDULER" };
            StyleLinkButton(scheduler);
            scheduler.style.width = ActionLinkWidth;
            scheduler.style.marginTop = 4;
            actions.Add(scheduler);

            header.Add(actions);
        }

        // The whole header row toggles, not just the arrow — an 18px glyph is a small target, and the
        // row already reads as one object. Clicks that started on a BUTTON are ignored so CANCEL PO
        // doesn't also collapse the card out from under the confirmation it's about to raise.
        header.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target is Button) return;
            ToggleExpanded(shipment.PONumber);
        });
        card.Add(header);

        // Collapsed stops here: the header alone already carries PO number, status, line/case counts
        // and total, which is everything needed to scan a list. The lines are the detail you open.
        if (!expanded) return card;

        // ── A broker load keeps its secret until something has physically landed ──
        //
        // This is the whole mechanic. If the manifest were readable off the PO list the moment the
        // trailer was bought, "sight-unseen" would be a flavour word rather than a rule, and the
        // Receiver breaking the load down would be theatre confirming what the player already knew.
        if (shipment.IsSalvage && !shipment.AnyReceived)
        {
            var sealedNote = MakeText("SEALED · contents unknown until it's broken down on the dock",
                                      15, ColSalvageText, bold: true);
            sealedNote.style.marginTop = 6;
            sealedNote.style.paddingLeft = 8;
            sealedNote.style.whiteSpace = WhiteSpace.Normal;
            card.Add(sealedNote);
            return card;
        }

        // ── Once it's been received, a broker load reports what it actually was ──
        if (shipment.IsSalvage)
        {
            int damaged = shipment.LineItems.Count(li => li.Salvage == SalvageCondition.Damaged);
            int shortDated = shipment.LineItems.Count(li => li.Salvage == SalvageCondition.ShortDated);
            int jackpot = shipment.LineItems.Count(li => li.Salvage == SalvageCondition.Jackpot);
            int good = shipment.LineItems.Count - damaged - shortDated - jackpot;

            var verdict = MakeText(
                $"BROKER LOAD · {good} good · {shortDated} short-dated · {damaged} damaged (written off)" +
                (jackpot > 0 ? $" · {jackpot} SPECIALTY" : ""),
                14, jackpot > 0 ? ColMoney : ColSalvageText, bold: true);
            verdict.style.marginTop = 6;
            verdict.style.paddingLeft = 8;
            verdict.style.whiteSpace = WhiteSpace.Normal;
            card.Add(verdict);
        }

        // ── Line items ──
        // Consolidated by SKU: each physical pallet is its own ShipmentLineItem (see
        // SubmitPurchaseOrder), but the player wants to see "how much of this item, total" rather
        // than a repeated row per pallet. The pallet count moves into the column that used to show
        // per-pallet received quantity, since "how many pallets of this" is what mattered there.
        var groupedLines = shipment.LineItems
            .GroupBy(li => li.SkuId)
            .Select(g => new
            {
                SkuId = g.Key,
                TotalQty = g.Sum(li => li.Quantity),
                PalletCount = g.Count(),
                TotalCost = g.Sum(li => li.TotalCost)
            });

        // Collected so the inset below can be corrected against a real layout rather than guessed.
        var lineRows = new List<(VisualElement Row, VisualElement Probe)>();

        foreach (var g in groupedLines)
        {
            var sku = FindSku(g.SkuId);
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            line.style.marginTop = 3;
            // Lines up with the vendor name's text, not the arrow — the arrow is 18px wide and the
            // card has 12px of its own left padding, so 18 of left padding here (card padding already
            // covers the other 12) puts this row's left edge exactly under "Spot Market", not under ▼.
            line.style.paddingLeft = 18;

            // Netted the same way the multi-vendor tab's IN DEMAND column is — a SKU already covered by
            // on-hand stock, other on-order POs, or cases sitting in an in-progress basket right now
            // reads as normal again, not permanently red just because gross demand exists somewhere.
            int lineNetDemand = Mathf.Max(0, (Economy()?.GetTotalInDemand(g.SkuId) ?? 0) -
                (Inventory()?.TotalOnHand(g.SkuId) ?? 0) - (Economy()?.GetTotalOnOrder(g.SkuId) ?? 0) -
                TotalInProgressCases(g.SkuId));
            var itemNo = MakeText(g.SkuId, 13, lineNetDemand > 0 ? ColDanger : ColChipOutText);
            itemNo.style.width = 90;
            itemNo.style.whiteSpace = WhiteSpace.NoWrap;
            line.Add(itemNo);

            var desc = MakeText(sku != null ? sku.ItemDescription : "(unknown item)", 13, ColTitleText);
            desc.style.flexGrow = 1;
            desc.style.whiteSpace = WhiteSpace.NoWrap;
            line.Add(desc);

            var qty = MakeText($"{g.TotalQty:N0} cs", 13, ColSubtleText);
            qty.style.width = 90;
            qty.style.unityTextAlign = TextAnchor.MiddleRight;
            line.Add(qty);

            var pallets = MakeText($"{g.PalletCount:N0} plt", 13, ColSubtleText);
            pallets.style.width = 100;
            pallets.style.unityTextAlign = TextAnchor.MiddleRight;
            line.Add(pallets);

            var lineCost = MakeText($"${g.TotalCost:N0}", 13, ColOrangeText);
            lineCost.style.width = 90;
            lineCost.style.unityTextAlign = TextAnchor.MiddleRight;
            line.Add(lineCost);

            card.Add(line);
            lineRows.Add((line, itemNo));
        }

        // MEASURED, not assumed. The 18px above is the arrow's box, which is the only part of the
        // header's left inset this file can name — the rest is whatever the title column's labels
        // resolve to under this font, and that left every line row sitting a few pixels left of the
        // supplier name. Once the card has a real layout, the leftover difference between the first
        // item number and the supplier label is applied to every row, so they line up exactly and
        // stay lined up if the fonts or sizes change.
        //
        // Self-terminating rather than one-shot: correcting the padding fires another geometry pass,
        // which measures ~0 difference and returns without touching anything. A one-shot would have
        // to gamble on the first pass already having final font metrics.
        if (lineRows.Count > 0)
        {
            card.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float delta = supplier.worldBound.x - lineRows[0].Probe.worldBound.x;
                if (float.IsNaN(delta) || Mathf.Abs(delta) < 0.5f) return;

                foreach (var lr in lineRows)
                    lr.Row.style.paddingLeft = lr.Row.resolvedStyle.paddingLeft + delta;
            });
        }

        return card;
    }

    /// <summary>
    /// PO numbers whose line items are hidden.
    ///
    /// Stores what's COLLAPSED rather than what's expanded, so a PO that has never been touched — and
    /// one that arrives while the panel is open — defaults to showing its lines. Keyed on the PO
    /// number rather than an index because the list re-sorts and re-filters between rebuilds; an
    /// index would silently transfer the collapse to whichever order happened to land in that row.
    ///
    /// Survives Rebuild(), which runs on every tab switch and after every action, so a card the
    /// player folded away stays folded.
    /// </summary>
    private readonly HashSet<string> _collapsedPos = new();

    private void ToggleExpanded(string poNumber)
    {
        if (!_collapsedPos.Add(poNumber)) _collapsedPos.Remove(poNumber);
        Rebuild();
    }

    /// <summary>
    /// Closes this panel and opens the Outbound Order Manager on its Schedule tab, showing the day
    /// this PO is wanted for — a hyperlink between the two halves of the dock.
    ///
    /// Closes rather than layering: both are full-size draggable windows, and stacking them leaves
    /// the player with two overlapping modals and no obvious way back. Going through the key manager
    /// (rather than calling Show directly) keeps the same exclusivity every other panel obeys, so
    /// Tab still closes it and opening a third panel still closes this one.
    ///
    /// Degrades to a toast if the Contracts panel can't be reached — a dead button that silently does
    /// nothing is worse than one that says why.
    /// </summary>
    private void OpenScheduler(ShipmentData shipment)
    {
        Hide();

        var topBar = Object.FindAnyObjectByType<TopBarUI>();
        var scheduler = topBar != null ? topBar.SchedulerPanel : null;
        if (scheduler == null)
        {
            UIToast.Show("Couldn't open the scheduler — the Scheduler panel isn't loaded.");
            return;
        }

        // Registered on key 0; routing through the manager is what closes any other open panel first.
        UIKeyBindingManager.Instance?.CloseAll();
        scheduler.ShowForDay(shipment != null ? shipment.ArrivalDayNumber : 0);
    }

    /// <summary>Closes this panel and opens Orders (key 8's ContractsPanel) — same shape as
    /// OpenScheduler above, just the other destination, for the ORDERS button.</summary>
    private void OpenOrders()
    {
        Hide();

        var topBar = Object.FindAnyObjectByType<TopBarUI>();
        var orders = topBar != null ? topBar.ContractsPanel : null;
        if (orders == null)
        {
            UIToast.Show("Couldn't open orders — the Contracts panel isn't loaded.");
            return;
        }

        UIKeyBindingManager.Instance?.CloseAll();
        orders.Show();
    }

    private void OnCancelPo(string poNumber)
    {
        var shipments = Shipments();
        if (shipments == null) return;

        ShowConfirm($"Cancel PO {poNumber}?\n\nThe goods will not be delivered and the order value is " +
                    $"refunded.", () =>
        {
            if (!shipments.CancelPurchaseOrder(poNumber, out string why)) UIToast.Show(why);
            else UIToast.Show($"PO {poNumber} cancelled and refunded.");
            Rebuild();
        });
    }

    // ── Confirmation dialog ──────────────────────────────────────────────────

    private VisualElement _confirmBlocker;
    private Label _confirmMessage;
    private Label _confirmTitle;
    private Button _confirmYesButton;
    private Button _confirmNoButton;
    private System.Action _confirmYes;

    /// <summary>Modal Yes/No over the whole panel. Added to the OVERLAY rather than the modal so it
    /// can't be dragged half off-screen with the window, same as ContractsPanel's.</summary>
    private VisualElement BuildConfirmDialog()
    {
        _confirmBlocker = new VisualElement();
        _confirmBlocker.style.position = Position.Absolute;
        _confirmBlocker.style.left = 0; _confirmBlocker.style.right = 0;
        _confirmBlocker.style.top = 0; _confirmBlocker.style.bottom = 0;
        _confirmBlocker.style.alignItems = Align.Center;
        _confirmBlocker.style.justifyContent = Justify.Center;
        _confirmBlocker.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
        _confirmBlocker.style.display = DisplayStyle.None;
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

        _confirmTitle = MakeText("ARE YOU SURE?", 22, ColOrangeText, bold: true);
        _confirmTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        _confirmTitle.style.marginBottom = 10;
        card.Add(_confirmTitle);

        _confirmMessage = MakeText(string.Empty, 15, ColTitleText);
        _confirmMessage.style.whiteSpace = WhiteSpace.Normal;
        _confirmMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
        _confirmMessage.style.marginBottom = 14;
        card.Add(_confirmMessage);

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.justifyContent = Justify.Center;

        // Green YES / red NO, matching the CREATE PO and CANCEL buttons this dialog is confirming —
        // the answer should look like the button that raised the question.
        _confirmYesButton = new Button(() => { var act = _confirmYes; HideConfirm(); act?.Invoke(); }) { text = "YES" };
        StyleActionButton(_confirmYesButton, ColCreateGreen, ColCreateGreenEdge, ColCreateGreenHover);
        _confirmYesButton.style.width = 150;
        _confirmYesButton.style.height = 44;
        _confirmYesButton.style.marginRight = 12;
        buttons.Add(_confirmYesButton);

        _confirmNoButton = new Button(HideConfirm) { text = "NO" };
        StyleActionButton(_confirmNoButton, ColCancelRed, ColCancelRedEdge, ColCancelRedHover);
        _confirmNoButton.style.width = 150;
        _confirmNoButton.style.height = 44;
        buttons.Add(_confirmNoButton);
        card.Add(buttons);

        _confirmBlocker.Add(card);
        return _confirmBlocker;
    }

    private void ShowConfirm(string message, System.Action onYes)
    {
        _confirmMessage.text = message;
        _confirmYes = onYes;
        _confirmTitle.text = "ARE YOU SURE?";
        SetConfirmIsNotice(false);
        _confirmBlocker.style.display = DisplayStyle.Flex;
        _confirmBlocker.BringToFront();
    }

    /// <summary>Same modal, one button — for telling the player something rather than asking. Reuses
    /// the dialog instead of a toast because an over-capacity load has to stop the interaction: a
    /// corner toast is exactly what someone spamming the + button doesn't read.</summary>
    private void ShowNotice(string message)
    {
        _confirmMessage.text = message;
        _confirmYes = null;
        _confirmTitle.text = "TRAILER FULL";
        SetConfirmIsNotice(true);
        _confirmBlocker.style.display = DisplayStyle.Flex;
        _confirmBlocker.BringToFront();
    }

    private void SetConfirmIsNotice(bool notice)
    {
        // The Yes button is meaningless on a notice, and leaving it visible invites the player to
        // "confirm" going over capacity — which isn't on offer.
        if (_confirmYesButton != null)
            _confirmYesButton.style.display = notice ? DisplayStyle.None : DisplayStyle.Flex;
        if (_confirmNoButton != null)
            _confirmNoButton.text = notice ? "OK" : "NO";
    }

    private void HideConfirm()
    {
        _confirmYes = null;
        _confirmBlocker.style.display = DisplayStyle.None;
    }

    // ── Data helpers ─────────────────────────────────────────────────────────

    private static ShipmentService Shipments()
        => ServiceLocator.TryGet<ShipmentService>(out var s) ? s : null;

    private static InventoryService Inventory()
        => ServiceLocator.TryGet<InventoryService>(out var s) ? s : null;

    private static int Today()
        => ServiceLocator.TryGet<SimulationTimeService>(out var t) && t != null ? t.Day : 1;

    private static MarketService Market()
        => ServiceLocator.TryGet<MarketService>(out var m) ? m : null;

    private static VendorEconomyService Economy()
        => ServiceLocator.TryGet<VendorEconomyService>(out var e) ? e : null;

    private static int Reputation()
        => ServiceLocator.TryGet<ReputationService>(out var r) && r != null ? r.Score : 0;

    /// <summary>
    /// Money, without the trailing ".00" that every figure on this panel was carrying.
    ///
    /// Every authored SKU costs a whole number of dollars, so "$2,880.00" was two characters of noise
    /// on the largest, most-read numbers on the screen. But this drops the decimals only when there
    /// AREN'T any rather than formatting flat to N0 — a $12.75 item priced later would otherwise be
    /// displayed as "$13" while being billed at $12.75, and a price that lies by a rounding is a worse
    /// bug than a tidy one. Cents show up exactly when they exist.
    /// </summary>
    private static string Money(float amount)
        => Mathf.Abs(amount - Mathf.Round(amount)) < 0.005f
         ? $"${amount:N0}"
         : $"${amount:N2}";

    private static SkuData FindSku(string skuId)
    {
        var inv = Inventory();
        return inv?.AllSkus.FirstOrDefault(s => s != null && s.SkuId == skuId);
    }

    /// <summary>The dock appointment holding a door for this PO, or null if it's still unscheduled
    /// (parked in the pool counts as unscheduled — a parked trailer occupies no door).</summary>
    private static DockAppointment ScheduledFor(ShipmentData shipment)
    {
        if (shipment == null) return null;
        if (!ServiceLocator.TryGet<DockScheduleService>(out var sched) || sched == null) return null;
        var appt = sched.FindForPo(shipment.PONumber);
        return appt != null && !appt.Parked ? appt : null;
    }

    /// <summary>One line describing when this PO's truck is coming — the whole point of the PO List
    /// now that scheduling is manual. An unbooked order names the thing the player still has to do
    /// rather than reporting a date that doesn't exist.</summary>
    private static string ScheduleTextFor(ShipmentData shipment)
    {
        var appt = ScheduledFor(shipment);
        if (appt == null) return "No Appointment Set";

        string day = appt.Day == Today() ? "today"
                   : appt.Day == Today() + 1 ? "tomorrow"
                   : $"day {appt.Day}";
        return $"Arriving {day}, {DockScheduleService.BlockLabel(appt.BlockIndex)}, door {appt.DoorNumber}";
    }


    // ── Styling ──────────────────────────────────────────────────────────────

    private Label MakeText(string text, int size, Color color, bool bold = false)
    {
        var label = new Label(text);
        ApplyFont(label, bold, size);
        label.style.color = new StyleColor(color);
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    /// <summary>
    /// Overlays a thin horizontal line across the middle of a label — a simple "done, closed
    /// business" strike-through for finished POs/orders. UI Toolkit's Label has no native
    /// text-decoration support, so this adds a small absolutely-positioned child bar instead;
    /// it stretches to the label's full width and centers vertically regardless of font size.
    /// </summary>
    private static void AddStrikeThrough(VisualElement target, Color lineColor)
    {
        var line = new VisualElement();
        line.pickingMode = PickingMode.Ignore;
        line.style.position = Position.Absolute;
        line.style.left = 0; line.style.right = 0;
        line.style.top = Length.Percent(50);
        line.style.height = 2;
        line.style.marginTop = -1;
        line.style.backgroundColor = new StyleColor(lineColor);
        target.Add(line);
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

    /// <summary>The two big commit buttons — CANCEL and CREATE PO. Bigger type, a thicker edge and a
    /// fixed width so they read as a matched pair bracketing the total rather than as two ordinary
    /// buttons that happen to be at the ends of a row.</summary>
    private static void StyleActionButton(Button b, Color fill, Color edge, Color hover)
    {
        ApplyFont(b, bold: true, size: 19);
        b.style.width = ActionButtonWidth;
        b.style.height = ActionButtonHeight;
        b.style.flexShrink = 0;
        b.style.backgroundColor = new StyleColor(fill);
        b.style.color = new StyleColor(Color.white);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 3;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(edge);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        b.style.marginLeft = 0; b.style.marginRight = 0;
        b.style.paddingLeft = 10; b.style.paddingRight = 10;
        b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = new StyleColor(hover));
        b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(fill));
    }

    /// <summary>
    /// The cross-panel "go there" button — blue with white lettering, deliberately not orange.
    ///
    /// Orange on this panel means "this commits something" (CREATE PO, CANCEL PO). SCHEDULER commits
    /// nothing; it just takes you somewhere. Giving navigation its own colour keeps that promise
    /// honest — and blue is already the panel's structural colour, so it reads as part of the frame
    /// rather than as a third kind of action.
    /// </summary>
    private static void StyleLinkButton(Button b)
    {
        ApplyFont(b, bold: true, size: 13);
        b.style.height = 28;
        b.style.flexShrink = 0;
        b.style.backgroundColor = new StyleColor(ColBlueEdge);
        b.style.color = new StyleColor(Color.white);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(ColBorder);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;
        b.style.paddingLeft = 10; b.style.paddingRight = 10;
        b.style.marginLeft = 0; b.style.marginRight = 0;
        b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = new StyleColor(ColBorder));
        b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(ColBlueEdge));
    }

    /// <summary>The square +/- steppers. Tinted rather than orange — orange is reserved for the
    /// actions that COMMIT (Create PO, Cancel PO), and a row of orange steppers would make every line
    /// look like a submit button.</summary>
    private static void StyleStepButton(Button b, Color tint)
    {
        ApplyFont(b, bold: true, size: 20);
        b.style.width = StepButtonSize;
        b.style.height = StepButtonSize;
        b.style.flexShrink = 0;
        b.style.marginLeft = 0; b.style.marginRight = 0;
        b.style.paddingLeft = 0; b.style.paddingRight = 0;
        b.style.paddingTop = 0; b.style.paddingBottom = 0;
        b.style.backgroundColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.22f));
        b.style.color = new StyleColor(tint);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(tint);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 5;
        b.RegisterCallback<MouseEnterEvent>(_ =>
            b.style.backgroundColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.40f)));
        b.RegisterCallback<MouseLeaveEvent>(_ =>
            b.style.backgroundColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.22f)));
    }

    private static void StyleQtyField(TextField field)
    {
        // Tall enough for the 18px numeral (see the field's ApplyFont call) — the field used to be
        // pinned to StepButtonSize (30px), which fit the old 15px text but clipped the top/bottom off
        // every digit once the font grew.
        field.style.height = StepButtonSize + 6f;
        var input = field.Q(TextField.textInputUssName);
        if (input != null)
        {
            input.style.backgroundColor = new StyleColor(ColStat);
            input.style.color = new StyleColor(ColTitleText);
            input.style.borderTopWidth = input.style.borderBottomWidth =
                input.style.borderLeftWidth = input.style.borderRightWidth = 2;
            input.style.borderTopColor = input.style.borderBottomColor =
                input.style.borderLeftColor = input.style.borderRightColor = new StyleColor(ColBlueEdge);
            input.style.borderTopLeftRadius = input.style.borderTopRightRadius =
                input.style.borderBottomLeftRadius = input.style.borderBottomRightRadius = 5;
            input.style.unityTextAlign = TextAnchor.MiddleCenter;
            // UI Toolkit's default text-input padding was eating into the taller glyph's headroom —
            // zero it out and let the field's own height (set above) be the only thing centering it.
            input.style.paddingTop = 0; input.style.paddingBottom = 0;
        }
    }

    private static Font LilitaFont()
    {
        if (_lilita != null) return _lilita;
#if UNITY_EDITOR
        var guids = UnityEditor.AssetDatabase.FindAssets("Lilita t:Font");
        if (guids.Length > 0)
            _lilita = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
        if (_lilita == null) _lilita = Resources.Load<Font>("Fonts/LilitaOne-Regular");
        return _lilita;
    }

    private static void ApplyFont(VisualElement element, bool bold = false, int size = 14)
    {
        var font = LilitaFont();
        if (font != null) element.style.unityFont = new StyleFont(font);
        element.style.fontSize = size;
        element.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
    }

    // ── INBOUND ORDER CREATION — multi-vendor deal-shopping tab ────────────────
    //
    // Comparison shopping: vendors are the collapsible groups (icon, running totals, its own trailer
    // fill-bar, its own DISPATCH ORDER), and expanding one lists that vendor's items — so the same SKU
    // can legitimately appear under several different vendors at different Partnership-driven prices,
    // and the player can build several vendors' loads side by side before dispatching any of them.
    // This tab replaced the old single-vendor "pick one house, build one basket" Create tab entirely,
    // taking over both its name and its position as the first tab.

    private void BuildMultiVendorTab()
    {
        _multiVendorPane.Clear();

        // The Broker's salvage loads and the Spot Deals board live here now, at the top of Inbound
        // Order Creation — this is the tab that actually builds and dispatches loads, so "deals" (a
        // one-click way to fill part of a load cheaply) belongs here rather than on the vendor roster.
        // Both collapse to nothing when empty (see their own doc comments), so this costs nothing on
        // a day with no live offers.
        _multiVendorPane.Add(BuildSalvageStrip());
        _multiVendorPane.Add(BuildSpotDealsStrip());
        _multiVendorPane.Add(BuildMultiVendorFilterBar());

        var registry = VendorRegistry.Load();
        var vendors = registry?.AllVendors ?? new List<VendorData>();
        if (_multiVendorFilterVendorIds.Count > 0)
            vendors = vendors.Where(v => v != null && _multiVendorFilterVendorIds.Contains(v.VendorId)).ToList();
        if (_multiVendorFilterCriticalOnly)
            vendors = vendors.Where(v => v != null && CountCriticalItems(v.VendorId) > 0).ToList();
        if (!string.IsNullOrEmpty(_multiVendorItemFilterSkuId))
            vendors = vendors.Where(v => v != null && VendorCarries(v.VendorId, _multiVendorItemFilterSkuId)).ToList();

        if (vendors.Count == 0)
        {
            var none = MakeText("No vendors match that filter.", 16, ColEmptyText);
            none.style.unityTextAlign = TextAnchor.MiddleCenter;
            none.style.marginTop = 30;
            _multiVendorPane.Add(none);
            return;
        }

        IOrderedEnumerable<VendorData> ordered = _multiVendorSortMode switch
        {
            MultiVendorSortMode.PartnershipHighToLow => vendors.Where(v => v != null)
                .OrderByDescending(v => Economy()?.GetState(v.VendorId)?.PartnershipLevel ?? 0)
                .ThenBy(v => v.DisplayName, System.StringComparer.OrdinalIgnoreCase),
            MultiVendorSortMode.PartnershipLowToHigh => vendors.Where(v => v != null)
                .OrderBy(v => Economy()?.GetState(v.VendorId)?.PartnershipLevel ?? 0)
                .ThenBy(v => v.DisplayName, System.StringComparer.OrdinalIgnoreCase),
            _ => vendors.Where(v => v != null)
                .OrderBy(v => v.DisplayName, System.StringComparer.OrdinalIgnoreCase),
        };
        foreach (var vendor in ordered)
            _multiVendorPane.Add(BuildMultiVendorGroup(vendor));
    }

    /// <summary>Header filter row: a multi-select dropdown (any number of vendors, plus a special
    /// "Critical Items" entry that pulls up vendors currently carrying at least one item in net
    /// demand) — replacing the old free-text search box, per Tad's explicit request.</summary>
    private VisualElement BuildMultiVendorFilterBar()
    {
        // The popout lives directly on _modal rather than nested under its own trigger button —
        // Position.Absolute only escapes its parent's LAYOUT box, not the document's PAINT order, so
        // nested here it was still being drawn UNDER the vendor group rows added right after this bar
        // (later siblings of a shared ancestor paint on top regardless of absolute positioning). A
        // stale one from the previous Rebuild() (every filter change rebuilds this bar from scratch)
        // is removed first so re-opening the filter doesn't stack duplicates on the modal.
        _modal.Q<VisualElement>("MultiVendorFilterPopout")?.RemoveFromHierarchy();
        _modal.Q<VisualElement>("MultiVendorSortPopout")?.RemoveFromHierarchy();
        _modal.Q<VisualElement>("MultiVendorItemPopout")?.RemoveFromHierarchy();

        // Cancel any auto-close timer left over from the popout instances Rebuild() is about to
        // replace — a fresh one gets started below (only if still open) so it always targets the
        // live element rather than firing harmlessly on one already gone from the hierarchy.
        _multiVendorFilterAutoClose?.Pause(); _multiVendorFilterAutoClose = null;
        _multiVendorSortAutoClose?.Pause(); _multiVendorSortAutoClose = null;
        _multiVendorItemAutoClose?.Pause(); _multiVendorItemAutoClose = null;

        var bar = new VisualElement();
        bar.style.flexDirection = FlexDirection.Row;
        bar.style.alignItems = Align.FlexStart;
        bar.style.marginBottom = 10;
        bar.style.flexShrink = 0;
        bar.style.position = Position.Relative;

        var label = MakeText("Filter vendors:", 14, ColSubtleText);
        label.style.marginRight = 8;
        label.style.marginTop = 8;
        bar.Add(label);

        var dropdownWrap = new VisualElement();
        dropdownWrap.style.position = Position.Relative;
        bar.Add(dropdownWrap);

        var dropdownBtn = new Button { text = FilterSummaryText() };
        dropdownBtn.name = "MultiVendorFilterButton";
        dropdownBtn.style.width = 240;
        dropdownBtn.style.height = 32;
        ApplyFont(dropdownBtn, bold: true, size: 13);
        dropdownBtn.style.backgroundColor = new StyleColor(ColStat);
        dropdownBtn.style.color = new StyleColor(ColTitleText);
        dropdownBtn.style.borderTopWidth = dropdownBtn.style.borderBottomWidth =
            dropdownBtn.style.borderLeftWidth = dropdownBtn.style.borderRightWidth = 2;
        dropdownBtn.style.borderTopColor = dropdownBtn.style.borderBottomColor =
            dropdownBtn.style.borderLeftColor = dropdownBtn.style.borderRightColor = new StyleColor(ColBlueEdge);
        dropdownBtn.style.borderTopLeftRadius = dropdownBtn.style.borderTopRightRadius =
            dropdownBtn.style.borderBottomLeftRadius = dropdownBtn.style.borderBottomRightRadius = 6;
        dropdownBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
        dropdownWrap.Add(dropdownBtn);

        var popout = new VisualElement { name = "MultiVendorFilterPopout" };
        popout.style.position = Position.Absolute;
        popout.style.width = 260;
        popout.style.maxHeight = 320;
        popout.style.backgroundColor = new StyleColor(ColBg);
        popout.style.borderTopWidth = popout.style.borderBottomWidth =
            popout.style.borderLeftWidth = popout.style.borderRightWidth = 2;
        popout.style.borderTopColor = popout.style.borderBottomColor =
            popout.style.borderLeftColor = popout.style.borderRightColor = new StyleColor(ColBorder);
        popout.style.borderTopLeftRadius = popout.style.borderTopRightRadius =
            popout.style.borderBottomLeftRadius = popout.style.borderBottomRightRadius = 6;
        popout.style.paddingTop = 6; popout.style.paddingBottom = 6;
        popout.style.paddingLeft = 4; popout.style.paddingRight = 4;
        popout.style.display = _multiVendorFilterOpen ? DisplayStyle.Flex : DisplayStyle.None;
        _modal.Add(popout);

        // Positions the popout in _modal's own local space off the trigger button's CURRENT world
        // bound, since it's no longer nested inside dropdownWrap and can't rely on relative layout
        // to sit under the button anymore.
        void PositionPopout()
        {
            Vector2 local = _modal.WorldToLocal(new Vector2(dropdownBtn.worldBound.x, dropdownBtn.worldBound.yMax + 4));
            float maxLeft = Mathf.Max(4f, _modal.resolvedStyle.width - 264f);
            popout.style.left = Mathf.Clamp(local.x, 4f, maxLeft);
            popout.style.top = local.y;
            popout.BringToFront();
        }

        // Covers a Rebuild() that fires while the filter is already open (any toggle click rebuilds
        // this whole bar) — the button isn't laid out yet on the same frame this method runs, so the
        // reposition has to wait one frame, same as CentreOnce's first-show deferral elsewhere.
        if (_multiVendorFilterOpen)
            _modal.schedule.Execute(PositionPopout).ExecuteLater(0);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.maxHeight = 300;
        popout.Add(scroll);

        var criticalToggle = new Toggle("Critical Items") { value = _multiVendorFilterCriticalOnly };
        criticalToggle.style.marginBottom = 4;
        criticalToggle.style.color = new StyleColor(ColDanger);
        ApplyFont(criticalToggle, bold: true, size: 13);
        criticalToggle.RegisterValueChangedCallback(evt =>
        {
            _multiVendorFilterCriticalOnly = evt.newValue;
            dropdownBtn.text = FilterSummaryText();
            Rebuild();
        });
        scroll.Add(criticalToggle);

        var rule = new VisualElement();
        rule.style.height = 1;
        rule.style.marginTop = 2; rule.style.marginBottom = 4;
        rule.style.backgroundColor = new StyleColor(ColBorder);
        scroll.Add(rule);

        var vendors = VendorRegistry.Load()?.AllVendors ?? new List<VendorData>();
        foreach (var vendor in vendors.Where(v => v != null)
                                       .OrderBy(v => v.DisplayName, System.StringComparer.OrdinalIgnoreCase))
        {
            string vendorId = vendor.VendorId;
            var toggle = new Toggle(vendor.DisplayName) { value = _multiVendorFilterVendorIds.Contains(vendorId) };
            ApplyFont(toggle, size: 13);
            toggle.style.color = new StyleColor(ColTitleText);
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) _multiVendorFilterVendorIds.Add(vendorId);
                else _multiVendorFilterVendorIds.Remove(vendorId);
                dropdownBtn.text = FilterSummaryText();
                Rebuild();
            });
            scroll.Add(toggle);
        }

        dropdownBtn.clicked += () =>
        {
            _multiVendorFilterOpen = !_multiVendorFilterOpen;
            popout.style.display = _multiVendorFilterOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_multiVendorFilterOpen) { PositionPopout(); ScheduleFilterAutoClose(popout); }
            else { _multiVendorFilterAutoClose?.Pause(); _multiVendorFilterAutoClose = null; }
        };
        // Sort dropdown — same custom-popout pattern as the vendor filter above (single-select: any
        // option closes the popout and applies immediately, rather than needing an explicit confirm).
        var sortWrap = new VisualElement();
        sortWrap.style.position = Position.Relative;
        sortWrap.style.marginLeft = 10;
        bar.Add(sortWrap);

        var sortBtn = new Button { text = SortSummaryText() };
        sortBtn.name = "MultiVendorSortButton";
        sortBtn.style.width = 240;
        sortBtn.style.height = 32;
        ApplyFont(sortBtn, bold: true, size: 13);
        sortBtn.style.backgroundColor = new StyleColor(ColStat);
        sortBtn.style.color = new StyleColor(ColTitleText);
        sortBtn.style.borderTopWidth = sortBtn.style.borderBottomWidth =
            sortBtn.style.borderLeftWidth = sortBtn.style.borderRightWidth = 2;
        sortBtn.style.borderTopColor = sortBtn.style.borderBottomColor =
            sortBtn.style.borderLeftColor = sortBtn.style.borderRightColor = new StyleColor(ColBlueEdge);
        sortBtn.style.borderTopLeftRadius = sortBtn.style.borderTopRightRadius =
            sortBtn.style.borderBottomLeftRadius = sortBtn.style.borderBottomRightRadius = 6;
        sortBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
        sortWrap.Add(sortBtn);

        var sortPopout = new VisualElement { name = "MultiVendorSortPopout" };
        sortPopout.style.position = Position.Absolute;
        sortPopout.style.width = 240;
        sortPopout.style.backgroundColor = new StyleColor(ColBg);
        sortPopout.style.borderTopWidth = sortPopout.style.borderBottomWidth =
            sortPopout.style.borderLeftWidth = sortPopout.style.borderRightWidth = 2;
        sortPopout.style.borderTopColor = sortPopout.style.borderBottomColor =
            sortPopout.style.borderLeftColor = sortPopout.style.borderRightColor = new StyleColor(ColBorder);
        sortPopout.style.borderTopLeftRadius = sortPopout.style.borderTopRightRadius =
            sortPopout.style.borderBottomLeftRadius = sortPopout.style.borderBottomRightRadius = 6;
        sortPopout.style.paddingTop = 4; sortPopout.style.paddingBottom = 4;
        sortPopout.style.paddingLeft = 4; sortPopout.style.paddingRight = 4;
        sortPopout.style.display = _multiVendorSortOpen ? DisplayStyle.Flex : DisplayStyle.None;
        _modal.Add(sortPopout);

        void PositionSortPopout()
        {
            Vector2 local = _modal.WorldToLocal(new Vector2(sortBtn.worldBound.x, sortBtn.worldBound.yMax + 4));
            float maxLeft = Mathf.Max(4f, _modal.resolvedStyle.width - 244f);
            sortPopout.style.left = Mathf.Clamp(local.x, 4f, maxLeft);
            sortPopout.style.top = local.y;
            sortPopout.BringToFront();
        }

        if (_multiVendorSortOpen)
            _modal.schedule.Execute(PositionSortPopout).ExecuteLater(0);

        void AddSortOption(string optionLabel, MultiVendorSortMode mode)
        {
            bool selected = _multiVendorSortMode == mode;
            var opt = new Button { text = (selected ? "✓ " : "    ") + optionLabel };
            opt.style.width = new StyleLength(StyleKeyword.Auto);
            opt.style.height = 30;
            opt.style.marginLeft = 0; opt.style.marginRight = 0; opt.style.marginTop = 0; opt.style.marginBottom = 0;
            opt.style.borderTopWidth = opt.style.borderBottomWidth =
                opt.style.borderLeftWidth = opt.style.borderRightWidth = 0;
            opt.style.backgroundColor = new StyleColor(selected ? ColCardEven : Color.clear);
            opt.style.color = new StyleColor(selected ? ColOrangeText : ColTitleText);
            ApplyFont(opt, bold: selected, size: 13);
            opt.style.unityTextAlign = TextAnchor.MiddleLeft;
            opt.clicked += () =>
            {
                _multiVendorSortMode = mode;
                _multiVendorSortOpen = false;
                _multiVendorSortAutoClose?.Pause(); _multiVendorSortAutoClose = null;
                Rebuild();
            };
            sortPopout.Add(opt);
        }
        AddSortOption("Name (A–Z)", MultiVendorSortMode.NameAZ);
        AddSortOption("Partnership: High to Low", MultiVendorSortMode.PartnershipHighToLow);
        AddSortOption("Partnership: Low to High", MultiVendorSortMode.PartnershipLowToHigh);

        sortBtn.clicked += () =>
        {
            _multiVendorSortOpen = !_multiVendorSortOpen;
            sortPopout.style.display = _multiVendorSortOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_multiVendorSortOpen) { PositionSortPopout(); ScheduleSortAutoClose(sortPopout); }
            else { _multiVendorSortAutoClose?.Pause(); _multiVendorSortAutoClose = null; }
        };

        // Item filter — same custom-popout pattern as Sort (single-select, picking an item applies
        // immediately and closes). Narrows every vendor group down to one SKU so the same item can be
        // compared across vendors without expanding each one and hunting for it.
        var itemWrap = new VisualElement();
        itemWrap.style.position = Position.Relative;
        itemWrap.style.marginLeft = 10;
        bar.Add(itemWrap);

        var itemBtn = new Button { text = ItemFilterSummaryText() };
        itemBtn.name = "MultiVendorItemButton";
        itemBtn.style.width = 220;
        itemBtn.style.height = 32;
        ApplyFont(itemBtn, bold: true, size: 13);
        itemBtn.style.backgroundColor = new StyleColor(ColStat);
        itemBtn.style.color = new StyleColor(ColTitleText);
        itemBtn.style.borderTopWidth = itemBtn.style.borderBottomWidth =
            itemBtn.style.borderLeftWidth = itemBtn.style.borderRightWidth = 2;
        itemBtn.style.borderTopColor = itemBtn.style.borderBottomColor =
            itemBtn.style.borderLeftColor = itemBtn.style.borderRightColor = new StyleColor(ColBlueEdge);
        itemBtn.style.borderTopLeftRadius = itemBtn.style.borderTopRightRadius =
            itemBtn.style.borderBottomLeftRadius = itemBtn.style.borderBottomRightRadius = 6;
        itemBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
        itemWrap.Add(itemBtn);

        var itemPopout = new VisualElement { name = "MultiVendorItemPopout" };
        itemPopout.style.position = Position.Absolute;
        itemPopout.style.width = 260;
        itemPopout.style.maxHeight = 320;
        itemPopout.style.backgroundColor = new StyleColor(ColBg);
        itemPopout.style.borderTopWidth = itemPopout.style.borderBottomWidth =
            itemPopout.style.borderLeftWidth = itemPopout.style.borderRightWidth = 2;
        itemPopout.style.borderTopColor = itemPopout.style.borderBottomColor =
            itemPopout.style.borderLeftColor = itemPopout.style.borderRightColor = new StyleColor(ColBorder);
        itemPopout.style.borderTopLeftRadius = itemPopout.style.borderTopRightRadius =
            itemPopout.style.borderBottomLeftRadius = itemPopout.style.borderBottomRightRadius = 6;
        itemPopout.style.paddingTop = 4; itemPopout.style.paddingBottom = 4;
        itemPopout.style.paddingLeft = 4; itemPopout.style.paddingRight = 4;
        itemPopout.style.display = _multiVendorItemFilterOpen ? DisplayStyle.Flex : DisplayStyle.None;
        _modal.Add(itemPopout);

        void PositionItemPopout()
        {
            Vector2 local = _modal.WorldToLocal(new Vector2(itemBtn.worldBound.x, itemBtn.worldBound.yMax + 4));
            float maxLeft = Mathf.Max(4f, _modal.resolvedStyle.width - 264f);
            itemPopout.style.left = Mathf.Clamp(local.x, 4f, maxLeft);
            itemPopout.style.top = local.y;
            itemPopout.BringToFront();
        }

        if (_multiVendorItemFilterOpen)
            _modal.schedule.Execute(PositionItemPopout).ExecuteLater(0);

        var itemScroll = new ScrollView(ScrollViewMode.Vertical);
        itemScroll.style.maxHeight = 300;
        itemPopout.Add(itemScroll);

        void AddItemOption(string optionLabel, string skuId)
        {
            bool selected = _multiVendorItemFilterSkuId == skuId;
            var opt = new Button { text = (selected ? "✓ " : "    ") + optionLabel };
            opt.style.width = new StyleLength(StyleKeyword.Auto);
            opt.style.height = 28;
            opt.style.marginLeft = 0; opt.style.marginRight = 0; opt.style.marginTop = 0; opt.style.marginBottom = 0;
            opt.style.borderTopWidth = opt.style.borderBottomWidth =
                opt.style.borderLeftWidth = opt.style.borderRightWidth = 0;
            opt.style.backgroundColor = new StyleColor(selected ? ColCardEven : Color.clear);
            opt.style.color = new StyleColor(selected ? ColOrangeText : ColTitleText);
            ApplyFont(opt, bold: selected, size: 13);
            opt.style.unityTextAlign = TextAnchor.MiddleLeft;
            opt.style.whiteSpace = WhiteSpace.NoWrap;
            opt.clicked += () =>
            {
                ApplyItemFilterSelection(skuId);
                _multiVendorItemFilterOpen = false;
                _multiVendorItemAutoClose?.Pause(); _multiVendorItemAutoClose = null;
                Rebuild();
            };
            itemScroll.Add(opt);
        }
        AddItemOption("All Items", null);

        var itemRule = new VisualElement();
        itemRule.style.height = 1;
        itemRule.style.marginTop = 2; itemRule.style.marginBottom = 4;
        itemRule.style.backgroundColor = new StyleColor(ColBorder);
        itemScroll.Add(itemRule);

        foreach (var (skuId, displayName) in AllCataloguedItems())
            AddItemOption(displayName, skuId);

        itemBtn.clicked += () =>
        {
            _multiVendorItemFilterOpen = !_multiVendorItemFilterOpen;
            itemPopout.style.display = _multiVendorItemFilterOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_multiVendorItemFilterOpen) { PositionItemPopout(); ScheduleItemAutoClose(itemPopout); }
            else { _multiVendorItemAutoClose?.Pause(); _multiVendorItemAutoClose = null; }
        };

        var hint = MakeText("The same item can appear under several vendors at different prices — " +
                             "compare and build multiple loads at once.", 12, ColEmptyText);
        hint.style.marginLeft = 16;
        hint.style.marginTop = 8;
        hint.style.whiteSpace = WhiteSpace.Normal;
        hint.style.flexShrink = 1;
        bar.Add(hint);

        // A Rebuild() can land here while a popout is still open (e.g. ticking a vendor checkbox, or
        // the Critical Items toggle, both call Rebuild() without touching the *Open flag) — restart
        // its 3s auto-close countdown against the freshly-built instance so an active pick session
        // keeps getting a full 3s from its last interaction instead of inheriting a stale timer.
        if (_multiVendorFilterOpen) ScheduleFilterAutoClose(popout);
        if (_multiVendorSortOpen) ScheduleSortAutoClose(sortPopout);
        if (_multiVendorItemFilterOpen) ScheduleItemAutoClose(itemPopout);

        return bar;
    }

    private string FilterSummaryText()
    {
        int vendorCount = _multiVendorFilterVendorIds.Count;
        if (vendorCount == 0 && !_multiVendorFilterCriticalOnly) return "All Vendors ▾";
        var parts = new List<string>();
        if (_multiVendorFilterCriticalOnly) parts.Add("Critical Items");
        if (vendorCount > 0) parts.Add($"{vendorCount} vendor{(vendorCount == 1 ? "" : "s")}");
        return string.Join(" + ", parts) + " ▾";
    }

    private string ItemFilterSummaryText()
    {
        if (string.IsNullOrEmpty(_multiVendorItemFilterSkuId)) return "Item: All ▾";
        var name = AllCataloguedItems().FirstOrDefault(i => i.skuId == _multiVendorItemFilterSkuId).displayName;
        return $"Item: {name ?? _multiVendorItemFilterSkuId} ▾";
    }

    /// <summary>Every distinct SKU carried by ANY vendor's catalogue, alphabetical by name — the
    /// option list for the Item filter dropdown. Small enough (vendor catalogues, not the whole SKU
    /// database) to just union on every open rather than caching.</summary>
    private List<(string skuId, string displayName)> AllCataloguedItems()
    {
        var registry = VendorRegistry.Load();
        var vendors = registry?.AllVendors ?? new List<VendorData>();
        var seen = new Dictionary<string, string>(); // skuId -> displayName

        foreach (var vendor in vendors)
        {
            if (vendor == null) continue;
            var catalogue = Economy()?.GetAvailableCatalogue(vendor.VendorId) ?? new List<VendorCatalogueEntry>();
            foreach (var entry in catalogue)
            {
                var sku = entry?.Sku;
                if (sku == null || seen.ContainsKey(sku.SkuId)) continue;
                seen[sku.SkuId] = string.IsNullOrEmpty(sku.ItemDescription) ? sku.SkuId : sku.ItemDescription;
            }
        }

        return seen.Select(kv => (skuId: kv.Key, displayName: kv.Value))
                   .OrderBy(i => i.displayName, System.StringComparer.OrdinalIgnoreCase)
                   .ToList();
    }
    private string SortSummaryText() => _multiVendorSortMode switch
    {
        MultiVendorSortMode.PartnershipHighToLow => "Sort: Partnership High to Low ▾",
        MultiVendorSortMode.PartnershipLowToHigh => "Sort: Partnership Low to High ▾",
        _ => "Sort: Name (A–Z) ▾",
    };


    /// <summary>One vendor's collapsible group: header (icon, name, running totals, trailer fill-bar,
    /// DISPATCH ORDER) plus a detail list of that vendor's orderable items, shown/hidden by
    /// `_expandedMultiVendors`. Mirrors ToolsWindowController.BuildShipmentRow's header/detail toggle —
    /// the one existing expand/collapse precedent in this codebase.</summary>
    private VisualElement BuildMultiVendorGroup(VendorData vendor)
    {
        string vendorId = vendor.VendorId;
        bool expanded = _expandedMultiVendors.Contains(vendorId);

        var container = new VisualElement();
        container.style.marginBottom = 6;
        container.style.overflow = Overflow.Hidden;
        container.style.borderTopLeftRadius = container.style.borderTopRightRadius =
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
        container.style.borderTopWidth = container.style.borderBottomWidth =
            container.style.borderLeftWidth = container.style.borderRightWidth = 2;
        container.style.borderTopColor = container.style.borderBottomColor =
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(ColBorder);

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.paddingTop = 8; header.style.paddingBottom = 8;
        header.style.paddingLeft = 10; header.style.paddingRight = 10;
        header.style.backgroundColor = new StyleColor(ColStat);
        header.pickingMode = PickingMode.Position;

        var arrow = MakeText(expanded ? "▾" : "▸", 16, ColTitleText, bold: true);
        arrow.style.width = 18;
        arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
        header.Add(arrow);

        var icon = new VisualElement();
        icon.style.width = 36; icon.style.height = 36;
        icon.style.flexShrink = 0;
        icon.style.marginLeft = 6; icon.style.marginRight = 10;
        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius =
            icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = 5;
        if (vendor.Icon != null) icon.style.backgroundImage = new StyleBackground(vendor.Icon);
        else icon.style.backgroundColor = new StyleColor(ColBlueEdge);
        header.Add(icon);

        var nameCol = new VisualElement();
        nameCol.style.width = 220;
        nameCol.style.flexShrink = 0;
        header.Add(nameCol);

        var name = MakeText(vendor.DisplayName, 17, ColTitleText, bold: true);
        name.style.marginBottom = 2;
        nameCol.Add(name);

        int partnershipLevel = Economy()?.GetState(vendorId)?.PartnershipLevel ?? 0;
        var partnership = MakeText(
            $"{partnershipLevel:+0;-0;0}  {PartnershipColorUtility.GetStatusText(partnershipLevel)}",
            12, PartnershipColorUtility.GetColor(partnershipLevel), bold: true);
        nameCol.Add(partnership);

        var statsRow = new VisualElement();
        statsRow.style.flexDirection = FlexDirection.Row;
        statsRow.style.alignItems = Align.Center;
        statsRow.style.flexGrow = 1;
        statsRow.style.flexWrap = Wrap.Wrap;
        header.Add(statsRow);

        var stats = MakeText("", 15, ColSubtleText);
        stats.style.whiteSpace = WhiteSpace.NoWrap;
        // Widened into its own fixed-width cell — stretched out to roughly where the truck fill bar's
        // dead space used to start (per Tad's marked-up screenshot) — so every row's case/pallet/
        // critical-items text lines up as a real column instead of hugging whatever length that row's
        // own text happens to be.
        stats.style.width = 380;
        stats.style.flexShrink = 0;
        statsRow.Add(stats);

        // Cost of the load being built — centred in the dead space between the stats column and the
        // truck fill bar (rather than hugging directly against the stats text) per Tad's explicit
        // request. Twice the size of the other detail labels and painted the same green as DISPATCH
        // ORDER, unchanged from before: it's the number that matters most once a truck starts filling
        // up, it's just relocated and centred within its own flexGrow cell now.
        var costBox = new VisualElement();
        costBox.style.flexGrow = 1;
        costBox.style.alignItems = Align.Center;
        costBox.style.justifyContent = Justify.Center;
        statsRow.Add(costBox);

        var costLabel = MakeText("", 26, ColCreateGreen, bold: true);
        costLabel.style.whiteSpace = WhiteSpace.NoWrap;
        costBox.Add(costLabel);

        var costCaption = MakeText("Cost of Load", 11, ColCreateGreen, bold: true);
        costCaption.style.whiteSpace = WhiteSpace.NoWrap;
        costCaption.style.marginTop = 0;
        costBox.Add(costCaption);

        var fillBarContainer = BuildTruckFillBar(out var fillElement);
        header.Add(fillBarContainer);

        // DISPATCH ORDER (top) and DEALS (bottom), stacked to the right of the truck sprite so their
        // combined height matches the sprite's own height (TruckActionsColumnHeight == BuildTruckFillBar's
        // `h`) and the two read as one unit rather than two mismatched buttons — per Tad's explicit
        // request to move DEALS below DISPATCH ORDER and size them as evenly as possible.
        var actionsColumn = new VisualElement();
        actionsColumn.style.flexDirection = FlexDirection.Column;
        actionsColumn.style.justifyContent = Justify.SpaceBetween;
        actionsColumn.style.marginLeft = 10;
        actionsColumn.style.width = 170;
        actionsColumn.style.height = TruckActionsColumnHeight;
        actionsColumn.style.flexShrink = 0;
        header.Add(actionsColumn);

        var dispatch = new Button(() => { AudioManager.Play("UIClick"); DispatchVendorOrder(vendor); }) { text = "DISPATCH ORDER" };
        StyleActionButton(dispatch, ColCreateGreen, ColCreateGreenEdge, ColCreateGreenHover);
        dispatch.style.width = 170;
        dispatch.style.height = TruckActionButtonHeight;
        ApplyFont(dispatch, bold: true, size: 12);
        actionsColumn.Add(dispatch);

        // Deals fill bar — the same countdown/drain visual the VENDORS tab's row used to carry,
        // moved here (under DISPATCH ORDER) per Tad's explicit request: deals belong with the tab
        // that actually builds and dispatches loads.
        var dealsBar = BuildMultiVendorDealBar(out var dealsFill, out var dealsLabel);
        dealsBar.style.width = 170;
        dealsBar.style.height = TruckActionButtonHeight;
        dealsBar.RegisterCallback<ClickEvent>(evt =>
        {
            AudioManager.Play("UIClick");
            OnMultiVendorDealClicked(vendor);
            evt.StopPropagation();
        });
        actionsColumn.Add(dealsBar);

        // Shared by the header's own initial paint and by every item row's qty change below — one
        // place that reads the current basket and repaints the header, so stats/fill-bar/dispatch
        // button can never disagree with what's actually in `_multiBaskets[vendorId]`. A TARGETED
        // refresh rather than a full Rebuild(): a full Rebuild() would reset both this tab's outer
        // scroll position and every expanded vendor's own inner scroll position back to the top on
        // every single +/- press, which is exactly the annoyance BuildItemCard's own Apply() (on the
        // old Create tab) already avoids the same way.
        void RefreshHeader()
        {
            var lines = MultiBasketLines(vendorId);
            var plan = TrailerCapacity.Plan(lines);
            float cost = lines.Sum(l => l.cases * UnitPriceForVendor(vendorId, l.sku));
            int critical = CountCriticalItems(vendorId);
            stats.text = $"{lines.Sum(l => l.cases):N0} case(s) · {plan.Pallets.Count:N0} pallet(s) · " +
                         $"[{critical}] critical items";
            costLabel.text = Money(cost);
            fillElement.style.width = Mathf.Clamp01(plan.Fill01) *
                (TruckBoxRightFrac - TruckBoxLeftFrac) * TruckFillBarWidth;

            bool hasItems = _multiBaskets.TryGetValue(vendorId, out var basket) && basket.Count > 0;
            dispatch.SetEnabled(hasItems);
            dispatch.style.opacity = hasItems ? 1f : 0.5f;
        }
        RefreshHeader();

        // Polled at the same 100ms cadence VendorsTabView's old PollDealBars used (see its doc
        // comment) — the deal's live countdown/expiry has nothing to do with basket edits, so it gets
        // its own light tick instead of piggybacking on qty-change repaints.
        void RefreshDealButton()
        {
            var deal = Deals()?.GetActiveDeal(vendorId);
            dealsFill.style.display = deal != null ? DisplayStyle.Flex : DisplayStyle.None;
            dealsLabel.text = deal != null ? $"DEAL! -{deal.DiscountPercent:0}%" : "NO DEALS";
            if (deal != null)
                dealsFill.style.width = new Length(Mathf.Clamp01(deal.Fraction) * 100f, LengthUnit.Percent);
            dealsBar.style.opacity = deal != null ? 1f : 0.5f;
            dealsBar.pickingMode = deal != null ? PickingMode.Position : PickingMode.Ignore;
        }
        RefreshDealButton();
        header.schedule.Execute(RefreshDealButton).Every(100);

        var detail = new ScrollView(ScrollViewMode.Vertical);
        detail.style.maxHeight = 340;
        detail.style.paddingTop = 6; detail.style.paddingBottom = 6;
        detail.style.paddingLeft = 8; detail.style.paddingRight = 8;
        detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

        var catalogue = Economy()?.GetAvailableCatalogue(vendorId) ?? new List<VendorCatalogueEntry>();
        int shown = 0;
        foreach (var entry in catalogue)
        {
            if (entry?.Sku == null) continue;
            if (!string.IsNullOrEmpty(_multiVendorItemFilterSkuId) && entry.Sku.SkuId != _multiVendorItemFilterSkuId) continue;
            detail.Add(BuildMultiVendorItemRow(vendor, entry.Sku, shown, RefreshHeader,
                () => PulsePalletAdded(costLabel)));
            shown++;
        }
        if (shown == 0)
        {
            var noneLabel = MakeText(
                !string.IsNullOrEmpty(_multiVendorItemFilterSkuId)
                    ? "This vendor doesn't carry that item."
                    : "This vendor has nothing orderable yet.", 13, ColEmptyText);
            noneLabel.style.marginTop = 8;
            detail.Add(noneLabel);
        }

        header.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.target is Button) return; // let DISPATCH ORDER handle its own click
            AudioManager.Play("UIClick");
            bool nowExpanded = detail.style.display == DisplayStyle.None;
            detail.style.display = nowExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            arrow.text = nowExpanded ? "▾" : "▸";
            if (nowExpanded) _expandedMultiVendors.Add(vendorId); else _expandedMultiVendors.Remove(vendorId);
        });

        container.Add(header);
        container.Add(detail);
        return container;
    }

    /// <summary>One SKU under one vendor's expanded list: icon, id/description, On Hand/On Order/In
    /// Demand/Buy/Sell/Margin, and the same +/-/typed-field stepper `BuildItemCard` uses on the old
    /// tab — writing into `_multiBaskets[vendorId]` instead of `_basket`. `onQtyChanged` is the owning
    /// group's RefreshHeader, called instead of a full Rebuild() so this row's own scroll position
    /// (and every other vendor's) survives a quantity change.</summary>
    private VisualElement BuildMultiVendorItemRow(VendorData vendor, SkuData sku, int rowIndex, System.Action onQtyChanged,
        System.Action onPalletAdded = null)
    {
        string vendorId = vendor.VendorId;
        string skuId = sku.SkuId;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 6; row.style.paddingBottom = 6;
        row.style.paddingLeft = 8; row.style.paddingRight = 8;
        row.style.marginBottom = 4;
        row.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColCardEven : ColCardOdd);
        row.style.borderTopLeftRadius = row.style.borderTopRightRadius =
            row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 6;

        var icon = new VisualElement();
        icon.style.width = 36; icon.style.height = 36;
        icon.style.flexShrink = 0;
        icon.style.marginRight = 8;
        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius =
            icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = 5;
        if (sku.Icon != null) icon.style.backgroundImage = new StyleBackground(sku.Icon);
        else icon.style.backgroundColor = new StyleColor(ColBlueEdge);
        row.Add(icon);

        int onHand = Inventory()?.TotalOnHand(skuId) ?? 0;
        int onOrder = Economy()?.GetTotalOnOrder(skuId) ?? 0;

        // IN DEMAND is the REMAINING shortfall, not the raw outbound requirement — it has to fall as
        // the player covers it. Per Tad's explicit request this now falls the MOMENT cases go onto a
        // trailer's load, not just once that load is actually dispatched as a real PO — so it nets off
        // on-hand stock, cases already on a dispatched PO (on-order), AND cases sitting in ANY
        // in-progress basket right now (this tab's per-vendor baskets, and the old single-vendor tab's
        // basket) — see TotalInProgressCases. Ordering enough to cover demand should read as "handled"
        // (0) as soon as it's on a load, not stay pinned at the gross number until dispatch.
        int grossInDemand = Economy()?.GetTotalInDemand(skuId) ?? 0;
        int inProgress = TotalInProgressCases(skuId);
        int inDemand = Mathf.Max(0, grossInDemand - onHand - onOrder - inProgress);

        var idCol = new VisualElement();
        idCol.style.width = 190;
        idCol.style.flexShrink = 0;
        idCol.style.marginRight = 10;
        // Matches the NETTED figure, not the raw gross demand — a SKU that's fully covered (on hand,
        // on order, or already loaded onto a trailer right now) has to read as normal again, not stay
        // red forever just because someone somewhere still wants it in the abstract.
        var num = MakeText(sku.SkuId, 11, inDemand > 0 ? ColDanger : ColSubtleText);
        num.style.marginTop = 0; num.style.marginBottom = 0;
        idCol.Add(num);
        var desc = MakeText(sku.ItemDescription, 14, ColTitleText, bold: true);
        desc.style.whiteSpace = WhiteSpace.Normal;
        desc.style.marginTop = 0;
        idCol.Add(desc);
        row.Add(idCol);

        int buy = UnitPriceForVendor(vendorId, sku);
        float sell = sku.SellValue;
        float margin = sell > 0f ? (sell - buy) / sell : 0f;

        row.Add(MultiVendorStatCell("ON HAND", onHand.ToString("N0"), ColSubtleText));
        row.Add(MultiVendorStatCell("ON ORDER", onOrder.ToString("N0"), ColSubtleText));
        var inDemandCell = MultiVendorStatCell("IN DEMAND", inDemand.ToString("N0"),
                                                inDemand > 0 ? ColDanger : ColSubtleText, out var inDemandLabel);
        row.Add(inDemandCell);
        row.Add(MultiVendorStatCell("BUY", Money(buy), ColBuyPrice));
        row.Add(MultiVendorStatCell("SELL", Money(sell), ColSellPrice));
        row.Add(MultiVendorStatCell("MARGIN", $"{margin * 100f:0}%", margin >= 0f ? ColMoney : ColDanger));

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        row.Add(spacer);

        var qtyRow = new VisualElement();
        qtyRow.style.flexDirection = FlexDirection.Row;
        qtyRow.style.alignItems = Align.Center;
        qtyRow.style.flexShrink = 0;

        var minus = new Button { text = "–" };
        StyleStepButton(minus, ColDanger);
        qtyRow.Add(minus);

        var field = new TextField { value = MultiQty(vendorId, skuId).ToString(), isDelayed = true };
        field.style.width = QtyFieldWidth;
        field.style.marginLeft = 6; field.style.marginRight = 6;
        ApplyFont(field, bold: true, size: 16);
        StyleQtyField(field);
        qtyRow.Add(field);

        var plus = new Button { text = "+" };
        StyleStepButton(plus, ColMoney);
        qtyRow.Add(plus);

        row.Add(qtyRow);

        void Apply(int newQty)
        {
            TryMultiSetQtyWithinCapacity(vendorId, sku, newQty);
            field.SetValueWithoutNotify(MultiQty(vendorId, skuId).ToString());

            // This row's own IN DEMAND number has to move the instant its qty changes, not wait for a
            // full Rebuild() — that's the whole point of netting against in-progress cases. Other
            // expanded vendor groups carrying the SAME sku won't see their copy update until the tab
            // next rebuilds (switching tabs, dispatching, etc.) — an accepted gap, since chasing that
            // live across every open group would mean a full Rebuild() on every keystroke/click here,
            // which is exactly the scroll-position churn this targeted-refresh approach exists to avoid.
            int newInDemand = Mathf.Max(0, (Economy()?.GetTotalInDemand(skuId) ?? 0) -
                (Inventory()?.TotalOnHand(skuId) ?? 0) - (Economy()?.GetTotalOnOrder(skuId) ?? 0) -
                TotalInProgressCases(skuId));
            inDemandLabel.text = newInDemand.ToString("N0");
            inDemandLabel.style.color = new StyleColor(newInDemand > 0 ? ColDanger : ColSubtleText);
            num.style.color = new StyleColor(newInDemand > 0 ? ColDanger : ColSubtleText);

            onQtyChanged?.Invoke();
        }

        int step = Mathf.Max(1, sku.Ti * sku.Hi);
        minus.clicked += () =>
        {
            AudioManager.Play("OrderDecrease");
            Apply(MultiQty(vendorId, skuId) - step);
        };
        plus.clicked += () =>
        {
            AudioManager.Play("OrderIncrease");
            Apply(MultiQty(vendorId, skuId) + step);
            PulsePalletAdded(field);
            onPalletAdded?.Invoke();
        };
        field.RegisterValueChangedCallback(evt =>
            Apply(int.TryParse(evt.newValue, out int typed) ? typed : MultiQty(vendorId, skuId)));

        return row;
    }

    /// <summary>Feedback for "a pallet just got added to this load": the qty field lerps up to 130%
    /// of its normal size and back down to normal over a 1-second round trip, in the Lilita font this
    /// whole panel already uses for its text (see ApplyFont) — per Tad's explicit request.</summary>
    private static void PulsePalletAdded(VisualElement field)
    {
        field.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale") };
        field.style.transitionDuration = new List<TimeValue> { new TimeValue(500, TimeUnit.Millisecond) };
        field.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOut) };
        field.style.scale = new Scale(new Vector2(1.3f, 1.3f));

        field.schedule.Execute(() =>
        {
            field.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseIn) };
            field.style.scale = new Scale(Vector2.one);
        }).ExecuteLater(500);
    }

    private VisualElement MultiVendorStatCell(string caption, string value, Color valueColor)
        => MultiVendorStatCell(caption, value, valueColor, out _);

    private VisualElement MultiVendorStatCell(string caption, string value, Color valueColor, out Label valueLabel)
    {
        var cell = new VisualElement();
        cell.style.width = 84;
        cell.style.flexShrink = 0;
        cell.style.marginRight = 10;

        var cap = MakeText(caption, 10, ColSubtleText, bold: true);
        cap.style.marginTop = 0; cap.style.marginBottom = 0;
        cell.Add(cap);

        var val = MakeText(value, 15, valueColor, bold: true);
        val.style.marginTop = 0; val.style.marginBottom = 0;
        cell.Add(val);
        valueLabel = val;

        return cell;
    }

    /// <summary>Cases of this SKU sitting on ANY in-progress (not-yet-dispatched) load right now, across
    /// every vendor's own `_multiBaskets` entry. Subtracted from gross demand so IN DEMAND falls the
    /// moment cases go onto a trailer, not just once that trailer is actually dispatched as a real PO
    /// (see BuildMultiVendorItemRow/Apply).</summary>
    private int TotalInProgressCases(string skuId)
    {
        int total = 0;
        foreach (var kv in _multiBaskets)
            if (kv.Value.TryGetValue(skuId, out int mc)) total += mc;
        return total;
    }

    /// <summary>Sets the Item filter and expands/collapses vendor groups to match — every vendor
    /// carrying the item pops open the moment a specific SKU is picked, and picking "All Items" (null)
    /// puts every group back exactly how it was before searching (see _expandedBeforeItemFilter).
    /// Switching directly from one SKU to another re-expands for the new item without touching the
    /// saved pre-search snapshot, so the ORIGINAL state is still what comes back at the end.</summary>
    private void ApplyItemFilterSelection(string skuId)
    {
        if (!string.IsNullOrEmpty(skuId))
        {
            if (_expandedBeforeItemFilter == null)
                _expandedBeforeItemFilter = new HashSet<string>(_expandedMultiVendors);

            var vendors = VendorRegistry.Load()?.AllVendors ?? new List<VendorData>();
            foreach (var vendor in vendors)
                if (vendor != null && VendorCarries(vendor.VendorId, skuId))
                    _expandedMultiVendors.Add(vendor.VendorId);
        }
        else if (_expandedBeforeItemFilter != null)
        {
            _expandedMultiVendors.Clear();
            foreach (var id in _expandedBeforeItemFilter) _expandedMultiVendors.Add(id);
            _expandedBeforeItemFilter = null;
        }

        _multiVendorItemFilterSkuId = skuId;
    }

    /// <summary>True if this vendor's catalogue includes the given SKU — the gate the item filter uses
    /// to hide vendor groups that don't carry the selected item at all.</summary>
    private bool VendorCarries(string vendorId, string skuId)
    {
        var catalogue = Economy()?.GetAvailableCatalogue(vendorId) ?? new List<VendorCatalogueEntry>();
        foreach (var entry in catalogue)
            if (entry?.Sku != null && entry.Sku.SkuId == skuId) return true;
        return false;
    }

    /// <summary>How many distinct SKUs this vendor carries are still in NET demand right now — same
    /// netting BuildMultiVendorItemRow's own IN DEMAND cell uses (on-hand + on-order + in-progress
    /// subtracted off), so a vendor's "[N] critical items" count and its expanded rows' own red IN
    /// DEMAND numbers can never disagree. Drives the vendor header's summary line.</summary>
    private int CountCriticalItems(string vendorId)
    {
        var catalogue = Economy()?.GetAvailableCatalogue(vendorId) ?? new List<VendorCatalogueEntry>();
        int count = 0;
        foreach (var entry in catalogue)
        {
            var sku = entry?.Sku;
            if (sku == null) continue;

            int gross = Economy()?.GetTotalInDemand(sku.SkuId) ?? 0;
            int onHand = Inventory()?.TotalOnHand(sku.SkuId) ?? 0;
            int onOrder = Economy()?.GetTotalOnOrder(sku.SkuId) ?? 0;
            int inProgress = TotalInProgressCases(sku.SkuId);
            if (gross - onHand - onOrder - inProgress > 0) count++;
        }
        return count;
    }

    // ── Per-vendor basket (the multi-vendor tab's own state, separate from `_basket`) ────────────

    private int MultiQty(string vendorId, string skuId)
        => !string.IsNullOrEmpty(vendorId) && _multiBaskets.TryGetValue(vendorId, out var basket) &&
           basket.TryGetValue(skuId, out int q) ? q : 0;

    private void MultiSetQty(string vendorId, string skuId, int qty)
    {
        qty = Mathf.Max(0, qty);
        if (!_multiBaskets.TryGetValue(vendorId, out var basket))
        {
            if (qty == 0) return;
            basket = new Dictionary<string, int>();
            _multiBaskets[vendorId] = basket;
        }

        if (qty == 0) basket.Remove(skuId);
        else basket[skuId] = qty;

        // An emptied-out vendor basket is removed entirely rather than left as an empty dictionary —
        // that's what keeps the tab-bar badge (`_multiBaskets.Count(kv => kv.Value.Count > 0)`) and
        // "does this vendor have an in-progress order" checks a simple Count/ContainsKey rather than
        // needing to also check for an empty-but-present entry.
        if (basket.Count == 0) _multiBaskets.Remove(vendorId);
    }

    private List<(SkuData sku, int cases)> MultiBasketLines(string vendorId)
    {
        var list = new List<(SkuData, int)>();
        if (string.IsNullOrEmpty(vendorId) || !_multiBaskets.TryGetValue(vendorId, out var basket)) return list;

        foreach (var kv in basket)
        {
            var sku = FindSku(kv.Key);
            if (sku != null) list.Add((sku, kv.Value));
        }
        return list;
    }

    /// <summary>Same refusal rule as TrySetQtyWithinCapacity, scoped to one vendor's own basket instead
    /// of the single shared `_basket` — a trailer is one trailer per vendor here too.</summary>
    private bool TryMultiSetQtyWithinCapacity(string vendorId, SkuData sku, int newQty)
    {
        if (sku == null) return false;

        if (newQty <= MultiQty(vendorId, sku.SkuId))
        {
            MultiSetQty(vendorId, sku.SkuId, newQty);
            return true;
        }

        if (TrailerCapacity.WouldOverflow(MultiBasketLines(vendorId), sku, newQty, out _))
        {
            ShowNotice("This load is over capacity.\n\nEither remove pallets, or dispatch this order " +
                       "and then start a new one for the rest.");
            return false;
        }

        MultiSetQty(vendorId, sku.SkuId, newQty);
        return true;
    }

    /// <summary>Same seam as BasePrice/UnitPrice, but parameterized on an explicit vendor instead of
    /// reading SelectedVendor() — this tab prices several vendors at once, not just whichever one the
    /// old tab's single `_vendorId` currently points at. Applies a claimed multi-vendor-tab deal
    /// discount (see _multiDealDiscountByKey) on top of the vendor's own Partnership-driven price —
    /// this tab has its own DEALS button per vendor group now, separate from the old tab's.</summary>
    private int UnitPriceForVendor(string vendorId, SkuData sku)
    {
        if (sku == null) return 0;

        int baseCost;
        var economy = Economy();
        if (economy != null)
        {
            int priced = Mathf.RoundToInt(economy.GetEffectiveCost(vendorId, sku, Market()));
            baseCost = priced > 0 ? priced : FallbackMarketPrice(sku);
        }
        else
        {
            baseCost = FallbackMarketPrice(sku);
        }

        float multiplier = _multiDealDiscountByKey.TryGetValue(DealKey(vendorId, sku.SkuId), out float pct)
            ? Mathf.Clamp01(1f - pct / 100f) : 1f;
        return Mathf.RoundToInt(baseCost * multiplier);
    }

    private static int FallbackMarketPrice(SkuData sku)
    {
        var market = Market();
        return market != null ? market.CurrentPrice(sku) : Mathf.RoundToInt(sku.BuyValue);
    }

    private static string DealKey(string vendorId, string skuId) => vendorId + "::" + skuId;

    private static VendorDealService Deals()
        => ServiceLocator.TryGet<VendorDealService>(out var d) ? d : null;

    // ── Truck fill-bar ───────────────────────────────────────────────────────

    internal const float TruckFillBarWidth = 140f;
    /// <summary>Width:height of TruckFillSprite.png (1408x768).</summary>
    internal const float TruckSpriteAspect = 1408f / 768f;
    /// <summary>Height of the truck sprite at TruckFillBarWidth — the DISPATCH ORDER/DEALS column next
    /// to it is sized to exactly match this, split evenly between the two buttons with a small gap.</summary>
    private const float TruckActionsColumnHeight = TruckFillBarWidth / TruckSpriteAspect;
    private const float TruckActionButtonGap = 6f;
    private const float TruckActionButtonHeight = (TruckActionsColumnHeight - TruckActionButtonGap) / 2f;
    // Trailer BOX sub-rectangle as a fraction of the whole sprite — measured against the source PNG.
    // Unlike the previous sprite (cab on the right), this artwork has the cab on the LEFT, so the box's
    // LEFT edge is the nose (nearest the cab) and its RIGHT edge is the rear.
    internal const float TruckBoxLeftFrac = 0.39f;     // the NOSE — front of the trailer, nearest the cab
    internal const float TruckBoxRightFrac = 0.92f;    // the rear of the trailer
    internal const float TruckBoxTopFrac = 0.32f;
    internal const float TruckBoxBottomFrac = 0.56f;

    internal static Texture2D _truckFillSprite;
    internal static Texture2D TruckFillSprite()
    {
        if (_truckFillSprite == null) _truckFillSprite = Resources.Load<Texture2D>("UI/TruckFillSprite");
        return _truckFillSprite;
    }

    /// <summary>Sprite background plus a red fill rect clipped to just the trailer box, anchored at the
    /// box's LEFT edge (the nose, nearest the cab) and growing RIGHT as `fillElement.style.width`
    /// increases — fills nose-to-rear as specced, not rear-to-nose. Caller owns repainting
    /// `fillElement.style.width` (see BuildMultiVendorGroup's RefreshHeader) since the fill level
    /// changes independently of rebuilding this whole element.</summary>
    internal static VisualElement BuildTruckFillBar(out VisualElement fillElement)
    {
        float h = TruckFillBarWidth / TruckSpriteAspect;

        var container = new VisualElement();
        container.style.width = TruckFillBarWidth;
        container.style.height = h;
        container.style.flexShrink = 0;
        container.style.marginLeft = 10;
        container.style.marginRight = 4;
        container.style.position = Position.Relative;
        // Explicit, not just "unstyled default" — a VisualElement's background is transparent unless
        // something says otherwise, but this one was showing up with an opaque light backing behind
        // the sprite's own transparent PNG regardless, so force it rather than rely on nothing being
        // set anywhere in the cascade.
        container.style.backgroundColor = new StyleColor(Color.clear);

        var image = new VisualElement();
        image.style.position = Position.Absolute;
        image.style.left = 0; image.style.top = 0; image.style.right = 0; image.style.bottom = 0;
        image.style.backgroundColor = new StyleColor(Color.clear);
        var sprite = TruckFillSprite();
        if (sprite != null)
        {
            image.style.backgroundImage = new StyleBackground(sprite);
            image.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }
        container.Add(image);

        var fill = new VisualElement();
        fill.style.position = Position.Absolute;
        fill.style.top = TruckBoxTopFrac * h;
        fill.style.height = (TruckBoxBottomFrac - TruckBoxTopFrac) * h;
        fill.style.left = TruckBoxLeftFrac * TruckFillBarWidth;
        fill.style.width = 0f; // painted by the caller's RefreshHeader on the very next line
        fill.style.backgroundColor = new StyleColor(new Color(0xE2 / 255f, 0x4B / 255f, 0x4A / 255f, 0.85f));
        container.Add(fill);

        fillElement = fill;
        return container;
    }

    // ── Multi-vendor DEALS button ────────────────────────────────────────────

    /// <summary>The countdown/drain fill-bar this vendor group's DEALS button uses — the same visual
    /// the VENDORS tab's row used to carry (icon-red track, draining red fill, centred label), moved
    /// here per Tad's explicit request since deals belong with the tab that builds and dispatches
    /// loads. Caller (BuildMultiVendorGroup) owns repainting the fill width/label/opacity every tick
    /// via RefreshDealButton — this only builds the static shell.</summary>
    private VisualElement BuildMultiVendorDealBar(out VisualElement fill, out Label label)
    {
        var root = new VisualElement();
        root.style.flexShrink = 0;
        root.style.backgroundColor = new StyleColor(new Color(0x22 / 255f, 0x2A / 255f, 0x33 / 255f, 1f));
        root.style.borderTopLeftRadius = root.style.borderTopRightRadius =
            root.style.borderBottomLeftRadius = root.style.borderBottomRightRadius = 6;
        root.style.borderTopWidth = root.style.borderBottomWidth =
            root.style.borderLeftWidth = root.style.borderRightWidth = 2;
        root.style.borderTopColor = root.style.borderBottomColor =
            root.style.borderLeftColor = root.style.borderRightColor = new StyleColor(ColDealRedEdge);
        root.style.overflow = Overflow.Hidden;
        root.style.position = Position.Relative;
        root.pickingMode = PickingMode.Position;

        var fillEl = new VisualElement();
        fillEl.style.position = Position.Absolute;
        fillEl.style.left = 0; fillEl.style.top = 0; fillEl.style.bottom = 0;
        fillEl.style.width = new Length(0f, LengthUnit.Percent);
        fillEl.style.backgroundColor = new StyleColor(ColDanger);
        fillEl.style.display = DisplayStyle.None;
        root.Add(fillEl);
        fill = fillEl;

        var labelEl = new Label("NO DEALS");
        labelEl.style.position = Position.Absolute;
        labelEl.style.left = 0; labelEl.style.right = 0; labelEl.style.top = 0; labelEl.style.bottom = 0;
        labelEl.style.unityTextAlign = TextAnchor.MiddleCenter;
        labelEl.style.color = new StyleColor(Color.white);
        ApplyFont(labelEl, bold: true, size: 15);
        root.Add(labelEl);
        label = labelEl;

        root.RegisterCallback<MouseEnterEvent>(_ => root.style.backgroundColor =
            new StyleColor(ColDealRedHover * 0.25f + new Color(0x22 / 255f, 0x2A / 255f, 0x33 / 255f, 0.75f)));
        root.RegisterCallback<MouseLeaveEvent>(_ => root.style.backgroundColor =
            new StyleColor(new Color(0x22 / 255f, 0x2A / 255f, 0x33 / 255f, 1f)));

        return root;
    }

    /// <summary>Same VendorDealService deal that drives this vendor group's own fill-bar DEALS
    /// button, but claiming it here
    /// adds the cases straight into THIS vendor's `_multiBaskets` entry (with its own per-vendor discount
    /// tracked in `_multiDealDiscountByKey`) instead of the old tab's single `_basket` — the two tabs'
    /// baskets are independent, so a deal claimed here must land in the basket the player is actually
    /// looking at. Reuses this panel's existing ShowConfirm rather than VendorsTabView's dedicated
    /// pop/bounce modal, since that modal lives inside VendorsTabView's own (frequently hidden) content
    /// tree and isn't visible while this tab is the active one.</summary>
    private void OnMultiVendorDealClicked(VendorData vendor)
    {
        string vendorId = vendor.VendorId;
        var deal = Deals()?.GetActiveDeal(vendorId);
        if (deal == null) return; // expired between click and handler, or button was stale

        var sku = Inventory()?.GetSkuData(deal.SkuId);
        if (sku == null) return;

        int cases = Mathf.Max(1, deal.Pallets) * Mathf.Max(1, sku.Ti * sku.Hi);
        int newQty = MultiQty(vendorId, sku.SkuId) + cases;

        if (TrailerCapacity.WouldOverflow(MultiBasketLines(vendorId), sku, newQty, out _))
        {
            ShowNotice($"This load doesn't have room for the deal — {deal.Pallets} pallet(s) of " +
                       $"{sku.ItemDescription}.\n\nDispatch this vendor's order first, or start a new " +
                       $"one, then come back for the deal.");
            return;
        }

        ShowConfirm($"Deal at {vendor.DisplayName}: -{deal.DiscountPercent:0}% off {sku.ItemDescription}." +
                    $"\n\n{deal.Pallets} pallet(s) · {cases} case(s). Add it to this vendor's order?",
                    () =>
                    {
                        MultiSetQty(vendorId, sku.SkuId, newQty);
                        _multiDealDiscountByKey[DealKey(vendorId, sku.SkuId)] = deal.DiscountPercent;
                        Deals()?.ClaimDeal(vendorId);
                        UIToast.Show($"Deal added to {vendor.DisplayName}'s order.");
                        Rebuild();
                    });
    }

    // ── Dispatch ─────────────────────────────────────────────────────────────

    private void DispatchVendorOrder(VendorData vendor)
    {
        string vendorId = vendor.VendorId;
        if (!_multiBaskets.TryGetValue(vendorId, out var basket) || basket.Count == 0)
        {
            UIToast.Show("Nothing on this order yet — set a quantity on at least one item.");
            return;
        }

        var plan = TrailerCapacity.Plan(MultiBasketLines(vendorId));
        if (plan.OverCapacity)
        {
            ShowNotice($"This load is over capacity — {plan.FloorSlotsUsed} floor positions needed, " +
                       $"{TrailerCapacity.FloorSlots} available.\n\nEither remove pallets, or dispatch " +
                       $"this order and then start a new one for the rest.");
            return;
        }

        int cases = basket.Values.Sum();
        if (cases < vendor.MinimumOrderCases)
        {
            ShowNotice($"{vendor.DisplayName} won't take an order this small.\n\n" +
                       $"Their minimum is {vendor.MinimumOrderCases:N0} cases and this order is " +
                       $"{cases:N0}.\n\nAdd {vendor.MinimumOrderCases - cases:N0} more, or dispatch a " +
                       $"different vendor's order instead.");
            return;
        }

        float cost = MultiBasketLines(vendorId).Sum(l => l.cases * UnitPriceForVendor(vendorId, l.sku));
        ShowConfirm($"Dispatch an order to {vendor.DisplayName}?\n\n" +
                    $"{basket.Count} line(s) · {cases:N0} case(s) · {plan.Pallets.Count} pallet(s) · " +
                    $"{Money(cost)}\n\nIt will wait in the Scheduler's unscheduled pool until you give " +
                    $"it a door and time.",
                    () => CommitDispatchVendorOrder(vendor));
    }

    private void CommitDispatchVendorOrder(VendorData vendor)
    {
        string vendorId = vendor.VendorId;
        var shipments = Shipments();
        if (shipments == null)
        {
            UIToast.Show("Purchasing is unavailable — the shipment service isn't running.");
            return;
        }

        var plan = TrailerCapacity.Plan(MultiBasketLines(vendorId));
        var items = new List<ShipmentLineItem>();
        foreach (var pallet in plan.Pallets)
        {
            var sku = FindSku(pallet.SkuId);
            if (sku == null) continue;
            items.Add(new ShipmentLineItem(pallet.SkuId, pallet.Cases,
                                           UnitPriceForVendor(vendorId, sku), sku.ShelfLifeDays)
            {
                FloorSlotIndex = pallet.FloorSlot,
                PalletTier = pallet.Tier
            });
        }

        var po = shipments.CreatePlayerPurchaseOrder(PONumberGenerator.GetRandomPONumber(),
                                                     vendorId, vendor.DisplayName, items, Today());
        if (po == null)
        {
            UIToast.Show("Couldn't raise that PO — nothing on it resolved to a real SKU.");
            return;
        }

        UIToast.Show($"PO {po.PONumber} raised with {vendor.DisplayName} — {po.TotalUnits:N0} case(s), " +
                     $"{Money(po.TotalCost)}. Book it a door on the Scheduler.");

        ServiceLocator.TryGet<VendorPerformanceTracker>(out var perf);
        perf?.RecordTransaction(vendorId, po.TotalCost, Today());

        _multiBaskets.Remove(vendorId);
        string prefix = vendorId + "::";
        foreach (var key in _multiDealDiscountByKey.Keys.Where(k => k.StartsWith(prefix)).ToList())
            _multiDealDiscountByKey.Remove(key);

        _tab = Tab.PoList;
        Rebuild();
    }
}
