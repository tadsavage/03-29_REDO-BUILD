using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// One switch for all <c>Debug.Log</c> chatter. OFF (default) = info logs are muted, warnings and
/// errors still show. The 290-odd Debug.Log calls stay in the code; flip this back on when you
/// want to trace something. Persisted in PlayerPrefs ("ShowDebugLogs"); in the Editor use
/// Tools ▸ Logging ▸ Show Debug.Log (checkmark = on).
/// </summary>
public static class DebugLogSwitch
{
    private const string PrefKey = "ShowDebugLogs";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefKey, 0) == 1;
        set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); Apply(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Debug.unityLogger.filterLogType = Enabled ? LogType.Log : LogType.Warning;
    }

#if UNITY_EDITOR
    private const string MenuPath = "Tools/Logging/Show Debug.Log";

    [InitializeOnLoadMethod]
    private static void ApplyInEditor() => Apply();

    [MenuItem(MenuPath)]
    private static void Toggle() => Enabled = !Enabled;

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }
#endif
}
