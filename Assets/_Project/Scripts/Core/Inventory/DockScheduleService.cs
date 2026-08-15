using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Economy;
using GameCore.Events;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>What a booked block is holding a door for.</summary>
    public enum AppointmentKind
    {
        /// <summary>A standing account's daily orders going out on one trailer.</summary>
        Outbound,
        /// <summary>An inbound purchase order occupying a door to be unloaded.</summary>
        Inbound,
        /// <summary>RETIRED — a one-off wholesale pallet drop, folded into Bulk (see
        /// ContractKind.OneOffWholesale). Kept for ordinal stability; nothing produces this value
        /// any more, and it's treated the same as Bulk anywhere it's still read.</summary>
        Wholesale,
        /// <summary>A bulk order — full pallets out of reserve, priced off cost of goods.
        /// NOTE: appended, never reordered — persisted by ordinal in DockAppointmentSnapshot.</summary>
        Bulk
    }

    /// <summary>One customer's trailer at one door for one two-hour block.</summary>
    [System.Serializable]
    public class DockAppointment
    {
        public string Id;
        public int Day;
        /// <summary>0–11. Block N covers hours [N*2, N*2+2).</summary>
        public int BlockIndex;
        public int DoorNumber;
        public AppointmentKind Kind;
        public string CustomerId;
        public string CustomerName;
        /// <summary>ContractData.ContractId, or null for inbound / hand-made orders.</summary>
        public string ContractId;
        /// <summary>Orders riding on this trailer. Empty for an inbound PO.</summary>
        public List<string> OrderIds = new();

        public int StartHour => BlockIndex * DockScheduleService.BlockHours;
        public int EndHour => StartHour + DockScheduleService.BlockHours;
        public string TimeLabel => $"{StartHour:00}:00–{EndHour:00}:00";
    }

    /// <summary>
    /// Orders on the board that no appointment is holding a door for, collapsed to the unit an
    /// appointment actually books: one customer's orders under one contract, i.e. one trailer.
    ///
    /// Not a persisted record — derived from the order list every time it's asked for, the same
    /// "always re-derivable from current state" approach StagingLaneAssignmentService takes. A stored
    /// list of stranded orders would be a second thing to keep in step with the schedule.
    /// </summary>
    public class UnscheduledGroup
    {
        public string CustomerId;
        public string CustomerName;
        public string ContractId;
        public bool IsBulk;
        public List<string> OrderIds = new();
        /// <summary>Soonest deadline in the group — what the UI sorts by, since the most urgent
        /// stranded trailer is the one the player needs to see first.</summary>
        public int EarliestDueDay;

        public AppointmentKind Kind => IsBulk ? AppointmentKind.Bulk : AppointmentKind.Outbound;

        /// <summary>Stable identity for UI selection. Customer alone isn't enough — one customer can
        /// hold several contracts, and each is its own trailer.</summary>
        public string Key => $"{CustomerId}|{ContractId}";
    }

    /// <summary>Save shape for a DockAppointment. Flat fields, same style as OrderSnapshot.</summary>
    [System.Serializable]
    public class DockAppointmentSnapshot
    {
        public string id;
        public int day;
        public int blockIndex;
        public int doorNumber;
        public int kind;
        public string customerId;
        public string customerName;
        public string contractId;
        public List<string> orderIds = new();
    }

    /// <summary>
    /// The dock appointment book: which customer has which door in which two-hour block.
    ///
    /// WHAT A BLOCK ACTUALLY RESERVES — a trailer standing at a door, not a piece of paperwork.
    /// That distinction is the whole design. A contract's CutoffHour still governs when its orders
    /// ARRIVE on the Work Queue; this decides when the truck to carry them shows up. Door count only
    /// constrains anything if the thing being counted physically occupies a door, so capacity here is
    /// doors, and the player's decision is which window to promise each account.
    ///
    /// INBOUND COMPETES FOR THE SAME DOORS. A dock door doesn't know which way a trailer is facing.
    /// If purchase orders were invisible here the player would book every door at 08:00 and then have
    /// a PO arrive with nowhere to go, so ShipmentService books an Inbound appointment for the block
    /// its truck actually lands in and it consumes capacity like anything else.
    ///
    /// ARRIVING ORDERS AUTO-BOOK, BUT THE PLAN IS STILL THE PLAYER'S TO CHANGE. An order that lands
    /// with no appointment already covering its customer+contract is placed into the first block with
    /// room, walking forward from right now toward the order's due day (never past it — a booking
    /// after the deadline would look handled while still guaranteeing the fine). That placement is a
    /// starting point, not a lock: TryMove/IsLocked impose no extra restriction on an auto-placed
    /// appointment, so the player can drag it anywhere else with room the same as a hand-booked one.
    /// The only orders that reach the stranded strip are ones that genuinely found no room before
    /// their due day, and those still cost the account after 30 days (OrderArrivalService.
    /// SweepMissedPickups) if they stay stranded.
    ///
    /// CAPACITY IS OUTBOUND-CAPABLE DOORS, NOT ALL DOORS. A door only counts if at least one of its
    /// shipping lanes will accept outbound work (LaneUsage.Outbound or Both — Both being the default,
    /// so in practice every door with lanes counts until the player says otherwise). Counting bare
    /// doors with no lanes would let the player "fix" a congested schedule by buying a door they
    /// can't actually stage into, and the 2-hour block would stop biting.
    /// </summary>
    public class DockScheduleService : IService
    {
        /// <summary>In-game hours per block. Twelve blocks span a 24-hour day.</summary>
        public const int BlockHours = 2;
        public const int BlocksPerDay = 24 / BlockHours;

        /// <summary>How many past days of appointments to keep. Purely so the Schedule tab can page
        /// back a day to see what happened; older ones are dropped at each day roll.</summary>
        private const int KeepPastDays = 2;

        private readonly List<DockAppointment> _appointments = new();
        private EventManager _eventManager;
        private SimulationTimeService _timeService;

        public IReadOnlyList<DockAppointment> Appointments => _appointments;

        public void Initialize()
        {
            _eventManager = EventManager.Instance;
            ServiceLocator.TryGet(out _timeService);

            OrderService.OnOrderArrived -= HandleOrderArrived;
            OrderService.OnOrderArrived += HandleOrderArrived;

            if (_eventManager != null)
                _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
        }

        public void Shutdown()
        {
            OrderService.OnOrderArrived -= HandleOrderArrived;
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            _appointments.Clear();
        }

        public void ClearAll() => _appointments.Clear();

        // ── Time helpers ─────────────────────────────────────────────────────

        public int CurrentDay => _timeService?.Day ?? 0;

        /// <summary>Block containing the given hour.</summary>
        public static int BlockForHour(int hour) => Mathf.Clamp(hour / BlockHours, 0, BlocksPerDay - 1);

        public int CurrentBlock => BlockForHour(_timeService?.Hour ?? 0);

        public static string BlockLabel(int blockIndex)
        {
            int start = blockIndex * BlockHours;
            return $"{start:00}:00–{start + BlockHours:00}:00";
        }

        // ── Capacity ─────────────────────────────────────────────────────────

        /// <summary>
        /// Door numbers that can take an outbound trailer, ascending. A door qualifies if it has at
        /// least one shipping lane whose usage isn't Inbound-only.
        ///
        /// Recomputed on each call rather than cached: lanes and doors are placed and deleted through
        /// the build FSM at any moment, and LaneNamingService already recomputes itself on a heartbeat
        /// — a cache here would just be a second thing to invalidate.
        /// </summary>
        public List<int> OutboundDoors()
        {
            var doors = new SortedSet<int>();
            foreach (var (door, lane) in LaneNamingService.AllLanes())
            {
                if (LaneConfigRegistry.Get(door, lane).Usage != LaneUsage.Inbound)
                    doors.Add(door);
            }
            return doors.ToList();
        }

        /// <summary>Appointments per block the yard can physically honour. 0 means the player has no
        /// outbound-capable door yet — the Schedule tab says so rather than showing an empty grid
        /// that looks like a bug.</summary>
        public int CapacityPerBlock => OutboundDoors().Count;

        public IEnumerable<DockAppointment> GetBlock(int day, int blockIndex)
            => _appointments.Where(a => a.Day == day && a.BlockIndex == blockIndex)
                            .OrderBy(a => a.DoorNumber);

        public bool HasRoom(int day, int blockIndex) => GetBlock(day, blockIndex).Count() < CapacityPerBlock;

        public DockAppointment FindById(string id)
            => string.IsNullOrEmpty(id) ? null : _appointments.FirstOrDefault(a => a.Id == id);

        /// <summary>The appointment an order is riding on, or null if it hasn't been scheduled.</summary>
        public DockAppointment FindForOrder(string orderId)
            => string.IsNullOrEmpty(orderId) ? null : _appointments.FirstOrDefault(a => a.OrderIds.Contains(orderId));

        /// <summary>Today-or-later appointments for a customer, earliest first.</summary>
        public IEnumerable<DockAppointment> UpcomingFor(string customerId)
        {
            int today = CurrentDay;
            return _appointments
                .Where(a => a.CustomerId == customerId && (a.Day > today || (a.Day == today && a.BlockIndex >= CurrentBlock)))
                .OrderBy(a => a.Day).ThenBy(a => a.BlockIndex);
        }

        // ── Booking ──────────────────────────────────────────────────────────

        /// <summary>
        /// Books the lowest-numbered free door in a block. Returns false with a reason the UI can
        /// show verbatim — the player needs to know WHICH constraint stopped them, since "no doors"
        /// and "that block is full" have completely different fixes.
        /// </summary>
        public bool TryBook(int day, int blockIndex, AppointmentKind kind,
                            string customerId, string customerName, string contractId,
                            out DockAppointment booked, out string failReason)
        {
            booked = null;
            failReason = null;

            if (blockIndex < 0 || blockIndex >= BlocksPerDay)
            {
                failReason = "That isn't a real time block.";
                return false;
            }

            var doors = OutboundDoors();
            if (doors.Count == 0)
            {
                failReason = "No outbound doors — place a dock door with shipping lanes first.";
                return false;
            }

            var taken = GetBlock(day, blockIndex).Select(a => a.DoorNumber).ToHashSet();
            int free = doors.FirstOrDefault(d => !taken.Contains(d));
            if (taken.Count >= doors.Count || free == 0)
            {
                failReason = $"{BlockLabel(blockIndex)} is full — all {doors.Count} door(s) are booked.";
                return false;
            }

            booked = new DockAppointment
            {
                Id = System.Guid.NewGuid().ToString(),
                Day = day,
                BlockIndex = blockIndex,
                DoorNumber = free,
                Kind = kind,
                CustomerId = customerId,
                CustomerName = customerName,
                ContractId = contractId
            };
            _appointments.Add(booked);
            return true;
        }

        /// <summary>
        /// Whether an appointment is a finished record rather than a plan the player can still change.
        ///
        /// A block only means anything while there's still a decision in it. Once the trailer has been
        /// and gone, the chip is history — dragging it to next Tuesday doesn't un-ship the freight, it
        /// just puts a lie on the calendar and burns a door that a real trailer needed. Three ways an
        /// appointment stops being a plan:
        ///
        ///   PAST      its block has already elapsed. Nothing can be scheduled into a time that's gone.
        ///   WORKED    no order on it still needs a door — reusing NeedsAppointment, the same predicate
        ///             that decides whether an order is stranded, so "this order needs a dock" has
        ///             exactly one definition. Anything from Loading onward counts as worked: at
        ///             Loading a truck is physically at the door, and past that the freight is on it.
        ///   INBOUND   BookInboundNow files these as an honest note that a PO's truck is taking a door
        ///             right now, not as a reservation. There was never a decision here to revise.
        ///
        /// An appointment with no orders attached yet is NOT locked — it's an empty booking the player
        /// made ahead of the freight, which is exactly the thing they should be able to move.
        /// </summary>
        public bool IsLocked(DockAppointment appt, out string reason)
        {
            reason = null;
            if (appt == null) { reason = "That appointment no longer exists."; return true; }

            if (appt.Kind == AppointmentKind.Inbound)
            {
                reason = "That's an inbound PO's truck taking a door — not a booking you can move.";
                return true;
            }

            if (appt.Day < CurrentDay || (appt.Day == CurrentDay && appt.BlockIndex < CurrentBlock))
            {
                reason = $"{BlockLabel(appt.BlockIndex)} on day {appt.Day} has already passed.";
                return true;
            }

            if (appt.OrderIds.Count > 0 && !AnyOrderStillNeedsDock(appt))
            {
                // Deliberately not "already loaded": this same branch catches cancelled orders, where
                // that would be a lie. "No longer needs a door" is true of every way it can trigger.
                reason = $"{appt.CustomerName}'s orders no longer need a door — that trailer is done.";
                return true;
            }

            return false;
        }

        public bool IsLocked(DockAppointment appt) => IsLocked(appt, out _);

        /// <summary>Does any order on this trailer still want a door? An order that's left the active
        /// list entirely (shipped and archived, or cancelled) counts as no.</summary>
        private bool AnyOrderStillNeedsDock(DockAppointment appt)
        {
            if (!ServiceLocator.TryGet(out OrderService orders)) return true; // can't tell — don't lock

            foreach (var order in orders.ActiveOrders)
                if (order != null && appt.OrderIds.Contains(order.OrderId) && NeedsAppointment(order))
                    return true;
            return false;
        }

        /// <summary>Moves an existing appointment to another block, keeping its orders. Fails (leaving
        /// the original untouched) if the destination is full, in the past, or if the appointment is
        /// no longer a live plan.</summary>
        public bool TryMove(string appointmentId, int day, int blockIndex, out string failReason)
        {
            failReason = null;
            var appt = FindById(appointmentId);
            if (appt == null) { failReason = "That appointment no longer exists."; return false; }
            if (appt.Day == day && appt.BlockIndex == blockIndex) return true;

            // Checked here and not only in the UI: the panel decides what to grey out, but this is what
            // makes it true. A chip can also finish WHILE it sits selected, between the click that picked
            // it up and the click that puts it down.
            if (IsLocked(appt, out failReason)) return false;

            if (day < CurrentDay || (day == CurrentDay && blockIndex < CurrentBlock))
            {
                failReason = "That time has already passed.";
                return false;
            }

            var doors = OutboundDoors();
            var taken = GetBlock(day, blockIndex).Where(a => a.Id != appointmentId)
                                                 .Select(a => a.DoorNumber).ToHashSet();
            int free = doors.FirstOrDefault(d => !taken.Contains(d));
            if (free == 0)
            {
                failReason = $"{BlockLabel(blockIndex)} is full.";
                return false;
            }

            appt.Day = day;
            appt.BlockIndex = blockIndex;
            appt.DoorNumber = free;
            return true;
        }

        public bool Cancel(string appointmentId)
        {
            var appt = FindById(appointmentId);
            if (appt == null) return false;
            _appointments.Remove(appt);
            return true;
        }

        /// <summary>Records the door an inbound PO's truck is taking right now. Called by
        /// ShipmentService as it spawns — the truck is already rolling, so this isn't a reservation
        /// so much as an honest note that the door is spoken for.</summary>
        public void BookInboundNow(string supplierId, string supplierName)
        {
            if (TryBook(CurrentDay, CurrentBlock, AppointmentKind.Inbound, supplierId, supplierName, null,
                        out _, out string why))
                return;

            // A PO that can't get a slot still arrives — the yard queue handles that. Log it so a
            // congested dock is visible rather than mysterious.
            Debug.Log($"[DockSchedule] Inbound {supplierName} not booked into {BlockLabel(CurrentBlock)}: {why}");
        }

        // ── Arrival ──────────────────────────────────────────────────────────

        /// <summary>
        /// An arriving order joins a trailer the player has already booked for that account. Otherwise
        /// it's auto-placed into the first block with room before its due day, so the Schedule tab is
        /// a tool for overriding a plan that already works rather than a chore that must be completed
        /// before anything ships — a player who never opens it doesn't lose accounts to a due date they
        /// didn't know was ticking. The placement is just a default: nothing about TryMove/IsLocked
        /// treats an auto-placed appointment any differently from a hand-booked one, so the player is
        /// always free to drag it somewhere else.
        ///
        /// The join branch stays for the same reason it always existed: same customer AND same contract
        /// shares a trailer. Matching on customer alone would put a bulk pallet drop on the same
        /// appointment as that customer's case-pick orders — physically two different trailers, and the
        /// chip could only be coloured as one of them.
        /// </summary>
        private void HandleOrderArrived(OrderData order)
        {
            if (order == null) return;

            var existing = UpcomingFor(order.CustomerId)
                .FirstOrDefault(a => a.Kind != AppointmentKind.Inbound && a.ContractId == order.ContractId);
            if (existing != null)
            {
                if (!existing.OrderIds.Contains(order.OrderId)) existing.OrderIds.Add(order.OrderId);
                return;
            }

            if (CapacityPerBlock <= 0) return; // nothing to book against; stays unscheduled

            var kind = order.IsBulk ? AppointmentKind.Bulk : AppointmentKind.Outbound;
            if (TryAutoPlace(order, kind, out var appt))
                appt.OrderIds.Add(order.OrderId);
        }

        /// <summary>Books the first block with room, starting at the current block today and walking
        /// forward. Never schedules past the order's due day — an appointment after the deadline is
        /// worse than none, because it looks handled while guaranteeing the fine.</summary>
        private bool TryAutoPlace(OrderData order, AppointmentKind kind, out DockAppointment booked)
        {
            booked = null;
            int day = CurrentDay;
            int block = CurrentBlock;
            int lastDay = Mathf.Max(order.DueDay, day);

            while (day <= lastDay)
            {
                for (; block < BlocksPerDay; block++)
                {
                    if (!HasRoom(day, block)) continue;
                    if (TryBook(day, block, kind, order.CustomerId, order.CustomerName, order.ContractId,
                                out booked, out _))
                        return true;
                }
                day++;
                block = 0;
            }

            Debug.LogWarning($"[DockSchedule] No free dock slot for {order.CustomerName} before day {order.DueDay} " +
                             $"— order is unscheduled. Capacity is {CapacityPerBlock} door(s) per block.");
            return false;
        }

        // ── Stranded orders ──────────────────────────────────────────────────

        /// <summary>
        /// Statuses that still need a trailer to turn up. Loading and Loaded are excluded because the
        /// truck is already at the door or has the freight on board — booking those a fresh block would
        /// consume capacity for a trip that's happening anyway. Backorder is excluded because nothing
        /// currently produces it, so what it should mean for the dock is undecided.
        /// </summary>
        private static bool NeedsAppointment(OrderData order)
            => order.Status == OrderData.OrderStatus.Pending
            || order.Status == OrderData.OrderStatus.PartiallyPicked
            || order.Status == OrderData.OrderStatus.FullyPicked
            || order.Status == OrderData.OrderStatus.Staged;

        /// <summary>
        /// Live orders no appointment is holding a door for, most urgent first.
        ///
        /// The overflow view now, not the primary one: arrival auto-places into the first block with
        /// room, so most orders never pass through here. What lands here is freight TryAutoPlace
        /// genuinely couldn't fit before its due day (or arrived while capacity was 0), plus anything
        /// the player deliberately unbooked. Anything still here when its due day passes costs the
        /// account (OrderArrivalService.SweepMissedPickups).
        /// </summary>
        public List<UnscheduledGroup> UnscheduledGroups()
        {
            var groups = new List<UnscheduledGroup>();
            if (!ServiceLocator.TryGet(out OrderService orders)) return groups;

            var byKey = new Dictionary<string, UnscheduledGroup>();
            foreach (var order in orders.ActiveOrders)
            {
                if (order == null || !NeedsAppointment(order)) continue;
                if (FindForOrder(order.OrderId) != null) continue;

                string key = $"{order.CustomerId}|{order.ContractId}";
                if (!byKey.TryGetValue(key, out var group))
                {
                    group = new UnscheduledGroup
                    {
                        CustomerId = order.CustomerId,
                        CustomerName = order.CustomerName,
                        ContractId = order.ContractId,
                        IsBulk = order.IsBulk,
                        EarliestDueDay = order.DueDay
                    };
                    byKey[key] = group;
                    groups.Add(group);
                }

                group.OrderIds.Add(order.OrderId);
                group.EarliestDueDay = Mathf.Min(group.EarliestDueDay, order.DueDay);
            }

            groups.Sort((a, b) => a.EarliestDueDay != b.EarliestDueDay
                ? a.EarliestDueDay.CompareTo(b.EarliestDueDay)
                : string.Compare(a.CustomerName, b.CustomerName, System.StringComparison.Ordinal));
            return groups;
        }

        // NOTE: there is deliberately no "synthetic DockAppointment per stranded group" helper here.
        // One existed and the Schedule tab's pool used it — but a synthetic appointment's Id is the
        // group key, not a real appointment id, so selecting one and clicking a slot sent an id
        // TryMove could never find. Stranded freight books through UnscheduledGroups + TryBookGroup;
        // it is not an appointment until that call succeeds.

        /// <summary>Books a stranded group into one specific block — the player's own choice from the
        /// Schedule tab, as opposed to the sweep's first-fit. Same capacity rules as everything else;
        /// the reason comes back verbatim for the UI to show.</summary>
        public bool TryBookGroup(int day, int blockIndex, UnscheduledGroup group, out string failReason)
        {
            failReason = null;
            if (group == null) { failReason = "Nothing selected to book."; return false; }

            if (!TryBook(day, blockIndex, group.Kind, group.CustomerId, group.CustomerName,
                         group.ContractId, out var booked, out failReason))
                return false;

            booked.OrderIds.AddRange(group.OrderIds);
            return true;
        }

        // ── Housekeeping ─────────────────────────────────────────────────────

        private void OnDayChanged(string eventId, int newDay)
        {
            int cutoff = newDay - KeepPastDays;
            _appointments.RemoveAll(a => a.Day < cutoff);

            // NOTE: no re-booking sweep here any more. The purge does strand orders that outlived
            // their appointment, but re-booking them automatically is exactly the behaviour that made
            // the Schedule tab optional — the player rebooks, or loses the account.
        }

        // ── Persistence ──────────────────────────────────────────────────────

        public List<DockAppointmentSnapshot> Export() => _appointments.Select(a => new DockAppointmentSnapshot
        {
            id = a.Id,
            day = a.Day,
            blockIndex = a.BlockIndex,
            doorNumber = a.DoorNumber,
            kind = (int)a.Kind,
            customerId = a.CustomerId,
            customerName = a.CustomerName,
            contractId = a.ContractId,
            orderIds = new List<string>(a.OrderIds)
        }).ToList();

        public void Import(List<DockAppointmentSnapshot> entries)
        {
            _appointments.Clear();
            if (entries == null) return;

            foreach (var s in entries)
            {
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                _appointments.Add(new DockAppointment
                {
                    Id = s.id,
                    Day = s.day,
                    BlockIndex = Mathf.Clamp(s.blockIndex, 0, BlocksPerDay - 1),
                    DoorNumber = s.doorNumber,
                    Kind = (AppointmentKind)s.kind,
                    CustomerId = s.customerId,
                    CustomerName = s.customerName,
                    ContractId = s.contractId,
                    OrderIds = s.orderIds ?? new List<string>()
                });
            }

            if (_appointments.Count > 0)
                Debug.Log($"[DockSchedule] Restored {_appointments.Count} dock appointment(s).");
        }
    }
}
