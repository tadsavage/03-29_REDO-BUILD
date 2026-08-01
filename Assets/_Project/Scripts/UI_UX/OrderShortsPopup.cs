using System.Linq;
using GameCore.Inventory;
using GameCore.Services;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Small draggable readout of what got CUT from an order — the detail behind the Work Queue's
/// Fill Rate column.
///
/// "16 / 23" tells the player an order shipped short; it doesn't tell them WHAT was short, and that's
/// the part that matters operationally (a customer missing 8 cases of one SKU is a different problem
/// from being one case light across eight lines). This lists every line item with a shortfall, the
/// cases ordered, picked, and cut.
///
/// Not a full-screen modal on purpose: it's an inspector for one row of a table you're still reading,
/// so it floats over the Work Queue rather than replacing it, and it's draggable so it can be moved
/// off whichever row you want to keep looking at. Closes on ESC (TopBarUI's escape chain), Tab
/// (UIKeyBindingManager.CloseAll via auxiliary registration), or its own red ✕.
/// </summary>
public class OrderShortsPopup : IUIPanel
{
    private static readonly Color ColBg          = new Color(20f / 255f, 28f / 255f, 38f / 255f, 0.98f);
    private static readonly Color ColBorder      = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleText   = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColSubtleText  = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    private static readonly Color ColBlueEdge    = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColRowEven     = new Color(36f / 255f, 48f / 255f, 62f / 255f, 0.65f);
    private static readonly Color ColRowOdd      = new Color(30f / 255f, 40f / 255f, 52f / 255f, 0.65f);
    private static readonly Color ColShort       = new Color(0xE2 / 255f, 0x4B / 255f, 0x4A / 255f, 1f);
    private static readonly Color ColShortSoft   = new Color(0xF0 / 255f, 0x95 / 255f, 0x95 / 255f, 1f);
    private static readonly Color ColGood        = new Color(0x7E / 255f, 0xD6 / 255f, 0x8A / 255f, 1f);
    private static readonly Color ColCloseRed    = new Color(0x8E / 255f, 0x2B / 255f, 0x2B / 255f, 1f);
    private static readonly Color ColCloseRedHi  = new Color(0xC0 / 255f, 0x3A / 255f, 0x3A / 255f, 1f);

    private const float PopupWidth  = 460f;
    private const float MaxListHeight = 260f;

    private const float ItemColWidth   = 96f;
    private const float QtyColWidth    = 66f;

    private readonly VisualElement _popup;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly ScrollView _list;
    private readonly Label _footer;
    private bool _visible;

    private bool _dragging;
    private Vector2 _dragOffset;

    private static Font _lilita;

    public OrderShortsPopup(VisualElement root)
    {
        _popup = Build(out _title, out _subtitle, out _list, out _footer);
        root.Add(_popup);
        Hide();

        // Registered as an auxiliary rather than under a number key: it has no hotkey of its own, but
        // Tab must still close it like every other panel.
        UIKeyBindingManager.Instance.RegisterAuxiliary(this);
    }

    public bool IsVisible => _visible;
    public bool IsOpen => _visible;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    /// <summary>IUIPanel requires a no-arg Show. Re-showing without an order would display a stale
    /// list, so this only un-hides what's already built — ShowFor is the real entry point.</summary>
    public void Show()
    {
        _visible = true;
        _popup.style.display = DisplayStyle.Flex;
        _popup.BringToFront();
    }

    public void Hide()
    {
        _visible = false;
        _popup.style.display = DisplayStyle.None;
    }

    public void Dispose()
    {
        UIKeyBindingManager.Instance?.UnregisterAuxiliary(this);
        if (_popup.parent != null) _popup.RemoveFromHierarchy();
    }

    /// <summary>Opens the popup for one order, positioned near the click.</summary>
    public void ShowFor(OrderData order, Vector2 screenPos)
    {
        if (order == null) return;
        Populate(order);
        Show();

        // Place after layout so the popup's real height is known — otherwise it can be pushed off the
        // bottom of the screen by its own not-yet-measured size.
        _popup.schedule.Execute(() => PositionNear(screenPos)).ExecuteLater(1);
    }

    private void PositionNear(Vector2 screenPos)
    {
        var parent = _popup.parent;
        if (parent == null) return;

        Rect bounds = parent.worldBound;
        float w = Mathf.Max(_popup.resolvedStyle.width, PopupWidth);
        float h = Mathf.Max(_popup.resolvedStyle.height, 120f);

        // Offset up-left of the cursor so the popup doesn't cover the row that was clicked.
        float x = screenPos.x - w - 12f;
        float y = screenPos.y - 24f;

        if (x < bounds.xMin + 8f) x = screenPos.x + 16f;                 // no room left — flip right
        x = Mathf.Clamp(x, bounds.xMin + 8f, Mathf.Max(bounds.xMin + 8f, bounds.xMax - w - 8f));
        y = Mathf.Clamp(y, bounds.yMin + 8f, Mathf.Max(bounds.yMin + 8f, bounds.yMax - h - 8f));

        _popup.style.left = x - bounds.xMin;
        _popup.style.top = y - bounds.yMin;
    }

