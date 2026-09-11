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

        /// <summary>
        /// The inbound PO this appointment is holding a door for, or null.
        ///
        /// Non-empty is what separates the two completely different things an Inbound appointment can
        /// be. BookInboundNow files a NOTE that a truck is standing at a door right now — there was
        /// never a decision in it and it can't be moved. A PO the player raised through Purchasing
        /// files a RESERVATION for freight that hasn't left the supplier yet, which is a plan like any
        /// other: it starts life parked in the unscheduled pool and the player drops it on the day,
        /// block and door they want it to arrive at.
        ///
        /// IsLocked keys off this, so the note stays immovable and the reservation stays draggable.
        /// </summary>
        public string ShipmentPoNumber;
        /// <summary>Orders riding on this trailer. Empty for an inbound PO, and empty for a recurring
        /// trailer that's been pre-booked ahead of its order arriving.</summary>
        public List<string> OrderIds = new();

        /// <summary>
        /// The player has pulled this trailer off the grid but it still exists — it's sitting in the
        /// unscheduled pool waiting to be put back at a door.
        ///
        /// This is what makes shuffling doors possible. Returning a trailer to the pool used to DELETE
        /// the appointment and let the pool re-derive a box from its orders, which worked only for
        /// freight that had orders: a recurring trailer pre-booked before its order arrives carries
        /// none, so unbooking one destroyed the only record of it and the box simply vanished. Parking
        /// keeps the appointment — with its customer, contract, day and BLOCK intact — so the same
        /// trailer can be put back down, and so a recurring trailer can't launder its fixed time slot
        /// by being unbooked and re-booked somewhere else.
        ///
        /// A parked trailer occupies no door: GetBlock excludes it, so it frees its slot the moment
        /// it's picked up and doesn't count against capacity.
        /// </summary>
        public bool Parked;

        /// <summary>
        /// This block's time has been swept past by DockScheduleService.SweepElapsedAppointments — its
        /// window has closed, so it no longer holds real dock capacity (GetBlock excludes it by default,
        /// the same way it excludes Parked). It's kept in the list rather than deleted so the Scheduler
        /// grid can still show it sitting in the block it happened in, instead of the slot just going
        /// blank once the clock passes it.
        ///
        /// This flag only ever means "capacity is free" — it says nothing about whether the trailer's
        /// work actually succeeded. Whether a closed-out chip reads as finished (a normal strike) or
        /// missed (a red one) is decided at render time by IsComplete, which is computed independently
        /// from live state and takes priority: a PO that came in fine before its block elapsed is
        /// ClosedOut AND IsComplete, and renders as finished, not missed.
        /// </summary>
        public bool ClosedOut;

        /// <summary>
        /// This trailer has already cost its customer satisfaction for sitting outside their requested
        /// hour. One offence per trailer, not one per click.
        ///
        /// Without it, every landing on an off-slot block docked satisfaction again — so a player
        /// shuffling doors to fit a busy morning could take five hits for the single fact that one
        /// trailer isn't at 16:00, and moving it from 20:00 to 22:00 (no worse for the customer, both
        /// wrong) cost as much as the original mistake. The penalty belongs to the STATE of being
        /// off-slot, which is reached once.
        ///
        /// Deliberately never cleared, including by moving back onto the requested slot. Putting it
        /// right is worth doing — it stops the trailer being late — but it doesn't un-annoy a customer
        /// who was already told their slot moved, and a flag that clears would make an off/on/off
        /// shuffle chargeable again, which is the exact thing this exists to prevent.
        /// </summary>
        public bool OffSlotPenaltyApplied;

        /// <summary>
        /// This trailer has, at some point, actually missed a promised window and paid a real
        /// consequence for it — set alongside <see cref="LateFineAmount"/>/<see
        /// cref="LateRelationshipPenalty"/> the moment that consequence is charged (see
        /// SweepElapsedAppointments' outbound branch and JudgeElapsedInboundAppointment's
        /// driver-never-showed branch).
        ///
        /// Deliberately separate from <see cref="ClosedOut"/> and never cleared by it. ClosedOut only
        /// means "this block's capacity is free" and gets reset the moment the trailer is re-placed on
        /// the grid (TryMoveToDoor) — that's what stops a rescheduled trailer staying struck through
        /// forever. But un-crossing it must not also erase the fact that this customer/vendor was
        /// already burned once; WasLate is that permanent record, rendered as a small "LATE" corner
        /// badge instead of the strike so the player is still warned after the trailer's back on a live
        /// slot.
        /// </summary>
        public bool WasLate;

        /// <summary>Dollar amount of the real late fee already charged against this trailer's order(s)
        /// — see OrderData.LastFineAmount, summed at the moment SweepElapsedAppointments fines them.
        /// 0 when the lateness cost reputation only (the inbound vendor case), never both at once.</summary>
        public int LateFineAmount;

        /// <summary>Magnitude of the vendor-partnership hit already charged for this PO missing its
        /// window entirely (see JudgeElapsedInboundAppointment) — e.g. 20, always rendered as a
        /// negative. 0 when this trailer's lateness was billed in dollars instead (the outbound case).</summary>
        public int LateRelationshipPenalty;

        /// <summary>
        /// The DAY this trailer was originally booked for, which is what "a day late" is measured
        /// against. Stamped once when the appointment is created and never changed by a move — that is
        /// the entire point: after the player drags it, Day says where it IS and this says where it was
        /// SUPPOSED to be, and the gap between them is the promise that got broken.
        ///
        /// The requested HOUR needs no equivalent field: it comes from the contract's CutoffHour, which
        /// can't be moved by dragging a chip. The day can't be derived that way, because a recurring
        /// account wants a trailer EVERY day and nothing in the contract says which one this is.
        ///
        /// 0 means "not recorded" — a save written before this existed. Treated as "no day
        /// displacement" rather than "day zero", which would read every restored appointment as
        /// catastrophically late the moment it was loaded.
        /// </summary>
        public int RequestedDay;

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
        /// <summary>False in a save written before parking existed — correct, since every appointment
        /// in such a save was on the grid.</summary>
        public bool parked;
        /// <summary>Null in a save written before player purchasing existed — correct, since no
        /// appointment in such a save was ever a PO reservation.</summary>
        public string shipmentPoNumber;
        /// <summary>False in a save written before the once-only off-slot penalty existed. That is the
        /// forgiving default: at worst an already-penalized trailer can be charged once more after
        /// loading such a save, rather than a fresh trailer being wrongly treated as already paid.</summary>
        public bool offSlotPenaltyApplied;
        /// <summary>0 in a save written before off-slot distance was measured. Read as "no baseline
        /// recorded", which costs nothing — the forgiving default, matching offSlotPenaltyApplied.</summary>
        public int requestedDay;
        /// <summary>False in a save written before ClosedOut existed — the forgiving default, same
        /// reasoning as parked/offSlotPenaltyApplied. Worst case an old save's already-elapsed
        /// appointments get re-swept (and re-closed) the next time SweepElapsedAppointments runs.</summary>
        public bool closedOut;
        /// <summary>False/0 in a save written before the LATE badge existed — the forgiving default,
        /// same reasoning as the other penalty flags above.</summary>
        public bool wasLate;
        public int lateFineAmount;
        public int lateRelationshipPenalty;
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
    /// EVERY ARRIVAL AUTO-BOOKS ITS OWN REQUESTED HOUR, RECURRING OR BULK ALIKE. The target is the
    /// contract's own CutoffHour — the closest thing on record to "the slot the customer actually
    /// asked for" — walking forward block-by-block (and day-by-day) from there if that slot's already
    /// taken, never past the order's due day (a booking after the deadline would look handled while
    /// still guaranteeing the fine). That placement is a starting point, not a lock: TryMoveToDoor/
    /// IsLocked impose no extra restriction on an auto-placed appointment, so the player can drag it
    /// anywhere else with room the same as a hand-booked one. Landing anywhere other than that
    /// requested-hour block — during auto-book OR a manual move — costs a small customer-satisfaction
    /// penalty (OrderArrivalService.PenalizeSatisfaction) and shows a toast; see MissedRequestedSlot.
    /// Freight only ever reaches the unscheduled pool if TryAutoPlace genuinely finds no room anywhere
    /// before the due day, and freight that stays unscheduled past its due day costs the account after
    /// 30 days on top of that (OrderArrivalService.SweepMissedPickups).
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

        /// <summary>How many past days of appointments to keep once a day fully rolls over. Zero —
        /// there's no need to look back at a past day's schedule, so a day's appointments are purged
        /// the moment it stops being "today" rather than lingering around for archiving.</summary>
        private const int KeepPastDays = 0;

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
            {
                _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
                _eventManager.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
            }
        }

        public void Shutdown()
        {
            OrderService.OnOrderArrived -= HandleOrderArrived;
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
            _appointments.Clear();
        }

        public void ClearAll() => _appointments.Clear();

        // ── Time helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// The clock, re-resolved from the locator if this service somehow lost it.
        ///
        /// WHY THIS ISN'T JUST THE FIELD. A null clock used to make CurrentDay and CurrentBlock both
        /// return 0, and 0 is not a harmless default here — it's "midnight on day zero", which makes
        /// EVERY block read as still in the future. The past-slot lockout vanishes, elapsed blocks
        /// stop turning red, and IsLocked stops refusing bookings into times that have already gone.
        /// The whole schedule silently fails OPEN, with nothing in the console to say so.
        ///
        /// Observed for real: a service instance that never had Initialize() run got registered over
        /// the live one, and the only symptom was that the grid quietly stopped locking the past.
        /// Re-resolving here means a service that missed its wiring repairs itself on first use, and
        /// says so once if it genuinely can't.
        /// </summary>
        private SimulationTimeService Clock
        {
            get
            {
                if (_timeService != null) return _timeService;
                ServiceLocator.TryGet(out _timeService);
                if (_timeService == null && !_warnedNoClock)
                {
                    _warnedNoClock = true;
                    Debug.LogError("[DockSchedule] No SimulationTimeService — the schedule can't tell " +
                                   "which blocks have passed, so past slots will NOT lock. This service " +
                                   "was probably never Initialize()d.");
                }
                return _timeService;
            }
        }
        private bool _warnedNoClock;

        public int CurrentDay => Clock?.Day ?? 0;

        /// <summary>Block containing the given hour.</summary>
        public static int BlockForHour(int hour) => Mathf.Clamp(hour / BlockHours, 0, BlocksPerDay - 1);

        public int CurrentBlock => BlockForHour(Clock?.Hour ?? 0);

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

        /// <summary>Trailers standing at a door in this block. PARKED trailers are excluded — a parked
        /// appointment is in the pool, not at a door, so it must not appear in the grid, block a door
        /// another trailer could use, or count against capacity. CLOSED-OUT trailers (their block has
        /// already been swept — see DockAppointment.ClosedOut) are excluded the same way by default,
        /// since their window is over and the door is free again; pass includeClosedOut:true to also
        /// get them back for DISPLAY purposes, which is all the Scheduler grid wants them for.</summary>
        public IEnumerable<DockAppointment> GetBlock(int day, int blockIndex, bool includeClosedOut = false)
            => _appointments.Where(a => !a.Parked && (includeClosedOut || !a.ClosedOut) &&
                                        a.Day == day && a.BlockIndex == blockIndex)
                            .OrderBy(a => a.DoorNumber);

        public bool HasRoom(int day, int blockIndex) => GetBlock(day, blockIndex).Count() < CapacityPerBlock;

        public DockAppointment FindById(string id)
            => string.IsNullOrEmpty(id) ? null : _appointments.FirstOrDefault(a => a.Id == id);

        /// <summary>Trailers the player has pulled off the grid, waiting in the pool to be put back.
        /// Ordered soonest-first so the most urgent is leftmost, matching UnscheduledGroups.</summary>
        public IEnumerable<DockAppointment> ParkedAppointments
            => _appointments.Where(a => a.Parked).OrderBy(a => a.Day).ThenBy(a => a.BlockIndex);

        /// <summary>
        /// The appointment actually HOLDING A DOOR for this order, or null if nothing is.
        ///
        /// Parked appointments deliberately don't count. This is what callers mean when they ask "is
        /// this freight booked?" — the missed-pickup sweep that loses accounts, and the Bulk Orders
        /// tab's "NO DOOR BOOKED" flag both have to treat a parked trailer as unbooked, because it is.
        /// UnscheduledGroups wants the other question and asks it directly (see IsOnAnyAppointment).
        /// </summary>
        public DockAppointment FindForOrder(string orderId)
            => string.IsNullOrEmpty(orderId)
             ? null
             : _appointments.FirstOrDefault(a => !a.Parked && a.OrderIds.Contains(orderId));

        /// <summary>Is this order attached to any appointment at all, parked or booked? Used only to
        /// keep the pool from listing an order twice — once inside its parked trailer's box and again
        /// as a loose stranded group.</summary>
        private bool IsOnAnyAppointment(string orderId)
            => !string.IsNullOrEmpty(orderId) && _appointments.Any(a => a.OrderIds.Contains(orderId));

        /// <summary>
        /// Is there already a trailer booked for this contract on this day?
        ///
        /// Keyed on CONTRACT, not customer: one customer can hold several contracts and each is its
        /// own trailer, so a customer-level check would see a bulk drop already booked and skip
        /// pre-booking that day's recurring run. What makes OrderArrivalService.MaintainRecurringSchedule
        /// idempotent — it can run on every day roll and every signing without ever double-booking.
        ///
        /// Inbound appointments are excluded: they carry no ContractId, so they can't match, but the
        /// filter is explicit rather than incidental.
        /// </summary>
        public bool HasAppointmentFor(string contractId, int day)
            => !string.IsNullOrEmpty(contractId)
            && _appointments.Any(a => a.Kind != AppointmentKind.Inbound
                                   && a.Day == day && a.ContractId == contractId);

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

            return TryBookAtDoor(day, blockIndex, free, kind, customerId, customerName, contractId,
                                 out booked, out failReason);
        }

        /// <summary>
        /// Books ONE SPECIFIC door in a block. The primitive both TryBook (auto-picks the lowest free
        /// door — arrivals, inbound POs) and TryBookGroupAtDoor (the player's explicit door choice from
        /// the Schedule tab) route through, so there's exactly one place a DockAppointment gets
        /// constructed. The block-range/no-doors-exist checks live in the callers, since what counts as
        /// a valid request differs ("any door free" vs "THIS door free").
        /// </summary>
        private bool TryBookAtDoor(int day, int blockIndex, int doorNumber, AppointmentKind kind,
                                   string customerId, string customerName, string contractId,
                                   out DockAppointment booked, out string failReason)
        {
            booked = null;
            failReason = null;

            if (!OutboundDoors().Contains(doorNumber))
            {
                failReason = $"Door {doorNumber} isn't an outbound-capable door.";
                return false;
            }

            if (GetBlock(day, blockIndex).Any(a => a.DoorNumber == doorNumber))
            {
                failReason = $"Door {doorNumber} is already booked in {BlockLabel(blockIndex)}.";
                return false;
            }

            booked = new DockAppointment
            {
                Id = System.Guid.NewGuid().ToString(),
                Day = day,
                BlockIndex = blockIndex,
                DoorNumber = doorNumber,
                Kind = kind,
                CustomerId = customerId,
                CustomerName = customerName,
                ContractId = contractId,
                // Where this trailer was MEANT to be. Every appointment is created on the day it's
                // wanted — pre-booked recurring trailers by MaintainRecurringSchedule, everything else
                // by the arrival or the player placing it — so creation day is the honest baseline.
                // Moves deliberately leave it alone; see DockAppointment.RequestedDay.
                RequestedDay = day
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

            // Only the "a truck is standing there right now" note is immovable. An inbound appointment
            // carrying a PO number is a reservation for freight still at the supplier — the player
            // raised it in Purchasing and is entitled to say when it should turn up.
            if (appt.Kind == AppointmentKind.Inbound && string.IsNullOrEmpty(appt.ShipmentPoNumber))
            {
                reason = "That's an inbound PO's truck taking a door — not a booking you can move.";
                return true;
            }

            // A PARKED trailer holds no door and no block — its stored Day/BlockIndex are only the
            // pool box's display defaults (see ParkInboundForPo: "Neither is honoured while it sits
            // parked"), not a slot it occupies, so its time can't have "passed". Judging it by that
            // stale block is what silently stopped a PO raised earlier in the day from ever being
            // placed once the clock moved past the block it happened to be parked at: IsLocked fired
            // on the parked block and TryMoveToDoor refused before it ever looked at the future slot
            // the player clicked. The real guard against landing on an elapsed slot is the DESTINATION
            // check in TryMoveToDoor, which stands regardless of this.
            if (!appt.Parked &&
                (appt.Day < CurrentDay || (appt.Day == CurrentDay && appt.BlockIndex < CurrentBlock)))
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

        /// <summary>
        /// Has this trailer's work actually FINISHED — as opposed to merely being unmovable?
        ///
        /// Deliberately separate from <see cref="IsLocked"/>, which conflates three unrelated things:
        /// "this is a truck-at-a-door note", "its block has elapsed", and "its work is done". Only the
        /// last is completion. A booking whose block has passed with the freight still sitting there is
        /// late, not finished, and striking it through would tell the player the opposite of the truth.
        ///
        /// Completion by kind:
        ///   OUTBOUND      no order it carries still wants a door. That covers shipped AND cancelled —
        ///                 both are "this trailer is no longer live work", which is exactly what a line
        ///                 through it means. It is NOT complete while it has no orders yet: that's an
        ///                 empty booking made ahead of the freight, the one thing most in need of doing.
        ///   INBOUND (PO)  the purchase order has been received or has departed, or has left the
        ///                 pending list altogether (ShipmentService.PurgeCompleted retires finished POs,
        ///                 so "not found" here means done, not missing).
        ///   INBOUND (note) BookInboundNow's record that a truck is at a door right now — finished
        ///                 precisely when no inbound truck is at that door any more.
        ///
        /// A parked appointment is never complete: it holds no door and hasn't started.
        /// </summary>
        public bool IsComplete(DockAppointment appt)
        {
            if (appt == null || appt.Parked) return false;

            if (appt.Kind == AppointmentKind.Inbound)
            {
                if (!string.IsNullOrEmpty(appt.ShipmentPoNumber))
                {
                    if (!ServiceLocator.TryGet(out ShipmentService shipments) || shipments == null)
                        return false; // can't tell — never claim done

                    ShipmentData po = null;
                    foreach (var s in shipments.PendingShipments)
                        if (s != null && s.PONumber == appt.ShipmentPoNumber) { po = s; break; }

                    if (po == null) return true; // retired by PurgeCompleted = finished
                    return po.Status == ShipmentData.ShipmentStatus.Received
                        || po.Status == ShipmentData.ShipmentStatus.Departed
                        || po.Status == ShipmentData.ShipmentStatus.Cancelled;
                }

                // A bare "truck is at this door" note: done when the truck has gone.
                return !InboundTruckAtDoor(appt.DoorNumber);
            }

            // Outbound. An appointment with nothing on it yet is a plan, not finished work.
            return appt.OrderIds.Count > 0 && !AnyOrderStillNeedsDock(appt);
        }

        // Doors with an inbound truck at them, refreshed at most this often. IsComplete is asked once
        // per visible chip on a 1s UI tick, and an unguarded FindObjectsByType per chip is a scene-wide
        // scan several times a second for an answer that cannot meaningfully change that fast.
        private static readonly HashSet<int> _inboundDoorCache = new();
        private static float _inboundDoorCacheTime = -1f;
        private const float InboundDoorCacheSeconds = 0.25f;

        /// <summary>Is an INBOUND truck currently docked at this door? Mirrors
        /// StagingLaneAssignmentService.HasInboundTruckDocked; kept local so this service doesn't take a
        /// dependency on the staging layer just to answer a scheduling question.</summary>
        private static bool InboundTruckAtDoor(int doorNumber)
        {
            if (Time.unscaledTime - _inboundDoorCacheTime > InboundDoorCacheSeconds)
            {
                _inboundDoorCacheTime = Time.unscaledTime;
                _inboundDoorCache.Clear();
                foreach (var truck in Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None))
                {
                    if (truck == null || truck.IsOutbound) continue;
                    if (truck.DockedAt != null) _inboundDoorCache.Add(truck.DockedAt.DoorNumber);
                }
            }
            return _inboundDoorCache.Contains(doorNumber);
        }

        /// <summary>
        /// Whether an appointment's block misses its own contract's requested hour (CutoffHour) — the
        /// closest thing on record to "the slot the customer actually asked for" (see the pool-box
        /// label in ContractsPanel.BuildPoolBox, which reads the same field the same way). Used to
        /// decide whether a booking or move earns the "missed their slot" toast and satisfaction
        /// penalty — see HandleOrderArrived (auto-book) and ContractsPanel.OnSlotClicked (manual).
        ///
        /// A Dev Console order (no ContractId) or a contract that no longer resolves can't be judged
        /// either way, so it never counts as a miss — there's nothing on record to have missed.
        /// </summary>
        public bool MissedRequestedSlot(DockAppointment appt)
            => appt != null && WouldMissRequestedSlot(appt.ContractId, appt.BlockIndex);

        /// <summary>
        /// The same judgement asked BEFORE a move instead of after — would this contract's trailer be
        /// off its requested hour if it sat in this block?
        ///
        /// Needed because the warning has to come first. MissedRequestedSlot can only answer for an
        /// appointment that has already been moved, which is fine for reporting a penalty and useless
        /// for offering the player a choice about incurring one.
        ///
        /// Answers false for anything with no contract on record (a Dev Console order) — there's
        /// nothing it could be said to have missed.
        /// </summary>
        public bool WouldMissRequestedSlot(string contractId, int blockIndex)
            => TryGetRequestedBlock(contractId, out int wanted) && blockIndex != wanted;

        /// <summary>
        /// THE SINGLE GATE ON OFF-SLOT PENALTIES. Returns true exactly once per appointment — the
        /// first time that trailer is found sitting outside its customer's requested hour — and false
        /// for every landing after that.
        ///
        /// Every path that penalizes an off-slot trailer must go through this and act only on true:
        /// the player's own move or pool booking (ContractsPanel.AnnounceSlotResult) and the automatic
        /// placement that couldn't get their hour (HandleOrderArrived). One gate rather than a flag
        /// each caller checks for itself, because the rule is "one hit per trailer" and two call sites
        /// each guarding independently would still be two hits.
        ///
        /// It also gates the FINE and the on-screen reaction, not just the satisfaction hit, so the
        /// three can never disagree — no fine without an angry customer, and no angry face floating up
        /// over a move that cost nothing.
        ///
        /// Returns false for a trailer already on its requested slot, and for anything with no
        /// contract on record, so a caller can use it as the whole test.
        /// </summary>
        public bool TryClaimOffSlotPenalty(DockAppointment appt)
        {
            if (appt == null || appt.OffSlotPenaltyApplied) return false;
            if (!MissedRequestedSlot(appt) && OffSlotDaysFrom(appt) == 0) return false;

            appt.OffSlotPenaltyApplied = true;
            return true;
        }

        // ── How badly a trailer misses the slot it was promised ──────────────
        //
        // PLACEHOLDER NUMBERS, deliberately. Tad's anchors: "a day late would be moderately significant
        // like 10 points, whereas anything over two hours is 2 points". They are named constants in one
        // place precisely so balancing is an edit, not an archaeology exercise.
        //
        // Charged per BLOCK and per DAY rather than as one curve over total hours, because those are
        // the two units the player actually manipulates: they drag a chip up and down a column of
        // two-hour blocks, or across to another day. A single hours-based formula would make a
        // one-block nudge and a one-day slip differ only in magnitude, when they're different mistakes.

        /// <summary>Reputation cost per 2-hour block away from the customer's requested hour.</summary>
        public const int RepPointsPerBlockOff = 2;

        /// <summary>Reputation cost per whole day away from the day the trailer was booked for.</summary>
        public const int RepPointsPerDayOff = 10;

        /// <summary>Ceiling on a single trailer's off-slot reputation hit. Without it, dragging one
        /// chip a week out could cost 70 points in a single click — a whole reputation band — for one
        /// mistake the player can still put right.</summary>
        public const int MaxOffSlotRepPenalty = 30;

        /// <summary>Vendor-partnership hit for a PO's driver never showing up for its booked window —
        /// see JudgeElapsedInboundAppointment. Named so the same number that's charged is the number
        /// shown on the "LATE" tooltip, not a second copy that can drift.</summary>
        public const int InboundLateRelationshipPenalty = 20;

        /// <summary>How many in-game HOURS a player has to fully receive a scheduled PO before it's
        /// lost outright — no refund, no backfill/credit. Measured from the appointment's own booked
        /// start, not from CurrentDay/CurrentBlock — see SweepReceiveDeadlines.</summary>
        public const int ReceiveDeadlineHours = 8;

        /// <summary>Vendor-partnership hit for a PO expiring unreceived — worse than
        /// InboundLateRelationshipPenalty since this is a total order loss, not just a slow door.</summary>
        public const int ReceiveDeadlineRelationshipPenalty = 35;

        /// <summary>How many whole days this trailer sits from the day it was booked for. 0 when the
        /// baseline was never recorded (pre-existing save) — see DockAppointment.RequestedDay.</summary>
        public int OffSlotDaysFrom(DockAppointment appt)
        {
            if (appt == null || appt.RequestedDay <= 0) return 0;
            // No contract means nobody asked for a slot, so there is no promise to have broken. This
            // is what keeps a BULK trailer free to move: the player chose its day themselves, and its
            // lateness is already priced by its own due-day fine rather than twice over here.
            if (!TryGetRequestedBlock(appt.ContractId, out _)) return 0;
            return Mathf.Abs(appt.Day - appt.RequestedDay);
        }

        /// <summary>How many 2-hour blocks this trailer sits from the hour its customer asked for.
        /// 0 when there's no contract on record to have asked for anything.</summary>
        public int OffSlotBlocksFrom(DockAppointment appt)
        {
            if (appt == null || !TryGetRequestedBlock(appt.ContractId, out int wanted)) return 0;
            return Mathf.Abs(appt.BlockIndex - wanted);
        }

        /// <summary>
        /// The reputation this landing costs. Scales with distance in both units, so nudging a trailer
        /// one block earlier is a shrug and shipping it a day late is a real mark against the account.
        ///
        /// Returns 0 for anything with no promise to break — a bulk order the player placed themselves,
        /// or freight with no contract on record. Never negative, never above the cap.
        /// </summary>
        public int OffSlotReputationCost(DockAppointment appt)
        {
            if (appt == null) return 0;

            int cost = OffSlotBlocksFrom(appt) * RepPointsPerBlockOff
                     + OffSlotDaysFrom(appt) * RepPointsPerDayOff;

            return Mathf.Clamp(cost, 0, MaxOffSlotRepPenalty);
        }

        /// <summary>
        /// The same sum asked BEFORE the move, so the confirmation dialog can quote a real price rather
        /// than "a negative impact". Takes the destination explicitly because the appointment hasn't
        /// been moved yet — reading it off the live object would price the move the player is trying to
        /// get away FROM.
        /// </summary>
        public int PredictOffSlotReputationCost(string contractId, int requestedDay, int targetDay, int targetBlock)
        {
            // Same rule as OffSlotDaysFrom: no contract, no promise, no penalty.
            if (!TryGetRequestedBlock(contractId, out int wanted)) return 0;

            int blocks = Mathf.Abs(targetBlock - wanted);
            int days = requestedDay > 0 ? Mathf.Abs(targetDay - requestedDay) : 0;

            return Mathf.Clamp(blocks * RepPointsPerBlockOff + days * RepPointsPerDayOff,
                               0, MaxOffSlotRepPenalty);
        }

        /// <summary>Predictive twin of DescribeOffSlot, for the same reason.</summary>
        public string PredictDescribeOffSlot(string contractId, int requestedDay, int targetDay, int targetBlock)
        {
            if (!TryGetRequestedBlock(contractId, out int wanted)) return "on their requested slot";
            int blocks = Mathf.Abs(targetBlock - wanted);
            int days = requestedDay > 0 ? Mathf.Abs(targetDay - requestedDay) : 0;
            return Describe(blocks, days);
        }

        /// <summary>Plain-language version of the same sum, for the warning dialog and the toast — the
        /// player should be told what it costs BEFORE they commit, in the units they moved it in.</summary>
        public string DescribeOffSlot(DockAppointment appt)
            => Describe(OffSlotBlocksFrom(appt), OffSlotDaysFrom(appt));

        private static string Describe(int blocks, int days)
        {
            if (blocks == 0 && days == 0) return "on their requested slot";

            var parts = new List<string>();
            if (days > 0) parts.Add($"{days} day{(days == 1 ? "" : "s")}");
            if (blocks > 0) parts.Add($"{blocks * BlockHours} hour{(blocks * BlockHours == 1 ? "" : "s")}");
            return string.Join(" and ", parts) + " off their slot";
        }

        /// <summary>The block a contract's customer actually asked for, derived from CutoffHour — the
        /// closest thing on record to "their slot", and the same value MaintainRecurringSchedule aims
        /// its pre-bookings at, so the promise and the judgement can't drift apart.</summary>
        public bool TryGetRequestedBlock(string contractId, out int blockIndex)
        {
            blockIndex = 0;
            if (string.IsNullOrEmpty(contractId)) return false;
            if (!ServiceLocator.TryGet(out OrderArrivalService arrivals) || arrivals == null) return false;
            var contract = arrivals.GetContract(contractId);
            if (contract == null) return false;
            blockIndex = BlockForHour(contract.CutoffHour);
            return true;
        }

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

        /// <summary>
        /// Moves an existing appointment to a SPECIFIC block+door, keeping its orders — the player's
        /// explicit choice of both, not an auto-picked door. Fails (leaving the original untouched) if
        /// the destination door is taken, isn't outbound-capable, is in the past, the appointment is no
        /// longer a live plan, or the recurring move rule below refuses it.
        ///
        /// A RECURRING TRAILER MOVES TO ANY BLOCK, EARLIER OR LATER, exactly like a bulk one. It was
        /// briefly pinned to its booked slot; that's been lifted (Tad's call, 2026-08-16) in favour of
        /// letting the player make the trade knowingly — moving off the hour the customer asked for is
        /// allowed, costs a fine and customer satisfaction, and the UI warns before it happens
        /// (ContractsPanel.OnSlotClicked → the off-slot confirmation). A rule the player can break for
        /// a stated price is a decision; a rule they can't break is just a wall.
        ///
        /// STILL PINNED TO ITS OWN DAY. Not a leftover — a recurring order exists on one specific day,
        /// so dragging Thursday's trailer to Friday isn't rescheduling a pickup, it's shipping a day
        /// late with the evidence moved out of sight; and dragging it to Wednesday would ship freight
        /// the customer hasn't ordered yet. The day-level version of "ship it late anyway" is already
        /// modelled properly, by leaving it where it is and eating the fee.
        ///
        /// Bulk is unrestricted in both: its deadline is a whole DAY rather than an hour, the player
        /// placed it themselves, and moving it past that day is a legitimate (if costly) choice the
        /// ordinary late fee already prices.
        /// </summary>
        public bool TryMoveToDoor(string appointmentId, int day, int blockIndex, int doorNumber,
                                  out string failReason)
        {
            failReason = null;
            var appt = FindById(appointmentId);
            if (appt == null) { failReason = "That appointment no longer exists."; return false; }
            // The "already there, nothing to do" shortcut must NOT fire for a parked trailer: putting
            // one back down on the exact door it came off is a real action (it un-parks), and taking
            // the shortcut would leave it stranded in the pool while reporting success.
            if (!appt.Parked && appt.Day == day && appt.BlockIndex == blockIndex && appt.DoorNumber == doorNumber)
                return true;

            // Checked here and not only in the UI: the panel decides what to grey out, but this is what
            // makes it true. A chip can also finish WHILE it sits selected, between the click that picked
            // it up and the click that puts it down.
            if (IsLocked(appt, out failReason)) return false;

            if (appt.Kind == AppointmentKind.Outbound)
            {
                if (day != appt.Day)
                {
                    // No possessive on the customer name anywhere in these two reasons — plenty of the
                    // authored companies already end in "s" ("Sneaky Pete's Seafood"), and "X's's" is
                    // what you get for assuming otherwise.
                    failReason = $"{appt.CustomerName} has a recurring order for day {appt.Day} — " +
                                 $"a standing delivery can't be moved to another day.";
                    return false;
                }
            }

            if (day < CurrentDay || (day == CurrentDay && blockIndex < CurrentBlock))
            {
                failReason = "That time has already passed.";
                return false;
            }

            if (!OutboundDoors().Contains(doorNumber))
            {
                failReason = $"Door {doorNumber} isn't an outbound-capable door.";
                return false;
            }

            bool takenByOther = GetBlock(day, blockIndex).Any(a => a.Id != appointmentId && a.DoorNumber == doorNumber);
            if (takenByOther)
            {
                failReason = $"Door {doorNumber} is already booked in {BlockLabel(blockIndex)}.";
                return false;
            }

            appt.Day = day;
            appt.BlockIndex = blockIndex;
            appt.DoorNumber = doorNumber;
            // Placing a parked trailer is how it gets back on the grid — this is the un-park. Same
            // call the player uses to move a booked one, so a parked trailer is subject to exactly the
            // same rules going back down as it was coming up.
            appt.Parked = false;
            // ClosedOut only ever meant "this block's capacity is free" — it must not survive a
            // successful re-placement, which by the checks above is always onto a CURRENT-OR-FUTURE
            // block. Without this, a trailer that was swept once (couldn't get a door in time / player
            // rescheduled it) stayed struck through forever, even after landing on a brand-new future
            // slot — reading as "already missed" for a delivery that hasn't happened yet. WasLate (see
            // DockAppointment) is the permanent record of that history; this flag is just capacity.
            appt.ClosedOut = false;
            return true;
        }

        public bool Cancel(string appointmentId)
        {
            var appt = FindById(appointmentId);
            if (appt == null) return false;
            _appointments.Remove(appt);
            return true;
        }

        /// <summary>
        /// Pulls a trailer off the grid into the unscheduled pool WITHOUT destroying it — the player
        /// picked it up to put it down somewhere else.
        ///
        /// Replaces Cancel on the return-to-pool path, and the difference is the whole point. Cancel
        /// deleted the appointment and relied on the pool re-deriving a box from its orders, which
        /// silently did nothing for a trailer that has no orders yet — precisely the case
        /// MaintainRecurringSchedule creates every day, so unbooking a pre-booked recurring trailer
        /// made it disappear with no way to get it back. Parking keeps the record, so the trailer sits
        /// in the pool and can be re-placed, which is what makes shuffling doors around workable.
        ///
        /// It also closes a hole the old path left open: re-placing a parked trailer goes back through
        /// TryMoveToDoor, so a recurring trailer still can't change its promised time — unbook and
        /// rebook is no longer a way around the fixed slot.
        ///
        /// Refuses a locked appointment for the same reasons a move does, and refuses an inbound one:
        /// that's a note that a PO's truck is at a door, not a booking anyone can pick up.
        /// </summary>
        public bool TryPark(string appointmentId, out string failReason)
        {
            failReason = null;
            var appt = FindById(appointmentId);
            if (appt == null) { failReason = "That appointment no longer exists."; return false; }
            if (appt.Parked) return true;
            if (IsLocked(appt, out failReason)) return false;

            appt.Parked = true;
            return true;
        }

        /// <summary>The appointment held for a given inbound PO, or null if none is.</summary>
        public DockAppointment FindForPo(string poNumber)
            => string.IsNullOrEmpty(poNumber)
             ? null
             : _appointments.FirstOrDefault(a => a.ShipmentPoNumber == poNumber);

        /// <summary>
        /// Called by OrderService.CancelOrders the moment a player cancels an order that was still
        /// Open/Available (nothing physically committed yet). Strips the cancelled order's ID out of
        /// every appointment carrying it, and parks any outbound appointment left holding zero orders
        /// — freeing its door for a new booking rather than leaving a phantom trailer on the grid with
        /// nothing left to ship.
        ///
        /// Inbound appointments are left alone even if this empties one: an Inbound entry is either a
        /// "truck is physically at this door right now" note or a PO reservation, neither of which
        /// this order's cancellation has any business tidying up.
        /// </summary>
        public void DetachCancelledOrder(string orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return;

            foreach (var appt in _appointments)
            {
                if (!appt.OrderIds.Remove(orderId)) continue;
                if (appt.OrderIds.Count == 0 && appt.Kind != AppointmentKind.Inbound && !appt.Parked)
                    TryPark(appt.Id, out _);
            }
        }

        /// <summary>
        /// Puts a freshly-raised purchase order into the unscheduled pool as a PARKED inbound
        /// appointment, so the player can drop it on the day, block and door they want it at.
        ///
        /// Parked rather than auto-placed on purpose. Inbound and outbound share the same doors, and
        /// the whole reason the Schedule tab exists is to make the player decide who gets which one —
        /// silently booking the first free slot would hand back the decision they just made by
        /// choosing a delivery day. It also means a PO can't consume a door the player was saving
        /// without them seeing it happen.
        ///
        /// Carries the PO's own delivery day so the box in the pool reads as the day the freight was
        /// ordered for, and so placing it doesn't have to re-derive that from the shipment.
        /// </summary>
        public DockAppointment ParkInboundForPo(string poNumber, string supplierId, string supplierName,
                                                int arrivalDay)
        {
            if (string.IsNullOrEmpty(poNumber)) return null;

            var existing = FindForPo(poNumber);
            if (existing != null) return existing; // already has one — never file a second

            var appt = new DockAppointment
            {
                Id = System.Guid.NewGuid().ToString(),
                Day = Mathf.Max(arrivalDay, CurrentDay),
                // Aimed at the PO's own delivery day and the current block. Neither is honoured while
                // it sits parked — a parked appointment occupies no door — but they're what the pool
                // box displays and what the slot defaults to if the player never moves it.
                BlockIndex = CurrentBlock,
                DoorNumber = 0,
                Kind = AppointmentKind.Inbound,
                CustomerId = supplierId,
                CustomerName = supplierName,
                ShipmentPoNumber = poNumber,
                Parked = true
            };
            _appointments.Add(appt);
            Debug.Log($"[DockSchedule] PO {poNumber} ({supplierName}) parked in the unscheduled pool for day {appt.Day}.");
            return appt;
        }

        /// <summary>Drops the appointment held for a PO — used when that PO is cancelled, so a
        /// cancelled order doesn't leave a ghost box sitting in the pool forever.</summary>
        public bool ReleasePo(string poNumber)
        {
            var appt = FindForPo(poNumber);
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
        /// it's auto-placed — RECURRING or BULK alike — targeting its own contract's requested hour
        /// (CutoffHour) first and walking forward from there if that slot's taken, so the Schedule tab
        /// is a tool for overriding a plan that already works rather than a chore that must be completed
        /// before anything ships. The placement is just a default: nothing about TryMoveToDoor/IsLocked
        /// treats an auto-placed appointment any differently from a hand-booked one, so the player is
        /// always free to drag it somewhere else. It only ever lands in the unscheduled pool if TryAutoPlace
        /// genuinely can't find room anywhere before the order's due day.
        ///
        /// The join branch stays for the same reason it always existed: same customer AND same contract
        /// shares a trailer. Matching on customer alone would put a bulk pallet drop on the same
        /// appointment as that customer's case-pick orders — physically two different trailers, and the
        /// chip could only be coloured as one of them.
        /// </summary>
        private void HandleOrderArrived(OrderData order)
        {
            if (order == null) return;

            // Prefer a trailer booked for TODAY over merely the next one upcoming. With recurring
            // accounts pre-booked a week ahead (OrderArrivalService.MaintainRecurringSchedule) there
            // are now several matching appointments in front of an arriving order, and taking the
            // earliest upcoming one is only right by accident: if today's block has already elapsed,
            // "earliest upcoming" is TOMORROW's trailer, and today's freight would silently ride a
            // slot booked for a different day's order. Falling through to auto-place instead puts it
            // on today where its deadline actually is, or strands it visibly if the dock is full.
            // A recurring order can now be materialized DAYS before its delivery slot so the player can
            // plan and pick it ahead of time. CreatedDayNumber is deliberately stamped with that slot
            // by OrderArrivalService.GenerateFor(contract, scheduledDay), so attach this manifest to
            // the appointment on THAT day — never blindly to CurrentDay. Otherwise the first future
            // order could join today's pre-booked trailer, while the Recurring Orders tab correctly
            // displays another future manifest for the same account.
            int scheduledDay = !order.IsBulk && order.CreatedDayNumber >= CurrentDay
                ? order.CreatedDayNumber
                : CurrentDay;
            var candidates = UpcomingFor(order.CustomerId)
                .Where(a => a.Kind != AppointmentKind.Inbound && a.ContractId == order.ContractId)
                .ToList();
            var existing = candidates.FirstOrDefault(a => a.Day == scheduledDay) ??
                           (order.IsBulk ? candidates.FirstOrDefault() : null);
            if (existing != null)
            {
                if (!existing.OrderIds.Contains(order.OrderId)) existing.OrderIds.Add(order.OrderId);
                ReconcileFutureRecurringAppointments();
                AnnounceOrderArrival(order, existing.DoorNumber);
                return;
            }

            if (CapacityPerBlock <= 0) return; // nothing to book against; stays unscheduled

            ServiceLocator.TryGet(out OrderArrivalService arrivals);
            int? requestedHour = !string.IsNullOrEmpty(order.ContractId)
                ? arrivals?.GetContract(order.ContractId)?.CutoffHour
                : null;

            var kind = order.IsBulk ? AppointmentKind.Bulk : AppointmentKind.Outbound;
            if (!TryAutoPlace(scheduledDay, requestedHour, Mathf.Max(order.DueDay, scheduledDay), kind,
                              order.CustomerId, order.CustomerName, order.ContractId, out var appt))
                return;

            appt.OrderIds.Add(order.OrderId);
            ReconcileFutureRecurringAppointments();
            // Through the same one-shot gate the player's own moves use — a trailer that already cost
            // satisfaction for being off-slot must not cost it again just because a second order
            // joined it, and this path can run repeatedly for one appointment (once per arriving
            // order on it).
            if (TryClaimOffSlotPenalty(appt))
            {
                // No confirmation dialog on this path and none wanted: the player didn't choose this.
                // Auto-placement aims at the requested block and only lands elsewhere when the dock was
                // genuinely full, so the wording says that rather than blaming them for a move they
                // never made. Satisfaction still drops — the customer doesn't care whose fault it is —
                // but no fine is charged here, because a fine is the price of a decision and there
                // wasn't one.
                UIToast.Show($"No door free at {order.CustomerName}'s usual time — booked into " +
                             $"{BlockLabel(appt.BlockIndex)} instead. Satisfaction down.");
                arrivals?.PenalizeSatisfaction(order.ContractId);
            }

            AnnounceOrderArrival(order, appt.DoorNumber);
        }

        /// <summary>Guard-shack "new order arrived" line — pallet count and critical-item count use
        /// the same math SchedulerPanel's order-details card already shows (FullPalletCases packing,
        /// on-hand-can't-cover-this-line for critical), just narrated instead of drawn.</summary>
        private static void AnnounceOrderArrival(OrderData order, int doorNumber)
        {
            if (order == null) return;
            ServiceLocator.TryGet<OrderService>(out var orders);
            ServiceLocator.TryGet<InventoryService>(out var inventory);

            int pallets = 0;
            int critical = 0;
            foreach (var li in order.LineItems)
            {
                int fullPallet = orders != null ? orders.FullPalletCases(li.SkuId) : 0;
                if (fullPallet > 0) pallets += Mathf.CeilToInt(li.QuantityNeeded / (float)fullPallet);

                int onHand = inventory != null ? inventory.GetTotalUnitsBySku(li.SkuId) : 0;
                if (onHand < li.QuantityNeeded) critical++;
            }

            string kindWord = order.IsBulk ? "Bulk order" : "Order";
            string displayNumber = string.IsNullOrEmpty(order.OrderNumber) ? order.OrderId : order.OrderNumber;
            SystemsLogWindow.LogGuard(
                $"{kindWord} {displayNumber} going to Door {doorNumber} with {pallets} pallet(s) — {critical} critical item(s).");
        }

        /// <summary>
        /// Repairs appointments written by the former early-generation behaviour, which attached a
        /// future recurring manifest to the trailer booked for the day it was generated rather than its
        /// own delivery day. For every future recurring order, its CreatedDayNumber is the delivery slot
        /// stamped by OrderArrivalService.GenerateFor; move only that order ID to the matching existing
        /// appointment. This is idempotent, preserves the player's chosen door/block/parked state, and
        /// makes Schedule and Recurring Orders read the exact same order data after a save is loaded.
        /// </summary>
        private void ReconcileFutureRecurringAppointments()
        {
            if (!ServiceLocator.TryGet<OrderService>(out var orders) || orders == null) return;

            foreach (var order in orders.ActiveOrders)
            {
                if (order == null || order.IsBulk || string.IsNullOrEmpty(order.ContractId)
                    || order.Status == OrderData.OrderStatus.Shipped
                    || order.Status == OrderData.OrderStatus.Cancelled
                    || order.CreatedDayNumber < CurrentDay) continue;

                var target = _appointments.FirstOrDefault(a => a.Kind != AppointmentKind.Inbound
                    && a.ContractId == order.ContractId && a.CustomerId == order.CustomerId
                    && a.Day == order.CreatedDayNumber);
                if (target == null) continue;

                foreach (var appointment in _appointments)
                {
                    if (appointment != target) appointment.OrderIds.Remove(order.OrderId);
                }
                if (!target.OrderIds.Contains(order.OrderId)) target.OrderIds.Add(order.OrderId);
            }
        }


        /// <summary>
        /// Books a customer's own requested hour if one was given and hasn't already passed today,
        /// walking forward block-by-block — and day-by-day past midnight — until room turns up, never
        /// past lastDay (an appointment after the deadline is worse than none, because it looks handled
        /// while guaranteeing the fine). Falls back to starting from right now when there's no requested
        /// hour to aim for (a Dev Console order, or a contract that no longer resolves).
        ///
        /// Public and built from primitives rather than an OrderData so two different callers share the
        /// same placement logic: an arriving order (HandleOrderArrived) and a just-signed recurring
        /// contract pre-booking its very first appointment BEFORE any order exists yet
        /// (OrderArrivalService.Sign) — without the pre-book, a recurring account's slot didn't show up
        /// on the Schedule tab until its first order actually generated at the contract's cutoff hour,
        /// which read as broken to a player expecting to see it the moment they signed.
        /// </summary>
        public bool TryAutoPlace(int day, int? requestedHour, int lastDay, AppointmentKind kind,
                                 string customerId, string customerName, string contractId,
                                 out DockAppointment booked)
        {
            booked = null;

            int block = requestedHour.HasValue ? BlockForHour(requestedHour.Value) : CurrentBlock;
            if (day == CurrentDay && block < CurrentBlock) block = CurrentBlock; // their hour already passed today — can't book into the past

            while (day <= lastDay)
            {
                for (; block < BlocksPerDay; block++)
                {
                    if (!HasRoom(day, block)) continue;
                    if (TryBook(day, block, kind, customerId, customerName, contractId, out booked, out _))
                        return true;
                }
                day++;
                block = 0;
            }

            Debug.LogWarning($"[DockSchedule] No free dock slot for {customerName} before day {lastDay} " +
                             $"— staying unscheduled. Capacity is {CapacityPerBlock} door(s) per block.");
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
        /// The overflow view, not the primary one — recurring and bulk orders alike auto-place on
        /// arrival now (HandleOrderArrived), so most freight never passes through here. What lands here
        /// is freight TryAutoPlace genuinely couldn't fit before its due day (or that arrived while
        /// capacity was 0), plus anything the player deliberately unbooked. Anything still here when its
        /// due day passes costs the account (OrderArrivalService.SweepMissedPickups).
        /// </summary>
        public List<UnscheduledGroup> UnscheduledGroups()
        {
            var groups = new List<UnscheduledGroup>();
            if (!ServiceLocator.TryGet(out OrderService orders)) return groups;

            var byKey = new Dictionary<string, UnscheduledGroup>();
            foreach (var order in orders.ActiveOrders)
            {
                if (order == null || !NeedsAppointment(order)) continue;
                // IsOnAnyAppointment, not FindForOrder: an order riding a PARKED trailer is already
                // represented in the pool by that trailer's own box. Testing "has a door" here instead
                // would list it twice — once as the parked box, once as a loose stranded group — and
                // booking either copy would leave the other behind.
                if (IsOnAnyAppointment(order.OrderId)) continue;

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

        /// <summary>Books a stranded group into one specific block AND door — the player's own choice
        /// of both from the Schedule tab, as opposed to the sweep's first-fit. Same capacity rules as
        /// everything else; the reason comes back verbatim for the UI to show.</summary>
        public bool TryBookGroupAtDoor(int day, int blockIndex, int doorNumber, UnscheduledGroup group,
                                       out DockAppointment booked, out string failReason)
        {
            booked = null;
            failReason = null;
            if (group == null) { failReason = "Nothing selected to book."; return false; }

            if (!TryBookAtDoor(day, blockIndex, doorNumber, group.Kind, group.CustomerId, group.CustomerName,
                               group.ContractId, out booked, out failReason))
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

        private void OnHourChanged(string eventId, int newHour)
        {
            SweepElapsedAppointments();
            SweepReceiveDeadlines();
            ReconcileFutureRecurringAppointments();
        }

        /// <summary>
        /// Enforces the hard 8-hour receive-or-lose-it deadline: distinct from
        /// JudgeElapsedInboundAppointment's 2-hour "driver never showed" judgment (which only fires
        /// once, right when the booked BLOCK elapses, and only catches a truck that never even
        /// dispatched). This checks EVERY still-open inbound PO appointment, every hour, against its own
        /// booked start time — so it also catches a PO that dispatched, got a door, and is just taking
        /// far too long to fully receive (or one still sitting parked in the unscheduled pool).
        ///
        /// On expiry: the PO is cancelled outright — no refund (already paid at dispatch, and nothing
        /// arrived), no backfill/credit option (RequestBackfill/RequestCredit both refuse a Cancelled
        /// shipment). Any live truck still around for it is forced out. Per Tad's spec: "if we don't
        /// receive within 8 hours of the scheduler's appt - we lose the order and get nothing, driver
        /// leaves - no credit and no backfill."
        /// </summary>
        private void SweepReceiveDeadlines()
        {
            if (!ServiceLocator.TryGet(out ShipmentService shipments) || shipments == null) return;
            if (Clock == null) return;

            long nowMinutes = Clock.TotalMinutesElapsed;

            foreach (var appt in _appointments.ToList())
            {
                if (appt.Kind != AppointmentKind.Inbound || string.IsNullOrEmpty(appt.ShipmentPoNumber)) continue;
                if (appt.ClosedOut) continue;
                // Still sitting in the unscheduled pool — the 8-hour clock is "since the scheduler's
                // APPOINTMENT", which doesn't exist yet for a PO the player hasn't placed on a door/block.
                if (appt.Parked) continue;

                ShipmentData po = shipments.PendingShipments.FirstOrDefault(s => s != null && s.PONumber == appt.ShipmentPoNumber);
                if (po == null) continue; // already archived/gone — nothing left to expire
                if (po.Status == ShipmentData.ShipmentStatus.Received ||
                    po.Status == ShipmentData.ShipmentStatus.Departed ||
                    po.Status == ShipmentData.ShipmentStatus.Cancelled) continue;

                long apptStartMinutes = (long)(appt.Day - 1) * 24 * 60 + appt.StartHour * 60;
                if (nowMinutes - apptStartMinutes < ReceiveDeadlineHours * 60) continue;

                // Force out any live truck still sitting on this PO before cancelling — ForceDeparture
                // internally marks the shipment Departed, which we immediately override to Cancelled
                // below so the PO doesn't read as fulfilled.
                var truck = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t != null && t.AssignedShipment == po);
                truck?.ForceDeparture();

                po.Status = ShipmentData.ShipmentStatus.Cancelled;
                appt.ClosedOut = true;
                appt.WasLate = true;

                if (ServiceLocator.TryGet(out VendorEconomyService economy) && economy != null &&
                    !string.IsNullOrEmpty(po.SupplierId))
                {
                    economy.AdjustPartnershipLevel(po.SupplierId, -ReceiveDeadlineRelationshipPenalty,
                        $"PO {po.PONumber} expired at the dock — {ReceiveDeadlineHours} hours passed with no full receipt");
                }

                UIToast.Show($"PO {po.PONumber} expired — {ReceiveDeadlineHours} hours passed with no full " +
                             $"receipt. Lost the load (${po.TotalCost:N0}, no refund) and took a " +
                             $"-{ReceiveDeadlineRelationshipPenalty} vendor hit.");
                SystemsLogWindow.LogWarning($"PO {po.PONumber} expired at the dock — {ReceiveDeadlineHours} " +
                                             $"hours passed with no full receipt. Order lost, no refund — " +
                                             $"that's ${po.TotalCost:N0} down the drain and a vendor hit of " +
                                             $"-{ReceiveDeadlineRelationshipPenalty}.");
            }
        }

        /// <summary>
        /// Judges every appointment whose booked block has fully elapsed, once per hour tick.
        ///
        /// THIS IS WHERE A RECURRING ORDER'S DEADLINE IS ENFORCED. A standing account's promise is a
        /// two-hour window, not a day, so the fine can't wait for the midnight roll-up in
        /// OrderService.OnDayChanged — by then the customer has been kept waiting most of a day and
        /// the window it was measured against is long gone. Any order still on the trailer when its
        /// block runs out has missed its deadline and is charged here: a one-time late fee at the
        /// CONTRACT'S OWN advertised rate (OrderService.FineMissedDeadline, sharing OrderData.
        /// HasBeenFined with the day-roll sweep so one miss can never be billed twice) plus a
        /// customer-satisfaction hit, exactly the two penalties a late bulk order takes.
        ///
        /// The fine is charged whether or not loading ever started — a trailer half-loaded at the end
        /// of its window is just as late as one nobody touched. What loading DOES decide is what
        /// happens to the appointment itself:
        ///
        ///   NEVER STARTED   the door slot is cancelled outright (same Cancel a player's own "return to
        ///                   pool" click uses — see ContractsPanel.OnReturnAppointmentToPoolClicked) so
        ///                   the freight falls back into the unscheduled pool on the next rebuild. This
        ///                   is Tad's "the trailer showed up and we didn't load it up because it wasn't
        ///                   ready or we didn't have the employees" case.
        ///   ALREADY LOADING pulling the appointment out from under a Dock Stocker mid-load would desync
        ///                   TrailerLoadController, which is still driving a coroutine against these
        ///                   exact pallets/orders — so the appointment is left alone and the load is
        ///                   allowed to finish. It's already been fined; letting it finish is not
        ///                   forgiveness, it's just not corrupting a running coroutine to make a point.
        ///
        /// An EMPTY elapsed appointment — a pre-booking from MaintainRecurringSchedule whose order
        /// never arrived, or whose orders have all shipped — is simply removed. Nothing to fine and
        /// nobody kept waiting; this is also what keeps stale pre-bookings from accumulating.
        ///
        /// Inbound appointments are never judged here — they're ShipmentService's note that a PO's
        /// truck is at a door, not a promise this service made to anyone.
        /// </summary>
        private void SweepElapsedAppointments()
        {
            if (!ServiceLocator.TryGet(out OrderService orderService) || orderService == null) return;
            ServiceLocator.TryGet(out OrderArrivalService arrivals);

            int today = CurrentDay;
            int nowBlock = CurrentBlock;

            // Inbound PO reservations whose day has fully passed are dropped rather than judged. There
            // is no customer to disappoint and no order to fine — the freight either turned up (in
            // which case ShipmentService has long since taken the appointment down) or the PO was
            // cancelled. Without this a missed PO box would sit in the unscheduled pool forever.
            // BookInboundNow's live-truck notes are still exempt entirely; they aren't reservations.
            _appointments.RemoveAll(a => a.Kind == AppointmentKind.Inbound
                                      && !string.IsNullOrEmpty(a.ShipmentPoNumber)
                                      && a.Day < today);

            var elapsed = _appointments
                .Where(a => (a.Kind != AppointmentKind.Inbound || !string.IsNullOrEmpty(a.ShipmentPoNumber)) &&
                            (a.Day < today || (a.Day == today && a.BlockIndex < nowBlock)))
                .ToList();

            foreach (var appt in elapsed)
            {
                if (appt.Kind == AppointmentKind.Inbound)
                {
                    JudgeElapsedInboundAppointment(appt);
                    continue;
                }

                var apptOrders = orderService.ActiveOrders
                    .Where(o => o != null && appt.OrderIds.Contains(o.OrderId))
                    .ToList();

                // Everything still on this trailer missed the window it was promised. Shipped orders
                // have already been archived out of ActiveOrders, so anything left here by definition
                // did not go out in time.
                foreach (var order in apptOrders)
                {
                    if (order.Status == OrderData.OrderStatus.Cancelled) continue;
                    if (!orderService.FineMissedDeadline(order, appt.EndHour)) continue;

                    // Satisfaction is docked once per order fined, alongside the money. Missing the
                    // slot the customer was promised is precisely the case Tad asked satisfaction to
                    // react to — it was a standing TODO on this method until the deadline became a
                    // block rather than a day and gave it a definite moment to fire on.
                    arrivals?.PenalizeSatisfaction(order.ContractId);

                    // Same record JudgeElapsedInboundAppointment writes for the vendor side, just in
                    // dollars instead of relationship points — what the "LATE" corner badge and its
                    // tooltip key off. Summed rather than overwritten: several orders can ride one
                    // trailer and each is fined independently.
                    appt.WasLate = true;
                    appt.LateFineAmount += order.LastFineAmount;
                }

                bool startedLoading = apptOrders.Any(o =>
                    o.Status == OrderData.OrderStatus.Loading ||
                    o.Status == OrderData.OrderStatus.Loaded ||
                    o.Status == OrderData.OrderStatus.Shipped);

                if (!startedLoading)
                {
                    appt.ClosedOut = true;
                    Debug.LogWarning($"[DockSchedule] {appt.CustomerName}'s {BlockLabel(appt.BlockIndex)} slot on " +
                                     $"day {appt.Day} elapsed with nothing loaded — door freed for rebooking, kept " +
                                     "on the grid struck through as missed.");
                }
            }
        }

        /// <summary>
        /// The inbound-PO half of SweepElapsedAppointments — split out because its judgment is by
        /// SHIPMENT STATUS rather than order status, and because "whose fault was it" actually matters
        /// here in a way it doesn't for outbound (a customer order fined for lateness is late
        /// regardless of why; an inbound PO's block can elapse for a reason that's on the vendor, or
        /// one that's on the player, and only one of those should cost the vendor relationship
        /// anything):
        ///
        ///   InTransit   the truck never turned up at all inside its promised window — that's on the
        ///               VENDOR, not the player, and is judged exactly like ShipmentService.
        ///               HandleNoAvailableDoor's "driver turned around" case (same -20 Partnership
        ///               penalty, same toast style) since both are "this delivery didn't happen when
        ///               it was supposed to, through no fault of the receiving dock". The appointment
        ///               is released — same direct removal the outbound branch above uses, not
        ///               TryPark, since TryPark's own IsLocked check refuses to park anything whose
        ///               block has already passed.
        ///   Receiving   the truck DID arrive and IS being worked — the player's own dock just didn't
        ///               finish in time. Left alone entirely, mirroring the outbound branch's
        ///               "already loading" exception: pulling the door out from under a live unload
        ///               would desync whatever's mid-coroutine against it, and it isn't the vendor's
        ///               fault regardless.
        ///   Received/Departed/Cancelled/Delayed/not found   already resolved one way or another
        ///               (IsComplete would already call these done, or ShipmentService's own retry/
        ///               delay handling owns them) — just release the stale slot, no extra penalty.
        /// </summary>
        private void JudgeElapsedInboundAppointment(DockAppointment appt)
        {
            if (!ServiceLocator.TryGet(out ShipmentService shipments) || shipments == null)
            {
                appt.ClosedOut = true;
                return;
            }

            ShipmentData po = null;
            foreach (var s in shipments.PendingShipments)
                if (s != null && s.PONumber == appt.ShipmentPoNumber) { po = s; break; }

            // Truck is physically at the door working right now — let it finish, don't touch the slot.
            if (po != null && po.Status == ShipmentData.ShipmentStatus.Receiving)
                return;

            if (po != null && po.Status == ShipmentData.ShipmentStatus.InTransit &&
                ServiceLocator.TryGet(out VendorEconomyService economy) && economy != null &&
                !string.IsNullOrEmpty(po.SupplierId))
            {
                economy.AdjustPartnershipLevel(po.SupplierId, -InboundLateRelationshipPenalty,
                    $"PO {po.PONumber}'s {BlockLabel(appt.BlockIndex)} slot on day {appt.Day} elapsed — driver never showed");
                UIToast.Show($"PO {po.PONumber} never showed up for its {BlockLabel(appt.BlockIndex)} slot — " +
                             "releasing the door and dinging the vendor relationship.");

                // Reputation-only per Tad's explicit call — no dollar fine exists for a vendor's own
                // late delivery. WasLate/LateRelationshipPenalty are what the "LATE" corner badge and
                // tooltip key off, surviving the ClosedOut reset above so the player is still told this
                // PO already burned the relationship once, even after it's re-placed on a future slot.
                appt.WasLate = true;
                appt.LateRelationshipPenalty = InboundLateRelationshipPenalty;
            }

            appt.ClosedOut = true;
            Debug.LogWarning($"[DockSchedule] Door {appt.DoorNumber}'s {BlockLabel(appt.BlockIndex)} slot on day " +
                             $"{appt.Day} elapsed for PO {appt.ShipmentPoNumber} (status={po?.Status.ToString() ?? "not found"}) — " +
                             "door freed for rebooking, kept on the grid (IsComplete decides finished vs missed).");
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
            orderIds = new List<string>(a.OrderIds),
            parked = a.Parked,
            shipmentPoNumber = a.ShipmentPoNumber,
            offSlotPenaltyApplied = a.OffSlotPenaltyApplied,
            closedOut = a.ClosedOut,
            wasLate = a.WasLate,
            lateFineAmount = a.LateFineAmount,
            lateRelationshipPenalty = a.LateRelationshipPenalty
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
                    OrderIds = s.orderIds ?? new List<string>(),
                    Parked = s.parked,
                    ShipmentPoNumber = s.shipmentPoNumber,
                    RequestedDay = s.requestedDay,
                    OffSlotPenaltyApplied = s.offSlotPenaltyApplied,
                    ClosedOut = s.closedOut,
                    WasLate = s.wasLate,
                    LateFineAmount = s.lateFineAmount,
                    LateRelationshipPenalty = s.lateRelationshipPenalty
                });
            }

            if (_appointments.Count > 0)
                Debug.Log($"[DockSchedule] Restored {_appointments.Count} dock appointment(s).");
        }
    }
}
