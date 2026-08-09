using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCore.Services;
using GameCore.Labor;
using GameCore.Inventory;

namespace GameCore.Actors
{
    /// <summary>
    /// Drives the "Order Selection" assignment for a dynamically-assigned employee — same shape as
    /// ReceivingTaskDriver (poll WorkQueueSystem, claim a task, walk there via AiNavigation.SeekPosition).
    ///
    /// SCOPE (fourth milestone): claims a pending OrderSelect task, walks to the best pick location
    /// for each not-yet-picked line item in turn, grabs cases onto a growing pallet that rides along
    /// with the selector. When a pallet "cubes out" (Ti*Hi-derived fill fraction hits 100%, standing
    /// in for the ~40x48x72" real-world cubing spec — no cubic-footage field exists on SkuData), a
    /// second pallet starts if the order isn't done yet; once BOTH pallets are cubed or the order is
    /// fully picked (whichever comes first), the selector delivers the pallet(s) to the order's
    /// assigned door's outbound staging lane (walking to the deepest open slot, same spot a real
    /// jack would back all the way into) before completing the task. No equipment/jack model yet —
    /// pallets are placeholder passengers on the selector, parented directly to them, until a later
    /// pass wires up real jack-carrying and an actual reverse-in animation.
    /// </summary>
    public class OrderSelectionTaskDriver : MonoBehaviour
    {
        private const float TaskPollInterval = 1f;
        private const float PickSecondsPerCase = 0.6f;

        /// <summary>A selector's jack carries exactly 2 empty CHEPs (one front, one back) per Tad's
        /// spec — never more, regardless of how much of the order is still unpicked.</summary>
        private const int MaxPalletsPerOrder = 2;

        private static readonly Vector3[] PalletOffsets = { new Vector3(0.9f, 0f, 0.6f), new Vector3(0.9f, 0f, -0.6f) };

        private AiNavigation _nav;
        private WorkQueueSystem _workQueue;
        private OrderService _orderService;
        private InventoryService _inventoryService;

        private float _pollTimer;
        private bool _taskInProgress;

        private WorkTask _currentTask;
        private OrderData _currentOrder;
        private readonly List<OutboundPalletBuilder> _pallets = new();

        private void Awake()
        {
            _nav = GetComponent<AiNavigation>();
            ServiceLocator.TryGet(out _workQueue);
            ServiceLocator.TryGet(out _orderService);
            ServiceLocator.TryGet(out _inventoryService);
        }

        private void Update()
        {
            if (_taskInProgress || _nav == null) return;

            if (_workQueue == null)
            {
                ServiceLocator.TryGet(out _workQueue);
                if (_workQueue == null) return;
            }
            if (_orderService == null)
            {
                ServiceLocator.TryGet(out _orderService);
                if (_orderService == null) return;
            }
            if (_inventoryService == null)
            {
                ServiceLocator.TryGet(out _inventoryService);
                if (_inventoryService == null) return;
            }

            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = TaskPollInterval;

            if (!TryClaimNextOrderTask(out _currentTask, out _currentOrder))
                return;

            _taskInProgress = true;
            _nav.SetTaskBusy(true);
            AdvanceToNextPick();
        }

