using UnityEngine;
using UnityEngine.UIElements;
using SaveLoadSystem;

public class TopBarUI : MonoBehaviour
{
    private Label _money;
    private Label _hourly;
    private Label _spent;
    private Label _time;

    private Label _fps;
    private Label _cell;

    private Button _saveButton;
    private Button _loadButton;

    private float _fpsTimer;
    private int _frames;

    private MoneyService _moneyService;
    private SimulationTimeService _timeService;
    private SaveLoadWindowController _saveLoadController;

    public void Init(UIDocument doc, MoneyService money, SimulationTimeService time, SaveLoadWindowController saveLoad)
    {
        _moneyService = money;
        _timeService = time;
        _saveLoadController = saveLoad;

        var root = doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;  // full-screen root must not block game raycasts

        // HUD root (the container you created in HUD.uxml)
        var hudRoot = root.Q<VisualElement>("Root");
        // TopBar is inside the TemplateContainer inside Root
        var topBar = hudRoot.Q<VisualElement>("TopBar");
        if (hudRoot == null)
        {
            Debug.LogError("HUD Root not found!");
            return;
        }
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

        _fps = topBar.Q<Label>("FPSLabel");
        _cell = topBar.Q<Label>("CellLabel");

        _saveButton = topBar.Q<Button>("SaveButton");
        _loadButton = topBar.Q<Button>("LoadButton");

        // Safety check
        if (_money == null || _hourly == null || _spent == null || _time == null || _fps == null || _cell == null)
        {
            Debug.LogError("One or more TopBar labels are missing.");
            return;
        }

        if (_saveButton != null) _saveButton.clicked += () => _saveLoadController?.Open(SaveLoadMode.Save);
        if (_loadButton != null) _loadButton.clicked += () => _saveLoadController?.Open(SaveLoadMode.Load);

        // Hook events
        _moneyService.OnMoneyChanged += Refresh;
        _timeService.OnTimeChanged += Refresh;

        Refresh();
    }
    private void Update()
    {
        UpdateFPS();
    }

    private void UpdateFPS()
    {
        _frames++;
        _fpsTimer += Time.deltaTime;

        if (_fpsTimer >= 0.5f)
        {
            int fps = Mathf.RoundToInt(_frames / _fpsTimer);
            if (_fps != null) _fps.text = $"FPS: {fps}";
            _frames = 0;
            _fpsTimer = 0f;
        }
    }

    public void SetState(string stateName)
    {
        // State label removed from UI
    }

    public void SetCell(int x, int y)
    {
        if (_cell != null) _cell.text = $"Cell: ({x},{y})";
    }

    private int _lastMoney = -1;
    private int _lastHourly = -1;
    private int _lastSpent = -1;
    private int _lastMinute = -1;
    private int _lastHour = -1;
    private int _lastDay = -1;

    private void Refresh()
    {
        if (_moneyService == null || _timeService == null) return;

        bool moneyChanged = _moneyService.CurrentCapital != _lastMoney || 
                            _moneyService.TotalHourlyCost != _lastHourly || 
                            _moneyService.SpentToday != _lastSpent;

        bool timeChanged = _timeService.Minute != _lastMinute || 
                           _timeService.Hour != _lastHour || 
                           _timeService.Day != _lastDay;

        if (moneyChanged)
        {
            _lastMoney = _moneyService.CurrentCapital;
            _lastHourly = _moneyService.TotalHourlyCost;
            _lastSpent = _moneyService.SpentToday;

            _money.text = $"Capital: ${_lastMoney:N0}";
            _hourly.text = $"Hourly: ${_lastHourly:N0}";
            _spent.text = $"Spent Today: ${_lastSpent:N0}";
        }

        if (timeChanged)
        {
            _lastMinute = _timeService.Minute;
            _lastHour = _timeService.Hour;
            _lastDay = _timeService.Day;

            _time.text = $"Time: {_lastHour:00}:{_lastMinute:00}  Day {_lastDay}";
        }
    }
    private void OnDestroy()
    {
        if (_moneyService != null)
            _moneyService.OnMoneyChanged -= Refresh;

        if (_timeService != null)
            _timeService.OnTimeChanged -= Refresh;
    }

}
