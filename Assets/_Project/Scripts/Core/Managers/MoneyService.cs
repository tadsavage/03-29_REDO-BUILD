using System;
using System.Collections.Generic;
using UnityEngine;

public class MoneyService
{
    public int CurrentCapital  { get; private set; }
    public int TotalHourlyCost { get; private set; }
    public bool CanAfford(int amount) => CurrentCapital >= amount;

    public int Current => CurrentCapital;

    public int SpentToday { get; private set; }
    public Dictionary<string, int> CategorySpendingToday { get; private set; }

    // Running lifetime totals — not reset on day change, not yet persisted to save
    public Dictionary<string, int> LifetimeIncome   { get; private set; }
    public Dictionary<string, int> LifetimeExpenses { get; private set; }

    // Per-role wage totals — populated by TrackWageByRole when employee wages are paid.
    public Dictionary<string, int> WagesByRole { get; private set; }

    public event Action OnMoneyChanged;

    public float SellBackRate { get; private set; }

    public MoneyService(int startingCapital, float sellBackRate = 0.5f)
    {
        CurrentCapital        = startingCapital;
        SellBackRate          = sellBackRate;
        CategorySpendingToday = new Dictionary<string, int>();
        LifetimeIncome        = new Dictionary<string, int>();
        LifetimeExpenses      = new Dictionary<string, int>();
        WagesByRole           = new Dictionary<string, int>();

        TrackLifetimeIncome(startingCapital, FinanceCategory.Starting);
    }

    // ------------------------------------------------------------
    // Core Money Operations
    // ------------------------------------------------------------

    // Add income from a named revenue source (Case Pick, Storage, etc.)
    public void AddIncome(int amount, string category = FinanceCategory.MiscIncome)
    {
        CurrentCapital += amount;
        TrackLifetimeIncome(amount, category);
        OnMoneyChanged?.Invoke();
    }

    public void Deduct(int amount, string category = "General")
    {
        CurrentCapital -= amount;
        TrackSpending(amount, category);
        TrackLifetimeExpense(amount, category);
        OnMoneyChanged?.Invoke();
    }

    public void Refund(int amount, string category = "General")
    {
        CurrentCapital += amount;
        TrackSpending(-amount, category);
        // Refunds do NOT reduce category spending (AAA sims track spending, not net)
        OnMoneyChanged?.Invoke();
    }

    // ------------------------------------------------------------
    // Hourly Cost Tracking
    // ------------------------------------------------------------
    // Keyed by finance category (Maintenance, Groundskeeping, Electricity…)
    // so ApplyHourlyCost can distribute expenses to the correct lifetime buckets.
    private readonly Dictionary<string, int> _hourlyCostByCat   = new();
    // finance cat → (objData category → $/hr) for tooltip sub-breakdowns.
    private readonly Dictionary<string, Dictionary<string, int>> _hourlyCostDetail = new();
    // finance cat → (label → total paid lifetime) — accumulated in ApplyHourlyCost.
    private readonly Dictionary<string, Dictionary<string, int>> _lifetimeDetail   = new();

    public void AddHourlyCost(int amount,
        string financeCategory = FinanceCategory.Maintenance,
        string detail = "")
    {
        TotalHourlyCost += amount;

        if (!_hourlyCostByCat.ContainsKey(financeCategory)) _hourlyCostByCat[financeCategory] = 0;
        _hourlyCostByCat[financeCategory] += amount;

        if (!string.IsNullOrEmpty(detail))
        {
            if (!_hourlyCostDetail.ContainsKey(financeCategory))
                _hourlyCostDetail[financeCategory] = new Dictionary<string, int>();
            if (!_hourlyCostDetail[financeCategory].ContainsKey(detail))
                _hourlyCostDetail[financeCategory][detail] = 0;
            _hourlyCostDetail[financeCategory][detail] += amount;
        }

        OnMoneyChanged?.Invoke();
    }

    public void RemoveHourlyCost(int amount,
        string financeCategory = FinanceCategory.Maintenance,
        string detail = "")
    {
        TotalHourlyCost = Mathf.Max(0, TotalHourlyCost - amount);

        if (_hourlyCostByCat.ContainsKey(financeCategory))
            _hourlyCostByCat[financeCategory] = Mathf.Max(0, _hourlyCostByCat[financeCategory] - amount);

        if (!string.IsNullOrEmpty(detail)
            && _hourlyCostDetail.TryGetValue(financeCategory, out var det)
            && det.ContainsKey(detail))
            det[detail] = Mathf.Max(0, det[detail] - amount);

        OnMoneyChanged?.Invoke();
    }

    // Called by SimulationTimeService.OnHourChanged
    public void ApplyHourlyCost()
    {
        if (TotalHourlyCost == 0) { OnMoneyChanged?.Invoke(); return; }

        CurrentCapital -= TotalHourlyCost;
        TrackSpending(TotalHourlyCost, "Hourly Costs");

        foreach (var kvp in _hourlyCostByCat)
        {
            if (kvp.Value <= 0) continue;
            TrackLifetimeExpense(kvp.Value, kvp.Key);

            if (!_hourlyCostDetail.TryGetValue(kvp.Key, out var detail)) continue;
            if (!_lifetimeDetail.ContainsKey(kvp.Key))
                _lifetimeDetail[kvp.Key] = new Dictionary<string, int>();
            var bucket = _lifetimeDetail[kvp.Key];
            foreach (var d in detail)
            {
                if (!bucket.ContainsKey(d.Key)) bucket[d.Key] = 0;
                bucket[d.Key] += d.Value;
            }
        }

        OnMoneyChanged?.Invoke();
    }

    // Returns a snapshot of lifetime sub-detail for a finance category (for tooltips).
    public Dictionary<string, int> GetLifetimeDetail(string financeCategory)
        => _lifetimeDetail.TryGetValue(financeCategory, out var d)
            ? new Dictionary<string, int>(d)
            : null;

    // Track a wage payment by employee role name. Called by the employee system.
    public void TrackWageByRole(int amount, string roleName)
    {
        if (!WagesByRole.ContainsKey(roleName)) WagesByRole[roleName] = 0;
        WagesByRole[roleName] += amount;
    }

    // ------------------------------------------------------------
    // Daily Tracking
    // ------------------------------------------------------------

    private void TrackSpending(int amount, string category)
    {
        SpentToday += amount;
        if (!CategorySpendingToday.ContainsKey(category)) CategorySpendingToday[category] = 0;
        CategorySpendingToday[category] += amount;
    }

    private void TrackLifetimeIncome(int amount, string category)
    {
        if (!LifetimeIncome.ContainsKey(category)) LifetimeIncome[category] = 0;
        LifetimeIncome[category] += amount;
    }

    private void TrackLifetimeExpense(int amount, string category)
    {
        if (!LifetimeExpenses.ContainsKey(category)) LifetimeExpenses[category] = 0;
        LifetimeExpenses[category] += amount;
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
        SpentToday = amount;
        OnMoneyChanged?.Invoke();
    }
    public void SetSellBackRate(float rate)
    {
        SellBackRate = rate;
    }
}
