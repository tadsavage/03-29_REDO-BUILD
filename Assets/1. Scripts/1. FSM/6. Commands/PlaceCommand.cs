using UnityEngine;

public class PlaceCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;

    private readonly Vector2Int _root;
    private readonly Vector2Int[] _offsets;
    private readonly ObjDataSO _data;
    private readonly float _rotation;

    private GameObject _instance;

    public PlaceCommand(
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        Vector2Int root,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation)
    {
        _grid = grid;
        _finalizer = finalizer;
        _root = root;
        _offsets = offsets;
        _data = data;
        _rotation = rotation;
    }

    public void Execute()
    {
        _instance = _finalizer.FinalizePlacement(_root, _offsets, _data, _rotation);
    }

    public void Undo()
    {
        if (_instance == null)
            return;

        var bd = _instance.GetComponent<BuildingData>();

        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            var list = _grid.GetObjectsInCell(cell);
            if (list == null) continue;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].instance == _instance)
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
}
