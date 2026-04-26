using UnityEngine;

public class DeleteCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly ObjDataSO _data;
    private readonly Vector2Int[] _offsets;
    private readonly Vector2Int _root;
    private readonly MoneyService _money;

    private GameObject _target;
    private bool _wasActive;

    public DeleteCommand(GameObject target, PlacementGrid grid, MoneyService money)
    {
        _target = target;
        _grid = grid;
        _money = money;
        // Extract placement info from BuildingData
        var bd = target.GetComponent<BuildingData>();
        _data = bd.Data;
        _root = bd.RootCell;
        _offsets = bd.Offsets;

    }

    public void Execute()
    {
        if (_target == null)
            return;

        // Remove from grid
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.RemoveStackObject(cell, _target, _data);
        }
        _money.Refund(_data.cost, _data.category);
        _money.RemoveHourlyCost(_data.hourlyCost);

        // Disable object
        _wasActive = _target.activeSelf;
        _target.SetActive(false);
    }

    public void Undo()
    {
        if (_target == null)
            return;

        // Re-add to grid
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.AddStackObject(cell, _target, _data);
        }
        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost);

        // Re-enable object
        _target.SetActive(_wasActive);
    }

    public void Redo()
    {
        _money.Refund(_data.cost, _data.category);
        _money.RemoveHourlyCost(_data.hourlyCost);

        Execute();
    }
}
