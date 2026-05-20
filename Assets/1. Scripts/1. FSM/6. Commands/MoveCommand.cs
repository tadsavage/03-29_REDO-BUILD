using UnityEngine;

public class MoveCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly GameObject _instance;
    private readonly ObjDataSO _data;
    
    private readonly Vector2Int _oldRoot;
    private readonly Vector2Int _newRoot;
    
    private readonly Vector2Int[] _oldOffsets;
    private readonly Vector2Int[] _newOffsets;
    
    private readonly float _oldRotation;
    private readonly float _newRotation;

    public MoveCommand(
        PlacementGrid grid, 
        GameObject obj, 
        ObjDataSO data, 
        Vector2Int oldRoot, 
        Vector2Int newRoot, 
        Vector2Int[] oldOffsets, 
        Vector2Int[] newOffsets, 
        float oldRotation, 
        float newRotation)
    {
        _grid = grid;
        _instance = obj;
        _data = data;
        _oldRoot = oldRoot;
        _newRoot = newRoot;
        _oldOffsets = oldOffsets;
        _newOffsets = newOffsets;
        _oldRotation = oldRotation;
        _newRotation = newRotation;
    }

    public void Execute() => Move(_oldRoot, _newRoot, _oldOffsets, _newOffsets, _newRotation);
    public void Undo() => Move(_newRoot, _oldRoot, _newOffsets, _oldOffsets, _oldRotation);
    public void Redo() => Execute();

    private void Move(Vector2Int from, Vector2Int to, Vector2Int[] fromOffsets, Vector2Int[] toOffsets, float toRotation)
    {
        if (_instance == null) return;

        foreach (var o in fromOffsets)
            _grid.RemoveStackObject(from + o, _instance, _data);

        _instance.transform.rotation = Quaternion.Euler(0f, toRotation, 0f);
        _instance.SetActive(true);

        // Update BuildingData BEFORE adding to grid so UpdateStackPositions knows the new root
        var bd = _instance.GetComponent<BuildingData>();
        if (bd != null)
        {
            bd.Initialize(to, toRotation, toOffsets);
        }

        foreach (var o in toOffsets)
            _grid.AddStackObject(to + o, _instance, _data);

        // Explicitly force height recalculation for all cells in old and new footprint
        foreach (var o in fromOffsets)
            _grid.UpdateStackPositions(from + o);
        foreach (var o in toOffsets)
            _grid.UpdateStackPositions(to + o);

        var po = _instance.GetComponent<PlacedObject>();
        if (po != null)
        {
            po.gridX = to.x;
            po.gridY = to.y;
            po.rotation = (int)(toRotation / 90f);
        }

        Vector3 pos = _grid.GetCellCenter(to);
        FXPool.Instance.Play("dust", pos);

        // Tell NavMesh to update if it's a modifier-based object
        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules)
        {
            NavMeshManager.Instance.MarkDirty();
        }
        }
}