using System.Collections.Generic;
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
    private Label _cell;

    private Button _saveButton;
    private Button _loadButton;
    private Button _mainMenuButton;

    private VisualElement _menuOverlay;
    private Button _menuMainMenuButton;
    private Button _menuSettingsButton;
    private Button _menuSaveButton;
    private Button _menuLoadButton;
    private Button _menuResumeButton;
    private Button _menuExitButton;

    // In-game settings
    private VisualElement _igSettingsOverlay;
    private VisualElement _igResConfirmOverlay;
    private Button _igBtnScreamin, _igBtnGood, _igBtnToaster;
    private Button _igBtnClerk, _igBtnSupervisor, _igBtnManager;
    private DropdownField _igResolutionDropdown;
    private Slider _igGameVolumeSlider, _igMusicVolumeSlider;
    private int _igCurrentResIdx = 1;
    private int _igPendingResIdx = -1;

    private static readonly Resolution[] CommonResolutions =
    {
        new Resolution { width = 1280, height = 720  },
        new Resolution { width = 1920, height = 1080 },
        new Resolution { width = 2560, height = 1440 },
        new Resolution { width = 3840, height = 2160 },
    };

    private const string GameVolParam  = "GameVolume";
    private const string MusicVolParam = "MusicVolume";

    // Game-speed (time advancer) buttons.
    private Button _spdPause, _spd1x, _spd2x, _spd3x;

    private MoneyService _moneyService;
    private SimulationTimeService _timeService;
    private FinancialBreakdownPanel _breakdownPanel;
    private SaveLoadWindowController _saveLoadController;
    private EmployeeInfoUI _employeeInfoUI;   // cached for Escape priority (close card before pause)

    private bool _menuOpen = false;
    private bool _pendingGoToMenu = false;
    private bool _pendingCloseAfterSave = false;
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
        _cell   = topBar.Q<Label>("CellLabel");

        if (_money == null || _hourly == null || _spent == null ||
            _time  == null || _cell  == null)
        {
            Debug.LogError("One or more TopBar labels are missing.");
            return;
        }

        _breakdownPanel = new FinancialBreakdownPanel(root, _moneyService);
        _money.RegisterCallback<ClickEvent>(_ => _breakdownPanel.Toggle());

        // FPS moved to the draggable DevHudWindow (F8). The freed top-bar slot now holds
        // the game-speed control (Pause / 1× / 2× / 3×).
        WireSpeedControl(topBar);

        _saveButton      = topBar.Q<Button>("SaveButton");
        _loadButton      = topBar.Q<Button>("LoadButton");
        _mainMenuButton  = topBar.Q<Button>("MainMenuButton");

        _menuOverlay        = hudRoot.Q<VisualElement>("menu-overlay");
        _menuMainMenuButton = hudRoot.Q<Button>("MenuMainMenuButton");
        _menuSettingsButton = hudRoot.Q<Button>("MenuSettingsButton");
        _menuSaveButton     = hudRoot.Q<Button>("MenuSaveButton");
        _menuLoadButton     = hudRoot.Q<Button>("MenuLoadButton");
        _menuResumeButton   = hudRoot.Q<Button>("MenuResumeButton");
        _menuExitButton     = hudRoot.Q<Button>("MenuExitButton");

        // In-game settings
        _igSettingsOverlay    = hudRoot.Q<VisualElement>("in-game-settings-overlay");
        _igResConfirmOverlay  = hudRoot.Q<VisualElement>("ig-res-confirm-overlay");
        _igBtnScreamin        = hudRoot.Q<Button>("ig-btn-screamin");
        _igBtnGood            = hudRoot.Q<Button>("ig-btn-good");
        _igBtnToaster         = hudRoot.Q<Button>("ig-btn-toaster");
        _igBtnClerk           = hudRoot.Q<Button>("ig-btn-diff-clerk");
        _igBtnSupervisor      = hudRoot.Q<Button>("ig-btn-diff-supervisor");
        _igBtnManager         = hudRoot.Q<Button>("ig-btn-diff-manager");
        _igResolutionDropdown = hudRoot.Q<DropdownField>("ig-resolution-dropdown");
        _igGameVolumeSlider   = hudRoot.Q<Slider>("ig-slider-game-volume");
        _igMusicVolumeSlider  = hudRoot.Q<Slider>("ig-slider-music-volume");

        if (_saveButton           != null) _saveButton.clicked           += () => _saveLoadController?.Open(SaveLoadMode.Save);
        if (_loadButton           != null) _loadButton.clicked           += () => _saveLoadController?.Open(SaveLoadMode.Load);
        if (_mainMenuButton       != null) _mainMenuButton.clicked       += ToggleMenuPopup;
        if (_menuMainMenuButton   != null) _menuMainMenuButton.clicked   += OnMenuDirectToMainMenu;
        if (_menuSettingsButton   != null) _menuSettingsButton.clicked   += OnMenuSettings;
        if (_menuSaveButton       != null) _menuSaveButton.clicked       += OnMenuSave;
        if (_menuLoadButton       != null) _menuLoadButton.clicked       += OnMenuLoad;
        if (_menuResumeButton     != null) _menuResumeButton.clicked     += CloseMenuPopup;
        if (_menuExitButton       != null) _menuExitButton.clicked       += OnExitGame;

        // In-game settings wiring
        _igBtnScreamin?.RegisterCallback<ClickEvent>(_ => IgApplyGraphicsPreset("Ultra"));
        _igBtnGood?.RegisterCallback<ClickEvent>(_ => IgApplyGraphicsPreset("Good"));
        _igBtnToaster?.RegisterCallback<ClickEvent>(_ => IgApplyGraphicsPreset("Toaster"));
        _igBtnClerk?.RegisterCallback<ClickEvent>(_ => IgApplyDifficulty(0));
        _igBtnSupervisor?.RegisterCallback<ClickEvent>(_ => IgApplyDifficulty(1));
        _igBtnManager?.RegisterCallback<ClickEvent>(_ => IgApplyDifficulty(2));
        hudRoot.Q<Button>("ig-settings-done")?.RegisterCallback<ClickEvent>(_ => CloseSettings());
        hudRoot.Q<Button>("ig-btn-res-yes")?.RegisterCallback<ClickEvent>(_ => IgOnResolutionAccepted());
        hudRoot.Q<Button>("ig-btn-res-no")?.RegisterCallback<ClickEvent>(_ => IgOnResolutionCancelled());

        if (_igGameVolumeSlider  != null) _igGameVolumeSlider.RegisterValueChangedCallback(evt => IgSetVolume(GameVolParam, evt.newValue));
        if (_igMusicVolumeSlider != null) _igMusicVolumeSlider.RegisterValueChangedCallback(evt => IgSetVolume(MusicVolParam, evt.newValue));

        IgBuildResolutionDropdown();
        IgApplyStoredSettings();

        if (SaveManager.Instance != null)
            SaveManager.Instance.OnSaveCompleted += OnSaveCompleted;

        _moneyService.OnMoneyChanged += Refresh;
        _timeService.OnTimeChanged   += Refresh;

        Refresh();
    }

    private void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // Save/load window takes priority: Escape closes it (and ensures
            // the pause menu doesn't pop back open in the same press) instead
            // of toggling the pause menu.
            if (_saveLoadController != null && _saveLoadController.IsOpen)
            {
                _saveLoadController.Close();
                _pendingGoToMenu = false;
                _pendingCloseAfterSave = false;
                CloseMenuPopup();
            }
            else if (EmployeeCardOpen())
            {
                // Escape backs out of the open employee card first (which also drops the
                // outline/camera-follow focus via the card's Hide → DropFocus). Only when no
                // card is open does Escape fall through to the pause menu.
                _employeeInfoUI.Hide();
            }
            else
            {
                ToggleMenuPopup();
            }
        }

        // Reset pending flags if user closed save window without saving
        if ((_pendingGoToMenu || _pendingCloseAfterSave) && _saveLoadController != null)
        {
            bool windowOpen = _saveLoadController.IsOpen;
            if (_saveWindowWasOpen && !windowOpen)
            {
                _pendingGoToMenu = false;
                _pendingCloseAfterSave = false;
            }
            _saveWindowWasOpen = windowOpen;
        }
    }

    // ── Menu popup ────────────────────────────────────────────────

    /// <summary>True if the employee info card is currently open (cached lookup).</summary>
    private bool EmployeeCardOpen()
    {
        if (_employeeInfoUI == null) _employeeInfoUI = FindAnyObjectByType<EmployeeInfoUI>();
        return _employeeInfoUI != null && _employeeInfoUI.IsVisible;
    }

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
        _pendingCloseAfterSave = true;
        _saveWindowWasOpen     = false;
        _saveLoadController?.Open(SaveLoadMode.Save);
    }

    private void OnMenuLoad()
    {
        CloseMenuPopup();
        _saveLoadController?.Open(SaveLoadMode.Load);
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
        if (_pendingGoToMenu)
        {
            _pendingGoToMenu = false;
            GoToMainMenu();
            return;
        }

        if (_pendingCloseAfterSave)
        {
            _pendingCloseAfterSave = false;
            _saveLoadController?.Close();
        }
    }

    private void GoToMainMenu()
    {
        MainMenuManager.SkipIntro = true;
        SceneManager.LoadScene("MainMenu");
    }

    // ── TopBar labels ─────────────────────────────────────────────

    // ── Game speed (time advancer) ────────────────────────────────
    private void WireSpeedControl(VisualElement topBar)
    {
        _spdPause = topBar.Q<Button>("SpeedPause");
        _spd1x    = topBar.Q<Button>("Speed1x");
        _spd2x    = topBar.Q<Button>("Speed2x");
        _spd3x    = topBar.Q<Button>("Speed3x");

        _spdPause?.RegisterCallback<ClickEvent>(_ => SetSpeed(0f));
        _spd1x?.RegisterCallback<ClickEvent>(_ => SetSpeed(1f));
        _spd2x?.RegisterCallback<ClickEvent>(_ => SetSpeed(2f));
        _spd3x?.RegisterCallback<ClickEvent>(_ => SetSpeed(3f));

        SetSpeed(1f); // start at normal speed + highlight 1×
    }

    // Holistic game speed: scales the whole simulation (clock, employees, animation).
    // 0 = paused. Uses Time.timeScale so everything advances together.
    private void SetSpeed(float scale)
    {
        Time.timeScale = scale;

        _spdPause?.RemoveFromClassList("topbar-speed-btn--active");
        _spd1x?.RemoveFromClassList("topbar-speed-btn--active");
        _spd2x?.RemoveFromClassList("topbar-speed-btn--active");
        _spd3x?.RemoveFromClassList("topbar-speed-btn--active");

        Button active = scale <= 0f ? _spdPause
                      : scale >= 3f ? _spd3x
                      : scale >= 2f ? _spd2x
                                    : _spd1x;
        active?.AddToClassList("topbar-speed-btn--active");
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

    // ── In-game settings ──────────────────────────────────────────

    private void OnMenuSettings()
    {
        CloseMenuPopup();
        if (_igSettingsOverlay != null)
        {
            _igSettingsOverlay.style.display = DisplayStyle.Flex;
            _igSettingsOverlay.pickingMode   = PickingMode.Position;
        }
    }

    private void CloseSettings()
    {
        if (_igSettingsOverlay != null)
        {
            _igSettingsOverlay.style.display = DisplayStyle.None;
            _igSettingsOverlay.pickingMode   = PickingMode.Ignore;
        }
        if (_igResConfirmOverlay != null)
        {
            _igResConfirmOverlay.style.display = DisplayStyle.None;
            _igResConfirmOverlay.pickingMode   = PickingMode.Ignore;
        }
        _igPendingResIdx = -1;
        OpenMenuPopup();
    }

    private void IgApplyStoredSettings()
    {
        string preset = PlayerPrefs.GetString("GraphicsPresetName", "Ultra");
        IgApplyGraphicsPreset(preset, notify: false);

        int diff = PlayerPrefs.GetInt("Difficulty", 0);
        IgApplyDifficulty(diff);

        float gv = PlayerPrefs.GetFloat(GameVolParam, 1f);
        float mv = PlayerPrefs.GetFloat(MusicVolParam, 0.7f);
        if (_igGameVolumeSlider  != null) _igGameVolumeSlider.SetValueWithoutNotify(gv);
        if (_igMusicVolumeSlider != null) _igMusicVolumeSlider.SetValueWithoutNotify(mv);
    }

    private void IgApplyGraphicsPreset(string preset, bool notify = true)
    {
        var mgr = FindAnyObjectByType<GraphicsPresetManager>();
        if (mgr != null)
            mgr.ApplyPreset((GraphicsPresetManager.Preset)System.Enum.Parse(
                typeof(GraphicsPresetManager.Preset), preset), notify);

        _igBtnScreamin?.RemoveFromClassList("gfx-btn--active");
        _igBtnGood?.RemoveFromClassList("gfx-btn--active");
        _igBtnToaster?.RemoveFromClassList("gfx-btn--active");
        switch (preset)
        {
            case "Ultra":   _igBtnScreamin?.AddToClassList("gfx-btn--active"); break;
            case "Good":    _igBtnGood?.AddToClassList("gfx-btn--active");     break;
            case "Toaster": _igBtnToaster?.AddToClassList("gfx-btn--active");  break;
        }
        PlayerPrefs.SetString("GraphicsPresetName", preset);
    }

    private void IgApplyDifficulty(int level)
    {
        PlayerPrefs.SetInt("Difficulty", level);
        _igBtnClerk?.RemoveFromClassList("gfx-btn--active");
        _igBtnSupervisor?.RemoveFromClassList("gfx-btn--active");
        _igBtnManager?.RemoveFromClassList("gfx-btn--active");
        switch (level)
        {
            case 0: _igBtnClerk?.AddToClassList("gfx-btn--active");      break;
            case 1: _igBtnSupervisor?.AddToClassList("gfx-btn--active"); break;
            case 2: _igBtnManager?.AddToClassList("gfx-btn--active");    break;
        }
    }

    private void IgBuildResolutionDropdown()
    {
        if (_igResolutionDropdown == null) return;

        var choices = new List<string>();
        foreach (var r in CommonResolutions)
            choices.Add($"{r.width} x {r.height}");
        _igResolutionDropdown.choices = choices;

        int w = Screen.width, h = Screen.height;
        _igCurrentResIdx = 1;
        for (int i = 0; i < CommonResolutions.Length; i++)
            if (CommonResolutions[i].width == w && CommonResolutions[i].height == h)
                _igCurrentResIdx = i;
        _igResolutionDropdown.SetValueWithoutNotify(choices[_igCurrentResIdx]);

        _igResolutionDropdown.RegisterValueChangedCallback(evt =>
        {
            int idx = _igResolutionDropdown.index;
            if (idx < 0 || idx >= CommonResolutions.Length || idx == _igCurrentResIdx) return;
            _igPendingResIdx = idx;
            if (_igResConfirmOverlay != null)
            {
                _igResConfirmOverlay.style.display = DisplayStyle.Flex;
                _igResConfirmOverlay.pickingMode   = PickingMode.Position;
            }
        });
    }

    private void IgOnResolutionAccepted()
    {
        if (_igPendingResIdx >= 0 && _igPendingResIdx < CommonResolutions.Length)
        {
            var r = CommonResolutions[_igPendingResIdx];
            Screen.SetResolution(r.width, r.height, Screen.fullScreen);
            _igCurrentResIdx = _igPendingResIdx;
            PlayerPrefs.SetInt("ResolutionIndex", _igCurrentResIdx);
        }
        _igPendingResIdx = -1;
        if (_igResConfirmOverlay != null)
        {
            _igResConfirmOverlay.style.display = DisplayStyle.None;
            _igResConfirmOverlay.pickingMode   = PickingMode.Ignore;
        }
    }

    private void IgOnResolutionCancelled()
    {
        if (_igResolutionDropdown != null)
            _igResolutionDropdown.SetValueWithoutNotify(_igResolutionDropdown.choices[_igCurrentResIdx]);
        _igPendingResIdx = -1;
        if (_igResConfirmOverlay != null)
        {
            _igResConfirmOverlay.style.display = DisplayStyle.None;
            _igResConfirmOverlay.pickingMode   = PickingMode.Ignore;
        }
    }

    private void IgSetVolume(string param, float linear)
    {
        if (AudioManager.instance != null)
        {
            if (param == GameVolParam)  AudioManager.instance.SetSfxVolume(linear);
            else                        AudioManager.instance.SetMusicVolume(linear);
        }
        PlayerPrefs.SetFloat(param, linear);
    }

    private void OnDestroy()
    {
        if (_moneyService != null) _moneyService.OnMoneyChanged -= Refresh;
        if (_timeService  != null) _timeService.OnTimeChanged   -= Refresh;
        if (SaveManager.Instance != null)
            SaveManager.Instance.OnSaveCompleted -= OnSaveCompleted;
        _breakdownPanel?.Dispose();
    }
}
