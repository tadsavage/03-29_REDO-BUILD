using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ObjDataSO", menuName = "Scriptable Objects/ObjDataSO")]
public class ObjDataSO : ScriptableObject
{
    [Header("Basic Info")]
    public int id;
    public string objName;
    public int cost;
    public int hourlyCost;
    public string category;

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

    [Header("Pathfinding Clear (ie. Doors)")]
    [Tooltip("If true, this object will not instantiate a NavMesh Obstacle on placement - allowing agents to pass-thru. Object will still need a custom script foor door animations - etc.")]
    public bool pathfindingClear = false;

    [Tooltip("NavMesh Area index to assign to this object (if pathfindingClear is true). 0 = Walkable, 3 = MHE Lanes, 4 = Pedestrian Lanes.")]
    public int navArea = 0;

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
        Vector2Int[] baseOffsets;

        // If custom shape exists, use it
        if (customShapeOffsets != null && customShapeOffsets.Length > 0)
        {
            baseOffsets = customShapeOffsets;
        }
        else
        {
            // Generate rectangular footprint in canonical orientation
            baseOffsets = new Vector2Int[footprint.x * footprint.y];
            int index = 0;
            for (int x = 0; x < footprint.x; x++)
            {
                for (int y = 0; y < footprint.y; y++)
                {
                    baseOffsets[index++] = new Vector2Int(x, y);
                }
            }
        }

        // Rotate using the SAME logic as custom shapes
        return RotateOffsets(baseOffsets, rotation);
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
    public class ObjDataRegistry : ScriptableObject
    {
        public List<ObjDataSO> buttonSOs = new();

        public ObjDataSO Get(int index)
        {
            if (index < 0 || index >= buttonSOs.Count)
                return null;

            return buttonSOs[index];
        }
    }
}
