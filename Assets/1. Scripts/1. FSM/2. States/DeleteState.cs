using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class DeleteState : IPlacementState
{
    private readonly RaycastController _raycast;
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementStateMachine _fsm;

    private BuildingHighlighter _hover;
    private readonly List<BuildingHighlighter> _dragTargets = new();

    private bool _isDragging;
    private Vector3 _dragStartWorld;

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
        _dragTargets.Clear();
    }

    public void Tick()
    {
        _raycast.Tick();

        // Right-click = exit delete mode
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            ClearHover();
            ClearDragHighlights();
            _raycast.DisableRay();
            _fsm.SetState(_fsm.IdleState);
            return;
        }

        if (!_raycast.HasHit)
        {
            ClearHover();
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
            _dragTargets.Clear();
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
                ClearHover();
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
        if (Mouse.current.leftButton.wasReleasedThisFrame && _hover != null)
        {
            DeleteObject(_hover);
            _hover = null;
        }
    }

    public void OnExit()
    {
        ClearHover();
        ClearDragHighlights();
        _raycast.DisableRay();
    }

    // =========================================================
    // HOVER DELETE
    // =========================================================
    private void UpdateHoverDelete(Vector2Int cell)
    {
        ClearHover();

        var objs = _grid.GetObjectsInCell(cell);
        if (objs == null || objs.Count == 0)
            return;

        var obj = objs[^1].instance; // top of stack
        if (!obj)
            return;

        _hover = obj.GetComponent<BuildingHighlighter>();
        if (_hover)
            _hover.HighlightDelete(true);
    }

    private void ClearHover()
    {
        if (_hover)
            _hover.HighlightDelete(false);

        _hover = null;
    }

    // =========================================================
    // DRAG DELETE
    // =========================================================
    private void UpdateDragDelete(Vector3 dragEndWorld)
    {
        ClearDragHighlights();

        Bounds area = MakeBounds(_dragStartWorld, dragEndWorld);

        // Scan all cells in grid
        for (int x = 0; x < _grid.Width; x++)
        {
            for (int y = 0; y < _grid.Height; y++)
            {
                Vector2Int cell = new(x, y);
                Vector3 center = _grid.GetCellCenter(cell);

                if (!area.Contains(center))
                    continue;

                var objs = _grid.GetObjectsInCell(cell);
                if (objs == null)
                    continue;

                foreach (var entry in objs)
                {
                    // Skip destroyed objects
                    if (entry.instance == null)
                        continue;

                    var h = entry.instance.GetComponent<BuildingHighlighter>();
                    if (h == null)
                        continue;

                    h.HighlightDelete(true);
                    _dragTargets.Add(h);

                    // highlight the cell visual too
                    _grid.HighlightCellForDelete(cell);
                }

            }
        }

        // Release = delete all
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            foreach (var h in _dragTargets)
                DeleteObject(h);

            _dragTargets.Clear();
            _isDragging = false;
        }

    
    }

    private void ClearDragHighlights()
    {
        foreach (var h in _dragTargets)
        {
            if (h)
                h.HighlightDelete(false);

            // restore all footprint cells
            var data = h.GetComponent<BuildingData>();
            if (data)
            {
                Vector2Int root = _grid.WorldToCell(h.transform.position);
                float rot = h.transform.eulerAngles.y;
                var offsets = data.Data.GetFootprintOffsets(-rot);

                foreach (var o in offsets)
                    _grid.RestoreCellVisual(root + o);
            }


        }
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
    // Optimized version that clears grid occupancy before deleting, to prevent lingering "ghost" objects in the grid
    private void DeleteObject(BuildingHighlighter h)
    {
        if (!h)
            return;

        var data = h.GetComponent<BuildingData>();
        if (!data)
            return;

        // 1. Get the root cell
        Vector2Int root = _grid.WorldToCell(h.transform.position);

        // 2. Get rotation (stored on the placed object)
        float rotation = h.transform.eulerAngles.y;

        // 3. Get footprint offsets
        Vector2Int[] offsets = data.Data.GetFootprintOffsets(-rotation);

        // 4. Clear ALL occupied cells
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            _grid.ClearCell(cell, false);
            _grid.RemoveCellVisual(cell);
        }

        // 5. Dust poof
        _finalizer.SpawnDust(h.transform.position);

        // 6. Destroy object
        data.Delete();
    }
}
