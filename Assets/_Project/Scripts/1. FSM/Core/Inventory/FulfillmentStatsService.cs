using UnityEngine;
using GameCore.Services;
using GameCore.Events;
using GameCore.Economy;

namespace GameCore.Inventory
{
    /// <summary>
    /// Tracks lifetime shipping totals and derives the four headline stats shown atop the Recurring
    /// Orders tab: Avg. Plts. Dly., Avg. Cases Dly., Avg. On-Time, Avg. Fill Rate %.
    ///
    /// Two different update rates, on purpose:
    /// - Raw totals (cases/pallets/orders shipped, orders late, cases requested) accumulate the
    ///   INSTANT an order ships, via the static OrderService.OnOrderShipped event — nothing is
    ///   ever missed or double-counted.
    /// - The four public per-day/percentage averages are only RECOMPUTED once per in-game hour
    ///   (GameEvents.Time.OnHourChanged). The UI reads these cached values every frame (so they
    ///   always look "live"), but the divide itself only happens 24 times a day instead of every
    ///   frame or every ship event.
    /// </summary>
    public class FulfillmentStatsService : IService
    {
        private EventManager _eventManager;
        private OrderService _orderService;
        private SimulationTimeService _timeService;

        // ── Raw lifetime totals — accumulate the instant an order ships ──
        private long _totalCasesShipped;
        private long _totalPalletsShipped;
        private long _totalOrdersShipped;
        private long _totalOrdersLate;
        private long _totalCasesRequested; // denominator for lifetime fill rate

        // ── Cached, recomputed once per in-game hour — what the UI actually reads ──

        /// <summary>Lifetime pallets shipped ÷ elapsed in-game days (fractional — day 2, 12:00 is 2.5).</summary>
        public float AvgPalletsPerDay { get; private set; }

        /// <summary>Lifetime cases shipped ÷ elapsed in-game days.</summary>
        public float AvgCasesPerDay { get; private set; }

        /// <summary>Fraction of every shipped order that went out without being fined for lateness.</summary>
        public float AvgOnTimeRate { get; private set; } = 1f;

        /// <summary>Lifetime cases actually shipped ÷ lifetime cases customers asked for.</summary>
        public float AvgFillRate { get; private set; } = 1f;

        public void Initialize()
        {
            _eventManager = EventManager.Instance;
            ServiceLocator.TryGet(out _orderService);
            ServiceLocator.TryGet(out _timeService);

            // Detach-then-attach: OnOrderShipped is a STATIC event, so a leaked instance from a
            // domain reload could otherwise double-subscribe and double-count every ship.
            OrderService.OnOrderShipped -= HandleOrderShipped;
            OrderService.OnOrderShipped += HandleOrderShipped;

            _eventManager?.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);

            RecomputeAverages(); // seed sane 100%-style defaults before the first hour tick lands
        }

        public void Shutdown()
        {
            OrderService.OnOrderShipped -= HandleOrderShipped;
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
        }

        private void HandleOrderShipped(OrderData order)
        {
            if (order == null) return;

            _totalOrdersShipped++;
            if (order.HasBeenFined) _totalOrdersLate++;

            _totalCasesShipped   += order.TotalUnitsPicked;
            _totalCasesRequested += order.TotalUnits;

            if (_orderService != null)
            {
                foreach (var lineItem in order.LineItems)
                {
                    int casesPerFullPallet = Mathf.Max(1, _orderService.FullPalletCases(lineItem.SkuId));
                    _totalPalletsShipped += Mathf.CeilToInt(lineItem.QuantityPicked / (float)casesPerFullPallet);
                }
            }
        }

        private void OnHourChanged(string eventId, int hour) => RecomputeAverages();

        /// <summary>Elapsed in-game days, fractional (TotalMinutesElapsed / 1440) — matches the
        /// clock's own bookkeeping rather than re-deriving it from Day/Hour separately.</summary>
        private float ElapsedDays() => _timeService == null ? 0f : _timeService.TotalMinutesElapsed / 1440f;

        private void RecomputeAverages()
        {
            // Guards the first in-game hour against a divide against ~0 days, which would otherwise
            // spike the per-day averages to a meaningless number before any real time has passed.
            float days = Mathf.Max(ElapsedDays(), 1f / 24f);

            AvgPalletsPerDay = _totalPalletsShipped / days;
            AvgCasesPerDay   = _totalCasesShipped / days;
            AvgOnTimeRate    = _totalOrdersShipped <= 0 ? 1f : 1f - (_totalOrdersLate / (float)_totalOrdersShipped);
            AvgFillRate      = _totalCasesRequested <= 0 ? 1f : _totalCasesShipped / (float)_totalCasesRequested;
        }
    }
}
