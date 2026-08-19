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
    /// One supplier: who they are, what they carry, what they charge, and how reliably they deliver.
    ///
    /// Deliberately mirrors ContractData's shape and spirit — that's the outbound counterpart, and
    /// the two sides of the same trade should not look like they came from different games. Same
    /// reasoning applies to keeping this a separate asset from the SKUs it sells: a SKU is a product,
    /// a vendor is a commercial relationship, and the same product should be able to appear at two
    /// houses on different terms.
    ///
    /// THREE AXES OF DIFFERENCE, and only three, because all three are actually consumed today:
    ///
    ///   PRICE        <see cref="PriceMultiplier"/> against the market price of the day.
    ///   RELIABILITY  <see cref="ShortShipmentChance"/> — how often they leave pallets behind.
    ///   COMMITMENT   <see cref="MinimumOrderCases"/> — the size you have to buy to deal at all.
    ///
    /// On-time percentage and net-30 payment terms were both drafted and CUT rather than authored
    /// unconsumed. Late delivery and trade credit don't exist yet, and a field nothing reads is how
    /// this codebase ended up with Overage/Shortage sitting dead for months — see
    /// ShipmentLineItem.Dropped for that story. Add them when the mechanic behind them lands.
    /// </summary>
    [CreateAssetMenu(fileName = "Vendor_", menuName = "Warehouse/Vendor")]
    public class VendorData : ScriptableObject
    {
        [SerializeField] private string _displayName = "New Vendor";
        [TextArea(2, 4)]
        [SerializeField] private string _pitch = "";
        [SerializeField] private Sprite _icon;

        [SerializeField] private VendorTier _tier = VendorTier.Staples;

        [Tooltip("Reputation needed before this vendor will deal with you at all.")]
        [SerializeField] private int _reputationRequired;

        [Tooltip("Multiplier on the market price of the day. 0.90 = 10% cheaper than the market, " +
                 "1.12 = a 12% premium for reliability.")]
        [SerializeField] private float _priceMultiplier = 1f;

        [Range(0f, 1f)]
        [Tooltip("Chance a purchase order from this vendor arrives short by 1-3 pallets.")]
        [SerializeField] private float _shortShipmentChance = 0.2f;

        [Tooltip("Smallest order this vendor will accept, in cases. 0 = no minimum.")]
        [SerializeField] private int _minimumOrderCases;

        [Tooltip("The SKUs this vendor carries. A SKU may appear at more than one vendor.")]
        [SerializeField] private List<SkuData> _catalogue = new();

        /// <summary>Identity is the asset name, same convention as ContractData.ContractId — it's
        /// stable, unique by construction, and what gets written into ShipmentData.SupplierId.</summary>
        public string VendorId => name;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string Pitch => _pitch;
        public Sprite Icon => _icon;
        public VendorTier Tier => _tier;
        public int ReputationRequired => _reputationRequired;
        public float PriceMultiplier => Mathf.Max(0.01f, _priceMultiplier);
        public float ShortShipmentChance => Mathf.Clamp01(_shortShipmentChance);
        public int MinimumOrderCases => Mathf.Max(0, _minimumOrderCases);
        public IReadOnlyList<SkuData> Catalogue => _catalogue;

        /// <summary>Human-readable reliability, for the vendor card. Inverted from the raw chance
        /// because "94% reliable" is a thing a player can compare at a glance and "0.06 short-ship
        /// probability" is not.</summary>
        public int ReliabilityPercent => Mathf.RoundToInt((1f - ShortShipmentChance) * 100f);

        public bool Carries(string skuId)
        {
            foreach (var s in _catalogue)
                if (s != null && s.SkuId == skuId) return true;
            return false;
        }

        /// <summary>What one case of this SKU costs HERE today: the market price, marked up or down by
        /// this vendor's own multiplier. Returns 0 for anything they don't carry, so a caller can't
        /// accidentally price a SKU against a vendor that never offered it.</summary>
        public int PriceFor(SkuData sku, MarketService market)
        {
            if (sku == null || !Carries(sku.SkuId)) return 0;
            int marketPrice = market != null ? market.CurrentPrice(sku) : Mathf.RoundToInt(sku.BuyValue);
            return Mathf.Max(1, Mathf.RoundToInt(marketPrice * PriceMultiplier));
        }
    }
}
