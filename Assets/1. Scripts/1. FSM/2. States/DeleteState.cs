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
        _indicator.UseDeleteMode();

        _isDragging = false;
        _dragTargets.Clear();
        ClearHover();
    }

    public void OnExit()
    {
        _raycast.DisableRay();
        _indicator.UseBuildMode();

        ClearHover();
        ClearDragHighlights();
    }

    // =========================================================
    //  MAIN LOOP
    // =========================================================
    public void Tick()
    {
        _raycast.Tick();

        // Right‑click = exit delete mode
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

        // =====================================================
        //  BEGIN DRAG
        // =====================================================
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragTargets.Clear();
            _dragStartWorld = hitPoint;
        }

        // =====================================================
        //  CONFIRM DRAG
        // =====================================================
        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            if ((hitPoint - _dragStartWorld).sqrMagnitude > 0.05f)
            {
                _isDragging = true;
                ClearHover();
                _indicator.ClearAll();
            }
        }

        // =====================================================
        //  DRAG DELETE MODE
        // =====================================================
        if (_isDragging)
        {
            UpdateDragDelete(hitPoint);
            return;
        }

        // =====================================================
        //  HOVER DELETE MODE
        // =====================================================
        UpdateHoverDelete(cell);

        // =====================================================
        //  SINGLE CLICK DELETE
        // =====================================================
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
    private void UpdateHoverDelete(Vector2Int cell)
    {
        ClearHover();

        // Always show 1×1 tile in hover mode
        _indicator.ShowCell(cell);

        var objs = _grid.GetObjectsInCell(cell);
        if (objs == null || objs.Count == 0)
            return;

        var obj = objs[^1].instance;
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
    //  DRAG DELETE
    // =========================================================
    private void UpdateDragDelete(Vector3 dragEndWorld)
    {
        ClearDragHighlights();

        // Build grid‑aligned rectangle
        Vector2Int a = _grid.WorldToCell(_dragStartWorld);
        Vector2Int b = _grid.WorldToCell(dragEndWorld);

        int minX = Mathf.Min(a.x, b.x);
        int maxX = Mathf.Max(a.x, b.x);
        int minY = Mathf.Min(a.y, b.y);
        int maxY = Mathf.Max(a.y, b.y);

        List<Vector2Int> footprint = new();

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int cell = new(x, y);
                footprint.Add(cell);

                var objs = _grid.GetObjectsInCell(cell);
                if (objs == null || objs.Count == 0)
                    continue;

                var obj = objs[^1].instance;
                if (!obj)
                    continue;

                var h = obj.GetComponent<BuildingHighlighter>();
                if (h == null)
                    continue;

                if (!_dragTargets.Contains(h))
                    _dragTargets.Add(h);

                h.HighlightDelete(true);
            }
        }

        // Show faint grid footprint
        _indicator.ShowCells(footprint);

        // Release = delete all
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
    //  DELETE OBJECT
    // =========================================================
    private void DeleteObject(BuildingHighlighter h)
    {
        if (!h)
            return;

        var data = h.GetComponent<BuildingData>();
        if (!data || data.Data == null)
            return;

        Vector2Int root = _grid.WorldToCell(h.transform.position);
        float rotation = h.transform.eulerAngles.y;

        Vector2Int[] offsets = data.Data.GetFootprintOffsets(-rotation);

        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null)
                continue;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].instance == data.gameObject)
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

        data.Delete();
    }
}