    // ── Content ──────────────────────────────────────────────────────────────

    private void Populate(OrderData order)
    {
        _title.text = "CUT FROM THIS ORDER";
        _subtitle.text = $"{order.CustomerName} · order {ShortId(order.OrderId)} · " +
                         $"{order.TotalUnitsPicked} of {order.TotalUnits} cases picked";

        _list.Clear();
        ServiceLocator.TryGet<InventoryService>(out var inv);

        // Only lines that actually came up short. A fully-picked line isn't "cut" and listing it would
        // bury the two lines that were, which is the whole reason to open this.
        var shorts = order.LineItems
            .Where(li => li != null && li.QuantityRemaining > 0)
            .OrderByDescending(li => li.QuantityRemaining)
            .ToList();

        if (shorts.Count == 0)
        {
            var none = MakeText(order.TotalUnits > 0 && order.TotalUnitsPicked >= order.TotalUnits
                    ? "Nothing was cut — this order shipped complete."
                    : "Nothing has been cut yet. Picking hasn't started or is still in progress.",
                13, ColGood);
            none.style.marginTop = 6;
            none.style.marginBottom = 6;
            _list.Add(none);
            _footer.text = string.Empty;
            return;
        }

        _list.Add(BuildHeaderRow());

        int row = 0;
        int totalCut = 0;
        foreach (var li in shorts)
        {
            totalCut += li.QuantityRemaining;
            _list.Add(BuildLineRow(li, inv, row++));
        }

        int lostRevenue = shorts.Sum(li => li.QuantityRemaining * li.SellingPrice);
        _footer.text = $"{shorts.Count} line(s) short · {totalCut} case(s) cut · " +
                       $"${lostRevenue:N0} not billed";
    }

    private VisualElement BuildHeaderRow()
    {
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.paddingLeft = 8; header.style.paddingRight = 8;
        header.style.paddingBottom = 4;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = new StyleColor(ColBlueEdge);
        header.style.marginBottom = 4;

        header.Add(HeaderCell("ITEM", ItemColWidth));
        var desc = HeaderCell("DESCRIPTION", 0);
        desc.style.flexGrow = 1;
        header.Add(desc);
        header.Add(HeaderCell("ORDERED", QtyColWidth));
        header.Add(HeaderCell("PICKED", QtyColWidth));
        header.Add(HeaderCell("CUT", QtyColWidth));
        return header;
    }

    private Label HeaderCell(string text, float width)
    {
        var l = MakeText(text, 10, ColSubtleText, bold: true);
        if (width > 0f) { l.style.width = width; l.style.minWidth = width; }
        l.style.flexShrink = 0;
        l.style.unityTextAlign = TextAnchor.MiddleLeft;
        return l;
    }

