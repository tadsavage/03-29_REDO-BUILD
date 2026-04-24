using UnityEditor;
using UnityEngine;

/// <summary>
/// Handles placing objects into the world, both during gameplay
/// and when loading from a save file.
/// </summary>
public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private ObjDataRegistry registry;
    [SerializeField] private PlacementGrid grid;

    /// <summary>
    /// Places an object during normal gameplay (player clicking).
    /// </summary>
    public PlacedObject PlaceObject(ObjDataSO so, int x, int y, int rot)
    {
        Vector2Int cell = new Vector2Int(x, y);
        Vector3 worldPos = grid.GetCellCenter(cell);

        GameObject go = Instantiate(so.prefab, worldPos, Quaternion.identity);

        PlacedObject po = go.GetComponent<PlacedObject>();
        po.Initialize(so, cell.x, cell.y, rot);

        // Register in save system
        PlacedObjectRegistry.Register(po);

        grid.AddStackObject(cell, go, so);

        return po;
    }


    /// <summary>
    /// Spawns an object from save data.
    /// </summary>
    public PlacedObject SpawnFromSave(ObjDataSO so, int x, int y, int rot)
    {
        Vector2Int cell = new Vector2Int(x, y);
        Vector3 worldPos = grid.GetCellCenter(cell);

        GameObject go = Instantiate(so.prefab, worldPos, Quaternion.identity);

        PlacedObject po = go.GetComponent<PlacedObject>();
        po.Initialize(so, cell.x, cell.y, rot);

        // Register loaded object
        PlacedObjectRegistry.Register(po);

        grid.AddStackObject(cell, go, so);

        return po;
    }

    /// <summary>
    /// Clears all placed objects from the world.
    /// </summary>
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
    }
    public void LoadGame()
    {
        Debug.Log("LoadGame() START");

        SaveData save = SaveSystem.Load("tad");
        if (save == null)
        {
            Debug.LogError("LoadGame: no save file found");
            return;
        }

        Debug.Log("LoadGame: data loaded OK");

        // 1. Restore money
        moneyService.SetMoney(save.money);

        // 2. Clear existing objects
        foreach (var entry in PlacedObjectRegistry.All)
            Destroy(entry.instance);

        PlacedObjectRegistry.Clear();
        grid.InitializeGrid();

        // 3. Spawn saved objects
        Debug.Log($"LoadGame: spawning objects, count = {save.placedObjects.Count}");

        foreach (var objSave in save.placedObjects)
        {
            ObjDataSO data = registry.GetByID(objSave.id);
            if (data == null)
            {
                Debug.LogError($"LoadGame: could not find SO for id {objSave.id}");
                continue;
            }

            Vector2Int cell = new Vector2Int(objSave.x, objSave.y);
            Vector3 worldPos = grid.CellToWorld(cell);
            Quaternion worldRot = Quaternion.Euler(0f, objSave.rot * 90f, 0f);

            GameObject obj = Instantiate(data.prefab, worldPos, worldRot);

            // Register in grid
            grid.AddStackObject(cell, obj, data);

            // Register globally
            PlacedObjectRegistry.Add(obj, data, objSave.x, objSave.y, objSave.rot);

            // Reinitialize highlighter
            var highlighter = obj.GetComponent<BuildingHighlighter>();
            if (highlighter != null)
                highlighter.Initialize();

            Debug.Log($"Spawned {data.objName} at {objSave.x},{objSave.y} rot {objSave.rot}");
        }

        Debug.Log("LoadGame() END");
    }

}
