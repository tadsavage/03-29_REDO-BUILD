using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Configures every PanelSettings asset in the project to scale with screen size,
/// using 1920x1080 as the reference resolution. This makes all UI Toolkit panels
/// look proportionally identical at any resolution — 4K will render at higher fidelity
/// but the same physical layout as 1080p.
///
/// Run once from: Tools / UI / Configure Scale-With-Screen-Size
/// </summary>
public static class PanelSettingsScaler
{
    [MenuItem("Tools/UI/Configure Scale-With-Screen-Size")]
    private static void ConfigureAll()
    {
        var guids = AssetDatabase.FindAssets("t:PanelSettings");
        int count = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Skip third-party / sample PanelSettings
            if (path.Contains("/Samples/") || path.Contains("\\Samples\\")) continue;

            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null) continue;

            ps.scaleMode          = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode    = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match              = 0.5f;

            EditorUtility.SetDirty(ps);
            count++;

            Debug.Log($"[PanelSettingsScaler] Configured: {path}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[PanelSettingsScaler] Done — {count} PanelSettings configured for ScaleWithScreenSize (1920×1080 reference).");
    }

    [MenuItem("Tools/UI/Configure Scale-With-Screen-Size", true)]
    private static bool ValidateNotPlaying() => !Application.isPlaying;
}
