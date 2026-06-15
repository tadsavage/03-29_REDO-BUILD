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
    [Tooltip("Outline colour. Default is the UI accent blue #5C9BC4.")]
    [SerializeField] private Color _outlineColor = new Color(0.361f, 0.608f, 0.769f, 1f);

    [Tooltip("Outline thickness in world units. ~0.05 reads as 'medium-thick' on a ~1.8m character.")]
    [SerializeField, Range(0f, 0.2f)] private float _outlineWidth = 0.05f;

    private Material _maskMat;
    private Material _fillMat;

    private EmployeeIdentity _current;
    private readonly List<GameObject> _clones = new();
    private FreeLookCamera _camera;

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
            _camera = FindFirstObjectByType<FreeLookCamera>();
        if (_camera != null)
        {
            _camera.FocusOn(identity.transform.position);
            _camera.SetFollowTarget(identity.transform);   // track them until the player pans away
        }
    }

    /// <summary>Outline a single employee (replaces any existing highlight).</summary>
    public void Highlight(EmployeeIdentity identity)
    {
        if (identity == null) return;
        if (_current == identity) return;   // already outlined — leave it

        Clear();
        _current = identity;
        BuildOutline(identity);
    }

    /// <summary>Remove the current outline, if any, and stop the camera following.</summary>
    public void Clear()
    {
        foreach (var go in _clones)
            if (go != null) Destroy(go);
        _clones.Clear();
        _current = null;

        if (_camera == null) _camera = FindFirstObjectByType<FreeLookCamera>();
        if (_camera != null) _camera.SetFollowTarget(null);
    }

    public bool IsHighlighted(EmployeeIdentity identity) => identity != null && _current == identity;

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
        _fillMat.SetColor("_OutlineColor", _outlineColor);
        _fillMat.SetFloat("_OutlineWidth", _outlineWidth);
        return true;
    }

    private void BuildOutline(EmployeeIdentity identity)
    {
        if (!EnsureMaterials()) return;

        var renderers = identity.GetComponentsInChildren<Renderer>(includeInactive: false);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (r is SpriteRenderer) continue;          // skip the 2D portrait billboard
            if (r.name == CloneName) continue;          // never clone our own clones

            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                CloneSkinned(smr, _maskMat);
                CloneSkinned(smr, _fillMat);
            }
            else if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    CloneStatic(r, mf.sharedMesh, _maskMat);
                    CloneStatic(r, mf.sharedMesh, _fillMat);
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

    private void CloneSkinned(SkinnedMeshRenderer src, Material mat)
    {
        var go = new GameObject(CloneName);
        go.layer = src.gameObject.layer;
        go.transform.SetParent(src.transform, worldPositionStays: false);

        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh          = src.sharedMesh;
        smr.bones               = src.bones;
        smr.rootBone            = src.rootBone;
        smr.localBounds         = src.localBounds;
        smr.quality             = SkinQuality.Bone1;     // cheap — we only need the silhouette
        smr.updateWhenOffscreen = src.updateWhenOffscreen;
        smr.sharedMaterials     = FillArray(mat, src.sharedMesh.subMeshCount);
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
