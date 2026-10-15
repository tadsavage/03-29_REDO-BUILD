using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;

/// <summary>
/// While an OUTBOUND truck is pinned (TruckHighlighter), draws a see-through box around every pallet
/// that is reserved for the order(s) on that truck - wherever it is: still in a reserve slot, riding a
/// forklift, standing in a staging lane. For the loose cases of a case-pick order there is no pallet to
/// outline, so the pick slot(s) the selector will work are boxed instead.
///
/// The colour is the truck's own order-type colour (TruckOrderColors: blue recurring, green bulk), so the
/// truck, its tooltip and its freight always agree. Inbound trucks get nothing.
///
/// A box rather than a mesh outline on purpose: a pallet carries ~200 case meshes, and cloning them all
/// (the way TruckHighlighter outlines a vehicle) would mean thousands of extra renderers for one click.
/// Boxes are parented to the pallet, so they ride the forks with it. The set is re-resolved once a
/// second, so freight that gets picked, moved or staged while the truck is pinned picks the box up too.
/// </summary>
public class OrderFreightHighlighter : MonoBehaviour
{
    private const float RefreshSeconds = 1f;
    private const float LineWidth = 0.05f;
    private static readonly Vector3 SlotBoxSize = new Vector3(1.25f, 1.1f, 1.1f);
    private static readonly Vector3 SlotBoxCenter = new Vector3(0f, 0.55f, 0f);

