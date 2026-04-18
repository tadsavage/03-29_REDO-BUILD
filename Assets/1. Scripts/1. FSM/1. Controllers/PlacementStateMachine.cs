using UnityEngine;

public class PlacementStateMachine : MonoBehaviour
{
    private IPlacementState _currentState;

    private IdleState _idleState;
    private RaycastPlacementState _raycastState;
    private BuildState _buildState;
    private DeleteState _deleteState;
    private MoveState _moveState;

    private PlacementActions _actions;

    public CommandHistory History { get; private set; } = new CommandHistory();

    public IPlacementState CurrentState => _currentState;
    public IdleState IdleState => _idleState;
    public RaycastPlacementState RaycastState => _raycastState;
    public BuildState BuildState => _buildState;
    public DeleteState DeleteState => _deleteState;
    public MoveState MoveState => _moveState;

    private void Awake()
    {
        // Core systems (NO UI references)
        RaycastController raycast = Object.FindFirstObjectByType<RaycastController>();
        CellIndicatorController indicator = Object.FindFirstObjectByType<CellIndicatorController>();
        PreviewController preview = Object.FindFirstObjectByType<PreviewController>();
        PlacementValidator validator = Object.FindFirstObjectByType<PlacementValidator>();
        PlacementFinalizer finalizer = Object.FindFirstObjectByType<PlacementFinalizer>();
        PlacementGrid grid = Object.FindFirstObjectByType<PlacementGrid>();

        // Input actions
        _actions = new PlacementActions();

        // Construct states
        _idleState = new IdleState();
        _raycastState = new RaycastPlacementState(raycast, indicator, grid);
        _buildState = new BuildState(_actions, preview, validator, finalizer, grid, this, raycast, indicator);
        _deleteState = new DeleteState(raycast, grid, finalizer, this, indicator, _actions);
        _moveState = new MoveState(_actions, preview, validator, finalizer, grid, this, raycast, indicator);

        // Start in idle
        _currentState = _idleState;
        _currentState.OnEnter();
    }

    private void Update()
    {
        _currentState?.Tick();
    }

    private void OnEnable()
    {
        _actions?.Enable();
    }

    private void OnDisable()
    {
        _actions?.Disable();
    }

    // ---------------------------------------------------------
    // CLEAN PUBLIC TRANSITION API (called from PlacementController)
    // ---------------------------------------------------------

    public void EnterIdle()
    {
        SetState(_idleState);
    }

    public void EnterRaycast()
    {
        SetState(_raycastState);
    }

    public void EnterBuild(ObjDataSO data)
    {
        _buildState.SetBuildData(data);
        SetState(_buildState);
    }

    public void EnterDelete()
    {
        SetState(_deleteState);
    }

    public void EnterMove()
    {
        SetState(_moveState);
    }

    public void Undo()
    {
        History.Undo();
    }

    public void Redo()
    {
        History.Redo();
    }

    // ---------------------------------------------------------
    // INTERNAL STATE SWITCHING
    // ---------------------------------------------------------
    private void SetState(IPlacementState newState)
    {
        if (newState == null || newState == _currentState)
            return;

        _currentState?.OnExit();
        _currentState = newState;
        _currentState?.OnEnter();
    }
}
