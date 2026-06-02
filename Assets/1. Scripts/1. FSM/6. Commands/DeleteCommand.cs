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
    private readonly List<GameObject> _reEnabledFloors = new();

    // Remember whether the product was spoiled at delete time, so Undo mirrors the
    // money path exactly (spoiled = no refund on delete, so no re-deduct on undo).
    private bool _wasContaminated;

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

        // 1. Remove from grid and check for floors to re-enable
        foreach (var o in _offsets)
        {
            Vector2Int cell = _root + o;
            _grid.RemoveStackObject(cell, _target, _data);

            // If the cell is no longer occupied (by buildings), re-enable any floors there
            if (!_grid.IsOccupied(cell))
            {
                var cellObjs = _grid.GetObjectsInCell(cell);
                if (cellObjs != null)
                {
                    foreach (var entry in cellObjs)
                    {
                        if (entry.data != null && entry.data.isFloor && entry.instance != null && !entry.instance.activeSelf)
                        {
                            entry.instance.SetActive(true);
                            if (!_reEnabledFloors.Contains(entry.instance))
                                _reEnabledFloors.Add(entry.instance);
                            }
                            }
                            }
                            }
                            _grid.UpdateStackPositions(cell);
                            }

        // 2. Remove from registry is now handled automatically by _target.SetActive(false) -> PlacedObject.OnDisable()

        // 3. Refund money — UNLESS the product spoiled. Spoiled product is a total
        //    loss: refund $0 and float a green "$0" instead of the normal refund.
        var contam = _target.GetComponent<ContaminationState>();
        _wasContaminated = contam != null && contam.IsContaminated;

        _money.RemoveHourlyCost(_data.hourlyCost);

        var pb = _target.GetComponent<PalletBuilder>();

        if (_wasContaminated)
        {
            // No money back. Green "$0" floats up ~1m from inside the cases (Mario-coin style).
            FloatingMoneyText.Show(_target.transform.position + Vector3.up * 1f, 0);
        }
        else
        {
            _money.Refund(_data.cost, _data.category);

            int totalRefund = _data.cost;
            if (pb != null && pb.CurrentLoadCost > 0)
            {
                _money.Refund(pb.CurrentLoadCost, "Inventory");
                totalRefund += pb.CurrentLoadCost;
            }

            // Floating "$" UX — green positive number as money returns to capital.
            if (totalRefund != 0)
                FloatingMoneyText.Show(_target.transform.position + Vector3.up * 1.5f, totalRefund);
        }

        // 4. Start Destruction Animation
var highlighter = _target.GetComponent<BuildingHighlighter>();
        if (highlighter != null)
            highlighter.HighlightDelete(false);

        var effect = _target.GetComponent<BuildingDestructionEffect>();
        if (effect == null) effect = _target.AddComponent<BuildingDestructionEffect>();
        effect.Initialize(_duration, _sinkAmount, _vibrationAmount, _vibrationSpeed);

        if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules || _reEnabledFloors.Count > 0)
        {
            NavMeshManager.Instance.MarkDirty();
        }

        }
        public void Undo()
        {
            if (_target == null)
                return;

            // 0. Abort any ongoing destruction animation
            var effect = _target.GetComponent<BuildingDestructionEffect>();
            if (effect != null) effect.Abort();

            // 1. Enable object first so UpdateStackPositions sees it as active
            _target.SetActive(true);

            // 2. Add back to grid
            foreach (var o in _offsets)
            {
                Vector2Int cell = _root + o;
                _grid.AddStackObject(cell, _target, _data);

                // 3. Re-disable floors we re-enabled during deletion
                foreach (var floor in _reEnabledFloors)
                {
                    if (floor != null)
                        floor.SetActive(false);
                }

                // 4. Update again because we changed floor visibility
                _grid.UpdateStackPositions(cell);
            }

            _reEnabledFloors.Clear();

            // 5. Deduct money (un-refund) — but only mirror what Execute actually
            //    refunded. Spoiled product was refunded $0, so don't re-charge it.
            _money.AddHourlyCost(_data.hourlyCost);

            if (!_wasContaminated)
            {
                _money.Deduct(_data.cost, _data.category);

                var pb = _target.GetComponent<PalletBuilder>();
                if (pb != null && pb.CurrentLoadCost > 0)
                {
                    _money.Deduct(pb.CurrentLoadCost, "Inventory");
                }
            }

            // 6. Ensure any highlights are cleared
var highlighter = _target.GetComponent<BuildingHighlighter>();
            if (highlighter != null)
                highlighter.HighlightDelete(false);

            if (_data.isFloor || _data.pathfindingClear || _data.ignorePlacementRules)
            {
                NavMeshManager.Instance.MarkDirty();
            }
        }

        public void Redo()
        {
            Execute();
        }
        }
