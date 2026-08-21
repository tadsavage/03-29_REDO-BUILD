using GameCore.Build;
using GameCore.Economy;
using GameCore.Events;
using System.Collections.Generic;
using UnityEngine;

public class DragPlaceCommand : PlacementCommandBase
{
    private readonly PlacementGrid _grid;
    private readonly PlacementFinalizer _finalizer;

    private readonly List<Vector2Int> _cells;
    private readonly Vector2Int[] _offsets;
    private readonly ObjDataSO _data;
    private readonly float _rotation;
    private readonly MoneyService _money;

    private readonly List<GameObject> _instances = new();

    // Floors disabled across all cells in this drag operation
    private readonly List<GameObject> _disabledFloors = new();

    // Total cost refunded for floors displaced by a dragged ground (mirrors PlaceCommand's
    // _replacedGroundsCost) — without this, drag-placing a foundation over an existing paid
    // floor silently destroyed its value with no refund.
    private int _replacedGroundsCost;

    // Auto-floor tiles placed alongside grounds (one per footprint cell per ground placed)
    private readonly List<GameObject> _autoFloors = new();
    private ObjDataSO _autoFloorData;
    private readonly List<GameObject> _autoFloorDisabled = new();

    private static bool IsGround(ObjDataSO d) =>
        d != null && (d.category == "Foundation" || d.category == "Grounds");

    public DragPlaceCommand(
        PlacementGrid grid,
        PlacementFinalizer finalizer,
        List<Vector2Int> cells,
        Vector2Int[] offsets,
        ObjDataSO data,
        float rotation,
        MoneyService money)
        : base($"Place {cells?.Count ?? 0}x {data?.objName ?? "Object"}")
    {
        _grid = grid;
        _finalizer = finalizer;
        _cells = cells;
        _offsets = offsets;
        _data = data;
        _rotation = rotation;
        _money = money;
    }

    public override void Execute()
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
                _money.AddHourlyCost(_data.hourlyCost, FinanceCategory.ForHourlyCost(_data.category), _data.category);

                // Fire RackPlacedEvent if this is a racking object
                if (_data != null && _data.category == "Racking")
                {
                    RackPlacedEvent.Fire(placed);
                }

