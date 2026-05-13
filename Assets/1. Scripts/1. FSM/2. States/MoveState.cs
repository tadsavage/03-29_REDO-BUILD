using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles selecting an existing placed object, picking it up,
/// moving it around the grid, rotating it, validating placement,
/// and confirming the new position.
/// 
/// Key behavior:
/// - Clicking ANY footprint cell selects the object.
/// - If the user clicked an offset cell, movement preserves that offset.
/// - Rotation only applies to the currently selected object.
/// - No rotation leaks between objects.
/// </summary>
public class MoveState : IPlacementState
{
    // ---------------------------------------------------------
    // DEPENDENCIES
    // ---------------------------------------------------------
    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementGrid _grid;
    private readonly PlacementStateMachine _fsm;
    private readonly RaycastController _raycast;
    private readonly CellIndicatorController _indicator;
    private readonly MoneyService _money;
    private TopBarUI _topBarUI;

    private TopBarUI topBarUI => _topBarUI != null ? _topBarUI : _topBarUI = Object.FindAnyObjectByType<TopBarUI>();

    // ---------------------------------------------------------
    // SELECTED OBJECT DATA
    // ---------------------------------------------------------
    private GameObject _obj;          // The actual object being moved
    private ObjDataSO _data;          // Its data (footprint, cost, etc.)
    private Vector2Int[] _offsets;    // Footprint offsets (rotated)
    private float _rotation;          // Current rotation (0/90/180/270)
    private Vector2Int _originalRoot; // Where the object started

    private bool _hasSelection;

    // ---------------------------------------------------------
    // OFFSET‑AWARE SELECTION
    // ---------------------------------------------------------
    private Vector2Int _clickedCell;      // Cell user clicked on
    private Vector2Int _originAtSelect;   // Root at selection time
    private Vector2Int _selectionDelta;   // clickedCell - originAtSelect

    // ---------------------------------------------------------
    // MOVEMENT + VISUALS
    // ---------------------------------------------------------
    private Vector2Int _lastHoverCell = new Vector2Int(int.MinValue, int.MinValue);
    private readonly List<Vector2Int> _footprint = new();

    private static readonly Color MoveHighlightBlue =
        new Color(0.20f, 0.60f, 1.00f, 0.15f);

    public bool IsPlacementState => true;
    public string ObjectName => _obj != null ? _obj.name : "None";

    // ---------------------------------------------------------
    // CONSTRUCTOR
    // ---------------------------------------------------------
    public MoveState(
        PlacementActions actions,
        PreviewController preview,
        PlacementValidator validator,
        PlacementFinalizer finalizer,
        PlacementGrid grid,
        PlacementStateMachine fsm,
        RaycastController raycast,
        CellIndicatorController indicator,
        MoneyService money)
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

