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

        string[] labels = { "Ultra Graphics Profile", "Good Graphics Profile", "Toaster Graphics Profile" };
        UIToast.Show($"Switched to {labels[(int)preset]}", 2.5f);
    }

    // ── Preset Definitions ────────────────────────────────────────────────────

    private void ApplyUltra()
    {
        SetVolume(profileUltra);
        if (_urp == null) return;

        _urp.renderScale                     = 1.0f;
        _urp.msaaSampleCount                 = 4;
        _urp.shadowCascadeCount              = 4;
        _urp.shadowDistance                  = 100f;
        _urp.mainLightShadowmapResolution    = 4096;
        // supportsSoftShadows is read-only on URP asset (controlled by shadow cascade settings)
        _urp.maxAdditionalLightsCount        = 8;
        _urp.supportsHDR                     = true;
        QualitySettings.lodBias              = 2.0f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        SetCameraAA(AntialiasingMode.SubpixelMorphologicalAntiAliasing);
        //Debug.Log("[Graphics] Ultra applied.");
    }

    private void ApplyGood()
    {
        SetVolume(profileGood);
        if (_urp == null) return;

        _urp.renderScale                     = 1.0f;
        _urp.msaaSampleCount                 = 2;
        _urp.shadowCascadeCount              = 2;
        _urp.shadowDistance                  = 60f;
        _urp.mainLightShadowmapResolution    = 2048;
        // supportsSoftShadows is read-only on URP asset (controlled by shadow cascade settings)
        _urp.maxAdditionalLightsCount        = 4;
        _urp.supportsHDR                     = true;
        QualitySettings.lodBias              = 1.5f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        SetCameraAA(AntialiasingMode.FastApproximateAntialiasing);
        //Debug.Log("[Graphics] Good applied.");
    }

    private void ApplyToaster()
    {
        SetVolume(profileToaster);
        if (_urp == null) return;

        _urp.renderScale                     = 0.85f;
        _urp.msaaSampleCount                 = 1;
        _urp.shadowCascadeCount              = 1;
        _urp.shadowDistance                  = 35f;
        _urp.mainLightShadowmapResolution    = 1024;
        // supportsSoftShadows is read-only on URP asset
        _urp.maxAdditionalLightsCount        = 2;
        _urp.supportsHDR                     = false; 
        QualitySettings.lodBias              = 0.7f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        SetCameraAA(AntialiasingMode.FastApproximateAntialiasing);
        //Debug.Log("[Graphics] Toaster applied.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
