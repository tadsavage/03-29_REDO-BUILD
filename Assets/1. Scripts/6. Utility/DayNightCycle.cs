using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Drives a time-of-day sky from the in-game clock (<see cref="SimulationTimeService"/>).
/// 08:00 game time reads as morning, 20:00 as evening, etc. Because it reads the sim
/// clock, any time-scale / fast-forward (SimulationTimeService.TimeScale = 2/3/5…) speeds
/// the whole sky up automatically — no extra wiring.
///
/// Crossfades the BOXOPHOBIC "Skybox Cubemap Extended Blend" material between its day
/// cubemap (_Tex) and night cubemap (_Tex_Blend) via _CubemapTransition, swings the sun
/// (directional light) across the sky, and shifts sun colour, sky exposure and ambient
/// through dawn → day → dusk → night. Everything is procedural with a handful of tunables —
/// no gradient/curve setup required — so it works the moment it's wired up, and a runtime
/// copy of the material is used so the asset on disk is never modified.
///
/// Setup: drop this on an empty GameObject, assign the "Skybox Cubemap Extended Blend"
/// material to <see cref="skyboxBlendMaterial"/>. The sun is auto-found if left empty.
/// </summary>
[DefaultExecutionOrder(-50)] // after GameContext (-100) has created the TimeService
public class DayNightCycle : MonoBehaviour
{
    [Header("References (auto-found if left empty)")]
    [Tooltip("Main directional light used as the sun. Auto-detects the brightest directional light if unset.")]
    [SerializeField] private Light sun;
    [Tooltip("Optional fill light enabled at night (moonlight). Leave empty to skip.")]
    [SerializeField] private Light moon;
    [Tooltip("The 'Skybox Cubemap Extended Blend' material. A runtime copy is used so the asset on disk is never modified.")]
    [SerializeField] private Material skyboxBlendMaterial;

    [Header("Sun Arc")]
    [Tooltip("Compass heading the sun arc swings along (degrees).")]
    [SerializeField] private float sunYaw = 170f;
    [Tooltip("Game hour the sun crosses the horizon at dawn.")]
    [Range(3f, 9f)] [SerializeField] private float sunriseHour = 6.5f;
    [Tooltip("Game hour the sun crosses the horizon at dusk.")]
    [Range(15f, 21f)] [SerializeField] private float sunsetHour = 19f;
    [Tooltip("Hours of dawn/dusk ramp on each side of sunrise/sunset.")]
    [Range(0.25f, 3f)] [SerializeField] private float twilightHours = 1.25f;

    [Header("Sun Light")]
    [SerializeField] private float maxSunIntensity = 1.25f;
    [SerializeField] private Color noonSunColor = new Color(1f, 0.96f, 0.88f);
    [Tooltip("Warm tint as the sun sits on the horizon (dawn/dusk).")]
    [SerializeField] private Color horizonSunColor = new Color(1f, 0.55f, 0.25f);
    [SerializeField] private float moonIntensity = 0.15f;
    [SerializeField] private Color moonColor = new Color(0.55f, 0.62f, 0.85f);

    [Header("Sky")]
    [Tooltip("Skybox exposure at midday.")]
    [SerializeField] private float dayExposure = 1.1f;
    [Tooltip("Skybox exposure at deep night.")]
    [SerializeField] private float nightExposure = 0.35f;

    [Header("Ambient")]
    [Tooltip("Daytime ambient floor. The cycle uses the BRIGHTER of this and your scene's authored ambient, so day never comes out darker than the lighting you tuned.")]
    [SerializeField] private Color dayAmbient = new Color(0.82f, 0.83f, 0.85f);
    [SerializeField] private Color nightAmbient = new Color(0.10f, 0.13f, 0.22f);
    [Tooltip("Drive RenderSettings ambient by time of day. Disable to leave your existing ambient 100% untouched (sky + sun still animate).")]
    [SerializeField] private bool controlAmbient = true;

    [Header("Global Brightness")]
    [Tooltip("Overall daytime brightness multiplier on BOTH the sun and ambient. 1.0 = neutral; 1.12 ≈ +12%.")]
    [Range(0.5f, 2f)] [SerializeField] private float globalBrightness = 1.12f;

    private SimulationTimeService _time;
    private Material _runtimeSky;
    private AmbientMode _prevAmbientMode;
    // Captured at Start from the scene you authored, so daytime preserves your look.
    private Color _dayAmbientTarget = Color.gray;
    private float _sunDayIntensity = 1.25f;
    private Color _sunDayColor = Color.white;
    private static readonly int TransitionID = Shader.PropertyToID("_CubemapTransition");
    private static readonly int ExposureID = Shader.PropertyToID("_Exposure");

