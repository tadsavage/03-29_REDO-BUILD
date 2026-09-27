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

        BuildLightRig(_stagePivot, StageOrigin);
        BuildBackdrop();

        _renderTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { name = "AODPreviewRT" };
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
        camData.renderPostProcessing = false;
        FramePivotDefault();
    }

    private void FramePivotDefault()
    {
        _camera.transform.position = StageOrigin + new Vector3(0f, 1.0f, 2.2f);
        _camera.transform.LookAt(StageOrigin + Vector3.up * 0.9f);
    }

    private static void BuildLightRig(Transform parent, Vector3 origin)
    {
        // Simple two-point light rig, unmasked (default layer, default culling mask) — matches
        // ItemCreatorPanel/EmployeePhotoBooth, neither of which restricts light cullingMask either.
        var keyLightGO = new GameObject("KeyLight");
        keyLightGO.transform.SetParent(parent, false);
        keyLightGO.transform.position = origin + new Vector3(1.2f, 2f, -1.5f);
        keyLightGO.transform.LookAt(origin + Vector3.up);
        var keyLight = keyLightGO.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.1f;

        var fillLightGO = new GameObject("FillLight");
        fillLightGO.transform.SetParent(parent, false);
        fillLightGO.transform.position = origin + new Vector3(-1.5f, 1f, 1.5f);
        fillLightGO.transform.LookAt(origin + Vector3.up * 0.8f);
        var fillLight = fillLightGO.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 0.45f;
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

        var wallMat = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial)
            { color = backdropColor };
        if (wallMat.HasProperty("_Smoothness")) wallMat.SetFloat("_Smoothness", 0.2f);
        if (wallMat.HasProperty("_Metallic")) wallMat.SetFloat("_Metallic", 0f);
        if (wallMat.HasProperty("_EmissionColor"))
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
        if (lib == null || part == null) return;

        var prefab = lib.PrefabFor(part);
        if (prefab == null) return;

        // A pivot centered on the mesh's own bounds, NOT the raw instance — rotating the instance
        // directly would spin it around whatever local origin its author picked (fine for a
        // centered body, but a hat or hairpiece authored off-center would visibly orbit rather than
        // spin in place). Reparented with worldPositionStays:true after FrameOn computes bounds, so
        // the pivot sits exactly on the mesh's visual center regardless of authoring pivot.
        var pivotGO = new GameObject("RotatePivot");
        pivotGO.transform.SetParent(stage._stagePivot, false);
        stage._rotatePivot = pivotGO.transform;
        stage._yaw = 0f;
        stage._pitch = 0f;

        var instance = Object.Instantiate(prefab, stage._rotatePivot);
        instance.name = "Preview_" + part.ObjectName;
        stage._currentInstance = instance;

        // Unity's Instantiate() (and our own "Preview_" rename above) changes the CLONED root
        // object's name away from the source prefab's — for a single-mesh-root prefab (the mesh
        // sits directly on the prefab's own root, no children) that root IS the node we're looking
        // for, so an exact `t.name == part.ObjectName` match against the live instance would never
        // succeed (root reads "Preview_man_hair_regular(Clone)" or similar, never "man_hair_regular").
        // This was the actual root cause of the "always renders black" bug — the target was never
        // found, ShowPart returned before ever calling FrameOn, and the camera sat at its default
        // framing looking at nothing. Strip both wrappers before comparing.
        Transform target = stage.FindTargetMesh(instance, part.ObjectName);
        if (target == null)
        {
            Debug.LogWarning($"[AODPreviewStage] Could not find '{part.ObjectName}' inside its own source prefab — nothing to preview.");
            return;
        }

        // Re-center the pivot on the mesh's actual bounds now that it exists (worldPositionStays
        // keeps the mesh's world transform identical, so nothing visually jumps).
        var renderer = target.GetComponent<Renderer>();
        Vector3 center = renderer != null ? renderer.bounds.center : target.position;
        stage._rotatePivot.SetPositionAndRotation(center, Quaternion.identity);

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

    private void FrameOnBounds(Bounds bounds)
    {
        float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
        float distance = radius / Mathf.Sin(Mathf.Deg2Rad * (_camera.fieldOfView * 0.5f)) * 1.15f;

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

        stage.FrameOnBounds(bounds);
    }

    /// <summary>Spins the currently-shown part in place — horizontal drag yaws (rotate around the
    /// vertical/Y axis), vertical drag pitches (rotate around the horizontal/X axis). Deltas are in
    /// degrees; callers convert pointer-drag pixels to degrees themselves so this stays UI-agnostic.
    /// No-op if nothing is currently shown.</summary>
    public static void Rotate(float yawDeltaDeg, float pitchDeltaDeg)
    {
        var stage = Instance;
        if (stage._rotatePivot == null) return;
        stage._yaw += yawDeltaDeg;
        stage._pitch = Mathf.Clamp(stage._pitch + pitchDeltaDeg, -80f, 80f);
        stage._rotatePivot.localRotation = Quaternion.Euler(stage._pitch, stage._yaw, 0f);
    }

    /// <summary>Removes whatever's currently on the stage. Safe to call even if nothing is showing.</summary>
    public static void Clear()
    {
        if (_instance == null) return;
        _instance.ClearInternal();
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

        BuildLightRig(_thumbStagePivot, _thumbStagePivot.position);

        _thumbRenderTexture = new RenderTexture(160, 160, 16, RenderTextureFormat.ARGB32) { name = "AODThumbRT" };
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
        if (stage._thumbCamera == null) stage.SetupThumbStage();

        var prefab = lib.PrefabFor(part);
        if (prefab == null) return null;

        var instance = Object.Instantiate(prefab, stage._thumbStagePivot);
        Transform target = stage.FindTargetMesh(instance, part.ObjectName);
        if (target == null)
        {
            Object.Destroy(instance);
            return null;
        }

        var renderer = target.GetComponent<Renderer>();
        Bounds bounds = renderer != null ? renderer.bounds : new Bounds(target.position, Vector3.one * 0.3f);
        float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
        float distance = radius / Mathf.Sin(Mathf.Deg2Rad * (stage._thumbCamera.fieldOfView * 0.5f)) * 1.15f;
        stage._thumbCamera.transform.position = bounds.center + CameraApproachDir * distance;
        stage._thumbCamera.transform.LookAt(bounds.center);

        stage._thumbCamera.Render();

        RenderTexture.active = stage._thumbRenderTexture;
        var tex = new Texture2D(stage._thumbRenderTexture.width, stage._thumbRenderTexture.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, stage._thumbRenderTexture.width, stage._thumbRenderTexture.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        Object.Destroy(instance);
        _thumbCache[part.ObjectName] = tex;
        return tex;
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
