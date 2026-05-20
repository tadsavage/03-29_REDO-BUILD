using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TopRightUI_TimeMoney : MonoBehaviour
{
    [SerializeField] private TMP_Text uiText;

    private MoneyService money;
    private SimulationTimeService time;

    private readonly StringBuilder sb = new StringBuilder();

    public void Initialize(MoneyService moneyService, SimulationTimeService timeService)
    {
        money = moneyService;
        time = timeService;

        // Subscribe to events
        money.OnMoneyChanged += Refresh;
        time.OnTimeChanged += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        if (money != null) money.OnMoneyChanged -= Refresh;
        if (time != null) time.OnTimeChanged -= Refresh;
    }

    private void Refresh()
    {
        sb.Clear();

        sb.AppendLine($"Capital: ${money.CurrentCapital:N0}");
        sb.AppendLine($"Hourly Cost: ${money.TotalHourlyCost:N0}");
        sb.AppendLine($"Time: {time.Hour:00}:{time.Minute:00}");
        sb.AppendLine($"Day: {time.Day}");
        sb.AppendLine($"Spent Today: ${money.SpentToday:N0}");
        sb.AppendLine("");
        sb.AppendLine("Breakdown:");

        foreach (KeyValuePair<string, int> kvp in money.CategorySpendingToday)
            sb.AppendLine($" - {kvp.Key}: ${kvp.Value:N0}");

        uiText.text = sb.ToString();
    }
}
