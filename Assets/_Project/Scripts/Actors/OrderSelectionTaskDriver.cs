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
    public partial class OrderSelectionTaskDriver : MonoBehaviour
    {
        private const float TaskPollInterval = 1f;
        private const float PickSecondsPerCase = 0.6f;

        /// <summary>Retry interval while a fully/partially-picked order has nowhere to stage (every
        /// lane full) — mirrors ReachTruckOperator's NoDestinationBackoff. Also the interval on which
        /// the held WorkTask's AssignedAtRealtime is refreshed, which is load-bearing: without it,
        /// WorkQueueSystem.ReleaseStaleAssignments (90s threshold) would eventually decide this claim
        /// is abandoned and hand it to a second selector — who would then build a SECOND pallet for
        /// the same order while this one is still physically carrying the first.</summary>
        private const float StagingRetryInterval = 15f;

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

        // ── Staging-space-unavailable retry state (see StagingRetryInterval) ──────────────────────
        private bool _waitingForStagingSpace;
        private float _stagingRetryTimer;
        private OrderData.OrderStatus _pendingCompletionStatus;

        private void Awake()
        {
            _nav = GetComponent<AiNavigation>();
            ServiceLocator.TryGet(out _workQueue);
            ServiceLocator.TryGet(out _orderService);
            ServiceLocator.TryGet(out _inventoryService);
        }

        private void Update()
        {
            if (_nav == null) return;

            // Holding already-picked pallets with nowhere to stage them — keep the claim alive
            // (see StagingRetryInterval) and retry on an interval instead of claiming new work.
            // Checked BEFORE the _taskInProgress early-return below since that flag stays true the
            // whole time this driver is waiting.
            if (_waitingForStagingSpace)
            {
                _stagingRetryTimer -= Time.deltaTime;
                if (_stagingRetryTimer > 0f) return;
                _stagingRetryTimer = StagingRetryInterval;

                if (_currentTask != null) _currentTask.AssignedAtRealtime = Time.realtimeSinceStartup;
                _waitingForStagingSpace = false; // FinishOrder re-sets this if still blocked
                // Re-claim the busy flag before the retry's SeekPosition call — cleared above so the
                // agent could patrol while waiting, but SeekPosition needs it set (same invariant the
                // rest of this task's lifecycle holds from initial claim through to FinishOrderCleanup)
                // or AiNavigation's own waypoint-progression fallback can hijack the destination before
                // the arrival callback fires.
                _nav.SetTaskBusy(true);
                FinishOrder(_pendingCompletionStatus);
                return;
            }

            // Keep a live claim fresh. A big order easily takes longer than the queue's 90 s stale-claim sweep,
            // which would otherwise hand it to a second selector mid-pick.
            if (_taskInProgress && _currentTask != null) _currentTask.AssignedAtRealtime = Time.realtimeSinceStartup;

            if (_taskInProgress) return;

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
            _pickFloorKey = long.MinValue;   // a new trip starts at the beginning of the sequence
            _nav.SetTaskBusy(true);
            // A left-behind case-picked pallet going back to its pick slots (OrderSelectionTaskDriver.Returns.cs).
            if (_currentTask.Type == WorkTaskType.Return) { StartReturnRun(); return; }
            // Pallet jack if one is free (see OrderSelectionTaskDriver.PalletJack.cs), otherwise by hand.
            if (TryStartJackRun()) return;
            AdvanceToNextPick();
        }

        /// <summary>Finds the next unpicked line item's best pick location and walks there; if the
        /// order is fully picked, completes the task instead. If nothing remaining is reachable,
        /// abandons the task in place (left Assigned, not retried) rather than looping forever —
        /// same philosophy as ReceivingTaskDriver's orphan handling.</summary>
        private void AdvanceToNextPick()
        {
            if (ReturnsRequested) { StartCoroutine(MidPickReturnRoutine()); return; }
            if (_currentOrder.IsFullyPicked)
            {
                if (HelperNotNeeded()) { EndUnneededHelperTrip(); return; }
                CompleteCurrentOrder();
                return;
            }

            if (!TryFindBestPickLocation(_currentOrder, out var location, out var lineItem, out int takeQty))
            {
                // Everything left is being fetched by other selectors on this order.
                if (HelperNotNeeded()) { EndUnneededHelperTrip(); return; }

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
                else if (_orderService.HasLivePalletPicks(_currentOrder))
                {
                    // Nothing for a CASE picker to take (the loose remainder has no stock), but Reach Trucks still owe whole pallets
                    // for this order. Sending it back to Open here would wipe its lane and strand those pallet picks with no
                    // destination, so the selector just finishes short on the loose cases and the order carries on as pallet picks.
                    Debug.LogWarning($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) has no loose cases to pick, " +
                                     $"but pallet picks are still outstanding - leaving the order released and letting the Reach Trucks deliver.");
                    FinishOrder(OrderData.OrderStatus.PartiallyPicked);
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

            // SeekPosition silently no-ops if a stale seek is already flagged — see the matching fix
            // in ReceivingTaskDriver/ReachTruckOperator/TrailerOffloadController for the same landmine.
            SeekPickSlot(location, lineItem, takeQty, 0);
        }

        private const float OnFootMaxPickReach = 5f;   // never take cases from a slot further than this from the selector

        /// <summary>Walks to the slot and only starts picking once the selector is actually close to it; a seek that
        /// ends short (partial path, grace-timeout) is retried, and after 3 misses the slot is skipped (Tad, 2026-10-21).</summary>
        private void SeekPickSlot(LocationData location, OrderLineItem lineItem, int takeQty, int attempt)
        {
            _nav.CancelSeekPosition();
            _nav.SeekPosition(location.WorldPosition, () =>
            {
                Vector3 a = transform.position, b = location.WorldPosition;
                a.y = 0f; b.y = 0f;
                if (Vector3.Distance(a, b) <= OnFootMaxPickReach)
                {
                    StartCoroutine(PickRoutine(location, lineItem, takeQty));
                    return;
                }
                if (attempt < 3) { SeekPickSlot(location, lineItem, takeQty, attempt + 1); return; }
                Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' could not get within {OnFootMaxPickReach:0}m of {location.Address} - skipping that pick.");
                AdvanceToNextPick();
            });
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
                if (ReturnsRequested) { StartCoroutine(MidPickReturnRoutine()); yield break; }
                yield return new WaitForSeconds(PickSecondsPerCase);

                if (!string.IsNullOrEmpty(location.PalletId))
                    _inventoryService.PickFromPallet(location.PalletId, 1);
                location.Pick(1);

                lineItem.QuantityPicked++;
                _orderService.NotePickedCase(_currentTask?.TaskId);
                var activePallet = _pallets[_pallets.Count - 1];
                activePallet.AddCase(lineItem.SkuId, sku.Prefab, caseDim, ti, hi, location.Address);

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
            bool onJack = OnJack;

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
            // Plan this pallet's whole picklist now (what will go on it until it cubes out) so the tooltip can show it
            // from the start. Cases already on this trip's earlier pallets are picked, so they're excluded already.
            string planTaskId = _currentTask != null ? _currentTask.TaskId : null;
            builder.SetPlannedPicklist(OrderPickPath.PlanPallet(_currentOrder,
                li => _orderService.SelectableRemaining(_currentOrder, li, planTaskId),
                id => _inventoryService.GetSkuData(id), _pickFloorKey));
            _pallets.Add(builder);
            if (onJack) SeatPalletOnJack(builder, index);

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

            MarkPartialUnlessLoading(_currentOrder);
            _cubingCapHit = true;
            FinishOrder();
        }

        /// <summary>Set when this trip stopped because the pallets were full, not because the order was done.</summary>
        private bool _cubingCapHit;

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
            int tripDoor = 0;
            string tripLane = null;

            if (order.EffectiveStagingDoor <= 0 || string.IsNullOrEmpty(order.AssignedLane))
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
            else if (!StagingLaneAssignmentService.TryFindTripLane(_orderService, _inventoryService, order,
                                                                   LivePalletCount(), out tripDoor, out tripLane))
            {
                failReason = $"no lane at Stage {order.EffectiveStagingDoor} or any nearby door has room for {LivePalletCount()} pallet(s) (every lane full, held by another customer, or an inbound trailer is docked)";
            }
            else if (!_inventoryService.TryFindStagingSlotInLane(tripDoor, tripLane, out var slot))
            {
                failReason = $"lane {tripDoor}{tripLane} reported room for {LivePalletCount()} pallet(s) but then handed back no slot";
            }
            else if (!LaneNamingService.TryGetSlotWorldPos(slot.Cell, out var pos))
            {
                failReason = $"slot {tripDoor}{tripLane}-{slot.Slot} resolved but has no computed world position yet (lane geometry not baked?)";
            }
            else
            {
                stagingPos = pos;
                if (LaneNamingService.TryGetLaneGeometry(slot.DoorNumber, slot.Lane, out var geo))
                    depthAxis = geo.DepthAxis;

                // Record where the pallets ACTUALLY land (possibly another lane or another door): loading
                // scans every lane in AllStagingLanes, so an unrecorded lane would strand them.
                if (!(tripDoor == order.EffectiveStagingDoor && tripLane == order.AssignedLane))
                {
                    Debug.Log($"[OrderSelectionTaskDriver] Order {order.OrderId} ({order.CustomerName}) overflowed " +
                              $"from {order.EffectiveStagingDoor}{order.AssignedLane} into {tripDoor}{tripLane}.");
                    order.AddOverflowLane(tripDoor, tripLane);
                }
            }

            if (stagingPos.HasValue)
            {
                Vector3 targetPos = stagingPos.Value;
                Vector3 axis = depthAxis;
                int stageDoor = tripDoor;
                string stageLane = tripLane;
                // Same silent-no-op guard as the pick-location seek above.
                _nav.CancelSeekPosition();
                _nav.SeekPosition(targetPos, () =>
                {
                    PlacePalletsAtStagingSlot(order, stageDoor, stageLane, targetPos, axis);
                    // Pallets are now physically sitting in a real staging lane slot — distinct
                    // from FullyPicked/PartiallyPicked, which only describe picking progress, not
                    // where the goods physically are. TrailerLoadController (D1) finds pallets to
                    // load by scanning position, not by this status, but it's the signal a later
                    // pass (billing, UI) can use to know an order is truck-ready.
                    order.Status = TripEndStatus(order, completionStatus, task);
                    FinishOrderCleanup(task);
                });
            }
            else
            {
                // Do NOT mark the order Staged/complete and do NOT drop the pallets — that used to
                // lie about the order's status and litter the aisles with untracked freight (see
                // TrailerLoadController's own scan, which never finds a dropped-in-place pallet).
                // The selector keeps carrying its pallet(s) and retries on a backoff, same honesty
                // as ReachTruckOperator.DeliverPalletToStagingLane's "no free lane" path — the player
                // can watch it patrol/wait and reassign the order to a different lane from the Work
                // Queue (or wait for the lane to clear) rather than being told it already shipped.
                Debug.LogWarning($"[OrderSelectionTaskDriver] Order {order.OrderId} ({order.CustomerName}) can't reach staging — {failReason}. Waiting with the pallet(s), retrying in {StagingRetryInterval:F0}s.");
                UIToast.Show($"Order Selector can't stage order for {order.CustomerName} — no free staging lane. Waiting to retry.");

                _pendingCompletionStatus = completionStatus;
                _waitingForStagingSpace = true;
                _stagingRetryTimer = StagingRetryInterval;
                _nav.SetTaskBusy(false);
                _nav.Patrol();
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
        private void PlacePalletsAtStagingSlot(OrderData order, int door, string lane, Vector3 fallbackBasePos, Vector3 depthAxis)
        {
            Vector3 axis = depthAxis.sqrMagnitude > 0.0001f ? depthAxis.normalized : Vector3.forward;
            // The lane this trip resolved (TryFindTripLane) - possibly an overflow lane at another door. It used to
            // be order.AssignedLane unconditionally, so an overflowed trip was dropped back into the full primary lane.
            if (door <= 0 || string.IsNullOrEmpty(lane)) { door = order != null ? order.EffectiveStagingDoor : 0; lane = order?.AssignedLane; }

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
                    _inventoryService.TryFindStagingSlotInLane(door, lane,
                                                               out var slot) &&
                    LaneNamingService.TryGetSlotWorldPos(slot.Cell, out var slotPos))
                {
                    // Same stacking rule the reach trucks and the inbound offload use: XZ from the
                    // slot, Y from the measured top of whatever is already in that cell. The previous
                    // pallet of this order has already been unparented by the time we get here, so it
                    // is counted and this one lands on top of it rather than through it.
                    float baseY = TrailerOffloadController.StagingDropBaseY(
                        door, lane, slot.Cell, pallet.gameObject);
                    pos = new Vector3(slotPos.x, baseY, slotPos.z);
                }
                else if (order != null)
                {
                    // Not cosmetic: this pallet is about to be left somewhere the loader does not scan,
                    // so it will not ship with the order. Loud on purpose.
                    Debug.LogError($"[OrderSelectionTaskDriver] No free slot in lane {door}" +
                                   $"{lane} for pallet {i + 1}/{_pallets.Count} of order {order.OrderId} " +
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

            // Another selector is still working this order: un-releasing it would pull the lane out from under them.
            if (_orderService.OtherSelectorsOnOrder(order, task) > 0 && _pallets.Sum(p => p != null ? p.TotalCases : 0) == 0)
            {
                EndUnneededHelperTrip();
                return;
            }

            // A continuation trip (see FinishOrderCleanup) on an order whose earlier trips already staged
            // freight: un-releasing it would wipe the lane those pallets sit in. Ship what's there instead.
            if (order.TotalUnitsPicked > 0)
            {
                foreach (var p in _pallets) if (p != null) Destroy(p.gameObject);
                _pallets.Clear();
                Debug.LogWarning($"[OrderSelectionTaskDriver] Order {order.OrderNumber} ({order.CustomerName}) has nothing left to pick " +
                                 $"on this trip; earlier trips staged {order.TotalUnitsPicked}/{order.TotalUnits} - it ships with that.");
                order.Status = TripEndStatus(order, OrderData.OrderStatus.Staged, task);
                FinishOrderCleanup(task);
                return;
            }

            order.Status = OrderData.OrderStatus.Pending;
            order.AssignedDoorNumber = 0;
            order.StagingDoorNumber = 0;
            order.ReleasedToFloor = false;
            order.AssignedLane = null;
            order.OverflowLanes.Clear();

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
            _orderService.ReleasePick(task?.TaskId);
            _workQueue.CompleteTask(task.TaskId);

            // MULTI-TRIP PICKING (2026-10-17 playtest): a selector carries at most MaxPalletsPerOrder pallets.
            // An order bigger than that used to ship short every time (a 439-case daily order shipped ~20%
            // and cost reputation each day). Now the rest goes back on the queue as another OrderSelect trip;
            // the order stays PartiallyPicked until the last trip stages, and AutoCloseLoadedOrders waits
            // while any OrderSelect task for it is live.
            //
            // SCRATCHES (Tad, 2026-10-18): a trip that ended with loose cases still owed because no pick face had any left
            // (the selector already took every last case from every reachable slot, down to zero) stages what it picked as
            // usual; the deficit becomes a brand new SCRATCHES pick list that must be picked before the truck departs,
            // unless the player cancels the rest of the order.
            // The route was closed while this trip was already heading for the lane: the pallets it just staged are
            // returned to their pick slots by RETURNS tasks (OrderService.CompleteReturnedOrder -> ReclaimOrphanedFreight).
            if (_currentOrder != null && _currentOrder.ReturnsMode)
            {
                _orderService.CompleteReturnedOrder(_currentOrder, task);
                _cubingCapHit = false;
                _currentTask = null;
                _currentOrder = null;
                _pallets.Clear();
                _taskInProgress = false;
                _nav.SetTaskBusy(false);
                _nav.Patrol();
                return;
            }

            bool moreToPick = _currentOrder != null &&
                              ((_cubingCapHit && _orderService.FileContinuationSelect(_currentOrder)) ||
                               _orderService.FileScratchSelect(_currentOrder));
            if (moreToPick)
            {
                // Freight already in the trailer: the order is LOADING, not back to picking. Dropping it to PartiallyPicked
                // made the dock schedule decide "nothing loaded" and close the appointment under a half-loaded trailer.
                string id = _currentOrder.OrderId;
                bool onTrailer = FindObjectsByType<OutboundPalletBuilder>().Any(p => p != null && p.OrderId == id && p.Lifecycle == PalletLifecycle.Shipped);
                _currentOrder.Status = onTrailer ? OrderData.OrderStatus.Loading : OrderData.OrderStatus.PartiallyPicked;
            }
            _cubingCapHit = false;

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
            if (task.Type == WorkTaskType.Return) return true;   // no order: see StartReturnRun

            string claimedOrderId = task.OrderId;
            order = _orderService.ActiveOrders.FirstOrDefault(o => o.OrderId == claimedOrderId);
            if (order == null)
            {
                Debug.LogWarning($"[OrderSelectionTaskDriver] Claimed task {task.TaskId} but no matching OrderData for OrderId {task.OrderId} — completing without selecting.");
                _workQueue.CompleteTask(task.TaskId);
                task = null;
                return false;
            }
            // Big order: offer another trip so an idle selector can join (OrderService.FileHelperSelectIfWorthwhile).
            _orderService.FileHelperSelectIfWorthwhile(order);
            return true;
        }

        /// <summary>Finds the next not-yet-picked line item that has a reachable pick location, and
        /// how many cases to take from it. Prefers the first assigned slot with enough quantity to
        /// fully satisfy the line item ("first available location with sufficient qty" per Tad's
        /// spec); if none has enough alone, falls back to the slot with the most stock so the order
        /// can still make progress — AdvanceToNextPick naturally revisits the remainder afterward,
        /// pulling from a second location if needed.</summary>
        /// <summary>The furthest point of the serpentine sequence this trip has reached; picks only go forward from here.</summary>
        private long _pickFloorKey = long.MinValue;

        private bool TryFindBestPickLocation(OrderData order, out LocationData location, out OrderLineItem lineItem, out int takeQty)
        {
            // Next pick = the earliest remaining slot in the serpentine sequence (aisle 1 bays up, aisle 2 down, ...),
            // across ALL line items - not line-item order, which zigzagged up and down the aisles (Tad, 2026-10-17).
            // On a BULK order most of a line is owed by PalletPick tasks; SelectableRemaining is what's genuinely left
            // for a case picker (for every other order it's just QuantityRemaining).
            // Cases other selectors on this order are already walking to are left out, and this selector reserves
            // its own target so they leave it alone (several selectors per order, 2026-10-17).
            string taskId = _currentTask != null ? _currentTask.TaskId : null;
            bool found = OrderPickPath.ChooseNext(order, li => _orderService.SelectableRemaining(order, li, taskId),
                                                  out location, out lineItem, out takeQty, _pickFloorKey);
            // ASCENDING ONLY (Tad, 2026-10-18): this trip never goes back up the sequence. Anything behind is left for the
            // next trip (a SCRATCHES list if nothing else picks it).
            if (found) _pickFloorKey = OrderPickPath.SequenceKey(location.Address);
            _orderService.ReservePick(taskId, order != null ? order.OrderId : null,
                                      found ? lineItem.SkuId : null, found ? takeQty : 0);
            return found;
        }

        /// <summary>This trip has nothing on its pallets and another selector is still working the order: it
        /// was a helper with nothing left to help with.</summary>
        private bool HelperNotNeeded()
            => _pallets.Sum(p => p != null ? p.TotalCases : 0) == 0 &&
               _orderService.OtherSelectorsOnOrder(_currentOrder, _currentTask) > 0;

        /// <summary>Ends a helper trip that found nothing to pick: no status change, no continuation.</summary>
        private void EndUnneededHelperTrip()
        {
            Debug.Log($"[OrderSelectionTaskDriver] '{name}': nothing left to help with on order {_currentOrder?.OrderNumber} - other selectors have it.");
            foreach (var p in _pallets) if (p != null) Destroy(p.gameObject);
            _pallets.Clear();
            _cubingCapHit = false;
            FinishOrderCleanup(_currentTask);
        }

        /// <summary>
        /// The order status a finishing trip leaves behind. With several selectors on one order the FIRST to
        /// finish must not call it Staged while others still carry cases, and a trailer that is already loading
        /// stays Loading (more pallets just landed for it). Only the last selector out sets the real result.
        /// </summary>
        private OrderData.OrderStatus TripEndStatus(OrderData order, OrderData.OrderStatus wanted, WorkTask mine)
        {
            if (order == null) return wanted;
            if (order.Status == OrderData.OrderStatus.Loading || order.Status == OrderData.OrderStatus.Loaded)
                return OrderData.OrderStatus.Loading;
            if (_orderService.OtherSelectorsOnOrder(order, mine) > 0) return OrderData.OrderStatus.PartiallyPicked;
            return wanted;
        }

        /// <summary>Marks picking as partial without knocking a loading order back out of Loading.</summary>
        private static void MarkPartialUnlessLoading(OrderData order)
        {
            if (order == null) return;
            if (order.Status == OrderData.OrderStatus.Loading || order.Status == OrderData.OrderStatus.Loaded) return;
            order.Status = OrderData.OrderStatus.PartiallyPicked;
        }
    }
}
