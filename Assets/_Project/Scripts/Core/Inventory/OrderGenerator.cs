using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Builds a randomized customer order: a random customer from the roster, 2-4 random SKUs,
    /// small quantities per line. Outbound analog of RandomDeliveryGenerator. Deliberately small —
    /// this is for exercising the Order Selection → staging → loading pipeline end-to-end before
    /// real order-sizing/SLA-pressure tuning happens.
    /// </summary>
    public static class OrderGenerator
    {
        public const int MinLineItems = 2;
        public const int MaxLineItems = 4; // inclusive
        public const int MinQuantityPerLine = 1;
        public const int MaxQuantityPerLine = 5; // inclusive
        public const int DefaultDueDaysOut = 2;

        public static OrderData GenerateRandomOrder(CustomerRegistry customerRegistry, InventoryService inventoryService,
            int currentDay, int currentMinute, System.Random rand = null)
        {
            rand ??= new System.Random();
            if (customerRegistry == null || inventoryService == null) return null;

            var customer = customerRegistry.GetRandom();
            if (customer == null)
            {
                Debug.LogWarning("[OrderGenerator] CustomerRegistry has no customers.");
                return null;
            }

            // Only SKUs with a real sell price are orderable.
            var eligible = inventoryService.AllSkus.Where(s => s != null && s.SellValue > 0f).ToList();
            if (eligible.Count == 0)
            {
                Debug.LogWarning("[OrderGenerator] No eligible SKUs found.");
                return null;
            }

            int lineCount = Mathf.Min(eligible.Count, rand.Next(MinLineItems, MaxLineItems + 1));
            var chosen = eligible.OrderBy(_ => rand.Next()).Take(lineCount).ToList();

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

            Debug.Log($"[OrderGenerator] Generated order {order.OrderId} for {customer.CompanyName}: " +
                      $"{order.LineItems.Count} line items, {order.TotalUnits} total units, due day {order.DueDay}.");
            return order;
        }
    }
}
