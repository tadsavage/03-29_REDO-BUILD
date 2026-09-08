using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Events;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>
    /// Central service owning all 20 VendorRuntimeState instances and answering every question the
    /// VENDORS tab and the Purchasing Tab need about a vendor's current economics.
    ///
    /// THE SINGLE SEAM all UI reads through — the VENDORS tab grid, the vendor list status dot, and
    /// the Purchasing Tab's filtered item list all call into this service rather than touching
    /// VendorData/VendorRuntimeState directly, so a Partnership Level change automatically reflects
    /// everywhere once it fires OnPartnershipLevelChanged on both notification channels below.
    ///
    /// Partnership Level is NOT derived from Reputation and does not read it — data flows one
    /// direction only, from a per-vendor Partnership change outward into the global Reputation score
    /// (see ReputationService.HandleVendorPartnershipChanged).
    /// </summary>
    public class VendorEconomyService : IService
    {
        private readonly Dictionary<string, VendorRuntimeState> _stateByVendorId = new();
        private EventManager _eventManager;

        /// <summary>Static C# event for backend service-to-service consumption (namely
        /// ReputationService), following the exact same convention as OrderService's
        /// OnOrderShipped/OnOrderFined/OnOrderCancelled — a subscriber doesn't need a reference to
        /// this service instance. (vendorId, delta, reason)</summary>
        public static event System.Action<string, int, string> OnPartnershipLevelChanged;

        public void Initialize()
        {
            _eventManager = EventManager.Instance;
            _stateByVendorId.Clear();

            var registry = VendorRegistry.Load();
            if (registry == null) return;

            foreach (var vendor in registry.AllVendors)
            {
                if (vendor == null || _stateByVendorId.ContainsKey(vendor.VendorId)) continue;

                _stateByVendorId[vendor.VendorId] = new VendorRuntimeState
                {
                    VendorId = vendor.VendorId,
                    PartnershipLevel = Random.Range(-100, 101),
                    // 1.0-4.0 hours in 0.5 steps: Random.Range(2,9) inclusive-exclusive gives 2..8,
                    // times 0.5 gives 1.0..4.0.
                    TravelTimeHours = Random.Range(2, 9) * 0.5f
                };
            }
        }

        public void Shutdown() => _stateByVendorId.Clear();

        public void ClearAll() => _stateByVendorId.Clear();

        public VendorRuntimeState GetState(string vendorId)
        {
            if (string.IsNullOrEmpty(vendorId)) return null;
            return _stateByVendorId.TryGetValue(vendorId, out var state) ? state : null;
        }

        /// <summary>
        /// Clamps the vendor's PartnershipLevel to [-100, 100], updates its VendorRuntimeState, and
        /// raises BOTH notification channels: the static C# event for backend consumers (e.g.
        /// ReputationService) and the EventManager string-ID event for UI consumers
        /// (VendorsTabView.Refresh). Two channels, matching the project's existing dual pattern.
        /// </summary>
        public void AdjustPartnershipLevel(string vendorId, int delta, string reason)
        {
            var state = GetState(vendorId);
            if (state == null || delta == 0) return;

            state.PartnershipLevel = Mathf.Clamp(state.PartnershipLevel + delta, -100, 100);

            OnPartnershipLevelChanged?.Invoke(vendorId, delta, reason);
            _eventManager?.Publish(GameEvents.Vendor.OnPartnershipLevelChanged, vendorId);
        }

        /// <summary>Catalogue entries currently unlocked at this vendor's Partnership Level. Cumulative
        /// by design: "at or below" the tier's MaxRarityUnlocked, not "exactly equal to" — unlocking
        /// Rare doesn't hide Uncommon items already unlocked at a lower tier.</summary>
        public List<VendorCatalogueEntry> GetAvailableCatalogue(string vendorId)
        {
            var registry = VendorRegistry.Load();
            var vendor = registry?.GetById(vendorId);
            var state = GetState(vendorId);
            if (vendor == null || state == null) return new List<VendorCatalogueEntry>();

            int maxRarity = (int)state.CurrentProfile.MaxRarityUnlocked;
            return vendor.Catalogue.Where(e => e != null && (int)e.Rarity <= maxRarity).ToList();
        }

        /// <summary>What one case of this SKU costs at this vendor today: the market price, marked
        /// up or down by the vendor's current Partnership-driven cost modifier.</summary>
        public float GetEffectiveCost(string vendorId, SkuData sku, MarketService market)
        {
            if (sku == null) return 0f;
            var state = GetState(vendorId);
            if (state == null) return market != null ? market.CurrentPrice(sku) : sku.BuyValue;

            float basePrice = market != null ? market.CurrentPrice(sku) : sku.BuyValue;
            float modifier = 1f + (state.CurrentProfile.CostModifierPercent / 100f);
            return Mathf.Max(1f, basePrice * modifier);
        }

        public float GetFillRate(string vendorId) => GetState(vendorId)?.CurrentProfile.FillRatePercent ?? 0f;

        public float GetDamagedGoodsRate(string vendorId) => GetState(vendorId)?.CurrentProfile.DamagedGoodsPercent ?? 0f;

        public int GetItemsAvailableCount(string vendorId) => GetAvailableCatalogue(vendorId).Count;

        public float GetTravelTimeHours(string vendorId) => GetState(vendorId)?.TravelTimeHours ?? 0f;

        /// <summary>
        /// "Pot Scratch Items": how many of this vendor's catalogue SKUs are wanted by an active
        /// outbound order right now with zero stock on hand, and aren't already covered by an
        /// in-transit/receiving inbound shipment. Composes OrderService (demand), InventoryService
        /// (on-hand stock) and ShipmentService (inbound coverage) — no single existing method answers
        /// this, so it's assembled here rather than added to any one of those services.
        /// </summary>
        public int GetPotScratchCount(string vendorId)
        {
            var registry = VendorRegistry.Load();
            var vendor = registry?.GetById(vendorId);
            if (vendor == null) return 0;

            ServiceLocator.TryGet<OrderService>(out var orders);
            ServiceLocator.TryGet<InventoryService>(out var inventory);
            ServiceLocator.TryGet<ShipmentService>(out var shipments);
            if (orders == null || inventory == null) return 0;

            var neededSkuIds = new HashSet<string>();
            foreach (var order in orders.ActiveOrders)
            {
                if (order == null) continue;
                if (order.Status != OrderData.OrderStatus.Pending &&
                    order.Status != OrderData.OrderStatus.PartiallyPicked) continue;

                foreach (var line in order.LineItems)
                {
                    if (line == null || string.IsNullOrEmpty(line.SkuId)) continue;
                    if (orders.SelectableRemaining(order, line) > 0) neededSkuIds.Add(line.SkuId);
                }
            }

            int count = 0;
            foreach (var entry in vendor.Catalogue)
            {
                var sku = entry?.Sku;
                if (sku == null || !neededSkuIds.Contains(sku.SkuId)) continue;
                if (inventory.TotalOnHand(sku.SkuId) > 0) continue;

                bool alreadyInbound = shipments != null && shipments.PendingShipments.Any(s =>
                    s != null &&
                    (s.Status == ShipmentData.ShipmentStatus.InTransit ||
                     s.Status == ShipmentData.ShipmentStatus.Receiving ||
                     s.Status == ShipmentData.ShipmentStatus.Delayed) &&
                    s.LineItems.Any(li => li.SkuId == sku.SkuId && li.ReceivedQuantity < li.Quantity));
                if (alreadyInbound) continue;

                count++;
            }
            return count;
        }

        /// <summary>
        /// "Best Price Items": how many SKUs in this vendor's currently-unlocked catalogue are cheaper
        /// here (today's effective cost) than at every other vendor that also carries the same SKU.
        /// Ties count as a win for both/all vendors carrying that price.
        /// </summary>
        public int GetBestPriceCount(string vendorId)
        {
            var registry = VendorRegistry.Load();
            if (registry == null) return 0;

            ServiceLocator.TryGet<MarketService>(out var market);
            var mine = GetAvailableCatalogue(vendorId);
            int count = 0;

            foreach (var entry in mine)
            {
                var sku = entry?.Sku;
                if (sku == null) continue;

                float myCost = GetEffectiveCost(vendorId, sku, market);
                bool isBest = true;

                foreach (var other in registry.AllVendors)
                {
                    if (other == null || other.VendorId == vendorId) continue;
                    if (!GetAvailableCatalogue(other.VendorId).Any(e => e?.Sku != null && e.Sku.SkuId == sku.SkuId)) continue;

                    float otherCost = GetEffectiveCost(other.VendorId, sku, market);
                    if (otherCost < myCost) { isBest = false; break; }
                }

                if (isBest) count++;
            }
            return count;
        }

        /// <summary>
        /// Total quantity of this SKU currently needed by active outbound orders — the "IN DEMAND"
        /// figure on the multi-vendor Inbound Order Creation New tab. A property of the SKU/warehouse,
        /// not of any one vendor (every vendor's listing of this SKU shows the same number). Reuses the
        /// exact per-line "remaining need" unit GetPotScratchCount already uses (SelectableRemaining),
        /// just summed instead of turned into a yes/no set.
        /// </summary>
        public int GetTotalInDemand(string skuId)
        {
            if (string.IsNullOrEmpty(skuId)) return 0;
            if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null) return 0;

            int total = 0;
            foreach (var order in orders.ActiveOrders)
            {
                if (order == null) continue;
                if (order.Status != OrderData.OrderStatus.Pending &&
                    order.Status != OrderData.OrderStatus.PartiallyPicked) continue;

                foreach (var line in order.LineItems)
                {
                    if (line == null || line.SkuId != skuId) continue;
                    total += orders.SelectableRemaining(order, line);
                }
            }
            return total;
        }

        /// <summary>
        /// Total quantity of this SKU already inbound on a live (not yet fully received) shipment —
        /// the "ON ORDER" figure alongside GetTotalInDemand. Same shipment-status filter
        /// GetPotScratchCount uses for "already covered," just summed rather than checked as a bool.
        /// </summary>
        public int GetTotalOnOrder(string skuId)
        {
            if (string.IsNullOrEmpty(skuId)) return 0;
            if (!ServiceLocator.TryGet<ShipmentService>(out var shipments) || shipments == null) return 0;

            int total = 0;
            foreach (var s in shipments.PendingShipments)
            {
                if (s == null) continue;
                if (s.Status != ShipmentData.ShipmentStatus.InTransit &&
                    s.Status != ShipmentData.ShipmentStatus.Receiving &&
                    s.Status != ShipmentData.ShipmentStatus.Delayed) continue;

                foreach (var li in s.LineItems)
                {
                    if (li == null || li.SkuId != skuId) continue;
                    total += Mathf.Max(0, li.Quantity - li.ReceivedQuantity);
                }
            }
            return total;
        }
    }
}
