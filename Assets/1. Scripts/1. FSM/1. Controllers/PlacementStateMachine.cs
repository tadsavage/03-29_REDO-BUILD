using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlacementStateMachine : MonoBehaviour
{
    private IPlacementState _currentState;

    private IdleState _idleState;
    private RaycastPlacementState _raycastState;
    private BuildState _buildState;
    private DeleteState _deleteState;
    private MoveState _moveState;

    private PlacementActions _actions;

    private readonly Stack<IPlacementState> _stateStack = new();

    public CommandHistory History { get; private set; } = new CommandHistory();
    public IPlacementState CurrentState => _currentState;

    public System.Action OnHistoryChanged;

    // Core refs for unified visual reset
    private PreviewController _preview;
    private CellIndicatorController _indicator;

    // Injected from PlacementController
    public GameContext Context { get; private set; }

    // UI reference (already initialized by UIBootstrapper)
    [SerializeField] private PreviewCostUI _costUI;

    public int DebugStackDepth => _stateStack.Count;

    // ---------------------------------------------------------
    // INITIALIZATION
    // ---------------------------------------------------------

    private void Awake()
    {
        _actions = new PlacementActions();
    }

    public void Initialize(GameContext context)
    {
        Context = context;
        // No UI initialization here anymore — UIBootstrapper handles that
    }

    private void Start()
    {
        // External dependencies (Context) are now valid

        RaycastController raycast = Object.FindFirstObjectByType<RaycastController>();
        _indicator = Object.FindFirstObjectByType<CellIndicatorController>();
        _preview = Object.FindFirstObjectByType<PreviewController>();
        PlacementValidator validator = Object.FindFirstObjectByType<PlacementValidator>();
        PlacementFinalizer finalizer = Object.FindFirstObjectByType<PlacementFinalizer>();
        PlacementGrid grid = Object.FindFirstObjectByType<PlacementGrid>();

        // Construct states
        _idleState = new IdleState();
        _raycastState = new RaycastPlacementState(raycast, _indicator, grid);

        _buildState = new BuildState(
            _actions,
            _preview,
            validator,
            finalizer,
            grid,
            this,
            raycast,
            _indicator,
            Context.MoneyService,
            _costUI);

        _deleteState = new DeleteState(
            raycast,
            grid,
            finalizer,
            this,
            _indicator,
            _actions,
            Context.MoneyService);

        _moveState = new MoveState(
            _actions,
            _preview,
            validator,
            finalizer,
            grid,
            this,
            raycast,
            _indicator,
            Context.MoneyService);

        // Start in idle
        _currentState = _idleState;
        _currentState.OnEnter();
    }

    private void Update()
    {
        _currentState?.Tick();

        // UNIVERSAL CANCEL (ESC or RMB)
        if (_currentState != _idleState)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame ||
                Mouse.current.rightButton.wasPressedThisFrame)
            {
                ReturnToPrevious();
            }
        }
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
    // INTERNAL STATE SWITCHING (with stack)
    // ---------------------------------------------------------
    private void SetState(IPlacementState newState, bool push = true)
    {
        if (newState == null || newState == _currentState)
            return;

        if (push && _currentState != null)
            _stateStack.Push(_currentState);

        _currentState?.OnExit();
        _currentState = newState;
        _currentState?.OnEnter();
    }

    public void ReturnToPrevious()
    {
        if (_stateStack.Count > 0)
        {
            var previous = _stateStack.Pop();
            SetState(previous, push: false);
        }
        else
        {
            EnterRaycast(); // fallback
        }
    }

    // ---------------------------------------------------------
    // CLEAN PUBLIC TRANSITION API
    // ---------------------------------------------------------
    public void EnterIdle() => SetState(_idleState, push: false);
    public void EnterRaycast() => SetState(_raycastState, push: false);

    public void EnterBuild(ObjDataSO data)
    {
        _buildState.SetBuildData(data);
        SetState(_buildState);
    }

    public void EnterDelete() => SetState(_deleteState);
    public void EnterMove() => SetState(_moveState);

    // ---------------------------------------------------------
    // HISTORY + VISUAL RESET
    // ---------------------------------------------------------
    public void Undo()
    {
        History.Undo();
        ResetVisualsAfterHistoryChange();
        OnHistoryChanged?.Invoke();
    }

    public void Redo()
    {
        History.Redo();
        ResetVisualsAfterHistoryChange();
        OnHistoryChanged?.Invoke();
    }

    private void ResetVisualsAfterHistoryChange()
    {
        _preview?.ResetAllVisuals();
        _indicator?.ClearAll();
    }
}
