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
    /// SKETCH — this is NOT wired up. It is registered nowhere and Initialize() is never called, so
    /// it has no effect on the running game. To switch it on, two lines in GameContext.Awake()
    /// alongside the other services:
    ///
    ///     ServiceLocator.Register&lt;GameCore.Inventory.OrderArrivalService&gt;(orderArrivalService);
    ///     orderArrivalService.Initialize();
    ///
    /// ...plus a CustomerRegistry/ContractData reference handed in, and Export/Import called from
    /// PlacementSystem's save path (see the persistence note below). Read the two caveats at the
    /// bottom of this comment BEFORE turning it on.
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
    /// TWO PREREQUISITES, both real:
    ///
    /// 1. TERMINAL ORDERS ARE NEVER RETIRED. OrderService._activeOrders accumulates Shipped and
    ///    Cancelled orders forever (observed at 67, nearly all terminal, in a single play session).
    ///    That list feeds the Work Queue rows, the per-customer stage gates, and TryPlanStageSpread.
    ///    While orders arrive by hand it is cosmetic; the moment they arrive every day it is a real
    ///    and compounding problem. Archive terminal orders out of the active list — keeping them for
    ///    financial history — BEFORE enabling automatic arrival.
    ///
    /// 2. ARRIVAL STATE MUST PERSIST. Export()/Import() below exist for exactly that and must be
    ///    called from PlacementSystem.ApplySaveData alongside orderService.Import(save.orders).
    ///    Without it, SignedContract.LastGeneratedDay resets and a save/load either duplicates a
    ///    day's orders or silently skips one.
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

        /// <summary>Contract offers available to sign. Hand in the authored ContractData assets —
        /// same pattern as CustomerRegistry being handed to OrderGenerator.</summary>
        public void SetCatalog(IEnumerable<ContractData> contracts)
        {
            _catalog.Clear();
            if (contracts != null) _catalog.AddRange(contracts.Where(c => c != null));
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

            _eventManager.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
        }

        public void Shutdown()
        {
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
        }

        // ── Signing ──────────────────────────────────────────────────────────

        public bool Sign(string contractId)
        {
            if (_signed.Any(s => s.ContractId == contractId && s.Active)) return false;
            var contract = _catalog.FirstOrDefault(c => c.ContractId == contractId);
            if (contract == null) return false;

            _signed.Add(new SignedContract
            {
                ContractId = contractId,
                SignedOnDay = _timeService?.Day ?? 0,
                // Never back-fill: a contract signed at 16:00 with a 17:00 cutoff should deliver its
                // first orders TODAY, but one signed at 18:00 must wait for tomorrow rather than
                // immediately dumping a day's volume on a player who just signed it.
                LastGeneratedDay = (_timeService != null && _timeService.Hour >= contract.CutoffHour)
                                 ? _timeService.Day
                                 : -1
            });

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
                if (newHour < contract.CutoffHour) continue;

                // Stamped BEFORE generating: a throw or an empty roll must not leave this contract
                // eligible to fire again on the next hour tick of the same day.
                signed.LastGeneratedDay = today;
                GenerateFor(contract, today);
            }
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
                    _timeService.Minute);

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
            active = s.Active
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
                    Active = snap.active
                });
            }

            if (_signed.Count > 0)
                Debug.Log($"[OrderArrivalService] Restored {_signed.Count} signed contract(s).");
        }
    }
}
