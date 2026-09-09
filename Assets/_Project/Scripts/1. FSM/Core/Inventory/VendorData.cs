using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// What kind of house this is. Tiers are gated by Reputation, but they are NOT a straight
    /// power curve — see the Anti-Obsolescence Rule in PURCHASING_DESIGN.md. They differ on a
    /// RISK/VELOCITY axis: staples are thin-margin, constant-demand and forgiving; specialty is
    /// fat-margin, lumpy-demand, slot-hungry and punishing to be short on. A mature player still
    /// runs flour as their base load, because fill rate is the score and staples are what keep it
    /// healthy across the constant orders.
    ///
    /// NOTE: append new values at the END — authored assets serialize this by ordinal.
    /// </summary>
    public enum VendorTier
    {
        Staples,     // everyone's first call
        Household,   // the middle of the catalogue
        Specialty,   // high-value, gatekeepy
        Broker       // salvage / close-out — Phase 4, no assets yet
    }

    /// <summary>
    /// One supplier: who they are, what they carry, and the smallest order they'll accept.
    ///
    /// Deliberately mirrors ContractData's shape and spirit — that's the outbound counterpart, and
    /// the two sides of the same trade should not look like they came from different games. Same
    /// reasoning applies to keeping this a separate asset from the SKUs it sells: a SKU is a product,
    /// a vendor is a commercial relationship, and the same product should be able to appear at two
    /// houses on different terms.
    ///
    /// REWORKED for the Partnership Level economy: cost, fill rate and damaged-goods rate are no
    /// longer static per-vendor fields on this asset. They're derived, on demand, from a per-vendor
    /// runtime Partnership Level (-100..100), randomized fresh each playthrough and owned by
    /// VendorEconomyService/VendorRuntimeState — see PartnershipTierUtility.GetProfile. That's why
    /// ReputationRequired, PriceMultiplier and ShortShipmentChance were removed from here outright
    /// rather than kept alongside the new system: a static per-vendor multiplier and a
    /// Partnership-driven modifier answering the same question would just be two numbers that could
    /// disagree.
    /// </summary>
    [CreateAssetMenu(fileName = "Vendor_", menuName = "Warehouse/Vendor")]
    public class VendorData : ScriptableObject
    {
        [SerializeField] private string _displayName = "New Vendor";
        [TextArea(2, 4)]
        [SerializeField] private string _pitch = "";
        [SerializeField] private Sprite _icon;

        [SerializeField] private VendorTier _tier = VendorTier.Staples;

        [Tooltip("Smallest order this vendor will accept, in cases. 0 = no minimum.")]
        [SerializeField] private int _minimumOrderCases;

        [Tooltip("Smallest order this vendor will accept, in dollars. 0 = no minimum.")]
        [SerializeField] private int _minimumOrderDollars;

        [Tooltip("The SKUs this vendor carries, each with a rarity tier that gates it behind a " +
                 "Partnership Level (see VendorEconomyService.GetAvailableCatalogue). A SKU may " +
                 "appear at more than one vendor, at different rarities.")]
        [SerializeField] private List<VendorCatalogueEntry> _catalogue = new();

        /// <summary>Identity is the asset name, same convention as ContractData.ContractId — it's
        /// stable, unique by construction, and what gets written into ShipmentData.SupplierId.</summary>
        public string VendorId => name;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string Pitch => _pitch;
        public Sprite Icon => _icon;
        public VendorTier Tier => _tier;
        public int MinimumOrderCases => Mathf.Max(0, _minimumOrderCases);
        public int MinimumOrderDollars => Mathf.Max(0, _minimumOrderDollars);
        public IReadOnlyList<VendorCatalogueEntry> Catalogue => _catalogue;

        public bool Carries(string skuId)
        {
            foreach (var entry in _catalogue)
                if (entry?.Sku != null && entry.Sku.SkuId == skuId) return true;
            return false;
        }
    }
}
