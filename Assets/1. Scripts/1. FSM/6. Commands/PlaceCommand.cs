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

    // Floors disabled when this object was placed
    private readonly List<GameObject> _disabledFloors = new();

    public PlaceCommand(
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation,
        MoneyService money)
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
            _instance = _finalizer.FinalizePlacement(
                _root,
                _offsets,
                _data,
                _rotation,
                _disabledFloors);
        }

        if (_instance == null)
            return;

        // Finalizer already added this instance to the grid for all offsets.
        // We only need to re-enable it on redo.
        _instance.SetActive(true);
        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost);
    }

    public void Undo()
    {
        if (_instance == null)
            return;

        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.RemoveStackObject(cell, _instance, _data);
        }

        _instance.SetActive(false);
        _money.Refund(_data.cost, _data.category);
        _money.RemoveHourlyCost(_data.hourlyCost);

        // Re-enable any floors we disabled
        foreach (var floor in _disabledFloors)
        {
            if (floor != null)
                floor.SetActive(true);
        }
    }

    public void Redo()
    {
        // Re-add to grid and reactivate
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.AddStackObject(cell, _instance, _data);
        }

        _instance.SetActive(true);
        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost);
    }
}
