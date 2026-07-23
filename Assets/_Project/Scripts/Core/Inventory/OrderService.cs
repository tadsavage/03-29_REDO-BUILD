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

        /// <summary>Add a new order to the system and file the WorkTask that lets an Order Selector
        /// pick it up. One task per ORDER (not per line item) — a single selector works the whole
        /// order continuously (walking between picks, building pallets) the same way a Receiver
        /// works a whole lane, rather than splitting one order across multiple claimants.</summary>
        public void ReceiveOrder(OrderData order)
        {
            order.AssignedDoorNumber = DoorAssignmentService.AssignDoorForCustomer(order.CustomerId, _inventoryService);

            _activeOrders.Add(order);
            OnOrderArrived?.Invoke(order);
            Debug.Log($"[OrderService] New Order received: {order.OrderId} from {order.CustomerName} -> Door {order.AssignedDoorNumber}");

            string doorNote = order.AssignedDoorNumber > 0 ? $"Door {order.AssignedDoorNumber}" : "no door yet";
            _workQueue?.CreateTask(
                WorkTaskType.OrderSelect,
                EmployeeRole.OrderSelector,
                palletId: null, // no single pallet — the selector builds one/two FOR this order
                description: $"Select order for {order.CustomerName} ({order.TotalUnits} units) -> {doorNote}",
                orderId: order.OrderId);
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
                    assignedDoorNumber = o.AssignedDoorNumber
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
                    AssignedDoorNumber = snap.assignedDoorNumber
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
