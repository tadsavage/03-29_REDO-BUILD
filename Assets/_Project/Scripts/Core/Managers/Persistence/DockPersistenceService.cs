using GameCore.Inventory;
using GameCore.Services;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles saving and loading of dock state: trucks, cargo, staging lanes, offload progress.
/// Snapshots trailer state and pallet locations so they can be restored on load.
/// </summary>
public static class DockPersistenceService
{
    /// <summary>
    /// Snapshot the current dock state: docked trucks, their cargo, and staged pallets.
    /// </summary>
    public static DockPersistenceData Snapshot()
    {
        var data = new DockPersistenceData();

        // Find all trucks in the yard
        var trucks = Object.FindObjectsByType<TruckController>();
        foreach (var truck in trucks)
        {
            if (truck == null) continue;

            // Only snapshot trucks that are at a dock or in a relevant state
            // Trucks that have fully departed are not saved
            if (truck.CurrentState == TruckController.TruckState.Exiting ||
                truck.CurrentState == TruckController.TruckState.ToExit)
                continue;

            var trailer = new DockPersistenceData.TrailerSnapshot
            {
                truckId = truck.gameObject.name,
                dockedAtDoorNumber = truck.DockedAt != null ? truck.DockedAt.DoorNumber : -1,
                state = truck.CurrentState.ToString(),
                cargoLoadIds = GetCargoLoadIds(truck),
                dockedTime = Time.timeSinceLevelLoad
            };

            data.trailersInDoors.Add(trailer);
        }

        // Snapshot all pallets currently in staging lanes
        var inventory = ServiceLocator.Get<InventoryService>();
        if (inventory != null)
        {
            var allPallets = inventory.GetAllPallets();
            foreach (var pallet in allPallets)
            {
                if (pallet == null) continue;

                // Only snapshot pallets that are in lanes (not on trucks, not in racks)
                if (IsInStagingLane(pallet.CurrentLocation.ToString()))
                {
                    data.palletsInLanes.Add(new DockPersistenceData.PalletSnapshot
                    {
                        loadId = pallet.LoadId ?? "",
                        skuId = pallet.SkuId,
                        quantity = pallet.Quantity,
                        location = pallet.CurrentLocation.ToString(),
                        isReceived = !pallet.IsContaminated,
                        shelfLifeDays = pallet.ExpirationDayNumber >= 0 ? pallet.ExpirationDayNumber - pallet.ReceivedDayNumber : 0
                    });
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Restore dock state from a saved snapshot.
    /// Recreates trucks and stages pallets in their saved locations.
    /// </summary>
    public static void Restore(DockPersistenceData data)
    {
        if (data == null)
            return;

        var inventory = ServiceLocator.Get<InventoryService>();

        // Handle trailers still being processed
        foreach (var trailer in data.trailersInDoors)
        {
            // Trailers in "Exiting" state should not be restored — they've already left
            if (trailer.state == "Exiting" || trailer.state == "ToExit")
                continue;

            // For partially offloaded trucks: mark cargo pallets as unreceived (ghost state)
            // so they show as pending in staging lanes but don't double-register as received
            if (trailer.cargoLoadIds != null && inventory != null)
            {
                foreach (var loadId in trailer.cargoLoadIds)
                {
                    var pallet = inventory.GetPalletByLoadId(loadId);
                    if (pallet != null)
                    {
                        // Don't fully restore — leave as ghost (contaminated) so offload can continue
                        pallet.IsContaminated = true;
                    }
                }
            }
        }

        // Pallets in staging lanes are already restored via InventoryPersistenceService
        // No additional work needed here beyond the truck-cargo handling above

        Debug.Log($"[DockPersistenceService] Restored {data.trailersInDoors.Count} trailers and {data.palletsInLanes.Count} pallets in lanes.");
    }

    /// <summary>
    /// Get the Load IDs of all cargo currently on a truck.
    /// TODO: Implement once PalletBuilder has load ID tracking.
    /// </summary>
    private static List<string> GetCargoLoadIds(TruckController truck)
    {
        var loadIds = new List<string>();

        if (truck == null) return loadIds;

        // TODO: Check if truck has a LoadContainer with pallets and extract their Load IDs
        // For now, this is a stub that returns empty list
        // Pallets on a truck will be lost on save/load until this is fully implemented

        return loadIds;
    }

    /// <summary>
    /// Check if a location string represents a staging lane location.
    /// Lane locations are formatted like "1A-0" (door + letter + level).
    /// Rack locations are formatted like "01-02-00" (aisle-bay-level-pos).
    /// </summary>
    private static bool IsInStagingLane(string location)
    {
        if (string.IsNullOrEmpty(location))
            return false;

        // Lane format: digit(s) + letter + dash + digit = "1A-0", "2C-1"
        // Rack format: 2digits + dash + 2digits + dash + ...
        // Quick heuristic: if it has a letter before the first dash, it's a lane
        int dashIndex = location.IndexOf('-');
        if (dashIndex < 0) return false;

        string beforeDash = location.Substring(0, dashIndex);
        // If it contains any letters, it's probably a lane
        return beforeDash.Any(char.IsLetter);
    }
}
