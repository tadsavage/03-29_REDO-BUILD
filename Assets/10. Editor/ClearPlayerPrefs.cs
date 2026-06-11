using UnityEditor;
using UnityEngine;

/// <summary>
/// Quick menu item to wipe all PlayerPrefs (save state, player name, etc).
/// Use this when you delete the _Saves folder and want a clean slate.
/// </summary>
public static class ClearPlayerPrefs
{
    [MenuItem("Tools/Clear PlayerPrefs & Restart")]
    public static void Run()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[ClearPlayerPrefs] All PlayerPrefs cleared. Delete _Saves folder if needed, then restart.");
        EditorUtility.DisplayDialog(
            "PlayerPrefs Cleared",
            "All PlayerPrefs have been deleted.\n\n" +
            "Next time you start a new game, it will create a fresh save.",
            "OK");
    }
}
