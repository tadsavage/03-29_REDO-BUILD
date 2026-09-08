using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>The 11 economic bands a vendor's raw Partnership Level (-100..100) falls into.</summary>
    public enum PartnershipTier
    {
        PlusFive,
        PlusFour,
        PlusThree,
        PlusTwo,
        PlusOne,
        Neutral,
        MinusOne,
        MinusTwo,
        MinusThree,
        MinusFour,
        MinusFive
    }

    /// <summary>The full set of derived economics and presentation for one Partnership tier. Every
    /// field a vendor's UI or economy math needs, computed from ONE stored int (see VendorRuntimeState),
    /// so cost, fill rate, damage rate, rarity gating and colour can never drift apart.</summary>
    public struct PartnershipTierProfile
    {
        public PartnershipTier Tier;
        public string DisplayLabel;
        public string HexColor;
        public float CostModifierPercent;
        public float FillRatePercent;
        public float DamagedGoodsPercent;
        public ItemRarity MaxRarityUnlocked;
    }

    /// <summary>
    /// Static lookup table mapping a vendor's raw Partnership Level integer to its economic tier and
    /// UI presentation. Single source of truth for GetTier/GetProfile so the backend economy math and
    /// the UI colour-coding can never drift out of sync with each other.
    /// </summary>
    public static class PartnershipTierUtility
    {
        /// <summary>Maps a raw Partnership Level to its band. Clamped to [-100, 100] first so an
        /// out-of-range value (a bad save, a debug override) still resolves to a real tier instead
        /// of falling through every branch.</summary>
        public static PartnershipTier GetTier(int partnershipLevel)
        {
            int level = Mathf.Clamp(partnershipLevel, -100, 100);

            if (level >= 81) return PartnershipTier.PlusFive;
            if (level >= 61) return PartnershipTier.PlusFour;
            if (level >= 41) return PartnershipTier.PlusThree;
            if (level >= 21) return PartnershipTier.PlusTwo;
            if (level >= 1) return PartnershipTier.PlusOne;
            if (level == 0) return PartnershipTier.Neutral;
            if (level >= -20) return PartnershipTier.MinusOne;
            if (level >= -40) return PartnershipTier.MinusTwo;
            if (level >= -60) return PartnershipTier.MinusThree;
            if (level >= -80) return PartnershipTier.MinusFour;
            return PartnershipTier.MinusFive;
        }

        /// <summary>Returns the full profile — cost/fill/damage/rarity/colour/label — for a given raw
        /// Partnership Level. This is the single source of truth every caller (backend economy math,
        /// UI colour-coding) reads from.</summary>
        public static PartnershipTierProfile GetProfile(int partnershipLevel)
        {
            var tier = GetTier(partnershipLevel);

            return tier switch
            {
                PartnershipTier.PlusFive => new PartnershipTierProfile
                {
                    Tier = tier, DisplayLabel = "Elite Partner", HexColor = "#3EC15A",
                    CostModifierPercent = -20f, FillRatePercent = 99f, DamagedGoodsPercent = 0.5f,
                    MaxRarityUnlocked = ItemRarity.Exotic
                },
                PartnershipTier.PlusFour => new PartnershipTierProfile
                {
                    Tier = tier, DisplayLabel = "Trusted Partner", HexColor = "#5ECB6C",
                    CostModifierPercent = -15f, FillRatePercent = 96f, DamagedGoodsPercent = 1f,
                    MaxRarityUnlocked = ItemRarity.Exotic
                },
                PartnershipTier.PlusThree => new PartnershipTierProfile
                {
                    Tier = tier, DisplayLabel = "Preferred Buyer", HexColor = "#8CD98A",
                    CostModifierPercent = -10f, FillRatePercent = 93f, DamagedGoodsPercent = 2f,
                    MaxRarityUnlocked = ItemRarity.Rare
                },
                PartnershipTier.PlusTwo => new PartnershipTierProfile
                {
                    Tier = tier, DisplayLabel = "Valued Customer", HexColor = "#B7E39B",
                    CostModifierPercent = -5f, FillRatePercent = 90f, DamagedGoodsPercent = 3f,
                    MaxRarityUnlocked = ItemRarity.Rare
                },
                PartnershipTier.PlusOne => new PartnershipTierProfile
                {
                    Tier = tier, DisplayLabel = "Friendly Terms", HexColor = "#D7EFB0",
                    CostModifierPercent = -2f, FillRatePercent = 87f, DamagedGoodsPercent = 4f,
                    MaxRarityUnlocked = ItemRarity.Uncommon
                },
                PartnershipTier.Neutral => new PartnershipTierProfile
                {
                    Tier = tier, DisplayLabel = "Neutral Relationship", HexColor = "#C9C9C9",
                    CostModifierPercent = 0f, FillRatePercent = 85f, DamagedGoodsPercent = 5f,
                    MaxRarityUnlocked = ItemRarity.Uncommon
                },
                PartnershipTier.MinusOne => new PartnershipTierProfile
                {
                    Tier = tier, DisplayLabel = "Strained Relationship", HexColor = "#F0C77A",
                    CostModifierPercent = 3f, FillRatePercent = 80f, DamagedGoodsPercent = 7f,
                    MaxRarityUnlocked = ItemRarity.Uncommon
                },
                PartnershipTier.MinusTwo => new PartnershipTierProfile
                {
                    Tier = tier, DisplayLabel = "Cold Terms", HexColor = "#F0A85A",
                    CostModifierPercent = 7f, FillRatePercent = 74f, DamagedGoodsPercent = 9f,
                    MaxRarityUnlocked = ItemRarity.Common
                },
                PartnershipTier.MinusThree => new PartnershipTierProfile
                {
                    Tier = tier, DisplayLabel = "Distrustful", HexColor = "#E88A4C",
                    CostModifierPercent = 12f, FillRatePercent = 66f, DamagedGoodsPercent = 12f,
                    MaxRarityUnlocked = ItemRarity.Common
                },
                PartnershipTier.MinusFour => new PartnershipTierProfile
                {
                    Tier = tier, DisplayLabel = "Hostile Terms", HexColor = "#E2634A",
                    CostModifierPercent = 18f, FillRatePercent = 55f, DamagedGoodsPercent = 16f,
                    MaxRarityUnlocked = ItemRarity.Common
                },
                _ => new PartnershipTierProfile
                {
                    Tier = tier, DisplayLabel = "Burned Bridge", HexColor = "#C4433D",
                    CostModifierPercent = 25f, FillRatePercent = 40f, DamagedGoodsPercent = 22f,
                    MaxRarityUnlocked = ItemRarity.Common
                },
            };
        }
    }
}
