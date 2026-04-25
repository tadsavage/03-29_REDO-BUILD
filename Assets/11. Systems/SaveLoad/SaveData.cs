using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public string saveName;
    public int money;
    public List<SavedObject> placedObjects = new();
}

[System.Serializable]
public class SavedObject
{
    public int id;   // ObjDataSO ID
    public int x;    // Grid X
    public int y;    // Grid Y
    public int rot;  // Rotation index (0–3)
}
