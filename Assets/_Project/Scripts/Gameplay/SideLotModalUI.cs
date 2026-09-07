using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// Small self-bootstrapping modal (same pattern as ConfirmationModal — own UIDocument, PanelSettings
/// borrowed from whatever UIDocument already exists, shown/hidden via display style) listing every
/// truck currently parked in a SideLotController across the whole scene. Rows highlight when the
/// parked shipment carries a SKU that an active customer order needs within the next 24 hours
/// (approximated as "due today or tomorrow" — OrderData.DueDay is day-granularity, no hour).
/// Opened by SideLotController.Show() when the player clicks anywhere on a side lot.
/// </summary>
public class SideLotModalUI : MonoBehaviour
{
    private static SideLotModalUI _instance;

    private UIDocument _doc;
    private VisualElement _modal;
    private VisualElement _rows;

    private static readonly Color ColBg        = new Color(20f / 255f, 28f / 255f, 38f / 255f, 0.98f);
    private static readonly Color ColBorder    = new Color(92f / 255f, 155f / 255f, 196f / 255f, 1f);
    private static readonly Color ColRowNormal = new Color(34f / 255f, 44f / 255f, 56f / 255f, 0.85f);
    private static readonly Color ColRowNeeded = new Color(0.55f, 0.10f, 0.10f, 0.9f); // urgent-red, matches ColDanger-style accents elsewhere
    private static readonly Color ColText      = new Color(0.75f, 0.80f, 0.85f, 1f);
    private static readonly Color ColMuted     = new Color(0.50f, 0.55f, 0.60f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[SideLotModalUI]") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<SideLotModalUI>();
        _instance.Build();
    }

    public static void Show()
    {
        if (_instance == null) Bootstrap();
        if (_instance == null || _instance._modal == null) return;
        _instance.Refresh();
        _instance._modal.style.display = DisplayStyle.Flex;
    }

    private static void Hide()
    {
        if (_instance == null || _instance._modal == null) return;
        _instance._modal.style.display = DisplayStyle.None;
    }

    private void Build()
    {
        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = FindPanelSettings();
        _doc.sortingOrder = 900;

        var root = _doc.rootVisualElement;
        if (root == null) return;
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;

        _modal = new VisualElement();
        _modal.style.position = Position.Absolute;
        _modal.style.left = 0; _modal.style.top = 0; _modal.style.right = 0; _modal.style.bottom = 0;
        _modal.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.45f));
        _modal.style.alignItems = Align.Center;
        _modal.style.justifyContent = Justify.Center;
        _modal.pickingMode = PickingMode.Position;
        _modal.style.display = DisplayStyle.None;
        // Backdrop click closes it — same convenience ConfirmationModal doesn't need (it always has
        // an explicit Yes/No), but a read-only info list should be dismissible from anywhere.
        _modal.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _modal) Hide();
        });
        root.Add(_modal);

        var panel = new VisualElement();
        panel.style.width = 420;
        panel.style.maxHeight = 480;
        panel.style.paddingLeft = 16; panel.style.paddingRight = 16;
        panel.style.paddingTop = 14; panel.style.paddingBottom = 14;
        panel.style.backgroundColor = new StyleColor(ColBg);
        SetBorder(panel, ColBorder, 2, 10);
        panel.pickingMode = PickingMode.Position;
        _modal.Add(panel);

        var title = new Label("Side Lot");
        title.style.color = new StyleColor(Color.white);
        title.style.fontSize = 20;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 10;
        panel.Add(title);

        _rows = new VisualElement();
        panel.Add(_rows);

        var closeBtn = new Button(Hide) { text = "Close" };
        closeBtn.style.marginTop = 12;
        closeBtn.style.backgroundColor = new StyleColor(ColRowNormal);
        closeBtn.style.color = new StyleColor(Color.white);
        SetBorder(closeBtn, ColBorder, 1, 6);
        panel.Add(closeBtn);
    }

    private void Refresh()
    {
        _rows.Clear();

        // Flattened across every slot on every lot now that a SideLotController can hold several
        // trucks at once (one per Jersey_barrier segment) instead of just one.
        var occupants = SideLotController.All
            .Where(l => l != null)
            .SelectMany(l => l.Slots)
            .Where(s => s.Occupant != null)
            .Select(s => s.Occupant)
            .ToList();

        if (occupants.Count == 0)
        {
            var empty = new Label("No trailers currently parked.");
            empty.style.color = new StyleColor(ColMuted);
            empty.style.unityFontStyleAndWeight = FontStyle.Italic;
            _rows.Add(empty);
            return;
        }

        var neededSkus = NeededSkusWithinNextDay();

        foreach (var truck in occupants)
        {
            var shipment = truck.AssignedShipment;
            bool needed = shipment != null && shipment.LineItems.Any(li => neededSkus.Contains(li.SkuId));

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 10; row.style.paddingRight = 10;
            row.style.paddingTop = 8; row.style.paddingBottom = 8;
            row.style.marginBottom = 4;
            row.style.backgroundColor = new StyleColor(needed ? ColRowNeeded : ColRowNormal);
            row.style.borderTopLeftRadius = row.style.borderTopRightRadius =
                row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 4;

            var label = new Label(shipment != null ? $"PO {shipment.PONumber}" : truck.name);
            label.style.color = new StyleColor(Color.white);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(label);

            var status = new Label(needed ? "NEEDED WITHIN 24H" : "Parked");
            status.style.color = new StyleColor(needed ? Color.white : ColText);
            status.style.fontSize = 11;
            row.Add(status);

            _rows.Add(row);
        }
    }

    /// <summary>Every SKU any active order needs, where DueDay is today or tomorrow — DueDay is
    /// day-granularity (see OrderData.DueDay), so "next 24 hours" is approximated at that
    /// resolution rather than a true rolling 24h window.</summary>
    private static HashSet<string> NeededSkusWithinNextDay()
    {
        var skus = new HashSet<string>();
        if (!ServiceLocator.TryGet(out OrderService orders) || orders == null) return skus;

        var gameCtx = Object.FindAnyObjectByType<GameContext>();
        if (gameCtx == null) return skus;
        int today = gameCtx.TimeService.Day;

        foreach (var order in orders.ActiveOrders)
        {
            if (order == null) continue;
            if (order.Status == OrderData.OrderStatus.Cancelled || order.Status == OrderData.OrderStatus.Shipped) continue;
            if (order.DueDay != today && order.DueDay != today + 1) continue;

            foreach (var li in order.LineItems)
                skus.Add(li.SkuId);
        }
        return skus;
    }

    private static PanelSettings FindPanelSettings()
    {
        var existing = Object.FindAnyObjectByType<UIDocument>();
        return existing != null ? existing.panelSettings : null;
    }

    private static void SetBorder(VisualElement e, Color c, float w, float r)
    {
        e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
        e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
        var sc = new StyleColor(c);
        e.style.borderTopColor = sc; e.style.borderBottomColor = sc;
        e.style.borderLeftColor = sc; e.style.borderRightColor = sc;
        e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
    }
}
