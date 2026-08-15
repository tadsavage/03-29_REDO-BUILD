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
                             && !IsInLossCooldown(c.ContractId));

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
            // Keyed on LastGeneratedDay, NOT on whether the cutoff hour has passed: a contract signed
            // after its own cutoff still fires on the next hour tick today (OnHourChanged only tests
            // newHour >= CutoffHour), so "it's gone 17:00" doesn't mean today's drop has happened.
            day = signed.LastGeneratedDay >= today ? today + 1 : today;
            return true;
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
            // Billed amount, not TotalRevenue: an order that shipped short only earned what actually
            // went on the truck, and that's what ShipOrder credited to the player.
            signed.RevenueEarned += order.LineItems.Sum(li => (long)li.QuantityPicked * li.SellingPrice);
        }

        private void HandleOrderFined(OrderData order, int fine)
        {
            var signed = FindSignedFor(order);
            if (signed == null) return;
            signed.OrdersLate++;
            signed.LateFeesPaid += fine;
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

            var signed = new SignedContract
            {
                ContractId = contractId,
                SignedOnDay = _timeService?.Day ?? 0,
                // Never back-fill: a contract signed at 16:00 with a 17:00 cutoff should deliver its
                // first orders TODAY, but one signed at 18:00 must wait for tomorrow rather than
                // immediately dumping a day's volume on a player who just signed it.
                LastGeneratedDay = (_timeService != null && _timeService.Hour >= contract.CutoffHour)
                                 ? _timeService.Day
                                 : -1
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

            Debug.Log($"[OrderArrivalService] Signed {contractId} ({contract.Customer?.CompanyName}) — " +
                      $"{contract.FrequencyLabel}, ~{contract.EstimatedCasesPerDay} cases/day, " +
                      $"due {contract.LeadTimeDays} day(s) out.");
            return true;
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

            foreach (var signed in _signed)
            {
                if (!signed.Active) continue;
                if (signed.LastGeneratedDay >= today) continue; // already delivered today

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
                if (newHour < contract.CutoffHour) continue;
                // Weekly waits out the rest of its week. A contract that has never fired
                // (LastGeneratedDay < 0) delivers on its first cutoff rather than making the player
                // wait a week for anything at all to arrive.
                if (contract.Frequency == OrderFrequency.Weekly &&
                    signed.LastGeneratedDay >= 0 &&
                    today - signed.LastGeneratedDay < 7) continue;

                // Stamped BEFORE generating: a throw or an empty roll must not leave this contract
                // eligible to fire again on the next hour tick of the same day.
                signed.LastGeneratedDay = today;
                GenerateFor(contract, today);
            }
        }

        // ── Day roll: refresh the bulk board, then judge missed pickups ──────

        private void OnDayChanged(string eventId, int newDay)
        {
            // Order matters. The loss sweep judges YESTERDAY's failures, so it runs against the board
            // as it stood; rolling fresh offers first would be harmless today but only by accident.
            SweepMissedPickups(newDay);
            RollDailyBulkOffers(newDay);
        }

        /// <summary>
        /// Ends every account whose freight sat past its deadline without a dock appointment, and
        /// cancels the stranded orders.
        ///
        /// This is what makes the Schedule tab load-bearing even though arrival auto-places a door for
        /// most orders (DockScheduleService.TryAutoPlace): "unscheduled" here means the dock was
        /// genuinely full before the due day, or the player deliberately unbooked the trailer and never
        /// rebooked it — either way, nobody promised this freight a truck.
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
        /// Clears yesterday's untaken bulk offers and rolls 1–3 fresh ones.
        ///
        /// Offers EXPIRE rather than accumulating, which is what makes the daily board a decision
        /// instead of a growing backlog the player can pick over at leisure. A bulk offer the player
        /// signed is already out of _catalog's available set, so only the ignored ones are dropped.
        /// </summary>
        private void RollDailyBulkOffers(int today)
        {
            ExpireStaleBulkOffers(today);

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

                var offer = ContractData.CreateRuntime(
                    contractId, customer,
                    $"{customer.CompanyName} wants a full-pallet drop. Cost of goods plus 5%.",
                    ContractKind.Bulk,
                    palletCount: 1,
                    ordersPerDayMin: 1, ordersPerDayMax: 1,
                    lineItemsMin: 1, lineItemsMax: 3,
                    casesPerLineMin: 1, casesPerLineMax: 1,
                    cutoffHour: 17,
                    leadTimeDays: rand.Next(1, 4),
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
                      $"due day {today + contract.LeadTimeDays}.");
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
            int orderCount = rand.Next(contract.OrdersPerDayMin, contract.OrdersPerDayMax + 1);

            for (int i = 0; i < orderCount; i++)
            {
                int lineCount = Mathf.Min(eligible.Count, rand.Next(contract.LineItemsMin, contract.LineItemsMax + 1));
                var chosen = eligible.OrderBy(_ => rand.Next()).Take(lineCount).ToList();

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

                foreach (var sku in chosen)
                {
                    int qty = rand.Next(contract.CasesPerLineMin, contract.CasesPerLineMax + 1);
                    order.LineItems.Add(new OrderLineItem(
                        sku.SkuId,
                        qty,
                        Mathf.RoundToInt(sku.BuyValue),
                        Mathf.RoundToInt(sku.SellValue * contract.PayRateMultiplier)));
                }

                _orderService.ReceiveOrder(order);
            }

            Debug.Log($"[OrderArrivalService] {customer.CompanyName}: {orderCount} order(s) arrived on day {today}, " +
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
                    Lost = snap.lost,
                    LostOnDay = snap.lostOnDay
                });
            }

            if (_signed.Count > 0)
                Debug.Log($"[OrderArrivalService] Restored {_signed.Count} signed contract(s).");
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
