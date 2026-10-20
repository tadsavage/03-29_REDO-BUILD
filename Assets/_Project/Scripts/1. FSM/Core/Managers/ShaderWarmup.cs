using UnityEngine;

/// <summary>
/// Pre-compiles the shader variants the game actually uses so they are not compiled lazily
/// (which is what makes materials draw missing/pink for a moment after a preset switch or a
/// save load). Call <see cref="Run"/> while a loading screen / frozen moment is acceptable.
///
/// The collection lives at Resources/Resource_Shaders/WarehouseShaderVariants and is refreshed with
/// the editor menu Tools > Shaders > Capture Used Variants (play through all 3 presets and a
/// save load first). Until that asset exists this is a harmless no-op.
/// </summary>
public static class ShaderWarmup
{
    public const string ResourcePath = "Resource_Shaders/WarehouseShaderVariants";

    private const string CrashGuardKey = "ShaderWarmup.InProgress";

    private static ShaderVariantCollection _collection;
    private static bool _loggedMissing;

    public static void Run(string reason)
    {
#if UNITY_EDITOR
        // The Editor compiles shaders on demand, so the warm-up buys nothing here, and WarmUp() has
        // hard-crashed the D3D11 driver on Play (native crash in GfxDeviceWorker -> ApplyGpuProgram,
        // right after "multisampled texture bound to a non-multisampled sampler"). Player builds only.
        return;
#pragma warning disable CS0162
#endif
        // Self-healing crash guard: the flag is written to disk BEFORE the risky call and cleared after.
        // If a previous run died inside WarmUp the flag is still set, so we skip it from then on.
        if (PlayerPrefs.GetInt(CrashGuardKey, 0) == 1)
        {
            if (!_loggedMissing)
            {
                _loggedMissing = true;
                Debug.LogWarning("[ShaderWarmup] The last warm-up crashed the game - skipping it. " +
                                 "Recapture the collection, then PlayerPrefs.DeleteKey(\"" + CrashGuardKey + "\").");
            }
            return;
        }

        if (_collection == null)
            _collection = Resources.Load<ShaderVariantCollection>(ResourcePath);

        if (_collection == null)
        {
            if (!_loggedMissing)
            {
                _loggedMissing = true;
                Debug.Log("[ShaderWarmup] No variant collection at Resources/" + ResourcePath +
                          " yet - run Tools > Shaders > Capture Used Variants.");
            }
            return;
        }

        if (_collection.isWarmedUp) return;

        float start = Time.realtimeSinceStartup;
        PlayerPrefs.SetInt(CrashGuardKey, 1);
        PlayerPrefs.Save();
        _collection.WarmUp();
        PlayerPrefs.DeleteKey(CrashGuardKey);
        Debug.Log($"[ShaderWarmup] {reason}: warmed {_collection.shaderCount} shaders / " +
                  $"{_collection.variantCount} variants in {(Time.realtimeSinceStartup - start) * 1000f:0} ms");
    }
}
