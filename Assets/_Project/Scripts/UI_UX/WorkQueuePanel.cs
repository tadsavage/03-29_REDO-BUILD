using System.Collections.Generic;
using System.Linq;
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
    private static readonly Color ColBg         = new Color(20f / 255f, 28f / 255f, 38f / 255f, 0.92f);
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
        private const float CheckboxWidth = 26f;
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
        private const float OrderWidth = 104f;
        private const float FillRateWidth = 92f;
        private const float SelectAllWidth = 130f;
        private const float CancelSelectedWidth = 168f;
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
    private enum ActionMode { None, ReleaseToLane, ReleaseToStagesAuto, ReleaseToLoading, ReleaseToLoadingAuto, CloseOut, Mixed }

    private readonly VisualElement _overlay;
    private readonly ScrollView _rowScroll;
    private readonly Label _bottomMessage;
    private readonly DropdownField _targetDropdown;
    private readonly Button _submitButton;
    private Button _selectAllButton;
    private Button _cancelSelectedButton;

    private readonly HashSet<string> _checkedOrderIds = new();
    private List<int> _dropdownStageDoors = new(); // choice index -> door number, when in stage mode ("Stage 3")
    private List<int> _dropdownDoors = new();       // choice index -> door number, when in door mode
    private ActionMode _mode = ActionMode.None;

    private bool _visible;
    private enum SortColumn { PaletteId, ItemNumber, Area, Priority, Role, Task, Status, From, To, Operator, Customer, Order, FillRate }
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
    }
    private void RefreshIfVisible()
    {
        if (!_visible) return;
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
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0; overlay.style.bottom = 0;
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
        titleBar.style.height = 58;
        titleBar.style.alignItems = Align.Center;

        titleBar.style.width = StyleKeyword.Auto;
        titleBar.style.alignSelf = Align.Stretch;
        titleBar.style.marginTop = -14;

        titleBar.style.flexShrink = 0;
        titleBar.style.marginLeft = -16;
        titleBar.style.marginRight = -16;
        titleBar.style.paddingLeft = 16;
        titleBar.style.paddingRight = 16;
        titleBar.style.backgroundColor = new StyleColor(new Color(0x2B / 255f, 0x6C / 255f, 0x94 / 255f, 0.92f));
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

        var title = new Label("Work Queue");
        ApplyFont(title, bold: true, size: 26);
        title.style.color = new StyleColor(ColTitleText);
        title.style.flexGrow = 1;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(title);

        var closeButton = new Button(Hide) { text = "✕" };
        ApplyFont(closeButton, bold: true, size: 20);
        closeButton.style.width = 42;
        closeButton.style.height = 42;
        closeButton.style.minWidth = 42;
        closeButton.style.minHeight = 42;
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
        // Balances the two left-hand buttons so the title stays centred in the bar.
        var titleSpacer = new VisualElement();
        titleSpacer.style.width = SelectAllWidth + 8 + CancelSelectedWidth - 42;
        titleSpacer.style.flexShrink = 0;
        titleBar.Add(titleSpacer);

        titleBar.Add(closeButton);
        modal.Add(titleBar);

        new DraggableWindow(modal, titleBar, closeButton);
        new ResizableWindow(modal, minW: 900f, minH: 260f, grip: 10f, titleInset: 42f, allowVerticalResize: false);

        // Column headers. The dark modal styling remains the new queue's visual shell;
        // these columns expose the complete work-task record used by the old queue.
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.flexShrink = 0;
        header.style.overflow = Overflow.Hidden;
        header.style.paddingLeft = RowPaddingLeft;
        header.Add(HeaderCell("", CheckboxWidth));
        header.Add(BuildFilterHeader("Palette ID", PaletteIdWidth, SortColumn.PaletteId));
        header.Add(BuildFilterHeader("Item#", ItemNumberWidth, SortColumn.ItemNumber));
        header.Add(BuildFilterHeader("Area", AreaWidth, SortColumn.Area, marginLeft: 12f));
        header.Add(BuildFilterHeader("Priority", PriorityWidth, SortColumn.Priority));
        header.Add(BuildFilterHeader("Role", RoleWidth, SortColumn.Role));
        header.Add(BuildFilterHeader("Task", TaskWidth - TaskIndent, SortColumn.Task, marginLeft: RoleTaskGap + TaskIndent));
        header.Add(BuildFilterHeader("Status", StatusWidth, SortColumn.Status));
        header.Add(BuildFilterHeader("From", LocationWidth, SortColumn.From));
        header.Add(BuildFilterHeader("To", LocationWidth, SortColumn.To));
        header.Add(BuildFilterHeader("Operator", OperatorWidth, SortColumn.Operator));
        header.Add(BuildFilterHeader("Customer", CustomerWidth, SortColumn.Customer));
        header.Add(HeaderCell("Order", OrderWidth, SortColumn.Order));
        header.Add(HeaderCell("Fill Rate", FillRateWidth, SortColumn.FillRate));
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
        bottomBar.style.paddingTop = 10;

        bottomMessage = new Label("Check some Open orders to release them to a staging lane.");
        ApplyFont(bottomMessage, size: 13);
        bottomMessage.style.color = new StyleColor(ColSubtleText);
        bottomMessage.style.flexGrow = 1;
        bottomMessage.style.flexShrink = 1;
        bottomMessage.style.minWidth = 0;
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

    private Button HeaderCell(string text, float width, SortColumn? sortColumn = null, float marginLeft = 0f)
    {
        var header = new Button();
        ApplyFont(header, bold: true, size: 12);
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
        header.style.unityTextAlign = TextAnchor.MiddleLeft;
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

        ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue);
        var taskRows = workQueue?.Tasks
            .Where(t => t.Type != WorkTaskType.OrderSelect && t.Status != WorkTaskStatus.Complete)
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
        phase == RowPhase.Available || phase == RowPhase.NoStock;

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
            var task = workQueue?.Tasks.FirstOrDefault(t => t.OrderId == order.OrderId && t.Type == WorkTaskType.OrderSelect);
            var phase = DeterminePhase(order, task);
            if (phase.HasValue) rows.Add((order, phase.Value, task));
        }
        return ApplyOrderFilters(SortOrders(rows));
    }

    /// <summary>Checks every visible row with an enabled checkbox; if they're already all
    /// checked, unchecks them instead. Filtered-out rows are left untouched either way.</summary>
    private void ToggleSelectAllVisible()
    {
        var selectable = BuildVisibleOrderRows()
            .Where(r => IsActionable(r.phase))
            .Select(r => r.order.OrderId)
            .ToList();
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

    /// <summary>Greys the button out when nothing on screen is checkable, and flips its label
    /// once everything checkable is checked.</summary>
    private void UpdateSelectAllButton()
    {
        if (_selectAllButton == null) return;
        var selectable = BuildVisibleOrderRows()
            .Where(r => IsActionable(r.phase))
            .Select(r => r.order.OrderId)
            .ToList();

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
            SortColumn.Area => rows.OrderBy(r => r.task != null ? AreaLabel(r.task.Area) : ""),
            SortColumn.Priority => rows.OrderBy(r => r.task?.Priority ?? 0),
            SortColumn.Role => rows.OrderBy(r => r.task?.RequiredRole.DisplayName() ?? ""),
            SortColumn.Task => rows.OrderBy(r => r.task?.Type.ToString() ?? "OrderSelect"),
            SortColumn.Status => rows.OrderBy(r => PhaseLabel(r.phase)),
            SortColumn.From => rows.OrderBy(r => r.task?.FromLocation ?? ""),
            SortColumn.To => rows.OrderBy(r => r.task?.ToLocation ?? ""),
            SortColumn.Operator => rows.OrderBy(r => GetOperatorName(r.task?.AssignedToEmployeeGuid)),
            SortColumn.Customer => rows.OrderBy(r => r.order.CustomerName),
            SortColumn.Order => rows.OrderBy(r => r.order.OrderId),
            SortColumn.ItemNumber => rows.OrderBy(r => GetOrderItemNumber(r.order)),
            SortColumn.FillRate => rows.OrderBy(r => FillRatio(r.order)),
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
        return order.LineItems.FirstOrDefault()?.SkuId ?? "\u2014";
    }
    private RowPhase? DeterminePhase(OrderData order, WorkTask task)
    {
        if (order.Status == OrderData.OrderStatus.Loading) return RowPhase.Loading;
        if (order.Status == OrderData.OrderStatus.Staged) return RowPhase.Staged;
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


        AddRowCell(row, "", CheckboxWidth, ColSubtleText);

        string itemNumber = GetTaskItemNumber(task);

        AddRowCell(row, ShortId(task.PalletId), PaletteIdWidth, ColTitleText);
        AddRowCell(row, itemNumber, ItemNumberWidth, ColTitleText);
        AddRowCell(row, AreaLabel(task.Area), AreaWidth, ColSubtleText, marginLeft: 12f);
        AddRowCell(row, task.Priority.ToString(), PriorityWidth, ColTitleText);
        AddRowCell(row, task.RequiredRole.DisplayName(), RoleWidth, ColSubtleText);
        AddRowCell(row, task.Type.ToString(), TaskWidth - TaskIndent, ColTitleText, marginLeft: RoleTaskGap + TaskIndent);
        AddRowCell(row, task.Status.ToString(), StatusWidth, ColStatusColor(task.Status), bold: true);
        AddRowCell(row, task.FromLocation ?? "—", LocationWidth, ColSubtleText);
        AddRowCell(row, task.ToLocation ?? "—", LocationWidth, ColSubtleText);
        AddRowCell(row, GetOperatorName(task.AssignedToEmployeeGuid), OperatorWidth, ColTitleText);
        AddRowCell(row, "—", CustomerWidth, ColSubtleText);
        AddRowCell(row, "—", OrderWidth, ColSubtleText);
        AddRowCell(row, "—", FillRateWidth, ColSubtleText);
        return row;
    }

    private VisualElement BuildRow(OrderData order, RowPhase phase, WorkTask task, int rowIndex)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 4; row.style.paddingBottom = 4; row.style.paddingLeft = RowPaddingLeft;
        row.style.flexShrink = 0;

        row.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColRowEven : ColRowOdd);

        bool actionable = IsActionable(phase);
        var checkbox = new Toggle { value = _checkedOrderIds.Contains(order.OrderId) };
        // Toggle carries default theme margins; zero them so the checkbox slot is exactly
        // CheckboxWidth and every column downstream lines up with its header.
        checkbox.style.width = CheckboxWidth;
        checkbox.style.minWidth = CheckboxWidth;
        checkbox.style.flexShrink = 0;
        checkbox.style.marginLeft = 0;
        checkbox.style.marginRight = 0;
        checkbox.SetEnabled(actionable);
        checkbox.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue) _checkedOrderIds.Add(order.OrderId);
            else _checkedOrderIds.Remove(order.OrderId);
            RebuildBottomBar();
            UpdateTitleBarButtons();
        });
        row.Add(checkbox);

        string itemNumber = GetOrderItemNumber(order);
        string area = task != null ? AreaLabel(task.Area) : "—";
        string paletteId = task?.PalletId ?? "—";
        string role = task != null ? task.RequiredRole.DisplayName() : "—";
        string taskName = task != null ? task.Type.ToString() : "—";
        string from = OrderFromLocation(order, task, phase);
        string to = OrderToLocation(order, task, phase);
        string operatorName = phase == RowPhase.Assigned ? GetOperatorName(task?.AssignedToEmployeeGuid) : "—";

        AddRowCell(row, ShortId(paletteId), PaletteIdWidth, ColSubtleText);
        AddRowCell(row, itemNumber, ItemNumberWidth, ColTitleText);
        AddRowCell(row, area, AreaWidth, ColSubtleText, marginLeft: 12f);
        AddRowCell(row, task != null ? task.Priority.ToString() : "—", PriorityWidth, ColTitleText);
        AddRowCell(row, role, RoleWidth, ColSubtleText);
        AddRowCell(row, taskName, TaskWidth - TaskIndent, ColTitleText, marginLeft: RoleTaskGap + TaskIndent);
        AddRowCell(row, PhaseLabel(phase), StatusWidth, PhaseColor(phase), bold: true);
        AddRowCell(row, from, LocationWidth, ColSubtleText);
        AddRowCell(row, to, LocationWidth, ColSubtleText);
        AddRowCell(row, operatorName, OperatorWidth, ColTitleText);
        AddRowCell(row, order.CustomerName, CustomerWidth, ColTitleText);
        AddRowCell(row, ShortId(order.OrderId), OrderWidth, ColSubtleText);
        AddRowCell(row, FillRateText(order), FillRateWidth, FillRateColor(order), bold: true);
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

    private static Color ColStatusColor(WorkTaskStatus status) => status switch
    {
        WorkTaskStatus.Open => ColStatusOpen,
        WorkTaskStatus.Available => ColStatusAvailable,
        WorkTaskStatus.Assigned => ColStatusAssigned,
        WorkTaskStatus.Complete => ColStatusLoaded,
        _ => ColSubtleText
    };

    private static Label AddRowCell(VisualElement row, string text, float width, Color color, bool bold = false, float marginLeft = 0f)
    {
        var label = new Label(text ?? "—");
        ApplyFont(label, bold, 12);
        label.style.width = width;
        label.style.minWidth = width;
        label.style.marginLeft = marginLeft;
        label.style.marginRight = 0;
        label.style.flexShrink = 0;
        label.style.color = new StyleColor(color);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        row.Add(label);
        return label;
    }

    /// <summary>"16 / 23" — cases picked over cases ordered. The raw material for the service-level
    /// KPI: an order that ships short still ships, and this is the record of by how much.</summary>
    private static string FillRateText(OrderData order)
        => order == null ? "—" : $"{order.TotalUnitsPicked} / {order.TotalUnits}";

    /// <summary>Green at 100%, amber short, red for nothing picked — only once picking has actually
    /// started, so an unreleased order reads as neutral rather than a failure.</summary>
    private static Color FillRateColor(OrderData order)
    {
        if (order == null || order.TotalUnits <= 0) return ColSubtleText;
        if (order.TotalUnitsPicked >= order.TotalUnits) return ColStatusLoaded;
        if (order.TotalUnitsPicked > 0) return ColStatusAssigned;
        return ColSubtleText;
    }

    private static float FillRatio(OrderData order)
        => order == null || order.TotalUnits <= 0 ? 0f : (float)order.TotalUnitsPicked / order.TotalUnits;

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
            SetBottomBar(ActionMode.None, "Check some Open orders to release them to a staging lane, Staged orders to release them to a door for loading, or Loaded orders to close them out.", new List<string> { "—" });
            return;
        }

        ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue);
        var phases = checkedOrders
            .Select(o => DeterminePhase(o, workQueue?.Tasks.FirstOrDefault(t => t.OrderId == o.OrderId && t.Type == WorkTaskType.OrderSelect)))
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

        // Available (released, waiting on a selector) and No Stock (a legacy backorder record) have no
        // Submit action of their own — Cancel Selected is the only thing that acts on them.
        if (phases[0] == RowPhase.Available || phases[0] == RowPhase.NoStock)
        {
            string what = phases[0] == RowPhase.Available
                ? "already released and waiting for an Order Selector to claim them"
                : "short of stock and stuck — nothing was ever picked for them";
            SetBottomBar(ActionMode.None, $"These {checkedOrders.Count} order(s) are {what}. Use Cancel Selected to call them off.",
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

            // The player picks a STAGE (a door's whole set of staging lanes), not an individual lane.
            // Staging starts in that door's first lane and overflows into the next as each fills, so
            // offering 1A/1B/1C separately just asked the player to make a choice the system now makes
            // for itself. One entry per door that has at least one pickable lane and isn't already
            // held by a different customer.
            // A stage is offered only if it has a pickable lane, isn't held by another customer, AND
            // has no inbound activity (received pallets sitting in its lanes, or an inbound trailer
            // docked at the door). Hiding those is what stops a stage being double-assigned — the
            // selector staging onto tiles a dock stocker is still filling.
            var stageDoors = LaneNamingService.AllLanes()
                .Select(l => l.door)
                .Distinct()
                .Where(d => StagingLaneAssignmentService.IsStageSelectableFor(orderService, inv, d, customerId))
                .OrderBy(d => d)
                .ToList();
            _dropdownStageDoors = stageDoors;

            if (stageDoors.Count == 0)
            {
                // Say WHY, per door. A blank dropdown with a generic message is impossible to act on —
                // the player can be staring at an empty dock with no idea what's blocking it.
                var reasons = LaneNamingService.AllLanes()
                    .Select(l => l.door)
                    .Distinct()
                    .OrderBy(d => d)
                    .Select(d =>
                    {
                        StagingLaneAssignmentService.IsStageSelectableFor(orderService, inv, d, customerId, out string why);
                        return $"Stage {d}: {why}";
                    })
                    .ToList();

                string detail = reasons.Count > 0 ? string.Join("   •   ", reasons) : "no staging lanes exist yet";
                Debug.LogWarning($"[WorkQueuePanel] No stage available for {checkedOrders[0].CustomerName} — {string.Join(" | ", reasons)}");
                SetBottomBar(ActionMode.ReleaseToLane, $"No available stage — {detail}", new List<string> { "—" }, enableTarget: false, enableSubmit: false);
            }
            else
            {
                SetBottomBar(ActionMode.ReleaseToLane, $"Release {checkedOrders.Count} order(s) for {checkedOrders[0].CustomerName} to a stage:",
                    stageDoors.Select(d => $"Stage {d}").ToList());
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
                           : "Submit Selection";
        _submitButton.SetEnabled(enableSubmit && (mode == ActionMode.ReleaseToLane || mode == ActionMode.ReleaseToStagesAuto || mode == ActionMode.ReleaseToLoading || mode == ActionMode.ReleaseToLoadingAuto || mode == ActionMode.CloseOut) && choices.Count > 0 && choices[0] != "—");
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
            if (idx < 0 || idx >= _dropdownStageDoors.Count) return;
            ok = orderService.ReleaseOrdersToStage(orderIds, _dropdownStageDoors[idx]);
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

    // REMOVED: ParseLeadingDoor(). It split a "3A" dropdown choice back into door + lane, which the
    // stage dropdown no longer produces — the choice is a door number now and the lane is resolved by
    // OrderService.ReleaseOrdersToStage.

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
            SortColumn.PaletteId => ShortId(task?.PalletId ?? "\u2014"),
            SortColumn.ItemNumber => GetOrderItemNumber(order),
            SortColumn.Area => task != null ? AreaLabel(task.Area) : "\u2014",
            SortColumn.Priority => task != null ? task.Priority.ToString() : "\u2014",
            SortColumn.Role => task != null ? task.RequiredRole.DisplayName() : "\u2014",
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
                if (task.Type == WorkTaskType.OrderSelect) continue;
                values.Add(GetTaskCellValue(col, task));
            }
        }

        if (ServiceLocator.TryGet<OrderService>(out var orderService) && orderService != null)
        {
            ServiceLocator.TryGet<WorkQueueSystem>(out var wq);
            foreach (var order in orderService.ActiveOrders)
            {
                var task = wq?.Tasks.FirstOrDefault(t => t.OrderId == order.OrderId && t.Type == WorkTaskType.OrderSelect);
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
    private VisualElement BuildFilterHeader(string text, float width, SortColumn col, float marginLeft = 0f)
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
        ApplyFont(filter.HeaderLabel, bold: true, size: 12);
        filter.HeaderLabel.style.color = new StyleColor(ColSubtleText);
        filter.HeaderLabel.style.flexGrow = 1;
        container.Add(filter.HeaderLabel);

        filter.HeaderIcon = new Label("\u25BC");
        ApplyFont(filter.HeaderIcon, size: 9);
        filter.HeaderIcon.style.color = new StyleColor(ColSubtleText);
        filter.HeaderIcon.style.marginLeft = 2;
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
