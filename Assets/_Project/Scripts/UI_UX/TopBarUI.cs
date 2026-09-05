using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using SaveLoadSystem;
using GameCore.Events;
using GameCore.Events.Payloads;

public class TopBarUI : MonoBehaviour
{
    private Label _money;
    private Label _hourly;
    private Label _spent;
    private Label _time;
    private Label _reputation;

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

    // Settings panel (delegated)
    private TopBarSettingsPanel _settingsPanel;

    // Game-speed (time advancer) buttons.
    private Button _spdPause, _spdQuarter, _spdHalf, _spd1x, _spd2x, _spd3x;

    private MoneyService _moneyService;
    private SimulationTimeService _timeService;
    private EconomyService _economyService;
    private EventManager _eventManager;
    private FinancialBreakdownPanel _breakdownPanel;   // "Hourly" — full per-category expense drill-down
    private CapitalSummaryPanel _capitalPanel;          // "Capital" — revenue + single expense total + net
    private SpentTodayPanel _spentTodayPanel;           // "Spent Today" — today's spend by GL line
    private ShiftStatusPanel _shiftStatusPanel;         // "Time" — hours left in shift + overtime count
    private ReputationPanel _reputationPanel;           // "Reputation" — score, standing, next-band gap
    private ShiftManagerPanel _shiftManagerPanel;       // "6" key — define named shifts (first draft, UI only)
    public ShiftManagerPanel ShiftManagerPanel => _shiftManagerPanel;
    private SlotAssignmentPanel _slotAssignmentPanel;   // "6" key — assign SKUs to rack Pick slots
    public SlotAssignmentPanel SlotAssignmentPanel => _slotAssignmentPanel;
    private WorkQueuePanel _workQueuePanel;             // "7" key — release orders to a staging lane / door
    public WorkQueuePanel WorkQueuePanel => _workQueuePanel;
    private ContractsPanel _contractsPanel;             // "8" key — sign customer contracts (where demand comes from); relabeled "Orders" on the play bar
    public ContractsPanel ContractsPanel => _contractsPanel;
    private SchedulerPanel _schedulerPanel;             // "0" key — standalone Scheduler (dock appointment grid)
    public SchedulerPanel SchedulerPanel => _schedulerPanel;
    private NewItemPanel _newItemPanel;                 // "5" key — assign pick slots to received items
    public NewItemPanel NewItemPanel => _newItemPanel;
    private PurchasingPanel _purchasingPanel;           // "9" key — raise POs to bring stock in
    public PurchasingPanel PurchasingPanel => _purchasingPanel;
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

        _money      = topBar.Q<Label>("MoneyLabel");
        _hourly     = topBar.Q<Label>("HourlyLabel");
        _spent      = topBar.Q<Label>("SpentLabel");
        _time       = topBar.Q<Label>("TimeLabel");
        _reputation = topBar.Q<Label>("CellLabel");  // Reuse the CellLabel position — was Headcount

        if (_money == null || _hourly == null || _spent == null ||
            _time  == null || _reputation  == null)
        {
            Debug.LogError("One or more TopBar labels are missing.");
            return;
        }

        _breakdownPanel   = new FinancialBreakdownPanel(root, _moneyService, _hourly.parent);
        _capitalPanel     = new CapitalSummaryPanel(root, _moneyService, _money.parent);
        _spentTodayPanel  = new SpentTodayPanel(root, _moneyService, _spent.parent);
        _shiftStatusPanel = new ShiftStatusPanel(root, _timeService, _time.parent);
        _reputationPanel  = new ReputationPanel(root, _reputation.parent);
        _shiftManagerPanel = new ShiftManagerPanel(root, _timeService);
        _slotAssignmentPanel = new SlotAssignmentPanel(root);
        _workQueuePanel = new WorkQueuePanel(root);
        _contractsPanel = new ContractsPanel(root);
        _schedulerPanel = new SchedulerPanel(root);
        _newItemPanel = new NewItemPanel(root);
        _purchasingPanel = new PurchasingPanel(root);

