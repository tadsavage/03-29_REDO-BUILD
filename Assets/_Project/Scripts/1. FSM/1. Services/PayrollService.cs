using UnityEngine;
using GameCore.Services;
using GameCore.Events;

namespace GameCore.Economy
{
    /// <summary>
    /// Pays every active, player-hired employee their hourly wage once per in-game hour tick.
    ///
    /// RESPONSIBILITY:
    /// - On GameEvents.Time.OnHourChanged, deduct one hour of wage per Active, non-system-managed
    ///   EmployeeIdentity in EmployeeRegistry, tagged under FinanceCategory.Wages.
    /// - Accumulate each employee's lifetime EmployeeRecord.totalWagesPaid.
    ///
    /// Deliberately separate from the generic AddHourlyCost/RemoveHourlyCost pool that
    /// PlaceCommand/DeleteCommand use for building upkeep — wages get their own clean,
    /// per-employee, per-hour ledger entry instead of being merged into an anonymous
    /// per-minute pool. A terminated employee is unregistered from EmployeeRegistry
    /// immediately (EmployeeTerminationService), so they stop being paid the moment
    /// they're removed — no separate "stop paying" step needed here.
    ///
    /// INTEGRATION POINTS:
    /// - Subscribes to: Time.OnHourChanged
    /// - Reads: EmployeeRegistry.All
    /// - Writes: MoneyService.RemoveCapital, EmployeeRecord.totalWagesPaid
    /// </summary>
    public class PayrollService : IService
    {
        // Concrete MoneyService, not IMoneyService — needs the RemoveCapital(amount, category,
        // detailKey) overload that records per-role wage breakdown for FinancialBreakdownPanel.
        private MoneyService _moneyService;
        private EventManager _eventManager;
        private System.Collections.Generic.HashSet<string> _employeesPaidToday = new();

        public void Initialize()
        {

            _eventManager = EventManager.Instance;
            if (_eventManager == null)
            {
                Debug.LogError("[PayrollService] EventManager not found.");
                return;
            }

            _moneyService = ServiceLocator.Get<MoneyService>();
            if (_moneyService == null)
            {
                Debug.LogError("[PayrollService] MoneyService not found.");
                return;
            }

            _eventManager.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);

        }

        public void Shutdown()
        {
            Debug.Log("[PayrollService] Shutting down...");

            if (_eventManager != null)
            {
                _eventManager.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
            }

            Debug.Log("[PayrollService] Shut down complete.");
        }

        private void OnHourChanged(string eventId, int newHour)
        {
            if (_moneyService == null || EmployeeRegistry.Instance == null)
                return;

            foreach (var identity in EmployeeRegistry.Instance.All)
            {
                if (identity == null || identity.SystemManaged)
                    continue;

                var record = identity.Record;
                if (record == null || record.status != EmploymentStatus.Active)
                    continue;

                float rate = record.hourlyWage;
                // Working past their own scheduled shift window — pay 1.5x for that hour.
                // Flexible-shift employees have no fixed window and are never overtime.
                if (ShiftSchedule.IsOvertime(record.shift, newHour))
                    rate *= 1.5f;

                int wage = Mathf.RoundToInt(rate);
                if (wage <= 0)
                    continue;

                var (topCategory, detailKey) = FinanceCategory.ForWageGLLine(record.role);
                _moneyService.RemoveCapital(wage, topCategory, detailKey);
                record.totalWagesPaid += wage;

                // Track that this employee was paid (for save/load prevention of double-payment)
                if (!string.IsNullOrEmpty(record.employeeGuid))
                    _employeesPaidToday.Add(record.employeeGuid);

                _moneyService.RecordWageSpend(wage);
            }
        }

        // ============ PERSISTENCE (SAVE/LOAD) ============

        /// <summary>Get list of employee GUIDs already paid in this in-game day.</summary>
        public System.Collections.Generic.List<string> GetEmployeesPaidToday()
        {
            return new System.Collections.Generic.List<string>(_employeesPaidToday);
        }

        /// <summary>Restore the list of employees already paid today (prevents double-payment on load).</summary>
        public void RestorePaidList(System.Collections.Generic.List<string> employeeGuids)
        {
            _employeesPaidToday.Clear();
            if (employeeGuids != null)
            {
                foreach (var guid in employeeGuids)
                    _employeesPaidToday.Add(guid);
            }
        }
    }
}
