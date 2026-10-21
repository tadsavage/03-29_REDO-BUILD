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
/// Each URP asset also points at its OWN ScriptableRendererData — PC_Renderer
/// (Ultra), Good_Renderer, Toaster_Renderer — so the renderer feature set and
/// the cavity material each preset uses are baked per-asset too. The cavity
/// full-screen pass is the single most expensive thing in the frame and its
/// cost is driven by the material's _Radius (the shader does a (2r+1)^2 loop
/// with 4 normal-buffer taps per iteration), so each tier gets its own
/// material: Cavity_Material06-07 r=3 (Ultra), Cavity_Material_Good r=1,
/// Cavity_Material_Toaster r=0.5.
///
/// Nothing here mutates a renderer asset at runtime — an earlier version
/// toggled features on PC_Renderer for every preset, which both corrupted the
/// Ultra renderer while Toaster was selected and left the asset dirty on disk.
///
/// Assign the three URP assets, the three VolumeProfile assets (PP_Ultra,
/// PP_Good, PP_Toaster) and the global Volume in the Inspector, then call
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

    public static GraphicsPresetManager Instance { get; private set; }

    /// <summary>Runs <paramref name="render"/> with the ULTRA pipeline asset active, then puts the player's asset back.
    /// The photo booth uses this so a portrait looks identical on every graphics profile: Good/Toaster differ in HDR,
    /// colour-grading mode, upscaling and renderer features, which made their portraits grey and gritty. Changing
    /// QualitySettings.renderPipeline takes effect immediately for a manual Camera.Render(), so no frame is waited.</summary>
    public static void RenderWithUltraPipeline(System.Action render)
    {
        var gp = Instance;
        if (gp == null || gp.urpUltra == null || gp.CurrentPreset == Preset.Ultra) { render(); return; }
        var prev = QualitySettings.renderPipeline;
        var prevAmbient = RenderSettings.ambientLight;
        QualitySettings.renderPipeline = gp.urpUltra;
        if (DayNightCycle.HasUltraAmbient) RenderSettings.ambientLight = DayNightCycle.UltraAmbient;   // no Good/Toaster boost
        try { render(); }
        finally
        {
            QualitySettings.renderPipeline = prev;
            RenderSettings.ambientLight = prevAmbient;
        }
    }
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

        ShaderWarmup.Run($"preset {preset}");

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
        DayNightCycle.PresetAmbientMultiplier = 1f;
        SetPipeline(urpUltra);
        SetVolume(profileUltra);
        SetSceneBlurVolumes(true);
        RenderSettings.fog = true;

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
        // Measured 2026-10-20: Good averaged 0.28 against Ultra's 0.45 (no backdrop fill lights). 1.4x only reached
        // 0.40 in a verification pass, so 1.55x is what actually matches.
        DayNightCycle.PresetAmbientMultiplier = 1.55f;
        SetPipeline(urpGood);
        SetVolume(profileGood);
        SetSceneBlurVolumes(true);
        RenderSettings.fog = true;

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
        // Same story as Good (no backdrop fill lights): Toaster measured ~0.27 against Ultra's 0.45.
        DayNightCycle.PresetAmbientMultiplier = 1.5f;
        SetPipeline(urpToaster);
        SetVolume(profileToaster);
        SetSceneBlurVolumes(false);   // no depth-of-field blur anywhere on Toaster
        RenderSettings.fog = false;   // no distance haze on Toaster (Tad, 2026-10-20)

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

    // Scene volumes OTHER than the preset's own global volume can carry a DepthOfField (e.g. the
    // yard-backdrop volume). They stack on top of whichever preset profile is active, so the
    // preset's own "DoF off" never reached them — Toaster still looked blurred. Toaster switches
    // every such volume off; Ultra/Good turn them back on.
    private void SetSceneBlurVolumes(bool blurAllowed)
    {
        foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (v == globalVolume || v.sharedProfile == null) continue;
            if (v.name.Contains("PhotoBooth")) continue;   // portrait rig keeps its own look in every preset
            if (v.sharedProfile.TryGet(out DepthOfField dof) && dof.active)
                v.enabled = blurAllowed;
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
