using UnityEngine;
using System.Collections.Generic;

public class MoveCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly GameObject _instance;
    private readonly ObjDataSO _data;
    private readonly MoneyService _money;
    
    private readonly Vector2Int _oldRoot;
    private readonly Vector2Int _newRoot;
    
    private readonly Vector2Int[] _oldOffsets;
    private readonly Vector2Int[] _newOffsets;
    
    private readonly float _oldRotation;
    private readonly float _newRotation;

    private struct ReplacedData
    {
        public GameObject instance;
        public ObjDataSO data;
        public Vector2Int root;
        public Vector2Int[] offsets;
        public float rotation;
    }
    private readonly List<ReplacedData> _replaced = new();

    public MoveCommand(
        PlacementGrid grid, 
        GameObject obj, 
        ObjDataSO data, 
        Vector2Int oldRoot, 
        Vector2Int newRoot, 
        Vector2Int[] oldOffsets, 
        Vector2Int[] newOffsets, 
        float oldRotation, 
        float newRotation,
        MoneyService money)
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
        _money = money;
    }

    public void Execute()
    {
        if (IsFoundation(_data))
            HandleReplacement(_newRoot, _newOffsets);

        Move(_oldRoot, _newRoot, _oldOffsets, _newOffsets, _newRotation);
    }

    public void Undo()
    {
        Move(_newRoot, _oldRoot, _newOffsets, _oldOffsets, _oldRotation);

        if (_replaced.Count > 0)
            RestoreReplaced();
    }

    public void Redo() => Execute();

    private bool IsFoundation(ObjDataSO data)
    {
        if (data == null) return false;
        return data.category == "Foundation" || data.category == "Grounds";
    }

    private void HandleReplacement(Vector2Int root, Vector2Int[] offsets)
    {
        _replaced.Clear();
        HashSet<GameObject> found = new HashSet<GameObject>();

        foreach (var o in offsets)
        {
            var cellObjs = _grid.GetObjectsInCell(root + o);
            if (cellObjs == null) continue;

            foreach (var entry in cellObjs)
            {
                if (entry.instance != null && entry.instance != _instance && IsFoundation(entry.data))
                {
                    found.Add(entry.instance);
                }
            }
        }

        foreach (var obj in found)
        {
            var bd = obj.GetComponent<BuildingData>();
            if (bd == null) continue;

            // Store data for Undo
            _replaced.Add(new ReplacedData
            {
                instance = obj,
                data = bd.Data,
                root = bd.RootCell,
                offsets = bd.Offsets,
                rotation = bd.Rotation
            });

            // Remove from grid
            foreach (var o in bd.Offsets)
            {
                _grid.RemoveStackObject(bd.RootCell + o, obj, bd.Data);
            }

            // Refund
            if (_money != null)
            {
                _money.Refund(bd.Data.cost, bd.Data.category);
                _money.RemoveHourlyCost(bd.Data.hourlyCost);
            }

            // Disable
            var highlighter = obj.GetComponent<BuildingHighlighter>();
            if (highlighter != null)
                highlighter.HighlightValid(false);

            obj.SetActive(false);
        }
    }

    private void RestoreReplaced()
    {
        foreach (var rd in _replaced)
        {
            if (rd.instance == null) continue;

            rd.instance.SetActive(true);

            // Re-add to grid
            foreach (var o in rd.offsets)
            {
                _grid.AddStackObject(rd.root + o, rd.instance, rd.data);
            }

            // Deduct refund
            if (_money != null)
            {
                _money.Deduct(rd.data.cost, rd.data.category);
                _money.AddHourlyCost(rd.data.hourlyCost);
            }
        }
        _replaced.Clear();
    }

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

        // Immediate position at cell center as a baseline
        _instance.transform.position = _grid.GetCellCenter(to);

        // If it has an agent, disable it temporarily so UpdateStackPositions sets transform.position directly
        // rather than using agent.Warp which might fail if not near a navmesh
        var agent = _instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        bool wasAgentEnabled = agent != null && agent.enabled;
        if (agent != null) agent.enabled = false;

        // Explicitly force height recalculation for all cells in old and new footprint
        foreach (var o in fromOffsets)
            _grid.UpdateStackPositions(from + o);
        foreach (var o in toOffsets)
            _grid.UpdateStackPositions(to + o);

        // Landing Logic: Capture the CORRECT target position calculated by the grid
        Vector3 finalPos = _instance.transform.position;
        float offset = PreviewController.Instance != null ? PreviewController.Instance.OffsetMovePreview : 1.0f;
        float smooth = PreviewController.Instance != null ? PreviewController.Instance.MoveSmoothTime : 0.1f;

        // Clean up any existing landing component if the user is moving fast
        var oldLanding = _instance.GetComponent<SmoothLanding>();
        if (oldLanding != null) Object.DestroyImmediate(oldLanding);

        var landing = _instance.AddComponent<SmoothLanding>();
        landing.Initialize(finalPos + Vector3.up * offset, finalPos, smooth);

        var po = _instance.GetComponent<PlacedObject>();
        if (po != null)
        {
            po.gridX = to.x;
            po.gridY = to.y;
            po.rotation = (int)(toRotation / 90f);
        }

        // Tell NavMesh to update if it's a modifier-based object or if we replaced foundations
        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || IsFoundation(_data) || _replaced.Count > 0)
        {
            NavMeshManager.Instance.MarkDirty();
        }
}
}