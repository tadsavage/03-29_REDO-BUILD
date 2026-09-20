using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure math: computes the optimal Ti (cases per layer) / Hi (layers) for a case of given
/// dimensions against a target max pallet height, on a standard 40"x48" pallet footprint.
/// Static utility (not a MonoBehaviour) so both PalletBuilder (runtime rendering) and Editor
/// tooling (master-record authoring) call the same numbers — added 2026-07-05 per Tad's request
/// to stop hand-guessing Ti/Hi and compute it from real case dimensions + rack height limits.
///
/// Two rack tiers exist in the warehouse: 1.0m ("48-inch") and 1.8m ("80-inch") floor-to-rack-
/// bottom clearance. <see cref="DetermineTargetPalletHeight"/> is the single place that decides
/// which tier a case targets, by case height — keep in sync if that rule changes.
/// </summary>
public static class PalletOptimizer
{
    public const float ShortRackHeightMeters = 1.0f;
    public const float TallRackHeightMeters = 1.8f;

    /// <summary>Case-height cutoff (meters) separating "short/medium" cases (targets the 1m rack
    /// tier) from "large" cases (targets the 1.8m tier). Checked against all 30 real SKUs
    /// (2026-07-05): splits 22/8 short/tall with no item within 2.5cm of the boundary, and both
    /// tiers land on 3+ usable layers for every item — see CLAUDE.md for the full breakdown.</summary>
    public const float TierThresholdCaseHeightMeters = 0.23f;

    /// <summary>Which rack tier (in meters) a case of this height should target.</summary>
    public static float DetermineTargetPalletHeight(float caseHeightMeters)
        => caseHeightMeters > TierThresholdCaseHeightMeters ? TallRackHeightMeters : ShortRackHeightMeters;

    // Struct to hold the result of a placement calculation
    public struct PalletResult
    {
        public int TotalCases;
        public int Layers;
        public int CasesPerLayer;
        public float TotalHeightMeters;
        public float VolumeUtilization; // Percentage 0-100
        public string LayerPatternDescription;
    }

    /// <summary>One case's footprint placement within a layer, in meters, relative to the
    /// bottom-left corner of the region that was packed (NOT yet centered on the pallet).</summary>
    public struct CaseSlot
    {
        public float x;
        public float z;
        public float rotationDegrees; // 0 or 90 around Y
    }

    /// <summary>Result of packing a rectangular region with identical rectangular cases.</summary>
    public struct PackResult
    {
        public List<CaseSlot> Slots;
        public string Description;

        public int Count => Slots.Count;
    }

    /// <summary>
    /// Packs a rectangular region (typically the pallet's 40"x48" footprint) with as many
    /// identical case footprints as will fit, maximizing covered surface area — NOT biased toward
    /// any particular orientation. Real palletizing has no universally "correct" layer pattern
    /// except "cover as much of the deck as possible"; this searches a real (if bounded) space of
    /// candidate layouts rather than picking between a few hardcoded patterns.
    ///
    /// Strategy: recursive guillotine-cut packing. At each level, try a uniform grid in both
    /// orientations (case length along region width, or along region length), THEN try splitting
    /// the region into two strips — along either axis, at every offset that lands on a case-aligned
    /// boundary — and recursively pack each strip, keeping whichever split (if any) beats a plain
    /// grid. This is the same family of technique real palletizing/cutting-stock software uses
    /// (mixed "brick"/pinwheel layers are exactly a 2-level guillotine split) and is exhaustive over
    /// every case-aligned cut point, not just a couple of hand-picked patterns.
    /// </summary>
    /// <param name="regionWidth">Region width (meters) — the pallet's short side by convention.</param>
    /// <param name="regionLength">Region length (meters) — the pallet's long side by convention.</param>
    /// <param name="caseWidth">Case width (meters), un-rotated.</param>
    /// <param name="caseLength">Case length (meters), un-rotated.</param>
    /// <param name="gap">Minimum gap enforced between adjacent cases (meters).</param>
    /// <param name="maxSplitDepth">How many levels of guillotine splitting to search. 2 already
    /// covers straight, turned, and 2-way/4-way mixed-strip layouts; raised past ~3 the search
    /// grows quickly for very little additional yield on a single pallet footprint.</param>
    /// <param name="isTopLevel">Leave at the default (true) for every real caller. Set false only by
    /// TrySplitAxis's own recursive sub-region calls — corner support only means something for the
    /// pallet's actual four corners, not the artificial cut boundaries of an internal search strip.</param>
    public static PackResult PackLayer(float regionWidth, float regionLength, float caseWidth, float caseLength,
        float gap = 0f, int maxSplitDepth = 2, bool isTopLevel = true)
    {
        var best = GridFill(regionWidth, regionLength, caseWidth, caseLength, gap);

        if (maxSplitDepth > 0 && caseWidth > 0f && caseLength > 0f)
        {
            TrySplitAxis(ref best, regionWidth, regionLength, caseWidth, caseLength, gap, maxSplitDepth, splitAlongWidth: true);
            TrySplitAxis(ref best, regionWidth, regionLength, caseWidth, caseLength, gap, maxSplitDepth, splitAlongWidth: false);
        }

        if (isTopLevel) SnapCasesToCorners(ref best, regionWidth, regionLength, caseWidth, caseLength, gap);

        return best;
    }

