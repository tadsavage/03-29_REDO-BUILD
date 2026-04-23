using UnityEngine;

/// <summary>
/// Component attached to every placeable prefab.
/// Stores grid position, rotation, and data reference.
/// Automatically registers/unregisters itself in the global registry.
/// </summary>
public class PlacedObject : MonoBehaviour
{
    public ObjDataSO data;
    public int gridX;
    public int gridY;
    public int rotation;

    private void OnEnable()
    {
        // Ensure registration even if spawned manually or from a save
        PlacedObjectRegistry.Register(this);
    }

    private void OnDestroy()
    {
        // Clean up registry when destroyed
        PlacedObjectRegistry.Unregister(this);
    }

    /// <summary>
    /// Initializes this placed object with its data and grid coordinates.
    /// </summary>
    public void Initialize(ObjDataSO so, int x, int y, int rot)
    {
        data = so;
        gridX = x;
        gridY = y;
        rotation = rot;

        transform.rotation = Quaternion.Euler(0, rot * 90f, 0);
    }

    /// <summary>
    /// Converts this object into serializable save data.
    /// </summary>
    public PlacedObjectData ToSaveData()
    {
        return new PlacedObjectData
        {
            id = data.objName,
            x = gridX,
            y = gridY,
            rot = rotation
        };
    }
}

/// <summary>
/// Serializable struct used for saving and loading placed objects.
/// </summary>
[System.Serializable]
public struct PlacedObjectData
{
    public string id;
    public int x;
    public int y;
    public int rot;
}
