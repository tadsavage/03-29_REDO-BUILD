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
        /// <summary>A one-off wholesale pallet drop.</summary>
        Wholesale
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

        /// <summary>Moves an existing appointment to another block, keeping its orders. Fails (leaving
        /// the original untouched) if the destination is full.</summary>
        public bool TryMove(string appointmentId, int day, int blockIndex, out string failReason)
        {
            failReason = null;
            var appt = FindById(appointmentId);
            if (appt == null) { failReason = "That appointment no longer exists."; return false; }
            if (appt.Day == day && appt.BlockIndex == blockIndex) return true;

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

        // ── Auto-placement ───────────────────────────────────────────────────

        /// <summary>
        /// Every arriving order gets a slot without the player touching the grid, so the Schedule tab
        /// is a tool for overriding a plan rather than a chore that must be completed before anything
        /// ships. The player reschedules what they care about and ignores the rest.
        ///
        /// Orders from the same customer on the same day share ONE appointment — one customer, one
        /// trailer. That matches how OrderService already refuses to share a staging lane between
        /// customers.
        /// </summary>
        private void HandleOrderArrived(OrderData order)
        {
            if (order == null) return;
            if (CapacityPerBlock <= 0) return; // nothing to book against; stays unscheduled

            // Same customer AND same contract shares a trailer. Matching on customer alone would put
            // a wholesale pallet drop on the same appointment as that customer's case-pick orders —
            // physically two different trailers, and the chip could only be coloured as one of them.
            var existing = UpcomingFor(order.CustomerId)
                .FirstOrDefault(a => a.Kind != AppointmentKind.Inbound && a.ContractId == order.ContractId);
            if (existing != null)
            {
                if (!existing.OrderIds.Contains(order.OrderId)) existing.OrderIds.Add(order.OrderId);
                return;
            }

            var kind = order.IsWholesale ? AppointmentKind.Wholesale : AppointmentKind.Outbound;
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

        // ── Housekeeping ─────────────────────────────────────────────────────

        private void OnDayChanged(string eventId, int newDay)
        {
            int cutoff = newDay - KeepPastDays;
            _appointments.RemoveAll(a => a.Day < cutoff);
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
