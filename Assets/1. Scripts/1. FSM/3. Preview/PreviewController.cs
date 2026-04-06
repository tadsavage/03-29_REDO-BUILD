using UnityEngine;
using System.Collections.Generic;

public class PreviewController : MonoBehaviour
{
    private PlacementGrid _grid;

    // Pooling
    private readonly Stack<GameObject> _pool = new();
    private readonly List<GameObject> _activeGhosts = new();

    // Single ghost
    private GameObject _singleGhost;
    private ObjDataSO _currentData;
    public float CurrentRotation { get; private set; }

    // Movement smoothing
    private Vector3 _targetPos;
    private Vector3 _velocity;
    private bool _hasTarget;
    [SerializeField] private float moveSmoothTime = 0.08f;

    // Fly-in animation
    private bool _isFlyingIn;
    private float _flyTime;
    private const float FlyDuration = 0.5f;
    private Vector3 _flyStartPos;

    // Active preview reference
    private GameObject _currentPreview;

    // Colors
    private Color _validColor = new Color(0.50f, 1.00f, 0.83f, 0.80f);
    private Color _invalidColor = new Color(1.00f, 0.42f, 0.42f, 0.80f);

    // Multi-ghost mode
    private bool _multiMode;

    private void Awake()
    {
        _grid = Object.FindFirstObjectByType<PlacementGrid>();
    }

    // ---------------------------------------------------------
    // PUBLIC API
    // ---------------------------------------------------------
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

    // ---------------------------------------------------------
    // MOVE GHOST (WITH STACK AUTO‑SNAP)
    // ---------------------------------------------------------
    public void MoveTo(Vector3 pos, Vector2Int cell, ObjDataSO data)
    {
        if (_isFlyingIn)
            return;

        // ================================
        // STACKING: Auto‑snap ghost height
        // Raise ghost to top of stack in this cell
        // ================================
        float stackY = 0f;
        if (data.isStackable)
            stackY = _grid.GetStackHeight(cell);

        pos.y += stackY;
        // ================================

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

    public void ShowGhost(Vector2Int cell, bool valid, float rotation)
    {
        if (!_multiMode)
            return;

        GameObject ghost = GetGhost();
        _activeGhosts.Add(ghost);

        // ================================
        // STACKING: Auto‑snap ghost height for drag placement
        // ================================
        float stackY = 0f;
        if (_currentData != null && _currentData.isStackable)
            stackY = _grid.GetStackHeight(cell);

        Vector3 pos = _grid.GetCellCenter(cell);
        pos.y += stackY;
        // ================================

        ghost.transform.position = pos;
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
    // GHOST CREATION
    // ---------------------------------------------------------
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

        // Make transparent
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

    // ---------------------------------------------------------
    // FLY-IN + SMOOTHING
    // ---------------------------------------------------------
    public void BeginFlyIn(Vector3 worldTarget)
    {
        _targetPos = worldTarget;
        _isFlyingIn = true;
        _flyTime = 0f;

        float randX = Random.Range(-5f, 5f);
        float randZ = Random.Range(-3f, 3f);

        _flyStartPos = worldTarget + new Vector3(randX, 8f, randZ);
        _currentPreview.transform.position = _flyStartPos;
    }

    private void Update()
    {
        if (_currentPreview == null)
            return;

        // Fly-in animation
        if (_isFlyingIn)
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
