using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using GameCore.Inventory;

/// <summary>
/// Incremental, case-by-case pallet builder for Order Selection — the outbound counterpart to
/// PalletBuilder, which only supports destroying and rebuilding an entire single-SKU pallet in one
/// shot. A selector's pallet needs to grow one case at a time, across multiple different SKUs (one
/// per order line item), without ever discarding what's already been placed.
///
/// Layout approach: each SKU uses its own best-fit single-layer pattern from PalletOptimizer (same
/// packing math PalletBuilder uses), and every case drops to the lowest spot in that pattern on a
/// height map of what's already placed — so a new SKU fills in beside a partial layer instead of
/// starting a new layer of air above it (see AddCase).
/// </summary>
public class OutboundPalletBuilder : MonoBehaviour
{
    private static readonly Vector3 PalletDim = new Vector3(1.2192f, 0.165f, 1.016f);
    private const float SpaceBetweenCases = 0.05f;
    // Matches PalletBuilder's own crookedCase/positionJitter defaults — see that field's tooltip for why
    // the rotation is "1 to 5" rather than a range spanning 0 (a range including 0 kept producing cases
    // dead straight, which read as too perfectly stacked).
    private const float MinCrookedCaseDegrees = 1f;
    private const float CrookedCaseDegrees = 5f;
    private const float PositionJitterMeters = 0.015f;

    // TEMP-DIAG-PALLETSPAWN
    private void Awake() { Debug.LogWarning($"[PALLETSPAWN] {name} at {transform.position} {System.Environment.StackTrace.Replace('\n', '|')}"); }

    private Transform _loadRoot;
    private string _currentSkuId;
    private List<(Vector3 pos, float rot)> _currentPattern;
    /// <summary>Footprint (pallet-local XZ) and top height of every case placed so far — the height map.</summary>
    private readonly List<(Vector2 min, Vector2 max, float top)> _placed = new();
    /// <summary>How far neighbouring cases may overlap at their edges so a case can sit flat (Tad: 0.05).</summary>
    private const float ClipTolerance = 0.05f;
    private readonly Dictionary<string, int> _caseCountBySku = new();

    public int TotalCases { get; private set; }

    /// <summary>Cases on this pallet per SKU. Empty for an adopted whole pallet (its SKU lives on PalletData).</summary>
    public IReadOnlyDictionary<string, int> CaseCountsBySku => _caseCountBySku;

    /// <summary>This pallet's PLANNED picklist (SKU, cases) in pick order, worked out when the pallet was started so the
    /// tooltip can show the whole list from the start. Null for pallets with no plan (loaded from a save, whole pallets).
    /// A forecast: what is actually on the pallet is CaseCountsBySku.</summary>
    public IReadOnlyList<KeyValuePair<string, int>> PlannedPicklist { get; private set; }
    public void SetPlannedPicklist(List<KeyValuePair<string, int>> plan) { PlannedPicklist = plan != null && plan.Count > 0 ? plan : null; }

    /// <summary>The SKU of every case under PalletLoad, in child order. Saved so a mixed pallet restores case-for-case.</summary>
    public IReadOnlyList<string> CaseSkus => _caseSkus;
    private readonly List<string> _caseSkus = new();

    /// <summary>The pick slot each case came from (parallel to CaseSkus; "" if unknown). A return walks these in reverse.</summary>
    public IReadOnlyList<string> CaseSources => _caseSources;
    private readonly List<string> _caseSources = new();

    /// <summary>Set when a return task is filed for this pallet: the task's PalletId, so the selector can find it.</summary>
    public string ReturnId { get; set; }

    /// <summary>Lifecycle status (see PalletLifecycle) - derived, never stored.</summary>
    public PalletLifecycle Lifecycle => PalletLifecycleResolver.Resolve(gameObject);

    /// <summary>The order this pallet was built for — set once, right after creation, by
    /// OrderSelectionTaskDriver. Lets a later system (TrailerLoadController, billing) find which
    /// order a staged pallet belongs to without needing a separate lookup table.</summary>
    public string OrderId { get; set; }

    /// <summary>Where THIS pallet is in the outbound flow. Tracked per pallet (not per order) so the
    /// Work Queue can show one pallet Loading while its siblings are still Staged. Staged = standing in
    /// a lane; Moving = a reach truck is relocating it between lanes; Loading = lifted by the loader and
    /// not yet set down; Loaded = in the trailer; Shipped = the trailer has left the property.</summary>
    public enum PalletStage { Staged, Moving, Loading, Loaded, Shipped }

    public PalletStage Stage { get; set; } = PalletStage.Staged;

