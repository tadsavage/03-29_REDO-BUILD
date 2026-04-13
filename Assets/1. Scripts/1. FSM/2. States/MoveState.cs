using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MoveState : IPlacementState
{
    #region FIELDS ***************************************
    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementGrid _grid;
    private readonly PlacementStateMachine _fsm;
    private readonly RaycastController _raycast;
    private readonly CellIndicatorController _indicator;

    private GameObject _obj;
    private ObjDataSO _data;

    private Vector2Int _originalRoot;
    private Vector2Int[] _offsets;
    private float _rotation;

    private bool _hasSelection;

    public bool IsPlacementState => true;

    public MoveState(
    PlacementActions actions,
    PreviewController preview,
    PlacementValidator validator,
    PlacementFinalizer finalizer,
    PlacementGrid grid,
    PlacementStateMachine fsm,
    RaycastController raycast,
    CellIndicatorController indicator)
    {
        _actions = actions;
        _preview = preview;
        _validator = validator;
        _finalizer = finalizer;
        _grid = grid;
        _fsm = fsm;
        _raycast = raycast;
        _indicator = indicator;

        _actions.BuildPlacement.BindPlaceToMouseLeft();
        _actions.BuildPlacement.BindCancelTo_RMB();
    }
    #endregion ******************************************

    // ---------------------------------------------------------
    // ENTER
    // ---------------------------------------------------------
    public void OnEnter()
    {
        _actions.BuildPlacement.Place.performed += OnConfirmMove;
        _actions.BuildPlacement.Cancel.performed += OnCancelMove;

        _raycast.EnableRay();
        _preview.ResetMoveGhostState();
        _hasSelection = false;
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

        _actions.BuildPlacement.Place.performed -= OnConfirmMove;
        _actions.BuildPlacement.Cancel.performed -= OnCancelMove;
    }
    // ---------------------------------------------------------
    // SELECT OBJECT
    // ---------------------------------------------------------

    // Raycast-select object if valid. Runs every frame until an object is selected, then transitions to move mode with that object.
    private void TrySelectObject()
    {
        _raycast.Tick();

        if (_raycast.HitObject == null)                                 // No object hit - KEEP RAYCASTING UNTIL CLICK
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)              // AND Left mouse button not pressed - KEEP RAYCASTING UNTIL CLICK
            return;

        var bd = _raycast.HitObject.GetComponent<BuildingData>();      // Object hit AND left mouse button was just pressed - BUT NO BUILDING-DATA so keep looking
        if (bd == null || bd.Data.ClearsGridAfterPlacement)
            return;
        //-------------------------------------------------------------------------------------------------------------------
        // Object hit AND left mouse button was just pressed AND building-data exists - CHECK IF TOP OF STACK
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
        // Left click happened on valid object, select it for moving
        //_obj = _raycast.HitObject;
        _obj = bd.gameObject; //just testing this out
        _data = bd.Data;
        _offsets = bd.Offsets;
        _rotation = bd.Rotation;
        _originalRoot = bd.RootCell;
        _preview.ApplyHighlight(_obj);
        _preview.ShowGhost(_obj);
        _obj.SetActive(false);   // <<< REQUIRED
        _hasSelection = true;
    }
    // ---------------------------------------------------------
    // TICK
    // ---------------------------------------------------------
    public void Tick()
    {
        if (!_hasSelection){
            // If no object selected yet, keep trying to select one
            _indicator.ClearAll();
            TrySelectObject();
            return;
        }

        _raycast.Tick();

        if (!_raycast.HasHit)
        {
            _preview.HideGhost();
            return;
        }
        Vector2Int newRoot = _raycast.HitCell;

        bool valid = _validator.IsValidPlacement(newRoot, _offsets, _data, _obj);


        // Show CellIndicators
        List<Vector2Int> footprint = new List<Vector2Int>();
        foreach (var o in _offsets)
            footprint.Add(newRoot + o);

        _indicator.ShowCells(footprint, valid);

        float stackY = 0f;
        if (_data.isStackable)
            stackY = _grid.GetStackHeight(newRoot, _obj);
    
        Vector3 pos = _grid.GetCellCenter(newRoot);
        pos.y += stackY;

        // Keep ghost active even if smoothing is running
        if (_preview != null && _obj != null) 
        {
            //where is the ghost?
            _preview.UpdateGhostPosition(pos);
        }
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

        // Update grid immediately and push command to history for undo/redo and finalization
        _fsm.History.Push(
            new MoveCommand(
                _grid,
                _finalizer,
                _obj,
                _data,
                _originalRoot,
                newRoot,
                _offsets,
                _rotation
            )
        );
        // Reset ghost + keep persistent move mode
        _preview.ResetMoveGhostState();

        _obj.SetActive(true);

        // Clear selection but stay in MoveState
        _obj = null;
        _data = null;
        _offsets = null;
        _rotation = 0f;
        _originalRoot = default;
        _hasSelection = false;
        _indicator.ClearAll();
    }
    // ---------------------------------------------------------
    // CANCEL MOVE
    // ---------------------------------------------------------
    private void OnCancelMove(InputAction.CallbackContext ctx)
    {
        if (_obj != null)
        {
            // Restore original object state
            _preview.RemoveHighlight(_obj);
            // No need to update grid since object was never removed from it, just hidden

            _obj.SetActive(true);

            // Reset ghost + exit move mode
            _preview.Hide();
            //_raycast.DisableRay();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        AudioManager.Play("Cancel");
        _indicator.ClearAll();
        _fsm.SetState(_fsm.IdleState);
    }
}
