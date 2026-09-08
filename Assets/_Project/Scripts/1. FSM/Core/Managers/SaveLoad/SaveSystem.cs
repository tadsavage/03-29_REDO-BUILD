using System.IO;
using UnityEngine;

public static class SaveSystem
{
    /// <summary>
    /// Gets the save folder path. Uses Application.persistentDataPath for shipping builds,
    /// but maintains backwards compatibility with editor saves.
    /// </summary>
    public static string SaveFolder
    {
        get
        {
            #if UNITY_EDITOR
                // In editor, also check legacy location for existing saves
                string legacyPath = Path.Combine(Application.dataPath, "_Saves");
                if (Directory.Exists(legacyPath) && Directory.GetFiles(legacyPath).Length > 0)
                    return legacyPath;
            #endif
            // Use persistent data path for built players and new saves
            return Path.Combine(Application.persistentDataPath, "Saves");
        }
    }

    public static void Save(SaveData data)
    {
        string folder = SaveFolder;
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(folder, data.saveName + ".json");
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
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SaveData>(json);
    }
}
