using UnityEngine;
using UnityEngine.InputSystem;

public class MoveState : IPlacementState
{
    private readonly PlacementActions _actions;
    private readonly PlacementStateMachine _fsm;
    private readonly RaycastController _raycast;
    private readonly PlacementGrid _grid;
    private readonly PreviewController _preview;
    private readonly CellIndicatorController _indicator;

    private GameObject _obj;
    private ObjDataSO _data;
    private Vector2Int _root;
    private float _rotation;
    private Vector2Int[] _offsets;

    private bool _selected;

    public bool IsPlacementState => true;
    public string ObjectName => _obj != null ? _obj.name : "None";

    public MoveState(
        PlacementActions actions,
        PreviewController preview,
        PlacementStateMachine fsm,
        RaycastController raycast,
        PlacementGrid grid,
        CellIndicatorController indicator)
    {
        _actions = actions;
        _preview = preview;
        _fsm = fsm;
        _raycast = raycast;
        _grid = grid;
        _indicator = indicator;

        _actions.BuildPlacement.BindPlaceToMouseLeft();
        _actions.BuildPlacement.BindRotateTo_R();
    }

    public void OnEnter()
    {
        _raycast.EnableRay();
        _indicator.UseMoveMode();
        _selected = false;

        _actions.BuildPlacement.Place.performed += OnPlace;
        _actions.BuildPlacement.Rotate.performed += OnRotate;
    }

    public void OnExit()
    {
        _raycast.DisableRay();
        _indicator.ClearAll();

        _actions.BuildPlacement.Place.performed -= OnPlace;
        _actions.BuildPlacement.Rotate.performed -= OnRotate;
    }

    public void Tick()
    {
        if (!_selected)
        {
            TrySelect();
            return;
        }

        // After selection, BuildState takes over.
    }

    private void TrySelect()
    {
        _raycast.Tick();

        if (_raycast.HitObject == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // SAFER BUILDINGDATA LOOKUP
        var bd = _raycast.HitObject.GetComponent<BuildingData>();
        if (bd == null)
            bd = _raycast.HitObject.GetComponentInParent<BuildingData>();
        if (bd == null || bd.Data == null)
            return;

        // Extract metadata
        _obj = bd.transform.root.gameObject;   // ⭐ ALWAYS use root object
        _data = bd.Data;
        _root = bd.RootCell;
        _rotation = bd.Rotation;
        _offsets = bd.Offsets;

        // Remove from grid BEFORE disabling
        foreach (var o in _offsets)
            _grid.RemoveStackObject(_root + o, _obj, _data);

        _obj.SetActive(false);

        // ⭐ ATOMIC HANDOFF — GUARANTEED ORDER
        _fsm.EnterBuildMoveMode(
            _obj,
            _data,
            _root,
            _rotation,
            _offsets
        );

        _selected = true;
    }

    private void OnPlace(InputAction.CallbackContext ctx) { }
    private void OnRotate(InputAction.CallbackContext ctx) { }
}
