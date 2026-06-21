using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string SaveFolder =>
        Path.Combine(Application.dataPath, "_Saves");

    public static void Save(SaveData data)
    {
        if (!Directory.Exists(SaveFolder))
            Directory.CreateDirectory(SaveFolder);

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(SaveFolder, data.saveName + ".json");
        File.WriteAllText(path, json);
    }

    public static SaveData Load(string saveName)
    {
        string path = Path.Combine(SaveFolder, saveName + ".json");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveSystem] No save found at {path}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveSystem] Failed to parse JSON from {path}: {ex.Message}");
            return null;
        }
    }
}
