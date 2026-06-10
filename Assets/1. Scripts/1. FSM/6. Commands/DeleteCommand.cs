using System.Collections.Generic;
using UnityEngine;

public class DeleteCommand : ICommand
{
    private readonly PlacementGrid _grid;
    private readonly ObjDataSO _data;
    private readonly Vector2Int[] _offsets;
    private readonly Vector2Int _root;
    private readonly MoneyService _money;

    private readonly float _duration;
    private readonly float _sinkAmount;
    private readonly float _vibrationAmount;
    private readonly float _vibrationSpeed;

    private readonly GameObject _target;

    // Floors that were hidden under the foundation and get revealed when it's deleted
    private readonly List<GameObject> _reEnabledFloors = new();

    // Floor tiles sitting on top of the foundation that must be deleted with it
    private readonly List<GameObject> _attachedFloors = new();
    private int _attachedFloorsRefundTotal;

    private bool _wasContaminated;

    private static bool IsGround(ObjDataSO d) => d != null && (d.category == "Foundation" || d.category == "Grounds");

    public DeleteCommand(GameObject target, PlacementGrid grid, MoneyService money,
        float duration, float sinkAmount, float vibrationAmount, float vibrationSpeed)
    {
        _target = target;
        _grid = grid;
        _money = money;
        _duration = duration;
        _sinkAmount = sinkAmount;
        _vibrationAmount = vibrationAmount;
        _vibrationSpeed = vibrationSpeed;

        var bd = target.GetComponent<BuildingData>();
        if (bd == null)
            throw new System.ArgumentException($"[DeleteCommand] Target '{target.name}' has no BuildingData component.");
        _data = bd.Data;
        _root = bd.RootCell;
        _offsets = bd.Offsets;

        // If deleting a foundation, find any active floor tiles in its footprint.
        // These floor tiles must be deleted with the foundation (golden rule exception).
        if (IsGround(_data))
        {
            HashSet<GameObject> seen = new HashSet<GameObject>();
            foreach (var o in _offsets)
            {
                var cell = _root + o;
                var objs = _grid.GetObjectsInCell(cell);
                if (objs == null) continue;
                foreach (var entry in objs)
                {
                    if (entry.data != null && entry.data.isFloor && entry.instance != null && entry.instance.activeSelf)
                    {
                        if (seen.Add(entry.instance))
                            _attachedFloors.Add(entry.instance);
                    }
                }
            }
        }
    }

    public void Execute()
    {
        if (_target == null)
            return;

        _reEnabledFloors.Clear();
        _attachedFloorsRefundTotal = 0;

        // 0. Contamination status (drives refund for everything in this command)
        var contam = _target.GetComponent<ContaminationState>();
        _wasContaminated = contam != null && contam.IsContaminated;

        // 1. Remove attached floor tiles from grid and deactivate them
        foreach (var floor in _attachedFloors)
        {
            if (floor == null) continue;
            var po = floor.GetComponent<PlacedObject>();
            if (po == null || po.data == null) continue;

            // Remove from EVERY cell of the floor tile's footprint
            var bd = floor.GetComponent<BuildingData>();
            if (bd != null)
            {
                foreach (var fo in bd.Offsets)
                    _grid.RemoveStackObject(bd.RootCell + fo, floor, po.data);
            }
            else
            {
                _grid.RemoveStackObject(_grid.WorldToCell(floor.transform.position), floor, po.data);
            }

            // Money refund for floor tile (skip if foundation is contaminated)
            if (!_wasContaminated)
            {
                int refund = Mathf.RoundToInt(po.data.cost * _money.SellBackRate);
                _money.Refund(refund, po.data.category);
                _money.RemoveHourlyCost(po.data.hourlyCost);
                _attachedFloorsRefundTotal += refund;
            }

            // Animation: floor tile sinks along with the foundation
            var effect = floor.GetComponent<BuildingDestructionEffect>();
            if (effect == null) effect = floor.AddComponent<BuildingDestructionEffect>();
            effect.Initialize(_duration, _sinkAmount, _vibrationAmount, _vibrationSpeed);
        }

        // 2. Remove primary object from grid; for foundations only, re-enable any yard floor tiles
        //    that were hidden beneath it.
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.RemoveStackObject(cell, _target, _data);

            if (IsGround(_data) && !_grid.IsOccupied(cell))
            {
                var cellObjs = _grid.GetObjectsInCell(cell);
                if (cellObjs != null)
                {
                    foreach (var entry in cellObjs)
                    {
                        if (entry.data?.isFloor != true) continue;
                        if (entry.instance == null || entry.instance.activeSelf) continue;
                        entry.instance.SetActive(true);
                        if (!_reEnabledFloors.Contains(entry.instance))
                            _reEnabledFloors.Add(entry.instance);
                    }
                }
            }

            _grid.UpdateStackPositions(cell);
        }

