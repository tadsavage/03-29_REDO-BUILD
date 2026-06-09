using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies one of three graphics presets at runtime.
///
/// Assign the three VolumeProfile assets (PP_Ultra, PP_Good, PP_Toaster)
/// and the global Volume in the Inspector, then call ApplyPreset() from
/// a settings menu or from DevSettings.
///
/// Current preset is persisted to PlayerPrefs key "GraphicsPreset".
/// </summary>
public class GraphicsPresetManager : MonoBehaviour
{
    public enum Preset { Ultra, Good, Toaster }

    [Header("Post-Process Profiles")]
    [SerializeField] private VolumeProfile profileUltra;
    [SerializeField] private VolumeProfile profileGood;
    [SerializeField] private VolumeProfile profileToaster;

    [Header("Scene Volume")]
    [SerializeField] private Volume globalVolume;

    [Header("Renderer Data")]
    [Tooltip("Assign PC_Renderer.asset here to allow per-preset toggling of renderer features.")]
    [SerializeField] private ScriptableRendererData rendererData;

    public static GraphicsPresetManager Instance { get; private set; }
    public Preset CurrentPreset { get; private set; } = Preset.Ultra;

    private UniversalRenderPipelineAsset _urp;

    private void Awake()
    {
        Instance = this;
        _urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        // Restore saved preset
        int saved = PlayerPrefs.GetInt("GraphicsPreset", (int)Preset.Ultra);
        ApplyPreset((Preset)saved);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ApplyPreset(Preset preset)
    {
        CurrentPreset = preset;
        PlayerPrefs.SetInt("GraphicsPreset", (int)preset);

        switch (preset)
        {
            case Preset.Ultra:   ApplyUltra();   break;
            case Preset.Good:    ApplyGood();    break;
            case Preset.Toaster: ApplyToaster(); break;
        }

        string[] labels = { "Ultra", "Good", "Toaster" };
        UIToast.Show($"Profile is {labels[(int)preset]}", 2.5f);
    }

    // ── Preset Definitions ────────────────────────────────────────────────────

    private void ApplyUltra()
    {
        FXPool.DisabledKeys.Remove("dust");
        SetRendererFeatures(true);
        SetVolume(profileUltra);
        if (_urp == null) return;

        _urp.renderScale                     = 1.0f;
        _urp.msaaSampleCount                 = 4;
        _urp.shadowCascadeCount              = 4;
        _urp.shadowDistance                  = 100f;
        _urp.mainLightShadowmapResolution    = 4096;
        _urp.maxAdditionalLightsCount        = 8;
        _urp.supportsHDR                     = true;
        QualitySettings.lodBias              = 2.0f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.particleRaycastBudget = 256;
        SetCameraAA(AntialiasingMode.SubpixelMorphologicalAntiAliasing);
    }

    private void ApplyGood()
    {
        FXPool.DisabledKeys.Remove("dust");
        SetRendererFeatures(true);
        SetVolume(profileGood);
        if (_urp == null) return;

        _urp.renderScale                     = 1.0f;
        _urp.msaaSampleCount                 = 2;
        _urp.shadowCascadeCount              = 2;
        _urp.shadowDistance                  = 60f;
        _urp.mainLightShadowmapResolution    = 2048;
        _urp.maxAdditionalLightsCount        = 4;
        _urp.supportsHDR                     = true;
        QualitySettings.lodBias              = 1.5f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.particleRaycastBudget = 64;
        SetCameraAA(AntialiasingMode.FastApproximateAntialiasing);
    }

    private void ApplyToaster()
    {
        FXPool.DisabledKeys.Add("dust");
        SetRendererFeatures(false);
        SetVolume(profileToaster);
        if (_urp == null) return;

        _urp.renderScale                     = 0.85f;
        _urp.msaaSampleCount                 = 1;
        _urp.shadowCascadeCount              = 1;
        _urp.shadowDistance                  = 35f;
        _urp.mainLightShadowmapResolution    = 1024;
        _urp.maxAdditionalLightsCount        = 2;
        _urp.supportsHDR                     = false;
        QualitySettings.lodBias              = 0.7f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.particleRaycastBudget = 4;
        SetCameraAA(AntialiasingMode.FastApproximateAntialiasing);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetRendererFeatures(bool enabled)
    {
        if (rendererData == null) return;
        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature == null) continue;
            if (feature.name == "ScreenSpaceAmbientOcclusion" ||
                feature.name == "FullScreenPassRendererFeature")
                feature.SetActive(enabled);
        }
    }

    private void SetVolume(VolumeProfile profile)
    {
        if (profile == null) return;

        if (globalVolume == null)
            globalVolume = FindAnyObjectByType<Volume>();

        if (globalVolume != null)
            globalVolume.sharedProfile = profile;
    }

    private static void SetCameraAA(AntialiasingMode mode)
    {
        var cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        if (cam == null) return;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data != null) data.antialiasing = mode;
    }
}
