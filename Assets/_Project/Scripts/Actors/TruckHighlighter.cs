using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws a see-through selection outline around ONE truck at a time — same technique as
/// EmployeeHighlighter (reuses its two outline shaders), just keyed on TruckController instead
/// of EmployeeIdentity. Backs the "click a truck to pin its tooltip open" feature in
/// WorldHoverPopupUI: the outline and the pinned tooltip always pin/unpin together.
///
/// Fully self-contained: access via TruckHighlighter.Instance, which lazily creates the manager
/// GameObject. Materials are created in code from the two Hidden/ shaders — no asset wiring.
/// </summary>
public class TruckHighlighter : MonoBehaviour
{
    private const string MaskShaderName = "Hidden/EmployeeOutlineMask";
    private const string FillShaderName = "Hidden/EmployeeOutlineFill";
    private const string CloneName = "__TruckOutlineClone";

    private static TruckHighlighter _instance;
    public static TruckHighlighter Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<TruckHighlighter>();
                if (_instance == null)
                {
                    var go = new GameObject("TruckHighlighter");
                    _instance = go.AddComponent<TruckHighlighter>();
                }
            }
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    [Header("Outline look")]
    [Tooltip("Outline colour. Bright blue for high visibility.")]
    [SerializeField] private Color _outlineColor = new Color(0.706f, 0.784f, 0.851f, 1f);

    [Tooltip("Outline thickness in world units.")]
    [SerializeField, Range(0f, 0.2f)] private float _outlineWidth = 0.06f;

    private Material _maskMat;
    private Material _fillMat;

    private TruckController _current;
    private readonly List<GameObject> _clones = new();

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
        // The highlighted truck may have driven off and been destroyed — clones are parented
        // under it, so Unity removes them automatically; just drop our stale reference.
        if (_current == null && _clones.Count > 0)
            _clones.Clear();
    }

    /// <summary>Outline a single truck (replaces any existing highlight).</summary>
    public void Highlight(TruckController truck)
    {
        if (truck == null) return;
        if (_current == truck) return;

        Clear();
        _current = truck;
        BuildOutline(truck);
    }

    /// <summary>Remove the current outline, if any.</summary>
    public void Clear()
    {
        foreach (var go in _clones)
            if (go != null) Destroy(go);
        _clones.Clear();
        _current = null;
    }

    public bool IsHighlighted(TruckController truck) => truck != null && _current == truck;

    // ── Internals ────────────────────────────────────────────────────────────────
    private bool EnsureMaterials()
    {
        if (_maskMat != null && _fillMat != null) return true;

        var maskShader = Shader.Find(MaskShaderName);
        var fillShader = Shader.Find(FillShaderName);
        if (maskShader == null || fillShader == null)
        {
            Debug.LogWarning($"[TruckHighlighter] Outline shaders not found " +
                             $"('{MaskShaderName}' / '{FillShaderName}'). Highlight skipped.");
            return false;
        }

        _maskMat = new Material(maskShader) { renderQueue = 3000, hideFlags = HideFlags.HideAndDontSave };
        _fillMat = new Material(fillShader) { renderQueue = 3001, hideFlags = HideFlags.HideAndDontSave };
        _fillMat.SetColor("_OutlineColor", _outlineColor);
        _fillMat.SetFloat("_OutlineWidth", _outlineWidth);
        return true;
    }

    private void BuildOutline(TruckController truck)
    {
        if (!EnsureMaterials()) return;

        // The cargo riding in LoadContainer (every pallet/case currently on the trailer) is excluded
        // — outlining "the truck" should mean the vehicle itself, not every individual box inside it.
        // A fully-loaded trailer can hold thousands of small meshes, which would otherwise mean
        // spawning thousands of extra outline-clone GameObjects just to highlight one truck.
        Transform loadContainer = truck.LoadContainer;

        var renderers = truck.GetComponentsInChildren<Renderer>(includeInactive: false);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (r is SpriteRenderer) continue;
            if (r.name == CloneName) continue;
            if (loadContainer != null && r.transform.IsChildOf(loadContainer)) continue;

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
        smr.quality             = SkinQuality.Bone1;
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
