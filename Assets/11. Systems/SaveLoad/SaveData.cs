using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public string saveName;
    public int money;

    // MUST match the struct returned by PlacedObject.ToSaveData()
    public List<PlacedObjectData> placedObjects = new();
}
