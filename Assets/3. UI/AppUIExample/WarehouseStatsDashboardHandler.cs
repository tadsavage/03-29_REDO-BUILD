using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using System.Collections;

public class WarehouseStatsDashboardHandler : MonoBehaviour
{
    private Label capitalLabel;
    private Label costLabel;
    private Label dayLabel;
    private Label timeLabel;
    private Label spentTodayLabel;
    private LinearProgress hourProgressBar;

    private MoneyService moneyService;
    private SimulationTimeService timeService;

    private void Start()
    {
        var uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

        // Find elements
        capitalLabel = root.Q<Label>("capitalValue");
        costLabel = root.Q<Label>("hourlyCostValue");
        dayLabel = root.Q<Label>("dayValue");
        timeLabel = root.Q<Label>("timeValue");
        spentTodayLabel = root.Q<Label>("spentTodayValue");
        hourProgressBar = root.Q<LinearProgress>("hourProgress");

        // Wait for GameContext to be initialized
        StartCoroutine(InitializeServices());
    }

    private IEnumerator InitializeServices()
    {
        while (moneyService == null)
        {
            var context = FindAnyObjectByType<GameContext>();
            if (context != null)
            {
                moneyService = context.MoneyService;
                timeService = context.TimeService;
            }
            else
            {
                var demo = FindAnyObjectByType<AppUIDemoManager>();
                if (demo != null)
                {
                    moneyService = demo.MoneyService;
                    timeService = demo.TimeService;
                }
            }
            yield return null;
        }

        // Register for updates
        moneyService.OnMoneyChanged += UpdateUI;
        timeService.OnTimeChanged += UpdateUI;

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (moneyService == null || timeService == null) return;

        if (capitalLabel != null)
            capitalLabel.text = $"${moneyService.CurrentCapital:N0}";

        if (costLabel != null)
            costLabel.text = $"-${moneyService.TotalHourlyCost:N0}";

        if (dayLabel != null)
            dayLabel.text = $"Day {timeService.Day}";

        if (timeLabel != null)
            timeLabel.text = $"{timeService.Hour:00}:{timeService.Minute:00}";

        if (spentTodayLabel != null)
            spentTodayLabel.text = $"${moneyService.SpentToday:N0}";

        if (hourProgressBar != null)
        {
            // Percentage of the current hour
            float progress = (timeService.Minute / 60f) * 100f;
            hourProgressBar.value = progress;
        }
    }

    private void OnDestroy()
    {
        if (moneyService != null) moneyService.OnMoneyChanged -= UpdateUI;
        if (timeService != null) timeService.OnTimeChanged -= UpdateUI;
    }
}
