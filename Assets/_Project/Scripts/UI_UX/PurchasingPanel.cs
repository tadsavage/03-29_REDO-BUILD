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

    /// <summary>Item icon, doubled from 58. At the old size the art was decoration; at this size you
    /// pick the row out by its picture instead of reading every name.</summary>
    private const float IconSize = 116f;

    /// <summary>Item name size, doubled from 16 — the card's headline, and it was reading as a
    /// caption next to the cost line beneath it.</summary>
    private const int ItemNameFontSize = 32;

    private const float QtyFieldWidth = 96f;
    private const float StepButtonSize = 34f;

    /// <summary>The two big commit buttons at the top of the create tab.</summary>
    private const float ActionButtonHeight = 56f;
    private const float ActionButtonWidth  = 240f;

    /// <summary>Line-cost plate. Wide enough for "$12,345.00" without the figure wrapping.</summary>
    private const float LineCostPlateWidth = 150f;

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
        idRow.style.marginBottom = 10;

        var hint = MakeText("Raise the order, then book its door and time on the Scheduler.",
                            16, ColSubtleText);
        hint.style.whiteSpace = WhiteSpace.NoWrap;
        idRow.Add(hint);

        var poPill = new VisualElement();
        poPill.style.flexShrink = 0;
        poPill.style.paddingLeft = 22; poPill.style.paddingRight = 22;
        poPill.style.paddingTop = 6; poPill.style.paddingBottom = 6;
        poPill.style.backgroundColor = new StyleColor(ColStat);
        poPill.style.borderTopWidth = poPill.style.borderBottomWidth =
            poPill.style.borderLeftWidth = poPill.style.borderRightWidth = 2;
        poPill.style.borderTopColor = poPill.style.borderBottomColor =
            poPill.style.borderLeftColor = poPill.style.borderRightColor = new StyleColor(ColMoney);
        poPill.style.borderTopLeftRadius = poPill.style.borderTopRightRadius =
            poPill.style.borderBottomLeftRadius = poPill.style.borderBottomRightRadius = 8;
        var poLabel = MakeText($"PO #: {_poNumber}", 24, ColMoney, bold: true);
        poLabel.style.marginTop = 0; poLabel.style.marginBottom = 0;
        poLabel.style.whiteSpace = WhiteSpace.NoWrap;
        poPill.Add(poLabel);
        idRow.Add(poPill);

        _tabHeader.Add(idRow);
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

        var cost = MakeText($"Item Cost: {Money(sku.BuyValue)}/case", 15, ColChipOutText, bold: true);
        cost.style.marginTop = 1;
        body.Add(cost);

        // ── Quantity stepper, left-justified under the cost line ──
        var qtyRow = new VisualElement();
        qtyRow.style.flexDirection = FlexDirection.Row;
        qtyRow.style.alignItems = Align.Center;
        qtyRow.style.marginTop = 5;

        var minus = new Button { text = "–" };
        StyleStepButton(minus, ColDanger);
        qtyRow.Add(minus);

        // A typeable field, not a label: the player asked to be able to click in and type a quantity,
        // and typing 240 is a great deal faster than pressing + 240 times.
        var field = new TextField { value = Qty(skuId).ToString(), isDelayed = true };
        field.style.width = QtyFieldWidth;
        field.style.marginLeft = 6; field.style.marginRight = 6;
        ApplyFont(field, bold: true, size: 15);
        StyleQtyField(field);
        qtyRow.Add(field);

        var plus = new Button { text = "+" };
        StyleStepButton(plus, ColMoney);
        qtyRow.Add(plus);

        body.Add(qtyRow);

        // Doubles as the per-line capacity readout once anything is ordered: how many pallets this
        // line becomes, and whether they stack. That's the information that explains the fill bar —
        // without it "why did adding 40 cases eat two slots?" has no answer on screen.
        var palletNote = MakeText($"Case Size: {TrailerCapacity.CasesPerPallet(sku)} cases/pallet",
                                  15, ColSubtleText);
        palletNote.style.marginTop = 3;
        body.Add(palletNote);

        card.Add(body);

        // ── Line Cost plate, right-hand end of the card ──
        // Its own dark plate with the caption stacked over the figure, per the mock. Kept OUT of the
        // stepper row: that row is controls, this is the consequence of them, and putting the number
        // that changes on its own plate is what makes the change visible.
        var plate = new VisualElement();
        plate.style.width = LineCostPlateWidth;
        plate.style.flexShrink = 0;
        plate.style.alignSelf = Align.Center;
        plate.style.alignItems = Align.Center;
        plate.style.justifyContent = Justify.Center;
        plate.style.marginLeft = 10;
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

        card.Add(plate);

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
            lineCost.text = Money(q * sku.BuyValue);

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

    /// <summary>Cancel / order total / Create PO. In the STATIONARY header rather than the scroll
    /// view — the total is the number you watch while adding items, and a footer that scrolls away is
    /// a footer you can't watch.</summary>
    private void BuildCreateFooter()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 10;

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
        totalBox.style.marginLeft = 14; totalBox.style.marginRight = 14;
        totalBox.style.paddingLeft = 18; totalBox.style.paddingRight = 18;
        totalBox.style.backgroundColor = new StyleColor(ColStat);
        totalBox.style.borderTopWidth = totalBox.style.borderBottomWidth =
            totalBox.style.borderLeftWidth = totalBox.style.borderRightWidth = 2;
        totalBox.style.borderTopColor = totalBox.style.borderBottomColor =
            totalBox.style.borderLeftColor = totalBox.style.borderRightColor = new StyleColor(ColBorder);
        totalBox.style.borderTopLeftRadius = totalBox.style.borderTopRightRadius =
            totalBox.style.borderBottomLeftRadius = totalBox.style.borderBottomRightRadius = 8;

        var totalCaption = MakeText("ORDER TOTAL:", 28, ColTitleText, bold: true);
        totalCaption.style.marginRight = 14;
        totalCaption.style.marginTop = 0; totalCaption.style.marginBottom = 0;
        totalCaption.style.whiteSpace = WhiteSpace.NoWrap;
        totalBox.Add(totalCaption);

        _orderTotalLabel = MakeText("$0", 30, ColMoney, bold: true);
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

        var caption = MakeText("TRAILER LOAD", 16, ColTitleText, bold: true);
        caption.style.whiteSpace = WhiteSpace.NoWrap;
        top.Add(caption);

        _capacityLabel = MakeText(string.Empty, 16, ColSubtleText, bold: true);
        _capacityLabel.style.whiteSpace = WhiteSpace.NoWrap;
        top.Add(_capacityLabel);
        wrap.Add(top);

        var track = new VisualElement();
        track.style.height = 22;
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

        return wrap;
    }

    private Label _capacityLabel;
    private VisualElement _capacityFill;

    private void RefreshOrderTotal()
    {
        if (_orderTotalLabel == null) return;
        _orderTotalLabel.text = Money(BasketTotal());

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
            if (sku != null) total += kv.Value * sku.BuyValue;
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

        ShowConfirm($"Create PO number {_poNumber}?\n\n" +
                    $"{_basket.Count} line(s) · {_basket.Values.Sum():N0} case(s) · " +
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
                                           Mathf.RoundToInt(sku.BuyValue), sku.ShelfLifeDays)
            {
                FloorSlotIndex = pallet.FloorSlot,
                PalletTier = pallet.Tier
            });
        }

        var po = shipments.CreatePlayerPurchaseOrder(_poNumber, "PLAYER_SUPPLIER", "Wholesale Supply",
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

        // ── Header row: disclosure arrow, PO number, supplier, status, totals, actions ──
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;

        var arrow = MakeText(expanded ? "▼" : "▶", 14, ColChipOutText, bold: true);
        arrow.style.width = 18;
        arrow.style.flexShrink = 0;
        arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
        arrow.style.marginTop = 0; arrow.style.marginBottom = 0;
        header.Add(arrow);

        var po = MakeText($"PO {shipment.PONumber}", 18, ColTitleText, bold: true);
        po.style.marginRight = 12;
        po.style.whiteSpace = WhiteSpace.NoWrap;
        header.Add(po);

        var supplier = MakeText(shipment.SupplierName, 14, ColSubtleText);
        supplier.style.marginRight = 12;
        supplier.style.whiteSpace = WhiteSpace.NoWrap;
        header.Add(supplier);

        var status = MakeText($"[{shipment.Status}]", 13,
                              shipment.Status == ShipmentData.ShipmentStatus.Cancelled ? ColDangerSoft : ColChipOutText,
                              bold: true);
        status.style.whiteSpace = WhiteSpace.NoWrap;
        header.Add(status);

        var spacer = new VisualElement(); spacer.style.flexGrow = 1; header.Add(spacer);

        var summary = MakeText($"{shipment.LineItems.Count} line(s) · {shipment.TotalUnits:N0} case(s)",
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

        // Reads the DOCK APPOINTMENT, not ArrivalDayNumber. With the delivery-day picker gone, that
        // field is just "the day the order was raised" and says nothing about when the truck comes —
        // the booking does. An unbooked PO says so plainly, since that's the outstanding decision.
        var when = MakeText(ScheduleTextFor(shipment), 13, ScheduledFor(shipment) == null ? ColDangerSoft : ColSubtleText);
        when.style.marginTop = 2;
        card.Add(when);

        // Collapsed stops here: the header alone already carries PO number, status, line/case counts
        // and total, which is everything needed to scan a list. The lines are the detail you open.
        if (!expanded) return card;

        // ── Line items ──
        foreach (var li in shipment.LineItems)
        {
            var sku = FindSku(li.SkuId);
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;
            line.style.marginTop = 3;
            line.style.paddingLeft = 8;

            var itemNo = MakeText(li.SkuId, 13, ColChipOutText);
            itemNo.style.width = 90;
            itemNo.style.whiteSpace = WhiteSpace.NoWrap;
            line.Add(itemNo);

            var desc = MakeText(sku != null ? sku.ItemDescription : "(unknown item)", 13, ColTitleText);
            desc.style.flexGrow = 1;
            desc.style.whiteSpace = WhiteSpace.NoWrap;
            line.Add(desc);

            var qty = MakeText($"{li.Quantity:N0} cs", 13, ColSubtleText);
            qty.style.width = 90;
            qty.style.unityTextAlign = TextAnchor.MiddleRight;
            line.Add(qty);

            // Received only matters once something has actually turned up; on a fresh PO it's noise.
            var received = MakeText(li.ReceivedQuantity > 0 ? $"{li.ReceivedQuantity:N0} rcvd" : "—",
                                    13, li.ReceivedQuantity >= li.Quantity ? ColMoney : ColSubtleText);
            received.style.width = 100;
            received.style.unityTextAlign = TextAnchor.MiddleRight;
            line.Add(received);

            var lineCost = MakeText($"${li.TotalCost:N0}", 13, ColOrangeText);
            lineCost.style.width = 90;
            lineCost.style.unityTextAlign = TextAnchor.MiddleRight;
            line.Add(lineCost);

            card.Add(line);
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

    /// <summary>Every SKU the player can buy: one with a real cost and a committed Ti/Hi, since a SKU
    /// with no pallet configuration can't be expressed as freight.</summary>
    private static List<SkuData> OrderableSkus()
    {
        var inv = Inventory();
        if (inv == null) return new List<SkuData>();
        return inv.AllSkus
            .Where(s => s != null && s.BuyValue > 0f && s.Ti > 0 && s.Hi > 0)
            .OrderBy(s => s.ItemDescription, System.StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        ApplyFont(b, bold: true, size: 26);
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
        field.style.height = StepButtonSize;
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
