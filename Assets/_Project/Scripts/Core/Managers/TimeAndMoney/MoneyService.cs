using GameCore.Economy;
using UnityEngine;
using GameCore.Services;
using GameCore.Events;
using GameCore.Events.Payloads;

namespace GameCore.Economy
{
    /// <summary>
    /// Refactored MoneyService: Implements IMoneyService with event publishing.
    ///
    /// MAJOR CHANGES FROM ORIGINAL:
    /// 1. Implements IService interface (Initialize/Shutdown)
    /// 2. Publishes events on all balance changes
    /// 3. Tracks daily spending separately from total capital
    /// 4. Integrates sell-back rate from difficulty settings
    /// 5. No direct dependencies on UI; uses events instead
    ///
    /// RESPONSIBILITY:
    /// - Maintain player's capital balance
    /// - Track hourly operating costs
    /// - Calculate and apply refunds with difficulty scaling
    /// - Publish events for balance changes
    /// - Reset daily spending at midnight
    ///
    /// INTEGRATION POINTS:
    /// - Subscribes to: GameEvents.Time.OnDayChanged (reset daily spending)
    /// - Publishes: GameEvents.Economy.OnMoneyChanged, OnTransactionApplied, OnBankrupt
    /// - Used by: BuildService (cost deduction), EconomyService (hourly costs), UI (balance display)
    ///
    /// THREAD SAFETY: Not thread-safe. Assumes single-threaded game loop.
    /// </summary>
    public class MoneyService : IMoneyService
    {
        // ============ INTERNAL STATE ============
        private int _startingCapital;
        private float _sellBackRate;
        private int _currentCapital;
        private int _totalHourlyCost = 0;
        private int _spentToday = 0;
        private bool _isBankrupt = false;

        // Lifetime tracking for financial breakdown UI
        private System.Collections.Generic.Dictionary<string, int> _lifetimeIncome = new();
        private System.Collections.Generic.Dictionary<string, int> _lifetimeExpenses = new();

        // Per-category sub-breakdown (e.g. Wages -> EmployeeRole name -> amount), powers the
        // FinancialBreakdownPanel tooltips. Populated via RemoveCapital(amount, category, detailKey).
        private System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>> _lifetimeDetail = new();

        // Today's ONE-TIME object-purchase spending broken out by raw ObjDataSO.category
        // (build-menu category) — e.g. buying foundations/walls/vehicles. Populated by
        // MoneyService.Deduct() (the legacy wrapper every PlaceCommand/MoveCommand/DeleteCommand
        // one-time cost goes through) and by EconomyService's recurring hourly object costs.
        // Wages and the daily Lease charge intentionally don't appear here — they aren't
        // "purchases from the build menu." Reset daily.
        private System.Collections.Generic.Dictionary<string, int> _spentTodayByObjectCategory = new();

        // Today's RECURRING hourly-cost spending split into two categories:
        // - _spentTodayUpkeep: object maintenance costs only (from EconomyService)
        // - _spentTodayWages: wage costs only (from PayrollService)
        // Explicitly excludes one-time purchase costs and the daily Lease charge. Reset daily.
        private int _spentTodayUpkeep = 0;
        private int _spentTodayWages = 0;

        // Hourly tracking (current hour accumulating, last hour completed)
        private int _revenueThisHour = 0;
        private int _expensesThisHour = 0;
        private int _revenueLastHour = 0;
        private int _expensesLastHour = 0;

        // Daily totals (persisted for historical view)
        private int _revenueToday = 0;
        private int _expensesToday = 0;
        private int _revenueYesterday = 0;
        private int _expensesYesterday = 0;

        // Weekly total (accumulates across 7 days)
        private int _revenueThisWeek = 0;
        private int _expensesThisWeek = 0;

        private EventManager _eventManager;

        // ============ LEGACY EVENTS (Backward Compatibility) ============
        public event System.Action OnMoneyChanged;

        // ============ PROPERTIES (IMoneyService) ============
        public int CurrentCapital => _currentCapital;
        public bool IsBankrupt => _isBankrupt;
        public int TotalHourlyCost => _totalHourlyCost;
        public int SpentToday => _spentToday;
        public float SellBackRate => _sellBackRate;

        // Lifetime tracking properties (for financial breakdown UI)
        public System.Collections.Generic.Dictionary<string, int> LifetimeIncome => _lifetimeIncome;
        public System.Collections.Generic.Dictionary<string, int> LifetimeExpenses => _lifetimeExpenses;

