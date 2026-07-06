using GameCore.Services;
using GameCore.Events;
using GameCore.Economy;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Manages inbound shipments (Purchase Orders). 
    /// Coordinates with TruckYardManager to bring product into the warehouse.
    /// </summary>
    public class ShipmentService : IService
    {
        private readonly List<ShipmentData> _pendingShipments = new();
        private TruckYardManager _yardManager;
        private SimulationTimeService _timeService;

        public IReadOnlyList<ShipmentData> PendingShipments => _pendingShipments;

        public void Initialize()
        {
            _yardManager = Object.FindAnyObjectByType<TruckYardManager>();
            _timeService = ServiceLocator.Get<SimulationTimeService>();
        }

        public void Shutdown()
        {
            _pendingShipments.Clear();
        }

        /// <summary>Creates a new Purchase Order and schedules a truck arrival.</summary>
        public void CreatePurchaseOrder(string supplierId, string supplierName, List<ShipmentLineItem> items)
        {
            int day = _timeService != null ? _timeService.Day : 1;
            int minute = _timeService != null ? _timeService.Minute : 480; // 8:00 AM default

            var shipment = new ShipmentData(supplierId, supplierName, day, minute);
            shipment.LineItems.AddRange(items);
            
            _pendingShipments.Add(shipment);

            Debug.Log($"[ShipmentService] Created PO {shipment.PONumber} for {supplierName} with {items.Count} items.");

            // For now, spawn immediately if possible
            TrySpawnTruck(shipment);
        }

        private void TrySpawnTruck(ShipmentData shipment)
        {
            if (_yardManager == null) _yardManager = Object.FindAnyObjectByType<TruckYardManager>();

            if (_yardManager != null)
            {
                _yardManager.SpawnNextTruck(shipment);
                Debug.Log($"[ShipmentService] Spawned truck for PO {shipment.PONumber}");
            }
            else
            {
                Debug.LogError("[ShipmentService] Cannot spawn truck: TruckYardManager not found in scene.");
            }
        }
}
}
