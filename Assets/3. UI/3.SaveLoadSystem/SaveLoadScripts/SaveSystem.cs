using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string SaveFolder =>
        Path.Combine(Application.persistentDataPath, "Saves");

    public static void Save(SaveData data)
    {
        if (!Directory.Exists(SaveFolder))
            Directory.CreateDirectory(SaveFolder);

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(SaveFolder, data.saveName + ".json");
        File.WriteAllText(path, json);
        Debug.Log($"[SaveSystem] Saved → {path}");
    }

    public static SaveData Load(string saveName)
    {
        string path = Path.Combine(SaveFolder, saveName + ".json");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveSystem] No save found at {path}");
            return null;
        }
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SaveData>(json);
    }
}