        /// <summary>Read-only view of every category's full detail bucket — debug/diagnostic use.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.Dictionary<string, int>> LifetimeDetailAll => _lifetimeDetail;

        /// <summary>Today's object-purchase spending broken out by raw ObjDataSO.category (powers
        /// the Spent Today panel's "Purchases" list).</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, int> SpentTodayByObjectCategory => _spentTodayByObjectCategory;

        /// <summary>Today's object maintenance costs (from EconomyService hourly deductions).</summary>
        public int SpentTodayUpkeep => _spentTodayUpkeep;

        /// <summary>Today's wage costs (from PayrollService hourly deductions).</summary>
        public int SpentTodayWages => _spentTodayWages;

        /// <summary>Total of both upkeep and wages (convenience property for backward compat).</summary>
        public int SpentTodayHourlyOnly => _spentTodayUpkeep + _spentTodayWages;

        // ============ HOURLY METRICS ============
        public int RevenueThisHour => _revenueThisHour;
        public int ExpensesThisHour => _expensesThisHour;
        public int RevenueLastHour => _revenueLastHour;
        public int ExpensesLastHour => _expensesLastHour;

        // ============ DAILY METRICS ============
        public int RevenueToday => _revenueToday;
        public int ExpensesToday => _expensesToday;
        public int RevenueYesterday => _revenueYesterday;
        public int ExpensesYesterday => _expensesYesterday;

        // ============ WEEKLY METRICS ============
        public int RevenueThisWeek => _revenueThisWeek;
        public int ExpensesThisWeek => _expensesThisWeek;

        /// <summary>Records an amount already deducted elsewhere (this is bookkeeping only, NOT
        /// a second deduction) under a raw object-category tag for the Spent Today panel.</summary>
        public void RecordSpentTodayByObjectCategory(string category, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(category)) return;
            if (!_spentTodayByObjectCategory.ContainsKey(category)) _spentTodayByObjectCategory[category] = 0;
            _spentTodayByObjectCategory[category] += amount;
        }

        /// <summary>Records hourly upkeep cost (object maintenance) as bookkeeping (amount already
        /// deducted elsewhere). Used by EconomyService.</summary>
        public void RecordUpkeepSpend(int amount)
        {
            if (amount > 0) _spentTodayUpkeep += amount;
        }

        /// <summary>Records hourly wage cost as bookkeeping (amount already deducted elsewhere).
        /// Used by PayrollService.</summary>
        public void RecordWageSpend(int amount)
        {
            if (amount > 0) _spentTodayWages += amount;
        }

        /// <summary>Records an amount already deducted elsewhere (bookkeeping only) as a
        /// recurring hourly cost for the Spent Today panel (legacy, calls RecordUpkeepSpend).</summary>
        public void RecordHourlySpend(int amount)
        {
            RecordUpkeepSpend(amount);
        }

        // ============ CONSTRUCTOR ============
        public MoneyService(int startingCapital, float sellBackRate = 0.75f)
        {
            _startingCapital = startingCapital;
            _sellBackRate = sellBackRate;
            _currentCapital = startingCapital;
            _spentToday = 0;
            _isBankrupt = false;
        }

        // ============ LIFECYCLE ============

        public void Initialize()
        {
            //Debug.Log($"[MoneyService] Initializing with ${_startingCapital:N0} starting capital, {_sellBackRate * 100}% sell-back rate");

            _eventManager = EventManager.Instance;
            if (_eventManager == null)
            {
                Debug.LogError("[MoneyService] EventManager not found.");
                return;
            }

            _eventManager.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
            _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            _eventManager.Publish(GameEvents.Economy.OnMoneyChanged, _currentCapital);

            //Debug.Log("[MoneyService] Initialized.");
        }

        public void Shutdown()
        {
            Debug.Log("[MoneyService] Shutting down...");

            if (_eventManager != null)
            {
                _eventManager.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
                _eventManager.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            }

            Debug.Log("[MoneyService] Shut down complete.");
        }

        // ============ BALANCE OPERATIONS ============

        public bool CanAfford(int amount)
        {
            return _currentCapital >= amount;
        }

