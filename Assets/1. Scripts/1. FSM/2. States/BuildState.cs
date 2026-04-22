using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildState : IPlacementState
{
    private readonly RaycastController _raycast;
    private readonly CellIndicatorController _indicator;

    private readonly PlacementActions _actions;
    private readonly PreviewController _preview;
    private readonly PlacementValidator _validator;
    private readonly PlacementFinalizer _finalizer;
    private readonly PlacementGrid _grid;
    private readonly PlacementStateMachine _fsm;
    private readonly MoneyService _money;

    private readonly PreviewCostUI _costUI;   // NEW UI CONTROLLER

    private ObjDataSO _currentData;

    private bool _placeRequested;
    private bool _rotateRequested;
    private float _currentRotation;

    private float _lastRotation;

    private bool _isDragging;
    private Vector2Int _dragStartCell;
    private readonly List<Vector2Int> _dragCells = new();

    private readonly List<Vector2Int> _indicatorBuffer = new();
    private readonly List<Vector2Int> _footprintBuffer = new();

    private Vector2Int _lastPlacedCell;
    private bool _justPlaced;

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
        PreviewCostUI costUI)   // NEW
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

        _actions.BuildPlacement.BindRotateTo_R();
        _actions.BuildPlacement.BindPlaceToMouseLeft();
    }

    public void OnEnter()
    {
        _actions.BuildPlacement.Rotate.performed += OnRotatePerformed;
        _actions.BuildPlacement.Place.canceled += OnPlacePerformed;

        if (_currentData == null)
            return;

        _indicator.UseBuildMode();
        _raycast.EnableRay();

        _preview.Show(_currentData);

        Vector3 firstTarget = _grid.GetCellCenter(_raycast.HitCell);
        _preview.BeginFlyIn(firstTarget);

        _placeRequested = false;
        _rotateRequested = false;

        _currentRotation = _preview.CurrentRotation;

        _isDragging = false;
        _dragCells.Clear();

        _lastRotation = _currentRotation;

        _costUI.Hide();   // NEW
    }

    public void OnExit()
    {
        _raycast.DisableRay();
        _indicator.ClearAll();
        _preview.Hide();
        _costUI.Hide();   // NEW

        _actions.BuildPlacement.Place.canceled -= OnPlacePerformed;
        _actions.BuildPlacement.Rotate.performed -= OnRotatePerformed;
    }

    public void Tick()
    {
        _raycast.Tick();

        if (!_raycast.HasHit)
        {
            _indicator.ClearAll();
            _preview.Hide();
            _costUI.Hide();   // NEW
            return;
        }

        Vector2Int root = _raycast.HitCell;

        if (_justPlaced && root == _lastPlacedCell)
        {
            _preview.Hide();
            _indicator.ClearAll();
            _placeRequested = false;
            _rotateRequested = false;
            _costUI.Hide();   // NEW
            return;
        }

        if (_justPlaced && root != _lastPlacedCell)
        {
            _justPlaced = false;
            _preview.Show(_currentData);
        }

        // --- CLICK / DRAG START ---
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isDragging = false;
            _dragCells.Clear();
            _dragStartCell = root;
        }

        if (Mouse.current.leftButton.isPressed && !_isDragging)
        {
            if (!Mouse.current.leftButton.wasPressedThisFrame && root != _dragStartCell)
            {
                _isDragging = true;

                _preview.Hide();
                _indicator.ClearAll();
                _costUI.Hide();   // NEW

                _justPlaced = false;
                _lastPlacedCell = new Vector2Int(int.MinValue, int.MinValue);

                _preview.BeginSelectionCells();
                return;
            }
        }

        if (_isDragging)
        {
            HandleDragPlacement(root);
            return;
        }

        // --- ROTATION ---
        if (_rotateRequested)
        {
            _rotateRequested = false;

            _currentRotation += 90f;
            if (_currentRotation >= 360f)
                _currentRotation = 0f;

            _preview.Rotate(_currentRotation);
        }

        if (_currentRotation != _lastRotation)
        {
            _lastRotation = _currentRotation;
        }

        // --- GHOST + VALIDATION ---
        Vector2Int[] offsets = _currentData.GetFootprintOffsets(-_currentRotation);

        _preview.MoveTo(_grid.GetCellCenter(root), root, _currentData);

        bool isValid = _validator.IsValidPlacement(root, offsets, _currentData);

        GameObject obj = _raycast.HitObject;
        if (obj != null)
        {
            var bd = obj.GetComponent<BuildingData>();
            if (bd != null && bd.Data != null && bd.Data.ClearsGridAfterPlacement)
                isValid = false;
        }

        _indicator.ShowCells(
            BuildFootprintBuffered(root, offsets),
            cell => _validator.IsCellValid(cell, _currentData)
        );

        if (isValid)
            _preview.SetGhostValid();
        else
            _preview.SetGhostInvalid();

        // --- COST PREVIEW (SINGLE) ---
        int cost = _currentData.cost;
        bool canAfford = _money.CanAfford(cost);
        _costUI.ShowCost(cost, canAfford);   // NEW

        // --- UI BLOCKING ---
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            _placeRequested = false;
            _rotateRequested = false;
            return;
        }

        // --- PLACE ---
        if (_placeRequested)
        {
            _placeRequested = false;

            GameObject obj2 = _raycast.HitObject;
            if (obj2 != null)
            {
                var bd = obj2.GetComponent<BuildingData>();
                if (bd != null && bd.Data != null && bd.Data.ClearsGridAfterPlacement)
                {
                    AudioManager.Play("InvalidPlace");
                    return;
                }
            }

            bool isValidNow = _validator.IsValidPlacement(root, offsets, _currentData);

            if (!isValidNow)
            {
                AudioManager.Play("InvalidPlace");
                _preview.SetGhostInvalid();
                _indicator.ShowCells(
                    BuildFootprintBuffered(root, offsets),
                    cell => false
                );
                return;
            }

            // --- MONEY CHECK (SINGLE) ---
            if (!_money.CanAfford(cost))
            {
                AudioManager.Play("InvalidPlace");
                Debug.Log($"[BuildState] Cannot afford single placement. Need {cost}, have {_money.Current}");
                _preview.SetGhostInvalid();
                _indicator.ShowCells(
                    BuildFootprintBuffered(root, offsets),
                    cell => false
                );
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

            Vector3 nextPos = _grid.GetCellCenter(root);
            _preview.BeginFlyIn(nextPos);

            _lastPlacedCell = root;
            _justPlaced = true;
        }
    }

    private void OnRotatePerformed(InputAction.CallbackContext ctx)
    {
        AudioManager.Play("Rotate");
        _rotateRequested = true;
    }

    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        if (_isDragging)
            return;

        _placeRequested = true;
    }

    private List<Vector2Int> BuildFootprintBuffered(Vector2Int root, Vector2Int[] offsets)
    {
        _footprintBuffer.Clear();

        foreach (var o in offsets)
            _footprintBuffer.Add(root + o);

        return _footprintBuffer;
    }

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

        for (int x = startX;
             stepX > 0 ? x <= endX : x >= endX;
             x += stepX)
        {
            for (int y = startY;
                 stepY > 0 ? y <= endY : y >= endY;
                 y += stepY)
            {
                Vector2Int cell = new Vector2Int(x, y);

                bool valid = _validator.IsCellValid(cell, _currentData);

                GameObject objAtCell = _raycast.RaycastCellCenter(cell);

                if (objAtCell != null)
                {
                    var bd = objAtCell.GetComponent<BuildingData>();
                    if (bd != null && bd.Data != null && bd.Data.ClearsGridAfterPlacement)
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

        // --- COST PREVIEW (DRAG) ---
        int totalCost = _dragCells.Count * _currentData.cost;
        bool canAfford = _money.CanAfford(totalCost);
        _costUI.ShowCost(totalCost, canAfford);   // NEW

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
            _costUI.Hide();   // NEW
            return;
        }

        // --- MONEY CHECK (DRAG) ---
        int totalCost = _dragCells.Count * _currentData.cost;
        if (!_money.CanAfford(totalCost))
        {
            AudioManager.Play("InvalidPlace");
            Debug.Log($"[BuildState] Cannot afford drag placement. Need {totalCost}, have {_money.Current}");

            foreach (var cell in _dragCells)
                _preview.ShowMultiGhost(cell, false, _currentRotation);

            _preview.EndSelectionCells();
            _indicator.ClearAll();
            _isDragging = false;
            _dragCells.Clear();
            _costUI.Hide();   // NEW
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
        _costUI.Hide();   // NEW
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

    public void SetBuildData(ObjDataSO data)
    {
        _currentData = data;
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
}