        // 3. Money: refund foundation; skip if contaminated
        _money.RemoveHourlyCost(_data.hourlyCost);

        var pb = _target.GetComponent<PalletBuilder>();

        if (_wasContaminated)
        {
            FloatingMoneyText.Show(_target.transform.position + Vector3.up * 1f, 0);
        }
        else
        {
            int adjustedCost = Mathf.RoundToInt(_data.cost * _money.SellBackRate);
            int adjustedLoad = pb != null ? Mathf.RoundToInt(pb.CurrentLoadCost * _money.SellBackRate) : 0;

            _money.Refund(adjustedCost, _data.category);

            int totalRefund = adjustedCost + _attachedFloorsRefundTotal;
            if (pb != null && pb.CurrentLoadCost > 0)
            {
                _money.Refund(adjustedLoad, "Inventory");
                totalRefund += adjustedLoad;
            }

            if (totalRefund != 0)
                FloatingMoneyText.Show(_target.transform.position + Vector3.up * 1.5f, totalRefund);
        }

        // 4. Destruction animation
        var highlighter = _target.GetComponent<BuildingHighlighter>();
        if (highlighter != null)
            highlighter.HighlightDelete(false);

        var foundationEffect = _target.GetComponent<BuildingDestructionEffect>();
        if (foundationEffect == null) foundationEffect = _target.AddComponent<BuildingDestructionEffect>();
        foundationEffect.Initialize(_duration, _sinkAmount, _vibrationAmount, _vibrationSpeed);

        // 5. NavMesh: always rebake when a foundation is deleted (floor surface changes)
        NavMeshManager.Instance?.MarkDirty();
    }

    public void Undo()
    {
        if (_target == null)
            return;

        // 0. Abort any ongoing destruction animations
        var foundationEffect = _target.GetComponent<BuildingDestructionEffect>();
        if (foundationEffect != null) foundationEffect.Abort();

        foreach (var floor in _attachedFloors)
        {
            if (floor == null) continue;
            var effect = floor.GetComponent<BuildingDestructionEffect>();
            if (effect != null) effect.Abort();
        }

        // 1. Re-enable foundation and attached floors
        _target.SetActive(true);
        foreach (var floor in _attachedFloors)
            if (floor != null) floor.SetActive(true);

        // 2. Add foundation back to grid; re-disable the yard tiles we had revealed
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.AddStackObject(cell, _target, _data);

            foreach (var floor in _reEnabledFloors)
                if (floor != null) floor.SetActive(false);
        }

        // 3. Add attached floor tiles back to grid
        foreach (var floor in _attachedFloors)
        {
            if (floor == null) continue;
            var po = floor.GetComponent<PlacedObject>();
            if (po == null || po.data == null) continue;

            var bd = floor.GetComponent<BuildingData>();
            if (bd != null)
            {
                foreach (var fo in bd.Offsets)
                    _grid.AddStackObject(bd.RootCell + fo, floor, po.data);
            }
            else
            {
                _grid.AddStackObject(_grid.WorldToCell(floor.transform.position), floor, po.data);
            }

            // Reverse money refund for floor tile
            if (!_wasContaminated)
            {
                int refund = Mathf.RoundToInt(po.data.cost * _money.SellBackRate);
                _money.Deduct(refund, po.data.category);
                _money.AddHourlyCost(po.data.hourlyCost);
            }
        }

        // 4. Re-calculate positions for all affected cells
        foreach (var o in _offsets)
            _grid.UpdateStackPositions(_root + o);

        _reEnabledFloors.Clear();

        // 5. Reverse foundation money
        _money.AddHourlyCost(_data.hourlyCost);

        if (!_wasContaminated)
        {
            var pb = _target.GetComponent<PalletBuilder>();
            int adjustedCost = Mathf.RoundToInt(_data.cost * _money.SellBackRate);
            int adjustedLoad = pb != null ? Mathf.RoundToInt(pb.CurrentLoadCost * _money.SellBackRate) : 0;

            _money.Deduct(adjustedCost, _data.category);

            if (pb != null && pb.CurrentLoadCost > 0)
                _money.Deduct(adjustedLoad, "Inventory");
        }

        // 6. Clear highlights
        var highlighter = _target.GetComponent<BuildingHighlighter>();
        if (highlighter != null)
            highlighter.HighlightDelete(false);

        NavMeshManager.Instance?.MarkDirty();
    }

    public void Redo()
    {
        Execute();
    }
}
