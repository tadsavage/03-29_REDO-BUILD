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

    private GameObject _objectBeingMoved;
    private Vector2Int _originalRoot;
    private Vector2Int[] _offsets;
    private float _rotation;

    public bool IsPlacementState => true;

    public MoveState(
        PlacementActions actions,
        PreviewController preview,
        PlacementValidator validator,
        PlacementFinalizer finalizer,
        PlacementGrid grid,
        PlacementStateMachine fsm,
        RaycastController raycast)
    {
        _actions = actions;
        _preview = preview;
        _validator = validator;
        _finalizer = finalizer;
        _grid = grid;
        _fsm = fsm;
        _raycast = raycast;

        _actions.BuildPlacement.BindPlaceToMouseLeft();
        _actions.BuildPlacement.Place.performed += OnConfirmMove;
        _actions.BuildPlacement.Cancel.AddBinding("<Mouse>/rightButton");
        _actions.BuildPlacement.Cancel.performed += OnCancelMove;
    }

    // ---------------------------------------------------------
    // SELECT OBJECT TO MOVE (NO GRID EDITS)
    // ---------------------------------------------------------
    public void SetObjectToMove(GameObject obj)
    {
        _objectBeingMoved = obj;

        var bd = obj.GetComponent<BuildingData>();
        _originalRoot = bd.RootCell;
        _offsets = bd.Offsets;
        _rotation = bd.Rotation;

        // Apply highlight BEFORE hiding
        _preview.ApplyHighlight(obj);

        obj.SetActive(false);
        _preview.ShowGhost(obj);
    }

    public void OnEnter()
    {
        _raycast.EnableRay();
    }

    public void OnExit()
    {
        _preview.HideGhost();
        _raycast.DisableRay();

        if (_objectBeingMoved != null)
        {
            _preview.RemoveHighlight(_objectBeingMoved);
            _objectBeingMoved.SetActive(true);
        }
    }

    public void Tick()
    {
        // 1. Select object
        if (_objectBeingMoved == null)
        {
            _raycast.Tick();

            if (_raycast.HitObject != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                var bd = _raycast.HitObject.GetComponent<BuildingData>();
                if (bd != null && !bd.Data.ClearsGridAfterPlacement)
                {
                    // Check if anything is stacked on top
                    foreach (var o in bd.Offsets)
                    {
                        Vector2Int cell = bd.RootCell + o;
                        var list = _grid.GetObjectsInCell(cell);

                        if (list != null && list.Count > 0)
                        {
                            // If the top object is NOT this object, something is stacked on it
                            if (list[list.Count - 1].instance != _raycast.HitObject)
                            {
                                AudioManager.Play("InvalidPlace");
                                return; // cannot move
                            }
                        }
                    }
                    SetObjectToMove(_raycast.HitObject);
                }
            }

            return;
        }

        // 2. Move ghost
        _raycast.Tick();
        if (!_raycast.HasHit)
        {
            _preview.HideGhost();
            return;
        }

        Vector2Int newRoot = _raycast.HitCell;

        bool valid = _validator.IsValidPlacement(newRoot, _offsets, _objectBeingMoved.GetComponent<BuildingData>().Data);

        if (valid)
            _preview.SetGhostValid();
        else
            _preview.SetGhostInvalid();

        float stackY = 0f;
        var data = _objectBeingMoved.GetComponent<BuildingData>().Data;

        if (data.isStackable)
            stackY = _grid.GetStackHeight(newRoot);

        Vector3 pos = _grid.GetCellCenter(newRoot);
        pos.y += stackY;

        _preview.UpdateGhostPosition(pos);
    }

    // ---------------------------------------------------------
    // CONFIRM MOVE
    // ---------------------------------------------------------
    private void OnConfirmMove(InputAction.CallbackContext ctx)
    {
        if (_objectBeingMoved == null)
            return;

        Vector2Int newRoot = _raycast.HitCell;

        var bd = _objectBeingMoved.GetComponent<BuildingData>();
        var data = bd.Data;

        if (!_validator.IsValidPlacement(newRoot, _offsets, data))
        {
            AudioManager.Play("InvalidPlace");
            return;
        }

        AudioManager.Play("ValidPlace");

        // Remove highlight from the object we're about to move
        _preview.RemoveHighlight(_objectBeingMoved);

        _fsm.History.Push(
            new MoveCommand(
                _grid,
                _finalizer,
               
                _objectBeingMoved,
                data,
                _originalRoot,
                newRoot,
                _offsets,
                _rotation
            )
        );

        _objectBeingMoved = null;
        _fsm.SetState(_fsm.IdleState);
    }
    // ---------------------------------------------------------
    // CANCEL MOVE
    // ---------------------------------------------------------
    private void OnCancelMove(InputAction.CallbackContext ctx)
    {
        if (_objectBeingMoved != null)
        {
            _preview.RemoveHighlight(_objectBeingMoved);
            _objectBeingMoved.SetActive(true);
            _objectBeingMoved = null;
        }

        AudioManager.Play("Cancel");
        _fsm.SetState(_fsm.IdleState);
    }
}