    /// <summary>Post-process: makes sure every one of the region's 4 corners has an actual case under
    /// it. The guillotine search above only maximizes case COUNT — it can (and does) land on a layout
    /// that's a grid plus a leftover strip of turned cases reaching only partway along an edge,
    /// leaving one corner completely uncovered. Since another pallet gets stacked on top of this one
    /// in-game, every corner needs support underneath it (matters even more once physics is ever
    /// turned on), so whichever placed case sits nearest an uncovered corner gets slid outward to
    /// actually touch it. The slide is only applied when it provably can't overlap another case —
    /// a failed fix just leaves the original (already-valid, just gappy) layout in place rather than
    /// risking a corrupted one.</summary>
    private static void SnapCasesToCorners(ref PackResult layer, float regionWidth, float regionLength,
        float caseWidth, float caseLength, float gap)
    {
        if (layer.Slots.Count == 0) return;

        var corners = new (float x, float z)[]
        {
            (0f, 0f), (regionWidth, 0f), (0f, regionLength), (regionWidth, regionLength)
        };

        foreach (var corner in corners)
        {
            if (IsCornerCovered(layer.Slots, corner.x, corner.z, caseWidth, caseLength, gap)) continue;

            int nearest = FindNearestSlotIndex(layer.Slots, corner.x, corner.z);
            if (nearest < 0) continue;

            var candidate = layer.Slots[nearest];
            GetHalfExtents(candidate.rotationDegrees, caseWidth, caseLength, out float hx, out float hz);
            candidate.x = corner.x <= regionWidth / 2f ? hx : regionWidth - hx;
            candidate.z = corner.z <= regionLength / 2f ? hz : regionLength - hz;

            if (OverlapsAnyOther(layer.Slots, nearest, candidate, caseWidth, caseLength, gap)) continue;

            layer.Slots[nearest] = candidate;
        }
    }

    private static void GetHalfExtents(float rotationDegrees, float caseWidth, float caseLength, out float hx, out float hz)
    {
        bool turned = Mathf.Abs(rotationDegrees - 90f) < 0.01f;
        hx = (turned ? caseLength : caseWidth) / 2f;
        hz = (turned ? caseWidth : caseLength) / 2f;
    }

    private static bool IsCornerCovered(List<CaseSlot> slots, float cornerX, float cornerZ,
        float caseWidth, float caseLength, float gap)
    {
        float epsilon = Mathf.Max(0.001f, gap * 0.5f);
        foreach (var s in slots)
        {
            GetHalfExtents(s.rotationDegrees, caseWidth, caseLength, out float hx, out float hz);
            if (cornerX >= s.x - hx - epsilon && cornerX <= s.x + hx + epsilon &&
                cornerZ >= s.z - hz - epsilon && cornerZ <= s.z + hz + epsilon)
                return true;
        }
        return false;
    }

    private static int FindNearestSlotIndex(List<CaseSlot> slots, float cornerX, float cornerZ)
    {
        int best = -1;
        float bestDistSq = float.MaxValue;
        for (int i = 0; i < slots.Count; i++)
        {
            float dx = slots[i].x - cornerX;
            float dz = slots[i].z - cornerZ;
            float distSq = dx * dx + dz * dz;
            if (distSq < bestDistSq) { bestDistSq = distSq; best = i; }
        }
        return best;
    }

