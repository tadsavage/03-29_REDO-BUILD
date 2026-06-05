using SaveLoadSystem;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles placing objects into the world during gameplay
/// and spawning them when loading from a save file.
/// </summary>
public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private ObjDataRegistry registry;
    [SerializeField] private PlacementGrid grid;
    [SerializeField] private SaveLoadWindowController saveLoadWindowController;
    [SerializeField] private FreeLookCamera freeLookCamera;
    [SerializeField] private SaveLoadSystem.SaveThumbnailCapture thumbnailCapture;

    private MoneyService moneyService;

    private float quicksaveCooldown = 1.0f;
    private float quicksaveTimer = 0f;
    private string lastSaveName = "autosave";

    // Called by GameContext
    public void Initialize(MoneyService money)
    {
        moneyService = money;
    }
    private void Start()
    {
        if (freeLookCamera == null)
            freeLookCamera = Camera.main.GetComponent<FreeLookCamera>();

        // Subscribe to slot save/load events for toast + SFX
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveCompleted += OnSlotSaveCompleted;
            SaveManager.Instance.OnLoadCompleted += OnSlotLoadCompleted;
        }
    }
    private void Update()
    {
        if (quicksaveTimer > 0f)
            quicksaveTimer -= Time.deltaTime;

        // Quicksave (F5)
        if (Keyboard.current.f5Key.isPressed && quicksaveTimer <= 0f)
        {
            SaveGame("autosave");
            quicksaveTimer = quicksaveCooldown;
            AudioManager.Play("UI_Save");
            UIToast.Show("Quick-save successful");

            if (thumbnailCapture != null)
            {
                string saveDir = System.IO.Path.Combine(Application.dataPath, "_Saves");
                thumbnailCapture.CaptureThumbnail(saveDir, "autosave_thumb.png", tex =>
                {
                    if (tex != null) Destroy(tex);
                });
            }
        }

        // Quickload (F9) — UNTOUCHED
        if (Keyboard.current.f9Key.isPressed && quicksaveTimer <= 0f)
        {
            LoadGame();
            AudioManager.Play("UI_Load");
            UIToast.Show("Quick-load successful");
        }

        // Save/Load window (F6)
        if (Keyboard.current.f6Key.wasPressedThisFrame)
        {
            if (saveLoadWindowController.IsOpen)
                saveLoadWindowController.Close();
            else
                saveLoadWindowController.Open(SaveLoadMode.Save);
        }
    }

    // ---------------------------------------------------------
    // NORMAL GAMEPLAY PLACEMENT
    // ---------------------------------------------------------
    private Transform _objectsContainer;

    private void EnsureContainer()
    {
        if (_objectsContainer == null)
        {
            var go = GameObject.Find("PlacedObjectsContainer");
            if (go == null) 
            {
                go = new GameObject("PlacedObjectsContainer");
                // Massive performance win for Editor: hide the container from hierarchy
                // to prevent the Hierarchy window from trying to render/sort 3,000+ items.
                go.hideFlags = HideFlags.HideInHierarchy;
            }
            _objectsContainer = go.transform;
        }
    }

    public PlacedObject PlaceObject(ObjDataSO so, int x, int y, int rot)
    {
        EnsureContainer();
        Vector2Int cell = new Vector2Int(x, y);
        Vector3 worldPos = grid.GetCellCenter(cell);

        GameObject go = Instantiate(so.prefab, worldPos,
                                    Quaternion.Euler(0f, rot * 90f, 0f), _objectsContainer);

        PlacedObject po = go.GetComponent<PlacedObject>();
        po.Initialize(so, x, y, rot);

        PlacedObjectRegistry.Register(po);
        grid.AddStackObject(cell, go, so);

        return po;
    }

    // ---------------------------------------------------------
    // QUICKSAVE (F5) — writes via your existing SaveSystem class
    // ---------------------------------------------------------
    public void SaveGame(string saveName)
    {
        lastSaveName = saveName;
        SaveData save = BuildSaveData(saveName);
        SaveSystem.Save(save);
    }

    // ---------------------------------------------------------
    // QUICKLOAD (F9) — reads via your existing SaveSystem class
    // ---------------------------------------------------------
    public void LoadGame()
    {
        //Debug.Log($"Attempting to load save: {lastSaveName}");
        SaveData save = SaveSystem.Load(lastSaveName);
        if (save == null)
        {
            Debug.LogWarning($"LoadGame: no save file found for {lastSaveName}");
            return;
        }
        ApplySaveData(save);
    }

    public void LoadGame(string saveName)
    {
        lastSaveName = saveName;
        LoadGame();
    }

    // ---------------------------------------------------------
    // SLOT SAVE/LOAD — called by SaveManager for multi-slot UI.
    // Same data format, different file path. Quicksave untouched.
    // ---------------------------------------------------------

    /// <summary>
    /// Serializes the full game state to a JSON string.
    /// SaveManager writes this to its own per-slot file.
    /// </summary>
    public string SerializeToJson()
    {
        SaveData save = BuildSaveData("slot_save");
        return JsonUtility.ToJson(save, true);
    }

    /// <summary>
    /// Deserializes a JSON string and applies it to the world.
    /// SaveManager reads from its own per-slot file.
    /// </summary>
    public void DeserializeFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        SaveData save = JsonUtility.FromJson<SaveData>(json);
        if (save == null)
        {
            Debug.LogError("[PlacementSystem] DeserializeFromJson: bad JSON");
            return;
        }
        ApplySaveData(save);
    }

    // ---------------------------------------------------------
    // SHARED HELPERS — used by BOTH quicksave AND slot save
    // ---------------------------------------------------------

    private SaveData BuildSaveData(string saveName)
    {
        SaveData save = new SaveData();
        save.saveName = saveName;
        save.money = moneyService.CurrentCapital;
        save.spentToday = moneyService.SpentToday;

        if (freeLookCamera != null)
            save.cameraData = freeLookCamera.GetState();

        save.devSettings = CollectDevSettings();

        if (ToolsWindowController.Instance != null)
        {
            var pos = ToolsWindowController.Instance.GetWindowPosition();
            save.toolsWindowX = pos.x;
            save.toolsWindowY = pos.y;
        }

        // Guidance lines
        var guid = Object.FindAnyObjectByType<NavAgentGuidance>();
        if (guid != null) save.guidanceLinesVisible = guid.showGuidanceLine;

        // Hover popup
        var hoverUI = Object.FindAnyObjectByType<WorldHoverPopupUI>();
        if (hoverUI != null) save.hoverPopupEnabled = hoverUI.IsEnabled;

        // Waypoint visibility — read from first Waypoint's mesh renderer
        var wp = Object.FindAnyObjectByType<Waypoint>();
        if (wp != null)
        {
            var mr = wp.GetComponentInChildren<MeshRenderer>();
            if (mr != null) save.waypointsVisible = mr.enabled;
        }

        foreach (var entry in PlacedObjectRegistry.All)
        {
            SavedObject obj = new SavedObject();
            obj.id = entry.data.id;
            obj.x = entry.gridX;
            obj.y = entry.gridY;
            obj.rot = entry.rotation;
            obj.customData = entry.customData;
            save.placedObjects.Add(obj);
}

        return save;
    }

    private void ApplySaveData(SaveData save)
    {
        moneyService.SetMoney(save.money);
        moneyService.SetSpentToday(save.spentToday);

        if (save.cameraData != null && freeLookCamera != null)
            freeLookCamera.SetState(save.cameraData);

        ClearAll();

        foreach (var objSave in save.placedObjects)
        {
            ObjDataSO so = registry.GetByID(objSave.id);
            if (so == null)
            {
                Debug.LogWarning($"[PlacementSystem] Skipping saved object with unknown id={objSave.id} at ({objSave.x},{objSave.y}) — not in ObjDataRegistry.");
                continue;
            }
            SpawnFromSave(so, objSave.x, objSave.y, objSave.rot, objSave.customData);
        }

        grid.RebuildFromRegistry();

        // Apply saved dev-settings to all matching scene components
        if (save.devSettings != null && save.devSettings.Count > 0)
            ApplyDevSettings(save.devSettings);

        // Restore Tools Window position
        ToolsWindowController.Instance?.SetWindowPosition(save.toolsWindowX, save.toolsWindowY);

        // Restore guidance lines
        foreach (var g in Object.FindObjectsByType<NavAgentGuidance>())
            g.showGuidanceLine = save.guidanceLinesVisible;

        // Restore hover popup state
        var hoverUI = Object.FindAnyObjectByType<WorldHoverPopupUI>();
        if (hoverUI != null) hoverUI.SetEnabled(save.hoverPopupEnabled);

        // Restore waypoints visibility
        foreach (var w in Object.FindObjectsByType<Waypoint>())
            foreach (var r in w.GetComponentsInChildren<MeshRenderer>())
                r.enabled = save.waypointsVisible;

        // Bake must be deferred one frame so Unity's deferred Destroy() calls flush first.
        // BakeSynchronous() called in the same frame as Destroy() feeds stale geometry to
        // CollectSources() (old + new objects both alive), producing a doubled NavMesh that
        // blocks door passages. Yielding one frame lets the old objects disappear first.
        StartCoroutine(BakeAfterDestroyFlush());
    }

    private System.Collections.IEnumerator BakeAfterDestroyFlush()
    {
        yield return null; // wait one frame for Destroy() to flush
        if (NavMeshManager.Instance != null)
            NavMeshManager.Instance.BakeSynchronous();
    }

        // ---------------------------------------------------------
    // LOAD GAME SPAWNING
    // ---------------------------------------------------------
    public PlacedObject SpawnFromSave(ObjDataSO so, int x, int y, int rot, string customData = "")
    {
        EnsureContainer();
        Vector2Int root = new Vector2Int(x, y);
        float rotationDeg = rot * 90f;

        float stackY = 0f;
        if (so.isStackable)
            stackY = grid.GetStackHeight(root);

        Vector3 worldPos = grid.GetCellCenter(root);
        worldPos.y += stackY;

        GameObject go = Instantiate(so.prefab, worldPos,Quaternion.Euler(0f, rotationDeg, 0f), _objectsContainer);

        PlacedObject po = go.GetComponent<PlacedObject>();
        po.Initialize(so, x, y, rot);
        po.customData = customData;

        // Ensure PalletBuilder loads its state if it exists
        var pb = go.GetComponent<PalletBuilder>();
        if (pb != null) pb.LoadBuildState();

        BuildingData bd = go.GetComponent<BuildingData>();
        Vector2Int[] offsets = so.GetFootprintOffsets(-rotationDeg);
        bd.Initialize(root, rotationDeg, offsets, so);

        PlacedObjectRegistry.Register(po);

        foreach (var o in offsets)
        {
            Vector2Int c = root + o;
            grid.AddStackObject(c, go, so);
        }

        return po;
    }

    // ---------------------------------------------------------
    // CLEAR ALL OBJECTS
    // ---------------------------------------------------------
    // Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts/PlacementSystem.cs

    public void ClearAll()
    {
        // Use a snapshot to avoid modification issues while iterating
        var snapshot = PlacedObjectRegistry.GetSnapshot();
        
        for (int i = snapshot.Length - 1; i >= 0; i--)
        {
            var obj = snapshot[i];
            if (obj != null)
            {
                Vector2Int cell = new Vector2Int(obj.gridX, obj.gridY);
                grid.RemoveStackObject(cell, obj.gameObject, obj.data);
                Destroy(obj.gameObject);
            }
        }

        PlacedObjectRegistry.Clear();
        grid.InitializeGrid();
    }
