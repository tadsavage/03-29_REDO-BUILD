using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor Prefab Creation Tool — Tools > Create Prefab
///
/// Drop an FBX/model into the Source field, fill out the form, click Create.
/// The tool will:
///   1. Detect object height from BoxCollider (or renderer bounds if absent).
///   2. Assign the next available ObjDataSO ID (highest existing + 1).
///   3. Save a prefab at Assets/2. Prefabs/{Category}/{Name}.prefab.
///      • Adds PlacedObject, BuildingData, and BoxCollider (if missing).
///      • Wires PlacedObject.data and BuildingData.objDataSO to the new SO.
///   4. Create an ObjDataSO at Assets/1. Scripts/3. ScriptableObjects/PrefabSOs/{Name}.asset.
///   5. Capture a 128-px thumbnail and save it as Assets/…/Icons/{Name}.png.
///   6. Register the new SO in ObjDataRegistry.
///   7. Add the SO to the matching BuildMenuUI category (creates the category if absent).
/// </summary>
public class EditorPrefabCreationTool : EditorWindow
{
    // ── Form fields ───────────────────────────────────────────────────────────
    private GameObject _sourceModel;
    private string     _objName              = "";
    private string     _category             = "";
    private int        _cost                 = 0;
    private int        _hourlyCost           = 0;
    private int        _footprintX           = 1;
    private int        _footprintY           = 1;
    private bool       _isStackable          = false;
    private bool       _isFloor              = false;
    private bool       _ignorePlacementRules = false;
    private float      _objHeight            = 1f;

    // ── Auto-resolved ─────────────────────────────────────────────────────────
    private int             _nextID;
    private ObjDataRegistry _registry;
    private BuildMenuUI     _buildMenuUI;

    // ── UI state ──────────────────────────────────────────────────────────────
    private Vector2     _scroll;
    private string      _statusMessage  = "";
    private MessageType _statusType     = MessageType.None;
    private bool        _heightFromBC   = false;   // true when height came from a BoxCollider
    private GUIStyle    _headerStyle;
    private GUIStyle    _sectionStyle;
    private GUIStyle    _pathStyle;

    // ── Paths ─────────────────────────────────────────────────────────────────
    private const string SODir   = "Assets/_Project/ScriptableObjects/PrefabSOs";
    private const string IconDir = "Assets/_Project/ScriptableObjects/Icons";
    private const string PrefabRootDir = "Assets/_Project/Prefabs";

    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Create Prefab")]
    public static void Open()
    {
        var w = GetWindow<EditorPrefabCreationTool>("Prefab Creator");
        w.minSize = new Vector2(450, 720);
        w.Show();
    }

    private void OnEnable()
    {
        AutoFindReferences();
        RefreshNextID();
    }

    // ── Reference discovery ───────────────────────────────────────────────────

    private void AutoFindReferences()
    {
        // ObjDataRegistry — single asset anywhere in the project
        string[] guids = AssetDatabase.FindAssets("t:ObjDataRegistry");
        if (guids.Length > 0)
            _registry = AssetDatabase.LoadAssetAtPath<ObjDataRegistry>(
                AssetDatabase.GUIDToAssetPath(guids[0]));

        // BuildMenuUI — look in all open scenes
        var found = Object.FindObjectsByType<BuildMenuUI>();
        _buildMenuUI = found.Length > 0 ? found[0] : null;
    }

    private void RefreshNextID()
    {
        int max = 0;
        foreach (string g in AssetDatabase.FindAssets("t:ObjDataSO"))
        {
            var so = AssetDatabase.LoadAssetAtPath<ObjDataSO>(AssetDatabase.GUIDToAssetPath(g));
            if (so != null && so.id > max) max = so.id;
        }
        _nextID = max + 1;
    }

    // ── GUI ───────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        // GUIStyle can only be created inside OnGUI (EditorStyles not available earlier)
        if (_headerStyle == null)
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        if (_sectionStyle == null)
            _sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        if (_pathStyle == null)
            _pathStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        // Header
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("PREFAB CREATION TOOL", _headerStyle, GUILayout.Height(24));
        EditorGUILayout.Space(6);
        DrawLine();

