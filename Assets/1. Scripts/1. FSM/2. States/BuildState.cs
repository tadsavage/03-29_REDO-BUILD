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

        _placeRequested = false;
        _rotateRequested = false;
        _currentRotation = 0f;

        _isDragging = false;
        _dragCells.Clear();
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
            _preview.Hide();

            // Reset drag state
            _isDragging = false;
            _dragCells.Clear();
        }

        // ---------------------------------------------------------
        // DRAG CONFIRMATION — user moved off the click cell
        // ---------------------------------------------------------
        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            if (root != _dragStartCell)
            {
                _isDragging = true;

                // ⭐ Set drag start cell NOW (correct timing)
                _dragStartCell = root;

                // ⭐ Treat the root cell as empty during drag
                _grid.ClearCell(_dragStartCell);

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
        Vector2Int[] offsets = _currentData.GetFootprintOffsets(_currentRotation);

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

            if (_validator.IsValidPlacement(root, offsets))
            {
                _finalizer.FinalizePlacement(root, offsets, _currentData, _currentRotation);

                _lastPlacedCell = root;
                _justPlaced = true;
            }
        }
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

        Vector2Int[] offsets = _currentData.GetFootprintOffsets(_currentRotation);

        // Draw drag rectangle
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
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
            AudioManager.Play("Invalid");
            _preview.EndSelectionCells();
            _indicator.ClearAll();
            _isDragging = false;
            return;
        }

        foreach (var cell in _dragCells)
        {
            Vector2Int[] offsets = _currentData.GetFootprintOffsets(_currentRotation);

            GameObject placed = _finalizer.FinalizePlacement(cell, offsets, _currentData, _currentRotation);

            _grid.SetOccupied(cell, placed, _currentData);
            _finalizer.SpawnDust(_grid.GetCellCenter(cell));
        }

        AudioManager.Play("ValidPlace");

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
        _rotateRequested = true;
    }

    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        _placeRequested = true;
    }
}
