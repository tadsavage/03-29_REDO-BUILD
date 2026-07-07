using GameCore.Services;
using GameCore.Economy;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles saving and loading of economy state: money, spending, hourly costs, payroll.
/// Prevents double-charging and double-payment on load.
/// </summary>
public static class EconomyPersistenceService
{
    /// <summary>
    /// Snapshot the current economy state.
    /// </summary>
    public static EconomyPersistenceData Snapshot()
    {
        var data = new EconomyPersistenceData();

        // Snapshot money service state
        var moneyService = ServiceLocator.Get<MoneyService>();
        if (moneyService != null)
        {
            data.money.capital = moneyService.CurrentCapital;
            data.money.spentToday = moneyService.SpentToday;
            data.money.spentTodayHourlyOnly = moneyService.GetSpentTodayHourlyOnly();

            var spentByCategory = moneyService.GetSpentTodayByCategory();
            data.money.spentTodayByObjectCategory = SerializableDictionary<string, int>.FromDictionary(spentByCategory);
        }

        // Snapshot economy service state (hourly costs)
        var economyService = ServiceLocator.Get<EconomyService>();
        if (economyService != null)
        {
            var hourlyByGLLine = economyService.GetHourlyByGLLine();
            var fractionalByGLLine = economyService.GetFractionalByGLLine();

            data.hourly.hourlyByGLLine = SerializableDictionary<string, int>.FromDictionary(hourlyByGLLine);
            data.hourly.fractionalByGLLine = SerializableDictionary<string, float>.FromDictionary(fractionalByGLLine);
        }

        // Snapshot payroll state (employees already paid today)
        var payrollService = ServiceLocator.Get<PayrollService>();
        if (payrollService != null)
        {
            data.payroll.employeesPaidToday = payrollService.GetEmployeesPaidToday() ?? new List<string>();
        }

        return data;
    }

    /// <summary>
    /// Restore economy state from a saved snapshot.
    /// Prevents double-payment and double-deduction of hourly costs.
    /// </summary>
    public static void Restore(EconomyPersistenceData data)
    {
        if (data == null)
            return;

        // Restore money service state
        var moneyService = ServiceLocator.Get<MoneyService>();
        if (moneyService != null && data.money != null)
        {
            moneyService.SetMoney(data.money.capital);
            moneyService.SetSpentToday(data.money.spentToday);
            moneyService.SetSpentTodayHourlyOnly(data.money.spentTodayHourlyOnly);

            if (data.money.spentTodayByObjectCategory != null)
            {
                var spentByCategory = data.money.spentTodayByObjectCategory.ToDictionary();
                moneyService.SetSpentTodayByCategory(spentByCategory);
            }
        }

        // Restore economy service state
        var economyService = ServiceLocator.Get<EconomyService>();
        if (economyService != null && data.hourly != null)
        {
            var hourlyByGLLine = data.hourly.hourlyByGLLine?.ToDictionary() ?? new Dictionary<string, int>();
            var fractionalByGLLine = data.hourly.fractionalByGLLine?.ToDictionary() ?? new Dictionary<string, float>();

            economyService.RestoreHourlyState(hourlyByGLLine, fractionalByGLLine);
        }

        // Restore payroll state (employees already paid)
        var payrollService = ServiceLocator.Get<PayrollService>();
        if (payrollService != null && data.payroll?.employeesPaidToday != null)
        {
            payrollService.RestorePaidList(data.payroll.employeesPaidToday);
        }

        Debug.Log("[EconomyPersistenceService] Restored economy state.");
    }
}
