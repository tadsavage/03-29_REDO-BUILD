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
    public float worldSpaceYHeight = 0f; // World Y position for objects that need vertical height tracking (e.g., stacked pallets)

    // True once this rack has been committed to a real aisle via RackSetupUI submit.
    // Safeguard for future systems (inventory, task assignment, etc.) that should only
    // touch racks that are part of an initialized aisle — not orange ghost placeholders.
    public bool isRackLive;

    // Racking location metadata, set when a rack is committed to an aisle. Lets a rack
    // placed directly on top of a live rack inherit its aisle + bay and just increment the
    // level (vertical stacking = more levels), without going through the chevron/setup UI.
    // -1 = not part of an initialized aisle yet.
    public int rackAisle = -1;
    public int rackBay = -1;
    public int rackLevelIndex = -1; // 0 = ground/first level

    // World-space horizontal direction pointing toward the aisle (the labeled face) for a committed
    // rack. Stored so the aisle side is deterministic and doesn't have to be re-derived from the
    // rack's live label states: stacked racks inherit it from the rack below, and a moved/rotated
    // rack re-hides the correct face from it. Vector3.zero = unset (legacy / not yet committed).
    public Vector3 rackAisleFacing = Vector3.zero;

    // World-space horizontal TRAVEL direction of the aisle (down the run, the chevron-arrow
    // direction) — the axis along which the position columns count up (pos 0 → pos 1). Persisted
    // through save/load (encoded into customData) so labels can be redrawn deterministically after
    // a load without a chevron present. Vector3.zero = unset.
    public Vector3 rackTravelDir = Vector3.zero;

    // The resolved level character actually shown on this rack's labels ("0","1","A","B"…). Stored
    // so a loaded rack can redraw its labels without re-deriving from the per-aisle Pick/Reserve
    // designations (which are runtime-only and lost on load). Empty = unset.
    public string rackLevelChar = "";

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

    /// <summary>
    /// Updates the world-space Y height for this placed object (e.g., when a pallet is moved or stacked).
    /// Call this after the object's final Y position is determined.
    /// </summary>
    public void UpdateWorldHeight(float newWorldY)
    {
        if (Mathf.Abs(worldSpaceYHeight - newWorldY) < 0.001f)
            return; // No meaningful change

        worldSpaceYHeight = newWorldY;
        // TODO: Fire GameEvents.Placement.OnPlacedObjectHeightChanged event when event system is available
    }
}