        /// <summary>Finds the next unpicked line item's best pick location and walks there; if the
        /// order is fully picked, completes the task instead. If nothing remaining is reachable,
        /// abandons the task in place (left Assigned, not retried) rather than looping forever —
        /// same philosophy as ReceivingTaskDriver's orphan handling.</summary>
        private void AdvanceToNextPick()
        {
            if (_currentOrder.IsFullyPicked)
            {
                CompleteCurrentOrder();
                return;
            }

            if (!TryFindBestPickLocation(_currentOrder, out var location, out var lineItem, out int takeQty))
            {
                // A bulk order's case picker is DONE the moment the only thing left is full pallets —
                // that work belongs to the Reach Trucks. Without this the selector would read the
                // pallet quantities as unreachable stock and report a short pick on a healthy order.
                // The order stays PartiallyPicked; whichever pallet pick lands last flips it to Staged
                // (OrderService.NotePalletPicked).
                if (_orderService.RemainingIsAllPalletPick(_currentOrder))
                {
                    Debug.Log($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) " +
                              $"— loose cases done ({_currentOrder.TotalUnitsPicked}/{_currentOrder.TotalUnits}); the " +
                              $"balance is full pallets on the Reach Trucks. Delivering to staging.");
                    FinishOrder(OrderData.OrderStatus.PartiallyPicked);
                    return;
                }

                // No reachable location for any remaining line item — genuinely out of stock, not a
                // transient miss. Rather than stranding the WIP pallet wherever the selector happens
                // to be standing (the old behavior — permanent aisle debris, and the WorkTask sat
                // Assigned forever until the stale-assignment sweep freed it for another selector to
                // re-claim and build a *second* pallet from scratch), the order takes one of two
                // exits depending on whether anything was actually picked. Neither is a backorder:
                // this warehouse ships short or the player calls the order off.
                int pickedSoFar = _pallets.Sum(p => p != null ? p.TotalCases : 0);
                if (pickedSoFar > 0)
                {
                    // Short pick, not a backorder — this warehouse ships what it has. The cases that
                    // made it onto a pallet go to staging exactly like a full order and the shortfall
                    // shows in the Work Queue's Fill Rate column; billing already charges only for
                    // QuantityPicked (see OrderService.ShipOrder), so a 16/23 order bills 16.
                    Debug.LogWarning($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) has no reachable pick location for its remaining line item(s) — staging short at {_currentOrder.TotalUnitsPicked}/{_currentOrder.TotalUnits} case(s).");
                    FinishOrder();
                }
                else
                {
                    // Nothing picked at all, so nothing is physically committed anywhere: hand the
                    // order back to the player rather than parking it in a status with no way out.
                    // It returns to Open (unreleased) and RELEASES ITS STAGE, so the door isn't held
                    // hostage by an order that never put a pallet in it. The player then decides —
                    // cancel it, or chase the stock down and release it again.
                    Debug.LogWarning($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage {_currentOrder.AssignedDoorNumber}.");
                    ReturnOrderToOpen();
                }
                return;
            }

            _nav.SeekPosition(location.WorldPosition, () => StartCoroutine(PickRoutine(location, lineItem, takeQty)));
        }

        /// <summary>Grabs `takeQty` cases one at a time (a short pause per case for pacing), updating
        /// both the pick face's local stock and the master pallet record, then adds each case to the
        /// active outbound pallet. Stops early — mid-line-item if need be — the instant the order
        /// finishes or both pallets cube out, per Tad's "whichever occurs first" spec.</summary>
        private IEnumerator PickRoutine(LocationData location, OrderLineItem lineItem, int takeQty)
        {
            EnsurePallet();

            var sku = _inventoryService.GetSkuData(lineItem.SkuId);
            if (sku == null || sku.Prefab == null)
            {
                Debug.LogWarning($"[OrderSelectionTaskDriver] SKU {lineItem.SkuId} missing SkuData/prefab — skipping this pick at {location.Address}.");
                AdvanceToNextPick();
                yield break;
            }

            Vector3 caseDim = new Vector3(sku.CaseWidth, sku.CaseHeight, sku.CaseLength);
            int ti = Mathf.Max(1, sku.Ti);
            int hi = Mathf.Max(1, sku.Hi);

            for (int i = 0; i < takeQty; i++)
            {
                yield return new WaitForSeconds(PickSecondsPerCase);

                if (!string.IsNullOrEmpty(location.PalletId))
                    _inventoryService.PickFromPallet(location.PalletId, 1);
                location.Pick(1);

                lineItem.QuantityPicked++;
                var activePallet = _pallets[_pallets.Count - 1];
                activePallet.AddCase(lineItem.SkuId, sku.Prefab, caseDim, ti, hi);

                // Order-complete takes priority over the cubing cap if both land on the same case —
                // the order data itself is the source of truth, not which condition we happened to
                // notice first.
                if (_currentOrder.IsFullyPicked)
                {
                    CompleteCurrentOrder();
                    yield break;
                }

                if (activePallet.IsCubedOut)
                {
                    if (_pallets.Count < MaxPalletsPerOrder)
                    {
                        StartNextPallet();
                    }
                    else
                    {
                        FinishDueToCubingCap();
                        yield break;
                    }
                }
            }

            AdvanceToNextPick();
        }

        /// <summary>Ensures at least one outbound WIP pallet exists for this order (the first one).</summary>
        private void EnsurePallet()
        {
            if (_pallets.Count == 0) StartNextPallet();
        }

