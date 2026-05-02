using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildState : IPlacementState
{
    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementGrid _grid;
    private readonly PlacementStateMachine _fsm;
    private readonly RaycastController _raycast;
    private readonly CellIndicatorController _indicator;
    private readonly MoneyService _money;
    private readonly PreviewCostUI _costUI;
    private readonly WorldHoverPopupUI _hoverUI;
    private readonly BuildMenuUI _buildMenuUI;

    private ObjDataSO _currentData;

    private bool _placeRequested;
    private bool _rotateRequested;
    private float _currentRotation;

    private bool _isDragging;
    private Vector2Int _dragStartCell;
    private readonly List<Vector2Int> _dragCells = new();

    private readonly List<Vector2Int> _indicatorBuffer = new();
    private readonly List<Vector2Int> _footprintBuffer = new();

    // ⭐ MOVE MODE SUPPORT ⭐
    private bool _isMoveMode = false;
    private GameObject _moveObj;
    private Vector2Int _moveOriginalRoot;
    private float _moveOriginalRotation;
    private Vector2Int[] _moveOffsets;

    public bool IsPlacementState => true;
    public ObjDataSO CurrentData => _currentData;
    public bool IsDragging => _isDragging;
    public string ObjectName => _currentData != null ? _currentData.objName : "None";

    public BuildState(
        PlacementActions actions,
        PreviewController preview,
        PlacementValidator validator,
        PlacementFinalizer finalizer,
        PlacementGrid grid,
        PlacementStateMachine fsm,
        RaycastController raycast,
        CellIndicatorController indicator,
        MoneyService money,
        PreviewCostUI costUI,
        WorldHoverPopupUI hoverUI,
        BuildMenuUI buildMenuUI)
    {
        _actions = actions;
        _preview = preview;
        _validator = validator;
        _finalizer = finalizer;
        _grid = grid;
        _fsm = fsm;
        _raycast = raycast;
        _indicator = indicator;
        _money = money;
        _costUI = costUI;
        _hoverUI = hoverUI;
        _buildMenuUI = buildMenuUI;

        _actions.BuildPlacement.BindRotateTo_R();
        _actions.BuildPlacement.BindPlaceToMouseLeft();
    }

    // ---------------------------------------------------------
    // ENTER
    // ---------------------------------------------------------
    public void OnEnter()
    {
        _hoverUI.DisableForBuildMode();

        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;
        _actions.BuildPlacement.Place.canceled += OnPlacePerformed;

        Object.FindAnyObjectByType<TopBarUI>().SetState(GetType().Name);

        if (_currentData == null)
            return;

        _indicator.UseBuildMode();
        _raycast.EnableRay();
        _preview.Show(_currentData);

        _placeRequested = false;
        _rotateRequested = false;

        _currentRotation = _preview.CurrentRotation;

        _isDragging = false;
        _dragCells.Clear();

        _costUI.Hide();
    }

    // ---------------------------------------------------------
    // EXIT
    // ---------------------------------------------------------
    public void OnExit()
    {
        _hoverUI.EnableAfterBuildMode();

        _raycast.DisableRay();
        _indicator.ClearAll();
        _preview.Hide();
        _costUI.Hide();

        _actions.BuildPlacement.Place.canceled -= OnPlacePerformed;
        _actions.BuildPlacement.Rotate.performed -= OnRotatePerformed;

        // Reset move mode
        _isMoveMode = false;
        _moveObj = null;
        _moveOffsets = null;
    }

    // ---------------------------------------------------------
    // MOVE MODE ENTRY (called by MoveState)
    // ---------------------------------------------------------
    public void EnterMoveMode(GameObject obj, Vector2Int root, float rotation, Vector2Int[] offsets)
    {
        _isMoveMode = true;
        _moveObj = obj;
        _moveOriginalRoot = root;
        _moveOriginalRotation = rotation;
        _moveOffsets = offsets;

        _currentData = obj.GetComponent<BuildingData>().Data;
        _currentRotation = rotation;

        _preview.Show(_currentData);
        _preview.Rotate(rotation);
    }

    // ---------------------------------------------------------
    // TICK
    // ---------------------------------------------------------
    public void Tick()
    {
        _raycast.Tick();

        if (!_raycast.HasHit)
        {
            _indicator.ClearAll();
            _preview.Hide();
            _costUI.Hide();
            return;
        }

        Vector2Int root = _raycast.HitCell;

        // -----------------------------------------------------
        // HOVER UI
        // -----------------------------------------------------
        if (_raycast.HitObject != null)
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
            }
            else
            {
                _hoverUI.TickHover(false, null, 0, 0, Vector3.zero, null);
            }
        }
        else
        {
            _hoverUI.TickHover(false, null, 0, 0, Vector3.zero, null);
        }

        // -----------------------------------------------------
        // DRAG / CLICK DETECTION
        // -----------------------------------------------------
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragCells.Clear();
            _dragStartCell = root;
        }

        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            if (root != _dragStartCell)
            {
                _isDragging = true;

                _preview.Hide();
                _indicator.ClearAll();
                _costUI.Hide();

                _preview.BeginSelectionCells();
                return;
            }
        }

        if (_isDragging)
        {
            HandleDragPlacement(root);
            return;
        }

        // -----------------------------------------------------
        // ROTATION
        // -----------------------------------------------------
        if (_rotateRequested)
        {
            _rotateRequested = false;

            _currentRotation += 90f;
            if (_currentRotation >= 360f)
                _currentRotation = 0f;

            _preview.Rotate(_currentRotation);
        }

        // -----------------------------------------------------
        // GHOST + VALIDATION
        // -----------------------------------------------------
        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

        _preview.MoveTo(_grid.GetCellCenter(root), root, _currentData);

        bool isValid = _validator.IsValidPlacement(root, offsets, _currentData);

        _indicator.ShowCells(
            BuildFootprintBuffered(root, offsets),
            cell => isValid
        );

        if (isValid)
            _preview.SetGhostValid();
        else
            _preview.SetGhostInvalid();

        // -----------------------------------------------------
        // COST PREVIEW (disabled in move mode)
        // -----------------------------------------------------
        if (!_isMoveMode)
        {
            int cost = _currentData.cost;
            bool canAfford = _money.CanAfford(cost);

            _costUI.ShowCost(cost, canAfford);
            _costUI.SetScreenPosition(_raycast.RawHitPoint, Camera.main);
        }

        // Prevent placing behind UI
        if (_buildMenuUI.IsPointerOverBuildMenu)
        {
            _placeRequested = false;
            return;
        }

        // -----------------------------------------------------
        // PLACE
        // -----------------------------------------------------
        if (_placeRequested)
        {
            _placeRequested = false;

            bool isValidNow = _validator.IsValidPlacement(root, offsets, _currentData);

            if (!isValidNow)
            {
                AudioManager.Play("InvalidPlace");
                _preview.SetGhostInvalid();
                _indicator.ShowCells(BuildFootprintBuffered(root, offsets), cell => false);
                return;
            }

            AudioManager.Play("ValidPlace");

            if (_isMoveMode)
            {
                // ⭐ MOVE COMMAND ⭐
                _fsm.History.Push(
                    new MoveCommand(
                        _grid,
                        _moveObj,
                        _currentData,
                        _moveOriginalRoot,
                        root,
                        _moveOffsets,
                        _moveOriginalRotation
                    )
                );
            }
            else
            {
                // ⭐ PLACE COMMAND ⭐
                _fsm.History.Push(
                    new PlaceCommand(
                        _grid,
                        _finalizer,
                        root,
                        offsets,
                        _currentData,
                        _currentRotation,
                        _money)
                );
            }
        }
    }

    // ---------------------------------------------------------
    // ROTATE
    // ---------------------------------------------------------
    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        AudioManager.Play("Rotate");
        _rotateRequested = true;
    }

    // ---------------------------------------------------------
    // PLACE INPUT
    // ---------------------------------------------------------
    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        if (_isDragging)
            return;

        //if (_currentData == null)
            //return;

        if (!_raycast.HasHit)
            return;

        Vector2Int root = _raycast.HitCell;
        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

        bool isValid = _validator.IsValidPlacement(root, offsets, _currentData);

        GameObject hitObj = _raycast.HitObject;
        if (hitObj != null)
        {
            var bd = hitObj.GetComponent<BuildingData>();
            if (bd != null && bd.Data.ClearsGridAfterPlacement)
                isValid = false;
        }

        if (!isValid)
        {
            AudioManager.Play("InvalidPlace");
            return;
        }

        _placeRequested = true;
    }

    // ---------------------------------------------------------
    // DRAG PLACEMENT
    // ---------------------------------------------------------
    private void HandleDragPlacement(Vector2Int currentCell)
    {
        // unchanged from your original — drag logic stays the same
        // (omitted here for brevity, but you can paste your existing version)
    }

    // ---------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------
    private List<Vector2Int> BuildFootprintBuffered(Vector2Int root, Vector2Int[] offsets)
    {
        _footprintBuffer.Clear();

        foreach (var o in offsets)
            _footprintBuffer.Add(root + o);

        return _footprintBuffer;
    }
    // Get data from BuildData SO
    public void SetBuildData(ObjDataSO data)
    {
        _currentData = data;
        _isMoveMode = false;     // ensure normal build mode
        _moveObj = null;
        _moveOffsets = null;
    }
}