    private void Start()
    {
        var ctx = FindAnyObjectByType<GameContext>();
        if (ctx != null) _time = ctx.TimeService;

        if (sun == null) sun = FindBrightestDirectional();

        // Capture the scene's AUTHORED lighting so daytime preserves the look you tuned —
        // the cycle only dims toward night; it never makes day darker than this.
        if (sun != null) { _sunDayIntensity = sun.intensity; _sunDayColor = sun.color; }
        else             { _sunDayIntensity = maxSunIntensity; _sunDayColor = noonSunColor; }
        Color authoredAmbient = RenderSettings.ambientLight;
        _dayAmbientTarget = Luminance(authoredAmbient) > Luminance(dayAmbient) ? authoredAmbient : dayAmbient;

        // Runtime copy so play-mode tweaks never dirty the shared asset on disk.
        // Prefer the explicitly-assigned material; otherwise drive whatever sky the
        // scene's Lighting settings already use.
        Material source = skyboxBlendMaterial != null ? skyboxBlendMaterial : RenderSettings.skybox;
        if (source != null)
        {
            _runtimeSky = new Material(source);
            RenderSettings.skybox = _runtimeSky;
        }

        if (controlAmbient)
        {
            _prevAmbientMode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = AmbientMode.Flat;
        }

        Apply(CurrentHour());
    }

    private void Update()
    {
        // Read every frame so the sky stays smooth even at high TimeScale.
        Apply(CurrentHour());
    }

    private float CurrentHour()
    {
        if (_time != null) return _time.Hour + _time.Minute / 60f; // 0..24
        return 12f; // editor / no clock → noon
    }

    private void Apply(float hour)
    {
        // ── Sun arc ──────────────────────────────────────────────────────────
        // pitch: hour 6 → 0 (rising on horizon), 12 → 90 (overhead),
        // 18 → 180 (setting), 0/24 → -90 (straight up = midnight, fully down).
        float pitch = (hour / 24f) * 360f - 90f;
        if (sun != null)
            sun.transform.rotation = Quaternion.Euler(pitch, sunYaw, 0f);

        // Sun elevation 0..1 (1 = overhead, 0 = at/under horizon).
        float elevation = Mathf.Clamp01(Mathf.Sin(pitch * Mathf.Deg2Rad));

        // Day strength: 1 in full daylight, 0 in full night, smooth twilight ramp.
        float dayStrength = DayStrength(hour);

        if (sun != null)
        {
            sun.intensity = _sunDayIntensity * dayStrength * globalBrightness;
            sun.color = Color.Lerp(horizonSunColor, _sunDayColor, elevation);
            sun.enabled = dayStrength > 0.001f;
        }

        if (moon != null)
        {
            float nightStrength = 1f - dayStrength;
            moon.intensity = moonIntensity * nightStrength;
            moon.color = moonColor;
            moon.enabled = nightStrength > 0.02f;
        }

        // ── Sky crossfade: 0 = day cubemap (_Tex), 1 = night cubemap (_Tex_Blend) ──
        if (_runtimeSky != null)
        {
            if (_runtimeSky.HasProperty(TransitionID))
                _runtimeSky.SetFloat(TransitionID, 1f - dayStrength);
            if (_runtimeSky.HasProperty(ExposureID))
                _runtimeSky.SetFloat(ExposureID, Mathf.Lerp(nightExposure, dayExposure, dayStrength));
        }

        // ── Ambient ──────────────────────────────────────────────────────────
        if (controlAmbient)
            RenderSettings.ambientLight = Color.Lerp(nightAmbient, _dayAmbientTarget * globalBrightness, dayStrength);
    }

    // 1 in full daylight, 0 in full night, smooth twilight ramp across the horizon.
    private float DayStrength(float hour)
    {
        float tw = twilightHours;
        if (hour <= sunriseHour - tw || hour >= sunsetHour + tw) return 0f;
        if (hour >= sunriseHour + tw && hour <= sunsetHour - tw) return 1f;
        if (hour < sunriseHour + tw) // dawn ramp
            return Mathf.SmoothStep(0f, 1f, (hour - (sunriseHour - tw)) / (2f * tw));
        // dusk ramp
        return Mathf.SmoothStep(1f, 0f, (hour - (sunsetHour - tw)) / (2f * tw));
    }

    private void OnDisable()
    {
        // Restore ambient mode if we changed it (e.g. on play-mode exit).
        if (controlAmbient) RenderSettings.ambientMode = _prevAmbientMode;
    }

    private static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    private static Light FindBrightestDirectional()
    {
        Light best = null;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            if (best == null || l.intensity > best.intensity) best = l;
        }
        return best;
    }
}
