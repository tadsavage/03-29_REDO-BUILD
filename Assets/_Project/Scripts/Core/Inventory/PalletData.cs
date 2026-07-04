using UnityEngine;
using System;

namespace GameCore.Inventory
{
    /// <summary>
    /// Represents a single pallet of inventory in the warehouse.
    /// A pallet holds a quantity of a single SKU and tracks its location and age.
    /// </summary>
    [System.Serializable]
    public class PalletData
    {
        public string PalletId { get; private set; }
        /// <summary>10-digit "license plate" assigned at receiving. Null for pallets registered before
        /// Load IDs existed (e.g. hand-placed in the Editor) or created outside ReceivePalletWithLoadId.</summary>
        public string LoadId { get; set; }
        public string SkuId { get; set; }
        public int Quantity { get; set; }
        public Vector2Int CurrentLocation { get; set; } // Grid cell where pallet is stored
        public int ReceivedDayNumber { get; set; } // In-game day number when received
        public int ExpirationDayNumber { get; set; } // -1 if non-perishable
        public bool IsContaminated { get; set; }

        public PalletData(string skuId, int quantity, Vector2Int location, int receivedDay, int expirationDay)
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
        public bool IsExpiringsoon(int currentDayNumber, int daysUntilWarning = 2)
        {
            return ExpirationDayNumber >= 0
                && currentDayNumber > ExpirationDayNumber - daysUntilWarning
                && currentDayNumber <= ExpirationDayNumber;
        }

        /// <summary>Age in days since received.</summary>
        public int AgeInDays(int currentDayNumber) => Mathf.Max(0, currentDayNumber - ReceivedDayNumber);
    }
}