        public void AddCapital(int amount, string reason = "Income")
        {
            if (amount < 0)
            {
                Debug.LogError("[MoneyService] Cannot add negative capital. Use RemoveCapital instead.");
                return;
            }

            if (amount == 0)
            {
                return;
            }

            _currentCapital += amount;

            bool wasBankrupt = _isBankrupt;
            _isBankrupt = _currentCapital <= 0;

            if (wasBankrupt && !_isBankrupt)
            {
                Debug.Log($"[MoneyService] Player recovered from bankruptcy. Capital: ${_currentCapital:N0}");
            }

            PublishMoneyChanged(reason, amount);

            // Track lifetime income
            if (!_lifetimeIncome.ContainsKey(reason)) _lifetimeIncome[reason] = 0;
            _lifetimeIncome[reason] += amount;

            // Track hourly/daily/weekly revenue
            _revenueThisHour += amount;
            _revenueToday += amount;
            _revenueThisWeek += amount;
        }

        public void RemoveCapital(int amount, string reason = "Expense")
        {
            ApplyRemoval(amount, reason);
        }

        /// <summary>
        /// Remove capital under a category, exactly like RemoveCapital, but additionally records
        /// the amount under a sub-key (e.g. an EmployeeRole name) within that category's lifetime
        /// detail bucket — powers FinancialBreakdownPanel's per-category tooltips (GetLifetimeDetail).
        /// </summary>
        public void RemoveCapital(int amount, string category, string detailKey)
        {
            if (!ApplyRemoval(amount, category)) return;
            if (string.IsNullOrEmpty(detailKey)) return;

            if (!_lifetimeDetail.TryGetValue(category, out var detail))
            {
                detail = new System.Collections.Generic.Dictionary<string, int>();
                _lifetimeDetail[category] = detail;
            }

            if (!detail.ContainsKey(detailKey)) detail[detailKey] = 0;
            detail[detailKey] += amount;
        }

        /// <summary>Core deduction logic shared by both RemoveCapital overloads. Returns false
        /// (no-op) for invalid/zero amounts.</summary>
        private bool ApplyRemoval(int amount, string reason)
        {
            if (amount < 0)
            {
                Debug.LogError("[MoneyService] Cannot remove negative capital. Use AddCapital instead.");
                return false;
            }

            if (amount == 0)
            {
                return false;
            }

            _currentCapital -= amount;
            _spentToday += amount;

            bool wasBankrupt = _isBankrupt;
            _isBankrupt = _currentCapital <= 0;

            if (!wasBankrupt && _isBankrupt)
            {
                Debug.LogWarning($"[MoneyService] Player is now bankrupt! Capital: ${_currentCapital:N0}");
            }

            PublishMoneyChanged(reason, -amount);

            if (!wasBankrupt && _isBankrupt)
            {
                _eventManager?.Publish(GameEvents.Economy.OnBankrupt);
            }

            // Track lifetime expenses
            if (!_lifetimeExpenses.ContainsKey(reason)) _lifetimeExpenses[reason] = 0;
            _lifetimeExpenses[reason] += amount;

            // Track hourly/daily/weekly expenses
            _expensesThisHour += amount;
            _expensesToday += amount;
            _expensesThisWeek += amount;

            return true;
        }

        // ============ REFUND CALCULATION ============

        public int CalculateRefund(int originalCost, float refundPercentage = 1.0f)
        {
            refundPercentage = Mathf.Clamp01(refundPercentage);
            int refundAmount = Mathf.RoundToInt(originalCost * refundPercentage * _sellBackRate);
            return refundAmount;
        }

        // ============ DAILY SPENDING RESET ============

        public void ResetDailySpending()
        {
            Debug.Log($"[MoneyService] Resetting daily spending. Previous: ${_spentToday:N0} (Upkeep: ${_spentTodayUpkeep:N0}, Wages: ${_spentTodayWages:N0})");
            _spentToday = 0;
            _spentTodayByObjectCategory.Clear();
            _spentTodayUpkeep = 0;
            _spentTodayWages = 0;
            _eventManager?.Publish(GameEvents.Economy.OnSpentTodayChanged, _spentToday);
        }

        // ============ EVENT HANDLERS ============

        private void OnHourChanged(string eventId, int newHour)
        {
            // Move current hour to last hour
            _revenueLastHour = _revenueThisHour;
            _expensesLastHour = _expensesThisHour;

            // Reset current hour accumulators
            _revenueThisHour = 0;
            _expensesThisHour = 0;
        }

        private void OnDayChanged(string eventId, int newDay)
        {
            // Move today's totals to yesterday
            _revenueYesterday = _revenueToday;
            _expensesYesterday = _expensesToday;

            // Reset today's accumulators and weekly (if day 7 → day 1)
            _revenueToday = 0;
            _expensesToday = 0;

            // Reset weekly on day 1 (or every 7 days)
            if (newDay % 7 == 1)
            {
                _revenueThisWeek = 0;
                _expensesThisWeek = 0;
            }

            ResetDailySpending();
        }

