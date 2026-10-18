using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the "Pallet Jack Parking" placeable (Tad, 2026-10-16): a 3x1-cell painted bay where order selectors
/// park their pallet jack after staging. Creates the prefab (flat navy plate + yellow border, a
/// PalletJackParkingSpot, PlacedObject/BuildingData like every placeable), its ObjDataSO (Vehicles tab,
/// footprint (-1,0)(0,0)(1,0) around the root cell, walkable), and adds it to the build-menu registry.
/// Safe to re-run: it overwrites the prefab and updates the existing asset.
/// </summary>
public static class CreatePalletJackParking
{
    const string PrefabPath = "Assets/_Project/Prefabs/MHE/PJ_Parking.prefab";
    const string SoPath = "Assets/_Project/ScriptableObjects/PrefabSOs/PJ_Parking.asset";
    const string MatDir = "Assets/_Project/ScriptableObjects/Materials/PJParking";
    const string ChargerModelPath = "Assets/_Project/Models/MHE_Charger.fbx";
    const float Cell = 1.33f;

    [MenuItem("Tools/Pallet Jack/Create Parking Spot Placeable")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects/Materials", "PJParking");
        var root = new GameObject("PJ_Parking");
        try
        {
            float len = Cell * 3f - 0.08f, wid = Cell - 0.10f;

            // Tad's MHE charger model (Barriers_Floors_Compilation.blend → Models/MHE_Charger.fbx, 2026-10-16):
            // the bay plate with the charging post at one end. Its long axis is the FBX's +Z (post end), turned
            // to the prefab's +X — the bay's long axis — so the post sits at +X, where the jack's nose parks.
            // Lifted 1 cm so the plate doesn't z-fight the floor tile.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ChargerModelPath);
            if (model != null)
            {
                var m = (GameObject)PrefabUtility.InstantiatePrefab(model);
                m.name = "MHE_Charger";
                m.transform.SetParent(root.transform, false);
                m.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                m.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * m.transform.localRotation;
                foreach (var c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            }
            else
            {
                // Fallback: the original flat painted bay.
                var plateMat  = MakeMat(MatDir + "/PJParking_Plate.mat",  new Color(0.10f, 0.16f, 0.26f));
                var stripeMat = MakeMat(MatDir + "/PJParking_Stripe.mat", new Color(0.95f, 0.75f, 0.10f));
                float t = 0.012f;
                AddBox(root.transform, "Plate", new Vector3(0f, t * 0.5f, 0f), new Vector3(len, t, wid), plateMat);
                float s = 0.08f, y = t + 0.002f;
                AddBox(root.transform, "Stripe_N", new Vector3(0f, y, wid * 0.5f - s * 0.5f), new Vector3(len, 0.004f, s), stripeMat);
                AddBox(root.transform, "Stripe_S", new Vector3(0f, y, -wid * 0.5f + s * 0.5f), new Vector3(len, 0.004f, s), stripeMat);
                AddBox(root.transform, "Stop_Bar", new Vector3(len * 0.5f - 0.35f, y, 0f), new Vector3(0.12f, 0.004f, wid * 0.7f), stripeMat);
            }

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.02f, 0f);
            col.size = new Vector3(len, 0.04f, wid);
            root.AddComponent<PlacedObject>();
            root.AddComponent<BuildingData>();
            root.AddComponent<BuildingHighlighter>();
            root.AddComponent<PalletJackParkingSpot>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            var so = AssetDatabase.LoadAssetAtPath<ObjDataSO>(SoPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<ObjDataSO>();
                AssetDatabase.CreateAsset(so, SoPath);
            }
            var pj = AssetDatabase.FindAssets("t:ObjDataSO").Select(g => AssetDatabase.LoadAssetAtPath<ObjDataSO>(AssetDatabase.GUIDToAssetPath(g)))
                                  .FirstOrDefault(x => x != null && x.id == 50);
            var sso = new SerializedObject(so);
            if (sso.FindProperty("id").intValue == 0)
            {
                int maxId = AssetDatabase.FindAssets("t:ObjDataSO").Select(g => AssetDatabase.LoadAssetAtPath<ObjDataSO>(AssetDatabase.GUIDToAssetPath(g)))
                                         .Where(x => x != null).Max(x => x.id);
                sso.FindProperty("id").intValue = maxId + 1;
            }
            sso.FindProperty("objName").stringValue = "Pallet Jack Parking";
            sso.FindProperty("cost").intValue = 150;
            sso.FindProperty("hourlyCost").intValue = 0;
            sso.FindProperty("category").stringValue = "Vehicles";
            sso.FindProperty("GL_Line").stringValue = "Pallet Jack";
            sso.FindProperty("prefab").objectReferenceValue = prefab;
            if (pj != null) sso.FindProperty("icon").objectReferenceValue = new SerializedObject(pj).FindProperty("icon").objectReferenceValue;
            sso.FindProperty("pathfindingClear").boolValue = true;
            // A painted floor marking: never blocks anything placed on it (the pallet jack, another bay) and
            // adds no height to its cells; still refused where a wall or rack stands.
            sso.FindProperty("ignorePlacementRules").boolValue = true;
            sso.FindProperty("blockedByTallObstructions").boolValue = true;
            sso.FindProperty("objHeight").floatValue = 0.02f;
            sso.FindProperty("footprint").vector2IntValue = new Vector2Int(3, 1);
            var offs = sso.FindProperty("customShapeOffsets");
            offs.arraySize = 3;
            offs.GetArrayElementAtIndex(0).vector2IntValue = new Vector2Int(-1, 0);
            offs.GetArrayElementAtIndex(1).vector2IntValue = new Vector2Int(0, 0);
            offs.GetArrayElementAtIndex(2).vector2IntValue = new Vector2Int(1, 0);
            sso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so);

            // Build menu registry.
            var regGuid = AssetDatabase.FindAssets("t:ObjDataRegistry").FirstOrDefault();
            if (regGuid != null)
            {
                var reg = AssetDatabase.LoadAssetAtPath<ObjDataRegistry>(AssetDatabase.GUIDToAssetPath(regGuid));
                var rso = new SerializedObject(reg);
                var list = rso.FindProperty("buttonSOs");
                bool present = false;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == so) present = true;
                if (!present)
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = so;
                    rso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(reg);
                }
            }
            // The build-menu TABS don't read the registry: each tab is an item list on BuildMenuUI.categories in
            // the scene. Add it to "Vehicles" there too (open scene only).
            var ui = Object.FindAnyObjectByType<BuildMenuUI>(FindObjectsInactive.Include);
            if (ui != null)
            {
                var uso = new SerializedObject(ui);
                var cats = uso.FindProperty("categories");
                for (int i = 0; i < cats.arraySize; i++)
                {
                    var c = cats.GetArrayElementAtIndex(i);
                    if (c.FindPropertyRelative("displayName").stringValue != "Vehicles") continue;
                    var items = c.FindPropertyRelative("items");
                    bool has = false;
                    for (int j = 0; j < items.arraySize; j++) if (items.GetArrayElementAtIndex(j).objectReferenceValue == so) has = true;
                    if (has) break;
                    items.arraySize++;
                    items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = so;
                    uso.ApplyModifiedProperties();
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(ui.gameObject.scene);
                    break;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.LogWarning($"[CreatePalletJackParking] Built {PrefabPath} + {SoPath} (id {so.id}) and registered it in the build menu.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static Material MakeMat(string path, Color c)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void AddBox(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
