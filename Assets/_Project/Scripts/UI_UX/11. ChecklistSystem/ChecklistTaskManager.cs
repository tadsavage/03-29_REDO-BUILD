using System.IO;
using UnityEngine;

public static class ChecklistTaskManager
{
    private static readonly string FilePath = Path.Combine(Application.dataPath, "BuildPhaseTasks.json");

    public static ChecklistRoot Load()
    {
        if (!File.Exists(FilePath))
        {
            Debug.LogWarning($"Checklist file not found at {FilePath}. Creating new.");
            return new ChecklistRoot();
        }

        try
        {
            string json = File.ReadAllText(FilePath);
            return JsonUtility.FromJson<ChecklistRoot>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error loading checklist: {ex.Message}");
            return new ChecklistRoot();
        }
    }

    public static void Save(ChecklistRoot root)
    {
        try
        {
            string json = JsonUtility.ToJson(root, true);
            File.WriteAllText(FilePath, json);
            Debug.Log($"Checklist saved to {FilePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error saving checklist: {ex.Message}");
        }
    }
}
