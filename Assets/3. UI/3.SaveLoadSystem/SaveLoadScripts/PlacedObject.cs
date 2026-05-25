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
    public string customData;

    private void OnEnable()
{
        // Ensure registration even if spawned manually or from a save
        PlacedObjectRegistry.Register(this);
    }

    private void OnDisable()
    {
        // Remove from registry when disabled (e.g. by Undo) to prevent saving inactive objects
        PlacedObjectRegistry.Unregister(this);
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
}