    private static OrderFreightHighlighter _instance;
    public static OrderFreightHighlighter Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<OrderFreightHighlighter>();
                if (_instance == null)
                    _instance = new GameObject("OrderFreightHighlighter").AddComponent<OrderFreightHighlighter>();
            }
            return _instance;
        }
    }
    public static bool HasInstance => _instance != null;

    private static Material _lineMaterial;

    private TruckController _truck;
    private Color _color;
    private float _nextRefresh;
    private readonly Dictionary<Transform, LineRenderer> _boxes = new();

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    private void OnDestroy() => ClearBoxes();

    /// <summary>Starts outlining the freight of this truck's order(s). Does nothing for an inbound truck.</summary>
    public void Show(TruckController truck)
    {
        Clear();
        if (truck == null || !truck.IsOutbound) return;
        _truck = truck;
        _color = TruckOrderColors.GetColor(truck);
        Refresh();
    }

    public void Clear()
    {
        _truck = null;
        ClearBoxes();
    }

    private void Update()
    {
        if (_truck == null)
        {
            if (_boxes.Count > 0) ClearBoxes();   // the truck drove off and was destroyed
            return;
        }
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + RefreshSeconds;
        Refresh();
    }

    // ── Resolve what to outline ──────────────────────────────────────────────

    private void Refresh()
    {
        var wanted = new Dictionary<Transform, bool>();   // target -> isPickSlot

        var appt = TruckOrderColors.ResolveOutboundAppointment(_truck);
        if (appt != null && appt.OrderIds != null && appt.OrderIds.Count > 0 &&
            ServiceLocator.TryGet<OrderService>(out var orderService) && orderService != null)
        {
            var ids = new HashSet<string>(appt.OrderIds);
            var orders = orderService.ActiveOrders.Where(o => ids.Contains(o.OrderId)).ToList();

            // Pallets already built / staged for these orders (lane, forks) - not the ones already on the trailer.
            foreach (var p in FindObjectsByType<OutboundPalletBuilder>(FindObjectsSortMode.None))
            {
                if (p == null || !ids.Contains(p.OrderId)) continue;
                if (p.Stage == OutboundPalletBuilder.PalletStage.Loaded || p.Stage == OutboundPalletBuilder.PalletStage.Shipped) continue;
                if (p.GetComponentInParent<TruckController>() != null) continue;
                wanted[p.transform] = false;
            }

            // Whole pallets reserved for the order but not staged yet: in a reserve slot or on a forklift.
            if (ServiceLocator.TryGet<WorkQueueSystem>(out var queue) && queue != null)
            {
                foreach (var t in queue.Tasks)
                {
                    if (t.Type != WorkTaskType.PalletPick || string.IsNullOrEmpty(t.PalletId) || !ids.Contains(t.OrderId)) continue;
                    if (t.Status == WorkTaskStatus.Complete || t.Status == WorkTaskStatus.Cancelled) continue;
                    var link = PalletMasterLink.Find(t.PalletId);
                    if (link != null) wanted[link.transform] = false;
                }
            }

            // Case-pick orders: the pick slot(s) the selector will visit.
            foreach (var order in orders)
            {
                if (order.IsFullyPicked || orderService.RemainingIsAllPalletPick(order)) continue;
                foreach (var address in OrderPickPath.Project(order))
                    if (LocationRegistry.TryGet(address, out var slot) && slot != null)
                        wanted[slot.transform] = true;
            }
        }

        // Drop boxes that are no longer wanted; add the new ones.
        foreach (var gone in _boxes.Keys.Where(k => k == null || !wanted.ContainsKey(k)).ToList())
        {
            if (_boxes[gone] != null) Destroy(_boxes[gone].gameObject);
            _boxes.Remove(gone);
        }
        foreach (var kv in wanted)
        {
            if (kv.Key == null) continue;
            if (_boxes.TryGetValue(kv.Key, out var existing) && existing != null)
            {
                existing.startColor = existing.endColor = _color;
                continue;
            }
            var line = kv.Value ? BuildSlotBox(kv.Key) : BuildPalletBox(kv.Key);
            if (line != null) _boxes[kv.Key] = line;
        }
    }

    // ── Boxes ────────────────────────────────────────────────────────────────

    private LineRenderer BuildPalletBox(Transform pallet)
    {
        bool any = false;
        Bounds world = default;
        foreach (var r in pallet.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled || r.GetComponent<LineRenderer>() != null) continue;
            if (!any) { world = r.bounds; any = true; } else world.Encapsulate(r.bounds);
        }
        if (!any) return null;

        // The pallet's own axes (it may be turned 90/180 degrees): bound the 8 world corners in local space.
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            var corner = world.center + Vector3.Scale(world.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var local = pallet.InverseTransformPoint(corner);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }
        return CreateBox(pallet, (min + max) * 0.5f, max - min);
    }

    private LineRenderer BuildSlotBox(Transform slot) => CreateBox(slot, SlotBoxCenter, SlotBoxSize);

    private LineRenderer CreateBox(Transform parent, Vector3 center, Vector3 size)
    {
        var go = new GameObject("__OrderFreightBox");
        go.transform.SetParent(parent, worldPositionStays: false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.widthMultiplier = LineWidth;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = LineMaterial();
        line.startColor = line.endColor = _color;

        Vector3 h = size * 0.5f;
        Vector3 c = center;
        Vector3 b0 = c + new Vector3(-h.x, -h.y, -h.z), b1 = c + new Vector3(h.x, -h.y, -h.z);
        Vector3 b2 = c + new Vector3(h.x, -h.y, h.z),   b3 = c + new Vector3(-h.x, -h.y, h.z);
        Vector3 t0 = c + new Vector3(-h.x, h.y, -h.z),  t1 = c + new Vector3(h.x, h.y, -h.z);
        Vector3 t2 = c + new Vector3(h.x, h.y, h.z),    t3 = c + new Vector3(-h.x, h.y, h.z);
        // One continuous strip that traces all 12 edges of the box.
        var pts = new[] { b0, b1, b2, b3, b0, t0, t1, b1, t1, t2, b2, t2, t3, b3, t3, t0 };
        line.positionCount = pts.Length;
        line.SetPositions(pts);
        return line;
    }

    /// <summary>Unlit, vertex-coloured, drawn through everything (ZTest Always) so a box on a pallet inside a
    /// rack, or behind a wall, is still visible - the same see-through feel as the truck outline.</summary>
    private static Material LineMaterial()
    {
        if (_lineMaterial != null) return _lineMaterial;
        var shader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
        _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 4000 };
        if (_lineMaterial.HasProperty("unity_GUIZTestMode"))
            _lineMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        return _lineMaterial;
    }

    private void ClearBoxes()
    {
        foreach (var line in _boxes.Values)
            if (line != null) Destroy(line.gameObject);
        _boxes.Clear();
    }
}