    /// <summary>
    /// Standing on the floor (in a lane), as opposed to riding forks, a selector, a pallet jack or a trailer.
    /// THE test for "is this pallet staged where it stands" - use it instead of <c>transform.parent != null</c>.
    /// A pallet restored from a save is parented to PlacedObjectsContainer, so the old parent check made every
    /// restored staged pallet look carried: the loader never took it and lane occupancy never counted it
    /// (2026-10-17 save/load audit).
    /// </summary>
    public bool IsOnFloor => IsOnFloorTransform(transform);

    public static bool IsOnFloorTransform(Transform t)
        => t != null && (t.parent == null || t.parent.name == "PlacedObjectsContainer");

    /// <summary>
    /// Marks an ALREADY-BUILT pallet as outbound freight for an order, without touching its cases.
    ///
    /// This is how a Pallet Pick joins the outbound world. A bulk order's full pallet is a real
    /// received pallet that a Reach Truck carried out of a reserve slot — its cases were modelled by
    /// PalletBuilder when it was received, so there is nothing to lay out and AddCase must not be
    /// called (it would stack a second set of cases on top of the ones already there).
    ///
    /// It still has to become an OutboundPalletBuilder, because that component is the ONLY thing two
    /// downstream systems look for: TrailerLoadController.FindStagedPalletsInLane scans for it to
    /// decide what goes on the trailer, and InventoryService.OutboundOccupiedCells scans for it to
    /// know a staging cell is taken. Without it the pallet sits in the lane looking correct, never
    /// loads, and the next order stages a pallet straight through it.
    ///
    /// FillFraction is set to a full pallet so IsCubedOut reads true — nothing may add cases to a
    /// pallet that is, by definition, already full.
    /// </summary>
    public void AdoptFullPallet(string orderId, int cases)
    {
        OrderId = orderId;
        TotalCases = Mathf.Max(0, cases);
        FillFraction = 1f;
    }

    /// <summary>Fraction of "one reference pallet's worth" of capacity consumed so far, summed
    /// across every SKU added. No cubic-footage field exists on SkuData, so each case contributes
    /// 1/(Ti*Hi) of its own SKU's full-pallet case count as a proxy for the volume it occupies —
    /// see Tad's "cubed out" spec (~40x48x72", ~70 cubic feet).</summary>
    public float FillFraction { get; private set; }

    /// <summary>True once FillFraction has reached a full reference pallet's worth — the selector
    /// stops adding to this pallet and (per Tad's spec) starts a second one, or finishes the order
    /// if a second pallet isn't available either.</summary>
    public bool IsCubedOut => FillFraction >= 1f;

    /// <summary>
    /// Adds one case of the given SKU to the pallet, BUILT UP rather than stacked in layers (Tad, 2026-10-16).
    ///
    /// Each SKU still uses its own best-fit layer pattern, but a case no longer starts a fresh layer above
    /// everything already on the pallet. Every pattern position is tested against a height map of the cases
    /// already placed: the case would rest on the highest case it actually overlaps (or the deck), and it goes
    /// in whichever position lets it rest LOWEST (ties keep pattern order). So the next SKU fills the holes
    /// beside a partial layer first and only climbs onto that partial layer once the low spots are full: a few
    /// gaps are fine, whole layers of air are not. Neighbouring cases may overlap by up to ClipTolerance at the
    /// edges so a case can sit flat; with physics later a case resting half on a step would tilt (and the
    /// pallet might topple), which is the fun part.
    /// ti/hi are the SKU's own cases-per-layer/layers-per-pallet (SkuData.Ti/Hi) — used only to
    /// accumulate FillFraction, not for layout on this mixed pallet.
    /// </summary>
    public void AddCase(string skuId, GameObject casePrefab, Vector3 caseDim, int ti, int hi, string sourceAddress = null)
    {
        if (casePrefab == null) return;

        if (_loadRoot == null)
        {
            var loadGo = new GameObject("PalletLoad");
            loadGo.transform.SetParent(transform, false);
            loadGo.transform.localPosition = Vector3.zero;
            _loadRoot = loadGo.transform;
        }

        if (_currentPattern == null || skuId != _currentSkuId)
        {
            RebuildPattern(caseDim);
            _currentSkuId = skuId;
        }

        // Lowest resting spot across the pattern.
        int bestIndex = 0;
        float bestBase = float.MaxValue;
        for (int i = 0; i < _currentPattern.Count; i++)
        {
            float b = RestingBase(_currentPattern[i], caseDim);
            if (b < bestBase - 0.001f) { bestBase = b; bestIndex = i; }
        }
        var slot = _currentPattern[bestIndex];

        var instance = Instantiate(casePrefab);
        instance.transform.SetParent(_loadRoot, false);
        instance.transform.localPosition = new Vector3(
            slot.pos.x + Random.Range(-PositionJitterMeters, PositionJitterMeters),
            bestBase,
            slot.pos.z + Random.Range(-PositionJitterMeters, PositionJitterMeters));

        // Case prefabs carry PlacedObject/BuildingData for the build menu — must not self-register
        // into the grid at (0,0) when spawned as part of a WIP pallet. Same stripping
        // PalletBuilder.Build() performs on every case it instantiates.
        var po = instance.GetComponent<PlacedObject>();
        if (po != null) { po.enabled = false; Destroy(po); }
        var bd = instance.GetComponent<BuildingData>();
        if (bd != null) Destroy(bd);
        var bh = instance.GetComponent<BuildingHighlighter>();
        if (bh != null) Destroy(bh);

        float magnitude = Random.Range(MinCrookedCaseDegrees, CrookedCaseDegrees);
        float randomRot = (Random.value < 0.5f ? -1f : 1f) * magnitude;
        instance.transform.localRotation = Quaternion.Euler(0, slot.rot + randomRot, 0);

        Footprint(slot, caseDim, out Vector2 min, out Vector2 max);
        _placed.Add((min, max, bestBase + caseDim.y));
        TotalCases++;
        FillFraction += 1f / Mathf.Max(1, ti * hi);

        _caseCountBySku.TryGetValue(skuId, out int existing);
        _caseCountBySku[skuId] = existing + 1;
        _caseSkus.Add(skuId);
        _caseSources.Add(sourceAddress ?? "");
    }