    private static bool OverlapsAnyOther(List<CaseSlot> slots, int skipIndex, CaseSlot candidate,
        float caseWidth, float caseLength, float gap)
    {
        GetHalfExtents(candidate.rotationDegrees, caseWidth, caseLength, out float chx, out float chz);
        for (int i = 0; i < slots.Count; i++)
        {
            if (i == skipIndex) continue;
            GetHalfExtents(slots[i].rotationDegrees, caseWidth, caseLength, out float hx, out float hz);
            float dx = Mathf.Abs(slots[i].x - candidate.x);
            float dz = Mathf.Abs(slots[i].z - candidate.z);
            if (dx < chx + hx + gap - 0.0005f && dz < chz + hz + gap - 0.0005f)
                return true;
        }
        return false;
    }

    private static void TrySplitAxis(ref PackResult best, float regionWidth, float regionLength,
        float caseWidth, float caseLength, float gap, int maxSplitDepth, bool splitAlongWidth)
    {
        float axisExtent = splitAlongWidth ? regionWidth : regionLength;

        // Candidate cut points: every case-aligned boundary along this axis, using either the
        // case's width or length as the repeating unit (covers straight and turned sub-grids).
        var offsets = new List<float>();
        for (float m = caseWidth; m < axisExtent - 0.001f; m += caseWidth) offsets.Add(m);
        for (float m = caseLength; m < axisExtent - 0.001f; m += caseLength) offsets.Add(m);

        foreach (var offset in offsets)
        {
            float firstDim = offset;
            float secondDim = axisExtent - offset - gap;
            if (secondDim <= 0.01f) continue;

            PackResult a, b;
            if (splitAlongWidth)
            {
                a = PackLayer(firstDim, regionLength, caseWidth, caseLength, gap, maxSplitDepth - 1, isTopLevel: false);
                b = PackLayer(secondDim, regionLength, caseWidth, caseLength, gap, maxSplitDepth - 1, isTopLevel: false);
            }
            else
            {
                a = PackLayer(regionWidth, firstDim, caseWidth, caseLength, gap, maxSplitDepth - 1, isTopLevel: false);
                b = PackLayer(regionWidth, secondDim, caseWidth, caseLength, gap, maxSplitDepth - 1, isTopLevel: false);
            }

            int combinedCount = a.Count + b.Count;
            if (combinedCount <= best.Count) continue;

            float shiftX = splitAlongWidth ? (firstDim + gap) : 0f;
            float shiftZ = splitAlongWidth ? 0f : (firstDim + gap);

            var slots = new List<CaseSlot>(a.Slots);
            foreach (var s in b.Slots)
                slots.Add(new CaseSlot { x = s.x + shiftX, z = s.z + shiftZ, rotationDegrees = s.rotationDegrees });

            best = new PackResult
            {
                Slots = slots,
                Description = $"Split[{(splitAlongWidth ? "W" : "L")}@{offset:F2}m] ({a.Count}+{b.Count})"
            };
        }
    }

    /// <summary>Fills a region with a single uniform grid of cases, trying both the un-rotated and
    /// 90°-rotated orientation and preferring the "straight" orientation (case length along region
    /// length) unless the "turned" orientation packs substantially more (30%+ bonus).
    ///
    /// CRITICAL FIX (2026-07-05): After computing the grid, check if the layer's aspect ratio
    /// is backwards relative to the pallet's aspect ratio. If the pallet is longer than wide
    /// but the grid is wider than deep, swap them — the layer must match the pallet's footprint
    /// orientation to render correctly. All 30 cases in the real SKU set have their entire
    /// layer rendering transposed (wrong) without this check.</summary>
    private static PackResult GridFill(float regionWidth, float regionLength, float caseWidth, float caseLength, float gap)
    {
        int straightX = Mathf.FloorToInt((regionWidth + gap) / (caseWidth + gap));
        int straightZ = Mathf.FloorToInt((regionLength + gap) / (caseLength + gap));
        int straightCount = Mathf.Max(0, straightX) * Mathf.Max(0, straightZ);

        int turnedX = Mathf.FloorToInt((regionWidth + gap) / (caseLength + gap));
        int turnedZ = Mathf.FloorToInt((regionLength + gap) / (caseWidth + gap));
        int turnedCount = Mathf.Max(0, turnedX) * Mathf.Max(0, turnedZ);

        // Prefer straight orientation (case length along region length) unless turned packs
        // significantly more cases (50%+ threshold). Cases with distinct long dimensions should
        // align their length with the pallet's length for proper positioning, even if it costs
        // a modest number of cases. Only flip to turned if the packing gain is substantial.
        bool useTurned = turnedCount > straightCount * 1.5f;
        int countX = useTurned ? turnedX : straightX;
        int countZ = useTurned ? turnedZ : straightZ;
        float pieceW = useTurned ? caseLength : caseWidth;
        float pieceL = useTurned ? caseWidth : caseLength;
        float rot = useTurned ? 90f : 0f;

        // ASPECT RATIO FIX: check if the grid is transposed relative to the pallet.
        // If the pallet is longer than wide but the computed grid is wider than deep,
        // swap them so the layer's aspect ratio matches the pallet's footprint.
        bool palletIsLongerThanWide = regionLength > regionWidth;
        bool gridIsWiderThanDeep = countX > countZ;
        if (palletIsLongerThanWide && gridIsWiderThanDeep)
        {
            // Swap: the layer was computed sideways. Flip countX/countZ and rotate 90°.
            int temp = countX;
            countX = countZ;
            countZ = temp;
            rot = rot == 0f ? 90f : 0f;  // Toggle rotation
            float tempW = pieceW;
            pieceW = pieceL;
            pieceL = tempW;
        }

        var slots = new List<CaseSlot>();
        if (countX > 0 && countZ > 0)
        {
            for (int zi = 0; zi < countZ; zi++)
            {
                for (int xi = 0; xi < countX; xi++)
                {
                    slots.Add(new CaseSlot
                    {
                        x = xi * (pieceW + gap) + pieceW / 2f,
                        z = zi * (pieceL + gap) + pieceL / 2f,
                        rotationDegrees = rot
                    });
                }
            }
        }

        return new PackResult { Slots = slots, Description = $"Grid {countX}x{countZ} @ {rot:F0}°" };
    }

