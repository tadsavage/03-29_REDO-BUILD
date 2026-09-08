using System.Collections.Generic;
using System.Linq;
using GameCore.Events;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>
    /// Single-responsibility rolling ledger: records real per-vendor activity — PO spend, pallets
    /// received, and inbound dwell time — and exposes trailing-window daily averages for the VENDORS
    /// tab's data grid.
    ///
    /// Sourced from ACTUAL recorded activity, not a static formula: PurchasingPanel.SubmitPurchaseOrder
    /// calls RecordTransaction when a PO is raised, TrailerOffloadController calls RecordPallets when
    /// an inbound trailer finishes offloading, and TruckController calls RecordDwellHours when an
    /// inbound trailer departs — so every figure reads 0 until the corresponding thing has actually
    /// happened at least once.
    /// </summary>
    public class VendorPerformanceTracker : IService
    {
        /// <summary>How many trailing days of history count toward the average — old enough to smooth
        /// out a single lucky/unlucky day, short enough that a vendor's CURRENT standing (not their
        /// standing a month ago) is what the grid reports.</summary>
        public const int TrailingWindowDays = 14;

        private readonly Dictionary<string, Dictionary<int, float>> _revenueByVendorByDay = new();

        /// <summary>Pallets received per vendor per day — fed by TrailerOffloadController when an
        /// inbound trailer finishes offloading.</summary>
        private readonly Dictionary<string, Dictionary<int, float>> _palletsByVendorByDay = new();

        /// <summary>Dwell hours (docked-to-departed) per vendor per day, summed per day the same way
        /// revenue is — averaged over days-with-a-completion, not over the trailing window blindly, so
        /// "Avg Hours in Door" answers "on days this vendor actually delivered" per the design ask.</summary>
        private readonly Dictionary<string, Dictionary<int, float>> _dwellHoursByVendorByDay = new();

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
            _palletsByVendorByDay.Clear();
            _dwellHoursByVendorByDay.Clear();
        }

        public void ClearAll()
        {
            _revenueByVendorByDay.Clear();
            _palletsByVendorByDay.Clear();
            _dwellHoursByVendorByDay.Clear();
        }

        /// <summary>Records one vendor-attributed spend (a raised PO's cost). Multiple transactions on
        /// the same simulationDay accumulate into that day's bucket rather than overwriting it. Despite
        /// the historical name this now tracks SPEND (what we paid a vendor), not sell-through revenue —
        /// see PurchasingPanel.SubmitPurchaseOrder, the only call site.</summary>
        public void RecordTransaction(string vendorId, float revenue, int simulationDay)
            => Accumulate(_revenueByVendorByDay, vendorId, revenue, simulationDay);

        /// <summary>Records pallets received from this vendor on one inbound delivery.</summary>
        public void RecordPallets(string vendorId, int pallets, int simulationDay)
            => Accumulate(_palletsByVendorByDay, vendorId, pallets, simulationDay);

        /// <summary>Records one inbound trailer's dock-to-departure dwell time, in hours.</summary>
        public void RecordDwellHours(string vendorId, float hours, int simulationDay)
            => Accumulate(_dwellHoursByVendorByDay, vendorId, hours, simulationDay);

        private static void Accumulate(Dictionary<string, Dictionary<int, float>> byVendorByDay,
                                        string vendorId, float value, int simulationDay)
        {
            if (string.IsNullOrEmpty(vendorId)) return;

            if (!byVendorByDay.TryGetValue(vendorId, out var byDay))
                byVendorByDay[vendorId] = byDay = new Dictionary<int, float>();

            byDay[simulationDay] = byDay.TryGetValue(simulationDay, out float existing)
                ? existing + value
                : value;
        }

        /// <summary>Averages recorded values over the trailing window of days that actually have
        /// data — a vendor sold through on 3 of the last 14 days averages over those 3, not over 14
        /// mostly-empty days, so a new relationship doesn't read as barely active purely because it's
        /// new. Shared by every Average* getter below.</summary>
        private float Average(Dictionary<string, Dictionary<int, float>> byVendorByDay, string vendorId)
        {
            if (string.IsNullOrEmpty(vendorId)) return 0f;
            if (!byVendorByDay.TryGetValue(vendorId, out var byDay) || byDay.Count == 0) return 0f;

            int cutoff = _currentDay - TrailingWindowDays;
            var inWindow = byDay.Where(kv => kv.Key > cutoff).ToList();
            if (inWindow.Count == 0) return 0f;

            return inWindow.Sum(kv => kv.Value) / inWindow.Count;
        }

        public float GetAverageDailyRevenue(string vendorId) => Average(_revenueByVendorByDay, vendorId);
        public float GetAverageDailyPallets(string vendorId) => Average(_palletsByVendorByDay, vendorId);

        /// <summary>Average dwell hours per DAY WITH A COMPLETED DELIVERY, not per calendar day in the
        /// window — a vendor that only delivers twice a week shouldn't read as "barely in the door"
        /// just because most days in the window have no entry at all.</summary>
        public float GetAverageDwellHours(string vendorId) => Average(_dwellHoursByVendorByDay, vendorId);

        private void OnDayChanged(string eventId, int day)
        {
            _currentDay = day;

            // Prunes days that have fallen out of the trailing window entirely, so the ledger doesn't
            // grow unbounded over a long session.
            int cutoff = _currentDay - TrailingWindowDays;
            foreach (var byDay in _revenueByVendorByDay.Values.Concat(_palletsByVendorByDay.Values)
                         .Concat(_dwellHoursByVendorByDay.Values))
            {
                var stale = byDay.Keys.Where(d => d <= cutoff).ToList();
                foreach (var d in stale) byDay.Remove(d);
            }
        }
    }
}
