using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools/Setup Benches
/// Creates Bench1 and Bench2 game-ready prefabs from the HEXXX source meshes,
/// generates ObjDataSO assets, captures thumbnails, registers everything in
/// ObjDataRegistry and adds both items to the Flavor category in BuildMenuUI.
///
/// After running: save the scene (Ctrl+S) then run Tools/ObjData/Auto-Assign Unique IDs.
/// </summary>
public static class BenchSetup
{
    const string BENCH1_SRC  = "Assets/HEXXX/Hyper Casual Urban Asset Pack/Prefab/Bench.prefab";
    const string BENCH2_SRC  = "Assets/HEXXX/Hyper Casual Urban Asset Pack/Prefab/Bench_2.prefab";
    const string PREFAB_DIR  = "Assets/2. Prefabs/Flavor(Misc)";
    const string SO_DIR      = "Assets/1. Scripts/3. ScriptableObjects/PrefabSOs";
    const string ICON_DIR    = "Assets/6. Art/Icons";
    const string REGISTRY    = "Assets/1. Scripts/3. ScriptableObjects/Obj Data Registry.asset";

    // Half-span of one cell (CellSize=1.33) — visual center between cells (0,0) and (1,0).
    const float CELL_HALF = 0.665f;

    [MenuItem("Tools/Setup Benches")]
    public static void Run()
    {
        var src1 = AssetDatabase.LoadAssetAtPath<GameObject>(BENCH1_SRC);
        var src2 = AssetDatabase.LoadAssetAtPath<GameObject>(BENCH2_SRC);
        if (!src1 || !src2) { Debug.LogError("[BenchSetup] Source bench prefabs not found."); return; }

        // --- 1. Create wrapper prefabs ---
        string path1 = $"{PREFAB_DIR}/Bench1.prefab";
        string path2 = $"{PREFAB_DIR}/Bench2.prefab";
        var prefab1 = BuildWrapper(src1, "Bench1", path1);
        var prefab2 = BuildWrapper(src2, "Bench2", path2);

        // --- 2. Create ObjDataSO assets (id will be finalised by Auto-Assign tool) ---
        var so1 = MakeSO("Bench1", prefab1, 98, $"{SO_DIR}/Bench1.asset");
        var so2 = MakeSO("Bench2", prefab2, 99, $"{SO_DIR}/Bench2.asset");

        // --- 3. Back-assign SO into prefab PlacedObject + BuildingData ---
        LinkSOtoPrefab(path1, so1);
        LinkSOtoPrefab(path2, so2);

        // --- 4. Generate thumbnails and assign ---
        so1.icon = CaptureThumbnail(prefab1, "Bench1 Thumbnail");
        so2.icon = CaptureThumbnail(prefab2, "Bench2 Thumbnail");
        EditorUtility.SetDirty(so1);
        EditorUtility.SetDirty(so2);

        // --- 5. Add to ObjDataRegistry ---
        AddToRegistry(so1, so2);

        // --- 6. Add to BuildMenuUI Flavor category in open scene ---
        AddToFlavorCategory(so1, so2);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BenchSetup] Done. Save the scene (Ctrl+S), then run Tools/ObjData/Auto-Assign Unique IDs.");
    }

    // -------------------------------------------------------------------------
    // Prefab builder
    // -------------------------------------------------------------------------
    static GameObject BuildWrapper(GameObject source, string wrapperName, string savePath)
    {
        var root = new GameObject(wrapperName);

        root.AddComponent<PlacedObject>();
        root.AddComponent<BuildingData>();
        root.AddComponent<BuildingHighlighter>();

        // Trigger BoxCollider — sized to fit the bench visual (approx 1.7 × 0.9 × 0.75).
        // Centered so it sits above y=0 and is offset to match the child's position.
        var bc      = root.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.center    = new Vector3(CELL_HALF, 0.45f, 0f);
        bc.size      = new Vector3(1.7f,      0.9f,  0.75f);

        // Nest the source bench as a PrefabInstance child, shifted so the mesh
        // is visually centred between the two grid cells (0,0) and (1,0).
        var child = (GameObject)PrefabUtility.InstantiatePrefab(source);
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = new Vector3(CELL_HALF, 0f, 0f);

        var saved = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);

        Debug.Log($"[BenchSetup] Prefab created: {savePath}");
        return saved;
    }

    // -------------------------------------------------------------------------
    // ObjDataSO factory
    // -------------------------------------------------------------------------
    static ObjDataSO MakeSO(string objName, GameObject prefab, int initialId, string assetPath)
    {
        var so = ScriptableObject.CreateInstance<ObjDataSO>();
        so.id                  = initialId;
        so.objName             = objName;
        so.cost                = 500;
        so.hourlyCost          = 0;
        so.category            = "Flavor";
        so.prefab              = prefab;
        so.ignorePlacementRules = false;
        so.isFloor             = false;
        so.ClearsGridAfterPlacement = false;
        so.pathfindingClear    = false;
        so.isStackable         = false;
        so.objHeight           = 0.9f;
        so.footprint           = new Vector2Int(2, 1);
        so.customShapeOffsets  = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };

        AssetDatabase.CreateAsset(so, assetPath);
        Debug.Log($"[BenchSetup] ObjDataSO created: {assetPath}");
        return so;
    }

    // -------------------------------------------------------------------------
    // Assign SO into prefab PlacedObject.data and BuildingData.objDataSO
    // -------------------------------------------------------------------------
    static void LinkSOtoPrefab(string prefabPath, ObjDataSO so)
    {
        var contents = PrefabUtility.LoadPrefabContents(prefabPath);

        var po = contents.GetComponent<PlacedObject>();
        if (po != null) po.data = so;

        var bd = contents.GetComponent<BuildingData>();
        if (bd != null)
        {
            var serialized = new SerializedObject(bd);
            var prop       = serialized.FindProperty("objDataSO");
            if (prop != null) { prop.objectReferenceValue = so; serialized.ApplyModifiedProperties(); }
        }

        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
    }

    // -------------------------------------------------------------------------
    // Thumbnail generator — renders the prefab with a temp camera
    // -------------------------------------------------------------------------
    static Texture2D CaptureThumbnail(GameObject prefab, string thumbName)
    {
        // Spawn prefab far from the origin so it doesn't interfere with the scene
        var instance = (GameObject)Object.Instantiate(prefab, new Vector3(9999f, 0f, 9999f), Quaternion.identity);

        // Calculate world-space bounds of the bench mesh
        var renderers = instance.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(instance.transform.position, Vector3.one);
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        // Render texture
        int size = 256;
        var rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32);

        // Temp camera positioned for a pleasant 3/4 elevated front view
        var camGO = new GameObject("_BenchThumbCam");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.25f, 0.50f, 0.68f);  // muted sky blue
        cam.fieldOfView     = 38f;
        cam.nearClipPlane   = 0.05f;
        cam.farClipPlane    = 50f;
        cam.targetTexture   = rt;

        Vector3 center = bounds.center;
        float   extent = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        camGO.transform.position = center + new Vector3(extent * 0.5f, extent * 1.1f, -extent * 1.6f);
        camGO.transform.LookAt(center);

        cam.Render();

        // Read pixels from the render texture
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // Save PNG
        string pngPath = $"{ICON_DIR}/{thumbName}.png";
        File.WriteAllBytes(pngPath, tex.EncodeToPNG());

        // Cleanup temp scene objects
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(instance);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);

        // Import asset and set as Default Texture2D (not Sprite — ObjDataSO.icon is Texture2D)
        AssetDatabase.ImportAsset(pngPath);
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.spriteImportMode  = SpriteImportMode.None;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        Debug.Log($"[BenchSetup] Thumbnail saved: {pngPath}");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
    }

    // -------------------------------------------------------------------------
    // ObjDataRegistry
    // -------------------------------------------------------------------------
    static void AddToRegistry(ObjDataSO so1, ObjDataSO so2)
    {
        var reg = AssetDatabase.LoadAssetAtPath<ObjDataRegistry>(REGISTRY);
        if (!reg) { Debug.LogError("[BenchSetup] ObjDataRegistry not found at " + REGISTRY); return; }

        bool dirty = false;
        if (!reg.buttonSOs.Contains(so1)) { reg.buttonSOs.Add(so1); dirty = true; }
        if (!reg.buttonSOs.Contains(so2)) { reg.buttonSOs.Add(so2); dirty = true; }
        if (dirty) EditorUtility.SetDirty(reg);

        Debug.Log("[BenchSetup] ObjDataRegistry updated.");
    }

    // -------------------------------------------------------------------------
    // BuildMenuUI — add to Flavor category via SerializedObject
    // -------------------------------------------------------------------------
    static void AddToFlavorCategory(ObjDataSO so1, ObjDataSO so2)
    {
        var buildMenuUI = Object.FindAnyObjectByType<BuildMenuUI>();
        if (!buildMenuUI)
        {
            Debug.LogWarning("[BenchSetup] BuildMenuUI not found. Open the Main scene and re-run.");
            return;
        }

        var serialized      = new SerializedObject(buildMenuUI);
        var categoriesProp  = serialized.FindProperty("categories");

        if (categoriesProp == null)
        {
            Debug.LogError("[BenchSetup] Could not find 'categories' property on BuildMenuUI.");
            return;
        }

        // Find the Flavor entry
        int flavorIndex = -1;
        for (int i = 0; i < categoriesProp.arraySize; i++)
        {
            var elem        = categoriesProp.GetArrayElementAtIndex(i);
            var idProp      = elem.FindPropertyRelative("id");
            var displayProp = elem.FindPropertyRelative("displayName");
            string id       = idProp?.stringValue   ?? "";
            string display  = displayProp?.stringValue ?? "";
            if (id == "Flavor" || display == "Flavor")
            {
                flavorIndex = i;
                break;
            }
        }

        if (flavorIndex < 0)
        {
            Debug.LogWarning("[BenchSetup] Flavor category not found in BuildMenuUI. Adding new category.");
            // Create a new Flavor category
            categoriesProp.InsertArrayElementAtIndex(categoriesProp.arraySize);
            var newCat = categoriesProp.GetArrayElementAtIndex(categoriesProp.arraySize - 1);
            newCat.FindPropertyRelative("id").stringValue          = "Flavor";
            newCat.FindPropertyRelative("displayName").stringValue = "Flavor";
            flavorIndex = categoriesProp.arraySize - 1;
        }

        var flavor    = categoriesProp.GetArrayElementAtIndex(flavorIndex);
        var itemsProp = flavor.FindPropertyRelative("items");

        // Add so1 if not already present
        if (!SOAlreadyInList(itemsProp, so1))
        {
            itemsProp.InsertArrayElementAtIndex(itemsProp.arraySize);
            itemsProp.GetArrayElementAtIndex(itemsProp.arraySize - 1).objectReferenceValue = so1;
        }

        // Add so2 if not already present
        if (!SOAlreadyInList(itemsProp, so2))
        {
            itemsProp.InsertArrayElementAtIndex(itemsProp.arraySize);
            itemsProp.GetArrayElementAtIndex(itemsProp.arraySize - 1).objectReferenceValue = so2;
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(buildMenuUI);
        EditorSceneManager.MarkSceneDirty(buildMenuUI.gameObject.scene);

        Debug.Log("[BenchSetup] Flavor category updated in BuildMenuUI.");
    }

    static bool SOAlreadyInList(SerializedProperty listProp, ObjDataSO target)
    {
        for (int i = 0; i < listProp.arraySize; i++)
        {
            if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == target)
                return true;
        }
        return false;
    }
}
