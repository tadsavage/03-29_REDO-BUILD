using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

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

        // Bind input
        _actions.BuildPlacement.BindRotateTo_R();
        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;

        _actions.BuildPlacement.BindPlaceToMouseLeft();
        _actions.BuildPlacement.Place.canceled += OnPlacePerformed; // place on release
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
    //  DRAG LOGIC
    // =========================================================
    private void HandleDragPlacement(Vector2Int currentCell)
    {
        _dragCells.Clear();

        int minX = Mathf.Min(_dragStartCell.x, currentCell.x);
        int maxX = Mathf.Max(_dragStartCell.x, currentCell.x);
        int minY = Mathf.Min(_dragStartCell.y, currentCell.y);
        int maxY = Mathf.Max(_dragStartCell.y, currentCell.y);

        if (_currentRotation != _lastRotation)
        {
            _currentOffsets = _currentData.GetFootprintOffsets(-_currentRotation);
            _lastRotation = _currentRotation;
        }

        Vector2Int[] offsets = _currentOffsets;
        Vector2Int stride = GetStride(offsets);

        for (int x = minX; x <= maxX; x += stride.x)
        {
            for (int y = minY; y <= maxY; y += stride.y)
            {
                Vector2Int cell = new Vector2Int(x, y);

                bool valid = _validator.IsCellValid(cell, offsets, _currentData);

                // ================================
                // STACKING: validate per-cell stack height / occupancy
                // ================================
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
                // ================================

                if (!valid)
                    continue;

                _dragCells.Add(cell);

                _indicator.ShowCell(cell);

                foreach (var o in offsets)
                {
                    Vector2Int subCell = cell + o;
                    _indicator.ShowCell(subCell);
                }

                _preview.ShowGhost(cell, true, _currentRotation);
            }
        }

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
