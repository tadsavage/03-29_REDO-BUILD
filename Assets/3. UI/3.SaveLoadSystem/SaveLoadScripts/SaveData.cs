using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public string saveName;
    public int money;
    public int spentToday;   // ⭐ NEW
    public CameraSaveData cameraData; // ⭐ NEW
    public List<SavedObject> placedObjects = new();
}

[System.Serializable]
public class CameraSaveData
{
    public Vector3 focusPoint;
    public float distance;
    public float pitch;
    public float yaw;
}

[System.Serializable]
public class SavedObject
{
    public int id;   // ObjDataSO ID
    public int x;    // Grid X
    public int y;    // Grid Y
    public int rot;  // Rotation index (0–3)
    public string customData;
}
