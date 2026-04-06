using UnityEngine;
using System.Collections.Generic;

public class PreviewController : MonoBehaviour
{
    // Dependency references
    private PlacementGrid _grid;
    private readonly Stack<GameObject> _pool = new();
    private readonly List<GameObject> _activeGhosts = new();

    // Single ghost for normal placement mode
    private GameObject _singleGhost;
    private ObjDataSO _currentData;
    public float CurrentRotation { get; private set; }

    // Smooth build placement fly-in effect (optional)
    private bool _isFlyingIn;
    private float _flyTime;
    [Header("Fly-In  Settings")]    // Smooth movement
    public Vector3 _flyStartPos;
    [SerializeField]private const float FlyDuration = .5f; // tweakable - cant serialize const, but can be changed in code
    [SerializeField] private float moveSmoothTime = 0.08f;
    private Vector3 _velocity;
    private Vector3 _targetPos = Vector3.zero;
    private bool _hasTarget;

    // Reference to the active preview ghost
    private GameObject _currentPreview;

    // MaterialPropertyBlock for efficient color changes
    private MaterialPropertyBlock _mpb;
    private Color _validColor = new Color(0f, 1f, 0f, 0.35f);
    private Color _invalidColor = new Color(1f, 0f, 0f, 0.35f);

    // Multi-ghost mode for drag placement
    private bool _multiMode;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _grid = Object.FindFirstObjectByType<PlacementGrid>();
    }

    // ---------------------------------------------------------
    // PUBLIC API
    // ---------------------------------------------------------

    public void Show(ObjDataSO data)
    {
        // If switching to a new prefab, destroy old ghosts and pool
        if (_currentData != data)
        {
            if (_singleGhost != null)
                Destroy(_singleGhost);

            ClearGhostPool();
            _singleGhost = CreateGhostFromPrefab(data.prefab);
        }

        _currentData = data;

        // Always reactivate the ghost
        _singleGhost.SetActive(true);

        // Assign preview reference BEFORE fly-in or smoothing
        _currentPreview = _singleGhost;// ⭐ Needed for smoothing + fly-in

        // Restore rotation
        _singleGhost.transform.rotation = Quaternion.Euler(0, CurrentRotation, 0);

        // Apply ghost tint
        SetGhostValid(_singleGhost);
    }

    public void Hide()
    {
        if (_singleGhost != null)
            _singleGhost.SetActive(false);

        ClearMultiGhosts();
    }

    public void MoveTo(Vector3 pos)
    {
        if (_isFlyingIn)
            return;
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

    // ---------------------------------------------------------
    // MULTI-GHOST MODE (DRAG PLACEMENT)
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

    public void ShowGhost(Vector2Int cell, bool valid, float rotation)
    {
        if (!_multiMode)
            return;

        GameObject ghost = GetGhost();
        _activeGhosts.Add(ghost);

        ghost.transform.position = _grid.GetCellCenter(cell);
        ghost.transform.rotation = Quaternion.Euler(0, rotation, 0);

        if (valid)
            SetGhostValid(ghost);
        else
            SetGhostInvalid(ghost);
    }


    // ---------------------------------------------------------
    // INTERNAL HELPERS
    // ---------------------------------------------------------

    private GameObject GetGhost()
    {
        if (_pool.Count > 0)
        {
            var go = _pool.Pop();
            go.SetActive(true);
            return go;
        }

        return CreateGhostFromPrefab(_currentData.prefab);
    }

    private void ClearMultiGhosts()
    {
        foreach (var g in _activeGhosts)
        {
            g.SetActive(false);
            _pool.Push(g);
        }
        _activeGhosts.Clear();
    }

    // ---------------------------------------------------------
    // AUTO-GHOST CREATION FROM OBJ PREFAB
    // ---------------------------------------------------------

    private GameObject CreateGhostFromPrefab(GameObject source)
    {
        GameObject ghost = Instantiate(source);
        ghost.name = source.name + "_Ghost";

        // Remove all scripts
        foreach (var comp in ghost.GetComponentsInChildren<MonoBehaviour>())
            DestroyImmediate(comp);

        // Remove all colliders
        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            DestroyImmediate(col);

        // Apply ghost material behavior
        foreach (var r in ghost.GetComponentsInChildren<Renderer>())
        {
            var mat = new Material(r.sharedMaterial);
            mat.SetFloat("_Surface", 1); // URP Transparent
            mat.renderQueue = 3000;
            r.sharedMaterial = mat;
        }

        return ghost;
    }

    private void SetGhostValid(GameObject go)
    {
        var r = go.GetComponentInChildren<Renderer>();
        r.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", _validColor);
        r.SetPropertyBlock(_mpb);
    }

    private void SetGhostInvalid(GameObject go)
    {
        var r = go.GetComponentInChildren<Renderer>();
        r.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", _invalidColor);
        r.SetPropertyBlock(_mpb);
    }

    public void RestoreMaterials()
    {
        // Optional: if you ever swap materials, restore here.
    }
    private void ClearGhostPool()
    {
        foreach (var g in _pool)
            Destroy(g);

        _pool.Clear();
    }
    public void BeginFlyIn(Vector3 worldTarget)
    {
        _targetPos = worldTarget;     // freeze target
        _isFlyingIn = true;
        _flyTime = 0f;

        // randomize start
        float randX = Random.Range(-5f, 5f);
        float randZ = Random.Range(-3f, 3f);

        _flyStartPos = worldTarget + new Vector3(randX, 8f, randZ);

        _currentPreview.transform.position = _flyStartPos;
    }
    private void Update()
    {
        if (_currentPreview == null)
            return;

        // Fly‑in animation
        if (_isFlyingIn)
{
    _flyTime += Time.deltaTime;
    float t = Mathf.Clamp01(_flyTime / FlyDuration);

    // smooth curve (optional but recommended)
    t = Mathf.SmoothStep(0f, 1f, t);

    _currentPreview.transform.position =
        Vector3.Lerp(_flyStartPos, _targetPos, t);

    if (t >= 1f)
        _isFlyingIn = false;

    return; // ⭐ IMPORTANT: do NOT update preview position while flying
}

        // Normal smoothing
        if (_hasTarget)
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
}
