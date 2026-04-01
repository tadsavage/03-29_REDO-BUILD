using System.IO;
using UnityEngine;

public static class ChecklistTaskManager
{
    private const string FileName = "BuildPhaseTasks.json";

    public static string GetFilePath()
    {
        return Path.Combine(Application.dataPath, FileName);
    }

    public static ChecklistRoot Load()
    {
        var path = GetFilePath();
        if (!File.Exists(path))
        {
            var data = new ChecklistRoot();
            return data;
        }

        var json = File.ReadAllText(path);
        return JsonUtility.FromJson<ChecklistRoot>(json);
    }

    public static void Save(ChecklistRoot data)
    {
        var path = GetFilePath();
        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
}
