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

        /// <summary>Finished POs, newest first. Bounded by COUNT rather than age, same reasoning as
        /// OrderService's order history: a ShipmentData records no closed-on day, and save size tracks
        /// record count anyway.</summary>
        private readonly List<ShipmentData> _archivedShipments = new();
        private const int MaxArchivedShipments = 250;

        private TruckYardManager _yardManager;
        private SimulationTimeService _timeService;
        private MoneyService _moneyService;

        /// <summary>The clock, re-resolved if this service lost it — same reasoning as
        /// DockScheduleService.Clock. A null one here defaults every "what day is it" question to 1,
        /// which would dispatch trucks for POs dated days ahead.</summary>
        private SimulationTimeService Clock
        {
            get
            {
                if (_timeService != null) return _timeService;
                ServiceLocator.TryGet(out _timeService);
                return _timeService;
            }
        }

        public IReadOnlyList<ShipmentData> PendingShipments => _pendingShipments;

        /// <summary>Finished POs, newest first — what the Purchasing panel's Archived tab lists.</summary>
        public IReadOnlyList<ShipmentData> ArchivedShipments => _archivedShipments;

        private EventManager _eventManager;

        public void Initialize()
        {
            _yardManager = Object.FindAnyObjectByType<TruckYardManager>();
            _timeService = ServiceLocator.Get<SimulationTimeService>();
            ServiceLocator.TryGet(out _moneyService); // optional — a PO still files if money is down

            // Backstop for the per-departure purge: a truck destroyed mid-route (deleted, scene
            // wiped, domain reload) never reaches BeginDeparture, so its PO would otherwise sit in
            // the list forever.
            _eventManager = EventManager.Instance;
            _eventManager?.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            // HOURLY as well as daily: a PO booked into this afternoon's 14:00–16:00 slot has to leave
            // when that block comes round, which a midnight-only tick can't express.
            _eventManager?.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
        }

        public void Shutdown()
        {
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
            _pendingShipments.Clear();
        }

        private void OnHourChanged(string eventId, int newHour)
            => DispatchDueShipments(Clock != null ? Clock.Day : 1);

        private void OnDayChanged(string eventId, int newDay)
        {
            PurgeCompleted();
            DispatchDueShipments(newDay);
        }

        /// <summary>
        /// Sends trucks for every PO whose booked dock slot has come round.
        ///
        /// A PLAYER PO WAITS FOR ITS APPOINTMENT. The Purchasing panel drops each new order into the
        /// Schedule tab's unscheduled pool; until the player places it on a door and block, no truck
        /// leaves. That's the point of putting it in the pool — inbound and outbound compete for the
        /// same doors, so freight that turned up before it had a slot would take a door the player was
        /// holding for something else. A parked PO simply sits there, visible, until scheduled.
        ///
        /// Once placed, it goes when that block starts: right day, at or past the booked block.
        ///
        /// A PO with NO appointment at all (a dev-tool order, or one whose booking was lost) falls
        /// back to the old day-only rule rather than never shipping — the fallback is what stops a
        /// missing appointment turning into freight that silently never arrives.
        /// </summary>
        private void DispatchDueShipments(int today)
        {
            ServiceLocator.TryGet(out DockScheduleService dockSchedule);
            int nowBlock = dockSchedule?.CurrentBlock ?? 0;

            foreach (var shipment in _pendingShipments.ToList())
            {
                if (shipment == null || shipment.Status != ShipmentData.ShipmentStatus.InTransit) continue;

                var appt = dockSchedule?.FindForPo(shipment.PONumber);
                if (appt != null)
                {
                    if (appt.Parked) continue;                       // not scheduled yet — it waits
                    if (appt.Day > today) continue;                  // booked for a later day
                    if (appt.Day == today && appt.BlockIndex > nowBlock) continue; // later today
                }
                else if (shipment.ArrivalDayNumber > today) continue;

                TrySpawnTruck(shipment);
            }
        }

        /// <summary>Wipes both PO lists without tearing down the service — used by the Dev Console's
        /// "Clear Scene" button so leftover test POs don't keep spawning trucks against a scene
        /// that's just been wiped.</summary>
        public void ClearAll()
        {
            _pendingShipments.Clear();
            _archivedShipments.Clear();
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
            var finished = _pendingShipments
                .Where(s => s == null ||
                            s.Status == ShipmentData.ShipmentStatus.Departed ||
                            s.Status == ShipmentData.ShipmentStatus.Received ||
                            s.Status == ShipmentData.ShipmentStatus.Cancelled)
                .ToList();
            if (finished.Count == 0) return 0;

            foreach (var s in finished)
            {
                _pendingShipments.Remove(s);
                if (s == null) continue;

                // ARCHIVED, not discarded. These used to be dropped on the floor; the Purchasing
                // panel's Archived tab is a record of what you've bought, which needs them to survive
                // leaving the pending list. Newest first so the tab reads top-down without sorting.
                _archivedShipments.Insert(0, s);
            }

            if (_archivedShipments.Count > MaxArchivedShipments)
                _archivedShipments.RemoveRange(MaxArchivedShipments,
                                               _archivedShipments.Count - MaxArchivedShipments);

            Debug.Log($"[ShipmentService] Retired {finished.Count} finished PO(s) to the archive; " +
                      $"{_pendingShipments.Count} still pending, {_archivedShipments.Count} archived.");
            return finished.Count;
        }

        /// <summary>Creates a new Purchase Order and schedules a truck arrival.</summary>
        public void CreatePurchaseOrder(string supplierId, string supplierName, List<ShipmentLineItem> items)
        {
            int day = Clock != null ? Clock.Day : 1;
            int minute = Clock != null ? Clock.Minute : 480; // 8:00 AM default

            var shipment = new ShipmentData(supplierId, supplierName, day, minute);
            shipment.LineItems.AddRange(items);

            _pendingShipments.Add(shipment);

            Debug.Log($"[ShipmentService] Created PO {shipment.PONumber} for {supplierName} with {items.Count} items.");

            // For now, spawn immediately if possible
            TrySpawnTruck(shipment);
        }

        /// <summary>
        /// Raises a PO the PLAYER built in the Purchasing panel: their own pre-reserved number, their
        /// chosen delivery day, and their money.
        ///
        /// Three things it does that the dev-tool CreatePurchaseOrder above deliberately doesn't:
        ///
        ///   NUMBER   the panel displays "PO #: 837194" while the order is still being filled in, so
        ///            the number is reserved up front and passed in. Minting one here would show the
        ///            player a number that isn't the one they get.
        ///   DAY      the truck waits for the requested day (DispatchDueShipments). Today spawns now.
        ///   MONEY    goods are paid for when ORDERED. Nothing else in the inbound path charges for
        ///            stock, so without this the player could fill a warehouse for free.
        ///
        /// Returns the shipment so the caller can show what it created. Refuses an empty basket rather
        /// than filing a PO for nothing.
        /// </summary>
        public ShipmentData CreatePlayerPurchaseOrder(string poNumber, string supplierId, string supplierName,
                                                      List<ShipmentLineItem> items, int arrivalDay)
        {
            if (items == null || items.Count == 0) return null;

            int today = Clock != null ? Clock.Day : 1;
            int minute = Clock != null ? Clock.Minute : 480;
            int day = Mathf.Max(today, arrivalDay);

            var shipment = new ShipmentData(poNumber, supplierId, supplierName, day, minute)
            {
                PlayerOrdered = true
            };
            shipment.LineItems.AddRange(items);
            _pendingShipments.Add(shipment);

            int cost = shipment.TotalCost;
            // Deduct(), not RemoveCapital(): this is a one-time purchase, and Deduct is what routes it
            // into the Spent Today panel's Purchases section instead of being counted as hourly upkeep.
            if (cost > 0) _moneyService?.Deduct(cost, "Inventory");

            // Straight into the Schedule tab's unscheduled pool. The delivery day says WHICH day the
            // freight is wanted; the pool is where the player says which door and which two-hour block
            // it turns up in — the same decision they make for every outbound trailer, against the
            // same finite set of doors.
            if (ServiceLocator.TryGet(out DockScheduleService dockSchedule) && dockSchedule != null)
                dockSchedule.ParkInboundForPo(shipment.PONumber, shipment.SupplierId,
                                              shipment.SupplierName, day);

            Debug.Log($"[ShipmentService] Player raised PO {shipment.PONumber} — {items.Count} line(s), " +
                      $"{shipment.TotalUnits} case(s), ${cost:N0}, wanted day {day}" +
                      (day <= today ? " (today)." : ".") + " Waiting in the unscheduled pool for a door.");

            // Deliberately NOT dispatched here, even for a same-day PO. The truck goes when the
            // player has given it a slot — see DispatchDueShipments. Spawning immediately would make
            // the pool box a lie: the freight would already be in the yard before it was scheduled.
            return shipment;
        }

        /// <summary>Cancels a pending PO the player raised. Refunds what it cost, since nothing has
        /// been delivered — a truck already dispatched is past the point of a clean cancel, so an
        /// order that's left InTransit is refused rather than half-undone.</summary>
        public bool CancelPurchaseOrder(string poNumber, out string failReason)
        {
            failReason = null;
            var shipment = _pendingShipments.FirstOrDefault(s => s != null && s.PONumber == poNumber);
            if (shipment == null) { failReason = "That PO is no longer pending."; return false; }
            if (shipment.Status != ShipmentData.ShipmentStatus.InTransit)
            {
                failReason = $"PO {poNumber} is already being received — too late to cancel.";
                return false;
            }

            int today = Clock != null ? Clock.Day : 1;
            if (shipment.ArrivalDayNumber <= today)
            {
                failReason = $"PO {poNumber}'s truck is already on its way — too late to cancel.";
                return false;
            }

            if (shipment.PlayerOrdered && shipment.TotalCost > 0)
                _moneyService?.AddCapital(shipment.TotalCost, FinanceCategory.CasePick);

            // Take its door reservation down with it, whether it was still in the pool or already
            // placed on the grid — a cancelled PO holding a slot would keep a door out of use for
            // freight that is never coming.
            if (ServiceLocator.TryGet(out DockScheduleService dockSchedule) && dockSchedule != null)
                dockSchedule.ReleasePo(poNumber);

            shipment.Status = ShipmentData.ShipmentStatus.Cancelled;
            PurgeCompleted(); // moves it straight to the archive, where a cancelled PO belongs
            return true;
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
                //
                // A PO the player already SCHEDULED is skipped: its appointment is the slot, and
                // filing a second one here would have the same truck holding two doors at once.
                if (ServiceLocator.TryGet(out DockScheduleService dockSchedule) && dockSchedule != null)
                {
                    if (dockSchedule.FindForPo(shipment.PONumber) == null)
                        dockSchedule.BookInboundNow(shipment.SupplierId, shipment.SupplierName);
                }
            }
            else
            {
                Debug.LogError("[ShipmentService] Cannot spawn truck: TruckYardManager not found in scene.");
            }
        }

        /// <summary>Flattens pending AND archived POs into one list for saving — archived rows are
        /// identifiable by their terminal Status, so Import can sort them back apart without a schema
        /// change, and a save written before the archive existed loads unchanged. Same approach
        /// OrderService takes with its own history.</summary>
        public List<ShipmentSnapshot> Export()
        {
            var list = new List<ShipmentSnapshot>();
            foreach (var s in _pendingShipments.Concat(_archivedShipments))
            {
                if (s == null) continue;
                var snap = new ShipmentSnapshot
                {
                    poNumber = s.PONumber,
                    supplierId = s.SupplierId,
                    supplierName = s.SupplierName,
                    arrivalDayNumber = s.ArrivalDayNumber,
                    arrivalTimeMinute = s.ArrivalTimeMinute,
                    status = (int)s.Status,
                    playerOrdered = s.PlayerOrdered
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
            _archivedShipments.Clear();
            if (entries == null) return;

            foreach (var snap in entries)
            {
                if (snap == null) continue;

                var shipment = new ShipmentData(snap.poNumber, snap.supplierId, snap.supplierName, snap.arrivalDayNumber, snap.arrivalTimeMinute, (ShipmentData.ShipmentStatus)snap.status)
                {
                    PlayerOrdered = snap.playerOrdered
                };
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

                // Claim the restored number so a new random PO can't collide with one from the save —
                // two live POs sharing a number would make TrySpawnTruck's duplicate check reject the
                // second one's truck.
                PONumberGenerator.RegisterExisting(shipment.PONumber);
            }

            // Sorts the terminal rows back out into the archive, and — for saves written before the
            // archive existed — is still what clears their backlog of finished POs out of the pending
            // list on first load.
            PurgeCompleted();

            if (_pendingShipments.Count > 0 || _archivedShipments.Count > 0)
                Debug.Log($"[ShipmentService] Restored {_pendingShipments.Count} pending and " +
                          $"{_archivedShipments.Count} archived shipment(s).");
        }
}
}
