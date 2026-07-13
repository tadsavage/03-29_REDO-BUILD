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

        public ShipmentData(string supplierId, string supplierName, int arrivalDay, int arrivalMinute)
        {
            PONumber = PONumberGenerator.GetNextPONumber();
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

        /// <summary>Check if all line items have been fully received.</summary>
        public bool IsFullyReceived => LineItems.Count > 0 && LineItems.All(item => item.ReceivedQuantity >= item.Quantity);

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
    }
}
