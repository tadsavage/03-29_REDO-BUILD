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
    private Vector2Int _lastHoverCell = new Vector2Int(int.MinValue, int.MinValue);

    private bool _isDragging;
    private Vector3 _dragStartWorld;

    public bool IsPlacementState => true;

    private readonly PreviewController _preview;

    public DeleteState(
        RaycastController raycast,
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        PlacementStateMachine fsm,
        PreviewController preview)
    {
        _raycast = raycast;
        _grid = grid;
        _finalizer = finalizer;
        _fsm = fsm;
        _preview = preview;
    }

    public void OnEnter()
    {
        _preview.SetDeleteMode(true);
        _raycast.EnableRay();
        _isDragging = false;
        _dragTargets.Clear();
        ClearHover();
    }

    public void OnExit()
    {
        _preview.SetDeleteMode(false);
        ClearHover();
        ClearDragHighlights();
        _raycast.DisableRay();
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

        // BEGIN DRAG
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragTargets.Clear();
            _dragStartWorld = hitPoint;
        }

        // CONFIRM DRAG
        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            if ((hitPoint - _dragStartWorld).sqrMagnitude > 0.05f)
            {
                _isDragging = true;
                ClearHover();
            }
        }

        // DRAG DELETE MODE
        if (_isDragging)
        {
            UpdateDragDelete(hitPoint);
            return;
        }

        // HOVER DELETE MODE
        UpdateHoverDelete(cell);

        // SINGLE CLICK DELETE
        if (Mouse.current.leftButton.wasReleasedThisFrame && _hover != null)
        {
            DeleteObject(_hover);

            // Play delete sound ONCE for single click
            AudioManager.Play("Delete");

            _hover = null;
        }
    }

    // =========================================================
    // HOVER DELETE (top of stack)
    // =========================================================
    private void UpdateHoverDelete(Vector2Int cell)
    {
        ClearHover();

        // Highlight cell visual
        _grid.HighlightCellForDelete(cell);

        // Show delete ghost
        ShowDeleteGhost(cell);

        // Highlight top object
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

        if (_lastHoverCell != new Vector2Int(int.MinValue, int.MinValue))
            _grid.RestoreCellVisual(_lastHoverCell);
    }


    // =========================================================
    // DRAG DELETE (rectangle, top-of-stack per cell)
    // =========================================================
    private void UpdateDragDelete(Vector3 dragEndWorld)
    {
        _preview.Hide();

        // Keep NewCell audio active during drag
        var _ = _raycast.HitCell;

        ClearDragHighlights();

        Bounds area = MakeBounds(_dragStartWorld, dragEndWorld);

        // Convert world bounds → grid bounds (clamped)
        Vector2Int min = _grid.WorldToCell(area.min);
        Vector2Int max = _grid.WorldToCell(area.max);

        min.x = Mathf.Clamp(min.x, 0, _grid.Width - 1);
        min.y = Mathf.Clamp(min.y, 0, _grid.Height - 1);
        max.x = Mathf.Clamp(max.x, 0, _grid.Width - 1);
        max.y = Mathf.Clamp(max.y, 0, _grid.Height - 1);

        // Scan only the drag rectangle
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

                // Only top of stack for this cell
                var obj = objs[^1].instance;
                if (!obj)
                    continue;

                var h = obj.GetComponent<BuildingHighlighter>();
                if (h == null)
                    continue;

                if (!_dragTargets.Contains(h))
                    _dragTargets.Add(h);

                h.HighlightDelete(true);
                _grid.HighlightCellForDelete(cell);
            }
        }

        // Release = delete all
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            foreach (var h in _dragTargets)
                DeleteObject(h);

            // Play delete sound ONCE for the whole drag
            AudioManager.Play("Delete");

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
    // DELETE OBJECT (top of stack, multi-cell footprint)
    // =========================================================
    private void DeleteObject(BuildingHighlighter h)
    {
        if (!h)
            return;

        var data = h.GetComponent<BuildingData>();
        if (!data || data.Data == null)
            return;

        // Root cell + rotation
        Vector2Int root = _grid.WorldToCell(h.transform.position);
        float rotation = h.transform.eulerAngles.y;

        // Footprint offsets
        Vector2Int[] offsets = data.Data.GetFootprintOffsets(-rotation);

        // Remove this instance from each footprint cell's stack
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null)
                continue;

            // Remove only this instance from the stack
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].instance == data.gameObject)
                    list.RemoveAt(i);
            }

            // If nothing left in this cell, clear visual
            if (list.Count == 0)
                _grid.RemoveCellVisual(cell);
        }

        // Dust poof for each footprint cell
        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            Vector3 pos = _grid.GetCellCenter(cell);
            _finalizer.SpawnDust(pos);
        }

        // Handled by Caller after this method returns - the below ias spam
        //AudioManager.Play("Delete");

        // Destroy object
        data.Delete();
    }
    private bool _deleteMode = false;

    public void SetDeleteMode(bool on)
    {
        _deleteMode = on;
    }
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

        // Compute correct height (stack-aware)
        float stackY = _grid.GetStackHeight(cell);
        Vector3 pos = _grid.GetCellCenter(cell);
        pos.y += stackY;

        // Show ghost using the prefab of the object to be deleted
        pos = obj.transform.position; // EXACT object position
        _preview.Show(data.Data);
        _preview.SetGhostDelete();
        _preview.MoveTo(pos, cell, data.Data);
    }

}
