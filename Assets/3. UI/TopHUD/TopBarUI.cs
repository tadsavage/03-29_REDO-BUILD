using UnityEngine;
using UnityEngine.UIElements;

public class TopBarUI : MonoBehaviour
{
    private Label _money;
    private Label _hourly;
    private Label _spent;
    private Label _time;

    private MoneyService _moneyService;
    private SimulationTimeService _timeService;

    public void Init(UIDocument doc, MoneyService money, SimulationTimeService time)
    {
        
        _moneyService = money;
        _timeService = time;

        var root = doc.rootVisualElement;

        // HUD root (the container you created in HUD.uxml)
        var hudRoot = root.Q<VisualElement>("Root");
        if (hudRoot == null)
        {
            Debug.LogError("HUD Root not found!");
            return;
        }

        // TopBar is inside the TemplateContainer inside Root
        var topBar = hudRoot.Q<VisualElement>("TopBar");

        if (topBar == null)
        {
            Debug.LogError("TopBar element not found inside HUD Root.");
            return;
        }

        // Query labels inside TopBar
        _money = topBar.Q<Label>("MoneyLabel");
        _hourly = topBar.Q<Label>("HourlyLabel");
        _spent = topBar.Q<Label>("SpentLabel");
        _time = topBar.Q<Label>("TimeLabel");

        // Safety check
        if (_money == null || _hourly == null || _spent == null || _time == null)
        {
            Debug.LogError("One or more TopBar labels are missing.");
            return;
        }

        // Hook events
        _moneyService.OnMoneyChanged += Refresh;
        _timeService.OnTimeChanged += Refresh;

        Refresh();
    }



    private void OnDestroy()
    {
        if (_moneyService != null)
            _moneyService.OnMoneyChanged -= Refresh;

        if (_timeService != null)
            _timeService.OnTimeChanged -= Refresh;
    }

    private void Refresh()
    {
        _money.text = $"Capital: ${_moneyService.CurrentCapital:N0}";
        _hourly.text = $"Hourly: ${_moneyService.TotalHourlyCost:N0}";
        _spent.text = $"Spent Today: ${_moneyService.SpentToday:N0}";
        _time.text = $"Time: {_timeService.Hour:00}:{_timeService.Minute:00}  Day {_timeService.Day}";
    }
}
