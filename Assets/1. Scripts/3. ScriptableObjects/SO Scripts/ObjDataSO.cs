using UnityEngine;

[CreateAssetMenu(fileName = "ObjDataSO", menuName = "Scriptable Objects/ObjDataSO")]
public class ObjDataSO : ScriptableObject
{
    [Header("Basic Info")]
    public string objName;
    public int cost;
    public float hourlyCost;

    [Header("Prefab + Visuals")]
    public GameObject prefab;
    public Texture2D icon;

    [Header("Special Rules")]
    [Tooltip("If true, this object ignores all placement rules, always places at y=0, and never blocks anything.")]
    public bool ignorePlacementRules = false;

    [Tooltip("If true, this object behaves like a floor/lane: non-blocking, no height, can be under other objects.")]
    public bool isFloor = false;

    [Header("Behavior")]
    [Tooltip("If true, placing this object will clear all existing objects in the footprint area (like a bulldozer).")]
    public bool ClearsGridAfterPlacement = false;

    [Header("Stacking")]
    [Tooltip("If true, this object can be stacked on top of others and contribute vertical height.")]
    public bool isStackable = false;

    [Tooltip("Physical height of this object in meters. Used to compute total stack height in a cell.")]
    public float objHeight = 1f;

    [Header("Footprint Settings")]
    [Tooltip("Base footprint size BEFORE rotation (width x height).")]
    public Vector2Int footprint = Vector2Int.one;

    [Tooltip("Optional custom footprint shape. If empty, rectangular footprint is used.")]
    public Vector2Int[] customShapeOffsets;

    public Vector2Int[] GetFootprintOffsets(float rotation)
    {
        if (customShapeOffsets != null && customShapeOffsets.Length > 0)
            return RotateOffsets(customShapeOffsets, rotation);

        return GenerateRectangularOffsets(rotation);
    }

    private Vector2Int[] GenerateRectangularOffsets(float rotation)
    {
        Vector2Int size = GetRotatedFootprint(rotation);
        Vector2Int[] offsets = new Vector2Int[size.x * size.y];

        int index = 0;
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                offsets[index++] = new Vector2Int(x, y);
            }
        }

        return offsets;
    }

    public Vector2Int GetRotatedFootprint(float rotation)
    {
        rotation = NormalizeRotation(rotation);

        if (rotation == 90f || rotation == 270f)
            return new Vector2Int(footprint.y, footprint.x);

        return footprint;
    }

    private Vector2Int[] RotateOffsets(Vector2Int[] baseOffsets, float rotation)
    {
        rotation = NormalizeRotation(rotation);

        Vector2Int[] result = new Vector2Int[baseOffsets.Length];

        for (int i = 0; i < baseOffsets.Length; i++)
        {
            Vector2Int o = baseOffsets[i];

            switch ((int)rotation)
            {
                case 0:
                    result[i] = new Vector2Int(o.x, o.y);
                    break;
                case 90:
                    result[i] = new Vector2Int(-o.y, o.x);
                    break;
                case 180:
                    result[i] = new Vector2Int(-o.x, -o.y);
                    break;
                case 270:
                    result[i] = new Vector2Int(o.y, -o.x);
                    break;
            }
        }

        return result;
    }

    private float NormalizeRotation(float r)
    {
        r %= 360f;
        if (r < 0) r += 360f;
        return Mathf.Round(r / 90f) * 90f;
    }
}
