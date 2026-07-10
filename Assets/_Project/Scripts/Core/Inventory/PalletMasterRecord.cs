using UnityEngine;
using System;

namespace GameCore.Inventory
{
    /// <summary>
    /// The master record for a pallet in the inventory system. This is the single source of truth
    /// tracked by InventoryService for all pallets that exist in the warehouse — whether ghosted in
    /// receiving or solid in storage. Persists even when the physical GameObject is ghosted or removed.
    ///
    /// The physical pallet GameObject itself will have a separate PalletData component attached once
    /// receiving is complete, but PalletMasterRecord is the inventory system's overhead record.
    /// </summary>
    [System.Serializable]
    public class PalletMasterRecord
    {
        public string PalletId { get; set; }
        /// <summary>10-digit "license plate" assigned at receiving. Null for pallets registered before
/// Load IDs existed (e.g. hand-placed in the Editor) or created outside ReceivePalletWithLoadId.</summary>
        public string LoadId { get; set; }
        public string SkuId { get; set; }
        public int Quantity { get; set; }
        public Vector2Int CurrentLocation { get; set; } // Grid cell where pallet is stored
        public float WorldHeightY { get; set; } // World Y position (height); used to restore pallet at correct elevation on load
        public int ReceivedDayNumber { get; set; } // In-game day number when received
        public int ExpirationDayNumber { get; set; } // -1 if non-perishable
        public bool IsContaminated { get; set; }
        public string StagingLaneId { get; set; } // Lane ID (e.g., "2A", "2B") if pallet is on dock, null if in storage

        public PalletMasterRecord(string skuId, int quantity, Vector2Int location, int receivedDay, int expirationDay)
        {
            PalletId = System.Guid.NewGuid().ToString();
            SkuId = skuId;
            Quantity = quantity;
            CurrentLocation = location;
            ReceivedDayNumber = receivedDay;
            ExpirationDayNumber = expirationDay;
            IsContaminated = false;
        }

        /// <summary>Check if this pallet has expired based on current day number.</summary>
        public bool IsExpired(int currentDayNumber)
        {
            return ExpirationDayNumber >= 0 && currentDayNumber > ExpirationDayNumber;
        }

        /// <summary>Check if this pallet is expiring soon (within 2 days).</summary>
        public bool IsExpiringsooon(int currentDayNumber, int daysUntilWarning = 2)
        {
            return ExpirationDayNumber >= 0
                && currentDayNumber > ExpirationDayNumber - daysUntilWarning
                && currentDayNumber <= ExpirationDayNumber;
        }

        /// <summary>Age in days since received.</summary>
        public int AgeInDays(int currentDayNumber) => Mathf.Max(0, currentDayNumber - ReceivedDayNumber);
    }
}