    /// <summary>Takes the most recently placed case off the pallet (a return puts cases back in reverse order).
    /// Returns its SKU and source slot, or false when the pallet is empty.</summary>
    public bool RemoveLastCase(out string skuId, out string sourceAddress)
    {
        skuId = null; sourceAddress = null;
        if (_loadRoot == null) _loadRoot = transform.Find("PalletLoad");
        int n = _caseSkus.Count;
        if (n == 0) return false;
        skuId = _caseSkus[n - 1];
        sourceAddress = n - 1 < _caseSources.Count ? _caseSources[n - 1] : "";
        _caseSkus.RemoveAt(n - 1);
        if (n - 1 < _caseSources.Count) _caseSources.RemoveAt(n - 1);
        if (_placed.Count > 0) _placed.RemoveAt(_placed.Count - 1);
        if (_loadRoot != null && _loadRoot.childCount > 0)
        {
            var last = _loadRoot.GetChild(_loadRoot.childCount - 1);
            last.SetParent(null);
            Destroy(last.gameObject);
        }
        TotalCases = Mathf.Max(0, TotalCases - 1);
        if (_caseCountBySku.TryGetValue(skuId ?? "", out int c)) { if (c <= 1) _caseCountBySku.Remove(skuId ?? ""); else _caseCountBySku[skuId ?? ""] = c - 1; }
        return true;
    }

    /// <summary>
    /// Save/load: puts one case back exactly where it was (local pose under PalletLoad) and rebuilds the height map,
    /// counts and fill so the pallet behaves as if it had been built case by case. Used by OutboundPalletPersistence
    /// and by restored outbound trailer cargo.
    /// </summary>
    public void RestoreCase(string skuId, GameObject casePrefab, Vector3 caseDim, int ti, int hi, Vector3 localPos, Quaternion localRot, string sourceAddress = null)
    {
        if (_loadRoot == null)
        {
            var existingLoad = transform.Find("PalletLoad");
            if (existingLoad != null) _loadRoot = existingLoad;
            else
            {
                var loadGo = new GameObject("PalletLoad");
                loadGo.transform.SetParent(transform, false);
                _loadRoot = loadGo.transform;
            }
        }
        if (casePrefab != null)
        {
            var instance = Instantiate(casePrefab, _loadRoot, false);
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = localRot;
            var po = instance.GetComponent<PlacedObject>();
            if (po != null) { po.enabled = false; Destroy(po); }
            var bd = instance.GetComponent<BuildingData>();
            if (bd != null) Destroy(bd);
            var bh = instance.GetComponent<BuildingHighlighter>();
            if (bh != null) Destroy(bh);
        }

        float yaw = localRot.eulerAngles.y;
        Footprint((localPos, Mathf.Round(yaw / 90f) * 90f), caseDim, out Vector2 min, out Vector2 max);
        _placed.Add((min, max, localPos.y + caseDim.y));
        TotalCases++;
        FillFraction += 1f / Mathf.Max(1, ti * hi);
        _caseCountBySku.TryGetValue(skuId ?? "", out int existing);
        _caseCountBySku[skuId ?? ""] = existing + 1;
        _caseSkus.Add(skuId ?? "");
        _caseSources.Add(sourceAddress ?? "");
    }

