using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The "diorama on a table" look: the built world floats as an island over a big dark slate stage plane,
/// lit by a soft overhead light, with the camera background and fog matched to the stage so the edges of
/// the view fade into it (Tad's mockup, 2026-10-22).
///
/// Runtime-only and self-contained: created on demand by <see cref="GraphicsPresetManager"/> (no scene
/// wiring). It hides the old concrete YardGroundPlane's RENDERER but keeps its collider, because placement
/// raycasts use it as the ground surface.
///
/// The overhead light is a shadowless directional pointing almost straight down. URP only gives the main
/// light (the DayNightCycle sun) shadows, so the sun still casts the shadows; this light just lifts the tops
/// of everything evenly like a studio softbox. Keep it modest or it washes the sun's shadows out.
/// </summary>
public class DioramaStage : MonoBehaviour
{
    public static DioramaStage Instance { get; private set; }

    // Stage sits just under the bottom of a 2.65 m ground block (-1.6) so the island reads as resting on it.
    const float StageY = -1.7f;
    const float StageSize = 1400f;
    // Albedo looks much lighter than it renders: linear lighting + ACES put 0.62 at ~0.25 on screen (the mockup slate).
    static readonly Color StageColour = new Color(0.62f, 0.67f, 0.72f);
    static readonly Color BackdropColour = new Color(0.30f, 0.33f, 0.37f);   // camera background + fog

    struct Look
    {
        public float overheadIntensity;
        public Color overheadColour;
        public bool fog;
        public float fogDensity;
    }

    static Look LookFor(GraphicsPresetManager.Preset preset)
    {
        switch (preset)
        {
            case GraphicsPresetManager.Preset.Toaster:
                return new Look { overheadIntensity = 0.20f, overheadColour = Color.white, fog = false, fogDensity = 0f };   // low so the hard shadows read
            case GraphicsPresetManager.Preset.Good:
                return new Look { overheadIntensity = 0.45f, overheadColour = new Color(1f, 0.98f, 0.95f), fog = true, fogDensity = 0.003f };
            default:
                return new Look { overheadIntensity = 0.50f, overheadColour = new Color(1f, 0.98f, 0.95f), fog = true, fogDensity = 0.0035f };
        }
    }

    Light _overhead;
    GameObject _stage;
    Look _look;
    bool _hasLook;

    public static DioramaStage Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[DioramaStage]");
        Instance = go.AddComponent<DioramaStage>();
        Instance.Build();
        return Instance;
    }

    /// <summary>OFF (Tad, 2026-10-23): the full-screen lit stage quad and the second, per-pixel directional light were a
    /// steady cost on every frame. With it off the old yard ground plane, camera background and fog stay as the scene
    /// sets them. Flip to true to bring the studio-diorama look back.</summary>
    public static bool Enabled = false;

    public static void ApplyPreset(GraphicsPresetManager.Preset preset)
    {
        if (!Application.isPlaying || !Enabled) return;
        Ensure().Apply(LookFor(preset));
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Build()
    {
        // Stage plane: a quad is enough (one draw, 2 triangles). No collider - nothing should ever hit it.
        _stage = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _stage.name = "StagePlane";
        Destroy(_stage.GetComponent<Collider>());
        _stage.transform.SetParent(transform, false);
        _stage.transform.position = new Vector3(0f, StageY, 0f);
        _stage.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _stage.transform.localScale = new Vector3(StageSize, StageSize, 1f);
        var mr = _stage.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader) { name = "DioramaStage (runtime)" };
        mat.SetColor("_BaseColor", StageColour);
        mat.SetFloat("_Smoothness", 0.12f);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;

        var lightGo = new GameObject("OverheadLight");
        lightGo.transform.SetParent(transform, false);
        // Almost straight down, tipped slightly toward the default camera so vertical faces still get some light.
        lightGo.transform.rotation = Quaternion.Euler(82f, 30f, 0f);
        _overhead = lightGo.AddComponent<Light>();
        _overhead.type = LightType.Directional;
        _overhead.shadows = LightShadows.None;
        _overhead.renderMode = LightRenderMode.ForcePixel;
    }

    // The preset is applied from GraphicsPresetManager.Awake, which can run before the main camera exists.
    void Start()
    {
        if (_hasLook) Apply(_look);
    }

    void Apply(Look look)
    {
        _look = look;
        _hasLook = true;
        _overhead.intensity = look.overheadIntensity;
        _overhead.color = look.overheadColour;

        var yard = GameObject.Find("PlacementController/YardGroundPlane");
        if (yard != null && yard.TryGetComponent(out MeshRenderer yr)) yr.enabled = false;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackdropColour;
        }

        RenderSettings.fog = look.fog;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = look.fogDensity;
        RenderSettings.fogColor = BackdropColour;
    }
}
