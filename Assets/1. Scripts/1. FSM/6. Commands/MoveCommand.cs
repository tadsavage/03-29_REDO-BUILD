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
        Debug.Log($"Moving from {from} to {to}");
        // 1. Unregister from old cells
        foreach (var o in _offsets)
        {
            Vector2Int cell = from + o;
            _grid.RemoveStackObject(cell, _instance, _data);
        }

        // 2. Compute height BEFORE adding object
        float stackY = _data.isStackable
            ? _grid.GetStackHeight(to)
            : 0f;
        Debug.Log($"Stack Y: {stackY}");
        // 3. Move object in world space
        Vector3 pos = _grid.GetCellCenter(to);
        pos.y += stackY;
        _instance.transform.position = pos;
        Debug.Log($"New position: {pos}");
        FXPool.Instance.Play("dust", pos);
        Debug.Log($"Moved object to {pos}");
        // 4. Register in new cells
        foreach (var o in _offsets)
        {
            Vector2Int cell = to + o;
            _grid.AddStackObject(cell, _instance, _data);
        }
        Debug.Log($"Registered object in new cells");
        // 5. Update BuildingData
        var bd = _instance.GetComponent<BuildingData>();
        bd.Initialize(to, _rotation, _offsets);
                Debug.Log($"Updated BuildingData for object at {to} with rotation {_rotation} and offsets [{string.Join(", ", _offsets)}]");
        // 6. Ensure object is active
        _instance.SetActive(true);
    }
}
