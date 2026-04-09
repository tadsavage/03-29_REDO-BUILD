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
    private readonly PreviewController _preview;
    private readonly CellIndicatorController _indicator;

    // =========================================================
    //  HOVER / DRAG STATE
    // =========================================================
    private BuildingHighlighter _hover;
    private readonly List<BuildingHighlighter> _dragTargets = new();

    private bool _isDragging;
    private Vector3 _dragStartWorld;

    public bool IsPlacementState => true;

    // =========================================================
    //  CONSTRUCTOR
    // =========================================================
    public DeleteState(
        RaycastController raycast,
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        PlacementStateMachine fsm,
        PreviewController preview,
        CellIndicatorController indicator)
    {
        _raycast = raycast;
        _grid = grid;
        _finalizer = finalizer;
        _fsm = fsm;
        _preview = preview;
        _indicator = indicator;
    }

    // =========================================================
    //  ENTER / EXIT
    // =========================================================
    public void OnEnter()
    {
        // REM: kill any build ghosts before entering delete
        _preview.Hide();
        _preview.ClearAllGhosts();
        _preview.SetDeleteMode(true);

        _raycast.EnableRay();

        _indicator.SetDeleteMode(true);
        _indicator.ClearAll();

        _isDragging = false;
        _dragTargets.Clear();
        ClearHover();
    }

    public void OnExit()
    {
        _preview.SetDeleteMode(false);
        _preview.ClearAllGhosts();

        _raycast.DisableRay();

        _indicator.SetDeleteMode(false);
        _indicator.ClearAll();

        ClearHover();
        ClearDragHighlights();
    }

    // =========================================================
    //  MAIN UPDATE LOOP
    // =========================================================
    public void Tick()
    {
        _raycast.Tick();

        // -----------------------------------------------------
        // RIGHT‑CLICK: exit delete mode
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

        // REM: always show a yellow cell indicator in delete mode
        if (!_isDragging)
        {
            _indicator.ClearAll();
            _indicator.ShowCell(cell, true);   // validity ignored in delete mode
        }

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
        // CONFIRM DRAG
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
            AudioManager.Play("Delete");   // REM: single click delete sound
            _hover = null;
        }
    }

    // =========================================================
    //  HOVER DELETE (top of stack)
    // =========================================================
    private void UpdateHoverDelete(Vector2Int cell)
    {
        ClearHover();

        // REM: show delete ghost + highlight top object if present
        ShowDeleteGhost(cell);

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
        _preview.Hide();

        if (_hover)
            _hover.HighlightDelete(false);

        _hover = null;
    }

    // =========================================================
    //  DRAG DELETE (rectangle, top‑of‑stack per cell)
    // =========================================================
    private void UpdateDragDelete(Vector3 dragEndWorld)
    {
        _preview.Hide();
        ClearDragHighlights();

        Bounds area = MakeBounds(_dragStartWorld, dragEndWorld);

        Vector2Int min = _grid.WorldToCell(area.min);
        Vector2Int max = _grid.WorldToCell(area.max);

        min.x = Mathf.Clamp(min.x, 0, _grid.Width - 1);
        min.y = Mathf.Clamp(min.y, 0, _grid.Height - 1);
        max.x = Mathf.Clamp(max.x, 0, _grid.Width - 1);
        max.y = Mathf.Clamp(max.y, 0, _grid.Height - 1);

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                Vector2Int cell = new(x, y);
                Vector3 center = _grid.GetCellCenter(cell);

                if (!area.Contains(center))
                    continue;

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
                _grid.HighlightCellForDelete(cell);   // REM: grid overlay for drag area
            }
        }

        // Release = delete all
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            foreach (var h in _dragTargets)
                DeleteObject(h);

            AudioManager.Play("Delete");   // REM: one sound for whole drag

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

            var data = h ? h.GetComponent<BuildingData>() : null;
            if (data && data.Data != null)
            {
                Vector2Int root = _grid.WorldToCell(h.transform.position);
                float rot = h.transform.eulerAngles.y;
                var offsets = data.Data.GetFootprintOffsets(-rot);

                foreach (var o in offsets)
                    _grid.RestoreCellVisual(root + o);
            }
        }

        _dragTargets.Clear();
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
    //  DELETE OBJECT (top of stack, multi‑cell footprint)
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

    // =========================================================
    //  DELETE GHOST (visual preview of target)
    // =========================================================
    private void ShowDeleteGhost(Vector2Int cell)
    {
        var objs = _grid.GetObjectsInCell(cell);
        if (objs == null || objs.Count == 0)
        {
            _preview.Hide();
            return;
        }

        var obj = objs[^1].instance;
        if (!obj)
        {
            _preview.Hide();
            return;
        }

        var data = obj.GetComponent<BuildingData>();
        if (!data || data.Data == null)
        {
            _preview.Hide();
            return;
        }

        Vector3 pos = obj.transform.position;

        // REM: reuse single ghost as delete ghost; deleteMode disables fly‑in / smoothing
        _preview.Show(data.Data);
        _preview.MoveTo(pos, cell, data.Data);
    }
}
