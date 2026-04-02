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

    public Vector2Int[] GetFootprintOffsets(float rotation)
    {
        Vector2Int size = PlacementMath.GetRotatedFootprint(footprint, rotation);

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
}
