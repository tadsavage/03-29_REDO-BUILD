using System.Collections.Generic;
using System.Linq;
using GameCore.Events;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>
    /// Single-responsibility rolling ledger: records realized profit per vendor transaction and
    /// exposes an "Average Daily Revenue" figure for the VENDORS tab's data grid.
    ///
    /// Sourced from ACTUAL recorded sell-through, not a static formula — OrderService/ShipmentService
    /// call RecordTransaction whenever a shipment originating from a vendor sells through, so this
    /// reads 0/empty until at least one such sale has happened. No gameplay call site wires that hook
    /// yet (see the plan's Implementation Notes); this tracker is ready for it.
    /// </summary>
    public class VendorPerformanceTracker : IService
    {
        /// <summary>How many trailing days of history count toward the average — old enough to smooth
        /// out a single lucky/unlucky day, short enough that a vendor's CURRENT standing (not their
        /// standing a month ago) is what the grid reports.</summary>
        public const int TrailingWindowDays = 14;

        private readonly Dictionary<string, Dictionary<int, float>> _revenueByVendorByDay = new();
        private EventManager _eventManager;
        private int _currentDay = 1;

        public void Initialize()
        {
            _eventManager = EventManager.Instance;
            if (_eventManager != null)
                _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
        }

        public void Shutdown()
        {
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            _revenueByVendorByDay.Clear();
        }

        public void ClearAll() => _revenueByVendorByDay.Clear();

        /// <summary>Records one vendor-attributed sale. Multiple transactions on the same
        /// simulationDay accumulate into that day's bucket rather than overwriting it.</summary>
        public void RecordTransaction(string vendorId, float revenue, int simulationDay)
        {
            if (string.IsNullOrEmpty(vendorId)) return;

            if (!_revenueByVendorByDay.TryGetValue(vendorId, out var byDay))
                _revenueByVendorByDay[vendorId] = byDay = new Dictionary<int, float>();

            byDay[simulationDay] = byDay.TryGetValue(simulationDay, out float existing)
                ? existing + revenue
                : revenue;
        }

        /// <summary>Averages recorded revenue over the trailing window of days that actually have
        /// data — a vendor sold through on 3 of the last 14 days averages over those 3, not over 14
        /// mostly-empty days, so a new relationship doesn't read as barely profitable purely because
        /// it's new.</summary>
        public float GetAverageDailyRevenue(string vendorId)
        {
            if (string.IsNullOrEmpty(vendorId)) return 0f;
            if (!_revenueByVendorByDay.TryGetValue(vendorId, out var byDay) || byDay.Count == 0) return 0f;

            int cutoff = _currentDay - TrailingWindowDays;
            var inWindow = byDay.Where(kv => kv.Key > cutoff).ToList();
            if (inWindow.Count == 0) return 0f;

            return inWindow.Sum(kv => kv.Value) / inWindow.Count;
        }

        private void OnDayChanged(string eventId, int day)
        {
            _currentDay = day;

            // Prunes days that have fallen out of the trailing window entirely, so the ledger doesn't
            // grow unbounded over a long session.
            int cutoff = _currentDay - TrailingWindowDays;
            foreach (var byDay in _revenueByVendorByDay.Values)
            {
                var stale = byDay.Keys.Where(d => d <= cutoff).ToList();
                foreach (var d in stale) byDay.Remove(d);
            }
        }
    }
}
