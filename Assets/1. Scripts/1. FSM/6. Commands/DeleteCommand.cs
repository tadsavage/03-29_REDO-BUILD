using UnityEngine;

public class DeleteCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly GameObject _instance;
    private readonly ObjDataSO _data;
    private readonly Vector2Int _root;
    private readonly Vector2Int[] _offsets;
    private readonly float _rotation;

    public DeleteCommand(GameObject instance, PlacementGrid grid)
    {
        _instance = instance;
        _grid = grid;

        var bd = instance.GetComponent<BuildingData>();
        _data = bd.Data;
        _root = bd.RootCell;
        _offsets = bd.Offsets;
        _rotation = bd.Rotation;
    }

    public void Execute()
    {
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
                    _grid.RemoveStackObject(cell, _instance, _data);
                }
            }
        }

        bd.Delete();
    }

    public void Undo()
    {
        float stackY = _data.isStackable ? _grid.GetStackHeight(_root) : 0f;

        Vector3 pos = _grid.GetCellCenter(_root);
        pos.y += stackY;

        Quaternion rot = Quaternion.Euler(0f, _rotation, 0f);

        GameObject restored = Object.Instantiate(_data.prefab, pos, rot);

        var bd = restored.GetComponent<BuildingData>();
        bd.Initialize(_root, _rotation, _offsets);

        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.AddStackObject(cell, restored, _data);
        }
    }
}
