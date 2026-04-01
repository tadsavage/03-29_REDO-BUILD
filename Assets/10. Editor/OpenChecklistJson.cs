using UnityEditor;
using UnityEngine;
using System.IO;

public static class OpenChecklistJson
{
    private const string FileName = "BuildPhaseTasks.json";

    [MenuItem("Window/Build Phase Checklist/Open JSON File")]
    public static void OpenJson()
    {
        string path = Path.Combine(Application.dataPath, FileName);

        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog(
                "JSON Not Found",
                $"Could not find {FileName} in the Assets folder.\n\nExpected at:\n{path}",
                "OK"
            );
            return;
        }

        // Open in the default code editor
        UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, 1);
    }
}
