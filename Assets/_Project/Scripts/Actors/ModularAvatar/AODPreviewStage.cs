using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Hidden render rig for the Avatar Object Database's live item preview. Self-bootstraps like
/// DockNumberingService/LaneNamingService (hidden DontDestroyOnLoad object, no scene wiring)
/// rather than needing AODPanel to manage its lifetime.
///
/// Modeled directly on ItemCreatorPanel's working pallet-preview rig (the "we did this for the
/// pallet builder" precedent), NOT on EmployeePhotoBooth's layer-masked approach — the first
/// AODPreviewStage attempt combined a dedicated culling layer (PhotoBoothFX) with lights whose
/// cullingMask was also restricted to it, and rendered solid black with no reproducible cause.
/// ItemCreatorPanel's rig never touches cullingMask at all (stays default/Everything) and isolates
/// purely by placing the whole rig far above the map (y=400) where nothing else exists to render —
/// that's the pattern this file now follows.
///
/// Two independent stages live here: the big interactive "selected part" stage (rotatable via
/// <see cref="Rotate"/>) and a second, physically separate thumbnail stage used to bake the small
/// grid-card renders (<see cref="GetOrCaptureThumbnail"/>) without disturbing whatever's currently
/// shown on the big one.
/// </summary>
public class AODPreviewStage : MonoBehaviour
{
    // Isolation by altitude only, matching ItemCreatorPanel's PreviewRig exactly (0,400,0) — nothing
    // else in the scene lives up here, so a default/"Everything" culling mask still only ever sees
    // this rig's own objects.
    private static readonly Vector3 StageOrigin = new Vector3(0f, 400f, 0f);

    // The thumbnail stage sits far enough away (world-space) that neither camera's frustum can ever
    // see the other stage's contents, despite both using the default/"Everything" culling mask.
    private static readonly Vector3 ThumbStageOffset = new Vector3(60f, 0f, 0f);

    private static AODPreviewStage _instance;

    private Camera _camera;
    private RenderTexture _renderTexture;
    private Transform _stagePivot;
    private GameObject _currentInstance;
    private Transform _rotatePivot;
    private float _yaw;
    private float _pitch;

    // The stage lights are DIRECTIONAL, and a directional light has no position: "isolated by altitude" does nothing for it, so it
    // lights the whole game scene as well. They therefore stay OFF except while the stage is actually rendering (Tad, 2026-10-10:
    // the scene went washed-out the moment the AOD was closed, every time, because these stayed on for the rest of the session).
    private readonly List<Light> _stageLights = new();
    private readonly List<Light> _thumbLights = new();

    private static void SetLights(List<Light> lights, bool on)
    {
        foreach (var l in lights) if (l != null) l.enabled = on;
    }

    private Transform _thumbStagePivot;
    private Camera _thumbCamera;
    private RenderTexture _thumbRenderTexture;
    private static readonly Dictionary<string, Texture2D> _thumbCache = new();

    public static RenderTexture Texture => Instance._renderTexture;

