using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The little bright studio the negotiation window looks into: one customer, lit from the front, soft light backdrop,
/// rendered to a RenderTexture that NegotiationPanel shows in its right-hand window.
///
/// Built like AODPreviewStage (far above the scene, isolated by distance, a physical backdrop instead of relying on camera
/// clear colour, UniversalAdditionalCameraData added so URP actually renders the camera) but with POINT lights, not
/// directional ones: a directional light has no position, so it would also light the whole game scene (that bug washed the
/// game out once already). Point lights with a short range cannot reach it. Bright and clean on purpose - the employee
/// portraits are dark and grainy and Tad does not want this to look like them.
///
/// The customer is a modular avatar seeded from the company name, in business-casual (no hardhat, vest, belt or gloves),
/// with its Animator switched OFF: <see cref="NegotiatorGestures"/> poses it from code.
/// </summary>
public class NegotiatorStage : MonoBehaviour
{
    static readonly Vector3 StageOrigin = new Vector3(240f, 470f, 0f);
    static NegotiatorStage _instance;
    // Dark navy, a touch lighter than the window chrome so the figure's edges still read against it.
    static readonly Color BackdropColor = new Color(0.07f, 0.10f, 0.16f, 1f);

    Camera _camera;
    RenderTexture _rt;
    GameObject _avatar;
    NegotiatorGestures _gestures;
    FaceExpressionController _face;
    readonly List<Light> _lights = new();
    string _key;
    FaceMood _faceMood = FaceMood.Neutral;

    public static RenderTexture Texture => Instance._rt;
    public static bool HasCustomer => _instance != null && _instance._avatar != null;

    static NegotiatorStage Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[NegotiatorStage]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NegotiatorStage>();
            _instance.Setup();
            return _instance;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────

    /// <summary>Puts this customer on stage (a repeat call for the same customer keeps the current avatar).</summary>
    public static void Show(string customerKey)
    {
        var s = Instance;
        s.SetActive(true);
        if (s._avatar == null || s._key != customerKey) s.Build(customerKey);
    }

    public static void Hide()
    {
        if (_instance != null) _instance.SetActive(false);
    }

    /// <summary>The DEAL HEAT ratio (score / budget) - drives posture and face.</summary>
    public static void SetHeat(float ratio)
    {
        if (_instance == null || _instance._gestures == null) return;
        _instance._gestures.Heat = ratio;
        var mood = ratio < 0.92f ? FaceMood.Neutral : ratio < 1.12f ? FaceMood.Confusion : FaceMood.Anger;
        _instance.SetFace(mood);
    }

    /// <summary>The customer just agreed: a smile and a wave.</summary>
    public static void Delighted()
    {
        if (_instance == null || _instance._gestures == null) return;
        _instance._gestures.Heat = 0f;
        _instance.SetFace(FaceMood.Neutral);
        _instance._face?.ForceSmile(3f);
        _instance._gestures.BurstWave(2.8f);
    }

    /// <summary>The customer just refused: a flash of fury.</summary>
    public static void Furious()
    {
        if (_instance == null || _instance._gestures == null) return;
        _instance.SetFace(FaceMood.Anger);
        _instance._gestures.BurstAngry(2.8f);
    }

    // ── Setup ────────────────────────────────────────────────────────────

    void Setup()
    {
        _rt = new RenderTexture(480, 600, 24, RenderTextureFormat.ARGB32) { name = "NegotiatorRT" };
        _rt.Create();

        var pivot = new GameObject("Pivot").transform;
        pivot.SetParent(transform, false);
        pivot.position = StageOrigin;

        BuildBackdrop(pivot);
        BuildLights(pivot);

        var camGO = new GameObject("NegotiatorCamera");
        camGO.transform.SetParent(transform, false);
        _camera = camGO.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = BackdropColor;
        _camera.targetTexture = _rt;
        _camera.fieldOfView = 36f;
        _camera.nearClipPlane = 0.05f;
        _camera.farClipPlane = 25f;
        _camera.allowMSAA = true;
        var data = _camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;   // no bloom/grain/vignette: this window must look clean
        camGO.transform.position = StageOrigin + new Vector3(0f, 1.35f, 3.5f);
        camGO.transform.LookAt(StageOrigin + new Vector3(0f, 1.2f, 0f));
        _camera.enabled = false;
    }

