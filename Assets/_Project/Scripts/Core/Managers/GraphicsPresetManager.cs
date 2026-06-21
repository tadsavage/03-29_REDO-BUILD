using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies one of three graphics presets at runtime.
///
/// Each preset is a self-contained UniversalRenderPipelineAsset (URP_Ultra =
/// PC_RPAsset, URP_Good, URP_Toaster). ApplyPreset() swaps the active pipeline
/// via QualitySettings.renderPipeline rather than mutating fields on a single
/// shared asset — so shadow quality, APV budget, reflection-probe projection,
/// additional-light shadow resolution, etc. are baked per-asset and nothing
/// dirties the shared asset on disk.
///
/// Assign the three URP assets, the three VolumeProfile assets (PP_Ultra,
/// PP_Good, PP_Toaster), the global Volume, the shared Ultra/Good renderer
/// (PC_Renderer.asset → rendererData) and Toaster's own renderer
/// (Toaster_Renderer.asset → rendererDataToaster) in the Inspector, then call
/// ApplyPreset() from a settings menu or from DevSettings.
///
/// Current preset is persisted to PlayerPrefs key "GraphicsPreset".
/// </summary>
public class GraphicsPresetManager : MonoBehaviour
{
    public enum Preset { Ultra, Good, Toaster }

    [Header("Render Pipeline Assets")]
    [Tooltip("Assign PC_RPAsset here (the project default / full quality).")]
    [SerializeField] private UniversalRenderPipelineAsset urpUltra;
    [SerializeField] private UniversalRenderPipelineAsset urpGood;
    [SerializeField] private UniversalRenderPipelineAsset urpToaster;

    [Header("Post-Process Profiles")]
    [SerializeField] private VolumeProfile profileUltra;
    [SerializeField] private VolumeProfile profileGood;
    [SerializeField] private VolumeProfile profileToaster;

    [Header("Scene Volume")]
    [SerializeField] private Volume globalVolume;

    [Header("Renderer Data")]
    [Tooltip("Assign PC_Renderer.asset here — the renderer shared by URP Ultra/Good — to allow per-preset toggling of renderer features (SSAO / full-screen pass).")]
    [SerializeField] private ScriptableRendererData rendererData;
    [Tooltip("Assign Toaster_Renderer.asset here — URP_Toaster uses its own renderer, separate from Ultra/Good's, so it needs its own feature toggle target.")]
    [SerializeField] private ScriptableRendererData rendererDataToaster;

    public static GraphicsPresetManager Instance { get; private set; }
    public Preset CurrentPreset { get; private set; } = Preset.Ultra;