        // ── SOURCE MODEL ──────────────────────────────────────────────────────
        Section("SOURCE MODEL");
        EditorGUI.BeginChangeCheck();
        _sourceModel = (GameObject)EditorGUILayout.ObjectField(
            "FBX / Model", _sourceModel, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck() && _sourceModel != null)
            OnModelChanged();

        if (_sourceModel != null)
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(_sourceModel), _pathStyle);

        DrawLine();

        // ── OBJECT INFO ───────────────────────────────────────────────────────
        Section("OBJECT INFO");
        _objName    = EditorGUILayout.TextField("Name", _objName);
        _category   = EditorGUILayout.TextField("Category", _category);
        EditorGUILayout.LabelField(
            "  e.g.  Walls · Grounds · Racking · Barriers · Vegetation · Floors",
            _pathStyle);
        _cost       = Mathf.Max(0, EditorGUILayout.IntField("Cost ($)", _cost));
        _hourlyCost = Mathf.Max(0, EditorGUILayout.IntField("Hourly Cost ($/hr)", _hourlyCost));
        DrawLine();

        // ── FOOTPRINT ─────────────────────────────────────────────────────────
        Section("FOOTPRINT");
        EditorGUILayout.BeginHorizontal();
        _footprintX = Mathf.Max(1, EditorGUILayout.IntField("Width  (X cells)", _footprintX));
        _footprintY = Mathf.Max(1, EditorGUILayout.IntField("Depth  (Y cells)", _footprintY));
        EditorGUILayout.EndHorizontal();
        DrawLine();

        // ── FLAGS ─────────────────────────────────────────────────────────────
        Section("FLAGS");
        _isStackable          = EditorGUILayout.Toggle("Is Stackable",           _isStackable);
        _isFloor              = EditorGUILayout.Toggle("Is Floor",               _isFloor);
        _ignorePlacementRules = EditorGUILayout.Toggle("Ignore Placement Rules", _ignorePlacementRules);
        DrawLine();

        // ── AUTO-DETECTED ─────────────────────────────────────────────────────
        Section("AUTO-DETECTED");

        EditorGUILayout.BeginHorizontal();
        _objHeight = EditorGUILayout.FloatField("Object Height (m)", _objHeight);
        using (new EditorGUI.DisabledGroupScope(_sourceModel == null))
        {
            if (GUILayout.Button("Re-detect", GUILayout.Width(70)))
            {
                _objHeight    = DetectHeight(_sourceModel);
                _heightFromBC = HasBoxCollider(_sourceModel);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(
            _heightFromBC ? "  Sourced from BoxCollider" : "  Estimated from renderer bounds",
            _pathStyle);

        using (new EditorGUI.DisabledGroupScope(true))
            EditorGUILayout.IntField("Assigned ID", _nextID);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh ID", GUILayout.Width(84))) RefreshNextID();
        EditorGUILayout.EndHorizontal();

        DrawLine();

        // ── SAVE PATHS (preview) ──────────────────────────────────────────────
        Section("SAVE PATHS");
        string safe = SanitizeName(_objName);
        bool   hasName = !string.IsNullOrWhiteSpace(safe);
        string catFolder = GetCategoryFolder(_category);
        EditorGUILayout.LabelField("Prefab", hasName ? $"{catFolder}{safe}.prefab" : "—", _pathStyle);
        EditorGUILayout.LabelField("SO",     hasName ? $"{SODir}/{safe}.asset"      : "—", _pathStyle);
        EditorGUILayout.LabelField("Icon",   hasName ? $"{IconDir}/{safe}.png"      : "—", _pathStyle);
        DrawLine();

        // ── SYSTEM REFERENCES ─────────────────────────────────────────────────
        Section("SYSTEM REFERENCES");
        _registry    = (ObjDataRegistry)EditorGUILayout.ObjectField(
            "ObjData Registry", _registry, typeof(ObjDataRegistry), false);
        _buildMenuUI = (BuildMenuUI)EditorGUILayout.ObjectField(
            "Build Menu UI",    _buildMenuUI, typeof(BuildMenuUI), true);

        if (_buildMenuUI == null)
            EditorGUILayout.HelpBox(
                "Build Menu UI not found — item will be added to the Registry only.",
                MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Re-find in Scene", GUILayout.Width(120)))
            AutoFindReferences();
        EditorGUILayout.EndHorizontal();

        DrawLine();

        // ── STATUS ────────────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }

        // ── CREATE BUTTON ─────────────────────────────────────────────────────
        EditorGUILayout.Space(8);
        var errors = GetValidationErrors();
        using (new EditorGUI.DisabledGroupScope(errors.Count > 0))
        {
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = errors.Count == 0
                ? new Color(0.28f, 0.82f, 0.35f)
                : Color.white;

            if (GUILayout.Button("  CREATE PREFAB  ", GUILayout.Height(46)))
                CreatePrefab();

            GUI.backgroundColor = oldBg;
        }

        foreach (var e in errors)
            EditorGUILayout.HelpBox(e, MessageType.Error);

        EditorGUILayout.Space(12);
        EditorGUILayout.EndScrollView();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private List<string> GetValidationErrors()
    {
        var list = new List<string>();
        if (_sourceModel == null)                      list.Add("Source model is required.");
        if (string.IsNullOrWhiteSpace(_objName))       list.Add("Name is required.");
        if (string.IsNullOrWhiteSpace(_category))      list.Add("Category is required.");
        if (_registry == null)                         list.Add("ObjData Registry is required.");
        return list;
    }

    // ── Model change ──────────────────────────────────────────────────────────

    private void OnModelChanged()
    {
        // Always overwrite — name, height, and collider info should reflect the new model.
        _objName      = ObjectNames.NicifyVariableName(_sourceModel.name);
        _objHeight    = DetectHeight(_sourceModel);
        _heightFromBC = HasBoxCollider(_sourceModel);

        RefreshNextID();

        // Kick off Unity's async preview so it's ready by the time the user clicks Create
        AssetPreview.GetAssetPreview(_sourceModel);
    }

    // ── Height detection ──────────────────────────────────────────────────────

    private bool HasBoxCollider(GameObject model)
        => model.GetComponentInChildren<BoxCollider>(true) != null;

    private float DetectHeight(GameObject model)
    {
        var bc = model.GetComponentInChildren<BoxCollider>(true);
        if (bc != null) return Mathf.Round(bc.size.y * 100f) / 100f;

        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return Mathf.Round(b.size.y * 100f) / 100f;
        }

        return 1f;
    }

    // ── Core creation pipeline ────────────────────────────────────────────────

    private void CreatePrefab()
    {
        string safe       = SanitizeName(_objName);
        string catFolder  = GetCategoryFolder(_category);
        string prefabPath = $"{catFolder}{safe}.prefab";
        string soPath     = $"{SODir}/{safe}.asset";
        string iconPath   = $"{IconDir}/{safe}.png";

        try
        {
            RefreshNextID();

            // ── Directories ───────────────────────────────────────────────────
            EnsureDirectory(catFolder.TrimEnd('/'));
            EnsureDirectory(SODir);
            EnsureDirectory(IconDir);

            // ── Thumbnail ─────────────────────────────────────────────────────
            // Capture before the temp GO is created so the preview isn't disrupted.
            Texture2D icon = CapturePreview(_sourceModel);
            if (icon != null)
            {
                string absIconPath = Path.Combine(
                    Application.dataPath, iconPath.Substring("Assets/".Length));
                File.WriteAllBytes(absIconPath, icon.EncodeToPNG());
                Object.DestroyImmediate(icon);

                AssetDatabase.ImportAsset(iconPath);
                ConfigureIconImportSettings(iconPath);
                icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            }

            // ── ObjDataSO — create first so the prefab can reference it ───────
            var so = CreateInstance<ObjDataSO>();
            so.id                   = _nextID;
            so.objName              = _objName.Trim();
            so.cost                 = _cost;
            so.hourlyCost           = _hourlyCost;
            so.category             = _category.Trim();
            so.objHeight            = _objHeight;
            so.footprint            = new Vector2Int(_footprintX, _footprintY);
            so.isStackable          = _isStackable;
            so.isFloor              = _isFloor;
            so.ignorePlacementRules = _ignorePlacementRules;
            so.icon                 = icon;
            // so.prefab assigned after prefab is saved to disk

            AssetDatabase.CreateAsset(so, soPath);

            // ── Build and save prefab ─────────────────────────────────────────
            GameObject tempGO = BuildPrefabGameObject(safe, so);
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempGO, prefabPath, out bool saved);
            Object.DestroyImmediate(tempGO);

            if (!saved || savedPrefab == null)
            {
                AssetDatabase.DeleteAsset(soPath);
                SetStatus($"Failed to save prefab at '{prefabPath}'.", MessageType.Error);
                return;
            }

            // ── Back-fill SO.prefab ───────────────────────────────────────────
            so.prefab = savedPrefab;
            EditorUtility.SetDirty(so);

            // ── Wire SO into saved prefab's components ────────────────────────
            // PlacedObject.data and BuildingData.objDataSO are serialized fields
            // that need to point at the SO. We use EditPrefabContentsScope so
            // the changes land inside the prefab asset (not a scene instance).
            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                var root = scope.prefabContentsRoot;

                var po = root.GetComponent<PlacedObject>();
                if (po != null) po.data = so;

                var bd = root.GetComponent<BuildingData>();
                if (bd != null)
                {
                    var bdSO = new SerializedObject(bd);
                    var prop = bdSO.FindProperty("objDataSO");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = so;
                        bdSO.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            // ── Add to ObjDataRegistry ────────────────────────────────────────
            var regSO   = new SerializedObject(_registry);
            var listProp = regSO.FindProperty("buttonSOs");
            listProp.arraySize++;
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = so;
            regSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_registry);

            // ── Add to Build Menu ─────────────────────────────────────────────
            if (_buildMenuUI != null)
                AddToBuildMenu(so);

            // ── Save everything ───────────────────────────────────────────────
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = so;
            EditorGUIUtility.PingObject(so);

            SetStatus(
                $"Created '{_objName}'  (ID {_nextID}).\n" +
                $"Prefab, SO, Registry and Build Menu updated.",
                MessageType.Info);

            _nextID++;
        }
        catch (System.Exception ex)
        {
            SetStatus($"Error: {ex.Message}", MessageType.Error);
            Debug.LogException(ex);
        }
    }

    // ── Prefab construction ───────────────────────────────────────────────────

    private GameObject BuildPrefabGameObject(string name, ObjDataSO so)
    {
        // Instantiate the source model into the active scene as a temp object.
        // For FBX files this creates a "Model Prefab" instance; for regular prefabs
        // it creates a prefab instance. Both are handled by SaveAsPrefabAsset below.
        string sourcePath = AssetDatabase.GetAssetPath(_sourceModel);
        var    sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(sourceAsset);
        if (go == null) go = Object.Instantiate(_sourceModel);
        go.name = name;

        // ── PlacedObject ──────────────────────────────────────────────────────
        var po = go.GetComponent<PlacedObject>() ?? go.AddComponent<PlacedObject>();
        po.data = so;

        // ── BuildingData ──────────────────────────────────────────────────────
        if (!go.TryGetComponent<BuildingData>(out _))
            go.AddComponent<BuildingData>();
        // BuildingData.objDataSO is private; we wire it inside EditPrefabContentsScope
        // after the prefab is saved to disk (where SerializedObject works reliably).

        // ── BuildingHighlighter ───────────────────────────────────────────────
        if (!go.TryGetComponent<BuildingHighlighter>(out _))
            go.AddComponent<BuildingHighlighter>();

        // ── BoxCollider ───────────────────────────────────────────────────────
        // Add to the root if none exists anywhere in the hierarchy.
        // Size from renderer bounds at world-origin instantiation (world ≈ local here).
        // Always force isTrigger = true so hover/placement raycasts work correctly.
        var existingBC = go.GetComponentInChildren<BoxCollider>(true);
        if (existingBC != null) existingBC.isTrigger = true;
        if (!go.GetComponentInChildren<BoxCollider>(true))
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                bc.center = go.transform.InverseTransformPoint(b.center);
                bc.size   = b.size;

                // Update detected height from the actual instantiated bounds
                _objHeight    = Mathf.Round(b.size.y * 100f) / 100f;
                _heightFromBC = false;   // came from renderer, not a pre-existing BC
                so.objHeight  = _objHeight;
            }
            else
            {
                bc.center = new Vector3(0f, _objHeight * 0.5f, 0f);
                bc.size   = new Vector3(1.33f, _objHeight, 1.33f);
            }
        }

        return go;
    }

    // ── Thumbnail capture ─────────────────────────────────────────────────────

    private Texture2D CapturePreview(GameObject model)
    {
        // Unity's async preview: should be warm by the time the user fills the form.
        Texture2D preview = AssetPreview.GetAssetPreview(model);

        // Fallback: the mini-thumbnail Unity shows in the project browser
        if (preview == null)
            preview = AssetPreview.GetMiniThumbnail(model);

        if (preview == null)
        {
            Debug.LogWarning($"[PrefabCreator] No preview available for '{model.name}'. Icon will be null.");
            return null;
        }

        // AssetPreview textures are GPU-only — blit to a temporary RT then read back.
        var rt   = RenderTexture.GetTemporary(128, 128, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        Graphics.Blit(preview, rt);
        RenderTexture.active = rt;

        var result = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
        result.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }

    private void ConfigureIconImportSettings(string iconPath)
    {
        if (AssetImporter.GetAtPath(iconPath) is not TextureImporter ti) return;
        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled       = false;
        ti.maxTextureSize      = 128;
        ti.SaveAndReimport();
    }

    // ── Build Menu injection ──────────────────────────────────────────────────

    private void AddToBuildMenu(ObjDataSO so)
    {
        var menuSO   = new SerializedObject(_buildMenuUI);
        var catsProp = menuSO.FindProperty("categories");

        if (catsProp == null)
        {
            Debug.LogWarning("[PrefabCreator] 'categories' field not found on BuildMenuUI. Check the field name.");
            return;
        }

        // Find category by displayName first, then by id (both case-insensitive)
        int idx = FindCategoryIndex(catsProp, "displayName", _category);
        if (idx == -1)
            idx = FindCategoryIndex(catsProp, "id", _category);

        if (idx == -1)
        {
            // Category not in menu yet — create it
            catsProp.arraySize++;
            var newCat = catsProp.GetArrayElementAtIndex(catsProp.arraySize - 1);
            newCat.FindPropertyRelative("id").stringValue          = _category.ToLower().Replace(" ", "_");
            newCat.FindPropertyRelative("displayName").stringValue = _category;
            // icon left null — user assigns it in the Inspector
            var newItems = newCat.FindPropertyRelative("items");
            newItems.ClearArray();
            newItems.arraySize = 1;
            newItems.GetArrayElementAtIndex(0).objectReferenceValue = so;
            Debug.Log($"[PrefabCreator] New Build Menu category '{_category}' created. " +
                       "Assign an icon on the BuildMenuUI component in the Inspector.");
        }
        else
        {
            var items = catsProp.GetArrayElementAtIndex(idx).FindPropertyRelative("items");
            items.arraySize++;
            items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = so;
        }

        menuSO.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(_buildMenuUI);
    }

    private int FindCategoryIndex(SerializedProperty catsProp, string field, string value)
    {
        for (int i = 0; i < catsProp.arraySize; i++)
        {
            var f = catsProp.GetArrayElementAtIndex(i).FindPropertyRelative(field);
            if (f != null && string.Equals(f.stringValue, value, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private string GetCategoryFolder(string cat)
    {
        cat = string.IsNullOrWhiteSpace(cat) ? "Misc" : cat.Trim();
        return $"{PrefabRootDir}/{cat}/";
    }

    private string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "NewObject";
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "_");
        return name.Trim();
    }

    // Recursively creates all missing folders in the path.
    private void EnsureDirectory(string path)
    {
        path = path.Replace("\\", "/").TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
        EnsureDirectory(parent);   // recurse until all parents exist
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private void SetStatus(string msg, MessageType type)
    {
        _statusMessage = msg;
        _statusType    = type;
        Repaint();
    }

    private void Section(string label)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(label, _sectionStyle);
    }

    private void DrawLine()
    {
        EditorGUILayout.Space(4);
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1f),
                           new Color(0.28f, 0.28f, 0.28f, 0.7f));
        EditorGUILayout.Space(4);
    }
}
