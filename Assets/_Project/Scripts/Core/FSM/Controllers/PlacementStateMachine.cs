using GameCore.Build;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;
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

    // Shared with BuildService — both must reference the SAME CommandHistory instance,
    // otherwise BuildService.Undo()/Redo() operate on an empty stack nothing ever pushes to.
    public CommandHistory History { get; private set; }
    public System.Action OnHistoryChanged;

    // ---------------------------------------------------------
    // CORE SYSTEM REFERENCES
    // ---------------------------------------------------------
    private PlacementActions _actions;

    private PreviewController _preview;
    private CellIndicatorController _indicator;
    private RaycastController _raycast;
    // Reusable buffer for the hover-tooltip's supplementary multi-hit raycast (avoids a
    // per-frame array allocation from RaycastAll while hovering).
    private readonly RaycastHit[] _hoverHitsBuffer = new RaycastHit[16];
    private BuildMenuUI _buildMenuUI;
    private TopBarUI _topBar;

    // Hover popup (assigned by UIBootstrapper)
    private WorldHoverPopupUI _hoverUI;

    // Injected externally
    public GameContext Context { get; private set; }

    [SerializeField] private PreviewCostUI _costUI;
    
    [Header("Destruction Settings")]
    [SerializeField] private float destructionDuration = 1.0f;
    [SerializeField] private float destructionSinkAmount = 1.5f;
    [SerializeField] private float destructionVibrationAmount = 0.05f;
    [SerializeField] private float destructionVibrationSpeed = 50.0f;

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

        // Adopt BuildService's CommandHistory so undo/redo state stays unified — BuildService
        // is registered by GameContext.Awake() before this runs. Fallback to a fresh instance
        // if it's somehow missing, so the FSM never ends up with a null History.
        History = ServiceLocator.TryGet<BuildService>(out var buildService) && buildService != null
            ? buildService.CommandHistory
            : new CommandHistory();
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
        _raycast = FindAnyObjectByType<RaycastController>();
        _indicator = FindAnyObjectByType<CellIndicatorController>();
        _preview = FindAnyObjectByType<PreviewController>();
        PlacementValidator validator = FindAnyObjectByType<PlacementValidator>();
        PlacementFinalizer finalizer = FindAnyObjectByType<PlacementFinalizer>();
        PlacementGrid grid = FindAnyObjectByType<PlacementGrid>();
        _buildMenuUI = FindAnyObjectByType<BuildMenuUI>();
        _topBar = FindAnyObjectByType<TopBarUI>();

        if (_raycast != null) _raycast.EnableRay();

        // ---------------------------------------------------------
        // SAFETY: Fallback initialization if Context wasn't set yet
        // ---------------------------------------------------------
        if (Context == null)
        {
            var ctx = FindAnyObjectByType<GameContext>();
            if (ctx != null) Initialize(ctx);
        }

        if (Context == null)
        {
            Debug.LogError("[PlacementStateMachine] FSM failed to find GameContext! Many states will fail.");
            return;
        }

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
            Context.MoneyService,
            destructionDuration,
            destructionSinkAmount,
            destructionVibrationAmount,
            destructionVibrationSpeed);

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
        if (_currentState != null) _currentState.OnEnter();

        // Wire UI events to state transitions
        if (_buildMenuUI != null)
        {
            _buildMenuUI.OnBuildItemClicked += (data) => EnterBuild(data);
            _buildMenuUI.OnDeleteClicked += () => EnterDelete();
            _buildMenuUI.OnMoveClicked += () => EnterMove();
            _buildMenuUI.OnUndoClicked += () => Undo();
            _buildMenuUI.OnRedoClicked += () => Redo();
            _buildMenuUI.OnCancelClicked += () => ReturnToPrevious();
            // Rotation is handled via input actions (R key), not UI button
        }
    }

    // ---------------------------------------------------------
    // UPDATE LOOP
    // ---------------------------------------------------------
    private void Update()
    {
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
            {
                // We don't call raycast.Tick() again here because IdleState already did it.
                HandleIdleHover(tickRaycast: false);

                // Ctrl + Left Click on a pallet opens PalletBuilder UI. Shift+Click is reserved for
                // Slot Assignment — this used to be bound to Shift and directly duplicated
                // PalletBuilder.OnMouseDown's own click handling (which is Ctrl-gated), so the two
                // disagreed about which modifier opened the panel. This IS the actual reason
                // Shift+Click kept opening Pallet Builder even after OnMouseDown was fixed.
                bool _ctrlHeld = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
                if (Mouse.current.leftButton.wasPressedThisFrame && _ctrlHeld && !_raycast.IsPointerOverUI)
                {
                    if (_raycast.HitObject != null)
                    {
                        var pb = _raycast.HitObject.GetComponentInParent<PalletBuilder>();
                        if (pb != null)
                        {
                            _hoverUI.HideImmediate();
                            pb.ToggleUI();
                        }
                    }
                }

                // Shift + Left Click: toggle lights OR open Slot Assignment on racks
                bool _shiftHeld = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                if (Mouse.current.leftButton.wasPressedThisFrame && _shiftHeld && !_raycast.IsPointerOverUI)
                {
                    if (_raycast.HitObject != null)
                    {
                        // Check if it's a light fixture — toggle all lights to match the clicked light's new state
                        var light = _raycast.HitObject.GetComponentInParent<Light>();
                        if (light != null)
                        {
                            bool newState = !light.enabled;
                            var allLights = FindObjectsByType<Light_Adjustments>();
                            foreach (var lightAdj in allLights)
                            {
                                lightAdj.SetState(newState);
                            }
                            _hoverUI.HideImmediate();
                            return;
                        }

                        // Otherwise, check if it's a live rack — open Slot Assignment panel
                        var rackPo = _raycast.HitObject.GetComponentInParent<PlacedObject>();
                        if (rackPo != null && rackPo.isRackLive && rackPo.data != null
                            && rackPo.data.category == "Racking" && rackPo.rackAisle >= 0
                            && _topBar != null && _topBar.SlotAssignmentPanel != null)
                        {
                            _hoverUI.HideImmediate();
                            _topBar.SlotAssignmentPanel.ShowForAisle(rackPo.rackAisle);
                        }
                    }
                }
            }
        }
        // Removed forced hide here as it conflicts with states that want to show hover info (like Delete/Move)


        // -----------------------------------------------------
        // KEYBOARD SHORTCUTS — suppressed while a modal owns input
        // -----------------------------------------------------
        // A text-entry modal (Save/Load, Rack Setup, Lane Setup) has the keyboard. Undo/Redo, Esc and
        // Tab must not fire underneath it — typing a save name containing Z or Y would otherwise
        // rewrite the build history behind the dialog. Mouse-driven state ticking above is unaffected.
        if (UIModalGuard.IsCapturing) return;

        // -----------------------------------------------------
        // UNDO / REDO (Global Shortcuts)
        // -----------------------------------------------------
        if (Keyboard.current.ctrlKey.isPressed)
        {
            if (Keyboard.current.zKey.wasPressedThisFrame)
            {
                Undo();
            }
            else if (Keyboard.current.yKey.wasPressedThisFrame)
            {
                Redo();
            }
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

        // -----------------------------------------------------
        // CLOSE ALL UI PANELS (TAB)
        // -----------------------------------------------------
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            // ShiftManagerPanel (key 5) needs special handling: TryClose saves changes if dirty
            var shiftManager = _topBar != null ? _topBar.ShiftManagerPanel : null;
            if (shiftManager != null && shiftManager.IsOpen)
            {
                shiftManager.TryClose();
                return; // Exit early since ShiftManager's TryClose handles hiding
            }

            // Close all other keybinding UIs (1-4, 6-7)
            var uiManager = UIKeyBindingManager.Instance;
            if (uiManager != null)
                uiManager.CloseAll();

            // Close EmployeeInfoUI if open (opens when clicking an employee)
            var employeeInfoUI = FindObjectOfType<EmployeeInfoUI>();
            if (employeeInfoUI != null && employeeInfoUI.IsVisible)
                employeeInfoUI.Hide();

            // Close SlotAssignmentPanel if open
            var slotAssignmentPanel = _topBar != null ? _topBar.SlotAssignmentPanel : null;
            if (slotAssignmentPanel != null && slotAssignmentPanel.IsVisible)
                slotAssignmentPanel.Hide();

            // The retained WorkQueuePanel is owned by TopBarUI and closed by the CloseAll() above —
            // true only since it was actually registered (key 7) and made to implement IUIPanel.
            // Before that this comment described an intention, not behaviour: CloseAll() iterates the
            // registry, the panel wasn't in it, and Tab left the modal open.

            // Close NewItemPanel (key 8) if open
            var newItemPanel = _topBar != null ? _topBar.NewItemPanel : null;
            if (newItemPanel != null && newItemPanel.IsVisible)
                newItemPanel.Hide();
        }
    }

    /// <summary>
    /// Handles hover popup behavior ONLY in IdleState.
    /// </summary>
