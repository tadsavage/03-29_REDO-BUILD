using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
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
    private Button _mainMenuButton;

    private VisualElement _menuOverlay;
    private Button _menuMainMenuButton;
    private Button _menuSaveButton;
    private Button _menuResumeButton;
    private Button _menuExitButton;

    private float _fpsTimer;
    private int _frames;

    private MoneyService _moneyService;
    private SimulationTimeService _timeService;
    private SaveLoadWindowController _saveLoadController;

    private bool _menuOpen = false;
    private bool _pendingGoToMenu = false;
    private bool _saveWindowWasOpen = false;

    public void Init(UIDocument doc, MoneyService money, SimulationTimeService time, SaveLoadWindowController saveLoad)
    {
        _moneyService = money;
        _timeService = time;
        _saveLoadController = saveLoad;

        var root = doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;

        var hudRoot = root.Q<VisualElement>("Root");
        var topBar = hudRoot?.Q<VisualElement>("TopBar");

        if (hudRoot == null) { Debug.LogError("HUD Root not found!"); return; }
        if (topBar == null)  { Debug.LogError("TopBar not found inside HUD Root."); return; }

        _money  = topBar.Q<Label>("MoneyLabel");
        _hourly = topBar.Q<Label>("HourlyLabel");
        _spent  = topBar.Q<Label>("SpentLabel");
        _time   = topBar.Q<Label>("TimeLabel");
        _fps    = topBar.Q<Label>("FPSLabel");
        _cell   = topBar.Q<Label>("CellLabel");

        if (_money == null || _hourly == null || _spent == null ||
            _time  == null || _fps   == null || _cell  == null)
        {
            Debug.LogError("One or more TopBar labels are missing.");
            return;
        }

        _saveButton      = topBar.Q<Button>("SaveButton");
        _loadButton      = topBar.Q<Button>("LoadButton");
        _mainMenuButton  = topBar.Q<Button>("MainMenuButton");

        _menuOverlay      = hudRoot.Q<VisualElement>("menu-overlay");
        _menuMainMenuButton = hudRoot.Q<Button>("MenuMainMenuButton");
        _menuSaveButton     = hudRoot.Q<Button>("MenuSaveButton");
        _menuResumeButton   = hudRoot.Q<Button>("MenuResumeButton");
        _menuExitButton     = hudRoot.Q<Button>("MenuExitButton");

        if (_saveButton       != null) _saveButton.clicked       += () => _saveLoadController?.Open(SaveLoadMode.Save);
        if (_loadButton       != null) _loadButton.clicked       += () => _saveLoadController?.Open(SaveLoadMode.Load);
        if (_mainMenuButton   != null) _mainMenuButton.clicked   += ToggleMenuPopup;
        if (_menuMainMenuButton != null) _menuMainMenuButton.clicked += OnMenuDirectToMainMenu;
        if (_menuSaveButton   != null) _menuSaveButton.clicked   += OnMenuSave;
        if (_menuResumeButton != null) _menuResumeButton.clicked += CloseMenuPopup;
        if (_menuExitButton   != null) _menuExitButton.clicked   += OnExitGame;

        if (SaveManager.Instance != null)
            SaveManager.Instance.OnSaveCompleted += OnSaveCompleted;

        _moneyService.OnMoneyChanged += Refresh;
        _timeService.OnTimeChanged   += Refresh;

        Refresh();
    }

    private void Update()
    {
        UpdateFPS();

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            ToggleMenuPopup();

        // Reset pending-go-to-menu if user closed save window without saving
        if (_pendingGoToMenu && _saveLoadController != null)
        {
            bool windowOpen = _saveLoadController.IsOpen;
            if (_saveWindowWasOpen && !windowOpen)
                _pendingGoToMenu = false;
            _saveWindowWasOpen = windowOpen;
        }
    }

    // ── Menu popup ────────────────────────────────────────────────

    private void ToggleMenuPopup()
    {
        if (_menuOpen) CloseMenuPopup();
        else           OpenMenuPopup();
    }

    private void OpenMenuPopup()
    {
        if (_menuOverlay == null) return;
        _menuOpen = true;
        _menuOverlay.style.display = DisplayStyle.Flex;
        _menuOverlay.pickingMode   = PickingMode.Position;
    }

    private void CloseMenuPopup()
    {
        if (_menuOverlay == null) return;
        _menuOpen = false;
        _menuOverlay.style.display = DisplayStyle.None;
        _menuOverlay.pickingMode   = PickingMode.Ignore;
    }

    private void OnMenuDirectToMainMenu()
    {
        CloseMenuPopup();
        GoToMainMenu();
    }

    private void OnMenuSave()
    {
        CloseMenuPopup();
        _pendingGoToMenu   = true;
        _saveWindowWasOpen = false;
        _saveLoadController?.Open(SaveLoadMode.Save);
    }

    private void OnExitGame()
    {
        CloseMenuPopup();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnSaveCompleted(int _)
    {
        if (!_pendingGoToMenu) return;
        _pendingGoToMenu = false;
        GoToMainMenu();
    }

    private void GoToMainMenu()
    {
        MainMenuManager.SkipIntro = true;
        SceneManager.LoadScene("MainMenu");
    }

    // ── TopBar labels ─────────────────────────────────────────────

    private void UpdateFPS()
    {
        _frames++;
        _fpsTimer += Time.deltaTime;
        if (_fpsTimer >= 0.5f)
        {
            if (_fps != null) _fps.text = $"FPS: {Mathf.RoundToInt(_frames / _fpsTimer)}";
            _frames = 0;
            _fpsTimer = 0f;
        }
    }

    public void SetState(string stateName) { }

    public void SetCell(int x, int y)
    {
        if (_cell != null) _cell.text = $"Cell: ({x},{y})";
    }

    private int _lastMoney = -1, _lastHourly = -1, _lastSpent = -1;
    private int _lastMinute = -1, _lastHour = -1, _lastDay = -1;

    private void Refresh()
    {
        if (_moneyService == null || _timeService == null) return;

        if (_moneyService.CurrentCapital  != _lastMoney  ||
            _moneyService.TotalHourlyCost != _lastHourly ||
            _moneyService.SpentToday      != _lastSpent)
        {
            _lastMoney  = _moneyService.CurrentCapital;
            _lastHourly = _moneyService.TotalHourlyCost;
            _lastSpent  = _moneyService.SpentToday;
            _money.text = $"Capital: ${_lastMoney:N0}";
            _hourly.text = $"Hourly: ${_lastHourly:N0}";
            _spent.text  = $"Spent Today: ${_lastSpent:N0}";
        }

        if (_timeService.Minute != _lastMinute ||
            _timeService.Hour   != _lastHour   ||
            _timeService.Day    != _lastDay)
        {
            _lastMinute = _timeService.Minute;
            _lastHour   = _timeService.Hour;
            _lastDay    = _timeService.Day;
            _time.text  = $"Time: {_lastHour:00}:{_lastMinute:00}  Day {_lastDay}";
        }
    }

    private void OnDestroy()
    {
        if (_moneyService != null) _moneyService.OnMoneyChanged -= Refresh;
        if (_timeService  != null) _timeService.OnTimeChanged   -= Refresh;
        if (SaveManager.Instance != null)
            SaveManager.Instance.OnSaveCompleted -= OnSaveCompleted;
    }
}