    /// <summary>
    /// Calculates the optimal palletization layout using metric data.
    /// </summary>
    /// <param name="caseLengthCm">Case length in centimeters.</param>
    /// <param name="caseWidthCm">Case width in centimeters.</param>
    /// <param name="caseHeightCm">Case height in centimeters.</param>
    /// <param name="maxPalletHeightMeters">Maximum allowable total height of the loaded pallet in meters.</param>
    /// <returns>A PalletResult containing the optimized layout data.</returns>
    public static PalletResult OptimizeLoad(float caseLengthCm, float caseWidthCm, float caseHeightCm, float maxPalletHeightMeters)
    {
        // 1. Standard GMA pallet (40" x 48") in meters — world X = 48" (long), world Z = 40" (short).
        float palletWidthM = 1.2192f;  // World X axis = 48" (long side)
        float palletLengthM = 1.016f;  // World Z axis = 40" (short side)
        float palletBaseHeightM = 0.165f; // Standard pallet thickness

        // 2. Convert case dimensions from cm to meters
        float cLen = caseLengthCm / 100f;
        float cWid = caseWidthCm / 100f;
        float cHgt = caseHeightCm / 100f;

        // 3. Determine maximum cargo height available
        float maxCargoHeight = maxPalletHeightMeters - palletBaseHeightM;
        if (maxCargoHeight <= 0)
        {
            Debug.LogError("[PalletOptimizer] Maximum pallet height must be greater than the pallet base height (0.165m).");
            return new PalletResult();
        }

        // 4. Calculate total number of possible layers
        int totalLayers = Mathf.FloorToInt(maxCargoHeight / cHgt);
        if (totalLayers <= 0)
        {
            Debug.LogWarning("[PalletOptimizer] Case height is taller than the available cargo height limit.");
            return new PalletResult();
        }

        // 5. Pack a single layer, maximizing surface coverage — see PackLayer for the search strategy.
        // regionWidth = 48" (world X), regionLength = 40" (world Z)
        var layer = PackLayer(palletWidthM, palletLengthM, cWid, cLen);

        // 6. Calculate final totals and metrics
        int totalCases = layer.Count * totalLayers;
        float actualCargoHeight = totalLayers * cHgt;
        float finalTotalHeight = actualCargoHeight + palletBaseHeightM;

        float palletSurfaceArea = palletWidthM * palletLengthM;
        float caseSurfaceArea = cLen * cWid;
        float areaUtilization = ((layer.Count * caseSurfaceArea) / palletSurfaceArea) * 100f;

        return new PalletResult
        {
            TotalCases = totalCases,
            Layers = totalLayers,
            CasesPerLayer = layer.Count,
            TotalHeightMeters = finalTotalHeight,
            VolumeUtilization = Mathf.Clamp(areaUtilization, 0f, 100f),
            LayerPatternDescription = layer.Description
        };
    }
}