        // ============ HELPER METHODS ============

        private void PublishMoneyChanged(string reason, int deltaAmount)
        {
            // Publish new GameEvents-based event
            _eventManager?.Publish(GameEvents.Economy.OnMoneyChanged, _currentCapital);

            // Invoke legacy event for backward compatibility
            OnMoneyChanged?.Invoke();

            TransactionData transaction = new TransactionData
            {
                Amount = deltaAmount,
                Reason = reason,
                Timestamp = (long)UnityEngine.Time.realtimeSinceStartupAsDouble
            };
            _eventManager?.Publish(GameEvents.Economy.OnTransactionApplied, transaction);

            if (deltaAmount < 0)
            {
                _eventManager?.Publish(GameEvents.Economy.OnSpentTodayChanged, _spentToday);
            }
        }

        // ============ LEGACY METHODS (Backward Compatibility) ============
        public void AddMoney(int amount)
        {
            AddCapital(amount, "Income");
        }

        public void RemoveMoney(int amount)
        {
            RemoveCapital(amount, "Expense");
        }

        /// <summary>DEBUG: Set money to exact amount (used by debug tools).</summary>
        public void SetMoney(int amount)
        {
            int delta = amount - _currentCapital;
            if (delta > 0)
            {
                AddCapital(delta, "Debug");
            }
            else if (delta < 0)
            {
                RemoveCapital(-delta, "Debug");
            }
        }

        /// <summary>DEBUG: Refund (add money) with reason (used by debug tools).</summary>
        public void Refund(int amount, string reason = "Refund")
        {
            AddCapital(amount, reason);
        }

        /// <summary>LEGACY: Add hourly cost (tracked per category).</summary>
        public void AddHourlyCost(int amount, string financeCategory = "Maintenance", string detail = "")
        {
            _totalHourlyCost += amount;
        }

        /// <summary>LEGACY: Remove hourly cost (tracked per category).</summary>
        public void RemoveHourlyCost(int amount, string financeCategory = "Maintenance", string detail = "")
        {
            _totalHourlyCost = Mathf.Max(0, _totalHourlyCost - amount);
        }

        /// <summary>LEGACY: Deduct capital for a one-time object purchase/cost adjustment
        /// (placement cost, move/delete refund reversal, etc). Every caller passes a real
        /// ObjDataSO.category string, so this is also the hook for the Spent Today panel's
        /// "Purchases" breakdown — see RecordSpentTodayByObjectCategory.</summary>
        public void Deduct(int amount, string category = "General")
        {
            RemoveCapital(amount, category);
            RecordSpentTodayByObjectCategory(category, amount);
        }

        /// <summary>LEGACY: Set spent today (for save/load).</summary>
        public void SetSpentToday(int amount)
        {
            _spentToday = amount;
        }

        /// <summary>LEGACY: Set sell-back rate (for save/load).</summary>
        public void SetSellBackRate(float rate)
        {
            _sellBackRate = rate;
        }

        /// <summary>Get lifetime detail by category (for financial breakdown UI tooltips).</summary>
        public System.Collections.Generic.Dictionary<string, int> GetLifetimeDetail(string financeCategory)
        {
            return _lifetimeDetail.TryGetValue(financeCategory, out var detail)
                ? detail
                : new System.Collections.Generic.Dictionary<string, int>();
        }

        // ============ PERSISTENCE (SAVE/LOAD) ============

        /// <summary>Get the hourly-only spending (for persistence).</summary>
        public int GetSpentTodayHourlyOnly()
        {
            return _spentTodayUpkeep + _spentTodayWages;
        }

        /// <summary>Get spending broken down by object category (for persistence).</summary>
        public System.Collections.Generic.Dictionary<string, int> GetSpentTodayByCategory()
        {
            return new System.Collections.Generic.Dictionary<string, int>(_spentTodayByObjectCategory);
        }

        /// <summary>Set hourly-only spending from save (for persistence).</summary>
        public void SetSpentTodayHourlyOnly(int amount)
        {
            _spentTodayUpkeep = amount / 2;
            _spentTodayWages = amount - _spentTodayUpkeep;
        }

        /// <summary>Set spending by category from save (for persistence).</summary>
        public void SetSpentTodayByCategory(System.Collections.Generic.Dictionary<string, int> dict)
        {
            _spentTodayByObjectCategory.Clear();
            if (dict != null)
            {
                foreach (var kvp in dict)
                    _spentTodayByObjectCategory[kvp.Key] = kvp.Value;
            }
        }
    }
}
