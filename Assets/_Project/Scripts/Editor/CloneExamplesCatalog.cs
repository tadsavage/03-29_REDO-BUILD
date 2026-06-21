using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CloneExamplesCatalog
{
    private const string ContainerName = "[Clone Examples]";
    private static double _rebuildAt = -1;

    static CloneExamplesCatalog()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                // Delay 1 second so all Start() methods finish spawning/loading objects
                _rebuildAt = EditorApplication.timeSinceStartup + 1.0;
                EditorApplication.update += WaitThenRebuild;
                break;

            case PlayModeStateChange.ExitingPlayMode:
                EditorApplication.update -= WaitThenRebuild;
                ClearCatalog();
                break;
        }
    }

    private static void WaitThenRebuild()
    {
        if (EditorApplication.timeSinceStartup < _rebuildAt) return;
        EditorApplication.update -= WaitThenRebuild;
        RebuildCatalog();
    }

    [MenuItem("Tools/Clone Examples/Rebuild Catalog")]
    private static void RebuildCatalog()
    {
        var container = GameObject.Find(ContainerName);
        if (container != null) Object.DestroyImmediate(container);
        container = new GameObject(ContainerName);

        var placed = Object.FindObjectsByType<PlacedObject>(FindObjectsInactive.Exclude);

        // One representative per unique ObjDataSO id
        var seen = new Dictionary<int, PlacedObject>();
        foreach (var p in placed)
        {
            if (p.data == null) continue;
            if (!seen.ContainsKey(p.data.id))
                seen[p.data.id] = p;
        }

        // Group by category
        var byCategory = new Dictionary<string, List<PlacedObject>>();
        foreach (var p in seen.Values)
        {
            string cat = !string.IsNullOrEmpty(p.data.category) ? p.data.category : "Misc";
            if (!byCategory.ContainsKey(cat))
                byCategory[cat] = new List<PlacedObject>();
            byCategory[cat].Add(p);
        }

        foreach (var kv in byCategory)
        {
            var catGO = new GameObject($"[{kv.Key}]");
            catGO.transform.SetParent(container.transform, false);

            foreach (var p in kv.Value)
            {
                string label = !string.IsNullOrEmpty(p.data.objName) ? p.data.objName : p.name;
                var entryGO = new GameObject(label);
                entryGO.transform.SetParent(catGO.transform, false);
                var ce = entryGO.AddComponent<CatalogEntry>();
                ce.typeName     = label;
                ce.category     = kv.Key;
                ce.liveInstance = p.gameObject;
            }
        }

        Selection.activeGameObject = container;
        Debug.Log($"[Clone Examples] Built: {seen.Count} unique types in {byCategory.Count} categories.");
    }

    [MenuItem("Tools/Clone Examples/Clear Catalog")]
    private static void ClearCatalog()
    {
        var container = GameObject.Find(ContainerName);
        if (container != null) Object.DestroyImmediate(container);
    }

    // Manual menu items only available in Play Mode
    [MenuItem("Tools/Clone Examples/Rebuild Catalog", true)]
    [MenuItem("Tools/Clone Examples/Clear Catalog", true)]
    private static bool ValidatePlayMode() => Application.isPlaying;
}
