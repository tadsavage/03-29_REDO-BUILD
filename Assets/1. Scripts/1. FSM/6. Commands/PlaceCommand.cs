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

    // ---------------------------------------------------------
    // EXECUTE (Place)
    // ---------------------------------------------------------
    public void Execute()
    {
        // First-time placement
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

        // Reactivate on redo
        _instance.SetActive(true);

        // Deduct cost
        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost);
    }

    // ---------------------------------------------------------
    // UNDO (Remove)
    // ---------------------------------------------------------
    public void Undo()
    {
        if (_instance == null)
            return;

        // Remove from grid
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.RemoveStackObject(cell, _instance, _data);
        }

        // Hide object
        _instance.SetActive(false);

        // Refund cost
        _money.Refund(_data.cost, _data.category);
        _money.RemoveHourlyCost(_data.hourlyCost);

        // Re-enable floors that were disabled
        foreach (var floor in _disabledFloors)
        {
            if (floor != null)
                floor.SetActive(true);
        }
    }

    // ---------------------------------------------------------
    // REDO (Re-place)
    // ---------------------------------------------------------
    public void Redo()
    {
        if (_instance == null)
            return;

        // Re-add to grid
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.AddStackObject(cell, _instance, _data);
        }

        // Reactivate
        _instance.SetActive(true);

        // Deduct cost again
        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost);

        // Re-disable floors if needed
        foreach (var floor in _disabledFloors)
        {
            if (floor != null)
                floor.SetActive(false);
        }
    }
}
