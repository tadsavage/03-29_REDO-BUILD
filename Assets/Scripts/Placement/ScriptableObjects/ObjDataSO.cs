using UnityEngine;

[CreateAssetMenu(fileName = "ObjDataSO", menuName = "Scriptable Objects/ObjDataSO")]
public class ObjDataSO : ScriptableObject
{
    [SerializeField] private string objName;
    [SerializeField] private int cost;
    [SerializeField] private float hourlyCost;
    [SerializeField] private Vector2Int footprint;
    [SerializeField] private Vector2Int cell;
    [SerializeField] private Vector3 worldLocation;
    [SerializeField] float height;
    [SerializeField] private GameObject prefab;
    [SerializeField] private bool isStackable;
}
