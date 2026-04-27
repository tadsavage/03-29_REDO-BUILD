using UnityEngine;
using UnityEngine.WSA;
using UnityEngine.InputSystem;

/// <summary>
/// Handles placing objects into the world during gameplay
/// and spawning them when loading from a save file.
/// </summary>
public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private ObjDataRegistry registry;
    [SerializeField] private PlacementGrid grid;

    private MoneyService moneyService;

    private float quicksaveCooldown = 1.0f;   // seconds
    private float quicksaveTimer = 0f;
    private string lastSaveName = "autosave";

    // Called by GameContext
    public void Initialize(MoneyService money)
    {
        moneyService = money;
    }

    private void Update()
    {
        // Tick cooldown
        if (quicksaveTimer > 0f)
            quicksaveTimer -= Time.deltaTime;

        // Quicksave (F5)
        if (Keyboard.current.f5Key.isPressed && quicksaveTimer <= 0f)
        {
            SaveGame("autosave");
            quicksaveTimer = quicksaveCooldown;

            AudioManager.Play("UI_Save");   // ⭐ your save SFX

            UIToast.Show("Quick-save successful");  // ⭐ your toast system
        }

        // Quickload (F9)
        if (Keyboard.current.f9Key.isPressed && quicksaveTimer <= 0f)
        {
            LoadGame();
            AudioManager.Play("UI_Load");   // optional

            UIToast.Show("Quick-load successful");  // ⭐ your toast system
        }
    }
    // ---------------------------------------------------------
    // NORMAL GAMEPLAY PLACEMENT
    // ---------------------------------------------------------
    public PlacedObject PlaceObject(ObjDataSO so, int x, int y, int rot)
    {
        Vector2Int cell = new Vector2Int(x, y);
        Vector3 worldPos = grid.GetCellCenter(cell);

        GameObject go = Instantiate(so.prefab, worldPos, Quaternion.Euler(0f, rot * 90f, 0f));

        PlacedObject po = go.GetComponent<PlacedObject>();
        po.Initialize(so, x, y, rot);

        PlacedObjectRegistry.Register(po);
        grid.AddStackObject(cell, go, so);

        return po;
    }
    // ---------------------------------------------------------
    // SAVE GAME
    // ---------------------------------------------------------
    public void SaveGame(string saveName)
    {
        lastSaveName = saveName;

        SaveData save = new SaveData();
        save.saveName = saveName;
        // 1. Save money
        save.money = moneyService.CurrentCapital;

        // 1b. Save spent today
        save.spentToday = moneyService.SpentToday; // ⭐ NEW


        foreach (var entry in PlacedObjectRegistry.All)
        {
            SavedObject obj = new SavedObject();
            obj.id = entry.data.id;
            obj.x = entry.gridX;
            obj.y = entry.gridY;
            obj.rot = entry.rotation;
            save.placedObjects.Add(obj);
        }

        SaveSystem.Save(save);
    }
    // ---------------------------------------------------------
    // LOAD GAME
    // ---------------------------------------------------------
    public void LoadGame()
    {
        Debug.Log($"Attempting to load save: {lastSaveName}");
        SaveData save = SaveSystem.Load(lastSaveName);
        if (save == null)
        {
            Debug.LogError($"LoadGame: no save file found for {lastSaveName}");
            return;
        }
        Debug.Log($"Loaded money: {save.money}, spent today: {save.spentToday}"); // ⭐ NEW
        moneyService.SetMoney(save.money);
        moneyService.SetSpentToday(save.spentToday);   // ⭐ NEW
        ClearAll();

        foreach (var objSave in save.placedObjects)
        {
            ObjDataSO so = registry.GetByID(objSave.id);
            SpawnFromSave(so, objSave.x, objSave.y, objSave.rot);
        }
        grid.RebuildFromRegistry();
    }
    // ---------------------------------------------------------
    // LOAD GAME SPAWNING
    // ---------------------------------------------------------
    public PlacedObject SpawnFromSave(ObjDataSO so, int x, int y, int rot)
    {
        Vector2Int root = new Vector2Int(x, y);
        float rotationDeg = rot * 90f;

        // 1. Ask grid what the current stack height is at this root
        float stackY = 0f;
        if (so.isStackable)
            stackY = grid.GetStackHeight(root); // BEFORE adding this new one

        // 2. Place at correct world position
        Vector3 worldPos = grid.GetCellCenter(root);
        worldPos.y += stackY;

        GameObject go = Instantiate(so.prefab, worldPos, Quaternion.Euler(0f, rotationDeg, 0f));

        // 3. PlacedObject init
        PlacedObject po = go.GetComponent<PlacedObject>();
        po.Initialize(so, x, y, rot);

        // 4. BuildingData init
        BuildingData bd = go.GetComponent<BuildingData>();
        Vector2Int[] offsets = so.GetFootprintOffsets(rotationDeg);
        bd.Initialize(root, rotationDeg, offsets);

        // 5. Registry
        PlacedObjectRegistry.Register(po);

        // 6. Register ALL footprint cells in grid
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
    public void ClearAll()
    {
        // Destroy all objects in the registry
        foreach (var obj in PlacedObjectRegistry.All)
        {
            if (obj != null)
            {
                Vector2Int cell = new Vector2Int(obj.gridX, obj.gridY);
                grid.RemoveStackObject(cell, obj.gameObject, obj.data);
                Destroy(obj.gameObject);
            }
        }

        // Clear registry AFTER the loop
        PlacedObjectRegistry.Clear();

        // Reset the grid
        grid.InitializeGrid();
    }
}
