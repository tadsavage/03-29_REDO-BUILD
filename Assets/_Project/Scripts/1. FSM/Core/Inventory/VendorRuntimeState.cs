namespace GameCore.Inventory
{
    /// <summary>
    /// The one piece of per-playthrough mutable vendor state — Partnership Level — kept off the
    /// shared VendorData asset. Matches how InventoryService keeps pallet state off SkuData: a
    /// ScriptableObject is shared master data on disk, and baking a "randomized" value into it would
    /// mean every playthrough shared the same roll. Owned exclusively by VendorEconomyService.
    /// </summary>
    public class VendorRuntimeState
    {
        public string VendorId;
        public int PartnershipLevel;

        /// <summary>Flavor-only travel time to the warehouse, in hours. No map/distance system exists
        /// yet, so this is a fixed random roll for the session (like PartnershipLevel), not derived
        /// from anything real. Set by VendorEconomyService.Initialize().</summary>
        public float TravelTimeHours;

        public PartnershipTierProfile CurrentProfile => PartnershipTierUtility.GetProfile(PartnershipLevel);
    }
}
