using System.Collections.Generic;
using UnityEngine;

public class DragPlaceCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;

    private readonly List<Vector2Int> _cells;
    private readonly Vector2Int[] _offsets;
    private readonly ObjDataSO _data;
    private readonly float _rotation;
    private readonly MoneyService _money;

    private readonly List<GameObject> _instances = new();
    private readonly List<GameObject> _disabledFloors = new();

    // Auto-floor tiles spawned for foundation drag-placements
    private readonly List<GameObject> _autoFloors = new();
    private readonly List<GameObject> _autoFloorDisabled = new();
    private ObjDataSO _autoFloorData;

    public DragPlaceCommand(
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        List<Vector2Int> cells,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation,
        MoneyService money)
    {
        _grid = grid;
        _finalizer = finalizer;
        _cells = cells;
        _offsets = offsets;
        _data = data;
        _rotation = rotation;
        _money = money;
    }

    public void Execute()
    {
        _instances.Clear();
        _disabledFloors.Clear();

        foreach (var cell in _cells)
        {
            GameObject placed = _finalizer.FinalizePlacement(
                cell,
                _offsets,
                _data,
                _rotation,
                _disabledFloors);

            if (placed != null)
            {
                _instances.Add(placed);

                _money.Deduct(_data.cost);
                _money.AddHourlyCost(_data.hourlyCost);

                foreach (var o in _offsets)
                    _grid.UpdateStackPositions(cell + o);
            }
        }

        // For floor-on-floor replacements: remove displaced tiles from grid so DeleteCommand
        // doesn't wrongly restore them if the parent Foundation is deleted later.
        if (_data.isFloor)
        {
            foreach (var floor in _disabledFloors)
            {
                if (floor == null) continue;
                var po = floor.GetComponent<PlacedObject>();
                if (po?.data?.isFloor != true) continue;
                var bd = floor.GetComponent<BuildingData>();
                if (bd != null)
                    foreach (var o in bd.Offsets)
                        _grid.RemoveStackObject(bd.RootCell + o, floor, po.data);
            }
        }

        // Auto-floor for foundations: one tile per occupied cell per placed foundation
        if (_data.defaultFloorTile != null && _autoFloors.Count == 0)
        {
            _autoFloorData = _data.defaultFloorTile;
            Vector2Int[] tileOffsets = _autoFloorData.GetFootprintOffsets(0f);

            foreach (var cell in _cells)
            {
                foreach (var o in _offsets)
                {
                    Vector2Int floorRoot = cell + o;
                    var tile = _finalizer.FinalizePlacement(floorRoot, tileOffsets, _autoFloorData, 0f, _autoFloorDisabled);
                    if (tile == null) continue;
                    tile.SetActive(true);
                    _autoFloors.Add(tile);
                    _money.Deduct(_autoFloorData.cost, _autoFloorData.category);
                    _money.AddHourlyCost(_autoFloorData.hourlyCost);
                }
            }
        }

        bool isGround = _data.category == "Foundation" || _data.category == "Grounds";
        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || _data.CanUseStairs || isGround)
            NavMeshManager.Instance.MarkDirty();
    }

    public void Undo()
    {
        // 1. Remove auto-floor tiles
        if (_autoFloors.Count > 0 && _autoFloorData != null)
        {
            Vector2Int[] tileOffsets = _autoFloorData.GetFootprintOffsets(0f);
            foreach (var tile in _autoFloors)
            {
                if (tile == null) continue;
                var bd = tile.GetComponent<BuildingData>();
                Vector2Int tileRoot = bd != null ? bd.RootCell : _grid.WorldToCell(tile.transform.position);
                foreach (var o in tileOffsets)
                    _grid.RemoveStackObject(tileRoot + o, tile, _autoFloorData);
                tile.SetActive(false);
                _money.Refund(_autoFloorData.cost, _autoFloorData.category);
                _money.RemoveHourlyCost(_autoFloorData.hourlyCost);
            }
            foreach (var floor in _autoFloorDisabled)
                if (floor != null) floor.SetActive(true);
        }

        // 2. Remove primary instances
        foreach (var instance in _instances)
        {
            if (instance == null) continue;

            var bd = instance.GetComponent<BuildingData>();
            if (bd == null)
            {
                Object.Destroy(instance);
                continue;
            }

            Vector2Int root = bd.RootCell;
            foreach (var o in _offsets)
                _grid.RemoveStackObject(root + o, instance, bd.Data);

            instance.SetActive(false);
            _money.Refund(_data.cost);
            _money.RemoveHourlyCost(_data.hourlyCost);
        }

        // 3. Restore any floors displaced by primary objects
        bool revealedFloor = false;
        foreach (var floor in _disabledFloors)
        {
            if (floor == null) continue;
            floor.SetActive(true);
            revealedFloor = true;

            if (_data.isFloor)
            {
                var po = floor.GetComponent<PlacedObject>();
                var bd = floor.GetComponent<BuildingData>();
                if (po?.data?.isFloor == true && bd != null)
                    foreach (var o in bd.Offsets)
                        _grid.AddStackObject(bd.RootCell + o, floor, po.data);
            }
        }

        foreach (var cell in _cells)
            foreach (var o in _offsets)
                _grid.UpdateStackPositions(cell + o);

        bool isGround = _data.category == "Foundation" || _data.category == "Grounds";
        if (revealedFloor || _data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || isGround)
            NavMeshManager.Instance.MarkDirty();
    }

    public void Redo()
    {
        // 1. Re-disable floors that were displaced
        foreach (var floor in _disabledFloors)
        {
            if (floor == null) continue;
            floor.SetActive(false);

            if (_data.isFloor)
            {
                var po = floor.GetComponent<PlacedObject>();
                var bd = floor.GetComponent<BuildingData>();
                if (po?.data?.isFloor == true && bd != null)
                    foreach (var o in bd.Offsets)
                        _grid.RemoveStackObject(bd.RootCell + o, floor, po.data);
            }
        }

        // 2. Re-enable primary instances and add back to grid
        foreach (var instance in _instances)
        {
            if (instance == null) continue;
            instance.SetActive(true);
            var bd = instance.GetComponent<BuildingData>();
            if (bd == null) continue;
            Vector2Int root = bd.RootCell;
            foreach (var o in _offsets)
                _grid.AddStackObject(root + o, instance, bd.Data);
            _money.Deduct(_data.cost);
            _money.AddHourlyCost(_data.hourlyCost);
        }

        // 3. Re-enable auto-floor tiles
        if (_autoFloors.Count > 0 && _autoFloorData != null)
        {
            Vector2Int[] tileOffsets = _autoFloorData.GetFootprintOffsets(0f);
            foreach (var floor in _autoFloorDisabled)
                if (floor != null) floor.SetActive(false);

            foreach (var tile in _autoFloors)
            {
                if (tile == null) continue;
                tile.SetActive(true);
                var bd = tile.GetComponent<BuildingData>();
                Vector2Int tileRoot = bd != null ? bd.RootCell : _grid.WorldToCell(tile.transform.position);
                foreach (var o in tileOffsets)
                    _grid.AddStackObject(tileRoot + o, tile, _autoFloorData);
                _money.Deduct(_autoFloorData.cost, _autoFloorData.category);
                _money.AddHourlyCost(_autoFloorData.hourlyCost);
            }
        }

        // 4. Update stack heights
        foreach (var cell in _cells)
            foreach (var o in _offsets)
                _grid.UpdateStackPositions(cell + o);

        bool isGround = _data.category == "Foundation" || _data.category == "Grounds";
        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || isGround)
            NavMeshManager.Instance.MarkDirty();
    }
}
