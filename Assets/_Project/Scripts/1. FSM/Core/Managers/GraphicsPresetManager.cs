using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies one of three graphics presets (Ultra / Good / Toaster == Low).
/// What lives where:
///  - URP asset (Ultra_URP / Good_URP / Toaster_URP): shadows, MSAA, render scale, light limits.
///  - Renderer asset (*_Renderer): cavity passes, ambient occlusion, avatar mask (AvatarsOverSSAO).
///  - Volume profile (*_Volume): bloom, depth of field, vignette.
///  - THIS script's Table: fog, ambient, texture detail, camera AA, probes, gloss, LOD, dust.
/// ApplyPreset swaps QualitySettings.renderPipeline (never mutates the shared assets). Zero allocation after Awake.
/// Ambient is applied through DayNightCycle.PresetAmbientMultiplier because DayNightCycle rewrites ambientLight every frame.
/// Current preset is persisted to PlayerPrefs "GraphicsPreset".
/// </summary>
public class GraphicsPresetManager : MonoBehaviour
{
    public enum Preset { Ultra, Good, Toaster }

    [System.Serializable]
    public struct Settings
    {
        public bool fog;
        public float fogDensity;            // Exponential mode
        public float ambientMultiplier;     // keep equal across presets so brightness never differs
        public int textureMipLimit;         // 0 full, 1 half, 2 quarter
        public AntialiasingMode cameraAA;
        public float reflectionIntensity;
        public float glossScale;            // _PresetGlossScale (warehouse materials)
        public float lodBias;
        public bool realtimeProbes;
        public bool dust;
    }

    static readonly Settings[] Table =
    {
        /* Ultra   */ new Settings { fog = true, fogDensity = 0.006f, ambientMultiplier = 1f, textureMipLimit = 0,
                                     cameraAA = AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                                     reflectionIntensity = 1f, glossScale = 1f, lodBias = 2f,   realtimeProbes = true,  dust = true  },
        /* Good    */ new Settings { fog = true, fogDensity = 0.006f, ambientMultiplier = 1f, textureMipLimit = 0,
                                     cameraAA = AntialiasingMode.FastApproximateAntialiasing,
                                     reflectionIntensity = 1f, glossScale = 1f, lodBias = 1.5f, realtimeProbes = true,  dust = true  },
        /* Toaster */ new Settings { fog = true, fogDensity = 0.006f, ambientMultiplier = 1f, textureMipLimit = 0,
                                     cameraAA = AntialiasingMode.FastApproximateAntialiasing,
                                     reflectionIntensity = 1f, glossScale = 1f, lodBias = 1f,   realtimeProbes = false, dust = false },
    };
    static readonly string[] Labels = { "Ultra", "Good", "Toaster" };
    static readonly int GlossScaleId = Shader.PropertyToID("_PresetGlossScale");

    [Header("Render Pipeline Assets")]
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
    public Preset CurrentPreset { get; private set; } = Preset.Ultra;

    [Header("Tilt-shift focus (Good/Ultra)")]
    [Tooltip("World Y of the warehouse floor. The focus plane is kept on the point where the camera's centre ray meets it.")]
    [SerializeField] private float floorHeight = 1.1f;
    [Tooltip("Seconds-ish smoothing of the focus distance when the camera zooms or tilts (lower = snappier).")]
    [SerializeField] private float focusSmoothing = 0.15f;
    [SerializeField] private float minFocusDistance = 3f;
    [SerializeField] private float maxFocusDistance = 400f;

    DepthOfField _dof;           // the live (instanced) DoF component; null when the preset has no DoF
    Camera _cam;
    float _focus = 20f;

    UniversalRenderPipelineAsset[] _urp;
    VolumeProfile[] _profiles;
    UniversalAdditionalCameraData _camData;

    /// <summary>Runs <paramref name="render"/> with the ULTRA pipeline asset active, then restores the player's asset.
    /// The photo booth uses this so portraits look identical on every profile.</summary>
    public static void RenderWithUltraPipeline(System.Action render)
    {
        var gp = Instance;
        if (gp == null || gp.urpUltra == null || gp.CurrentPreset == Preset.Ultra) { render(); return; }
        var prev = QualitySettings.renderPipeline;
        var prevAmbient = RenderSettings.ambientLight;
        var prevReflection = RenderSettings.reflectionIntensity;
        var prevGloss = Shader.GetGlobalFloat(GlossScaleId);
        Shader.SetGlobalFloat(GlossScaleId, Table[(int)Preset.Ultra].glossScale);
        QualitySettings.renderPipeline = gp.urpUltra;
        if (DayNightCycle.HasUltraAmbient) RenderSettings.ambientLight = DayNightCycle.UltraAmbient;
        RenderSettings.reflectionIntensity = Table[(int)Preset.Ultra].reflectionIntensity;
        try { render(); }
        finally
        {
            QualitySettings.renderPipeline = prev;
            RenderSettings.ambientLight = prevAmbient;
            RenderSettings.reflectionIntensity = prevReflection;
            Shader.SetGlobalFloat(GlossScaleId, prevGloss);
        }
    }

