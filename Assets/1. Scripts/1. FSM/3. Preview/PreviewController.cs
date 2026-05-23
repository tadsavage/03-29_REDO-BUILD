using System.Collections.Generic;
using UnityEngine;

public class PreviewController : MonoBehaviour
{
    private PlacementGrid _grid;

    // ---------------------------------------------------------
    // GHOST POOLING
    // ---------------------------------------------------------
    private readonly Stack<GameObject> _pool = new();
    private readonly Dictionary<Vector2Int, GameObject> _multiGhosts = new();

    private GameObject _singleGhost;
    private ObjDataSO _currentData;
    public float CurrentRotation { get; private set; }

    private Vector3 _targetPos;
    private Vector3 _velocity;
    private bool _hasTarget;

    // ---------------------------------------------------------
    // MOVEMENT / FLY-IN
    // ---------------------------------------------------------
    [Header("Move Smoothing")]
    [SerializeField] private float moveSmoothTime = 0.08f;
    [SerializeField] private float moveSmoothSpeed = 0.25f;

    [Header("Fly-In Settings")]
    [SerializeField] private bool useFlyIn = false;
    [SerializeField] private float flyDuration = 0.25f;

    private bool _isFlyingIn;
    private float _flyTime;
    private Vector3 _flyStartPos;

    private GameObject _currentPreview;

    // ---------------------------------------------------------
    // FLAT COLOR HIGHLIGHTS (SEMI-TRANSPARENT)
    // ---------------------------------------------------------
    private static readonly Color HighlightGreen = new(0.20f, 1.00f, 0.20f, 0.65f);
    private static readonly Color HighlightRed = new(1.00f, 0.20f, 0.20f, 0.65f);

    private MaterialPropertyBlock _highlightMPB;
    private MaterialPropertyBlock _restoreMPB;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Material _ghostMaterial;

    private bool _multiMode;
    private bool _deleteMode;

    private void Awake()
    {
        _grid = Object.FindFirstObjectByType<PlacementGrid>();

        _highlightMPB = new MaterialPropertyBlock();
        _restoreMPB = new MaterialPropertyBlock();
    }

    private readonly Dictionary<GameObject, Renderer[]> _ghostRendererCache = new();

    // ---------------------------------------------------------
    // FLAT HIGHLIGHT API
    // ---------------------------------------------------------
    public void ApplyFlatHighlight(GameObject obj, Color color)
    {
        if (obj == null)
            return;

        _highlightMPB.Clear();
        _highlightMPB.SetColor(BaseColorID, color);

        if (!_ghostRendererCache.TryGetValue(obj, out var renderers))
        {
            renderers = obj.GetComponentsInChildren<Renderer>(true);
            _ghostRendererCache[obj] = renderers;
        }

        foreach (var r in renderers)
        {
            if (r != null)
                r.SetPropertyBlock(_highlightMPB);
        }
    }

    public void ClearFlatHighlight(GameObject obj)
    {
        if (obj == null)
            return;

        if (!_ghostRendererCache.TryGetValue(obj, out var renderers))
        {
            renderers = obj.GetComponentsInChildren<Renderer>(true);
            _ghostRendererCache[obj] = renderers;
        }

        foreach (var r in renderers)
        {
            if (r != null)
                r.SetPropertyBlock(_restoreMPB); // clears override
        }
    }

    // ---------------------------------------------------------
    // RESET
    // ---------------------------------------------------------
    public void ResetMoveGhostState()
    {
        HideGhost();
        ClearMultiGhosts();
        ClearGhostPool();

        _currentPreview = null;
        _hasTarget = false;
        _isFlyingIn = false;
        _velocity = Vector3.zero;
    }

    public void ResetAllVisuals()
    {
        ResetMoveGhostState();
        Hide();
        _multiMode = false;
        _deleteMode = false;
    }

    public void SetDeleteMode(bool on)
    {
        _deleteMode = on;
    }

    // ---------------------------------------------------------
    // SINGLE GHOST
    // ---------------------------------------------------------
    private bool IsGround(ObjDataSO data)
    {
        if (data == null) return false;
        return data.category == "Foundation" || data.category == "Grounds";
    }

    public void Show(ObjDataSO data)
{
        if (_currentData != data)
        {
            if (_singleGhost != null)
            {
                _ghostRendererCache.Remove(_singleGhost);
                Destroy(_singleGhost);
            }

            ClearGhostPool();
            _singleGhost = CreateGhostFromPrefab(data.prefab);
        }

        _currentData = data;

        _singleGhost.SetActive(true);
        _currentPreview = _singleGhost;

        _singleGhost.transform.rotation = Quaternion.Euler(0, CurrentRotation, 0);
        SetGhostValid();
    }

    public void Hide()
    {
        if (_singleGhost != null)
            _singleGhost.SetActive(false);

        ClearMultiGhosts();
    }

    // ---------------------------------------------------------
    // MOVE SINGLE GHOST
    // ---------------------------------------------------------
    public void MoveTo(Vector3 pos, Vector2Int cell, ObjDataSO data)
    {
        if (_isFlyingIn)
            return;

        // Foundations are always at y=0. Everything else (Floors, Objects) sits on the stack.
        float stackY = IsGround(data) ? 0f : _grid.GetStackHeight(cell);

        if (!_deleteMode)
            pos.y += stackY;

        _targetPos = pos;
        _hasTarget = true;
    }

