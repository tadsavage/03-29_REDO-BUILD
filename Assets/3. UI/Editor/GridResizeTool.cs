using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GridResizeTool
{
    [MenuItem("Tools/Grid/Resize to 100x100")]
    private static void ResizeTo100x100()
    {
        var grid = Object.FindFirstObjectByType<PlacementGrid>();
        if (grid == null)
        {
            EditorUtility.DisplayDialog("Grid Resize", "No PlacementGrid found in the active scene.", "OK");
            return;
        }

        int oldW = grid.Width;
        int oldH = grid.Height;

        if (oldW == 100 && oldH == 100)
        {
            EditorUtility.DisplayDialog("Grid Resize", "Grid is already 100×100.", "OK");
            return;
        }

        // ── Resize the grid ──────────────────────────────────────────────────
        Undo.RecordObject(grid, "Resize Grid to 100×100");
        grid.Width  = 100;
        grid.Height = 100;
        EditorUtility.SetDirty(grid);

        // ── Expand camera XZ bounds to cover the new grid ────────────────────
        // Grid world span: Origin → Origin + (Width * CellSize)
        const float pad = 10f;
        float gridMaxX = grid.Origin.x + grid.Width  * grid.CellSize;
        float gridMaxZ = grid.Origin.z + grid.Height * grid.CellSize;

        var cam = Object.FindFirstObjectByType<FreeLookCamera>();
        if (cam != null)
        {
            var so = new SerializedObject(cam);
            so.FindProperty("xMin").floatValue = grid.Origin.x - pad;
            so.FindProperty("xMax").floatValue = gridMaxX + pad;
            so.FindProperty("zMin").floatValue = grid.Origin.z - pad;
            so.FindProperty("zMax").floatValue = gridMaxZ + pad;
            so.ApplyModifiedProperties();
        }

        // ── Mark scene dirty so Ctrl+S saves the changes ─────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string camMsg = cam != null
            ? $"\nCamera XZ bounds set to [{grid.Origin.x - pad:F0}–{gridMaxX + pad:F0}] × [{grid.Origin.z - pad:F0}–{gridMaxZ + pad:F0}]."
            : "\n(FreeLookCamera not found — camera bounds unchanged.)";

        Debug.Log($"[GridResize] Resized {oldW}×{oldH} → 100×100.{camMsg}");

        EditorUtility.DisplayDialog("Grid Resize Done",
            $"Grid resized from {oldW}×{oldH} to 100×100.{camMsg}\n\n" +
            "⚠ Save the scene now (Ctrl+S).\n" +
            "⚠ Re-bake the NavMesh to cover the new area (Window → AI → Navigation → Bake).",
            "Got it");
    }
}
