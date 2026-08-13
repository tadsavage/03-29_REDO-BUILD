using GameCore.Services;
using GameCore.Events;
using GameCore.Economy;
using GameCore.Labor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Manages the lifecycle of customer orders: arrival, assignment to employees, and fulfillment.
    /// Works with InventoryService to identify stock locations and allocate units.
    /// </summary>
    public class OrderService : IService
    {
        private readonly List<OrderData> _activeOrders = new();
        private readonly Dictionary<string, List<PickingTask>> _assignedTasks = new();
        
        private InventoryService _inventoryService;
        private SimulationTimeService _timeService;
        private EventManager _eventManager;
        private WorkQueueSystem _workQueue;
        private MoneyService _moneyService;

        // Events
        public static event System.Action<OrderData> OnOrderArrived;
        public static event System.Action<OrderData> OnOrderFulfilled;
        public static event System.Action<OrderData> OnOrderShipped;

        /// <summary>Raised by <see cref="CancelOrders"/> when the player calls an order off.</summary>
        public static event System.Action<OrderData> OnOrderCancelled;

        /// <summary>Raised once per order the day it first goes overdue, with the dollar amount
        /// charged. Exists so the contract that produced the order can count it against that
        /// account's on-time record without OrderService having to know contracts exist.</summary>
        public static event System.Action<OrderData, int> OnOrderFined;

        /// <summary>Orders that still need something from the player or the workforce. A finished
        /// order leaves this list the moment it goes terminal — see Archive.</summary>
        public IReadOnlyList<OrderData> ActiveOrders => _activeOrders;

        /// <summary>Finished orders (Shipped or Cancelled), most recent last. Kept for reporting;
        /// nothing operational should read this.</summary>
        public IReadOnlyList<OrderData> OrderHistory => _orderHistory;

        private readonly List<OrderData> _orderHistory = new();

        /// <summary>
        /// Hard cap on retained finished orders — oldest dropped first once exceeded.
        ///
        /// Bounded by COUNT rather than by age because OrderData records no closed-on day, and adding
        /// one would mean changing OrderSnapshot's schema. Count is the honest thing to bound by here:
        /// the reason this cap exists is save size and scan cost, both of which track the number of
        /// records, not their age.
        /// </summary>
        private const int MaxArchivedOrders = 250;

        public void Initialize()
        {
            _inventoryService = ServiceLocator.Get<InventoryService>();
            _timeService = ServiceLocator.Get<SimulationTimeService>();
            _eventManager = EventManager.Instance;
            _workQueue = ServiceLocator.Get<WorkQueueSystem>();
            _moneyService = ServiceLocator.Get<MoneyService>();

            if (_eventManager != null)
            {
                _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            }
        }

        public void Shutdown()
        {
            if (_eventManager != null)
            {
                _eventManager.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            }
            _activeOrders.Clear();
            _assignedTasks.Clear();
        }

        /// <summary>Add a new order to the system and file its WorkTask in the Open status — not yet
        /// claimable by an Order Selector until the player releases it to a staging lane via the
        /// Work Queue panel (see ReleaseOrdersToLane), which is what actually routes it to a
        /// door/lane. One task per ORDER (not per line item) — a single selector works the whole
        /// order continuously (walking between picks, building pallets) the same way a Receiver
        /// works a whole lane, rather than splitting one order across multiple claimants.</summary>
        public void ReceiveOrder(OrderData order)
        {
            _activeOrders.Add(order);
            OnOrderArrived?.Invoke(order);
            Debug.Log($"[OrderService] New Order received: {order.OrderId} from {order.CustomerName} ({order.TotalUnits} units) — awaiting release to a staging lane.");

            if (order.IsBulk)
            {
                FileBulkTasks(order);
                return;
            }

            var task = _workQueue?.CreateTask(
                WorkTaskType.OrderSelect,
                EmployeeRole.OrderSelector,
                palletId: null, // no single pallet — the selector builds one/two FOR this order
                description: $"Select order for {order.CustomerName} ({order.TotalUnits} units)",
                orderId: order.OrderId);
            if (task != null) task.Status = WorkTaskStatus.Open;
        }

        /// <summary>
        /// Files the work for a BULK order: one PalletPick per whole pallet, plus a single OrderSelect
        /// covering every loose case left over across the whole order.
        /// </summary>
        /// <remarks>
        /// 620 cases of a 60-per-pallet SKU is ten Reach Truck trips and one selector walking off
        /// twenty cases — not eleven pallets, and not 620 cases picked by hand.
        ///
        /// ONE OrderSelect for the whole order rather than one per line, matching the non-bulk rule:
        /// a selector works an order continuously, and two selectors on one order would build pallets
        /// against each other.
        ///
        /// A SKU with no committed Ti/Hi can't express a full pallet, so its whole line falls through
        /// to the case picker. That's the honest failure — it keeps the order fillable instead of
        /// filing pallet picks that could never resolve a source pallet.
        /// </remarks>
        private void FileBulkTasks(OrderData order)
        {
            int palletTasks = 0;
            int looseCases = 0;

            foreach (var li in order.LineItems)
            {
                int fullPallet = FullPalletCases(li.SkuId);
                if (fullPallet <= 0)
                {
                    Debug.LogWarning($"[OrderService] SKU {li.SkuId} on bulk order {order.OrderId} has no " +
                                     $"committed Ti/Hi — its {li.QuantityNeeded} case(s) fall back to a case pick.");
                    looseCases += li.QuantityNeeded;
                    continue;
                }

                int pallets = li.QuantityNeeded / fullPallet;
                looseCases += li.QuantityNeeded % fullPallet;

                var area = _inventoryService?.GetSkuData(li.SkuId)?.StorageArea ?? PalletData.AreaCategory.Grocery;
                for (int i = 0; i < pallets; i++)
                {
                    // PalletId and FromLocation stay null: the source reserve pallet is chosen when a
                    // Reach Truck claims this, not now. See WorkTask.SkuId.
                    var pt = _workQueue?.CreateTask(
                        WorkTaskType.PalletPick,
                        EmployeeRole.ReachTruckOperator,
                        palletId: null,
                        description: $"Pallet pick — {fullPallet} cs of {li.SkuId} for {order.CustomerName}",
                        area: area,
                        orderId: order.OrderId,
                        skuId: li.SkuId);
                    if (pt != null) pt.Status = WorkTaskStatus.Open;
                    palletTasks++;
                }
            }

            if (looseCases > 0)
            {
                var st = _workQueue?.CreateTask(
                    WorkTaskType.OrderSelect,
                    EmployeeRole.OrderSelector,
                    palletId: null,
                    description: $"Select {looseCases} loose case(s) for {order.CustomerName}",
                    orderId: order.OrderId);
                if (st != null) st.Status = WorkTaskStatus.Open;
            }

            Debug.Log($"[OrderService] BULK order {order.OrderId} ({order.CustomerName}): filed {palletTasks} " +
                      $"pallet pick(s)" + (looseCases > 0 ? $" and 1 case pick for {looseCases} loose case(s)." : "."));
        }

        /// <summary>Cases in one full pallet of this SKU (Ti x Hi), or 0 if it has no committed pallet
        /// maths. The single definition of "a full pallet" on the outbound side.</summary>
        public int FullPalletCases(string skuId)
        {
            var sku = _inventoryService?.GetSkuData(skuId);
            if (sku == null || sku.Ti <= 0 || sku.Hi <= 0) return 0;
            return sku.Ti * sku.Hi;
        }

        /// <summary>Every not-yet-finished task belonging to an order, of any type. Bulk orders carry
        /// several (N pallet picks and maybe a case pick), so anything that used to find "the order's
        /// task" with FirstOrDefault has to come through here or it will see only one of them.</summary>
        public IEnumerable<WorkTask> LiveTasksForOrder(string orderId)
            => _workQueue == null || string.IsNullOrEmpty(orderId)
             ? Enumerable.Empty<WorkTask>()
             : _workQueue.Tasks.Where(t => t.OrderId == orderId
                                        && t.Status != WorkTaskStatus.Complete
                                        && t.Status != WorkTaskStatus.Cancelled);

        /// <summary>
        /// Cases on this order that outstanding PalletPick tasks are going to deliver for a SKU.
        ///
        /// This is what stops the case picker from walking off the pallet quantities. An
        /// OrderSelectionTaskDriver looks at OrderLineItem.QuantityRemaining and would happily pick
        /// all 620 cases by hand; subtracting this leaves it only the 20 it's actually there for.
        /// Derived from the live task list rather than stored on the line item, so it can't drift out
        /// of step with the work that actually exists.
        /// </summary>
        public int OutstandingPalletPickCases(string orderId, string skuId)
        {
            int fullPallet = FullPalletCases(skuId);
            if (fullPallet <= 0) return 0;

            int tasks = LiveTasksForOrder(orderId).Count(t => t.Type == WorkTaskType.PalletPick && t.SkuId == skuId);
            return tasks * fullPallet;
        }

        /// <summary>How many cases of this line a case picker may still take — what's left after both
        /// what's already picked and what the Reach Trucks still owe.</summary>
        public int SelectableRemaining(OrderData order, OrderLineItem line)
        {
            if (order == null || line == null) return 0;
            if (!order.IsBulk) return line.QuantityRemaining;
            return Mathf.Max(0, line.QuantityRemaining - OutstandingPalletPickCases(order.OrderId, line.SkuId));
        }

        /// <summary>True when nothing is left for a case picker because every remaining case is owed
        /// by a PalletPick. Lets the selector finish its remainder and deliver normally instead of
        /// reporting a short pick, which is what it would otherwise conclude from finding no
        /// pickable location for the pallet quantities.</summary>
        public bool RemainingIsAllPalletPick(OrderData order)
        {
            if (order == null || !order.IsBulk) return false;
            if (order.IsFullyPicked) return false;
            return order.LineItems.All(li => SelectableRemaining(order, li) <= 0);
        }

        /// <summary>
        /// Credits one delivered full pallet against a bulk order and advances the order's status.
        ///
        /// Called by the Reach Truck the moment the pallet is physically set down in the staging lane,
        /// which is why it can set Staged: the goods really are where a loader will find them. Whoever
        /// finishes last — the final pallet pick or the case picker's remainder — is what flips the
        /// order to Staged, so neither has to know about the other.
        /// </summary>
        public void NotePalletPicked(string orderId, string skuId, int cases)
        {
            var order = _activeOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null) return;

            var line = order.LineItems.FirstOrDefault(li => li.SkuId == skuId && !li.IsFullyPicked);
            if (line == null)
            {
                Debug.LogWarning($"[OrderService] Pallet pick delivered {cases} cs of {skuId} for order " +
                                 $"{orderId}, but no unfilled line wants that SKU — not credited.");
                return;
            }

            // Clamped: a pallet carrying more than the line still needs must not push QuantityPicked
            // past QuantityNeeded, which would make TotalUnitsRemaining negative and bill the customer
            // for cases they never ordered.
            line.QuantityPicked = Mathf.Min(line.QuantityNeeded, line.QuantityPicked + cases);

            if (order.IsFullyPicked)
            {
                MarkOrderFulfilled(orderId);
                order.Status = OrderData.OrderStatus.Staged;
                Debug.Log($"[OrderService] Bulk order {orderId} ({order.CustomerName}) complete — staged.");
            }
            else if (order.Status == OrderData.OrderStatus.Pending)
            {
                order.Status = OrderData.OrderStatus.PartiallyPicked;
            }
        }

        /// <summary>Releases a batch of still-Open orders — must all belong to the same customer —
        /// to a specific staging lane: stamps AssignedDoorNumber/AssignedLane on each and flips each
        /// order's WorkTask from Open to Available so Order Selectors can start claiming them. Fails
        /// atomically (no partial changes) if any order isn't actually Open, the batch spans more
        /// than one customer, or the lane is already owned by a different customer.</summary>
        /// <summary>Releases a batch to a whole STAGE (a door's set of staging lanes) rather than to
        /// one named lane — what the Work Queue panel now offers as "Stage 1", "Stage 2"… Picks the
        /// first lane of that Stage with room as the starting point; if it later fills, the selector
        /// overflows into the next lane of the same Stage on its own
        /// (InventoryService.TryFindStagingLaneForPallets), so this choice is a starting point, not a
        /// commitment. That overflow is decided per ORDER, never per pallet — one order's pallets all
        /// land in the lane recorded on OrderData.AssignedLane.</summary>
        public bool ReleaseOrdersToStage(List<string> orderIds, int doorNumber)
        {
            ServiceLocator.TryGet<InventoryService>(out var inv);

            // Re-check here, not just in the dropdown that offered this stage: the panel rebuilds on a
            // refresh tick, so a trailer can dock or a putaway can land in the gap between the player
            // seeing the list and clicking Submit. Refusing here is what actually prevents the double
            // assignment; hiding it in the UI is only the first line.
            if (StagingLaneAssignmentService.HasInboundTruckDocked(doorNumber))
            {
                Debug.LogWarning($"[OrderService] Stage {doorNumber} has an inbound trailer docked — release refused.");
                return false;
            }
            // Lane-level inbound stock needs no separate check here: TryResolveStartLane picks from
            // LanesInStage, which already excludes any lane holding received pallets.
            if (!StagingLaneAssignmentService.TryResolveStartLane(this, inv, doorNumber, out string startLane))
            {
                Debug.LogWarning($"[OrderService] Stage {doorNumber} has no pickable staging lane — release refused.");
                return false;
            }
            return ReleaseOrdersToLane(orderIds, doorNumber, startLane);
        }

        /// <summary>One customer's share of a multi-customer release, and the stage it will go to.</summary>
        public class StageRelease
        {
            public string CustomerId;
            public string CustomerName;
            public List<string> OrderIds;
            public int DoorNumber;
        }

        /// <summary>
        /// Works out which stage each customer in a mixed selection would get, WITHOUT changing
        /// anything — so the panel can show the player the exact plan before they commit to it, and
        /// then commit that same plan (see ReleaseOrdersToStages). One algorithm serving both is the
        /// point: a preview computed separately from the commit drifts out of agreement with it.
        ///
        /// A stage holds ONE customer at a time — staging overflows A→B→C within a stage, so a second
        /// customer sharing it would have the first's overflow land in its lanes. Multi-customer
        /// release therefore means one stage EACH, not a shared stage. Customers are served in the
        /// order they appear in the selection; each takes a stage it already owns if it has one
        /// (adding to a customer's existing staging, the natural thing) before consuming a free stage.
        ///
        /// All-or-nothing: if any customer can't be placed, nothing is planned and
        /// <paramref name="failReason"/> names the customer and why.
        /// </summary>
        public bool TryPlanStageSpread(List<string> orderIds, out List<StageRelease> plan, out string failReason)
        {
            plan = new List<StageRelease>();
            failReason = null;
            if (orderIds == null || orderIds.Count == 0) { failReason = "nothing selected"; return false; }

            ServiceLocator.TryGet<InventoryService>(out var inv);

            // Group by customer, preserving the order they were selected in.
            var byCustomer = new Dictionary<string, StageRelease>();
            var customerOrder = new List<string>();
            foreach (var id in orderIds)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                if (order == null) { failReason = $"order {id} no longer exists"; plan = null; return false; }

                if (!byCustomer.TryGetValue(order.CustomerId, out var group))
                {
                    group = new StageRelease
                    {
                        CustomerId = order.CustomerId,
                        CustomerName = order.CustomerName,
                        OrderIds = new List<string>()
                    };
                    byCustomer[order.CustomerId] = group;
                    customerOrder.Add(order.CustomerId);
                }
                group.OrderIds.Add(id);
            }

            var allDoors = LaneNamingService.AllLanes().Select(l => l.door).Distinct().OrderBy(d => d).ToList();
            var claimed = new HashSet<int>(); // stages spoken for by EARLIER customers in this same batch

            foreach (var customerId in customerOrder)
            {
                var group = byCustomer[customerId];
                int chosen = 0;

                // Prefer a stage this customer already occupies, so their orders stay together.
                foreach (int door in allDoors)
                {
                    if (claimed.Contains(door)) continue;
                    if (StagingLaneAssignmentService.GetOwningCustomerIdForDoor(this, door) != customerId) continue;
                    if (!StagingLaneAssignmentService.IsStageSelectableFor(this, inv, door, customerId)) continue;
                    chosen = door;
                    break;
                }

                if (chosen == 0)
                {
                    foreach (int door in allDoors)
                    {
                        if (claimed.Contains(door)) continue;
                        if (!StagingLaneAssignmentService.IsStageSelectableFor(this, inv, door, customerId)) continue;
                        chosen = door;
                        break;
                    }
                }

                if (chosen == 0)
                {
                    failReason = customerOrder.Count > 1
                        ? $"no free stage left for {group.CustomerName} — {customerOrder.Count} customers selected but only {claimed.Count} stage(s) could be assigned"
                        : $"no available stage for {group.CustomerName}";
                    plan = null;
                    return false;
                }

                claimed.Add(chosen);
                group.DoorNumber = chosen;
                plan.Add(group);
            }

            return true;
        }

        /// <summary>
        /// Releases a selection spanning any number of customers in one action, each to its own stage
        /// (see TryPlanStageSpread for how stages are chosen). Plans the whole batch before touching
        /// anything, so an unplaceable customer aborts the release rather than half-committing it.
        /// </summary>
        public bool ReleaseOrdersToStages(List<string> orderIds, out string failReason)
        {
            if (!TryPlanStageSpread(orderIds, out var plan, out failReason)) return false;

            foreach (var group in plan)
            {
                // Each group re-validates on the way in (inbound trailer, lane resolution, ownership).
                // The plan above already cleared all of that, so a refusal here means the world moved
                // between planning and committing within this same call — report it rather than
                // continuing and leaving the batch half-released.
                if (ReleaseOrdersToStage(group.OrderIds, group.DoorNumber)) continue;

                failReason = $"{group.CustomerName} could not be released to Stage {group.DoorNumber}";
                Debug.LogWarning($"[OrderService] Multi-customer release aborted partway: {failReason}. " +
                                 $"{plan.IndexOf(group)} of {plan.Count} customer(s) were already released.");
                return false;
            }

            Debug.Log($"[OrderService] Released {orderIds.Count} order(s) across {plan.Count} customer(s): " +
                      string.Join(", ", plan.Select(g => $"{g.CustomerName} -> Stage {g.DoorNumber}")));
            return true;
        }

        public bool ReleaseOrdersToLane(List<string> orderIds, int doorNumber, string lane)
        {
            if (_workQueue == null || orderIds == null || orderIds.Count == 0 || string.IsNullOrEmpty(lane)) return false;

            // Every live task for the order, not just its OrderSelect: a bulk order carries N
            // PalletPicks and possibly a case pick, and releasing only one of them would leave the
            // rest permanently Open — invisible to every operator and unreleasable a second time.
            var pairs = new List<(OrderData order, List<WorkTask> tasks)>();
            foreach (var id in orderIds)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                if (order == null) return false;

                var tasks = LiveTasksForOrder(id).ToList();
                if (tasks.Count == 0) return false;
                // All-or-nothing per order: a partly-released order would put half its work on the
                // floor while the panel still offers it as releasable.
                if (tasks.Any(t => t.Status != WorkTaskStatus.Open)) return false;
                pairs.Add((order, tasks));
            }

            string customerId = pairs[0].order.CustomerId;
            if (pairs.Any(p => p.order.CustomerId != customerId)) return false;
            // Availability is reckoned per STAGE (the whole door), not per lane: an order staged into
            // Stage 1 may overflow across all of 1A/1B/1C, so no other customer can be given any lane
            // of that door while it's in use.
            if (!StagingLaneAssignmentService.IsStageAvailableFor(this, doorNumber, customerId)) return false;

            int released = 0;
            foreach (var (order, tasks) in pairs)
            {
                order.AssignedDoorNumber = doorNumber;
                order.AssignedLane = lane;
                foreach (var task in tasks)
                {
                    task.Status = WorkTaskStatus.Available;
                    // A PalletPick's destination is decided here, at release — it's the lane the
                    // player just chose. The Reach Truck reads it off the task rather than looking the
                    // order up, so the pallet can't land somewhere the order doesn't record.
                    if (task.Type == WorkTaskType.PalletPick) task.AssignToLocation($"{doorNumber}{lane}");
                    released++;
                }
            }

            Debug.Log($"[OrderService] Released {pairs.Count} order(s) ({released} task(s)) for {customerId} to {doorNumber}{lane}.");
            return true;
        }

        /// <summary>
        /// True if this order can still be called off. A distribution centre like this has no such
        /// thing as a backorder: an order the pickers can't fill is either cancelled outright or held
        /// while the stock is hunted down / received in, so "cancellable" means nothing of it is
        /// physically committed yet. That is: still awaiting release (its task is Open), or released
        /// but not yet claimed by a selector (Available) — plus any legacy Backorder record left in a
        /// save from before that status was retired. An order a selector is actively picking, or whose
        /// goods are already staged, loading or loaded, is NOT cancellable — those have pallets in the
        /// world that would be orphaned.
        /// </summary>
        public bool CanCancelOrder(OrderData order)
        {
            if (order == null) return false;
            if (order.Status == OrderData.OrderStatus.Backorder) return true;   // legacy saves only
            if (order.Status != OrderData.OrderStatus.Pending) return false;

            // Every live task has to be uncommitted, not just the first one found: a bulk order with
            // one Reach Truck already carrying a pallet has goods in motion even if its case pick
            // hasn't started.
            return LiveTasksForOrder(order.OrderId)
                .All(t => t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.Available);
        }

        /// <summary>
        /// Cancels every cancellable order in the batch and returns how many were actually cancelled;
        /// any id that isn't cancellable is skipped rather than failing the whole call, so a mixed
        /// selection does the part it can. Each cancellation cancels the order's OrderSelect task (so
        /// no selector can claim work that no longer exists) and clears its door/lane stamp, which
        /// releases the stage the order was holding.
        /// </summary>
        public int CancelOrders(List<string> orderIds)
        {
            if (orderIds == null || orderIds.Count == 0) return 0;

            int cancelled = 0;
            foreach (var id in orderIds)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                if (!CanCancelOrder(order)) continue;

                // ToList first — cancelling mutates nothing in the queue, but LiveTasksForOrder is a
                // lazy query over it and the status writes below would change what it yields mid-walk.
                foreach (var task in LiveTasksForOrder(id).ToList())
                {
                    task.Status = WorkTaskStatus.Cancelled;
                    task.AssignedToEmployeeGuid = null; // release the claim so nothing tries to "resume mine"
                }

                Debug.Log($"[OrderService] Order {order.OrderId} ({order.CustomerName}) cancelled by the player — " +
                          $"{order.TotalUnitsPicked}/{order.TotalUnits} case(s) had been picked; released Stage {order.AssignedDoorNumber}.");

                order.AssignedDoorNumber = 0;
                order.AssignedLane = null;
                order.Status = OrderData.OrderStatus.Cancelled;
                StampClosedNow(order);
                OnOrderCancelled?.Invoke(order);
                // Safe to mutate _activeOrders here: this loop walks orderIds, not the order list.
                Archive(order);
                cancelled++;
            }

            return cancelled;
        }

        /// <summary>Releases a batch of fully-Staged orders — same customer, same staging lane — to
        /// a door for loading: sets each order's status to Loading and files ONE Load WorkTask
        /// covering that whole lane, which TrailerLoadController claims and physically loads onto
        /// the door's trailer. Summons an outbound trailer to the door first if one isn't already
        /// sitting there — the player is assigning to a door, not to a specific truck, so there's no
        /// separate "call a trailer" step. Fails (no changes) if any order isn't Staged, or the batch
        /// spans more than one customer or lane.</summary>
        public bool ReleaseOrdersToLoading(List<string> orderIds, int doorNumber)
        {
            if (_workQueue == null || orderIds == null || orderIds.Count == 0) return false;

            var orders = orderIds.Select(id => _activeOrders.FirstOrDefault(o => o.OrderId == id)).ToList();
            if (orders.Any(o => o == null || o.Status != OrderData.OrderStatus.Staged)) return false;

            string customerId = orders[0].CustomerId;
            // Still one customer and one door per release, but NO LONGER one lane: staging overflows
            // across a Stage (1A → 1B → 1C), so a customer's orders can legitimately be spread over
            // several lanes of the same door. Group by lane and file one Load task per lane that
            // actually holds something — a single task pointed at one lane would strand every pallet
            // that overflowed into the others.
            if (orders.Any(o => o.CustomerId != customerId || o.AssignedDoorNumber != doorNumber)) return false;
            if (orders.Any(o => string.IsNullOrEmpty(o.AssignedLane))) return false;

            var lanes = orders.Select(o => o.AssignedLane).Distinct().OrderBy(l => l).ToList();

            bool truckAtDoor = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None)
                .Any(t => t.IsOutbound && t.DockedAt != null && t.DockedAt.DoorNumber == doorNumber);
            if (!truckAtDoor)
                Object.FindAnyObjectByType<TruckYardManager>()?.SpawnOutboundTruck(doorNumber);

            foreach (var order in orders)
                order.Status = OrderData.OrderStatus.Loading;

            foreach (var lane in lanes)
            {
                _workQueue.CreateTask(
                    WorkTaskType.Load,
                    EmployeeRole.Loader,
                    palletId: null,
                    description: $"Load {customerId} from {doorNumber}{lane} -> Door {doorNumber}",
                    fromLocation: $"{doorNumber}{lane}",
                    toLocation: doorNumber.ToString());
            }

            Debug.Log($"[OrderService] Released {orders.Count} order(s) for {customerId} at Stage {doorNumber} " +
                      $"(lane(s) {string.Join(", ", lanes)}) to loading — {lanes.Count} load task(s) filed.");
            return true;
        }

        /// <summary>
        /// Releases Staged orders spanning several customers to loading in one action. Each customer's
        /// orders go to the door their goods are already staged at — nothing is chosen here, the door
        /// was fixed when they were released to a staging lane.
        ///
        /// A trailer holds ONE customer's freight, so this never puts two customers on one truck: the
        /// batch is split by door, and a door is single-customer by construction (a stage is owned by
        /// one customer while occupied). That invariant is re-checked rather than assumed — if a door
        /// somehow holds two customers, this refuses instead of loading a mixed trailer.
        ///
        /// Validates the whole batch before releasing any of it, so one bad order can't leave half the
        /// selection Loading and half Staged.
        /// </summary>
        public bool ReleaseOrdersToLoadingBatch(List<string> orderIds, out string failReason)
        {
            failReason = null;
            if (orderIds == null || orderIds.Count == 0) { failReason = "nothing selected"; return false; }

            var byDoor = new Dictionary<int, List<OrderData>>();
            var doorOrder = new List<int>();

            foreach (var id in orderIds)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                if (order == null) { failReason = $"order {id} no longer exists"; return false; }
                if (order.Status != OrderData.OrderStatus.Staged)
                {
                    failReason = $"{order.CustomerName}'s order is {order.Status}, not Staged";
                    return false;
                }
                if (order.AssignedDoorNumber <= 0 || string.IsNullOrEmpty(order.AssignedLane))
                {
                    failReason = $"{order.CustomerName}'s order has no staging lane recorded";
                    return false;
                }

                if (!byDoor.TryGetValue(order.AssignedDoorNumber, out var list))
                {
                    list = new List<OrderData>();
                    byDoor[order.AssignedDoorNumber] = list;
                    doorOrder.Add(order.AssignedDoorNumber);
                }
                list.Add(order);
            }

            foreach (int door in doorOrder)
            {
                var customersAtDoor = byDoor[door].Select(o => o.CustomerId).Distinct().ToList();
                if (customersAtDoor.Count <= 1) continue;
                failReason = $"Door {door} has orders from {customersAtDoor.Count} customers — a trailer loads one customer";
                return false;
            }

            foreach (int door in doorOrder)
            {
                var ids = byDoor[door].Select(o => o.OrderId).ToList();
                if (ReleaseOrdersToLoading(ids, door)) continue;

                failReason = $"Door {door} could not be released to loading";
                Debug.LogWarning($"[OrderService] Multi-door loading release aborted partway: {failReason}. " +
                                 $"{doorOrder.IndexOf(door)} of {doorOrder.Count} door(s) were already released.");
                return false;
            }

            Debug.Log($"[OrderService] Released {orderIds.Count} order(s) to loading across {doorOrder.Count} door(s): " +
                      string.Join(", ", doorOrder.Select(d => $"{byDoor[d][0].CustomerName} -> Door {d}")));
            return true;
        }

        /// <summary>
        /// Puts back to Staged any order at this door/lane that a load pass finished WITHOUT loading —
        /// i.e. it's still Loading and wasn't in the set the loader actually put aboard.
        ///
        /// This happens when an order's staged pallets aren't findable in its lane: they were dropped
        /// in an aisle because staging was full, or were otherwise moved. TrailerLoadController scans
        /// the lane for OutboundPalletBuilder objects, finds nothing for that order, and completes —
        /// leaving the order in Loading forever. Loading rows carry no enabled checkbox in the Work
        /// Queue panel, so the player has no way to close them out or re-release them; the order is
        /// simply stranded. Reverting to Staged makes it actionable again and surfaces the real
        /// problem (missing pallets) rather than silently wedging the queue.
        /// </summary>
        public int RevertUnloadedOrdersToStaged(int doorNumber, string lane, ICollection<string> loadedOrderIds)
        {
            int reverted = 0;
            foreach (var order in _activeOrders)
            {
                if (order.Status != OrderData.OrderStatus.Loading) continue;
                if (order.AssignedDoorNumber != doorNumber || order.AssignedLane != lane) continue;
                if (loadedOrderIds != null && loadedOrderIds.Contains(order.OrderId)) continue;

                order.Status = OrderData.OrderStatus.Staged;
                reverted++;
                Debug.LogWarning($"[OrderService] Order {order.OrderId} ({order.CustomerName}) was released to loading " +
                                 $"from {doorNumber}{lane} but no staged pallet for it could be found there — " +
                                 $"reverted to Staged so it can be re-released. Its pallets are most likely sitting " +
                                 $"somewhere other than the lane.");
            }
            return reverted;
        }

        /// <summary>Marks an order Loaded once every pallet it staged has been physically carried onto
        /// its assigned door's trailer — called by TrailerLoadController once its lane-wide load pass
        /// finishes. Distinct from Shipped: billing and trailer departure now wait for the player's
        /// explicit close-out (see CloseOutOrders) instead of firing the instant the AI finishes
        /// stacking pallets.
        ///
        /// <paramref name="palletsLoaded"/> is ADDED to the order's running PalletsShipped rather
        /// than assigned: an order too big for one trailer is loaded across two passes, and each
        /// pass only knows about the pallets it personally carried aboard.</summary>
        public void MarkOrderLoaded(string orderId, int palletsLoaded = 0)
        {
            var order = _activeOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null) return;
            order.Status = OrderData.OrderStatus.Loaded;
            if (palletsLoaded > 0) order.PalletsShipped += palletsLoaded;
            Debug.Log($"[OrderService] Order {orderId} ({order.CustomerName}) loaded onto its trailer — awaiting close-out.");
        }

        /// <summary>Player-triggered close-out for a batch of fully-Loaded orders: bills and ships
        /// each one (see ShipOrder), then — per door touched — releases that door's outbound trailer
        /// to depart once nothing assigned there is still Loading/Loaded (not just the orders in this
        /// batch; a door only clears once its whole load has been closed out). Fails (no changes) if
        /// any order isn't actually Loaded.
        ///
        /// <paramref name="totalBilled"/> reports what the batch actually earned, summed from what
        /// ShipOrder really credited rather than recomputed by the caller — a partially-picked order
        /// bills only the cases that shipped, so any second calculation of "what this sale was worth"
        /// would eventually disagree with the money that changed hands. The UI uses it to show the
        /// figure it just banked (see WorkQueuePanel / MoneyFlightFx). 0 whenever this returns
        /// false.</summary>
        public bool CloseOutOrders(List<string> orderIds, out int totalBilled)
        {
            totalBilled = 0;
            if (orderIds == null || orderIds.Count == 0) return false;

            var orders = orderIds.Select(id => _activeOrders.FirstOrDefault(o => o.OrderId == id)).ToList();
            if (orders.Any(o => o == null || o.Status != OrderData.OrderStatus.Loaded)) return false;

            var revenueByDoor = new Dictionary<int, int>();
            foreach (var order in orders)
            {
                int doorNumber = order.AssignedDoorNumber;
                int revenue = ShipOrder(order.OrderId);
                revenueByDoor.TryGetValue(doorNumber, out int existing);
                revenueByDoor[doorNumber] = existing + revenue;
                totalBilled += revenue;
            }

            foreach (var kvp in revenueByDoor)
                TryReleaseDoorIfClear(kvp.Key, kvp.Value);

            Debug.Log($"[OrderService] Closed out {orders.Count} order(s) — billed ${totalBilled:N0}.");
            return true;
        }

        /// <summary>Shows the closed-out batch's total sale value hovering over its door (same
        /// "Mario-coin" FloatingMoneyText effect used elsewhere), then — if nothing assigned to this
        /// door is still Loading/Loaded — releases the outbound trailer docked there so it can
        /// depart. The other half of the manual close-out flow (see CloseOutOrders).</summary>
        private void TryReleaseDoorIfClear(int doorNumber, int revenueJustBilled)
        {
            var truck = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.IsOutbound && t.DockedAt != null && t.DockedAt.DoorNumber == doorNumber);

            if (revenueJustBilled > 0 && truck != null)
                FloatingMoneyText.Show(truck.DockedAt.transform.position + Vector3.up * 2.5f, revenueJustBilled);

            bool stillPending = _activeOrders.Any(o => o.AssignedDoorNumber == doorNumber &&
                (o.Status == OrderData.OrderStatus.Loading || o.Status == OrderData.OrderStatus.Loaded));
            if (stillPending || truck == null) return;

            truck.CompleteLoad();
            Debug.Log($"[OrderService] Door {doorNumber}'s trailer cleared to depart — every order closed out.");
        }

        /// <summary>Finds the next pending order that needs picking.</summary>
        public OrderData GetNextPendingOrder()
        {
            return _activeOrders.FirstOrDefault(o => o.Status == OrderData.OrderStatus.Pending || o.Status == OrderData.OrderStatus.PartiallyPicked);
        }

        /// <summary>
        /// Generates a list of picking tasks for an order based on current inventory.
        /// </summary>
        public List<PickingTask> GeneratePickingTasks(OrderData order)
        {
            var tasks = new List<PickingTask>();
            
            foreach (var lineItem in order.LineItems.Where(li => !li.IsFullyPicked))
            {
                int remaining = lineItem.QuantityRemaining;
                var pallets = _inventoryService.GetPalletsBySku(lineItem.SkuId);

                foreach (var pallet in pallets)
                {
                    if (remaining <= 0) break;

                    int toPick = Mathf.Min(remaining, pallet.Quantity);
                    tasks.Add(new PickingTask(order.OrderId, pallet.PalletId, pallet.CurrentLocation, toPick));
                    remaining -= toPick;
                }
            }

            return tasks;
        }

        public void MarkOrderFulfilled(string orderId)
        {
            var order = _activeOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order != null)
            {
                order.Status = OrderData.OrderStatus.FullyPicked;
                OnOrderFulfilled?.Invoke(order);
                Debug.Log($"[OrderService] Order {orderId} fully picked.");
            }
        }

        /// <summary>Bills the customer for whatever actually shipped (SellingPrice x QuantityPicked
        /// per line item — an order that hit B3's cubing cap partway through only bills for the
        /// units that made it onto a pallet, never the full originally-requested amount) and marks
        /// the order Shipped. Called from CloseOutOrders once the player manually closes out a
        /// Loaded order. Safe to call more than once for the same order (a no-op, returning 0, after
        /// the first) so a bug upstream can't double-charge the customer. Returns the amount billed.</summary>
        public int ShipOrder(string orderId)
        {
            var order = _activeOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null)
            {
                // Shipping archives the order, so a repeat call finds it in history, not here. That's
                // the documented no-op path now — only warn if the id is genuinely unknown.
                if (_orderHistory.Any(o => o.OrderId == orderId)) return 0;

                Debug.LogWarning($"[OrderService] ShipOrder: no active order found for {orderId}.");
                return 0;
            }
            if (order.Status == OrderData.OrderStatus.Shipped) return 0;

            int revenue = order.LineItems.Sum(li => li.QuantityPicked * li.SellingPrice);
            _moneyService?.AddCapital(revenue, FinanceCategory.CasePick);

            order.Status = OrderData.OrderStatus.Shipped;
            StampClosedNow(order);
            OnOrderShipped?.Invoke(order);
            // Terminal now — out of the working list before CloseOutOrders asks the door whether
            // anything there is still Loading/Loaded, so a just-shipped order can't hold its own
            // trailer at the dock.
            Archive(order);
            Debug.Log($"[OrderService] Order {orderId} ({order.CustomerName}) shipped — billed ${revenue} for {order.TotalUnitsPicked} unit(s).");
            return revenue;
        }

        /// <summary>Fallback late fee for an order that carries no rate of its own — a Dev Console
        /// order, or one restored from a save written before OrderData.LateFeePercent existed.
        ///
        /// Contract-generated orders now carry their OWN rate, stamped at creation from
        /// ContractData.LateFeePercent. Until that landed, every contract advertised a bespoke rate
        /// on its card and then got charged this flat 25% regardless — the terms were decoration.</summary>
        private const float LateFeePercentClerk = 0.25f;

        private void OnDayChanged(string eventId, int newDay)
        {
            // Fine every order that just went overdue and hasn't already been charged — a one-time
            // hit the day it first crosses its due date, not a recurring daily charge. Fires
            // regardless of Status: an order still sitting unreleased in the Work Queue panel is
            // just as late as one that's Staged or Loading. Shipped and Cancelled orders are excluded
            // structurally now, not incidentally — they've been archived out of _activeOrders, so a
            // delivered order can never be fined for going overdue after the fact.
            // ToList() because OnOrderFined listeners are free to touch order state; the enumeration
            // itself is over _activeOrders and nothing here archives, but snapshotting keeps a future
            // listener from invalidating the iterator.
            foreach (var order in _activeOrders.Where(o => o.IsOverdue(newDay) && !o.HasBeenFined).ToList())
            {
                float rate = order.LateFeePercent > 0f ? order.LateFeePercent : LateFeePercentClerk;
                int fine = Mathf.RoundToInt(rate * order.TotalRevenue);
                _moneyService?.RemoveCapital(fine, FinanceCategory.Fines);
                order.HasBeenFined = true;
                OnOrderFined?.Invoke(order, fine);

                Debug.LogWarning($"[OrderService] Order {order.OrderId} ({order.CustomerName}) is OVERDUE — fined ${fine} ({rate:P0} of ${order.TotalRevenue} order cost).");
            }
        }

        /// <summary>
        /// Moves a finished order out of the working list and into history.
        ///
        /// Called the instant an order goes terminal (Shipped or Cancelled) rather than swept up
        /// later, so a closed-out order leaves the Work Queue immediately. Without this, terminal
        /// orders accumulated in _activeOrders forever — 67 in one observed session, nearly all
        /// finished — and every one of them was re-scanned by WorkQueuePanel.BuildLiveSignature four
        /// times a second and re-serialised into every save. Harmless at a few hand-made orders per
        /// session; compounding without limit once orders arrive on a schedule.
        ///
        /// Safe to call from a loop over any collection EXCEPT _activeOrders itself.
        /// </summary>
        /// <summary>Records the in-game moment an order went terminal, for the Work Queue's Completed
        /// tab. Written once, at the transition itself, rather than derived later — nothing else in
        /// the save records when a shipment left, and a figure recomputed from anything downstream
        /// would just be a guess.
        ///
        /// Leaves the stamp at -1 if the clock isn't available, which the tab renders as "—". A
        /// missing timestamp is honest; day 0 would be a lie that also sorts to the bottom.</summary>
        private void StampClosedNow(OrderData order)
        {
            if (order == null || _timeService == null) return;
            order.ClosedDayNumber = _timeService.Day;
            order.ClosedMinuteOfDay = _timeService.Hour * 60 + _timeService.Minute;
        }

        private void Archive(OrderData order)
        {
            if (order == null) return;

            _activeOrders.Remove(order);
            _orderHistory.Add(order);

            // Oldest-first trim: history is append-ordered, so index 0 is the longest-finished.
            int excess = _orderHistory.Count - MaxArchivedOrders;
            if (excess > 0) _orderHistory.RemoveRange(0, excess);
        }

        /// <summary>Flatten orders for saving — active first, then history, in one list. Both go into
        /// the same OrderSnapshot list ON PURPOSE: Status already distinguishes them, so Import can
        /// sort them back out, and the save schema needs no new field. That also means an existing
        /// save full of un-retired terminal orders migrates itself on the next load.
        ///
        /// _assignedTasks is intentionally not persisted — nothing in the codebase writes to it yet
        /// (reserved for future picking-task assignment tracking), so there's nothing real to
        /// snapshot.</summary>
        public List<OrderSnapshot> Export()
        {
            var list = new List<OrderSnapshot>();
            foreach (var o in _activeOrders.Concat(_orderHistory))
            {
                var snap = new OrderSnapshot
                {
                    orderId = o.OrderId,
                    customerId = o.CustomerId,
                    customerName = o.CustomerName,
                    deliveryAddress = o.DeliveryAddress,
                    createdDayNumber = o.CreatedDayNumber,
                    dueDay = o.DueDay,
                    createdTimeMinute = o.CreatedTimeMinute,
                    status = (int)o.Status,
                    paymentMethod = (int)o.PaymentMethod,
                    assignedDoorNumber = o.AssignedDoorNumber,
                    assignedLane = o.AssignedLane,
                    hasBeenFined = o.HasBeenFined,
                    contractId = o.ContractId,
                    lateFeePercent = o.LateFeePercent,
                    // isWholesale deliberately left false/unwritten — that field is retired, folded
                    // into isBulk. Only Import still reads it, for saves written before the merge.
                    isBulk = o.IsBulk,
                    closedDayNumber = o.ClosedDayNumber,
                    closedMinuteOfDay = o.ClosedMinuteOfDay,
                    palletsShipped = o.PalletsShipped
                };
                foreach (var li in o.LineItems)
                {
                    snap.lineItems.Add(new OrderLineItemSnapshot
                    {
                        skuId = li.SkuId,
                        quantityNeeded = li.QuantityNeeded,
                        quantityPicked = li.QuantityPicked,
                        unitCost = li.UnitCost,
                        sellingPrice = li.SellingPrice
                    });
                }
                list.Add(snap);
            }
            return list;
        }

        /// <summary>Restores orders from a save file, sorting each into the working list or history
        /// by its saved Status. Deliberately does NOT fire OnOrderArrived — that event exists to
        /// notify UI/employees of a genuinely NEW order, and a restored order isn't new.
        ///
        /// This split is also the migration path for saves written before archiving existed: their
        /// terminal orders were all stored as "active", and land in history on the next load without
        /// anything having to rewrite the file.</summary>
        public void Import(List<OrderSnapshot> entries)
        {
            _activeOrders.Clear();
            _orderHistory.Clear();
            _assignedTasks.Clear();
            if (entries == null) return;

            foreach (var snap in entries)
            {
                if (snap == null) continue;

                var order = new OrderData(snap.orderId, snap.customerId, snap.customerName, snap.deliveryAddress, snap.createdDayNumber, snap.dueDay, snap.createdTimeMinute, (OrderData.OrderStatus)snap.status, (OrderData.OrderPaymentMethod)snap.paymentMethod)
                {
                    AssignedDoorNumber = snap.assignedDoorNumber,
                    AssignedLane = snap.assignedLane,
                    HasBeenFined = snap.hasBeenFined,
                    ContractId = snap.contractId,
                    // 0 means the field wasn't in the file — keep the old flat rate rather than
                    // silently making a legacy order free to be late.
                    LateFeePercent = snap.lateFeePercent > 0f ? snap.lateFeePercent : LateFeePercentClerk,
                    // A save written before Wholesale merged into Bulk carries isWholesale=true and
                    // isBulk=false — fold it forward so that order keeps getting full-pallet
                    // fulfilment instead of silently becoming an ordinary case-pick order on load.
                    IsBulk = snap.isBulk || snap.isWholesale,
                    // Day numbers start at 1, so 0 can only mean "this save predates the field".
                    ClosedDayNumber = snap.closedDayNumber > 0 ? snap.closedDayNumber : -1,
                    // The DAY decides whether the pair was recorded, never the minute's own value —
                    // 0 is a legitimate minute (midnight), so it can't stand in for "unset" here.
                    ClosedMinuteOfDay = snap.closedDayNumber > 0 ? snap.closedMinuteOfDay : -1,
                    PalletsShipped = snap.palletsShipped
                };
                foreach (var liSnap in snap.lineItems)
                {
                    var li = new OrderLineItem(liSnap.skuId, liSnap.quantityNeeded, liSnap.unitCost, liSnap.sellingPrice)
                    {
                        QuantityPicked = liSnap.quantityPicked
                    };
                    order.LineItems.Add(li);
                }
                bool terminal = order.Status == OrderData.OrderStatus.Shipped
                             || order.Status == OrderData.OrderStatus.Cancelled;
                if (terminal) _orderHistory.Add(order);
                else _activeOrders.Add(order);
            }

            // Trim once, after the whole file is in, rather than per-add: a legacy save can carry far
            // more than the cap and the oldest are the ones to drop.
            int excess = _orderHistory.Count - MaxArchivedOrders;
            if (excess > 0) _orderHistory.RemoveRange(0, excess);

            if (_activeOrders.Count > 0 || _orderHistory.Count > 0)
                Debug.Log($"[OrderService] Restored {_activeOrders.Count} active order(s) " +
                          $"and {_orderHistory.Count} finished order(s) in history.");
        }

        /// <summary>
        /// Reconciles restored orders against the outbound pallets that actually exist in the scene,
        /// and hands any phantom back to picking. Call once at the END of a load, after pallets and
        /// work tasks are restored.
        ///
        /// An order's Status/AssignedDoorNumber/AssignedLane persist, but the physical staged pallets
        /// do NOT — there is no OutboundPalletSnapshot, and OutboundPalletBuilder instances are plain
        /// world objects the save never captures. A save taken while orders sat staged therefore
        /// reloads into a world where the Work Queue reports a lane holding freight that isn't there:
        /// the lane reads Staged, the dock is empty, and releasing it to loading files a Load task the
        /// loader can never satisfy — leaving a truck parked at the door indefinitely, since
        /// TruckController.KeepDockAlive() suppresses the empty-trailer timeout while a task is
        /// outstanding.
        ///
        /// Any order claiming its goods are in a lane (Staged or Loading) with no matching pallet is
        /// reset to Pending and unreleased, its QuantityPicked cleared, and a fresh OrderSelect task
        /// filed in Open so the player can release it again. The cases picked before the save are
        /// genuinely gone — that rack decrement DID persist — so re-picking from current stock is the
        /// honest recovery, not a duplicate.
        ///
        /// Loaded orders are reported but NOT reset: their pallets were inside a trailer that also
        /// didn't persist, but they represent finished work awaiting billing, and voiding that revenue
        /// isn't a decision to make silently on a load.
        /// </summary>
        public int ReconcileStagedOrdersAgainstScene()
        {
            var livePalletOrderIds = new HashSet<string>();
            foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>(FindObjectsSortMode.None))
                if (p != null && !string.IsNullOrEmpty(p.OrderId))
                    livePalletOrderIds.Add(p.OrderId);

            int reset = 0;
            var strandedLoaded = new List<string>();
            var vacatedLanes = new HashSet<string>();

            foreach (var order in _activeOrders)
            {
                if (order == null) continue;
                if (livePalletOrderIds.Contains(order.OrderId)) continue;

                if (order.Status == OrderData.OrderStatus.Loaded)
                {
                    strandedLoaded.Add($"{order.OrderId} ({order.CustomerName})");
                    continue;
                }
                if (order.Status != OrderData.OrderStatus.Staged &&
                    order.Status != OrderData.OrderStatus.Loading)
                    continue;

                Debug.LogWarning($"[OrderService] Order {order.OrderId} ({order.CustomerName}) restored as " +
                                 $"{order.Status} at {order.AssignedDoorNumber}{order.AssignedLane}, but none of its " +
                                 $"staged pallets exist in the scene (staged pallets aren't persisted). Returning it to " +
                                 $"picking — {order.TotalUnitsPicked} previously picked case(s) are gone and must be " +
                                 $"re-picked from current stock.");

                if (order.Status == OrderData.OrderStatus.Loading)
                    vacatedLanes.Add($"{order.AssignedDoorNumber}{order.AssignedLane}");

                order.Status = OrderData.OrderStatus.Pending;
                order.AssignedDoorNumber = 0;
                order.AssignedLane = null;
                foreach (var li in order.LineItems)
                    li.QuantityPicked = 0;

                // Reuse surviving tasks if the work-queue snapshot restored any, rather than filing a
                // second set for the same order. If the order had already been staged its tasks were
                // Complete and won't have been exported at all — file fresh ones, Open, exactly as
                // ReceiveOrder does (minus OnOrderArrived: this order isn't new).
                var surviving = LiveTasksForOrder(order.OrderId).ToList();
                if (surviving.Count > 0)
                {
                    foreach (var t in surviving)
                    {
                        t.Status = WorkTaskStatus.Open;
                        t.AssignedToEmployeeGuid = null;
                        // A PalletPick's source is re-resolved on the next claim; a stale PalletId from
                        // before the reload would point at a pallet this order no longer holds.
                        if (t.Type == WorkTaskType.PalletPick) t.PalletId = null;
                    }
                }
                else if (order.IsBulk)
                {
                    // QuantityPicked was just zeroed above, so FileBulkTasks re-derives the same
                    // pallet/remainder split the order arrived with.
                    FileBulkTasks(order);
                }
                else
                {
                    var task = _workQueue?.CreateTask(
                        WorkTaskType.OrderSelect,
                        EmployeeRole.OrderSelector,
                        palletId: null,
                        description: $"Select order for {order.CustomerName} ({order.TotalUnits} units)",
                        orderId: order.OrderId);
                    if (task != null) task.Status = WorkTaskStatus.Open;
                }

                reset++;
            }

            // A Load task is keyed by LANE, not by order, and one task can cover several orders' pallets
            // — so it may only be cancelled once no order is still Loading out of that lane. Left
            // behind, it would send a loader and a commandeered dock stocker to an empty lane.
            foreach (var laneAddress in vacatedLanes)
            {
                bool stillLoading = _activeOrders.Any(
                    o => o != null && o.Status == OrderData.OrderStatus.Loading &&
                         $"{o.AssignedDoorNumber}{o.AssignedLane}" == laneAddress);
                if (stillLoading) continue;

                var deadLoads = _workQueue?.Tasks.Where(
                    t => t.Type == WorkTaskType.Load && t.FromLocation == laneAddress
                      && t.Status != WorkTaskStatus.Complete && t.Status != WorkTaskStatus.Cancelled).ToList();
                if (deadLoads == null) continue;

                foreach (var dead in deadLoads)
                {
                    dead.Status = WorkTaskStatus.Cancelled;
                    Debug.LogWarning($"[OrderService] Cancelled Load task for {laneAddress} — every order it " +
                                     $"covered was a phantom, so there is nothing in that lane to load.");
                }
            }

            if (reset > 0)
                Debug.LogWarning($"[OrderService] Returned {reset} phantom staged order(s) to picking after load.");
            if (strandedLoaded.Count > 0)
                Debug.LogWarning($"[OrderService] {strandedLoaded.Count} order(s) restored as Loaded with no pallets in " +
                                 $"the scene — their trailer didn't persist either, so they can't be closed out as-is: " +
                                 $"{string.Join(", ", strandedLoaded)}");
            return reset;
        }
    }

    /// <summary>
    /// Represents a single physical step in fulfilling an order: go here, take this, from that pallet.
    /// </summary>
    public class PickingTask
    {
        public string OrderId { get; private set; }
        public string PalletId { get; private set; }
        public Vector2Int Location { get; private set; }
        public int Quantity { get; private set; }
        public bool IsCompleted { get; set; }

        public PickingTask(string orderId, string palletId, Vector2Int location, int quantity)
        {
            OrderId = orderId;
            PalletId = palletId;
            Location = location;
            Quantity = quantity;
        }
    }
}
