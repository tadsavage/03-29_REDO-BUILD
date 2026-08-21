using GameCore.Build;
using GameCore.Economy;
using GameCore.Events;
using System.Collections.Generic;
using UnityEngine;

public class PlaceCommand : PlacementCommandBase
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

    // Full refund value credited when a ground replaces another ground (including its floor tiles)
    private int _replacedGroundsCost;

    // Walls removed when a door is placed over them (straight walls with canBeReplacedByDoor)
    private readonly List<GameObject> _replacedWalls = new();
    private int _wallRefundTotal; // total sell-back-adjusted refund credited for those walls

    private static bool IsGround(ObjDataSO d) =>
        d != null && (d.category == "Foundation" || d.category == "Grounds");

    public PlaceCommand(PlacementGrid grid, PlacementFinalizer finalizer,
        Vector2Int root, Vector2Int[] offsets, ObjDataSO data,
        float rotation, MoneyService money)
        : base($"Place {data?.objName ?? "Object"}")
    {
        _grid = grid;
        _finalizer = finalizer;
        _root = root;
        _offsets = offsets;
        _data = data;
        _rotation = rotation;
        _money = money;
    }

    public override void Execute()
    {
        if (_instance == null)
        {
            _instance = _finalizer.FinalizePlacement(_root, _offsets, _data, _rotation, _disabledFloors);
        }

        if (_instance == null) return;

        _instance.SetActive(true);

        // Fire RackPlacedEvent if this is a racking object
        if (_data != null && _data.category == "Racking")
        {
            RackPlacedEvent.Fire(_instance);
        }

        // --- Door↔Wall mutual replacement ---
        // Doors (replacesWalls) remove overlapping walls (canBeReplacedByDoor).
        // Walls (canBeReplacedByDoor) remove overlapping doors (replacesWalls) — entire
        // door footprint is removed even if only one cell of the wall overlaps the door.
        // First Execute only; Redo re-uses the cached _replacedWalls list.
        if ((_data.replacesWalls || _data.canBeReplacedByDoor) && _replacedWalls.Count == 0)
        {
            var seen = new HashSet<GameObject>();
            _wallRefundTotal = 0;

            foreach (var o in _offsets)
            {
                var cellObjects = _grid.GetObjectsInCell(_root + o);
                if (cellObjects == null) continue;

                foreach (var placed in cellObjects.ToArray())
                {
                    if (placed.instance == null || placed.data == null) continue;

                    bool shouldReplace = (_data.replacesWalls  && placed.data.canBeReplacedByDoor)
                                     || (_data.canBeReplacedByDoor && placed.data.replacesWalls);
                    if (!shouldReplace) continue;
                    if (!seen.Add(placed.instance)) continue;

                    // Remove from EVERY cell of the replaced object's footprint.
                    // Critical for the wall→door case: a 1×1 wall must remove the
                    // entire 3×1 or 5×1 door, not just the one overlapping cell.
                    var bd = placed.instance.GetComponent<BuildingData>();
                    if (bd != null)
                        foreach (var wo in bd.Offsets)
                            _grid.RemoveStackObject(bd.RootCell + wo, placed.instance, placed.data);
                    else
                        _grid.RemoveStackObject(_root + o, placed.instance, placed.data);

                    placed.instance.SetActive(false);
                    _replacedWalls.Add(placed.instance);

                    int refund = Mathf.RoundToInt(placed.data.cost * _money.SellBackRate);
                    _money.Refund(refund, placed.data.category);
                    _money.RemoveHourlyCost(placed.data.hourlyCost, FinanceCategory.ForHourlyCost(placed.data.category), placed.data.category);
                    _wallRefundTotal += refund;
                }
            }
        }

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
                _money.RemoveHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
            }
        }

        // For ground placements: refund the displaced ground + any floor tiles that were on it.
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

        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost, FinanceCategory.ForHourlyCost(_data.category), _data.category);

        // --- Auto-floor for foundations/grounds ---
        // Place one floor tile per footprint cell so the entire slab is covered.
        //
        // A combined prefab (FoundationFloorGroup present) already ships its 4 default tiles as
        // real children — PlacementFinalizer just initialized/registered them at the correct cells
        // above. Spawning 4 MORE here would double the tiles and double-charge for them, so this
        // discovers the pre-existing children instead of calling FinalizePlacement again. A plain
        // Foundation (no group) keeps the original spawn-4-new-instances behavior unchanged.
        var floorGroup = _instance.GetComponent<FoundationFloorGroup>();
        if (_data.defaultFloorTile != null && _autoFloors.Count == 0)
        {
            _autoFloorData = _data.defaultFloorTile;
            _autoFloorTotalCost = 0;

            if (floorGroup != null && floorGroup.DefaultTiles != null)
            {
                foreach (var tilePO in floorGroup.DefaultTiles)
                {
                    if (tilePO == null) continue;
                    _autoFloors.Add(tilePO.gameObject);
                    _autoFloorTotalCost += _autoFloorData.cost;
                    _money.Deduct(_autoFloorData.cost, _autoFloorData.category);
                    _money.AddHourlyCost(_autoFloorData.hourlyCost, FinanceCategory.ForHourlyCost(_autoFloorData.category), _autoFloorData.category);
                }
            }
            else
            {
                Vector2Int[] tileOffsets = _autoFloorData.GetFootprintOffsets(0f); // always 1×1
                foreach (var o in _offsets)
                {
                    Vector2Int cellRoot = _root + o;
                    var tile = _finalizer.FinalizePlacement(cellRoot, tileOffsets, _autoFloorData, 0f, _autoFloorDisabled);
                    if (tile == null) continue;
                    tile.SetActive(true);
                    _autoFloors.Add(tile);
                    _autoFloorTotalCost += _autoFloorData.cost;
                    _money.Deduct(_autoFloorData.cost, _autoFloorData.category);
                    _money.AddHourlyCost(_autoFloorData.hourlyCost, FinanceCategory.ForHourlyCost(_autoFloorData.category), _autoFloorData.category);
                }
            }
        }

        // Floating money: show net cost after all replacements and auto-floors.
        if (_data.isFloor)
        {
            int netCost = _data.cost - _replacedFloorsCost;
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -netCost);
        }
        else if ((_data.replacesWalls || _data.canBeReplacedByDoor) && _wallRefundTotal > 0)
        {
            int netCost = _data.cost - _wallRefundTotal;
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -netCost);
        }
        else if (IsGround(_data))
        {
            // Net = new ground + auto-floor tiles - refund for old ground and its tiles
            int netCost = _data.cost + _autoFloorTotalCost - _replacedGroundsCost;
            if (netCost != 0)
                FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -netCost);
        }
        else if (_data.cost != 0)
        {
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -_data.cost);
        }

        // Force height recalculation for all footprint cells
        foreach (var o in _offsets)
            _grid.UpdateStackPositions(_root + o);

        // NavMesh: foundations, grounds, floors, pathfinding-clear, stairs all need a bake
        if (NeedsNavMesh(_data) || _disabledFloors.Count > 0)
            NavMeshManager.Instance?.MarkDirty();

        PublishBuildEvent(GameEvents.Build.OnObjectPlaced, _instance.GetComponent<PlacedObject>());
    }

    public override void Undo()
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
                _money.RemoveHourlyCost(_autoFloorData.hourlyCost, FinanceCategory.ForHourlyCost(_autoFloorData.category), _autoFloorData.category);
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
                _money.AddHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
            }
        }

        // Reverse ground replacement refunds (re-charge displaced grounds and their floor tiles)
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

        _money.Refund(_data.cost, _data.category);
        _money.RemoveHourlyCost(_data.hourlyCost, FinanceCategory.ForHourlyCost(_data.category), _data.category);

        // Restore walls that were replaced by this door
        if (_replacedWalls.Count > 0)
        {
            foreach (var wall in _replacedWalls)
            {
                if (wall == null) continue;
                var po = wall.GetComponent<PlacedObject>();
                if (po == null) continue;

                wall.SetActive(true);

                var bd = wall.GetComponent<BuildingData>();
                if (bd != null)
                    foreach (var wo in bd.Offsets)
                        _grid.AddStackObject(bd.RootCell + wo, wall, po.data);

                // Reverse the sell-back refund we gave for this wall
                int refund = Mathf.RoundToInt(po.data.cost * _money.SellBackRate);
                _money.Deduct(refund, po.data.category);
                _money.AddHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
            }
        }

        // Re-enable displaced floors
        bool revealedFloor = false;
        foreach (var floor in _disabledFloors)
            if (floor != null) { floor.SetActive(true); revealedFloor = true; }

        foreach (var o in _offsets)
            _grid.UpdateStackPositions(_root + o);

        if (NeedsNavMesh(_data) || revealedFloor)
            NavMeshManager.Instance?.MarkDirty();
    }

    public override void Redo()
    {
        if (_instance == null) return;

        // 0. Re-disable walls replaced by this door and re-credit their sell-back value
        if (_replacedWalls.Count > 0)
        {
            foreach (var wall in _replacedWalls)
            {
                if (wall == null) continue;
                var po = wall.GetComponent<PlacedObject>();
                if (po == null) continue;

                var bd = wall.GetComponent<BuildingData>();
                if (bd != null)
                    foreach (var wo in bd.Offsets)
                        _grid.RemoveStackObject(bd.RootCell + wo, wall, po.data);

                wall.SetActive(false);

                int refund = Mathf.RoundToInt(po.data.cost * _money.SellBackRate);
                _money.Refund(refund, po.data.category);
                _money.RemoveHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
            }
        }

        // 1. Re-disable floors replaced by primary object
        foreach (var floor in _disabledFloors)
            if (floor != null) floor.SetActive(false);

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
                _money.RemoveHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
            }
        }

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

        _money.Deduct(_data.cost, _data.category);
        _money.AddHourlyCost(_data.hourlyCost, FinanceCategory.ForHourlyCost(_data.category), _data.category);

        if (_autoFloors.Count > 0 && _autoFloorData != null)
        {
            foreach (var tile in _autoFloors)
            {
                if (tile == null) continue;
                _money.Deduct(_autoFloorData.cost, _autoFloorData.category);
                _money.AddHourlyCost(_autoFloorData.hourlyCost, FinanceCategory.ForHourlyCost(_autoFloorData.category), _autoFloorData.category);
            }
        }

        if (_data.isFloor)
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -(_data.cost - _replacedFloorsCost));
        else if ((_data.replacesWalls || _data.canBeReplacedByDoor) && _wallRefundTotal > 0)
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -(_data.cost - _wallRefundTotal));
        else if (IsGround(_data))
        {
            int netCost = _data.cost + _autoFloorTotalCost - _replacedGroundsCost;
            if (netCost != 0)
                FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -netCost);
        }
        else if (_data.cost != 0)
            FloatingMoneyText.Show(_instance.transform.position + Vector3.up * 1.5f, -_data.cost);

        if (NeedsNavMesh(_data))
            NavMeshManager.Instance?.MarkDirty();

        PublishBuildEvent(GameEvents.Build.OnObjectPlaced, _instance.GetComponent<PlacedObject>());
    }

    private static bool NeedsNavMesh(ObjDataSO d) =>
        d.isFloor || d.pathfindingClear || d.ignorePlacementRules || d.CanUseStairs || IsGround(d);
}
