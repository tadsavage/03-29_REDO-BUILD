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
public class WorkQueuePanel
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
        private const float CheckboxWidth = 26f;
        private const float PaletteIdWidth = 92f;
        private const float ItemNumberWidth = 76f;
        private const float AreaWidth = 82f;
        private const float PriorityWidth = 62f;
        private const float RoleWidth = 156f;
        private const float TaskWidth = 96f;
        private const float RoleTaskGap = 12f;
        private const float StatusWidth = 88f;
        private const float LocationWidth = 72f;
        private const float OperatorWidth = 112f;
        private const float CustomerWidth = 116f;
        private const float OrderWidth = 104f;
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

    private enum RowPhase { Open, Available, Assigned, Staged, Loading, Loaded }
    private enum ActionMode { None, ReleaseToLane, ReleaseToLoading, CloseOut, Mixed }

    private readonly VisualElement _overlay;
    private readonly ScrollView _rowScroll;
    private readonly Label _bottomMessage;
    private readonly DropdownField _targetDropdown;
    private readonly Button _submitButton;

    private readonly HashSet<string> _checkedOrderIds = new();
    private List<string> _dropdownLanes = new();   // choice index -> "3A" style address, when in lane mode
    private List<int> _dropdownDoors = new();       // choice index -> door number, when in door mode
    private ActionMode _mode = ActionMode.None;

    private bool _visible;
    private enum SortColumn { PaletteId, ItemNumber, Area, Priority, Role, Task, Status, From, To, Operator, Customer, Order }
    private SortColumn _sortColumn = SortColumn.Priority;
    private bool _sortAscending;


    private string _liveSignature;

    public WorkQueuePanel(VisualElement root)
    {
        _overlay = Build(out _rowScroll, out _bottomMessage, out _targetDropdown, out _submitButton);
        root.Add(_overlay);
        _overlay.schedule.Execute(RefreshIfVisible).Every(250);
        Hide();
    }

    public bool IsVisible => _visible;
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
        modal.style.width = 1180;
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

        var titleSpacer = new VisualElement();
        titleSpacer.style.width = 28;
        titleBar.Add(titleSpacer);

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
        header.Add(HeaderCell("", CheckboxWidth));
        header.Add(HeaderCell("Palette ID", PaletteIdWidth, SortColumn.PaletteId));
        header.Add(HeaderCell("Item#", ItemNumberWidth, SortColumn.ItemNumber));
        header.Add(HeaderCell("Area", AreaWidth, SortColumn.Area, marginLeft: 12f));
        header.Add(HeaderCell("Priority", PriorityWidth, SortColumn.Priority));
        header.Add(HeaderCell("Role", RoleWidth, SortColumn.Role));
        header.Add(HeaderCell("Task", TaskWidth, SortColumn.Task, marginLeft: RoleTaskGap));
        header.Add(HeaderCell("Status", StatusWidth, SortColumn.Status));
        header.Add(HeaderCell("From", LocationWidth, SortColumn.From));
        header.Add(HeaderCell("To", LocationWidth, SortColumn.To));
        header.Add(HeaderCell("Operator", OperatorWidth, SortColumn.Operator));
        header.Add(HeaderCell("Customer", CustomerWidth, SortColumn.Customer));
        header.Add(HeaderCell("Order", OrderWidth, SortColumn.Order));
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
            return;
        }

        // Sorting helpers are declared below the row rebuild method.

        var rows = new List<(OrderData order, RowPhase phase, WorkTask task)>();
        foreach (var order in orderService.ActiveOrders)
        {
            var task = workQueue?.Tasks.FirstOrDefault(t => t.OrderId == order.OrderId && t.Type == WorkTaskType.OrderSelect);
            var phase = DeterminePhase(order, task);
            if (phase.HasValue) rows.Add((order, phase.Value, task));
        }
        rows = SortOrders(rows);

        // Drop checked ids that no longer resolve to a still-actionable row (submitted, or picked
        // up by a selector concurrently) so their checkmark doesn't linger looking "stuck".
        var stillActionable = rows.Where(r => r.phase == RowPhase.Open || r.phase == RowPhase.Staged || r.phase == RowPhase.Loaded)
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
            SortColumn.ItemNumber => rows.OrderBy(r => r.order.LineItems.FirstOrDefault()?.SkuId ?? ""),
            _ => rows.OrderBy(r => r.order.CreatedTimeMinute)
        };
        return (_sortAscending ? sorted : sorted.Reverse()).ToList();
    }

    private static string GetTaskItemNumber(WorkTask task)
    {
        if (string.IsNullOrEmpty(task.PalletId)) return "";
        if (!ServiceLocator.TryGet<InventoryService>(out var inventory) || inventory == null) return "";
        var pallet = inventory.GetPallet(task.PalletId);
        if (pallet == null) return "";
        var sku = inventory.AllSkus.FirstOrDefault(s => s.SkuId == pallet.SkuId);
        return sku != null ? sku.ItemNumber.ToString() : pallet.SkuId;
    }
    private RowPhase? DeterminePhase(OrderData order, WorkTask task)
    {
        if (order.Status == OrderData.OrderStatus.Loading) return RowPhase.Loading;
        if (order.Status == OrderData.OrderStatus.Staged) return RowPhase.Staged;
        if (order.Status == OrderData.OrderStatus.Shipped || order.Status == OrderData.OrderStatus.Cancelled) return null;
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
        row.style.paddingTop = 4; row.style.paddingBottom = 4; row.style.paddingLeft = 6;
        row.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColRowEven : ColRowOdd);
        row.style.flexShrink = 0;


        AddRowCell(row, "", CheckboxWidth, ColSubtleText);

        string itemNumber = "—";
        if (ServiceLocator.TryGet<InventoryService>(out var inventory) && inventory != null)
        {
            var pallet = inventory.GetPallet(task.PalletId);
            if (pallet != null)
            {
                var sku = inventory.AllSkus.FirstOrDefault(s => s.SkuId == pallet.SkuId);
                itemNumber = sku != null ? sku.ItemNumber.ToString() : pallet.SkuId;
            }
        }

        AddRowCell(row, ShortId(task.PalletId), PaletteIdWidth, ColTitleText);
        AddRowCell(row, itemNumber, ItemNumberWidth, ColTitleText);
        AddRowCell(row, AreaLabel(task.Area), AreaWidth, ColSubtleText, marginLeft: 12f);
        AddRowCell(row, task.Priority.ToString(), PriorityWidth, ColTitleText);
        AddRowCell(row, task.RequiredRole.DisplayName(), RoleWidth, ColSubtleText);
        AddRowCell(row, task.Type.ToString(), TaskWidth, ColTitleText, marginLeft: RoleTaskGap);
        AddRowCell(row, task.Status.ToString(), StatusWidth, ColStatusColor(task.Status), bold: true);
        AddRowCell(row, task.FromLocation ?? "—", LocationWidth, ColSubtleText);
        AddRowCell(row, task.ToLocation ?? "—", LocationWidth, ColSubtleText);
        AddRowCell(row, GetOperatorName(task.AssignedToEmployeeGuid), OperatorWidth, ColTitleText);
        AddRowCell(row, "—", CustomerWidth, ColSubtleText);
        AddRowCell(row, "—", OrderWidth, ColSubtleText);
        return row;
    }

    private VisualElement BuildRow(OrderData order, RowPhase phase, WorkTask task, int rowIndex)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 4; row.style.paddingBottom = 4; row.style.paddingLeft = 6;
        row.style.flexShrink = 0;

        row.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColRowEven : ColRowOdd);

        bool actionable = phase == RowPhase.Open || phase == RowPhase.Staged || phase == RowPhase.Loaded;
        var checkbox = new Toggle { value = _checkedOrderIds.Contains(order.OrderId) };
        checkbox.style.width = CheckboxWidth;
        checkbox.SetEnabled(actionable);
        checkbox.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue) _checkedOrderIds.Add(order.OrderId);
            else _checkedOrderIds.Remove(order.OrderId);
            RebuildBottomBar();
        });
        row.Add(checkbox);

        string itemNumber = order.LineItems.FirstOrDefault()?.SkuId ?? "—";
        string area = task != null ? AreaLabel(task.Area) : "—";
        string paletteId = task?.PalletId ?? "—";
        string role = task != null ? task.RequiredRole.DisplayName() : "—";
        string taskName = task != null ? task.Type.ToString() : "—";
        string from = task?.FromLocation ?? "—";
        string to = task?.ToLocation ?? "—";
        string operatorName = phase == RowPhase.Assigned ? GetOperatorName(task?.AssignedToEmployeeGuid) : "—";

        AddRowCell(row, ShortId(paletteId), PaletteIdWidth, ColSubtleText);
        AddRowCell(row, itemNumber, ItemNumberWidth, ColTitleText);
        AddRowCell(row, area, AreaWidth, ColSubtleText, marginLeft: 12f);
        AddRowCell(row, task != null ? task.Priority.ToString() : "—", PriorityWidth, ColTitleText);
        AddRowCell(row, role, RoleWidth, ColSubtleText);
        AddRowCell(row, taskName, TaskWidth, ColTitleText, marginLeft: RoleTaskGap);
        AddRowCell(row, PhaseLabel(phase), StatusWidth, PhaseColor(phase), bold: true);
        AddRowCell(row, from, LocationWidth, ColSubtleText);
        AddRowCell(row, to, LocationWidth, ColSubtleText);
        AddRowCell(row, operatorName, OperatorWidth, ColTitleText);
        AddRowCell(row, order.CustomerName, CustomerWidth, ColTitleText);
        AddRowCell(row, ShortId(order.OrderId), OrderWidth, ColSubtleText);
        return row;
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
        label.style.flexShrink = 0;
        label.style.color = new StyleColor(color);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        row.Add(label);
        return label;
    }

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
        if (customers.Count > 1)
        {
            SetBottomBar(ActionMode.Mixed, "Only one customer at a time — a staging lane / trailer load holds a single customer's orders for now.", new List<string> { "—" });
            return;
        }
        string customerId = customers[0];

        if (phases.Count != 1)
        {
            SetBottomBar(ActionMode.Mixed, "Selection mixes different stages — check only Open orders together, or only Staged orders together.", new List<string> { "—" });
            return;
        }

        if (phases[0] == RowPhase.Open)
        {
            ServiceLocator.TryGet<InventoryService>(out var inv);
            var lanes = LaneNamingService.AllLanes()
                .Where(l => inv != null && inv.LaneAllowsPicking(l.door, l.lane))
                .Where(l => StagingLaneAssignmentService.IsLaneAvailableFor(orderService, l.door, l.lane, customerId))
                .OrderBy(l => l.door).ThenBy(l => l.lane)
                .Select(l => $"{l.door}{l.lane}")
                .ToList();
            _dropdownLanes = lanes;

            if (lanes.Count == 0)
            {
                SetBottomBar(ActionMode.ReleaseToLane, "No available staging lane (every lane is either Inbound-only or already assigned to a different customer).", new List<string> { "—" }, enableTarget: false, enableSubmit: false);
            }
            else
            {
                SetBottomBar(ActionMode.ReleaseToLane, $"Release {checkedOrders.Count} order(s) for {checkedOrders[0].CustomerName} to a staging lane:", lanes);
            }
            return;
        }

        if (phases[0] == RowPhase.Loaded)
        {
            SetBottomBar(ActionMode.CloseOut, $"Close out {checkedOrders.Count} order(s) for {checkedOrders[0].CustomerName} — bills them and releases the trailer once its whole load is closed out:", new List<string> { "Close Out" }, enableTarget: false);
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
        int targetDoor = checkedOrders[0].AssignedDoorNumber;
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

        _submitButton.text = mode == ActionMode.ReleaseToLoading ? "Assign" : mode == ActionMode.CloseOut ? "Close Out" : "Submit Selection";
        _submitButton.SetEnabled(enableSubmit && (mode == ActionMode.ReleaseToLane || mode == ActionMode.ReleaseToLoading || mode == ActionMode.CloseOut) && choices.Count > 0 && choices[0] != "—");
        _submitButton.style.opacity = _submitButton.enabledSelf ? 1f : 0.5f;
    }

    private void OnSubmitClicked()
    {
        if (!ServiceLocator.TryGet<OrderService>(out var orderService) || orderService == null) return;
        var orderIds = _checkedOrderIds.ToList();
        if (orderIds.Count == 0) return;

        bool ok;
        if (_mode == ActionMode.ReleaseToLane)
        {
            int idx = _targetDropdown.index;
            if (idx < 0 || idx >= _dropdownLanes.Count) return;
            string address = _dropdownLanes[idx]; // "3A"
            int door = ParseLeadingDoor(address, out string lane);
            ok = orderService.ReleaseOrdersToLane(orderIds, door, lane);
        }
        else if (_mode == ActionMode.ReleaseToLoading)
        {
            int idx = _targetDropdown.index;
            if (idx < 0 || idx >= _dropdownDoors.Count) return;
            ok = orderService.ReleaseOrdersToLoading(orderIds, _dropdownDoors[idx]);
        }
        else if (_mode == ActionMode.CloseOut)
        {
            ok = orderService.CloseOutOrders(orderIds);
        }
        else return;

        if (ok) _checkedOrderIds.Clear();
        else UIToast.Show("Could not submit that selection — it may have changed. Refreshing.");

        RebuildRows();
    }

    private static int ParseLeadingDoor(string address, out string lane)
    {
        lane = address.Length > 0 ? address.Substring(address.Length - 1) : "";
        string doorPart = address.Length > 1 ? address.Substring(0, address.Length - 1) : "0";
        return int.TryParse(doorPart, out int door) ? door : 0;
    }
}
