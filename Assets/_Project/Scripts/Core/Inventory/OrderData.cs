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

        /// <summary>ContractData.ContractId of the contract that produced this order, or null for a
        /// hand-made Dev Console order. Stamped at creation by OrderArrivalService.
        ///
        /// This exists so a shipped order can be credited back to the account that brought it in —
        /// the Accounts tab's per-contract totals are accumulated from OnOrderShipped/OnOrderFined,
        /// and CustomerId alone can't do the job because one customer may hold several contracts.</summary>
        public string ContractId { get; set; }

        /// <summary>Share of order revenue charged if this order goes overdue, copied off the
        /// contract at creation rather than looked up at fine time.
        ///
        /// Copied deliberately: the fine must reflect the terms the player accepted when the order
        /// arrived, not whatever the asset says days later, and it keeps OrderService free of any
        /// dependency on the contract system. Defaults to the old flat 25% so Dev Console orders and
        /// saves written before this field existed behave exactly as they did.</summary>
        public float LateFeePercent { get; set; } = 0.25f;

        /// <summary>True for a one-off wholesale drop, where every line item is exactly a full pallet
        /// (Ti x Hi cases) rather than a case pick. Set at creation by OrderArrivalService.
        ///
        /// Read today only to colour the order's dock appointment, but it's the flag the full-pallet
        /// fulfilment mechanic will need when it exists — right now these orders still get walked off
        /// one case at a time by the case-pick selector.</summary>
        public bool IsWholesale { get; set; }

        /// <summary>True for a bulk order — a customer's off-the-cuff drop, priced off cost of goods
        /// and fulfilled in FULL PALLETS by PalletPick tasks rather than case by case.
        ///
        /// Unlike IsWholesale (which is still only cosmetic), this one is load-bearing:
        /// OrderService.ReceiveOrder branches on it to file PalletPick tasks instead of a single
        /// OrderSelect, so flipping it after creation would leave the order with the wrong work
        /// already filed.</summary>
        public bool IsBulk { get; set; }

        /// <summary>In-game day this order went terminal — the day its trailer was closed out and
        /// picked up (ShipOrder), or the day it was called off (CancelOrders). -1 until then, and
        /// also in any order restored from a save written before this field existed: the Completed
        /// tab shows those as "—" rather than pretending they closed on day 0.</summary>
        public int ClosedDayNumber { get; set; } = -1;

        /// <summary>Minute of the day (hour x 60 + minute) the ClosedDayNumber stamp was taken.
        /// Exists so two shipments that left on the same day still sort against each other, and so
        /// the Completed tab can show a clock time rather than just a date.</summary>
        public int ClosedMinuteOfDay { get; set; } = -1;

        /// <summary>How many physical pallets went aboard a trailer for this order. ACCUMULATED by
        /// OrderService.MarkOrderLoaded as each load pass finishes rather than set once — an order
        /// whose pallets don't all fit on one trailer is loaded across two, and each pass reports
        /// only the pallets it personally put on.</summary>
        public int PalletsShipped { get; set; }

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

        /// <summary>What this order actually earned — selling price x the units that were really
        /// picked, not the units ordered. Deliberately the same expression OrderService.ShipOrder
        /// bills on, so the Completed tab's figure can never drift from the money that changed
        /// hands: an order that shipped short shows what it really made.</summary>
        public int ShippedRevenue => LineItems.Sum(item => item.QuantityPicked * item.SellingPrice);

        /// <summary>Cost of goods for the units that actually shipped.</summary>
        public int ShippedCogs => LineItems.Sum(item => item.QuantityPicked * item.UnitCost);

        /// <summary>Net profit on what shipped — revenue less cost of goods, and nothing else.
        /// Wages, hourly running costs and any late fee charged against this order are NOT deducted
        /// here; this is the margin on the freight itself, which is what the Completed tab reports
        /// per deal. Facility-wide costs belong to the finance panel, not to one order.</summary>
        public int ShippedProfit => ShippedRevenue - ShippedCogs;

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
        public string contractId;
        /// <summary>0 in a save written before this field existed — Import treats 0 as "unset" and
        /// falls back to the old flat 25%, so old saves keep their original fine behaviour.</summary>
        public float lateFeePercent;
        public bool isWholesale;
        /// <summary>False in a save written before bulk orders existed — correct, since nothing in
        /// such a save was ever a bulk order.</summary>
        public bool isBulk;
        /// <summary>0 in a save written before these existed. Day numbers start at 1, so 0 is
        /// unambiguously "not recorded" — Import maps it back to -1 rather than to day 0.</summary>
        public int closedDayNumber;
        public int closedMinuteOfDay;
        /// <summary>0 in a save written before pallet counts were recorded. Shown as "—" rather
        /// than as a genuine zero-pallet shipment, which can't happen.</summary>
        public int palletsShipped;
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
