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
                    PartnershipLevel = Random.Range(-100, 101)
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
    }
}
