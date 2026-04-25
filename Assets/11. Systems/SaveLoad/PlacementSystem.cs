using UnityEngine;

/// <summary>
/// Handles placing objects into the world during gameplay
/// and spawning them when loading from a save file.
/// </summary>
public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private ObjDataRegistry registry;
    [SerializeField] private PlacementGrid grid;

    private MoneyService moneyService;

    // Called by GameContext
    public void Initialize(MoneyService money)
    {
        moneyService = money;
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
    // LOAD GAME SPAWNING
    // ---------------------------------------------------------
    public PlacedObject SpawnFromSave(ObjDataSO so, int x, int y, int rot)
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
    // CLEAR ALL OBJECTS
    // ---------------------------------------------------------
    public void ClearAll()
    {
        foreach (var obj in PlacedObjectRegistry.All)
        {
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

    // ---------------------------------------------------------
    // SAVE GAME
    // ---------------------------------------------------------
    public void SaveGame(string saveName)
    {
        Debug.Log("SaveGame() START");

        SaveData save = new SaveData();
        save.saveName = saveName;

        // 1. Save money
        save.money = moneyService.CurrentCapital;

        // 2. Save all placed objects
        foreach (var entry in PlacedObjectRegistry.All)
        {
            SavedObject obj = new SavedObject();
            obj.id = entry.data.id;
            obj.x = entry.gridX;
            obj.y = entry.gridY;
            obj.rot = entry.rotation;

            save.placedObjects.Add(obj);
        }

        Debug.Log("SaveGame: saving " + save.placedObjects.Count + " objects");

        SaveSystem.Save(save);

        Debug.Log("SaveGame() END");
    }

    // ---------------------------------------------------------
    // LOAD GAME
    // ---------------------------------------------------------
    public void LoadGame()
    {
        Debug.Log("LoadGame() START");

        SaveData save = SaveSystem.Load("tad");
        if (save == null)
        {
            Debug.LogError("LoadGame: no save file found");
            return;
        }

        // 1. Restore money
        moneyService.SetMoney(save.money);

        // 2. Clear world
        ClearAll();

        // 3. Spawn objects
        foreach (var objSave in save.placedObjects)
        {
            ObjDataSO so = registry.GetByID(objSave.id);
            if (so == null)
            {
                Debug.LogError($"LoadGame: ObjDataSO not found for id {objSave.id}");
                continue;
            }

            SpawnFromSave(so, objSave.x, objSave.y, objSave.rot);
        }

        Debug.Log("LoadGame() END");
    }
}
