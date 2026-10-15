using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tools for the shader warm-up collection (see ShaderWarmup).
/// Capture Used Variants saves every shader variant Unity has compiled/used in this Editor session,
/// so: enter Play, cycle Ultra/Good/Toaster, load a save, open the main panels, THEN run it.
/// </summary>
public static class ShaderVariantCaptureMenu
{
    private const string AssetPath = "Assets/_Project/Resources/" + ShaderWarmup.ResourcePath + ".shadervariants";

    [MenuItem("Tools/Shaders/Capture Used Variants")]
    public static void Capture()
    {
        var save = typeof(ShaderUtil).GetMethod("SaveCurrentShaderVariantCollection",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (save == null)
        {
            Debug.LogError("[ShaderVariantCapture] ShaderUtil.SaveCurrentShaderVariantCollection not found in this Unity version.");
            return;
        }

        save.Invoke(null, new object[] { AssetPath });
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);

        var svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(AssetPath);
        Debug.Log(svc != null
            ? $"[ShaderVariantCapture] Saved {svc.shaderCount} shaders / {svc.variantCount} variants to {AssetPath}"
            : "[ShaderVariantCapture] Save ran but the asset could not be loaded.");
    }

    [MenuItem("Tools/Shaders/Clear Tracked Variants")]
    public static void ClearTracked()
    {
        var clear = typeof(ShaderUtil).GetMethod("ClearCurrentShaderVariantCollection",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        clear?.Invoke(null, null);
        Debug.Log("[ShaderVariantCapture] Cleared the tracked-variant list.");
    }

    [MenuItem("Tools/Shaders/Register Collection As Preloaded (Graphics Settings)")]
    public static void RegisterPreloaded()
    {
        var svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(AssetPath);
        if (svc == null) { Debug.LogError("[ShaderVariantCapture] Run Capture Used Variants first."); return; }

        var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (settings == null || settings.Length == 0) { Debug.LogError("[ShaderVariantCapture] GraphicsSettings not found."); return; }

        var so = new SerializedObject(settings[0]);
        var list = so.FindProperty("m_PreloadedShaders");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == svc) { Debug.Log("[ShaderVariantCapture] Already registered."); return; }

        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = svc;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[ShaderVariantCapture] Registered in Graphics Settings > Preloaded Shaders.");
    }
}
