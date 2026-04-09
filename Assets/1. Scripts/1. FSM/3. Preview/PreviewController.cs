using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PreviewController : MonoBehaviour
{
    // =========================================================
    //  DEPENDENCIES
    // =========================================================
    private PlacementGrid _grid;
    private GameObject _ghostInstance;
    // =========================================================
    //  POOLING
    //  - _pool: inactive ghost objects ready for reuse
    //  - _multiGhosts: active ghosts keyed by cell position
    // =========================================================
    private readonly Stack<GameObject> _pool = new();
    private readonly Dictionary<Vector2Int, GameObject> _multiGhosts = new();

    // =========================================================
    //  SINGLE-GHOST (HOVER PREVIEW)
    // =========================================================
    private GameObject _singleGhost;
    private ObjDataSO _currentData;
    public float CurrentRotation { get; private set; }

    // =========================================================
    //  MOVEMENT SMOOTHING (for single ghost)
    // =========================================================
    private Vector3 _targetPos;
    private Vector3 _velocity;
    private bool _hasTarget;
    [SerializeField] private float moveSmoothTime = 0.08f;

    // =========================================================
    //  FLY-IN ANIMATION
    // =========================================================
    private bool _isFlyingIn;
    private float _flyTime;
    private const float FlyDuration = 0.5f;
    private Vector3 _flyStartPos;

    // The currently active preview object (single ghost)
    private GameObject _currentPreview;

    // =========================================================
    //  COLORS
    // =========================================================
    private readonly Color _validColor = new(0.50f, 1.00f, 0.83f, 0.5f);
    private readonly Color _invalidColor = new(1.00f, 0.42f, 0.42f, 0.75f);

    // =========================================================
    //  MODE FLAGS
    // =========================================================
    private bool _multiMode; // true during drag placement

    private void Awake()
    {
        _grid = Object.FindFirstObjectByType<PlacementGrid>();
    }
    private bool _deleteMode = false;

    public void SetDeleteMode(bool on)
    {
        _deleteMode = on;
    }
    // =========================================================
    //  PUBLIC API — SINGLE GHOST (HOVER PREVIEW)
    // =========================================================
    public void Show(ObjDataSO data)
    {
        // If switching to a new prefab, rebuild the single ghost
        if (_currentData != data)
        {
            if (_singleGhost != null)
                Destroy(_singleGhost);

            ClearGhostPool();
            _singleGhost = CreateGhostFromPrefab(data.prefab);
        }

        _currentData = data;

        _singleGhost.SetActive(true);
        _currentPreview = _singleGhost;

        _singleGhost.transform.rotation = Quaternion.Euler(0, CurrentRotation, 0);
        SetGhostValid(_singleGhost);
    }

    public void Hide()
    {
        if (_singleGhost != null)
            _singleGhost.SetActive(false);

        ClearMultiGhosts();
    }

    // =========================================================
    //  MOVE SINGLE GHOST (WITH STACK AUTO-SNAP)
    // =========================================================
    public void MoveTo(Vector3 pos, Vector2Int cell, ObjDataSO data)
    {
        if (_isFlyingIn)
            return;

        // Auto-snap vertical position to top of stack
        float stackY = data.isStackable ? _grid.GetStackHeight(cell) : 0f;
        if (!_deleteMode)
        {
            pos.y += stackY; // or whatever your offset is
        }

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
            SetGhostValid(_singleGhost);
    }

    public void SetGhostInvalid()
    {
        if (_singleGhost != null)
            SetGhostInvalid(_singleGhost);
    }

    // =========================================================
    //  MULTI-GHOST MODE (DRAG PREVIEW)
    // =========================================================
    public void BeginSelectionCells()
    {
        _multiMode = true;

        // Hide single ghost during drag
        if (_singleGhost != null)
            _singleGhost.SetActive(false);

        ClearMultiGhosts();
    }

    public void EndSelectionCells()
    {
        _multiMode = false;

        ClearMultiGhosts();

        // Restore single ghost after drag
        if (_singleGhost != null)
            _singleGhost.SetActive(true);
    }

    // =========================================================
    //  SHOW MULTI-GHOST (ONE PER CELL)
    // =========================================================
    public void ShowGhost(Vector2Int cell, bool valid, float rotation)
    {
        if (!_multiMode)
            return;

        GameObject ghost;

        // Reuse existing ghost for this cell
        if (_multiGhosts.TryGetValue(cell, out ghost))
        {
            ghost.SetActive(true);
        }
        else
        {
            // Pull from pool or create new
            ghost = _pool.Count > 0
                ? _pool.Pop()
                : CreateGhostFromPrefab(_currentData.prefab);

            ghost.SetActive(true);
            _multiGhosts[cell] = ghost;
        }

        // Auto-snap vertical position to stack height
        float stackY = (_currentData != null && _currentData.isStackable)
            ? _grid.GetStackHeight(cell)
            : 0f;

        Vector3 pos = _grid.GetCellCenter(cell);
        pos.y += stackY;

        ghost.transform.position = pos;
        ghost.transform.rotation = Quaternion.Euler(0, rotation, 0);

        if (valid)
            SetGhostValid(ghost);
        else
            SetGhostInvalid(ghost);
    }

    // =========================================================
    //  CLEAR MULTI-GHOSTS (CALLED EVERY DRAG FRAME)
    // =========================================================
    public void ClearMultiGhosts()
    {
        foreach (var kvp in _multiGhosts)
        {
            kvp.Value.SetActive(false);
            _pool.Push(kvp.Value);
        }

        _multiGhosts.Clear();
    }

    // =========================================================
    //  GHOST CREATION (TRANSPARENT, NO SCRIPTS, NO COLLIDERS)
    // =========================================================
    private GameObject CreateGhostFromPrefab(GameObject source)
    {
        GameObject ghost = Instantiate(source);
        ghost.name = source.name + "_Ghost";

        // Remove scripts
        foreach (var comp in ghost.GetComponentsInChildren<MonoBehaviour>())
            DestroyImmediate(comp);

        // Remove colliders
        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            DestroyImmediate(col);

        // Convert materials to transparent ghost materials
        foreach (var r in ghost.GetComponentsInChildren<Renderer>())
        {
            var mat = new Material(r.sharedMaterial);

            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_AlphaClip", 0);

            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            r.sharedMaterial = mat;
        }

        return ghost;
    }

    private void SetGhostValid(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterial.SetColor("_BaseColor", _validColor);
    }

    private void SetGhostInvalid(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterial.SetColor("_BaseColor", _invalidColor);
    }

    private void ClearGhostPool()
    {
        foreach (var g in _pool)
            Destroy(g);

        _pool.Clear();
    }

    // =========================================================
    //  FLY-IN + SMOOTHING (SINGLE GHOST ONLY)
    // =========================================================
    public void BeginFlyIn(Vector3 worldTarget)
    {
        _targetPos = worldTarget;
        _isFlyingIn = true;
        _flyTime = 0f;

        float randX = Random.Range(-5f, 5f);
        float randZ = Random.Range(-3f, 8f);

        _flyStartPos = worldTarget + new Vector3(randX, 8f, randZ);
        _currentPreview.transform.position = _flyStartPos;
    }

    private void Update()
    {
        if (_currentPreview == null)
            return;

        // Fly-in animation
        if (_isFlyingIn && !_deleteMode)
        {
            _flyTime += Time.deltaTime;
            float t = Mathf.Clamp01(_flyTime / FlyDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            _currentPreview.transform.position =
                Vector3.Lerp(_flyStartPos, _targetPos, t);

            if (t >= 1f)
                _isFlyingIn = false;

            return;
        }

        // Smooth movement
        if (_hasTarget && !_deleteMode)
        {
            _currentPreview.transform.position =
                Vector3.SmoothDamp(
                    _currentPreview.transform.position,
                    _targetPos,
                    ref _velocity,
                    moveSmoothTime
                );
        }
    }
    public void SetGhostDelete()
    {
        if (_ghostInstance == null)
            return;

        var highlighter = _ghostInstance.GetComponent<BuildingHighlighter>();
        if (highlighter != null)
            highlighter.HighlightDelete(true);
    }
}

