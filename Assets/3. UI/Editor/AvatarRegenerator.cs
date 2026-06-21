using UnityEditor;
using UnityEngine;
using System.IO;

public static class AvatarRegenerator
{
    private static readonly string[] ModelPaths = new[]
    {
        "Assets/5. Models/WorkerNew.fbx",
        "Assets/5. Models/WorkerFemale.fbx",
        "Assets/5. Models/BossNew.fbx",
        "Assets/5. Models/Exterminator.fbx",
        "Assets/5. Models/Security.fbx"
    };

    [MenuItem("Tools/Regenerate Worker Avatars")]
    public static void RegenerateWorkerAvatars()
    {
        int successCount = 0;
        int failCount = 0;

        foreach (string path in ModelPaths)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[AvatarRegenerator] File not found: {path}");
                failCount++;
                continue;
            }

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null)
            {
                Debug.LogError($"[AvatarRegenerator] Could not get ModelImporter for: {path}");
                failCount++;
                continue;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport();

            Debug.Log($"[AvatarRegenerator] Humanoid avatar regeneration triggered: {path}");
            successCount++;
        }

        AssetDatabase.Refresh();

        Debug.Log($"[AvatarRegenerator] Done. Success: {successCount}, Failed: {failCount}");
    }
}