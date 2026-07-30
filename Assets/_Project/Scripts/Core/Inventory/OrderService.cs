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

        public IReadOnlyList<OrderData> ActiveOrders => _activeOrders;

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

            var task = _workQueue?.CreateTask(
                WorkTaskType.OrderSelect,
                EmployeeRole.OrderSelector,
                palletId: null, // no single pallet — the selector builds one/two FOR this order
                description: $"Select order for {order.CustomerName} ({order.TotalUnits} units)",
                orderId: order.OrderId);
            if (task != null) task.Status = WorkTaskStatus.Open;
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

        public bool ReleaseOrdersToLane(List<string> orderIds, int doorNumber, string lane)
        {
            if (_workQueue == null || orderIds == null || orderIds.Count == 0 || string.IsNullOrEmpty(lane)) return false;

            var pairs = new List<(OrderData order, WorkTask task)>();
            foreach (var id in orderIds)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                var task = _workQueue.Tasks.FirstOrDefault(t => t.OrderId == id && t.Type == WorkTaskType.OrderSelect);
                if (order == null || task == null || task.Status != WorkTaskStatus.Open) return false;
                pairs.Add((order, task));
            }

            string customerId = pairs[0].order.CustomerId;
            if (pairs.Any(p => p.order.CustomerId != customerId)) return false;
            // Availability is reckoned per STAGE (the whole door), not per lane: an order staged into
            // Stage 1 may overflow across all of 1A/1B/1C, so no other customer can be given any lane
            // of that door while it's in use.
            if (!StagingLaneAssignmentService.IsStageAvailableFor(this, doorNumber, customerId)) return false;

            foreach (var (order, task) in pairs)
            {
                order.AssignedDoorNumber = doorNumber;
                order.AssignedLane = lane;
                task.Status = WorkTaskStatus.Available;
            }

            Debug.Log($"[OrderService] Released {pairs.Count} order(s) for {customerId} to {doorNumber}{lane}.");
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

            var task = _workQueue?.Tasks.FirstOrDefault(t => t.OrderId == order.OrderId && t.Type == WorkTaskType.OrderSelect);
            return task == null || task.Status == WorkTaskStatus.Open || task.Status == WorkTaskStatus.Available;
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

                var task = _workQueue?.Tasks.FirstOrDefault(t => t.OrderId == id && t.Type == WorkTaskType.OrderSelect);
                if (task != null && task.Status != WorkTaskStatus.Complete && task.Status != WorkTaskStatus.Cancelled)
                {
                    task.Status = WorkTaskStatus.Cancelled;
                    task.AssignedToEmployeeGuid = null; // release the claim so nothing tries to "resume mine"
                }

                Debug.Log($"[OrderService] Order {order.OrderId} ({order.CustomerName}) cancelled by the player — " +
                          $"{order.TotalUnitsPicked}/{order.TotalUnits} case(s) had been picked; released Stage {order.AssignedDoorNumber}.");

                order.AssignedDoorNumber = 0;
                order.AssignedLane = null;
                order.Status = OrderData.OrderStatus.Cancelled;
                OnOrderCancelled?.Invoke(order);
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
        /// stacking pallets.</summary>
        public void MarkOrderLoaded(string orderId)
        {
            var order = _activeOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null) return;
            order.Status = OrderData.OrderStatus.Loaded;
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
                Debug.LogWarning($"[OrderService] ShipOrder: no active order found for {orderId}.");
                return 0;
            }
            if (order.Status == OrderData.OrderStatus.Shipped) return 0;

            int revenue = order.LineItems.Sum(li => li.QuantityPicked * li.SellingPrice);
            _moneyService?.AddCapital(revenue, FinanceCategory.CasePick);

            order.Status = OrderData.OrderStatus.Shipped;
            OnOrderShipped?.Invoke(order);
            Debug.Log($"[OrderService] Order {orderId} ({order.CustomerName}) shipped — billed ${revenue} for {order.TotalUnitsPicked} unit(s).");
            return revenue;
        }

        /// <summary>Late fee as a fraction of order cost — per Tad's original spec this varies by
        /// difficulty. Difficulty is currently hardcoded to Clerk everywhere (GameContext.Awake()),
        /// so there's no real selection to read from yet; once one exists, this should scale the
        /// same way GameContext's startingCapital/sellBackRate already do rather than staying a
        /// flat constant.</summary>
        private const float LateFeePercentClerk = 0.25f;

        private void OnDayChanged(string eventId, int newDay)
        {
            // Fine every order that just went overdue and hasn't already been charged — a one-time
            // hit the day it first crosses its due date, not a recurring daily charge. Fires
            // regardless of Status: an order still sitting unreleased in the Work Queue panel is
            // just as late as one that's Staged or Loading — only Shipped/Cancelled orders are
            // naturally excluded by IsOverdue not mattering to them anymore in practice.
            foreach (var order in _activeOrders.Where(o => o.IsOverdue(newDay) && !o.HasBeenFined))
            {
                int fine = Mathf.RoundToInt(LateFeePercentClerk * order.TotalRevenue);
                _moneyService?.RemoveCapital(fine, FinanceCategory.Fines);
                order.HasBeenFined = true;

                Debug.LogWarning($"[OrderService] Order {order.OrderId} ({order.CustomerName}) is OVERDUE — fined ${fine} ({LateFeePercentClerk:P0} of ${order.TotalRevenue} order cost).");
            }
        }

        /// <summary>Flatten all active orders for saving. _assignedTasks is intentionally not
        /// persisted — nothing in the codebase writes to it yet (reserved for future picking-task
        /// assignment tracking), so there's nothing real to snapshot.</summary>
        public List<OrderSnapshot> Export()
        {
            var list = new List<OrderSnapshot>();
            foreach (var o in _activeOrders)
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
                    hasBeenFined = o.HasBeenFined
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

        /// <summary>Restores active orders from a save file. Deliberately does NOT fire
        /// OnOrderArrived — that event exists to notify UI/employees of a genuinely NEW order,
        /// and a restored order isn't new.</summary>
        public void Import(List<OrderSnapshot> entries)
        {
            _activeOrders.Clear();
            _assignedTasks.Clear();
            if (entries == null) return;

            foreach (var snap in entries)
            {
                if (snap == null) continue;

                var order = new OrderData(snap.orderId, snap.customerId, snap.customerName, snap.deliveryAddress, snap.createdDayNumber, snap.dueDay, snap.createdTimeMinute, (OrderData.OrderStatus)snap.status, (OrderData.OrderPaymentMethod)snap.paymentMethod)
                {
                    AssignedDoorNumber = snap.assignedDoorNumber,
                    AssignedLane = snap.assignedLane,
                    HasBeenFined = snap.hasBeenFined
                };
                foreach (var liSnap in snap.lineItems)
                {
                    var li = new OrderLineItem(liSnap.skuId, liSnap.quantityNeeded, liSnap.unitCost, liSnap.sellingPrice)
                    {
                        QuantityPicked = liSnap.quantityPicked
                    };
                    order.LineItems.Add(li);
                }
                _activeOrders.Add(order);
            }

            if (_activeOrders.Count > 0)
                Debug.Log($"[OrderService] Restored {_activeOrders.Count} active order(s).");
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

                // Reuse a surviving OrderSelect task if the work-queue snapshot restored one, rather
                // than filing a second task for the same order. If the order had already been staged,
                // its OrderSelect task was Complete and won't have been exported at all — file a fresh
                // one, Open, exactly as ReceiveOrder does (minus OnOrderArrived: this order isn't new).
                var task = _workQueue?.Tasks.FirstOrDefault(
                    t => t.OrderId == order.OrderId && t.Type == WorkTaskType.OrderSelect
                      && t.Status != WorkTaskStatus.Complete && t.Status != WorkTaskStatus.Cancelled);
                if (task != null)
                {
                    task.Status = WorkTaskStatus.Open;
                    task.AssignedToEmployeeGuid = null;
                }
                else
                {
                    task = _workQueue?.CreateTask(
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