    /// <summary>Sale value of everything on this pallet: for each SKU present, its case count times
    /// that SKU's SellingPrice on the given order, summed across all items (Tad's spec — Case Sale
    /// Price x # of cases, repeated per item, added up for the pallet total).</summary>
    public int CalculateSaleValue(OrderData order)
    {
        if (order == null) return 0;
        int total = 0;
        foreach (var kvp in _caseCountBySku)
        {
            var lineItem = order.LineItems.FirstOrDefault(li => li.SkuId == kvp.Key);
            if (lineItem != null) total += kvp.Value * lineItem.SellingPrice;
        }
        return total;
    }

    // ── NavMesh blocking ─────────────────────────────────────────────────────────────────────
    // An outbound pallet is built directly in the world and deliberately carries no PlacedObject /
    // BuildingData (it isn't InventoryService-managed stock), so BuildingData.ConfigureObstacle() —
    // which is what gives every ordinary pallet its carving NavMeshObstacle — never runs on it. That
    // is why MHE and humanoids walked straight through staged outbound pallets. Same settings the
    // Inventory category gets: box, carving, stationary-only, and inset inside its 1.33 cell so
    // neighbouring pallets carve separate footprints instead of merging into one blob.
    private const float ObstacleFootprint = 1.15f;
    private NavMeshObstacle _obstacle;

    /// <summary>Carve while the pallet is standing on the ground; stop carving while it rides a
    /// selector or a set of forks. A carving obstacle dragged across the floor cuts a moving trench
    /// through the NavMesh and shoves aside every agent it passes, so carrying must switch it off.
    /// The component is created lazily on the first `true`, so a work-in-progress pallet being built
    /// on a selector never carves at all.</summary>
    public void SetNavObstacleActive(bool active)
    {
        if (_obstacle == null) _obstacle = GetComponent<NavMeshObstacle>();

        if (!active)
        {
            if (_obstacle != null) _obstacle.enabled = false;
            return;
        }

        if (_obstacle == null)
        {
            _obstacle = gameObject.AddComponent<NavMeshObstacle>();
            _obstacle.shape = NavMeshObstacleShape.Box;
            _obstacle.carveOnlyStationary = true;
            _obstacle.size = new Vector3(ObstacleFootprint, PalletDim.y, ObstacleFootprint);
            _obstacle.center = new Vector3(0f, PalletDim.y * 0.45f, 0f);
        }
        _obstacle.carving = true;
        _obstacle.enabled = true;
    }

    /// <summary>Height this case would rest at in that pattern position: the top of the highest placed case its
    /// footprint (shrunk by ClipTolerance per side) overlaps, or the pallet deck.</summary>
    private float RestingBase((Vector3 pos, float rot) slot, Vector3 caseDim)
    {
        Footprint(slot, caseDim, out Vector2 min, out Vector2 max);
        min += new Vector2(ClipTolerance, ClipTolerance);
        max -= new Vector2(ClipTolerance, ClipTolerance);
        float baseY = PalletDim.y;
        foreach (var c in _placed)
        {
            if (max.x <= c.min.x || min.x >= c.max.x || max.y <= c.min.y || min.y >= c.max.y) continue;
            if (c.top > baseY) baseY = c.top;
        }
        return baseY;
    }

    private static void Footprint((Vector3 pos, float rot) slot, Vector3 caseDim, out Vector2 min, out Vector2 max)
    {
        bool turned = Mathf.Abs(Mathf.DeltaAngle(slot.rot, 90f)) < 45f || Mathf.Abs(Mathf.DeltaAngle(slot.rot, 270f)) < 45f;
        float hx = (turned ? caseDim.z : caseDim.x) * 0.5f;
        float hz = (turned ? caseDim.x : caseDim.z) * 0.5f;
        min = new Vector2(slot.pos.x - hx, slot.pos.z - hz);
        max = new Vector2(slot.pos.x + hx, slot.pos.z + hz);
    }

    private void RebuildPattern(Vector3 caseDim)
    {
        var result = PalletOptimizer.PackLayer(PalletDim.x, PalletDim.z, caseDim.x, caseDim.z, SpaceBetweenCases);
        float halfW = PalletDim.x / 2f;
        float halfL = PalletDim.z / 2f;
        _currentPattern = new List<(Vector3, float)>();
        foreach (var s in result.Slots)
            _currentPattern.Add((new Vector3(s.x - halfW, 0, s.z - halfL), s.rotationDegrees));
    }
}
