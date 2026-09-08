using System.Collections.Generic;
using System.Linq;
using GameCore.Events;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>
    /// Rolling per-contract revenue ledger for the Recurring Orders tab's "Average Weekly Revenue"
    /// figure. Mirrors VendorPerformanceTracker's day-bucketed shape (that's the proven pattern for
    /// this kind of trailing-window stat in this codebase) rather than inventing a new one, scoped to
    /// SignedContract.ContractId instead of VendorId and fed by OrderService.OnOrderShipped instead of
    /// the vendor-side record calls.
    ///
    /// Ephemeral, like VendorPerformanceTracker/FulfillmentStatsService — no Export()/Import(). It
    /// re-derives itself from replayed OnOrderShipped activity after a load, so SignedContract/
    /// ContractSnapshot don't need a new persisted field for this.
    /// </summary>
    public class ContractRevenueTracker : IService
    {
        /// <summary>Trailing days of history averaged into the weekly figure. Four weeks rather than
        /// one — a contract that only ships every 2-3 days would have just one or two data points in
        /// a 7-day window, so a single unusually large or small shipment could swing "weekly revenue"
        /// wildly. Averaging revenue-per-week across the last 4 weeks smooths that out.</summary>
        public const int TrailingWindowDays = 28;

        private readonly Dictionary<string, Dictionary<int, float>> _revenueByContractByDay = new();

        private EventManager _eventManager;
        private int _currentDay = 1;

        public void Initialize()
        {
            _eventManager = EventManager.Instance;
            if (_eventManager != null)
                _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);

            // Detach-then-attach: OnOrderShipped is a STATIC event, so a leaked instance from a prior
            // domain reload could otherwise double-subscribe — same guard OrderArrivalService and
            // FulfillmentStatsService already use on this exact event.
            OrderService.OnOrderShipped -= HandleOrderShipped;
            OrderService.OnOrderShipped += HandleOrderShipped;
        }

        public void Shutdown()
        {
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            OrderService.OnOrderShipped -= HandleOrderShipped;
            _revenueByContractByDay.Clear();
        }

        public void ClearAll() => _revenueByContractByDay.Clear();

        /// <summary>Bulk/one-off orders carry no ContractId (see OrderData.ContractId's own doc
        /// comment) — same null/empty guard OrderArrivalService.FindSignedFor uses, so this tracker
        /// only ever accumulates revenue that actually belongs to a recurring account.</summary>
        private void HandleOrderShipped(OrderData order)
        {
            if (order == null || string.IsNullOrEmpty(order.ContractId)) return;

            if (!_revenueByContractByDay.TryGetValue(order.ContractId, out var byDay))
                _revenueByContractByDay[order.ContractId] = byDay = new Dictionary<int, float>();

            byDay[_currentDay] = byDay.TryGetValue(_currentDay, out float existing)
                ? existing + order.ShippedRevenue
                : order.ShippedRevenue;
        }

        /// <summary>Total revenue earned in the trailing window, averaged down to a per-week figure —
        /// a literal "revenue earned per week, averaged over the last 4 weeks." Returns 0 for a
        /// contract that has never shipped (or isn't recognized).</summary>
        public float GetAverageWeeklyRevenue(string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return 0f;
            if (!_revenueByContractByDay.TryGetValue(contractId, out var byDay) || byDay.Count == 0) return 0f;

            int cutoff = _currentDay - TrailingWindowDays;
            float sum = byDay.Where(kv => kv.Key > cutoff).Sum(kv => kv.Value);
            return sum / (TrailingWindowDays / 7f);
        }

        private void OnDayChanged(string eventId, int day)
        {
            _currentDay = day;

            int cutoff = _currentDay - TrailingWindowDays;
            foreach (var byDay in _revenueByContractByDay.Values)
            {
                var stale = byDay.Keys.Where(d => d <= cutoff).ToList();
                foreach (var d in stale) byDay.Remove(d);
            }
        }
    }
}
