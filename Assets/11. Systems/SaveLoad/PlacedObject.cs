using UnityEngine;

public class PlacedObject : MonoBehaviour
{
    public ObjDataSO data;
    public int gridX;
    public int gridY;
    public int rotation;

    public void Initialize(ObjDataSO so, int x, int y, int rot)
    {
        data = so;
        gridX = x;
        gridY = y;
        rotation = rot;

        transform.rotation = Quaternion.Euler(0, rot * 90f, 0);
    }

    public PlacedObjectData ToSaveData()
    {
        return new PlacedObjectData
        {
            id = data.objName,
            x = gridX,
            y = gridY,
            rot = rotation
        };
    }
}

[System.Serializable]
public struct PlacedObjectData
{
    public string id;
    public int x;
    public int y;
    public int rot;
}
