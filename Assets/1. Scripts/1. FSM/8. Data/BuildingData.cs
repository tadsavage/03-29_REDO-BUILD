using UnityEngine;

public class BuildingData : MonoBehaviour
{
    [SerializeField] private ObjDataSO objDataSO;

    public ObjDataSO Data => objDataSO;

    // Required for undo/redo + MoveState
    public Vector2Int RootCell { get; private set; }
    public float Rotation { get; private set; }
    public Vector2Int[] Offsets { get; private set; }

    // ❌ Removed Awake() — it caused wrong offsets on load
    // Offsets must ONLY come from Initialize()

    /// <summary>
    /// Called by PlacementFinalizer, MoveCommand, and RebuildFromRegistry.
    /// This is the ONLY place offsets & rotation should be set.
    /// </summary>
    public void Initialize(Vector2Int root, float rotation, Vector2Int[] offsets)
    {
        RootCell = root;
        Rotation = rotation;
        Offsets = offsets;
    }

    public void Delete()
    {
        Destroy(gameObject);
    }

    public void SetOffsets(Vector2Int[] offsets)
    {
        Offsets = offsets;
    }
}
