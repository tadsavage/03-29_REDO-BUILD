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

    private Vector2Int _lastHoverCell = new Vector2Int(int.MinValue, int.MinValue);

    private readonly List<Vector2Int> _footprint = new();

    private static readonly Color MoveHighlightBlue = new Color(0.20f, 0.60f, 1.00f, 0.15f);

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
        _indicator.ShowCell(_raycast.HitCell);

        if (_raycast.HitObject == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // Prefer BuildingData on the hit object, but allow parent lookup (raycast may hit child meshes)
        var bd = _raycast.HitObject.GetComponent<BuildingData>()
                 ?? _raycast.HitObject.GetComponentInParent<BuildingData>();

        if (bd == null)
            return;

        if (bd.Data == null)
        {
            Debug.LogWarning($"TrySelectObject: BuildingData on {bd.gameObject.name} has no Data (ObjDataSO).");
            return;
        }

        // Ensure offsets exist; compute from SO + rotation if missing and persist via BuildingData API
        Vector2Int[] offsets = bd.Offsets;
        if (offsets == null || offsets.Length == 0)
        {
            Debug.LogWarning($"TrySelectObject: Offsets missing on {bd.gameObject.name}. Computing from SO as fallback.");
            // IMPORTANT: use the same convention as placement (negative rotation)
            offsets = bd.Data.GetFootprintOffsets(-bd.Rotation);
            if (offsets == null || offsets.Length == 0)
            {
                Debug.LogError($"TrySelectObject: Could not compute offsets for {bd.gameObject.name}. Aborting selection.");
                return;
            }

            // Persist computed offsets back to BuildingData
            bd.SetOffsets(offsets);
        }

        if (bd.Data.ClearsGridAfterPlacement)
            return;

        // Resolve the TRUE top object across the footprint
        GameObject trueTop = null;

        foreach (var o in offsets)
        {
            Vector2Int cell = bd.RootCell + o;
            GameObject topGO = _grid.GetTopObject(cell);
            if (topGO == null)
                continue;

            if (trueTop == null)
                trueTop = topGO;
            else if (trueTop != topGO)
            {
                Debug.LogWarning($"TrySelectObject: conflicting top objects across footprint: {trueTop.name} vs {topGO.name}");
                AudioManager.Play("InvalidPlace");
                return;
            }
        }

        if (trueTop == null)
        {
            Debug.Log("TrySelectObject: trueTop is null after scanning footprint — aborting selection.");
            return;
        }

        // If the raycast hit a child mesh, prefer the canonical top object's BuildingData
        if (trueTop != _raycast.HitObject)
        {
            bd = trueTop.GetComponent<BuildingData>() ?? trueTop.GetComponentInParent<BuildingData>();
            if (bd == null)
            {
                Debug.LogWarning("TrySelectObject: trueTop has no BuildingData; aborting.");
                return;
            }

            // Use its offsets (already stored consistently)
            offsets = bd.Offsets;
            if (offsets == null || offsets.Length == 0)
            {
                offsets = bd.Data.GetFootprintOffsets(-bd.Rotation);
                bd.SetOffsets(offsets);
            }
        }

        // Select object
        _obj = bd.gameObject;
        _data = bd.Data;
        _offsets = offsets;
        _rotation = bd.Rotation;
        _originalRoot = bd.RootCell;

        _preview.ApplyFlatHighlight(_obj, MoveHighlightBlue);
        _preview.Show(_data);

        // Remove from grid BEFORE disabling so grid state is consistent
        foreach (var o in _offsets)
        {
            Vector2Int cell = _originalRoot + o;
            _grid.RemoveStackObject(cell, _obj, _data);
        }

        _obj.SetActive(false);
        _hasSelection = true;
    }

    // ---------------------------------------------------------
    // TICK
    // ---------------------------------------------------------
    public void Tick()
    {
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

        if (newRoot != _lastHoverCell)
        {
            AudioManager.Play("NewCell");
            _lastHoverCell = newRoot;
        }

        if (_data == null || _offsets == null)
        {
            Debug.LogWarning("Tick: missing _data or _offsets while in move mode; cancelling selection.");
            _hasSelection = false;
            return;
        }

        bool valid = _validator.IsValidPlacement(newRoot, _offsets, _data, _obj);

        _footprint.Clear();
        foreach (var o in _offsets)
            _footprint.Add(newRoot + o);

        _indicator.ShowCells(_footprint, cell => valid);

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
        if (!_hasSelection)
            return;

        if (_data == null || _offsets == null)
        {
            Debug.LogWarning("OnConfirmMove: missing data or offsets; aborting.");
            return;
        }

        Vector2Int newRoot = _raycast.HitCell;

        if (!_validator.IsValidPlacement(newRoot, _offsets, _data, _obj))
        {
            AudioManager.Play("InvalidPlace");
            return;
        }

        AudioManager.Play("ValidPlace");

        _preview.ClearFlatHighlight(_obj);

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

        // Recompute offsets for the new rotation using the SAME convention as placement
        if (_data != null)
            _offsets = _data.GetFootprintOffsets(-_rotation);
    }
}
