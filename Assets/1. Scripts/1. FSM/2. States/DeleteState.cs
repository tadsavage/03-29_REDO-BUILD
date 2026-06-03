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
    private WorldHoverPopupUI _hoverUI;
    private TopBarUI _topBarUI;

    private readonly float _destructionDuration;
    private readonly float _destructionSinkAmount;
    private readonly float _destructionVibrationAmount;
    private readonly float _destructionVibrationSpeed;

    private TopBarUI topBarUI => _topBarUI != null ? _topBarUI : _topBarUI = Object.FindAnyObjectByType<TopBarUI>();
    private WorldHoverPopupUI hoverUI => _hoverUI != null ? _hoverUI : _hoverUI = Object.FindAnyObjectByType<WorldHoverPopupUI>();

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
        MoneyService money,
        float destructionDuration,
        float destructionSinkAmount,
        float destructionVibrationAmount,
        float destructionVibrationSpeed)
    {
        _raycast = raycast;
        _grid = grid;
        _finalizer = finalizer;
        _fsm = fsm;
        _indicator = indicator;
        _actions = actions;
        _money = money;
        _destructionDuration = destructionDuration;
        _destructionSinkAmount = destructionSinkAmount;
        _destructionVibrationAmount = destructionVibrationAmount;
        _destructionVibrationSpeed = destructionVibrationSpeed;
    }

    public void OnEnter()
    {
        BuildModeOverride.Instance?.Activate();

        _raycast.EnableRay();
        _indicator.UseDeleteMode();

        _isDragging = false;
        _dragTargets.Clear();
        ClearHover();

        topBarUI?.SetState(GetType().Name);

        _fsm.OnHistoryChanged += OnHistoryChanged;
    }

    public void OnExit()
    {
        BuildModeOverride.Instance?.Deactivate();

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
            hoverUI?.TickHover(false, null, 0, 0, Vector3.zero, null);
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
                hoverUI?.TickHover(
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
                hoverUI?.TickHover(false, null, 0, 0, Vector3.zero, null);
            }
        }
        else
        {
            hoverUI?.TickHover(false, null, 0, 0, Vector3.zero, null);
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
            _fsm.History.Push(new DeleteCommand(bd.gameObject, _grid, _money, _destructionDuration, _destructionSinkAmount, _destructionVibrationAmount, _destructionVibrationSpeed));

            AudioManager.Play("Delete");
        }
}

    private void UpdateHoverDelete(Vector2Int cell)
    {
        _indicator.ShowCell(cell);

        BuildingHighlighter newHover = null;

        // RESPONSIVENESS FIX: Check HitObject directly first (like MoveState)
        if (_raycast.HitObject != null)
        {
            var bd = _raycast.HitObject.GetComponentInParent<BuildingData>();
            if (bd != null && bd.Data != null)
            {
                // Floor tiles cannot be deleted — redirect to the Foundation in the same cell
                if (bd.Data.isFloor)
                    bd = FindFoundationInCell(cell);

                if (bd != null)
                    newHover = bd.GetComponent<BuildingHighlighter>();
            }
        }

        // Fallback to grid lookup for safety
        if (newHover == null)
        {
            var objs = _grid.GetObjectsInCell(cell);
            if (objs != null && objs.Count > 0)
            {
                // Walk from top down; skip floor tiles
                for (int i = objs.Count - 1; i >= 0; i--)
                {
                    var entry = objs[i];
                    if (entry.data != null && entry.data.isFloor) continue;
                    if (entry.instance != null)
                    {
                        newHover = entry.instance.GetComponent<BuildingHighlighter>();
                        break;
                    }
                }
            }
        }

        if (newHover != _hover)
        {
            ClearHover();
            _hover = newHover;
            if (_hover)
                _hover.HighlightDelete(true);
        }
    }

    // Returns the BuildingData for the Foundation in the given cell, or null if none.
    private BuildingData FindFoundationInCell(Vector2Int cell)
    {
        var objs = _grid.GetObjectsInCell(cell);
        if (objs == null) return null;
        foreach (var entry in objs)
        {
            if (entry.data?.category == "Foundation" && entry.instance != null)
                return entry.instance.GetComponent<BuildingData>();
        }
        return null;
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
                // Walk from top down; floor tiles cannot be deleted so skip them
                for (int i = objs.Count - 1; i >= 0; i--)
                {
                    var entry = objs[i];
                    if (entry.data != null && entry.data.isFloor) continue;
                    if (entry.instance != null)
                    {
                        var h = entry.instance.GetComponent<BuildingHighlighter>();
                        if (h != null) { newTargets.Add(h); break; }
                    }
                }
            }
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
                        _fsm.History.AddToBatch(new DeleteCommand(bd.gameObject, _grid, _money, _destructionDuration, _destructionSinkAmount, _destructionVibrationAmount, _destructionVibrationSpeed));
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