        /// <summary>Spawns an additional outbound WIP pallet (up to MaxPalletsPerOrder), riding along
        /// with the selector at a fixed offset. Placeholder until Phase C's jack-carrying replaces
        /// "parented to the selector" with "parented to the jack".</summary>
        private void StartNextPallet()
        {
            var prefab = ResolveChepEmptyPrefab();
            if (prefab == null)
            {
                Debug.LogError("[OrderSelectionTaskDriver] No buildable single-pallet prefab found in the Inventory build menu registry — cannot start an outbound pallet.");
                return;
            }

            int index = _pallets.Count;
            var go = Instantiate(prefab);
            go.name = $"OutboundPallet_{_currentOrder.OrderId}_{index + 1}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = PalletOffsets[Mathf.Min(index, PalletOffsets.Length - 1)];
            go.transform.localRotation = Quaternion.identity;

            // This prefab is a build-menu placeable item (PlacedObject/BuildingData) with its own
            // PalletBuilder — none of that applies to a transient outbound WIP pallet, which must
            // not self-register into the grid at (0,0) and must not have its cases wiped by an
            // unrelated Build() call from elsewhere.
            var existingBuilder = go.GetComponent<PalletBuilder>();
            if (existingBuilder != null) Destroy(existingBuilder);
            var po = go.GetComponent<PlacedObject>();
            if (po != null) { po.enabled = false; Destroy(po); }
            var bd = go.GetComponent<BuildingData>();
            if (bd != null) Destroy(bd);
            var bh = go.GetComponent<BuildingHighlighter>();
            if (bh != null) Destroy(bh);

            var builder = go.AddComponent<OutboundPalletBuilder>();
            builder.OrderId = _currentOrder.OrderId;
            _pallets.Add(builder);

            if (index > 0)
                Debug.Log($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) pallet #{index + 1} started — pallet #{index} cubed out.");
        }

        /// <summary>Unparents every pallet built for the current order (world-position-preserving)
        /// so they're left behind instead of continuing to ride along with the employee.</summary>
        private void UnparentAllPallets()
        {
            foreach (var pallet in _pallets)
            {
                if (pallet == null) continue;
                pallet.transform.SetParent(null, true);
                // These are the pallets dropped wherever the selector happened to be standing when
                // staging was unreachable. They are obstructions in an aisle — they must carve, or
                // every agent walks through the pile.
                pallet.SetNavObstacleActive(true);
            }
        }

        /// <summary>Resolves the single-pallet "ChepEmpty" prefab via the Inventory build menu
        /// registry — same resolution TestPalletSpawner uses. Do NOT Resources.Load a pallet by
        /// name: multiple ambiguous "ChepEmpty" assets live under different Resources/ folders.</summary>
        private static GameObject ResolveChepEmptyPrefab()
        {
            var buildMenu = FindAnyObjectByType<BuildMenuUI>();
            var registry = buildMenu != null ? buildMenu.registry : null;
            if (registry == null)
            {
                var all = Resources.FindObjectsOfTypeAll<ObjDataRegistry>();
                registry = all.Length > 0 ? all[0] : null;
            }
            if (registry == null) return null;

            foreach (var so in registry.buttonSOs)
            {
                if (so == null || so.prefab == null) continue;
                if (so.category != "Inventory") continue;
                if (so.prefab.GetComponent<PalletBuilder>() == null) continue;
                return so.prefab;
            }
            return null;
        }

        private void CompleteCurrentOrder()
        {
            int caseCount = _pallets.Sum(p => p != null ? p.TotalCases : 0);
            Debug.Log($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) fully picked — {caseCount} case(s) across {_pallets.Count} pallet(s). Delivering to staging.");

            _orderService.MarkOrderFulfilled(_currentOrder.OrderId);
            FinishOrder();
        }

