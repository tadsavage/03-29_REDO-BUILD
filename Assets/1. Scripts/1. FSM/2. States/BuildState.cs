using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// BuildState handles placing new objects on the grid:
/// - Hovering the grid
/// - Drag placement (multi-place)
/// - Rotation
/// - Cost preview
/// - Validity preview
/// - Final placement
///
/// IMPORTANT:
/// This state NO LONGER interacts with the hover popup UI.
/// Only IdleState controls hover popups.
/// </summary>
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
    private readonly BuildMenuUI _buildMenuUI;
    private TopBarUI _topBarUI;

    private TopBarUI topBarUI => _topBarUI != null ? _topBarUI : _topBarUI = Object.FindAnyObjectByType<TopBarUI>();

    private ObjDataSO _currentData;

    private bool _placeRequested;
    private bool _rotateRequested;
    private float _currentRotation;

    private bool _isDragging;
    private Vector2Int _dragStartCell;
    private readonly List<Vector2Int> _dragCells = new();

    private readonly List<Vector2Int> _indicatorBuffer = new();
    private readonly List<Vector2Int> _footprintBuffer = new();

    private float _scrollCooldown = 0f;
    private const float ScrollThreshold = 0.1f;

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
        _buildMenuUI = buildMenuUI;

        _actions.BuildPlacement.BindRotateTo_R();
        _actions.BuildPlacement.BindPlaceToMouseLeft();
    }

    // ---------------------------------------------------------
    // ENTER
    // ---------------------------------------------------------
    public void OnEnter()
    {
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
        _raycast.DisableRay();
        _indicator.ClearAll();
        _preview.Hide();
        _costUI.Hide();

        _actions.BuildPlacement.Place.canceled -= OnPlacePerformed;
        _actions.BuildPlacement.Rotate.performed -= OnRotatePerformed;
    }

    // ---------------------------------------------------------
    // TICK
    // ---------------------------------------------------------
    public void Tick()
    {
        _raycast.Tick();

        if (_raycast.IsPointerOverUI)
        {
            _indicator.ClearAll();
            _preview.Hide();
            _costUI.Hide();
            return;
        }

        // Ensure preview is shown if we just left the UI
        if (_currentData != null)
        {
            _preview.Show(_currentData);
        }

        if (!_raycast.HasHit)
        {
_indicator.ClearAll();
            _preview.Hide();
            _costUI.Hide();
            return;
        }

        Vector2Int root = _raycast.HitCell;
        topBarUI?.SetCell(root.x, root.y);

        // -----------------------------------------------------
        // DRAG / CLICK DETECTION
        // -----------------------------------------------------

        // 1. Mouse pressed → record starting cell
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragCells.Clear();
            _dragStartCell = root;
        }

        // 2. If mouse held AND cell changed → start drag
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

        // 3. If dragging, handle drag placement
        if (_isDragging)
        {
            HandleDragPlacement(root);
            return;
        }

        // ---------------------------------------------------------
        // ROTATION
        // ---------------------------------------------------------
        if (_scrollCooldown > 0)
        {
            _scrollCooldown -= Time.deltaTime;
        }

        float scrollDelta = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollDelta) > ScrollThreshold && _scrollCooldown <= 0)
        {
            RotateObject();
            _scrollCooldown = 0.2f; // cooldown in seconds
        }

        if (_rotateRequested)
        {
            _rotateRequested = false;

            _currentRotation += 90f;
            if (_currentRotation >= 360f)
                _currentRotation = 0f;

            _preview.Rotate(_currentRotation);
        }

        // ---------------------------------------------------------
        // GHOST + VALIDATION
        // ---------------------------------------------------------
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

        // ---------------------------------------------------------
        // COST PREVIEW
        // ---------------------------------------------------------
        int cost = _currentData.cost;
        bool canAfford = _money.CanAfford(cost);

        _costUI.ShowCost(cost, canAfford);
        _costUI.SetScreenPosition(_raycast.RawHitPoint, Camera.main);

        // ---------------------------------------------------------
        // PLACE
        // ---------------------------------------------------------
        if (_placeRequested)
        {
            _placeRequested = false;

            // Prevent placing on "ClearsGridAfterPlacement" objects
            GameObject hitObj = _raycast.HitObject;
            if (hitObj != null)
            {
                var bd = hitObj.GetComponent<BuildingData>();
                if (bd != null && bd.Data.ClearsGridAfterPlacement)
                {
                    AudioManager.Play("InvalidPlace");
                    return;
                }
            }

            bool isValidNow = _validator.IsValidPlacement(root, offsets, _currentData);

            if (!isValidNow || !_money.CanAfford(cost))
            {
                AudioManager.Play("InvalidPlace");
                _preview.SetGhostInvalid();
                _indicator.ShowCells(BuildFootprintBuffered(root, offsets), cell => false);
                return;
            }

            AudioManager.Play("ValidPlace");

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

    // ---------------------------------------------------------
    // ROTATE INPUT
    // ---------------------------------------------------------
    public void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        RotateObject();
    }

    private void RotateObject()
    {
        AudioManager.Play("Rotate");
        _rotateRequested = true;
    }

    // ---------------------------------------------------------
    // PLACE INPUT
    // ---------------------------------------------------------
    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        if (_isDragging || _raycast.IsPointerOverUI)
            return;

        if (_currentData == null)
            return;

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
        _dragCells.Clear();

        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);
        Vector2Int stride = GetStride(offsets);

        int stepX = (_dragStartCell.x <= currentCell.x) ? stride.x : -stride.x;
        int stepY = (_dragStartCell.y <= currentCell.y) ? stride.y : -stride.y;

        int startX = _dragStartCell.x;
        int endX = currentCell.x;

        int startY = _dragStartCell.y;
        int endY = currentCell.y;

        _indicatorBuffer.Clear();

        _preview.EndSelectionCells();
        _preview.BeginSelectionCells();

        for (int x = startX; stepX > 0 ? x <= endX : x >= endX; x += stepX)
        {
            for (int y = startY; stepY > 0 ? y <= endY : y >= endY; y += stepY)
            {
                Vector2Int cell = new Vector2Int(x, y);

                bool valid = _validator.IsCellValid(cell, _currentData);

                GameObject objAtCell = _raycast.RaycastCellCenter(cell);
                if (objAtCell != null)
                {
                    var bd = objAtCell.GetComponent<BuildingData>();
                    if (bd != null && bd.Data.ClearsGridAfterPlacement)
                        valid = false;
                }

                if (!_currentData.ignorePlacementRules)
                {
                    if (_currentData.isStackable)
                    {
                        if (!_grid.CanStack(cell, _currentData))
                            valid = false;
                    }
                    else
                    {
                        if (_grid.IsOccupied(cell))
                            valid = false;
                    }
                }

                _indicatorBuffer.Add(cell);
                foreach (var o in offsets)
                    _indicatorBuffer.Add(cell + o);

                if (valid)
                {
                    _dragCells.Add(cell);
                    _preview.ShowMultiGhost(cell, true, _currentRotation);
                }
                else
                {
                    _preview.ShowMultiGhost(cell, false, _currentRotation);
                }
            }
        }

        _indicator.ShowCells(_indicatorBuffer, cell => IsFootprintValid(cell));

        int totalCost = _dragCells.Count * _currentData.cost;
        bool canAfford = _money.CanAfford(totalCost);
        _costUI.ShowCost(totalCost, canAfford);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndDragPlacement();
            return;
        }
    }

    private void EndDragPlacement()
    {
        if (_dragCells.Count == 0)
        {
            AudioManager.Play("InvalidPlace");
            _preview.EndSelectionCells();
            _indicator.ClearAll();
            _isDragging = false;
            _costUI.Hide();
            return;
        }

        int totalCost = _dragCells.Count * _currentData.cost;
        if (!_money.CanAfford(totalCost))
        {
            AudioManager.Play("InvalidPlace");

            foreach (var cell in _dragCells)
                _preview.ShowMultiGhost(cell, false, _currentRotation);

            _preview.EndSelectionCells();
            _indicator.ClearAll();
            _isDragging = false;
            _dragCells.Clear();
            _costUI.Hide();
            return;
        }

        AudioManager.Play("ValidPlace");

        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

        _fsm.History.Push(
            new DragPlaceCommand(
                _grid,
                _finalizer,
                new List<Vector2Int>(_dragCells),
                offsets,
                _currentData,
                _currentRotation,
                _money)
        );

        _preview.EndSelectionCells();
        _indicator.ClearAll();
        _isDragging = false;
        _placeRequested = false;
        _dragCells.Clear();
        _costUI.Hide();
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

    private Vector2Int GetStride(Vector2Int[] offsets)
    {
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var o in offsets)
        {
            if (o.x < minX) minX = o.x;
            if (o.x > maxX) maxX = o.x;
            if (o.y < minY) minY = o.y;
            if (o.y > maxY) maxY = o.y;
        }

        int width = (maxX - minX) + 1;
        int height = (maxY - minY) + 1;

        return new Vector2Int(width, height);
    }

    private bool IsFootprintValid(Vector2Int root)
    {
        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

        foreach (var o in offsets)
        {
            Vector2Int cell = root + o;
            if (!_validator.IsCellValid(cell, _currentData))
                return false;
        }
        return true;
    }

    public void SetBuildData(ObjDataSO data)
    {
        _currentData = data;
    }
}
