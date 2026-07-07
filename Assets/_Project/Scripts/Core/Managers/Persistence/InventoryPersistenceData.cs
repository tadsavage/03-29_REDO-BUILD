using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snapshot of all pallets in the inventory system.
/// Includes location, SKU, quantity, expiration, and received status.
/// </summary>
[System.Serializable]
public class InventoryPersistenceData
{
    public List<PalletSnapshot> pallets = new();

    [System.Serializable]
    public class PalletSnapshot
    {
        public string loadId;
        public string skuId = "";  // SKU ID (e.g., "035-12345")
        public int quantity = 0;
        public string location = "";  // rack address like "01-02-00" or lane like "1A-0"
        public float worldHeightY = 0f;  // World Y position (height) for restore positioning
        public bool isReceived = false;  // true = in inventory, false = ghost (not yet processed)
        public int shelfLifeDays = 0;
        public int dateReceived = 0;  // in-game day number
        public int expirationDate = 0;  // in-game day number
    }
}