        /// <summary>Both pallets (the jack's full physical capacity) are cubed out and the order
        /// still has unpicked units left — per Tad's spec the order is done regardless, "whichever
        /// occurs first". The selector's task is complete; what happens to the unfulfilled remainder
        /// (backorder, partial ship, etc.) is a separate concern for a later milestone — OrderData
        /// already has a PartiallyPicked status and an OnOrderCancelled hook reserved for exactly
        /// this kind of follow-up.</summary>
        private void FinishDueToCubingCap()
        {
            int caseCount = _pallets.Sum(p => p != null ? p.TotalCases : 0);
            Debug.Log($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) stopped at {MaxPalletsPerOrder} cubed-out pallet(s) — {caseCount} case(s), {_currentOrder.TotalUnitsPicked}/{_currentOrder.TotalUnits} units picked. Selector's capacity is full; delivering to staging.");

            _currentOrder.Status = OrderData.OrderStatus.PartiallyPicked;
            FinishOrder();
        }

        /// <summary>Shared tail for both a fully-picked order and the cubing-cap cutoff: deliver the
        /// pallet(s) to this order's assigned door's outbound staging lane — walking to the deepest
        /// open slot (the spot a real jack would back all the way into, per Tad's spec), detaching
        /// them there — then complete the WorkTask and resume patrol. Falls back to detaching in
        /// place if no staging slot is available right now (no door was assigned at creation, or
        /// every outbound lane at that door is currently full) rather than getting stuck; Phase D can
        /// revisit orders that landed here once loading exists.</summary>
        private void FinishOrder(OrderData.OrderStatus completionStatus = OrderData.OrderStatus.Staged)
        {
            var task = _currentTask;
            var order = _currentOrder;

            Vector3? stagingPos = null;
            Vector3 depthAxis = Vector3.forward;
            string failReason = null;

            if (order.AssignedDoorNumber <= 0 || string.IsNullOrEmpty(order.AssignedLane))
            {
                failReason = "order was never released to a staging lane (AssignedDoorNumber/AssignedLane unset — check the Work Queue panel release)";
            }
            // Search the whole STAGE, not just the one lane this order was released to: start in its
            // own lane and overflow into the next lane of the same door (A → B → C) as each fills.
            // Before this, a full lane meant the pallets were dropped wherever the selector happened
            // to be standing, which is what littered the aisles.
            //
            // The lane is resolved ONCE here, for the whole order, and every pallet goes in it — the
            // overflow decision must not be made per-pallet. AssignedLane can only name one lane, so a
            // pallet that overflowed into a second lane was invisible to ReleaseOrdersToLoading and
            // TrailerLoadController: it sat in the lane after the truck left, still billed, with no
            // task left to move it (a Shipped order files none). See
            // InventoryService.TryFindStagingLaneForPallets.
            else if (!_inventoryService.TryFindStagingLaneForPallets(order.AssignedDoorNumber, order.AssignedLane,
                                                                    LivePalletCount(), out string resolvedLane))
            {
                failReason = $"no lane in Stage {order.AssignedDoorNumber} has room for all {LivePalletCount()} of this order's pallets (every lane full or too tight, or none allows outbound picking)";
            }
            else if (!_inventoryService.TryFindStagingSlotInLane(order.AssignedDoorNumber, resolvedLane, out var slot))
            {
                failReason = $"lane {order.AssignedDoorNumber}{resolvedLane} reported room for {LivePalletCount()} pallet(s) but then handed back no slot";
            }
            else if (!LaneNamingService.TryGetSlotWorldPos(slot.Cell, out var pos))
            {
                failReason = $"slot {order.AssignedDoorNumber}{resolvedLane}-{slot.Slot} resolved but has no computed world position yet (lane geometry not baked?)";
            }
            else
            {
                stagingPos = pos;
                if (LaneNamingService.TryGetLaneGeometry(slot.DoorNumber, slot.Lane, out var geo))
                    depthAxis = geo.DepthAxis;

                // Record where the pallets ACTUALLY landed. If this order overflowed out of the lane
                // it was released to, AssignedLane has to follow — it's what ReleaseOrdersToLoading
                // groups by and what TrailerLoadController scans to find the staged pallets. Leaving
                // it pointing at the original lane would strand the overflowed pallets.
                if (!string.IsNullOrEmpty(resolvedLane) && resolvedLane != order.AssignedLane)
                {
                    Debug.Log($"[OrderSelectionTaskDriver] Order {order.OrderId} ({order.CustomerName}) overflowed " +
                              $"from {order.AssignedDoorNumber}{order.AssignedLane} into {order.AssignedDoorNumber}{resolvedLane}.");
                    order.AssignedLane = resolvedLane;
                }
            }

            if (stagingPos.HasValue)
            {
                Vector3 targetPos = stagingPos.Value;
                Vector3 axis = depthAxis;
                _nav.SeekPosition(targetPos, () =>
                {
                    PlacePalletsAtStagingSlot(order, targetPos, axis);
                    // Pallets are now physically sitting in a real staging lane slot — distinct
                    // from FullyPicked/PartiallyPicked, which only describe picking progress, not
                    // where the goods physically are. TrailerLoadController (D1) finds pallets to
                    // load by scanning position, not by this status, but it's the signal a later
                    // pass (billing, UI) can use to know an order is truck-ready.
                    order.Status = completionStatus;
                    FinishOrderCleanup(task);
                });
            }
            else
            {
                Debug.LogWarning($"[OrderSelectionTaskDriver] Order {order.OrderId} ({order.CustomerName}) can't reach staging — {failReason}. Leaving pallet(s) where the selector currently stands.");
                order.Status = completionStatus;
                UnparentAllPallets();
                FinishOrderCleanup(task);
            }
        }

