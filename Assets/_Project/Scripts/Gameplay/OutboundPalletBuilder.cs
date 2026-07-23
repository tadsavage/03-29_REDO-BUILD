using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Incremental, case-by-case pallet builder for Order Selection — the outbound counterpart to
/// PalletBuilder, which only supports destroying and rebuilding an entire single-SKU pallet in one
/// shot. A selector's pallet needs to grow one case at a time, across multiple different SKUs (one
/// per order line item), without ever discarding what's already been placed.
///
/// Layout approach: each SKU gets its own layer group, stacked on top of whatever's already on the
/// pallet, using that SKU's own best-fit single-layer pattern from PalletOptimizer (same packing
/// math PalletBuilder uses). Real mixed-SKU pallets are typically built this way too — one SKU's
/// cases per layer, not interleaved with a different case footprint.
/// </summary>
public class OutboundPalletBuilder : MonoBehaviour
{
    private static readonly Vector3 PalletDim = new Vector3(1.2192f, 0.165f, 1.016f);
    private const float SpaceBetweenCases = 0.05f;
    private const float CrookedCaseDegrees = 2f;

    private Transform _loadRoot;
    private string _currentSkuId;
    private List<(Vector3 pos, float rot)> _currentPattern;
    private int _currentPatternIndex;
    private float _currentLayerY;
    private float _nextLayerY;

    public int TotalCases { get; private set; }

    /// <summary>The order this pallet was built for — set once, right after creation, by
    /// OrderSelectionTaskDriver. Lets a later system (TrailerLoadController, billing) find which
    /// order a staged pallet belongs to without needing a separate lookup table.</summary>
    public string OrderId { get; set; }

    /// <summary>Fraction of "one reference pallet's worth" of capacity consumed so far, summed
    /// across every SKU added. No cubic-footage field exists on SkuData, so each case contributes
    /// 1/(Ti*Hi) of its own SKU's full-pallet case count as a proxy for the volume it occupies —
    /// see Tad's "cubed out" spec (~40x48x72", ~70 cubic feet).</summary>
    public float FillFraction { get; private set; }

    /// <summary>True once FillFraction has reached a full reference pallet's worth — the selector
    /// stops adding to this pallet and (per Tad's spec) starts a second one, or finishes the order
    /// if a second pallet isn't available either.</summary>
    public bool IsCubedOut => FillFraction >= 1f;

    /// <summary>Adds one case of the given SKU to the pallet at the next open slot. Starting a SKU
    /// different from whatever's currently on top begins a fresh layer stacked on top of everything
    /// placed so far — matches how a selector actually works (finish this line item's picks, then
    /// move to the next), rather than interleaving different case footprints on one layer.
    /// ti/hi are the SKU's own cases-per-layer/layers-per-pallet (SkuData.Ti/Hi) — used only to
    /// accumulate FillFraction, not for layout on this mixed pallet.</summary>
    public void AddCase(string skuId, GameObject casePrefab, Vector3 caseDim, int ti, int hi)
    {
        if (casePrefab == null) return;

        if (_loadRoot == null)
        {
            var loadGo = new GameObject("PalletLoad");
            loadGo.transform.SetParent(transform, false);
            loadGo.transform.localPosition = Vector3.zero;
            _loadRoot = loadGo.transform;
            _nextLayerY = PalletDim.y;
        }

        bool newGroup = _currentPattern == null || skuId != _currentSkuId;
        if (newGroup)
        {
            RebuildPattern(caseDim);
            _currentSkuId = skuId;
            _currentPatternIndex = 0;
            _currentLayerY = _nextLayerY;
        }
        else if (_currentPatternIndex >= _currentPattern.Count)
        {
            _currentLayerY = _nextLayerY;
            _currentPatternIndex = 0;
        }

        var slot = _currentPattern[_currentPatternIndex];
        _currentPatternIndex++;

        var instance = Instantiate(casePrefab);
        instance.transform.SetParent(_loadRoot, false);
        instance.transform.localPosition = new Vector3(slot.pos.x, _currentLayerY, slot.pos.z);

        // Case prefabs carry PlacedObject/BuildingData for the build menu — must not self-register
        // into the grid at (0,0) when spawned as part of a WIP pallet. Same stripping
        // PalletBuilder.Build() performs on every case it instantiates.
        var po = instance.GetComponent<PlacedObject>();
        if (po != null) { po.enabled = false; Destroy(po); }
        var bd = instance.GetComponent<BuildingData>();
        if (bd != null) Destroy(bd);
        var bh = instance.GetComponent<BuildingHighlighter>();
        if (bh != null) Destroy(bh);

        float randomRot = Random.Range(-CrookedCaseDegrees, CrookedCaseDegrees);
        instance.transform.localRotation = Quaternion.Euler(0, slot.rot + randomRot, 0);

        _nextLayerY = _currentLayerY + caseDim.y;
        TotalCases++;
        FillFraction += 1f / Mathf.Max(1, ti * hi);
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
