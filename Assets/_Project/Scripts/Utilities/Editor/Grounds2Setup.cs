using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the "Grounds 2.0" diorama ground kit from Assets/_Project/Models/Diorama/DioramaKit.fbx
/// (made by build_diorama_kit.py in that folder): materials, re-pivoted meshes, prefabs, ObjDataSOs,
/// registry entries and the "Grounds 2.0" category on the bottom build bar.
///
/// Re-runnable: re-export the FBX from Blender, then run Tools > Grounds 2.0 > Build Kit again. Existing
/// assets are updated in place (same GUIDs, same ids), so placed tiles in saves keep working.
///
/// Tiles copy the existing Grounds prefabs (grd_YardPadA for paving, grd_Grass1 for grass): category
/// "Grounds", 2x2 footprint, pivot at the first cell's centre, top at y = 1.05.
/// </summary>
public static class Grounds2Setup
{
    const string Root = "Assets/_Project/Models/Diorama";
    const string FbxPath = Root + "/DioramaKit.fbx";
    const string MatDir = Root + "/Materials";
    const string MeshDir = Root + "/Meshes";
    const string PrefabDir = "Assets/_Project/Prefabs/Grounds2";
    const string SoDir = "Assets/_Project/ScriptableObjects/PrefabSOs";
    const string IconDir = "Assets/_Project/Sprites/Icons/ItemThimbnails";
    const string CategoryIcon = "Assets/_Project/Sprites/Icons/Category Icons/Grounds2.png";
    const string RegistryPath = "Assets/_Project/ScriptableObjects/Obj Data Registry.asset";
    const string CategoryId = "Grounds 2.0";

    // Authored pivot (top-centre of the 2.65 m block) -> game grounds pivot (first cell centre, top 1.05).
    static readonly Vector3 PivotOffset = new Vector3(0.6625f, 1.05f, 0.6625f);

    static readonly Dictionary<string, Color> MatColours = new Dictionary<string, Color>
    {
        { "Dio_Asphalt",   new Color(0.270f, 0.280f, 0.300f) },
        { "Dio_Concrete",  new Color(0.600f, 0.610f, 0.610f) },
        { "Dio_Grass",     new Color(0.420f, 0.620f, 0.200f) },
        { "Dio_Curb",      new Color(0.800f, 0.800f, 0.780f) },
        { "Dio_Topsoil",   new Color(0.460f, 0.330f, 0.210f) },
        { "Dio_Dirt",      new Color(0.300f, 0.210f, 0.140f) },
        { "Dio_Rock",      new Color(0.380f, 0.370f, 0.355f) },
        { "Dio_RockDark",  new Color(0.300f, 0.290f, 0.280f) },
        { "Dio_RockLight", new Color(0.500f, 0.490f, 0.470f) },
    };

    struct TileDef
    {
        public string fbxName, assetName, displayName, icon, template;
        public int id, navArea;
        public string category, gl;
        public Vector3 offset;
    }

    static readonly TileDef[] Tiles =
    {
        new TileDef { fbxName = "Dio_Tile_ParkingLot", assetName = "g2_ParkingLot", displayName = "Parking Lot",
                      icon = "Dio_ParkingLot Thumbnail.png", template = "grd_YardPadA", id = 930, navArea = 0,
                      category = "Grounds", gl = "Groundskeeping", offset = PivotOffset },
        new TileDef { fbxName = "Dio_Tile_Slab", assetName = "g2_Slab", displayName = "Concrete Slab",
                      icon = "Dio_Slab Thumbnail.png", template = "grd_YardPadA", id = 931, navArea = 0,
                      category = "Grounds", gl = "Groundskeeping", offset = PivotOffset },
        new TileDef { fbxName = "Dio_Tile_Grass", assetName = "g2_Grass", displayName = "Grass",
                      icon = "Dio_Grass Thumbnail.png", template = "grd_Grass1", id = 932, navArea = 5,
                      category = "Grounds", gl = "Groundskeeping", offset = PivotOffset },
        // Curb: 2-cell strip that sits ON the paving along the near (-Z) edge of the two cells.
        new TileDef { fbxName = "Dio_Curb", assetName = "g2_Curb", displayName = "Curb",
                      icon = "Dio_Curb Thumbnail.png", template = "Barrier2sq", id = 933, navArea = -1,
                      category = "Barrier", gl = "Barriers", offset = new Vector3(0.6625f, 1.05f, -0.6625f + 0.06f) },
    };

