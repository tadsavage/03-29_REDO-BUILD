using GameCore.Build;
using GameCore.Economy;
using GameCore.Events;
using UnityEngine;

public class RevertFloorTileCommand : PlacementCommandBase
{
    private readonly GameObject _customFloor;
    private readonly GameObject _defaultFloor;
    private readonly BuildingData _foundation;
    private readonly Vector2Int _cell;
    private readonly PlacementGrid _grid;
    private readonly MoneyService _money;

    private readonly ObjDataSO _customData;
    private readonly ObjDataSO _defaultData;
    private readonly int _customRefund;

    public RevertFloorTileCommand(
        GameObject customFloor,
        GameObject defaultFloor,
        BuildingData foundation,
        Vector2Int cell,
        PlacementGrid grid,
        MoneyService money)
        : base($"Revert Tile ({cell.x}, {cell.y})")
    {
        _customFloor = customFloor;
        _defaultFloor = defaultFloor;
        _foundation = foundation;
        _cell = cell;
        _grid = grid;
        _money = money;

        var customPO = customFloor != null ? customFloor.GetComponent<PlacedObject>() : null;
        _customData = customPO != null ? customPO.data : customFloor?.GetComponent<BuildingData>()?.Data;

        var defaultPO = defaultFloor != null ? defaultFloor.GetComponent<PlacedObject>() : null;
        _defaultData = defaultPO != null ? defaultPO.data : (defaultFloor?.GetComponent<BuildingData>()?.Data ?? foundation?.Data?.defaultFloorTile);

        if (_customData != null && _money != null)
        {
            _customRefund = Mathf.RoundToInt(_customData.cost * _money.SellBackRate);
        }
    }

    public override void Execute()
    {
        // 1. Remove custom floor tile from grid and deactivate
        if (_customFloor != null && _customData != null)
        {
            var bd = _customFloor.GetComponent<BuildingData>();
            if (bd != null && bd.Offsets != null)
            {
                foreach (var o in bd.Offsets)
                    _grid.RemoveStackObject(bd.RootCell + o, _customFloor, _customData);
            }
            else
            {
                _grid.RemoveStackObject(_cell, _customFloor, _customData);
            }

            _customFloor.SetActive(false);

            if (_money != null)
            {
                _money.Refund(_customRefund, _customData.category);
                _money.RemoveHourlyCost(_customData.hourlyCost, FinanceCategory.ForHourlyCost(_customData.category), _customData.category);
            }

            PublishBuildEvent(GameEvents.Build.OnObjectDeleted, _customFloor.GetComponent<PlacedObject>());
        }

        // 2. Re-enable default floor tile and add to grid
        if (_defaultFloor != null && _defaultData != null)
        {
            _defaultFloor.SetActive(true);
            var bd = _defaultFloor.GetComponent<BuildingData>();
            if (bd != null && bd.Offsets != null)
            {
                foreach (var o in bd.Offsets)
                    _grid.AddStackObject(bd.RootCell + o, _defaultFloor, _defaultData);
            }
            else
            {
                _grid.AddStackObject(_cell, _defaultFloor, _defaultData);
            }

            if (_money != null)
            {
                _money.AddHourlyCost(_defaultData.hourlyCost, FinanceCategory.ForHourlyCost(_defaultData.category), _defaultData.category);
            }

            PublishBuildEvent(GameEvents.Build.OnObjectPlaced, _defaultFloor.GetComponent<PlacedObject>());
        }

        if (_customRefund != 0)
        {
            FloatingMoneyText.Show(_grid.GetCellCenter(_cell) + Vector3.up * 1.5f, _customRefund);
        }

        _grid.UpdateStackPositions(_cell);
        NavMeshManager.Instance?.MarkDirty();
    }

    public override void Undo()
    {
        // 1. Deactivate default floor tile and remove from grid
        if (_defaultFloor != null && _defaultData != null)
        {
            var bd = _defaultFloor.GetComponent<BuildingData>();
            if (bd != null && bd.Offsets != null)
            {
                foreach (var o in bd.Offsets)
                    _grid.RemoveStackObject(bd.RootCell + o, _defaultFloor, _defaultData);
            }
            else
            {
                _grid.RemoveStackObject(_cell, _defaultFloor, _defaultData);
            }

            _defaultFloor.SetActive(false);

            if (_money != null)
            {
                _money.RemoveHourlyCost(_defaultData.hourlyCost, FinanceCategory.ForHourlyCost(_defaultData.category), _defaultData.category);
            }
        }

        // 2. Reactivate custom floor tile and add to grid
        if (_customFloor != null && _customData != null)
        {
            _customFloor.SetActive(true);
            var bd = _customFloor.GetComponent<BuildingData>();
            if (bd != null && bd.Offsets != null)
            {
                foreach (var o in bd.Offsets)
                    _grid.AddStackObject(bd.RootCell + o, _customFloor, _customData);
            }
            else
            {
                _grid.AddStackObject(_cell, _customFloor, _customData);
            }

            if (_money != null)
            {
                _money.Deduct(_customRefund, _customData.category);
                _money.AddHourlyCost(_customData.hourlyCost, FinanceCategory.ForHourlyCost(_customData.category), _customData.category);
            }

            PublishBuildEvent(GameEvents.Build.OnObjectPlaced, _customFloor.GetComponent<PlacedObject>());
        }

        _grid.UpdateStackPositions(_cell);
        NavMeshManager.Instance?.MarkDirty();
    }

    public override void Redo()
    {
        Execute();
    }
}
