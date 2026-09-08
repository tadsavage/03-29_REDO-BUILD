using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Builds a batch of randomized orders for ONE random customer — 3-5 orders, each 3-5 random
    /// SKUs with 5-10 cases per line. Outbound analog of RandomDeliveryGenerator. One customer per
    /// batch (rather than one per order) so the Work Queue panel's "release these to a lane" flow
    /// has something realistic to group and release together — this is for exercising the Order
    /// Selection → staging → loading pipeline end-to-end before real order-sizing/SLA-pressure
    /// tuning happens.
    /// </summary>
    public static class OrderGenerator
    {
        public const int MinOrdersPerBatch = 3;
        public const int MaxOrdersPerBatch = 5; // inclusive
        public const int MinLineItems = 3;
        public const int MaxLineItems = 5; // inclusive
        public const int MinQuantityPerLine = 5;
        public const int MaxQuantityPerLine = 10; // inclusive
        public const int DefaultDueDaysOut = 2;

        /// <summary>Picks one random customer and generates a batch of 3-5 orders for them.
        /// Returns an empty list (logging why) if the registry/SKU catalog can't support it.</summary>
        public static List<OrderData> GenerateRandomOrdersForCustomer(CustomerRegistry customerRegistry, InventoryService inventoryService,
            int currentDay, int currentMinute, System.Random rand = null)
        {
            rand ??= new System.Random();
            var orders = new List<OrderData>();
            if (customerRegistry == null || inventoryService == null) return orders;

            var customer = customerRegistry.GetRandom();
            if (customer == null)
            {
                Debug.LogWarning("[OrderGenerator] CustomerRegistry has no customers.");
                return orders;
            }

            var eligible = inventoryService.AllSkus.Where(s => s != null && s.SellValue > 0f).ToList();
            if (eligible.Count == 0)
            {
                Debug.LogWarning("[OrderGenerator] No eligible SKUs found.");
                return orders;
            }

            int orderCount = rand.Next(MinOrdersPerBatch, MaxOrdersPerBatch + 1);
            for (int i = 0; i < orderCount; i++)
                orders.Add(BuildOrder(customer, eligible, currentDay, currentMinute, rand));

            Debug.Log($"[OrderGenerator] Generated {orders.Count} order(s) for {customer.CompanyName}.");
            return orders;
        }

        private static OrderData BuildOrder(CustomerData customer, List<SkuData> eligibleSkus,
            int currentDay, int currentMinute, System.Random rand)
        {
            int lineCount = Mathf.Min(eligibleSkus.Count, rand.Next(MinLineItems, MaxLineItems + 1));
            var chosen = eligibleSkus.OrderBy(_ => rand.Next()).Take(lineCount).ToList();

            var order = new OrderData(
                customer.CustomerId,
                customer.CompanyName,
                $"{customer.CompanyName} Distribution Center",
                currentDay,
                currentDay + DefaultDueDaysOut,
                currentMinute);

            foreach (var sku in chosen)
            {
                int qty = rand.Next(MinQuantityPerLine, MaxQuantityPerLine + 1);
                order.LineItems.Add(new OrderLineItem(
                    sku.SkuId,
                    qty,
                    Mathf.RoundToInt(sku.BuyValue),
                    Mathf.RoundToInt(sku.SellValue)));
            }

            Debug.Log($"[OrderGenerator] Order {order.OrderId} for {customer.CompanyName}: " +
                      $"{order.LineItems.Count} line items, {order.TotalUnits} total units, due day {order.DueDay}.");
            return order;
        }
    }
}
