using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// DEBUG-ONLY floating window to manually drive & observe CHUNK 1 (Inbound). Spawn a randomized
/// delivery truck, watch it queue/inspect/dock, and read live shipment + work-queue state.
///
/// Rebuilt 2026-07-05 from the old IMGUI (OnGUI) panel into a UI-Toolkit window matching the rest of
/// the game's UI (Lilita One, navy/blue chrome): a conventional title bar with a red ✕, drag by the
/// title bar (<see cref="DraggableWindow"/>), resize by the edges (<see cref="ResizableWindow"/>),
/// expandable per-shipment pivot rows, and a scrollable column-formatted work queue.
///
/// Self-bootstrapping (like PalletInventoryTracker) — no scene setup. Borrows the HUD's PanelSettings
/// at runtime so it scales with the rest of the UI. P = spawn a truck. 7 (number row) = show/hide the
/// window (the red ✕ hides it too). Prototype-testing only; remove or gate behind a build flag before shipping.
/// </summary>
public class InboundTestPanel : MonoBehaviour
{
    private static InboundTestPanel _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[InboundTestPanel]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<InboundTestPanel>();
    }

    // ── Palette (matches SlotAssignmentPanel / the navy-and-blue HUD aesthetic) ────────────────
    private static readonly Color ColWindowBg   = new Color(6f / 255f, 10f / 255f, 18f / 255f, 0.80f);   // darker + transparent
    private static readonly Color ColBorder     = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleBar    = new Color(18f / 255f, 26f / 255f, 42f / 255f, 0.95f);
    private static readonly Color ColTitleText  = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColSubtleText = new Color(0x8A / 255f, 0xA6 / 255f, 0xBE / 255f, 1f);
    private static readonly Color ColSectionText = new Color(0xD8 / 255f, 0xE8 / 255f, 0xF4 / 255f, 1f);
    private static readonly Color ColRedX        = new Color(0.75f, 0.20f, 0.16f, 1f);
    private static readonly Color ColRedXHover   = new Color(0.90f, 0.28f, 0.22f, 1f);
    private static readonly Color ColBtn         = new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 1f);
    private static readonly Color ColBtnEdge     = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColBtnHover    = new Color(0x5B / 255f, 0xA6 / 255f, 0xD8 / 255f, 1f);
    private static readonly Color ColShipRow     = new Color(28f / 255f, 40f / 255f, 54f / 255f, 0.85f);
    private static readonly Color ColShipRowHi   = new Color(40f / 255f, 56f / 255f, 74f / 255f, 0.90f);
    private static readonly Color ColShipDetailBg = new Color(12f / 255f, 20f / 255f, 30f / 255f, 0.9f);

    // Work queue: royal blue background, bright yellow text (per spec).
    private static readonly Color ColQueueBg      = new Color(0x1E / 255f, 0x3A / 255f, 0x8C / 255f, 0.92f); // royal blue field
    private static readonly Color ColQueueHeaderBg = new Color(0x14 / 255f, 0x28 / 255f, 0x66 / 255f, 1f);
    private static readonly Color ColQueueRowA    = new Color(0x27 / 255f, 0x49 / 255f, 0xA8 / 255f, 0.55f);
    private static readonly Color ColQueueRowB    = new Color(0x1E / 255f, 0x3A / 255f, 0x8C / 255f, 0.45f);
    private static readonly Color ColQueueText    = new Color(1f, 0.90f, 0.16f, 1f);      // bright yellow
    private static readonly Color ColQueueHeaderText = new Color(1f, 0.96f, 0.55f, 1f);   // paler yellow for headings

    // Work-queue column widths (header & data rows share these so columns line up).
    private const float WPallet = 96f, WItem = 72f, WArea = 70f, WPri = 48f, WRole = 52f, WTask = 90f, WFrom = 96f, WTo = 96f;

    private UIDocument _doc;
    private VisualElement _window;
    private VisualElement _shipmentsList;
    private VisualElement _queueHeaderRow;
    private ScrollView _queueScroll;
    private bool _built;

    private int _testCounter;
    private readonly HashSet<string> _expanded = new();
    private string _shipSig = "";
    private string _queueSig = "";

    // Work queue sorting
    private enum SortColumn { Pallet, Item, Area, Priority, Role, Task, From, To }
    private SortColumn _sortColumn = SortColumn.Pallet;
    private bool _sortAscending = true;

    // ── Font ──────────────────────────────────────────────────────────────────────────────────
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

    private static void ApplyFont(VisualElement el, int size = -1, bool bold = false)
    {
        var f = LilitaFont();
        if (f != null) el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
        if (bold) el.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (size > 0) el.style.fontSize = size;
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (!_built)
        {
            TryBuild();
            if (!_built) return;
        }

        if (Keyboard.current != null && !UIModalGuard.IsCapturing)
        {
            if (Keyboard.current.digit7Key.wasPressedThisFrame) ToggleWindow();
        }

        RefreshQueue();
    }

    private void ToggleWindow()
    {
        if (_window == null) return;
        bool visible = _window.resolvedStyle.display != DisplayStyle.None;
        _window.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
    }

    // Borrow the HUD's PanelSettings so we scale with the rest of the UI; wait until one exists.
    private void TryBuild()
    {
        var docs = FindObjectsByType<UIDocument>();
        PanelSettings ps = null;
        foreach (var d in docs)
            if (d != null && d.panelSettings != null) { ps = d.panelSettings; break; }
        if (ps == null) return;

        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = ps;
        _doc.sortingOrder = 150; // above the HUD (DevHud is 100)

        var root = _doc.rootVisualElement;
        if (root == null) { Destroy(_doc); _doc = null; return; }
        root.pickingMode = PickingMode.Ignore; // only the window blocks input, not the whole screen
        root.Clear();
        BuildWindow(root);
        _window.style.display = DisplayStyle.None; // start hidden
        _built = true;
    }

    // ── Window chrome ─────────────────────────────────────────────────────────────────────────
    private void BuildWindow(VisualElement root)
    {
        _window = new VisualElement { name = "inbound-test-window" };
        _window.style.position = Position.Absolute;
        _window.style.left = 100;
        _window.style.top = 100; // clear the TopBar
        _window.style.width = 640;
        _window.style.height = 540;
        _window.style.minWidth = 260;
        _window.style.minHeight = 220;
        _window.style.backgroundColor = ColWindowBg;
        SetBorder(_window, ColBorder, 2f);
        SetRadius(_window, 8f);
        _window.style.overflow = Overflow.Hidden;

        // Title bar (drag handle) with red ✕.
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.justifyContent = Justify.SpaceBetween;
        titleBar.style.height = 30;
        titleBar.style.paddingLeft = 10;
        titleBar.style.backgroundColor = ColTitleBar;
        titleBar.style.borderBottomWidth = 2;
        titleBar.style.borderBottomColor = ColBorder;
        titleBar.style.borderTopLeftRadius = 6;
        titleBar.style.borderTopRightRadius = 6;

        var title = new Label("WORK QUEUE");
        ApplyFont(title, size: 22, bold: true);  // 50% bigger font
        title.style.color = ColTitleText;
        title.style.letterSpacing = 1f;
        titleBar.Add(title);

        var closeBtn = new Button(() => _window.style.display = DisplayStyle.None) { text = "✕" };
        ApplyFont(closeBtn, size: 14, bold: true);
        closeBtn.style.width = 30;
        closeBtn.style.height = 30;
        closeBtn.style.marginTop = 0; closeBtn.style.marginBottom = 0;
        closeBtn.style.marginLeft = 0; closeBtn.style.marginRight = 0;
        closeBtn.style.paddingLeft = 0; closeBtn.style.paddingRight = 0;
        closeBtn.style.paddingTop = 0; closeBtn.style.paddingBottom = 0;
        closeBtn.style.backgroundColor = ColRedX;
        closeBtn.style.color = Color.white;
        closeBtn.style.borderTopWidth = closeBtn.style.borderBottomWidth =
            closeBtn.style.borderLeftWidth = closeBtn.style.borderRightWidth = 0;
        closeBtn.style.borderTopLeftRadius = 0; closeBtn.style.borderBottomLeftRadius = 0;
        closeBtn.style.borderBottomRightRadius = 0; closeBtn.style.borderTopRightRadius = 6;
        closeBtn.RegisterCallback<PointerEnterEvent>(_ => closeBtn.style.backgroundColor = ColRedXHover);
        closeBtn.RegisterCallback<PointerLeaveEvent>(_ => closeBtn.style.backgroundColor = ColRedX);
        titleBar.Add(closeBtn);

        _window.Add(titleBar);

        // Body (padded content area).
        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.paddingLeft = 12; body.style.paddingRight = 12;
        body.style.paddingTop = 10; body.style.paddingBottom = 12;
        _window.Add(body);

        // Work Queue section (no title - already shown in ribbon).
        _queueHeaderRow = BuildQueueHeaderRow();
        body.Add(_queueHeaderRow);

        _queueScroll = new ScrollView();
        _queueScroll.style.flexGrow = 1;
        _queueScroll.style.backgroundColor = ColQueueBg;
        _queueScroll.style.borderBottomLeftRadius = _queueScroll.style.borderBottomRightRadius = 6;
        body.Add(_queueScroll);

        root.Add(_window);

        // Drag by the title bar, resize by the edges.
        new DraggableWindow(_window, titleBar, closeBtn);
        new ResizableWindow(_window, minW: 260f, minH: 220f, grip: 8f, titleInset: 32f);

        // Force a first data pass so it isn't empty until a signature changes.
        _shipSig = _queueSig = "\0";
    }

    private Label SectionHeader(string text)
    {
        var l = new Label(text);
        ApplyFont(l, size: 17, bold: true);
        l.style.color = ColSectionText;
        l.style.marginBottom = 5;
        return l;
    }

    private Button StyleButton(Button b)
    {
        ApplyFont(b, size: 15, bold: true);
        b.style.backgroundColor = ColBtn;
        b.style.color = Color.white;
        b.style.whiteSpace = WhiteSpace.Normal;
        b.style.borderTopWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
        b.style.borderBottomWidth = 3;
        b.style.borderBottomColor = ColBtnEdge;
        SetRadius(b, 8f);
        b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = ColBtnHover);
        b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = ColBtn);
        return b;
    }

    // ── Spawn ───────────────────────────────────────────────────────────────────────────────
    private void SpawnTestTruck()
    {
        if (!ServiceLocator.TryGet<ShipmentService>(out var shipmentService) || shipmentService == null)
        {
            Debug.LogError("[InboundTestPanel] ShipmentService not available.");
            return;
        }
        if (!ServiceLocator.TryGet<InventoryService>(out var inventoryService) || inventoryService == null)
        {
            Debug.LogError("[InboundTestPanel] InventoryService not available.");
            return;
        }

        _testCounter++;
        var items = RandomDeliveryGenerator.GenerateFullTrailerLoad(inventoryService);
        if (items.Count == 0)
        {
            Debug.LogWarning("[InboundTestPanel] RandomDeliveryGenerator produced no pallets — check that SKUs have committed Ti/Hi.");
            return;
        }
        shipmentService.CreatePurchaseOrder($"SUPP_TEST{_testCounter}", "Test Supplier", items);
    }

    // ── Shipments (expandable pivot rows) ─────────────────────────────────────────────────────
    private void RefreshShipments()
    {
        if (_shipmentsList == null) return;
        if (!ServiceLocator.TryGet<ShipmentService>(out var svc) || svc == null) return;

        var shipments = svc.PendingShipments;
        var sb = new StringBuilder();
        foreach (var s in shipments) sb.Append(s.PONumber).Append(s.Status).Append(s.LineItems.Count).Append('|');
        string sig = sb.ToString();
        if (sig == _shipSig) return; // nothing changed → keep expansion/scroll intact
        _shipSig = sig;

        _shipmentsList.Clear();
        if (shipments.Count == 0)
        {
            var none = new Label("(no shipments)");
            ApplyFont(none, size: 13);
            none.style.color = ColSubtleText;
            _shipmentsList.Add(none);
            return;
        }

        InventoryService inv = null;
        ServiceLocator.TryGet(out inv);

        foreach (var s in shipments)
            _shipmentsList.Add(BuildShipmentRow(s, inv));
    }

    private VisualElement BuildShipmentRow(ShipmentData s, InventoryService inv)
    {
        var container = new VisualElement();
        container.style.marginBottom = 4;

        bool expanded = _expanded.Contains(s.PONumber);

        // Header (clickable pivot toggle).
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.paddingTop = 5; header.style.paddingBottom = 5;
        header.style.paddingLeft = 8; header.style.paddingRight = 8;
        header.style.backgroundColor = ColShipRow;
        SetRadius(header, 5f);

        var caret = new Label(expanded ? "▾" : "▸");
        ApplyFont(caret, size: 13, bold: true);
        caret.style.color = ColTitleText;
        caret.style.width = 16;
        header.Add(caret);

        var summary = new Label($"PO {s.PONumber}   {s.SupplierName}   [{s.Status}]   {s.LineItems.Count} pallets");
        ApplyFont(summary, size: 14, bold: true);
        // Change PO number color to light red if departed
        summary.style.color = s.Status == GameCore.Inventory.ShipmentData.ShipmentStatus.Departed
            ? new Color(1f, 0.7f, 0.7f)  // Light red
            : ColTitleText;
        summary.style.flexGrow = 1;
        header.Add(summary);

        var detail = BuildShipmentDetail(s, inv);
        detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

        header.RegisterCallback<PointerEnterEvent>(_ => header.style.backgroundColor = ColShipRowHi);
        header.RegisterCallback<PointerLeaveEvent>(_ => header.style.backgroundColor = ColShipRow);
        header.RegisterCallback<PointerDownEvent>(_ =>
        {
            bool nowExpanded = detail.style.display == DisplayStyle.None;
            detail.style.display = nowExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            caret.text = nowExpanded ? "▾" : "▸";
            if (nowExpanded) _expanded.Add(s.PONumber); else _expanded.Remove(s.PONumber);
        });

        container.Add(header);
        container.Add(detail);
        return container;
    }

    // Pivot detail: line items grouped by SKU with Ti/Hi/pallet-height + pertinent data.
    private VisualElement BuildShipmentDetail(ShipmentData s, InventoryService inv)
    {
        var wrap = new VisualElement();
        wrap.style.backgroundColor = ColShipDetailBg;
        wrap.style.paddingTop = 4; wrap.style.paddingBottom = 6;
        wrap.style.paddingLeft = 6; wrap.style.paddingRight = 6;
        wrap.style.borderBottomLeftRadius = wrap.style.borderBottomRightRadius = 5;

        wrap.Add(PivotRow("Item #", "Description", "Plts", "Cases", "Ti", "Hi", "Plt Ht", isHeader: true, icon: null));

        var groups = s.LineItems.GroupBy(li => li.SkuId);
        foreach (var g in groups)
        {
            var sku = inv != null ? inv.GetSkuData(g.Key) : null;
            string desc = sku != null ? sku.ItemDescription : "—";
            string ti = sku != null ? sku.Ti.ToString() : "—";
            string hi = sku != null ? sku.Hi.ToString() : "—";
            string ph = sku != null ? $"{sku.PltHeight:0.00}m" : "—";
            int plts = g.Count();
            int cases = g.Sum(li => li.Quantity);
            Sprite icon = sku != null ? sku.Icon : null;
            wrap.Add(PivotRow(g.Key, desc, plts.ToString(), cases.ToString(), ti, hi, ph, isHeader: false, icon: icon));
        }
        return wrap;
    }

    private VisualElement PivotRow(string item, string desc, string plts, string cases, string ti, string hi, string ph, bool isHeader, Sprite icon = null)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 2; row.style.paddingBottom = 2;

        Color c = isHeader ? ColSubtleText : ColTitleText;
        row.Add(Cell(item, 72, c, isHeader, TextAnchor.MiddleLeft));
        row.Add(IconCell(icon, 28, isHeader));
        var d = Cell(desc, 0, c, isHeader, TextAnchor.MiddleLeft);
        d.style.flexGrow = 1; d.style.flexBasis = 120; d.style.overflow = Overflow.Hidden;
        row.Add(d);
        row.Add(Cell(plts,  50, c, isHeader, TextAnchor.MiddleCenter));
        row.Add(Cell(cases, 54, c, isHeader, TextAnchor.MiddleCenter));
        row.Add(Cell(ti,    38, c, isHeader, TextAnchor.MiddleCenter));
        row.Add(Cell(hi,    38, c, isHeader, TextAnchor.MiddleCenter));
        row.Add(Cell(ph,    62, c, isHeader, TextAnchor.MiddleCenter));
        return row;
    }

    // ── Work queue (columns, one record per line, scrollable) ─────────────────────────────────
    private VisualElement BuildQueueHeaderRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.backgroundColor = ColQueueHeaderBg;
        row.style.paddingTop = 4; row.style.paddingBottom = 4;
        row.style.paddingLeft = 6; row.style.paddingRight = 6;
        row.style.borderTopLeftRadius = row.style.borderTopRightRadius = 6;

        AddSortableHeaderCell(row, "Pallet ID", WPallet, SortColumn.Pallet);
        AddSortableHeaderCell(row, "Item #",    WItem,   SortColumn.Item);
        AddSortableHeaderCell(row, "Area",      WArea,   SortColumn.Area);
        AddSortableHeaderCell(row, "Pri",       WPri,    SortColumn.Priority);
        AddSortableHeaderCell(row, "Role",      WRole,   SortColumn.Role);
        AddSortableHeaderCell(row, "Task",      WTask,   SortColumn.Task);
        AddSortableHeaderCell(row, "From",      WFrom,   SortColumn.From);
        AddSortableHeaderCell(row, "To",        WTo,     SortColumn.To);
        return row;
    }

    private void AddSortableHeaderCell(VisualElement row, string label, float width, SortColumn col)
    {
        var btn = new Button(() => ToggleSort(col));
        btn.text = _sortColumn == col ? (label + (_sortAscending ? " ↑" : " ↓")) : label;
        ApplyFont(btn, size: 12);
        btn.style.color = ColQueueHeaderText;
        btn.style.backgroundColor = ColQueueHeaderBg;
        btn.style.width = width;
        btn.style.paddingLeft = btn.style.paddingRight = 4;
        btn.style.paddingTop = btn.style.paddingBottom = 0;
        btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 0;
        btn.style.unityTextAlign = label.Contains("#") || label == "Task" || label == "From" || label == "To"
            ? TextAnchor.MiddleLeft
            : TextAnchor.MiddleCenter;
        btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = new Color(0.2f, 0.3f, 0.5f, 1f));
        btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = ColQueueHeaderBg);
        row.Add(btn);
    }

    private void ToggleSort(SortColumn col)
    {
        if (_sortColumn == col)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = col;
            _sortAscending = true;
        }
        _queueSig = ""; // Force refresh
    }

    private void ApplyQueueSort(List<WorkTask> tasks)
    {
        switch (_sortColumn)
        {
            case SortColumn.Pallet:
                tasks.Sort((a, b) => CompareString(a.PalletId, b.PalletId));
                break;
            case SortColumn.Item:
                tasks.Sort((a, b) => CompareString(a.PalletId, b.PalletId));
                break;
            case SortColumn.Area:
                tasks.Sort((a, b) => a.Area.CompareTo(b.Area));
                break;
            case SortColumn.Priority:
                tasks.Sort((a, b) => CompareInt(100, 100)); // all same priority
                break;
            case SortColumn.Role:
                tasks.Sort((a, b) => a.RequiredRole.CompareTo(b.RequiredRole));
                break;
            case SortColumn.Task:
                tasks.Sort((a, b) => a.Type.CompareTo(b.Type));
                break;
            case SortColumn.From:
                tasks.Sort((a, b) => CompareString(a.FromLocation, b.FromLocation));
                break;
            case SortColumn.To:
                tasks.Sort((a, b) => CompareString(a.ToLocation, b.ToLocation));
                break;
        }
    }

    private int CompareString(string a, string b)
    {
        int cmp = string.Compare(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
        return _sortAscending ? cmp : -cmp;
    }

    private int CompareInt(int a, int b)
    {
        int cmp = a.CompareTo(b);
        return _sortAscending ? cmp : -cmp;
    }

    private void RefreshQueue()
    {
        if (_queueScroll == null) return;
        if (!ServiceLocator.TryGet<WorkQueueSystem>(out var queue) || queue == null) return;

        var tasks = new List<WorkTask>(queue.Tasks);
        var sb = new StringBuilder();
        foreach (var t in tasks) sb.Append(t.TaskId).Append(t.Status).Append('|');
        string sig = sb.ToString();
        if (sig == _queueSig) return;
        _queueSig = sig;

        // Apply sorting
        ApplyQueueSort(tasks);

        _queueScroll.Clear();
        if (tasks.Count == 0)
        {
            var none = new Label("(no pending tasks)");
            ApplyFont(none, size: 13);
            none.style.color = ColQueueText;
            none.style.paddingLeft = 8; none.style.paddingTop = 6; none.style.paddingBottom = 6;
            _queueScroll.Add(none);
            return;
        }

        InventoryService inv = null;
        ServiceLocator.TryGet(out inv);

        int i = 0;
        foreach (var t in tasks)
        {
            _queueScroll.Add(BuildQueueRow(t, inv, i));
            i++;
        }
    }

    private VisualElement BuildQueueRow(WorkTask t, InventoryService inv, int index)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.paddingTop = 3; row.style.paddingBottom = 3;
        row.style.paddingLeft = 6; row.style.paddingRight = 6;
        row.style.backgroundColor = index % 2 == 0 ? ColQueueRowA : ColQueueRowB;

        string pallet = string.IsNullOrEmpty(t.PalletId) ? "" : Short(t.PalletId);
        string item = ItemNumberFor(t, inv);
        string area = AreaLabel(t.Area);
        string role = RoleCode(t.RequiredRole);
        string task = TaskLabel(t);

        // From/To logic based on task type
        string from = "—";
        string to = "—";

        if (t.Type == WorkTaskType.Receive)
        {
            // For receiving: no From/To, location is tracked separately
            from = "—";
            to = "—";
        }
        else if (t.Type == WorkTaskType.Putaway)
        {
            // For putaway: From = current location, To = destination
            from = string.IsNullOrEmpty(t.FromLocation) ? "—" : t.FromLocation;
            to = string.IsNullOrEmpty(t.ToLocation) ? "—" : t.ToLocation;
        }
        else
        {
            // For other tasks: use stored values
            from = string.IsNullOrEmpty(t.FromLocation) ? "—" : t.FromLocation;
            to = string.IsNullOrEmpty(t.ToLocation) ? "—" : t.ToLocation;
        }

        row.Add(QueueCell(pallet, WPallet, ColQueueText, false, TextAnchor.MiddleLeft));
        row.Add(QueueCell(item,   WItem,   ColQueueText, false, TextAnchor.MiddleLeft));
        row.Add(QueueCell(area,   WArea,   ColQueueText, false, TextAnchor.MiddleCenter));
        row.Add(QueueCell("100",  WPri,    ColQueueText, false, TextAnchor.MiddleCenter));
        row.Add(QueueCell(role,   WRole,   ColQueueText, false, TextAnchor.MiddleCenter));
        row.Add(QueueCell(task,   WTask,   ColQueueText, false, TextAnchor.MiddleLeft));
        row.Add(QueueCell(from,   WFrom,   ColQueueText, false, TextAnchor.MiddleLeft));
        row.Add(QueueCell(to,     WTo,     ColQueueText, false, TextAnchor.MiddleLeft));
        return row;
    }

    private static string ItemNumberFor(WorkTask t, InventoryService inv)
    {
        if (inv == null || string.IsNullOrEmpty(t.PalletId)) return "";
        var pallet = inv.GetPallet(t.PalletId);
        return pallet != null ? pallet.SkuId : "";
    }

    // 3-letter role codes per Tad's labor-standard spec.
    private static string RoleCode(EmployeeRole role) => role switch
    {
        EmployeeRole.OrderSelector       => "SEL",
        EmployeeRole.ReachTruckOperator  => "RTO",
        EmployeeRole.DockStockerOperator => "DSG", // G = grocery
        EmployeeRole.Receiver            => "RCV",
        EmployeeRole.Loader              => "LDR",
        _                                => role.ToString().Substring(0, System.Math.Min(3, role.ToString().Length)).ToUpper()
    };

    // Task label by work type (Loader → Load/Offload, Reach → Putaway/Replen/Move as those types land).
    private static string TaskLabel(WorkTask t) => t.Type switch
    {
        WorkTaskType.Receive     => "Receive",
        WorkTaskType.OrderSelect => "Selection",
        WorkTaskType.Putaway     => "Putaway",
        WorkTaskType.Replenish   => "Replen",
        WorkTaskType.Load        => "Load",
        _                        => t.Type.ToString()
    };

    // Area label by category (3-letter abbreviation).
    private static string AreaLabel(PalletData.AreaCategory area) => area switch
    {
        PalletData.AreaCategory.Grocery    => "GRO",
        PalletData.AreaCategory.Frozen     => "FRZ",
        PalletData.AreaCategory.Perishable => "PER",
        _                                   => area.ToString()
    };

    // ── Cell helpers ──────────────────────────────────────────────────────────────────────────
    private VisualElement QueueCell(string text, float width, Color color, bool bold, TextAnchor align)
        => Cell(text, width, color, bold, align);

    private VisualElement Cell(string text, float width, Color color, bool bold, TextAnchor align)
    {
        var l = new Label(text);
        ApplyFont(l, size: 13, bold: bold);
        l.style.color = color;
        if (width > 0) l.style.width = width;
        l.style.unityTextAlign = align;
        l.style.whiteSpace = WhiteSpace.NoWrap;
        l.style.overflow = Overflow.Hidden;
        l.style.textOverflow = TextOverflow.Ellipsis;
        return l;
    }

    private VisualElement IconCell(Sprite icon, float width, bool isHeader)
    {
        var container = new VisualElement();
        container.style.width = width;
        container.style.height = 22;
        container.style.alignItems = Align.Center;
        container.style.justifyContent = Justify.Center;

        if (!isHeader && icon != null)
        {
            var image = new Image { image = icon.texture };
            image.style.width = 20;
            image.style.height = 20;
            image.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            container.Add(image);
        }

        return container;
    }

    private static string Short(string id)
        => string.IsNullOrEmpty(id) ? "" : (id.Length > 8 ? id.Substring(0, 8) : id);

    private static void SetRadius(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
    }

    private static void SetBorder(VisualElement e, Color c, float w)
    {
        e.style.borderTopColor = c; e.style.borderBottomColor = c;
        e.style.borderLeftColor = c; e.style.borderRightColor = c;
        e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
        e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
    }
}
