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

    private readonly List<GameObject> _instances = new();

    // Floors disabled across all cells in this drag operation
    private readonly List<GameObject> _disabledFloors = new();

    public DragPlaceCommand(
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        List<Vector2Int> cells,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation)
    {
        _grid = grid;
        _finalizer = finalizer;
        _cells = cells;
        _offsets = offsets;
        _data = data;
        _rotation = rotation;
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
                _instances.Add(placed);
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
                Object.Destroy(instance);
                continue;
            }

            Vector2Int root = bd.RootCell;

            foreach (var o in _offsets)
            {
                Vector2Int cell = root + o;
                _grid.RemoveStackObject(cell, instance, bd.Data);
            }

            bd.Delete();
        }

        _instances.Clear();

        // Re-enable any floors we disabled
        foreach (var floor in _disabledFloors)
        {
            if (floor != null)
                floor.SetActive(true);
        }

        _disabledFloors.Clear();
    }

    public void Redo()
    {
        // Recreate all instances using the same cells
        Execute();
    }
}