private void OnSlotSaveCompleted(int slotIndex)
    {
        AudioManager.Play("UI_Save");
        UIToast.Show($"Saved to Slot {slotIndex + 1}");
    }

    private void OnSlotLoadCompleted(int slotIndex)
    {
        AudioManager.Play("UI_Load");
        UIToast.Show($"Loaded Slot {slotIndex + 1}");
    }

    private void OnDestroy()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveCompleted -= OnSlotSaveCompleted;
            SaveManager.Instance.OnLoadCompleted -= OnSlotLoadCompleted;
        }
    }

    // ---------------------------------------------------------
    // DEV SETTINGS PERSISTENCE
    // Scans the same script types as ToolsWindowController and
    // serialises every tunable (float/int/bool) serialized field.
    // On load, applies values to ALL instances of each type so
    // balance changes affect every agent / object in the scene.
    // ---------------------------------------------------------

    private static readonly HashSet<string> DevScanTypes = new()
    {
        "FreeLookCamera", "AiNavigation", "AgentAnimation", "RatBehavior",
        "WallVisibilityManager", "LightPulse", "Gate_Open_Close",
        "NavMeshManager", "VehicleThrottleAudio", "AmbientMumble", "PalletBuilder",
    };

    private static readonly BindingFlags DevFieldFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static List<DevSettingEntry> CollectDevSettings()
    {
        var result = new List<DevSettingEntry>();
        var seen   = new HashSet<string>();

        foreach (var mb in FindObjectsByType<MonoBehaviour>())
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            if (!DevScanTypes.Contains(typeName) || seen.Contains(typeName)) continue;
            seen.Add(typeName);

            foreach (var field in mb.GetType().GetFields(DevFieldFlags))
            {
                if (!IsTunableField(field)) continue;
                var val = field.GetValue(mb);
                if (val == null) continue;
                result.Add(new DevSettingEntry
                {
                    key = $"{typeName}.{field.Name}",
                    val = val.ToString()
                });
            }
        }
        return result;
    }

    private static void ApplyDevSettings(List<DevSettingEntry> settings)
    {
        // Group all matching scene components by type name
        var byType = new Dictionary<string, List<MonoBehaviour>>();
        foreach (var mb in FindObjectsByType<MonoBehaviour>())
        {
            if (mb == null) continue;
            string tn = mb.GetType().Name;
            if (!DevScanTypes.Contains(tn)) continue;
            if (!byType.ContainsKey(tn)) byType[tn] = new List<MonoBehaviour>();
            byType[tn].Add(mb);
        }

        foreach (var entry in settings)
        {
            int dot = entry.key.IndexOf('.');
            if (dot < 0) continue;
            string typeName  = entry.key.Substring(0, dot);
            string fieldName = entry.key.Substring(dot + 1);
            if (!byType.TryGetValue(typeName, out var instances) || instances.Count == 0) continue;

            var field = instances[0].GetType().GetField(fieldName, DevFieldFlags);
            if (field == null) continue;

            object parsed = null;
            if      (field.FieldType == typeof(float) && float.TryParse(entry.val, out float f)) parsed = f;
            else if (field.FieldType == typeof(int)   && int.TryParse(entry.val,   out int   i)) parsed = i;
            else if (field.FieldType == typeof(bool)  && bool.TryParse(entry.val,  out bool  b)) parsed = b;
            if (parsed == null) continue;

            // Apply to every instance of this type — balance settings should be universal
            foreach (var mb in instances)
                field.SetValue(mb, parsed);
        }
    }

    private static bool IsTunableField(FieldInfo f)
    {
        if (f.FieldType != typeof(float) && f.FieldType != typeof(int) && f.FieldType != typeof(bool))
            return false;
        return (f.IsPublic && f.GetCustomAttribute<HideInInspector>() == null)
            || (!f.IsPublic && f.GetCustomAttribute<SerializeField>() != null);
    }
}
