using TMPro;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;

/// <summary>
/// Applies an aisle rename/reconfigure (new aisle number and/or new per-level Pick/Reserve scheme)
/// submitted from RackSetupUI's EDIT mode to every LIVE rack already tagged with the old aisle
/// number — including racks currently holding pallets. Re-derives each slot's address exactly the
/// way AisleInitializer's initial commit does (same bay number + LocationNameGenerator.LevelChar
/// formula, just with the new aisle number / designations), then migrates every piece of
/// address-keyed state that would otherwise go stale:
///   - LocationStatusRegistry (the actual source of truth PutawayLogic queries)
///   - SlotAssignmentService (SKU -> pick-slot address)
///   - the physical pallets' own PalletData.LocationName
///   - any Work Queue task still referencing the old address (Putaway/Replenish/PalletPick)
///
/// A slot that flips from Pick to Reserve simply drops its SlotAssignmentService entry — the SKU
/// that lived there surfaces through the existing "no pick slot assigned" warnings
/// (SchedulerPanel/ContractsPanel/WorldHoverPopupUI) until a manager reassigns it. Bay numbers,
/// travel direction, and aisle-facing are never touched — those are geometry-derived and settled
/// at first commit; only the human-readable address (and, incidentally, Pick/Reserve identity)
/// ever changes on a rename.
/// </summary>
public static class AisleRenameService
{
    /// <summary>Renames every live rack slot tagged with <paramref name="oldAisle"/> to
    /// <paramref name="newAisle"/>, re-deriving each level's Pick/Reserve char from
    /// <paramref name="newDesignations"/>. Returns the number of slot addresses actually changed.</summary>
    public static int Rename(int oldAisle, int newAisle, string[] newDesignations)
    {
        int changedCount = 0;

        foreach (var po in PlacedObjectRegistry.GetSnapshot())
        {
            if (po == null || !po.isRackLive) continue;
            if (po.data == null || po.data.category != "Racking") continue;
            if (po.rackAisle != oldAisle) continue;

            string newLevelChar = LocationNameGenerator.LevelChar(po.rackLevelIndex, newDesignations);

            // Touch every label (front AND rear) like AisleInitializer.SetRackLabels does — only the
            // currently-ACTIVE face is tracked by any registry, but keeping both faces' text in sync
            // means a later face-flip (ConfigureFaces) never uncovers a stale address.
            foreach (var tmp in po.GetComponentsInChildren<TextMeshPro>(true))
            {
                string oldAddress = tmp.text;
                if (string.IsNullOrEmpty(oldAddress)) continue;

                string positionDigit = oldAddress.Substring(oldAddress.Length - 1);
                string newAddress = $"{newAisle:D2}-{po.rackBay:D2}-{newLevelChar}{positionDigit}";

                tmp.text = newAddress;
                if (newAddress == oldAddress) continue;
                if (!tmp.gameObject.activeInHierarchy) continue; // hidden face carries no real state

                var locData = tmp.transform.parent != null
                    ? tmp.transform.parent.GetComponentInChildren<LocationData>(true)
                    : null;

                // Mirrors SlotRegistry's own live rule (TryParseAddress: IsPick = levelChar == "0")
                // so the identity we assign here matches exactly what the next Recompute would
                // derive from this same label text.
                bool isPickNow = newLevelChar == "0";
                MigrateSlotState(oldAddress, newAddress, locData, isPickNow);

                changedCount++;
            }

            po.rackAisle = newAisle;
            po.rackLevelChar = newLevelChar;
        }

        // Force both registries to rebuild NOW instead of waiting on their 1s heartbeat, so
        // PutawayLogic/OrderPickPath/SlotAssignmentPanel see the new addresses immediately.
        LocationRegistry.ForceRecompute();
        SlotRegistry.ForceRecompute();

        return changedCount;
    }

    private static void MigrateSlotState(string oldAddress, string newAddress, LocationData locData, bool isPickNow)
    {
        bool wasPick = locData != null && locData.Type == LocationType.Pick;
        string palletId = locData != null ? locData.PalletId : null;

        // LocationData identity — rename its GameObject/Type immediately rather than waiting on the
        // next heartbeat. Every other field (contents, status) is untouched by Initialize().
        locData?.Initialize(newAddress, isPickNow ? LocationType.Pick : LocationType.Reserve);

        // LocationStatusRegistry — the actual source of truth PutawayLogic queries.
        LocationStatus status = LocationStatusRegistry.Get(oldAddress);
        LocationStatusRegistry.Release(oldAddress);
        if (status != LocationStatus.Available)
            LocationStatusRegistry.Set(newAddress, status);

        // SlotAssignmentService — only a Pick slot ever carries a SKU assignment. A slot that stops
        // being Pick simply drops it (the SKU just needs a new pick slot, flagged by the existing
        // "no pick slot assigned" warning elsewhere). A slot that stays Pick keeps its assignment,
        // just under the new address.
        if (wasPick)
        {
            string sku = SlotAssignmentService.GetSku(oldAddress);
            SlotAssignmentService.Clear(oldAddress);
            if (!string.IsNullOrEmpty(sku) && isPickNow)
                SlotAssignmentService.Assign(newAddress, sku);
        }

        // The physical pallet's own human-readable location string, so it never drifts out of sync
        // with the slot that's actually holding it.
        if (!string.IsNullOrEmpty(palletId))
        {
            var link = PalletMasterLink.Find(palletId);
            var pd = link != null ? link.GetComponent<PalletData>() : null;
            if (pd != null) pd.SetLocation(pd.CurrentLocation, newAddress);
        }

        // Any Work Queue task still pointing at the old address (a Putaway destination, a Replenish
        // source/destination, a PalletPick source) — rewritten in place so a job already in flight
        // doesn't silently target an address that no longer exists.
        if (ServiceLocator.TryGet<WorkQueueSystem>(out var wq) && wq != null)
        {
            foreach (var task in wq.Tasks)
                task.RenameLocationReferences(oldAddress, newAddress);
        }
    }
}
