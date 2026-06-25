using GameCore.Economy;
namespace GameCore.Economy
{
    using GameCore.Services;

    /// <summary>
    /// Interface defining the contract for all money/capital management.
    ///
    /// RATIONALE:
    /// - Separates "what a money system does" from "how it does it"
    /// - Allows swapping implementations (test vs. production)
    /// - Enables multiple money systems if needed (player budget + project budgets)
    /// - Forces explicit API (no hidden side effects)
    ///
    /// CORE RESPONSIBILITIES:
    /// - Track player's current capital (balance)
    /// - Add/remove capital (income/expenses)
    /// - Calculate refunds based on sell-back rate
    /// - Publish events when balance changes
    /// - Handle bankruptcy state
    ///
    /// EVENT EMISSION:
    /// - Publishes GameEvents.Economy.OnMoneyChanged on any balance change
    /// - Publishes GameEvents.Economy.OnTransactionApplied for detailed transaction logging
    /// - Publishes GameEvents.Economy.OnBankrupt when balance reaches 0 or below
    /// </summary>
    public interface IMoneyService : IService
    {
        // ============ BALANCE QUERIES ============

        /// <summary>Get the player's current capital balance (can be negative if bankrupt).</summary>
        int CurrentCapital { get; }

        /// <summary>Check if the player can afford a purchase.</summary>
        bool CanAfford(int amount);

        /// <summary>Check if the player is bankrupt (capital <= 0).</summary>
        bool IsBankrupt { get; }

        // ============ CAPITAL OPERATIONS ============

        /// <summary>Add capital to the player's balance (income, refund, bonus).</summary>
        void AddCapital(int amount, string reason = "Income");

        /// <summary>Remove capital from the player's balance (expense, cost, penalty).</summary>
        void RemoveCapital(int amount, string reason = "Expense");

        /// <summary>Calculate a refund amount based on sell-back rate and difficulty.</summary>
        int CalculateRefund(int originalCost, float refundPercentage = 1.0f);

        // ============ COST TRACKING ============

        /// <summary>Get the total hourly operating cost (all placed objects combined).</summary>
        int TotalHourlyCost { get; }

        /// <summary>Get the total amount spent so far today (since last midnight).</summary>
        int SpentToday { get; }

        /// <summary>Reset the "spent today" counter (called at midnight).</summary>
        void ResetDailySpending();

        // ============ SELL-BACK RATE ============

        /// <summary>Get the current sell-back rate (difficulty-dependent). Ranges 0.0 to 1.0.</summary>
        float SellBackRate { get; }
    }
}
