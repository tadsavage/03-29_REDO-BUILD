using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class DeleteState : IPlacementState
{
    private readonly RaycastController _raycast;
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementStateMachine _fsm;
    private readonly CellIndicatorController _indicator;
    private readonly PlacementActions _actions;
    private readonly MoneyService _money;
    private readonly WorldHoverPopupUI _hoverUI = Object.FindAnyObjectByType<WorldHoverPopupUI>();
    private TopBarUI _topBarUI;

    private TopBarUI topBarUI => _topBarUI != null ? _topBarUI : _topBarUI = Object.FindAnyObjectByType<TopBarUI>();

    private BuildingHighlighter _hover;
    private readonly List<BuildingHighlighter> _dragTargets = new();

    private bool _isDragging;
    private Vector3 _dragStartWorld;

    public bool IsPlacementState => true;

    public DeleteState(
        RaycastController raycast,
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        PlacementStateMachine fsm,
        CellIndicatorController indicator,
        PlacementActions actions,
        MoneyService money)
    {
        _raycast = raycast;
        _grid = grid;
        _finalizer = finalizer;
        _fsm = fsm;
        _indicator = indicator;
        _actions = actions;
        _money = money;
    }

    public void OnEnter()
    {
        _raycast.EnableRay();
        _indicator.UseDeleteMode();

        _isDragging = false;
        _dragTargets.Clear();
        ClearHover();

        Object.FindAnyObjectByType<TopBarUI>().SetState(GetType().Name);

        _fsm.OnHistoryChanged += OnHistoryChanged;
    }

    public void OnExit()
    {
        _raycast.DisableRay();
        _indicator.UseBuildMode();

        ClearHover();
        ClearDragHighlights();

        _fsm.OnHistoryChanged -= OnHistoryChanged;
    }

    private void OnHistoryChanged()
    {
        // When Undo/Redo happens, forget any hover/drag state
        ClearHover();
        ClearDragHighlights();
        _indicator.ClearAll();
        _isDragging = false;
    }

    public void Tick()
    {
        _raycast.Tick();

        if (_raycast.IsPointerOverUI)
        {
            ClearHover();
            _indicator.ClearAll();
            _hoverUI.TickHover(false, null, 0, 0, Vector3.zero, null);
            return;
        }

        if (!_raycast.HasHit)
        {
            ClearHover();
            _indicator.ClearAll();
        }

        Vector3 hitPoint = _raycast.HitPoint;
        Vector2Int cell = _raycast.HitCell;
        topBarUI?.SetCell(cell.x, cell.y);

        if (_raycast.HitObject != null)
        {
            var bd = _raycast.HitObject.GetComponent<BuildingData>();
            if (bd != null)
            {
                _hoverUI.TickHover(
                    true,
                    bd.Data.objName,
                    bd.Data.cost,
                    bd.Data.hourlyCost,
                    _raycast.RawHitPoint,
                    Camera.main
                );
            }
            else
            {
                _hoverUI.TickHover(false, null, 0, 0, Vector3.zero, null);
            }
        }
        else
        {
            _hoverUI.TickHover(false, null, 0, 0, Vector3.zero, null);
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragTargets.Clear();
            _dragStartWorld = hitPoint;
        }

        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            if ((hitPoint - _dragStartWorld).sqrMagnitude > 0.05f)
            {
                _isDragging = true;
                ClearHover();
                _indicator.ClearAll();
            }
        }

        if (_isDragging)
        {
            UpdateDragDelete(hitPoint);
            return;
        }

        UpdateHoverDelete(cell);

        if (Mouse.current.leftButton.wasReleasedThisFrame && _hover != null)
        {
            var bd = _hover.GetComponent<BuildingData>();

            // IMPORTANT: Clear highlight before deleting/disabling
            ClearHover();

            // Single delete = single command
            _fsm.History.Push(new DeleteCommand(bd.gameObject, _grid, _money));

            AudioManager.Play("Delete");
            FXPool.Instance.Play("dust", bd.gameObject.transform.position);
        }
}

    private void UpdateHoverDelete(Vector2Int cell)
    {
        ClearHover();

        _indicator.ShowCell(cell);

        var objs = _grid.GetObjectsInCell(cell);
        if (objs != null && objs.Count > 0)
        {
            var obj = objs[^1].instance;
            if (obj)
            {
                _hover = obj.GetComponent<BuildingHighlighter>();
                if (_hover)
                    _hover.HighlightDelete(true);

                return;
            }
        }

        GameObject hitObj = _raycast.HitObject;
        if (hitObj != null)
        {
            var bd = hitObj.GetComponent<BuildingData>();
            if (bd != null && bd.Data != null && bd.Data.ClearsGridAfterPlacement)
            {
                _hover = hitObj.GetComponent<BuildingHighlighter>();
                if (_hover)
                    _hover.HighlightDelete(true);

                return;
            }
        }
    }

    private void ClearHover()
    {
        if (_hover)
            _hover.HighlightDelete(false);

        _hover = null;
    }

    private readonly HashSet<BuildingHighlighter> _lastDragTargets = new();

    private void UpdateDragDelete(Vector3 dragEndWorld)
    {
        Vector2Int a = _grid.WorldToCell(_dragStartWorld);
        Vector2Int b = _grid.WorldToCell(dragEndWorld);

        List<Vector2Int> footprint = GetRectangleCells(a, b);
        
        // 1. Collect new targets using Grid data instead of Physics Raycasts
        HashSet<BuildingHighlighter> newTargets = new HashSet<BuildingHighlighter>();

        foreach (var cell in footprint)
        {
            var objs = _grid.GetObjectsInCell(cell);
            if (objs != null && objs.Count > 0)
            {
                var topEntry = objs[^1];
                if (topEntry.instance != null)
                {
                    var h = topEntry.instance.GetComponent<BuildingHighlighter>();
                    if (h != null) newTargets.Add(h);
                }
            }
            
            // Check for non-grid objects (like floors that clear grid) only if absolutely necessary
            // or if they are on a specific layer. We skip raycasting every cell.
        }

        // 2. Only update highlights if the selection changed
        foreach (var h in _lastDragTargets)
        {
            if (!newTargets.Contains(h))
            {
                if (h != null) h.HighlightDelete(false);
            }
        }

        foreach (var h in newTargets)
        {
            if (!_lastDragTargets.Contains(h))
            {
                if (h != null) h.HighlightDelete(true);
            }
        }

        _lastDragTargets.Clear();
        foreach (var h in newTargets) _lastDragTargets.Add(h);
        
        _dragTargets.Clear();
        _dragTargets.AddRange(newTargets);

        _indicator.ShowCells(footprint, cell => true);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            _fsm.History.BeginBatch();
            foreach (var h in _dragTargets)
            {
                if (h != null)
                {
                    h.HighlightDelete(false);
                    var bd = h.GetComponent<BuildingData>();
                    if (bd != null)
                    {
                        _fsm.History.AddToBatch(new DeleteCommand(bd.gameObject, _grid, _money));
                        FXPool.Instance.Play("dust", h.gameObject.transform.position);
                    }
                }
            }
            _fsm.History.EndBatch();
            AudioManager.Play("Delete");

            _dragTargets.Clear();
            _lastDragTargets.Clear();
            _isDragging = false;
            _indicator.ClearAll();
        }
    }

    private void ClearDragHighlights()
    {
        foreach (var h in _dragTargets)
        {
            if (h)
                h.HighlightDelete(false);
        }

        _dragTargets.Clear();
    }

    private List<Vector2Int> GetRectangleCells(Vector2Int a, Vector2Int b)
    {
        List<Vector2Int> cells = new();

        int minX = Mathf.Min(a.x, b.x);
        int maxX = Mathf.Max(a.x, b.x);
        int minY = Mathf.Min(a.y, b.y);
        int maxY = Mathf.Max(a.y, b.y);

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                cells.Add(new Vector2Int(x, y));

        return cells;
    }
}
