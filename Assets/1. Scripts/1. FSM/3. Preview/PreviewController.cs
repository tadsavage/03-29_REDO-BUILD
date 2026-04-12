using System.Collections.Generic;
using UnityEngine;

public class PreviewController : MonoBehaviour
{
    // =========================================================
    //  DEPENDENCIES
    // =========================================================
    private PlacementGrid _grid;

    // =========================================================
    //  POOLING
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
    //  MOVE SMOOTHING
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

    private GameObject _currentPreview;

    // =========================================================
    //  COLORS (MPB)
    // =========================================================
    private readonly Color _validColor = new(0.50f, 1.00f, 0.83f, 0.5f);
    private readonly Color _invalidColor = new(1.00f, 0.42f, 0.42f, 0.75f);
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private readonly Dictionary<GameObject, Material[][]> _originalMats = new();
    [SerializeField] private Material _highlightMat;

    private MaterialPropertyBlock _mpb;

    // =========================================================
    //  GHOST MATERIAL (assign a transparent ghost material in inspector)
    // =========================================================
    [SerializeField] private Material _ghostMaterial; // optional: assign a dedicated ghost material

    // =========================================================
    //  MODE FLAGS
    // =========================================================
    private bool _multiMode;
    private bool _deleteMode;

    private void Awake()
    {
        _grid = Object.FindFirstObjectByType<PlacementGrid>();
        _mpb = new MaterialPropertyBlock();
    }

    // ---------------------------------------------------------
    // RESET FOR PERSISTENT MOVE MODE
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
    public void SetDeleteMode(bool on)
    {
        _deleteMode = on;
    }

    // =========================================================
    //  PUBLIC API — SINGLE GHOST
    // =========================================================
    public void Show(ObjDataSO data)
    {
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
    //  MOVE SINGLE GHOST
    // =========================================================
    public void MoveTo(Vector3 pos, Vector2Int cell, ObjDataSO data)
    {
        if (_isFlyingIn)
            return;

        float stackY = data.isStackable ? _grid.GetStackHeight(cell) : 0f;
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
            SetGhostValid(_singleGhost);
    }

    public void SetGhostInvalid()
    {
        if (_singleGhost != null)
            SetGhostInvalid(_singleGhost);
    }

    public void ApplyHighlight(GameObject obj)
    {
        if (obj == null) return;

        // Get ALL renderers on root + children
        var renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        // Store original materials
        if (!_originalMats.ContainsKey(obj))
        {
            Material[][] mats = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
                mats[i] = renderers[i].sharedMaterials;

            _originalMats[obj] = mats;
        }

        // Apply highlight material to ALL renderers
        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = _highlightMat;

            r.sharedMaterials = mats;
        }
    }

    public void RemoveHighlight(GameObject obj)
    {
        if (obj == null) return;

        if (!_originalMats.TryGetValue(obj, out var mats))
            return;

        var renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        // Restore original materials
        for (int i = 0; i < renderers.Length && i < mats.Length; i++)
            renderers[i].sharedMaterials = mats[i];
        _originalMats.Remove(obj);
    }
    // =========================================================
    //  MULTI-GHOST MODE
    // =========================================================
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

    // =========================================================
    //  SHOW MULTI-GHOST
    // =========================================================
    public void ShowGhost(Vector2Int cell, bool valid, float rotation)
    {
        if (!_multiMode)
            return;

        GameObject ghost;
        if (_multiGhosts.TryGetValue(cell, out ghost))
        {
            ghost.SetActive(true);
        }
        else
        {
            ghost = _pool.Count > 0
                ? _pool.Pop()
                : CreateGhostFromPrefab(_currentData.prefab);

            ghost.SetActive(true);
            _multiGhosts[cell] = ghost;
        }

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
    //  CLEAR MULTI-GHOSTS
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
    //  GHOST CREATION (NO MATERIAL INSTANCING)
    // =========================================================
    private GameObject CreateGhostFromPrefab(GameObject source)
    {
        GameObject ghost = Instantiate(source);
        ghost.name = source.name + "_Ghost";

        foreach (var comp in ghost.GetComponentsInChildren<MonoBehaviour>())
            Destroy(comp);

        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            Destroy(col);

        // Ensure renderers are prepared for MPB coloring.
        // If you prefer to force a dedicated ghost material, assign _ghostMaterial in inspector.
        if (_ghostMaterial != null)
        {
            foreach (var r in ghost.GetComponentsInChildren<Renderer>())
            {
                // assign the ghost material asset (shared) so shader settings are correct
                // this avoids creating new material instances at runtime
                r.sharedMaterial = _ghostMaterial;
            }
        }
        else
        {
            // If no dedicated ghost material, ensure existing materials expose _BaseColor.
            // We don't modify sharedMaterial here to avoid instancing.
        }

        // Default appearance (valid)
        ApplyGhostAppearance(ghost, _validColor, _validColor.a);

        return ghost;
    }

    // =========================================================
    //  MOVE STATE GHOST API
    // =========================================================
    public void ShowGhost(GameObject source)
    {
        // Create a ghost from the object being moved
        if (_singleGhost != null)
        {
            Destroy(_singleGhost);
        }
        ClearGhostPool();
        _singleGhost = CreateGhostFromPrefab(source);
        _singleGhost.SetActive(true);
        _currentPreview = _singleGhost;

        // Match placement preview appearance
        ApplyGhostAppearance(_singleGhost, _validColor, _validColor.a);
    }

    public void HideGhost()
    {
        if (_singleGhost != null)
            _singleGhost.SetActive(false);
    }

    public void UpdateGhostPosition(Vector3 worldPos)
    {
        _targetPos = worldPos;
        _hasTarget = true;
    }

    // =========================================================
    //  GHOST COLORING (MPB)
    // =========================================================
    private void ApplyGhostAppearance(GameObject ghost, Color color, float alpha)
    {
        if (ghost == null) return;

        var renderers = ghost.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        _mpb.Clear();
        Color c = color;
        c.a = alpha;
        _mpb.SetColor(BaseColorID, c);

        foreach (var r in renderers)
        {
            r.SetPropertyBlock(_mpb);
        }
    }

    private void SetGhostValid(GameObject go)
    {
        ApplyGhostAppearance(go, _validColor, _validColor.a);
    }

    private void SetGhostInvalid(GameObject go)
    {
        ApplyGhostAppearance(go, _invalidColor, _invalidColor.a);
    }

    public void ClearGhostPool()
    {
        foreach (var g in _pool)
            Destroy(g);

        _pool.Clear();
    }

    // =========================================================
    //  FLY-IN + SMOOTHING
    // =========================================================
    public void BeginFlyIn(Vector3 worldTarget)
    {
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
}
