using UnityEngine;

public class BuildingData : MonoBehaviour
{
    [SerializeField] private ObjDataSO objDataSO;

    public ObjDataSO Data => objDataSO;

    // Required for undo/redo + MoveState + save/load
    public Vector2Int RootCell { get; private set; }
    public float Rotation { get; private set; }
    public Vector2Int[] Offsets { get; private set; }

    /// <summary>
    /// The ONLY place where RootCell, Rotation, and Offsets are set.
    /// Called by:
    /// - PlacementFinalizer (initial placement)
    /// - MoveCommand (moving an object)
    /// - PlacementGrid.RebuildFromRegistry (loading a save)
    /// </summary>
    public void Initialize(Vector2Int root, float rotation, Vector2Int[] offsets)
    {
        RootCell = root;
        Rotation = rotation;

        // Safety: ensure offsets are never null or empty
        if (offsets == null || offsets.Length == 0)
        {
            // Recompute using the unified rotation convention
            Offsets = Data != null
                ? Data.GetFootprintOffsets(-rotation)
                : new Vector2Int[] { Vector2Int.zero };
        }
        else
        {
            Offsets = offsets;
        }
    }

    /// <summary>
    /// Used only when MoveState detects missing offsets on legacy objects.
    /// </summary>
    public void SetOffsets(Vector2Int[] offsets)
    {
        if (offsets == null || offsets.Length == 0)
            return;

        Offsets = offsets;
    }

    public void Delete()
    {
        Destroy(gameObject);
    }
}
