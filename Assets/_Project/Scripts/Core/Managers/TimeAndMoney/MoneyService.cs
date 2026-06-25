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
            Debug.Log($"[MoneyService] Initializing with ${_startingCapital:N0} starting capital, {_sellBackRate * 100}% sell-back rate");

            _eventManager = EventManager.Instance;
            if (_eventManager == null)
            {
                Debug.LogError("[MoneyService] EventManager not found.");
                return;
            }

            _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            _eventManager.Publish(GameEvents.Economy.OnMoneyChanged, _currentCapital);

            Debug.Log("[MoneyService] Initialized.");
        }

        public void Shutdown()
        {
            Debug.Log("[MoneyService] Shutting down...");

            if (_eventManager != null)
            {
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
        }

        public void RemoveCapital(int amount, string reason = "Expense")
        {
            if (amount < 0)
            {
                Debug.LogError("[MoneyService] Cannot remove negative capital. Use AddCapital instead.");
                return;
            }

            if (amount == 0)
            {
                return;
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
            Debug.Log($"[MoneyService] Resetting daily spending. Previous: ${_spentToday:N0}");
            _spentToday = 0;
            _eventManager?.Publish(GameEvents.Economy.OnSpentTodayChanged, _spentToday);
        }

        // ============ EVENT HANDLERS ============

        private void OnDayChanged(string eventId, int newDay)
        {
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

        /// <summary>LEGACY: Deduct capital (expense with category tracking).</summary>
        public void Deduct(int amount, string category = "General")
        {
            RemoveCapital(amount, category);
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

        /// <summary>LEGACY: Get lifetime detail by category (for financial breakdown UI).</summary>
        public System.Collections.Generic.Dictionary<string, int> GetLifetimeDetail(string financeCategory)
        {
            // Stub - returns empty for now
            return new System.Collections.Generic.Dictionary<string, int>();
        }
    }
}
