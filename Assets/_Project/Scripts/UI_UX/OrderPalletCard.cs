using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using GameCore.Services;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Click an outbound pallet (or a case on it) while an order is being picked and a card opens over it showing that
/// ORDER: total cases to pick, cases remaining, and one filled bar per item so the player can watch the selector's
/// progress (Tad, 2026-10-18). Same look as the truck card. The card FOLLOWS the pallet - on the floor, on a
/// jack's forks, in a staging lane - and closes once the pallet is loaded into a trailer (or gone). Click the
/// pallet again, or the X, to close it. One card at a time.
/// </summary>
public class OrderPalletCard : MonoBehaviour
{
    private const float CardWidth = 400f;
    private const float RefreshSeconds = 0.25f;
    private static readonly Color Navy = new Color(0.078f, 0.110f, 0.173f, 0.96f);
    private static readonly Color Accent = new Color(0.36f, 0.61f, 0.77f, 1f);
    private static readonly Color Text = new Color(0.85f, 0.9f, 0.95f, 1f);
    private static readonly Color Done = new Color(0.30f, 0.78f, 0.42f, 1f);

    private static OrderPalletCard _instance;
    private static Font _lilita;

    private OutboundPalletBuilder _pallet;
    private string _orderId;
    private VisualElement _card, _list;
    private Label _title, _orderLabel, _totals, _stateLabel;
    private EmployeeInfoUI _infoUI;
    private Camera _cam;
    private float _nextRefresh;
    private bool _pinned;          // clicked: stays up until right-clicked / X / the pallet is loaded
    private float _hoverSeenAt;    // last time the cursor was over the pallet (unpinned card closes shortly after)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _instance = null; _lilita = null; }