        // Bind controls
        _actions.BuildPlacement.BindPlaceToMouseLeft();
        _actions.BuildPlacement.BindRotateTo_R();
    }

    // ---------------------------------------------------------
    // ENTER / EXIT
    // ---------------------------------------------------------
    public void OnEnter()
    {
        _actions.BuildPlacement.Place.performed += OnConfirmMove;
        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;

        _raycast.EnableRay();
        _preview.ResetMoveGhostState();
        _indicator.UseMoveMode();

        _hasSelection = false;
        _lastHoverCell = new Vector2Int(int.MinValue, int.MinValue);

        Object.FindAnyObjectByType<TopBarUI>().SetState(GetType().Name);
    }

    public void OnExit()
    {
        _raycast.DisableRay();
        _preview.ResetMoveGhostState();
        _indicator.ClearAll();

        if (_obj != null)
            _preview.ClearFlatHighlight(_obj);

        // Clear selection state
        _obj = null;
        _data = null;
        _offsets = null;
        _rotation = 0f;
        _originalRoot = default;
        _hasSelection = false;

        _actions.BuildPlacement.Rotate.performed -= OnRotatePerformed;
        _actions.BuildPlacement.Place.performed -= OnConfirmMove;
    }

    // ---------------------------------------------------------
    // SELECT OBJECT - Stack Aware
    // ---------------------------------------------------------
    private void TrySelectObject()
    {
        _raycast.Tick();

        // Must click something
        if (_raycast.HitObject == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // Determine which cell was clicked
        Vector2Int clickedCell = _raycast.HitCell;

        // Ask grid for the TRUE top object in that cell
        GameObject trueTop = _grid.GetTopObject(clickedCell);

        // If nothing is on this cell, bail
        if (trueTop == null)
            return;

        // Get BuildingData from the TRUE top object
        var bd = trueTop.GetComponent<BuildingData>();
        if (bd == null || bd.Data == null)
            return;
        if (bd == null || bd.Data == null)
            return;
        // Cannot move animated-type navmesh objects
        if (bd.Data.ClearsGridAfterPlacement)
            return;

        // Capture object data
        _obj = bd.gameObject;
        _data = bd.Data;
        _offsets = bd.Offsets;
        _rotation = bd.Rotation;
        _originalRoot = bd.RootCell;

        // -----------------------------------------------------
        // OFFSET‑AWARE SELECTION
        // -----------------------------------------------------
        _clickedCell = _raycast.HitCell;
        _originAtSelect = _originalRoot;
        _selectionDelta = _clickedCell - _originAtSelect;
        // If clicked origin → (0,0)
        // If clicked offset → e.g. (-1,0)

        // Highlight + show ghost
        _preview.ApplyFlatHighlight(_obj, MoveHighlightBlue);
        _preview.Show(_data);
        _preview.Rotate(_rotation); // IMPORTANT: match selected object's rotation

        // Position ghost at original root
        Vector3 startPos = _grid.GetCellCenter(_originalRoot);
        _preview.MoveTo(startPos, _originalRoot, _data);
        _lastHoverCell = _originalRoot;

        // Remove object from grid while moving
        foreach (var o in _offsets)
        {
            Vector2Int cell = _originalRoot + o;
            _grid.RemoveStackObject(cell, _obj, _data);
        }

        _obj.SetActive(false);
        _hasSelection = true;
    }

    // ---------------------------------------------------------
    // TICK — MOVEMENT + VALIDATION
    // ---------------------------------------------------------
    public void Tick()
    {
        _raycast.Tick();

        if (_raycast.IsPointerOverUI)
        {
            if (!_hasSelection)
            {
                _indicator.ClearAll();
                return;
            }
            // If moving, we still want to show the ghost maybe? 
            // Or hide it? Usually, if you move the mouse over UI while holding an object, 
            // the object should stay at its last valid position or hide.
            _preview.HideGhost();
            _indicator.ClearAll();
            return;
        }

        // Restore ghost if we have a selection and just left the UI
        if (_hasSelection)
        {
            _preview.Show(_data);
            _preview.Rotate(_rotation);
        }

        Vector2Int hitCell = _raycast.HitCell;
_indicator.ShowCell(hitCell);
        topBarUI?.SetCell(hitCell.x, hitCell.y);

        if (!_hasSelection)
        {
            TrySelectObject();
            return;
        }

        if (!_raycast.HasHit)
        {
            _preview.HideGhost();
            return;
        }

        // -----------------------------------------------------
        // OFFSET‑AWARE MOVEMENT
        // newRoot = hitCell - (clickedCell - originAtSelect)
        // -----------------------------------------------------
        Vector2Int newRoot = hitCell - _selectionDelta;

        if (newRoot != _lastHoverCell)
        {
            AudioManager.Play("NewCell");
            _lastHoverCell = newRoot;
        }

        bool valid = _validator.IsValidPlacement(newRoot, _offsets, _data, _obj);

        // Build footprint for indicator
        _footprint.Clear();
        foreach (var o in _offsets)
            _footprint.Add(newRoot + o);

        _indicator.ShowCells(_footprint, cell => valid);

        // Move ghost
        Vector3 pos = _grid.GetCellCenter(newRoot);
        _preview.MoveTo(pos, newRoot, _data);

        if (valid)
            _preview.SetGhostValid();
        else
            _preview.SetGhostInvalid();
    }

    // ---------------------------------------------------------
    // CONFIRM MOVE
    // ---------------------------------------------------------
    private void OnConfirmMove(InputAction.CallbackContext ctx)
    {
        if (!_hasSelection || _raycast.IsPointerOverUI)
            return;

        Vector2Int hitCell = _raycast.HitCell;
Vector2Int newRoot = hitCell - _selectionDelta;

        if (!_validator.IsValidPlacement(newRoot, _offsets, _data, _obj))
        {
            AudioManager.Play("InvalidPlace");
            return;
        }

        AudioManager.Play("ValidPlace");

        _preview.ClearFlatHighlight(_obj);

        // Push undo/redo command
        _fsm.History.Push(
            new MoveCommand(
                _grid,
                _obj,
                _data,
                _originalRoot,
                newRoot,
                _offsets,
                _rotation
            )
        );

        _preview.ResetMoveGhostState();

        // Clear selection
        _obj = null;
        _data = null;
        _offsets = null;
        _rotation = 0f;
        _originalRoot = default;
        _hasSelection = false;

        _indicator.ClearAll();
    }

    // ---------------------------------------------------------
    // ROTATE SELECTED OBJECT
    // ---------------------------------------------------------
    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        if (!_hasSelection)
            return;

        AudioManager.Play("Rotate");

        _rotation += 90f;
        if (_rotation >= 360f)
            _rotation = 0f;

        _preview.Rotate(_rotation);

        // Update footprint for new rotation
        if (_data != null)
            _offsets = _data.GetFootprintOffsets(-_rotation);
    }
}