    [MenuItem("Tools/Grounds 2.0/Build Kit")]
    public static void BuildKit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Grounds2Setup] Stop Play mode first.");
            return;
        }
        EnsureDir(MatDir); EnsureDir(MeshDir); EnsureDir(PrefabDir);

        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null) { Debug.LogError("[Grounds2Setup] Missing " + FbxPath); return; }

        ConfigureIcons();
        var mats = BuildMaterials();
        var registry = AssetDatabase.LoadAssetAtPath<ObjDataRegistry>(RegistryPath);
        var built = new List<ObjDataSO>();

        foreach (var def in Tiles)
        {
            var src = FindChild(model.transform, def.fbxName);
            if (src == null) { Debug.LogError("[Grounds2Setup] FBX has no object " + def.fbxName); continue; }
            var mesh = BuildMesh(src.GetComponent<MeshFilter>().sharedMesh, def);
            var materials = MapMaterials(src.GetComponent<MeshRenderer>().sharedMaterials, mats);
            var so = BuildSO(def);
            BuildPrefab(def, mesh, materials, so);
            built.Add(so);

            if (registry != null && !registry.buttonSOs.Contains(so))
            {
                registry.buttonSOs.Add(so);
                EditorUtility.SetDirty(registry);
            }
        }

        AssetDatabase.SaveAssets();
        AddCategoryToScene(built);
        Debug.LogWarning($"[Grounds2Setup] Built {built.Count} Grounds 2.0 items. Save the scene (Ctrl+S) to keep the new bottom-bar category.");
    }

    static void EnsureDir(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }

    static Transform FindChild(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var r = FindChild(c, name);
            if (r != null) return r;
        }
        return null;
    }

    static void ConfigureIcons()
    {
        var template = AssetImporter.GetAtPath(IconDir + "/Grass1 Thumbnail.png") as TextureImporter;
        var catTemplate = AssetImporter.GetAtPath("Assets/_Project/Sprites/Icons/Category Icons/Foundation.png") as TextureImporter;
        foreach (var def in Tiles) CopyImporter(template, IconDir + "/" + def.icon);
        CopyImporter(catTemplate, CategoryIcon);
    }

    static void CopyImporter(TextureImporter template, string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null || template == null) return;
        var s = new TextureImporterSettings();
        template.ReadTextureSettings(s);
        imp.SetTextureSettings(s);
        imp.textureCompression = template.textureCompression;
        imp.maxTextureSize = 512;
        imp.SaveAndReimport();
    }

    static Dictionary<string, Material> BuildMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var result = new Dictionary<string, Material>();
        foreach (var kv in MatColours)
        {
            string path = MatDir + "/" + kv.Key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", kv.Value);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            result[kv.Key] = m;
        }
        return result;
    }

    static Material[] MapMaterials(Material[] imported, Dictionary<string, Material> mats)
    {
        var outMats = new Material[imported.Length];
        for (int i = 0; i < imported.Length; i++)
        {
            // The FBX importer can suffix duplicate names ("Dio_Asphalt1"), so also try without trailing digits.
            string n = imported[i] != null ? imported[i].name : "";
            if (!mats.TryGetValue(n, out outMats[i]) && !mats.TryGetValue(n.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9'), out outMats[i]))
            {
                Debug.LogWarning("[Grounds2Setup] No material for FBX slot '" + n + "', using rock.");
                outMats[i] = mats["Dio_Rock"];
            }
        }
        return outMats;
    }

    static Mesh BuildMesh(Mesh source, TileDef def)
    {
        string path = MeshDir + "/" + def.assetName + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create = mesh == null;
        if (create) mesh = new Mesh();
        mesh.Clear();
        mesh.name = def.assetName;
        var verts = source.vertices;
        for (int i = 0; i < verts.Length; i++) verts[i] += def.offset;
        mesh.indexFormat = source.indexFormat;
        mesh.vertices = verts;
        mesh.normals = source.normals;
        mesh.uv = source.uv;
        mesh.subMeshCount = source.subMeshCount;
        for (int s = 0; s < source.subMeshCount; s++) mesh.SetTriangles(source.GetTriangles(s), s);
        mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static ObjDataSO BuildSO(TileDef def)
    {
        string path = SoDir + "/" + def.assetName + ".asset";
        var so = AssetDatabase.LoadAssetAtPath<ObjDataSO>(path);
        var template = AssetDatabase.LoadAssetAtPath<ObjDataSO>(SoDir + "/" + def.template + ".asset");
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<ObjDataSO>();
            if (template != null) EditorUtility.CopySerialized(template, so);
            AssetDatabase.CreateAsset(so, path);
        }
        so.name = def.assetName;
        so.id = def.id;
        so.objName = def.displayName;
        so.cost = 0;
        so.hourlyCost = 0;
        so.category = def.category;
        so.GL_Line = def.gl;
        so.icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + "/" + def.icon);
        so.defaultFloorTile = null;
        so.footprint = def.category == "Grounds" ? new Vector2Int(2, 2) : new Vector2Int(2, 1);
        so.customShapeOffsets = def.category == "Grounds"
            ? new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) }
            : new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };
        EditorUtility.SetDirty(so);
        return so;
    }

    static void BuildPrefab(TileDef def, Mesh mesh, Material[] materials, ObjDataSO so)
    {
        string path = PrefabDir + "/" + def.assetName + ".prefab";
        var templateSo = AssetDatabase.LoadAssetAtPath<ObjDataSO>(SoDir + "/" + def.template + ".asset");
        var templatePrefab = templateSo != null ? templateSo.prefab : null;
        if (templatePrefab == null) { Debug.LogError("[Grounds2Setup] Template prefab missing for " + def.template); return; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(templatePrefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = def.assetName;
        // Template children (e.g. barrier sub-meshes) are not part of the new look.
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);

        var mf = go.GetComponent<MeshFilter>(); if (mf == null) mf = go.AddComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>(); if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        mr.sharedMaterials = materials;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        var box = go.GetComponent<BoxCollider>();
        if (box != null)
        {
            var b = mesh.bounds;
            if (def.category == "Grounds")
            {
                box.center = new Vector3(0.665f, -0.275f, 0.665f);
                box.size = new Vector3(2.65f, 2.65f, 2.65f);
            }
            else
            {
                box.center = b.center;
                box.size = b.size;
            }
        }

        var bd = go.GetComponent<BuildingData>();
        if (bd != null) SetObjectRef(bd, "objDataSO", so);
        var po = go.GetComponent<PlacedObject>();
        if (po != null) SetObjectRef(po, "data", so);

        if (def.navArea >= 0)
        {
            var mod = go.GetComponent("NavMeshModifier");
            if (mod != null)
            {
                var sm = new SerializedObject(mod);
                sm.FindProperty("m_OverrideArea").boolValue = true;
                sm.FindProperty("m_Area").intValue = def.navArea;
                sm.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        so.prefab = prefab;
        EditorUtility.SetDirty(so);
    }

    static void SetObjectRef(Object target, string field, Object value)
    {
        var s = new SerializedObject(target);
        var p = s.FindProperty(field);
        if (p == null) return;
        p.objectReferenceValue = value;
        s.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddCategoryToScene(List<ObjDataSO> items)
    {
        var menu = Object.FindAnyObjectByType<BuildMenuUI>(FindObjectsInactive.Include);
        if (menu == null) { Debug.LogError("[Grounds2Setup] No BuildMenuUI in the open scene - open Main.unity and run again."); return; }

        var s = new SerializedObject(menu);
        var cats = s.FindProperty("categories");
        int found = -1, groundsIndex = -1;
        for (int i = 0; i < cats.arraySize; i++)
        {
            string id = cats.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
            if (id == CategoryId) found = i;
            if (id == "Grounds") groundsIndex = i;
        }
        if (found < 0)
        {
            found = groundsIndex >= 0 ? groundsIndex + 1 : cats.arraySize;
            cats.InsertArrayElementAtIndex(Mathf.Min(found, cats.arraySize));
            if (found >= cats.arraySize) found = cats.arraySize - 1;
        }
        var cat = cats.GetArrayElementAtIndex(found);
        cat.FindPropertyRelative("id").stringValue = CategoryId;
        cat.FindPropertyRelative("displayName").stringValue = CategoryId;
        cat.FindPropertyRelative("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(CategoryIcon);
        var list = cat.FindPropertyRelative("items");
        list.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        s.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
    }
}
