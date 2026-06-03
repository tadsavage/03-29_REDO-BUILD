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

    // Active floor tiles (foundation's auto-floor or any upgrade tile) deleted along with the foundation
    private readonly List<GameObject> _linkedFloors = new();

    private bool _wasContaminated;

    private static bool IsFoundation(ObjDataSO d) => d != null && d.category == "Foundation";

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
        _data = bd.Data;
        _root = bd.RootCell;
        _offsets = bd.Offsets;
    }

    public void Execute()
    {
        if (_target == null)
            return;

        _reEnabledFloors.Clear();
        _linkedFloors.Clear();

        // 1. For foundations: remove the active floor tile(s) in the same cells first.
        //    This covers both the auto-placed default floor and any upgrade tiles.
        if (IsFoundation(_data))
        {
            foreach (var o in _offsets)
            {
                Vector2Int cell = _root + o;
                var cellObjs = _grid.GetObjectsInCell(cell);
                if (cellObjs == null) continue;

                for (int i = cellObjs.Count - 1; i >= 0; i--)
                {
                    var entry = cellObjs[i];
                    if (entry.data?.isFloor != true) continue;
                    if (entry.instance == null || !entry.instance.activeSelf) continue;

                    _linkedFloors.Add(entry.instance);
                    _grid.RemoveStackObject(cell, entry.instance, entry.data);
                    _money.Refund(entry.data.cost, entry.data.category);
                    _money.RemoveHourlyCost(entry.data.hourlyCost);
                    entry.instance.SetActive(false);
                }
            }
        }

        // 2. Remove foundation from grid; re-enable yard floor tiles that were hidden beneath it
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.RemoveStackObject(cell, _target, _data);

            if (!_grid.IsOccupied(cell))
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
        var contam = _target.GetComponent<ContaminationState>();
        _wasContaminated = contam != null && contam.IsContaminated;

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

            int totalRefund = adjustedCost;
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

        var effect = _target.GetComponent<BuildingDestructionEffect>();
        if (effect == null) effect = _target.AddComponent<BuildingDestructionEffect>();
        effect.Initialize(_duration, _sinkAmount, _vibrationAmount, _vibrationSpeed);

        // 5. NavMesh: always rebake when a foundation is deleted (floor surface changes)
        NavMeshManager.Instance?.MarkDirty();
    }

    public void Undo()
    {
        if (_target == null)
            return;

        // 0. Abort any ongoing destruction animation
        var effect = _target.GetComponent<BuildingDestructionEffect>();
        if (effect != null) effect.Abort();

        // 1. Re-enable foundation
        _target.SetActive(true);

        // 2. Add foundation back to grid; re-disable the yard tiles we had revealed
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.AddStackObject(cell, _target, _data);

            foreach (var floor in _reEnabledFloors)
                if (floor != null) floor.SetActive(false);

            _grid.UpdateStackPositions(cell);
        }
        _reEnabledFloors.Clear();

        // 3. Restore linked floor tiles (foundation's auto-floor / upgrade tiles)
        foreach (var floor in _linkedFloors)
        {
            if (floor == null) continue;
            var bd = floor.GetComponent<BuildingData>();
            if (bd == null) continue;

            floor.SetActive(true);
            foreach (var o in bd.Offsets)
                _grid.AddStackObject(bd.RootCell + o, floor, bd.Data);

            _grid.UpdateStackPositions(bd.RootCell);

            // Reverse the refund we issued in Execute()
            _money.Deduct(bd.Data.cost, bd.Data.category);
            _money.AddHourlyCost(bd.Data.hourlyCost);
        }

        // 4. Reverse foundation money
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

        // 5. Clear highlights
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
