using System.Collections.Generic;
using UnityEngine;

public class PlaceCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;
    private readonly MoneyService _money;

    private readonly Vector2Int _root;
    private readonly Vector2Int[] _offsets;
    private readonly ObjDataSO _data;
    private readonly float _rotation;

    // The primary placed object
    private GameObject _instance;
    // Floors (yard tiles, old floor upgrades) displaced by the primary object
    private readonly List<GameObject> _disabledFloors = new();

    // Auto-placed floor tiles that accompany a Foundation (one per footprint cell)
    private readonly List<GameObject> _autoFloors = new();
    private ObjDataSO _autoFloorData;
    private readonly List<GameObject> _autoFloorDisabled = new();

    // Total cost paid for auto-floors (all cells combined)
    private int _autoFloorTotalCost;

    // Net cost of replaced floors — used so we charge only the diff for tile upgrades
    private int _replacedFloorsCost;

    private static bool IsGround(ObjDataSO d) =>
        d != null && (d.category == "Foundation" || d.category == "Grounds");

    public PlaceCommand(PlacementGrid grid, PlacementFinalizer finalizer,
        Vector2Int root, Vector2Int[] offsets, ObjDataSO data,
        float rotation, MoneyService money)
    {
        _grid = grid;
        _finalizer = finalizer;
        _root = root;
        _offsets = offsets;
        _data = data;
        _rotation = rotation;
        _money = money;
    }

    public void Execute()
    {
        if (_instance == null)
        {
            _instance = _finalizer.FinalizePlacement(_root, _offsets, _data, _rotation, _disabledFloors);
        }

        if (_instance == null) return;

        _instance.SetActive(true);

        // --- Cost handling ---
        // For floor-tile placements: charge only the difference over the old tile.
        _replacedFloorsCost = 0;
        if (_data.isFloor)
        {
            foreach (var floor in _disabledFloors)
            {
                if (floor == null) continue;
                var po = floor.GetComponent<PlacedObject>();
                if (po?.data?.isFloor != true) continue;
                _replacedFloorsCost += po.data.cost;
                _money.Refund(po.data.cost, po.data.category);
                _money.RemoveHourlyCost(po.data.hourlyCost);

                // Remove from grid so DeleteCommand doesn't wrongly restore this tile
                // if the parent Foundation is deleted while this floor is displaced.
                var bd = floor.GetComponent<BuildingData>();
                if (bd != null)
                    foreach (var o in bd.Offsets)
                        _grid.RemoveStackObject(bd.RootCell + o, floor, po.data);
            }
        }

        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost);

        // Floating money: net cost difference for floor upgrades; full cost otherwise
        if (_data.isFloor)
        {
            int netCost = _data.cost - _replacedFloorsCost;
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -netCost);
        }
        else if (_data.cost != 0)
        {
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -_data.cost);
        }

        // --- Auto-floor for foundations ---
        // Place one floor tile per footprint cell so the entire slab is covered.
        if (_data.defaultFloorTile != null)
            Debug.Log($"[PlaceCommand] defaultFloorTile={_data.defaultFloorTile.objName}, autoFloors.Count={_autoFloors.Count}, offsets={_offsets.Length}");
        if (_data.defaultFloorTile != null && _autoFloors.Count == 0)
        {
            _autoFloorData = _data.defaultFloorTile;
            Vector2Int[] tileOffsets = _autoFloorData.GetFootprintOffsets(0f); // always 1×1

            _autoFloorTotalCost = 0;
            foreach (var o in _offsets)
            {
                Vector2Int cellRoot = _root + o;
                var tile = _finalizer.FinalizePlacement(cellRoot, tileOffsets, _autoFloorData, 0f, _autoFloorDisabled);
                Debug.Log($"[PlaceCommand] Auto-floor at {cellRoot}: {(tile != null ? "spawned" : "NULL (blocked)")}");
                if (tile == null) continue;
                tile.SetActive(true);
                _autoFloors.Add(tile);
                _autoFloorTotalCost += _autoFloorData.cost;
                _money.Deduct(_autoFloorData.cost, _autoFloorData.category);
                _money.AddHourlyCost(_autoFloorData.hourlyCost);
            }
        }

        // Force height recalculation for all footprint cells
        foreach (var o in _offsets)
            _grid.UpdateStackPositions(_root + o);

        // NavMesh: foundations, grounds, floors, pathfinding-clear, stairs all need a bake
        if (NeedsNavMesh(_data) || _disabledFloors.Count > 0)
            NavMeshManager.Instance.MarkDirty();
    }

    public void Undo()
    {
        if (_instance == null) return;

        // 1. Remove auto-floor tiles (one per foundation cell)
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

        // 2. Remove primary object
        foreach (var o in _offsets)
            _grid.RemoveStackObject(_root + o, _instance, _data);
        _instance.SetActive(false);

        // 3. Reverse floor cost diff (re-charge old tiles we refunded)
        if (_data.isFloor && _replacedFloorsCost > 0)
        {
            foreach (var floor in _disabledFloors)
            {
                if (floor == null) continue;
                var po = floor.GetComponent<PlacedObject>();
                if (po?.data?.isFloor != true) continue;
                _money.Deduct(po.data.cost, po.data.category);
                _money.AddHourlyCost(po.data.hourlyCost);
            }
        }

        _money.Refund(_data.cost, _data.category);
        _money.RemoveHourlyCost(_data.hourlyCost);

        // Re-enable displaced floors
        bool revealedFloor = false;
        foreach (var floor in _disabledFloors)
        {
            if (floor == null) continue;
            floor.SetActive(true);
            revealedFloor = true;

            // For floor-on-floor replacements Execute removed the tile from the grid;
            // add it back so grid state is consistent after undo.
            if (_data.isFloor)
            {
                var po = floor.GetComponent<PlacedObject>();
                var bd = floor.GetComponent<BuildingData>();
                if (po?.data?.isFloor == true && bd != null)
                    foreach (var o in bd.Offsets)
                        _grid.AddStackObject(bd.RootCell + o, floor, po.data);
            }
        }

        foreach (var o in _offsets)
            _grid.UpdateStackPositions(_root + o);

        if (NeedsNavMesh(_data) || revealedFloor)
            NavMeshManager.Instance.MarkDirty();
    }

    public void Redo()
    {
        if (_instance == null) return;

        // 1. Re-disable floors replaced by primary object
        foreach (var floor in _disabledFloors)
        {
            if (floor == null) continue;
            floor.SetActive(false);

            // Mirror Execute: remove from grid for floor-on-floor replacements.
            if (_data.isFloor)
            {
                var po = floor.GetComponent<PlacedObject>();
                var bd = floor.GetComponent<BuildingData>();
                if (po?.data?.isFloor == true && bd != null)
                    foreach (var o in bd.Offsets)
                        _grid.RemoveStackObject(bd.RootCell + o, floor, po.data);
            }
        }

        // 2. Re-enable primary + add to grid
        _instance.SetActive(true);
        foreach (var o in _offsets)
            _grid.AddStackObject(_root + o, _instance, _data);

        // 3. Re-enable and re-add auto-floor tiles
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
            }
        }

        foreach (var o in _offsets)
            _grid.UpdateStackPositions(_root + o);

        // Mirror Execute() money
        if (_data.isFloor && _replacedFloorsCost > 0)
        {
            foreach (var floor in _disabledFloors)
            {
                if (floor == null) continue;
                var po = floor.GetComponent<PlacedObject>();
                if (po?.data?.isFloor != true) continue;
                _money.Refund(po.data.cost, po.data.category);
                _money.RemoveHourlyCost(po.data.hourlyCost);
            }
        }
        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost);

        if (_data.isFloor)
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -(_data.cost - _replacedFloorsCost));
        else if (_data.cost != 0)
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -_data.cost);

        if (_autoFloors.Count > 0 && _autoFloorData != null)
        {
            foreach (var tile in _autoFloors)
            {
                if (tile == null) continue;
                _money.Deduct(_autoFloorData.cost, _autoFloorData.category);
                _money.AddHourlyCost(_autoFloorData.hourlyCost);
            }
        }

        if (NeedsNavMesh(_data))
            NavMeshManager.Instance.MarkDirty();
    }

    private static bool NeedsNavMesh(ObjDataSO d) =>
        d.isFloor || d.pathfindingClear || d.ignorePlacementRules || d.CanUseStairs || IsGround(d);
}
