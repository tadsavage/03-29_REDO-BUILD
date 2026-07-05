using UnityEditor;
using UnityEngine;
using GameCore.Inventory;

/// <summary>
/// One-time (re-runnable) batch tool: recomputes Ti/Hi for every SkuData asset via
/// PalletOptimizer, targeting the 1m or 1.8m rack tier by case height
/// (PalletOptimizer.DetermineTargetPalletHeight), and commits the result to each SKU's master
/// record. Replaces hand-guessed Ti/Hi with numbers verified to actually fit a real 40"x48"
/// pallet footprint and the warehouse's real rack heights. Added 2026-07-05 per Tad's request.
/// </summary>
public static class PalletOptimizerBatchTool
{
    [MenuItem("Tools/Inventory Tools/Recompute Ti-Hi For All SKUs (Pallet Optimizer)")]
    public static void RecomputeAll()
    {
        var guids = AssetDatabase.FindAssets("t:SkuData");
        int updated = 0, skipped = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sku = AssetDatabase.LoadAssetAtPath<SkuData>(path);
            if (sku == null) continue;

            if (sku.CaseLength <= 0f || sku.CaseWidth <= 0f || sku.CaseHeight <= 0f)
            {
                Debug.LogWarning($"[PalletOptimizerBatchTool] '{sku.ItemDescription}' has invalid case dimensions — skipped.");
                skipped++;
                continue;
            }

            int oldTi = sku.Ti, oldHi = sku.Hi;
            float targetHeight = PalletOptimizer.DetermineTargetPalletHeight(sku.CaseHeight);
            var result = PalletOptimizer.OptimizeLoad(
                sku.CaseLength * 100f,
                sku.CaseWidth * 100f,
                sku.CaseHeight * 100f,
                targetHeight);

            if (result.CasesPerLayer <= 0 || result.Layers <= 0)
            {
                Debug.LogWarning($"[PalletOptimizerBatchTool] '{sku.ItemDescription}' produced an invalid layout ({result.CasesPerLayer}x{result.Layers}) — skipped, left at Ti={oldTi} Hi={oldHi}.");
                skipped++;
                continue;
            }

            var so = new SerializedObject(sku);
            so.FindProperty("_ti").intValue = result.CasesPerLayer;
            so.FindProperty("_hi").intValue = result.Layers;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(sku);

            string tier = targetHeight >= PalletOptimizer.TallRackHeightMeters ? "1.8m" : "1.0m";
            Debug.Log($"[PalletOptimizerBatchTool] {sku.ItemDescription} ({sku.ItemNumber}): " +
                      $"Ti {oldTi}->{result.CasesPerLayer}, Hi {oldHi}->{result.Layers}, " +
                      $"tier={tier}, {result.LayerPatternDescription}, " +
                      $"height={result.TotalHeightMeters:F2}m, utilization={result.VolumeUtilization:F1}%");
            updated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PalletOptimizerBatchTool] Done. Updated {updated} SKUs, skipped {skipped}.");
    }
}
