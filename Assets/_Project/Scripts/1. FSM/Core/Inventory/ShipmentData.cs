using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Represents an inbound shipment (Purchase Order) from a supplier.
    /// Contains list of SKUs arriving, and metadata for tracking and billing.
    /// </summary>
    [System.Serializable]
    public class ShipmentData
    {
        public string PONumber { get; private set; }
        public string SupplierId { get; set; }
        public string SupplierName { get; set; }
        public int ArrivalDayNumber { get; set; }
        public int ArrivalTimeMinute { get; set; } // 0-1440 (minutes in day)
        public List<ShipmentLineItem> LineItems { get; set; } = new();
        public ShipmentStatus Status { get; set; } = ShipmentStatus.InTransit;

        /// <summary>
        /// True for a PO the PLAYER raised through the Purchasing panel, false for one the dev tools
        /// or the random-delivery generator produced.
        ///
        /// Drives two things that have to differ. A player PO shows its own random number on the PO
        /// List and is the player's money; a generated one is scenery for testing. And a player PO is
        /// billed at creation — you pay when you order, not when the truck shows up — which must not
        /// happen for generated ones or the dev buttons would quietly drain capital.
        /// </summary>
        public bool PlayerOrdered { get; set; }

        /// <summary>
        /// True for a trailer bought sight-unseen from the broker.
        ///
        /// Drives one thing only: the Purchasing panel hides this PO's line items until something has
        /// actually been received against it. That concealment IS the product — a salvage load whose
        /// contents you can read off the PO list the moment you buy it is just a discounted order.
        /// </summary>
        public bool IsSalvage { get; set; }

        /// <summary>True once anything on this shipment has physically landed. What the panel uses to
        /// decide whether a salvage manifest is still a secret.</summary>
        public bool AnyReceived => LineItems.Any(li => li.ReceivedQuantity > 0);

        public ShipmentData(string supplierId, string supplierName, int arrivalDay, int arrivalMinute)
        {
            // 'G' is hardcoded rather than derived from LineItems (still empty at this point anyway):
            // Perishable/Frozen inventory doesn't exist in the game yet, so Grocery is the only
            // reachable area — same fallback OrderService.DominantOrderNumberPrefix uses.
            PONumber = OrderNumberGenerator.GetNext('I', 'G', arrivalDay);
            SupplierId = supplierId;
            SupplierName = supplierName;
            ArrivalDayNumber = arrivalDay;
            ArrivalTimeMinute = arrivalMinute;
        }

        /// <summary>Creates a PO carrying a number chosen by the caller — the Purchasing panel shows
        /// the number on screen BEFORE the order exists ("PO #: 837194" sits above the item list while
        /// you're still filling it in), so the number has to be reserved first and handed in, not
        /// minted here where the panel would never see it.</summary>
        public ShipmentData(string poNumber, string supplierId, string supplierName,
                            int arrivalDay, int arrivalMinute)
        {
            PONumber = string.IsNullOrEmpty(poNumber) ? OrderNumberGenerator.GetNext('I', 'G', arrivalDay) : poNumber;
            SupplierId = supplierId;
            SupplierName = supplierName;
            ArrivalDayNumber = arrivalDay;
            ArrivalTimeMinute = arrivalMinute;
        }

        /// <summary>Restore-only constructor — reconstructs a shipment with its original saved
        /// PONumber instead of minting a new one via PONumberGenerator. Used by
        /// ShipmentService.Import when restoring from a save file.</summary>
        internal ShipmentData(string poNumber, string supplierId, string supplierName, int arrivalDay, int arrivalMinute, ShipmentStatus status)
        {
            PONumber = poNumber;
            SupplierId = supplierId;
            SupplierName = supplierName;
            ArrivalDayNumber = arrivalDay;
            ArrivalTimeMinute = arrivalMinute;
            Status = status;
        }

        /// <summary>Total units across all line items (expected).</summary>
        public int TotalUnits => LineItems.Sum(item => item.Quantity);

        /// <summary>Total units actually received across all line items.</summary>
        public int TotalReceivedUnits => LineItems.Sum(item => item.ReceivedQuantity);

        /// <summary>Total cost of shipment at expected quantity.</summary>
        public int TotalCost => LineItems.Sum(item => item.Quantity * item.UnitCost);

        /// <summary>Total cost of shipment based on actual received quantity (for invoicing).</summary>
        public int TotalReceivedCost => LineItems.Sum(item => item.TotalReceivedCost);

        /// <summary>Total overage across all line items (positive = more received than expected).</summary>
        public int TotalOverage => LineItems.Sum(item => item.Overage);

        /// <summary>Total shortage across all line items (positive = less received than expected).</summary>
        public int TotalShortage => LineItems.Sum(item => item.Shortage);

        /// <summary>Check if all line items have been fully received — a line the player accepted
        /// CreditTaken on is exempted permanently instead of blocking this forever, since a credited
        /// shortage was never going to physically arrive.</summary>
        public bool IsFullyReceived => LineItems.Count > 0 &&
            LineItems.All(item => item.ReceivedQuantity >= item.Quantity || (item.Dropped && item.CreditTaken));

        /// <summary>True while this PO has a short-shipped line the player hasn't yet requested a
        /// backfill or accepted credit for — drives the Scheduler's Request Backfill/Request Credit
        /// buttons.</summary>
        public bool HasUnresolvedShortage => LineItems.Any(li => li.Dropped && !li.CreditTaken && li.ReceivedQuantity < li.Quantity);

        /// <summary>Update received quantity for a specific line item SKU. Picks the first line
        /// item for that SKU that ISN'T already fully received — a shipment with multiple pallets
        /// of the same SKU (the common case: one line item per pallet) has multiple line items
        /// sharing a SkuId, and always filling the first match would leave every other one stuck
        /// at 0 forever, so IsFullyReceived would never become true even once every pallet is in.</summary>
        public void UpdateReceivedQuantity(string skuId, int additionalQuantity)
        {
            var lineItem = LineItems.FirstOrDefault(li => li.SkuId == skuId && li.ReceivedQuantity < li.Quantity);
            if (lineItem != null)
            {
                lineItem.ReceivedQuantity = Mathf.Min(lineItem.ReceivedQuantity + additionalQuantity, lineItem.Quantity);
            }
        }

        public enum ShipmentStatus { InTransit, Receiving, Received, Departed, Cancelled, Delayed }
    }

    /// <summary>A single line item in a shipment (SKU + qty + pricing).</summary>
    [System.Serializable]
    public class ShipmentLineItem
    {
        public string SkuId { get; set; }
        public int Quantity { get; set; } // Expected quantity
        public int ReceivedQuantity { get; set; } // Actual quantity received (updated as pallets are received)
        public int UnitCost { get; set; } // Cost per unit (what we paid supplier)
        public int ShelfLifeDays { get; set; } // -1 if non-perishable

        /// <summary>Which trailer floor position (0..11) this line item's pallet occupies, and
        /// which vertical tier (0 = floor, 1 = stacked on top of tier 0's pallet at that same
        /// slot). -1 = unset — TruckController.LoadShipment falls back to its legacy fixed
        /// 12-slot round-robin cycle when a shipment's line items don't carry this. Set by
        /// RandomDeliveryGenerator, which is the only producer of real slot/tier assignments today.</summary>
        public int FloorSlotIndex { get; set; } = -1;
        public int PalletTier { get; set; } = 0;

        /// <summary>
        /// True when the supplier short-shipped this pallet: it was ordered and paid for, but it
        /// never physically made the truck.
        ///
        /// Rolled once at dispatch by ShipmentService.ApplySupplierVariance, and read by
        /// TruckController.LoadShipment, which skips building a pallet for it. Deliberately a FLAG
        /// rather than removing the line item — Quantity is what was ORDERED, and deleting the line
        /// would erase the evidence that anything was missing. Left in place, ReceivedQuantity stays
        /// 0 and <see cref="Shortage"/> reports the full pallet, which is what makes the PO list able
        /// to say "2 short" instead of quietly showing a smaller order than the player raised.
        ///
        /// This is the field that finally loads a gun the codebase has had built for months: before
        /// it, the trailer was always constructed from the PO, so received could never differ from
        /// ordered and Overage/Shortage were structurally always zero.
        /// </summary>
        public bool Dropped { get; set; }

        /// <summary>
        /// True once the player has accepted the vendor's credit for this short-shipped line instead
        /// of requesting a backfill delivery. Permanently exempts the line from
        /// <see cref="ShipmentData.IsFullyReceived"/> — set by ShipmentService.RequestCredit.
        /// </summary>
        public bool CreditTaken { get; set; }

        /// <summary>
        /// What this pallet turned out to be, when it came off a BROKER load. Ordinary for everything
        /// bought through a normal vendor.
        ///
        /// Set at offer time by BrokerService and carried on the manifest even for a Damaged pallet
        /// (which also sets <see cref="Dropped"/>, so nothing is built for it). Keeping the condition
        /// rather than just dropping the line is what lets the PO list say "3 damaged, written off"
        /// instead of quietly handing back a smaller trailer than the one that was sold.
        /// </summary>
        public SalvageCondition Salvage { get; set; } = SalvageCondition.Ordinary;

        public ShipmentLineItem(string skuId, int quantity, int unitCost, int shelfLifeDays)
        {
            SkuId = skuId;
            Quantity = quantity;
            ReceivedQuantity = 0;
            UnitCost = unitCost;
            ShelfLifeDays = shelfLifeDays;
        }

        public int TotalCost => Quantity * UnitCost;
        public int TotalReceivedCost => ReceivedQuantity * UnitCost;
        public int Overage => ReceivedQuantity - Quantity;
        public int Shortage => Quantity - ReceivedQuantity;
    }

    /// <summary>JSON-serializable snapshot of a ShipmentData for save/load. ShipmentData itself uses
    /// auto-properties (JsonUtility can't serialize those directly), so this plain-field mirror is
    /// what actually goes in SaveData — see ShipmentService.Export()/Import().</summary>
    [System.Serializable]
    public class ShipmentSnapshot
    {
        public string poNumber;
        public string supplierId;
        public string supplierName;
        public int arrivalDayNumber;
        public int arrivalTimeMinute;
        public int status; // (int)ShipmentData.ShipmentStatus
        /// <summary>False in a save written before player purchasing existed — correct, since every PO
        /// in such a save came from the dev tools or the delivery generator.</summary>
        public bool playerOrdered;
        public bool isSalvage;
        public List<ShipmentLineItemSnapshot> lineItems = new();
    }

    [System.Serializable]
    public class ShipmentLineItemSnapshot
    {
        public string skuId;
        public int quantity;
        public int receivedQuantity;
        public int unitCost;
        public int shelfLifeDays;
        public int floorSlotIndex;
        public int palletTier;
        /// <summary>False in a save written before supplier variance existed — correct, since every
        /// PO in such a save arrived complete by construction.</summary>
        public bool dropped;
        /// <summary>False in a save written before backfill/credit existed — correct, since no such
        /// resolution could have been recorded yet.</summary>
        public bool creditTaken;
        /// <summary>0 (Ordinary) in a save written before broker loads existed — correct, since every
        /// PO in such a save came through a normal vendor.</summary>
        public int salvage;
    }
}
