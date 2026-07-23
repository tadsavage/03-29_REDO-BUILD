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

        // Reserved for order cancellation (TODO: Phase 2 "Partial-order handling — backorder, split,
        // cancel") — no CancelOrder() method exists yet to raise it, so it's legitimately unused today.
#pragma warning disable CS0067
        public static event System.Action<OrderData> OnOrderCancelled;
#pragma warning restore CS0067

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
            if (!StagingLaneAssignmentService.IsLaneAvailableFor(this, doorNumber, lane, customerId)) return false;

            foreach (var (order, task) in pairs)
            {
                order.AssignedDoorNumber = doorNumber;
                order.AssignedLane = lane;
                task.Status = WorkTaskStatus.Available;
            }

            Debug.Log($"[OrderService] Released {pairs.Count} order(s) for {customerId} to {doorNumber}{lane}.");
            return true;
        }

        /// <summary>Releases a batch of fully-Staged orders — same customer, same staging lane — to
        /// a door for loading: sets each order's status to Loading and files ONE Load WorkTask
        /// covering that whole lane, which TrailerLoadController claims and physically loads onto
        /// the door's trailer. Fails (no changes) if any order isn't Staged, or the batch spans more
        /// than one customer or lane.</summary>
        public bool ReleaseOrdersToLoading(List<string> orderIds, int doorNumber)
        {
            if (_workQueue == null || orderIds == null || orderIds.Count == 0) return false;

            var orders = orderIds.Select(id => _activeOrders.FirstOrDefault(o => o.OrderId == id)).ToList();
            if (orders.Any(o => o == null || o.Status != OrderData.OrderStatus.Staged)) return false;

            string customerId = orders[0].CustomerId;
            string lane = orders[0].AssignedLane;
            if (string.IsNullOrEmpty(lane)) return false;
            if (orders.Any(o => o.CustomerId != customerId || o.AssignedLane != lane || o.AssignedDoorNumber != doorNumber)) return false;

            foreach (var order in orders)
                order.Status = OrderData.OrderStatus.Loading;

            _workQueue.CreateTask(
                WorkTaskType.Load,
                EmployeeRole.Loader,
                palletId: null,
                description: $"Load {customerId} from {doorNumber}{lane} -> Door {doorNumber}",
                fromLocation: $"{doorNumber}{lane}",
                toLocation: doorNumber.ToString());

            Debug.Log($"[OrderService] Released {orders.Count} order(s) for {customerId} at {doorNumber}{lane} to loading.");
            return true;
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
        /// the order Shipped. Called once TrailerLoadController (D1) finishes loading a truck with
        /// this order's staged pallet(s) — "billing at load time" per Tad's spec. Safe to call more
        /// than once for the same order (a no-op after the first) so a bug upstream can't
        /// double-charge the customer.</summary>
        public void ShipOrder(string orderId)
        {
            var order = _activeOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null)
            {
                Debug.LogWarning($"[OrderService] ShipOrder: no active order found for {orderId}.");
                return;
            }
            if (order.Status == OrderData.OrderStatus.Shipped) return;

            int revenue = order.LineItems.Sum(li => li.QuantityPicked * li.SellingPrice);
            _moneyService?.AddCapital(revenue, FinanceCategory.CasePick);

            order.Status = OrderData.OrderStatus.Shipped;
            OnOrderShipped?.Invoke(order);
            Debug.Log($"[OrderService] Order {orderId} ({order.CustomerName}) shipped — billed ${revenue} for {order.TotalUnitsPicked} unit(s).");
        }

        private void OnDayChanged(string eventId, int newDay)
        {
            // Check for overdue orders
            foreach (var order in _activeOrders.Where(o => o.IsOverdue(newDay)))
            {
                Debug.LogWarning($"[OrderService] Order {order.OrderId} is OVERDUE!");
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
                    assignedLane = o.AssignedLane
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
                    AssignedLane = snap.assignedLane
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
