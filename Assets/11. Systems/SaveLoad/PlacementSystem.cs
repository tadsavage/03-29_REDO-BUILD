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
        po.Initialize(so, x, y, rot);

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
        po.Initialize(so, x, y, rot);

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
}
