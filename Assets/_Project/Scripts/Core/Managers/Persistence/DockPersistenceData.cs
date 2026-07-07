using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snapshot of dock state: trucks at doors, pallets in staging lanes, offload progress.
/// Saved with each game state and restored on load.
/// </summary>
[System.Serializable]
public class DockPersistenceData
{
    public List<TrailerSnapshot> trailersInDoors = new();
    public List<PalletSnapshot> palletsInLanes = new();

    [System.Serializable]
    public class TrailerSnapshot
    {
        public string truckId;  // truck name or unique ID
        public int dockedAtDoorNumber = -1;
        public string state;  // "Docked", "AwaitingOffload", "Exiting", etc.
        public List<string> cargoLoadIds = new();  // pallet Load IDs still on truck
        public float dockedTime = 0f;
    }

    [System.Serializable]
    public class PalletSnapshot
    {
        public string loadId;
        public string skuId = "";
        public int quantity = 0;
        public string location = "";  // e.g., "1A-0"
        public bool isReceived = false;
        public int shelfLifeDays = 0;
    }
}