    public void Rotate(float angle)
    {
        CurrentRotation = angle;

        if (_singleGhost != null)
            _singleGhost.transform.rotation = Quaternion.Euler(0, angle, 0);
    }

    public void SetGhostValid()
    {
        if (_singleGhost != null)
            ApplyFlatHighlight(_singleGhost, HighlightGreen);
    }

    public void SetGhostInvalid()
    {
        if (_singleGhost != null)
            ApplyFlatHighlight(_singleGhost, HighlightRed);
    }

    // ---------------------------------------------------------
    // MULTI-GHOST MODE
    // ---------------------------------------------------------
    public void BeginSelectionCells()
    {
        _multiMode = true;

        if (_singleGhost != null)
            _singleGhost.SetActive(false);

        ClearMultiGhosts();
    }

    public void EndSelectionCells()
    {
        _multiMode = false;

        ClearMultiGhosts();

        if (_singleGhost != null)
            _singleGhost.SetActive(true);
    }

    public void ShowMultiGhost(Vector2Int cell, bool valid, float rotation)
    {
        if (!_multiMode)
            return;

        if (!_multiGhosts.TryGetValue(cell, out GameObject ghost))
        {
            ghost = _pool.Count > 0
                ? _pool.Pop()
                : CreateGhostFromPrefab(_currentData.prefab);

            _multiGhosts[cell] = ghost;
        }

        ghost.SetActive(true);

        // Foundations are always at y=0. Everything else (Floors, Objects) sits on the stack.
        float stackY = IsGround(_currentData) ? 0f : _grid.GetStackHeight(cell);

        Vector3 pos = _grid.GetCellCenter(cell);
        pos.y += stackY;

        ghost.transform.position = pos;
        ghost.transform.rotation = Quaternion.Euler(0, rotation, 0);

        ApplyFlatHighlight(ghost, valid ? HighlightGreen : HighlightRed);
    }

    public void ClearMultiGhosts()
    {
        foreach (var kvp in _multiGhosts)
        {
            kvp.Value.SetActive(false);
            _pool.Push(kvp.Value);
        }

        _multiGhosts.Clear();
    }

    // ---------------------------------------------------------
    // GHOST CREATION
    // ---------------------------------------------------------
    private GameObject CreateGhostFromPrefab(GameObject source)
    {
        GameObject ghost = Instantiate(source);
        ghost.name = source.name + "_Ghost";

        // Set to Ignore Raycast layer (2) so it doesn't block its own raycasts
        ghost.layer = 2; 

        // Use DestroyImmediate to ensure they are gone before the next line/frame
        // and check for Component to catch everything (Obstacles, Modifiers, etc.)
        foreach (var comp in ghost.GetComponentsInChildren<Component>())
        {
            if (comp is Transform || comp is Renderer || comp is MeshFilter)
                continue;

            DestroyImmediate(comp);
        }

        if (_ghostMaterial != null)
        {
            foreach (var r in ghost.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = _ghostMaterial;
        }

        ApplyFlatHighlight(ghost, HighlightGreen);
        return ghost;
    }

    // ---------------------------------------------------------
    // GHOST POOL
    // ---------------------------------------------------------
    public void ClearGhostPool()
    {
        foreach (var g in _pool)
        {
            if (g != null)
            {
                _ghostRendererCache.Remove(g);
                Destroy(g);
            }
        }

        _pool.Clear();
    }

    // ---------------------------------------------------------
    // FLY-IN + SMOOTHING
    // ---------------------------------------------------------
    public void BeginFlyIn(Vector3 worldTarget)
    {
        if (!useFlyIn)
            return;

        _targetPos = worldTarget;
        _isFlyingIn = true;
        _flyTime = 0f;

        float randX = Random.Range(-5f, 5f);
        float randZ = Random.Range(-3f, 8f);

        _flyStartPos = worldTarget + new Vector3(randX, 8f, randZ);
        if (_currentPreview != null)
            _currentPreview.transform.position = _flyStartPos;
    }

    private void Update()
    {
        if (_currentPreview == null)
            return;

        // Fly-in
        if (_isFlyingIn && useFlyIn && !_deleteMode)
        {
            _flyTime += Time.deltaTime;
            float t = Mathf.Clamp01(_flyTime / flyDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            _currentPreview.transform.position =
                Vector3.Lerp(_flyStartPos, _targetPos, t);

            if (t >= 0.75f)
                _isFlyingIn = false;

            return;
        }

        // Smooth follow
        if (_hasTarget && !_deleteMode)
        {
            float adjustedSmooth = moveSmoothTime / Mathf.Max(0.01f, moveSmoothSpeed);

            _currentPreview.transform.position =
                Vector3.SmoothDamp(
                    _currentPreview.transform.position,
                    _targetPos,
                    ref _velocity,
                    adjustedSmooth
                );

            if ((_currentPreview.transform.position - _targetPos).sqrMagnitude < 0.01f)
            {
                _hasTarget = false;
                _velocity = Vector3.zero;
            }
        }
    }

    // ---------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------
    public void HideGhost()
    {
        if (_singleGhost != null)
            _singleGhost.SetActive(false);
    }
}
