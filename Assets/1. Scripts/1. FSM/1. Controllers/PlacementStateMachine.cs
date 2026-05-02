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

    // Core refs
    private PreviewController _preview;
    private CellIndicatorController _indicator;
    private RaycastController _raycast;
    private WorldHoverPopupUI _hoverUI;
    private BuildMenuUI _buildMenuUI;

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

    private void Start()
    {
        // External dependencies
        _raycast = Object.FindFirstObjectByType<RaycastController>();
        _indicator = Object.FindFirstObjectByType<CellIndicatorController>();
        _preview = Object.FindFirstObjectByType<PreviewController>();
        PlacementValidator validator = Object.FindFirstObjectByType<PlacementValidator>();
        PlacementFinalizer finalizer = Object.FindFirstObjectByType<PlacementFinalizer>();
        PlacementGrid grid = Object.FindFirstObjectByType<PlacementGrid>();
        _hoverUI = Object.FindFirstObjectByType<WorldHoverPopupUI>();
        _buildMenuUI = Object.FindFirstObjectByType<BuildMenuUI>();

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
            _hoverUI,
            _buildMenuUI);

        _deleteState = new DeleteState(
            _raycast,
            grid,
            finalizer,
            this,
            _indicator,
            _actions,
            Context.MoneyService);

        // ⭐ NEW MoveState constructor (unified pipeline)
        _moveState = new MoveState(
            _actions,
            _preview,
            this,
            _raycast,
            grid,
            _indicator);

        // Start in idle
        _currentState = _idleState;
        _currentState.OnEnter();
    }

    private void Update()
    {
        _currentState?.Tick();

        if (_currentState == _idleState)
        {
            if (_raycast != null && _hoverUI != null)
                HandleIdleHover();
        }
        else
        {
            _hoverUI?.ForceHide();
        }

        // UNIVERSAL CANCEL
        if (_currentState != _idleState)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame ||
                Mouse.current.rightButton.wasPressedThisFrame)
            {
                ReturnToPrevious();
            }
        }
    }
    public void EnterBuildMoveMode(
    GameObject obj,
    ObjDataSO data,
    Vector2Int root,
    float rotation,
    Vector2Int[] offsets)
    {
        // 1) Set data
        _buildState.SetBuildData(data);

        // 2) Put BuildState as current state (no stack push)
        SetState(_buildState, push: false);

        // 3) Now enter move mode on that BuildState
        _buildState.EnterMoveMode(obj, root, rotation, offsets);
    }
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

        _hoverUI.TickHover(false, null, 0, 0, Vector3.zero, null);
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
    // INTERNAL STATE SWITCHING
    // ---------------------------------------------------------
    private void SetState(IPlacementState newState, bool push = true)
    {
        if (newState == null || newState == _currentState)
            return;

        if (_currentState == _idleState)
            _hoverUI?.ForceHide();

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
            EnterRaycast();
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
        SetState(_buildState, push: false);
    }

    public void EnterDelete() => SetState(_deleteState);
    public void EnterMove() => SetState(_moveState, push: false);


    // ---------------------------------------------------------
    // MOVE MODE HANDOFF (called by MoveState)
    // ---------------------------------------------------------
    public void BuildState_EnterMoveMode(GameObject obj, Vector2Int root, float rotation, Vector2Int[] offsets)
    {
        _buildState.EnterMoveMode(obj, root, rotation, offsets);
    }

    // ---------------------------------------------------------
    // HISTORY
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
