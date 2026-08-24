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

        /// <summary>Player-facing order number — a letter for the dominant storage area on this
        /// order (G = Grocery, P = Perishable, F = Frozen, once those exist), the day it was created
        /// zero-padded to 3 digits, and a 1-digit sequence for the Nth order of that area raised that
        /// same day. Example: the 2nd grocery order raised on day 1 reads "G0012".
        ///
        /// Recurring and Bulk orders share one sequence per area/day because both are minted through
        /// OrderService.ReceiveOrder — there is no separate numbering system for either, which is what
        /// lets a Work Queue entry, a Recurring Orders line and a Bulk Orders line all be found by the
        /// same short code. Set once by OrderService.GenerateOrderNumber at creation; never
        /// reassigned. Null for an order restored from a save written before this field existed until
        /// OrderService.Import backfills it.</summary>
        public string OrderNumber { get; set; }

        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string DeliveryAddress { get; set; }
        public int CreatedDayNumber { get; set; }

        /// <summary>
        /// The DEADLINE — the last in-game day this order may ship on. Not a delivery estimate: the
        /// customer's own latest acceptable pickup.
        ///
        /// The player is expected to book a dock appointment (Contracts → Schedule) on or before this
        /// day. Two separate consequences hang off it, and an order can take both:
        ///
        ///  • Satisfaction — a trailer booked away from the hour the customer asked for docks
        ///    SignedContract.SatisfactionPercent (OrderArrivalService.PenalizeSatisfaction), and
        ///    shipping past this day fines the account (see OrderService.OnDayChanged).
        ///  • Refusal — freight still sitting here the morning AFTER this day with no appointment
        ///    booked is freight the customer never agreed to wait for. OrderArrivalService.
        ///    SweepMissedPickups cancels it and LOSES the contract outright, barring that customer
        ///    for ContractLossCooldownDays.
        ///
        /// Bulk offers roll this anywhere from SAME DAY (DueDay == CreatedDayNumber) to 72 hours out.
        /// </summary>
        public int DueDay { get; set; }
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

        /// <summary>
        /// True once this order has missed its deadline and been charged for it. One late fee per
        /// order, ever — this flag is the single guard, and it is deliberately shared by BOTH things
        /// that can decide an order is late (OrderService.ChargeLateFee is the only writer):
        ///
        ///   • DockScheduleService.SweepElapsedAppointments — the end of a recurring trailer's booked
        ///     two-hour block came and went with freight still on it. Fires on the hour tick.
        ///   • OrderService.OnDayChanged — a bulk order's DueDay passed. Fires at midnight.
        ///
        /// Sharing one flag rather than one per trigger is what makes double-billing structurally
        /// impossible: whichever deadline actually governed this order charges first and the other
        /// sweep then skips it, with no need for the two to know about each other.
        ///
        /// Also read as the definition of "on time" — a shipped order with this clear made its
        /// deadline, which is what earns the customer-satisfaction nudge in
        /// OrderArrivalService.HandleOrderShipped.
        /// </summary>
        public bool HasBeenFined { get; set; }

        /// <summary>RETIRED. Was the guard for a separate flat-25% "ran over its door slot while
        /// loading" fine (OrderService.FineLateLoad), which is gone: that fine only ever hit trailers
        /// that had already STARTED loading — so a trailer nobody touched all window went completely
        /// unpunished — and it charged a rate the customer's contract never advertised. Missing a
        /// booked block is now just "late", billed at the contract's own rate under HasBeenFined.
        ///
        /// Kept as a field, never written any more, purely so save files written before that change
        /// still deserialize. Nothing reads it.</summary>
        public bool HasBeenLateLoadFined { get; set; }

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

        /// <summary>True for a bulk order — a customer's off-the-cuff drop, priced off cost of goods
        /// and fulfilled in FULL PALLETS by PalletPick tasks rather than case by case. Load-bearing:
        /// OrderService.ReceiveOrder branches on it to file PalletPick tasks instead of a single
        /// OrderSelect, so flipping it after creation would leave the order with the wrong work
        /// already filed. Also true for orders created back when this was a separate "wholesale"
        /// concept — see OrderSnapshot.isWholesale.</summary>
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
        /// picked, not the units ordered. Deliberately the same figure OrderService.ShipOrder bills,
        /// SAME-DAY RUSH BONUS INCLUDED, so the Completed tab can never drift from the money that
        /// changed hands: an order that shipped short shows what it really made, and a rush that beat
        /// its deadline shows the doubled figure the player actually banked.
        ///
        /// The bonus is keyed on ClosedDayNumber (stamped by ShipOrder at close-out) rather than on
        /// today's date, so this stays correct forever — a rush that beat its deadline last week
        /// still reads as doubled. -1 means "never closed", which no rush can have earned.</summary>
        public int ShippedRevenue
        {
            get
            {
                int billed = LineItems.Sum(item => item.QuantityPicked * item.SellingPrice);
                return ClosedDayNumber >= 0 && QualifiesForSameDayBonus(ClosedDayNumber)
                     ? Mathf.RoundToInt(billed * SameDayRushRevenueMultiplier)
                     : billed;
            }
        }

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

        // ── Same-day rush ────────────────────────────────────────────────────

        /// <summary>
        /// Revenue multiplier paid for a same-day order that actually ships on its own day.
        ///
        /// The double is the entire reason a player would take a rush over a comfortable 72-hour
        /// deal: the deadline is brutal (pick, stage, load and close out before midnight) and the
        /// downside is an ORDINARY late fee, not a bigger one. Upside doubled, downside unchanged —
        /// that's the gamble. Applied in OrderService.ShipOrder, which is the only place the money
        /// is actually credited.
        /// </summary>
        public const float SameDayRushRevenueMultiplier = 2f;

        /// <summary>
        /// True when the customer wanted this out the door the same day it arrived AND is paying the
        /// rush premium for it.
        ///
        /// BULK ONLY, deliberately. A recurring account's deadline is the end of its booked two-hour
        /// block, which is always "today" by construction — every standing delivery would otherwise
        /// read as a same-day rush and quietly double the revenue of the entire recurring economy.
        /// There is no rush bonus on recurring work: it's the routine the player signed up for, not a
        /// gamble they took, and Tad's rule is that it earns a small satisfaction nudge instead
        /// (OrderArrivalService.RewardSatisfaction).
        ///
        /// Derived from the dates rather than stored as its own flag so it can't drift out of step
        /// with the deadline, and so orders restored from older saves classify correctly with no
        /// migration.
        /// </summary>
        public bool IsSameDayRush => IsBulk && DueDay <= CreatedDayNumber;

        /// <summary>True when a same-day rush is being closed out on time and has earned its bonus.
        /// A rush that slipped past midnight bills at the ordinary rate AND eats the ordinary late
        /// fee — it doesn't get to keep the double for turning up eventually.</summary>
        public bool QualifiesForSameDayBonus(int currentDay) => IsSameDayRush && currentDay <= DueDay;

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
        /// <summary>Empty in a save written before OrderNumber existed — Import regenerates one on
        /// load rather than leaving it blank, so an old save still gets friendly numbers going
        /// forward.</summary>
        public string orderNumber;
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
        /// <summary>False in a save written before the late-load fine existed — correct, since
        /// nothing in such a save could have been charged it.</summary>
        public bool hasBeenLateLoadFined;
        public string contractId;
        /// <summary>0 in a save written before this field existed — Import treats 0 as "unset" and
        /// falls back to the old flat 25%, so old saves keep their original fine behaviour.</summary>
        public float lateFeePercent;
        /// <summary>RETIRED field, kept only so a save written before Wholesale merged into Bulk
        /// still deserializes without error. Never written by Export any more; Import folds it into
        /// isBulk (see OrderService.Import).</summary>
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
