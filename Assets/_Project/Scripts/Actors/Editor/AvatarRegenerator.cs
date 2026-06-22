using UnityEditor;
using UnityEngine;
using System.IO;

public static class AvatarRegenerator
{
    private static readonly string[] ModelPaths = new[]
    {
        "Assets/_Project/Models/WorkerNew.fbx",
        "Assets/_Project/Models/WorkerFemale.fbx",
        "Assets/_Project/Models/BossNew.fbx",
        "Assets/_Project/Models/Exterminator.fbx",
        "Assets/_Project/Models/Security.fbx"
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