    void BuildBackdrop(Transform pivot)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Backdrop";
        Destroy(wall.GetComponent<Collider>());
        wall.transform.SetParent(pivot, false);
        wall.transform.localPosition = new Vector3(0f, 1.2f, -2.2f);
        wall.transform.localScale = new Vector3(14f, 10f, 0.2f);

        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = unlit != null ? new Material(unlit) : new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial);
        // .gamma: the RT is shown by UI Toolkit without sRGB encoding (same fix AODPreviewStage documents).
        var c = BackdropColor.gamma;
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        var r = wall.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.receiveShadows = false;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void BuildLights(Transform pivot)
    {
        // Moody studio: a warm key from front-left, a dim cool fill, and two coloured rim lights behind so the figure separates
        // from the dark backdrop. All POINT lights with a short range (a directional light would light the whole game scene).
        AddLight(pivot, new Vector3(1.8f, 2.4f, 2.4f), 9.0f, 9f, new Color(1f, 0.93f, 0.82f));
        AddLight(pivot, new Vector3(-2.0f, 1.4f, 2.2f), 2.7f, 9f, new Color(0.7f, 0.82f, 1f));
        AddLight(pivot, new Vector3(-1.6f, 2.2f, -1.4f), 7.5f, 8f, new Color(1f, 0.62f, 0.3f));   // orange rim, back left
        AddLight(pivot, new Vector3(1.6f, 2.2f, -1.4f), 7.5f, 8f, new Color(0.35f, 0.6f, 1f));    // blue rim, back right
    }

    void AddLight(Transform pivot, Vector3 offset, float intensity, float range, Color color)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(pivot, false);
        go.transform.localPosition = offset;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.intensity = intensity;
        l.range = range;
        l.color = color;
        l.shadows = LightShadows.None;
        l.enabled = false;
        _lights.Add(l);
    }

    void SetActive(bool on)
    {
        if (_camera != null) _camera.enabled = on;
        foreach (var l in _lights) if (l != null) l.enabled = on;
    }

    // ── The customer ─────────────────────────────────────────────────────

    void Build(string key)
    {
        if (_avatar != null) Destroy(_avatar);
        _avatar = null; _gestures = null; _face = null;
        _key = key;

        // The cast first: a suit-and-tie Polyperfect character. Falls back to a modular avatar if the cast asset is missing/empty.
        var cast = Resources.Load<NegotiatorCast>(NegotiatorCast.ResourcePath);
        var prefab = cast != null ? cast.PickFor(key) : null;
        if (prefab != null)
        {
            var person = Instantiate(prefab, StageOrigin, Quaternion.identity, transform);
            person.name = "Customer";
            foreach (var smr in person.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            var an = person.GetComponentInChildren<Animator>();
            if (an != null) an.enabled = false;   // posed from code
            _face = FaceExpressionController.Attach(person, FaceMood.Neutral);
            if (_face != null) { _face.UseUnscaledTime = true; _face.ForceSmile(2.5f); }
            _gestures = person.AddComponent<NegotiatorGestures>();
            _faceMood = FaceMood.Neutral;
            _avatar = person;
            return;
        }

        var lib = ModularAvatarAssembler.LoadLibrary();
        if (lib == null || lib.PartCount == 0) return;

        uint h = 2166136261u;
        foreach (char c in key ?? "") { h ^= c; h *= 16777619u; }
        bool female = (h & 1u) == 1u;
        string gender = female ? "female" : "male";
        int seed = (int)(h & 0x7FFFFFFF);

        var ov = BusinessCasual(lib, gender, new System.Random(seed));
        var avatar = ModularAvatarAssembler.Build(lib, gender, new System.Random(seed), null, ov, out _);
        if (avatar == null) return;

        avatar.name = "Customer";
        avatar.transform.SetParent(transform, false);
        avatar.transform.position = StageOrigin;
        avatar.transform.rotation = Quaternion.identity;   // faces +Z, toward the camera

        foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
        var anim = avatar.GetComponent<Animator>();
        if (anim != null) anim.enabled = false;   // posed from code

        _face = FaceExpressionController.Attach(avatar, FaceMood.Neutral);
        if (_face != null) { _face.UseUnscaledTime = true; _face.ForceSmile(2.5f); }
        _gestures = avatar.AddComponent<NegotiatorGestures>();
        _faceMood = FaceMood.Neutral;
        _avatar = avatar;
    }

    /// <summary>No safety gear, no belt, no gloves: a shirt and jeans, whatever hair the seed gave.</summary>
    static Dictionary<string, string> BusinessCasual(AvatarPartLibrary lib, string gender, System.Random rng)
    {
        var ov = new Dictionary<string, string>
        {
            ["vest"] = "", ["hat.hardhat"] = "", ["hat.cap"] = "", ["hat.headphones"] = "",
            ["waist"] = "", ["neck"] = "", ["face"] = "", ["glasses"] = "",
        };
        string Pick(string slot, string contains)
        {
            var list = new List<string>();
            foreach (var p in lib.VariantsFor(gender, slot))
                if (p.ObjectName.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) >= 0 && !p.ObjectName.ToUpper().Contains("NSFW")) list.Add(p.ObjectName);
            return list.Count == 0 ? null : list[rng.Next(list.Count)];
        }
        string shirt = Pick("torso", "Tshirt"); if (shirt != null) ov["torso"] = shirt;
        string jeans = Pick("legs", "Jeans"); if (jeans != null) ov["legs"] = jeans;
        string hands = Pick("hands", "Body"); if (hands != null) ov["hands"] = hands;
        return ov;
    }

    void SetFace(FaceMood mood)
    {
        if (_face == null || mood == _faceMood) return;
        _faceMood = mood;
        _face.SetMood(mood);
    }
}
