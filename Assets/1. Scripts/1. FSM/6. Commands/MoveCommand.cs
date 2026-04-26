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

    public MoveCommand(
        PlacementGrid grid,
        GameObject obj,
        ObjDataSO data,
        Vector2Int oldRoot,
        Vector2Int newRoot,
        Vector2Int[] offsets,
        float rotation)
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
        if (_instance == null)
            return;

        // ---------------------------------------------------------
        // 1. Remove from old grid cells
        // ---------------------------------------------------------
        foreach (var o in _offsets)
        {
            Vector2Int cell = from + o;
            _grid.RemoveStackObject(cell, _instance, _data);
        }

        // ---------------------------------------------------------
        // 2. Compute stack height BEFORE placing
        // ---------------------------------------------------------
        float stackY = 0f;
        if (_data.isStackable)
            stackY = _grid.GetStackHeight(to);

        // ---------------------------------------------------------
        // 3. Move object in world space
        // ---------------------------------------------------------
        Vector3 pos = _grid.GetCellCenter(to);
        pos.y += stackY;

        Quaternion rot = Quaternion.Euler(0f, _rotation, 0f);

        _instance.SetActive(true);
        _instance.transform.position = pos;
        _instance.transform.rotation = rot;

        // ---------------------------------------------------------
        // 4. Add to new grid cells
        // ---------------------------------------------------------
        foreach (var o in _offsets)
        {
            Vector2Int cell = to + o;
            _grid.AddStackObject(cell, _instance, _data);
        }
        // 5. Update BuildingData
        var bd = _instance.GetComponent<BuildingData>();
        bd.Initialize(to, _rotation, _offsets);
        //Debug.Log($"Placed w/Finalizer {_data.objName} at {to}");

        // ---------------------------------------------------------
        // 5b. Update PlacedObject logical coordinates (CRITICAL)
        // ---------------------------------------------------------
        var po = _instance.GetComponent<PlacedObject>();
        po.gridX = to.x;
        po.gridY = to.y;
        // ---------------------------------------------------------
        // 6. FX (once, not per cell)
        // ---------------------------------------------------------
        FXPool.Instance.Play("dust", pos);
    }
}
