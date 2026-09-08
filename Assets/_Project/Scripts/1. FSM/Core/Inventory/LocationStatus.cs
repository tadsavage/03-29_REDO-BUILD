namespace GameCore.Inventory
{
    /// <summary>
    /// Availability lifecycle state for a reserve or pick slot in the racking.
    ///
    /// Transitions for putaway:
    ///   Available  → Reserved   : when AssignPutawayDestination locks the TO location at RTO pickup
    ///   Reserved   → Occupied   : when the putaway physical sequence completes and the pallet is released
    ///   Reserved   → Available  : if the task is cancelled before completion
    ///   QAHold / Problem        : set by inventory control; PutawayLogic never targets these
    /// </summary>
    public enum LocationStatus
    {
        /// <summary>Empty and eligible for putaway search.</summary>
        Available,

        /// <summary>Locked by an active putaway task; no other RTO may target this slot.</summary>
        Reserved,

        /// <summary>Inventory control hold (e.g. product recall) — untouchable by putaway.</summary>
        QAHold,

        /// <summary>Structural damage identified — untouchable by putaway.</summary>
        Problem,

        /// <summary>Slot is occupied by a pallet that has been put away and released.</summary>
        Occupied
    }
}
