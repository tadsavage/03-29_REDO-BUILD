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

    private static ShaderVariantCollection _collection;
    private static bool _loggedMissing;

    public static void Run(string reason)
    {
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
        _collection.WarmUp();
        Debug.Log($"[ShaderWarmup] {reason}: warmed {_collection.shaderCount} shaders / " +
                  $"{_collection.variantCount} variants in {(Time.realtimeSinceStartup - start) * 1000f:0} ms");
    }
}
