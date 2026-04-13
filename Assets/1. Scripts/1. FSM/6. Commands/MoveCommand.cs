using UnityEngine;

public class MoveCommand : ICommand
{
    #region FIELDS ***************************************
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
    #endregion ******************************************

    public void Execute() => Move(_oldRoot, _newRoot);
    public void Undo() => Move(_newRoot, _oldRoot);

    private void Move(Vector2Int from, Vector2Int to)
    {
        // 1. Remove from old cells
        foreach (var o in _offsets)
            _grid.RemoveStackObject(from + o, _instance, _data);

        // 2. Compute stack height BEFORE placing
        float stackY = 0f;
        if (_data.isStackable)
            stackY = _grid.GetStackHeight(to);

        // 3. Move object in world space
        Vector3 pos = _grid.GetCellCenter(to);
        pos.y += stackY;
        Quaternion rot = Quaternion.Euler(0f, _rotation, 0f);

        // 4. Ensure object is active and Placed in right spot
        if (_instance != null)
        {
            _instance.SetActive(true);
            _instance.transform.position = pos;
            _instance.transform.rotation = rot;
        }
        // 5. Register in new cells
        foreach (var o in _offsets) 
        {
            _grid.AddStackObject(to + o, _instance, _data);
            FXPool.Instance.Play("dust", pos);
        }
        // 6. Update BuildingData
        var bd = _instance.GetComponent<BuildingData>();
        bd.Initialize(to, _rotation, _offsets);
    }
}
