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

        private EventManager _eventManager;

        public void Initialize()
        {
            _yardManager = Object.FindAnyObjectByType<TruckYardManager>();
            _timeService = ServiceLocator.Get<SimulationTimeService>();

            // Backstop for the per-departure purge: a truck destroyed mid-route (deleted, scene
            // wiped, domain reload) never reaches BeginDeparture, so its PO would otherwise sit in
            // the list forever.
            _eventManager = EventManager.Instance;
            _eventManager?.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
        }

        public void Shutdown()
        {
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            _pendingShipments.Clear();
        }

        private void OnDayChanged(string eventId, int newDay) => PurgeCompleted();

        /// <summary>Wipes the PO list without tearing down the service — used by the Dev Console's
        /// "Clear Scene" button so leftover test POs don't keep spawning trucks against a scene
        /// that's just been wiped.</summary>
        public void ClearAll()
        {
            _pendingShipments.Clear();
        }

        /// <summary>
        /// Drops finished POs (Departed / Received / Cancelled) out of the pending list.
        ///
        /// The list is called PENDING and a departed truck's PO is by definition not pending — but
        /// nothing ever removed them, so the Dev Console's inbound section accumulated a red
        /// "[Departed]" row per truck for the life of the save, and every one was re-serialised on
        /// every save. Same class of leak as the terminal orders OrderService.Archive now retires.
        ///
        /// Safe: a truck holds a direct reference to its own ShipmentData, so removing it from this
        /// list can't strand a truck mid-route. Called when a truck departs, when a new PO is created,
        /// at each day roll, and once on load to clean out existing saves.
        /// </summary>
        /// <returns>How many were removed.</returns>
        public int PurgeCompleted()
        {
            int removed = _pendingShipments.RemoveAll(s =>
                s == null ||
                s.Status == ShipmentData.ShipmentStatus.Departed ||
                s.Status == ShipmentData.ShipmentStatus.Received ||
                s.Status == ShipmentData.ShipmentStatus.Cancelled);

            if (removed > 0)
                Debug.Log($"[ShipmentService] Retired {removed} finished PO(s); {_pendingShipments.Count} still pending.");
            return removed;
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
            if (shipment == null || shipment.Status == ShipmentData.ShipmentStatus.Received || shipment.Status == ShipmentData.ShipmentStatus.Departed) return;

            if (_yardManager == null) _yardManager = Object.FindAnyObjectByType<TruckYardManager>();

            if (_yardManager != null)
            {
                // Check if a truck for this PO already exists in the scene to prevent duplicates
                var existing = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None)
                    .Any(t => t != null && t.AssignedShipment != null && t.AssignedShipment.PONumber == shipment.PONumber);

                if (existing)
                {
                    Debug.Log($"[ShipmentService] Truck for PO {shipment.PONumber} already in yard — skipping duplicate spawn.");
                    return;
                }

                _yardManager.SpawnNextTruck(shipment);
                Debug.Log($"[ShipmentService] Spawned truck for PO {shipment.PONumber}");

                // Inbound and outbound share the same physical doors, so an arriving PO has to show
                // up on the dock schedule and consume a slot — otherwise the player books every door
                // for outbound at 08:00 and this truck arrives with nowhere to go.
                if (ServiceLocator.TryGet(out DockScheduleService dockSchedule))
                    dockSchedule.BookInboundNow(shipment.SupplierId, shipment.SupplierName);
            }
            else
            {
                Debug.LogError("[ShipmentService] Cannot spawn truck: TruckYardManager not found in scene.");
            }
        }

        /// <summary>Flatten all pending shipments for saving.</summary>
        public List<ShipmentSnapshot> Export()
        {
            var list = new List<ShipmentSnapshot>();
            foreach (var s in _pendingShipments)
            {
                var snap = new ShipmentSnapshot
                {
                    poNumber = s.PONumber,
                    supplierId = s.SupplierId,
                    supplierName = s.SupplierName,
                    arrivalDayNumber = s.ArrivalDayNumber,
                    arrivalTimeMinute = s.ArrivalTimeMinute,
                    status = (int)s.Status
                };
                foreach (var li in s.LineItems)
                {
                    snap.lineItems.Add(new ShipmentLineItemSnapshot
                    {
                        skuId = li.SkuId,
                        quantity = li.Quantity,
                        receivedQuantity = li.ReceivedQuantity,
                        unitCost = li.UnitCost,
                        shelfLifeDays = li.ShelfLifeDays,
                        floorSlotIndex = li.FloorSlotIndex,
                        palletTier = li.PalletTier
                    });
                }
                list.Add(snap);
            }
            return list;
        }

        /// <summary>Restores pending shipments from a save file. Deliberately does NOT call
        /// TrySpawnTruck — truck/dock state isn't persisted yet (see the Trucks phase in the
        /// save-load-persistence-gap-architecture memory), so a restored shipment just becomes
        /// data again; no new truck is dispatched for it. Wiring an actual truck back up to a
        /// restored shipment is the Trucks phase's job, not this one's.</summary>
        public void Import(List<ShipmentSnapshot> entries)
        {
            _pendingShipments.Clear();
            if (entries == null) return;

            foreach (var snap in entries)
            {
                if (snap == null) continue;

                var shipment = new ShipmentData(snap.poNumber, snap.supplierId, snap.supplierName, snap.arrivalDayNumber, snap.arrivalTimeMinute, (ShipmentData.ShipmentStatus)snap.status);
                foreach (var liSnap in snap.lineItems)
                {
                    var li = new ShipmentLineItem(liSnap.skuId, liSnap.quantity, liSnap.unitCost, liSnap.shelfLifeDays)
                    {
                        ReceivedQuantity = liSnap.receivedQuantity,
                        FloorSlotIndex = liSnap.floorSlotIndex,
                        PalletTier = liSnap.palletTier
                    };
                    shipment.LineItems.Add(li);
                }
                _pendingShipments.Add(shipment);
            }

            // Existing saves are full of finished POs from before they were ever retired — this is
            // what actually clears the backlog out of the Dev Console's inbound list on first load.
            PurgeCompleted();

            if (_pendingShipments.Count > 0)
                Debug.Log($"[ShipmentService] Restored {_pendingShipments.Count} pending shipment(s).");
        }
}
}
