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

    // True once this rack has been committed to a real aisle via RackSetupUI submit.
    // Safeguard for future systems (inventory, task assignment, etc.) that should only
    // touch racks that are part of an initialized aisle — not orange ghost placeholders.
    public bool isRackLive;

    private void OnEnable()
    {
        // Prevent registration if this object is a child of another PlacedObject.
        // This avoids nested components (like cases on a pallet) from being saved 
        // as independent objects at (0,0).
        if (transform.parent != null && transform.parent.GetComponentInParent<PlacedObject>() != null)
        {
            return;
        }

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
