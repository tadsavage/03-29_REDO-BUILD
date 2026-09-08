using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Economy;   // SimulationTimeService lives here, not in GameCore.Services
using GameCore.Events;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>
    /// Turns signed contracts into arriving customer orders — the thing that replaces the Dev
    /// Console's "Create Test Order" button in a real build.
    ///
    /// LIVE. Registered and initialized last in GameContext.Awake() (it resolves the other services
    /// out of the locator), and Export/Import are called from PlacementSystem's save path. Both
    /// prerequisites listed at the bottom of this comment have been met.
    ///
    /// DESIGN
    ///
    /// Hooks OnHourChanged rather than OnDayChanged so orders can land at a contract's cutoff HOUR.
    /// Orders appearing at midnight give the player no deadline to feel; orders landing at 17:00 for
    /// delivery two days out create the daily rhythm the loop design asks for. OnDayChanged is still
    /// the right hook for E1's fine sweep — that genuinely is a midnight roll-up.
    ///
    /// Arrival does NOT need any downstream change: OrderService.ReceiveOrder already files each
    /// order's WorkTask as Open, awaiting the player's release through the Work Queue. Orders simply
    /// start appearing there on their own.
    ///
    /// The Dev Console button stays useful and should NOT be removed — it remains the only way to
    /// force a specific test batch on demand.
    ///
    /// TWO PREREQUISITES, both now met — recorded because both are load-bearing and easy to undo:
    ///
    /// 1. TERMINAL ORDERS ARE RETIRED. OrderService.Archive moves Shipped/Cancelled orders out of
    ///    _activeOrders the instant they go terminal. Before that they accumulated forever (67 in one
    ///    observed session, nearly all finished) and every one was re-scanned by the Work Queue four
    ///    times a second. Cosmetic while orders arrived by hand; compounding once they arrive daily.
    ///
    /// 2. ARRIVAL STATE PERSISTS. Export()/Import() are called from PlacementSystem alongside
    ///    orderService.Import(save.orders). Without it SignedContract.LastGeneratedDay resets and a
    ///    save/load either duplicates a day's orders or silently skips one.
    /// </summary>
    public class OrderArrivalService : IService
    {
        /// <summary>How long a contract lost to a missed pickup stays off the board. Flat and simple
        /// on purpose — reputation, length of service and demand are meant to drive this eventually,
        /// and a tunable curve now would just be a placeholder pretending to be a system.</summary>
        public const int ContractLossCooldownDays = 30;

        /// <summary>Bulk offers rolled onto the board each day.</summary>
        public const int BulkOffersPerDayMin = 1;
        public const int BulkOffersPerDayMax = 3;

        // ── Business hours for new customer requests ─────────────────────────
        //
        // Nobody phones in a bulk order at 03:00. Offers used to roll at the midnight day-change,
        // which meant a full board of same-day rushes was waiting the moment the day flipped —
        // deadlines already burning through the night shift, and the player's first act each morning
        // was triage on work that arrived while nothing was open.
        //
        // Now the board clears at midnight and REFILLS during the working day, once, at a random hour
        // inside this window. A same-day bulk rush is still possible (that was the point of it), but
        // it arrives with a working day in front of it rather than behind it.

        /// <summary>Longest gap between deliveries any OrderFrequency can express — Weekly's 7 days.
        /// Bounds the forward scan in TryGetNextArrival. Deliberately its own constant rather than
        /// reusing ScheduleHorizonDays, which is also 7 but means something unrelated (how far ahead
        /// trailers are pre-booked); tying the scan to that would make lowering the horizon quietly
        /// break weekly accounts' "next drop" display.</summary>
        private const int LongestCadenceDays = 7;

        /// <summary>Earliest hour a new customer request can land. Inclusive.</summary>
        public const int OfferWindowOpenHour = 8;

        /// <summary>Latest hour a new customer request can land. Inclusive — an offer may arrive AT
        /// 18:00, never after it.</summary>
        public const int OfferWindowCloseHour = 18;

        /// <summary>
        /// How far ahead the Schedule tab is kept populated with recurring accounts' trailers.
        ///
        /// A standing account's appointments are a KNOWN QUANTITY — the contract already says which
        /// days it delivers and at what hour — so making the player discover each one the morning it
        /// lands is busywork, and worse, it hides the thing the schedule exists to show: whether next
        /// Thursday is already full before you sign a second account into it. A week out is enough to
        /// see a collision coming and still short enough that the grid is readable.
        ///
        /// Bulk deliberately gets NO pre-booking. A bulk order doesn't exist until the player accepts
        /// it, so there's nothing to book ahead — placing it is the decision.
        /// </summary>
        public const int ScheduleHorizonDays = 7;

        /// <summary>
        /// How far ahead of its scheduled slot a recurring order's real composition (SKUs and
        /// quantities) is rolled and exposed to the player — Tad's ask: "all orders in the system,
        /// whether they're for another day or not, should have a quantity if it's expected to
        /// arrive within 48 hours or less."
        ///
        /// The order still carries the SAME DueDay it always would have (the scheduled slot's day),
        /// it's just materialized into a real OrderData up to this many hours early instead of
        /// exactly at the contract's cutoff hour on the day itself — so the Accounts tab and its
        /// ORDER DETAILS pane have real cases/pallets to show, not a placeholder, the moment a
        /// player is within planning range of it.
        /// </summary>
        public const int GenerateAheadHours = 48;

        private readonly List<SignedContract> _signed = new();
        private readonly List<ContractData> _catalog = new();

        /// <summary>
        /// Day each RUNTIME-generated offer was rolled, keyed by ContractId.
        ///
        /// Two jobs. It's the "is this offer stale?" clock — the daily roll clears bulk offers the
        /// player didn't take — and it's the marker for which catalog entries came from
        /// ContractData.CreateRuntime rather than from an authored asset, which is what Export has to
        /// write out (an authored offer needs no snapshot; it's still in the ContractRegistry on load).
        ///
        /// Kept beside the catalog rather than as a field on ContractData because a ScriptableObject
        /// created at runtime is the wrong place for bookkeeping about the board it sits on.
        /// </summary>
        private readonly Dictionary<string, int> _generatedOfferDay = new();

        private EventManager _eventManager;
        private OrderService _orderService;
        private InventoryService _inventoryService;
        private SimulationTimeService _timeService;
        private DockScheduleService _dockSchedule;
        private CustomerRegistry _customers;

        /// <summary>Every contract the player has signed and not cancelled.</summary>
        public IReadOnlyList<SignedContract> Signed => _signed;

        /// <summary>Contract offers available to sign, in registry order.</summary>
        public IReadOnlyList<ContractData> Catalog => _catalog;

        /// <summary>
        /// Offers the player can actually take right now — what the New Contracts tab lists.
        ///
        /// Excludes anything currently signed, and anything inside its post-loss cooldown. The
        /// cooldown check deliberately looks at the signed record even though loss clears Active:
        /// without it, an account lost for missing its pickup would be back on the board the very
        /// next frame, which is the opposite of a consequence.
        /// </summary>
        public IEnumerable<ContractData> AvailableOffers =>
            _catalog.Where(c => !_signed.Any(s => s.ContractId == c.ContractId && s.Active)
                             && !IsInLossCooldown(c.ContractId)
                             && CurrentReputation >= c.ReputationRequired);

        /// <summary>
        /// The player's standing, or 0 if the reputation service isn't running.
        ///
        /// Resolved lazily rather than cached at Initialize: OrderArrivalService is constructed
        /// before ReputationService in GameContext, and a field captured at init would be null
        /// forever — which would silently read as reputation 0 and lock every gated customer out of
        /// the game permanently.
        /// </summary>
        private int CurrentReputation
        {
            get
            {
                if (_reputation == null) ServiceLocator.TryGet(out _reputation);
                return _reputation?.Score ?? 0;
            }
        }
        private ReputationService _reputation;

        /// <summary>
        /// Customers who would deal with you if you were better regarded — shown greyed on the
        /// Customers tab rather than hidden, for the same reason locked vendors are: a door you can
        /// see is a goal, a door you can't is just a smaller game.
        /// </summary>
        public IEnumerable<ContractData> ReputationLockedOffers =>
            _catalog.Where(c => !_signed.Any(s => s.ContractId == c.ContractId && s.Active)
                             && !IsInLossCooldown(c.ContractId)
                             && CurrentReputation < c.ReputationRequired);

        /// <summary>True while this contract is barred after being lost to a missed pickup. Reads the
        /// most recent record for the id, since a contract can be signed, lost, re-taken and lost
        /// again over a long game.</summary>
        public bool IsInLossCooldown(string contractId)
        {
            var lost = _signed.LastOrDefault(s => s.ContractId == contractId && s.Lost);
            if (lost == null) return false;
            int today = _timeService?.Day ?? 0;
            return today - lost.LostOnDay < ContractLossCooldownDays;
        }

        /// <summary>Days left before a lost contract can be signed again, or 0 if it isn't barred.
        /// The New Contracts tab shows this so a missing customer is explained rather than just
        /// absent.</summary>
        public int LossCooldownDaysRemaining(string contractId)
        {
            var lost = _signed.LastOrDefault(s => s.ContractId == contractId && s.Lost);
            if (lost == null) return 0;
            int today = _timeService?.Day ?? 0;
            return Mathf.Max(0, ContractLossCooldownDays - (today - lost.LostOnDay));
        }

        /// <summary>Contracts barred right now, for the panel to list as "lost — back in N days".</summary>
        public IEnumerable<ContractData> LostOffers =>
            _catalog.Where(c => IsInLossCooldown(c.ContractId));

        public bool IsSigned(string contractId) =>
            _signed.Any(s => s.ContractId == contractId && s.Active);

        /// <summary>The signed record for a contract, or null if it was never taken.</summary>
        public SignedContract GetSigned(string contractId) =>
            _signed.FirstOrDefault(s => s.ContractId == contractId && s.Active);

        /// <summary>The catalog asset behind a signed record.</summary>
        public ContractData GetContract(string contractId) =>
            _catalog.FirstOrDefault(c => c.ContractId == contractId);

        /// <summary>
        /// Standing accounts actually sending work — signed, active, and NOT a spent one-off.
        ///
        /// This distinction is the whole reason the method exists. A delivered one-off stays Active
        /// forever by design (that's what keeps the Sign button off its card), so a plain
        /// Signed.Count(s =&gt; s.Active) counts it as a running account. The panel footer did exactly
        /// that and reported three accounts running when two were.
        ///
        /// Keyed on IsBulk (which also covers the retired wholesale ordinal, see ContractData) rather
        /// than IsOneTime alone: the legacy wholesale asset predates Frequency and still carries its
        /// Daily default, so IsOneTime alone wouldn't exclude it — IsBulk does, unconditionally.
        /// </summary>
        public IEnumerable<SignedContract> RunningAccounts => _signed.Where(s =>
        {
            if (!s.Active) return false;
            var c = GetContract(s.ContractId);
            return c != null && !c.IsOneTime && !c.IsBulk;
        });

        /// <summary>Day/hour a recurring contract next drops orders. A contract that has already run
        /// today rolls to tomorrow. Meaningless for a bulk order, which returns false.</summary>
        public bool TryGetNextArrival(string contractId, out int day, out int hour)
        {
            day = 0; hour = 0;
            var contract = GetContract(contractId);
            var signed = GetSigned(contractId);
            if (contract == null || signed == null || contract.IsBulk) return false;

            int today = _timeService?.Day ?? 0;
            hour = contract.CutoffHour;

            // Walks forward through DeliversOn rather than reproducing the cadence rules inline. The
            // old one-liner ("already ran today? then tomorrow") was right for Daily and silently
            // wrong for Weekly — it promised the Accounts tab a drop tomorrow when the real one was
            // six days out. Sharing the predicate means the date shown here, the day the order
            // actually generates, and the day its trailer is pre-booked can't disagree.
            //
            // Bounded by the longest cadence OrderFrequency can express, NOT by ScheduleHorizonDays.
            // They're both 7 today, which is exactly why this is worth being explicit about: bounding
            // the scan by the booking horizon happened to work only because a weekly drop is never
            // more than 7 days out, and lowering the horizon later would silently start reporting
            // "no next arrival" for every weekly account. The two numbers mean different things.
            for (int d = today; d <= today + LongestCadenceDays; d++)
            {
                if (!DeliversOn(contract, signed, d)) continue;
                if (signed.LastGeneratedDay >= d) continue; // that day's drop has already happened
                day = d;
                return true;
            }
            return false;
        }

        /// <summary>Contract offers available to sign. Hand in the authored ContractData assets —
        /// same pattern as CustomerRegistry being handed to OrderGenerator.</summary>
        public void SetCatalog(IEnumerable<ContractData> contracts)
        {
            _catalog.Clear();
            if (contracts != null) _catalog.AddRange(contracts.Where(c => c != null));
        }

        /// <summary>
        /// Puts one more offer on the board. This is the hook contract ARRIVAL will use — offers are
        /// meant to show up over time at a rate driven by reputation and difficulty, rather than the
        /// catalog being a fixed shelf loaded once at startup.
        ///
        /// Rejects a duplicate ContractId rather than shadowing the existing one, because ContractId
        /// is what SignedContract and OrderData both key on — two entries under one id would make
        /// GetContract's answer depend on list order.
        /// </summary>
        public bool AddOffer(ContractData contract)
        {
            if (contract == null) return false;
            if (_catalog.Any(c => c != null && c.ContractId == contract.ContractId)) return false;
            _catalog.Add(contract);
            return true;
        }

        public void Initialize()
        {
            _eventManager = EventManager.Instance;
            if (_eventManager == null)
            {
                Debug.LogError("[OrderArrivalService] EventManager not found.");
                return;
            }

            ServiceLocator.TryGet(out _orderService);
            ServiceLocator.TryGet(out _inventoryService);
            ServiceLocator.TryGet(out _timeService);
            if (_orderService == null || _inventoryService == null || _timeService == null)
            {
                Debug.LogError("[OrderArrivalService] OrderService/InventoryService/SimulationTimeService not found.");
                return;
            }

            // Optional dependencies — neither is fatal. Without the dock schedule the loss sweep can't
            // tell a booked order from a stranded one, so it stands down rather than guessing and
            // wrongly killing every account. Without the roster no bulk offers roll.
            ServiceLocator.TryGet(out _dockSchedule);
            _customers = CustomerRegistry.Load();

            // Self-load the catalog so no caller has to remember to. SetCatalog still exists for
            // tests and for handing in a different list.
            if (_catalog.Count == 0)
            {
                var registry = ContractRegistry.Load();
                if (registry != null) SetCatalog(registry.contracts);
            }

            _eventManager.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
            _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);

            // Detach-then-attach: these are STATIC events, so a service instance leaked by a domain
            // reload would otherwise keep a live handler on a dead _signed list. This doesn't unhook
            // that leaked instance's own handler, but it does stop THIS one double-counting if
            // Initialize is ever called twice.
            OrderService.OnOrderShipped -= HandleOrderShipped;
            OrderService.OnOrderShipped += HandleOrderShipped;
            OrderService.OnOrderFined -= HandleOrderFined;
            OrderService.OnOrderFined += HandleOrderFined;
        }

        /// <summary>
        /// DEV ONLY — resets the contract board to a fresh start: every signed account (running, lost
        /// and delivered alike) is dropped, every runtime-generated bulk offer is discarded, and the
        /// authored offer catalog is reloaded from the ContractRegistry.
        ///
        /// The authored catalog is RELOADED rather than left empty because it isn't state — it's the
        /// shelf offers are drawn from, populated once at Initialize and never rebuilt. Clearing it
        /// outright would leave standing-order offers permanently gone until the Editor restarted,
        /// which is a broken game rather than a clean one.
        ///
        /// Deliberately does NOT touch orders already on the floor. Cancelling one account doesn't
        /// (see Cancel), and freight that's mid-pick has pallets, tasks, staging lanes and a docked
        /// trailer behind it — quietly voiding all that from a contracts wipe would leave the
        /// warehouse holding goods no order claims. Clear those from the Work Queue instead.
        ///
        /// Returns the number of signed records dropped.
        /// </summary>
        public int ClearAllContracts()
        {
            int dropped = _signed.Count;
            _signed.Clear();

            // Only the generated ones — an authored asset removed here would be gone until restart.
            foreach (string id in _generatedOfferDay.Keys.ToList())
                _catalog.RemoveAll(c => c != null && c.ContractId == id);
            _generatedOfferDay.Clear();

            var registry = ContractRegistry.Load();
            if (registry != null) SetCatalog(registry.contracts);

            Debug.LogWarning($"[OrderArrivalService] DEV: contract board wiped — {dropped} signed record(s) " +
                             $"dropped, generated offers discarded, {_catalog.Count} authored offer(s) restored. " +
                             $"Orders already on the floor are untouched.");
            return dropped;
        }

        public void Shutdown()
        {
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            OrderService.OnOrderShipped -= HandleOrderShipped;
            OrderService.OnOrderFined -= HandleOrderFined;
        }

        // ── Performance bookkeeping ──────────────────────────────────────────

        private void HandleOrderShipped(OrderData order)
        {
            var signed = FindSignedFor(order);
            if (signed == null) return;
            signed.OrdersDelivered++;
            // ShippedRevenue, not TotalRevenue: an order that shipped short only earned what actually
            // went on the truck. Reading the property rather than re-summing the line items also keeps
            // the SAME-DAY RUSH double in the account's earned-to-date — hand-summing here was already
            // a second copy of ShipOrder's arithmetic, and the bonus is exactly the kind of change
            // that makes two copies quietly disagree.
            signed.RevenueEarned += order.ShippedRevenue;

            // EVERY ORDER OUT ON TIME NUDGES SATISFACTION BACK UP — Tad's ask, and the half that makes
            // the number a relationship rather than a ratchet. Deliberately much smaller than the
            // penalty (see RewardSatisfaction): a lapse should take many good days to work off, so an
            // account you've been mistreating stays visibly damaged for a while.
            //
            // "On time" is HasBeenFined being clear. That flag is set by whichever deadline actually
            // governed this order — the end of a recurring trailer's booked block, or a bulk order's
            // due day — so this one test is correct for both without knowing which applied.
            if (!order.HasBeenFined) RewardSatisfaction(order.ContractId);
        }

        private void HandleOrderFined(OrderData order, int fine)
        {
            var signed = FindSignedFor(order);
            if (signed == null) return;
            signed.OrdersLate++;
            signed.LateFeesPaid += fine;
        }

        /// <summary>Flat penalty applied each time a trailer for this contract is booked or moved away
        /// from the customer's own requested hour — see DockScheduleService.MissedRequestedSlot, the
        /// sole caller. Small and flat on purpose, same reasoning as ContractLossCooldownDays: a tunable
        /// curve now would be a placeholder pretending to be a system.</summary>
        public const float SatisfactionPenaltyPerMiss = 5f;

        /// <summary>
        /// Satisfaction earned back for one order delivered on time.
        ///
        /// A FIFTH of the penalty, on purpose. Reputation should be slow to rebuild and quick to lose:
        /// at 1 point a shipment it takes five clean deliveries to undo a single missed slot, so a
        /// customer you've let down stays visibly unhappy long enough for the player to feel it, while
        /// a well-run account still drifts back to 100 over a normal week's work rather than being
        /// permanently marked by one bad afternoon.
        /// </summary>
        public const float SatisfactionRewardPerOnTimeOrder = 1f;

        /// <summary>Docks a small, flat amount of customer satisfaction for a signed contract. Silently
        /// no-ops for a contract that isn't (or is no longer) signed — a Dev Console order or a lost
        /// contract has nothing left to penalize.</summary>
        public void PenalizeSatisfaction(string contractId)
            => PenalizeSatisfaction(contractId, SatisfactionPenaltyPerMiss);

        /// <summary>
        /// Same hit at an explicit size, for offences that aren't "you moved my slot".
        ///
        /// Cancelling an order the customer already placed is categorically worse than shifting when
        /// their trailer turns up — one is an inconvenience, the other is a refusal to supply — so it
        /// can't share the per-miss constant. See CancelledOrderSatisfactionPenalty.
        /// </summary>
        public void PenalizeSatisfaction(string contractId, float amount)
        {
            var signed = GetSigned(contractId);
            if (signed == null) return;
            signed.SatisfactionPercent = Mathf.Max(0f, signed.SatisfactionPercent - Mathf.Abs(amount));
        }

        /// <summary>Satisfaction lost when the player cancels an order outright. Deliberately several
        /// times SatisfactionPenaltyPerMiss: refusing to supply is not a scheduling inconvenience.
        /// Placeholder magnitude, like the rest of the balance numbers.</summary>
        public const float CancelledOrderSatisfactionPenalty = 25f;

        /// <summary>Nudges customer satisfaction back up for an order that made its deadline. Capped at
        /// 100 — a perfect account can't bank credit against future lateness, which would let a player
        /// buy their way out of a bad week with a good one. Same silent no-op as PenalizeSatisfaction
        /// for an order with no live contract behind it.</summary>
        public void RewardSatisfaction(string contractId)
        {
            var signed = GetSigned(contractId);
            if (signed == null) return;
            signed.SatisfactionPercent = Mathf.Min(100f, signed.SatisfactionPercent + SatisfactionRewardPerOnTimeOrder);
        }

        /// <summary>Resolves a finished order back to the contract that produced it. Matches on the
        /// stamped ContractId only — never falls back to CustomerId, because one customer may hold
        /// several contracts and a wrong attribution is worse than none. Dev Console orders carry no
        /// ContractId and are correctly credited to nothing.</summary>
        private SignedContract FindSignedFor(OrderData order)
            => order == null || string.IsNullOrEmpty(order.ContractId)
             ? null
             : _signed.FirstOrDefault(s => s.ContractId == order.ContractId);

        // ── Signing ──────────────────────────────────────────────────────────

        public bool Sign(string contractId)
        {
            if (_signed.Any(s => s.ContractId == contractId && s.Active)) return false;
            // A contract inside its post-loss cooldown is off the board in AvailableOffers, but the
            // check belongs here too: the panel can rebuild from a stale list, and this is the call
            // that actually creates the account.
            if (IsInLossCooldown(contractId))
            {
                Debug.Log($"[OrderArrivalService] Refused to sign '{contractId}' — lost, " +
                          $"{LossCooldownDaysRemaining(contractId)} day(s) of cooldown left.");
                return false;
            }
            var contract = _catalog.FirstOrDefault(c => c.ContractId == contractId);
            if (contract == null) return false;

            int signDay = _timeService?.Day ?? 0;
            var signed = new SignedContract
            {
                ContractId = contractId,
                SignedOnDay = signDay,
                // A RECURRING ACCOUNT NEVER DELIVERS ON THE DAY IT WAS SIGNED. Stamping the signing
                // day as already-generated is what enforces it: OnHourChanged skips any contract whose
                // LastGeneratedDay is >= today, so the first orders can't land before tomorrow, and
                // DeliversOn applies the same rule to the pre-booked schedule.
                //
                // The old behaviour let a contract signed before its cutoff hour deliver immediately.
                // That is fine on paper and awful in practice: sign at 22:00 with a 16:00–18:00 slot
                // and the account opens with freight that is already hours late through no fault of
                // the player. A new customer's first pickup is tomorrow — same as a real account
                // being set up.
                LastGeneratedDay = signDay
            };
            _signed.Add(signed);

            // Signing IS the delivery commitment — the order lands immediately rather than waiting
            // for a cutoff hour. Stays ACTIVE deliberately, even though it will never generate again:
            // Active means "this contract is taken" — it's what IsSigned reports, which is both what
            // greys the card out and what stops a second signing. Clearing it here (the original
            // mistake, back when this was a separate wholesale-only path) made a delivered one-off
            // read as never-signed: the card kept its Sign button and every click produced another
            // full trailer. What stops it re-firing is the IsOneTime/IsBulk skip in OnHourChanged,
            // not this flag.
            if (contract.IsBulk)
            {
                int today = _timeService?.Day ?? 0;
                signed.LastGeneratedDay = today;
                GenerateBulk(contract, today);
                return true;
            }

            // Fills in this account's whole week of trailers immediately, not just its first. Without
            // any pre-booking a recurring account's slot didn't appear on the Schedule tab until its
            // first order actually generated at the contract's cutoff hour — which could be a full day
            // away (see the "never back-fill" comment above) and read as broken to a player who'd just
            // signed. Booking only the first fixed that one case and still left the rest of the week
            // blank, which is the half that actually matters: the player signs a second account
            // against a grid that looks empty and only discovers the collision a week later.
            MaintainRecurringSchedule();

            // Generate the FIRST delivery's real order/line items synchronously, right now — not on
            // whatever OnHourChanged tick happens to fire next. Per Tad: the player needs to know what
            // a brand-new account ordered immediately so shortages can be covered before that trailer's
            // appointment block even opens. This only moves the ORDER DATA earlier; the appointment
            // itself (booked just above by MaintainRecurringSchedule) still targets the correct first
            // delivery day untouched — tomorrow for Daily, a week out for Weekly (see DeliversOn) — so
            // the account still never delivers on its own signing day.
            //
            // Same "generate a future day's order before that day arrives" pattern OnHourChanged's own
            // horizon sweep already relies on (see its doc comment) — this just guarantees day one of
            // it happens at sign time instead of waiting for the next hour tick, rather than being a new
            // kind of early generation.
            for (int day = signDay + 1; day <= signDay + ScheduleHorizonDays; day++)
            {
                if (!DeliversOn(contract, signed, day)) continue;
                signed.LastGeneratedDay = day;
                GenerateFor(contract, day);
                break; // only day one jumps the queue; the rest keep following the normal cadence
            }

            Debug.Log($"[OrderArrivalService] Signed {contractId} ({contract.Customer?.CompanyName}) — " +
                      $"{contract.FrequencyLabel}, ~{contract.EstimatedCasesPerDay} cases/day, " +
                      $"ships in the {DockScheduleService.BlockLabel(DockScheduleService.BlockForHour(contract.CutoffHour))} slot.");
            return true;
        }

        /// <summary>
        /// Keeps every running account's trailers booked <see cref="ScheduleHorizonDays"/> days ahead.
        ///
        /// THE DEADLINE FOR A RECURRING ORDER IS THE END OF ITS BOOKED BLOCK, not a day — which only
        /// works if the block genuinely exists before the order does. That's this method's real job:
        /// it makes the appointment the promise, so an order can be judged against a two-hour window
        /// the moment it lands (DockScheduleService.SweepElapsedAppointments) instead of waiting for a
        /// midnight roll-up that would be far too coarse to mean anything.
        ///
        /// Books EMPTY appointments. When the order arrives, HandleOrderArrived's "join a trailer the
        /// player has already booked" branch finds the one for that customer+contract and attaches the
        /// order to it — no double-booking, and no difference between a slot booked here and one the
        /// player placed by hand. The player can still move it, subject to the recurring move rule
        /// (earlier, same day only — see DockScheduleService.TryMoveToDoor).
        ///
        /// Idempotent, and cheap enough to call on every day roll and every signing: it skips any day
        /// that already has an appointment for that contract, so re-running it books nothing new.
        /// Stale bookings need no cleanup here — an elapsed appointment with nothing loaded is
        /// released by SweepElapsedAppointments, and cancelling an account stops it being extended.
        /// </summary>
        public void MaintainRecurringSchedule()
        {
            if (_dockSchedule == null || _timeService == null) return;
            if (_dockSchedule.CapacityPerBlock <= 0) return; // no outbound door yet — nothing to book against

            int today = _timeService.Day;

            foreach (var signed in _signed.ToList())
            {
                if (!signed.Active) continue;
                var contract = GetContract(signed.ContractId);
                if (contract == null || contract.Customer == null) continue;
                // One-offs and bulk have nothing recurring to project forward.
                if (contract.IsBulk || contract.IsOneTime) continue;

                for (int day = today; day <= today + ScheduleHorizonDays; day++)
                {
                    if (!DeliversOn(contract, signed, day)) continue;
                    if (_dockSchedule.HasAppointmentFor(signed.ContractId, day)) continue;

                    // lastDay == day: a recurring trailer that can't fit on its OWN day is not booked
                    // at all rather than pushed to tomorrow. Its deadline is a block on this day, so a
                    // slot on any other day isn't a late booking — it's a booking for the wrong order.
                    // The freight then shows up in the unscheduled pool where the player can see the
                    // dock is over-committed, which is the honest outcome.
                    //
                    // A full day is skipped, NOT treated as the end of the horizon: today being
                    // over-committed says nothing about next Tuesday, and giving up on the first
                    // failure would leave the rest of the week blank for exactly the account whose
                    // schedule the player most needs to see.
                    _dockSchedule.TryAutoPlace(day, contract.CutoffHour, day, AppointmentKind.Outbound,
                        contract.Customer.CustomerId, contract.Customer.CompanyName,
                        signed.ContractId, out _);
                }
            }
        }

        /// <summary>
        /// Does this contract send work on the given day? Daily is every day after the signing day;
        /// Weekly repeats a week at a time from it.
        ///
        /// NEVER THE SIGNING DAY ITSELF — the strict `day &lt;= SignedOnDay` test, not `&lt;`. A new
        /// account's first pickup is tomorrow at the earliest, so signing at 22:00 can't open the
        /// relationship with freight that's already missed its window. This is the schedule-side half
        /// of the rule; the arrival-side half is Sign() stamping LastGeneratedDay with the signing day.
        /// Both are needed: one stops the pre-booking, the other stops the order.
        ///
        /// Anchored on SignedOnDay rather than on an absolute calendar so a weekly account signed on
        /// day 3 delivers on 10, 17, 24 — a week from the handshake, which is the thing the player
        /// agreed to — instead of snapping to some global week boundary they never chose. Deliberately
        /// NOT anchored on LastGeneratedDay any more: that field now always holds the signing day right
        /// after signing, so using it would have re-derived the same anchor by a longer route while
        /// quietly shifting the whole series every time an order generated.
        /// </summary>
        private static bool DeliversOn(ContractData contract, SignedContract signed, int day)
        {
            if (day <= signed.SignedOnDay) return false;
            if (contract.Frequency != OrderFrequency.Weekly) return true;

            return (day - signed.SignedOnDay) % 7 == 0;
        }

        /// <summary>
        /// Ends a contract because the player never booked a door for its freight. Distinct from
        /// <see cref="Cancel"/>, which is the player's own decision and carries no cooldown.
        ///
        /// Returns the customer name for the toast, or null if there was nothing to lose — a
        /// hand-made Dev Console order carries no ContractId, and an order can outlive a contract
        /// the player already cancelled.
        /// </summary>
        public string LoseContract(string contractId, int day)
        {
            if (string.IsNullOrEmpty(contractId)) return null;

            var signed = _signed.FirstOrDefault(s => s.ContractId == contractId && s.Active);
            if (signed == null) return null;

            signed.Active = false;
            signed.Lost = true;
            signed.LostOnDay = day;

            var contract = GetContract(contractId);
            string who = contract?.Customer?.CompanyName ?? contractId;
            Debug.Log($"[OrderArrivalService] LOST {contractId} ({who}) on day {day} — freight was never " +
                      $"booked a door. Barred for {ContractLossCooldownDays} day(s).");
            return who;
        }

        public bool Cancel(string contractId)
        {
            var s = _signed.FirstOrDefault(x => x.ContractId == contractId && x.Active);
            if (s == null) return false;
            s.Active = false;
            return true;
        }

        // ── Arrival ──────────────────────────────────────────────────────────

        private void OnHourChanged(string eventId, int newHour)
        {
            if (_orderService == null || _timeService == null) return;
            int today = _timeService.Day;

            TryRollOffersThisHour(today, newHour);

            foreach (var signed in _signed)
            {
                if (!signed.Active) continue;

                var contract = _catalog.FirstOrDefault(c => c.ContractId == signed.ContractId);
                if (contract == null)
                {
                    Debug.LogWarning($"[OrderArrivalService] Signed contract '{signed.ContractId}' has no " +
                                     $"matching asset in the catalog — skipping. (Asset renamed or removed?)");
                    continue;
                }
                // A one-off delivered everything it owed at signing. It stays in _signed (and so stays
                // marked taken in the modal) but must never produce a second trailer. IsBulk is still
                // tested alongside IsOneTime because the authored legacy wholesale asset (now folded
                // into IsBulk) predates Frequency and carries its Daily default — dropping this check
                // would restart it, firing a daily order forever.
                if (contract.IsOneTime || contract.IsBulk) continue;

                // Build the complete planning horizon, not only the very next 48-hour slot. The Schedule
                // tab already reserves one recurring trailer per delivery day through ScheduleHorizonDays,
                // and players need the real item quantities for every one of those pending shipments in
                // order to pick them ahead of schedule. Generating every not-yet-generated delivery slot
                // here makes the order manifest and its pre-booked trailer a one-to-one plan.
                //
                // `LastGeneratedDay` is safe as the single watermark because we always walk forward.
                // Daily contracts generate consecutive days; weekly contracts find their single matching
                // day in the same horizon. Skip today's slot after its requested cutoff—if it was not
                // generated while current, it is a missed delivery rather than work that should appear
                // retroactively on the planning board.
                for (int scheduledDay = today; scheduledDay <= today + ScheduleHorizonDays; scheduledDay++)
                {
                    if (!DeliversOn(contract, signed, scheduledDay)) continue;
                    if (signed.LastGeneratedDay >= scheduledDay) continue;
                    if (scheduledDay == today && newHour > contract.CutoffHour) continue;

                    signed.LastGeneratedDay = scheduledDay;
                    GenerateFor(contract, scheduledDay);
                }
            }
        }

        // ── Day roll: refresh the bulk board, then judge missed pickups ──────

        private void OnDayChanged(string eventId, int newDay)
        {
            // Order matters. The loss sweep judges YESTERDAY's failures, so it runs against the board
            // as it stood; anything that touches the offer list first would be harmless today but only
            // by accident.
            SweepMissedPickups(newDay);

            // Midnight only CLEARS the board — the day's fresh offers now arrive during business hours
            // (TryRollOffersThisHour), so the New Contracts tab is genuinely empty overnight and fills
            // as the working day goes on.
            ExpireStaleBulkOffers(newDay);
            PickTodaysOfferHour(newDay);

            // Extends the booked week by one more day, so the horizon stays ScheduleHorizonDays out
            // rather than draining away as days pass. Runs LAST: an account lost by the sweep above
            // is no longer Active and must not get tomorrow's trailer booked for it.
            MaintainRecurringSchedule();
        }

        /// <summary>Day <see cref="_offerHourToday"/> was rolled for. -1 = not picked yet this
        /// session, which is also the state after a load — see PickTodaysOfferHour.</summary>
        private int _offerHourDay = -1;
        private int _offerHourToday = OfferWindowOpenHour;

        /// <summary>
        /// Chooses the hour inside the business-hours window at which today's customer requests come
        /// in. One hour per day rather than one per offer: a handful of calls arriving together reads
        /// as "the morning's post", where scattering them across the day would have the board quietly
        /// growing behind the player's back while they're looking at it.
        ///
        /// Not persisted. On a load, PickTodaysOfferHour hasn't run for the restored day, so it's
        /// re-rolled — which is why TryRollOffersThisHour tests `hour &gt;= _offerHourToday` rather than
        /// equality, and why it also checks whether today's offers already exist. Between them, a save
        /// loaded at 16:00 whose offers already arrived gets no second batch, and one loaded at 16:00
        /// that re-rolls a 10:00 slot catches up immediately instead of silently skipping the day.
        /// </summary>
        private void PickTodaysOfferHour(int day)
        {
            _offerHourDay = day;
            _offerHourToday = new System.Random(day).Next(OfferWindowOpenHour, OfferWindowCloseHour + 1);
        }

        /// <summary>
        /// Rolls the day's bulk offers if the working day has reached the hour they're due and they
        /// haven't already arrived.
        ///
        /// "Already arrived" is derived from _generatedOfferDay rather than tracked in a flag of its
        /// own, so it survives a save/load for free — an offer rolled today is still stamped today
        /// after a reload, and no extra save field has to be kept in step. A day that legitimately
        /// rolls nothing (no eligible SKUs) simply retries on the next hour tick inside the window,
        /// which is the behaviour you want anyway.
        /// </summary>
        private void TryRollOffersThisHour(int today, int hour)
        {
            if (hour < OfferWindowOpenHour || hour > OfferWindowCloseHour) return;
            if (_offerHourDay != today) PickTodaysOfferHour(today);
            if (hour < _offerHourToday) return;
            if (_generatedOfferDay.Values.Any(d => d == today)) return;

            RollDailyBulkOffers(today);
        }

        /// <summary>
        /// Ends every account whose freight sat past its deadline without a dock appointment, and
        /// cancels the stranded orders.
        ///
        /// This is what makes the Schedule tab load-bearing even though recurring orders auto-place a
        /// door on arrival (DockScheduleService.TryAutoPlace): "unscheduled" here means a bulk order
        /// still waiting on the player to place it, a recurring order the dock was genuinely full for
        /// before the due day, or a trailer the player deliberately unbooked and never rebooked — either
        /// way, nobody promised this freight a truck.
        ///
        /// The contract is lost either way, but the ORDERS are only cleared where that's safe.
        /// OrderService.CanCancelOrder refuses anything physically committed — a part-picked or staged
        /// order has real pallets sitting in a lane, and cancelling it would orphan them. Those are
        /// left on the board for the player to ship late or call off themselves. Freight nobody has
        /// touched is cancelled, so it stops drawing late fees for a customer who isn't coming.
        /// </summary>
        private void SweepMissedPickups(int newDay)
        {
            if (_orderService == null) return;
            // No schedule service means no way to tell booked freight from stranded freight. Standing
            // down is the only safe answer — guessing would lose every contract in the game.
            if (_dockSchedule == null) return;

            // Materialised before touching anything: cancelling mutates the active list.
            var missed = _orderService.ActiveOrders
                .Where(o => o != null
                         && o.DueDay < newDay
                         && o.Status != OrderData.OrderStatus.Shipped
                         && o.Status != OrderData.OrderStatus.Cancelled
                         && o.Status != OrderData.OrderStatus.Loading
                         && o.Status != OrderData.OrderStatus.Loaded
                         && _dockSchedule.FindForOrder(o.OrderId) == null)
                .ToList();
            if (missed.Count == 0) return;

            // One toast per CONTRACT, not per order — a standing account with three stranded orders
            // is one loss, and three identical toasts would read as three separate failures.
            var lostAlready = new HashSet<string>();
            foreach (var order in missed)
            {
                if (string.IsNullOrEmpty(order.ContractId)) continue;   // Dev Console order — nothing to lose
                if (!lostAlready.Add(order.ContractId)) continue;

                string who = LoseContract(order.ContractId, newDay);
                if (who != null)
                    UIToast.Show($"Customer {who} contract loss due to pickup not being setup timely");
            }

            int cancelled = _orderService.CancelOrders(missed.Select(o => o.OrderId).ToList());

            Debug.Log($"[OrderArrivalService] Missed-pickup sweep on day {newDay}: {missed.Count} stranded " +
                      $"order(s), {cancelled} cancelled ({missed.Count - cancelled} left standing — already " +
                      $"picked or staged), {lostAlready.Count} contract(s) lost.");
        }

        /// <summary>
        /// Rolls the day's 1–3 fresh bulk offers. Called from TryRollOffersThisHour during business
        /// hours, NOT at the day roll — new customer requests arrive while somebody's answering the
        /// phone.
        ///
        /// Expiry is the day roll's job (ExpireStaleBulkOffers, called from OnDayChanged) and no
        /// longer happens here. That split is deliberate: the board must be swept the moment the day
        /// turns, or yesterday's dead offers would sit there until whatever hour today's batch happens
        /// to land — and "already rolled today" is derived from _generatedOfferDay, which expiry
        /// mutates, so doing both in one call would have made that test depend on call order.
        /// </summary>
        private void RollDailyBulkOffers(int today)
        {
            if (_customers == null) _customers = CustomerRegistry.Load();
            if (_customers == null || _customers.customers.Count == 0) return;
            if (_inventoryService == null) return;

            // Full pallets are the entire point, so a SKU with no committed Ti/Hi can't express one.
            var eligible = _inventoryService.AllSkus
                .Where(s => s != null && s.BuyValue > 0f && s.Ti > 0 && s.Hi > 0)
                .ToList();
            if (eligible.Count == 0)
            {
                Debug.LogWarning("[OrderArrivalService] No SKUs with committed Ti/Hi and a buy price — " +
                                 "no bulk offers rolled.");
                return;
            }

            var rand = new System.Random();
            int count = rand.Next(BulkOffersPerDayMin, BulkOffersPerDayMax + 1);

            for (int i = 0; i < count; i++)
            {
                var customer = _customers.customers[rand.Next(_customers.customers.Count)];
                if (customer == null) continue;

                // Day and index in the id so a customer can appear twice on one board, and so an offer
                // is traceable back to the day it was rolled when reading a log after the fact.
                string contractId = $"BULK_{customer.CustomerId}_D{today}_{i}";
                if (_catalog.Any(c => c != null && c.ContractId == contractId)) continue;
                // Don't offer to someone the player just lost — the cooldown would refuse the signing
                // anyway, and a card that can't be clicked is worse than no card.
                if (IsInLossCooldown(contractId)) continue;

                // THE DEADLINE — the latest day this freight may ship, rolled fresh per offer and
                // shown under the ACCEPT button on the card. Range is SAME DAY (0) through 72 hours
                // (3): Next's upper bound is exclusive, so Next(0, 4) gives 0-3.
                //
                // A 0 here is a genuine rush, not a degenerate case. The player must pick, stage,
                // load and close it out before midnight, and in exchange it pays double (see
                // OrderData.SameDayRushRevenueMultiplier). Miss any deadline and the customer's
                // satisfaction takes a hit; leave the freight unbooked past it and they refuse the
                // load entirely — SweepMissedPickups cancels the order and loses the account.
                int deadlineDays = rand.Next(0, 4);

                var offer = ContractData.CreateRuntime(
                    contractId, customer,
                    deadlineDays <= 0
                        ? "Contract pays Cost of Goods plus 5%, Double if the load ships on the same " +
                          "day it was ordered!!!"
                        : $"{customer.CompanyName} wants a full-pallet drop. Cost of goods plus 5%.",
                    ContractKind.Bulk,
                    palletCount: 1,
                    ordersPerDayMin: 1, ordersPerDayMax: 1,
                    lineItemsMin: 1, lineItemsMax: 3,
                    casesPerLineMin: 1, casesPerLineMax: 1,
                    cutoffHour: 17,
                    leadTimeDays: deadlineDays,
                    payRateMultiplier: 1f,
                    lateFeePercent: 0.25f,
                    frequency: OrderFrequency.OneTime,
                    bulkLinesMin: 1, bulkLinesMax: 3,
                    bulkPalletsPerLineMin: 1, bulkPalletsPerLineMax: 10);

                if (!AddOffer(offer)) continue;
                _generatedOfferDay[contractId] = today;
            }

            Debug.Log($"[OrderArrivalService] Rolled {count} bulk offer(s) for day {today}.");
        }

        /// <summary>Drops generated offers older than today that nobody took. Signed ones stay in the
        /// catalog — a SignedContract resolves its terms through GetContract, so removing the asset
        /// out from under it would orphan the order it produced.</summary>
        private void ExpireStaleBulkOffers(int today)
        {
            var stale = _generatedOfferDay
                .Where(kv => kv.Value < today && !_signed.Any(s => s.ContractId == kv.Key))
                .Select(kv => kv.Key)
                .ToList();

            foreach (string id in stale)
            {
                _catalog.RemoveAll(c => c != null && c.ContractId == id);
                _generatedOfferDay.Remove(id);
            }
        }

        /// <summary>
        /// One bulk order: 1–3 lines, each a whole number of FULL PALLETS of a SKU plus whatever part
        /// case the customer happened to ask for on top.
        ///
        /// That remainder is the interesting half. 620 cases of a 60-per-pallet SKU is ten pallets and
        /// twenty loose cases — ten PalletPick tasks for a Reach Truck and one ordinary case pick for
        /// a selector, both staged into the same lane and loaded onto the same trailer. Rounding it up
        /// to eleven pallets would have been simpler and would have deleted the mechanic.
        ///
        /// PRICING is cost of goods plus a 5% surcharge, deliberately ignoring both the SKU's SellValue
        /// and the contract's PayRateMultiplier: bulk is a low-margin volume deal priced off what the
        /// goods cost, which is what makes it a different decision from a standing account.
        /// </summary>
        private void GenerateBulk(ContractData contract, int today)
        {
            var customer = contract.Customer;
            if (customer == null)
            {
                Debug.LogWarning($"[OrderArrivalService] Contract '{contract.ContractId}' has no customer assigned.");
                return;
            }

            var eligible = _inventoryService.AllSkus
                .Where(s => s != null && s.BuyValue > 0f && s.Ti > 0 && s.Hi > 0)
                .ToList();
            if (eligible.Count == 0)
            {
                Debug.LogWarning("[OrderArrivalService] No SKUs with committed Ti/Hi — bulk order not generated.");
                return;
            }

            var rand = new System.Random();
            int lineCount = Mathf.Min(eligible.Count,
                rand.Next(contract.BulkLinesMin, contract.BulkLinesMax + 1));
            var chosen = eligible.OrderBy(_ => rand.Next()).Take(lineCount).ToList();

            // DueDay = today + the deadline the offer advertised. today + 0 is a SAME-DAY RUSH: the
            // order is born already due, so OrderData.IsSameDayRush is true and ShipOrder pays double
            // if the player closes it out before the day rolls. It is NOT overdue on arrival —
            // IsOverdue is strictly currentDay > DueDay — so no fine is charged the moment it lands.
            var order = new OrderData(
                customer.CustomerId,
                customer.CompanyName,
                $"{customer.CompanyName} Distribution Center",
                today,
                today + contract.LeadTimeDays,
                _timeService.Minute)
            {
                ContractId = contract.ContractId,
                LateFeePercent = contract.LateFeePercent,
                IsBulk = true
            };

            int totalCases = 0;
            int totalPallets = 0;
            foreach (var sku in chosen)
            {
                int fullPallet = sku.Ti * sku.Hi;
                int pallets = rand.Next(contract.BulkPalletsPerLineMin, contract.BulkPalletsPerLineMax + 1);
                // A remainder about a third of the time — always a real part case, never a second full
                // pallet's worth, so the split into "pallet picks plus one case pick" stays honest.
                int remainder = rand.Next(0, 3) == 0 ? rand.Next(1, fullPallet) : 0;
                int qty = pallets * fullPallet + remainder;

                totalCases += qty;
                totalPallets += pallets;

                order.LineItems.Add(new OrderLineItem(
                    sku.SkuId,
                    qty,
                    Mathf.RoundToInt(sku.BuyValue),
                    Mathf.RoundToInt(sku.BuyValue * BulkSurchargeMultiplier)));
            }

            _orderService.ReceiveOrder(order);
            Debug.Log($"[OrderArrivalService] BULK accepted — {customer.CompanyName}: {totalCases} case(s) " +
                      $"across {order.LineItems.Count} line(s) (~{totalPallets} full pallet(s)), " +
                      $"must ship by day {order.DueDay}" +
                      (order.IsSameDayRush ? " — SAME-DAY RUSH, pays double if it makes the truck." : "."));
        }

        /// <summary>Cost of goods plus a 5% surcharge — the whole of bulk pricing.</summary>
        public const float BulkSurchargeMultiplier = 1.05f;

        private void GenerateFor(ContractData contract, int today)
        {
            var customer = contract.Customer;
            if (customer == null)
            {
                Debug.LogWarning($"[OrderArrivalService] Contract '{contract.ContractId}' has no customer assigned.");
                return;
            }

            var eligible = _inventoryService.AllSkus.Where(s => s != null && s.SellValue > 0f).ToList();
            if (eligible.Count == 0)
            {
                Debug.LogWarning("[OrderArrivalService] No sellable SKUs — no orders generated.");
                return;
            }

            var rand = new System.Random();

            // Used to roll a separate OrderData (its own OrderId/OrderNumber) per batch, and every
            // batch for the same customer+contract on the same day joins the same trailer appointment
            // (see HandleOrderArrived's join branch) -- so one recurring account's single daily
            // delivery could show up in the Work Queue under two or three different order numbers for
            // what was really one trailer. Per Tad: one accepted order = one order number. The
            // OrdersPerDay roll still drives how much gets ordered today -- more batches means more
            // total line variety/volume -- it just no longer mints a new order number per batch; every
            // batch folds into the SAME OrderData, merging quantity into an existing line if a later
            // batch happens to pick a SKU an earlier one already did.
            int batchCount = rand.Next(contract.OrdersPerDayMin, contract.OrdersPerDayMax + 1);

            var order = new OrderData(
                customer.CustomerId,
                customer.CompanyName,
                $"{customer.CompanyName} Distribution Center",
                today,
                today + contract.LeadTimeDays,
                _timeService.Minute)
            {
                ContractId = contract.ContractId,
                LateFeePercent = contract.LateFeePercent
            };

            for (int i = 0; i < batchCount; i++)
            {
                int lineCount = Mathf.Min(eligible.Count, rand.Next(contract.LineItemsMin, contract.LineItemsMax + 1));
                var chosen = eligible.OrderBy(_ => rand.Next()).Take(lineCount).ToList();

                foreach (var sku in chosen)
                {
                    int qty = rand.Next(contract.CasesPerLineMin, contract.CasesPerLineMax + 1);
                    var existingLine = order.LineItems.FirstOrDefault(li => li.SkuId == sku.SkuId);
                    if (existingLine != null)
                        existingLine.QuantityNeeded += qty;
                    else
                        order.LineItems.Add(new OrderLineItem(
                            sku.SkuId,
                            qty,
                            Mathf.RoundToInt(sku.BuyValue),
                            Mathf.RoundToInt(sku.SellValue * contract.PayRateMultiplier)));
                }
            }

            _orderService.ReceiveOrder(order);
            Debug.Log($"[OrderArrivalService] {customer.CompanyName}: 1 order arrived on day {today} " +
                      $"({order.LineItems.Count} line item(s) across {batchCount} batch(es)), " +
                      $"due day {today + contract.LeadTimeDays}.");
        }

        // ── Persistence ──────────────────────────────────────────────────────

        public List<ContractSnapshot> Export() => _signed.Select(s => new ContractSnapshot
        {
            contractId = s.ContractId,
            signedOnDay = s.SignedOnDay,
            lastGeneratedDay = s.LastGeneratedDay,
            active = s.Active,
            ordersDelivered = s.OrdersDelivered,
            ordersLate = s.OrdersLate,
            revenueEarned = s.RevenueEarned,
            lateFeesPaid = s.LateFeesPaid,
            satisfactionPercent = s.SatisfactionPercent,
            lost = s.Lost,
            lostOnDay = s.LostOnDay
        }).ToList();

        public void Import(List<ContractSnapshot> entries)
        {
            _signed.Clear();
            if (entries == null) return;

            foreach (var snap in entries)
            {
                if (snap == null || string.IsNullOrEmpty(snap.contractId)) continue;
                _signed.Add(new SignedContract
                {
                    ContractId = snap.contractId,
                    SignedOnDay = snap.signedOnDay,
                    LastGeneratedDay = snap.lastGeneratedDay,
                    Active = snap.active,
                    OrdersDelivered = snap.ordersDelivered,
                    OrdersLate = snap.ordersLate,
                    RevenueEarned = snap.revenueEarned,
                    LateFeesPaid = snap.lateFeesPaid,
                    SatisfactionPercent = snap.satisfactionPercent >= 0f ? snap.satisfactionPercent : 100f,
                    Lost = snap.lost,
                    LostOnDay = snap.lostOnDay
                });
            }

            if (_signed.Count > 0)
                Debug.Log($"[OrderArrivalService] Restored {_signed.Count} signed contract(s).");

            // Tops the booked week back up after a load. Appointments persist too, so this normally
            // books nothing — but a save taken mid-week, or one written before pre-booking existed,
            // would otherwise come back with a short or empty horizon and no way to refill it until
            // the next midnight. Idempotent by HasAppointmentFor, so running it here is free.
            MaintainRecurringSchedule();
        }

        // ── Generated-offer persistence ──────────────────────────────────────
        //
        // Separate from the signed-contract export above because these are two different things:
        // ContractSnapshot records a DECISION the player made, this records the BOARD they were
        // looking at. An authored offer needs no snapshot — it's still in the ContractRegistry after a
        // load — so only runtime offers are written, which is exactly what _generatedOfferDay tracks.

        public List<GeneratedOfferSnapshot> ExportGeneratedOffers()
        {
            var list = new List<GeneratedOfferSnapshot>();
            foreach (var kv in _generatedOfferDay)
            {
                var c = _catalog.FirstOrDefault(x => x != null && x.ContractId == kv.Key);
                if (c == null) continue;

                list.Add(new GeneratedOfferSnapshot
                {
                    contractId = c.ContractId,
                    customerId = c.Customer != null ? c.Customer.CustomerId : null,
                    pitch = c.Pitch,
                    kind = (int)c.Kind,
                    frequency = (int)c.Frequency,
                    palletCount = c.PalletCount,
                    ordersPerDayMin = c.OrdersPerDayMin,
                    ordersPerDayMax = c.OrdersPerDayMax,
                    lineItemsMin = c.LineItemsMin,
                    lineItemsMax = c.LineItemsMax,
                    casesPerLineMin = c.CasesPerLineMin,
                    casesPerLineMax = c.CasesPerLineMax,
                    bulkLinesMin = c.BulkLinesMin,
                    bulkLinesMax = c.BulkLinesMax,
                    bulkPalletsPerLineMin = c.BulkPalletsPerLineMin,
                    bulkPalletsPerLineMax = c.BulkPalletsPerLineMax,
                    cutoffHour = c.CutoffHour,
                    leadTimeDays = c.LeadTimeDays,
                    payRateMultiplier = c.PayRateMultiplier,
                    lateFeePercent = c.LateFeePercent,
                    createdOnDay = kv.Value
                });
            }
            return list;
        }

        /// <summary>
        /// Rebuilds runtime offers from a save. Call AFTER <see cref="Import"/> and after
        /// <see cref="SetCatalog"/> — it adds to the catalog rather than replacing it, since the
        /// authored offers are already in there.
        ///
        /// A snapshot whose customer no longer resolves is skipped with a warning rather than rebuilt
        /// customer-less: every downstream consumer reads contract.Customer, and a null one would
        /// produce an offer card with no name that generates nothing when signed.
        /// </summary>
        public void ImportGeneratedOffers(List<GeneratedOfferSnapshot> entries)
        {
            _generatedOfferDay.Clear();
            if (entries == null || entries.Count == 0) return;

            if (_customers == null) _customers = CustomerRegistry.Load();

            int restored = 0;
            foreach (var s in entries)
            {
                if (s == null || string.IsNullOrEmpty(s.contractId)) continue;
                if (_catalog.Any(c => c != null && c.ContractId == s.contractId)) continue;

                var customer = _customers != null ? _customers.GetById(s.customerId) : null;
                if (customer == null)
                {
                    Debug.LogWarning($"[OrderArrivalService] Generated offer '{s.contractId}' names customer " +
                                     $"'{s.customerId}', which is no longer in the registry — skipping.");
                    continue;
                }

                var offer = ContractData.CreateRuntime(
                    s.contractId, customer, s.pitch, (ContractKind)s.kind,
                    s.palletCount,
                    s.ordersPerDayMin, s.ordersPerDayMax,
                    s.lineItemsMin, s.lineItemsMax,
                    s.casesPerLineMin, s.casesPerLineMax,
                    s.cutoffHour, s.leadTimeDays,
                    s.payRateMultiplier, s.lateFeePercent,
                    (OrderFrequency)s.frequency,
                    s.bulkLinesMin, s.bulkLinesMax,
                    s.bulkPalletsPerLineMin, s.bulkPalletsPerLineMax);

                if (!AddOffer(offer)) continue;
                _generatedOfferDay[s.contractId] = s.createdOnDay;
                restored++;
            }

            if (restored > 0)
                Debug.Log($"[OrderArrivalService] Restored {restored} generated offer(s).");
        }
    }
}
