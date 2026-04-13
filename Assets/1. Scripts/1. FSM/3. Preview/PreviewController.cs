using System.Collections.Generic;
using UnityEngine;

public class PreviewController : MonoBehaviour
{
    #region FIELDS ***************************************
    private PlacementGrid _grid;

    private readonly Stack<GameObject> _pool = new();
    private readonly Dictionary<Vector2Int, GameObject> _multiGhosts = new();

    private GameObject _singleGhost;
    private ObjDataSO _currentData;
    public float CurrentRotation { get; private set; }

    private Vector3 _targetPos;
    private Vector3 _velocity;
    private bool _hasTarget;

    [Header("Move Smoothing")]
    [SerializeField] private float moveSmoothTime = 0.08f;

    [Tooltip("Multiplier for how fast the ghost follows the ray during Move mode.")]
    [SerializeField] private float moveSmoothSpeed = 0.25f;

    [Tooltip("How close the ghost stays to the original object on the first frame (0 = stays at original, 1 = snaps to ray).")]
    [SerializeField] private float initialMoveLerp = 0.25f;

    [Header("Fly-In Settings")]
    [SerializeField] private bool useFlyIn = false;

    private bool _isFlyingIn;
    private float _flyTime;
    private const float FlyDuration = 0.5f;
    private Vector3 _flyStartPos;

    private GameObject _currentPreview;

    private readonly Color _validColor = new(0.50f, 1.00f, 0.83f, 0.5f);
    private readonly Color _invalidColor = new(1.00f, 0.42f, 0.42f, 0.75f);
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private readonly Dictionary<GameObject, Material[][]> _originalMats = new();
    [SerializeField] private Material _highlightMat;

    private MaterialPropertyBlock _mpb;

    [SerializeField] private Material _ghostMaterial;

    private bool _multiMode;
    private bool _deleteMode;
    #endregion

    private void Awake()
    {
        _grid = Object.FindFirstObjectByType<PlacementGrid>();
        _mpb = new MaterialPropertyBlock();
    }

    // ---------------------------------------------------------
    // RESET FOR MOVE MODE
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

    // =========================================================
    //  HIGHLIGHTING
    // =========================================================
    public void ApplyHighlight(GameObject obj)
    {
        if (obj == null) return;

        var renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        if (!_originalMats.ContainsKey(obj))
        {
            Material[][] mats = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
                mats[i] = renderers[i].sharedMaterials;

            _originalMats[obj] = mats;
        }

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

    public void ShowMultiGhost(Vector2Int cell, bool valid, float rotation)
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
    //  GHOST CREATION
    // =========================================================
    private GameObject CreateGhostFromPrefab(GameObject source)
    {
        GameObject ghost = Instantiate(source);
        ghost.name = source.name + "_Ghost";

        foreach (var comp in ghost.GetComponentsInChildren<MonoBehaviour>())
            Destroy(comp);

        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            Destroy(col);

        if (_ghostMaterial != null)
        {
            foreach (var r in ghost.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = _ghostMaterial;
        }

        ApplyGhostAppearance(ghost, _validColor, _validColor.a);

        return ghost;
    }

    // =========================================================
    //  MOVE STATE GHOST API
    // =========================================================
    public void ShowGhost(GameObject source)
    {
        if (_singleGhost != null)
        {
            Destroy(_singleGhost);
            _singleGhost = null;
        }

        ClearGhostPool();

        _singleGhost = CreateGhostFromPrefab(source);
        _singleGhost.SetActive(true);
        _currentPreview = _singleGhost;

        // Start ghost near the original object instead of snapping to ray end
        _currentPreview.transform.position = Vector3.Lerp(
            source.transform.position,
            _targetPos,
            initialMoveLerp
        );

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
    //  GHOST COLORING
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
            r.SetPropertyBlock(_mpb);
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

        // Fly-in (optional)
        if (_isFlyingIn && useFlyIn && !_deleteMode)
        {
            _flyTime += Time.deltaTime;
            float t = Mathf.Clamp01(_flyTime / FlyDuration);
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

            if ((_currentPreview.transform.position - _targetPos).sqrMagnitude < 0.04f)
            {
                _hasTarget = false;
                _velocity = Vector3.zero;
            }
        }
    }
}
