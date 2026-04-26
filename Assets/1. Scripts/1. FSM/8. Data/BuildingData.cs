using UnityEngine;

public class BuildingData : MonoBehaviour
{
    [SerializeField] private ObjDataSO objDataSO;

    public ObjDataSO Data => objDataSO;

    // NEW: required for undo/redo
    public Vector2Int RootCell { get; private set; }
    public float Rotation { get; private set; }
    public Vector2Int[] Offsets { get; private set; }

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
}