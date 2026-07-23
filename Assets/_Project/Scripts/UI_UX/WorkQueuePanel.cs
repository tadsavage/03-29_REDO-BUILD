using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// "Work Queue" panel — lets the player release Open orders to a staging lane (so Order Selectors
/// can start picking them), and later release fully-Staged orders to a door (so a Loader/dock
/// stocker starts loading them onto the waiting trailer). Bound to the "7" key (see TopBarUI), same
/// programmatic UIToolkit shape/aesthetic as SlotAssignmentPanel (Lilita font, navy/blue palette,
/// DraggableWindow).
///
/// Rows are individual orders, grouped visually by customer (sorted by customer name). Each row's
/// Status column reflects whichever status is currently authoritative for that order: its
/// OrderSelect WorkTask's status (Open/Available/Assigned) while still being picked, or the order's
/// own OrderStatus (Staged/Loading) once picking is done — see DeterminePhase. Only Open and Staged
/// rows have an enabled checkbox; those are the two points where the player actually chooses
/// something (a lane, then a door). The bottom bar is contextual: checking only Open rows shows a
/// lane dropdown + Submit; checking only Staged rows shows a door dropdown + Submit; anything mixed
/// (different phases, or more than one customer — a lane/trailer holds one customer at a time for
/// now) disables submission with an explanatory message.
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
    private static readonly Color ColStatusLoading   = new Color(0x7E / 255f, 0xD6 / 255f, 0xC8 / 255f, 1f);

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

    private enum RowPhase { Open, Available, Assigned, Staged, Loading }
    private enum ActionMode { None, ReleaseToLane, ReleaseToLoading, Mixed }

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

    public WorkQueuePanel(VisualElement root)
    {
        _overlay = Build(out _rowScroll, out _bottomMessage, out _targetDropdown, out _submitButton);
        root.Add(_overlay);
        Hide();
    }

    public bool IsVisible => _visible;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.pickingMode = PickingMode.Position;
        RebuildRows();
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
        modal.style.minWidth = 700;
        modal.style.maxHeight = 680;

        // Title bar
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.alignItems = Align.Center;
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
        closeButton.style.width = 28; closeButton.style.height = 28;
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

        // Column header
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.paddingLeft = 6; header.style.paddingBottom = 4;
        header.Add(HeaderCell("", 26));
        header.Add(HeaderCell("Customer", 170));
        header.Add(HeaderCell("Order", 190));
        header.Add(HeaderCell("Status", 110));
        header.Add(HeaderCell("Operator", 140));
        modal.Add(header);

        rowScroll = new ScrollView();
        rowScroll.style.maxHeight = 420;
        rowScroll.style.marginBottom = 10;
        modal.Add(rowScroll);

        // Bottom bar
        var bottomBar = new VisualElement();
        bottomBar.style.flexDirection = FlexDirection.Row;
        bottomBar.style.alignItems = Align.Center;
        bottomBar.style.borderTopWidth = 2;
        bottomBar.style.borderTopColor = new StyleColor(ColBorder);
        bottomBar.style.paddingTop = 10;

        bottomMessage = new Label("Check some Open orders to release them to a staging lane.");
        ApplyFont(bottomMessage, size: 13);
        bottomMessage.style.color = new StyleColor(ColSubtleText);
        bottomMessage.style.flexGrow = 1;
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

    private static Label HeaderCell(string text, float width)
    {
        var l = new Label(text);
        ApplyFont(l, bold: true, size: 12);
        l.style.color = new StyleColor(ColSubtleText);
        l.style.width = width;
        return l;
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

        if (!ServiceLocator.TryGet<OrderService>(out var orderService) || orderService == null)
        {
            var empty = new Label("OrderService not available.");
            ApplyFont(empty, size: 13);
            empty.style.color = new StyleColor(ColSubtleText);
            _rowScroll.Add(empty);
            RebuildBottomBar();
            return;
        }
        ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue);

        var rows = new List<(OrderData order, RowPhase phase, WorkTask task)>();
        foreach (var order in orderService.ActiveOrders)
        {
            var task = workQueue?.Tasks.FirstOrDefault(t => t.OrderId == order.OrderId && t.Type == WorkTaskType.OrderSelect);
            var phase = DeterminePhase(order, task);
            if (phase.HasValue) rows.Add((order, phase.Value, task));
        }
        rows = rows.OrderBy(r => r.order.CustomerName).ThenBy(r => r.order.CreatedTimeMinute).ToList();

        // Drop checked ids that no longer resolve to a still-actionable row (submitted, or picked
        // up by a selector concurrently) so their checkmark doesn't linger looking "stuck".
        var stillActionable = rows.Where(r => r.phase == RowPhase.Open || r.phase == RowPhase.Staged)
            .Select(r => r.order.OrderId).ToHashSet();
        _checkedOrderIds.RemoveWhere(id => !stillActionable.Contains(id));

        if (rows.Count == 0)
        {
            var empty = new Label("No orders in the queue. Generate some test orders to get started.");
            ApplyFont(empty, size: 13);
            empty.style.color = new StyleColor(ColSubtleText);
            _rowScroll.Add(empty);
        }
        else
        {
            int i = 0;
            foreach (var (order, phase, task) in rows)
            {
                _rowScroll.Add(BuildRow(order, phase, task, i));
                i++;
            }
        }

        RebuildBottomBar();
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

    private VisualElement BuildRow(OrderData order, RowPhase phase, WorkTask task, int rowIndex)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 4; row.style.paddingBottom = 4; row.style.paddingLeft = 6;
        row.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColRowEven : ColRowOdd);

        bool actionable = phase == RowPhase.Open || phase == RowPhase.Staged;

        var checkbox = new Toggle { value = _checkedOrderIds.Contains(order.OrderId) };
        checkbox.style.width = 26;
        checkbox.SetEnabled(actionable);
        checkbox.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue) _checkedOrderIds.Add(order.OrderId);
            else _checkedOrderIds.Remove(order.OrderId);
            RebuildBottomBar();
        });
        row.Add(checkbox);

        var customerLabel = new Label(order.CustomerName);
        ApplyFont(customerLabel, size: 13);
        customerLabel.style.width = 170;
        customerLabel.style.color = new StyleColor(ColTitleText);
        row.Add(customerLabel);

        string laneNote = !string.IsNullOrEmpty(order.AssignedLane) ? $" [{order.AssignedDoorNumber}{order.AssignedLane}]" : "";
        var descLabel = new Label($"{order.OrderId.Substring(0, 8)} — {order.TotalUnits} units{laneNote}");
        ApplyFont(descLabel, size: 12);
        descLabel.style.width = 190;
        descLabel.style.color = new StyleColor(ColSubtleText);
        row.Add(descLabel);

        var statusLabel = new Label(PhaseLabel(phase));
        ApplyFont(statusLabel, bold: true, size: 13);
        statusLabel.style.width = 110;
        statusLabel.style.color = new StyleColor(PhaseColor(phase));
        row.Add(statusLabel);

        string operatorName = phase == RowPhase.Assigned ? GetOperatorName(task?.AssignedToEmployeeGuid) : "";
        var operatorLabel = new Label(operatorName);
        ApplyFont(operatorLabel, size: 12);
        operatorLabel.style.width = 140;
        operatorLabel.style.color = new StyleColor(ColTitleText);
        row.Add(operatorLabel);

        return row;
    }

    private static string PhaseLabel(RowPhase phase) => phase switch
    {
        RowPhase.Open => "Open",
        RowPhase.Available => "Available",
        RowPhase.Assigned => "Assigned",
        RowPhase.Staged => "Staged",
        RowPhase.Loading => "Loading",
        _ => "?",
    };

    private static Color PhaseColor(RowPhase phase) => phase switch
    {
        RowPhase.Open => ColStatusOpen,
        RowPhase.Available => ColStatusAvailable,
        RowPhase.Assigned => ColStatusAssigned,
        RowPhase.Staged => ColStatusStaged,
        RowPhase.Loading => ColStatusLoading,
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
            SetBottomBar(ActionMode.None, "Check some Open orders to release them to a staging lane, or Staged orders to release them to a door for loading.", new List<string> { "—" });
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

        if (phases[0] != RowPhase.Staged)
        {
            // A stray Available/Assigned/Loading row shouldn't be checkable at all (checkboxes are
            // disabled for those phases), but this guards the rare race where a row's phase changed
            // between the checkbox click and this rebuild instead of silently treating it as Staged.
            SetBottomBar(ActionMode.Mixed, "That order isn't ready for either action right now — refreshing.", new List<string> { "—" });
            return;
        }

        // Staged -> release to a door for loading. Only doors with a trailer currently sitting there.
        var doorsWithTrucks = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None)
            .Where(t => t.IsOutbound && t.DockedAt != null)
            .Select(t => t.DockedAt.DoorNumber)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
        _dropdownDoors = doorsWithTrucks;

        if (doorsWithTrucks.Count == 0)
        {
            SetBottomBar(ActionMode.ReleaseToLoading, "No door currently has a trailer waiting.", new List<string> { "—" }, enableTarget: false, enableSubmit: false);
        }
        else
        {
            var choices = doorsWithTrucks.Select(d => $"Door {d}").ToList();
            SetBottomBar(ActionMode.ReleaseToLoading, $"Release {checkedOrders.Count} order(s) for {checkedOrders[0].CustomerName} to a door for loading:", choices);
        }
    }

    private void SetBottomBar(ActionMode mode, string message, List<string> choices, bool enableTarget = true, bool enableSubmit = true)
    {
        _mode = mode;
        _bottomMessage.text = message;
        _bottomMessage.style.color = new StyleColor(mode == ActionMode.Mixed ? ColWarning : ColSubtleText);

        _targetDropdown.choices = choices.Count > 0 ? choices : new List<string> { "—" };
        _targetDropdown.SetValueWithoutNotify(_targetDropdown.choices[0]);
        _targetDropdown.SetEnabled(enableTarget && mode != ActionMode.None && mode != ActionMode.Mixed);

        _submitButton.text = mode == ActionMode.ReleaseToLoading ? "Assign" : "Submit Selection";
        _submitButton.SetEnabled(enableSubmit && (mode == ActionMode.ReleaseToLane || mode == ActionMode.ReleaseToLoading) && choices.Count > 0 && choices[0] != "—");
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
