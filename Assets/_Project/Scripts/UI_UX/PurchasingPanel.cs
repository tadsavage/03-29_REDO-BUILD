using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// PURCHASING — where raw stock comes into the building. Play-bar key 9.
///
/// The inbound counterpart to the Contracts panel: that one is demand the player accepts, this is
/// supply the player commits to. Three tabs, mirroring the life of a purchase order:
///
///   INBOUND ORDER CREATION  build a basket against the SKU database, pick a delivery day, raise it.
///   PO LIST                 orders raised and not yet finished — what's coming and when.
///   ARCHIVED POS            finished orders, kept as a record of what's been bought.
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

    /// <summary>Line-cost plate colours, lifted from the mock: a near-black plate with a muted caption
    /// over a warm tan figure. Its own palette on purpose — it's the one number that changes as you
    /// press the steppers, and it has to pop off a card that's already blue-on-navy.</summary>
    private static readonly Color ColPlateBg      = new Color(0.04f, 0.05f, 0.07f, 1f);
    private static readonly Color ColPlateCaption = new Color(0x9A / 255f, 0xA6 / 255f, 0xB2 / 255f, 1f);
    private static readonly Color ColPlateValue   = new Color(0xF0 / 255f, 0xC2 / 255f, 0x7A / 255f, 1f);

    private const float ModalWidth  = 1180f;
    private const float ModalHeight = 760f;
    /// <summary>Narrowest the window can be dragged before the two item columns stop being readable.</summary>
    private const float ModalMinWidth = 900f;

    /// <summary>Both item columns are BLUE. The mock had one column green and one blue, which reads as
    /// two different KINDS of supplier — they aren't, they're just two halves of one list.</summary>
    private const float ItemColGap = 14f;

    /// <summary>Compact catalogue sizing keeps the ordering surface dominant: purchase controls are
    /// a short dashboard above, while enough item cards remain visible to compare several SKUs without
    /// immediately scrolling.</summary>
    private const float IconSize = 84f;
    private const int ItemNameFontSize = 22;

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


    private enum Tab { Create, PoList, Archived }
    private Tab _tab = Tab.Create;

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

    /// <summary>The basket: SKU id → cases ordered. Only non-zero entries live here, so "is anything
    /// on this order" is a Count check rather than a scan of every SKU in the database.</summary>
    private readonly Dictionary<string, int> _basket = new();

    /// <summary>The number shown at the top of the create tab. Reserved when the tab is opened rather
    /// than when the order is submitted, because the player reads it off the screen while filling the
    /// order in — it has to be the number they actually get.</summary>
    /// <summary>Which supplier the Create tab is buying from. Persisted only for the life of the
    /// panel — SelectedVendor() re-anchors it to the first unlocked house whenever it stops being a
    /// valid choice.</summary>
    private string _vendorId;

    private string _poNumber;


    private Label _orderTotalLabel;
    private static Font _lilita;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public PurchasingPanel(VisualElement root)
    {
        _overlay = Build(out _modal, out _tabBar, out _tabHeader, out _content, out _footerMessage);
        root.Add(_overlay);
        _overlay.Add(BuildConfirmDialog());
        Hide();
    }

    public bool IsVisible => _visible;
    public bool IsOpen => _visible;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        _overlay.style.display = DisplayStyle.Flex;
        // Order screens sit above both bars — same reasoning as ContractsPanel.Show. This overlay was
        // already full-screen (bottom = 0); the raise is what guarantees it beats the top bar and any
        // panel opened before it, and KeepOnTop hands the top slot back to a visible toast.
        _overlay.BringToFront();
        UIToast.KeepOnTop();
        // A fresh number per opening. Nothing is spent by reserving one, and the alternative — one
        // number reused until an order is finally raised — means the number on screen changes meaning
        // depending on how many times you opened and abandoned the panel.
        if (_poNumber == null) NewPoNumber();
        Rebuild();
        CentreOnce();
        _resizeWindow?.ResetToNormal();
    }

    public void Hide()
    {
        _visible = false;
        HideConfirm();
        _overlay.style.display = DisplayStyle.None;
    }

    public void Dispose()
    {
        if (_overlay.parent != null) _overlay.RemoveFromHierarchy();
    }

    private void NewPoNumber() => _poNumber = PONumberGenerator.GetRandomPONumber();

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

        _titleLabel = MakeText("PURCHASING", 26, ColTitleText, bold: true);
        _titleLabel.style.flexGrow = 1;
        _titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(_titleLabel);

        // Resize + close, the same pair ContractsPanel carries — square, blue-edged, flush together.
        // Deliberately NOT the orange treatment: orange is this panel's "commit" colour (Create PO),
        // and a window chrome button that looks like a submit button is a trap.
        const float titleBtnSize = 48f;

        _scaleBtn = new Button { tooltip = "Resize window (normal / large / fill screen)" };
        StyleSquareButton(_scaleBtn);
        _scaleBtn.style.width = titleBtnSize;
        _scaleBtn.style.height = titleBtnSize;
        _scaleBtn.style.marginRight = 6;
        _scaleBtn.style.flexShrink = 0;
        ResizableWindow.AddStackedSquaresGlyph(_scaleBtn, titleBtnSize, ColTitleText, isFilled: false);
        _scaleBtn.RegisterCallback<PointerEnterEvent>(_ =>
            _scaleBtn.style.backgroundColor = new StyleColor(new Color(0.35f, 0.55f, 0.95f, 0.35f)));
        _scaleBtn.RegisterCallback<PointerLeaveEvent>(_ =>
            _scaleBtn.style.backgroundColor = new StyleColor(new Color(0.16f, 0.22f, 0.29f, 1f)));
        titleBar.Add(_scaleBtn);

        var close = new Button(Hide) { text = "✕" };
        StyleSquareButton(close);
        close.style.width = titleBtnSize;
        close.style.height = titleBtnSize;
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
        titleBar.Add(close);
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
            _resizeWindow.UpdateScaleButtonIcon(_scaleBtn, titleBtnSize, ColTitleText);
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
        int archived = shipments?.ArchivedShipments.Count ?? 0;

        _tabBar.Add(MakeTab("Inbound Order Creation", Tab.Create, _basket.Count));
        _tabBar.Add(MakeTab("PO List", Tab.PoList, live));
        _tabBar.Add(MakeTab("Archived POs", Tab.Archived, archived));

        switch (_tab)
        {
            case Tab.Create: BuildCreateTab(); break;
            case Tab.PoList: BuildShipmentList(shipments?.PendingShipments, live: true); break;
            default: BuildShipmentList(shipments?.ArchivedShipments, live: false); break;
        }
    }

    private static string TitleFor(Tab tab) => tab switch
    {
        Tab.Create => "INBOUND INVENTORY ORDERING",
        Tab.PoList => "PURCHASE ORDERS — IN FLIGHT",
        _ => "PURCHASE ORDERS — ARCHIVE"
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

    // ── Tab 1: Inbound Order Creation ────────────────────────────────────────

    private void BuildCreateTab()
    {
        // The whole-order controls live in the STATIONARY header, not the scroll view: they apply to
        // every line, and scrolling a 31-item catalogue away from the number you're ordering against
        // (or the total you're watching) is exactly when you want them.
        //
        // NO DELIVERY-DAY PICKER. Every PO is scheduled by hand on the Scheduler after it's raised —
        // it lands in the unscheduled pool and the player drops it on the day, block and door they
        // want. A dropdown here was a second, weaker way to say the same thing, and the two could
        // disagree: dispatch reads the APPOINTMENT when one exists, so a PO "ordered for Day 5" and
        // placed on Day 3 arrived on Day 3 and the dropdown was simply a lie about it.
        //
        // What's left is identity — the PO number — plus a line telling the player where the timing
        // decision actually gets made.
        var idRow = new VisualElement();
        idRow.style.flexDirection = FlexDirection.Row;
        idRow.style.alignItems = Align.Center;
        idRow.style.justifyContent = Justify.SpaceBetween;
        idRow.style.marginBottom = 5;

        var hint = MakeText("Raise the PO, then assign its door and time on the Scheduler.",
                             13, ColSubtleText);
                            // removed duplicate compact-header font setting
        hint.style.whiteSpace = WhiteSpace.NoWrap;
        idRow.Add(hint);

        // Button and PO pill share ONE right-hand group rather than being two more children of the
        // row. idRow is SpaceBetween, which spreads its children across the full width — a third
        // child would have been stranded in the middle of the row instead of sitting beside the pill.
        var idRight = new VisualElement();
        idRight.style.flexDirection = FlexDirection.Row;
        // Stretch, so the button takes its height from the PO pill beside it instead of carrying a
        // number of its own. The pill is content-sized — padding plus a 19pt Lilita label — so any
        // hardcoded height here would be a guess that quietly stopped matching the moment that font
        // size changed.
        idRight.style.alignItems = Align.Stretch;
        idRight.style.flexShrink = 0;
        idRow.Add(idRight);

        // The return leg of the trip the hint to the left describes. Scheduling is where a raised PO
        // actually becomes a delivery, so "go there now" belongs next to the sentence telling the
        // player that's where the decision gets made — and the Scheduler already has a matching
        // "Back to Purchasing" button, so this closes the loop in both directions.
        var toScheduler = new Button(() => OpenScheduler(null)) { text = "Back to Scheduler" };
        StyleOrangeButton(toScheduler);
        // Clears the fixed 30px StyleOrangeButton applies — an explicit height beats align-stretch,
        // so without this the row would stretch around a button that refused to grow.
        toScheduler.style.height = StyleKeyword.Auto;
        toScheduler.style.flexShrink = 0;
        toScheduler.style.marginRight = 10;
        toScheduler.style.paddingLeft = 14; toScheduler.style.paddingRight = 14;
        toScheduler.tooltip = "Close purchasing and open the Scheduler, where POs are given a day, " +
                              "time block and door.";
        idRight.Add(toScheduler);

        var poPill = new VisualElement();
        poPill.style.flexShrink = 0;
        poPill.style.paddingLeft = 14; poPill.style.paddingRight = 14;
        poPill.style.paddingTop = 3; poPill.style.paddingBottom = 3;
        // Orange scheme, matching every other orange callout on this panel (spot deals card, active
        // tab) — was a green-bordered ColStat pill; the LOAD COST readout above now owns green.
        poPill.style.backgroundColor = new StyleColor(new Color(ColOrange.r, ColOrange.g, ColOrange.b, 0.22f));
        poPill.style.borderTopWidth = poPill.style.borderBottomWidth =
            poPill.style.borderLeftWidth = poPill.style.borderRightWidth = 2;
        poPill.style.borderTopColor = poPill.style.borderBottomColor =
            poPill.style.borderLeftColor = poPill.style.borderRightColor = new StyleColor(ColOrangeEdge);
        poPill.style.borderTopLeftRadius = poPill.style.borderTopRightRadius =
            poPill.style.borderBottomLeftRadius = poPill.style.borderBottomRightRadius = 8;
        var poLabel = MakeText($"PO #: {_poNumber}", 19, ColOrangeText, bold: true);
        poLabel.style.marginTop = 0; poLabel.style.marginBottom = 0;
        poLabel.style.whiteSpace = WhiteSpace.NoWrap;
        poPill.Add(poLabel);
        idRight.Add(poPill);

        _tabHeader.Add(idRow);
        // Supplier first: it decides what the catalogue below even contains, so it has to be read
        // before the items, not after them.
        _tabHeader.Add(BuildVendorBar());
        _tabHeader.Add(BuildCapacityMeter());

        // ── Two blue columns of item cards ──
        var skus = OrderableSkus();
        if (skus.Count == 0)
        {
            var none = MakeText("No SKUs are available to order. Check that the SKU database is loaded.",
                                16, ColEmptyText);
            none.style.unityTextAlign = TextAnchor.MiddleCenter;
            none.style.marginTop = 30;
            _content.Add(none);

            // Footer STILL gets built. Returning early here left the tab with no Cancel button and no
            // way out but the ✕ — an empty catalogue is exactly the broken-looking state where the
            // player most needs the normal controls to still be there.
            BuildCreateFooter();
            RefreshOrderTotal();
            return;
        }

        // Above the catalogue: the broker's load if there is one, then today's expiring offers.
        // Broker first — it appears rarely and costs four figures, so it outranks the spot board.
        _content.Add(BuildSalvageStrip());
        _content.Add(BuildSpotDealsStrip());

        var columns = new VisualElement();
        columns.style.flexDirection = FlexDirection.Row;
        columns.style.alignItems = Align.FlexStart;

        var leftCol = MakeItemColumn(ItemColGap);
        var rightCol = MakeItemColumn(0f);
        columns.Add(leftCol);
        columns.Add(rightCol);
        _content.Add(columns);

        // Split down the middle rather than alternating left/right, so the list reads top-to-bottom
        // in each column like a page rather than zig-zagging across the gap.
        int half = Mathf.CeilToInt(skus.Count / 2f);
        for (int i = 0; i < skus.Count; i++)
            (i < half ? leftCol : rightCol).Add(BuildItemCard(skus[i], i));

        BuildCreateFooter();
        RefreshOrderTotal();
    }

    private VisualElement MakeItemColumn(float marginRight)
    {
        var column = new VisualElement();
        column.style.flexGrow = 1;
        column.style.flexBasis = 0;
        column.style.marginRight = marginRight;
        column.style.paddingTop = 10; column.style.paddingBottom = 10;
        column.style.paddingLeft = 10; column.style.paddingRight = 10;
        // BOTH columns blue. The mock had one green — that reads as two different kinds of supplier,
        // and they're just two halves of one catalogue.
        column.style.backgroundColor = new StyleColor(new Color(ColBorder.r, ColBorder.g, ColBorder.b, 0.06f));
        column.style.borderTopWidth = column.style.borderBottomWidth =
            column.style.borderLeftWidth = column.style.borderRightWidth = 2;
        column.style.borderTopColor = column.style.borderBottomColor =
            column.style.borderLeftColor = column.style.borderRightColor = new StyleColor(ColBorder);
        column.style.borderTopLeftRadius = column.style.borderTopRightRadius =
            column.style.borderBottomLeftRadius = column.style.borderBottomRightRadius = 10;
        return column;
    }

    /// <summary>
    /// One orderable SKU: icon, name, unit cost, the quantity stepper, case size, and the line total.
    ///
    /// The stepper sits LEFT-ALIGNED directly under the cost line, in the space the mock wasted on an
    /// empty framed box. Everything the player reads about this item — name, cost, the number they're
    /// changing — now sits on one left edge instead of the eye crossing the card to find the control.
    /// </summary>
    private VisualElement BuildItemCard(SkuData sku, int rowIndex)
    {
        string skuId = sku.SkuId;

        var card = new VisualElement();
        card.style.flexDirection = FlexDirection.Row;
        card.style.alignItems = Align.FlexStart;
        card.style.paddingTop = 8; card.style.paddingBottom = 8;
        card.style.paddingLeft = 10; card.style.paddingRight = 10;
        card.style.marginBottom = 6;
        card.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColCardEven : ColCardOdd);
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;
        card.style.borderLeftWidth = 3;
        card.style.borderLeftColor = new StyleColor(ColBlueEdge);

        // Icon
        var icon = new VisualElement();
        icon.style.width = IconSize; icon.style.height = IconSize;
        icon.style.flexShrink = 0;
        icon.style.marginRight = 10;
        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius =
            icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = 6;
        if (sku.Icon != null) icon.style.backgroundImage = new StyleBackground(sku.Icon);
        else icon.style.backgroundColor = new StyleColor(ColBlueEdge);
        card.Add(icon);

        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;

        var name = MakeText(sku.ItemDescription, ItemNameFontSize, ColTitleText, bold: true);
        name.style.whiteSpace = WhiteSpace.Normal;
        body.Add(name);

        // Price line: today's market price, how it sits against this SKU's normal, and the week
        // behind it. The sparkline is the whole reason the price moving is a MECHANIC rather than
        // noise — without a week of context, a number that changes every morning is just a number
        // that changes every morning.
        var priceRow = new VisualElement();
        priceRow.style.flexDirection = FlexDirection.Row;
        priceRow.style.alignItems = Align.Center;
        priceRow.style.marginTop = 1;
        // WRAPS, and every child refuses to shrink. The card body is what's left after a 116px icon
        // and a 150px cost plate, which at two columns is under 250px — narrower than cost + chip +
        // sparkline laid out in a line. Without this the chip renders as "NORM" and "10% O" and the
        // sparkline is clipped away entirely. Wrapping drops them to a second line instead, and keeps
        // doing the right thing as the window is dragged narrower.
        priceRow.style.flexWrap = Wrap.Wrap;

        var cost = MakeText($"Item Cost: {Money(UnitPrice(sku))}/case", 15, ColChipOutText, bold: true);
        cost.style.marginTop = 0; cost.style.marginBottom = 0;
        cost.style.flexShrink = 0;
        cost.style.whiteSpace = WhiteSpace.NoWrap;
        priceRow.Add(cost);

        priceRow.Add(MakeTrendChip(sku));
        priceRow.Add(MakeSparkline(sku));
        body.Add(priceRow);

        // ── Quantity stepper — top-right corner of the card frame, stacked above the Line Cost
        // plate (see the rightCol built below). Used to sit left-justified under the cost line in
        // the card body; moved per repeated request to the upper-right corner instead.
        var qtyRow = new VisualElement();
        qtyRow.style.flexDirection = FlexDirection.Row;
        qtyRow.style.alignItems = Align.Center;

        var minus = new Button { text = "–" };
        StyleStepButton(minus, ColDanger);
        qtyRow.Add(minus);

        // A typeable field, not a label: the player asked to be able to click in and type a quantity,
        // and typing 240 is a great deal faster than pressing + 240 times.
        var field = new TextField { value = Qty(skuId).ToString(), isDelayed = true };
        field.style.width = QtyFieldWidth;
        field.style.marginLeft = 6; field.style.marginRight = 6;
        // Larger numeral improves scanability. Paired with StyleQtyField's taller field/zeroed
        // padding below — bumping only the font size while the field stayed 30px tall clipped every
        // digit's top and bottom off, leaving what looked like two stray dashes instead of "0".
        ApplyFont(field, bold: true, size: 18);
        StyleQtyField(field);
        qtyRow.Add(field);

        var plus = new Button { text = "+" };
        StyleStepButton(plus, ColMoney);
        qtyRow.Add(plus);

        // Doubles as the per-line capacity readout once anything is ordered: how many pallets this
        // line becomes, and whether they stack. That's the information that explains the fill bar —
        // without it "why did adding 40 cases eat two slots?" has no answer on screen.
        var palletNote = MakeText($"Case Size: {TrailerCapacity.CasesPerPallet(sku)} cases/pallet",
                                  15, ColSubtleText);
        palletNote.style.marginTop = 3;
        body.Add(palletNote);

        card.Add(body);

        // ── Right-hand column: quantity stepper stacked ABOVE the Line Cost plate, both pinned to
        // the upper-right corner of the card frame. Card is Align.FlexStart, so this column starts
        // flush with the card's top edge instead of centering down the middle.
        var rightCol = new VisualElement();
        rightCol.style.flexShrink = 0;
        rightCol.style.alignItems = Align.FlexEnd;
        rightCol.style.marginLeft = 10;
        rightCol.Add(qtyRow);

        var plate = new VisualElement();
        plate.style.width = LineCostPlateWidth;
        plate.style.flexShrink = 0;
        plate.style.alignItems = Align.Center;
        plate.style.justifyContent = Justify.Center;
        plate.style.marginTop = 6;
        plate.style.paddingTop = 6; plate.style.paddingBottom = 6;
        plate.style.paddingLeft = 10; plate.style.paddingRight = 10;
        plate.style.backgroundColor = new StyleColor(ColPlateBg);
        plate.style.borderTopLeftRadius = plate.style.borderTopRightRadius =
            plate.style.borderBottomLeftRadius = plate.style.borderBottomRightRadius = 8;

        var plateCaption = MakeText("Line Cost:", 15, ColPlateCaption, bold: true);
        plateCaption.style.marginTop = 0; plateCaption.style.marginBottom = 0;
        plateCaption.style.whiteSpace = WhiteSpace.NoWrap;
        plate.Add(plateCaption);

        var lineCost = MakeText("$0", 26, ColPlateValue, bold: true);
        lineCost.style.marginTop = 0; lineCost.style.marginBottom = 0;
        lineCost.style.whiteSpace = WhiteSpace.NoWrap;
        plate.Add(lineCost);

        rightCol.Add(plate);
        card.Add(rightCol);

        // One updater shared by all three controls, so the field, the line cost and the order total
        // can never disagree about what this line holds.
        void Apply(int newQty)
        {
            // Refused if it won't fit the trailer; the field then snaps back to what's actually on
            // the order, so the number on screen is never a quantity the PO doesn't hold.
            TrySetQtyWithinCapacity(sku, newQty);

            int q = Qty(skuId);
            field.SetValueWithoutNotify(q.ToString());
            // Always shows a figure, "$0" included — a plate that empties itself makes the card jump
            // every time a line is cleared, and a zero line cost is a real answer.
            lineCost.text = Money(q * UnitPrice(sku));

            int pallets = TrailerCapacity.PalletsFor(sku, q);
            palletNote.text = pallets == 0
                ? $"Case Size: {TrailerCapacity.CasesPerPallet(sku)} cases/pallet"
                : $"{pallets} pallet(s) · {(sku.PltHeight > TrailerCapacity.StackableHeight ? "rides alone" : "stackable")}";

            RefreshOrderTotal();
        }

        // Step by a PALLET, not a case. The player is buying freight — a pallet is the unit that
        // arrives, occupies a rack slot and gets put away, and stepping one case at a time through a
        // 200-case order is not a decision, it's an errand. Typing still gives exact case counts.
        int step = Mathf.Max(1, sku.Ti * sku.Hi);
        minus.clicked += () => Apply(Qty(skuId) - step);
        plus.clicked += () => Apply(Qty(skuId) + step);
        field.RegisterValueChangedCallback(evt =>
        {
            // Anything unparseable reverts to what was there rather than silently zeroing the line —
            // a typo shouldn't quietly remove an item the player already added.
            Apply(int.TryParse(evt.newValue, out int typed) ? typed : Qty(skuId));
        });

        Apply(Qty(skuId)); // paint the initial state through the same path
        return card;
    }

    // ── Vendor roster ────────────────────────────────────────────────────────

    /// <summary>Sized so the whole roster fits on ONE row at the default modal width (7 x 148 + gaps
    /// is under the ~1150px of usable header). It still wraps when the window is dragged narrower —
    /// which is why the bar and its row both refuse to shrink; without that the wrapped second row
    /// drew straight over the trailer meter below it.</summary>
    private const float VendorChipMinWidth = 124f;

    /// <summary>
    /// The houses that will deal with you, and the ones that won't yet.
    ///
    /// LOCKED VENDORS ARE SHOWN, greyed, with what they'd cost you in reputation. A locked door you
    /// can see is a goal; a locked door you can't see is just a smaller game — and the whole point of
    /// tiering the roster is that the player knows there's something better to earn.
    /// </summary>
    private VisualElement BuildVendorBar()
    {
        var wrap = new VisualElement();
        wrap.style.marginBottom = 10;
        wrap.style.flexShrink = 0;

        var registry = VendorRegistry.Load();
        if (registry == null || registry.vendors.Count == 0) return wrap;

        int rep = Reputation();
        var unlocked = registry.Unlocked(rep);
        var locked = registry.Locked(rep);

        var head = new VisualElement();
        head.style.flexDirection = FlexDirection.Row;
        head.style.alignItems = Align.Center;
        head.style.justifyContent = Justify.SpaceBetween;
        head.style.marginBottom = 2;
        // Vendor chips identify themselves; the separate SUPPLIER / reputation heading duplicated
        // that information and consumed a full row above the catalogue.
        head.style.display = DisplayStyle.None;

        var title = MakeText("SUPPLIER", 16, ColTitleText, bold: true);
        title.style.whiteSpace = WhiteSpace.NoWrap;
        head.Add(title);

        // Reputation belongs HERE, next to the thing it gates, rather than on the TopBar with the
        // money. It's not a resource you spend — it's the reason this row looks the way it does.
        var band = ReputationService.BandFor(rep);
        int next = ReputationService.NextBandThreshold(rep);
        string repText = next < 0
            ? $"Reputation {rep} · {ReputationService.BandLabel(band)}"
            : $"Reputation {rep} · {ReputationService.BandLabel(band)} · {next - rep} to " +
              $"{ReputationService.BandLabel(ReputationService.BandFor(next))}";
        var repLabel = MakeText(repText, 14, ColSubtleText);
        repLabel.style.whiteSpace = WhiteSpace.NoWrap;
        head.Add(repLabel);
        wrap.Add(head);

        // Resolved ONCE here rather than per chip: SelectedVendor() rescans the registry, allocates
        // an Unlocked() list, and can re-anchor _vendorId as a side effect. Not something to run
        // fourteen times to draw seven boxes.
        var current = SelectedVendor();
        string currentId = current != null ? current.VendorId : null;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.flexShrink = 0;
        foreach (var v in unlocked) row.Add(BuildVendorChip(v, true, currentId));
        foreach (var v in locked) row.Add(BuildVendorChip(v, false, currentId));
        wrap.Add(row);

        return wrap;
    }

    private VisualElement BuildVendorChip(VendorData vendor, bool unlocked, string currentId)
    {
        bool selected = unlocked && vendor.VendorId == currentId;

        var chip = new VisualElement();
        chip.style.minWidth = VendorChipMinWidth;
        chip.style.flexGrow = 1;
        chip.style.flexBasis = 0;
        chip.style.marginRight = 4;
        chip.style.marginBottom = 3;
        chip.style.paddingTop = 3; chip.style.paddingBottom = 3;
        chip.style.paddingLeft = 6; chip.style.paddingRight = 6;
        chip.style.overflow = Overflow.Hidden;
        chip.style.backgroundColor = new StyleColor(
            !unlocked ? new Color(ColStat.r, ColStat.g, ColStat.b, 0.55f)
            : selected ? new Color(ColOrange.r, ColOrange.g, ColOrange.b, 0.30f)
                       : ColStat);
        chip.style.borderTopWidth = chip.style.borderBottomWidth =
            chip.style.borderLeftWidth = chip.style.borderRightWidth = 2;
        var edge = !unlocked ? ColEmptyText : selected ? ColOrange : ColBlueEdge;
        chip.style.borderTopColor = chip.style.borderBottomColor =
            chip.style.borderLeftColor = chip.style.borderRightColor = new StyleColor(edge);
        chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius =
            chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 7;

        var name = MakeText(vendor.DisplayName, 12,
                            unlocked ? (selected ? ColOrangeText : ColTitleText) : ColEmptyText, bold: true);
        name.style.marginTop = 0; name.style.marginBottom = 0;
        // WRAPS rather than clipping. At 148px a chip has ~130px of usable width and half the roster
        // is longer than that, so NoWrap turned "Fairweather Trading Co." into "Fairweather Tradi" —
        // and a supplier's name is its identity, the one thing on the chip that must not be guessed
        // at. The row stretches all chips to the tallest, so a two-line name costs alignment nothing.
        name.style.whiteSpace = WhiteSpace.Normal;
        chip.Add(name);

        if (!unlocked)
        {
            // Just the number. Naming the band this threshold sits in read as "Needs 250 rep (Known)"
            // to a player who was already Known — vendor thresholds are deliberately spaced BETWEEN
            // band boundaries so the roster opens as a ladder, not in three lumps.
            var need = MakeText($"Needs {vendor.ReputationRequired} rep · {vendor.ReputationRequired - Reputation()} to go",
                                12, ColEmptyText);
            need.style.marginTop = 1; need.style.marginBottom = 0;
            need.style.whiteSpace = WhiteSpace.NoWrap;
            chip.Add(need);
            return chip;   // no click handler: an unearned vendor isn't a control
        }

        // The three axes that actually differ, in one line: price against the market, how often they
        // short you, and the smallest order they'll take.
        float pm = vendor.PriceMultiplier;
        string priceTag = Mathf.Abs(pm - 1f) < 0.005f ? "market"
                        : pm < 1f ? $"{Mathf.RoundToInt((1f - pm) * 100f)}% under"
                                  : $"{Mathf.RoundToInt((pm - 1f) * 100f)}% over";
        Color priceCol = Mathf.Abs(pm - 1f) < 0.005f ? ColSubtleText : pm < 1f ? ColMoney : ColDangerSoft;

        var terms = MakeText($"{priceTag} · {vendor.ReliabilityPercent}% reliable" +
                             (vendor.MinimumOrderCases > 0 ? $" · min {vendor.MinimumOrderCases:N0}" : ""),
                             12, priceCol);
        terms.style.marginTop = 1; terms.style.marginBottom = 0;
        // Wraps for the same reason the name does — and this line matters more than it looks, because
        // the minimum-order clause is the one term that will REFUSE a PO. Clipped to
        // "market · 85% reliable · mi", it read as decoration right up until the order was rejected.
        terms.style.whiteSpace = WhiteSpace.Normal;
        chip.Add(terms);

        chip.RegisterCallback<ClickEvent>(_ => OnSelectVendor(vendor));
        return chip;
    }

    /// <summary>
    /// Switches supplier, and throws the basket away when it isn't empty.
    ///
    /// ONE PO IS ONE VENDOR — a purchase order is an agreement with a specific house at their prices,
    /// so a basket can't survive the switch. Carrying the lines over and silently repricing them
    /// would be worse than clearing: the player would be looking at quantities they chose against
    /// numbers that no longer applied.
    /// </summary>
    private void OnSelectVendor(VendorData vendor)
    {
        if (vendor == null || vendor.VendorId == _vendorId) return;

        bool hadBasket = _basket.Count > 0;
        _vendorId = vendor.VendorId;
        _basket.Clear();
        Rebuild();

        UIToast.Show(hadBasket
            ? $"Switched to {vendor.DisplayName} — the previous order was cleared, since a PO is with " +
              $"one supplier at their prices."
            : $"Buying from {vendor.DisplayName}.");
    }

    // ── Price trend widgets ──────────────────────────────────────────────────

    private const float SparkHeight = 20f;
    private const float SparkBarWidth = 5f;
    private const float SparkBarGap = 2f;

    /// <summary>How far off normal a price has to be before it's worth calling out. Under this it
    /// reads "NORMAL" — a chip that lit up over a 1% wobble would cry wolf every morning.</summary>
    private const float TrendDeadbandPercent = 3f;

    /// <summary>
    /// Today's price against this SKU's authored normal — NOT against yesterday.
    ///
    /// Yesterday is the wrong comparison to put on a buying decision: a price that fell 2% but is
    /// still 20% over normal is not a bargain, and a chip saying "down" would be telling the player
    /// to buy it. What matters is whether this is cheap for THIS ITEM.
    /// </summary>
    private VisualElement MakeTrendChip(SkuData sku)
    {
        var chip = new VisualElement();
        chip.style.flexShrink = 0;
        chip.style.marginLeft = 8;
        chip.style.paddingLeft = 6; chip.style.paddingRight = 6;
        chip.style.paddingTop = 1; chip.style.paddingBottom = 1;
        chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius =
            chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 4;

        var market = Market();
        if (market == null) return chip;   // no market running: make no claim about the price

        float vsNormal = market.VsNormalPercent(sku);
        bool cheap = vsNormal <= -TrendDeadbandPercent;
        bool dear = vsNormal >= TrendDeadbandPercent;

        string text = cheap ? $"{Mathf.RoundToInt(-vsNormal)}% UNDER"
                    : dear ? $"{Mathf.RoundToInt(vsNormal)}% OVER"
                           : "NORMAL";
        Color fg = cheap ? ColMoney : dear ? ColDangerSoft : ColSubtleText;

        chip.style.backgroundColor = new StyleColor(new Color(fg.r, fg.g, fg.b, 0.14f));

        var label = MakeText(text, 12, fg, bold: true);
        label.style.marginTop = 0; label.style.marginBottom = 0;
        label.style.flexShrink = 0;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        chip.Add(label);
        return chip;
    }

    /// <summary>
    /// Seven bars: this SKU's price for the last week, oldest on the left, today on the right.
    ///
    /// Scaled to its OWN min/max rather than to zero. A $20 item moving between $18 and $23 would be
    /// seven near-identical full-height bars on a zero baseline — technically honest and completely
    /// unreadable. Relative scaling is what makes the shape of the week visible at this size.
    /// </summary>
    private VisualElement MakeSparkline(SkuData sku)
    {
        var wrap = new VisualElement();
        wrap.style.flexDirection = FlexDirection.Row;
        wrap.style.alignItems = Align.FlexEnd;
        wrap.style.height = SparkHeight;
        wrap.style.marginLeft = 8;
        wrap.style.flexShrink = 0;

        var history = Market()?.History(sku.SkuId);
        if (history == null || history.Count < 2) return wrap;

        int min = history.Min();
        int max = history.Max();
        int range = Mathf.Max(1, max - min);

        for (int i = 0; i < history.Count; i++)
        {
            // A floor of 3px so the week's low is still a visible bar rather than a gap in the chart.
            float t = (history[i] - min) / (float)range;
            var bar = new VisualElement();
            bar.style.width = SparkBarWidth;
            bar.style.height = 3f + t * (SparkHeight - 3f);
            bar.style.marginRight = i == history.Count - 1 ? 0 : SparkBarGap;
            bar.style.backgroundColor = new StyleColor(
                i == history.Count - 1 ? ColPlateValue                                      // today
                                       : new Color(ColBorder.r, ColBorder.g, ColBorder.b, 0.55f));
            wrap.Add(bar);
        }

        return wrap;
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

    // ── Warehouse capacity ───────────────────────────────────────────────────

    /// <summary>Reserve rack slots free right now, and how many exist. Reads the same
    /// LocationStatusRegistry the reach truck obeys, NOT LocationData — the two can disagree, and the
    /// registry is the one that decides whether an arriving pallet actually has somewhere to go.</summary>
    private static (int free, int total) ReserveSlotAvailability()
    {
        int free = 0, total = 0;
        foreach (var slot in SlotRegistry.ReserveSlots)
        {
            total++;
            if (LocationStatusRegistry.IsAvailable(slot.Address)) free++;
        }
        return (free, total);
    }

    /// <summary>Cancel / order total / Create PO. In the STATIONARY header rather than the scroll
    /// view — the total is the number you watch while adding items, and a footer that scrolls away is
    /// a footer you can't watch.</summary>
    private void BuildCreateFooter()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 5;

        // CANCEL is RED and CREATE PO is GREEN — the two irreversible ends of this screen, coloured
        // for what they do rather than both wearing the panel's orange. Orange still means "an action
        // worth noticing" everywhere else; these two are the destination, so they get their own
        // vocabulary and a size to match. Deliberately the only two buttons on the panel this big.
        var cancel = new Button(OnCancelOrder) { text = "CANCEL" };
        StyleActionButton(cancel, ColCancelRed, ColCancelRedEdge, ColCancelRedHover);
        row.Add(cancel);

        // The total sits BETWEEN them and stretches to fill whatever's left, so the number the player
        // is watching is physically the largest thing on the row and the two buttons stay pinned to
        // the outer edges at fixed widths regardless of how wide the window is dragged.
        var totalBox = new VisualElement();
        totalBox.style.flexDirection = FlexDirection.Row;
        totalBox.style.alignItems = Align.Center;
        totalBox.style.justifyContent = Justify.Center;
        totalBox.style.flexGrow = 1;
        totalBox.style.height = ActionButtonHeight;
        totalBox.style.marginLeft = 8; totalBox.style.marginRight = 8;
        totalBox.style.paddingLeft = 12; totalBox.style.paddingRight = 12;
        totalBox.style.backgroundColor = new StyleColor(ColStat);
        totalBox.style.borderTopWidth = totalBox.style.borderBottomWidth =
            totalBox.style.borderLeftWidth = totalBox.style.borderRightWidth = 2;
        totalBox.style.borderTopColor = totalBox.style.borderBottomColor =
            totalBox.style.borderLeftColor = totalBox.style.borderRightColor = new StyleColor(ColBorder);
        totalBox.style.borderTopLeftRadius = totalBox.style.borderTopRightRadius =
            totalBox.style.borderBottomLeftRadius = totalBox.style.borderBottomRightRadius = 8;

        var totalCaption = MakeText("ORDER TOTAL:", 20, ColTitleText, bold: true);
        totalCaption.style.marginRight = 8;
        totalCaption.style.marginTop = 0; totalCaption.style.marginBottom = 0;
        totalCaption.style.whiteSpace = WhiteSpace.NoWrap;
        totalBox.Add(totalCaption);

        _orderTotalLabel = MakeText("$0", 22, ColMoney, bold: true);
        _orderTotalLabel.style.marginTop = 0; _orderTotalLabel.style.marginBottom = 0;
        _orderTotalLabel.style.whiteSpace = WhiteSpace.NoWrap;
        totalBox.Add(_orderTotalLabel);
        row.Add(totalBox);

        var create = new Button(OnCreatePoClicked) { text = "CREATE PO" };
        StyleActionButton(create, ColCreateGreen, ColCreateGreenEdge, ColCreateGreenHover);
        row.Add(create);

        _tabHeader.Add(row);
    }

    /// <summary>
    /// The trailer fill meter: how much of ONE trailer this order takes.
    ///
    /// Money answers "what does this cost"; this answers "does it physically go on the truck", which
    /// is the constraint the player is actually working against and had no way to see. Reads in
    /// 24ths (a short pallet is one, a tall one two) per the spec, with the floor-position count
    /// beside it because that's the number that decides when it's full.
    /// </summary>
    private VisualElement BuildCapacityMeter()
    {
        var wrap = new VisualElement();
        wrap.style.marginBottom = 10;

        var top = new VisualElement();
        top.style.flexDirection = FlexDirection.Row;
        top.style.alignItems = Align.Center;
        top.style.justifyContent = Justify.SpaceBetween;
        top.style.marginBottom = 4;

        var caption = MakeText("TRAILER LOAD", 13, ColTitleText, bold: true);
        caption.style.whiteSpace = WhiteSpace.NoWrap;
        top.Add(caption);

        _capacityLabel = MakeText(string.Empty, 13, ColSubtleText, bold: true);
        _capacityLabel.style.whiteSpace = WhiteSpace.NoWrap;
        top.Add(_capacityLabel);
        wrap.Add(top);

        var track = new VisualElement();
        track.style.height = 14;
        track.style.backgroundColor = new StyleColor(ColStat);
        track.style.borderTopWidth = track.style.borderBottomWidth =
            track.style.borderLeftWidth = track.style.borderRightWidth = 2;
        track.style.borderTopColor = track.style.borderBottomColor =
            track.style.borderLeftColor = track.style.borderRightColor = new StyleColor(ColBorder);
        track.style.borderTopLeftRadius = track.style.borderTopRightRadius =
            track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 6;
        track.style.overflow = Overflow.Hidden;

        _capacityFill = new VisualElement();
        _capacityFill.style.height = Length.Percent(100);
        _capacityFill.style.width = Length.Percent(0);
        _capacityFill.style.backgroundColor = new StyleColor(ColMoney);
        track.Add(_capacityFill);
        wrap.Add(track);

        // Running dollar tab for the load being built — sits with the trailer readouts (not just the
        // footer total) so the player sees what the load costs so far right alongside how full it is.
        var costRow = new VisualElement();
        // ORDER TOTAL below is the authoritative live cost. Hide this duplicate large readout so the
        // compact summary deck gives its height back to the item catalogue.
        costRow.style.display = DisplayStyle.None;
        costRow.style.flexDirection = FlexDirection.Row;
        costRow.style.alignItems = Align.Center;
        costRow.style.justifyContent = Justify.SpaceBetween;
        costRow.style.marginTop = 6;

        var costCaption = MakeText("LOAD COST", 14, ColSubtleText, bold: true);
        costCaption.style.whiteSpace = WhiteSpace.NoWrap;
        costRow.Add(costCaption);

        // Framed like the PO # pill above (green border + green-tinted fill) so the number the
        // player is tracking while building the load reads as its own callout, not just body text.
        var costPill = new VisualElement();
        costPill.style.flexShrink = 0;
        costPill.style.paddingLeft = 14; costPill.style.paddingRight = 14;
        costPill.style.paddingTop = 4; costPill.style.paddingBottom = 4;
        costPill.style.backgroundColor = new StyleColor(new Color(ColMoney.r, ColMoney.g, ColMoney.b, 0.18f));
        costPill.style.borderTopWidth = costPill.style.borderBottomWidth =
            costPill.style.borderLeftWidth = costPill.style.borderRightWidth = 2;
        costPill.style.borderTopColor = costPill.style.borderBottomColor =
            costPill.style.borderLeftColor = costPill.style.borderRightColor = new StyleColor(ColMoney);
        costPill.style.borderTopLeftRadius = costPill.style.borderTopRightRadius =
            costPill.style.borderBottomLeftRadius = costPill.style.borderBottomRightRadius = 8;

        _loadCostLabel = MakeText("$0", 42, ColMoney, bold: true); // 50% bigger than the prior 28px
        _loadCostLabel.style.marginTop = 0; _loadCostLabel.style.marginBottom = 0;
        _loadCostLabel.style.whiteSpace = WhiteSpace.NoWrap;
        costPill.Add(_loadCostLabel);
        costRow.Add(costPill);
        wrap.Add(costRow);

        // WILL IT FIT IN THE BUILDING, not just on the truck.
        //
        // The trailer meter above answers "does this go on one load"; this answers "is there anywhere
        // to put it when it lands". Rack space was always a binding constraint, but it bit hours
        // later — the reach truck failing to find a reserve slot, long after the decision that caused
        // it. A constraint the player can't feel while choosing isn't a constraint, it's a surprise.
        _warehouseFitLabel = MakeText(string.Empty, 14, ColSubtleText);
        _warehouseFitLabel.style.marginTop = 4;
        _warehouseFitLabel.style.whiteSpace = WhiteSpace.NoWrap;
        wrap.Add(_warehouseFitLabel);

        return wrap;
    }

    private Label _capacityLabel;
    private VisualElement _capacityFill;
    private Label _warehouseFitLabel;
    private Label _loadCostLabel;

    /// <summary>Fills in the "will it fit in the building" line under the trailer meter.</summary>
    private void RefreshWarehouseFit(TrailerLoadPlan plan)
    {
        if (_warehouseFitLabel == null) return;

        var (free, total) = ReserveSlotAvailability();

        // No racking placed yet is a normal early-game state, not an error — say nothing rather than
        // reporting "0 slots free", which reads as the warehouse being full.
        if (total == 0)
        {
            _warehouseFitLabel.text = string.Empty;
            return;
        }

        int pallets = plan.Pallets.Count;
        int overflow = Mathf.Max(0, pallets - free);

        _warehouseFitLabel.text = overflow > 0
            ? $"WAREHOUSE: {pallets} pallet(s) inbound · {free}/{total} reserve slots free · " +
              $"{overflow} with nowhere to go"
            : $"WAREHOUSE: {pallets} pallet(s) inbound · {free}/{total} reserve slots free";

        _warehouseFitLabel.style.color = new StyleColor(overflow > 0 ? ColDangerSoft : ColSubtleText);
    }

    private void RefreshOrderTotal()
    {
        if (_orderTotalLabel == null) return;
        float basketTotal = BasketTotal();
        _orderTotalLabel.text = Money(basketTotal);

        if (_loadCostLabel != null)
            _loadCostLabel.text = Money(basketTotal);

        var plan = CurrentPlan();

        if (_capacityFill != null)
        {
            _capacityFill.style.width = Length.Percent(plan.Fill01 * 100f);
            // Green while there's room, amber on the last slot or two, red once it won't fit. The
            // amber band matters more than it looks: at 11/12 floors the next tall pallet is refused,
            // and a bar that stayed green right up to the refusal would read as a bug.
            _capacityFill.style.backgroundColor = new StyleColor(
                plan.OverCapacity ? ColCancelRed
                : plan.FloorSlotsUsed >= TrailerCapacity.FloorSlots - 1 ? ColOrange
                : ColMoney);
        }

        if (_capacityLabel != null)
        {
            _capacityLabel.text =
                $"{plan.UnitsUsed}/{TrailerCapacity.MaxUnits} slots  ·  " +
                $"{plan.FloorSlotsUsed}/{TrailerCapacity.FloorSlots} floor positions" +
                (plan.TallCount > 0 ? $"  ·  {plan.TallCount} tall" : "");
            _capacityLabel.style.color = new StyleColor(plan.OverCapacity ? ColDangerSoft : ColSubtleText);
        }

        RefreshWarehouseFit(plan);

        _footerMessage.text = _basket.Count == 0
            ? $"{OrderableSkus().Count} item(s) available for ordering. One trailer holds " +
              $"{TrailerCapacity.FloorSlots} floor positions — pallets over " +
              $"{TrailerCapacity.StackableHeight:0.00}m ride alone, shorter ones stack two high."
            : $"{_basket.Count} line(s) · {_basket.Values.Sum():N0} case(s) · " +
              $"{plan.Pallets.Count} pallet(s) on PO {_poNumber}.";
    }

    private float BasketTotal()
    {
        float total = 0f;
        foreach (var kv in _basket)
        {
            var sku = FindSku(kv.Key);
            if (sku != null) total += kv.Value * UnitPrice(sku);
        }
        return total;
    }

    private int Qty(string skuId) => _basket.TryGetValue(skuId, out int q) ? q : 0;

    private void SetQty(string skuId, int qty)
    {
        // Clamped at zero and stored only when non-zero: the basket is "what's on the order", and a
        // zero line isn't on the order.
        qty = Mathf.Max(0, qty);
        if (qty == 0) _basket.Remove(skuId);
        else _basket[skuId] = qty;
    }

    /// <summary>The basket as the capacity planner wants it: real SkuData plus case counts. Skips any
    /// id that no longer resolves rather than planning around a null.</summary>
    private List<(SkuData sku, int cases)> BasketLines()
    {
        var list = new List<(SkuData, int)>();
        foreach (var kv in _basket)
        {
            var sku = FindSku(kv.Key);
            if (sku != null) list.Add((sku, kv.Value));
        }
        return list;
    }

    private TrailerLoadPlan CurrentPlan() => TrailerCapacity.Plan(BasketLines());

    /// <summary>
    /// Applies a new case count for one SKU unless it would push the load past one trailer.
    ///
    /// The increase is REFUSED rather than allowed-and-flagged. A trailer is one trailer: there is no
    /// such thing as a PO that's 130% loaded, so letting the basket go over and only complaining at
    /// Create PO would mean building an order that can't exist and finding out at the end. Blocking
    /// at the moment of the change is also what makes the message actionable — it names the two ways
    /// forward while the player still has the item in hand.
    ///
    /// Reductions always go through: you can never fix an overfull load if it won't let you take
    /// things off it.
    /// </summary>
    private bool TrySetQtyWithinCapacity(SkuData sku, int newQty)
    {
        if (sku == null) return false;

        if (newQty <= Qty(sku.SkuId))   // removing or unchanged — never blocked
        {
            SetQty(sku.SkuId, newQty);
            return true;
        }

        if (TrailerCapacity.WouldOverflow(BasketLines(), sku, newQty, out _))
        {
            ShowNotice("This load is over capacity.\n\nEither remove pallets, or create this PO and " +
                       "then start a new PO for the rest.");
            return false;
        }

        SetQty(sku.SkuId, newQty);
        return true;
    }

    /// <summary>Cancel: closes the panel and throws the in-progress basket away, per the brief. The PO
    /// number goes with it — that order never existed, and reusing its number on the next one would
    /// make the number meaningless as an identifier.</summary>
    private void OnCancelOrder()
    {
        _basket.Clear();
        NewPoNumber();
        Hide();
    }

    private void OnCreatePoClicked()
    {
        if (_basket.Count == 0)
        {
            UIToast.Show("Nothing on this order yet — set a quantity on at least one item.");
            return;
        }

        // Backstop. TrySetQtyWithinCapacity already refuses anything that would overflow, so this
        // shouldn't fire — but a PO that can't be loaded must never be creatable, and that guarantee
        // belongs at the point of creation rather than resting on every edit path having behaved.
        var plan = CurrentPlan();
        if (plan.OverCapacity)
        {
            ShowNotice($"This load is over capacity — {plan.FloorSlotsUsed} floor positions needed, " +
                       $"{TrailerCapacity.FloorSlots} available.\n\nEither remove pallets, or create " +
                       $"this PO and then start a new PO for the rest.");
            return;
        }

        // MINIMUM ORDER. The third thing that separates one house from another: a specialty vendor
        // won't break a load for you. Enforced at creation rather than by blocking the steppers,
        // because unlike trailer capacity a small basket isn't WRONG — it just isn't finished, and
        // refusing every keystroke on the way up to the minimum would be maddening.
        var vendorForMin = SelectedVendor();
        int cases = _basket.Values.Sum();
        if (vendorForMin != null && cases < vendorForMin.MinimumOrderCases)
        {
            ShowNotice($"{vendorForMin.DisplayName} won't take an order this small.\n\n" +
                       $"Their minimum is {vendorForMin.MinimumOrderCases:N0} cases and this order is " +
                       $"{cases:N0}.\n\nAdd {vendorForMin.MinimumOrderCases - cases:N0} more, or buy " +
                       $"from another supplier.");
            return;
        }

        var vendorName = vendorForMin != null ? vendorForMin.DisplayName : "Wholesale Supply";
        ShowConfirm($"Create PO number {_poNumber} with {vendorName}?\n\n" +
                    $"{_basket.Count} line(s) · {cases:N0} case(s) · " +
                    $"{plan.Pallets.Count} pallet(s) · " +
                    $"{Money(BasketTotal())}\n\nIt will wait in the Scheduler's unscheduled pool " +
                    $"until you give it a door and time.",
                    SubmitPurchaseOrder);
    }

    private void SubmitPurchaseOrder()
    {
        var shipments = Shipments();
        if (shipments == null)
        {
            UIToast.Show("Purchasing is unavailable — the shipment service isn't running.");
            return;
        }

        // ONE LINE ITEM PER PHYSICAL PALLET, each tagged with the floor slot and tier the capacity
        // planner assigned it.
        //
        // This is what makes the trailer show what was actually bought. TruckController.LoadShipment
        // has two branches: if every line carries FloorSlotIndex >= 0 it places each pallet exactly
        // there and supports double-stacking; otherwise it falls back to building a fixed 12 pallets
        // in a round-robin over the lines. Player POs used to hit that fallback — so a 4-case order
        // and a 4000-case order both arrived as 12 full pallets in a rotation that had nothing to do
        // with the order. Sending pre-planned pallets takes the good branch and the two agree.
        var plan = CurrentPlan();
        var items = new List<ShipmentLineItem>();
        foreach (var pallet in plan.Pallets)
        {
            var sku = FindSku(pallet.SkuId);
            if (sku == null) continue;
            items.Add(new ShipmentLineItem(pallet.SkuId, pallet.Cases,
                                           UnitPrice(sku), sku.ShelfLifeDays)
            {
                FloorSlotIndex = pallet.FloorSlot,
                PalletTier = pallet.Tier
            });
        }

        var vendor = SelectedVendor();
        var po = shipments.CreatePlayerPurchaseOrder(
            _poNumber,
            vendor != null ? vendor.VendorId : "PLAYER_SUPPLIER",
            vendor != null ? vendor.DisplayName : "Wholesale Supply",
            items, Today());
        if (po == null)
        {
            UIToast.Show("Couldn't raise that PO — nothing on it resolved to a real SKU.");
            return;
        }

        UIToast.Show($"PO {po.PONumber} raised — {po.TotalUnits:N0} case(s), ${po.TotalCost:N0}. " +
                     $"Book it a door on the Scheduler.");

        // Fresh order state, then straight to the list so the player sees what they just created.
        _basket.Clear();
        NewPoNumber();
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

        var status = MakeText($"[{shipment.Status}]", 13,
                              shipment.Status == ShipmentData.ShipmentStatus.Cancelled ? ColDangerSoft : ColChipOutText,
                              bold: true);
        status.style.whiteSpace = WhiteSpace.NoWrap;
        subRow.Add(status);

        var spacer = new VisualElement(); spacer.style.flexGrow = 1; header.Add(spacer);

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

            var itemNo = MakeText(g.SkuId, 13, ColChipOutText);
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
        var contracts = topBar != null ? topBar.ContractsPanel : null;
        if (contracts == null)
        {
            UIToast.Show("Couldn't open the scheduler — the Outbound Order Manager isn't loaded.");
            return;
        }

        // Registered on key 6; routing through the manager is what closes any other open panel first.
        UIKeyBindingManager.Instance?.CloseAll();
        contracts.ShowScheduleTab(shipment != null ? shipment.ArrivalDayNumber : 0);
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

    private static int Reputation()
        => ServiceLocator.TryGet<ReputationService>(out var r) && r != null ? r.Score : 0;

    /// <summary>The vendor currently being bought from, or null if the roster is missing entirely
    /// (in which case the panel falls back to the open market and behaves as it did before vendors).</summary>
    private VendorData SelectedVendor()
    {
        var registry = VendorRegistry.Load();
        if (registry == null) return null;

        var unlocked = registry.Unlocked(Reputation());
        if (unlocked.Count == 0) return null;

        var chosen = unlocked.FirstOrDefault(v => v.VendorId == _vendorId);

        // Re-anchors rather than showing an empty catalogue. The selected vendor can stop being a
        // valid choice between openings — reputation can fall out of their band, or the asset can be
        // edited — and a panel pointing at a vendor that no longer serves you looks broken.
        if (chosen == null)
        {
            chosen = unlocked[0];
            _vendorId = chosen.VendorId;
        }
        return chosen;
    }

    /// <summary>
    /// What this SKU costs per case TODAY, from the vendor currently selected.
    ///
    /// Every price on this panel goes through here — the card, the line cost, the order total and the
    /// line items the PO is actually built from — so the number the player reads and the number they
    /// are charged cannot disagree. Falls back to the bare market price, then to the SKU's authored
    /// BuyValue, so purchasing stays usable rather than free if either service is missing.
    /// </summary>
    private int UnitPrice(SkuData sku)
    {
        if (sku == null) return 0;

        var vendor = SelectedVendor();
        if (vendor != null)
        {
            int priced = vendor.PriceFor(sku, Market());
            if (priced > 0) return priced;
        }

        var market = Market();
        return market != null ? market.CurrentPrice(sku) : Mathf.RoundToInt(sku.BuyValue);
    }

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

    /// <summary>
    /// Every SKU the player can buy RIGHT NOW: one with a real cost and a committed Ti/Hi (a SKU with
    /// no pallet configuration can't be expressed as freight), and carried by the selected vendor.
    ///
    /// The vendor filter is the whole point of the roster — you don't shop a global catalogue and
    /// pick a supplier afterwards, you walk into a house and see what they keep. Falls back to the
    /// full catalogue when there's no roster at all, so a project without VendorRegistry.asset still
    /// has a working purchasing screen.
    /// </summary>
    private List<SkuData> OrderableSkus()
    {
        var inv = Inventory();
        if (inv == null) return new List<SkuData>();

        var all = inv.AllSkus.Where(s => s != null && s.BuyValue > 0f && s.Ti > 0 && s.Hi > 0);

        var vendor = SelectedVendor();
        if (vendor != null) all = all.Where(s => vendor.Carries(s.SkuId));

        return all.OrderBy(s => s.ItemDescription, System.StringComparer.OrdinalIgnoreCase).ToList();
    }

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
        if (appt == null) return "NOT SCHEDULED — book a door on the Scheduler";

        string day = appt.Day == Today() ? "today"
                   : appt.Day == Today() + 1 ? "tomorrow"
                   : $"day {appt.Day}";
        return $"Arriving {day}, {DockScheduleService.BlockLabel(appt.BlockIndex)}, door {appt.DoorNumber}";
    }

    // NOTE: the delivery-day picker and its helpers (DeliveryDayChoices / DeliveryChoiceIndex /
    // DeliveryDayText / WeekdayName) were deleted along with the dropdown. Scheduling is manual —
    // see the comment in BuildCreateTab. If a weekday label is ever wanted again, derive it from the
    // in-game day count rather than DateTime.Now: EmployeeStatSystem.GetCurrentDayOfWeek still reads
    // the real calendar and would drift from the game's own clock.

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
}