        // Register shift manager with UIKeyBindingManager for keybinding support (key 6)
        if (UIKeyBindingManager.Instance != null)
        {
            UIKeyBindingManager.Instance.RegisterUI(5, _newItemPanel);
            UIKeyBindingManager.Instance.RegisterUI(6, _shiftManagerPanel);
            // Key 7 — registration is also what makes Tab close it: PlacementStateMachine's Tab handler
            // closes panels through UIKeyBindingManager.CloseAll(), which only iterates the registry.
            // Contracts used to register floating: true so it could be read next to the Work Queue.
            // It's exclusive now by request — opening it clears the screen like any other panel.
            UIKeyBindingManager.Instance.RegisterUI(7, _workQueuePanel);
            UIKeyBindingManager.Instance.RegisterUI(8, _contractsPanel);
            UIKeyBindingManager.Instance.RegisterUI(9, _purchasingPanel);
            UIKeyBindingManager.Instance.RegisterUI(0, _schedulerPanel);
            // Note: SlotAssignmentPanel doesn't implement IUIPanel yet, can be accessed via UI button
        }

        _money.RegisterCallback<ClickEvent>(_ => ToggleExclusive(_capitalPanel));
        _hourly.RegisterCallback<ClickEvent>(_ => ToggleExclusive(_breakdownPanel));
        _spent.RegisterCallback<ClickEvent>(_ => ToggleExclusive(_spentTodayPanel));
        _time.RegisterCallback<ClickEvent>(_ => ToggleExclusive(_shiftStatusPanel));
        _reputation.RegisterCallback<ClickEvent>(_ => ToggleExclusive(_reputationPanel));

        RegisterPanelHoverTracking(_money, _capitalPanel);
        RegisterPanelHoverTracking(_hourly, _breakdownPanel);
        RegisterPanelHoverTracking(_spent, _spentTodayPanel);
        RegisterPanelHoverTracking(_time, _shiftStatusPanel);
        RegisterPanelHoverTracking(_reputation, _reputationPanel);
        root.schedule.Execute(PollPanelAutoClose).Every(100);

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

        if (_saveButton           != null) _saveButton.clicked           += () => _saveLoadController?.Open(SaveLoadMode.Save);
        if (_loadButton           != null) _loadButton.clicked           += () => _saveLoadController?.Open(SaveLoadMode.Load);
        if (_mainMenuButton       != null) _mainMenuButton.clicked       += ToggleMenuPopup;
        if (_menuMainMenuButton   != null) _menuMainMenuButton.clicked   += OnMenuDirectToMainMenu;
        if (_menuSettingsButton   != null) _menuSettingsButton.clicked   += OnMenuSettings;
        if (_menuSaveButton       != null) _menuSaveButton.clicked       += OnMenuSave;
        if (_menuLoadButton       != null) _menuLoadButton.clicked       += OnMenuLoad;
        if (_menuResumeButton     != null) _menuResumeButton.clicked     += CloseMenuPopup;
        if (_menuExitButton       != null) _menuExitButton.clicked       += OnExitGame;

        // Initialize settings panel
        _settingsPanel = gameObject.AddComponent<TopBarSettingsPanel>();
        _settingsPanel.Init(hudRoot);

        if (SaveManager.Instance != null)
            SaveManager.Instance.OnSaveCompleted += OnSaveCompleted;

        // Subscribe to new GameEvents-based economy/time updates
        _eventManager = EventManager.Instance;
        if (_eventManager != null)
        {
            _eventManager.Subscribe<int>(GameEvents.Economy.OnMoneyChanged, OnMoneyChangedEvent);
            _eventManager.Subscribe<SimulationTimeData>(GameEvents.Time.OnMinutePassed, OnTimePassedEvent);
        }
        else
        {
            // Fallback: subscribe to legacy events if EventManager is not available
            _moneyService.OnMoneyChanged += Refresh;
            _timeService.OnTimeChanged   += Refresh;
        }

