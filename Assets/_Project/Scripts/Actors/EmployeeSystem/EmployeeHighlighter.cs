using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Draws a see-through selection outline around ONE employee at a time and makes the
/// camera focus on them — the Sims/SimCity "find my unit" behaviour.
///
/// The outline is built at runtime by cloning the employee's renderers (the body is a skinned
/// FBX) and rendering each clone twice:
///   • a MASK clone  (Hidden/EmployeeOutlineMask) stamps the silhouette into the stencil buffer,
///   • a FILL clone  (Hidden/EmployeeOutlineFill) draws an inverted-hull rim only outside that
///     stencil — so you get a clean outline, not a solid blob.
/// Both use ZTest Always, so the outline shows through walls. Draw order is forced by RENDER
/// QUEUE (mask below fill), which is reliable; relying on multi-pass order within one material
/// is not.
///
/// Fully self-contained: access via EmployeeHighlighter.Instance, which lazily creates the
/// manager GameObject. Materials are created in code from the two Hidden/ shaders — no asset
/// wiring, no Resources lookup. (For a player build, add both shaders to Graphics →
/// Always Included Shaders so Shader.Find resolves them.)
/// </summary>
public class EmployeeHighlighter : MonoBehaviour
{
    private const string MaskShaderName = "Hidden/EmployeeOutlineMask";
    private const string FillShaderName = "Hidden/EmployeeOutlineFill";
    private const string CloneName = "__EmployeeOutlineClone";

