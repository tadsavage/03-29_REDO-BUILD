using UnityEngine;

public class PlacementStateMachine : MonoBehaviour
{
    private IPlacementState _currentState;

    private IdleState _idleState;
    private RaycastPlacementState _raycastState;
    private BuildState _buildState;
    private DeleteState _deleteState;
    private PlacementActions _actions;

    public IPlacementState CurrentState
    {
        get { return _currentState; }
    }
    public IdleState IdleState
    {
        get { return _idleState; }
    }
    public RaycastPlacementState RaycastState
    {
        get { return _raycastState; }
    }
    public BuildState BuildState
    {
        get { return _buildState; }
    }

    public DeleteState DeleteState
    {
        get { return _deleteState; }
    }

    private void Awake()
    {
        // Core systems
        RaycastController raycast = Object.FindFirstObjectByType<RaycastController>();
        CellIndicatorController indicator = Object.FindFirstObjectByType<CellIndicatorController>();
        PreviewController preview = Object.FindFirstObjectByType<PreviewController>();
        PlacementValidator validator = Object.FindFirstObjectByType<PlacementValidator>();
        PlacementFinalizer finalizer = Object.FindFirstObjectByType<PlacementFinalizer>();
        PlacementGrid grid = Object.FindFirstObjectByType<PlacementGrid>();
        BuildBarBinder binder = Object.FindFirstObjectByType<BuildBarBinder>();
        binder.OnDeleteClicked += () =>
        {
            SetState(_deleteState);
        };

        // Input actions
        _actions = new PlacementActions();

        // States
        _idleState = new IdleState();
        _raycastState = new RaycastPlacementState(raycast, indicator, grid);
        _buildState = new BuildState(_actions, preview, validator, finalizer, grid, this, raycast, indicator);
        _deleteState = new DeleteState(raycast, grid, finalizer, this, preview, indicator);

        // Start in idle
        _currentState = _idleState;
    }
    public void SetState(IPlacementState newState)
    {
        if (_currentState != null)
            _currentState.OnExit();

        _currentState = newState;

        if (_currentState != null)
            _currentState.OnEnter();
    }
    private void Update()
    {
        if (_currentState != null)
            _currentState.Tick();
    }
    private void OnEnable()
    {
        _actions?.Enable();
    }
    private void OnDisable()
    {
        _actions?.Disable();
    }
}