    private static OrderPalletCard Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<OrderPalletCard>();
                if (_instance == null) _instance = new GameObject("OrderPalletCard").AddComponent<OrderPalletCard>();
            }
            return _instance;
        }
    }

    /// <summary>Cursor is over the pallet (tooltips on): show the card without pinning it. Ignored while a card is pinned.</summary>
    public static void Hover(OutboundPalletBuilder pallet)
    {
        if (pallet == null || string.IsNullOrEmpty(pallet.OrderId)) return;
        Instance.HoverInternal(pallet);
    }

    private void HoverInternal(OutboundPalletBuilder pallet)
    {
        if (_pinned) return;
        if (_card == null && !Build()) return;
        _hoverSeenAt = Time.unscaledTime;
        if (_pallet != pallet || _card.style.display != DisplayStyle.Flex)
        {
            _pallet = pallet;
            _orderId = pallet.OrderId;
            _card.pickingMode = PickingMode.Ignore;   // a hover card never steals the click meant for the pallet
            _card.style.display = DisplayStyle.Flex;
            Refresh();
            Place();
        }
    }

    /// <summary>Click: pins the card for this pallet's order (it stays and follows the pallet), or unpins/closes it if it already shows this pallet.</summary>
    public static void Toggle(OutboundPalletBuilder pallet)
    {
        if (pallet == null || string.IsNullOrEmpty(pallet.OrderId)) return;
        Instance.ToggleInternal(pallet);
    }

    private void ToggleInternal(OutboundPalletBuilder pallet)
    {
        if (_pinned && _pallet == pallet && _card != null && _card.style.display == DisplayStyle.Flex) { Close(); return; }
        if (_card == null && !Build()) return;
        _pinned = true;
        _card.pickingMode = PickingMode.Position;
        _pallet = pallet;
        _orderId = pallet.OrderId;
        _card.style.display = DisplayStyle.Flex;
        _card.BringToFront();
        Refresh();
        Place();
    }

    private void Close()
    {
        _pinned = false;
        _pallet = null;
        if (_card != null) _card.style.display = DisplayStyle.None;
    }

    private void OnDestroy() { _card?.RemoveFromHierarchy(); if (_instance == this) _instance = null; }

    private void LateUpdate()
    {
        if (_card == null || _card.style.display != DisplayStyle.Flex) return;

        // Gone, or loaded into a trailer / shipped: the card has done its job.
        if (!_pinned && Time.unscaledTime - _hoverSeenAt > 0.25f) { Close(); return; }   // hover card: cursor moved off
        if (_pallet == null || !_pallet.isActiveAndEnabled ||
            _pallet.Stage == OutboundPalletBuilder.PalletStage.Loaded || _pallet.Stage == OutboundPalletBuilder.PalletStage.Shipped ||
            IsInTrailer(_pallet.transform))
        {
            Close();
            return;
        }

        if (Time.unscaledTime >= _nextRefresh) { _nextRefresh = Time.unscaledTime + RefreshSeconds; Refresh(); }
        Place();
    }

    private static bool IsInTrailer(Transform t)
    {
        for (var p = t.parent; p != null; p = p.parent)
            if (p.GetComponent<TruckController>() != null) return true;
        return false;
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private bool Build()
    {
        var host = HudRoot();
        if (host == null) return false;

        _card = new VisualElement { pickingMode = PickingMode.Position };
        _card.style.position = Position.Absolute;
        _card.style.width = CardWidth;
        _card.style.backgroundColor = Navy;
        _card.style.paddingLeft = _card.style.paddingRight = 12;
        _card.style.paddingTop = 8; _card.style.paddingBottom = 10;
        SetBorder(_card, 3, Accent);
        _card.style.borderTopLeftRadius = _card.style.borderTopRightRadius = _card.style.borderBottomLeftRadius = _card.style.borderBottomRightRadius = 6;
        _card.style.display = DisplayStyle.None;
        // Right-click anywhere on the card closes it (Tad, 2026-10-18).
        _card.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 1) { Close(); evt.StopPropagation(); } });

        var top = new VisualElement { pickingMode = PickingMode.Ignore };
        top.style.flexDirection = FlexDirection.Row;
        top.style.justifyContent = Justify.SpaceBetween;
        top.style.alignItems = Align.FlexStart;
        var titles = new VisualElement { pickingMode = PickingMode.Ignore };
        titles.style.flexGrow = 1; titles.style.flexShrink = 1;
        _title = Lbl("", 20, Color.white, true); titles.Add(_title);
        _orderLabel = Lbl("", 16, Accent, true); titles.Add(_orderLabel);
        top.Add(titles);
        var close = new Button(Close) { text = "X" };
        close.style.width = 32; close.style.height = 28; close.style.flexShrink = 0;
        close.style.backgroundColor = new Color(0.8f, 0.12f, 0.12f, 1f);
        close.style.color = Color.white;
        close.style.unityFontStyleAndWeight = FontStyle.Bold;
        close.style.marginLeft = 6; close.style.marginTop = 0; close.style.marginBottom = 0; close.style.marginRight = 0;
        close.style.paddingLeft = close.style.paddingRight = close.style.paddingTop = close.style.paddingBottom = 0;
        top.Add(close);
        _card.Add(top);

        _stateLabel = Lbl("", 13, new Color(0.65f, 0.8f, 0.9f, 1f), false);
        _stateLabel.style.marginTop = 2;
        _card.Add(_stateLabel);

        var rule = new VisualElement { pickingMode = PickingMode.Ignore };
        rule.style.height = 1; rule.style.marginTop = 6; rule.style.marginBottom = 6;
        rule.style.backgroundColor = new Color(0.36f, 0.61f, 0.77f, 0.55f);
        _card.Add(rule);

        _totals = Lbl("", 17, Text, true);
        _card.Add(_totals);

        var head = new VisualElement { pickingMode = PickingMode.Ignore };
        head.style.flexDirection = FlexDirection.Row;
        head.style.justifyContent = Justify.FlexEnd;
        head.style.marginTop = 4; head.style.paddingBottom = 3; head.style.marginBottom = 6;
        head.style.borderBottomWidth = 1;
        head.style.borderBottomColor = new Color(0.36f, 0.61f, 0.77f, 0.55f);
        var hl = Lbl("PICKLIST FOR THIS PALLET", 12, new Color(0.55f, 0.78f, 0.95f, 1f), true);
        hl.style.letterSpacing = 1f;
        head.Add(hl);
        _card.Add(head);

        _list = new VisualElement { pickingMode = PickingMode.Ignore };
        _list.style.flexDirection = FlexDirection.Column;
        _card.Add(_list);

        host.Add(_card);
        return true;
    }

    private void Refresh()
    {
        if (_card == null || string.IsNullOrEmpty(_orderId)) return;
        ServiceLocator.TryGet(out OrderService orders);
        var order = orders?.ActiveOrders.FirstOrDefault(o => o.OrderId == _orderId) ?? orders?.OrderHistory.FirstOrDefault(o => o.OrderId == _orderId);
        if (order == null) { Close(); return; }

        ServiceLocator.TryGet(out InventoryService inv);

        _title.text = order.CustomerName;
        _orderLabel.text = string.IsNullOrEmpty(order.OrderNumber) ? "Order" : $"Order {order.OrderNumber}";
        string lane = !string.IsNullOrEmpty(order.AssignedLane) ? $"  |  Lane {order.EffectiveStagingDoor}{order.AssignedLane}" : "";
        string stage = _pallet != null ? _pallet.Stage.ToString() : "";
        _stateLabel.text = $"This pallet: {stage}{lane}  |  Order: {order.Status}";

        // THIS pallet's picklist only: the items and case counts on it. An order is made of several picklists (one per
        // pallet, each cubing out at about a full reference pallet, ~70 cu ft), so the order-wide totals don't belong
        // here. Picklists aren't planned ahead - the selector fills a pallet until it cubes out - so a pallet still being
        // built shows what is on it so far and grows as cases go on.
        // (sku, planned, picked): the planned list in pick order, then anything on the pallet that wasn't planned.
        var lines = new List<(string sku, int planned, int picked)>();
        if (_pallet != null && _pallet.PlannedPicklist != null)
        {
            var seen = new HashSet<string>();
            foreach (var kv in _pallet.PlannedPicklist)
            {
                _pallet.CaseCountsBySku.TryGetValue(kv.Key, out int on);
                lines.Add((kv.Key, kv.Value, on));
                seen.Add(kv.Key);
            }
            foreach (var kv in _pallet.CaseCountsBySku)
                if (!seen.Contains(kv.Key)) lines.Add((kv.Key, kv.Value, kv.Value));
        }
        else if (_pallet != null && _pallet.CaseCountsBySku.Count > 0)
        {
            foreach (var kv in _pallet.CaseCountsBySku) lines.Add((kv.Key, kv.Value, kv.Value));
            lines.Sort((a, b) => b.planned.CompareTo(a.planned));
        }
        else if (_pallet != null)
        {
            // A whole pallet taken straight from stock carries no per-case record: read it off the pallet's own data.
            var pd = _pallet.GetComponent<PalletData>();
            if (pd != null && !string.IsNullOrEmpty(pd.ItemNumber)) lines.Add((pd.ItemNumber, pd.CaseQuantity, pd.CaseQuantity));
        }

        int planned = 0, pickedTotal = 0;
        foreach (var l in lines) { planned += l.planned; pickedTotal += Mathf.Min(l.picked, l.planned); }
        if (planned == 0 && _pallet != null) { planned = _pallet.TotalCases; pickedTotal = planned; }
        int cube = _pallet != null ? Mathf.Clamp(Mathf.RoundToInt(_pallet.FillFraction * 100f), 0, 100) : 0;
        _totals.text = $"PICKLIST: {pickedTotal} / {planned} CASES   CUBE: {cube}%{(cube >= 100 ? " (FULL)" : "")}";

        _list.Clear();
        if (lines.Count == 0)
        {
            var empty = Lbl("No picks planned for this pallet.", 14, new Color(0.65f, 0.8f, 0.9f, 1f), false);
            _list.Add(empty);
            return;
        }
        foreach (var l in lines)
        {
            var sku = inv?.GetSkuData(l.sku);
            string desc = sku != null ? sku.ItemDescription : l.sku;
            int have = Mathf.Min(l.picked, l.planned);
            bool done = l.planned > 0 && have >= l.planned;
            float frac = l.planned > 0 ? Mathf.Clamp01(have / (float)l.planned) : 1f;

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.style.width = 90; track.style.height = 22; track.style.flexShrink = 0; track.style.marginRight = 10;
            track.style.backgroundColor = new Color(0.04f, 0.06f, 0.10f, 1f);
            SetBorder(track, 1, Accent);
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 4;
            track.style.overflow = Overflow.Hidden;
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.height = Length.Percent(100);
            fill.style.width = new Length(frac * 100f, LengthUnit.Percent);
            fill.style.backgroundColor = done ? Done : Accent;
            track.Add(fill);
            var qty = Lbl(done ? $"{l.planned} cs  ✓" : $"{have} / {l.planned} cs", 13, Color.white, true);
            qty.style.position = Position.Absolute;
            qty.style.left = 0; qty.style.right = 0; qty.style.top = 0; qty.style.bottom = 0;
            qty.style.unityTextAlign = TextAnchor.MiddleCenter;
            track.Add(qty);

            var name = Lbl($"{desc}", 16, done ? new Color(0.55f, 0.9f, 0.62f, 1f) : Text, false);
            name.style.flexGrow = 1; name.style.flexShrink = 1;

            row.Add(track);
            row.Add(name);
            _list.Add(row);
        }
    }

    /// <summary>Keeps the card just above the pallet on screen, clamped inside the HUD.</summary>
    private void Place()
    {
        if (_cam == null) _cam = Camera.main;
        var panel = _card.panel;
        if (_cam == null || panel == null || _pallet == null) return;

        Vector3 top = _pallet.transform.position + Vector3.up * 1.9f;
        foreach (var r in _pallet.GetComponentsInChildren<Renderer>(false))
            if (r != null && r.enabled && !(r is ParticleSystemRenderer) && !(r is LineRenderer) && !r.name.StartsWith("ContactShadow"))
                top.y = Mathf.Max(top.y, r.bounds.max.y + 0.35f);
        top.x = _pallet.transform.position.x; top.z = _pallet.transform.position.z;

        var vp = _cam.WorldToViewportPoint(top);
        bool visible = vp.z > 0.2f;
        _card.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
        if (!visible) return;

        Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, top, _cam);
        float w = CardWidth;
        float h = _card.resolvedStyle.height; if (float.IsNaN(h) || h < 1f) h = 260f;
        float pw = panel.visualTree.layout.width, ph = panel.visualTree.layout.height;
        float left = Mathf.Clamp(p.x - w * 0.5f, 4f, Mathf.Max(4f, pw - w - 4f));
        float topPx = Mathf.Clamp(p.y - h, 4f, Mathf.Max(4f, ph - h - 4f));
        _card.style.left = left;
        _card.style.top = topPx;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private VisualElement HudRoot()
    {
        if (_infoUI == null) _infoUI = FindAnyObjectByType<EmployeeInfoUI>();
        var root = _infoUI != null ? _infoUI.HudRoot : null;
        if (root != null) return root;
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
        {
            var r = doc != null ? doc.rootVisualElement : null;
            var card = r?.Q<VisualElement>("employee-info-panel");
            if (card != null) return card.parent ?? r;
        }
        return null;
    }

    private static Label Lbl(string text, int size, Color color, bool bold)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.style.fontSize = size;
        l.style.color = color;
        l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.whiteSpace = WhiteSpace.Normal;
        if (LilitaFont() != null) l.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(LilitaFont()));
        return l;
    }

    private static void SetBorder(VisualElement e, float w, Color c)
    {
        e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
        e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
    }

    private static Font LilitaFont()
    {
        if (_lilita == null)
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
            if (guids.Length > 0) _lilita = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            _lilita = Resources.Load<Font>("Resource_Fonts/LilitaOne-Regular");
#endif
        }
        return _lilita;
    }
}
