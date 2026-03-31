using UnityEngine;

[CreateAssetMenu(fileName = "ObjDataSO", menuName = "Scriptable Objects/ObjDataSO")]
public class ObjDataSO : ScriptableObject
{
    public string objName;
    public int cost;
    public float hourlyCost;
    public Vector2Int footprint;
    public Vector2Int cell;
    public Vector3 worldLocation;
    public float height;
    public GameObject prefab;
    public Texture2D icon;
    public bool isStackable;
}
