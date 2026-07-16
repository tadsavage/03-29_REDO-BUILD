using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.Persistence
{
    /// <summary>
    /// Handles save/load persistence for all trucks currently in the yard.
    ///
    /// Capture rules:
    /// • Trucks departing (DepartToApproach, ToLeaveNoTurn, ToExit, Exiting) are NOT saved —
    ///   they're treated as already gone, and their PO is stamped Received/Departed so it
    ///   doesn't respawn a new truck on the next play session.
    /// • Pallets physically on a dock stocker's forks at save time are folded back into the
    ///   carrying truck's <see cref="TruckSnapshot.trailerPallets"/> list so they reappear on
    ///   the trailer on load (the user said "return to trailer at original position").
    /// • Pallets already dropped into staging lanes are handled separately by
    ///   <see cref="PalletPersistenceService"/> — they remain in the lane, still ghosted.
    ///
    /// Restore rules:
    /// • Each saved truck is instantiated from the yard manager's TruckPrefab at its exact
    ///   saved world transform.
    /// • The truck's assigned shipment is looked up in ShipmentService by PO number and
    ///   re-linked without re-spawning any cargo (cargo comes from the trailer snapshot).
    /// • The dock slot is found by door number from DockSlot.All and claimed.
    /// • <see cref="TruckController.RestoreFromSnapshot"/> handles state, doors, ghost, and
    ///   cargo reconstruction.
    /// • Gate-queued trucks (Queuing state) are added back to the yard manager's gate queue
    ///   in their original order, then <see cref="TruckYardManager.FinalizeGateQueue"/> lays
    ///   them out.
    /// </summary>
    public static class TruckPersistenceService
    {
        // ── Capture ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans all TruckControllers in the scene, skips departing trucks (marking their PO
        /// received), folds fork-carried pallets back into the owning truck's snapshot, and
        /// returns the complete list of truck snapshots to save.
        /// </summary>
        public static List<TruckSnapshot> CaptureAll()
        {
            var snapshots = new List<TruckSnapshot>();

            // Build a map of: pallet transform currently on a dock-stocker's forks → the truck
            // whose offload was interrupted. These pallets are folded back into that truck's
            // trailerPallets list so they restore on the trailer rather than being lost.
            var forkPallets = BuildForkPalletMap();

            // Collect all trucks in yard, ordered so gate-queue slots are deterministic.
            var allTrucks = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None);

            foreach (var truck in allTrucks)
            {
                if (truck == null) continue;

                // ── Departing trucks: skip & stamp PO ────────────────────────────
                if (truck.IsDeparting)
                {
                    var dep = truck.AssignedShipment;
                    if (dep != null &&
                        dep.Status != ShipmentData.ShipmentStatus.Received &&
                        dep.Status != ShipmentData.ShipmentStatus.Departed)
                    {
                        dep.Status = ShipmentData.ShipmentStatus.Departed;
                        Debug.Log($"[TruckPersistenceService] Departing truck — PO {dep.PONumber} stamped Departed, not saved.");
                    }
                    continue;
                }

                var snap = new TruckSnapshot
                {
                    poNumber           = truck.AssignedShipment?.PONumber ?? "",
                    assignedDoorNumber = truck.AssignedDock?.DoorNumber ?? -1,
                    truckState         = (int)truck.CurrentState,
                    worldPosition      = truck.transform.position,
                    worldRotation      = truck.transform.rotation,
                    dockedTime         = truck.DockedTime,
                    offloadClaimed     = truck.OffloadClaimed,
                    offloadComplete    = truck.OffloadComplete,
                    doorsOpen          = truck.DoorsOpen,
                    gateQueueIndex     = 0,  // assigned below for queuing trucks
                };

                // Capture pallets still on the trailer
                snap.trailerPallets = truck.CaptureTrailerPallets();

                // Fold in any pallets currently on a dock-stocker's forks that belong to
                // this truck's interrupted offload sequence.
                foreach (var kvp in forkPallets)
                {
                    if (kvp.Value == truck)
                    {
                        var forkSnap = CaptureSinglePallet(kvp.Key);
                        if (forkSnap != null)
                        {
                            snap.trailerPallets.Add(forkSnap);
                            Debug.Log($"[TruckPersistenceService] Returned fork-carried pallet '{kvp.Key.name}' to truck '{truck.name}' snapshot.");
                        }
                    }
                }

                snapshots.Add(snap);
                Debug.Log($"[TruckPersistenceService] Captured '{truck.name}' | state={(TruckController.TruckState)snap.truckState} | door={snap.assignedDoorNumber} | PO={snap.poNumber} | trailer pallets={snap.trailerPallets.Count}");
            }

            // Assign gate-queue order indices for Queuing trucks so they restore in the
            // correct order (front → back). Sort by proximity to the gate stop.
            AssignGateQueueIndices(snapshots);

            return snapshots;
        }

        // ── Restore ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Instantiates every saved truck at its exact saved transform, re-links its shipment,
        /// claims its dock, and restores its full state. Must be called AFTER placed objects
        /// (including ShippingDoors/DockSlots) have been re-spawned so DockSlot.All is populated.
        /// </summary>
        public static void RestoreAll(List<TruckSnapshot> snapshots, TruckYardManager yardManager)
        {
            if (snapshots == null || snapshots.Count == 0) return;
            if (yardManager == null)
            {
                Debug.LogError("[TruckPersistenceService] TruckYardManager is null — cannot restore trucks.");
                return;
            }

            ServiceLocator.TryGet<ShipmentService>(out var shipmentSvc);

            // Restore trucks, accumulating gate-queue trucks sorted by saved queue index.
            // CRITICAL: Sort by gateQueueIndex so they are registered in the correct order!
            var sortedSnaps = snapshots.OrderBy(s => s.gateQueueIndex).ToList();
            var queuedTrucks = new List<(TruckController ctrl, int queueIndex)>();

            foreach (var snap in sortedSnaps)
            {
                if (snap == null) continue;
                var ctrl = RestoreOneTruck(snap, yardManager, shipmentSvc);
                if (ctrl == null) continue;

                var savedState = (TruckController.TruckState)snap.truckState;
                bool isQueuing = savedState == TruckController.TruckState.Queuing ||
                                 savedState == TruckController.TruckState.GuardCheck;

                yardManager.RegisterRestoredTruck(ctrl, addToGateQueue: isQueuing);

                if (isQueuing)
                    queuedTrucks.Add((ctrl, snap.gateQueueIndex));
            }

            // Lay out the gate queue once all queued trucks are registered.
            if (queuedTrucks.Count > 0)
                yardManager.FinalizeGateQueue();

            Debug.Log($"[TruckPersistenceService] Restored {snapshots.Count} truck(s) to yard.");
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Spawns one truck prefab at the saved transform, wires it, re-links the shipment,
        /// and calls RestoreFromSnapshot. Returns the controller or null on failure.
        /// </summary>
        private static TruckController RestoreOneTruck(TruckSnapshot snap, TruckYardManager yardManager, ShipmentService shipmentSvc)
        {
            var prefab = yardManager.TruckPrefab;
            if (prefab == null)
            {
                Debug.LogError("[TruckPersistenceService] TruckYardManager.TruckPrefab is null — cannot restore truck.");
                return null;
            }

            // Find the DockSlot matching the saved door number.
            DockSlot dock = null;
            if (snap.assignedDoorNumber > 0)
                dock = DockSlot.All.FirstOrDefault(d => d.DoorNumber == snap.assignedDoorNumber);

            if (dock == null && snap.assignedDoorNumber > 0)
                Debug.LogWarning($"[TruckPersistenceService] Could not find DockSlot for door {snap.assignedDoorNumber} — truck may not dock correctly.");

            // Orphan debris: no PO to reference and no dock to claim. These accumulated from an
            // earlier restore bug (a door-numbering race — now fixed — could orphan a legitimately
            // docked truck from its dock on load; once orphaned it can never depart cleanly, so it
            // and its dead PO reference just got re-saved and re-restored every cycle). A truck with
            // neither has nothing left to resume — drop it instead of resurrecting a dead truck.
            if (string.IsNullOrEmpty(snap.poNumber) && dock == null)
            {
                Debug.LogWarning($"[TruckPersistenceService] Dropping orphaned truck snapshot (no PO, no dock, state={(TruckController.TruckState)snap.truckState}) — nothing to resume.");
                return null;
            }

            // FIX: Clamp the truck's Y position to 0 if it's suspiciously high (e.g., at spawn height 1.15).
            // Trucks should stay at ground level except during their scripted route. If Y is far from 0,
            // it's likely a stale spawn position that wasn't properly updated during dock.
            Vector3 restorePos = snap.worldPosition;
            if (restorePos.y > 0.5f)  // anything higher than ~0.5m is abnormal
            {
                Debug.LogWarning($"[TruckPersistenceService] Truck Y position is {restorePos.y}m (expected ~0). Resetting to ground level.");
                restorePos.y = 0f;
            }

            // Instantiate at the exact saved transform (or corrected position).
            var go = Object.Instantiate(prefab, restorePos, snap.worldRotation);
            go.name = string.IsNullOrEmpty(snap.poNumber)
                ? $"Truck→Door{snap.assignedDoorNumber}"
                : $"Truck→PO_{snap.poNumber}";

            var ctrl = go.GetComponent<TruckController>() ?? go.AddComponent<TruckController>();

            // Re-link the assigned shipment BEFORE RestoreFromSnapshot so any logic that
            // reads AssignedShipment (e.g. BeginDeparture marking it Departed) sees the real PO.
            if (!string.IsNullOrEmpty(snap.poNumber) && shipmentSvc != null)
            {
                var shipment = shipmentSvc.PendingShipments.FirstOrDefault(s => s.PONumber == snap.poNumber);
                if (shipment != null)
                    ctrl.SetShipment(shipment);
                else
                    Debug.LogWarning($"[TruckPersistenceService] Shipment PO '{snap.poNumber}' not found in ShipmentService — truck has no PO reference.");
            }

            // Restore full state (transform is overwritten by snapshot values inside this call,
            // so the Instantiate position is only a fallback).
            ctrl.RestoreFromSnapshot(snap, dock);

            Debug.Log($"[TruckPersistenceService] Restored '{go.name}' | state={(TruckController.TruckState)snap.truckState} | door={snap.assignedDoorNumber}");
            return ctrl;
        }

        /// <summary>
        /// Builds a map: pallet transform currently on a dock stocker's forks → the truck being
        /// offloaded. Identifies fork-carried pallets by scanning every MHEOperatorSlot's Forks
        /// child for children that have an unreceived PalletData (LoadId == "") — those are
        /// cargo pallets mid-transit, not warehouse pallets.
        /// </summary>
        private static Dictionary<Transform, TruckController> BuildForkPalletMap()
        {
            var map = new Dictionary<Transform, TruckController>();

            // Find the truck currently being actively offloaded (claimed but not complete).
            TruckController offloadTruck = null;
            foreach (var t in Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None))
            {
                if (t != null && t.OffloadClaimed && !t.OffloadComplete &&
                    t.CurrentState == TruckController.TruckState.Docked)
                {
                    offloadTruck = t;
                    break;
                }
            }
            if (offloadTruck == null) return map;

            // Scan every dock stocker's Forks child for carried pallets.
            foreach (var slot in Object.FindObjectsByType<MHEOperatorSlot>(FindObjectsSortMode.None))
            {
                if (slot == null) continue;
                Transform forks = FindDeepChild(slot.transform, "Forks");
                if (forks == null) continue;

                foreach (Transform child in forks)
                {
                    if (child == null) continue;
                    var pd = child.GetComponent<GameCore.Inventory.PalletData>();
                    // Unreceived pallet (empty LoadId) = came directly from a trailer. Excludes
                    // anything that already has a PalletMasterLink — that means
                    // TrailerOffloadController.RegisterAndQueue already gave it a real
                    // InventoryService record, so MHEOperatorPersistenceService owns capturing it
                    // (and resuming/staging it) instead of folding it back onto the trailer.
                    if (pd != null && string.IsNullOrEmpty(pd.LoadId)
                        && child.GetComponent<GameCore.Inventory.PalletMasterLink>() == null)
                    {
                        map[child] = offloadTruck;
                        Debug.Log($"[TruckPersistenceService] Fork-carried pallet '{child.name}' found on DS '{slot.name}' — will be returned to trailer.");
                    }
                }
            }

            return map;
        }

        /// <summary>Captures a single pallet transform (from a dock stocker's forks) into a
        /// <see cref="TrailerPalletSnapshot"/>.</summary>
        private static TrailerPalletSnapshot CaptureSinglePallet(Transform pallet)
        {
            if (pallet == null) return null;
            var snap = new TrailerPalletSnapshot();

            ParseSlotAndTierFromName(pallet.name, out snap.floorSlot, out snap.palletTier);

            var pd      = pallet.GetComponent<GameCore.Inventory.PalletData>();
            var builder = pallet.GetComponentInChildren<PalletBuilder>();

            snap.skuId = (pd != null && !string.IsNullOrEmpty(pd.ItemNumber)) ? pd.ItemNumber
                       : (builder?.linkedSku != null ? builder.linkedSku.SkuId : "");

            // Capture fallback SkuData fields for robust recovery
            if (builder != null && builder.linkedSku != null)
            {
                var sku = builder.linkedSku;
                snap.itemDescription = sku.ItemDescription;
                snap.caseLength = sku.CaseLength;
                snap.caseWidth = sku.CaseWidth;
                snap.caseHeight = sku.CaseHeight;
                snap.caseWeight = sku.CaseWeight;
                snap.buyValue = sku.BuyValue;
                snap.sellValue = sku.SellValue;
                snap.storageArea = (int)sku.StorageArea;
                snap.shelfLifeDays = sku.ShelfLifeDays;
            }

            if (builder != null)
            {
                snap.capturedTi = builder.manualTi;
                snap.capturedHi = builder.manualHi;
            }

            var palletLoad = pallet.Find("PalletLoad");
            if (palletLoad != null)
            {
                for (int j = 0; j < palletLoad.childCount; j++)
                {
                    snap.caseLocalPositions.Add(palletLoad.GetChild(j).localPosition);
                    snap.caseLocalRotations.Add(palletLoad.GetChild(j).localRotation);
                }
            }
            return snap;
        }

        /// <summary>Assigns <see cref="TruckSnapshot.gateQueueIndex"/> for all Queuing/GuardCheck
        /// trucks based on their saved position relative to the gate stop. The truck nearest the
        /// gate gets index 0 (front).</summary>
        private static void AssignGateQueueIndices(List<TruckSnapshot> snapshots)
        {
            var queuing = snapshots
                .Where(s => s.truckState == (int)TruckController.TruckState.Queuing ||
                             s.truckState == (int)TruckController.TruckState.GuardCheck)
                .OrderByDescending(s => s.worldPosition.magnitude) // nearest gate stop = largest distance from yard origin? Use door distance
                .ToList();

            // Better heuristic: the TruckYardManager gate stop is the reference.
            // If available, sort by distance to gate stop (ascending = front first).
            var yardManager = Object.FindAnyObjectByType<TruckYardManager>();
            if (yardManager != null && yardManager.GateStopPosition.HasValue)
            {
                Vector3 gate = yardManager.GateStopPosition.Value;
                queuing = queuing.OrderBy(s => Vector3.Distance(s.worldPosition, gate)).ToList();
            }

            for (int i = 0; i < queuing.Count; i++)
                queuing[i].gateQueueIndex = i;
        }

        private static void ParseSlotAndTierFromName(string name, out int slot, out int tier)
        {
            slot = 0; tier = 0;
            if (string.IsNullOrEmpty(name)) return;
            var parts = name.Split('_');
            foreach (var p in parts)
            {
                if (p.StartsWith("Slot", System.StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(p.Substring(4), out int s)) slot = s;
                if (p.StartsWith("Tier", System.StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(p.Substring(4), out int t)) tier = t;
            }
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform c in parent)
            {
                if (c.name == childName) return c;
                var r = FindDeepChild(c, childName);
                if (r != null) return r;
            }
            return null;
        }
    }
}