        /// <summary>How many pallets this order actually still has to put down. Drives the "does this
        /// lane have room for the whole order" check — a destroyed/null entry must not reserve a slot.</summary>
        private int LivePalletCount()
        {
            int n = 0;
            for (int i = 0; i < _pallets.Count; i++)
                if (_pallets[i] != null) n++;
            return n;
        }

        /// <summary>
        /// Detaches every pallet built for this order into its OWN staging slot.
        ///
        /// This used to drop them all relative to one resolved slot, offset 1.2m apart along the lane
        /// axis. A cell is 1.33m, so the sibling landed INSIDE the same cell's claim radius: both
        /// pallets mapped to one nearest-slot in InventoryService.OutboundOccupiedCells, only that one
        /// cell ever read as occupied, and the next order staged into this lane was handed a slot that
        /// already had a pallet sitting across it — pallets staged inside each other.
        ///
        /// Each pallet now resolves its own free slot, and does so AFTER the previous one has been
        /// unparented, so the occupancy scan (which ignores pallets still riding a selector) can see
        /// it and hand back the next slot along.
        ///
        /// Those per-pallet lookups are confined to order.AssignedLane, which FinishOrder has already
        /// chosen with room for the whole order. They must never overflow into another lane: the order
        /// records one lane, and a pallet outside it never gets loaded or cleared.
        /// </summary>
        private void PlacePalletsAtStagingSlot(OrderData order, Vector3 fallbackBasePos, Vector3 depthAxis)
        {
            Vector3 axis = depthAxis.sqrMagnitude > 0.0001f ? depthAxis.normalized : Vector3.forward;

            for (int i = 0; i < _pallets.Count; i++)
            {
                var pallet = _pallets[i];
                if (pallet == null) continue;

                Vector3 pos = fallbackBasePos + axis * (i * 1.4f);
                // Locked to order.AssignedLane — NOT TryFindStagingSlotAtDoor, which would overflow
                // this pallet into a neighbouring lane that the order has no way to record. FinishOrder
                // already picked a lane with room for every pallet, so this lookup is expected to
                // succeed for all of them.
                if (order != null && _inventoryService != null &&
                    _inventoryService.TryFindStagingSlotInLane(order.AssignedDoorNumber, order.AssignedLane,
                                                               out var slot) &&
                    LaneNamingService.TryGetSlotWorldPos(slot.Cell, out var slotPos))
                {
                    pos = slotPos;
                }
                else if (order != null)
                {
                    // Not cosmetic: this pallet is about to be left somewhere the loader does not scan,
                    // so it will not ship with the order. Loud on purpose.
                    Debug.LogError($"[OrderSelectionTaskDriver] No free slot in lane {order.AssignedDoorNumber}" +
                                   $"{order.AssignedLane} for pallet {i + 1}/{_pallets.Count} of order {order.OrderId} " +
                                   $"({order.CustomerName}) — dropping it beside the last one, where the loader will " +
                                   $"NOT find it. The lane was verified to have room before placement began, so " +
                                   $"something claimed a slot mid-drop.");
                }

                pallet.transform.SetParent(null, true);
                pallet.transform.position = pos;
                pallet.transform.rotation = Quaternion.LookRotation(axis, Vector3.up);
                // On the ground now — start carving so MHE and humanoids path around it instead of
                // straight through it. Also what makes the NEXT iteration's slot lookup see this cell
                // as taken, so siblings can't be handed the same slot.
                pallet.SetNavObstacleActive(true);
            }
        }

