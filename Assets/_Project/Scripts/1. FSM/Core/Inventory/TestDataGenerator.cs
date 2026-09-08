using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;

/// <summary>
/// Generates realistic test data (shipments and orders) from the imported SKU database.
/// Used for prototyping and testing the warehouse gameplay loop.
/// </summary>
public class TestDataGenerator : MonoBehaviour
{
    [SerializeField] private SkuData[] _allSkus;
#pragma warning disable CS0414
    [SerializeField] private int _itemsPerShipment = 5;
    [SerializeField] private int _itemsPerOrder = 3;
#pragma warning restore CS0414

    private System.Random _random;

    private void Start()
    {
        _random = new System.Random();

        // Find all imported SkuData assets
        if (_allSkus == null || _allSkus.Length == 0)
            _allSkus = Resources.LoadAll<SkuData>("Inventory/SKUs");

        if (_allSkus.Length == 0)
        {
            Debug.LogError("[TestDataGenerator] No SKU data found. Create SkuData assets under Assets/_Project/Resources/Inventory/SKUs.");
            return;
        }

    }

    /// <summary>Generate a random shipment from supplier.</summary>
    public ShipmentData GenerateShipment(int dayNumber, int timeMinute)
    {
        var supplierNames = new[] { "AMERICAS BEV JUICE", "Coca Cola", "Pepsi", "Generic Distributor", "Regional Supplier" };
        string supplier = supplierNames[_random.Next(supplierNames.Length)];
        string supplierId = $"SUPP_{_random.Next(1000, 9999)}";

        var shipment = new ShipmentData(supplierId, supplier, dayNumber, timeMinute);

        // Add 3-7 random line items
        int itemCount = _random.Next(3, 8);
        for (int i = 0; i < itemCount; i++)
        {
            var sku = _allSkus[_random.Next(_allSkus.Length)];
            int quantity = _random.Next(10, 100); // Cases
            int shelfLifeDays = sku.ShelfLifeDays;

            var lineItem = new ShipmentLineItem(
                skuId: sku.SkuId,
                quantity: quantity,
                unitCost: Mathf.RoundToInt(sku.BuyValue),
                shelfLifeDays: shelfLifeDays
            );
            shipment.LineItems.Add(lineItem);
        }

        return shipment;
    }

    /// <summary>Generate a random customer order.</summary>
    public OrderData GenerateOrder(int dayNumber, int timeMinute)
    {
        var customerNames = new[] { "Retail Store A", "Grocery Chain B", "Restaurant Group C", "Convenience Stores", "Corporate Cafeteria" };
        string customerName = customerNames[_random.Next(customerNames.Length)];
        string customerId = $"CUST_{_random.Next(1000, 9999)}";
        string address = $"{_random.Next(100, 999)} Main St, City, State 12345";

        // Due date: today to 3 days out
        int dueDay = dayNumber + _random.Next(0, 4);

        var order = new OrderData(customerId, customerName, address, dayNumber, dueDay, timeMinute);

        // Add 2-5 random line items
        int itemCount = _random.Next(2, 6);
        for (int i = 0; i < itemCount; i++)
        {
            var sku = _allSkus[_random.Next(_allSkus.Length)];
            int quantity = _random.Next(5, 50); // Cases

            var lineItem = new OrderLineItem(
                skuId: sku.SkuId,
                quantityNeeded: quantity,
                unitCost: Mathf.RoundToInt(sku.BuyValue),
                sellingPrice: Mathf.RoundToInt(sku.SellValue)
            );
            order.LineItems.Add(lineItem);
        }

        return order;
    }

    /// <summary>Generate a full day's worth of shipments and orders.</summary>
    public (List<ShipmentData> shipments, List<OrderData> orders) GenerateDay(int dayNumber)
    {
        var shipments = new List<ShipmentData>();
        var orders = new List<OrderData>();

        // Generate 2-4 shipments during the day
        int shipmentCount = _random.Next(2, 5);
        for (int i = 0; i < shipmentCount; i++)
        {
            int timeMinute = _random.Next(0, 1440); // Random time of day
            shipments.Add(GenerateShipment(dayNumber, timeMinute));
        }

        // Generate 3-8 orders during the day
        int orderCount = _random.Next(3, 9);
        for (int i = 0; i < orderCount; i++)
        {
            int timeMinute = _random.Next(0, 1440);
            orders.Add(GenerateOrder(dayNumber, timeMinute));
        }

        return (shipments, orders);
    }

    // ============ EDITOR HELPERS ============

    [ContextMenu("Generate Sample Day")]
    public void DebugGenerateSampleDay()
    {
        if (_allSkus == null || _allSkus.Length == 0)
        {
            Debug.LogError("No SKUs loaded.");
            return;
        }

        var (shipments, orders) = GenerateDay(1);

        Debug.Log($"=== Generated Sample Day ===");
        Debug.Log($"Shipments: {shipments.Count}");
        foreach (var shipment in shipments)
        {
            Debug.Log($"  - {shipment.SupplierName}: {shipment.TotalUnits} units, ${shipment.TotalCost}");
            foreach (var item in shipment.LineItems)
                Debug.Log($"      {item.SkuId}: {item.Quantity} @ ${item.UnitCost}/case");
        }

        Debug.Log($"Orders: {orders.Count}");
        foreach (var order in orders)
        {
            Debug.Log($"  - {order.CustomerName}: {order.TotalUnits} units, ${order.TotalRevenue} revenue");
            foreach (var item in order.LineItems)
                Debug.Log($"      {item.SkuId}: {item.QuantityNeeded} @ ${item.SellingPrice}/case");
        }
    }
}
