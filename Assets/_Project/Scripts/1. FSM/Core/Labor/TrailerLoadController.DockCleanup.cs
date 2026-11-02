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
    /// DOCK CLEANUP (Tad, 2026-11-01): the dock stocker keeps the dock tidy in its spare time.
    ///
    /// When there is nothing else for a dock stocker to do (no dock-stocker task waiting in the Work Queue, no docked trailer to load or
    /// offload), any order whose staged pallets are spread over MORE THAN TWO lanes is pulled together:
    ///   1. Its first two lanes (a and b) are filled up, stacking as it goes. Pallets already in a or b are never moved.
    ///   2. When a and b are full, the next free lane at the same door is filled the same way.
    /// Pallets come from the order's LAST lane, one pallet per run (so any real work that shows up gets the machine back within a minute),
    /// taken from that lane's door end and set down from the target lane's far end, never driving through a pallet.
    /// </summary>
    public partial class TrailerLoadController
    {
        private const float CleanupScanSeconds = 5f;
        private float _nextCleanupScan;
        private readonly Dictionary<string, float> _cleanupRetryAt = new Dictionary<string, float>();

        /// <summary>One cleanup move to make: which pallet, from where, to where.</summary>
        private struct CleanupMove
        {
            public OrderData Order;
            public Transform Pallet;
            public int SrcDoor; public string SrcLane;
            public int DstDoor; public string DstLane; public Vector2Int DstCell;
            public bool NewLane;
        }

        private void TryStartDockCleanup(WorkQueueSystem workQueue)
        {
            if (Time.unscaledTime < _nextCleanupScan) return;
            _nextCleanupScan = Time.unscaledTime + CleanupScanSeconds;

            // Only when there is nothing else: no dock-stocker job waiting, no trailer at a door that still needs loading or offloading.
            if (workQueue.GetPendingTasksForRole(EmployeeRole.DockStockerOperator).Any()) return;
            foreach (var t in TrucksThisFrame())
                if (t != null && t.DockedAt != null && (t.AwaitingLoad || t.AwaitingOffload)) return;

            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return;
            if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null) return;

            CleanupMove? move = null;
            foreach (var order in orders.ActiveOrders.ToList())
            {
                if (order == null || order.Status != OrderData.OrderStatus.Staged) continue;
                if (_cleanupRetryAt.TryGetValue(order.OrderId, out float retry) && Time.time < retry) continue;
                if (orders.LiveTasksForOrder(order.OrderId).Any()) continue;   // still being picked or loaded: leave it be
                if (TryPlanCleanup(inv, workQueue, orders, order, out var m)) { move = m; break; }
            }
            if (move == null) return;

            var slot = FindAvailableDockStocker();
            if (slot == null) return;

            var mv = move.Value;
            Debug.Log($"[TrailerLoad][CLEANUP] {slot.name}: order {mv.Order.OrderId} is spread over too many lanes - moving a pallet " +
                      $"{mv.SrcDoor}{mv.SrcLane} -> {LaneNamingService.AddressAt(mv.DstCell)}{(mv.NewLane ? " (new lane)" : "")}.");
            StartCoroutine(DockCleanupRoutine(slot, mv));
        }

        /// <summary>The order's lanes that actually hold its staged pallets, in fill order: its primary lane's door first, then by door
        /// number; within a door by lane letter. The first two are "a" and "b".</summary>
        private List<(int door, string lane)> OrderLanesInUse(InventoryService inv, OrderData order)
        {
            int homeDoor = order.EffectiveStagingDoor;
            var lanes = new List<(int, string)>();
            foreach (var (d, l) in order.AllStagingLanes().Distinct())
                if (LanePallets(inv, d, l).Any(p => p.OrderId == order.OrderId)) lanes.Add((d, l));
            lanes.Sort((a, b) =>
            {
                int ka = a.Item1 == homeDoor ? 0 : 1, kb = b.Item1 == homeDoor ? 0 : 1;
                if (ka != kb) return ka.CompareTo(kb);
                if (a.Item1 != b.Item1) return a.Item1.CompareTo(b.Item1);
                return string.CompareOrdinal(a.Item2, b.Item2);
            });
            return lanes;
        }

        /// <summary>Staged outbound pallets standing in a lane (on the floor, not riding anything).</summary>
        private static List<OutboundPalletBuilder> LanePallets(InventoryService inv, int door, string lane)
        {
            var list = new List<OutboundPalletBuilder>();
            foreach (var s in LaneNamingService.GetLane(door, lane))
                foreach (var go in inv.GetOutboundPalletObjectsAt(door, lane, s.Cell))
                {
                    var ob = go != null ? go.GetComponent<OutboundPalletBuilder>() : null;
                    if (ob != null && OutboundPalletBuilder.IsOnFloorTransform(ob.transform)) list.Add(ob);
                }
            return list;
        }

        private bool TryPlanCleanup(InventoryService inv, WorkQueueSystem queue, OrderService orders, OrderData order, out CleanupMove move)
        {
            move = default;
            var lanes = OrderLanesInUse(inv, order);
            if (lanes.Count <= 2) return false;   // two lanes or fewer: tidy enough

            // Source: the LAST lane. Its door-end front pallet (top of the front stack) is the one that can come out.
            var (srcDoor, srcLane) = lanes[lanes.Count - 1];
            if (!TryFrontOrderPallet(inv, srcDoor, srcLane, order.OrderId, out var pallet)) return false;

            // Destination: the first earlier lane with room (a, then b, then any other earlier lane), filled from its far end.
            for (int i = 0; i < lanes.Count - 1; i++)
            {
                var (d, l) = lanes[i];
                if (!LaneIsOnlyThisOrders(inv, d, l, order.OrderId)) continue;
                if (inv.IsStagingSlotReserved(LaneNamingService.GetLane(d, l).FirstOrDefault().Cell)) continue;
                if (TryFindSlotFromFarEnd(inv, queue, d, l, LaneNamingService.GetLane(d, l), out var cell))
                {
                    move = new CleanupMove { Order = order, Pallet = pallet.transform, SrcDoor = srcDoor, SrcLane = srcLane, DstDoor = d, DstLane = l, DstCell = cell };
                    return true;
                }
            }

            // Every earlier lane is full. A fresh lane only pays off when it can absorb two or more scattered lanes; with just one lane
            // past a and b it would only swap one lane for another.
            if (lanes.Count < 4) return false;
            var (homeDoor, bLane) = lanes[1];
            if (!TryFindFreeAdjacentLane(inv, orders, homeDoor, bLane, lanes, out string freeLane)) return false;
            if (!TryFindSlotFromFarEnd(inv, queue, homeDoor, freeLane, LaneNamingService.GetLane(homeDoor, freeLane), out var freeCell)) return false;
            move = new CleanupMove { Order = order, Pallet = pallet.transform, SrcDoor = srcDoor, SrcLane = srcLane,
                                     DstDoor = homeDoor, DstLane = freeLane, DstCell = freeCell, NewLane = true };
            return true;
        }

        /// <summary>The pallet that comes out of this lane first from its door end: the top of the first occupied slot. Refused when that
        /// pallet is another order's, is claimed for loading, or stock is in the way.</summary>
        private static bool TryFrontOrderPallet(InventoryService inv, int door, string lane, string orderId, out OutboundPalletBuilder pallet)
        {
            pallet = null;
            foreach (var s in LaneNamingService.GetLane(door, lane))   // slot 1 = door end
            {
                if (PhysicalStack(inv, s.Cell).Count > 0) return false;   // received stock in front: not ours to move
                var here = inv.GetOutboundPalletObjectsAt(door, lane, s.Cell)
                              .Select(go => go != null ? go.GetComponent<OutboundPalletBuilder>() : null)
                              .Where(ob => ob != null && OutboundPalletBuilder.IsOnFloorTransform(ob.transform))
                              .ToList();
                if (here.Count == 0) continue;
                var top = here.OrderByDescending(ob => ob.transform.position.y).First();
                if (top.OrderId != orderId || top.Stage != OutboundPalletBuilder.PalletStage.Staged ||
                    OutboundPalletBuilder.LoadClaimed.Contains(top)) return false;
                pallet = top;
                return true;
            }
            return false;
        }

        private static bool LaneIsOnlyThisOrders(InventoryService inv, int door, string lane, string orderId)
        {
            foreach (var s in LaneNamingService.GetLane(door, lane))
                if (PhysicalStack(inv, s.Cell).Count > 0) return false;   // received stock in the lane
            return LanePallets(inv, door, lane).All(p => p.OrderId == orderId);
        }

        /// <summary>The nearest completely empty lane at the same door as lane b (by letter distance), that no other live order claims and
        /// that sorts BEFORE the lanes being emptied, so the next runs keep filling it.</summary>
        private static bool TryFindFreeAdjacentLane(InventoryService inv, OrderService orders, int door, string bLane,
                                                    List<(int door, string lane)> orderLanes, out string lane)
        {
            lane = null;
            int best = int.MaxValue;
            foreach (var (d, l) in LaneNamingService.AllLanes())
            {
                if (d != door || orderLanes.Contains((d, l))) continue;
                if (!inv.LaneAllowsPicking(d, l)) continue;
                if (StagingLaneAssignmentService.LaneHasInboundStock(inv, d, l)) continue;
                if (inv.LaneHasStagedOutboundPallets(d, l)) continue;
                if (orders.ActiveOrders.Any(o => o != null && o.UsesLane(d, l) &&
                        o.Status != OrderData.OrderStatus.Shipped && o.Status != OrderData.OrderStatus.Cancelled)) continue;
                var slots = LaneNamingService.GetLane(d, l);
                if (slots.Count == 0 || slots.Any(s => inv.IsStagingSlotReserved(s.Cell))) continue;
                int dist = Mathf.Abs(string.CompareOrdinal(l, bLane));
                if (dist < best) { best = dist; lane = l; }
            }
            return lane != null;
        }

        private IEnumerator DockCleanupRoutine(MHEOperatorSlot slot, CleanupMove mv)
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
            Vector3 srcDoorPos = DoorPosition(mv.SrcDoor, ds.position);
            var pallet = mv.Pallet;
            var ob = pallet != null ? pallet.GetComponent<OutboundPalletBuilder>() : null;
            string failWhy = null;

            if (ob != null) OutboundPalletBuilder.LoadClaimed.Add(ob);   // the loader leaves it alone while it's on the move
            try
            {
                // 1. To the source lane's door end.
                Vector3 srcPivot = EntryPivot(mv.SrcDoor, mv.SrcLane, srcDoorPos, ds.position.y);
                Vector3 toPivot = srcPivot - ds.position; toPivot.y = 0f;
                bool there = toPivot.magnitude <= 3f;
                if (!there) yield return SeekViaNavMesh(ds, nav, agent, srcPivot, $"cleanup: → lane {mv.SrcDoor}{mv.SrcLane}", r => there = r);
                if (!there) { failWhy = $"no route to lane {mv.SrcDoor}{mv.SrcLane}"; yield break; }

                // Still the front pallet? (Something may have changed while driving over.)
                if (pallet == null || !TryFrontOrderPallet(inv, mv.SrcDoor, mv.SrcLane, mv.Order.OrderId, out var front) || front.transform != pallet)
                { failWhy = "the lane changed"; yield break; }

                // 2. Out of the source lane.
                var carry = new CarryState();
                if (ob != null) ob.Stage = OutboundPalletBuilder.PalletStage.Moving;
                yield return PickUpFromLaneEntry(ds, forks, forkRestY, pallet, mv.SrcDoor, mv.SrcLane, srcDoorPos, inv, carry);
                if (!carry.Ok) { failWhy = "couldn't pick it up"; yield break; }

                // 3. Round to the target lane's far end and in.
                bool reached = false, dropped = false;
                LaneNamingService.TryGetLaneGeometry(mv.DstDoor, mv.DstLane, out var dstGeo);
                yield return SeekViaNavMesh(ds, nav, agent, dstGeo.ExitPoint, $"cleanup: → {mv.DstDoor}{mv.DstLane} far end", r => reached = r);
                if (reached)
                    yield return SetDownFromFarEnd(ds, forks, forkRestY, pallet, mv.DstDoor, mv.DstLane, mv.DstCell, inv, carry, r => dropped = r);

                if (!dropped)
                {
                    // Put it back where it came from rather than strand it on the forks.
                    if (reached)
                    {
                        bool back = false;
                        yield return SeekViaNavMesh(ds, nav, agent, EntryPivot(mv.SrcDoor, mv.SrcLane, srcDoorPos, ds.position.y), "cleanup: back to source lane", r => back = r);
                    }
                    yield return ReturnCarriedToOrigin(ds, forks, forkRestY, pallet, mv.SrcDoor, mv.SrcLane, srcDoorPos, inv, carry);
                    failWhy = $"couldn't set it down in {mv.DstDoor}{mv.DstLane}";
                    yield break;
                }

                // 4. Bookkeeping: the order now uses the target lane; a lane it no longer has pallets in is given back.
                var order = mv.Order;
                order.AddOverflowLane(mv.DstDoor, mv.DstLane);
                if (!LanePallets(inv, mv.SrcDoor, mv.SrcLane).Any(p => p.OrderId == order.OrderId))
                    order.OverflowLanes.RemoveAll(r => r.door == mv.SrcDoor && r.lane == mv.SrcLane);
                SystemsLogWindow.Log($"Dock stocker consolidated order {order.OrderId}: moved a pallet from {mv.SrcDoor}{mv.SrcLane} " +
                                     $"to {LaneNamingService.AddressAt(mv.DstCell)}.");
            }
            finally
            {
                if (ob != null)
                {
                    OutboundPalletBuilder.LoadClaimed.Remove(ob);
                    if (ob.Stage == OutboundPalletBuilder.PalletStage.Moving) ob.Stage = OutboundPalletBuilder.PalletStage.Staged;
                }
                if (failWhy != null)
                {
                    _cleanupRetryAt[mv.Order.OrderId] = Time.time + 60f;
                    Debug.Log($"[TrailerLoad][CLEANUP] Gave up on order {mv.Order.OrderId} for now: {failWhy}.");
                }

                // Hand the machine back (mirror of DigAndLoadRoutine).
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
            }
        }
    }
}