    private void Awake()
    {
        Instance = this;

        // Restore saved preset (silent — this is a startup restore, not a user action)
        int saved = PlayerPrefs.GetInt("GraphicsPreset", (int)Preset.Ultra);
        ApplyPreset((Preset)saved, notify: false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // notify controls the "Profile is X" toast — pass false when restoring a saved
    // preset on startup/load so it doesn't linger in front of the player before
    // they've done anything; user-initiated preset changes keep the default (true).
    public void ApplyPreset(Preset preset, bool notify = true)
    {
        CurrentPreset = preset;
        PlayerPrefs.SetInt("GraphicsPreset", (int)preset);

        switch (preset)
        {
            case Preset.Ultra:   ApplyUltra();   break;
            case Preset.Good:    ApplyGood();    break;
            case Preset.Toaster: ApplyToaster(); break;
        }

        if (notify)
        {
            string[] labels = { "Ultra", "Good", "Toaster" };
            UIToast.Show($"Profile is {labels[(int)preset]}", 2.5f);
        }
    }

    // ── Preset Definitions ────────────────────────────────────────────────────
    // Pipeline-level quality (shadows, APV, reflection probes, opaque texture,
    // render scale, MSAA, light counts) lives in the URP assets above. Only the
    // global QualitySettings knobs that aren't part of the URP asset are set here.

    private void ApplyUltra()
    {
        FXPool.DisabledKeys.Remove("dust");
        SetPipeline(urpUltra);
        SetRendererFeatures(rendererData, true);
        SetVolume(profileUltra);

        QualitySettings.lodBias                  = 2.0f;
        QualitySettings.anisotropicFiltering     = AnisotropicFiltering.ForceEnable;
        QualitySettings.particleRaycastBudget    = 256;
        QualitySettings.globalTextureMipmapLimit = 0;   // full-res textures
        QualitySettings.skinWeights              = SkinWeights.FourBones;
        QualitySettings.realtimeReflectionProbes = true;
        SetCameraAA(AntialiasingMode.SubpixelMorphologicalAntiAliasing);
    }

    private void ApplyGood()
    {
        FXPool.DisabledKeys.Remove("dust");
        SetPipeline(urpGood);
        SetRendererFeatures(rendererData, true);
        SetVolume(profileGood);

        QualitySettings.lodBias                  = 1.5f;
        QualitySettings.anisotropicFiltering     = AnisotropicFiltering.Enable;
        QualitySettings.particleRaycastBudget    = 64;
        QualitySettings.globalTextureMipmapLimit = 0;   // full-res textures
        QualitySettings.skinWeights              = SkinWeights.FourBones;
        QualitySettings.realtimeReflectionProbes = true;
        SetCameraAA(AntialiasingMode.FastApproximateAntialiasing);
    }

    private void ApplyToaster()
    {
        FXPool.DisabledKeys.Add("dust");
        SetPipeline(urpToaster);
        // URP_Toaster references its own renderer (Toaster_Renderer.asset), not the
        // PC_Renderer.asset that Ultra/Good share — must toggle the matching asset
        // or this silently does nothing to the active pipeline.
        SetRendererFeatures(rendererDataToaster, false);
        SetVolume(profileToaster);

        QualitySettings.lodBias                  = 0.7f;
        // Anisotropic filtering is nearly free on any modern GPU; disabling it
        // causes heavy vertical shimmer/streaking on walls at grazing angles.
        QualitySettings.anisotropicFiltering     = AnisotropicFiltering.ForceEnable;
        QualitySettings.particleRaycastBudget    = 4;
        QualitySettings.globalTextureMipmapLimit = 1;   // half-res textures (bandwidth/VRAM)
        QualitySettings.skinWeights              = SkinWeights.TwoBones;
        QualitySettings.realtimeReflectionProbes = false;
        SetCameraAA(AntialiasingMode.FastApproximateAntialiasing);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetPipeline(UniversalRenderPipelineAsset asset)
    {
        if (asset == null) return;
        // Overrides the pipeline for the active quality level; takes effect next frame.
        QualitySettings.renderPipeline = asset;
    }

    // SSAO and the cavity full-screen pass (FullScreenPassRendererFeature →
    // Cavity_Material06-07) both follow the preset: on for Ultra/Good, off on Toaster
    // to claw back frames on weak GPUs (a full-screen pass is real cost there).
    // Takes the renderer data explicitly — Ultra/Good share one renderer, but Toaster
    // uses a separate one, so a single hardcoded target would silently miss it.
    private static void SetRendererFeatures(ScriptableRendererData data, bool enabled)
    {
        if (data == null) return;
        foreach (var feature in data.rendererFeatures)
        {
            if (feature == null) continue;
            if (feature.name == "CavityPass" ||
                feature.name == "ScreenSpaceAmbientOcclusion")
                feature.SetActive(enabled);
        }
    }

    private void SetVolume(VolumeProfile profile)
    {
        if (profile == null) return;

        // Only fall back to a search if nothing was wired in the Inspector — and when
        // searching, require isGlobal so a local volume zone never gets its profile
        // overwritten by mistake.
        if (globalVolume == null)
        {
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v.isGlobal) { globalVolume = v; break; }
            }
        }

        if (globalVolume != null)
            globalVolume.sharedProfile = profile;
    }

    // Applies to every camera with UniversalAdditionalCameraData, not just Camera.main —
    // so a second camera (minimap, UI overlay, etc.) never gets left on a stale AA mode.
    private static void SetCameraAA(AntialiasingMode mode)
    {
        foreach (var cam in Camera.allCameras)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.antialiasing = mode;
        }
    }
}
