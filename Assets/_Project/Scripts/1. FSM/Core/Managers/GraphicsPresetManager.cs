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
        /* Toaster */ new Settings { fog = true, fogDensity = 0.006f, ambientMultiplier = 0.8f, textureMipLimit = 0,
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
    [SerializeField] private float minFocus = 1.5f;
    [SerializeField] private float maxFocusDistance = 400f;
    [Tooltip("Focus on whatever the screen-centre ray hits (employee, machine, rack); falls back to the floor plane.")]
    [SerializeField] private bool raycastFocus = true;
    [SerializeField] private LayerMask focusLayers = ~0;
    [Tooltip("When focused closer than this, the blur ramp is compressed so the mid-ground blurs faster.")]
    [SerializeField] private float closeFocusDistance = 4f;
    [Range(0.1f, 1f)] [SerializeField] private float closeRampScale = 0.4f;
    [Header("Sharp zone (Bokeh)")]
    [Tooltip("Metres that stay sharp in FRONT of and BEHIND the focus point, at the reference distance. Smaller = stronger blur everywhere outside the zone. Good matches Ultra (0.6, Tad 2026-11-05).")]
    [SerializeField] private float zoneGoodSharp = 0.6f;
    [SerializeField] private float zoneUltra = 0.6f;
    [Tooltip("Focus distance at which the zone above applies. Farther away (zoomed out) the zone grows, closer (zoomed in) it shrinks.")]
    [SerializeField] private float zoneRefDistance = 8f;
    [Tooltip("Higher = more blur allowed inside the zone edge (blurrier overall). Fine-tune if the zone looks too loose or too tight.")]
    [SerializeField] private float zoneTolerance = 0.0014f;
    [Tooltip("Bokeh only: when focused close (a person, a machine) the aperture is multiplied by this, so foreground and background blur harder.")]
    [Range(0.2f, 1f)] [SerializeField] private float closeAperture = 0.85f;

    // The game camera's Volume Layer Mask is 0 (it ignores scene volumes and sees only the URP asset's own profile), so the
    // focus-locked blur lives on a private global Volume on this layer, and the camera's mask is opened for that layer only.
    const int DofLayer = 29;
    Volume _dofVolume;
    VolumeProfile _dofProfile;
    DepthOfField _dofLive;       // our runtime DoF (focus distance driven here)
    DepthOfField _dofSource;     // the active preset's DoF in its Volume asset (live-read, so Inspector edits show in Play)
    Camera _cam;
    float _focus = 20f;
    readonly RaycastHit[] _hits = new RaycastHit[24];
    int _groundLayer = -1;   // resolved in Awake (NameToLayer is not allowed in initializers)
    const float MaxTriggerSizeSqr = 15f * 15f;   // triggers bigger than this (lanes, zones, foundations) are not focus subjects

    [System.NonSerialized] UniversalRenderPipelineAsset[] _urp;
    [System.NonSerialized] VolumeProfile[] _profiles;
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
        EnsureTables();
        ApplyPreset((Preset)PlayerPrefs.GetInt("GraphicsPreset", (int)Preset.Ultra), notify: false);
    }

    // ApplyPreset can be called before this object's Awake (TopBarSettingsPanel.ApplyStoredSettings during TopBarUI.Init).
    // The arrays used to be null then, the NullReferenceException aborted TopBarUI.Init and the whole top bar came up blank.
    private void EnsureTables()
    {
        if (_groundLayer < 0) _groundLayer = LayerMask.NameToLayer("Ground");
        // Length check too: the Editor's reload serializer hands private arrays back EMPTY, not null (IndexOutOfRange, 2026-11-05).
        if (_urp == null || _urp.Length != 3)           _urp      = new[] { urpUltra, urpGood, urpToaster };
        if (_profiles == null || _profiles.Length != 3) _profiles = new[] { profileUltra, profileGood, profileToaster };
    }

    // notify=false for startup/load restores so the toast doesn't linger before the player does anything.
    public void ApplyPreset(Preset preset, bool notify = true)
    {
        EnsureTables();
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
        _dofSource = null;
        if (_profiles[i] != null && _profiles[i].TryGet(out DepthOfField src) && src.active && (src.mode.value == DepthOfFieldMode.Gaussian || src.mode.value == DepthOfFieldMode.Bokeh)) _dofSource = src;
        EnsureDofVolume();
        if (_dofVolume != null) _dofVolume.enabled = _dofSource != null;
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

    void EnsureDofVolume()
    {
        if (_dofVolume != null) return;
        var go = new GameObject("[PresetDoF]") { hideFlags = HideFlags.DontSave, layer = DofLayer };
        _dofVolume = go.AddComponent<Volume>();
        _dofVolume.isGlobal = true;
        _dofVolume.priority = 200f;
        _dofProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _dofLive = _dofProfile.Add<DepthOfField>(true);
        _dofVolume.sharedProfile = _dofProfile;
        _dofVolume.enabled = false;
    }

    // Keeps the Bokeh focus plane on the floor under the screen centre, smoothly, so zooming/tilting never defocuses the floor.
    // Blur look (Gaussian start / end / max radius) is re-read each frame from the preset's Volume asset, so tweaking it in the Inspector works live.
    private void LateUpdate()
    {
        if (_dofSource == null || _dofLive == null) return;
        if (_cam == null)
        {
            _cam = Camera.main; if (_cam == null) return;
            var data = _cam.GetUniversalAdditionalCameraData();
            if (data != null) data.volumeLayerMask |= 1 << DofLayer;
        }
        var t = _cam.transform;
        Vector3 dir = t.forward;
        float target = maxFocusDistance;
        if (dir.y < -0.001f) target = (floorHeight - t.position.y) / dir.y;   // floor plane under the screen centre
        if (raycastFocus)
        {
            // Pallets, jacks and employees only carry TRIGGER colliders, which a normal raycast ignores (it would pass straight through
            // them to the floor). So collect triggers too, skip ground and big zone volumes, and focus on the nearest real thing.
            int n = Physics.RaycastNonAlloc(t.position, dir, _hits, maxFocusDistance, focusLayers, QueryTriggerInteraction.Collide);
            float best = float.MaxValue; int bestRank = -1;
            for (int h = 0; h < n; h++)
            {
                var col = _hits[h].collider;
                if (col == null) continue;
                if (col.isTrigger && (col.gameObject.layer == _groundLayer || col.bounds.size.sqrMagnitude > MaxTriggerSizeSqr)) continue;
                // People win over machines, machines over everything else, so the driver stays crisp rather than the jack's bounding box in front of them.
                string nm = col.name;
                int rank = nm.StartsWith("Employee_") ? 2 : (nm.Contains("_FULL") || nm.StartsWith("MHE_")) ? 1 : 0;
                if (rank > bestRank || (rank == bestRank && _hits[h].distance < best)) { bestRank = rank; best = _hits[h].distance; }
            }
            if (best < target) target = best;
        }
        target = Mathf.Clamp(target, minFocus, maxFocusDistance);
        _focus = Mathf.Lerp(_focus, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.01f, focusSmoothing)));

        float close = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(closeFocusDistance * 2f, closeFocusDistance * 0.75f, _focus)); // 1 far .. 0 close
        if (_dofSource.mode.value == DepthOfFieldMode.Bokeh)
        {
            // BOKEH blurs both sides of the focus plane: foreground, mid-ground and background all separate. The closer the focus,
            // the lower the f-stop (shallower), so a close-up subject pops harder. Look comes from the preset's Volume asset (live).
            _dofLive.mode.Override(DepthOfFieldMode.Bokeh);
            _dofLive.focusDistance.Override(_focus);
            // Aperture is DERIVED so that a band of +/- zone metres around the focus point stays sharp (thin-lens CoC, 24 mm sensor).
            // The band scales with focus distance, so zooming out widens it and zooming in tightens it. The asset's own Aperture is ignored;
            // Focal Length, blades and sampling still come from the asset.
            float zoneHalf = (CurrentPreset == Preset.Ultra ? zoneUltra : zoneGoodSharp) * Mathf.Clamp(_focus / Mathf.Max(0.1f, zoneRefDistance), 0.5f, 3f);
            float F = Mathf.Max(0.005f, _dofSource.focalLength.value * 0.001f);
            float N = F * F * zoneHalf / (Mathf.Max(0.01f, _focus - F) * (_focus + zoneHalf) * 0.024f * Mathf.Max(0.0001f, zoneTolerance));
            _dofLive.aperture.Override(Mathf.Clamp(N, 1f, 32f));
            _dofLive.focalLength.Override(_dofSource.focalLength.value);
            _dofLive.bladeCount.Override(_dofSource.bladeCount.value);
            _dofLive.bladeCurvature.Override(_dofSource.bladeCurvature.value);
            _dofLive.bladeRotation.Override(_dofSource.bladeRotation.value);
            _dofLive.highQualitySampling.Override(_dofSource.highQualitySampling.value);
        }
        else
        {
            // GAUSSIAN blurs only BEYOND gaussianStart (no near blur, no halo over rack labels).
            float behind = _dofSource.gaussianStart.value;
            float ramp = Mathf.Max(1f, _dofSource.gaussianEnd.value - behind);
            float k = Mathf.Lerp(closeRampScale, 1f, close);
            float start = _focus + behind * Mathf.Lerp(0.35f, 1f, close);
            _dofLive.mode.Override(DepthOfFieldMode.Gaussian);
            _dofLive.gaussianStart.Override(start);
            _dofLive.gaussianEnd.Override(start + ramp * k);
            _dofLive.gaussianMaxRadius.Override(_dofSource.gaussianMaxRadius.value);
            _dofLive.highQualitySampling.Override(_dofSource.highQualitySampling.value);
        }
    }

    private void OnDestroy()
    {
        if (_dofVolume != null) Destroy(_dofVolume.gameObject);
        if (_dofProfile != null) Destroy(_dofProfile);
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
