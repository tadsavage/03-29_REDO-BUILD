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

    // 🔥 Input flags (set in callbacks, consumed in Tick)
    private bool _placeRequested = false;
    private bool _rotateRequested = false;

    // 🔥 State data (e.g. current rotation)
    private float _currentRotation = 0f;

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

        // Bind inputs
        _actions.BuildPlacement.BindRotateTo_R();
        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;

        _actions.BuildPlacement.BindPlaceToMouseLeft();
        _actions.BuildPlacement.Place.performed += OnPlacePerformed;
    }

    // ------------------------------
    // INPUT CALLBACKS (flags only)
    // ------------------------------

    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        _rotateRequested = true;
    }

    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        _placeRequested = true;
    }

    // ------------------------------
    // STATE INTERFACE
    // ------------------------------

    public bool IsPlacementState => true;

    public void OnEnter()
    {
        if (_currentData == null)
            return;

        _raycast.EnableRay();
        _preview.Show(_currentData);

        // Reset flags
        _placeRequested = false;
        _rotateRequested = false;
    }

    public void Tick()
    {
        // Update raycast every frame
        _raycast.Tick();

        if (_raycast.HasHit)
        {
            _indicator.ShowAtCell(_raycast.HitCell);
            _preview.MoveTo(_grid.GetCellCenter(_raycast.HitCell));
        }
        else
        {
            _indicator.Hide();
            _preview.Hide();
        }

        // ------------------------------
        // UI BLOCKING (correct timing)
        // ------------------------------
        if (IsPointerOverUI())
        {
            _placeRequested = false;
            _rotateRequested = false;
            return;
        }

        // ------------------------------
        // HANDLE ROTATION
        // ------------------------------
        if (_rotateRequested)
        {
            _rotateRequested = false;
            Debug.Log("Object rotated");
        }

        // ------------------------------
        // HANDLE PLACEMENT
        // ------------------------------
        if (_placeRequested)
        {
            _placeRequested = false;
            Debug.Log("Object placed");
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
        SetData(data);
    }

    public void SetData(ObjDataSO data)
    {
        _currentData = data;
    }

    // ------------------------------
    // UI BLOCKER
    // ------------------------------

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }
}
