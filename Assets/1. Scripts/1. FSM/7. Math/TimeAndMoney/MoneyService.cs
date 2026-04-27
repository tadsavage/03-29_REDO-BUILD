using System;
using System.Collections.Generic;
using UnityEngine;

public class MoneyService
{
    public int CurrentCapital { get; private set; }
    public int TotalHourlyCost { get; private set; }
    public bool CanAfford(int amount)
    {
        return CurrentCapital >= amount;
    }

    public int Current => CurrentCapital;

    public int SpentToday { get; private set; }
    public Dictionary<string, int> CategorySpendingToday { get; private set; }

    public event Action OnMoneyChanged;

    public MoneyService(int startingCapital)
    {
        CurrentCapital = startingCapital;
        CategorySpendingToday = new Dictionary<string, int>();
    }

    // ------------------------------------------------------------
    // Core Money Operations
    // ------------------------------------------------------------

    public void Deduct(int amount, string category = "General")
    {
        CurrentCapital -= amount;

        TrackSpending(amount, category);

        OnMoneyChanged?.Invoke();
    }

    public void Refund(int amount, string category = "General")
    {
        CurrentCapital += amount;
        TrackSpending(-amount, category);
        // Refunds do NOT reduce category spending
        // (AAA sims track spending, not net)
        OnMoneyChanged?.Invoke();
    }

    // ------------------------------------------------------------
    // Hourly Cost Tracking
    // ------------------------------------------------------------

    public void AddHourlyCost(int amount)
    {
        TotalHourlyCost += amount;
        OnMoneyChanged?.Invoke();
    }

    public void RemoveHourlyCost(int amount)
    {
        TotalHourlyCost -= amount;
        if (TotalHourlyCost < 0)
            TotalHourlyCost = 0;

        OnMoneyChanged?.Invoke();
    }

    // Called by SimulationTimeService.OnHourChanged
    public void ApplyHourlyCost()
    {
        CurrentCapital -= TotalHourlyCost;

        TrackSpending(TotalHourlyCost, "Hourly Costs");

        OnMoneyChanged?.Invoke();
    }

    // ------------------------------------------------------------
    // Daily Tracking
    // ------------------------------------------------------------

    private void TrackSpending(int amount, string category)
    {
        SpentToday += amount;

        if (!CategorySpendingToday.ContainsKey(category))
            CategorySpendingToday[category] = 0;

        CategorySpendingToday[category] += amount;
    }

    // Called by SimulationTimeService.OnDayChanged
    public void ResetDailySpending()
    {
        SpentToday = 0;
        CategorySpendingToday.Clear();

        OnMoneyChanged?.Invoke();
    }
    public void SetMoney(int amount)
    {
        CurrentCapital = amount;
        OnMoneyChanged?.Invoke();
    }
    public void SetSpentToday(int amount)
    {
        //Debug.Log($"Setting SpentToday to: {amount}");
        SpentToday = amount;
        OnMoneyChanged?.Invoke();
    }
}
