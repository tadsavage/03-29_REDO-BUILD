using UnityEngine;

public class MoveCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;

    private readonly GameObject _instance;
    private readonly ObjDataSO _data;

    private readonly Vector2Int _oldRoot;
    private readonly Vector2Int _newRoot;
    private readonly Vector2Int[] _offsets;
    private readonly float _rotation;

    public MoveCommand(
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        GameObject instance,
        ObjDataSO data,
        Vector2Int oldRoot,
        Vector2Int newRoot,
        Vector2Int[] offsets,
        float rotation)
    {
        _grid = grid;
        _finalizer = finalizer;

        _instance = instance;
        _data = data;

        _oldRoot = oldRoot;
        _newRoot = newRoot;
        _offsets = offsets;
        _rotation = rotation;
    }

    public void Execute() => Move(_oldRoot, _newRoot);
    public void Undo() => Move(_newRoot, _oldRoot);

    private void Move(Vector2Int from, Vector2Int to)
    {
        // 1. Remove from old cells
        foreach (var o in _offsets)
        {
            Vector2Int cell = from + o;
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

        // 2. Compute height BEFORE adding object (long form)
        float stackY;
        if (_data.isStackable)
        {
            stackY = _grid.GetStackHeight(to);
        }
        else
        {
            stackY = 0f;
        }

        // 3. Move object in world space
        Vector3 pos = _grid.GetCellCenter(to);
        pos.y += stackY;
        _instance.transform.position = pos;
        FXPool.Instance.Play("dust", pos);

        // 4. Add to new cells
        foreach (var o in _offsets)
        {
            Vector2Int cell = to + o;
            _grid.AddStackObject(cell, _instance, _data);
            Debug.Log($"Added object to pos {pos}");
        }

        // 5. Update BuildingData
        var bd = _instance.GetComponent<BuildingData>();
        bd.Initialize(to, _rotation, _offsets);

        // 6. Reactivate object
        _instance.SetActive(true);
    }
}
