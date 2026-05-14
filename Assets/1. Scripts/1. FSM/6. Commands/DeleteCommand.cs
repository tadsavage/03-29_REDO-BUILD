using System.Collections.Generic;
using UnityEngine;

public class DeleteCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly ObjDataSO _data;
    private readonly Vector2Int[] _offsets;
    private readonly Vector2Int _root;
    private readonly MoneyService _money;

    private readonly GameObject _target;
    private readonly List<GameObject> _reEnabledFloors = new();

    public DeleteCommand(GameObject target, PlacementGrid grid, MoneyService money)
    {
        _target = target;
        _grid = grid;
        _money = money;

        var bd = target.GetComponent<BuildingData>();
        _data = bd.Data;
        _root = bd.RootCell;
        _offsets = bd.Offsets;
    }

    public void Execute()
    {
        if (_target == null)
            return;

        _reEnabledFloors.Clear();

        // 1. Remove from grid and check for floors to re-enable
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.RemoveStackObject(cell, _target, _data);

            // If the cell is no longer occupied (by buildings), re-enable any floors there
            if (!_grid.IsOccupied(cell))
            {
                var cellObjs = _grid.GetObjectsInCell(cell);
                if (cellObjs != null)
                {
                    foreach (var entry in cellObjs)
                    {
                        if (entry.data != null && entry.data.isFloor && entry.instance != null && !entry.instance.activeSelf)
                        {
                            entry.instance.SetActive(true);
                            if (!_reEnabledFloors.Contains(entry.instance))
                                _reEnabledFloors.Add(entry.instance);
                        }
                    }
                }
            }
        }

        // 2. Remove from registry is now handled automatically by _target.SetActive(false) -> PlacedObject.OnDisable()

        // 3. Refund money
        _money.Refund(_data.cost, _data.category);
        _money.RemoveHourlyCost(_data.hourlyCost);

        // 4. Disable object instead of destroying it to allow Undo
        var highlighter = _target.GetComponent<BuildingHighlighter>();
        if (highlighter != null)
            highlighter.HighlightDelete(false);

        _target.SetActive(false);

        NavMeshManager.Instance.MarkDirty();

    }
    public void Undo()
    {
            if (_target == null)
                return;

            // 1. Add back to grid
            foreach (var o in _offsets)
            {
                Vector2Int cell = _root + o;
                _grid.AddStackObject(cell, _target, _data);
            }

            // 2. Re-disable floors we re-enabled during deletion
            foreach (var floor in _reEnabledFloors)
            {
                if (floor != null)
                    floor.SetActive(false);
            }
            _reEnabledFloors.Clear();

            // 3. Registration is now handled automatically by _target.SetActive(true) -> PlacedObject.OnEnable()

            // 4. Deduct money (un-refund)
            _money.Deduct(_data.cost, _data.category);
            _money.AddHourlyCost(_data.hourlyCost);

            // 5. Ensure any highlights are cleared before enabling
            var highlighter = _target.GetComponent<BuildingHighlighter>();
            if (highlighter != null)
                highlighter.HighlightDelete(false);

            // 6. Enable object
            _target.SetActive(true);

             NavMeshManager.Instance.MarkDirty();
    }
    public void Redo()
    {
        Execute();
    }
}
