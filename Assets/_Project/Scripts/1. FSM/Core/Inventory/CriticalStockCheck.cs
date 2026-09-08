using System.Collections.Generic;
using System.Linq;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>
    /// Shared "is this SKU urgently needed" check, used by guard-shack/side-lot dispatch announcements
    /// that don't have a single order line's own quantity to compare against (an inbound PO line
    /// represents units ARRIVING, not units a specific order needs). Mirrors the definition
    /// SchedulerPanel.SummarizeOrderLines already uses for a single order line — on-hand stock can't
    /// cover what's needed — generalized here to "on-hand can't cover AGGREGATE demand across every
    /// active order for that SKU", since a guard announcement only has the incoming SKU, not an order.
    /// </summary>
    public static class CriticalStockCheck
    {
        /// <summary>Counts how many of the given SKU ids (one per line/pallet — duplicates count
        /// separately, matching "N critical items on this load" rather than "N critical SKUs") are
        /// critical: current on-hand stock is less than the combined QuantityNeeded across every active
        /// order for that SKU. Returns 0 if the required services aren't registered yet.</summary>
        public static int CountCriticalLines(IEnumerable<string> skuIds)
        {
            if (skuIds == null) return 0;
            if (!ServiceLocator.TryGet<InventoryService>(out var inventory) || inventory == null) return 0;
            if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null) return 0;

            var neededCache = new Dictionary<string, int>();
            int NeededFor(string skuId)
            {
                if (neededCache.TryGetValue(skuId, out var cached)) return cached;
                int needed = orders.ActiveOrders
                    .SelectMany(o => o.LineItems)
                    .Where(li => li.SkuId == skuId)
                    .Sum(li => li.QuantityNeeded);
                neededCache[skuId] = needed;
                return needed;
            }

            int critical = 0;
            foreach (var skuId in skuIds)
            {
                if (string.IsNullOrEmpty(skuId)) continue;
                if (inventory.GetTotalUnitsBySku(skuId) < NeededFor(skuId)) critical++;
            }
            return critical;
        }
    }
}
