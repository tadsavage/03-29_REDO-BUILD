using UnityEngine;

public class MoveCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly GameObject _instance;
    private readonly ObjDataSO _data;
    private readonly Vector2Int _oldRoot;
    private readonly Vector2Int _newRoot;
    private readonly Vector2Int[] _offsets;
    private readonly float _rotation;

    public MoveCommand(PlacementGrid grid, GameObject obj, ObjDataSO data, Vector2Int oldRoot, Vector2Int newRoot, Vector2Int[] offsets, float rotation)
    {
        _grid = grid;
        _instance = obj;
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
        if (_instance == null) return;

        foreach (var o in _offsets)
            _grid.RemoveStackObject(from + o, _instance, _data);

        float stackY = _data.isFloor ? 0f : _grid.GetStackHeight(to);
        Vector3 pos = _grid.GetCellCenter(to);
        pos.y += stackY;

        _instance.transform.position = pos;
        _instance.transform.rotation = Quaternion.Euler(0f, _rotation, 0f);

        foreach (var o in _offsets)
            _grid.AddStackObject(to + o, _instance, _data);

        var bd = _instance.GetComponent<BuildingData>();
        bd.Initialize(to, _rotation, _offsets);

        var po = _instance.GetComponent<PlacedObject>();
        po.gridX = to.x;
        po.gridY = to.y;

        FXPool.Instance.Play("dust", pos);

        // Tell NavMesh to update if it's a modifier-based object
        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules)
        {
            NavMeshManager.Instance.MarkDirty();
        }
        }
}