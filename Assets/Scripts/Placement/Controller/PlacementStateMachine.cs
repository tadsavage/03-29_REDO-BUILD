using UnityEngine;

public class PlacementStateMachine : MonoBehaviour
{
    private IPlacementState _currentState;

    private IdleState _idleState;
    private RaycastPlacementState _raycastState;
    private BuildState _buildState;
    private DeleteState _deleteState;

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

        // Input actions
        PlacementActions actions = new PlacementActions();

        // States
        _idleState = new IdleState();
        _raycastState = new RaycastPlacementState(raycast, indicator);
        _buildState = new BuildState(actions, preview, validator, finalizer, grid, this);
        _deleteState = new DeleteState(actions, finalizer, this);

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
}