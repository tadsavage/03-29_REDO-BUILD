namespace GameCore.Inventory
{
    /// <summary>
    /// Classifies a rack slot by its role in the pick-path.
    ///
    ///   <b>Pick</b>   — forward slot; picker visits this address to fulfil orders.
    ///                   Level char is numeric ("0", "1", …).
    ///
    ///   <b>Reserve</b>— bulk storage slot; pallets are put away here and replenish
    ///                   pick slots as needed.
    ///                   Level char is alphabetic ("A", "B", …).
    /// </summary>
    public enum LocationType
    {
        /// <summary>Forward (pick-face) slot — visited by pickers to fulfil outbound orders.</summary>
        Pick,

        /// <summary>Bulk reserve slot — receives putaway pallets and replenishes pick faces.</summary>
        Reserve
    }
}