        Refresh();
    }

    public void OpenNewItemPanelWithSku(string skuId)
    {
        _newItemPanel?.ShowWithSku(skuId);
    }

    private void Update()
    {
        // Update panels that need periodic refresh
        _newItemPanel?.Update();

        // All routed through UIKeyBindingManager rather than toggled directly. Toggling a panel
        // straight left every other panel open — the manager is what closes the incumbent first, and
        // what gives the Shift Manager its chance to confirm before losing unsaved edits.
        var keys = UIKeyBindingManager.Instance;

        if (!UIModalGuard.IsCapturing && Keyboard.current.digit5Key.wasPressedThisFrame)
            keys.ToggleUI(5);

        // 6 opens the Shift Manager. The Slot Assignment panel that used to live here is still
        // reachable by clicking a rack (PlacementStateMachine -> ShowForAisle); it just no longer has
        // a number key of its own.
        if (!UIModalGuard.IsCapturing && Keyboard.current.digit6Key.wasPressedThisFrame)
            keys.ToggleUI(6);

        if (!UIModalGuard.IsCapturing && Keyboard.current.digit7Key.wasPressedThisFrame)
            keys.ToggleUI(7);

        // 8 opens ORDERS (the Contracts panel, relabeled) — kept next to Purchasing on 9.
        if (!UIModalGuard.IsCapturing && Keyboard.current.digit8Key.wasPressedThisFrame)
            keys.ToggleUI(8);

        // 9 opens PURCHASING — the inbound counterpart to Orders on 8.
        if (!UIModalGuard.IsCapturing && Keyboard.current.digit9Key.wasPressedThisFrame)
            keys.ToggleUI(9);

        // 0 opens the standalone Scheduler panel (dock appointment grid).
        if (!UIModalGuard.IsCapturing && Keyboard.current.digit0Key.wasPressedThisFrame)
            keys.ToggleUI(0);

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // Confirmation modals (ConfirmationModal.Show — the Yes/No prompts) only resolve through
            // their own buttons. Escape must not dismiss them, and must not fall through to closing
            // whatever panel is open behind them while a decision is still pending.
            if (ConfirmationModal.IsOpen)
            {
                // handled: swallow the press, do nothing.
            }
            // A hotkey-less sub-popup (the Work Queue's Fill Rate shorts readout) is the innermost
            // thing on screen, so it backs out first — and CloseAuxiliaries returning true is what
            // stops the same press falling through and opening the pause menu behind it.
            else if (keys.CloseAuxiliaries())
            {
                // handled
            }
            // Save/load window takes priority: Escape closes it (and ensures
            // the pause menu doesn't pop back open in the same press) instead
            // of toggling the pause menu.
            else if (_saveLoadController != null && _saveLoadController.IsOpen)
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
            else if (keys.AnyPanelOpen)
            {
                // Any other non-modal UI (Shift Manager, Work Queue, Orders, Purchasing, Scheduler,
                // New Item, Dev Console, Hiring Board, Employee Roster, Employee List) is open.
                // Escape closes it and returns to the game screen instead of opening the pause menu
                // on top of it.
                keys.CloseAll();
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
        _spdPause   = topBar.Q<Button>("SpeedPause");
        _spdQuarter = topBar.Q<Button>("SpeedQuarter");
        _spdHalf    = topBar.Q<Button>("SpeedHalf");
        _spd1x      = topBar.Q<Button>("Speed1x");
        _spd2x      = topBar.Q<Button>("Speed2x");
        _spd3x      = topBar.Q<Button>("Speed3x");

        _spdPause?.RegisterCallback<ClickEvent>(_ => SetSpeed(0f));
        _spdQuarter?.RegisterCallback<ClickEvent>(_ => SetSpeed(0.25f));
        _spdHalf?.RegisterCallback<ClickEvent>(_ => SetSpeed(0.5f));
        _spd1x?.RegisterCallback<ClickEvent>(_ => SetSpeed(1f));
        _spd2x?.RegisterCallback<ClickEvent>(_ => SetSpeed(2f));
        _spd3x?.RegisterCallback<ClickEvent>(_ => SetSpeed(3f));

        SetSpeed(0f); // always start paused — per Tad's explicit request, so a fresh session (or a
                      // just-loaded save) never has trucks/employees/the clock already moving before
                      // the player has had a chance to look at the state they're starting from.
    }

    // Holistic game speed: scales the whole simulation (clock, employees, animation).
    // 0 = paused. Uses Time.timeScale so everything advances together.
    private void SetSpeed(float scale)
    {
        Time.timeScale = scale;

        _spdPause?.RemoveFromClassList("topbar-speed-btn--active");
        _spdQuarter?.RemoveFromClassList("topbar-speed-btn--active");
        _spdHalf?.RemoveFromClassList("topbar-speed-btn--active");
        _spd1x?.RemoveFromClassList("topbar-speed-btn--active");
        _spd2x?.RemoveFromClassList("topbar-speed-btn--active");
        _spd3x?.RemoveFromClassList("topbar-speed-btn--active");

        Button active = scale <= 0f ? _spdPause
                      : Mathf.Approximately(scale, 0.25f) ? _spdQuarter
                      : Mathf.Approximately(scale, 0.5f) ? _spdHalf
                      : scale >= 3f ? _spd3x
                      : scale >= 2f ? _spd2x
                                    : _spd1x;
        active?.AddToClassList("topbar-speed-btn--active");
    }

    // Only one of the TopBar dropdowns may be open at a time.
    private void ToggleExclusive(ITopBarPanel panel)
    {
        bool wasOpen = panel.IsVisible;
        _capitalPanel?.Hide();
        _breakdownPanel?.Hide();
        _spentTodayPanel?.Hide();
        _shiftStatusPanel?.Hide();
        _reputationPanel?.Hide();
        if (!wasOpen) panel.Show();
    }

    // ── Auto-close TopBar dropdowns after ~1.5s unhovered ──────────────────────
    // Click opens a panel; if the mouse leaves BOTH the trigger label and the panel itself and
    // stays away, it auto-closes after _panelAutoCloseMs. Hovering either resets the idle clock.
    private const float PanelAutoCloseMs = 1500f;
    private readonly Dictionary<ITopBarPanel, bool> _panelHovered = new();
    private float _panelIdleMs;

    private void RegisterPanelHoverTracking(VisualElement trigger, ITopBarPanel panel)
    {
        _panelHovered[panel] = false;

        trigger.RegisterCallback<MouseEnterEvent>(_ => _panelHovered[panel] = true);
        trigger.RegisterCallback<MouseLeaveEvent>(_ => _panelHovered[panel] = false);
        panel.Root.RegisterCallback<MouseEnterEvent>(_ => _panelHovered[panel] = true);
        panel.Root.RegisterCallback<MouseLeaveEvent>(_ => _panelHovered[panel] = false);
    }

    private void PollPanelAutoClose()
    {
        ITopBarPanel active = _capitalPanel.IsVisible ? _capitalPanel
                            : _breakdownPanel.IsVisible ? _breakdownPanel
                            : _spentTodayPanel.IsVisible ? _spentTodayPanel
                            : _shiftStatusPanel.IsVisible ? _shiftStatusPanel
                            : _reputationPanel.IsVisible ? _reputationPanel
                            : null;

        if (active == null)
        {
            _panelIdleMs = 0f;
            return;
        }

        if (_panelHovered.TryGetValue(active, out bool hovered) && hovered)
        {
            _panelIdleMs = 0f;
            return;
        }

        _panelIdleMs += 100f;
        if (_panelIdleMs < PanelAutoCloseMs) return;

        active.Hide();
        _panelIdleMs = 0f;
    }

    public void SetState(string stateName) { }

    public void SetCell(int x, int y)
    {
        // Cell info moved to DevHud window via TopBar's SetCell routing
        var devHud = FindAnyObjectByType<DevHudWindow>();
        if (devHud != null) devHud.SetCell(x, y);
    }

    private int _lastMoney = -1;
    private int _lastRevenueThisHour = -1, _lastExpensesThisHour = -1;
    private int _lastRevenueLastHour = -1, _lastExpensesLastHour = -1;
    private int _lastRevenueToday = -1, _lastExpensesToday = -1;
    private int _lastRevenueYesterday = -1, _lastExpensesYesterday = -1;
    private int _lastRevenueWeek = -1, _lastExpensesWeek = -1;
    private int _lastMinute = -1, _lastHour = -1, _lastDay = -1;
    private int _lastReputationScore = -1;

    private void Refresh()
    {
        if (_moneyService == null || _timeService == null) return;

        // Update capital
        if (_moneyService.CurrentCapital != _lastMoney)
        {
            _lastMoney = _moneyService.CurrentCapital;
            _money.text = $"Capital: ${_lastMoney:N0}";
        }

        // Update hourly expense/revenue metrics
        if (_moneyService.ExpensesThisHour != _lastExpensesThisHour ||
            _moneyService.RevenueThisHour != _lastRevenueThisHour ||
            _moneyService.ExpensesLastHour != _lastExpensesLastHour ||
            _moneyService.RevenueLastHour != _lastRevenueLastHour)
        {
            _lastExpensesThisHour = _moneyService.ExpensesThisHour;
            _lastRevenueThisHour = _moneyService.RevenueThisHour;
            _lastExpensesLastHour = _moneyService.ExpensesLastHour;
            _lastRevenueLastHour = _moneyService.RevenueLastHour;

            string hourlyText = $"Hour Expenses: ${_lastExpensesThisHour:N0} | Last Hour: ${_lastExpensesLastHour:N0}";
            _hourly.text = hourlyText;
        }

        // Update daily expense metrics
        if (_moneyService.ExpensesToday != _lastExpensesToday ||
            _moneyService.ExpensesYesterday != _lastExpensesYesterday)
        {
            _lastExpensesToday = _moneyService.ExpensesToday;
            _lastExpensesYesterday = _moneyService.ExpensesYesterday;
            _spent.text = $"Today: ${_lastExpensesToday:N0} | Yesterday: ${_lastExpensesYesterday:N0}";
        }

        // Update reputation — resolved lazily (not cached at Init) for the same reason
        // ReputationPanel does: GameContext can construct TopBarUI before ReputationService.
        if (ServiceLocator.TryGet(out ReputationService reputation) && reputation != null &&
            reputation.Score != _lastReputationScore)
        {
            _lastReputationScore = reputation.Score;
            _reputation.text = $"Rep: {ReputationService.BandLabel(reputation.Band)}";
            _reputation.style.color = ReputationService.ColorFor(reputation.Score);
            _reputationPanel?.RefreshIfVisible();
        }

        if (_timeService.Minute != _lastMinute ||
            _timeService.Hour   != _lastHour   ||
            _timeService.Day    != _lastDay)
        {
            _lastMinute = _timeService.Minute;
            _lastHour   = _timeService.Hour;
            _lastDay    = _timeService.Day;
            _time.text  = $"Day {_lastDay}  Time: {_lastHour:00}:{_lastMinute:00}";
            _shiftStatusPanel?.RefreshIfVisible();
        }
    }

    // ── In-game settings ──────────────────────────────────────────

    private void OnMenuSettings()
    {
        CloseMenuPopup();
        _settingsPanel?.Open();
    }

    // ── Event handlers for new GameEvents-based system ────────────────

    private void OnMoneyChangedEvent(string eventId, int newBalance)
    {
        Refresh();
    }

    private void OnTimePassedEvent(string eventId, SimulationTimeData timeData)
    {
        Refresh();
    }

    private void OnDestroy()
    {
        // Unsubscribe from new GameEvents if EventManager is available
        if (_eventManager != null)
        {
            _eventManager.Unsubscribe<int>(GameEvents.Economy.OnMoneyChanged, OnMoneyChangedEvent);
            _eventManager.Unsubscribe<SimulationTimeData>(GameEvents.Time.OnMinutePassed, OnTimePassedEvent);
        }
        else
        {
            // Unsubscribe from legacy events if they were used as fallback
            if (_moneyService != null) _moneyService.OnMoneyChanged -= Refresh;
            if (_timeService  != null) _timeService.OnTimeChanged   -= Refresh;
        }

        if (SaveManager.Instance != null)
            SaveManager.Instance.OnSaveCompleted -= OnSaveCompleted;
        _breakdownPanel?.Dispose();
        _capitalPanel?.Dispose();
        _spentTodayPanel?.Dispose();
        _shiftStatusPanel?.Dispose();
        _reputationPanel?.Dispose();
        _shiftManagerPanel?.Dispose();
        _slotAssignmentPanel?.Dispose();
        _workQueuePanel?.Dispose();
    }
}
