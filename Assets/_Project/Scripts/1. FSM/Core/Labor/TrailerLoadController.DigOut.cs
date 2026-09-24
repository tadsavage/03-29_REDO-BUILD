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
    /// DIG-OUT &amp; DIRECT LOAD (Tad, 2026-09-23).
    ///
    /// When an outbound order's pallet is still sitting in an inbound staging lane (a PalletPick whose
    /// FromLocation is "STG…" — see OrderService.FileReleasedOrderTasks) and that order's trailer is
    /// docked at the SAME door, a dock stocker can take the job instead of a reach truck:
    ///
    ///   1. Work the lane from its DOOR end. Anything in front of the order's pallet (and anything
    ///      stacked on top of it) is a blocker.
    ///   2. Each blocker is carried round to ANOTHER lane and put in from that lane's FAR end (the end
    ///      furthest from its door) — never into a lane that has staged outbound freight, is assigned to
    ///      an order, or has a staging reservation, and never past a pallet already in it.
    ///   3. Once the order's pallet is the front one, it goes straight onto the trailer.
    ///
    /// The reach truck can still do the same PalletPick its own way (from the lane's far end, only when
    /// the pallet — or one of the same product — is the one it can reach). Whichever vehicle claims the
    /// Available task first does it.
    ///
    /// NEVER DRIVES THROUGH PALLETS: lane entries only ever go as deep as the first occupied slot
    /// (pickups take the front pallet, drops stop at the frontier from the far end), and the long legs
    /// between lanes use the NavMesh, which pallets carve.
    /// </summary>
    public partial class TrailerLoadController
    {
        /// <summary>How long a PalletPick is left alone after the dock stocker couldn't dig it out
        /// (nowhere to put a blocker, a reach truck already working the lane, …) before trying again.</summary>
        private const float DigRetrySeconds = 15f;
        /// <summary>Safety cap on blockers moved in one run — a lane is at most a handful of slots two high.</summary>
        private const int MaxBlockersPerRun = 16;

        private readonly Dictionary<string, float> _digRetryAt = new Dictionary<string, float>();

        /// <summary>Per-carry bookkeeping a coroutine can't return through out-params.</summary>
        private class CarryState
        {
            public bool Ok;
            public Transform OriginalParent;
            public Vector2Int OriginalCell;
            public Vector3 OriginalPos;
            public Quaternion OriginalRot;
        }

        // ── Claiming ─────────────────────────────────────────────────────────────────────────────

        private void TryStartDigAndLoad(WorkQueueSystem workQueue)
        {
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return;

            // Gather every pick the dock stocker could do right now, then take the one with the FEWEST
            // pallets in the way. Otherwise it happily digs for the deepest of an order's pallets and
            // shoves the order's OWN front pallets into another lane as "blockers" (seen live
            // 2026-09-23) — instead of just loading those first.
            WorkTask best = null;
            TruckController bestTruck = null;
            int bestSrcDoor = 0, bestBlockers = int.MaxValue;
            string bestSrcLane = null;

            foreach (var task in workQueue.Tasks.ToList())
            {
                if (task.Type != WorkTaskType.PalletPick || task.Status != WorkTaskStatus.Available) continue;
                if (string.IsNullOrEmpty(task.PalletId) || string.IsNullOrEmpty(task.OrderId)) continue;
                if (!TryParseStagingAddress(task.FromLocation, out int srcDoor, out string srcLane)) continue;
                if (!TryParseLaneAddress(task.ToLocation, out int dstDoor, out _)) continue;
                if (srcDoor != dstDoor) continue; // a dock stocker works its own door's dock only
                if (_digRetryAt.TryGetValue(task.TaskId, out float retryAt) && Time.time < retryAt) continue;

                var truck = FindDockedOutboundTruck(dstDoor);
                if (truck == null || !truck.AwaitingLoad) continue;
                if (truck.LoadContainer == null || truck.LoadContainer.childCount >= TruckController.PalletSlotCount) continue;

                if (!TryPlanDig(inv, workQueue, srcDoor, srcLane, task.PalletId, out var blockers, out string why))
                {
                    BackOff(task, why);
                    continue;
                }
                if (blockers.Count > 0 &&
                    !TryFindDepositSlot(inv, workQueue, srcDoor, srcLane, out _, out _, out _))
                {
                    BackOff(task, "no lane with room to take the pallets in the way");
                    continue;
                }

                if (blockers.Count >= bestBlockers) continue;
                best = task; bestTruck = truck; bestSrcDoor = srcDoor; bestSrcLane = srcLane; bestBlockers = blockers.Count;
                if (bestBlockers == 0) break; // can't do better than a pallet already at the front
            }

            if (best == null) return;

            var slot = FindAvailableDockStocker();
            if (slot == null) return; // no manned DS free — try again next poll

            string opGuid = slot.CurrentOperator?.Record?.employeeGuid;
            if (!workQueue.TryClaimSpecificTask(best, opGuid)) return;

            Debug.Log($"[TrailerLoad][DIG] {slot.name} taking pallet pick {best.TaskId} from {bestSrcDoor}{bestSrcLane} " +
                      $"({bestBlockers} pallet(s) in the way) straight onto {bestTruck.name}.");
            StartCoroutine(DigAndLoadRoutine(bestTruck, slot, best, bestSrcDoor, bestSrcLane));
        }

        private void BackOff(WorkTask task, string why)
        {
            bool firstTime = !_digRetryAt.ContainsKey(task.TaskId);
            _digRetryAt[task.TaskId] = Time.time + DigRetrySeconds;
            if (firstTime)
                Debug.Log($"[TrailerLoad][DIG] Can't dig out pallet pick {task.TaskId} ({task.FromLocation}) yet: {why}. Retrying in {DigRetrySeconds:0}s.");
        }

        /// <summary>"STG1A-5" → door 1, lane "A". (Staging addresses are "STG" + LaneSlot.Name.)</summary>
        private static bool TryParseStagingAddress(string address, out int door, out string lane)
        {
            door = 0; lane = null;
            if (string.IsNullOrEmpty(address) || !address.StartsWith("STG", System.StringComparison.OrdinalIgnoreCase)) return false;
            string rest = address.Substring(3);
            int dash = rest.IndexOf('-');
            return TryParseLaneAddress(dash >= 0 ? rest.Substring(0, dash) : rest, out door, out lane);
        }

        // ── Planning ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Pallets standing in a cell, top first. Physical only — an inventory record with no
        /// pallet in the world (the "A Chep" ghosts) neither blocks nor can be moved.</summary>
        private static List<Transform> PhysicalStack(InventoryService inv, Vector2Int cell)
        {
            var list = new List<Transform>();
            foreach (var rec in inv.GetPalletsAtLocation(cell))
            {
                var link = PalletMasterLink.Find(rec.PalletId);
                if (link == null) continue;
                if (link.GetComponentInParent<MHEOperatorSlot>() != null) continue; // riding someone's forks
                list.Add(link.transform);
            }
            list.Sort((a, b) => b.position.y.CompareTo(a.position.y));
            return list;
        }

        /// <summary>
        /// Walks the lane from its door end and lists every pallet that has to move before
        /// <paramref name="targetPalletId"/> is the front, top pallet — in the order they must go
        /// (front slot first, top of each stack first). Refuses if staged outbound freight is in the way
        /// (that belongs to some order and isn't this routine's to move) or a reach truck is already
        /// mid-job on one of them.
        /// </summary>
        private bool TryPlanDig(InventoryService inv, WorkQueueSystem queue, int door, string lane, string targetPalletId,
                                out List<Transform> blockers, out string why)
        {
            blockers = new List<Transform>();
            why = null;

            foreach (var s in LaneNamingService.GetLane(door, lane)) // slot 1 = door end
            {
                if (inv.GetOutboundPalletObjectsAt(door, lane, s.Cell).Any(go => go != null && go.transform.parent == null))
                {
                    why = $"staged outbound freight at {s.Name} is in the way";
                    return false;
                }

                var stack = PhysicalStack(inv, s.Cell);
                int idx = stack.FindIndex(t => t.GetComponent<PalletMasterLink>()?.PalletId == targetPalletId);
                var inFront = idx >= 0 ? stack.Take(idx) : stack;

                foreach (var t in inFront)
                {
                    string id = t.GetComponent<PalletMasterLink>()?.PalletId;
                    if (queue.Tasks.Any(x => x.PalletId == id && x.Status == WorkTaskStatus.Assigned))
                    {
                        why = $"another vehicle is already working pallet {id} in the way";
                        return false;
                    }
                    blockers.Add(t);
                }

                if (idx >= 0)
                {
                    if (blockers.Count > MaxBlockersPerRun) { why = $"{blockers.Count} pallets in the way"; return false; }
                    return true;
                }
            }

            why = $"pallet {targetPalletId} isn't in lane {door}{lane} any more";
            return false;
        }

        /// <summary>
        /// Where the next blocker can go: a lane OTHER than the one being dug, that takes putaway
        /// (Receiving/Both), holds no staged outbound freight, isn't assigned to a live order and has no
        /// staging reservation — entered from its FAR end and filled no deeper than the first pallet
        /// already in it. Same-door lanes first, then the nearest.
        /// </summary>
        private bool TryFindDepositSlot(InventoryService inv, WorkQueueSystem queue, int srcDoor, string srcLane,
                                        out int depDoor, out string depLane, out Vector2Int depCell)
        {
            depDoor = 0; depLane = null; depCell = default;
            ServiceLocator.TryGet<OrderService>(out var orders);
            LaneNamingService.TryGetLaneGeometry(srcDoor, srcLane, out var srcGeo);

            float best = float.MaxValue;
            foreach (var (d, l) in LaneNamingService.AllLanes())
            {
                if (d == srcDoor && l == srcLane) continue;
                if (!inv.LaneAcceptsPutaway(d, l)) continue;
                if (orders != null && orders.ActiveOrders.Any(o =>
                        o.AssignedDoorNumber == d && o.AssignedLane == l &&
                        o.Status != OrderData.OrderStatus.Shipped && o.Status != OrderData.OrderStatus.Cancelled))
                    continue;

                var slots = LaneNamingService.GetLane(d, l);
                if (slots.Count == 0) continue;
                if (slots.Any(s => inv.IsStagingSlotReserved(s.Cell) ||
                                   inv.GetOutboundPalletObjectsAt(d, l, s.Cell).Any(go => go != null)))
                    continue;

                if (!TryFindSlotFromFarEnd(inv, queue, d, l, slots, out var cell)) continue;

                float score = (d == srcDoor ? 0f : 10000f) + (LaneNamingService.TryGetLaneGeometry(d, l, out var geo)
                    ? (geo.ExitPoint - srcGeo.EntryPoint).sqrMagnitude : 0f);
                if (score >= best) continue;
                best = score;
                depDoor = d; depLane = l; depCell = cell;
            }
            return depLane != null;
        }

        /// <summary>Drive in from the lane's far end and stop at the first occupied slot: the deepest
        /// empty slot before it is the drop, or — if the far-most slot itself is the frontier — stack on
        /// it when there's room and nothing is mid-putaway off it.</summary>
        private static bool TryFindSlotFromFarEnd(InventoryService inv, WorkQueueSystem queue, int d, string l,
                                                  List<LaneNamingService.LaneSlot> slotsDoorFirst, out Vector2Int cell)
        {
            cell = default;
            int maxH = LaneConfigRegistry.Get(d, l).MaxStackHeight;
            bool found = false;

            for (int i = slotsDoorFirst.Count - 1; i >= 0; i--) // far end → door end
            {
                var c = slotsDoorFirst[i].Cell;
                var stack = PhysicalStack(inv, c);
                if (stack.Count == 0) { cell = c; found = true; continue; }

                if (!found && stack.Count < maxH)
                {
                    string topId = stack[0].GetComponent<PalletMasterLink>()?.PalletId;
                    if (!queue.Tasks.Any(x => x.PalletId == topId && x.Status == WorkTaskStatus.Assigned))
                    { cell = c; found = true; }
                }
                break; // can't drive past an occupied slot
            }
            return found;
        }

        private bool HasStagedPalletForOrder(string orderId)
        {
            foreach (var p in FindObjectsByType<OutboundPalletBuilder>())
                if (p != null && p.transform.parent == null && p.OrderId == orderId) return true;
            return false;
        }

        // ── The routine ──────────────────────────────────────────────────────────────────────────

        private IEnumerator DigAndLoadRoutine(TruckController truck, MHEOperatorSlot slot, WorkTask task,
                                              int srcDoor, string srcLane)
        {
            truck.ClaimForLoad();
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
            ServiceLocator.TryGet<OrderService>(out var orders);

            Vector3 srcDoorPos = DoorPosition(srcDoor, ds.position);
            bool ok = true;
            string failWhy = null;

            // ── 1+2. Move blockers out, one at a time, re-planning each time ──
            for (int moved = 0; ok && moved <= MaxBlockersPerRun; moved++)
            {
                if (ds == null) { ok = false; break; }
                if (!TryPlanDig(inv, queue, srcDoor, srcLane, task.PalletId, out var blockers, out failWhy)) { ok = false; break; }
                if (blockers.Count == 0) break;

                // Find the drop BEFORE picking anything up, so a full building never leaves the DS
                // holding a pallet with nowhere to put it.
                if (!TryFindDepositSlot(inv, queue, srcDoor, srcLane, out int depDoor, out string depLane, out var depCell))
                { ok = false; failWhy = "no lane with room to take the pallets in the way"; break; }

                var blocker = blockers[0];
                string blockerId = blocker.GetComponent<PalletMasterLink>()?.PalletId;
                var carry = new CarryState();
                yield return PickUpFromLaneEntry(ds, forks, forkRestY, blocker, srcDoor, srcLane, srcDoorPos, inv, carry);
                if (!carry.Ok) { ok = false; failWhy = $"couldn't pick up {blockerId}"; break; }

                // Round to the far end of the other lane via the NavMesh — pallets carve it, so this is
                // the leg that goes AROUND lanes rather than through them.
                bool reached = false;
                LaneNamingService.TryGetLaneGeometry(depDoor, depLane, out var depGeo);
                yield return SeekViaNavMesh(ds, nav, agent, depGeo.ExitPoint, $"dig: → {depDoor}{depLane} far end", r => reached = r);

                bool dropped = false;
                if (reached)
                    yield return SetDownFromFarEnd(ds, forks, forkRestY, blocker, depDoor, depLane, depCell, inv, carry, r => dropped = r);

                if (!dropped)
                {
                    // Couldn't get there / in — put it back where it came from rather than strand it.
                    if (reached)
                    {
                        bool back = false;
                        yield return SeekViaNavMesh(ds, nav, agent, EntryPivot(srcDoor, srcLane, srcDoorPos, ds.position.y),
                                                    "dig: return to source lane", r => back = r);
                    }
                    yield return ReturnCarriedToOrigin(ds, forks, forkRestY, blocker, srcDoor, srcLane, srcDoorPos, inv, carry);
                    ok = false; failWhy = $"couldn't set {blockerId} down in {depDoor}{depLane}";
                    break;
                }

                RecordRelocation(inv, queue, blockerId, depDoor, depLane, depCell, blocker.position.y);
                SystemsLogWindow.Log($"Dock stocker moved a pallet out of {srcDoor}{srcLane} into {LaneNamingService.AddressAt(depCell)} " +
                                     "to reach an outbound order.");

                bool home = false;
                yield return SeekViaNavMesh(ds, nav, agent, EntryPivot(srcDoor, srcLane, srcDoorPos, ds.position.y),
                                            "dig: back to source lane", r => home = r);
                if (!home) { ok = false; failWhy = "no NavMesh route back to the source lane"; break; }
            }

            // ── 3. The order's pallet is now the front one: straight onto the trailer ──
            if (ok && ds != null)
            {
                var link = PalletMasterLink.Find(task.PalletId);
                var rec = inv?.GetPallet(task.PalletId);
                if (link == null || rec == null) { ok = false; failWhy = "the order's pallet disappeared"; }
                else if (truck == null || truck.DockedAt == null) { ok = false; failWhy = "the trailer left"; }
                else
                {
                    int cases = rec.Quantity;
                    string skuId = rec.SkuId;
                    var carry = new CarryState();
                    yield return PickUpFromLaneEntry(ds, forks, forkRestY, link.transform, srcDoor, srcLane, srcDoorPos, inv, carry);
                    if (!carry.Ok) { ok = false; failWhy = "couldn't pick up the order's pallet"; }
                    else
                    {
                        int slotIndex = CargoSlotForSequence(truck.LoadContainer.childCount);
                        Vector3 doorPos = truck.DockedAt != null ? truck.DockedAt.transform.position : srcDoorPos;
                        yield return PlaceCarriedInTrailer(ds, forks, truck, link.transform, slotIndex, doorPos, ds.position.y);

                        // Same bookkeeping as a reach-truck pallet pick, plus the loader's "on the trailer".
                        var outbound = link.GetComponent<OutboundPalletBuilder>() ?? link.gameObject.AddComponent<OutboundPalletBuilder>();
                        outbound.AdoptFullPallet(task.OrderId, cases);
                        outbound.SetNavObstacleActive(false);
                        var po = link.GetComponent<PlacedObject>();
                        if (po != null) po.enabled = false;

                        // ORDER MATTERS (see ReachTruckOperator.DeliverPalletToStagingLane): complete the
                        // task BEFORE removing the pallet, or the destroy event cancels this very task.
                        queue?.CompleteTask(task.TaskId);
                        inv.DestroyPallet(task.PalletId);
                        orders?.NotePalletPicked(task.OrderId, skuId, cases);
                        var order = orders?.ActiveOrders.FirstOrDefault(o => o.OrderId == task.OrderId);
                        if (order != null)
                        {
                            order.PalletsShipped += 1;
                            // Everything for this order is now aboard and nothing else is coming: mark it
                            // Loaded directly — no staged pallet exists for a Load task to find.
                            if (order.Status == OrderData.OrderStatus.Staged &&
                                !orders.LiveTasksForOrder(order.OrderId).Any() &&
                                !HasStagedPalletForOrder(order.OrderId))
                                orders.MarkOrderLoaded(order.OrderId, 0);
                        }

                        Debug.Log($"[TrailerLoad][DIG] Loaded {cases} cs of {skuId} for order {task.OrderId} from " +
                                  $"{srcDoor}{srcLane} straight onto {truck.name} cargoSlot {slotIndex}.");
                    }
                }
            }

            if (!ok && task.Status == WorkTaskStatus.Assigned)
            {
                task.Status = WorkTaskStatus.Available;
                task.AssignedToEmployeeGuid = null;
                BackOff(task, failWhy ?? "unknown");
            }

            // ── Restore the DS to patrol (mirror of LoadRoutine) ──
            if (forks != null) SetForkLocalY(forks, forkRestY);
            if (agent != null)
            {
                agent.enabled = agentWas;
                RestoreAgentToWorkingSurface(ds, agent, homePos);
            }
            if (nav != null)
            {
                nav.enabled = navWas;
                nav.GoToRandomWaypoint();
            }
            DockEquipmentCommandeerRegistry.Release(slot);
            if (truck != null) truck.ReleaseLoadClaim();
        }

        // ── Moves ────────────────────────────────────────────────────────────────────────────────

        private static Vector3 DoorPosition(int door, Vector3 fallback)
        {
            var dock = DockSlot.All.FirstOrDefault(d => d != null && d.DoorNumber == door);
            return dock != null ? dock.transform.position : fallback;
        }

        /// <summary>The door-side pivot of a lane: LanePivotDistance out from slot 1, where every
        /// lane-side turn happens (same point LoadOnePallet uses).</summary>
        private Vector3 EntryPivot(int door, string lane, Vector3 doorPos, float driveY)
        {
            LaneEntryGeometry(LaneNamingService.GetLane(door, lane), doorPos, out Vector3 entryW, out Vector3 downLane);
            return new Vector3(entryW.x, driveY, entryW.z) - downLane * LanePivotDistance;
        }

        /// <summary>
        /// Takes the FRONT pallet out of a lane from its door end — the same moves as LoadOnePallet's
        /// pickup (come out to the aisle first, pivot, forks in low, stop, lift, seat, reverse out), for
        /// any pallet rather than only staged outbound ones. Leaves the DS at the lane's entry pivot with
        /// the pallet carried at PalletLiftClearance, and the pallet off the inventory grid.
        /// </summary>
        private IEnumerator PickUpFromLaneEntry(Transform ds, Transform forks, float forkRestY, Transform palletT,
                                                int door, string lane, Vector3 doorPos, InventoryService inv, CarryState carry)
        {
            carry.Ok = false;
            if (ds == null || palletT == null) yield break;

            carry.OriginalParent = palletT.parent;
            carry.OriginalPos = palletT.position;
            carry.OriginalRot = palletT.rotation;
            carry.OriginalCell = _grid.WorldToCell(palletT.position);

            LaneEntryGeometry(LaneNamingService.GetLane(door, lane), doorPos, out Vector3 entryW, out Vector3 downLane);
            Vector3 entryPivot = new Vector3(entryW.x, ds.position.y, entryW.z) - downLane * LanePivotDistance;

            // Out to the dock aisle first if starting deeper than the pivot — never run along a lane.
            float depthPastPivot = Vector3.Dot(ds.position - entryPivot, downLane);
            if (depthPastPivot > 0f) yield return DriveTailFirst(ds, ds.position - downLane * depthPastPivot);
            yield return DriveTailFirst(ds, entryPivot);

            if (inv != null)
                while (!inv.TryEnterLaneForDelivery(door, lane)) yield return null;
            try
            {
                if (palletT == null || ds == null) yield break;
                yield return FaceForks(ds, downLane);
                if (forks != null) yield return LiftForks(forks, forkRestY);
                yield return DriveInToGrab(ds, forks, forkRestY, palletT, downLane);
                if (palletT == null || ds == null) yield break;

                Transform carrier = forks != null ? forks : ds;
                Vector3 facing = palletT.forward;
                palletT.SetParent(carrier, worldPositionStays: false);
                palletT.localPosition = ForkCarryLocalPos;
                palletT.localRotation = NearestFacing(Quaternion.Euler(ForkCarryLocalEuler), carrier.InverseTransformDirection(facing));
                SetCarried(palletT, true);

                string id = palletT.GetComponent<PalletMasterLink>()?.PalletId;
                if (inv != null && !string.IsNullOrEmpty(id)) inv.MovePallet(id, new Vector2Int(-1, -1)); // off the grid while carried

                if (forks != null) yield return LiftForks(forks, forks.localPosition.y + PalletLiftClearance);
                yield return DriveTailFirst(ds, entryPivot);
                carry.Ok = true;
            }
            finally
            {
                if (inv != null) inv.ReleaseLaneEntry(door, lane);
            }
        }

        /// <summary>
        /// Puts the carried pallet into a lane from its FAR end: square up on the lane's centre line
        /// just outside it, face the forks toward the door, raise to clear anything it's stacking onto,
        /// drive straight in (only ever through empty slots — see TryFindSlotFromFarEnd) until the pallet
        /// is over its cell, seat it, then reverse back out.
        /// </summary>
        private IEnumerator SetDownFromFarEnd(Transform ds, Transform forks, float forkRestY, Transform palletT,
                                              int door, string lane, Vector2Int cell, InventoryService inv,
                                              CarryState carry, System.Action<bool> onDone)
        {
            if (ds == null || palletT == null || !LaneNamingService.TryGetLaneGeometry(door, lane, out var geo))
            { onDone(false); yield break; }

            Vector3 depth = Flat(geo.DepthAxis);                 // door → far
            Vector3 farPivot = new Vector3(geo.ExitPoint.x, ds.position.y, geo.ExitPoint.z);
            Vector3 cellW = _grid.GetCellCenter(cell);

            yield return DriveTailFirst(ds, farPivot);           // close the NavMesh stopping distance exactly

            if (inv != null)
                while (!inv.TryEnterLaneForDelivery(door, lane)) yield return null;
            try
            {
                if (ds == null || palletT == null) { onDone(false); yield break; }

                // Re-check the lane hasn't filled in front of the chosen cell while we drove round.
                var slots = LaneNamingService.GetLane(door, lane);
                ServiceLocator.TryGet<WorkQueueSystem>(out var queue);
                if (!TryFindSlotFromFarEnd(inv, queue, door, lane, slots, out var nowCell) || nowCell != cell)
                { onDone(false); yield break; }

                float dropBaseY = TrailerOffloadController.StagingDropBaseY(door, lane, cell, palletT.gameObject);

                yield return FaceForks(ds, -depth);
                // Clear the top of whatever it's being stacked on before moving over it.
                if (forks != null)
                {
                    float needLift = (dropBaseY + PalletLiftClearance) - palletT.position.y;
                    if (needLift > 0f) yield return LiftForks(forks, forks.localPosition.y + needLift);
                }

                Vector3 palletOffset = palletT.position - ds.position; palletOffset.y = 0f;
                Vector3 overCell = new Vector3(cellW.x, ds.position.y, cellW.z) - palletOffset;
                // Straight along the lane axis only — project the target onto the entry line so it
                // can't cut diagonally across a neighbouring lane.
                Vector3 along = farPivot + depth * Vector3.Dot(overCell - farPivot, depth);
                yield return DriveForksFirst(ds, along);
                if (ds == null || palletT == null) { onDone(false); yield break; }

                if (forks != null)
                    yield return LiftForks(forks, forks.localPosition.y + (dropBaseY - palletT.position.y));

                palletT.SetParent(carry.OriginalParent, worldPositionStays: true);
                palletT.position = new Vector3(cellW.x, dropBaseY, cellW.z);
                palletT.rotation = NearestFacing(Quaternion.LookRotation(depth, Vector3.up), palletT.forward);
                SetCarried(palletT, false);

                yield return DriveTailFirst(ds, farPivot);
                if (forks != null) yield return LiftForks(forks, forkRestY);
                onDone(true);
            }
            finally
            {
                if (inv != null) inv.ReleaseLaneEntry(door, lane);
            }
        }

        /// <summary>Failure path: drive back in from the source lane's door end and put the pallet back
        /// exactly where it was picked from (it was the front pallet, so that spot is still reachable).</summary>
        private IEnumerator ReturnCarriedToOrigin(Transform ds, Transform forks, float forkRestY, Transform palletT,
                                                  int door, string lane, Vector3 doorPos, InventoryService inv, CarryState carry)
        {
            if (ds == null || palletT == null) yield break;
            LaneEntryGeometry(LaneNamingService.GetLane(door, lane), doorPos, out Vector3 entryW, out Vector3 downLane);
            Vector3 entryPivot = new Vector3(entryW.x, ds.position.y, entryW.z) - downLane * LanePivotDistance;

            yield return DriveTailFirst(ds, entryPivot);
            if (inv != null)
                while (!inv.TryEnterLaneForDelivery(door, lane)) yield return null;
            try
            {
                if (ds == null || palletT == null) yield break;
                yield return FaceForks(ds, downLane);
                Vector3 palletOffset = palletT.position - ds.position; palletOffset.y = 0f;
                Vector3 over = new Vector3(carry.OriginalPos.x, ds.position.y, carry.OriginalPos.z) - palletOffset;
                yield return DriveForksFirst(ds, entryPivot + downLane * Vector3.Dot(over - entryPivot, downLane));
                if (ds == null || palletT == null) yield break;
                if (forks != null)
                    yield return LiftForks(forks, forks.localPosition.y + (carry.OriginalPos.y - palletT.position.y));

                palletT.SetParent(carry.OriginalParent, worldPositionStays: true);
                palletT.SetPositionAndRotation(carry.OriginalPos, carry.OriginalRot);
                SetCarried(palletT, false);
                string id = palletT.GetComponent<PalletMasterLink>()?.PalletId;
                if (inv != null && !string.IsNullOrEmpty(id)) inv.MovePallet(id, carry.OriginalCell);

                yield return DriveTailFirst(ds, entryPivot);
                if (forks != null) yield return LiftForks(forks, forkRestY);
            }
            finally
            {
                if (inv != null) inv.ReleaseLaneEntry(door, lane);
            }
        }

        /// <summary>While carried a pallet mustn't carve the NavMesh (it would drag a trench) or be
        /// treated as a placed object; on the ground it must do both again.</summary>
        private static void SetCarried(Transform palletT, bool carried)
        {
            var obstacle = palletT.GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = !carried;
            var po = palletT.GetComponent<PlacedObject>();
            if (po != null) po.enabled = !carried;
        }

        /// <summary>A blocker now lives in a different lane: inventory location/height/lane, its
        /// PlacedObject grid cell, and every job that pointed at its old staging address.</summary>
        private static void RecordRelocation(InventoryService inv, WorkQueueSystem queue, string palletId,
                                             int door, string lane, Vector2Int cell, float baseY)
        {
            if (inv == null || string.IsNullOrEmpty(palletId)) return;
            inv.MovePallet(palletId, cell);
            var rec = inv.GetPallet(palletId);
            if (rec != null)
            {
                rec.StagingLaneId = $"{door}{lane}";
                rec.WorldHeightY = baseY;
            }

            var link = PalletMasterLink.Find(palletId);
            var po = link != null ? link.GetComponent<PlacedObject>() : null;
            if (po != null) { po.gridX = cell.x; po.gridY = cell.y; }

            string address = LaneNamingService.AddressAt(cell);
            if (queue == null || string.IsNullOrEmpty(address)) return;
            foreach (var t in queue.Tasks)
                if (t.PalletId == palletId) t.RelocateStagingSource("STG" + address);
        }

        // ── NavMesh leg (ported from TrailerOffloadController.SeekViaNavMesh — see its doc; every guard
        //    there was earned in ReachTruckOperator and is kept deliberately) ─────────────────────────
        private IEnumerator SeekViaNavMesh(Transform ds, AiNavigation nav, NavMeshAgent agent, Vector3 target,
                                           string phase, System.Action<bool> onDone)
        {
            if (ds == null || nav == null || agent == null)
            {
                Debug.LogError($"[TrailerLoad] {phase}: no AiNavigation/NavMeshAgent on this dock stocker.");
                onDone?.Invoke(false);
                yield break;
            }

            agent.enabled = true;
            nav.enabled = true;

            float registerWaited = 0f;
            while (!agent.isOnNavMesh && registerWaited < 1f)
            {
                agent.Warp(ds.position);
                registerWaited += Time.deltaTime;
                yield return null;
            }
            if (!agent.isOnNavMesh)
            {
                Debug.LogError($"[TrailerLoad] '{ds.name}' {phase}: agent not on the NavMesh at {ds.position}.");
                nav.SetTaskBusy(false);
                agent.enabled = false; nav.enabled = false;
                onDone?.Invoke(false);
                yield break;
            }

            Vector3 driveTarget = new Vector3(target.x, ds.position.y, target.z);
            if (NavMesh.SamplePosition(driveTarget, out var hit, 4f, agent.areaMask)) driveTarget = hit.position;

            nav.CancelSeekPosition();
            agent.Warp(ds.position);
            nav.SetTaskBusy(true);

            bool arrived = false;
            nav.SeekPosition(driveTarget, () => arrived = true);
            yield return null;
            if (!arrived && !agent.pathPending && !agent.hasPath)
            {
                Debug.LogError($"[TrailerLoad] '{ds.name}' {phase}: no path to {driveTarget} from {ds.position}.");
                nav.CancelSeekPosition(); nav.SetTaskBusy(false);
                agent.enabled = false; nav.enabled = false;
                onDone?.Invoke(false);
                yield break;
            }

            float elapsed = 0f, sinceRecheck = 0f;
            int reissues = 0;
            while (!arrived && nav.IsSeekingTask && elapsed < 30f)
            {
                elapsed += Time.deltaTime;
                sinceRecheck += Time.deltaTime;
                if (sinceRecheck >= 0.5f)
                {
                    sinceRecheck = 0f;
                    if (!agent.isOnNavMesh) agent.Warp(ds.position);
                    else if (!agent.pathPending && !agent.hasPath && reissues < 10)
                    {
                        reissues++;
                        nav.CancelSeekPosition();
                        nav.SeekPosition(driveTarget, () => arrived = true);
                    }
                }
                yield return null;
            }

            nav.SetTaskBusy(false);
            agent.enabled = false;
            nav.enabled = false;
            if (!arrived)
                Debug.LogError($"[TrailerLoad] '{ds.name}' {phase}: never arrived at {driveTarget} (elapsed {elapsed:F1}s) — " +
                               "refusing to straight-line through pallets.");
            onDone?.Invoke(arrived);
        }
    }
}