private void HandleIdleHover(bool tickRaycast = true)
    {
        if (tickRaycast) _raycast.Tick();

        if (_raycast.HitObject != null)
        {
            // Walk EVERY collider along the ray (closest first), not just the single closest
            // hit -- a rack's own big trigger BoxCollider spans its whole footprint, so from
            // most camera angles it sits in front of (closer to camera than) a pallet resting
            // on one of its shelves or a label mounted on its face. A single-hit raycast would
            // only ever find the rack shell and block anything behind it; this lets a more
            // specific hit (pallet, rack slot/label) further along the same ray win instead.
            int count = Physics.RaycastNonAlloc(_raycast.CurrentRay, _hoverHitsBuffer, 500f, _raycast.CurrentObjectMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(_hoverHitsBuffer, 0, count, Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

            GameObject fallbackBuildingHit = null;

            for (int i = 0; i < count; i++)
            {
                var go = _hoverHitsBuffer[i].collider.gameObject;
                Vector3 hitPoint = _hoverHitsBuffer[i].point;

                var palletData = go.GetComponentInParent<GameCore.Inventory.PalletData>();
                if (palletData != null)
                {
                    _hoverUI.TickHoverPallet(true, palletData, hitPoint, Camera.main);
                    return;
                }

                var palletBuilder = go.GetComponentInParent<PalletBuilder>();
                if (palletBuilder != null)
                {
                    _hoverUI.TickHoverPalletBuilder(true, palletBuilder, hitPoint, Camera.main);
                    return;
                }

                var locationData = go.GetComponentInParent<LocationData>();
                if (locationData != null)
                {
                    _hoverUI.TickHoverLocation(true, locationData, hitPoint, Camera.main);
                    return;
                }

                // Rack label hit -- resolve LocationData via the label's own address text. Must
                // be the label's OWN direct child, not a deep GetComponentInChildren -- that
                // would match ANY TMP label anywhere under a hit object (e.g. the rack's own big
                // BoxCollider hits the rack root, which has every label on it as a descendant).
                TMPro.TextMeshPro labelText = null;
                foreach (Transform childT in go.transform)
                {
                    labelText = childT.GetComponent<TMPro.TextMeshPro>();
                    if (labelText != null) break;
                }
                if (labelText != null && !string.IsNullOrWhiteSpace(labelText.text)
                    && LocationRegistry.TryGet(labelText.text, out var labelLocationData))
                {
                    _hoverUI.TickHoverLocation(true, labelLocationData, hitPoint, Camera.main);
                    return;
                }

                // Generic building fallback -- remember the FIRST (closest) one, but keep
                // looking further down the ray in case a more specific hit is behind it.
                // Racks are excluded: they only ever show their location/label tooltip (or
                // nothing, when no label/pallet is under the cursor), never the generic one.
                if (fallbackBuildingHit == null)
                {
                    var bd = go.GetComponentInParent<BuildingData>();
                    if (bd != null && bd.Data != null && bd.Data.category != "Racking") fallbackBuildingHit = go;
                }
            }

            if (fallbackBuildingHit != null)
            {
                var bd = fallbackBuildingHit.GetComponentInParent<BuildingData>();
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

        // No hit or no building/pallet → hide popup
        _hoverUI.TickHover(false, null, 0, 0, Vector3.zero, null);
    }

    private void OnEnable() => _actions?.Enable();
    private void OnDisable() => _actions?.Disable();

    private void OnDestroy()
    {
        _actions?.Dispose();
    }

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
