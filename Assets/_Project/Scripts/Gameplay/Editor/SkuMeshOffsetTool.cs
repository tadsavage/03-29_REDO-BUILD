using UnityEditor;
using UnityEngine;
using GameCore.Inventory;

/// <summary>
/// Editor utility to auto-detect and populate mesh Y-offsets for all SKU assets.
/// Scans every SKU's case prefab, reads the MeshFilter bounds.center.y, and stores it
/// in SkuData._meshYOffsetMeters. Used after swapping pallet dimensions (2026-07-05) to
/// fix cases that clip into pallets or float above them due to non-center-origin meshes.
/// </summary>
public class SkuMeshOffsetTool : EditorWindow
{
    [MenuItem("Tools/Inventory Tools/Auto-Detect All SKU Mesh Offsets")]
    public static void AutoDetectAllMeshOffsets()
    {
        var skus = Resources.LoadAll<SkuData>("Inventory/SKUs");
        if (skus.Length == 0)
        {
            EditorUtility.DisplayDialog("Mesh Offset Tool", "No SKU assets found in Resources/Inventory/SKUs/", "OK");
            return;
        }

        int successCount = 0;
        int skipCount = 0;

        foreach (var sku in skus)
        {
            if (sku.Prefab == null)
            {
                Debug.LogWarning($"[SkuMeshOffsetTool] SKU {sku.ItemNumber} ({sku.ItemDescription}): Prefab is null, skipping.");
                skipCount++;
                continue;
            }

            MeshFilter mf = sku.Prefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                float offset = mf.sharedMesh.bounds.center.y;
                var so = new SerializedObject(sku);
                so.FindProperty("_meshYOffsetMeters").floatValue = offset;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(sku);

                Debug.Log($"[SkuMeshOffsetTool] SKU {sku.ItemNumber} ({sku.ItemDescription}): Mesh Y-offset = {offset:F4}m");
                successCount++;
            }
            else
            {
                Debug.LogWarning($"[SkuMeshOffsetTool] SKU {sku.ItemNumber} ({sku.ItemDescription}): No MeshFilter in prefab, skipping.");
                skipCount++;
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Mesh Offset Tool",
            $"Complete!\n\nUpdated: {successCount}\nSkipped: {skipCount}\n\nAll SKU mesh offsets have been auto-detected and saved.",
            "OK");
    }
}