    private static EmployeeHighlighter _instance;
    public static EmployeeHighlighter Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<EmployeeHighlighter>();
                if (_instance == null)
                {
                    var go = new GameObject("EmployeeHighlighter");
                    _instance = go.AddComponent<EmployeeHighlighter>();
                }
            }
            return _instance;
        }
    }

    /// <summary>True if a highlighter instance already exists (avoids lazily creating one).</summary>
    public static bool HasInstance => _instance != null;

    [Header("Outline look")]
    // Orange, the same one the trucks' inbound outline uses (TruckOrderColors.Inbound) - Tad, 2026-10-10. Fixed in code on purpose: the old
    // serialized colour/width on this component in Main.unity are no longer read.
    private static Color OutlineColor => TruckOrderColors.Inbound;

    [Tooltip("Outline thickness in SCREEN PIXELS (constant at any zoom). 2-3 = tight.")]
    [SerializeField, Range(0.5f, 8f)] private float _outlinePixels = 2.5f;

    private Material _maskMat;
    private Material _fillMat;

    private EmployeeIdentity _current;
    private Transform _root;   // what is actually outlined / followed: the employee, or the vehicle they are riding

    /// <summary>True while either Shift key is held (the "select + follow" modifier for world clicks).</summary>
    public static bool ShiftHeld => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

    /// <summary>An employee riding a Reach Truck / Dock Stocker / Pallet Jack is outlined and followed as the WHOLE machine
    /// (the rider is parented under it, so the machine's renderers include them); everyone else is outlined as themselves.</summary>
    private static Transform OutlineRoot(EmployeeIdentity identity) =>
        identity.AssignedSlot != null ? identity.AssignedSlot.transform : identity.transform;
    private readonly List<GameObject> _clones = new();
    private FreeLookCamera _camera;

    // Geometry under the floor (the reach truck's mast pokes 2 m below it) must not show through: clip both outline passes below the outlined
    // thing's own pivot height (its feet / its wheels), tracked every frame so it follows docks and ramps.
    private const float ClipBelowPivot = 0.03f;
    private void LateUpdate()
    {
        if (_root == null || _maskMat == null || _fillMat == null) return;
        float y = _root.position.y - ClipBelowPivot;
        _maskMat.SetFloat("_OutlineClipY", y);
        _fillMat.SetFloat("_OutlineClipY", y);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_maskMat != null) Destroy(_maskMat);
        if (_fillMat != null) Destroy(_fillMat);
    }

    private void Update()
    {
        // The highlighted employee may have been destroyed (fired walk-off, despawn). Clones
        // are parented under it, so Unity removes them automatically — just drop our state and
        // stop the camera following a ghost.
        if (_current == null && _clones.Count > 0)
        {
            _clones.Clear();
            if (_camera != null) _camera.SetFollowTarget(null);
        }

        // TAB cancels focus (stop outlining + following) but leaves the info card open, so the
        // player can free-look / orbit (including right-click drag) while still reading the card.
        if (_current != null && Keyboard.current != null && Keyboard.current[Key.Tab].wasPressedThisFrame)
            Clear();
    }

    /// <summary>Outline the employee, recenter the camera on them, AND have the camera follow
    /// them as they move (Sims/SimCity style). Re-calling with the same employee just refocuses.</summary>
    public void FocusAndHighlight(EmployeeIdentity identity)
    {
        if (identity == null) return;

        Highlight(identity);

        if (_camera == null)
            _camera = FindAnyObjectByType<FreeLookCamera>();
        if (_camera != null)
        {
            var follow = OutlineRoot(identity);
            _camera.FocusOn(follow.position);
            _camera.SetFollowTarget(follow);   // track them (or their machine) until the player pans away
        }
    }

    /// <summary>Outline a single employee (replaces any existing highlight).</summary>
    public void Highlight(EmployeeIdentity identity)
    {
        if (identity == null) return;
        var root = OutlineRoot(identity);
        if (_current == identity && _root == root) return;   // already outlined — leave it

        Clear();
        _current = identity;
        _root = root;
        BuildOutline(root);
    }

    /// <summary>Remove the current outline, if any, and stop the camera following.</summary>
    public void Clear()
    {
        foreach (var go in _clones)
            if (go != null) Destroy(go);
        _clones.Clear();
        _current = null;
        _root = null;

        if (_camera == null) _camera = FindAnyObjectByType<FreeLookCamera>();
        if (_camera != null) _camera.SetFollowTarget(null);
    }

    public bool IsHighlighted(EmployeeIdentity identity) => identity != null && _current == identity;

    /// <summary>True while someone (or a machine) is currently outlined / followed.</summary>
    public bool HasHighlight => _current != null;

    // ── Internals ────────────────────────────────────────────────────────────────
    private bool EnsureMaterials()
    {
        if (_maskMat != null && _fillMat != null) return true;

        var maskShader = Shader.Find(MaskShaderName);
        var fillShader = Shader.Find(FillShaderName);
        if (maskShader == null || fillShader == null)
        {
            Debug.LogWarning($"[EmployeeHighlighter] Outline shaders not found " +
                             $"('{MaskShaderName}' / '{FillShaderName}'). Highlight skipped.");
            return false;
        }

        // Mask draws first, fill second — forced by render queue (3000 < 3001), both inside the
        // transparent phase so the depth/stencil buffer is shared between them.
        _maskMat = new Material(maskShader) { renderQueue = 3000, hideFlags = HideFlags.HideAndDontSave };
        _fillMat = new Material(fillShader) { renderQueue = 3001, hideFlags = HideFlags.HideAndDontSave };
        _fillMat.SetColor("_OutlineColor", OutlineColor);
        _fillMat.SetFloat("_OutlinePixels", _outlinePixels);
        return true;
    }

    private void BuildOutline(Transform root)
    {
        if (!EnsureMaterials()) return;

        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: false);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (r is SpriteRenderer) continue;          // skip the 2D portrait billboard
            if (r.name == CloneName) continue;          // never clone our own clones
            // Only what is actually DRAWN: a switched-off renderer (the hidden legacy worker body under the modular avatar, parts a garment
            // hides) used to get a clone too, so the outline followed the wrong body shape (Tad, 2026-10-10: "janky").
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;

            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                CloneSkinned(smr, _maskMat, smr.sharedMesh);
                CloneSkinned(smr, _fillMat, SmoothedOutlineMesh(smr.sharedMesh));
            }
            else if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name != "FootContactShadowQuad")   // not the soft ground-shadow blob
                {
                    CloneStatic(r, mf.sharedMesh, _maskMat);
                    CloneStatic(r, SmoothedOutlineMesh(mf.sharedMesh), _fillMat);
                }
            }
        }
    }

    private static Material[] FillArray(Material mat, int subMeshCount)
    {
        var arr = new Material[Mathf.Max(1, subMeshCount)];
        for (int i = 0; i < arr.Length; i++) arr[i] = mat;
        return arr;
    }

    // These meshes are flat-shaded low-poly: every hard edge has duplicated vertices, each carrying its own face normal. Pushing the hull out along those
    // splits the corners apart (cracks, uneven width). The fill hull therefore uses a copy of the mesh whose normals are AVERAGED over every vertex that
    // shares a position, so the whole hull moves outward together. Bone weights/bind poses are copied with the mesh. Cached per source mesh.
    private static readonly Dictionary<Mesh, Mesh> _smoothedCache = new();

    private static Mesh SmoothedOutlineMesh(Mesh src)
    {
        if (src == null) return null;
        if (_smoothedCache.TryGetValue(src, out var cached) && cached != null) return cached;
        if (!src.isReadable) return src;   // cannot read vertices at runtime: fall back to the plain mesh

        var smooth = Object.Instantiate(src);
        smooth.name = src.name + "_outlineSmooth";
        smooth.hideFlags = HideFlags.HideAndDontSave;
        var verts = src.vertices;
        var normals = src.normals;
        if (normals == null || normals.Length != verts.Length) { _smoothedCache[src] = src; return src; }

        static long Key(Vector3 p) =>
            ((long)Mathf.RoundToInt(p.x * 10000f) * 73856093L) ^ ((long)Mathf.RoundToInt(p.y * 10000f) * 19349663L) ^ ((long)Mathf.RoundToInt(p.z * 10000f) * 83492791L);
        var sums = new Dictionary<long, Vector3>(verts.Length);
        for (int i = 0; i < verts.Length; i++)
        {
            long k = Key(verts[i]);
            sums[k] = sums.TryGetValue(k, out var s) ? s + normals[i] : normals[i];
        }
        var outN = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var n = sums[Key(verts[i])];
            outN[i] = n.sqrMagnitude > 1e-10f ? n.normalized : normals[i];
        }
        smooth.normals = outN;
        _smoothedCache[src] = smooth;
        return smooth;
    }

    private void CloneSkinned(SkinnedMeshRenderer src, Material mat, Mesh mesh)
    {
        var go = new GameObject(CloneName);
        go.layer = src.gameObject.layer;
        go.transform.SetParent(src.transform, worldPositionStays: false);

        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh          = mesh;
        smr.bones               = src.bones;
        smr.rootBone            = src.rootBone;
        smr.localBounds         = src.localBounds;
        smr.quality             = src.quality;           // MUST match the real renderer: Bone1 skinned the clone differently from the visible body, so the outline did not follow it
        smr.updateWhenOffscreen = src.updateWhenOffscreen;
        smr.sharedMaterials     = FillArray(mat, mesh.subMeshCount);
        smr.shadowCastingMode   = ShadowCastingMode.Off;
        smr.receiveShadows      = false;

        _clones.Add(go);
    }

    private void CloneStatic(Renderer src, Mesh mesh, Material mat)
    {
        var go = new GameObject(CloneName);
        go.layer = src.gameObject.layer;
        go.transform.SetParent(src.transform, worldPositionStays: false);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials   = FillArray(mat, mesh.subMeshCount);
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        _clones.Add(go);
    }
}
