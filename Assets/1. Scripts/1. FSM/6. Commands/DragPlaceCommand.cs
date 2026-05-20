using System.Collections.Generic;
using UnityEngine;

public class DragPlaceCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;

    private readonly List<Vector2Int> _cells;
    private readonly Vector2Int[] _offsets;
    private readonly ObjDataSO _data;
    private readonly float _rotation;
    private readonly MoneyService _money;

    private readonly List<GameObject> _instances = new();

    // Floors disabled across all cells in this drag operation
    private readonly List<GameObject> _disabledFloors = new();

    public DragPlaceCommand(
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        List<Vector2Int> cells,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation,
        MoneyService money)
    {
        _grid = grid;
        _finalizer = finalizer;
        _cells = cells;
        _offsets = offsets;
        _data = data;
        _rotation = rotation;
        _money = money;
    }

    public void Execute()
    {
        _instances.Clear();
        _disabledFloors.Clear();

        foreach (var cell in _cells)
        {
            GameObject placed = _finalizer.FinalizePlacement(
                cell,
                _offsets,
                _data,
                _rotation,
                _disabledFloors);

            if (placed != null)
            {
                _instances.Add(placed);

                // Deduct cost per placed object
                _money.Deduct(_data.cost);
                _money.AddHourlyCost(_data.hourlyCost);

                // Force height recalculation for every cell in this object's footprint
                foreach (var o in _offsets)
                    _grid.UpdateStackPositions(cell + o);
            }
        }
    }


    public void Undo()
    {
        foreach (var instance in _instances)
        {
            if (instance == null)
                continue;

            var bd = instance.GetComponent<BuildingData>();
            if (bd == null)
            {
                Object.Destroy(instance); // fallback for non-building objects
                continue;
            }

            Vector2Int root = bd.RootCell;

            foreach (var o in _offsets)
            {
                Vector2Int cell = root + o;
                _grid.RemoveStackObject(cell, instance, bd.Data);
            }

            instance.SetActive(false);
            
            // Refund money for each object
            _money.Refund(_data.cost);
            _money.RemoveHourlyCost(_data.hourlyCost);
        }

        // Re-enable any floors we disabled during execution
        bool revealedFloor = false;
        foreach (var floor in _disabledFloors)
        {
            if (floor != null)
            {
                floor.SetActive(true);
                revealedFloor = true;
            }
        }

        // Update stack heights for all affected cells
        foreach (var cell in _cells)
        {
            foreach (var o in _offsets)
            {
                _grid.UpdateStackPositions(cell + o);
            }
        }

        if (revealedFloor || _data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules)
        {
            NavMeshManager.Instance.MarkDirty();
        }
    }

    public void Redo()
    {
        // 1. Enable objects first
        foreach (var instance in _instances)
        {
            if (instance != null)
                instance.SetActive(true);
        }

        // 2. Add back to grid
        foreach (var instance in _instances)
        {
            if (instance == null) continue;
            var bd = instance.GetComponent<BuildingData>();
            if (bd == null) continue;

            Vector2Int root = bd.RootCell;
            foreach (var o in _offsets)
            {
                _grid.AddStackObject(root + o, instance, bd.Data);
            }
        }

        // 3. Re-disable floors
        foreach (var floor in _disabledFloors)
        {
            if (floor != null)
                floor.SetActive(false);
        }

        // 4. Update stack heights
        foreach (var cell in _cells)
        {
            foreach (var o in _offsets)
            {
                _grid.UpdateStackPositions(cell + o);
            }
        }

        // 5. Deduct money
        foreach (var instance in _instances)
        {
            _money.Deduct(_data.cost);
            _money.AddHourlyCost(_data.hourlyCost);
        }

        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules)
        {
            NavMeshManager.Instance.MarkDirty();
        }
    }
}
