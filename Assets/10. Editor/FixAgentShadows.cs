using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Text;

public static class FixAgentShadows
{
    private static readonly string[] AgentPrefabPaths =
    {
        "Assets/2. Prefabs/Workers/WorkerMale.prefab",
        "Assets/2. Prefabs/Workers/WorkerFemale.prefab",
        "Assets/2. Prefabs/Workers/BossNew.prefab",
        "Assets/2. Prefabs/Workers/Exterminator.prefab",
        "Assets/2. Prefabs/Workers/Security.prefab",
        "Assets/2. Prefabs/Flavor(Misc)/Rat.prefab",
        "Assets/2. Prefabs/MHE/DS.prefab",
        "Assets/2. Prefabs/MHE/DS_FULL.prefab",
        "Assets/2. Prefabs/MHE/PJ.prefab",
        "Assets/2. Prefabs/MHE/RT.prefab",
        "Assets/2. Prefabs/MHE/ScissorLift.prefab",
    };

    [MenuItem("Tools/Fix Agent Shadows")]
    public static void Run()
    {
        var report = new StringBuilder();
        int totalRenderers = 0;

        // ── 1. Fix renderer shadow flags in every agent prefab ──────────────
        foreach (var path in AgentPrefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                report.AppendLine($"  SKIP (not found): {path}");
                continue;
            }

            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            var root = scope.prefabContentsRoot;
            int count = 0;

            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows    = true;
                count++;
            }
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows    = true;
                count++;
            }

            totalRenderers += count;
            report.AppendLine($"  {System.IO.Path.GetFileName(path)}: {count} renderer(s)");
        }

        AssetDatabase.SaveAssets();

        // ── 2. Ensure the scene's directional light uses Soft shadows ────────
        var sceneReport = new StringBuilder();
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (light.type != LightType.Directional) continue;
            bool changed = light.shadows != LightShadows.Soft;
            light.shadows = LightShadows.Soft;
            sceneReport.AppendLine($"  Light '{light.name}': shadows={LightShadows.Soft}" +
                                   (changed ? " (was changed)" : " (already Soft)"));
        }

        // ── 3. Confirm URP pipeline has soft shadows enabled ─────────────────
        var urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline
                       as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;

        string urpInfo = urpAsset != null
            ? $"URP asset: '{urpAsset.name}' — supportsSoftShadows={urpAsset.supportsSoftShadows}"
            : "URP asset: not found (check Graphics Settings)";

        Debug.Log($"[FixAgentShadows] Done.\n" +
                  $"Renderers updated: {totalRenderers}\n" +
                  $"Prefabs:\n{report}" +
                  $"Lights:\n{sceneReport}" +
                  $"{urpInfo}");

        EditorUtility.DisplayDialog(
            "Fix Agent Shadows",
            $"Updated {totalRenderers} renderer(s) across {AgentPrefabPaths.Length} prefabs.\n\n" +
            $"All renderers now cast + receive shadows.\n" +
            $"Directional lights set to Soft shadows.\n\n" +
            $"{urpInfo}",
            "OK");
    }
}
