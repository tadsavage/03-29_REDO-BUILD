namespace GameCore.Inventory
{
    /// <summary>
    /// The 4 item rarity tiers used for vendor catalogue gating. Ordinal order matters: a vendor's
    /// Partnership Level unlocks "at or below" a given tier (see VendorEconomyService.GetAvailableCatalogue),
    /// so Common must sit first and Exotic last.
    ///
    /// NOTE: append new values at the END — VendorCatalogueEntry serializes this by ordinal.
    /// </summary>
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Exotic
    }
}
