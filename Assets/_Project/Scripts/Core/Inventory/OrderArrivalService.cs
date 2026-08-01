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
        private readonly List<SignedContract> _signed = new();
        private readonly List<ContractData> _catalog = new();

        private EventManager _eventManager;
        private OrderService _orderService;
        private InventoryService _inventoryService;
        private SimulationTimeService _timeService;

        /// <summary>Every contract the player has signed and not cancelled.</summary>
        public IReadOnlyList<SignedContract> Signed => _signed;

        /// <summary>Contract offers available to sign, in registry order.</summary>
        public IReadOnlyList<ContractData> Catalog => _catalog;

        /// <summary>Offers not yet signed — what the Offers tab lists.</summary>
        public IEnumerable<ContractData> AvailableOffers =>
            _catalog.Where(c => !_signed.Any(s => s.ContractId == c.ContractId && s.Active));

        public bool IsSigned(string contractId) =>
            _signed.Any(s => s.ContractId == contractId && s.Active);

        /// <summary>The signed record for a contract, or null if it was never taken.</summary>
        public SignedContract GetSigned(string contractId) =>
            _signed.FirstOrDefault(s => s.ContractId == contractId && s.Active);

        /// <summary>The catalog asset behind a signed record.</summary>
        public ContractData GetContract(string contractId) =>
            _catalog.FirstOrDefault(c => c.ContractId == contractId);

        /// <summary>
        /// Standing accounts actually sending work — signed, active, and NOT a spent wholesale deal.
        ///
        /// This distinction is the whole reason the method exists. A delivered one-off stays Active
        /// forever by design (that's what keeps the Sign button off its card), so a plain
        /// Signed.Count(s =&gt; s.Active) counts it as a running account. The panel footer did exactly
        /// that and reported three accounts running when two were.
        /// </summary>
        public IEnumerable<SignedContract> RunningAccounts => _signed.Where(s =>
        {
            if (!s.Active) return false;
            var c = GetContract(s.ContractId);
            return c != null && !c.IsWholesale;
        });

        /// <summary>Signed records for spent one-off deals — delivered, done, kept for the record.</summary>
        public IEnumerable<SignedContract> DeliveredWholesale => _signed.Where(s =>
        {
            if (!s.Active) return false;
            var c = GetContract(s.ContractId);
            return c != null && c.IsWholesale;
        });

        /// <summary>Day/hour a recurring contract next drops orders. A contract that has already run
        /// today rolls to tomorrow. Meaningless for wholesale, which returns false.</summary>
        public bool TryGetNextArrival(string contractId, out int day, out int hour)
        {
            day = 0; hour = 0;
            var contract = GetContract(contractId);
            var signed = GetSigned(contractId);
            if (contract == null || signed == null || contract.IsWholesale) return false;

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

            // Self-load the catalog so no caller has to remember to. SetCatalog still exists for
            // tests and for handing in a different list.
            if (_catalog.Count == 0)
            {
                var registry = ContractRegistry.Load();
                if (registry != null) SetCatalog(registry.contracts);
            }

            _eventManager.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);

            // Detach-then-attach: these are STATIC events, so a service instance leaked by a domain
            // reload would otherwise keep a live handler on a dead _signed list. This doesn't unhook
            // that leaked instance's own handler, but it does stop THIS one double-counting if
            // Initialize is ever called twice.
            OrderService.OnOrderShipped -= HandleOrderShipped;
            OrderService.OnOrderShipped += HandleOrderShipped;
            OrderService.OnOrderFined -= HandleOrderFined;
            OrderService.OnOrderFined += HandleOrderFined;
        }

        public void Shutdown()
        {
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
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

            if (contract.IsWholesale)
            {
                // Stays ACTIVE deliberately, even though it will never generate again. Active means
                // "this contract is taken" — it's what IsSigned reports, which is both what greys the
                // card out and what stops a second signing. Clearing it here (the original mistake)
                // made a delivered one-off read as never-signed: the card kept its Sign button and
                // every click produced another full trailer.
                //
                // What stops it re-firing is the IsWholesale skip in OnHourChanged, not this flag.
                int today = _timeService?.Day ?? 0;
                signed.LastGeneratedDay = today;
                GenerateWholesale(contract, today);
                return true;
            }

            Debug.Log($"[OrderArrivalService] Signed {contractId} ({contract.Customer?.CompanyName}) — " +
                      $"~{contract.EstimatedCasesPerDay} cases/day, due {contract.LeadTimeDays} day(s) out.");
            return true;
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
                // marked taken in the modal) but must never produce a second trailer.
                if (contract.IsWholesale) continue;
                if (newHour < contract.CutoffHour) continue;

                // Stamped BEFORE generating: a throw or an empty roll must not leave this contract
                // eligible to fire again on the next hour tick of the same day.
                signed.LastGeneratedDay = today;
                GenerateFor(contract, today);
            }
        }

        /// <summary>
        /// One wholesale drop: a single order whose every line item is exactly ONE FULL PALLET of a
        /// SKU — Ti x Hi cases, never a partial layer and never a loose case. That's what makes it a
        /// pallet pick rather than a big case pick.
        ///
        /// KNOWN GAP (deliberate, per Tad): the quantities are full pallets but FULFILMENT still runs
        /// through the case-pick selector, which will walk these off one case at a time. The order
        /// shape is right; the mechanic that moves whole pallets from reserve to the staging lane
        /// doesn't exist yet. Building it is a separate piece of work comparable in size to the
        /// outbound picking system.
        /// </summary>
        private void GenerateWholesale(ContractData contract, int today)
        {
            var customer = contract.Customer;
            if (customer == null)
            {
                Debug.LogWarning($"[OrderArrivalService] Contract '{contract.ContractId}' has no customer assigned.");
                return;
            }

            // Only SKUs with real pallet maths — a Ti/Hi of zero can't express "one full pallet".
            var eligible = _inventoryService.AllSkus
                .Where(s => s != null && s.SellValue > 0f && s.Ti > 0 && s.Hi > 0)
                .ToList();
            if (eligible.Count == 0)
            {
                Debug.LogWarning("[OrderArrivalService] No SKUs with committed Ti/Hi — wholesale order not generated.");
                return;
            }

            var rand = new System.Random();
            int pallets = Mathf.Max(1, contract.PalletCount);

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
                IsWholesale = true
            };

            // Distinct SKUs where possible so the trailer isn't 12 pallets of one thing; if the
            // catalogue is smaller than the pallet count, SKUs repeat as separate full-pallet lines.
            var picks = eligible.OrderBy(_ => rand.Next()).Take(Mathf.Min(pallets, eligible.Count)).ToList();
            int totalCases = 0;
            for (int i = 0; i < pallets; i++)
            {
                var sku = picks[i % picks.Count];
                int fullPallet = sku.Ti * sku.Hi;
                totalCases += fullPallet;
                order.LineItems.Add(new OrderLineItem(
                    sku.SkuId,
                    fullPallet,
                    Mathf.RoundToInt(sku.BuyValue),
                    Mathf.RoundToInt(sku.SellValue * contract.PayRateMultiplier)));
            }

            _orderService.ReceiveOrder(order);
            Debug.Log($"[OrderArrivalService] WHOLESALE signed — {customer.CompanyName}: {pallets} full pallet(s), " +
                      $"{totalCases} cases across {order.LineItems.Count} line(s), due day {today + contract.LeadTimeDays}.");
        }

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
            lateFeesPaid = s.LateFeesPaid
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
                    LateFeesPaid = snap.lateFeesPaid
                });
            }

            if (_signed.Count > 0)
                Debug.Log($"[OrderArrivalService] Restored {_signed.Count} signed contract(s).");
        }
    }
}
