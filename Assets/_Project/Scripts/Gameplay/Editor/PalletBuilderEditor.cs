using UnityEditor;
using UnityEngine;
using GameCore.Inventory;

[CustomEditor(typeof(PalletBuilder))]
public class PalletBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PalletBuilder builder = (PalletBuilder)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Build Pallet", GUILayout.Height(30)))
        {
            builder.Build();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Pallet Optimizer", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledGroupScope(builder.linkedSku == null))
        {
            if (GUILayout.Button("Auto-Compute Ti/Hi From Linked SKU", GUILayout.Height(26)))
            {
                var result = builder.ComputeOptimalTiHi();
                EditorUtility.SetDirty(builder);
                builder.Build();
                Debug.Log($"[PalletBuilderEditor] {builder.linkedSku.ItemDescription}: " +
                          $"Ti={result.CasesPerLayer} Hi={result.Layers} " +
                          $"({result.LayerPatternDescription}) — " +
                          $"{result.VolumeUtilization:F1}% surface utilization, " +
                          $"{result.TotalHeightMeters:F2}m total height.");
            }

            if (GUILayout.Button("Submit Ti/Hi To Master Record (SkuData)", GUILayout.Height(26)))
            {
                SubmitToMasterRecord(builder);
            }
        }

        if (builder.linkedSku == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Linked SKU above to auto-compute Ti/Hi from its real case dimensions, " +
                "or to submit a manually-tuned Ti/Hi back to that SKU's master record.",
                MessageType.Info);
        }
    }

    /// <summary>Writes the builder's current manualTi/manualHi back onto linkedSku's private
    /// _ti/_hi fields via SerializedObject (same pattern as CaseGeneratorTool) — this is the
    /// explicit, separate step that actually commits a previewed layout to the item master
    /// record, so merely clicking "Auto-Compute" or hand-tweaking Ti/Hi in the inspector never
    /// silently changes SkuData.</summary>
    private void SubmitToMasterRecord(PalletBuilder builder)
    {
        if (builder.linkedSku == null) return;

        var so = new SerializedObject(builder.linkedSku);
        so.FindProperty("_ti").intValue = builder.manualTi;
        so.FindProperty("_hi").intValue = builder.manualHi;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(builder.linkedSku);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PalletBuilderEditor] Submitted Ti={builder.manualTi} Hi={builder.manualHi} " +
                  $"to {builder.linkedSku.ItemDescription}'s master record " +
                  $"(new PltHeight = {builder.linkedSku.PltHeight:F2}m).");
    }
}
