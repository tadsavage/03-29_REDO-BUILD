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
        foreach (var cell in _cells)
        {
            GameObject placed = _finalizer.FinalizePlacement(cell, _offsets, _data, _rotation);
            _instances.Add(placed);
        }
    }

    public void Undo()
    {
        foreach (var instance in _instances)
        {
            var bd = instance.GetComponent<BuildingData>();
            Vector2Int root = bd.RootCell;

            foreach (var o in _offsets)
            {
                Vector2Int cell = root + o;
                var list = _grid.GetObjectsInCell(cell);
                if (list == null) continue;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].instance == instance)
                    {
                        list.RemoveAt(i);
                        _grid.AdjustStackHeight(cell, -_data.objHeight);
                    }
                }

                if (list.Count == 0)
                    _grid.RemoveCellVisual(cell);
            }

            bd.Delete();
        }

        _instances.Clear();
    }
}
