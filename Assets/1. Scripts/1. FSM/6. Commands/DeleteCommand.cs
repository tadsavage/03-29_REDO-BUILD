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

        var bd = target.GetComponent<BuildingData>();
        _data = bd.Data;
        _root = bd.RootCell;
        _offsets = bd.Offsets;
    }

    public void Execute()
    {
        if (_target == null)
            return;

        // 1. Remove from grid
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.RemoveStackObject(cell, _target, _data);
        }

        // 2. Remove from registry (CRITICAL)
        var po = _target.GetComponent<PlacedObject>();
        PlacedObjectRegistry.Unregister(po);

        // 3. Refund money
        _money.Refund(_data.cost, _data.category);
        _money.RemoveHourlyCost(_data.hourlyCost);

        // 4. Destroy object
        Object.Destroy(_target);
    }

    public void Undo()
    {
        // Undo requires respawning the object.
        // You can implement this later if needed.
        Debug.LogWarning("Undo for DeleteCommand not implemented.");
    }

    public void Redo()
    {
        Execute();
    }
}
