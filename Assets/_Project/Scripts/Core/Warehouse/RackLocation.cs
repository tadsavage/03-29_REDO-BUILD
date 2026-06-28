using UnityEngine;
using System;

namespace Warehouse
{
    [System.Serializable]
    public class RackLocation
    {
        // Identity
        public string LocationName { get; set; }        // "01-AA-1" format
        public int AisleNumber { get; set; }            // 1–99
        public string PositionCode { get; set; }        // "AA", "AB", "AC", etc.
        public string Level { get; set; }               // "1", "2" (pick) or "A", "B", "C" (reserve)

        // Physical
        public Vector3 WorldPosition { get; set; }
        public Vector3Int GridCell { get; set; }        // (x, y, z) in grid coords
        public float Height { get; set; }               // 48, 80, etc. (inches)

        // Type
        public LocationType Type { get; set; }          // Pick or Reserve

        // State
        public LocationStatus Status { get; set; }      // Available, Assigned, OnHold, etc.

        // Inventory
        public int MaxPallets { get; set; }            // Usually 1–2 per location
        public int CurrentPalletCount { get; set; }     // How many pallets currently here

        // Constructor
        public RackLocation()
        {
            Status = LocationStatus.Available;
            MaxPallets = 1;
            CurrentPalletCount = 0;
            Type = LocationType.Pick;
        }

        // Utility
        public bool CanAcceptPallet() => CurrentPalletCount < MaxPallets && Status == LocationStatus.Available;

        public bool IsReserve() => Type == LocationType.Reserve;

        public bool IsPick() => Type == LocationType.Pick;
    }

    [System.Serializable]
    public enum LocationType
    {
        Pick,
        Reserve,
        Bulk,      // For future use
        Freezer    // For future use
    }

    [System.Serializable]
    public enum LocationStatus
    {
        Available,
        Assigned,
        OnHold,
        Damaged,
        Blocked
    }
}
