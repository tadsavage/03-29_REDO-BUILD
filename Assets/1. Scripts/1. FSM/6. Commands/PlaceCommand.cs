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

        // Tell NavMesh to update
        NavMeshManager.Instance.MarkDirty();
    }

    public void Undo()
    {
        if (_instance == null) return;

        foreach (var o in _offsets)
            _grid.RemoveStackObject(_root + o, _instance, _data);

        _instance.SetActive(false);
        _money.Refund(_data.cost, _data.category);
        _money.RemoveHourlyCost(_data.hourlyCost);

        foreach (var floor in _disabledFloors)
            if (floor != null) floor.SetActive(true);

        NavMeshManager.Instance.MarkDirty();
    }

    public void Redo()
    {
        foreach (var o in _offsets)
            _grid.AddStackObject(_root + o, _instance, _data);

        _instance.SetActive(true);
        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost);

        NavMeshManager.Instance.MarkDirty();
    }
}