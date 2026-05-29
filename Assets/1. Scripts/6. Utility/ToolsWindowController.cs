using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Unified Tools Window — hosts the Pallet Builder and Dev Console in a tabbed panel.
///
/// Setup:
///   1. Add a UIDocument + this component to a persistent scene GameObject.
///   2. Set UIDocument Source Asset to ToolsWindow.uxml.
///   3. Use BuildPanel.asset (or a dedicated PanelSettings) with sort order ≥ 20.
///
/// Usage:
///   - Backtick (`) toggles the Dev Console tab.
///   - PalletBuilder calls ToolsWindowController.Instance.OpenForPallet(this).
///   - Wheel events on the window are swallowed — camera won't zoom through it.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ToolsWindowController : MonoBehaviour
{
    public static ToolsWindowController Instance { get; private set; }

    private const string TAB_PALLET = "pallet";
    private const string TAB_DEV    = "dev";

    // ── Core ──────────────────────────────────────────────────────
    private UIDocument    _doc;
    private VisualElement _window;
    private bool          _visible;
    private string        _activeTab = TAB_PALLET;

    // ── Drag ──────────────────────────────────────────────────────
    private bool    _dragging;
    private Vector2 _dragStartPointer;
    private Vector2 _windowStartPos;

    // ── Tabs ──────────────────────────────────────────────────────
    private Button        _tabPallet, _tabDev;
    private VisualElement _contentPallet, _contentDev;

    // ── Pallet builder ────────────────────────────────────────────
    private PalletBuilder  _targetBuilder;
    private TextField      _maxHeightField, _spaceField, _gapField, _crookedField;
    private Toggle         _overrideToggle;
    private TextField      _manualTiField, _manualHiField;
    private DropdownField  _prefabDropdown;
    private Button         _buildButton;
    private List<ObjDataSO> _availablePrefabs = new();

    // ── Dev console ───────────────────────────────────────────────
    private GameContext          _ctx;
    private PlacementStateMachine _fsm;
    private PlacementGrid        _grid;

    private Label _balance, _hourly, _spent;
    private Label _time, _speed;
    private Label _objects, _undo;
    private Label _state, _stack;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        _doc = GetComponent<UIDocument>();
    }

    private void Start()
    {
        _ctx  = FindFirstObjectByType<GameContext>();
        _fsm  = FindFirstObjectByType<PlacementStateMachine>();
        _grid = FindFirstObjectByType<PlacementGrid>();

        var root = _doc.rootVisualElement;

        // Window starts hidden
        _window = root.Q("tools-window");
        _window.style.display = DisplayStyle.None;

        // Swallow wheel events so the camera never zooms through this window
        _window.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());

        // Also block pointer enter/leave so external UI hover checks work correctly
        _window.RegisterCallback<PointerEnterEvent>(evt => evt.StopPropagation());

        // Buttons
        root.Q<Button>("tools-close").clicked += Hide;

        // Tabs
        _tabPallet     = root.Q<Button>("tab-btn-pallet");
        _tabDev        = root.Q<Button>("tab-btn-dev");
        _contentPallet = root.Q("tab-content-pallet");
        _contentDev    = root.Q("tab-content-dev");

        _tabPallet.clicked += () => SwitchTab(TAB_PALLET);
        _tabDev.clicked    += () => SwitchTab(TAB_DEV);

        // Title bar drag
        var titlebar = root.Q("tools-titlebar");
        titlebar.RegisterCallback<PointerDownEvent>(OnDragDown);
        titlebar.RegisterCallback<PointerMoveEvent>(OnDragMove);
        titlebar.RegisterCallback<PointerUpEvent>(OnDragUp);
        titlebar.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);

        // ── Pallet builder elements ────────────────────────────
        _maxHeightField = root.Q<TextField>("plt-maxheight");
        _spaceField     = root.Q<TextField>("plt-space");
        _gapField       = root.Q<TextField>("plt-gap");
        _crookedField   = root.Q<TextField>("plt-crooked");
        _overrideToggle = root.Q<Toggle>("plt-override");
        _manualTiField  = root.Q<TextField>("plt-manualt");
        _manualHiField  = root.Q<TextField>("plt-manualh");
        _prefabDropdown = root.Q<DropdownField>("plt-prefab");
        _buildButton    = root.Q<Button>("plt-build");
        _buildButton.clicked += OnBuildClicked;

        // ── Dev console elements ───────────────────────────────
        _balance = root.Q<Label>("stat-balance");
        _hourly  = root.Q<Label>("stat-hourly");
        _spent   = root.Q<Label>("stat-spent");
        _time    = root.Q<Label>("stat-time");
        _speed   = root.Q<Label>("stat-speed");
        _objects = root.Q<Label>("stat-objects");
        _undo    = root.Q<Label>("stat-undo");
        _state   = root.Q<Label>("stat-state");
        _stack   = root.Q<Label>("stat-stack");

        root.Q<Button>("btn-add-1k").clicked   += () => _ctx?.MoneyService.Refund(1_000,   "Debug");
        root.Q<Button>("btn-add-10k").clicked  += () => _ctx?.MoneyService.Refund(10_000,  "Debug");
        root.Q<Button>("btn-add-100k").clicked += () => _ctx?.MoneyService.Refund(100_000, "Debug");
        root.Q<Button>("btn-zero").clicked     += () => _ctx?.MoneyService.SetMoney(0);

        root.Q<Button>("btn-pause").clicked += () => _ctx?.TimeService.SetTimeScale(0f);
        root.Q<Button>("btn-1x").clicked    += () => _ctx?.TimeService.SetTimeScale(1f);
        root.Q<Button>("btn-2x").clicked    += () => _ctx?.TimeService.SetTimeScale(2f);
        root.Q<Button>("btn-5x").clicked    += () => _ctx?.TimeService.SetTimeScale(5f);

        root.Q<Button>("btn-rebuild").clicked += () => _grid?.RebuildFromRegistry();
        root.Q<Button>("btn-clear").clicked   += ClearAll;

        if (_ctx != null)
            _ctx.MoneyService.OnMoneyChanged += RefreshEconomy;

        RefreshEconomy();
        SwitchTab(TAB_PALLET);
    }

    private void Update()
    {
        // Backtick: toggle dev tab (close if already on dev, otherwise open to dev)
        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            if (_visible && _activeTab == TAB_DEV) Hide();
            else { SwitchTab(TAB_DEV); Show(); }
        }

        if (!_visible || _ctx == null) return;

        var t = _ctx.TimeService;
        _time.text  = $"Day {t.Day}  {t.Hour:D2}:{t.Minute:D2}";
        _speed.text = t.TimeScale == 0f ? "PAUSED" : $"{t.TimeScale}×";

        _objects.text = PlacedObjectRegistry.Count.ToString();

        if (_fsm != null)
        {
            _state.text = _fsm.CurrentState?.GetType().Name ?? "—";
            _stack.text = _fsm.DebugStackDepth.ToString();
            _undo.text  = $"{_fsm.History.UndoCount} / {_fsm.History.RedoCount}";
        }
    }

    // ── Public API ────────────────────────────────────────────────

    /// <summary>Show the window on the Pallet Builder tab for a specific builder.</summary>
    public void OpenForPallet(PalletBuilder builder)
    {
        // If already showing this builder, close instead (toggle behaviour)
        if (_visible && _activeTab == TAB_PALLET && _targetBuilder == builder)
        {
            Hide();
            return;
        }

        _targetBuilder = builder;
        PopulatePrefabDropdown();
        RefreshPalletUI();
        SwitchTab(TAB_PALLET);
        Show();
    }

    public void Show()
    {
        _visible = true;
        _window.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        _visible = false;
        _window.style.display = DisplayStyle.None;
    }

    // ── Tab switching ─────────────────────────────────────────────

    private void SwitchTab(string tab)
    {
        _activeTab = tab;
        bool onPallet = tab == TAB_PALLET;

        _contentPallet.style.display = onPallet ? DisplayStyle.Flex : DisplayStyle.None;
        _contentDev.style.display    = onPallet ? DisplayStyle.None : DisplayStyle.Flex;

        _tabPallet.EnableInClassList("tools-tab--active",   onPallet);
        _tabPallet.EnableInClassList("tools-tab--inactive", !onPallet);
        _tabDev.EnableInClassList("tools-tab--active",      !onPallet);
        _tabDev.EnableInClassList("tools-tab--inactive",    onPallet);
    }

    // ── Title bar drag ────────────────────────────────────────────

    private void OnDragDown(PointerDownEvent evt)
    {
        _dragging         = true;
        _dragStartPointer = evt.position;
        _windowStartPos   = new Vector2(_window.resolvedStyle.left, _window.resolvedStyle.top);

        // If window was right-anchored, convert to explicit left
        if (float.IsNaN(_windowStartPos.x))
        {
            var root = _doc.rootVisualElement;
            _windowStartPos.x = root.resolvedStyle.width
                                 - _window.resolvedStyle.width
                                 - _window.resolvedStyle.right;
        }

        _window.style.right = StyleKeyword.Auto;
        _window.style.left  = _windowStartPos.x;
        _window.style.top   = _windowStartPos.y;

        _doc.rootVisualElement.Q("tools-titlebar").CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnDragMove(PointerMoveEvent evt)
    {
        if (!_dragging) return;
        var titlebar = _doc.rootVisualElement.Q("tools-titlebar");
        if (!titlebar.HasPointerCapture(evt.pointerId)) return;

        var delta = (Vector2)evt.position - _dragStartPointer;
        _window.style.left = _windowStartPos.x + delta.x;
        _window.style.top  = _windowStartPos.y + delta.y;
        evt.StopPropagation();
    }

    private void OnDragUp(PointerUpEvent evt)
    {
        var titlebar = _doc.rootVisualElement.Q("tools-titlebar");
        if (titlebar.HasPointerCapture(evt.pointerId))
            titlebar.ReleasePointer(evt.pointerId);
        _dragging = false;
    }

    // ── Pallet builder logic ──────────────────────────────────────

    private void PopulatePrefabDropdown()
    {
        if (_prefabDropdown == null) return;
        _availablePrefabs.Clear();
        var names = new List<string>();

        var buildMenu = FindAnyObjectByType<BuildMenuUI>();
        if (buildMenu?.registry == null) return;

        int selectedIndex = 0;
        foreach (var so in buildMenu.registry.buttonSOs)
        {
            if (so == null || so.category != "Inventory" || so.prefab == null) continue;
            if (so.objName.Contains("Chep")) continue;

            _availablePrefabs.Add(so);
            names.Add(so.objName);

            if (_targetBuilder != null && _targetBuilder.casePrefab == so.prefab)
                selectedIndex = names.Count - 1;
        }

        _prefabDropdown.choices = names;
        if (names.Count > 0) _prefabDropdown.index = selectedIndex;
    }

    private void RefreshPalletUI()
    {
        if (_targetBuilder == null) return;
        _maxHeightField.value = _targetBuilder.maxTotalHeight.ToString(CultureInfo.InvariantCulture);
        _spaceField.value     = _targetBuilder.spaceBetweenCases.ToString(CultureInfo.InvariantCulture);
        _gapField.value       = _targetBuilder.verticalGap.ToString(CultureInfo.InvariantCulture);
        _crookedField.value   = _targetBuilder.crookedCase.ToString(CultureInfo.InvariantCulture);
        _overrideToggle.value = _targetBuilder.useTiHiOverride;
        _manualTiField.value  = _targetBuilder.manualTi.ToString();
        _manualHiField.value  = _targetBuilder.manualHi.ToString();
    }

    private void OnBuildClicked()
    {
        if (_targetBuilder == null) return;

        if (_prefabDropdown?.index >= 0 && _prefabDropdown.index < _availablePrefabs.Count)
            _targetBuilder.casePrefab = _availablePrefabs[_prefabDropdown.index].prefab;

        if (float.TryParse(_maxHeightField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float h))
            _targetBuilder.maxTotalHeight = h;
        if (float.TryParse(_spaceField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float s))
            _targetBuilder.spaceBetweenCases = s;
        if (float.TryParse(_gapField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float g))
            _targetBuilder.verticalGap = g;
        if (float.TryParse(_crookedField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float c))
            _targetBuilder.crookedCase = c;

        _targetBuilder.useTiHiOverride = _overrideToggle.value;

        if (int.TryParse(_manualTiField.value, out int ti)) _targetBuilder.manualTi = ti;
        if (int.TryParse(_manualHiField.value, out int hi)) _targetBuilder.manualHi = hi;

        _targetBuilder.Build();
    }

    // ── Dev console logic ─────────────────────────────────────────

    private void RefreshEconomy()
    {
        if (_ctx == null) return;
        var m = _ctx.MoneyService;
        _balance.text = $"${m.CurrentCapital:N0}";
        _hourly.text  = $"${m.TotalHourlyCost:N0}/hr";
        _spent.text   = $"${m.SpentToday:N0}";
    }

    private void ClearAll()
    {
        foreach (var obj in PlacedObjectRegistry.GetSnapshot())
            if (obj != null) Destroy(obj.gameObject);
        _grid?.RebuildFromRegistry();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_ctx != null) _ctx.MoneyService.OnMoneyChanged -= RefreshEconomy;
        if (_buildButton != null) _buildButton.clicked     -= OnBuildClicked;
    }
}
