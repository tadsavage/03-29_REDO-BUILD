using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Central state machine controlling all placement-related states:
/// - IdleState (hover + inspect)
/// - RaycastPlacementState (grid hover only)
/// - BuildState (placing new objects)
/// - MoveState (moving existing objects)
/// - DeleteState (deleting objects)
///
/// IMPORTANT:
/// Only IdleState is allowed to drive the hover popup.
/// All other states force-hide it.
/// </summary>
public class PlacementStateMachine : MonoBehaviour
{
    // ---------------------------------------------------------
    // STATE FIELDS
    // ---------------------------------------------------------
    private IPlacementState _currentState;

    private IdleState _idleState;
    private RaycastPlacementState _raycastState;
    private BuildState _buildState;
    private DeleteState _deleteState;
    private MoveState _moveState;

    private readonly Stack<IPlacementState> _stateStack = new();

    public IPlacementState CurrentState => _currentState;
    public CommandHistory History { get; private set; } = new CommandHistory();
    public System.Action OnHistoryChanged;

    // ---------------------------------------------------------
    // CORE SYSTEM REFERENCES
    // ---------------------------------------------------------
    private PlacementActions _actions;

    private PreviewController _preview;
    private CellIndicatorController _indicator;
    private RaycastController _raycast;
    private BuildMenuUI _buildMenuUI;

    // Hover popup (assigned by UIBootstrapper)
    private WorldHoverPopupUI _hoverUI;

    // Injected externally
    public GameContext Context { get; private set; }

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
    }

    /// <summary>
    /// Called by UIBootstrapper to inject the hover popup reference.
    /// </summary>
    public void SetHoverUI(WorldHoverPopupUI ui)
    {
        _hoverUI = ui;
    }

    private void Start()
    {
        // Find shared systems
        _raycast = FindFirstObjectByType<RaycastController>();
        _indicator = FindFirstObjectByType<CellIndicatorController>();
        _preview = FindFirstObjectByType<PreviewController>();
        PlacementValidator validator = FindFirstObjectByType<PlacementValidator>();
        PlacementFinalizer finalizer = FindFirstObjectByType<PlacementFinalizer>();
        PlacementGrid grid = FindFirstObjectByType<PlacementGrid>();
        _buildMenuUI = FindFirstObjectByType<BuildMenuUI>();

        _raycast.EnableRay();

        // Construct states
        _idleState = new IdleState();
        _raycastState = new RaycastPlacementState(_raycast, _indicator, grid);

        _buildState = new BuildState(
            _actions,
            _preview,
            validator,
            finalizer,
            grid,
            this,
            _raycast,
            _indicator,
            Context.MoneyService,
            _costUI,
            _buildMenuUI);

        _deleteState = new DeleteState(
            _raycast,
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
            _raycast,
            _indicator,
            Context.MoneyService);

        // Start in Idle
        _currentState = _idleState;
        _currentState.OnEnter();
    }

    // ---------------------------------------------------------
    // UPDATE LOOP
    // ---------------------------------------------------------
    private void Update()
    {
        // -----------------------------------------------------
        // PREVENT LAST-FRAME POPUP FLASH
        // -----------------------------------------------------
        // If user clicks while in IdleState, hide popup BEFORE Idle Tick runs.
        if (_currentState == _idleState && _hoverUI != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                _hoverUI.HideImmediate();
        }

        // -----------------------------------------------------
        // STATE TICK
        // -----------------------------------------------------
        _currentState?.Tick();

        // -----------------------------------------------------
        // IDLE HOVER LOGIC
        // -----------------------------------------------------
        if (_currentState == _idleState)
        {
            if (_raycast != null && _hoverUI != null)
                HandleIdleHover();
        }
        else
        {
            // All non-idle states force-hide popup
            _hoverUI?.HideImmediate();
        }

        // -----------------------------------------------------
        // UNIVERSAL CANCEL (ESC or RMB)
        // -----------------------------------------------------
        if (_currentState != _idleState)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame ||
                Mouse.current.rightButton.wasPressedThisFrame)
            {
                ReturnToPrevious();
            }
        }
    }

    /// <summary>
    /// Handles hover popup behavior ONLY in IdleState.
    /// </summary>
    private void HandleIdleHover()
    {
        _raycast.Tick();

        if (_raycast.HasHit && _raycast.HitObject != null)
        {
            var bd = _raycast.HitObject.GetComponent<BuildingData>();
            if (bd != null)
            {
                _hoverUI.TickHover(
                    true,
                    bd.Data.objName,
                    bd.Data.cost,
                    bd.Data.hourlyCost,
                    _raycast.RawHitPoint,
                    Camera.main
                );
                return;
            }
        }

        // No hit or no building → hide popup
        _hoverUI.TickHover(false, null, 0, 0, Vector3.zero, null);
    }

    private void OnEnable() => _actions?.Enable();
    private void OnDisable() => _actions?.Disable();

    // ---------------------------------------------------------
    // STATE SWITCHING
    // ---------------------------------------------------------
    private void SetState(IPlacementState newState, bool push = true)
    {
        if (newState == null || newState == _currentState)
            return;

        // Leaving Idle → hide popup immediately
        if (_currentState == _idleState)
            _hoverUI?.HideImmediate();

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
    // PUBLIC TRANSITION API
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