    private static AODPreviewStage Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[AODPreviewStage]");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<AODPreviewStage>();
            _instance.Setup();
            return _instance;
        }
    }

    private void Setup()
    {
        _stagePivot = new GameObject("StagePivot").transform;
        _stagePivot.SetParent(transform, false);
        _stagePivot.position = StageOrigin;

        _stageLights.AddRange(BuildLightRig(_stagePivot, StageOrigin));
        BuildBackdrop();

        _renderTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) { name = "AODPreviewRT" };
        _renderTexture.Create();

        var camGO = new GameObject("AODPreviewCamera");
        camGO.transform.SetParent(transform, false);
        _camera = camGO.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f); // matches the AOD panel's navy chrome
        // cullingMask deliberately left at default (-1, Everything) — see class doc comment.
        _camera.targetTexture = _renderTexture;
        _camera.fieldOfView = 30f;
        _camera.nearClipPlane = 0.05f;
        _camera.farClipPlane = 20f;
        _camera.allowMSAA = true;
        // A bare AddComponent<Camera>() doesn't get a UniversalAdditionalCameraData attached under
        // URP unless something touches this extension method — without it URP never recognizes the
        // camera as a valid Base camera and it renders nothing into its target texture at all
        // (confirmed live: GetComponent<UniversalAdditionalCameraData>() was null and the RT read
        // back pure (0,0,0), not even the SolidColor clear color). ItemCreatorPanel's working rig
        // calls this same accessor (for unrelated post-processing settings) which is why it "just
        // worked" there and not here originally.
        var camData = _camera.GetUniversalAdditionalCameraData();
        // Post-processing is on ONLY to pull the saturation down (Tad, 2026-10-09: colors looked "nuclear"). The grade comes from
        // a LOCAL volume that sits around this stage (box collider, high up at y=400) and only this camera's layer mask can see,
        // so no scene volume (bloom etc.) leaks in and the game's own cameras are never graded.
        camData.renderPostProcessing = true;
        camData.volumeLayerMask = 1 << PreviewVolumeLayer;
        BuildPreviewGradeVolume();
        _camera.enabled = false;   // enabled by Show*, disabled again by Clear
        FramePivotDefault();
    }

    private const int PreviewVolumeLayer = 31;       // only used as a volume mask, nothing is rendered on it
    private const float PreviewSaturation = -30f;    // URP Color Adjustments saturation, -100..100 (0 = unchanged); raise toward 0 for more color

    private void BuildPreviewGradeVolume()
    {
        var go = new GameObject("PreviewGradeVolume") { layer = PreviewVolumeLayer };
        go.transform.SetParent(transform, false);
        go.transform.position = StageOrigin;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(20f, 20f, 20f);

        var vol = go.AddComponent<UnityEngine.Rendering.Volume>();
        vol.isGlobal = false;
        vol.priority = 100f;
        var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        var ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.Override(PreviewSaturation);
        vol.sharedProfile = profile;
    }

    private void FramePivotDefault()
    {
        _camera.transform.position = StageOrigin + new Vector3(0f, 1.0f, 2.2f);
        _camera.transform.LookAt(StageOrigin + Vector3.up * 0.9f);
    }

    private static List<Light> BuildLightRig(Transform parent, Vector3 origin)
    {
        var rig = new List<Light>();
        // Simple two-point light rig, unmasked (default layer, default culling mask) — matches
        // ItemCreatorPanel/EmployeePhotoBooth, neither of which restricts light cullingMask either.
        var keyLightGO = new GameObject("KeyLight");
        keyLightGO.transform.SetParent(parent, false);
        keyLightGO.transform.position = origin + new Vector3(1.2f, 2f, -1.5f);
        keyLightGO.transform.LookAt(origin + Vector3.up);
        var keyLight = keyLightGO.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.4f;   // was 1.1; 1.8 over-saturated the colors, so backed off (Tad, 2026-10-09)
        keyLight.enabled = false;    // see _stageLights: only on while rendering
        rig.Add(keyLight);

        var fillLightGO = new GameObject("FillLight");
        fillLightGO.transform.SetParent(parent, false);
        fillLightGO.transform.position = origin + new Vector3(-1.5f, 1f, 1.5f);
        fillLightGO.transform.LookAt(origin + Vector3.up * 0.8f);
        var fillLight = fillLightGO.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 0.7f;   // was 0.45; 0.9 was too much
        fillLight.enabled = false;
        rig.Add(fillLight);
        return rig;
    }

    // Same camera-to-target axis FrameOn() dynamically frames along — reused here so the static
    // backdrop always sits "behind" whatever part is currently on stage, regardless of that part's
    // own size (a hairpiece and a full body frame at very different distances, but both look toward
    // this same direction).
    private static readonly Vector3 CameraApproachDir = new Vector3(0.35f, 0.22f, 1f).normalized;

    /// <summary>Physical background wall, NOT a camera clearFlags color — this project's URP setup
    /// was independently confirmed (live) to never apply CameraClearFlags.SolidColor/backgroundColor
    /// for this camera (corner pixels stayed near-black even after setting backgroundColor to solid
    /// red), matching a documented precedent in ItemCreatorPanel.BuildBackdrop() for the exact same
    /// symptom. A physical, lit/emissive Cube behind the stage is the fix that's already proven to
    /// work in this codebase — same recipe: Cube (not Quad, no wrong-facing backface to worry about),
    /// built from the pipeline's own default material (a raw Shader.Find lookup skips ShaderGUI's
    /// keyword setup and renders invisible), plus a low emissive glow as a belt-and-suspenders so it
    /// reads regardless of whatever is crushing regular lit materials to black elsewhere in this rig.</summary>
    private void BuildBackdrop()
    {
        var backdropColor = new Color(0.08f, 0.10f, 0.14f, 1f); // matches the AOD panel's navy chrome

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "PreviewBackdrop";
        Object.Destroy(wall.GetComponent<Collider>());
        wall.transform.SetParent(_stagePivot, false);
        // Far enough behind the stage center (opposite the camera's approach direction) to stay
        // out of frame for a tiny part and still fill it for a full body; big enough in scale to
        // cover both extremes without needing to move per part.
        wall.transform.localPosition = Vector3.up * 0.9f - CameraApproachDir * 6f;
        wall.transform.localRotation = Quaternion.LookRotation(CameraApproachDir);
        wall.transform.localScale = new Vector3(14f, 14f, 0.2f);

        // UNLIT in exactly the preview frame's own navy (AODPanel._previewFrame uses the same color), so
        // the square render sits invisibly inside the now taller-than-wide frame instead of showing as a
        // lighter band. (The lit wall picked up the stage lights and read brighter than the frame.)
        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        var wallMat = unlitShader != null
            ? new Material(unlitShader)
            : new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial);
        // .gamma: the RT is read back by UI Toolkit WITHOUT sRGB encoding, so a linear-space project shows an
        // unlit color too dark (measured: navy 0.08 came out near-black). Pre-encoding gives the intended value.
        var wallShown = unlitShader != null ? backdropColor.gamma : backdropColor;
        wallMat.color = wallShown;
        if (unlitShader != null && wallMat.HasProperty("_BaseColor")) wallMat.SetColor("_BaseColor", wallShown);
        if (wallMat.HasProperty("_Smoothness")) wallMat.SetFloat("_Smoothness", 0.2f);
        if (wallMat.HasProperty("_Metallic")) wallMat.SetFloat("_Metallic", 0f);
        if (unlitShader == null && wallMat.HasProperty("_EmissionColor"))
        {
            wallMat.EnableKeyword("_EMISSION");
            wallMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            wallMat.SetColor("_EmissionColor", backdropColor * 1.5f);
        }
        var wallRenderer = wall.GetComponent<MeshRenderer>();
        wallRenderer.sharedMaterial = wallMat;
        wallRenderer.receiveShadows = false;
        wallRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Instantiates a single part's source prefab in isolation on the preview stage, hiding
    /// every sibling "part" mesh in that same source file so only the requested one is visible — a
    /// source FBX/prefab can carry many gender_slot_variant meshes on one shared root (e.g. every
    /// hardhat color lives in the same prefab), and this shows exactly one at a time. Frames the
    /// camera on the target renderer's own bounds so a tiny prop and a full body both fill the frame
    /// reasonably. Call <see cref="Clear"/> when the preview is no longer needed (e.g. panel closed).</summary>
    public static void ShowPart(AvatarPartLibrary lib, IAvatarPart part)
    {
        var stage = Instance;
        stage.ClearInternal();
        stage._camera.enabled = true;   // only renders while something is on the stage (see Clear)
        SetLights(stage._stageLights, true);
        if (lib == null || part == null) return;

        var prefab = lib.PrefabFor(part);
        if (prefab == null) return;

        // ROTATION PIVOT = the part's own visual center. The pivot is created empty; the part is instantiated
        // under the stage (NOT under the pivot), its bounds are measured, the pivot is moved to that center,
        // and ONLY THEN is the part parented under it with worldPositionStays:true. (The old order created the
        // part under the pivot and then moved the pivot, which dragged the part along with it — leaving the
        // pivot a full "center offset" away from the mesh, so rotating orbited a point in empty space. That
        // was the "head/hair/neck anchor to the world origin, messy rotation" problem.)
        var pivotGO = new GameObject("RotatePivot");
        pivotGO.transform.SetParent(stage._stagePivot, false);
        stage._rotatePivot = pivotGO.transform;
        stage._yaw = 0f;
        stage._pitch = 0f;

        var instance = Object.Instantiate(prefab, stage._stagePivot);
        instance.name = "Preview_" + part.ObjectName;
        stage._currentInstance = instance;

        // Unity's Instantiate() (and our own "Preview_" rename above) changes the CLONED root
        // object's name away from the source prefab's — for a single-mesh-root prefab (the mesh
        // sits directly on the prefab's own root, no children) that root IS the node we're looking
        // for, so an exact `t.name == part.ObjectName` match against the live instance would never
        // succeed. Strip both wrappers before comparing (see CleanInstanceName).
        Transform target = stage.FindTargetMesh(instance, part.ObjectName);
        if (target == null)
        {
            Debug.LogWarning($"[AODPreviewStage] Could not find '{part.ObjectName}' inside its own source prefab — nothing to preview.");
            instance.transform.SetParent(stage._rotatePivot, true);
            return;
        }

        var renderer = target.GetComponent<Renderer>();

        // Hands/gloves ship as one mesh holding BOTH hands across a T-pose span — the big preview showed
        // two tiny specks. Same single-item treatment as the grid thumbnail (Tad, 2026-10-01).
        if (IsGlovePart(part))
        {
            var single = BuildSingleGlove(target, null);   // world-space mesh on an identity transform
            if (single != null)
            {
                if (renderer != null) renderer.enabled = false;
                Bounds gb = single.GetComponent<Renderer>().bounds;
                stage._rotatePivot.position = gb.center;
                single.transform.SetParent(stage._rotatePivot, true);
                instance.transform.SetParent(stage._rotatePivot, true);
                stage.FitCameraTight(stage._camera, gb, 0.8f);   // 0.8 leaves room for the glove to tumble without clipping
                return;
            }
        }

        Vector3 center = renderer != null ? renderer.bounds.center : target.position;
        stage._rotatePivot.position = center;
        instance.transform.SetParent(stage._rotatePivot, true);
        stage.FrameOn(target);
    }

    private Transform FindTargetMesh(GameObject instance, string objectName)
    {
        Transform target = null;
        foreach (var t in instance.GetComponentsInChildren<Transform>(true))
        {
            bool hasMesh = t.GetComponent<MeshFilter>() != null || t.GetComponent<SkinnedMeshRenderer>() != null;
            if (!hasMesh) continue;

            bool isTarget = CleanInstanceName(t.name) == objectName;
            if (isTarget) target = t;
            else if (ParsesAsPart(t.name)) t.gameObject.SetActive(false); // hide every OTHER part, keep the armature/empties as-is
        }
        return target;
    }

    private void FrameOn(Transform target)
    {
        var renderer = target.GetComponent<Renderer>();
        Bounds bounds = renderer != null ? renderer.bounds : new Bounds(target.position, Vector3.one * 0.3f);
        FrameOnBounds(bounds);
    }

    private void FrameOnBounds(Bounds bounds, float zoom = 1f)
    {
        float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
        float distance = radius / Mathf.Sin(Mathf.Deg2Rad * (_camera.fieldOfView * 0.5f)) * 1.15f / zoom;

        _camera.transform.position = bounds.center + CameraApproachDir * distance;
        _camera.transform.LookAt(bounds.center);
    }

    /// <summary>Shows a fully-assembled avatar instance (from ModularAvatarAssembler.Build) as a
    /// whole, rather than isolating one part — used by the AOD's "Pimp My Employee" editor to
    /// preview the complete look as slots are swapped. Unlike <see cref="ShowPart"/>, this ADOPTS
    /// an already-built GameObject rather than instantiating from a prefab reference (the caller
    /// owns the Build() call, since it needs the same instance to also read back per-slot
    /// picks) — ownership transfers to the stage, which destroys it on the next Show*/Clear call.</summary>
    public static void ShowAssembledInstance(GameObject instance)
    {
        var stage = Instance;
        stage.ClearInternal();
        stage._camera.enabled = true;
        SetLights(stage._stageLights, true);
        if (instance == null) return;

        var pivotGO = new GameObject("RotatePivot");
        pivotGO.transform.SetParent(stage._stagePivot, false);
        stage._rotatePivot = pivotGO.transform;
        stage._yaw = 0f;
        stage._pitch = 0f;

        // Land the avatar at the stage's world position first (worldPositionStays:false — Build()
        // instantiates with no parent, at wherever the prefab's own identity transform put it,
        // usually real world origin, nowhere near this stage).
        instance.transform.SetParent(stage._rotatePivot, worldPositionStays: false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        stage._currentInstance = instance;

        var renderers = instance.GetComponentsInChildren<Renderer>();
        Bounds bounds;
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        }
        else
        {
            bounds = new Bounds(instance.transform.position, Vector3.one);
        }

        // Re-center the ROTATE PIVOT on the avatar's combined visual bounds (a humanoid's own root
        // transform usually sits at the feet, which would make it visibly orbit around the floor
        // instead of spinning in place) — move the pivot to the bounds center and compensate the
        // instance by the same delta so nothing visually jumps. Valid because the pivot carries no
        // rotation yet at this point, so world-space and local-space deltas are identical.
        Vector3 delta = bounds.center - stage._rotatePivot.position;
        stage._rotatePivot.position += delta;
        instance.transform.position -= delta;

        stage.FrameOnBounds(bounds, 1.5f);   // full avatar shown 50% bigger (Tad, 2026-10-09)
    }

    /// <summary>Spins the currently-shown part in place around the vertical (Y) axis, through its OWN CENTER
    /// (the rotation pivot sits on the part's visual center - see ShowPart). X tilt was tried and removed
    /// 2026-10-01 per Tad, so <paramref name="pitchDeltaDeg"/> is accepted for compatibility but ignored.
    /// No-op if nothing is currently shown.</summary>
    public static void Rotate(float yawDeltaDeg, float pitchDeltaDeg = 0f)
    {
        var stage = Instance;
        if (stage._rotatePivot == null) return;
        stage._rotatePivot.rotation = Quaternion.AngleAxis(yawDeltaDeg, Vector3.up) * stage._rotatePivot.rotation;
    }

    /// <summary>Removes whatever's currently on the stage. Safe to call even if nothing is showing.</summary>
    public static void Clear()
    {
        if (_instance == null) return;
        _instance.ClearInternal();
        // Nothing to draw: stop rendering the 1024x1024 preview target every frame while the AOD is closed.
        if (_instance._camera != null) _instance._camera.enabled = false;
        SetLights(_instance._stageLights, false);   // directional: would otherwise keep lighting the whole game scene
    }

    private void ClearInternal()
    {
        if (_rotatePivot != null) Object.Destroy(_rotatePivot.gameObject);
        _rotatePivot = null;
        _currentInstance = null;
        FramePivotDefault();
    }

    // ── Grid-card thumbnails — a second, independent stage so baking a thumbnail never disturbs
    // whatever the player currently has open (and rotated) in the big interactive preview. ──

    private void SetupThumbStage()
    {
        _thumbStagePivot = new GameObject("ThumbStagePivot").transform;
        _thumbStagePivot.SetParent(transform, false);
        _thumbStagePivot.position = StageOrigin + ThumbStageOffset;

        _thumbLights.AddRange(BuildLightRig(_thumbStagePivot, _thumbStagePivot.position));

        _thumbRenderTexture = new RenderTexture(320, 320, 16, RenderTextureFormat.ARGB32) { name = "AODThumbRT" }; // 2x (was 160) — cards are drawn at double size
        _thumbRenderTexture.Create();

        var camGO = new GameObject("AODThumbCamera");
        camGO.transform.SetParent(transform, false);
        _thumbCamera = camGO.AddComponent<Camera>();
        _thumbCamera.clearFlags = CameraClearFlags.SolidColor;
        _thumbCamera.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
        _thumbCamera.targetTexture = _thumbRenderTexture;
        _thumbCamera.fieldOfView = 30f;
        _thumbCamera.nearClipPlane = 0.05f;
        _thumbCamera.farClipPlane = 20f;
        var camData = _thumbCamera.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = false;
        BuildThumbBackdrop();
    }

    // Light, slightly grayish avocado green (white mixed in per Tad, 2026-10-01) behind every grid thumbnail (Tad, 2026-10-01 — they were on black). Like the big
    // preview's navy wall this is a PHYSICAL cube, not a camera clear color (clear colors never apply to
    // these cameras in this URP setup). Unlit so the stage lights can't shift the color; falls back to the
    // default lit material + emission if the Unlit shader isn't found.
    private static readonly Color ThumbBackdropColor = new Color(0.82f, 0.87f, 0.69f, 1f);

    private void BuildThumbBackdrop()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "ThumbBackdrop";
        Object.Destroy(wall.GetComponent<Collider>());
        wall.transform.SetParent(_thumbStagePivot, false);
        wall.transform.localPosition = Vector3.up * 0.9f - CameraApproachDir * 6f;
        wall.transform.localRotation = Quaternion.LookRotation(CameraApproachDir);
        wall.transform.localScale = new Vector3(14f, 14f, 0.2f);

        Material mat;
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit != null)
        {
            mat = new Material(unlit);
            // .gamma: same RT read-back issue as the preview wall (see BuildBackdrop) - without it this green
            // displayed noticeably darker/more saturated than the value written below.
            var shown = ThumbBackdropColor.gamma;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", shown);
            mat.color = shown;
        }
        else
        {
            mat = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial) { color = ThumbBackdropColor };
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", ThumbBackdropColor);
            }
        }
        var r = wall.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.receiveShadows = false;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Returns a cached thumbnail for this part, baking one on first request. The bake is a
    /// synchronous instantiate→render→readback→destroy cycle on the separate thumb stage — cheap
    /// enough per-call, and cached forever (per objectName) until <see cref="ClearThumbnailCache"/>
    /// is called (e.g. after a folder rescan changes what a part looks like).</summary>
    public static Texture2D GetOrCaptureThumbnail(AvatarPartLibrary lib, IAvatarPart part)
    {
        if (lib == null || part == null) return null;
        if (_thumbCache.TryGetValue(part.ObjectName, out var cached) && cached != null) return cached;

        var stage = Instance;
        if (stage._thumbCamera == null)
        {
            stage.SetupThumbStage();
            // Warm-up render: the very first Render() of a freshly-created URP camera drew the backdrop black
            // (the first card in the grid kept a black background while every later one was green).
            SetLights(stage._thumbLights, true);
            stage._thumbCamera.Render();
            SetLights(stage._thumbLights, false);
        }

        var prefab = lib.PrefabFor(part);
        if (prefab == null) return null;

        var instance = Object.Instantiate(prefab, stage._thumbStagePivot);
        Transform target = stage.FindTargetMesh(instance, part.ObjectName);
        if (target == null)
        {
            Object.DestroyImmediate(instance);
            return null;
        }

        var renderer = target.GetComponent<Renderer>();
        Bounds bounds = renderer != null ? renderer.bounds : new Bounds(target.position, Vector3.one * 0.3f);

        Mesh singleGloveMesh = null;
        GameObject single = null;
        if (IsGlovePart(part))
        {
            // Gloves ship as ONE mesh holding the pair — a thumbnail of both is two tiny blobs in a
            // mostly-empty frame. Replace the pair with just one glove and fit the camera TIGHT to it so
            // it fills the card (Tad, 2026-10-01).
            // Built in world space on an identity-transform object (NOT parented under `instance`, whose
            // 0.01 root scale would shrink it again) — so it's destroyed explicitly below.
            single = BuildSingleGlove(target, null);
            if (single != null)
            {
                singleGloveMesh = single.GetComponent<MeshFilter>().sharedMesh;
                if (renderer != null) renderer.enabled = false;
                bounds = single.GetComponent<Renderer>().bounds;
                stage.FitCameraTight(stage._thumbCamera, bounds, 0.96f);
            }
            else
            {
                stage.FrameCameraLoose(stage._thumbCamera, bounds);
            }
        }
        else
        {
            stage.FrameCameraLoose(stage._thumbCamera, bounds);
        }

        SetLights(stage._thumbLights, true);
        stage._thumbCamera.Render();
        SetLights(stage._thumbLights, false);

        RenderTexture.active = stage._thumbRenderTexture;
        var tex = new Texture2D(stage._thumbRenderTexture.width, stage._thumbRenderTexture.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, stage._thumbRenderTexture.width, stage._thumbRenderTexture.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // DestroyImmediate, not Destroy: the grid bakes ALL its thumbnails inside one frame, and a deferred
        // Destroy leaves each finished part alive on this shared stage until end of frame — so every later
        // thumbnail also photographed the leftovers of the ones before it (orange arm strips inside the
        // glove cards, ghost shapes in the headphone card). Found 2026-10-01 by looking at the grid.
        Object.DestroyImmediate(instance);
        if (single != null) Object.DestroyImmediate(single);
        if (singleGloveMesh != null) Object.DestroyImmediate(singleGloveMesh);
        _thumbCache[part.ObjectName] = tex;
        return tex;
    }

    // Every "hands" part (gloves AND the bare-hands body part) ships as one mesh holding BOTH hands,
    // spread across a T-pose-wide span — so the whole slot gets the single-item thumbnail treatment.
    private static bool IsGlovePart(IAvatarPart part) => part.Slot == "hands";

    /// <summary>The original, loose framing — a bounding sphere with 15% slack. Fine for a whole body or
    /// a hat; too much empty space for a small flat item, which is why gloves use FitCameraTight.</summary>
    private void FrameCameraLoose(Camera cam, Bounds bounds)
    {
        float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
        float distance = radius / Mathf.Sin(Mathf.Deg2Rad * (cam.fieldOfView * 0.5f)) * 1.15f;
        cam.transform.position = bounds.center + CameraApproachDir * distance;
        cam.transform.LookAt(bounds.center);
    }

    /// <summary>Backs the camera off along the usual approach direction just far enough that all 8
    /// corners of <paramref name="bounds"/> fit inside <paramref name="fill"/> (0-1) of the frame —
    /// binary-searched on the real projection rather than a bounding sphere, so a flat/elongated item
    /// fills the frame instead of floating in it.</summary>
    private void FitCameraTight(Camera cam, Bounds bounds, float fill)
    {
        cam.transform.position = bounds.center + CameraApproachDir * 2f;
        cam.transform.LookAt(bounds.center);

        var corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
            corners[i] = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));

        float lo = 0.02f, hi = 10f;
        for (int iter = 0; iter < 24; iter++)
        {
            float mid = (lo + hi) * 0.5f;
            cam.transform.position = bounds.center + CameraApproachDir * mid;
            cam.transform.LookAt(bounds.center);

            float worst = 0f;
            foreach (var c in corners)
            {
                Vector3 vp = cam.WorldToViewportPoint(c);
                worst = Mathf.Max(worst, Mathf.Abs(vp.x - 0.5f) * 2f, Mathf.Abs(vp.y - 0.5f) * 2f);
            }
            if (worst > fill) lo = mid; else hi = mid;
        }
        cam.transform.position = bounds.center + CameraApproachDir * hi;
        cam.transform.LookAt(bounds.center);
    }

    /// <summary>Builds a standalone mesh object holding ONE glove of a pair-in-one-mesh glove part, and
    /// parents it under <paramref name="parent"/> in the same world pose as the source. Bakes a skinned
    /// source first (so it's the posed vertices, not the bind-pose-less raw mesh), keeps only the
    /// triangles on one side of the pair's center along the wider of X/Z, and carries across the
    /// source materials per submesh. Returns null if the source isn't a mesh that can be split.</summary>
    private static GameObject BuildSingleGlove(Transform source, Transform parent)
    {
        Mesh baked;
        Material[] mats;
        Matrix4x4 toWorld;   // baked-vertex space -> world space
        var smr = source.GetComponent<SkinnedMeshRenderer>();
        var mf = source.GetComponent<MeshFilter>();
        var mr = source.GetComponent<MeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
        {
            baked = new Mesh();
            // MEASURED live (2026-10-01): BakeMesh(useScale:false) already returns METER-scale vertices for
            // these FBX-derived prefabs (the renderer transform carries a 0.01 lossy scale + a 90 deg X
            // rotation that the bake folds in as scale but NOT as rotation); useScale:true returns the raw
            // centimeter-scale mesh instead. So: bake with false, and apply only position + rotation.
            smr.BakeMesh(baked, false);
            mats = smr.sharedMaterials;
            toWorld = Matrix4x4.TRS(source.position, source.rotation, Vector3.one);
        }
        else if (mf != null && mf.sharedMesh != null && mr != null)
        {
            baked = Object.Instantiate(mf.sharedMesh);
            mats = mr.sharedMaterials;
            toWorld = source.localToWorldMatrix; // full TRS incl. the 0.01 root scale static glove FBXs carry
        }
        else return null;

        var verts = baked.vertices;
        if (verts.Length == 0) return null;

        // The output mesh is built DIRECTLY IN WORLD SPACE and lives on an identity-transform object
        // (no parent). An earlier version parented a re-scaled object under the source instance, whose
        // own root scale of 0.01 then shrank it a second time to ~nothing (the gloves rendered invisible).
        var wmin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var wmax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        var world = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            world[i] = toWorld.MultiplyPoint3x4(verts[i]);
            wmin = Vector3.Min(wmin, world[i]);
            wmax = Vector3.Max(wmax, world[i]);
        }
        // Sanity check: a glove pair should span centimeters-to-a-couple-meters. Anything else means the
        // bake space was misread, and a silent invisible thumbnail is worse than the loose fallback.
        Vector3 size = wmax - wmin;
        if (size.magnitude < 0.01f || size.magnitude > 20f) { Object.DestroyImmediate(baked); return null; }

        // Which axis is the pair spread along (hands go left/right along X in a T/A-pose, but don't assume).
        int axis = (size.x >= size.z) ? 0 : 2;
        float mid = (wmin[axis] + wmax[axis]) * 0.5f;

        // Keep only the triangles on the "low" side of the pair, and COMPACT the vertex arrays down to the
        // vertices those triangles actually use. Compaction matters: Mesh bounds are computed from EVERY
        // vertex, referenced or not, so leaving the other glove's vertices in the buffer made the "single"
        // glove's bounds still span both hands and the tight camera fit framed the empty middle.
        var bakedNormals = baked.normals;
        var bakedUv = baked.uv;
        bool hasN = bakedNormals != null && bakedNormals.Length == verts.Length;
        bool hasUv = bakedUv != null && bakedUv.Length == verts.Length;

        var remap = new int[verts.Length];
        for (int i = 0; i < remap.Length; i++) remap[i] = -1;
        var newVerts = new List<Vector3>();
        var newNormals = new List<Vector3>();
        var newUvs = new List<Vector2>();
        var subTris = new List<List<int>>();
        int kept = 0;
        for (int sIdx = 0; sIdx < baked.subMeshCount; sIdx++)
        {
            var tris = baked.GetTriangles(sIdx);
            var keep = new List<int>(tris.Length / 2);
            for (int t = 0; t < tris.Length; t += 3)
            {
                float c = (world[tris[t]][axis] + world[tris[t + 1]][axis] + world[tris[t + 2]][axis]) / 3f;
                if (c >= mid) continue;
                for (int k = 0; k < 3; k++)
                {
                    int old = tris[t + k];
                    if (remap[old] < 0)
                    {
                        remap[old] = newVerts.Count;
                        newVerts.Add(world[old]);
                        if (hasN) newNormals.Add(toWorld.MultiplyVector(bakedNormals[old]).normalized);
                        if (hasUv) newUvs.Add(bakedUv[old]);
                    }
                    keep.Add(remap[old]);
                }
            }
            subTris.Add(keep);
            kept += keep.Count;
        }
        var indexFormat = baked.indexFormat;
        Object.DestroyImmediate(baked);
        if (kept == 0) return null;

        var outMesh = new Mesh { name = "SingleGlove", indexFormat = indexFormat };
        outMesh.SetVertices(newVerts);
        if (hasN) outMesh.SetNormals(newNormals);
        if (hasUv) outMesh.SetUVs(0, newUvs);
        outMesh.subMeshCount = subTris.Count;
        for (int sIdx = 0; sIdx < subTris.Count; sIdx++) outMesh.SetTriangles(subTris[sIdx], sIdx);
        outMesh.RecalculateBounds();

        var go = new GameObject("SingleGlove");
        // Identity transform, vertices already in world space. `parent` (null = scene root) is only
        // honored if a caller wants it, and must be unscaled.
        if (parent != null) go.transform.SetParent(parent, worldPositionStays: true);
        go.AddComponent<MeshFilter>().sharedMesh = outMesh;
        var outRenderer = go.AddComponent<MeshRenderer>();
        outRenderer.sharedMaterials = mats;
        return go;
    }

    /// <summary>Drops every cached thumbnail so the next grid Refresh re-bakes from scratch — call
    /// after a folder rescan, since a re-exported part can change appearance under the same name.</summary>
    public static void ClearThumbnailCache()
    {
        foreach (var kv in _thumbCache)
            if (kv.Value != null) Object.Destroy(kv.Value);
        _thumbCache.Clear();
    }

    /// <summary>Strips Unity's automatic "(Clone)" suffix and our own "Preview_" prefix so a live
    /// instance's (possibly renamed) transform name can be compared against the source part's
    /// original <see cref="AvatarPartLibrary.Part.objectName"/>.</summary>
    private static string CleanInstanceName(string raw)
    {
        string s = raw;
        if (s.StartsWith("Preview_")) s = s.Substring("Preview_".Length);
        if (s.EndsWith("(Clone)")) s = s.Substring(0, s.Length - "(Clone)".Length);
        return s;
    }

    // Same gender_slot_variant convention check used by ModularAvatarImporter/ModularAvatarAssembler.
    private static bool ParsesAsPart(string name)
    {
        var seg = name.Split('_');
        if (seg.Length < 3) return false;
        string g = seg[0].ToLower();
        return g == "male" || g == "female" || g == "man" || g == "woman" || g == "neutral";
    }
}