    private VisualElement BuildLineRow(OrderLineItem li, InventoryService inv, int rowIndex)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 8; row.style.paddingRight = 8;
        row.style.paddingTop = 4; row.style.paddingBottom = 4;
        row.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColRowEven : ColRowOdd);

        var sku = inv != null ? inv.GetSkuData(li.SkuId) : null;
        string description = sku != null && !string.IsNullOrWhiteSpace(sku.ItemDescription)
            ? sku.ItemDescription
            : "(no description)";

        var item = MakeText(li.SkuId, 12, ColTitleText, bold: true);
        item.style.width = ItemColWidth; item.style.minWidth = ItemColWidth;
        item.style.flexShrink = 0;
        row.Add(item);

        var desc = MakeText(description, 12, ColSubtleText);
        desc.style.flexGrow = 1;
        desc.style.flexShrink = 1;
        desc.style.overflow = Overflow.Hidden;
        desc.style.whiteSpace = WhiteSpace.NoWrap;
        row.Add(desc);

        row.Add(QtyCell(li.QuantityNeeded.ToString(), ColSubtleText));
        row.Add(QtyCell(li.QuantityPicked.ToString(), ColTitleText));
        row.Add(QtyCell(li.QuantityRemaining.ToString(), ColShortSoft, bold: true));
        return row;
    }

    private Label QtyCell(string text, Color color, bool bold = false)
    {
        var l = MakeText(text, 12, color, bold);
        l.style.width = QtyColWidth; l.style.minWidth = QtyColWidth;
        l.style.flexShrink = 0;
        l.style.unityTextAlign = TextAnchor.MiddleLeft;
        return l;
    }

    // ── Shell ────────────────────────────────────────────────────────────────

    private VisualElement Build(out Label title, out Label subtitle, out ScrollView list, out Label footer)
    {
        // No full-screen dimming overlay — this floats OVER the Work Queue, which stays readable and
        // clickable underneath. The popup element itself is the whole thing.
        var popup = new VisualElement { name = "order-shorts-popup" };
        popup.style.position = Position.Absolute;
        popup.style.width = PopupWidth;
        popup.style.backgroundColor = new StyleColor(ColBg);
        popup.style.borderTopWidth = popup.style.borderBottomWidth =
            popup.style.borderLeftWidth = popup.style.borderRightWidth = 2;
        popup.style.borderTopColor = popup.style.borderBottomColor =
            popup.style.borderLeftColor = popup.style.borderRightColor = new StyleColor(ColBorder);
        popup.style.borderTopLeftRadius = popup.style.borderTopRightRadius =
            popup.style.borderBottomLeftRadius = popup.style.borderBottomRightRadius = 10;
        popup.style.paddingBottom = 8;

        // Title bar — also the drag handle.
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.paddingLeft = 10; titleBar.style.paddingRight = 6;
        titleBar.style.paddingTop = 6; titleBar.style.paddingBottom = 6;
        titleBar.style.backgroundColor = new StyleColor(new Color(0.12f, 0.16f, 0.24f, 1f));
        titleBar.style.borderTopLeftRadius = titleBar.style.borderTopRightRadius = 8;

        title = MakeText("CUT FROM THIS ORDER", 15, ColShortSoft, bold: true);
        title.style.flexGrow = 1;
        titleBar.Add(title);

        var close = new Button(Hide) { text = "✕" };
        ApplyFont(close, bold: true, size: 13);
        close.style.width = 24; close.style.height = 24;
        close.style.paddingLeft = 0; close.style.paddingRight = 0;
        close.style.paddingTop = 0; close.style.paddingBottom = 0;
        close.style.marginLeft = 0; close.style.marginRight = 0;
        close.style.marginTop = 0; close.style.marginBottom = 0;
        close.style.backgroundColor = new StyleColor(ColCloseRed);
        close.style.color = new StyleColor(Color.white);
        close.style.borderTopWidth = close.style.borderBottomWidth =
            close.style.borderLeftWidth = close.style.borderRightWidth = 0;
        close.style.borderTopLeftRadius = close.style.borderTopRightRadius =
            close.style.borderBottomLeftRadius = close.style.borderBottomRightRadius = 5;
        close.RegisterCallback<MouseEnterEvent>(_ => close.style.backgroundColor = new StyleColor(ColCloseRedHi));
        close.RegisterCallback<MouseLeaveEvent>(_ => close.style.backgroundColor = new StyleColor(ColCloseRed));
        titleBar.Add(close);
        popup.Add(titleBar);

        // Drag on the BAR, not the popup, so a drag can't start from a row and the ✕ keeps its click.
        titleBar.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || evt.target is Button) return;
            _dragging = true;
            _dragOffset = (Vector2)evt.position - new Vector2(popup.worldBound.x, popup.worldBound.y);
            titleBar.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });
        titleBar.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!_dragging) return;
            Rect bounds = popup.parent != null ? popup.parent.worldBound : new Rect(0, 0, Screen.width, Screen.height);
            Vector2 target = (Vector2)evt.position - _dragOffset - new Vector2(bounds.x, bounds.y);
            // Keep a grabbable strip of the title bar on screen.
            popup.style.left = Mathf.Clamp(target.x, -(PopupWidth - 100f), Mathf.Max(0f, bounds.width - 100f));
            popup.style.top = Mathf.Clamp(target.y, 0f, Mathf.Max(0f, bounds.height - 40f));
            evt.StopPropagation();
        });
        titleBar.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!_dragging) return;
            _dragging = false;
            titleBar.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        });

        subtitle = MakeText(string.Empty, 12, ColSubtleText);
        subtitle.style.paddingLeft = 10; subtitle.style.paddingRight = 10;
        subtitle.style.paddingTop = 6; subtitle.style.paddingBottom = 6;
        popup.Add(subtitle);

        list = new ScrollView(ScrollViewMode.Vertical);
        list.style.maxHeight = MaxListHeight;
        list.style.paddingLeft = 2; list.style.paddingRight = 2;
        popup.Add(list);

        footer = MakeText(string.Empty, 12, ColShort, bold: true);
        footer.style.paddingLeft = 10; footer.style.paddingRight = 10;
        footer.style.paddingTop = 8;
        footer.style.whiteSpace = WhiteSpace.Normal;
        popup.Add(footer);

        return popup;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string ShortId(string value)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        return value.Length > 8 ? value.Substring(0, 8) : value;
    }

    private Label MakeText(string text, int size, Color color, bool bold = false)
    {
        var label = new Label(text);
        ApplyFont(label, bold, size);
        label.style.color = new StyleColor(color);
        return label;
    }

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
}
