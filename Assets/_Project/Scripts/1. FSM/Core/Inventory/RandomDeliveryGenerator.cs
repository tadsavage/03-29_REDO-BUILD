using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Builds a randomized, physically-real inbound delivery: picks 3-5 random SKUs from the
    /// master record and fills the trailer's 12 floor positions with them, double-stacking any
    /// SKU whose pallet is short enough (2 x PltHeight <= trailer interior height) to fit two
    /// high inside the 2m-tall trailer box — so a load can range from 12 up to 24 pallets
    /// depending on what got picked. Per-SKU pallet counts land unevenly by design (each floor
    /// slot is assigned independently at random, not round-robin) — e.g. 3 items might split
    /// 10/10/4 rather than 4/4/4. Added 2026-07-05 per Tad's request to replace the old
    /// hardcoded single-SKU-x12 test delivery with something that actually exercises real Ti/Hi/
    /// PltHeight data and real trailer-height constraints.
    ///
    /// Each output ShipmentLineItem represents exactly ONE pallet (Quantity = sku.Ti * sku.Hi
    /// cases), tagged with FloorSlotIndex/PalletTier so TruckController.LoadShipment can place it
    /// at the correct floor position and stack height instead of cycling line items round-robin.
    /// </summary>
    public static class RandomDeliveryGenerator
    {
        public const int FloorSlotCount = 12;
        public const float TrailerInteriorHeightMeters = 2.0f;

        /// <summary>Chance a double-stack-eligible slot actually gets stacked to 2 (vs. left at
        /// 1) — not maxed to 100% so a generated load doesn't always hit the theoretical ceiling.</summary>
        public const float DoubleStackChance = 0.75f;

        public static List<ShipmentLineItem> GenerateFullTrailerLoad(InventoryService inventoryService, System.Random rand = null)
        {
            rand ??= new System.Random();
            var result = new List<ShipmentLineItem>();
            if (inventoryService == null) return result;

            // Only SKUs that have actually been run through the Pallet Optimizer — a SKU with no
            // committed Ti/Hi has no real pallet to build, so it can't be part of a real delivery.
            var eligible = inventoryService.AllSkus.Where(s => s != null && s.Ti > 0 && s.Hi > 0).ToList();
            if (eligible.Count == 0)
            {
                Debug.LogWarning("[RandomDeliveryGenerator] No SKUs with committed Ti/Hi found — run the Pallet Optimizer batch tool first.");
                return result;
            }

            int itemCount = Mathf.Min(eligible.Count, rand.Next(3, 6)); // 3-5 inclusive
            var chosen = eligible.OrderBy(_ => rand.Next()).Take(itemCount).ToList();

            // Assign each of the 12 floor slots to one of the chosen SKUs. Guarantee every chosen
            // SKU covers at least one slot first, then let the rest fall randomly — this is what
            // produces uneven per-SKU pallet counts (e.g. 10/10/4) instead of a flat round-robin split.
            var slotSkus = new SkuData[FloorSlotCount];
            var slotOrder = Enumerable.Range(0, FloorSlotCount).OrderBy(_ => rand.Next()).ToList();
            for (int i = 0; i < chosen.Count && i < FloorSlotCount; i++)
                slotSkus[slotOrder[i]] = chosen[i];
            for (int i = chosen.Count; i < FloorSlotCount; i++)
                slotSkus[slotOrder[i]] = chosen[rand.Next(chosen.Count)];

            for (int slot = 0; slot < FloorSlotCount; slot++)
            {
                var sku = slotSkus[slot];
                bool canDoubleStack = (sku.PltHeight * 2f) <= TrailerInteriorHeightMeters;
                int tiers = (canDoubleStack && rand.NextDouble() < DoubleStackChance) ? 2 : 1;

                for (int tier = 0; tier < tiers; tier++)
                {
                    result.Add(new ShipmentLineItem(sku.SkuId, sku.Ti * sku.Hi, Mathf.RoundToInt(sku.BuyValue), sku.ShelfLifeDays)
                    {
                        FloorSlotIndex = slot,
                        PalletTier = tier
                    });
                }
            }

            LogSummary(result, slotSkus);
            return result;
        }

        private static void LogSummary(List<ShipmentLineItem> lineItems, SkuData[] slotSkus)
        {
            var perSku = lineItems.GroupBy(li => li.SkuId)
                                   .Select(g => $"{g.Key}={g.Count()} pallets")
                                   .ToArray();
            int doubleStacked = slotSkus.Length > 0
                ? lineItems.Count(li => li.PalletTier == 1)
                : 0;
            Debug.Log($"[RandomDeliveryGenerator] Generated {lineItems.Count} pallets across {FloorSlotCount} floor slots " +
                      $"({doubleStacked} double-stacked): {string.Join(", ", perSku)}");
        }
    }
}
