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

        Vector2Int root = _raycast.HitCell;

        // Compute rotated footprint offsets for this object
        Vector2Int[] offsets = _currentData.GetFootprintOffsets(_currentRotation);

        // Move preview to the root cell (grid‑aligned)
        _preview.MoveTo(_grid.GetCellCenter(root));

        // Show indicators for all occupied cells
        _indicator.ShowCells(root, offsets, _grid);

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
            }
        }
    }

    public void OnExit()
    {
        _raycast.DisableRay();
        _indicator.Hide();
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
