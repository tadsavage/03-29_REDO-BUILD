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

    [Header("Footprint Settings")]
    [Tooltip("Base footprint size BEFORE rotation (width x height).")]
    public Vector2Int footprint = Vector2Int.one;

    [Tooltip("Optional custom footprint shape. If empty, rectangular footprint is used.")]
    public Vector2Int[] customShapeOffsets;

    [Tooltip("If true, object does not occupy grid after placement.")]
    public bool ClearsGridAfterPlacement = false;

    // ================================
    // STACKING SETTINGS
    // ================================
    [Header("Stacking")]
    [Tooltip("If true, this object can be stacked on top of others and contribute vertical height.")]
    public bool isStackable = false;

    [Tooltip("Physical height of this object in meters. Used to compute total stack height in a cell.")]
    public float objHeight = 1f;
    // ================================

    // ---------------------------------------------------------
    // FOOTPRINT API
    // ---------------------------------------------------------

    /// <summary>
    /// Returns the footprint offsets rotated by 0/90/180/270 degrees.
    /// Supports both rectangular and custom-shaped footprints.
    /// </summary>
    public Vector2Int[] GetFootprintOffsets(float rotation)
    {
        // If custom shape is defined, use it
        if (customShapeOffsets != null && customShapeOffsets.Length > 0)
            return RotateOffsets(customShapeOffsets, rotation);

        // Otherwise use rectangular footprint
        return GenerateRectangularOffsets(rotation);
    }

    // ---------------------------------------------------------
    // RECTANGULAR FOOTPRINT
    // ---------------------------------------------------------
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

    /// <summary>
    /// Returns the footprint size after rotation.
    /// </summary>
    public Vector2Int GetRotatedFootprint(float rotation)
    {
        rotation = NormalizeRotation(rotation);

        if (rotation == 90f || rotation == 270f)
            return new Vector2Int(footprint.y, footprint.x);

        return footprint;
    }

    // ---------------------------------------------------------
    // CUSTOM SHAPE FOOTPRINT
    // ---------------------------------------------------------
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
                    // 90° should go UP
                    result[i] = new Vector2Int(-o.y, o.x);
                    break;

                case 180:
                    result[i] = new Vector2Int(-o.x, -o.y);
                    break;

                case 270:
                    // 270° should go DOWN
                    result[i] = new Vector2Int(o.y, -o.x);
                    break;
            }
        }

        return result;
    }

    // ---------------------------------------------------------
    // UTILITY
    // ---------------------------------------------------------
    private float NormalizeRotation(float r)
    {
        r %= 360f;
        if (r < 0) r += 360f;
        return Mathf.Round(r / 90f) * 90f;
    }
}
