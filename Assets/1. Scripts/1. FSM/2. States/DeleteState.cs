using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class DeleteState : IPlacementState
{
    #region FIELDS ****************************************
    // =========================================================
    //  DEPENDENCIES
    // =========================================================
    private readonly RaycastController _raycast;
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementStateMachine _fsm;
    private readonly CellIndicatorController _indicator;
    private readonly PlacementActions _actions;

    // =========================================================
    //  HOVER + DRAG STATE
    // =========================================================
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
        PlacementActions actions)
    {
        _raycast = raycast;
        _grid = grid;
        _finalizer = finalizer;
        _fsm = fsm;
        _indicator = indicator;
        _actions = actions;
        _actions.BuildPlacement.BindCancelTo_RMB();
    }
#endregion

    // =========================================================
    //  ENTER / EXIT
    // =========================================================
    public void OnEnter()
    {
        _actions.BuildPlacement.Cancel.performed += OnCancelDelete;
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
        _actions.BuildPlacement.Cancel.performed -= OnCancelDelete;
    }
    // =========================================================
    //  MAIN LOOP
    // =========================================================
    public void Tick()
    {
        _raycast.Tick();

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            ClearHover();
            ClearDragHighlights();
            _fsm.EnterIdle();
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
            _fsm.History.Push(new DeleteCommand(bd.gameObject, _grid));
            AudioManager.Play("Delete");
            FXPool.Instance.Play("dust", bd.gameObject.transform.position); 
            _hover = null;
        }
    }

    // =========================================================
    //  HOVER DELETE
    // =========================================================
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

    // =========================================================
    //  DRAG DELETE
    // =========================================================
    private void UpdateDragDelete(Vector3 dragEndWorld)
    {
        ClearDragHighlights();

        Vector2Int a = _grid.WorldToCell(_dragStartWorld);
        Vector2Int b = _grid.WorldToCell(dragEndWorld);

        List<Vector2Int> footprint = GetRectangleCells(a, b);

        foreach (var cell in footprint)
        {
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
            {
                var bd = h.GetComponent<BuildingData>();
                _fsm.History.Push(new DeleteCommand(bd.gameObject, _grid));
                // Play FX at the center of each cell in the footprint for better visual feedback
                FXPool.Instance.Play("dust", h.gameObject.transform.position);
            }

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
    private void OnCancelDelete(InputAction.CallbackContext ctx)
    {
        AudioManager.Play("Cancel");

        _indicator.ClearAll();
        _raycast.DisableRay();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _fsm.EnterIdle();
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
