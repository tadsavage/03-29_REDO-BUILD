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
    }

    public void Execute()
    {
        if (_target == null)
            return;

        _reEnabledFloors.Clear();

        // 1. Remove object from grid; for foundations only, re-enable any yard floor tiles
        //    that were hidden beneath it. Floor tiles themselves are NEVER deleted.
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

        // 2. Money: refund; skip if contaminated
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

        // 3. Reverse foundation money
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
