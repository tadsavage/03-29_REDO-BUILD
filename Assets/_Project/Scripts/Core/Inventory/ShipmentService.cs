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

        // ── Supplier variance ────────────────────────────────────────────────

        /// <summary>Fallback short-ship rate for freight with no vendor behind it — a spot-market
        /// deal, or a PO raised before the vendor roster existed. Kept well under half on purpose: a
        /// short load has to be an EVENT the player reacts to, and something that happens most times
        /// is just a tax the player learns to pre-order against.</summary>
        private const float DefaultShortShipmentChance = 0.25f;

        /// <summary>
        /// How likely this PO is to arrive short — derived from the VENDOR'S current Partnership
        /// Level fill rate where one is known (100% fill rate = 0% short-ship chance).
        ///
        /// This is what turns the roster from a price list into a cast. A house running a strained
        /// Partnership and a poor fill rate is a genuine decision against one running Elite Partner
        /// terms and almost never missing, and the player learns which is which the only way that
        /// matters: by being let down.
        /// </summary>
        private static float ShortShipChanceFor(ShipmentData shipment)
        {
            var vendor = VendorRegistry.Load()?.GetById(shipment?.SupplierId);
            if (vendor == null) return DefaultShortShipmentChance;

            if (!ServiceLocator.TryGet<VendorEconomyService>(out var economy) || economy == null)
                return DefaultShortShipmentChance;

            return Mathf.Clamp01(1f - (economy.GetFillRate(vendor.VendorId) / 100f));
        }

        private const int MinPalletsDropped = 1;
        private const int MaxPalletsDropped = 3;

        /// <summary>Tracks which POs have already been rolled, so a shipment that gets dispatched
        /// twice (a re-spawn after a failed first attempt) can't be short-shipped twice.</summary>
        private readonly HashSet<string> _varianceApplied = new();

        /// <summary>Raised when a PO is short-shipped: (shipment, pallets dropped, dollars credited).
        /// The panel and the toast listen; nothing here assumes a UI exists.</summary>
        public static event System.Action<ShipmentData, int, int> OnShipmentShorted;

        /// <summary>
        /// Rolls what the supplier ACTUALLY put on the truck, once, at dispatch.
        ///
        /// Dispatch is the right moment for this and the alternatives are worse. Rolling at PO
        /// creation would show the player a short order before the truck existed; rolling at
        /// receiving would mean the trailer arrives carrying pallets that then have to vanish off it.
        /// Rolling here mutates the manifest before TruckController.LoadShipment reads it, so the
        /// physical trailer, the PO list and the receiving records all agree from the first frame.
        ///
        /// The player is CREDITED for what didn't come. POs are billed at creation, and being charged
        /// for freight that never arrived reads as the game stealing from you rather than the supplier
        /// letting you down — the interesting loss is the missing stock and the fill rate it costs,
        /// not the money.
        /// </summary>
        private void ApplySupplierVariance(ShipmentData shipment)
        {
            if (shipment == null || string.IsNullOrEmpty(shipment.PONumber)) return;
            if (!_varianceApplied.Add(shipment.PONumber)) return;

            // Generated/dev-tool freight is scenery for testing; only the player's own money and fill
            // rate are on the line, so only player POs carry risk.
            if (!shipment.PlayerOrdered) return;

            // A broker load is ALREADY the gamble. Its damaged pallets were rolled and priced in at
            // offer time; short-shipping it on top would be charging the player twice for the same
            // uncertainty, and the missing pallets would be indistinguishable from the junk they knew
            // they were buying.
            if (shipment.IsSalvage) return;

            var deliverable = shipment.LineItems.Where(li => li != null && !li.Dropped).ToList();
            if (deliverable.Count <= 1) return;   // never strand a single-pallet PO with nothing at all

            if (Random.value > ShortShipChanceFor(shipment)) return;

            // Never more than half the load, so a short shipment is a setback rather than a wipeout.
            int maxDrop = Mathf.Min(MaxPalletsDropped, deliverable.Count / 2);
            if (maxDrop < MinPalletsDropped) return;

            int dropCount = Random.Range(MinPalletsDropped, maxDrop + 1);
            int credited = 0;

            for (int i = 0; i < dropCount; i++)
            {
                int idx = Random.Range(0, deliverable.Count);
                var li = deliverable[idx];
                deliverable.RemoveAt(idx);

                li.Dropped = true;
                credited += li.TotalCost;
            }

            if (credited > 0)
                _moneyService?.AddCapital(credited, FinanceCategory.CasePick);

            Debug.LogWarning($"[ShipmentService] PO {shipment.PONumber} short-shipped by {dropCount} " +
                             $"pallet(s) — ${credited:N0} credited back.");

            // Told LOUDLY, at dispatch, not discovered later on the PO list. A short load changes what
            // the player can promise today, and finding out by noticing a thin trailer is not finding
            // out. Same direct-UIToast pattern DockScheduleService and OrderArrivalService already use.
            UIToast.Show($"PO {shipment.PONumber} short-shipped — {dropCount} pallet(s) didn't make " +
                         $"the truck. ${credited:N0} credited back.");

            OnShipmentShorted?.Invoke(shipment, dropCount, credited);
        }

        private void TrySpawnTruck(ShipmentData shipment)
        {
            if (shipment == null || shipment.Status == ShipmentData.ShipmentStatus.Received || shipment.Status == ShipmentData.ShipmentStatus.Departed) return;

            // Before the yard-manager lookup, so the manifest is settled no matter which branch below
            // ends up spawning the truck.
            ApplySupplierVariance(shipment);

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

                if (!_yardManager.SpawnNextTruck(shipment))
                {
                    HandleNoAvailableDoor(shipment);
                    return;
                }

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

        /// <summary>
        /// Called when a PO's appointment came due and every door was occupied — the driver doesn't
        /// wait around, he turns around and goes back to his facility (per Tad's spec: no queueing,
        /// no silent retry). Three things happen, all-or-nothing per attempt:
        ///   1. The appointment is handed back to the unscheduled pool via TryPark, so the player has
        ///      to actively give it a new door/time rather than it silently re-attempting forever.
        ///   2. The vendor's Partnership Level takes a flat -20 hit — a missed door is on the player,
        ///      not the vendor, and the relationship pays for it.
        ///   3. A toast tells the player exactly what happened and what to do about it.
        ///
        /// If there's no real appointment to release (a dev-tool order, or one whose booking was lost
        /// — see DispatchDueShipments' fallback comment), none of the above fires: there's no pool box
        /// to return it to, and parking nothing while still leaving the shipment InTransit would just
        /// re-trigger this every tick forever. That case keeps the old silent-retry behavior instead.
        /// </summary>
        private void HandleNoAvailableDoor(ShipmentData shipment)
        {
            if (!ServiceLocator.TryGet(out DockScheduleService dockSchedule) || dockSchedule == null)
                return;

            var appt = dockSchedule.FindForPo(shipment.PONumber);
            if (appt == null)
            {
                Debug.LogWarning($"[ShipmentService] No free door for PO {shipment.PONumber} and no " +
                                  "appointment to release — will keep retrying.");
                return;
            }

            dockSchedule.TryPark(appt.Id, out string failReason);
            if (failReason != null)
                Debug.LogWarning($"[ShipmentService] Couldn't park PO {shipment.PONumber}'s appointment " +
                                  $"after a failed door attempt: {failReason}");

            if (ServiceLocator.TryGet(out VendorEconomyService economy) && economy != null &&
                !string.IsNullOrEmpty(shipment.SupplierId))
            {
                economy.AdjustPartnershipLevel(shipment.SupplierId, -20,
                    $"No available door for PO {shipment.PONumber} — driver turned around");
            }

            UIToast.Show($"No available door for PO {shipment.PONumber}, driver has turned around and " +
                         "went back to his facility - please reschedule him");
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
                    playerOrdered = s.PlayerOrdered,
                    isSalvage = s.IsSalvage
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
                        palletTier = li.PalletTier,
                        dropped = li.Dropped,
                        salvage = (int)li.Salvage
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
                    PlayerOrdered = snap.playerOrdered,
                    IsSalvage = snap.isSalvage
                };
                foreach (var liSnap in snap.lineItems)
                {
                    var li = new ShipmentLineItem(liSnap.skuId, liSnap.quantity, liSnap.unitCost, liSnap.shelfLifeDays)
                    {
                        ReceivedQuantity = liSnap.receivedQuantity,
                        FloorSlotIndex = liSnap.floorSlotIndex,
                        PalletTier = liSnap.palletTier,
                        Dropped = liSnap.dropped,
                        Salvage = (SalvageCondition)liSnap.salvage
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
