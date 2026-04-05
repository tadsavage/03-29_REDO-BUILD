using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class BuildState : IPlacementState
{
    // ---------------------------------------------------------
    // Dependencies
    // ---------------------------------------------------------
    private readonly RaycastController _raycast;
    private readonly CellIndicatorController _indicator;

    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementGrid _grid;
    private readonly PlacementStateMachine _fsm;

    // ---------------------------------------------------------
    // Build Data
    // ---------------------------------------------------------
    private ObjDataSO _currentData;

    private bool _placeRequested;
    private bool _rotateRequested;
    private float _currentRotation;

    private Vector2Int[] _currentOffsets;
    private float _lastRotation;

    // ---------------------------------------------------------
    // Drag Placement
    // ---------------------------------------------------------
    private bool _isDragging;
    private Vector2Int _dragStartCell;
    private readonly System.Collections.Generic.List<Vector2Int> _dragCells = new();

    // ---------------------------------------------------------
    // Anti‑flicker for single placement
    // ---------------------------------------------------------
    private Vector2Int _lastPlacedCell;
    private bool _justPlaced;

    // ---------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------
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
        _actions.BuildPlacement.Place.performed += OnPlacePerformed;
    }

    public bool IsPlacementState => true;

    // ---------------------------------------------------------
    // ENTER STATE
    // ---------------------------------------------------------
    public void OnEnter()
    {
        if (_currentData == null)
            return;

        _raycast.EnableRay();
        _preview.Show(_currentData);

        // Optional fly-in effect from above
        Vector3 firstTarget = _grid.GetCellCenter(_raycast.HitCell);
        _preview.BeginFlyIn(firstTarget);

        _placeRequested = false;
        _rotateRequested = false;

        // Restore last rotation from preview
        _currentRotation = _preview.CurrentRotation;

        _isDragging = false;
        _dragCells.Clear();

        // Compute correct offsets immediately
        _currentOffsets = _currentData.GetFootprintOffsets(-_currentRotation);
        _lastRotation = _currentRotation;
    }

    // ---------------------------------------------------------
    // MAIN UPDATE LOOP
    // ---------------------------------------------------------
    public void Tick()
    {
        _raycast.Tick();

        // No hit → hide everything
        if (!_raycast.HasHit)
        {
            _indicator.Hide();
            _preview.Hide();
            return;
        }

        // Right‑click cancel
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
        // Anti‑flicker: hide ghost if hovering over last placed cell
        // ---------------------------------------------------------
        if (_justPlaced && root == _lastPlacedCell)
        {
            _preview.Hide();
            _indicator.Hide();
            _placeRequested = false;
            _rotateRequested = false;
            return;
        }

        // Moved off last placed cell → restore ghost
        if (_justPlaced && root != _lastPlacedCell)
        {
            _justPlaced = false;
            _preview.Show(_currentData);
        }

        // ---------------------------------------------------------
        // MOUSE DOWN — prepare for drag detection
        // ---------------------------------------------------------
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Hide single‑placement ghost immediately
            if (_isDragging)
                _preview.Hide();
            // Do NOT hide the ghost here for single placement
            _isDragging = false;
            _dragCells.Clear();
            _dragStartCell = root;

            // Reset drag state
            _isDragging = false;
            _dragCells.Clear();

            // ⭐ IMPORTANT: record the click cell here
            _dragStartCell = root;
        }

        // ---------------------------------------------------------
        // DRAG CONFIRMATION — user moved off the click cell
        // ---------------------------------------------------------
        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            // Only enter drag if we've actually moved off the start cell
            if (root != _dragStartCell)
            {
                _isDragging = true;

                // Reset visuals
                _preview.Hide();
                _preview.RestoreMaterials();
                _indicator.ClearAll();

                // Reset anti‑flicker
                _justPlaced = false;
                _lastPlacedCell = new Vector2Int(int.MinValue, int.MinValue);

                // Enter multi‑ghost mode
                _preview.BeginSelectionCells();
            }
        }
        // ---------------------------------------------------------
        // DRAGGING MODE
        // ---------------------------------------------------------
        if (_isDragging)
        {
            HandleDragPlacement(root);
            return;
        }

        // ---------------------------------------------------------
        // ROTATION
        // ---------------------------------------------------------
        if (_rotateRequested)
        {
            _rotateRequested = false;

            _currentRotation += 90f;
            if (_currentRotation >= 360f)
                _currentRotation = 0f;

            _preview.Rotate(_currentRotation);
        }

        // ---------------------------------------------------------
        // SINGLE‑CELL PLACEMENT
        // ---------------------------------------------------------
        // Only recompute when rotation changes
        if (_currentRotation != _lastRotation)
        {
            _currentOffsets = _currentData.GetFootprintOffsets(-_currentRotation);
            _lastRotation = _currentRotation;
        }

        Vector2Int[] offsets = _currentOffsets;
        _preview.MoveTo(_grid.GetCellCenter(root));

        bool isValid = _validator.IsValidPlacement(root, offsets);
        _indicator.ShowCells(root, offsets, _grid, isValid);

        if (isValid)
            _preview.SetGhostValid();
        else
            _preview.SetGhostInvalid();

        // Ignore clicks over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            _placeRequested = false;
            _rotateRequested = false;
            return;
        }

        // Finalize single placement
        if (_placeRequested)
        {
            _placeRequested = false;

            bool isValidNow = _validator.IsValidPlacement(root, offsets);

            if (!isValidNow)
            {
                // ❌ INVALID PLACEMENT
                AudioManager.Play("InvalidPlace");

                // Keep ghost visible and red
                _preview.SetGhostInvalid();
                _indicator.ShowCells(root, offsets, _grid, false);
                return; // ⭐ STOP HERE — do NOT place anything
            }
            else
            {
                AudioManager.Play("ValidPlace");
            }
            // ✔ VALID PLACEMENT
            _finalizer.FinalizePlacement(root, offsets, _currentData, _currentRotation);

            Vector3 nextPos = _grid.GetCellCenter(root);
            _preview.BeginFlyIn(nextPos);

            _lastPlacedCell = root;
            _justPlaced = true;
        }
    }
    // ---------------------------------------------------------
    // STRIDE CALCULATION (for drag placement of 2x1)
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
    // ---------------------------------------------------------
    // DRAG LOGIC
    // ---------------------------------------------------------
    private void HandleDragPlacement(Vector2Int currentCell)
    {
        _dragCells.Clear();

        int minX = Mathf.Min(_dragStartCell.x, currentCell.x);
        int maxX = Mathf.Max(_dragStartCell.x, currentCell.x);
        int minY = Mathf.Min(_dragStartCell.y, currentCell.y);
        int maxY = Mathf.Max(_dragStartCell.y, currentCell.y);

        //Vector2Int[] offsets = _currentData.GetFootprintOffsets(_currentRotation);
        // Only recompute when rotation changes
        if (_currentRotation != _lastRotation)
        {
            _currentOffsets = _currentData.GetFootprintOffsets(-_currentRotation);
            _lastRotation = _currentRotation;
        }

        Vector2Int[] offsets = _currentOffsets;

        Vector2Int stride = GetStride(offsets);

        // Draw drag rectangle using stride
        for (int x = minX; x <= maxX; x += stride.x)
        {
            for (int y = minY; y <= maxY; y += stride.y)
            {
                Vector2Int cell = new Vector2Int(x, y);
                bool valid = _validator.IsCellValid(cell, offsets);

                if (valid)
                    _dragCells.Add(cell);

                _indicator.ShowCell(cell, valid);
                _preview.ShowGhost(cell, valid, _currentRotation);
            }
        }
        // Mouse up → finalize drag placement
        if (Mouse.current.leftButton.wasReleasedThisFrame)
            EndDragPlacement();
    }

    // ---------------------------------------------------------
    // FINALIZE DRAG PLACEMENT
    // ---------------------------------------------------------
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
        foreach (var cell in _dragCells)
            {
                Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

                GameObject placed = _finalizer.FinalizePlacement(cell, offsets, _currentData, _currentRotation);

                // ⭐ Only occupy grid if this object should block
                if (!_currentData.ClearsGridAfterPlacement)
                    _grid.SetOccupied(cell, placed, _currentData);

                _finalizer.SpawnDust(_grid.GetCellCenter(cell));
            }

        //AudioManager.Play("ValidPlace");

        _preview.EndSelectionCells();
        _indicator.ClearAll();
        _isDragging = false;
    }
    // ---------------------------------------------------------
    // EXIT STATE
    // ---------------------------------------------------------
    public void OnExit()
    {
        _preview.RestoreMaterials();
        _raycast.DisableRay();
        _indicator.Hide();
        _indicator.ClearAll();
        _preview.Hide();
    }

    // ---------------------------------------------------------
    // SET BUILD DATA
    // ---------------------------------------------------------
    public void SetBuildData(ObjDataSO data)
    {
        _currentData = data;
    }

    // ---------------------------------------------------------
    // INPUT CALLBACKS
    // ---------------------------------------------------------
    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        AudioManager.Play("Rotate");
        _rotateRequested = true;
    }

    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        _placeRequested = true;
    }
}
