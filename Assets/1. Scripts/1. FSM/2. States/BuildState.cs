using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildState : IPlacementState
{
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
    private readonly System.Collections.Generic.List<Vector2Int> _dragCells = new();

    // =========================================================
    //  ANTI-FLICKER (prevents ghost disappearing after placement)
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

        // Rotation Key Binding (R)
        _actions.BuildPlacement.BindRotateTo_R();
        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;

        // Place Binding (Mouse Left Button)
        _actions.BuildPlacement.BindPlaceToMouseLeft();
        _actions.BuildPlacement.Place.canceled += OnPlacePerformed; 
    }

    public bool IsPlacementState => true;

    // =========================================================
    //  ENTER STATE
    // =========================================================
    public void OnEnter()
    {
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

        // ---------------------------------------------------------
        // RIGHT-CLICK CANCEL
        // ---------------------------------------------------------
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            AudioManager.Play("Cancel");

            _preview.Hide();
            _indicator.ClearAll();
            _raycast.DisableRay();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _fsm.SetState(_fsm.IdleState);
            return;
        }

        Vector2Int root = _raycast.HitCell;

        // ---------------------------------------------------------
        // ANTI-FLICKER
        // ---------------------------------------------------------
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

        // ---------------------------------------------------------
        // DRAG START
        // ---------------------------------------------------------
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragCells.Clear();
            _dragStartCell = root;
        }

        // ---------------------------------------------------------
        // DRAG CONFIRMATION
        // ---------------------------------------------------------
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
                return;   // ← IMPORTANT: skip normal placement this frame
            }
        }

        // ---------------------------------------------------------
        // DRAGGING MODE
        // ---------------------------------------------------------
        if (_isDragging)
        {
            HandleDragPlacement(root);
            return;   // ← CRITICAL: prevents normal placement from running
        }

        // ---------------------------------------------------------
        // NORMAL PLACEMENT MODE
        // ---------------------------------------------------------

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

        // =========================================================
        //  OBSTACLE DETECTION (free-moving objects)
        // =========================================================
        GameObject obj = _raycast.HitObject;

        if (obj != null)
        {
            var bd = obj.GetComponent<BuildingData>();
            if (bd != null && bd.Data != null && bd.Data.ClearsGridAfterPlacement)
            {
                // A free-moving object is blocking placement
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

        // SHOW FOOTPRINT
        _indicator.ShowCells(BuildFootprint(root, offsets), isValid);

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

            // =========================================================
            //  FINAL OBSTACLE CHECK
            // =========================================================
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
                _indicator.ShowCells(BuildFootprint(root, offsets), false);
                return;
            }

            AudioManager.Play("ValidPlace");

            _finalizer.FinalizePlacement(root, offsets, _currentData, _currentRotation);

            Vector3 nextPos = _grid.GetCellCenter(root);
            _preview.BeginFlyIn(nextPos);

            _lastPlacedCell = root;
            _justPlaced = true;
        }
    }

    // =========================================================
    //  STRIDE CALCULATION (used for drag placement)
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
    private System.Collections.Generic.List<Vector2Int> BuildFootprint(Vector2Int root, Vector2Int[] offsets)
    {
        var cells = new System.Collections.Generic.List<Vector2Int>(offsets.Length);

        foreach (var o in offsets)
            cells.Add(root + o);

        return cells;
    }

    // =========================================================
    //  DRAG LOGIC (FINAL, FIXED, UNIFIED)
    // =========================================================
    private void HandleDragPlacement(Vector2Int currentCell)
    {
        // REM: clear previous drag roots
        _dragCells.Clear();

        // REM: update offsets if rotation changed
        if (_currentRotation != _lastRotation)
        {
            _currentOffsets = _currentData.GetFootprintOffsets(-_currentRotation);
            _lastRotation = _currentRotation;
        }

        Vector2Int[] offsets = _currentOffsets;
        Vector2Int stride = GetStride(offsets);

        // =========================================================
        //  Determine stride direction based on drag direction
        // =========================================================
        int stepX = (_dragStartCell.x <= currentCell.x) ? stride.x : -stride.x;
        int stepY = (_dragStartCell.y <= currentCell.y) ? stride.y : -stride.y;

        int startX = _dragStartCell.x;
        int endX = currentCell.x;

        int startY = _dragStartCell.y;
        int endY = currentCell.y;

        // =========================================================
        //  Unified indicator list for the entire drag frame
        // =========================================================
        List<Vector2Int> allIndicatorCells = new();

        _preview.EndSelectionCells();
        _preview.BeginSelectionCells();

        // =========================================================
        //  Iterate the rectangle using directional stride
        // =========================================================
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

                // =========================================================
                //  OBSTACLE DETECTION (free-moving objects)
                // =========================================================
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

                // =========================================================
                //  AUTO‑SKIP BLOCKED CELLS
                // =========================================================
                if (!valid)
                    continue;

                // =========================================================
                //  VALID ROOT — add to drag list
                // =========================================================
                _dragCells.Add(cell);

                allIndicatorCells.Add(cell);
                foreach (var o in offsets)
                    allIndicatorCells.Add(cell + o);

                _preview.ShowGhost(cell, true, _currentRotation);
            }
        }

        // =========================================================
        //  ONE CALL PER FRAME — identical to DeleteState
        // =========================================================
        _indicator.ShowCells(allIndicatorCells, true);

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
        // ================================
        // PLAY VALID SOUND FOR DRAG PLACEMENT
        // ================================
        AudioManager.Play("ValidPlace");
        foreach (var cell in _dragCells)
        {
            Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

            // Finalizer handles stack height + grid registration
            GameObject placed = _finalizer.FinalizePlacement(cell, offsets, _currentData, _currentRotation);

            _finalizer.SpawnDust(_grid.GetCellCenter(cell));
        }

        _preview.EndSelectionCells();
        _indicator.ClearAll();
        _isDragging = false;
        _placeRequested = false;
        _dragCells.Clear();
    }

    // =========================================================
    //  EXIT STATE
    // =========================================================
    public void OnExit()
    {
        _raycast.DisableRay();
        _indicator.ClearAll();
        _preview.Hide();
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
            return; // <-- prevents single placement after drag

        _placeRequested = true;
    }
}
