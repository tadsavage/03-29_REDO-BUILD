using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MoveState : IPlacementState
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

    private GameObject _obj;
    private ObjDataSO _data;

    private Vector2Int _originalRoot;
    private Vector2Int[] _offsets;
    private float _rotation;

    private bool _hasSelection;

    // NEW — track last hovered cell for "NewCell" sound
    private Vector2Int _lastHoverCell = new Vector2Int(int.MinValue, int.MinValue);

    // Reusable footprint buffer (no GC)
    private readonly List<Vector2Int> _footprint = new();

    public bool IsPlacementState => true;
    public string ObjectName => _obj != null ? _obj.name : "None";

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

        _actions.BuildPlacement.BindPlaceToMouseLeft();
        _actions.BuildPlacement.BindRotateTo_R();
    }

    // ---------------------------------------------------------
    // ENTER
    // ---------------------------------------------------------
    public void OnEnter()
    {
        _actions.BuildPlacement.Place.performed += OnConfirmMove;
        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;

        _raycast.EnableRay();
        _preview.ResetMoveGhostState();
        _indicator.UseMoveMode(); // same visuals as BuildState
        _hasSelection = false;
        _lastHoverCell = new Vector2Int(int.MinValue, int.MinValue);
    }

    // ---------------------------------------------------------
    // EXIT
    // ---------------------------------------------------------
    public void OnExit()
    {
        _raycast.DisableRay();
        _preview.ResetMoveGhostState();
        _indicator.ClearAll();

        if (_obj != null)
            _preview.RemoveHighlight(_obj);

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
    // SELECT OBJECT
    // ---------------------------------------------------------
    private void TrySelectObject()
    {
        _raycast.Tick();

        if (_raycast.HitObject == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        var bd = _raycast.HitObject.GetComponent<BuildingData>();
        if (bd == null || bd.Data.ClearsGridAfterPlacement)
            return;

        // Must be top of stack
        foreach (var o in bd.Offsets)
        {
            var list = _grid.GetObjectsInCell(bd.RootCell + o);
            if (list != null && list.Count > 0)
            {
                if (list[list.Count - 1].instance != _raycast.HitObject)
                {
                    AudioManager.Play("InvalidPlace");
                    return;
                }
            }
        }
        // Select object
        _obj = bd.gameObject;
        _data = bd.Data;
        _offsets = bd.Offsets;
        _rotation = bd.Rotation;
        _originalRoot = bd.RootCell;

        _preview.ApplyHighlight(_obj);
        _preview.ShowGhost(_obj);

        // Remove from grid BEFORE disabling
        foreach (var o in _offsets)
        {
            Vector2Int cell = _originalRoot + o;
            _grid.RemoveStackObject(cell, _obj, _data);
        }

        _obj.SetActive(false);
        _hasSelection = true;
    }

    // ---------------------------------------------------------
    // TICK — FULL BUILDSTATE‑QUALITY UX
    // ---------------------------------------------------------
    public void Tick()
    {        // -- Show Cell Indicator on hover (even if invalid) ---
        Vector2Int newRoot = _raycast.HitCell;
        _indicator.ShowCell(newRoot);
        if (!_hasSelection)
        {
            TrySelectObject();
            return;
        }

        _raycast.Tick();

        if (!_raycast.HasHit)
        {
            _preview.HideGhost();
            return;
        }

        // --- NEW CELL SOUND ---
        if (newRoot != _lastHoverCell)
        {
            AudioManager.Play("NewCell");
            _lastHoverCell = newRoot;
        }

        // --- VALIDATION ---
        bool valid = _validator.IsValidPlacement(newRoot, _offsets, _data, _obj);

        // --- FOOTPRINT INDICATOR ---
        _footprint.Clear();
        foreach (var o in _offsets)
            _footprint.Add(newRoot + o);

        _indicator.ShowCells(
            _footprint,
            cell => valid // blue if valid, red if invalid
        );

        // --- STACK HEIGHT ---
        float stackY = 0f;
        if (_data.isStackable)
            stackY = _grid.GetStackHeight(newRoot, _obj);

        // --- GHOST POSITION ---
        Vector3 pos = _grid.GetCellCenter(newRoot);
        pos.y += stackY;
        _preview.UpdateGhostPosition(pos);

        // --- GHOST COLOR ---
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
        if (!_hasSelection)
            return;

        Vector2Int newRoot = _raycast.HitCell;

        if (!_validator.IsValidPlacement(newRoot, _offsets, _data, _obj))
        {
            AudioManager.Play("InvalidPlace");
            return;
        }

        AudioManager.Play("ValidPlace");

        _preview.RemoveHighlight(_obj);

        // Push move command
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

        // Reset selection but remain in MoveState
        _obj = null;
        _data = null;
        _offsets = null;
        _rotation = 0f;
        _originalRoot = default;
        _hasSelection = false;

        _indicator.ClearAll();
    }

    // ---------------------------------------------------------
    // ROTATE
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

        // Recompute offsets for rotated footprint
        _offsets = _data.GetFootprintOffsets(-_rotation);
    }
}