    private void Awake()
    {
        Instance = this;
        _urp      = new[] { urpUltra, urpGood, urpToaster };
        _profiles = new[] { profileUltra, profileGood, profileToaster };
        ApplyPreset((Preset)PlayerPrefs.GetInt("GraphicsPreset", (int)Preset.Ultra), notify: false);
    }

    // notify=false for startup/load restores so the toast doesn't linger before the player does anything.
    public void ApplyPreset(Preset preset, bool notify = true)
    {
        CurrentPreset = preset;
        PlayerPrefs.SetInt("GraphicsPreset", (int)preset);
        int i = (int)preset;
        var s = Table[i];

        // Pipeline asset + post-process profile.
        if (_urp[i] != null) QualitySettings.renderPipeline = _urp[i];
        if (_profiles[i] != null)
        {
            if (globalVolume == null) globalVolume = FindAnyObjectByType<Volume>();
            if (globalVolume != null) globalVolume.sharedProfile = _profiles[i];
        }
        // Instance the profile once per apply so per-frame focus writes never dirty the asset on disk.
        _dof = null;
        if (globalVolume != null && globalVolume.profile != null
            && globalVolume.profile.TryGet(out DepthOfField d) && d.active && d.mode.value == DepthOfFieldMode.Bokeh) _dof = d;
        SetSceneBlurVolumes(false);   // extra scene DoF volumes stay off so the preset's own DoF is the only blur

        // Fog.
        RenderSettings.fog = s.fog;
        if (s.fog) { RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = s.fogDensity; }

        // Ambient (via DayNightCycle), globals, particles.
        DayNightCycle.PresetAmbientMultiplier = s.ambientMultiplier;
        QualitySettings.globalTextureMipmapLimit = s.textureMipLimit;
        QualitySettings.lodBias = s.lodBias;
        QualitySettings.realtimeReflectionProbes = s.realtimeProbes;
        RenderSettings.reflectionIntensity = s.reflectionIntensity;
        Shader.SetGlobalFloat(GlossScaleId, s.glossScale);
        if (s.dust) FXPool.DisabledKeys.Remove("dust"); else FXPool.DisabledKeys.Add("dust");

        // Camera AA (component cached; re-resolved only if lost).
        if (_camData == null)
        {
            var cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (cam != null) _camData = cam.GetUniversalAdditionalCameraData();
        }
        if (_camData != null) _camData.antialiasing = s.cameraAA;

        DioramaStage.ApplyPreset(preset);
        ShaderWarmup.Run($"preset {preset}");
        if (notify) UIToast.Show($"Profile is {Labels[i]}", 2.5f);
    }

    // Keeps the Bokeh focus plane on the floor under the screen centre, smoothly, so zooming/tilting never defocuses the floor.
    private void LateUpdate()
    {
        if (_dof == null) return;
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
        var t = _cam.transform;
        Vector3 dir = t.forward;
        float target = maxFocusDistance;
        if (dir.y < -0.001f) target = (floorHeight - t.position.y) / dir.y;   // distance along the view ray to the floor plane
        target = Mathf.Clamp(target, minFocusDistance, maxFocusDistance);
        _focus = Mathf.Lerp(_focus, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.01f, focusSmoothing)));
        _dof.focusDistance.value = _focus;
    }

    // Scene volumes other than the preset's own can carry a DepthOfField (e.g. the yard backdrop) that stacks on top.
    private void SetSceneBlurVolumes(bool blurAllowed)
    {
        foreach (var v in FindObjectsByType<Volume>(FindObjectsInactive.Include))
        {
            if (v == globalVolume || v.sharedProfile == null) continue;
            if (v.name.Contains("PhotoBooth")) continue;
            if (v.sharedProfile.TryGet(out DepthOfField dof) && dof.active) v.enabled = blurAllowed;
        }
    }
}
