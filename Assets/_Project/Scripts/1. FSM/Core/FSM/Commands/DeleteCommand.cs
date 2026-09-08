using GameCore.Build;
using GameCore.Economy;
using GameCore.Events;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DeleteCommand : PlacementCommandBase
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

    // A combined Foundation's own intact default tiles (FoundationFloorGroup) — refunded and pulled
    // from the grid exactly like _attachedFloors (grid membership doesn't come from Transform
    // parenting, so that part still needs doing explicitly), but WITHOUT their own sink/vibrate
    // BuildingDestructionEffect: they're real children of _target, so its own effect + the
    // SetActive(false) cascade already carries them visually for free. Kept separate from
    // _attachedFloors specifically to skip that redundant per-tile effect.
    private readonly List<GameObject> _ownedDefaultTiles = new();

    // Inactive floor tiles that were REPLACED on the foundation (the player dropped a custom tile
    // over the foundation's default tile — the default is disabled but stays in the grid at
    // foundation height). If left in the grid these get revealed as floating orphans when the
    // foundation is deleted. Cleared from the grid on Execute (no refund — already refunded at
    // swap time, and they stay inactive/invisible), re-added on Undo.
    private readonly List<GameObject> _hiddenFoundationFloors = new();

    // Floor tiles whose local Y is above this sit ON the foundation slab (default + any custom
    // replacements) rather than on the ground plane (the free yard tile). Used to tell the two
    // apart when deleting: slab tiles go with the foundation, the yard tile is revealed. Foundation
    // objHeight ≈ 1.06, so this lands ≈ 0.53 — well above the yard tile (~0.02).
    private readonly float _onFoundationY;

    private bool _wasContaminated;

    private static bool IsGround(ObjDataSO d) => d != null && (d.category == "Foundation" || d.category == "Grounds");

    public DeleteCommand(GameObject target, PlacementGrid grid, MoneyService money,
        float duration, float sinkAmount, float vibrationAmount, float vibrationSpeed)
        : base($"Delete {target?.GetComponent<BuildingData>()?.Data?.objName ?? "Object"}")
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
        _onFoundationY = (_data != null ? _data.objHeight : 1f) * 0.5f;

        // If deleting a foundation, collect every floor tile sitting ON its slab — these go with it.
        // Two kinds, split by whether they're currently visible:
        //   • ACTIVE tiles (the one visible tile per cell — default OR a custom pedestrian/lane the
        //     player dropped) → sink + refund (see _attachedFloors handling in Execute).
        //   • INACTIVE tiles (a default tile that a custom tile replaced — still in the grid at
        //     foundation height, just disabled) → clear from the grid silently (_hiddenFoundationFloors).
        //     These are the floating "ghost tile" orphans if left behind.
        // The free ground-plane yard tile is EXCLUDED here (its Y is ~0, below _onFoundationY): it was
        // only disabled when the foundation went down and must be REVEALED again, not deleted.
        if (IsGround(_data))
        {
            if (_offsets == null)
            {
                Debug.LogWarning($"[DeleteCommand] _offsets is null for {target.name}. Skipping attached floor detection.");
            }
            else
            {
                var floorGroup = target.GetComponent<FoundationFloorGroup>();
                HashSet<GameObject> seen = new HashSet<GameObject>();
                foreach (var o in _offsets)
                {
                    var cell = _root + o;
                    var objs = _grid.GetObjectsInCell(cell);
                    if (objs == null) continue;
                    foreach (var entry in objs)
                    {
                        if (entry.data == null || !entry.data.isFloor || entry.instance == null)
                            continue;

                        // Ground-plane yard tile — revealed, not deleted. Skip it.
                        if (entry.instance.transform.position.y <= _onFoundationY)
                            continue;

                        if (!seen.Add(entry.instance))
                            continue;

                        if (!entry.instance.activeSelf)
                        {
                            _hiddenFoundationFloors.Add(entry.instance); // replaced/hidden default → clear silently
                            continue;
                        }

                        // An intact default child of THIS foundation rides its own destruction effect
                        // and active-state cascade for free — route separately so Execute/Undo skip
                        // the redundant per-tile BuildingDestructionEffect (still refunded + removed
                        // from the grid explicitly, same as any other attached floor).
                        bool isOwnedDefault = floorGroup != null &&
                            floorGroup.Owns(entry.instance.GetComponent<PlacedObject>());
                        if (isOwnedDefault)
                            _ownedDefaultTiles.Add(entry.instance);
                        else
                            _attachedFloors.Add(entry.instance);          // visible slab tile → sink + refund
                    }
                }
            }
        }
    }

    public override void Execute()
    {
        if (_target == null)
            return;

        // If an operator is riding this equipment, vacate them BEFORE the destruction
        // animation disables the vehicle — they're parented under it, so without this they'd
        // be dragged into inactivity (or left at whatever height the anchor happened to put
        // them) instead of staying on the job, on foot, on the floor. They keep their role and
        // resume normal on-foot logic afterward: seek a worker waypoint if one exists, or wave
        // with the "no waypoint" alert if none do — the same as any other idle worker.
        var operatorSlot = _target.GetComponent<MHEOperatorSlot>();
        if (operatorSlot != null && operatorSlot.IsOccupied)
        {
            var vacated = operatorSlot.VacateOperator();
            if (vacated != null) SnapToFloorSurface(vacated.transform);
        }

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
                _money.RemoveHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
                _attachedFloorsRefundTotal += refund;
            }

            // Animation: floor tile sinks along with the foundation
            var effect = floor.GetComponent<BuildingDestructionEffect>();
            if (effect == null) effect = floor.AddComponent<BuildingDestructionEffect>();
            effect.Initialize(_duration, _sinkAmount, _vibrationAmount, _vibrationSpeed);
        }

        // 1a. Owned default tiles: refund + remove from grid explicitly (grid membership isn't tied
        //     to Transform parenting), but NO per-tile BuildingDestructionEffect — they're real
        //     children of _target, so its own effect below sinks them for free, and _target.SetActive
        //     (also below, via BuildingDestructionEffect's completion) cascades to deactivate them too.
        foreach (var floor in _ownedDefaultTiles)
        {
            if (floor == null) continue;
            var po = floor.GetComponent<PlacedObject>();
            if (po == null || po.data == null) continue;

            var bd = floor.GetComponent<BuildingData>();
            if (bd != null)
                foreach (var fo in bd.Offsets)
                    _grid.RemoveStackObject(bd.RootCell + fo, floor, po.data);
            else
                _grid.RemoveStackObject(_grid.WorldToCell(floor.transform.position), floor, po.data);

            if (!_wasContaminated)
            {
                int refund = Mathf.RoundToInt(po.data.cost * _money.SellBackRate);
                _money.Refund(refund, po.data.category);
                _money.RemoveHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
                _attachedFloorsRefundTotal += refund;
            }
        }

        // 1.5 Clear any REPLACED default tiles (disabled, still in the grid at foundation height)
        //     out of the grid so step 2/4 doesn't reveal them as floating orphans. They stay
        //     inactive/invisible; Undo re-adds them to the grid to restore the prior state.
        foreach (var floor in _hiddenFoundationFloors)
        {
            if (floor == null) continue;
            var po = floor.GetComponent<PlacedObject>();
            var data = po != null ? po.data : floor.GetComponent<BuildingData>()?.Data;
            var bd = floor.GetComponent<BuildingData>();
            if (bd != null)
            {
                foreach (var fo in bd.Offsets)
                    _grid.RemoveStackObject(bd.RootCell + fo, floor, data);
            }
            else
            {
                _grid.RemoveStackObject(_grid.WorldToCell(floor.transform.position), floor, data);
            }
        }

        // 2. Remove primary object from grid; for foundations only, queue any yard floor tiles
        //    that were hidden beneath it to be re-enabled once the destruction animation finishes
        //    (see step 4 — revealing them now would overlap the still-visible, still-sinking
        //    foundation/floor for the whole animation, producing floating/overlapping artifacts).
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
                        // Only the ground-plane yard tile is revealed. An elevated hidden tile would
                        // float where the foundation was — those are cleared in step 1.5 instead.
                        if (entry.instance.transform.position.y > _onFoundationY) continue;
                        if (!_reEnabledFloors.Contains(entry.instance))
                            _reEnabledFloors.Add(entry.instance);
                    }
                }
            }

            _grid.UpdateStackPositions(cell);
        }

        // 3. Money: refund foundation; skip if contaminated
        _money.RemoveHourlyCost(_data.hourlyCost, FinanceCategory.ForHourlyCost(_data.category), _data.category);

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
        foundationEffect.OnComplete = RevealHiddenFloors;

        // 5. NavMesh: always rebake when a foundation is deleted (floor surface changes)
        NavMeshManager.Instance?.MarkDirty();

        PublishBuildEvent(GameEvents.Build.OnObjectDeleted, _target.GetComponent<PlacedObject>());
    }

    // Snaps a freshly-vacated operator onto the real walkable surface beneath them (e.g. the
    // foundation top at Y=1.15) rather than leaving them at whatever world position the
    // vehicle's operator anchor happened to place them at — which can be inside the foundation
    // mesh. Mirrors AiNavigation.SnapToNavMeshSurface's upward-first search (prefer the
    // elevated floor surface over the ground plane below it).
    private static void SnapToFloorSurface(Transform t)
    {
        float[] yOffsets = { 0f, 0.5f, 1.0f, -0.5f, -1.0f };
        foreach (float offset in yOffsets)
        {
            Vector3 sample = new Vector3(t.position.x, t.position.y + offset, t.position.z);
            if (NavMesh.SamplePosition(sample, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
            {
                t.position = hit.position;
                var agent = t.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled)
                {
                    try { agent.Warp(hit.position); }
                    catch (System.Exception ex) { Debug.LogError($"[DeleteCommand] Failed to warp agent to floor surface: {ex.Message}"); }
                }
                return;
            }
        }
    }

    // Re-enables the yard tiles queued in step 2, called only once the foundation has finished
    // sinking out of view. If Undo() runs first, BuildingDestructionEffect.Abort() clears the
    // callback so this never fires on a delete that got reversed.
    private void RevealHiddenFloors()
    {
        foreach (var floor in _reEnabledFloors)
            if (floor != null) floor.SetActive(true);
    }

    public override void Undo()
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
                _money.AddHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
            }
        }

        // 3a. Re-add owned default tiles to the grid and reverse their refund. Explicit SetActive(true)
        //     here is redundant with _target's own reactivation cascade (they're its children) but
        //     harmless — keeps this block symmetric with _attachedFloors instead of relying on cascade
        //     timing relative to BuildingDestructionEffect.Abort() above.
        foreach (var floor in _ownedDefaultTiles)
        {
            if (floor == null) continue;
            floor.SetActive(true);

            var po = floor.GetComponent<PlacedObject>();
            if (po == null || po.data == null) continue;

            var bd = floor.GetComponent<BuildingData>();
            if (bd != null)
                foreach (var fo in bd.Offsets)
                    _grid.AddStackObject(bd.RootCell + fo, floor, po.data);
            else
                _grid.AddStackObject(_grid.WorldToCell(floor.transform.position), floor, po.data);

            if (!_wasContaminated)
            {
                int refund = Mathf.RoundToInt(po.data.cost * _money.SellBackRate);
                _money.Deduct(refund, po.data.category);
                _money.AddHourlyCost(po.data.hourlyCost, FinanceCategory.ForHourlyCost(po.data.category), po.data.category);
            }
        }

        // 3b. Re-add the replaced/hidden default tiles to the grid (they stay inactive — they were
        //     disabled when a custom tile was dropped over them; restoring them keeps the pre-delete
        //     state so a further undo of that custom placement can reveal them correctly).
        foreach (var floor in _hiddenFoundationFloors)
        {
            if (floor == null) continue;
            var po = floor.GetComponent<PlacedObject>();
            var data = po != null ? po.data : floor.GetComponent<BuildingData>()?.Data;
            var bd = floor.GetComponent<BuildingData>();
            if (bd != null)
            {
                foreach (var fo in bd.Offsets)
                    _grid.AddStackObject(bd.RootCell + fo, floor, data);
            }
            else
            {
                _grid.AddStackObject(_grid.WorldToCell(floor.transform.position), floor, data);
            }
        }

        // 4. Re-calculate positions for all affected cells
        foreach (var o in _offsets)
            _grid.UpdateStackPositions(_root + o);

        _reEnabledFloors.Clear();

        // 5. Reverse foundation money
        _money.AddHourlyCost(_data.hourlyCost, FinanceCategory.ForHourlyCost(_data.category), _data.category);

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

        PublishBuildEvent(GameEvents.Build.OnObjectPlaced, _target.GetComponent<PlacedObject>());
    }

    public override void Redo()
    {
        Execute();
    }
}
