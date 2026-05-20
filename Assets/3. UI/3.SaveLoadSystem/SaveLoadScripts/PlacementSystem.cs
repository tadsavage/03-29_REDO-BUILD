using SaveLoadSystem;
using System.Collections.Generic;
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

        // Quicksave (F5) — UNTOUCHED
        if (Keyboard.current.f5Key.isPressed && quicksaveTimer <= 0f)
        {
            SaveGame("autosave");
            quicksaveTimer = quicksaveCooldown;
            AudioManager.Play("UI_Save");
            UIToast.Show("Quick-save successful");
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

        foreach (var entry in PlacedObjectRegistry.All)
        {
            SavedObject obj = new SavedObject();
            obj.id = entry.data.id;
            obj.x = entry.gridX;
            obj.y = entry.gridY;
            obj.rot = entry.rotation;
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
            SpawnFromSave(so, objSave.x, objSave.y, objSave.rot);
        }

        grid.RebuildFromRegistry();

        // Perform an immediate bake after everything is loaded
        if (NavMeshManager.Instance != null)
        {
            NavMeshManager.Instance.BakeImmediate();
        }
        }

        // ---------------------------------------------------------
    // LOAD GAME SPAWNING
    // ---------------------------------------------------------
    public PlacedObject SpawnFromSave(ObjDataSO so, int x, int y, int rot)
    {
        EnsureContainer();
        Vector2Int root = new Vector2Int(x, y);
        float rotationDeg = rot * 90f;

        float stackY = 0f;
        if (so.isStackable)
            stackY = grid.GetStackHeight(root);

        Vector3 worldPos = grid.GetCellCenter(root);
        worldPos.y += stackY;

        GameObject go = Instantiate(so.prefab, worldPos,
                                    Quaternion.Euler(0f, rotationDeg, 0f), _objectsContainer);

        PlacedObject po = go.GetComponent<PlacedObject>();
        po.Initialize(so, x, y, rot);

        BuildingData bd = go.GetComponent<BuildingData>();
        Vector2Int[] offsets = so.GetFootprintOffsets(-rotationDeg);
        bd.Initialize(root, rotationDeg, offsets);

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
        // Unsubscribe to prevent leaks
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveCompleted -= OnSlotSaveCompleted;
            SaveManager.Instance.OnLoadCompleted -= OnSlotLoadCompleted;
        }

    }
}
