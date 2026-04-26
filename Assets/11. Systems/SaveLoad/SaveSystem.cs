using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string SaveFolder => Path.Combine(Application.persistentDataPath, "Saves");

    public static void Save(SaveData data)
    {
        if (!Directory.Exists(SaveFolder))
            Directory.CreateDirectory(SaveFolder);

        string path = Path.Combine(SaveFolder, data.saveName + ".json");
        string json = JsonUtility.ToJson(data, true);

        File.WriteAllText(path, json);
       // Debug.Log($"Saved to: {path}");
    }

    public static SaveData Load(string saveName)
    {
        string path = Path.Combine(SaveFolder, saveName + ".json");

        if (!File.Exists(path))
        {
            Debug.LogWarning($"Save not found: {path}");
            return null;
        }

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SaveData>(json);
    }

    public static string[] GetAllSaveFiles()
    {
        if (!Directory.Exists(SaveFolder))
            return new string[0];

        string[] files = Directory.GetFiles(SaveFolder, "*.json");
        for (int i = 0; i < files.Length; i++)
            files[i] = Path.GetFileNameWithoutExtension(files[i]);

        return files;
    }
}
