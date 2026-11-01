using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.Labor
{
    /// <summary>
    /// DIGGING OUT A BURIED TRAILER (Tad, 2026-10-31).
    ///
    /// A trailer is waiting at its door and one of its order's staged pallets sits under, or behind, something that is NOT
    /// going on this trailer: another order's pallet, or received stock that has not been put away yet. Before this the loader
    /// just stopped ("stopping before it") and the order went late, because nobody owned the job of getting that pallet out of
    /// the way.
    ///
    /// Now the scan below files a <see cref="WorkTaskType.Dig"/> task (priority 900, the top of the range) for every lane in
    /// that state and a dock stocker works it BEFORE any ordinary load pass:
    ///   1. Take the front pallet of the lane when it is a blocker (door end, top of the stack first).
    ///   2. Carry it round to the far end of a lane that is empty and belongs to nobody, and set it down.
    ///   3. Repeat until the first pallet in the lane is one for this trailer. The normal Load task takes it from there.
    /// Received stock that is in the way also gets its putaway raised to the top (<see cref="BlockingStockPalletIds"/>) so a
    /// reach truck takes it out too and does nothing else first. The two vehicles never fight over a pallet: the dig plan stops
    /// at any pallet somebody has already been assigned to move.
    /// </summary>
    public partial class TrailerLoadController
    {
        /// <summary>Priority of a dig-out job. The Work Queue's steppers top out at 900.</summary>
        public const int DigPriority = 900;

        /// <summary>Received stock that stands in the way of a waiting trailer's pallets. A reach truck takes these putaways first.</summary>
        public static readonly HashSet<string> BlockingStockPalletIds = new HashSet<string>();

        private readonly HashSet<string> _digLanes = new HashSet<string>();
        private readonly Dictionary<string, float> _blockerRetryAt = new Dictionary<string, float>();
        private float _nextBlockerScan;
        private const float BlockerScanSeconds = 2f;

        private class BlockedLane
        {
            public int Door;
            public string Lane;
            public int DockDoor;
            public string AppointmentId;
            public HashSet<string> OrderIds;
            public string OrderLabel;
            public int Blockers;
        }
        private readonly List<BlockedLane> _blocked = new List<BlockedLane>();

        // ── Scan + claim ─────────────────────────────────────────────────────────────────────────

        /// <summary>Returns true when a dig-out was started this poll.</summary>
        private bool TryStartBlockerDig(WorkQueueSystem queue)
        {
            if (Time.unscaledTime >= _nextBlockerScan)
            {
                _nextBlockerScan = Time.unscaledTime + BlockerScanSeconds;
                ScanForBlockedLanes(queue);
            }

            var task = queue.Tasks.FirstOrDefault(t => t.Type == WorkTaskType.Dig && t.Status == WorkTaskStatus.Available);
            if (task == null) return false;
            var info = _blocked.FirstOrDefault(b => b.Door + b.Lane == task.FromLocation);
            if (info == null) { task.Status = WorkTaskStatus.Cancelled; return false; }
            if (_blockerRetryAt.TryGetValue(task.PalletId, out float retry) && Time.time < retry) return false;

            var slot = FindAvailableDockStocker();
            if (slot == null) return false;

            string opGuid = slot.CurrentOperator?.Record?.employeeGuid;
            if (!queue.TryClaimSpecificTask(task, opGuid)) return false;

            _digLanes.Add(task.FromLocation);
            Debug.Log($"[TrailerLoad][BLOCKERS] {slot.name} digging out {info.Door}{info.Lane} ({info.Blockers} pallet(s) in the way of {info.OrderLabel}).");
            StartCoroutine(BlockerDigRoutine(slot, task, info));
            return true;
        }

        private void ScanForBlockedLanes(WorkQueueSystem queue)
        {
            _blocked.Clear();
            BlockingStockPalletIds.Clear();

            if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null ||
                !ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null ||
                !ServiceLocator.TryGet<DockScheduleService>(out var sched) || sched == null)
                return;

            var trucks = TrucksThisFrame().Where(t => t != null && t.IsOutbound && t.AwaitingLoad && t.DockedAt != null &&
                                                      !string.IsNullOrEmpty(t.OutboundAppointmentId)).ToList();
            var confirmed = new HashSet<string>();

            if (trucks.Count > 0)
            {
                var outbound = FindObjectsByType<OutboundPalletBuilder>().Where(p => p != null && p.IsOnFloor && !string.IsNullOrEmpty(p.OrderId)).ToList();

                foreach (var truck in trucks)
                {
                    var appt = sched.FindById(truck.OutboundAppointmentId);
                    if (appt == null || appt.OrderIds == null) continue;
                    var ids = new HashSet<string>(orders.ActiveOrders
                        .Where(o => o != null && appt.OrderIds.Contains(o.OrderId) &&
                                    o.Status != OrderData.OrderStatus.Shipped && o.Status != OrderData.OrderStatus.Cancelled)
                        .Select(o => o.OrderId));
                    if (ids.Count == 0) continue;

                    // The lanes where this trailer's staged pallets stand.
                    var lanes = new HashSet<(int door, string lane)>();
                    foreach (var p in outbound)
                    {
                        if (!ids.Contains(p.OrderId) || p.Stage != OutboundPalletBuilder.PalletStage.Staged) continue;
                        if (LaneNamingService.TryGetSlot(_grid.WorldToCell(p.transform.position), out var s)) lanes.Add((s.DoorNumber, s.Lane));
                    }

                    foreach (var (door, lane) in lanes)
                    {
                        string key = $"{door}{lane}";
                        if (_digLanes.Contains(key)) { confirmed.Add(key); continue; }
                        // A load pass is already working this lane.
                        if (queue.Tasks.Any(t => t.Type == WorkTaskType.Load && t.Status == WorkTaskStatus.Assigned && t.FromLocation == key)) continue;

                        var plan = PlanBlockers(inv, queue, outbound, door, lane, ids, out string stopWhy);
                        if (plan.Count == 0) continue;

                        // The far end may already give the loader every pallet: then nothing needs digging.
                        int own = outbound.Count(p => ids.Contains(p.OrderId) && p.Stage == OutboundPalletBuilder.PalletStage.Staged &&
                                                      LaneNamingService.TryGetSlot(_grid.WorldToCell(p.transform.position), out var ls) &&
                                                      ls.DoorNumber == door && ls.Lane == lane);
                        var reachable = FindStagedPalletsInLane(door, lane, truck.DockedAt.DoorNumber, truck.OutboundAppointmentId, out _);
                        if (reachable.Count >= own) continue;

                        foreach (var t in plan)
                        {
                            string id = t.GetComponent<PalletMasterLink>()?.PalletId;
                            if (!string.IsNullOrEmpty(id) && t.GetComponent<OutboundPalletBuilder>() == null) BlockingStockPalletIds.Add(id);
                        }

                        confirmed.Add(key);
                        string label = string.Join(", ", orders.ActiveOrders.Where(o => ids.Contains(o.OrderId)).Select(o => o.OrderNumber));
                        _blocked.Add(new BlockedLane { Door = door, Lane = lane, DockDoor = truck.DockedAt.DoorNumber,
                            AppointmentId = truck.OutboundAppointmentId, OrderIds = ids, OrderLabel = label, Blockers = plan.Count });

                        // One live Dig task per lane.
                        string taskKey = "DIG:" + key;
                        if (!queue.Tasks.Any(t => t.Type == WorkTaskType.Dig && t.PalletId == taskKey &&
                                                  (t.Status == WorkTaskStatus.Available || t.Status == WorkTaskStatus.Assigned)))
                        {
                            var task = queue.CreateTask(WorkTaskType.Dig, EmployeeRole.DockStockerOperator, taskKey,
                                $"Dig out lane {key}: move {plan.Count} pallet(s) out of the way of {label}",
                                key, truck.DockedAt.DoorNumber.ToString(), PalletData.AreaCategory.Grocery, DigPriority);
                            if (task != null)
                                SystemsLogWindow.Log($"Lane {key} has {plan.Count} pallet(s) in front of {label}'s load - a dock stocker will dig them out first.");
                        }
                    }
                }
            }

            // A dig task whose lane is no longer blocked (somebody moved the pallets, the trailer left) has nothing to do.
            foreach (var t in queue.Tasks)
                if (t.Type == WorkTaskType.Dig && t.Status == WorkTaskStatus.Available && !confirmed.Contains(t.FromLocation))
                    t.Status = WorkTaskStatus.Cancelled;
        }

        /// <summary>
        /// The pallets that must leave the lane, in the order a vehicle meets them from the door: the run of pallets that are NOT
        /// for these orders and stand in front of (or on top of) one that is. Empty when the first pallet met is the trailer's own
        /// (the ordinary load pass takes it) or when none of the trailer's pallets lies behind anything.
        /// </summary>
        private List<Transform> PlanBlockers(InventoryService inv, WorkQueueSystem queue, List<OutboundPalletBuilder> outbound,
                                             int door, string lane, HashSet<string> orderIds, out string why)
        {
            why = null;
            var run = new List<Transform>();
            foreach (var s in LaneNamingService.GetLane(door, lane))   // door end first
            {
                var inCell = new List<Transform>();
                foreach (var p in outbound)
                    if (_grid.WorldToCell(p.transform.position) == s.Cell) inCell.Add(p.transform);
                foreach (var t in PhysicalStack(inv, s.Cell))
                    if (!inCell.Contains(t)) inCell.Add(t);
                inCell.Sort((a, b) => b.position.y.CompareTo(a.position.y));   // top first

                foreach (var t in inCell)
                {
                    var ob = t.GetComponent<OutboundPalletBuilder>();
                    bool isOwn = ob != null && orderIds.Contains(ob.OrderId) && ob.Stage == OutboundPalletBuilder.PalletStage.Staged;
                    if (isOwn) return run;   // reached the trailer's own pallet: everything before it is the blocker run

                    if (ob != null && OutboundPalletBuilder.LoadClaimed.Contains(ob)) { why = "a pallet in the way is being loaded onto another trailer"; return new List<Transform>(); }
                    string id = t.GetComponent<PalletMasterLink>()?.PalletId;
                    if (!string.IsNullOrEmpty(id) && queue.Tasks.Any(x => x.PalletId == id && x.Status == WorkTaskStatus.Assigned && x.Type != WorkTaskType.Dig))
                    { why = $"another vehicle is already working pallet {id}"; return new List<Transform>(); }
                    run.Add(t);
                    if (run.Count > MaxBlockersPerRun) { why = "too many pallets in the way"; return new List<Transform>(); }
                }
            }
            return new List<Transform>();   // ran out of lane without meeting an own pallet: nothing is buried
        }

        // ── The routine ──────────────────────────────────────────────────────────────────────────

        private IEnumerator BlockerDigRoutine(MHEOperatorSlot slot, WorkTask task, BlockedLane info)
        {
            DockEquipmentCommandeerRegistry.Commandeer(slot);

            Transform ds = slot.transform;
            Vector3 homePos = ds.position;
            var nav = ds.GetComponent<AiNavigation>();
            var agent = ds.GetComponent<NavMeshAgent>();
            bool navWas = nav != null && nav.enabled;
            bool agentWas = agent != null && agent.enabled;
            if (agent != null) agent.enabled = false;
            if (nav != null) nav.enabled = false;

            Transform forks = FindDeepChild(ds, ForkChildName);
            float forkRestY = forks != null ? forks.localPosition.y : 0f;

            ServiceLocator.TryGet<InventoryService>(out var inv);
            ServiceLocator.TryGet<WorkQueueSystem>(out var queue);
            string key = task.FromLocation;
            Vector3 srcDoorPos = DoorPosition(info.Door, ds.position);
            var battery = slot.GetComponent<EquipmentBattery>();

            bool ok = true;
            string failWhy = null;
            int movedCount = 0;

            for (int moved = 0; ok && moved <= MaxBlockersPerRun; moved++)
            {
                if (ds == null || inv == null || queue == null) { ok = false; failWhy = "equipment gone"; break; }
                if (battery != null && battery.NeedsCharge) { ok = false; failWhy = "battery low"; break; }

                var outbound = FindObjectsByType<OutboundPalletBuilder>().Where(p => p != null && p.IsOnFloor && !string.IsNullOrEmpty(p.OrderId)).ToList();
                var blockers = PlanBlockers(inv, queue, outbound, info.Door, info.Lane, info.OrderIds, out failWhy);
                if (blockers.Count == 0) break;   // the trailer's own pallet is first: done

                if (!TryFindDepositSlot(inv, queue, info.Door, info.Lane, out int depDoor, out string depLane, out var depCell))
                { ok = false; failWhy = "no empty lane to put the pallets in the way"; break; }

                var blocker = blockers[0];
                string blockerId = blocker.GetComponent<PalletMasterLink>()?.PalletId;
                var ob = blocker.GetComponent<OutboundPalletBuilder>();
                string blockerOrder = ob != null ? ob.OrderId : null;

                var carry = new CarryState();
                yield return PickUpFromLaneEntry(ds, forks, forkRestY, blocker, info.Door, info.Lane, srcDoorPos, inv, carry);
                if (!carry.Ok) { ok = false; failWhy = "couldn't pick up the pallet in the way"; break; }

                bool reached = false;
                LaneNamingService.TryGetLaneGeometry(depDoor, depLane, out var depGeo);
                yield return SeekViaNavMesh(ds, nav, agent, depGeo.ExitPoint, $"blockers: to {depDoor}{depLane} far end", r => reached = r);

                bool dropped = false;
                if (reached)
                    yield return SetDownFromFarEnd(ds, forks, forkRestY, blocker, depDoor, depLane, depCell, inv, carry, r => dropped = r);

                if (!dropped)
                {
                    if (reached)
                    {
                        bool back = false;
                        yield return SeekViaNavMesh(ds, nav, agent, EntryPivot(info.Door, info.Lane, srcDoorPos, ds.position.y),
                                                    "blockers: return to source lane", r => back = r);
                    }
                    yield return ReturnCarriedToOrigin(ds, forks, forkRestY, blocker, info.Door, info.Lane, srcDoorPos, inv, carry);
                    ok = false; failWhy = $"couldn't set the pallet down in {depDoor}{depLane}";
                    break;
                }

                movedCount++;
                if (!string.IsNullOrEmpty(blockerId) && inv.GetPallet(blockerId) != null)
                    RecordRelocation(inv, queue, blockerId, depDoor, depLane, depCell, blocker.position.y);

                // Another order's staged pallet now stands in a different lane: that order must collect it from there.
                if (!string.IsNullOrEmpty(blockerOrder) && ServiceLocator.TryGet<OrderService>(out var os) && os != null)
                    os.ActiveOrders.FirstOrDefault(o => o.OrderId == blockerOrder)?.AddOverflowLane(depDoor, depLane);

                SystemsLogWindow.Log($"Dock stocker moved a pallet out of {info.Door}{info.Lane} into {LaneNamingService.AddressAt(depCell)} to reach {info.OrderLabel}.");

                bool home = false;
                yield return SeekViaNavMesh(ds, nav, agent, EntryPivot(info.Door, info.Lane, srcDoorPos, ds.position.y),
                                            "blockers: back to source lane", r => home = r);
                if (!home) { ok = false; failWhy = "no NavMesh route back to the lane"; break; }
            }

            if (forks != null) SetForkLocalY(forks, forkRestY);
            if (agent != null)
            {
                agent.enabled = agentWas;
                RestoreAgentToWorkingSurface(ds, agent, homePos);
            }
            if (nav != null)
            {
                nav.enabled = navWas;
                nav.GetComponent<EquipmentBattery>()?.NoteJobFinished();
            }
            DockEquipmentCommandeerRegistry.Release(slot);
            _digLanes.Remove(key);

            if (ok)
            {
                queue?.CompleteTask(task.TaskId);
                Debug.Log($"[TrailerLoad][BLOCKERS] {key} dug out ({movedCount} pallet(s) moved).");
            }
            else
            {
                task.Status = WorkTaskStatus.Available;
                task.AssignedToEmployeeGuid = null;
                _blockerRetryAt[task.PalletId] = Time.time + DigRetrySeconds;
                ThrottledLog.Warn("tl-blockers-" + key, $"[TrailerLoad][BLOCKERS] Couldn't finish digging out {key}: {failWhy}. Retrying in {DigRetrySeconds:0}s.");
            }
        }
    }
}
