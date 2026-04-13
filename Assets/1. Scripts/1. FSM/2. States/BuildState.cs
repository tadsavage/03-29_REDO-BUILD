using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using static UnityEditor.PlayerSettings;

public class BuildState : IPlacementState
{
    #region FIELDS ****************************************
    // =========================================================
    //  DEPENDENCIES
    // =========================================================
    private readonly RaycastController _raycast;
    private readonly CellIndicatorController _indicator;

    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementGrid _grid;
    private readonly PlacementStateMachine _fsm;

    // =========================================================
    //  BUILD DATA
    // =========================================================
    private ObjDataSO _currentData;

    private bool _placeRequested;
    private bool _rotateRequested;
    private float _currentRotation;

    private Vector2Int[] _currentOffsets;
    private float _lastRotation;

    // =========================================================
    //  DRAG PLACEMENT
    // =========================================================
    private bool _isDragging;
    private Vector2Int _dragStartCell;
    private readonly List<Vector2Int> _dragCells = new();

    // Reusable buffers (no per-frame allocations)
    private readonly List<Vector2Int> _indicatorBuffer = new();
    private readonly List<Vector2Int> _footprintBuffer = new();

    // =========================================================
    //  ANTI-FLICKER
    // =========================================================
    private Vector2Int _lastPlacedCell;
    private bool _justPlaced;

    // =========================================================
    //  CONSTRUCTOR
    // =========================================================
    public BuildState(
        PlacementActions actions,
        PreviewController preview,
        PlacementValidator validator,
        PlacementFinalizer finalizer,
        PlacementGrid grid,
        PlacementStateMachine fsm,
        RaycastController raycast,
        CellIndicatorController indicator)
    {
        _actions = actions;
        _preview = preview;
        _validator = validator;
        _finalizer = finalizer;
        _grid = grid;
        _fsm = fsm;
        _raycast = raycast;
        _indicator = indicator;
        _actions.BuildPlacement.BindRotateTo_R();
        _actions.BuildPlacement.BindPlaceToMouseLeft();
        _actions.BuildPlacement.BindCancelTo_RMB();
    }
    public bool IsPlacementState => true;
    #endregion *****************************************

    // =========================================================
    //  ENTER STATE
    // =========================================================
    public void OnEnter()
    {

        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;
        _actions.BuildPlacement.Place.canceled += OnPlacePerformed;
        _actions.BuildPlacement.Cancel.performed += OnCancelBuild;

        if (_currentData == null)
            return;

        _indicator.UseBuildMode();
        _raycast.EnableRay();

        _preview.Show(_currentData);

        Vector3 firstTarget = _grid.GetCellCenter(_raycast.HitCell);
        _preview.BeginFlyIn(firstTarget);

        _placeRequested = false;
        _rotateRequested = false;

        _currentRotation = _preview.CurrentRotation;

        _isDragging = false;
        _dragCells.Clear();

        _currentOffsets = _currentData.GetFootprintOffsets(-_currentRotation);
        _lastRotation = _currentRotation;
    }
    // =========================================================
    //  EXIT STATE
    // =========================================================
    public void OnExit()
    {
        _raycast.DisableRay();
        _indicator.ClearAll();
        _preview.Hide();
        _actions.BuildPlacement.Place.canceled -= OnPlacePerformed;
        _actions.BuildPlacement.Cancel.performed -= OnCancelBuild;
        _actions.BuildPlacement.Rotate.performed -= OnRotatePerformed;
    }
    // =========================================================
    //  MAIN UPDATE LOOP
    // =========================================================
    public void Tick()
    {
        _raycast.Tick();

        if (!_raycast.HasHit)
        {
            _indicator.ClearAll();
            _preview.Hide();
            return;
        }
        Vector2Int root = _raycast.HitCell;

        // ANTI-FLICKER
        if (_justPlaced && root == _lastPlacedCell)
        {
            _preview.Hide();
            _indicator.ClearAll();
            _placeRequested = false;
            _rotateRequested = false;
            return;
        }

        if (_justPlaced && root != _lastPlacedCell)
        {
            _justPlaced = false;
            _preview.Show(_currentData);
        }

        // DRAG START
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragCells.Clear();
            _dragStartCell = root;
        }