        /// <summary>Un-releases an order the selector couldn't pick a single case for: its OrderSelect
        /// task goes back to Open (as if never released) and its door/lane stamp is cleared so the
        /// stage it was holding frees up immediately. Deliberately NOT CompleteTask — the work still
        /// needs doing once the stock exists; the player re-releases it from the Work Queue, or
        /// cancels it. This is the replacement for the old Backorder dead end, which left the order
        /// invisible in the panel while it silently owned a whole door.</summary>
        private void ReturnOrderToOpen()
        {
            var task = _currentTask;
            var order = _currentOrder;

            order.Status = OrderData.OrderStatus.Pending;
            order.AssignedDoorNumber = 0;
            order.AssignedLane = null;

            if (task != null)
            {
                task.Status = WorkTaskStatus.Open;
                task.AssignedToEmployeeGuid = null;
            }

            UnparentAllPallets();

            _currentTask = null;
            _currentOrder = null;
            _pallets.Clear();

            _taskInProgress = false;
            _nav.SetTaskBusy(false);
            _nav.Patrol();
        }

        private void FinishOrderCleanup(WorkTask task)
        {
            _workQueue.CompleteTask(task.TaskId);

            _currentTask = null;
            _currentOrder = null;
            _pallets.Clear();

            _taskInProgress = false;
            _nav.SetTaskBusy(false);
            _nav.Patrol();
        }

        /// <summary>Claims the oldest pending OrderSelect task (simple FIFO — unlike Receiving's
        /// exit-first lane ordering, there's no equivalent physical constraint here) and resolves it
        /// to the real OrderData via WorkTask.OrderId.</summary>
        private bool TryClaimNextOrderTask(out WorkTask task, out OrderData order)
        {
            task = null;
            order = null;

            var identity = GetComponent<EmployeeIdentity>();
            string guid = identity?.Record?.employeeGuid;
            if (string.IsNullOrEmpty(guid)) return false;

            if (!_workQueue.TryClaimNextTask(EmployeeRole.OrderSelector, guid, out task))
                return false;

            // Lambdas can't close over an out/ref parameter of the enclosing method — copy to a
            // local first (task itself is unaffected, still the same claimed WorkTask).
            string claimedOrderId = task.OrderId;
            order = _orderService.ActiveOrders.FirstOrDefault(o => o.OrderId == claimedOrderId);
            if (order == null)
            {
                Debug.LogWarning($"[OrderSelectionTaskDriver] Claimed task {task.TaskId} but no matching OrderData for OrderId {task.OrderId} — completing without selecting.");
                _workQueue.CompleteTask(task.TaskId);
                task = null;
                return false;
            }
            return true;
        }

        /// <summary>Finds the next not-yet-picked line item that has a reachable pick location, and
        /// how many cases to take from it. Prefers the first assigned slot with enough quantity to
        /// fully satisfy the line item ("first available location with sufficient qty" per Tad's
        /// spec); if none has enough alone, falls back to the slot with the most stock so the order
        /// can still make progress — AdvanceToNextPick naturally revisits the remainder afterward,
        /// pulling from a second location if needed.</summary>
        private bool TryFindBestPickLocation(OrderData order, out LocationData location, out OrderLineItem lineItem, out int takeQty)
        {
            location = null;
            lineItem = null;
            takeQty = 0;

            foreach (var li in order.LineItems)
            {
                if (li.IsFullyPicked) continue;

                // On a BULK order most of this line is owed by outstanding PalletPick tasks, and a
                // selector must not walk those off by hand — 620 cases means ten Reach Truck pallets
                // and twenty cases here, not 620 case picks. SelectableRemaining is what's genuinely
                // left for a case picker; for every other order it's just QuantityRemaining.
                int want = _orderService.SelectableRemaining(order, li);
                if (want <= 0) continue;

                // Slot choice lives in OrderPickPath so the Work Queue can preview an order's route
                // using the identical rule — see that class for why it isn't duplicated here.
                var best = OrderPickPath.ChooseSlot(li.SkuId, want, out int qty);
                if (best == null) continue;

                location = best;
                lineItem = li;
                takeQty = qty;
                return true;
            }
            return false;
        }
    }
}
