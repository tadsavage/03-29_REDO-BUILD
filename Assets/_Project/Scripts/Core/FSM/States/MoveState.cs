using GameCore.Economy;
using GameCore.Build;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles selecting an existing placed object, picking it up,
/// moving it around the grid, rotating it, validating placement,
/// and confirming the new position.
///
/// Inherits from PlacementStateBase for standardized service access and event handling.
///
/// Key behavior:
/// - Clicking ANY footprint cell selects the object.
/// - If the user clicked an offset cell, movement preserves that offset.
/// - Rotation only applies to the currently selected object.
/// - No rotation leaks between objects.
/// </summary>
public class MoveState : PlacementStateBase
{
    // ---------------------------------------------------------
    // DEPENDENCIES
    // ---------------------------------------------------------
    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private new readonly PlacementGrid _grid;
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
    private Vector2Int[] _originalOffsets;
    private float _originalRotation;

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

    private float _scrollCooldown = 0f;
    private const float ScrollThreshold = 0.01f;

    private BuildingHighlighter _hoveredHighlighter;

    // Floor tiles that travel with the foundation
    private struct RiderTile
    {
        public GameObject go;
        public ObjDataSO data;
        public Vector2Int cell;
        public Vector2Int localOffset; // cell - root
    }
    private readonly List<RiderTile> _riders = new();

    public override bool IsPlacementState => true;
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
    public override void OnEnter()
    {
        base.OnEnter(); // Initialize event manager and services

        BuildModeOverride.Instance?.Activate();

        _actions.BuildPlacement.Place.performed += OnConfirmMove;
        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;

        _raycast.EnableRay();
        _preview.ResetMoveGhostState();
        _preview.SetMovePreviewMode(true);
        _indicator.UseMoveMode();

        _hasSelection = false;
        _lastHoverCell = new Vector2Int(int.MinValue, int.MinValue);

        topBarUI?.SetState(GetType().Name);
    }

    public override void OnExit()
    {
        BuildModeOverride.Instance?.Deactivate();

        _raycast.DisableRay();
        _preview.ResetMoveGhostState();
        _preview.SetMovePreviewMode(false);
        _indicator.ClearAll();
        ClearHoverHighlight();

        if (_obj != null)
{
            _preview.ClearFlatHighlight(_obj);

            // If we still have a selection, it means the move wasn't confirmed.
            // We should put it back.
            if (_hasSelection)
            {
                _obj.SetActive(true);
                foreach (var o in _offsets)
                {
                    _grid.AddStackObject(_originalRoot + o, _obj, _data);
                }

                // Put rider tiles back where they came from
                foreach (var rider in _riders)
                {
                    if (rider.go == null) continue;
                    rider.go.SetActive(true);
                    _grid.AddStackObject(rider.cell, rider.go, rider.data);
                }
            }
        }

        // Clear selection state
        _obj = null;
        _data = null;
        _offsets = null;
        _rotation = 0f;
        _originalRoot = default;
        _hasSelection = false;
        _riders.Clear();

        _actions.BuildPlacement.Rotate.performed -= OnRotatePerformed;
        _actions.BuildPlacement.Place.performed -= OnConfirmMove;

        base.OnExit(); // Clean up base state
    }

    // ---------------------------------------------------------
    // SELECT OBJECT - Stack Aware
    // ---------------------------------------------------------
    private void TrySelectObject()
    {
        _raycast.Tick();

        // Must hit an object and not be over UI
        if (_raycast.HitObject == null || _raycast.IsPointerOverUI)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // Try to get BuildingData directly from the hit object (mesh)
        var bd = _raycast.HitObject.GetComponentInParent<BuildingData>();

        // Fallback to the top object in the hit cell (grid data)
        if (bd == null)
        {
            GameObject trueTop = _grid.GetTopObject(_raycast.HitCell);
            if (trueTop != null) bd = trueTop.GetComponent<BuildingData>();
        }

        // Validate we found a movable object; floor tiles can only be replaced, not moved
        if (bd == null || bd.Data == null || bd.Data.ClearsGridAfterPlacement || bd.Data.isFloor)
            return;

        ClearHoverHighlight();

        // Capture object data
        _obj = bd.gameObject;
        _data = bd.Data;
        _offsets = bd.Offsets;
        _rotation = bd.Rotation;
        _originalRoot = bd.RootCell;

        _originalOffsets = _offsets;
        _originalRotation = _rotation;

        // -----------------------------------------------------
        // OFFSET-AWARE SELECTION
        // -----------------------------------------------------
        _clickedCell = _raycast.HitCell;
        _originAtSelect = _originalRoot;
        _selectionDelta = _clickedCell - _originAtSelect;

        Vector3 lastWorldPos = _obj.transform.position;
        float foundationY = _obj.transform.position.y;

        // REMOVE from grid FIRST so SnapTo calculates the correct baseline height
        // (now that the slot it was occupying is 'empty')
        foreach (var o in _offsets)
        {
            Vector2Int cell = _originalRoot + o;
            _grid.RemoveStackObject(cell, _obj, _data);
        }

        // Pick up any floor tiles ABOVE the foundation (Y > foundation.Y).
        // Yard tiles below stay in place.
        GatherRiderTiles(foundationY);

        // Reveal underlying yard tiles (or other hidden floor tiles) under the picked up foundation
        if (_data != null && (_data.category == "Foundation" || _data.category == "Grounds"))
        {
            foreach (var o in _offsets)
            {
                Vector2Int cell = _originalRoot + o;
                var cellObjs = _grid.GetObjectsInCell(cell);
                if (cellObjs != null)
                {
                    foreach (var entry in cellObjs)
                    {
                        if (entry.data != null && entry.data.isFloor)
                        {
                            if (entry.instance != null && !entry.instance.activeSelf)
                            {
                                // Check if this is one of our rider tiles (we shouldn't enable it because it's disabled for movement)
                                bool isRider = false;
                                foreach (var r in _riders)
                                {
                                    if (r.go == entry.instance)
                                    {
                                        isRider = true;
                                        break;
                                    }
                                }
                                if (!isRider)
                                {
                                    entry.instance.SetActive(true);
                                }
                            }
                        }
                    }
                }
                _grid.UpdateStackPositions(cell);
            }
        }

        // Highlight + show ghost
        _preview.ApplyFlatHighlight(_obj, MoveHighlightBlue);
        _preview.Show(_data);
        _preview.Rotate(_rotation); // IMPORTANT: match selected object's rotation

        // Snap ghost to the object's last position.
        // It will then smooth-lift to the offset in the next frame.
        _preview.SnapTo(lastWorldPos, _originalRoot, _data);
        _lastHoverCell = _originalRoot;

        _obj.SetActive(false);
        _hasSelection = true;
    }