        // DRAG CONFIRMATION
        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            if (root != _dragStartCell)
            {
                _isDragging = true;

                _preview.Hide();
                _indicator.ClearAll();

                _justPlaced = false;
                _lastPlacedCell = new Vector2Int(int.MinValue, int.MinValue);

                _preview.BeginSelectionCells();
                return;
            }
        }

        // DRAGGING MODE
        if (_isDragging)
        {
            HandleDragPlacement(root);
            return;
        }

        // NORMAL PLACEMENT MODE

        // ROTATION
        if (_rotateRequested)
        {
            _rotateRequested = false;

            _currentRotation += 90f;
            if (_currentRotation >= 360f)
                _currentRotation = 0f;

            _preview.Rotate(_currentRotation);
        }

        // UPDATE FOOTPRINT IF ROTATED
        if (_currentRotation != _lastRotation)
        {
            _currentOffsets = _currentData.GetFootprintOffsets(-_currentRotation);
            _lastRotation = _currentRotation;
        }

        Vector2Int[] offsets = _currentOffsets;

        // MOVE PREVIEW
        _preview.MoveTo(_grid.GetCellCenter(root), root, _currentData);

        // VALIDITY CHECK
        bool isValid = _validator.IsValidPlacement(root, offsets, _currentData);

        // OBSTACLE DETECTION (free-moving objects)
        GameObject obj = _raycast.HitObject;

        if (obj != null)
        {
            var bd = obj.GetComponent<BuildingData>();
            if (bd != null && bd.Data != null && bd.Data.ClearsGridAfterPlacement)
            {
                isValid = false;
            }
        }

        if (_currentData.isStackable)
        {
            if (!_grid.CanStack(root, _currentData))
                isValid = false;
        }
        else
        {
            if (_grid.IsOccupied(root))
                isValid = false;
        }

        // SHOW FOOTPRINT (buffered)
        _indicator.ShowCells(BuildFootprintBuffered(root, offsets), isValid);

        if (isValid)
            _preview.SetGhostValid();
        else
            _preview.SetGhostInvalid();

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            _placeRequested = false;
            _rotateRequested = false;
            return;
        }

        // PLACE OBJECT
        if (_placeRequested)
        {
            _placeRequested = false;

            // FINAL OBSTACLE CHECK
            GameObject obj2 = _raycast.HitObject;

            if (obj2 != null)
            {
                var bd = obj2.GetComponent<BuildingData>();
                if (bd != null && bd.Data != null && bd.Data.ClearsGridAfterPlacement)
                {
                    AudioManager.Play("InvalidPlace");
                    return;
                }
            }

            bool isValidNow = _validator.IsCellValid(root, offsets, _currentData);

            if (_currentData.isStackable)
            {
                if (!_grid.CanStack(root, _currentData))
                    isValidNow = false;
            }
            else
            {
                if (_grid.IsOccupied(root))
                    isValidNow = false;
            }

            if (!isValidNow)
            {
                AudioManager.Play("InvalidPlace");
                _preview.SetGhostInvalid();
                _indicator.ShowCells(BuildFootprintBuffered(root, offsets), false);
                return;
            }

            AudioManager.Play("ValidPlace");
            _fsm.History.Push(
            new PlaceCommand(
             _grid,
             _finalizer,
             root,
             offsets,
             _currentData,
             _currentRotation)
            );

            Vector3 nextPos = _grid.GetCellCenter(root);
            _preview.BeginFlyIn(nextPos);

            _lastPlacedCell = root;
            _justPlaced = true;
        }
    }

    private void OnCancelBuild(InputAction.CallbackContext ctx)
    {
        _preview.Hide();
        _indicator.ClearAll();
        _raycast.DisableRay();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        AudioManager.Play("Cancel");

        _fsm.SetState(_fsm.IdleState);
        return;
    }

    // =========================================================
    //  STRIDE CALCULATION
    // =========================================================
    private Vector2Int GetStride(Vector2Int[] offsets)
    {
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var o in offsets)
        {
            if (o.x < minX) minX = o.x;
            if (o.x > maxX) maxX = o.x;
            if (o.y < minY) minY = o.y;
            if (o.y > maxY) maxY = o.y;
        }

        int width = (maxX - minX) + 1;
        int height = (maxY - minY) + 1;

        return new Vector2Int(width, height);
    }

    private List<Vector2Int> BuildFootprintBuffered(Vector2Int root, Vector2Int[] offsets)
    {
        _footprintBuffer.Clear();

        foreach (var o in offsets)
            _footprintBuffer.Add(root + o);

        return _footprintBuffer;
    }

    // =========================================================
    //  DRAG LOGIC (allocation-free)
    // =========================================================
    private void HandleDragPlacement(Vector2Int currentCell)
    {
        _dragCells.Clear();

        if (_currentRotation != _lastRotation)
        {
            _currentOffsets = _currentData.GetFootprintOffsets(-_currentRotation);
            _lastRotation = _currentRotation;
        }

        Vector2Int[] offsets = _currentOffsets;
        Vector2Int stride = GetStride(offsets);

        int stepX = (_dragStartCell.x <= currentCell.x) ? stride.x : -stride.x;
        int stepY = (_dragStartCell.y <= currentCell.y) ? stride.y : -stride.y;

        int startX = _dragStartCell.x;
        int endX = currentCell.x;

        int startY = _dragStartCell.y;
        int endY = currentCell.y;

        _indicatorBuffer.Clear();

        _preview.EndSelectionCells();
        _preview.BeginSelectionCells();

        for (int x = startX;
             stepX > 0 ? x <= endX : x >= endX;
             x += stepX)
        {
            for (int y = startY;
                 stepY > 0 ? y <= endY : y >= endY;
                 y += stepY)
            {
                Vector2Int cell = new Vector2Int(x, y);

                bool valid = _validator.IsCellValid(cell, offsets, _currentData);

                GameObject objAtCell = _raycast.RaycastCellCenter(cell);

                if (objAtCell != null)
                {
                    var bd = objAtCell.GetComponent<BuildingData>();
                    if (bd != null && bd.Data != null && bd.Data.ClearsGridAfterPlacement)
                    {
                        valid = false;
                    }
                }

                if (_currentData.isStackable)
                {
                    if (!_grid.CanStack(cell, _currentData))
                        valid = false;
                }
                else
                {
                    if (_grid.IsOccupied(cell))
                        valid = false;
                }

                if (!valid)
                    continue;

                _dragCells.Add(cell);

                _indicatorBuffer.Add(cell);
                foreach (var o in offsets)
                    _indicatorBuffer.Add(cell + o);

                _preview.ShowMultiGhost(cell, true, _currentRotation);
            }
        }

        _indicator.ShowCells(_indicatorBuffer, true);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndDragPlacement();
            return;
        }
    }

    // =========================================================
    //  FINALIZE DRAG PLACEMENT
    // =========================================================
    private void EndDragPlacement()
    {
        if (_dragCells.Count == 0)
        {
            AudioManager.Play("InvalidPlace");
            _preview.EndSelectionCells();
            _indicator.ClearAll();
            _isDragging = false;
            return;
        }

        AudioManager.Play("ValidPlace");

        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

        // Place all objects first
        _fsm.History.Push(
        new DragPlaceCommand(
            _grid,
            _finalizer,
            new List<Vector2Int>(_dragCells),
            offsets,
            _currentData,
            _currentRotation)
        );
        // Cleanup AFTER the loop
        _preview.EndSelectionCells();
        _indicator.ClearAll();
        _isDragging = false;
        _placeRequested = false;
        _dragCells.Clear();
    }
    // =========================================================
    //  SET BUILD DATA
    // =========================================================
    public void SetBuildData(ObjDataSO data)
    {
        _currentData = data;
    }
    // =========================================================
    //  INPUT CALLBACKS
    // =========================================================
    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        AudioManager.Play("Rotate");
        _rotateRequested = true;
    }

    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        if (_isDragging)
            return;

        _placeRequested = true;
    }
}
