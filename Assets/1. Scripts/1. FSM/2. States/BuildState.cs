using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class BuildState : IPlacementState
{
    private readonly RaycastController _raycast;
    private readonly CellIndicatorController _indicator;

    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementGrid _grid;
    private readonly PlacementStateMachine _fsm;

    private ObjDataSO _currentData;

    private bool _placeRequested;
    private bool _rotateRequested;
    private float _currentRotation;
    private float _lastPlacedRotation;

    // NEW — track the last placed cell and whether we're still hovering it
    private Vector2Int _lastPlacedCell;
    private bool _justPlaced;

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
        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;

        _actions.BuildPlacement.BindPlaceToMouseLeft();
        _actions.BuildPlacement.Place.performed += OnPlacePerformed;
    }

    public bool IsPlacementState => true;

    public void OnEnter()
    {
        if (_currentData == null)
            return;

        _raycast.EnableRay();
        _preview.Show(_currentData);

        _placeRequested = false;
        _rotateRequested = false;
        _currentRotation = 0f;
    }

    public void Tick()
    {
        _raycast.Tick();

        if (!_raycast.HasHit)
        {
            _indicator.Hide();
            _preview.Hide();
            return;
        }

        // Global cancel (Right-click or Escape)
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            AudioManager.Play("Cancel");

            _preview.RestoreMaterials();
            _preview.Hide();
            _indicator.ClearAll();
            _raycast.DisableRay();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _fsm.SetState(_fsm.IdleState);
            return;
        }

        Vector2Int root = _raycast.HitCell;

        // NEW — if we're still hovering the cell we just placed on:
        // hide preview, hide indicators, block placement/rotation
        if (_justPlaced && root == _lastPlacedCell)
        {
            _preview.Hide();
            _indicator.Hide();
            _placeRequested = false;
            _rotateRequested = false;
            return;
        }

        // NEW — once we move off that cell, return to normal behavior
        if (_justPlaced && root != _lastPlacedCell)
        {
            _justPlaced = false;

            // REAPPEAR GHOST NOW THAT WE MOVED OFF THE PLACED CELL
            _preview.Show(_currentData);

            // ⭐ Restore rotation of last placed object
            _preview.Rotate(_lastPlacedRotation);
            _currentRotation = _lastPlacedRotation;
        }

        // Compute rotated footprint offsets for this object
        Vector2Int[] offsets = _currentData.GetFootprintOffsets(_currentRotation);

        // Move preview to the root cell (grid‑aligned)
        _preview.transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);
        _preview.MoveTo(_grid.GetCellCenter(root));
     
        // Show indicators for all occupied cells
        bool isValid = _validator.IsValidPlacement(root, offsets);
        _indicator.ShowCells(root, offsets, _grid, isValid);

        if (isValid)
            _preview.SetGhostValid();
        else
            _preview.SetGhostInvalid();

        // Block world actions when pointer is over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            _placeRequested = false;
            _rotateRequested = false;
            return;
        }

        // Handle rotation
        if (_rotateRequested)
        {
            _rotateRequested = false;

            _currentRotation += 90f;
            if (_currentRotation >= 360f)
                _currentRotation = 0f;

            _preview.Rotate(_currentRotation);
        }

        // Handle placement
        if (_placeRequested)
        {
            _placeRequested = false;

            if (_validator.IsValidPlacement(root, offsets))
            {
                _finalizer.FinalizePlacement(root, offsets, _currentData, _currentRotation);

                // NEW — mark this cell as "just placed"
                _lastPlacedCell = root;
                _lastPlacedRotation = _currentRotation;   // ⭐ store rotation
                _justPlaced = true;
            }
        }
    }

    public void OnExit()
    {
        Debug.Log("Exiting BuildState");
        _preview.RestoreMaterials();
        _raycast.DisableRay();
        _indicator.Hide();
        _indicator.ClearAll();
        _preview.RestoreMaterials();
        _preview.Hide();
    }

    public void SetBuildData(ObjDataSO data)
    {
        _currentData = data;
    }

    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        _rotateRequested = true;
    }

    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        _placeRequested = true;
    }
}
