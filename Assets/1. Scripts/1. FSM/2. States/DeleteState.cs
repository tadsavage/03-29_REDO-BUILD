using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class DeleteState : IPlacementState
{
    private readonly RaycastController _raycast;
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementStateMachine _fsm;

    private bool _isDragging;
    private Vector3 _dragStartWorld;

    // Diff-based highlight tracking
    private readonly HashSet<BuildingHighlighter> _currentHighlights = new();
    private readonly HashSet<BuildingHighlighter> _newHighlights = new();

    public bool IsPlacementState => true;

    public DeleteState(
        RaycastController raycast,
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        PlacementStateMachine fsm)
    {
        _raycast = raycast;
        _grid = grid;
        _finalizer = finalizer;
        _fsm = fsm;
    }

    public void OnEnter()
    {
        _raycast.EnableRay();
        _isDragging = false;
        _currentHighlights.Clear();
    }

    public void Tick()
    {
        _raycast.Tick();

        // Right-click = exit delete mode
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            ClearAllHighlights();
            _raycast.DisableRay();
            _fsm.SetState(_fsm.IdleState);
            return;
        }

        if (!_raycast.HasHit)
        {
            ClearAllHighlights();
            return;
        }

        Vector3 hitPoint = _raycast.HitPoint;
        Vector2Int cell = _raycast.HitCell;

        // -----------------------------
        // BEGIN DRAG
        // -----------------------------
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragStartWorld = hitPoint;
        }

        // -----------------------------
        // CONFIRM DRAG
        // -----------------------------
        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            if ((hitPoint - _dragStartWorld).sqrMagnitude > 0.05f)
            {
                _isDragging = true;
                ClearAllHighlights();
            }
        }

        // -----------------------------
        // DRAG DELETE MODE
        // -----------------------------
        if (_isDragging)
        {
            UpdateDragDelete(hitPoint);
            return;
        }

        // -----------------------------
        // HOVER DELETE MODE
        // -----------------------------
        UpdateHoverDelete(cell);

        // -----------------------------
        // SINGLE CLICK DELETE
        // -----------------------------
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (_currentHighlights.Count > 0)
            {
                foreach (var h in _currentHighlights)
                    DeleteObject(h);

                _finalizer.SpawnDust(hitPoint);
                ClearAllHighlights();
            }
        }
    }

    public void OnExit()
    {
        ClearAllHighlights();
        _raycast.DisableRay();
    }

    // =========================================================
    // HOVER DELETE (single object)
    // =========================================================
    private void UpdateHoverDelete(Vector2Int cell)
    {
        ClearAllHighlights();

        var objs = _grid.GetObjectsInCell(cell);
        if (objs == null || objs.Count == 0)
            return;

        var obj = objs[^1].instance;
        if (!obj)
            return;

        var h = obj.GetComponent<BuildingHighlighter>();
        if (h)
        {
            h.HighlightDelete(true);
            _currentHighlights.Add(h);
        }
    }

    // =========================================================
    // DRAG DELETE (optimized)
    // =========================================================
    private void UpdateDragDelete(Vector3 dragEndWorld)
    {
        _newHighlights.Clear();

        // Compute drag rectangle bounds
        Bounds area = MakeBounds(_dragStartWorld, dragEndWorld);

        // Convert world bounds → grid bounds
        Vector2Int min = _grid.WorldToCell(area.min);
        Vector2Int max = _grid.WorldToCell(area.max);

        // Clamp to grid
        min.x = Mathf.Clamp(min.x, 0, _grid.Width - 1);
        min.y = Mathf.Clamp(min.y, 0, _grid.Height - 1);
        max.x = Mathf.Clamp(max.x, 0, _grid.Width - 1);
        max.y = Mathf.Clamp(max.y, 0, _grid.Height - 1);

        // Scan ONLY the drag rectangle
        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                Vector2Int cell = new(x, y);
                var objs = _grid.GetObjectsInCell(cell);
                if (objs == null)
                    continue;

                foreach (var entry in objs)
                {
                    if (!entry.instance)
                        continue;

                    var h = entry.instance.GetComponent<BuildingHighlighter>();
                    if (h == null)
                        continue;

                    _newHighlights.Add(h);
                }
            }
        }

        // Diff-based highlight update
        foreach (var h in _currentHighlights)
            if (!_newHighlights.Contains(h))
                h.HighlightDelete(false);

        foreach (var h in _newHighlights)
            if (!_currentHighlights.Contains(h))
                h.HighlightDelete(true);

        // Swap sets
        _currentHighlights.Clear();
        foreach (var h in _newHighlights)
            _currentHighlights.Add(h);

        // Release = delete all
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            foreach (var h in _currentHighlights)
            {
                // Spawn dust at each object BEFORE deleting
                _finalizer.SpawnDust(h.transform.position);

                DeleteObject(h);
            }

            _currentHighlights.Clear();
            _isDragging = false;
        }
    }

    private void ClearAllHighlights()
    {
        foreach (var h in _currentHighlights)
            if (h) h.HighlightDelete(false);

        _currentHighlights.Clear();
    }

    private Bounds MakeBounds(Vector3 a, Vector3 b)
    {
        Vector3 center = (a + b) * 0.5f;
        Vector3 size = new(
            Mathf.Abs(a.x - b.x),
            10f,
            Mathf.Abs(a.z - b.z)
        );
        return new Bounds(center, size);
    }

    // =========================================================
    // DELETE OBJECT
    // =========================================================
    private void DeleteObject(BuildingHighlighter h)
    {
        if (!h)
            return;

        var data = h.GetComponent<BuildingData>();
        if (!data)
            return;

        Vector2Int root = _grid.WorldToCell(h.transform.position);
        float rotation = h.transform.eulerAngles.y;

        Vector2Int[] offsets = data.Data.GetFootprintOffsets(-rotation);

        // Clear all occupied cells
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            _grid.ClearCell(cell, false);
            _grid.RemoveCellVisual(cell);
        }

        data.Delete();
    }
}
