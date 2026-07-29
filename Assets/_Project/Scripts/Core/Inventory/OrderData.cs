using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Represents a customer order for goods to be fulfilled and shipped.
    /// Tracks what items are needed, current fulfillment status, and SLA deadline.
    /// </summary>
    [System.Serializable]
    public class OrderData
    {
        public string OrderId { get; private set; }
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string DeliveryAddress { get; set; }
        public int CreatedDayNumber { get; set; }
        public int DueDay { get; set; } // Day order must ship by
        public List<OrderLineItem> LineItems { get; set; } = new();
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public OrderPaymentMethod PaymentMethod { get; set; } = OrderPaymentMethod.Prepaid;
        public int CreatedTimeMinute { get; set; } // When order arrived (for FIFO priority)

        /// <summary>Outbound door this order is staged at — set once, when the player releases this
        /// order to a staging lane via the Work Queue panel (OrderService.ReleaseOrdersToLane). 0
        /// until then; the order sits Open/unrouted in the meantime.</summary>
        public int AssignedDoorNumber { get; set; }

        /// <summary>Lane letter ("A", "B"...) at AssignedDoorNumber this order is staged into, set
        /// alongside AssignedDoorNumber by the same release action. Null until released. A staging
        /// lane holds only one customer's orders at a time (StagingLaneAssignmentService) — every
        /// order released to the same lane shares this value.</summary>
        public string AssignedLane { get; set; }

        /// <summary>True once the late fee (E1) has been charged for this order. Fining is a
        /// one-time event the day an order first goes overdue, not a recurring daily charge — this
        /// flag is what stops OrderService.OnDayChanged from re-fining the same order every
        /// subsequent day it remains unshipped.</summary>
        public bool HasBeenFined { get; set; }

        public OrderData(string customerId, string customerName, string deliveryAddress, int createdDay, int dueDay, int createdMinute)
        {
            OrderId = System.Guid.NewGuid().ToString();
            CustomerId = customerId;
            CustomerName = customerName;
            DeliveryAddress = deliveryAddress;
            CreatedDayNumber = createdDay;
            DueDay = dueDay;
            CreatedTimeMinute = createdMinute;
        }

        /// <summary>Restore-only constructor — reconstructs an order with its original saved
        /// OrderId instead of minting a new GUID. Used by OrderService.Import when restoring
        /// from a save file.</summary>
        internal OrderData(string orderId, string customerId, string customerName, string deliveryAddress, int createdDay, int dueDay, int createdMinute, OrderStatus status, OrderPaymentMethod paymentMethod)
        {
            OrderId = orderId;
            CustomerId = customerId;
            CustomerName = customerName;
            DeliveryAddress = deliveryAddress;
            CreatedDayNumber = createdDay;
            DueDay = dueDay;
            CreatedTimeMinute = createdMinute;
            Status = status;
            PaymentMethod = paymentMethod;
        }

        /// <summary>Total units across all line items.</summary>
        public int TotalUnits => LineItems.Sum(item => item.QuantityNeeded);

        /// <summary>Total units already picked.</summary>
        public int TotalUnitsPicked => LineItems.Sum(item => item.QuantityPicked);

        /// <summary>Total units still needed.</summary>
        public int TotalUnitsRemaining => TotalUnits - TotalUnitsPicked;

        /// <summary>Total revenue for fully shipping this order.</summary>
        public int TotalRevenue => LineItems.Sum(item => item.QuantityNeeded * item.SellingPrice);

        /// <summary>Expected profit after COGS.</summary>
        public int ExpectedProfit => TotalRevenue - LineItems.Sum(item => item.QuantityNeeded * item.UnitCost);

        /// <summary>Is order fully picked and ready to stage?</summary>
        public bool IsFullyPicked => TotalUnitsRemaining <= 0;

        /// <summary>Is order overdue?</summary>
        public bool IsOverdue(int currentDay) => currentDay > DueDay;

        /// <summary>Days until due (negative if overdue).</summary>
        public int DaysUntilDue(int currentDay) => DueDay - currentDay;

        /// <summary>NOTE: persisted by ordinal in the save file — never reorder or remove a value.
        /// Backorder is RETIRED and no longer produced: a DC like this doesn't backorder, it ships
        /// short (see the Fill Rate column) or the player cancels the order. The value stays only so
        /// saves written before that change still load; such rows surface in the Work Queue as
        /// "No Stock" and can be cancelled from there.</summary>
        public enum OrderStatus { Pending, PartiallyPicked, FullyPicked, Staged, Loading, Loaded, Shipped, Cancelled, Backorder }
        public enum OrderPaymentMethod { Prepaid, COD, Invoice }
    }

    /// <summary>A single line item in a customer order (SKU + qty needed).</summary>
    [System.Serializable]
    public class OrderLineItem
    {
        public string SkuId { get; set; }
        public int QuantityNeeded { get; set; }
        public int QuantityPicked { get; set; } = 0;
        public int UnitCost { get; set; } // What we paid (for profit calc)
        public int SellingPrice { get; set; } // What customer pays

        public OrderLineItem(string skuId, int quantityNeeded, int unitCost, int sellingPrice)
        {
            SkuId = skuId;
            QuantityNeeded = quantityNeeded;
            UnitCost = unitCost;
            SellingPrice = sellingPrice;
        }

        /// <summary>Units still needed for this line item.</summary>
        public int QuantityRemaining => QuantityNeeded - QuantityPicked;

        /// <summary>Is this line item fully picked?</summary>
        public bool IsFullyPicked => QuantityRemaining <= 0;

        /// <summary>Gross revenue for this line item.</summary>
        public int TotalRevenue => QuantityNeeded * SellingPrice;

        /// <summary>Gross profit for this line item.</summary>
        public int TotalProfit => TotalRevenue - (QuantityNeeded * UnitCost);
    }

    /// <summary>JSON-serializable snapshot of an OrderData for save/load. OrderData itself uses
    /// auto-properties (JsonUtility can't serialize those directly), so this plain-field mirror is
    /// what actually goes in SaveData — see OrderService.Export()/Import().</summary>
    [System.Serializable]
    public class OrderSnapshot
    {
        public string orderId;
        public string customerId;
        public string customerName;
        public string deliveryAddress;
        public int createdDayNumber;
        public int dueDay;
        public int createdTimeMinute;
        public int status; // (int)OrderData.OrderStatus
        public int paymentMethod; // (int)OrderData.OrderPaymentMethod
        public int assignedDoorNumber;
        public string assignedLane;
        public bool hasBeenFined;
        public List<OrderLineItemSnapshot> lineItems = new();
    }

    [System.Serializable]
    public class OrderLineItemSnapshot
    {
        public string skuId;
        public int quantityNeeded;
        public int quantityPicked;
        public int unitCost;
        public int sellingPrice;
    }
}