                foreach (var o in _offsets)
                    _grid.UpdateStackPositions(cell + o);
            }
        }

        // Refund floors/grounds displaced by this drag (e.g. a foundation dragged over an
        // existing paid floor tile) — mirrors PlaceCommand's ground-replacement refund.
        _replacedGroundsCost = 0;
        if (IsGround(_data) && _disabledFloors.Count > 0)
        {
            foreach (var obj in _disabledFloors)
            {
                if (obj == null) continue;
                var po = obj.GetComponent<PlacedObject>();
                if (po?.data == null) continue;
                _replacedGroundsCost += po.data.cost;
                _money.Refund(po.data.cost, po.data.category);
                _money.RemoveHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
            }
        }

        // Auto-floor: place one floor tile per footprint cell for every ground placed.
        //
        // A combined prefab (FoundationFloorGroup present) already ships its 4 default tiles as
        // real children — PlacementFinalizer initialized/registered them at the correct cells when
        // that instance was placed above. Spawning 4 MORE here would double the tiles and
        // double-charge for them, so this discovers the pre-existing children instead of calling
        // FinalizePlacement again. A plain Foundation (no group) keeps the original behavior.
        if (_data.defaultFloorTile != null && _autoFloors.Count == 0)
        {
            _autoFloorData = _data.defaultFloorTile;
            Vector2Int[] tileOffsets = _autoFloorData.GetFootprintOffsets(0f);

            foreach (var instance in _instances)
            {
                var bd = instance.GetComponent<BuildingData>();
                if (bd == null) continue;
                Vector2Int root = bd.RootCell;

                var floorGroup = instance.GetComponent<FoundationFloorGroup>();
                if (floorGroup != null && floorGroup.DefaultTiles != null)
                {
                    foreach (var tilePO in floorGroup.DefaultTiles)
                    {
                        if (tilePO == null) continue;
                        _autoFloors.Add(tilePO.gameObject);
                        _money.Deduct(_autoFloorData.cost, _autoFloorData.category);
                        _money.AddHourlyCost(_autoFloorData.hourlyCost, FinanceCategory.ForHourlyCost(_autoFloorData.category), _autoFloorData.category);
                    }
                    continue;
                }

                foreach (var o in _offsets)
                {
                    Vector2Int cellRoot = root + o;
                    var tile = _finalizer.FinalizePlacement(cellRoot, tileOffsets, _autoFloorData, 0f, _autoFloorDisabled);
                    if (tile == null) continue;
                    tile.SetActive(true);
                    _autoFloors.Add(tile);
                    _money.Deduct(_autoFloorData.cost, _autoFloorData.category);
                    _money.AddHourlyCost(_autoFloorData.hourlyCost, FinanceCategory.ForHourlyCost(_autoFloorData.category), _autoFloorData.category);
                    _grid.UpdateStackPositions(cellRoot);
                }
            }
        }

        bool isGround = IsGround(_data);
        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || _data.CanUseStairs || isGround)
            NavMeshManager.Instance?.MarkDirty();

        PublishBuildEvent(GameEvents.Build.OnObjectPlaced);
    }

    public override void Undo()
    {
        // 1. Remove auto-floor tiles first
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
                _money.RemoveHourlyCost(_autoFloorData.hourlyCost, FinanceCategory.ForHourlyCost(_autoFloorData.category), _autoFloorData.category);
            }
            foreach (var floor in _autoFloorDisabled)
                if (floor != null) floor.SetActive(true);
        }

        // 2. Remove placed grounds
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
            _money.RemoveHourlyCost(_data.hourlyCost, FinanceCategory.ForHourlyCost(_data.category), _data.category);
        }

        // 3. Re-enable displaced floors and reverse their refund
        bool revealedFloor = false;
        foreach (var floor in _disabledFloors)
        {
            if (floor != null) { floor.SetActive(true); revealedFloor = true; }
        }

        if (IsGround(_data) && _replacedGroundsCost > 0)
        {
            foreach (var obj in _disabledFloors)
            {
                if (obj == null) continue;
                var po = obj.GetComponent<PlacedObject>();
                if (po?.data == null) continue;
                _money.Deduct(po.data.cost, po.data.category);
                _money.AddHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
            }
        }

        // 4. Update stack heights
        foreach (var cell in _cells)
            foreach (var o in _offsets)
                _grid.UpdateStackPositions(cell + o);

        bool isGround = IsGround(_data);
        if (revealedFloor || _data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || isGround)
            NavMeshManager.Instance.MarkDirty();
    }

    public override void Redo()
    {
        // 1. Re-disable floors displaced by the original placement and re-apply their refund
        foreach (var floor in _disabledFloors)
            if (floor != null) floor.SetActive(false);

        if (IsGround(_data) && _replacedGroundsCost > 0)
        {
            foreach (var obj in _disabledFloors)
            {
                if (obj == null) continue;
                var po = obj.GetComponent<PlacedObject>();
                if (po?.data == null) continue;
                _money.Refund(po.data.cost, po.data.category);
                _money.RemoveHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
            }
        }

        // 2. Re-enable and re-add grounds
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
            _money.AddHourlyCost(_data.hourlyCost, FinanceCategory.ForHourlyCost(_data.category), _data.category);
        }

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
                _money.Deduct(_autoFloorData.cost, _autoFloorData.category);
                _money.AddHourlyCost(_autoFloorData.hourlyCost, FinanceCategory.ForHourlyCost(_autoFloorData.category), _autoFloorData.category);
            }
        }

        // 4. Update all stack heights
        foreach (var cell in _cells)
            foreach (var o in _offsets)
                _grid.UpdateStackPositions(cell + o);

        bool isGround2 = IsGround(_data);
        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || isGround2)
            NavMeshManager.Instance?.MarkDirty();

        PublishBuildEvent(GameEvents.Build.OnObjectPlaced);
    }
}
