using UnityEngine;

[CreateAssetMenu(fileName = "ObjDataSO", menuName = "Scriptable Objects/ObjDataSO")]
public class ObjDataSO : ScriptableObject
{
    public string objName;
    public int cost;
    public float hourlyCost;
    // Base footprint (width x depth)
    public Vector2Int footprint = Vector2Int.one;
    public Vector2Int cell;
    public Vector3 worldLocation;
    public float height;
    public GameObject prefab;
    public Texture2D icon;
    public bool isStackable;
    public bool ClearsGridAfterPlacement; //For objects that move ie. MHE and Workers.

    public Vector2Int[] GetFootprintOffsets(float rotation)
    {
        Vector2Int size = PlacementMath.GetRotatedFootprint(footprint, rotation);
        Vector2Int[] offsets = new Vector2Int[size.x * size.y];
        if(size.x == 1 && size.y == 1)
        {
            return offsets; 
        }
        int index = 0;
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++) // Fill offsets based on the rotated size
            {
                offsets[index++] = new Vector2Int(x, y);
            }
            if (rotation == 0) // No rotation, offsets are straightforward
            {
                offsets[index - 1] = new Vector2Int(-x, 0);
            }
            if (rotation == 270) // 270° rotation, swap X and Y, so the last offset should be (0, -1)
            {
                offsets[index - 1] = new Vector2Int(0, -1);
            }            
        }
        return offsets;
    }
}
