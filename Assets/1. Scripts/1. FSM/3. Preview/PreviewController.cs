using System.Collections.Generic;
using UnityEngine;

public class PreviewController : MonoBehaviour
{
    public static PreviewController Instance { get; private set; }
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
    // MOVEMENT / HOVER
    // ---------------------------------------------------------
    [Header("Move Smoothing")]
    [SerializeField] private float moveSmoothTime = 0.08f;
    [SerializeField] private float moveSmoothSpeed = 0.25f;

    [Header("Hover Settings")]
    [SerializeField] private float offsetMovePreview = .5f;

    public float OffsetMovePreview => offsetMovePreview;
    public float MoveSmoothTime => moveSmoothTime;

    private GameObject _currentPreview;

    // ---------------------------------------------------------
    // FACTORIO-STYLE HIGHLIGHTS (SUBTLE TINTS)
    // ---------------------------------------------------------
    private static readonly Color HighlightGreen = new(0.9f, 1.0f, 0.9f, 0.4f);
    private static readonly Color HighlightRed = new(1.0f, 0.9f, 0.9f, 0.4f);

    private MaterialPropertyBlock _highlightMPB;
    private MaterialPropertyBlock _restoreMPB;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Material _ghostMaterial;

    private bool _multiMode;
    private bool _deleteMode;
    private bool _isMovePreviewMode;

    private void Awake()
    {
        Instance = this;
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

    public void SetMovePreviewMode(bool on)
    {
        _isMovePreviewMode = on;
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
        _targetPos = CalculateTargetPos(pos, cell, data);
        _hasTarget = true;
    }

    public void SnapTo(Vector3 baselinePos, Vector2Int cell, ObjDataSO data)
    {
        _targetPos = CalculateTargetPos(_grid.GetCellCenter(cell), cell, data);
        _hasTarget = true; // We want it to start moving towards the target goal (lifting)
        _velocity = Vector3.zero;

        if (_currentPreview != null)
        {
            // Snap the visual exactly where the object was (the baseline).
            // The lift offset will be applied in the Update loop starting this frame.
            _currentPreview.transform.position = baselinePos;
        }
    }

    private Vector3 CalculateTargetPos(Vector3 pos, Vector2Int cell, ObjDataSO data)
    {
        float stackY = 0f;

        // Logic for preview height calculation:
        // 1. If we are placing a Ground/Foundation, it stays at y=0.
        // 2. Otherwise, we calculate the cumulative height of valid surfaces.
        // 3. Grounds and Floors always contribute to the base height.
        // 4. Other objects ONLY contribute height if BOTH the ghost and the existing object are stackable.
        if (data != null && !IsGround(data))
        {
            var list = _grid.GetObjectsInCell(cell);
            if (list != null)
            {
                bool groundHeightAdded = false;
                foreach (var entry in list)
                {
                    if (entry.instance == null || !entry.instance.activeSelf) continue;

                    bool entryIsGround = IsGround(entry.data);

                    if (entryIsGround)
                    {
                        if (!groundHeightAdded)
                        {
                            stackY += entry.data.objHeight;
                            groundHeightAdded = true;
                        }
                    }
                    else if (entry.data.isFloor)
                    {
                        stackY += entry.data.objHeight;
                    }
                    else if (data.isStackable && entry.data.isStackable)
                    {
                        // Objects that ignore rules or clear grid don't add height 
                        // unless they are specifically floors/grounds (handled above)
                        if (entry.data.ignorePlacementRules || entry.data.ClearsGridAfterPlacement)
                            continue;

                        stackY += entry.data.objHeight;
                    }
                }
            }
        }

        if (!_deleteMode)
            pos.y += stackY;

        return pos;
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

        // Destroy non-visual components. Components must be removed in dependency order
        // (dependents before their dependencies) to avoid "can't remove X because Y depends on it".
        // We retry the loop until no more components can be removed.
        bool removed = true;
        while (removed)
        {
            removed = false;
            foreach (var comp in ghost.GetComponentsInChildren<Component>())
            {
                if (comp is Transform || comp is Renderer || comp is MeshFilter)
                    continue;

                try
                {
                    DestroyImmediate(comp);
                    removed = true;
                }
                catch { }
            }
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
    // SMOOTH FOLLOW + LIFT
    // ---------------------------------------------------------
    private void Update()
    {
        if (_currentPreview == null)
            return;

        // Smooth follow with vertical offset (Lift) - only in Move Mode
        if (_hasTarget && !_deleteMode)
        {
            float adjustedSmooth = moveSmoothTime / Mathf.Max(0.01f, moveSmoothSpeed);

            Vector3 finalTarget = _targetPos;
            if (_isMovePreviewMode)
            {
                finalTarget.y += offsetMovePreview;
            }

            _currentPreview.transform.position =
                Vector3.SmoothDamp(
                    _currentPreview.transform.position,
                    finalTarget,
                    ref _velocity,
                    adjustedSmooth
                );

            if ((_currentPreview.transform.position - finalTarget).sqrMagnitude < 0.01f)
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
