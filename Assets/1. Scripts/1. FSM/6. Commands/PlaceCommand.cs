using System.Collections.Generic;
using UnityEngine;

public class PlaceCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;
    private readonly MoneyService _money;

    private readonly Vector2Int _root;
    private readonly Vector2Int[] _offsets;
    private readonly ObjDataSO _data;
    private readonly float _rotation;

    private GameObject _instance;
    private readonly List<GameObject> _disabledFloors = new();

    public PlaceCommand(PlacementGrid grid, PlacementFinalizer finalizer, Vector2Int root, Vector2Int[] offsets, ObjDataSO data, float rotation, MoneyService money)
    {
        _grid = grid;
        _finalizer = finalizer;
        _root = root;
        _offsets = offsets;
        _data = data;
        _rotation = rotation;
        _money = money;
    }

    public void Execute()
    {
        if (_instance == null)
        {
            _instance = _finalizer.FinalizePlacement(_root, _offsets, _data, _rotation, _disabledFloors);
        }

        if (_instance == null) return;

        _instance.SetActive(true);
        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost);

        // Floating "$" UX — red number rising out as money leaves capital.
        if (_data.cost != 0)
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -_data.cost);

        // Explicitly force height recalculation for all cells in footprint
        foreach (var o in _offsets)
            _grid.UpdateStackPositions(_root + o);

        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || _data.CanUseStairs)
        {
            NavMeshManager.Instance.MarkDirty();
        }
        }

        public void Undo()
        {
        if (_instance == null) return;

        foreach (var o in _offsets)
            _grid.RemoveStackObject(_root + o, _instance, _data);

        _instance.SetActive(false);
        _money.Refund(_data.cost, _data.category);
        _money.RemoveHourlyCost(_data.hourlyCost);

        // Floating "$" UX — green number as the purchase is refunded on undo.
        if (_data.cost != 0)
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, _data.cost);

        bool revealedFloor = false;
        foreach (var floor in _disabledFloors)
        {
            if (floor != null)
            {
                floor.SetActive(true);
                revealedFloor = true;
            }
        }

        // Explicitly force height recalculation for all cells in footprint
        foreach (var o in _offsets)
            _grid.UpdateStackPositions(_root + o);

        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || _data.CanUseStairs || revealedFloor)
        {
            NavMeshManager.Instance.MarkDirty();
        }
        }

        public void Redo()
        {
        if (_instance == null) return;

        // 1. Enable first so UpdateStackPositions sees it
        _instance.SetActive(true);

        foreach (var o in _offsets)
        {
            _grid.AddStackObject(_root + o, _instance, _data);
        }

        // Explicitly force height recalculation for all cells in footprint
        foreach (var o in _offsets)
            _grid.UpdateStackPositions(_root + o);

        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost);

        // Floating "$" UX — red number again as money leaves capital on redo.
        if (_data.cost != 0)
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -_data.cost);

        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || _data.CanUseStairs)
        {
            NavMeshManager.Instance.MarkDirty();
        }
        }
}