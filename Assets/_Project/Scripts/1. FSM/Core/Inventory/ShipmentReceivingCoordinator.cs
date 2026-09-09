using UnityEngine;
using GameCore.Services;
using GameCore.Events;
using System.Collections.Generic;

namespace GameCore.Inventory
{
    /// <summary>
    /// Coordinates the receiving of shipments across the warehouse. When a pallet is received (PalletData
    /// script applied), this service:
    ///
    /// 1. Updates the shipment's received quantity for that pallet's SKU
    /// 2. Tracks overage/shortage
    /// 3. Detects when all pallets from a shipment have been received
    /// 4. Fires "ShipmentFullyReceived" event to invoicing system
    /// 5. Signals dock light to change from red (unloading) to green (ready to depart)
    ///
    /// Registered with ServiceLocator by GameContext and initialized early in startup.
    /// </summary>
    public class ShipmentReceivingCoordinator : IService
    {
        private EventManager _eventManager;
        private InventoryService _inventoryService;

        // Track which shipment each pallet belongs to
        private Dictionary<string, ShipmentData> _palletToShipmentMap = new();

        // All active shipments
        public List<ShipmentData> ActiveShipments { get; private set; } = new();

        public void Initialize()
        {
            _eventManager = EventManager.Instance;
            if (_eventManager == null)
            {
                Debug.LogError("[ShipmentReceivingCoordinator] EventManager not found");
                return;
            }

            ServiceLocator.TryGet<InventoryService>(out _inventoryService);
            if (_inventoryService == null)
            {
                Debug.LogError("[ShipmentReceivingCoordinator] InventoryService not found");
                return;
            }

            // Subscribe to pallet received events
            _eventManager.Subscribe<PalletMasterRecord>(GameEvents.Inventory.OnPalletReceived, OnPalletReceived);

            Debug.Log("[ShipmentReceivingCoordinator] Initialized");
        }

        public void Shutdown()
        {
            if (_eventManager != null)
                _eventManager.Unsubscribe<PalletMasterRecord>(GameEvents.Inventory.OnPalletReceived, OnPalletReceived);

            _palletToShipmentMap.Clear();
            ActiveShipments.Clear();
        }

        /// <summary>Register a shipment so its pallets can be tracked during receiving.</summary>
        public void RegisterShipment(ShipmentData shipment)
        {
            if (shipment == null)
                return;

            if (!ActiveShipments.Contains(shipment))
                ActiveShipments.Add(shipment);

            Debug.Log($"[ShipmentReceivingCoordinator] Registered PO {shipment.PONumber} from {shipment.SupplierName}");
        }

        /// <summary>
        /// Link a pallet to its source shipment. Called when a pallet is created from a shipment.
        /// </summary>
        public void LinkPalletToShipment(string palletId, ShipmentData shipment)
        {
            _palletToShipmentMap[palletId] = shipment;
        }

        /// <summary>Called when a pallet is received (PalletData script applied). Updates shipment tracking.</summary>
        private void OnPalletReceived(string eventId, PalletMasterRecord pallet)
        {
            if (pallet == null)
                return;

            // Find which shipment this pallet came from
            if (!_palletToShipmentMap.TryGetValue(pallet.PalletId, out var shipment))
            {
                // Pallet not linked to a shipment (e.g., hand-placed in editor)
                return;
            }

            // Update the shipment's received quantity
            shipment.UpdateReceivedQuantity(pallet.SkuId, pallet.Quantity);

            Debug.Log($"[ShipmentReceivingCoordinator] Updated PO {shipment.PONumber}: " +
                      $"received {pallet.Quantity} × {pallet.SkuId}");

            // Check if shipment is now fully received
            if (shipment.IsFullyReceived)
            {
                OnShipmentFullyReceived(shipment);
            }
        }

        /// <summary>Public entry point for anything OUTSIDE the per-pallet receiving path that can also
        /// make a shipment fully received — e.g. ShipmentService.RequestCredit accepting a shortage
        /// credit. Runs the exact same completion routine a physically-received pallet would trigger,
        /// so there's one place that flips Status/fires events, not two that could drift apart. No-op
        /// if the shipment isn't actually fully received yet.</summary>
        public void CompleteIfFullyReceived(ShipmentData shipment)
        {
            if (shipment == null) return;
            if (shipment.Status == ShipmentData.ShipmentStatus.Received) return;
            if (!shipment.IsFullyReceived) return;
            OnShipmentFullyReceived(shipment);
        }

        /// <summary>Called when all pallets from a shipment have been received.</summary>
        private void OnShipmentFullyReceived(ShipmentData shipment)
        {
            shipment.Status = ShipmentData.ShipmentStatus.Received;

            Debug.Log($"[ShipmentReceivingCoordinator] PO {shipment.PONumber} fully received! " +
                      $"Total: {shipment.TotalReceivedUnits} units, " +
                      $"Overage: {shipment.TotalOverage}, Shortage: {shipment.TotalShortage}");

            // Fire event to invoicing system
            if (_eventManager != null)
            {
                _eventManager.Publish(GameEvents.Inventory.OnShipmentFullyReceived, shipment);
            }

            // Fire event to dock light system (change to green)
            if (_eventManager != null)
            {
                _eventManager.Publish(GameEvents.Dock.OnShipmentReadyToDeparture, shipment);
            }

            // Clean up
            ActiveShipments.Remove(shipment);
            RemoveShipmentPalletLinks(shipment);
        }

        /// <summary>Remove all pallet-to-shipment links for a shipment (cleanup).</summary>
        private void RemoveShipmentPalletLinks(ShipmentData shipment)
        {
            var keysToRemove = new List<string>();
            foreach (var kv in _palletToShipmentMap)
            {
                if (kv.Value == shipment)
                    keysToRemove.Add(kv.Key);
            }

            foreach (var key in keysToRemove)
                _palletToShipmentMap.Remove(key);
        }
    }
}
