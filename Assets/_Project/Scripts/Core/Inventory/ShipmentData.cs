using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents an inbound shipment from a supplier.
/// Contains list of SKUs arriving, and metadata for tracking and billing.
/// </summary>
[System.Serializable]
public class ShipmentData
{
    public string ShipmentId { get; private set; }
    public string SupplierId { get; set; }
    public string SupplierName { get; set; }
    public int ArrivalDayNumber { get; set; }
    public int ArrivalTimeMinute { get; set; } // 0-1440 (minutes in day)
    public List<ShipmentLineItem> LineItems { get; set; } = new();
    public ShipmentStatus Status { get; set; } = ShipmentStatus.InTransit;

    public ShipmentData(string supplierId, string supplierName, int arrivalDay, int arrivalMinute)
    {
        ShipmentId = System.Guid.NewGuid().ToString();
        SupplierId = supplierId;
        SupplierName = supplierName;
        ArrivalDayNumber = arrivalDay;
        ArrivalTimeMinute = arrivalMinute;
    }

    /// <summary>Total units across all line items.</summary>
    public int TotalUnits => LineItems.Sum(item => item.Quantity);

    /// <summary>Total cost of shipment (before markup).</summary>
    public int TotalCost => LineItems.Sum(item => item.Quantity * item.UnitCost);

    public enum ShipmentStatus { InTransit, Received, Cancelled, Delayed }
}

/// <summary>A single line item in a shipment (SKU + qty + pricing).</summary>
[System.Serializable]
public class ShipmentLineItem
{
    public string SkuId { get; set; }
    public int Quantity { get; set; }
    public int UnitCost { get; set; } // Cost per unit (what we paid supplier)
    public int ShelfLifeDays { get; set; } // -1 if non-perishable

    public ShipmentLineItem(string skuId, int quantity, int unitCost, int shelfLifeDays)
    {
        SkuId = skuId;
        Quantity = quantity;
        UnitCost = unitCost;
        ShelfLifeDays = shelfLifeDays;
    }

    public int TotalCost => Quantity * UnitCost;
}
