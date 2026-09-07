using System.Collections.Generic;
using System.Linq;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// "Work Queue" panel — lets the player release Open orders to a staging lane (so Order Selectors
/// can start picking them), release fully-Staged orders to a door (so a Loader/dock stocker starts
/// loading them onto a trailer — summoned automatically if one isn't already there), and close out
/// Loaded orders once they're aboard (bills them and, once nothing else assigned to that door is
/// still Loading/Loaded, releases the trailer to depart). Bound to the "7" key (see TopBarUI), same
/// programmatic UIToolkit shape/aesthetic as SlotAssignmentPanel (Lilita font, navy/blue palette,
/// DraggableWindow).
///
/// Rows are individual orders, grouped visually by customer (sorted by customer name). Each row's
/// Status column reflects whichever status is currently authoritative for that order: its
/// OrderSelect WorkTask's status (Open/Available/Assigned) while still being picked, or the order's
/// own OrderStatus (Staged/Loading/Loaded) once picking is done — see DeterminePhase. Only Open,
/// Staged, and Loaded rows have an enabled checkbox; those are the three points where the player
/// actually chooses something (a lane, then a door, then to close out). The bottom bar is
/// contextual: checking only Open rows shows a lane dropdown + Submit; checking only Staged rows
/// shows a door dropdown + Assign; checking only Loaded rows shows a Close Out button; anything
/// mixed (different phases, or more than one customer — a lane/trailer holds one customer at a time
/// for now) disables submission with an explanatory message.
/// </summary>
public class WorkQueuePanel : IUIPanel
{
    private static readonly Color ColBg         = new Color(18f / 255f, 26f / 255f, 36f / 255f, 1f); // fully opaque per Tad's explicit call
    private static readonly Color ColBorder     = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleText  = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColSubtleText = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    private static readonly Color ColOrange     = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeEdge = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    private static readonly Color ColOrangeText = new Color(0xFD / 255f, 0xE8 / 255f, 0xCC / 255f, 1f);
    private static readonly Color ColOrangeHover = new Color(0xC6 / 255f, 0x7F / 255f, 0x42 / 255f, 1f);
    private static readonly Color ColLabelCell  = new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 1f);
    private static readonly Color ColBlueEdge   = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColBlueHover  = new Color(0x5B / 255f, 0xA6 / 255f, 0xD8 / 255f, 1f);
    private static readonly Color ColRowEven    = new Color(36f / 255f, 48f / 255f, 62f / 255f, 0.65f);
    private static readonly Color ColRowOdd     = new Color(30f / 255f, 40f / 255f, 52f / 255f, 0.65f);
    private static readonly Color ColWarning    = new Color(0xE0 / 255f, 0x6C / 255f, 0x5A / 255f, 1f);
    private static readonly Color ColStatusOpen      = new Color(0x9A / 255f, 0x9A / 255f, 0x9A / 255f, 1f);
    private static readonly Color ColStatusAvailable = new Color(0x8B / 255f, 0xC6 / 255f, 0xE8 / 255f, 1f);
    private static readonly Color ColStatusAssigned  = new Color(0xF2 / 255f, 0xC2 / 255f, 0x5A / 255f, 1f);
    private static readonly Color ColStatusStaged    = new Color(0x7E / 255f, 0xD6 / 255f, 0x8A / 255f, 1f);
    private static readonly Color ColStatusLoaded    = new Color(0x4C / 255f, 0xB8 / 255f, 0x6A / 255f, 1f);

    private static readonly Color ColStatusLoading   = new Color(0x7E / 255f, 0xD6 / 255f, 0xC8 / 255f, 1f);
        // Header row indent — must equal the data rows' paddingLeft so columns line up.
        private const float RowPaddingLeft = 6f;
        private const float PaletteIdWidth = 92f;
        private const float ItemNumberWidth = 76f;
        private const float AreaWidth = 82f;
        private const float PriorityWidth = 62f;
        private const float RoleWidth = 156f;
        private const float TaskWidth = 96f;
        private const float RoleTaskGap = 12f;
        // Task column only: indent its contents ~2-3 characters and take the space out of the
        // column's own width, so Status and every column after it stay put.
        private const float TaskIndent = 15f;
        private const float StatusWidth = 88f;
        private const float LocationWidth = 72f;
        private const float OperatorWidth = 112f;
        private const float CustomerWidth = 116f;
        // ~3 characters of breathing room between Customer and Order -- per Tad's explicit call, the
        // two used to sit flush against each other with long customer names running right up to the
        // order number.
        private const float CustomerOrderGap = 22f;
        private const float OrderWidth = 104f;
        private const float DelDateWidth = 92f;
        private const float SelectAllWidth = 130f;
        private const float CancelSelectedWidth = 168f;
        // Per-row priority stepper (see BuildPriorityStepper) -- fits in the gap between the
        // Priority and Role columns, narrow enough to never crowd Role's own text.
        private const float PriorityStepperWidth = 24f;
        private const int PriorityStep = 100;
        private const int PriorityMin = 100;
        private const int PriorityMax = 900;
    private static Font _lilita;



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

    // NoStock = a legacy Backorder record (that status is retired). Shown so the player can cancel it
    // instead of it sitting invisible while holding a stage.
    private enum RowPhase { Open, Available, Assigned, Staged, Loading, Loaded, NoStock }
    // ReleaseToStagesAuto: a multi-customer Open selection, where the target dropdown is inert because
    // the stage is decided per customer by OrderService.TryPlanStageSpread rather than picked.
    private enum ActionMode { None, ReleaseToLane, ReleaseToStagesAuto, ReleaseToLoading, ReleaseToLoadingAuto, CloseOut, Mixed, ReassignLane }

    private readonly VisualElement _overlay;
    private readonly ScrollView _rowScroll;
    private readonly Label _bottomMessage;
    private readonly DropdownField _targetDropdown;
    private readonly Button _submitButton;
    private Button _selectAllButton;
    private Button _cancelSelectedButton;
    private Button _scaleButton;
    private Label _dayLabel;
    private Label _timeLabel;

    private readonly HashSet<string> _checkedOrderIds = new();
    // Row-click selection (replaces the old per-row checkbox). Anchor is the last row clicked plain
    // or ctrl -- shift-click/shift-drag measure their range from it. Rebuilt fresh every RebuildRows()
    // pass so it can never point at a destroyed row.
    private string _selectionAnchorOrderId;
    private bool _isDragSelecting;
    private readonly List<(string orderId, VisualElement row, bool isEven)> _selectableRows = new();
    private List<(int door, string lane)> _dropdownLanes = new(); // choice index -> lane, when releasing to a staging lane ("1D")
    private List<int> _dropdownDoors = new();       // choice index -> door number, when in door mode
    private ActionMode _mode = ActionMode.None;

    private bool _visible;
    private ResizableWindow _resizeWindow;
    private enum SortColumn { PaletteId, ItemNumber, Area, Priority, Role, Task, Status, From, To, Operator, Customer, Order, DelDate }
    private SortColumn _sortColumn = SortColumn.Priority;
    private bool _sortAscending;

    // ── Per-column autofilter (Excel-style dropdowns) ──
    private VisualElement _modal;
    private VisualElement _activeFilterPopup;
    private SortColumn? _activeFilterColumn;

    /// <summary>Filter state for one column.</summary>
    private class ColumnFilter
    {
        public bool Active;
        public readonly HashSet<string> Selection = new();
        public string SearchText = "";
        public Label HeaderLabel;
        public Label HeaderIcon;
    }

    private readonly Dictionary<SortColumn, ColumnFilter> _columnFilters = new();

    /// <summary>Columns that have an autofilter dropdown (excludes Order and checkbox).</summary>
    private static readonly SortColumn[] FilterableColumns =
    {
        SortColumn.PaletteId, SortColumn.ItemNumber, SortColumn.Area,
        SortColumn.Priority, SortColumn.Role, SortColumn.Task,
        SortColumn.Status, SortColumn.From, SortColumn.To,
        SortColumn.Operator, SortColumn.Customer,
    };

    private ColumnFilter GetFilter(SortColumn col)
    {
        if (!_columnFilters.TryGetValue(col, out var f))
        {
            f = new ColumnFilter();
            _columnFilters[col] = f;
        }
        return f;
    }

    private string _liveSignature;

    public WorkQueuePanel(VisualElement root)
    {
        _overlay = Build(out _rowScroll, out _bottomMessage, out _targetDropdown, out _submitButton);
        root.Add(_overlay);
        _overlay.schedule.Execute(RefreshIfVisible).Every(250);
        // Ends a shift-drag range-select no matter which row (or gap between rows) the button comes
        // up over. Registered once here rather than per-row since it isn't row-specific.
        _overlay.RegisterCallback<PointerUpEvent>(_ => _isDragSelecting = false);
        // Right-click anywhere in the panel clears the selection, on a row or off it -- rows only
        // StopPropagation() on their own LEFT-click handling (see BuildRow), so a right-click on a
        // row bubbles all the way up to here same as one on empty space.
        _overlay.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 1) ClearSelection();
        });
        // Left-click on empty space WITHIN the row list also clears -- deliberately scoped to
        // _rowScroll rather than the whole overlay: a blanket overlay-level handler would also fire
        // (and clear the selection) on every click of Select All / Cancel Selected / a column header /
        // Submit, since none of those StopPropagation() their own PointerDownEvent, which would break
        // Cancel Selected and Submit acting on an empty set the instant they're clicked. An actual row
        // still stops propagation before this fires, so this only catches genuinely empty space.
        _rowScroll.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 0) ClearSelection();
        });
        Hide();
    }

    public bool IsVisible => _visible;

    /// <summary>IUIPanel's view of the same flag. Implementing the interface is what puts this panel
    /// into UIKeyBindingManager's registry, and therefore into CloseAll() — which is what Tab calls
    /// (PlacementStateMachine). Without it the modal ignored Tab while every other panel closed.</summary>
    public bool IsOpen => _visible;

    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        _overlay.style.display = DisplayStyle.Flex;
        _liveSignature = null;
        RebuildRows();

        // Always opens filled rather than normal size, per Tad's explicit call -- same reasoning and
        // same deferred-one-frame pattern as PurchasingPanel.Show(): on the very first Show() of a
        // session the panel hasn't been through a layout pass yet, so FillScreen's size math has
        // nothing real to measure (see FillScreen's own doc comment).
        _overlay.schedule.Execute(() =>
        {
            _resizeWindow?.FillScreen();
            if (_resizeWindow != null) _resizeWindow.UpdateScaleButtonIcon(_scaleButton, 63f, ColSubtleText); // titleBtnSize is a local const in the constructor, out of scope here
        }).ExecuteLater(16);
    }
    /// <summary>"Day 5  Time: 14:30" -- same format the persistent top bar uses. Ticks on every
    /// 250ms poll independent of the row-signature diff below, since the clock moves even when
    /// nothing in the queue has changed.</summary>
    private void UpdateDayTimeLabel()
    {
        if (_dayLabel == null || _timeLabel == null) return;
        int day = 0; float hour = 0f, minute = 0f;
        if (ServiceLocator.TryGet(out SimulationTimeService time) && time != null)
        {
            day = time.Day; hour = time.Hour; minute = time.Minute;
        }
        _dayLabel.text = $"Day {day}";
        _timeLabel.text = $"Time: {(int)hour:00}:{(int)minute:00}";
    }

    private void RefreshIfVisible()
    {
        if (!_visible) return;
        UpdateDayTimeLabel();
        string signature = BuildLiveSignature();
        if (signature == _liveSignature) return;
        _liveSignature = signature;
        RebuildRows();
    }

    private static string BuildLiveSignature()
    {
        ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue);
        ServiceLocator.TryGet<OrderService>(out var orderService);

        string tasks = workQueue == null ? "" : string.Join("|", workQueue.Tasks
            .Where(t => t.Status != WorkTaskStatus.Complete)
            .Select(t => $"{t.TaskId}:{t.Status}:{t.PalletId}:{t.OrderId}:{t.AssignedToEmployeeGuid}:{t.FromLocation}:{t.ToLocation}"));
        string orders = orderService == null ? "" : string.Join("|", orderService.ActiveOrders
            .Select(o => $"{o.OrderId}:{o.Status}:{o.CustomerName}:{o.AssignedDoorNumber}:{o.AssignedLane}:{o.TotalUnitsPicked}"));
        return tasks + "#" + orders;
    }



    public void Hide()
    {
        _visible = false;
        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
    }

    public void Dispose()
    {
        if (_overlay.parent != null) _overlay.RemoveFromHierarchy();
    }

    // ── Shell ────────────────────────────────────────────────────────────────
    private VisualElement Build(out ScrollView rowScroll, out Label bottomMessage,
        out DropdownField targetDropdown, out Button submitButton)
    {
        var overlay = new VisualElement { name = "workqueue-overlay" };
        overlay.style.position = Position.Absolute;
        // Stops above the bottom HUD rather than covering it. This scrim is pickable while open (it
        // deliberately eats world clicks), and this panel is built into the Toast document at
        // sortingOrder 999999, so a full-height scrim swallows every click on the bar's buttons and
        // the Build/Play tabs, and no sortingOrder on the bar can win. Reserving that strip is what
        // actually keeps it reachable — and it has to include the tabs, which sit on top of the bar
        // and stick up past it. The modal itself is unaffected: overflow is visible, so it still
        // draws and picks outside the scrim's rect if it needs the room.
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0;
        overlay.style.bottom = BuildMenuUI.BottomHudReservedHeight;
        overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
        overlay.style.justifyContent = Justify.FlexStart;
        overlay.style.alignItems = Align.Center;

        var modal = new VisualElement { name = "workqueue-modal" };
        _modal = modal;
        modal.style.position = Position.Absolute;
        modal.style.left = 90;
        modal.style.top = 80;
        modal.style.backgroundColor = new StyleColor(ColBg);
        modal.style.borderTopWidth = modal.style.borderBottomWidth =
            modal.style.borderLeftWidth = modal.style.borderRightWidth = 3;
        modal.style.borderTopColor = modal.style.borderBottomColor =
            modal.style.borderLeftColor = modal.style.borderRightColor = new StyleColor(ColBorder);
        modal.style.borderTopLeftRadius = modal.style.borderTopRightRadius =
            modal.style.borderBottomLeftRadius = modal.style.borderBottomRightRadius = 16;
        modal.style.paddingTop = 14; modal.style.paddingBottom = 14;
        modal.style.paddingLeft = 16; modal.style.paddingRight = 16;
        // Columns total ~1270px with Fill Rate added; at the old 1180 the right-hand columns were
        // already overrunning each other (Customer's text ran into the order id).
        modal.style.width = 1320;
        modal.style.height = 680;
        modal.style.minWidth = 900;
        modal.style.minHeight = 260;
        modal.style.maxHeight = StyleKeyword.None;

        // Title bar
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.height = 70;
        titleBar.style.alignItems = Align.Center;

        titleBar.style.width = StyleKeyword.Auto;
        titleBar.style.alignSelf = Align.Stretch;
        titleBar.style.marginTop = -14;

        titleBar.style.flexShrink = 0;
        titleBar.style.marginLeft = -16;
        titleBar.style.marginRight = -16;
        titleBar.style.paddingLeft = 16;
        titleBar.style.paddingRight = 16;
        titleBar.style.backgroundColor = new StyleColor(new Color(12f / 255f, 18f / 255f, 26f / 255f, 1f));
        titleBar.style.borderBottomWidth = 2;
        titleBar.style.borderBottomColor = new StyleColor(ColBorder);
        titleBar.style.marginBottom = 10;

        // Select All — checks every currently-visible (post-filter) row that has an enabled
        // checkbox; flips to Clear All once they're all checked.
        _selectAllButton = StyleOrangeButton(new Button(ToggleSelectAllVisible) { text = "Select All" });
        _selectAllButton.style.width = SelectAllWidth;
        _selectAllButton.style.flexShrink = 0;
        _selectAllButton.style.paddingLeft = 0;
        _selectAllButton.style.paddingRight = 0;
        // The title bar is the drag handle; without this the press would start a window drag
        // and steal the pointer capture before the click resolves.
        _selectAllButton.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
        titleBar.Add(_selectAllButton);

        // Cancel Selected — calls off checked orders that nothing is physically committed to yet
        // (Open, Available, or a legacy No Stock row). This is what replaces the backorder: an order
        // the warehouse can't fill is either shipped short or cancelled outright.
        _cancelSelectedButton = StyleButton(new Button(CancelSelected) { text = "Cancel Selected" },
            ColWarning, ColOrangeEdge, ColOrangeText, ColOrangeHover);
        _cancelSelectedButton.style.width = CancelSelectedWidth;
        _cancelSelectedButton.style.flexShrink = 0;
        _cancelSelectedButton.style.marginLeft = 8;
        _cancelSelectedButton.style.paddingLeft = 0;
        _cancelSelectedButton.style.paddingRight = 0;
        _cancelSelectedButton.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
        titleBar.Add(_cancelSelectedButton);

        // Centered on the title bar's full width via absolute positioning rather than a flexGrow
        // spacer -- the left button cluster (Select All/Cancel Selected) and the right-side
        // scale/close buttons are very unequal widths, so a flexGrow box centers the text in
        // whatever space is left over between them, not on the panel's true horizontal centerline.
        // Absolute positioning spanning the whole bar keeps the title centered on the panel
        // regardless of how those clusters are sized. Priority is now set per-row via the small
        // up/down stepper next to each row's Priority cell (see BuildPriorityStepper) rather than
        // a title-bar dropdown + button, so there's no priority control here anymore.
        var title = new Label("Work Queue");
        ApplyFont(title, bold: true, size: 39); // 26 * 1.5 -- per Tad's explicit call
        title.style.color = new StyleColor(ColTitleText);
        title.style.position = Position.Absolute;
        title.style.left = 0; title.style.right = 0; title.style.top = 0; title.style.bottom = 0;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.pickingMode = PickingMode.Ignore;
        titleBar.Add(title);

        // Now that the title no longer sits in the flex flow (it's absolute, above), this flexGrow
        // spacer takes over its old job of soaking up the leftover row width so the scale/close
        // buttons added below stay pinned to the title bar's right edge.
        var titleBarSpacer = new VisualElement();
        titleBarSpacer.style.flexGrow = 1;
        titleBar.Add(titleBarSpacer);

        const float titleBtnSize = 63f; // 1.5x the base 42px square button
        // Routed through CloseAll(), not a bare Hide() — this panel is registered on key 7, and only
        // UIKeyBindingManager.ToggleUI/CloseAll ever reset _currentOpenKey back to -1. A direct Hide()
        // left it stuck, and PlacementStateMachine.HandleIdleHover gates the world hover popup on
        // CurrentOpenKey == -1 — so clicking this ✕ silently killed every world tooltip afterward even
        // though the panel had visibly closed. CloseAll() calls Hide() on every open registered panel
        // (this one included) and THEN clears CurrentOpenKey, so it's a safe superset of the old call.
        var closeButton = new Button(() => { UIKeyBindingManager.Instance?.CloseAll(); AudioManager.Play("UIClose"); }) { text = "✕" };
        ApplyFont(closeButton, bold: true, size: 30);
        closeButton.style.width = titleBtnSize;
        closeButton.style.height = titleBtnSize;
        closeButton.style.minWidth = titleBtnSize;
        closeButton.style.minHeight = titleBtnSize;
        closeButton.style.marginTop = 0;
        closeButton.style.marginBottom = 0;
        closeButton.style.marginLeft = 0;
        closeButton.style.marginRight = 0;
        closeButton.style.paddingTop = 0;
        closeButton.style.paddingBottom = 0;
        closeButton.style.paddingLeft = 0;
        closeButton.style.paddingRight = 0;
        closeButton.style.alignSelf = Align.Center;
        closeButton.style.unityTextAlign = TextAnchor.MiddleCenter;
        closeButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
        closeButton.style.color = new StyleColor(ColSubtleText);
        closeButton.RegisterCallback<PointerEnterEvent>(_ =>
        {
            closeButton.style.backgroundColor = new StyleColor(new Color(0.8f, 0.3f, 0.2f, 1f));
            closeButton.style.color = new StyleColor(Color.white);
        });
        closeButton.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            closeButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            closeButton.style.color = new StyleColor(ColSubtleText);
        });
        closeButton.RegisterCallback<PointerDownEvent>(_ =>
            closeButton.style.backgroundColor = new StyleColor(new Color(0.6f, 0.16f, 0.12f, 1f)));
        closeButton.RegisterCallback<PointerUpEvent>(_ =>
            closeButton.style.backgroundColor = new StyleColor(new Color(0.8f, 0.3f, 0.2f, 1f)));
        // Cycles normal / large / fill-screen (see ResizableWindow.CycleScale below). Same size as the
        // close button and on the same title-bar row, so the two sit flush together.
        _scaleButton = new Button { text = string.Empty };
        RuntimeTooltip.Attach(_scaleButton, "Resize window (normal / large / fill screen)");
        _scaleButton.style.width = titleBtnSize;
        _scaleButton.style.height = titleBtnSize;
        _scaleButton.style.minWidth = titleBtnSize;
        _scaleButton.style.minHeight = titleBtnSize;
        _scaleButton.style.marginTop = 0;
        _scaleButton.style.marginBottom = 0;
        _scaleButton.style.marginLeft = 0;
        _scaleButton.style.marginRight = 8;
        _scaleButton.style.paddingTop = 0;
        _scaleButton.style.paddingBottom = 0;
        _scaleButton.style.paddingLeft = 0;
        _scaleButton.style.paddingRight = 0;
        _scaleButton.style.alignSelf = Align.Center;
        _scaleButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
        _scaleButton.style.color = new StyleColor(ColSubtleText);
        ResizableWindow.AddStackedSquaresGlyph(_scaleButton, titleBtnSize, ColSubtleText, isFilled: false);
        _scaleButton.RegisterCallback<PointerEnterEvent>(_ =>
        {
            _scaleButton.style.backgroundColor = new StyleColor(new Color(0.35f, 0.55f, 0.95f, 0.35f));
            _scaleButton.style.color = new StyleColor(Color.white);
        });
        _scaleButton.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            _scaleButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            _scaleButton.style.color = new StyleColor(ColSubtleText);
        });

        // Was a bare spacer balancing the two left-hand buttons so the title stays centred --
        // repurposed into a Day/Time badge per Tad's explicit call, same gold used by the Scheduler's
        // own live-clock badge (SchedulerPanel's sweepColor) so the two read as the same kind of
        // readout. Kept the same footprint/width so the title still centres correctly.
        var dayTimeBadge = new VisualElement();
        dayTimeBadge.style.width = SelectAllWidth + 8 + CancelSelectedWidth - titleBtnSize - 8 - titleBtnSize;
        dayTimeBadge.style.height = titleBtnSize;
        dayTimeBadge.style.flexShrink = 0;
        // Switched from a flex-flow marginLeft hack to absolute positioning anchored off the title
        // bar's right edge. The old approach sat this badge right after the title's flexGrow:1 box --
        // pushing the margin further negative also handed the title MORE leftover width to grow into
        // (Yoga counts a negative margin as shrinking this item's claimed space), so the badge's net
        // screen position barely moved no matter how far the margin was pushed. Anchoring via `right`
        // removes it from that flex negotiation entirely: this value now maps 1:1 to on-screen offset
        // from the title bar's right edge, past the scale/close buttons. Per Tad's explicit call to
        // move it another half inch left, this replaces the old (ineffective) -186 margin.
        dayTimeBadge.style.position = Position.Absolute;
        dayTimeBadge.style.right = 202;
        dayTimeBadge.style.justifyContent = Justify.Center;
        dayTimeBadge.style.alignItems = Align.Center;
        // A tad darker than the Scheduler's own sweepColor gold (F5C73C) -- per Tad's explicit call.
        dayTimeBadge.style.backgroundColor = new StyleColor(new Color(0xD8 / 255f, 0xAF / 255f, 0x35 / 255f, 1f));
        dayTimeBadge.style.borderTopLeftRadius = dayTimeBadge.style.borderTopRightRadius =
            dayTimeBadge.style.borderBottomLeftRadius = dayTimeBadge.style.borderBottomRightRadius = 6;
        // Day on top, Time underneath, both centered -- per Tad's explicit call. Default VisualElement
        // flexDirection is Column, so the badge's own justify/align-items above already centers this
        // two-line stack both horizontally and vertically as a group.
        _dayLabel = new Label();
        ApplyFont(_dayLabel, bold: true, size: 18);
        _dayLabel.style.color = new StyleColor(Color.white);
        _dayLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        // Zeroed out -- the default Label's own padding/margin was the real gap between the two
        // lines, same fix as the Scheduler tooltip's pallet-count badge earlier tonight. -4/-4 pulled
        // them too close; backed off 50% to -2/-2 per Tad's explicit call.
        _dayLabel.style.paddingTop = 0; _dayLabel.style.paddingBottom = 0;
        // Nudged up a small amount, per Tad's explicit call.
        _dayLabel.style.marginTop = -3; _dayLabel.style.marginBottom = -2;
        dayTimeBadge.Add(_dayLabel);
        _timeLabel = new Label();
        ApplyFont(_timeLabel, bold: true, size: 18);
        _timeLabel.style.color = new StyleColor(Color.white);
        _timeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _timeLabel.style.paddingTop = 0; _timeLabel.style.paddingBottom = 0;
        _timeLabel.style.marginTop = -2; _timeLabel.style.marginBottom = 0;
        dayTimeBadge.Add(_timeLabel);
        UpdateDayTimeLabel();
        titleBar.Add(dayTimeBadge);

        titleBar.Add(_scaleButton);
        titleBar.Add(closeButton);
        modal.Add(titleBar);

        new DraggableWindow(modal, titleBar, closeButton);
        _resizeWindow = new ResizableWindow(modal, minW: 900f, minH: 260f, grip: 10f, titleInset: 54f, allowVerticalResize: false);
        _scaleButton.clicked += () =>
        {
            _resizeWindow.CycleScale();
            _resizeWindow.UpdateScaleButtonIcon(_scaleButton, titleBtnSize, ColSubtleText);
            AudioManager.Play(_resizeWindow.IsFilled ? "UIMax" : "UIMin");
        };

        // Column headers. The dark modal styling remains the new queue's visual shell;
        // these columns expose the complete work-task record used by the old queue.
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.flexShrink = 0;
        header.style.overflow = Overflow.Hidden;
        header.style.paddingLeft = RowPaddingLeft;
        header.Add(BuildFilterHeader("Palette ID", PaletteIdWidth, SortColumn.PaletteId));
        header.Add(BuildFilterHeader("Item#", ItemNumberWidth, SortColumn.ItemNumber));
        header.Add(BuildFilterHeader("Area", AreaWidth, SortColumn.Area, marginLeft: 12f));
        header.Add(BuildFilterHeader("Priority", PriorityWidth, SortColumn.Priority));
        header.Add(BuildFilterHeader("Role", RoleWidth, SortColumn.Role, marginLeft: PriorityStepperWidth));
        header.Add(BuildFilterHeader("Task", TaskWidth - TaskIndent, SortColumn.Task, marginLeft: RoleTaskGap + TaskIndent));
        header.Add(BuildFilterHeader("Status", StatusWidth, SortColumn.Status));
        header.Add(BuildFilterHeader("From", LocationWidth, SortColumn.From));
        header.Add(BuildFilterHeader("To", LocationWidth, SortColumn.To));
        header.Add(BuildFilterHeader("Operator", OperatorWidth, SortColumn.Operator));
        header.Add(BuildFilterHeader("Customer", CustomerWidth, SortColumn.Customer, fontSize: 12)); // stays original size -- every other column got 25% bigger, per Tad's explicit call
        header.Add(HeaderCell("Order", OrderWidth, SortColumn.Order, marginLeft: CustomerOrderGap));
        header.Add(HeaderCell("Del. Date", DelDateWidth, SortColumn.DelDate));
        modal.Add(header);

        rowScroll = new ScrollView
        {
            verticalScrollerVisibility = ScrollerVisibility.Auto,
            horizontalScrollerVisibility = ScrollerVisibility.Hidden
        };
        rowScroll.style.flexGrow = 1;
        rowScroll.style.maxHeight = 520;
        rowScroll.style.overflow = Overflow.Hidden;
        modal.Add(rowScroll);

        // Bottom bar
        var bottomBar = new VisualElement();
        bottomBar.style.flexDirection = FlexDirection.Row;
        bottomBar.style.alignItems = Align.FlexStart;
        bottomBar.style.borderTopWidth = 2;
        bottomBar.style.borderTopColor = new StyleColor(ColBorder);
        bottomBar.style.paddingTop = 7.5f; // 10 * 0.75 -- per Tad's explicit call to shrink this bar 25%

        bottomMessage = new Label("To select records for update Left-Click, Hold L-Ctrl while Left-Clicking to Select multiple records and if you hold SHIFT and DRAG across records you can select many records easily.");
        // fontSize 9.75 = 13 * 0.75, and maxWidth 75% -- both dimensions of this box reduced 25%,
        // per Tad's explicit call.
        ApplyFont(bottomMessage);
        bottomMessage.style.fontSize = 9.75f;
        bottomMessage.style.color = new StyleColor(ColSubtleText);
        bottomMessage.style.flexGrow = 1;
        bottomMessage.style.flexShrink = 1;
        bottomMessage.style.minWidth = 0;
        bottomMessage.style.maxWidth = new Length(75, LengthUnit.Percent);
        bottomMessage.style.whiteSpace = WhiteSpace.Normal;
        bottomMessage.style.overflow = Overflow.Hidden;
        bottomBar.Add(bottomMessage);

        targetDropdown = new DropdownField(new List<string> { "—" }, 0);
        ApplyFont(targetDropdown, size: 13);
        targetDropdown.style.width = 200;
        targetDropdown.style.marginRight = 8;
        bottomBar.Add(targetDropdown);

        submitButton = StyleOrangeButton(new Button(OnSubmitClicked) { text = "Submit Selection" });
        bottomBar.Add(submitButton);

        modal.Add(bottomBar);

        overlay.Add(modal);
        return overlay;
    }

    private Button HeaderCell(string text, float width, SortColumn? sortColumn = null, float marginLeft = 0f, int fontSize = 15)
    {
        var header = new Button();
        ApplyFont(header, bold: true, size: fontSize);
        header.style.color = new StyleColor(ColSubtleText);
        header.style.flexShrink = 0;
        header.style.width = width;
        header.style.marginLeft = marginLeft;

        header.style.minWidth = width;

        header.style.paddingLeft = 0;
        header.style.paddingRight = 0;
        header.style.paddingTop = 0;
        header.style.paddingBottom = 0;
        header.style.backgroundColor = new StyleColor(Color.clear);
        header.style.borderTopWidth = header.style.borderBottomWidth =
            header.style.borderLeftWidth = header.style.borderRightWidth = 0;
        header.style.unityTextAlign = TextAnchor.MiddleCenter; // centered over its column, per Tad's explicit call
        header.text = text;

        if (sortColumn.HasValue)
        {
            SortColumn column = sortColumn.Value;
            header.clicked += () =>
            {
                if (_sortColumn == column) _sortAscending = !_sortAscending;
                else
                {
                    _sortColumn = column;
                    _sortAscending = true;
                }
                header.text = HeaderText(text, column);
                RebuildRows();
            };
            header.RegisterCallback<PointerEnterEvent>(_ => header.style.color = new StyleColor(ColTitleText));
            header.RegisterCallback<PointerLeaveEvent>(_ => header.style.color = new StyleColor(ColSubtleText));
        }

        return header;
    }

    private string HeaderText(string text, SortColumn column)
    {
        if (_sortColumn != column) return text;
        return text + (_sortAscending ? " ▲" : " ▼");
    }


    private static Button StyleOrangeButton(Button b) => StyleButton(b, ColOrange, ColOrangeEdge, ColOrangeText, ColOrangeHover);

    private static Button StyleButton(Button b, Color bg, Color edge, Color text, Color hover)
    {
        ApplyFont(b, bold: true, size: 14);
        b.style.backgroundColor = new StyleColor(bg);
        b.style.color = new StyleColor(text);
        b.style.borderBottomWidth = 3;
        b.style.borderBottomColor = new StyleColor(edge);
        b.style.borderTopWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        b.style.paddingTop = 5; b.style.paddingBottom = 5;
        b.style.paddingLeft = 14; b.style.paddingRight = 14;
        b.RegisterCallback<PointerEnterEvent>(_ => { if (b.enabledSelf) b.style.backgroundColor = new StyleColor(hover); });
        b.RegisterCallback<PointerLeaveEvent>(_ => { if (b.enabledSelf) b.style.backgroundColor = new StyleColor(bg); });
        return b;
    }

    // ── Rows ─────────────────────────────────────────────────────────────────

    private void RebuildRows()
    {
        if (!_visible) return;
        _rowScroll.Clear();
        // Every row element this tracks is about to be destroyed by the Clear() above -- drop the
        // stale references so RefreshRowHighlights (fired by a selection change mid-drag) can never
        // touch a dead VisualElement.
        _selectableRows.Clear();

        ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue);
        // OrderSelect and PalletPick are excluded because both are ORDER work, already represented by
        // the order rows below — and a bulk order files one PalletPick per pallet, so ten of them
        // would bury its own order row under ten near-identical task rows.
        var taskRows = workQueue?.Tasks
            .Where(t => t.Type != WorkTaskType.OrderSelect && t.Type != WorkTaskType.PalletPick
                     && t.Status != WorkTaskStatus.Complete)
            .ToList() ?? new List<WorkTask>();
        taskRows = SortTasks(taskRows);
        taskRows = ApplyTaskFilters(taskRows);

        if (!ServiceLocator.TryGet<OrderService>(out var orderService) || orderService == null)
        {
            foreach (var task in taskRows)
                _rowScroll.Add(BuildTaskRow(task, _rowScroll.childCount));

            if (taskRows.Count == 0)
            {
                var empty = new Label("No work in the queue. Generate some orders or inbound tasks to get started.");
                ApplyFont(empty, size: 13);
                empty.style.color = new StyleColor(ColSubtleText);
                _rowScroll.Add(empty);
            }
            RebuildBottomBar();
            UpdateTitleBarButtons();
            return;
        }

        // Sorting helpers are declared below the row rebuild method.

        var rows = BuildVisibleOrderRows();

        // Drop checked ids that no longer resolve to a still-actionable row (submitted, or picked
        // up by a selector concurrently) so their checkmark doesn't linger looking "stuck".
        var stillActionable = rows.Where(r => IsActionable(r.phase))
            .Select(r => r.order.OrderId).ToHashSet();
        _checkedOrderIds.RemoveWhere(id => !stillActionable.Contains(id));

        foreach (var task in taskRows)
            _rowScroll.Add(BuildTaskRow(task, _rowScroll.childCount));

        if (rows.Count == 0 && taskRows.Count == 0)
        {
            var empty = new Label("No work in the queue. Generate some orders or inbound tasks to get started.");
            ApplyFont(empty, size: 13);
            empty.style.color = new StyleColor(ColSubtleText);
            _rowScroll.Add(empty);
        }
        else
        {
            foreach (var (order, phase, task) in rows)
                _rowScroll.Add(BuildRow(order, phase, task, _rowScroll.childCount));
        }

        RebuildBottomBar();
        UpdateTitleBarButtons();
    }

    /// <summary>Rows the player can check. Three of them are the points where something gets picked:
    /// a lane (Open), a door (Staged), close-out (Loaded). Available and No Stock are checkable only
    /// so they can be CANCELLED — nothing of either is physically committed yet.</summary>
    private static bool IsActionable(RowPhase phase) =>
        phase == RowPhase.Open || phase == RowPhase.Staged || phase == RowPhase.Loaded ||
        phase == RowPhase.Available || phase == RowPhase.NoStock ||
        // Loading too: a trailer that took everything the lane had never reaches Loaded (there was
        // nothing left to fetch), and close-out is the only way to bill it and free the door.
        phase == RowPhase.Loading;

    /// <summary>Phases the Cancel Selected button acts on — mirrors OrderService.CanCancelOrder,
    /// which re-checks authoritatively before anything is actually cancelled.</summary>
    private static bool IsCancellable(RowPhase phase) =>
        phase == RowPhase.Open || phase == RowPhase.Available || phase == RowPhase.NoStock;

    /// <summary>The order rows the panel is currently showing — sorted, and narrowed by whatever
    /// column autofilters are active. Single source of truth for both the row list and Select All.</summary>
    private List<(OrderData order, RowPhase phase, WorkTask task)> BuildVisibleOrderRows()
    {
        var rows = new List<(OrderData order, RowPhase phase, WorkTask task)>();
        if (!ServiceLocator.TryGet<OrderService>(out var orderService) || orderService == null) return rows;
        ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue);

        foreach (var order in orderService.ActiveOrders)
        {
            var task = RepresentativeTask(order, workQueue);
            var phase = DeterminePhase(order, task);
            if (phase.HasValue) rows.Add((order, phase.Value, task));
        }
        return ApplyOrderFilters(SortOrders(rows));
    }

    /// <summary>Order ids for every row on screen right now that can be selected, in on-screen
    /// order — the reference list shift-range math (click and drag) walks. Recomputed fresh each
    /// call, never cached, so it can never disagree with what RebuildRows just rendered.</summary>
    private List<string> VisibleSelectableOrderIds()
        => BuildVisibleOrderRows().Where(r => IsActionable(r.phase)).Select(r => r.order.OrderId).ToList();

    /// <summary>
    /// The one task that stands for a whole order on its row.
    ///
    /// A plain order has exactly one (its OrderSelect), but a bulk order has a PalletPick per pallet
    /// and possibly a case pick on top — so the row has to summarise several. It takes the LEAST
    /// advanced of them, because that's what the player can still act on: an order with nine pallets
    /// claimed and one still Open is not "in progress", it's an order with work nobody has taken.
    /// </summary>
    private static WorkTask RepresentativeTask(OrderData order, WorkQueueSystem workQueue)
    {
        if (workQueue == null) return null;

        WorkTask best = null;
        foreach (var t in workQueue.Tasks)
        {
            if (t.OrderId != order.OrderId) continue;
            // Load is included alongside the picking types so a released (Loading) order still shows
            // a live task once it has one — without it, Role/Priority went blank the moment picking
            // finished even though a real Load task (role Loader) exists and is being worked.
            if (t.Type != WorkTaskType.OrderSelect && t.Type != WorkTaskType.PalletPick && t.Type != WorkTaskType.Load) continue;
            if (t.Status == WorkTaskStatus.Complete || t.Status == WorkTaskStatus.Cancelled) continue;
            if (best == null || PhaseRank(t.Status) < PhaseRank(best.Status)) best = t;
        }
        return best;
    }

    /// <summary>Lower = less advanced. Open before Available before Assigned.</summary>
    private static int PhaseRank(WorkTaskStatus status) => status switch
    {
        WorkTaskStatus.Open => 0,
        WorkTaskStatus.Available => 1,
        WorkTaskStatus.Assigned => 2,
        _ => 3,
    };

    /// <summary>Checks every visible row with an enabled checkbox; if they're already all
    /// checked, unchecks them instead. Filtered-out rows are left untouched either way.</summary>
    private void ToggleSelectAllVisible()
    {
        var selectable = VisibleSelectableOrderIds();
        if (selectable.Count == 0) return;

        if (selectable.All(_checkedOrderIds.Contains)) _checkedOrderIds.ExceptWith(selectable);
        else _checkedOrderIds.UnionWith(selectable);

        RebuildRows();
    }

    /// <summary>The checked orders that can actually be cancelled right now. OrderService.CanCancelOrder
    /// is the authority — the row phase is only how the button decides whether to look enabled.</summary>
    private List<string> CancellableCheckedOrderIds()
    {
        var ids = new List<string>();
        if (!ServiceLocator.TryGet<OrderService>(out var orderService) || orderService == null) return ids;

        foreach (var (order, phase, _) in BuildVisibleOrderRows())
        {
            if (!_checkedOrderIds.Contains(order.OrderId)) continue;
            if (!IsCancellable(phase) || !orderService.CanCancelOrder(order)) continue;
            ids.Add(order.OrderId);
        }
        return ids;
    }

    /// <summary>Cancels every checked order that can be cancelled, leaving any other checked row
    /// (a selector is mid-pick on it, or its goods are already staged) untouched.</summary>
    private void CancelSelected()
    {
        if (!ServiceLocator.TryGet<OrderService>(out var orderService) || orderService == null) return;

        var ids = CancellableCheckedOrderIds();
        if (ids.Count == 0) return;

        int cancelled = orderService.CancelOrders(ids);
        _checkedOrderIds.ExceptWith(ids);
        _liveSignature = null;   // statuses changed; force the next refresh tick to rebuild
        RebuildRows();

        if (cancelled > 0)
            _bottomMessage.text = $"Cancelled {cancelled} order(s).";
    }

    /// <summary>Shows how many of the checked rows the button would actually cancel, so a mixed
    /// selection says what it will do instead of silently doing part of it.</summary>
    private void UpdateCancelSelectedButton()
    {
        if (_cancelSelectedButton == null) return;
        int n = CancellableCheckedOrderIds().Count;
        _cancelSelectedButton.SetEnabled(n > 0);
        _cancelSelectedButton.text = n > 0 ? $"Cancel Selected ({n})" : "Cancel Selected";
        _cancelSelectedButton.style.opacity = n > 0 ? 1f : 0.5f;
    }

    /// <summary>Refreshes both title-bar buttons' enabled state and labels against the current
    /// selection. Called wherever the checked set or the visible rows change.</summary>
    private void UpdateTitleBarButtons()
    {
        UpdateSelectAllButton();
        UpdateCancelSelectedButton();
    }

    /// <summary>Applies +/- one priority step to every still-active task (not Complete/Cancelled)
    /// belonging to each checked order -- not just the one task the Priority column currently
    /// displays for that row, so raising an order's priority speeds up everything left to do on it
    /// (case picks, pallet picks, a load) rather than just whichever step happens to be shown.
    /// If nothing is checked, the row whose stepper was clicked is selected first so the click
    /// always has a visible target -- per Tad's explicit call. Clamped to 100-900 either way.</summary>
    private void AdjustPriorityForSelected(string clickedOrderId, int delta)
    {
        if (_checkedOrderIds.Count == 0)
        {
            _checkedOrderIds.Add(clickedOrderId);
            _selectionAnchorOrderId = clickedOrderId;
        }
        if (!ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue) || workQueue == null) return;

        int affectedOrders = 0;
        foreach (var orderId in _checkedOrderIds)
        {
            bool any = false;
            foreach (var t in workQueue.Tasks.Where(t => t.OrderId == orderId
                                                        && t.Status != WorkTaskStatus.Complete
                                                        && t.Status != WorkTaskStatus.Cancelled))
            {
                t.SetPriority(Mathf.Clamp(t.Priority + delta, PriorityMin, PriorityMax));
                any = true;
            }
            if (any) affectedOrders++;
        }

        _liveSignature = null; // priorities changed; force the next refresh tick to rebuild
        RebuildRows();

        if (affectedOrders > 0)
            UIToast.Show($"{(delta > 0 ? "Raised" : "Lowered")} priority on {affectedOrders} order(s).");
    }

    /// <summary>Same +/- one step per click, clamped to 100-900, for a standalone labor task row
    /// (Putaway/Inbound receiving etc.) -- these have no order to group by, so the stepper just
    /// acts on that single task directly.</summary>
    private void AdjustTaskPriority(WorkTask task, int delta)
    {
        task.SetPriority(Mathf.Clamp(task.Priority + delta, PriorityMin, PriorityMax));
        _liveSignature = null;
        RebuildRows();
    }

    /// <summary>Small vertical up/down stepper that sits in the gap between the Priority and Role
    /// columns -- replaces the old title-bar priority dropdown + Set Priority button with a
    /// per-row control, per Tad's explicit call. <paramref name="onUp"/>/<paramref name="onDown"/>
    /// decide what a click actually changes (the checked selection, or a single standalone task).</summary>
    private VisualElement BuildPriorityStepper(System.Action onUp, System.Action onDown)
    {
        var stepper = new VisualElement();
        stepper.style.width = PriorityStepperWidth;
        stepper.style.minWidth = PriorityStepperWidth;
        stepper.style.flexShrink = 0;
        stepper.style.flexDirection = FlexDirection.Column;
        stepper.style.justifyContent = Justify.Center;
        stepper.style.alignItems = Align.Center;

        stepper.Add(BuildStepperButton("\u25B2", onUp));
        stepper.Add(BuildStepperButton("\u25BC", onDown));
        return stepper;
    }

    private Button BuildStepperButton(string glyph, System.Action onClick)
    {
        var button = new Button(onClick) { text = glyph };
        ApplyFont(button, bold: true, size: 7);
        button.style.width = PriorityStepperWidth - 6f;
        button.style.height = 11f;
        button.style.minWidth = PriorityStepperWidth - 6f;
        button.style.minHeight = 11f;
        button.style.marginTop = 0; button.style.marginBottom = 1;
        button.style.marginLeft = 0; button.style.marginRight = 0;
        button.style.paddingTop = 0; button.style.paddingBottom = 0;
        button.style.paddingLeft = 0; button.style.paddingRight = 0;
        button.style.borderTopWidth = button.style.borderBottomWidth =
            button.style.borderLeftWidth = button.style.borderRightWidth = 0;
        button.style.borderTopLeftRadius = button.style.borderTopRightRadius =
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 3;
        var idleColor = new Color(1f, 1f, 1f, 0.08f);
        var hoverColor = new Color(1f, 1f, 1f, 0.22f);
        button.style.backgroundColor = new StyleColor(idleColor);
        button.style.color = new StyleColor(ColSubtleText);
        // Stops the row's own click-to-select handler from also firing -- a stepper click must only
        // ever change the CURRENT checked set (or select just this row if none is checked yet), never
        // reset an existing multi-row selection down to one row the way a normal row click would.
        button.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
        button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = new StyleColor(hoverColor));
        button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = new StyleColor(idleColor));
        return button;
    }

    // ── Row-click selection ─────────────────────────────────────────────────
    // Replaces the old per-row checkbox: the whole row is now the clickable target. Plain click
    // selects only that row; ctrl-click toggles it without touching the rest; shift-click and
    // shift-drag select the inclusive range between the last anchor and the row under the pointer.
    // None of this calls the heavy RebuildRows() (which tears down and re-sorts every row) — a fast
    // shift-drag can fire this many times a second, so selection changes only repaint the rows
    // that already exist (RefreshRowHighlights) plus the two title-bar buttons and the bottom bar,
    // same lightweight refresh the old checkbox's value-changed callback did.

    /// <summary>Right-click anywhere, or a left-click on empty space in the row list — clears the
    /// whole selection and its anchor. No-ops (skips the refresh) if nothing was selected.</summary>
    private void ClearSelection()
    {
        if (_checkedOrderIds.Count == 0 && _selectionAnchorOrderId == null) return;
        _checkedOrderIds.Clear();
        _selectionAnchorOrderId = null;
        _isDragSelecting = false;
        RefreshRowHighlights();
        RebuildBottomBar();
        UpdateTitleBarButtons();
    }

    /// <summary>Row PointerDownEvent handler. Left button only.</summary>
    private void HandleRowPointerDown(string orderId, bool ctrl, bool shift)
    {
        if (shift && _selectionAnchorOrderId != null)
        {
            ApplyRangeSelection(orderId);
            return;
        }

        if (ctrl)
        {
            if (!_checkedOrderIds.Add(orderId)) _checkedOrderIds.Remove(orderId);
        }
        else
        {
            _checkedOrderIds.Clear();
            _checkedOrderIds.Add(orderId);
        }
        _selectionAnchorOrderId = orderId;

        RefreshRowHighlights();
        RebuildBottomBar();
        UpdateTitleBarButtons();
    }

    /// <summary>Selects every row between the anchor and <paramref name="toOrderId"/> inclusive, in
    /// current on-screen order — shared by shift-click and shift-drag. Replaces rather than adds to
    /// the existing selection, matching standard list/Explorer shift-range behaviour. The anchor
    /// itself is left untouched so repeated shift-clicks/drag steps keep re-measuring from the same
    /// starting row.</summary>
    private void ApplyRangeSelection(string toOrderId)
    {
        var visible = VisibleSelectableOrderIds();
        int a = visible.IndexOf(_selectionAnchorOrderId);
        int b = visible.IndexOf(toOrderId);
        if (a < 0 || b < 0) return;
        if (a > b) (a, b) = (b, a);

        _checkedOrderIds.Clear();
        for (int i = a; i <= b; i++) _checkedOrderIds.Add(visible[i]);

        RefreshRowHighlights();
        RebuildBottomBar();
        UpdateTitleBarButtons();
    }

    /// <summary>Recolors every currently-built row from _checkedOrderIds without touching the DOM —
    /// the cheap counterpart to RebuildRows() for a selection-only change.</summary>
    private void RefreshRowHighlights()
    {
        foreach (var (orderId, row, isEven) in _selectableRows)
            row.style.backgroundColor = new StyleColor(RowBackground(isEven, _checkedOrderIds.Contains(orderId)));
    }

    /// <summary>The existing even/odd row stripe, blended toward the existing blue-hover accent when
    /// selected — keeps the striping visible underneath rather than replacing it outright.</summary>
    private static Color RowBackground(bool isEven, bool selected)
    {
        Color baseColor = isEven ? ColRowEven : ColRowOdd;
        if (!selected) return baseColor;
        Color hi = ColBlueHover;
        const float t = 0.45f;
        return new Color(
            Mathf.Lerp(baseColor.r, hi.r, t),
            Mathf.Lerp(baseColor.g, hi.g, t),
            Mathf.Lerp(baseColor.b, hi.b, t),
            Mathf.Max(baseColor.a, 0.9f));
    }

    /// <summary>Greys the button out when nothing on screen is checkable, and flips its label
    /// once everything checkable is checked.</summary>
    private void UpdateSelectAllButton()
    {
        if (_selectAllButton == null) return;
        var selectable = VisibleSelectableOrderIds();

        bool any = selectable.Count > 0;
        _selectAllButton.SetEnabled(any);
        _selectAllButton.text = any && selectable.All(_checkedOrderIds.Contains) ? "Clear All" : "Select All";
        _selectAllButton.style.opacity = any ? 1f : 0.5f;
    }

    // Sorting helpers are declared before DeterminePhase.
    private List<WorkTask> SortTasks(List<WorkTask> tasks)
    {
        IEnumerable<WorkTask> sorted = _sortColumn switch
        {
            SortColumn.PaletteId => tasks.OrderBy(t => t.PalletId),
            SortColumn.ItemNumber => tasks.OrderBy(GetTaskItemNumber),
            SortColumn.Area => tasks.OrderBy(t => AreaLabel(t.Area)),
            SortColumn.Priority => tasks.OrderBy(t => t.Priority),
            SortColumn.Role => tasks.OrderBy(t => t.RequiredRole.DisplayName()),
            SortColumn.Task => tasks.OrderBy(t => t.Type.ToString()),
            SortColumn.Status => tasks.OrderBy(t => t.Status.ToString()),
            SortColumn.From => tasks.OrderBy(t => t.FromLocation),
            SortColumn.To => tasks.OrderBy(t => t.ToLocation),
            SortColumn.Operator => tasks.OrderBy(t => GetOperatorName(t.AssignedToEmployeeGuid)),
            _ => tasks.OrderByDescending(t => t.Priority).ThenBy(t => t.Type.ToString())
        };
        return (_sortAscending ? sorted : sorted.Reverse()).ToList();
    }

    private List<(OrderData order, RowPhase phase, WorkTask task)> SortOrders(List<(OrderData order, RowPhase phase, WorkTask task)> rows)
    {
        IEnumerable<(OrderData order, RowPhase phase, WorkTask task)> sorted = _sortColumn switch
        {
            SortColumn.Area => rows.OrderBy(r => GetOrderAreaLabel(r.order)),
            SortColumn.Priority => rows.OrderBy(r => r.task?.Priority ?? 0),
            SortColumn.Role => rows.OrderBy(r => r.task?.RequiredRole.DisplayName()
                ?? (r.phase == RowPhase.Staged ? EmployeeRole.Loader.DisplayName() : "")),
            SortColumn.Task => rows.OrderBy(r => r.task?.Type.ToString() ?? "OrderSelect"),
            SortColumn.Status => rows.OrderBy(r => PhaseLabel(r.phase)),
            SortColumn.From => rows.OrderBy(r => r.task?.FromLocation ?? ""),
            SortColumn.To => rows.OrderBy(r => r.task?.ToLocation ?? ""),
            SortColumn.Operator => rows.OrderBy(r => GetOperatorName(r.task?.AssignedToEmployeeGuid)),
            SortColumn.Customer => rows.OrderBy(r => r.order.CustomerName),
            SortColumn.Order => rows.OrderBy(r => r.order.OrderId),
            SortColumn.ItemNumber => rows.OrderBy(r => GetOrderItemNumber(r.order)),
            SortColumn.DelDate => rows.OrderBy(r => r.order?.DueDay ?? int.MaxValue),
            _ => rows.OrderBy(r => r.order.CreatedTimeMinute)
        };
        return (_sortAscending ? sorted : sorted.Reverse()).ToList();
    }

    private static string GetTaskItemNumber(WorkTask task)
    {
        if (string.IsNullOrEmpty(task.PalletId)) return "\u2014";
        if (!ServiceLocator.TryGet<InventoryService>(out var inventory) || inventory == null) return "\u2014";
        var pallet = inventory.GetPallet(task.PalletId);
        if (pallet == null) return "\u2014";
        var sku = inventory.AllSkus.FirstOrDefault(s => s.SkuId == pallet.SkuId);
        return sku != null ? sku.ItemNumber.ToString() : pallet.SkuId;
    }

    /// <summary>Returns the item number displayed for an order row — matches BuildRow exactly.</summary>
    private static string GetOrderItemNumber(OrderData order)
    {
        if (order == null || order.LineItems.Count == 0) return "\u2014";
        var distinctSkus = order.LineItems.Select(li => li.SkuId).Distinct().ToList();
        return distinctSkus.Count > 1 ? "Mixed" : distinctSkus[0];
    }

    /// <summary>Area for an order row, derived from the storage area(s) of the SKUs on the order
    /// rather than the representative task's Area \u2014 that field defaults to Grocery for every
    /// non-bulk OrderSelect task regardless of what's actually on the order, and goes missing
    /// entirely once the order's task completes (Staged/Loading/Loaded). "Mixed" once the order's
    /// line items span more than one storage area, same reasoning as GetOrderItemNumber.</summary>
    private static string GetOrderAreaLabel(OrderData order)
    {
        if (order == null || order.LineItems.Count == 0) return "\u2014";
        if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return "\u2014";

        var areas = order.LineItems
            .Select(li => inv.AllSkus.FirstOrDefault(s => s.SkuId == li.SkuId))
            .Where(s => s != null)
            .Select(s => s.StorageArea)
            .Distinct()
            .ToList();

        if (areas.Count == 0) return "\u2014";
        return areas.Count > 1 ? "Mixed" : AreaLabel(areas[0]);
    }

    /// <summary>Pallet ID for a Staged/Loading/Loaded order row once its picking task is gone (that
    /// task's PalletId was always null anyway \u2014 a selector's pallet is only known once built). Finds
    /// the order's own OutboundPalletBuilder instance(s) by OrderId and labels them by build order
    /// ("Pallet 1", "Pallets 1, 2") rather than their GUID-less scene name, which carries no useful
    /// identity of its own.</summary>
    private static string GetStagedPalletLabel(OrderData order)
    {
        if (order == null) return "\u2014";
        int count = Object.FindObjectsByType<OutboundPalletBuilder>(FindObjectsSortMode.None)
            .Count(p => p != null && p.OrderId == order.OrderId);
        if (count == 0) return "\u2014";

        var labels = Enumerable.Range(1, count).Select(i => i.ToString());
        return count == 1 ? $"Pallet {labels.First()}" : $"Pallets {string.Join(", ", labels)}";
    }
    private RowPhase? DeterminePhase(OrderData order, WorkTask task)
    {
        if (order.Status == OrderData.OrderStatus.Loading) return RowPhase.Loading;
        if (order.Status == OrderData.OrderStatus.Staged) return RowPhase.Staged;
        // A PARTIALLY PICKED order with freight already in its lane is offered as Staged so the player
        // can send it to a trailer. It used to fall through to its still-live pick task and render as
        // Available — a phase with a disabled checkbox — so an order the warehouse could only half
        // fill had no reachable action at all: the pallets sat in the lane and the trailer sat at the
        // door. Loading what you have is the normal dock behaviour; the Fill Rate column is what tells
        // the player it's short.
        if (order.Status == OrderData.OrderStatus.PartiallyPicked
            && order.TotalUnitsPicked > 0
            && order.AssignedDoorNumber > 0
            && !string.IsNullOrEmpty(order.AssignedLane)) return RowPhase.Staged;
        // Loaded had no branch at all, so it fell through to the task lookup below and returned null
        // (the OrderSelect task is long Complete by then) — no row, for the one phase whose row is the
        // ONLY way to reach close-out. Nothing could be billed and no outbound trailer could ever be
        // released to depart, so Loaded orders just accumulated forever.
        if (order.Status == OrderData.OrderStatus.Loaded) return RowPhase.Loaded;
        if (order.Status == OrderData.OrderStatus.Shipped || order.Status == OrderData.OrderStatus.Cancelled) return null;
        // Legacy Backorder rows: their OrderSelect task is long Complete, so without this branch they
        // resolve to no row at all — invisible, unactionable, and still holding a whole stage.
        if (order.Status == OrderData.OrderStatus.Backorder) return RowPhase.NoStock;
        if (task == null) return null;
        return task.Status switch
        {
            WorkTaskStatus.Open => RowPhase.Open,
            WorkTaskStatus.Available => RowPhase.Available,
            WorkTaskStatus.Assigned => RowPhase.Assigned,
            _ => null,
        };
    }

    private VisualElement BuildTaskRow(WorkTask task, int rowIndex)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 4; row.style.paddingBottom = 4; row.style.paddingLeft = RowPaddingLeft;
        row.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColRowEven : ColRowOdd);
        row.style.flexShrink = 0;

        string itemNumber = GetTaskItemNumber(task);

        AddRowCell(row, ShortId(task.PalletId), PaletteIdWidth, ColTitleText);
        AddRowCell(row, itemNumber, ItemNumberWidth, ColTitleText);
        AddRowCell(row, AreaLabel(task.Area), AreaWidth, ColSubtleText, marginLeft: 12f);
        // Right-aligned (not the usual MiddleCenter) with a small paddingRight buffer -- per Tad's
        // explicit call to tighten the gap to the stepper's arrows without the number touching them.
        AddRowCell(row, task.Priority.ToString(), PriorityWidth, ColTitleText, align: TextAnchor.MiddleRight, paddingRight: 6f);
        row.Add(BuildPriorityStepper(() => AdjustTaskPriority(task, PriorityStep),
                                      () => AdjustTaskPriority(task, -PriorityStep)));
        AddRowCell(row, task.RequiredRole.DisplayName(), RoleWidth, ColSubtleText);
        AddRowCell(row, WorkTaskTypeDisplayName(task.Type), TaskWidth - TaskIndent, ColTitleText, marginLeft: RoleTaskGap + TaskIndent);
        AddRowCell(row, task.Status.ToString(), StatusWidth, ColStatusColor(task.Status), bold: true);
        AddRowCell(row, task.FromLocation ?? "—", LocationWidth, ColSubtleText);
        AddRowCell(row, task.ToLocation ?? "—", LocationWidth, ColSubtleText);
        AddRowCell(row, GetOperatorName(task.AssignedToEmployeeGuid), OperatorWidth, ColTitleText);
        AddRowCell(row, "—", CustomerWidth, ColSubtleText, fontSize: 12); // Customer column stays original size
        AddRowCell(row, "—", OrderWidth, ColSubtleText, marginLeft: CustomerOrderGap);
        AddRowCell(row, "—", DelDateWidth, ColSubtleText);
        return row;
    }

    private VisualElement BuildRow(OrderData order, RowPhase phase, WorkTask task, int rowIndex)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 4; row.style.paddingBottom = 4; row.style.paddingLeft = RowPaddingLeft;
        row.style.flexShrink = 0;

        bool actionable = IsActionable(phase);
        bool isEven = rowIndex % 2 == 0;
        row.style.backgroundColor = new StyleColor(RowBackground(isEven, _checkedOrderIds.Contains(order.OrderId)));

        // The row itself is the click target now (see the "Row-click selection" section below) --
        // plain click selects only this row, ctrl-click toggles it, shift-click/shift-drag select
        // the range from the last anchor. Only actionable rows participate, same as the old
        // checkbox's SetEnabled(actionable) gate.
        if (actionable)
        {
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return; // left button only
                HandleRowPointerDown(order.OrderId, evt.ctrlKey, evt.shiftKey);
                _isDragSelecting = evt.shiftKey;
                evt.StopPropagation();
            });
            row.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (_isDragSelecting) ApplyRangeSelection(order.OrderId);
            });
            _selectableRows.Add((order.OrderId, row, isEven));
        }

        string itemNumber = GetOrderItemNumber(order);
        string area = GetOrderAreaLabel(order);
        // A Staged/Loading order's picking task carried no PalletId (a selector's pallet is only
        // known once actually built) — once that task is also gone, look up the real staged pallet
        // instead of showing a permanent blank.
        string paletteId = !string.IsNullOrEmpty(task?.PalletId) ? ShortId(task.PalletId) : GetStagedPalletLabel(order);
        // Staged has no live task (the OrderSelect that built the pallet already completed, and no
        // Load task exists until the player releases it to a door) — but the ROLE that will pick it
        // up next is not actually unknown, it's always Loader. Showing "—" there read as missing data
        // rather than "waiting on you to release it," which Priority (still "—" here) already conveys.
        string role = task != null ? task.RequiredRole.DisplayName()
            : phase == RowPhase.Staged ? EmployeeRole.Loader.DisplayName() : "—";
        string taskName = TaskTypeLabel(order, phase, task);
        string from = OrderFromLocation(order, task, phase);
        string to = OrderToLocation(order, task, phase);
        string operatorName = phase == RowPhase.Assigned ? GetOperatorName(task?.AssignedToEmployeeGuid) : "—";

        AddRowCell(row, paletteId, PaletteIdWidth, ColSubtleText);
        AddRowCell(row, itemNumber, ItemNumberWidth, ColTitleText);
        AddRowCell(row, area, AreaWidth, ColSubtleText, marginLeft: 12f);
        // Right-aligned (not the usual MiddleCenter) with a small paddingRight buffer -- per Tad's
        // explicit call to tighten the gap to the stepper's arrows without the number touching them.
        AddRowCell(row, task != null ? task.Priority.ToString() : "—", PriorityWidth, ColTitleText, align: TextAnchor.MiddleRight, paddingRight: 6f);
        if (task != null)
        {
            string orderId = order.OrderId;
            row.Add(BuildPriorityStepper(() => AdjustPriorityForSelected(orderId, PriorityStep),
                                          () => AdjustPriorityForSelected(orderId, -PriorityStep)));
        }
        else
        {
            // No live task on this row (e.g. Staged/Loaded with nothing left to prioritize) --
            // a blank spacer of the same width keeps Role's column lined up with the header either way.
            var stepperSpacer = new VisualElement();
            stepperSpacer.style.width = PriorityStepperWidth;
            stepperSpacer.style.minWidth = PriorityStepperWidth;
            stepperSpacer.style.flexShrink = 0;
            row.Add(stepperSpacer);
        }
        AddRowCell(row, role, RoleWidth, ColSubtleText);
        AddRowCell(row, taskName, TaskWidth - TaskIndent, ColTitleText, marginLeft: RoleTaskGap + TaskIndent);
        AddRowCell(row, PhaseLabel(phase), StatusWidth, PhaseColor(phase), bold: true);
        AddRowCell(row, from, LocationWidth, ColSubtleText);
        AddRowCell(row, to, LocationWidth, ColSubtleText);
        AddRowCell(row, operatorName, OperatorWidth, ColTitleText);
        AddRowCell(row, order.CustomerName, CustomerWidth, ColTitleText, fontSize: 12); // Customer column stays original size, per Tad's explicit call
        AddRowCell(row, order.OrderNumber ?? ShortId(order.OrderId), OrderWidth, ColSubtleText, marginLeft: CustomerOrderGap);
        AddRowCell(row, DelDateText(order), DelDateWidth, ColSubtleText);
        return row;
    }

    /// <summary>
    /// From/To mean different things either side of release, because the order itself does.
    ///
    /// An OPEN order hasn't been given a lane or a door yet — the useful answer is the pick run it's
    /// about to become, so From/To are the first and last pick faces of its projected route
    /// (OrderPickPath, the same slot-choice rule the selector uses).
    ///
    /// Once RELEASED the goods have a physical home: From is the staging lane they're in, To is the
    /// door they leave by. Both come from the ORDER rather than the attached task — a Staged order's
    /// OrderSelect task is already Complete, so reading the task left these blank exactly when the
    /// location mattered most.
    /// </summary>
    private static string OrderFromLocation(OrderData order, WorkTask task, RowPhase phase)
    {
        if (phase == RowPhase.Open)
        {
            var path = OrderPickPath.Project(order);
            return path.Count > 0 ? path[0] : "—";
        }
        if (order != null && order.AssignedDoorNumber > 0 && !string.IsNullOrEmpty(order.AssignedLane))
            return $"{order.AssignedDoorNumber}{order.AssignedLane}";
        return string.IsNullOrEmpty(task?.FromLocation) ? "—" : task.FromLocation;
    }

    /// <summary>See OrderFromLocation. Released orders read "Door N" rather than the bare number a
    /// Load task carries in ToLocation, so the column reads as a destination.</summary>
    private static string OrderToLocation(OrderData order, WorkTask task, RowPhase phase)
    {
        if (phase == RowPhase.Open)
        {
            var path = OrderPickPath.Project(order);
            return path.Count > 0 ? path[path.Count - 1] : "—";
        }
        if (order != null && order.AssignedDoorNumber > 0)
            return $"Door {order.AssignedDoorNumber}";
        return string.IsNullOrEmpty(task?.ToLocation) ? "—" : task.ToLocation;
    }

    private static string AreaLabel(PalletData.AreaCategory area) => area switch
    {
        PalletData.AreaCategory.Grocery => "GRO",
        PalletData.AreaCategory.Perishable => "PER",
        PalletData.AreaCategory.Frozen => "FRO",
        _ => area.ToString()
    };

    /// <summary>The Task column's label. While the order is still OPEN — before the player has
    /// clicked Assign Staging Lane — every order reads as ONE generic pick job named after its area
    /// (GroSel/PerSel/FrzSel): the order hasn't actually been broken into Pallet Picks + a case pick
    /// yet, even though the granular WorkTasks already exist underneath (unclaimable while Open — see
    /// OrderService.FileOrderTasks). Once released, the granular tasks speak for themselves: a
    /// PalletPick reads "Pallet Pick" and the leftover-case task reads CasePickGro/Per/Frz.</summary>
    private static string TaskTypeLabel(OrderData order, RowPhase phase, WorkTask task)
    {
        if (phase == RowPhase.Open) return $"{OrderAreaCode(order)}Sel";
        if (task == null) return "—";
        return task.Type switch
        {
            WorkTaskType.PalletPick => "Pallet Pick",
            WorkTaskType.OrderSelect => $"CasePick{AreaCodeShort(task.Area)}",
            WorkTaskType.Load => "Load",
            _ => WorkTaskTypeDisplayName(task.Type)
        };
    }

    /// <summary>Player-facing name for a WorkTaskType — mostly just the enum name, except where that
    /// reads wrong: the moves from reserve locations to picking locations are called "Replenishment"
    /// (Tad's spec), not the enum's internal "Replenish".</summary>
    private static string WorkTaskTypeDisplayName(WorkTaskType type) => type switch
    {
        WorkTaskType.Replenish => "Replenishment",
        _ => type.ToString()
    };

    /// <summary>Gro/Per/Frz — the short area code used by TaskTypeLabel, derived from
    /// GetOrderAreaLabel so both share the same "which area does this order belong to" answer rather
    /// than each re-deriving it (and possibly disagreeing) from the order's line items.</summary>
    private static string OrderAreaCode(OrderData order) => GetOrderAreaLabel(order) switch
    {
        "PER" => "Per",
        "FRO" => "Frz",
        _ => "Gro" // GRO, Mixed, or unresolved all default to Gro until Perishable/Frozen exist
    };

    private static string AreaCodeShort(PalletData.AreaCategory area) => area switch
    {
        PalletData.AreaCategory.Perishable => "Per",
        PalletData.AreaCategory.Frozen => "Frz",
        _ => "Gro"
    };

    private static Color ColStatusColor(WorkTaskStatus status) => status switch
    {
        WorkTaskStatus.Open => ColStatusOpen,
        WorkTaskStatus.Available => ColStatusAvailable,
        WorkTaskStatus.Assigned => ColStatusAssigned,
        WorkTaskStatus.Complete => ColStatusLoaded,
        _ => ColSubtleText
    };

    // 15 = 12 * 1.25 -- every column got 25% bigger text except Customer Name, which stays at the
    // original 12 via an explicit override at its own call site, per Tad's explicit call.
    private static Label AddRowCell(VisualElement row, string text, float width, Color color, bool bold = false, float marginLeft = 0f, float fontSize = 15f, TextAnchor align = TextAnchor.MiddleCenter, float paddingRight = 0f)
    {
        var label = new Label(text ?? "—");
        ApplyFont(label, bold, (int)fontSize);
        label.style.width = width;
        label.style.minWidth = width;
        label.style.marginLeft = marginLeft;
        label.style.marginRight = 0;
        label.style.paddingRight = paddingRight;
        label.style.flexShrink = 0;
        label.style.color = new StyleColor(color);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        // Centered to match the now-centered header text above it -- per Tad's explicit call
        // (headers were centered first, leaving data left-aligned underneath them, which read as
        // more misaligned than the original all-left-aligned layout).
        label.style.unityTextAlign = align;
        row.Add(label);
        return label;
    }

    /// <summary>The date the customer ORIGINALLY wanted this order picked up — OrderData.DueDay is
    /// set once when the order is raised and never touched by rescheduling, so this stays fixed even
    /// if the order's dock appointment later gets dragged to a different day, per Tad's explicit call
    /// that this must not track the current appointment date.</summary>
    private static string DelDateText(OrderData order)
        => order == null ? "—" : $"Day {order.DueDay}";

    private static string ShortId(string value)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        return value.Length > 8 ? value.Substring(0, 8) : value;
    }


    private static string PhaseLabel(RowPhase phase) => phase switch
    {
        RowPhase.Open => "Open",
        RowPhase.Available => "Available",
        RowPhase.Assigned => "Assigned",
        RowPhase.Staged => "Staged",
        RowPhase.Loading => "Loading",
        RowPhase.Loaded => "Loaded",
        RowPhase.NoStock => "No Stock",
        _ => "?",
    };

    private static Color PhaseColor(RowPhase phase) => phase switch
    {
        RowPhase.Open => ColStatusOpen,
        RowPhase.Available => ColStatusAvailable,
        RowPhase.Assigned => ColStatusAssigned,
        RowPhase.Staged => ColStatusStaged,
        RowPhase.Loading => ColStatusLoading,
        RowPhase.Loaded => ColStatusLoaded,
        RowPhase.NoStock => ColWarning,
        _ => ColSubtleText,
    };

    private static string GetOperatorName(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return "—";
        var emp = EmployeeRegistry.Instance?.GetByGuid(guid);
        return emp?.Record != null ? emp.Record.employeeName : "???";
    }

    // ── Bottom bar ───────────────────────────────────────────────────────────

    private void RebuildBottomBar()
    {
        if (!ServiceLocator.TryGet<OrderService>(out var orderService) || orderService == null)
        {
            SetBottomBar(ActionMode.None, "OrderService not available.", new List<string> { "—" });
            return;
        }

        var checkedOrders = _checkedOrderIds
            .Select(id => orderService.ActiveOrders.FirstOrDefault(o => o.OrderId == id))
            .Where(o => o != null)
            .ToList();

        if (checkedOrders.Count == 0)
        {
            SetBottomBar(ActionMode.None, "To select records for update Left-Click, Hold L-Ctrl while Left-Clicking to Select multiple records and if you hold SHIFT and DRAG across records you can select many records easily.", new List<string> { "—" });
            return;
        }

        ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue);
        // An order is picked EITHER via a standard OrderSelect task OR, when it's a full-pallet order,
        // via one or more PalletPick tasks instead — never both. Only matching OrderSelect here meant
        // full-pallet orders always resolved `task == null` -> DeterminePhase returned null -> phases
        // ended up EMPTY (not "one phase") -> the bottom bar wrongly reported a mixed selection ("check
        // only Open orders together...") even when a single full-pallet order was checked by itself.
        var phases = checkedOrders
            .Select(o => DeterminePhase(o, workQueue?.Tasks.FirstOrDefault(t => t.OrderId == o.OrderId
                && (t.Type == WorkTaskType.OrderSelect || t.Type == WorkTaskType.PalletPick))))
            .Where(p => p.HasValue).Select(p => p.Value).Distinct().ToList();

        var customers = checkedOrders.Select(o => o.CustomerId).Distinct().ToList();

        if (phases.Count != 1)
        {
            SetBottomBar(ActionMode.Mixed, "Selection mixes different stages — check only Open orders together, or only Staged orders together.", new List<string> { "—" });
            return;
        }

        // The one-customer rule is about SHARED PHYSICAL SPACE: a staging lane, and the trailer loaded
        // out of it, hold a single customer's goods at a time — so releasing to a stage or to a door
        // has to be one customer. Close-out shares nothing: it bills each order on its own line items
        // and releases each door separately once nothing at that door is still Loading/Loaded (see
        // OrderService.CloseOutOrders / TryReleaseDoorIfClear), so a batch spanning several customers
        // and several doors closes out exactly as correctly as one.
        // Every action here now spans customers, because none of them actually shares physical space
        // between customers: staging gives each customer its own stage, loading sends each to the door
        // its goods are already staged at (one trailer per door), and close-out bills each order on its
        // own line items. The old blanket one-customer rule was about a shared lane/trailer, and that
        // is enforced where it belongs — per stage and per door in OrderService — rather than by
        // refusing the whole selection up front.
        string customerId = customers[0];

        // No Stock (a legacy backorder record) has no Submit action of its own — Cancel Selected is
        // the only thing that acts on it.
        if (phases[0] == RowPhase.NoStock)
        {
            SetBottomBar(ActionMode.None, $"These {checkedOrders.Count} order(s) are short of stock and stuck — nothing was ever picked for them. Use Cancel Selected to call them off.",
                new List<string> { "—" }, enableTarget: false, enableSubmit: false);
            return;
        }

        // Available: released and waiting on an operator. Its lane can still be changed — the escape
        // hatch for an order stuck because the lane it was released to filled up (see
        // ReachTruckOperator's PalletPick backoff), and just as usefully lets the player redirect an
        // order that hasn't even started picking yet. Restricted to the SAME door: this only ever
        // changes which lane an order stages into, never which door its trailer will be at, so
        // everything downstream (loading, close-out) that keys off AssignedDoorNumber is untouched.
        if (phases[0] == RowPhase.Available)
        {
            var doors = checkedOrders.Select(o => o.AssignedDoorNumber).Where(d => d > 0).Distinct().ToList();
            var currentLanes = checkedOrders.Select(o => o.AssignedLane).Where(l => !string.IsNullOrEmpty(l)).Distinct().ToList();

            if (doors.Count == 1 && currentLanes.Count <= 1)
            {
                ServiceLocator.TryGet<InventoryService>(out var inv2);
                int door = doors[0];
                string currentLane = currentLanes.Count == 1 ? currentLanes[0] : null;

                var candidateLanes = LaneNamingService.AllLanes()
                    .Where(l => l.door == door && l.lane != currentLane)
                    .Where(l => inv2 == null || inv2.LaneAllowsPicking(l.door, l.lane))
                    .Where(l => !StagingLaneAssignmentService.LaneHasInboundStock(inv2, l.door, l.lane))
                    .Where(l => !TrailerOffloadController.IsLanePendingInbound(l.door, l.lane))
                    .OrderBy(l => l.lane)
                    .ToList();
                _dropdownLanes = candidateLanes;

                if (candidateLanes.Count > 0)
                {
                    string laneNote = currentLane != null ? $" from lane {door}{currentLane}" : string.Empty;
                    SetBottomBar(ActionMode.ReassignLane,
                        $"{checkedOrders.Count} order(s) waiting for an Order Selector — move{laneNote} to a different lane, or Cancel Selected to call them off:",
                        candidateLanes.Select(l => $"{l.door}{l.lane}").ToList());
                    return;
                }
            }

            SetBottomBar(ActionMode.None, $"These {checkedOrders.Count} order(s) are already released and waiting for an Order Selector to claim them. Use Cancel Selected to call them off.",
                new List<string> { "—" }, enableTarget: false, enableSubmit: false);
            return;
        }

        if (phases[0] == RowPhase.Open)
        {
            ServiceLocator.TryGet<InventoryService>(out var inv);

            // Several customers at once: there's no single stage to offer, so the target dropdown has
            // nothing to choose and the system assigns one stage per customer instead. Show the player
            // the exact plan it will commit — computed by the same method that commits it, so what
            // they read is what happens.
            if (customers.Count > 1)
            {
                if (!orderService.TryPlanStageSpread(_checkedOrderIds.ToList(), out var plan, out string why))
                {
                    SetBottomBar(ActionMode.ReleaseToStagesAuto,
                        $"Can't release these {customers.Count} customers — {why}.",
                        new List<string> { "—" }, enableTarget: false, enableSubmit: false);
                }
                else
                {
                    string assignments = string.Join(",   ", plan.Select(g => $"{g.CustomerName} → Stage {g.DoorNumber}"));
                    SetBottomBar(ActionMode.ReleaseToStagesAuto,
                        $"Release {checkedOrders.Count} order(s) across {customers.Count} customers — one stage each:   {assignments}",
                        new List<string> { "Release" }, enableTarget: false);
                }
                return;
            }

            // The player picks a specific LANE now, not a whole door's stage — a door with an inbound
            // trailer docked can still offer its other, unoccupied lanes for outbound release (only the
            // lane(s) the dock stocker is actually using are excluded). Every lane across every door is
            // a candidate; each is independently gated the same way a single lane always was:
            // pickable usage, no landed inbound stock, no pending inbound drop (see
            // TrailerOffloadController.IsLanePendingInbound), and not already owned by a different
            // customer's staging.
            var allLanes = LaneNamingService.AllLanes();
            var freeLanes = new List<(int door, string lane)>();
            var occupiedLanes = new List<string>();
            foreach (var (door, lane) in allLanes)
            {
                bool pickable = inv == null || inv.LaneAllowsPicking(door, lane);
                bool hasInboundStock = StagingLaneAssignmentService.LaneHasInboundStock(inv, door, lane);
                bool pendingInbound = TrailerOffloadController.IsLanePendingInbound(door, lane);
                bool availableForCustomer = StagingLaneAssignmentService.IsLaneAvailableFor(orderService, door, lane, customerId);

                if (pickable && !hasInboundStock && !pendingInbound && availableForCustomer)
                    freeLanes.Add((door, lane));
                else if (pickable)
                    // Inbound-only lanes are never offered either way, so they're not worth listing as
                    // "occupied" — only lanes that WOULD be candidates but are currently spoken for.
                    occupiedLanes.Add($"{door}{lane}");
            }
            freeLanes = freeLanes.OrderBy(l => l.door).ThenBy(l => l.lane).ToList();
            _dropdownLanes = freeLanes;

            string occupiedNote = occupiedLanes.Count > 0
                ? $"   (Occupied: {string.Join(", ", occupiedLanes.OrderBy(s => s))})"
                : string.Empty;

            if (freeLanes.Count == 0)
            {
                Debug.LogWarning($"[WorkQueuePanel] No staging lane available for {checkedOrders[0].CustomerName}.{occupiedNote}");
                SetBottomBar(ActionMode.ReleaseToLane, $"No available staging lane.{occupiedNote}", new List<string> { "—" }, enableTarget: false, enableSubmit: false);
            }
            else
            {
                // Default the dropdown to whatever door the Schedule tab already booked this order's
                // trailer at, if that door has a free lane — a reminder of the door plan already made,
                // rather than silently offering the lowest free lane instead.
                ServiceLocator.TryGet<DockScheduleService>(out var schedule);
                int? plannedDoor = schedule?.FindForOrder(checkedOrders[0].OrderId)?.DoorNumber;
                var plannedLane = plannedDoor.HasValue ? freeLanes.FirstOrDefault(l => l.door == plannedDoor.Value) : default;
                bool plannedDoorAvailable = plannedDoor.HasValue && plannedLane != default;

                string message = plannedDoorAvailable
                    ? $"Release {checkedOrders.Count} order(s) for {checkedOrders[0].CustomerName} to a staging lane (Door {plannedDoor.Value} scheduled):{occupiedNote}"
                    : $"Release {checkedOrders.Count} order(s) for {checkedOrders[0].CustomerName} to a staging lane:{occupiedNote}";

                SetBottomBar(ActionMode.ReleaseToLane, message, freeLanes.Select(l => $"{l.door}{l.lane}").ToList());

                if (plannedDoorAvailable)
                    _targetDropdown.SetValueWithoutNotify($"{plannedLane.door}{plannedLane.lane}");
            }
            return;
        }

        if (phases[0] == RowPhase.Loaded)
        {
            string who = customers.Count == 1
                ? $"for {checkedOrders[0].CustomerName}"
                : $"across {customers.Count} customers";
            int doorCount = checkedOrders.Select(o => o.AssignedDoorNumber).Distinct().Count();
            string doors = doorCount == 1 ? "the trailer" : $"each of the {doorCount} trailers";
            SetBottomBar(ActionMode.CloseOut, $"Close out {checkedOrders.Count} order(s) {who} — bills them and releases {doors} once its whole load is closed out:", new List<string> { "Close Out" }, enableTarget: false);
            return;
        }

        if (phases[0] == RowPhase.Loading)
        {
            // Only once nothing of theirs is left standing in the lane — billing a customer for cases
            // still on the warehouse floor is the one thing close-out must never allow.
            var blocked = checkedOrders.Where(o => !orderService.CanCloseOut(o, out _)).ToList();
            if (blocked.Count > 0)
            {
                orderService.CanCloseOut(blocked[0], out string whyNot);
                SetBottomBar(ActionMode.CloseOut,
                    $"Can't close out yet — {blocked[0].CustomerName}'s order {whyNot}.",
                    new List<string> { "—" }, enableTarget: false, enableSubmit: false);
                return;
            }

            int shortCount = checkedOrders.Count(o => o.TotalUnitsPicked < o.TotalUnits);
            string shortNote = shortCount > 0
                ? $"  {shortCount} will ship SHORT — customer satisfaction takes the hit."
                : string.Empty;
            SetBottomBar(ActionMode.CloseOut,
                $"Close out {checkedOrders.Count} order(s) with whatever made it onto the trailer." + shortNote,
                new List<string> { "Close Out" }, enableTarget: false);
            return;
        }

        if (phases[0] != RowPhase.Staged)
        {
            // A stray Available/Assigned/Loading row shouldn't be checkable at all (checkboxes are
            // disabled for those phases), but this guards the rare race where a row's phase changed
            // between the checkbox click and this rebuild instead of silently treating it as Staged.
            SetBottomBar(ActionMode.Mixed, "That order isn't ready for any action right now — refreshing.", new List<string> { "—" });
            return;
        }

        // Staged -> release to a door for loading. The order is already tied to a door (set when it
        // was released to its staging lane) — loading always targets that same door, so there's
        // nothing left to pick; a trailer is summoned automatically on submit if one isn't already
        // sitting there (see OrderService.ReleaseOrdersToLoading).
        var stagedDoors = checkedOrders.Select(o => o.AssignedDoorNumber).Distinct().OrderBy(d => d).ToList();

        // Several doors at once — one trailer each, so there's still nothing to choose, just more of
        // it. Spell out which customer goes to which door rather than a bare count: this is the moment
        // the player commits several trucks at once and it should be obvious what was included.
        if (stagedDoors.Count > 1)
        {
            string assignments = string.Join(",   ", stagedDoors.Select(d =>
            {
                var first = checkedOrders.First(o => o.AssignedDoorNumber == d);
                return $"{first.CustomerName} → Door {d}";
            }));
            SetBottomBar(ActionMode.ReleaseToLoadingAuto,
                $"Release {checkedOrders.Count} order(s) to loading — one trailer per door:   {assignments}",
                new List<string> { "Assign" }, enableTarget: false);
            return;
        }

        int targetDoor = stagedDoors[0];
        _dropdownDoors = new List<int> { targetDoor };
        SetBottomBar(ActionMode.ReleaseToLoading, $"Release {checkedOrders.Count} order(s) for {checkedOrders[0].CustomerName} to Door {targetDoor} for loading:", new List<string> { $"Door {targetDoor}" });
    }

    private void SetBottomBar(ActionMode mode, string message, List<string> choices, bool enableTarget = true, bool enableSubmit = true)
    {
        _mode = mode;
        _bottomMessage.text = message;
        _bottomMessage.style.color = new StyleColor(mode == ActionMode.Mixed ? ColWarning : ColSubtleText);

        _targetDropdown.choices = choices.Count > 0 ? choices : new List<string> { "—" };
        _targetDropdown.SetValueWithoutNotify(_targetDropdown.choices[0]);
        _targetDropdown.SetEnabled(enableTarget && mode != ActionMode.None && mode != ActionMode.Mixed);

        _submitButton.text = mode == ActionMode.ReleaseToLoading ? "Assign"
                           : mode == ActionMode.ReleaseToLoadingAuto ? "Assign All"
                           : mode == ActionMode.CloseOut ? "Close Out"
                           : mode == ActionMode.ReleaseToStagesAuto ? "Release All"
                           : mode == ActionMode.ReassignLane ? "Move"
                           : "Submit Selection";
        _submitButton.SetEnabled(enableSubmit && (mode == ActionMode.ReleaseToLane || mode == ActionMode.ReleaseToStagesAuto || mode == ActionMode.ReleaseToLoading || mode == ActionMode.ReleaseToLoadingAuto || mode == ActionMode.CloseOut || mode == ActionMode.ReassignLane) && choices.Count > 0 && choices[0] != "—");
        _submitButton.style.opacity = _submitButton.enabledSelf ? 1f : 0.5f;
    }

    private void OnSubmitClicked()
    {
        if (!ServiceLocator.TryGet<OrderService>(out var orderService) || orderService == null) return;
        var orderIds = _checkedOrderIds.ToList();
        if (orderIds.Count == 0) return;

        bool ok;
        int billed = 0;
        bool failAlreadyReported = false; // a branch that names its own reason suppresses the generic toast
        if (_mode == ActionMode.ReleaseToLane)
        {
            int idx = _targetDropdown.index;
            if (idx < 0 || idx >= _dropdownLanes.Count) return;
            var (door, lane) = _dropdownLanes[idx];
            ok = orderService.ReleaseOrdersToLane(orderIds, door, lane);
        }
        else if (_mode == ActionMode.ReleaseToStagesAuto)
        {
            // No dropdown index to read — the stage per customer was decided by the planner, and is
            // re-planned here rather than carried over from the preview so a stage taken in the
            // meantime is caught at commit time instead of being assigned blind.
            ok = orderService.ReleaseOrdersToStages(orderIds, out string why);
            if (!ok)
            {
                UIToast.Show($"Release failed — {why}");
                failAlreadyReported = true;
            }
        }
        else if (_mode == ActionMode.ReleaseToLoading)
        {
            int idx = _targetDropdown.index;
            if (idx < 0 || idx >= _dropdownDoors.Count) return;
            ok = orderService.ReleaseOrdersToLoading(orderIds, _dropdownDoors[idx]);
        }
        else if (_mode == ActionMode.ReleaseToLoadingAuto)
        {
            // Split by door inside the service — each door is its own trailer and its own Load task.
            ok = orderService.ReleaseOrdersToLoadingBatch(orderIds, out string whyLoad);
            if (!ok)
            {
                UIToast.Show($"Release to loading failed — {whyLoad}");
                failAlreadyReported = true;
            }
        }
        else if (_mode == ActionMode.CloseOut)
        {
            ok = orderService.CloseOutOrders(orderIds, out billed);
        }
        else if (_mode == ActionMode.ReassignLane)
        {
            int idx = _targetDropdown.index;
            if (idx < 0 || idx >= _dropdownLanes.Count) return;
            var (door, lane) = _dropdownLanes[idx];
            ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue2);

            ok = true;
            foreach (var id in orderIds)
            {
                var order = orderService.ActiveOrders.FirstOrDefault(o => o.OrderId == id);
                if (order == null) { ok = false; continue; }

                // Door is untouched — only which lane the order stages into changes, so the trailer
                // (spawned/found at AssignedDoorNumber) and every close-out/loading lookup that keys
                // off it are unaffected.
                order.AssignedLane = lane;

                // OrderSelect tasks read order.AssignedLane directly at delivery time (see
                // OrderSelectionTaskDriver.FinishOrder) — nothing to touch there. PalletPick tasks
                // carry their OWN destination baked into ToLocation at creation, so any of this
                // order's still-open ones have to be redirected explicitly or they'd keep aiming at
                // the lane just vacated.
                if (workQueue2 != null)
                    foreach (var t in workQueue2.Tasks.Where(t => t.OrderId == id && t.Type == WorkTaskType.PalletPick
                                                                 && t.Status != WorkTaskStatus.Complete
                                                                 && t.Status != WorkTaskStatus.Cancelled))
                        t.AssignToLocation($"{door}{lane}");
            }

            if (ok) UIToast.Show($"Moved {orderIds.Count} order(s) to lane {door}{lane}.");
        }
        else return;

        if (ok) _checkedOrderIds.Clear();
        else if (!failAlreadyReported) UIToast.Show("Could not submit that selection — it may have changed. Refreshing.");

        RebuildRows();

        // Close-out is the one action here that moves money, and the modal hides the world popup
        // FloatingMoneyText would otherwise show over the door — so the sale has to be acknowledged
        // inside the panel. Played AFTER RebuildRows so the rows the money came from are already gone
        // and the sweep reads as the consequence, not something happening alongside.
        if (ok && billed != 0)
        {
            var root = _overlay?.parent;
            MoneyFlightFx.Play(root, _modal, root?.Q<Label>("MoneyLabel"), billed,
                               label => ApplyFont(label, bold: true));
        }
    }

    // ── Per-column autofilter (Excel-style dropdowns) ───────────────────────

    /// <summary>Extracts the displayed cell value for a task row in the given column.</summary>
    private static string GetTaskCellValue(SortColumn col, WorkTask task)
    {
        return col switch
        {
            SortColumn.PaletteId => ShortId(task.PalletId),
            SortColumn.ItemNumber => GetTaskItemNumber(task),
            SortColumn.Area => AreaLabel(task.Area),
            SortColumn.Priority => task.Priority.ToString(),
            SortColumn.Role => task.RequiredRole.DisplayName(),
            SortColumn.Task => task.Type.ToString(),
            SortColumn.Status => task.Status.ToString(),
            SortColumn.From => task.FromLocation ?? "\u2014",
            SortColumn.To => task.ToLocation ?? "\u2014",
            SortColumn.Operator => GetOperatorName(task.AssignedToEmployeeGuid),
            SortColumn.Customer => "\u2014",
            _ => "\u2014"
        };
    }

    /// <summary>Extracts the displayed cell value for an order row in the given column.</summary>
    private static string GetOrderCellValue(SortColumn col, OrderData order, RowPhase phase, WorkTask task)
    {
        return col switch
        {
            SortColumn.PaletteId => !string.IsNullOrEmpty(task?.PalletId) ? ShortId(task.PalletId) : GetStagedPalletLabel(order),
            SortColumn.ItemNumber => GetOrderItemNumber(order),
            SortColumn.Area => GetOrderAreaLabel(order),
            SortColumn.Priority => task != null ? task.Priority.ToString() : "\u2014",
            SortColumn.Role => task != null ? task.RequiredRole.DisplayName()
                : phase == RowPhase.Staged ? EmployeeRole.Loader.DisplayName() : "\u2014",
            SortColumn.Task => task != null ? task.Type.ToString() : "\u2014",
            SortColumn.Status => PhaseLabel(phase),
            SortColumn.From => OrderFromLocation(order, task, phase),
            SortColumn.To => OrderToLocation(order, task, phase),
            SortColumn.Operator => phase == RowPhase.Assigned ? GetOperatorName(task?.AssignedToEmployeeGuid) : "\u2014",
            SortColumn.Customer => order.CustomerName,
            _ => "\u2014"
        };
    }

    /// <summary>Applies all active column filters to the task row list.</summary>
    private List<WorkTask> ApplyTaskFilters(List<WorkTask> tasks)
    {
        foreach (var col in FilterableColumns)
        {
            var f = GetFilter(col);
            if (!f.Active) continue;
            tasks = tasks.Where(t => f.Selection.Contains(GetTaskCellValue(col, t))).ToList();
        }
        return tasks;
    }

    /// <summary>Applies all active column filters to the order row list.</summary>
    private List<(OrderData order, RowPhase phase, WorkTask task)> ApplyOrderFilters(
        List<(OrderData order, RowPhase phase, WorkTask task)> rows)
    {
        foreach (var col in FilterableColumns)
        {
            var f = GetFilter(col);
            if (!f.Active) continue;
            rows = rows.Where(r => f.Selection.Contains(GetOrderCellValue(col, r.order, r.phase, r.task))).ToList();
        }
        return rows;
    }

    /// <summary>Collects every unique value displayed in the given column across all rows.</summary>
    private List<string> CollectAllValues(SortColumn col)
    {
        var values = new HashSet<string>();

        if (ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue) && workQueue != null)
        {
            foreach (var task in workQueue.Tasks)
            {
                if (task.Status == WorkTaskStatus.Complete) continue;
                // Same exclusions as RebuildRows — the filter's option list has to describe the rows
                // the panel actually shows, or it offers values nothing can match.
                if (task.Type == WorkTaskType.OrderSelect || task.Type == WorkTaskType.PalletPick) continue;
                values.Add(GetTaskCellValue(col, task));
            }
        }

        if (ServiceLocator.TryGet<OrderService>(out var orderService) && orderService != null)
        {
            ServiceLocator.TryGet<WorkQueueSystem>(out var wq);
            foreach (var order in orderService.ActiveOrders)
            {
                var task = RepresentativeTask(order, wq);
                var phase = DeterminePhase(order, task);
                if (phase.HasValue)
                    values.Add(GetOrderCellValue(col, order, phase.Value, task));
            }
        }

        var sorted = values.ToList();
        sorted.Sort(System.StringComparer.OrdinalIgnoreCase);
        return sorted;
    }

    // ── Header construction ──

    /// <summary>Builds a clickable column header with a filter dropdown indicator.</summary>
    private VisualElement BuildFilterHeader(string text, float width, SortColumn col, float marginLeft = 0f, int fontSize = 15)
    {
        var filter = GetFilter(col);

        var container = new VisualElement();
        container.style.flexShrink = 0;
        container.style.width = width;
        container.style.minWidth = width;
        container.style.flexDirection = FlexDirection.Row;
        container.style.alignItems = Align.Center;
        container.style.paddingLeft = 0;
        container.style.paddingRight = 0;
        container.style.marginLeft = marginLeft;

        filter.HeaderLabel = new Label(text);
        ApplyFont(filter.HeaderLabel, bold: true, size: fontSize);
        filter.HeaderLabel.style.color = new StyleColor(ColSubtleText);
        filter.HeaderLabel.style.flexGrow = 1;
        filter.HeaderLabel.style.unityTextAlign = TextAnchor.MiddleCenter; // centered over its column, per Tad's explicit call
        container.Add(filter.HeaderLabel);

        // Hidden rather than removed, per Tad's explicit call -- UpdateFilterHeaderAppearance still
        // writes its color on every hover/active-filter change, and this stays the null-safe target
        // for that instead of needing a guard everywhere it's touched. Visually gone, but the header
        // is still just as clickable (ToggleFilterPopup is on the whole container, not the icon) and
        // the active-filter state still shows via HeaderLabel's own color change.
        filter.HeaderIcon = new Label("\u25BC");
        ApplyFont(filter.HeaderIcon, size: 9);
        filter.HeaderIcon.style.color = new StyleColor(ColSubtleText);
        filter.HeaderIcon.style.marginLeft = 2;
        filter.HeaderIcon.style.display = DisplayStyle.None;
        container.Add(filter.HeaderIcon);

        container.RegisterCallback<ClickEvent>(_ => ToggleFilterPopup(col, container));
        container.RegisterCallback<PointerEnterEvent>(_ =>
        {
            if (!filter.Active)
            {
                filter.HeaderLabel.style.color = new StyleColor(ColTitleText);
                filter.HeaderIcon.style.color = new StyleColor(ColTitleText);
            }
        });
        container.RegisterCallback<PointerLeaveEvent>(_ => UpdateFilterHeaderAppearance(col));

        UpdateFilterHeaderAppearance(col);
        return container;
    }

    private void UpdateFilterHeaderAppearance(SortColumn col)
    {
        var f = GetFilter(col);
        if (f.HeaderLabel == null) return;
        var c = f.Active ? ColOrange : ColSubtleText;
        f.HeaderLabel.style.color = new StyleColor(c);
        f.HeaderIcon.style.color = new StyleColor(c);
    }

    // ── Popup management ──

    private void ToggleFilterPopup(SortColumn col, VisualElement anchor)
    {
        if (_activeFilterColumn == col && _activeFilterPopup != null)
        {
            CloseFilterPopup();
            return;
        }
        CloseFilterPopup();
        OpenFilterPopup(col, anchor);
    }

    private void OpenFilterPopup(SortColumn col, VisualElement anchor)
    {
        var filter = GetFilter(col);
        var allValues = CollectAllValues(col);

        if (!filter.Active)
        {
            filter.Selection.Clear();
            foreach (var v in allValues)
                filter.Selection.Add(v);
        }

        var anchorBounds = anchor.worldBound;
        var modalBounds = _modal.worldBound;
        float popupX = anchorBounds.x - modalBounds.x;
        float popupY = anchorBounds.y + anchorBounds.height - modalBounds.y;

        _activeFilterPopup = BuildFilterPopup(col, filter, allValues, popupX, popupY);
        _modal.Add(_activeFilterPopup);
        _activeFilterColumn = col;
    }

    private void CloseFilterPopup()
    {
        if (_activeFilterPopup != null)
        {
            _activeFilterPopup.RemoveFromHierarchy();
            _activeFilterPopup = null;
        }
        if (_activeFilterColumn.HasValue)
        {
            var f = GetFilter(_activeFilterColumn.Value);
            f.SearchText = "";
        }
        _activeFilterColumn = null;
    }

    private VisualElement BuildFilterPopup(SortColumn col, ColumnFilter filter,
        List<string> allValues, float x, float y)
    {
        const float PopupWidth = 280f;
        const float PopupMaxHeight = 420f;
        const float ListMaxHeight = 220f;

        // Full-modal click-catcher
        var clickCatcher = new VisualElement();
        clickCatcher.style.position = Position.Absolute;
        clickCatcher.style.left = 0; clickCatcher.style.top = 0;
        clickCatcher.style.right = 0; clickCatcher.style.bottom = 0;
        clickCatcher.style.backgroundColor = new StyleColor(Color.clear);
        clickCatcher.RegisterCallback<ClickEvent>(e =>
        {
            e.StopPropagation();
            CloseFilterPopup();
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

        // ── Sort buttons ──
        var sortAsc = new Button(() =>
        {
            _sortColumn = col;
            _sortAscending = true;
            RebuildRows();
            CloseFilterPopup();
        }) { text = "\u2191 Sort Ascending" };
        ApplyFont(sortAsc, size: 12);
        StyleFilterButton(sortAsc);
        panel.Add(sortAsc);

        var sortDesc = new Button(() =>
        {
            _sortColumn = col;
            _sortAscending = false;
            RebuildRows();
            CloseFilterPopup();
        }) { text = "\u2193 Sort Descending" };
        ApplyFont(sortDesc, size: 12);
        StyleFilterButton(sortDesc);
        panel.Add(sortDesc);

        AddSeparator(panel);

        // ── Search field ──
        var searchField = new TextField { value = filter.SearchText };
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
            filter.SearchText = evt.newValue ?? "";
            RefreshFilterCheckboxList(panel, col, allValues);
        });
        panel.Add(searchField);

        // ── Select All toggle ──
        var selectAllToggle = new Toggle { label = "Select All", value = true };
        selectAllToggle.name = "select-all-toggle";
        ApplyFont(selectAllToggle, size: 12);
        selectAllToggle.style.color = new StyleColor(ColTitleText);
        selectAllToggle.style.marginBottom = 4;
        selectAllToggle.RegisterValueChangedCallback(evt =>
        {
            var visible = FilterBySearch(allValues, filter.SearchText);
            if (evt.newValue)
            {
                foreach (var v in visible) filter.Selection.Add(v);
            }
            else
            {
                foreach (var v in visible) filter.Selection.Remove(v);
            }
            filter.Active = filter.Selection.Count < allValues.Count;
            RefreshFilterCheckboxList(panel, col, allValues);
            UpdateFilterHeaderAppearance(col);
            RebuildRows();
        });
        panel.Add(selectAllToggle);

        // ── Checkbox list ──
        var itemScroll = new ScrollView
        {
            verticalScrollerVisibility = ScrollerVisibility.Auto,
            horizontalScrollerVisibility = ScrollerVisibility.Hidden
        };
        itemScroll.style.maxHeight = ListMaxHeight;
        itemScroll.style.marginBottom = 6;
        itemScroll.name = "filter-list";
        panel.Add(itemScroll);

        PopulateFilterCheckboxList(itemScroll, col, allValues);

        AddSeparator(panel);

        // ── Bottom bar ──
        var bottomRow = new VisualElement();
        bottomRow.style.flexDirection = FlexDirection.Row;
        bottomRow.style.alignItems = Align.Center;

        var clearBtn = new Button(() =>
        {
            filter.Active = false;
            filter.Selection.Clear();
            foreach (var v in allValues) filter.Selection.Add(v);
            RefreshFilterCheckboxList(panel, col, allValues);
            UpdateFilterHeaderAppearance(col);
            RebuildRows();
        }) { text = "Clear Filter" };
        ApplyFont(clearBtn, size: 11);
        StyleFilterButton(clearBtn);
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

        UpdateFilterCount(panel, col, allValues);

        return clickCatcher;
    }

    // ── Popup helpers ──

    private static void AddSeparator(VisualElement parent)
    {
        var sep = new VisualElement();
        sep.style.height = 1;
        sep.style.backgroundColor = new StyleColor(new Color(ColBorder.r, ColBorder.g, ColBorder.b, 0.4f));
        sep.style.marginTop = 4; sep.style.marginBottom = 4;
        parent.Add(sep);
    }

    private static void StyleFilterButton(Button b)
    {
        b.style.backgroundColor = new StyleColor(Color.clear);
        b.style.color = new StyleColor(ColTitleText);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 0;
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 4;
        b.style.paddingTop = 4; b.style.paddingBottom = 4;
        b.style.paddingLeft = 8; b.style.paddingRight = 8;
        b.style.marginBottom = 2;
        b.style.unityTextAlign = TextAnchor.MiddleLeft;
        b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f)));
        b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(Color.clear));
    }

    private static List<string> FilterBySearch(List<string> allValues, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return allValues;
        return allValues.Where(v => v.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
    }

    private void RefreshFilterCheckboxList(VisualElement popupPanel, SortColumn col, List<string> allValues)
    {
        var filter = GetFilter(col);
        var scroll = popupPanel.Q<ScrollView>("filter-list");
        if (scroll != null)
        {
            scroll.Clear();
            PopulateFilterCheckboxList(scroll, col, allValues);
        }

        var selectAll = popupPanel.Q<Toggle>("select-all-toggle");
        if (selectAll != null)
        {
            var visible = FilterBySearch(allValues, filter.SearchText);
            bool allVisibleSelected = visible.Count > 0 && visible.All(v => filter.Selection.Contains(v));
            selectAll.SetValueWithoutNotify(allVisibleSelected);
        }

        UpdateFilterCount(popupPanel, col, allValues);
    }

    private void PopulateFilterCheckboxList(ScrollView scroll, SortColumn col, List<string> allValues)
    {
        var filter = GetFilter(col);
        var visible = FilterBySearch(allValues, filter.SearchText);

        if (visible.Count == 0)
        {
            var empty = new Label("No items match search.");
            ApplyFont(empty, size: 11);
            empty.style.color = new StyleColor(ColSubtleText);
            empty.style.paddingLeft = 4;
            empty.style.paddingTop = 4;
            scroll.Add(empty);
            return;
        }

        foreach (var value in visible)
        {
            var toggle = new Toggle
            {
                label = value,
                value = filter.Selection.Contains(value)
            };
            ApplyFont(toggle, size: 12);
            toggle.style.color = new StyleColor(ColTitleText);
            toggle.style.paddingLeft = 4;
            toggle.style.marginBottom = 2;
            toggle.style.whiteSpace = WhiteSpace.NoWrap;

            string capturedValue = value;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    filter.Selection.Add(capturedValue);
                else
                    filter.Selection.Remove(capturedValue);

                filter.Active = filter.Selection.Count < allValues.Count;
                UpdateFilterHeaderAppearance(col);

                var selectAll = scroll.parent?.Q<Toggle>("select-all-toggle");
                if (selectAll != null)
                {
                    var visItems = FilterBySearch(allValues, filter.SearchText);
                    bool allVis = visItems.Count > 0 && visItems.All(v => filter.Selection.Contains(v));
                    selectAll.SetValueWithoutNotify(allVis);
                }

                UpdateFilterCount(scroll.parent, col, allValues);
                RebuildRows();
            });

            scroll.Add(toggle);
        }
    }

    private void UpdateFilterCount(VisualElement popupPanel, SortColumn col, List<string> allValues)
    {
        var filter = GetFilter(col);
        var label = popupPanel?.Q<Label>("filter-count");
        if (label == null) return;

        int selected = filter.Selection.Count;
        int total = allValues.Count;
        label.text = filter.Active
            ? $"{selected} of {total} selected"
            : $"{total} items";
    }
}