    // ---------------------------------------------------------
    // UPDATE — MOVEMENT + VALIDATION
    // ---------------------------------------------------------
    public override void Update()
    {
        _raycast.Tick();

        if (_raycast.IsPointerOverUI)
        {
            ClearHoverHighlight();
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
            UpdateHoverHighlight();
            TrySelectObject();
            return;
        }

        if (!_raycast.HasHit)
        {
            _preview.HideGhost();
            return;
        }

        // -----------------------------------------------------
        // SCROLL WHEEL ROTATION
        // -----------------------------------------------------
        if (_scrollCooldown > 0)
        {
            _scrollCooldown -= Time.deltaTime;
        }

        float scrollDelta = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollDelta) > ScrollThreshold && _scrollCooldown <= 0)
        {
            RotateObject();
            _scrollCooldown = 0.2f;
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

        // Build rider list for the command with proper offsets
        var riderList = new List<MoveCommand.RiderObject>();
        foreach (var rider in _riders)
        {
            if (rider.go != null)
            {
                // Find the new local offset by looking up the old offset in the old footprint
                Vector2Int newLocal = rider.localOffset;
                int idx = System.Array.IndexOf(_originalOffsets, rider.localOffset);
                if (idx >= 0 && idx < _offsets.Length)
                    newLocal = _offsets[idx];

                riderList.Add(new MoveCommand.RiderObject
                {
                    instance = rider.go,
                    data = rider.data,
                    oldLocal = rider.localOffset,
                    newLocal = newLocal
                });
            }
        }

        // Push undo/redo command with riders
        _fsm.History.Push(
            new MoveCommand(
                _grid,
                _obj,
                _data,
                _originalRoot,
                newRoot,
                _originalOffsets,
                _offsets,
                _originalRotation,
                _rotation,
                _money,
                riderList
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
        _riders.Clear();

        _indicator.ClearAll();
    }

    // ---------------------------------------------------------
    // ROTATE SELECTED OBJECT
    // ---------------------------------------------------------
    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        RotateObject();
    }

    private void RotateObject()
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

    private void UpdateHoverHighlight()
    {
        if (_hasSelection) return;

        BuildingHighlighter newHighlighter = null;
        bool isValid = true;

        if (_raycast.HitObject != null && !_raycast.IsPointerOverUI)
        {
            // Try to get BuildingData directly from the hit mesh
            var bd = _raycast.HitObject.GetComponentInParent<BuildingData>();

            // Fallback to top object in cell
            if (bd == null)
            {
                GameObject topObj = _grid.GetTopObject(_raycast.HitCell);
                if (topObj != null) bd = topObj.GetComponent<BuildingData>();
            }

            if (bd != null && bd.Data != null && !bd.Data.ClearsGridAfterPlacement && !bd.Data.isFloor)
            {
                newHighlighter = bd.GetComponent<BuildingHighlighter>();
                
                // Check if the object is currently in a valid position
                isValid = _validator.IsValidPlacement(bd.RootCell, bd.Offsets, bd.Data, bd.gameObject);
            }
        }

        if (newHighlighter != _hoveredHighlighter)
        {
            ClearHoverHighlight();
            _hoveredHighlighter = newHighlighter;
            if (_hoveredHighlighter != null)
            {
                if (isValid)
                    _hoveredHighlighter.HighlightValid(true);
                else
                    _hoveredHighlighter.HighlightInvalid(true);
            }
        }
    }

    private void GatherRiderTiles(float foundationY)
    {
        _riders.Clear();

        foreach (var o in _originalOffsets)
        {
            Vector2Int cell = _originalRoot + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null) continue;

            foreach (var entry in list.ToArray())
            {
                if (entry.data == null || !entry.data.isFloor) continue;
                if (entry.instance == null || !entry.instance.activeSelf) continue;

                // Never pick up the yard tile (ID 200)
                if (entry.data.id == 200) continue;

                // Only pick up floors that are ABOVE the foundation
                if (entry.instance.transform.position.y > foundationY)
                {
                    _riders.Add(new RiderTile
                    {
                        go = entry.instance,
                        data = entry.data,
                        cell = cell,
                        localOffset = cell - _originalRoot
                    });
                    _grid.RemoveStackObject(cell, entry.instance, entry.data);
                    entry.instance.SetActive(false);
                }
            }
        }
    }

    private void ClearHoverHighlight()
    {
        if (_hoveredHighlighter != null)
        {
            _hoveredHighlighter.HighlightValid(false);
            _hoveredHighlighter.HighlightInvalid(false);
            _hoveredHighlighter = null;
        }
    }
}
