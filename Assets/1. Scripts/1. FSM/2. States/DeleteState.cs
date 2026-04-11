using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class DeleteState : IPlacementState
{
    // =========================================================
    //  DEPENDENCIES
    // =========================================================
    private readonly RaycastController _raycast;
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementStateMachine _fsm;
    private readonly CellIndicatorController _indicator;

    // =========================================================
    //  HOVER + DRAG STATE
    // =========================================================
    private BuildingHighlighter _hover;                 // REM: object currently highlighted under cursor
    private readonly List<BuildingHighlighter> _dragTargets = new(); // REM: all objects highlighted during drag rectangle

    private bool _isDragging;                           // REM: true once drag threshold passed
    private Vector3 _dragStartWorld;                    // REM: world position where drag began

    public bool IsPlacementState => true;

    public DeleteState(
        RaycastController raycast,
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        PlacementStateMachine fsm,
        CellIndicatorController indicator)
    {
        _raycast = raycast;
        _grid = grid;
        _finalizer = finalizer;
        _fsm = fsm;
        _indicator = indicator;
    }

    // =========================================================
    //  ENTER / EXIT
    // =========================================================
    public void OnEnter()
    {
        _raycast.EnableRay();
        _indicator.UseDeleteMode();     // REM: yellow tiles for delete mode

        _isDragging = false;
        _dragTargets.Clear();
        ClearHover();
    }

    public void OnExit()
    {
        _raycast.DisableRay();
        _indicator.UseBuildMode();      // REM: restore build colors

        ClearHover();
        ClearDragHighlights();
    }

    // =========================================================
    //  MAIN LOOP
    // =========================================================
    public void Tick()
    {
        _raycast.Tick();

        // -----------------------------------------------------
        // RIGHT‑CLICK = exit delete mode
        // -----------------------------------------------------
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            ClearHover();
            ClearDragHighlights();
            _fsm.SetState(_fsm.IdleState);
            return;
        }

        if (!_raycast.HasHit)
        {
            ClearHover();
            _indicator.ClearAll();
            return;
        }

        Vector3 hitPoint = _raycast.HitPoint;
        Vector2Int cell = _raycast.HitCell;

        // -----------------------------------------------------
        // BEGIN DRAG
        // -----------------------------------------------------
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragTargets.Clear();
            _dragStartWorld = hitPoint;
        }

        // -----------------------------------------------------
        // CONFIRM DRAG (threshold)
        // -----------------------------------------------------
        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            if ((hitPoint - _dragStartWorld).sqrMagnitude > 0.05f)
            {
                _isDragging = true;
                ClearHover();
                _indicator.ClearAll();
            }
        }

        // -----------------------------------------------------
        // DRAG DELETE MODE
        // -----------------------------------------------------
        if (_isDragging)
        {
            UpdateDragDelete(hitPoint);
            return;
        }

        // -----------------------------------------------------
        // HOVER DELETE MODE
        // -----------------------------------------------------
        UpdateHoverDelete(cell);

        // -----------------------------------------------------
        // SINGLE CLICK DELETE
        // -----------------------------------------------------
        if (Mouse.current.leftButton.wasReleasedThisFrame && _hover != null)
        {
            DeleteObject(_hover);
            AudioManager.Play("Delete");
            _hover = null;
        }
    }

    // =========================================================
    //  HOVER DELETE
    // =========================================================
    // =========================================================
    //  HOVER DELETE
    // =========================================================
    private void UpdateHoverDelete(Vector2Int cell)
    {
        ClearHover();

        _indicator.ShowCell(cell);

        // 1. GRID OBJECTS
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

        // 2. FREE OBJECTS (via HitObject)
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
    // REM: clear hover highlight if we move to a new cell or exit delete mode
    private void ClearHover()
    {
        if (_hover)
            _hover.HighlightDelete(false);

        _hover = null;
    }

    /// =========================================================
    //  DRAG DELETE
    // =========================================================
    private void UpdateDragDelete(Vector3 dragEndWorld)
    {
        ClearDragHighlights();

        Vector2Int a = _grid.WorldToCell(_dragStartWorld);
        Vector2Int b = _grid.WorldToCell(dragEndWorld);

        List<Vector2Int> footprint = _grid.GetRectangleCells(a, b);

        foreach (var cell in footprint)
        {
            // GRID OBJECTS
            var objs = _grid.GetObjectsInCell(cell);
            if (objs != null && objs.Count > 0)
            {
                var obj = objs[^1].instance;
                if (obj)
                {
                    var h = obj.GetComponent<BuildingHighlighter>();
                    if (h != null)
                    {
                        if (!_dragTargets.Contains(h))
                            _dragTargets.Add(h);

                        h.HighlightDelete(true);
                    }
                }
            }

            // FREE OBJECTS
            var hitObj = _raycast.RaycastCellCenter(cell);
            if (hitObj != null)
            {
                var bd = hitObj.GetComponent<BuildingData>();
                if (bd != null && bd.Data != null && bd.Data.ClearsGridAfterPlacement)
                {
                    var h = hitObj.GetComponent<BuildingHighlighter>();
                    if (h != null)
                    {
                        if (!_dragTargets.Contains(h))
                            _dragTargets.Add(h);

                        h.HighlightDelete(true);
                    }
                }
            }
        }

        _indicator.ShowCells(footprint);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            foreach (var h in _dragTargets)
                DeleteObject(h);

            AudioManager.Play("Delete");

            _dragTargets.Clear();
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

    // =========================================================
    //  DELETE OBJECT (grid + free objects)
    // =========================================================
    private void DeleteObject(BuildingHighlighter h)
    {
        if (!h)
            return;

        var bd = h.GetComponent<BuildingData>();
        if (bd == null || bd.Data == null)
            return;

        // =====================================================
        // 1. FREE OBJECTS (ClearsGridAfterPlacement)
        // =====================================================
        if (bd.Data.ClearsGridAfterPlacement)
        {
            bd.Delete();
            return;
        }

        // =====================================================
        // 2. GRID OBJECTS (existing logic)
        // =====================================================
        Vector2Int root = _grid.WorldToCell(h.transform.position);
        float rotation = h.transform.eulerAngles.y;

        Vector2Int[] offsets = bd.Data.GetFootprintOffsets(-rotation);

        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null)
                continue;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].instance == bd.gameObject)
                    list.RemoveAt(i);
            }

            if (list.Count == 0)
                _grid.RemoveCellVisual(cell);
        }

        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            Vector3 pos = _grid.GetCellCenter(cell);
            _finalizer.SpawnDust(pos);
        }

        bd.Delete();
    }
}
