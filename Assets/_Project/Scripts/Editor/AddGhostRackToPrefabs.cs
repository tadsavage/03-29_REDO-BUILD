using UnityEditor;
using UnityEngine;
using Warehouse;
using System.IO;

public class AddGhostRackToPrefabs
{
    [MenuItem("Tools/Setup/Add GhostRack to All Rack Prefabs")]
    public static void AddGhostRackToAllRackPrefabs()
    {
        string rackingPath = "Assets/_Project/Prefabs/Racking";

        if (!Directory.Exists(rackingPath))
        {
            Debug.LogError($"Racking directory not found: {rackingPath}");
            return;
        }

        // Find all prefab files in the directory
        string[] prefabPaths = Directory.GetFiles(rackingPath, "Rack-*.prefab");
        Debug.Log($"Found {prefabPaths.Length} rack prefabs to process");

        int addedCount = 0;
        foreach (string path in prefabPaths)
        {
            // Normalize path for Unity
            string unityPath = path.Replace("\\", "/");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(unityPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Failed to load prefab: {unityPath}");
                continue;
            }

            // Check if it already has GhostRack
            if (prefab.GetComponent<GhostRack>() != null)
            {
                Debug.Log($"✓ {unityPath} already has GhostRack");
                continue;
            }

            // Add GhostRack component
            prefab.AddComponent<GhostRack>();
            EditorUtility.SetDirty(prefab);
            addedCount++;
            Debug.Log($"✓ Added GhostRack to {unityPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"✓ Complete! Added GhostRack to {addedCount} prefabs");
    }
}
