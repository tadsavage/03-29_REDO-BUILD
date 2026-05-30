using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Unified Tools Window — Pallet Builder + Dev Console in one draggable panel.
///
/// Setup:
///   1. Create a GameObject, add UIDocument + this component.
///   2. UIDocument Source Asset → ToolsWindow.uxml
///   3. Assign a PanelSettings with Sort Order 50.
///   4. Enter Play Mode:
///      - Backtick (`) opens the Dev Console tab.
///      - Clicking any pallet in the scene opens the Pallet Builder tab.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ToolsWindowController : MonoBehaviour
{
    public static ToolsWindowController Instance { get; private set; }

    private UIDocument _doc;
    private VisualElement _window;
    private bool _visible;

    // Core services
    private GameContext _ctx;
    private PlacementStateMachine _fsm;
    private PlacementGrid _grid;

    // Tabs
    private Button _tabPallet, _tabDev;
    private VisualElement _contentPallet, _contentDev;

    // Dev console labels
    private Label _balance, _hourly, _spent;
    private Label _time, _speed;
    private Label _objects, _undo;
    private Label _state, _stack;

    // Pallet builder
    private PalletBuilder _targetBuilder;
    private readonly List<ObjDataSO> _palletPrefabs = new();
    private DropdownField _pltPrefab;
    private TextField _pltMaxHeight, _pltSpace, _pltGap, _pltCrooked, _pltManualT, _pltManualH;
    private Toggle _pltOverride;

    // Drag state
    private VisualElement _titlebar;
    private bool _dragging;
    private Vector2 _dragStartScreen;
    private Vector2 _windowStartPos;

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
        root.pickingMode = PickingMode.Ignore;

        // USS picking-mode isn't reliably applied at runtime — set overlay to Ignore in C# too
        var overlay = root.Q("tools-overlay");
        if (overlay != null) overlay.pickingMode = PickingMode.Ignore;

        _window = root.Q("tools-window");
        if (_window == null)
        {
            Debug.LogError("[ToolsWindow] 'tools-window' not found — is ToolsWindow.uxml set as the UIDocument Source Asset?");
            return;
        }

        // Start hidden — tilde or pallet click opens it
        _window.style.display = DisplayStyle.None;

        // Wire every button with a simple null-guarded clicked handler (no StopPropagation weirdness)
        Wire<Button>("tools-close",  root, b => b.clicked += () => Hide());
        Wire<Button>("tab-btn-pallet", root, b => { _tabPallet = b; b.clicked += () => SwitchTab("pallet"); });
        Wire<Button>("tab-btn-dev",    root, b => { _tabDev    = b; b.clicked += () => SwitchTab("dev"); });

        _contentPallet = root.Q("tab-content-pallet");
        _contentDev    = root.Q("tab-content-dev");
        _titlebar      = root.Q("tools-titlebar");

        if (_titlebar != null)
            _titlebar.RegisterCallback<PointerDownEvent>(OnTitlebarDown);

        // Dev console labels
        _balance = root.Q<Label>("stat-balance");
        _hourly  = root.Q<Label>("stat-hourly");
        _spent   = root.Q<Label>("stat-spent");
        _time    = root.Q<Label>("stat-time");
        _speed   = root.Q<Label>("stat-speed");
        _objects = root.Q<Label>("stat-objects");
        _undo    = root.Q<Label>("stat-undo");
        _state   = root.Q<Label>("stat-state");
        _stack   = root.Q<Label>("stat-stack");

        // Dev console buttons
        Wire<Button>("btn-add-1k",   root, b => b.clicked += () => _ctx?.MoneyService.Refund(1_000,   "Debug"));
        Wire<Button>("btn-add-10k",  root, b => b.clicked += () => _ctx?.MoneyService.Refund(10_000,  "Debug"));
        Wire<Button>("btn-add-100k", root, b => b.clicked += () => _ctx?.MoneyService.Refund(100_000, "Debug"));
        Wire<Button>("btn-zero",     root, b => b.clicked += () => _ctx?.MoneyService.SetMoney(0));
        Wire<Button>("btn-pause",    root, b => b.clicked += () => _ctx?.TimeService.SetTimeScale(0f));
        Wire<Button>("btn-1x",       root, b => b.clicked += () => _ctx?.TimeService.SetTimeScale(1f));
        Wire<Button>("btn-2x",       root, b => b.clicked += () => _ctx?.TimeService.SetTimeScale(2f));
        Wire<Button>("btn-5x",       root, b => b.clicked += () => _ctx?.TimeService.SetTimeScale(5f));
        Wire<Button>("btn-rebuild",  root, b => b.clicked += () => _grid?.RebuildFromRegistry());
        Wire<Button>("btn-clear",    root, b => b.clicked += ClearAll);

        // Pallet builder fields
        _pltPrefab    = root.Q<DropdownField>("plt-prefab");
        _pltMaxHeight = root.Q<TextField>("plt-maxheight");
        _pltSpace     = root.Q<TextField>("plt-space");
        _pltGap       = root.Q<TextField>("plt-gap");
        _pltCrooked   = root.Q<TextField>("plt-crooked");
        _pltOverride  = root.Q<Toggle>("plt-override");
        _pltManualT   = root.Q<TextField>("plt-manualt");
        _pltManualH   = root.Q<TextField>("plt-manualh");
        Wire<Button>("plt-build", root, b => b.clicked += OnPalletBuild);

        if (_ctx != null)
            _ctx.MoneyService.OnMoneyChanged += RefreshEconomy;

        SwitchTab("pallet");
        RefreshEconomy();

        //Debug.Log("[ToolsWindow] Ready. Press backtick (`) to open.");
    }

    // Null-safe element wiring helper — logs a warning if the element isn't found
    private static void Wire<T>(string name, VisualElement root, System.Action<T> setup) where T : VisualElement
    {
        var el = root.Q<T>(name);
        if (el != null) setup(el);
        else Debug.LogWarning($"[ToolsWindow] Element '{name}' not found in UXML.");
    }

    private void Update()
    {
        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            if (_visible) Hide();
            else Show("dev");
        }

        // Drag: screen X is left→right (matches panel), screen Y is bottom→top (opposite of panel)
        if (_dragging)
        {
            if (Mouse.current.leftButton.isPressed)
            {
                var screenDelta = (Vector2)Mouse.current.position.ReadValue() - _dragStartScreen;
                _window.style.left = _windowStartPos.x + screenDelta.x;
                _window.style.top  = _windowStartPos.y - screenDelta.y;
            }
            else
            {
                _dragging = false;
            }
        }

        if (!_visible || _ctx == null) return;

        var t = _ctx.TimeService;
        _time.text    = $"Day {t.Day}  —  {t.Hour:D2}:{t.Minute:D2}";
        _speed.text   = t.TimeScale == 0f ? "PAUSED" : $"{t.TimeScale}×";
        _objects.text = PlacedObjectRegistry.Count.ToString();

        if (_fsm != null)
        {
            _state.text = _fsm.CurrentState?.GetType().Name ?? "—";
            _stack.text = _fsm.DebugStackDepth.ToString();
            _undo.text  = $"{_fsm.History.UndoCount} / {_fsm.History.RedoCount}";
        }
    }

    // ── Public API (called by PalletBuilder) ─────────────────────

    public void OpenForPallet(PalletBuilder pb)
    {
        _targetBuilder = pb;
        PopulatePrefabDropdown();
        RefreshPalletUI();
        Show("pallet");
    }

    public void Hide()
    {
        _visible = false;
        _window.style.display = DisplayStyle.None;
    }

    // ── Tabs ──────────────────────────────────────────────────────

    private void Show(string tab)
    {
        _visible = true;
        _window.style.display = DisplayStyle.Flex;
        SwitchTab(tab);
    }

    private void SwitchTab(string tab)
    {
        bool pallet = tab == "pallet";
        _contentPallet.style.display = pallet ? DisplayStyle.Flex : DisplayStyle.None;
        _contentDev.style.display    = pallet ? DisplayStyle.None : DisplayStyle.Flex;

        SetTabActive(_tabPallet, pallet);
        SetTabActive(_tabDev,   !pallet);
    }

    private static void SetTabActive(Button btn, bool active)
    {
        btn.RemoveFromClassList(active ? "tools-tab--inactive" : "tools-tab--active");
        btn.AddToClassList(active ? "tools-tab--active" : "tools-tab--inactive");
    }

    // ── Dev console ───────────────────────────────────────────────

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

    // ── Pallet builder ────────────────────────────────────────────

    private void PopulatePrefabDropdown()
    {
        if (_pltPrefab == null) return;
        _palletPrefabs.Clear();
        var names = new List<string>();

        var buildMenu = FindAnyObjectByType<BuildMenuUI>();
        if (buildMenu?.registry == null) return;

        int selectedIndex = 0;
        foreach (var so in buildMenu.registry.buttonSOs)
        {
            if (so == null || so.category != "Inventory" || so.prefab == null || so.objName.Contains("Chep")) continue;
            _palletPrefabs.Add(so);
            names.Add(so.objName);
            if (_targetBuilder != null && _targetBuilder.casePrefab == so.prefab)
                selectedIndex = names.Count - 1;
        }

        _pltPrefab.choices = names;
        if (names.Count > 0) _pltPrefab.index = selectedIndex;
    }

    private void RefreshPalletUI()
    {
        if (_targetBuilder == null) return;
        _pltMaxHeight.value = _targetBuilder.maxTotalHeight.ToString(CultureInfo.InvariantCulture);
        _pltSpace.value     = _targetBuilder.spaceBetweenCases.ToString(CultureInfo.InvariantCulture);
        _pltGap.value       = _targetBuilder.verticalGap.ToString(CultureInfo.InvariantCulture);
        _pltCrooked.value   = _targetBuilder.crookedCase.ToString(CultureInfo.InvariantCulture);
        _pltOverride.value  = _targetBuilder.useTiHiOverride;
        _pltManualT.value   = _targetBuilder.manualTi.ToString();
        _pltManualH.value   = _targetBuilder.manualHi.ToString();
    }

    private void OnPalletBuild()
    {
        if (_targetBuilder == null) return;

        if (_pltPrefab != null && _pltPrefab.index >= 0 && _pltPrefab.index < _palletPrefabs.Count)
            _targetBuilder.casePrefab = _palletPrefabs[_pltPrefab.index].prefab;

        if (float.TryParse(_pltMaxHeight.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float h)) _targetBuilder.maxTotalHeight    = h;
        if (float.TryParse(_pltSpace.value,     NumberStyles.Float, CultureInfo.InvariantCulture, out float s)) _targetBuilder.spaceBetweenCases  = s;
        if (float.TryParse(_pltGap.value,       NumberStyles.Float, CultureInfo.InvariantCulture, out float g)) _targetBuilder.verticalGap         = g;
        if (float.TryParse(_pltCrooked.value,   NumberStyles.Float, CultureInfo.InvariantCulture, out float c)) _targetBuilder.crookedCase         = c;

        _targetBuilder.useTiHiOverride = _pltOverride.value;
        if (int.TryParse(_pltManualT.value, out int ti)) _targetBuilder.manualTi = ti;
        if (int.TryParse(_pltManualH.value, out int hi)) _targetBuilder.manualHi = hi;

        _targetBuilder.Build();
    }

    // ── Drag ──────────────────────────────────────────────────────

    private void OnTitlebarDown(PointerDownEvent evt)
    {
        // Walk up from the actual click target — if any ancestor up to the titlebar
        // is a Button, don't start drag (handles close btn and any other buttons added later)
        var el = evt.target as VisualElement;
        while (el != null && el != _titlebar)
        {
            if (el is Button) return;
            el = el.parent;
        }

        _dragging        = true;
        _dragStartScreen = Mouse.current.position.ReadValue();
        _windowStartPos  = new Vector2(_window.resolvedStyle.left, _window.resolvedStyle.top);

        if (float.IsNaN(_windowStartPos.x))
        {
            var root  = _doc.rootVisualElement;
            float right = _window.resolvedStyle.right;
            _windowStartPos.x = float.IsNaN(right)
                ? 12f
                : root.resolvedStyle.width - _window.resolvedStyle.width - right;
        }

        _window.style.right = StyleKeyword.Auto;
        _window.style.left  = _windowStartPos.x;
        _window.style.top   = _windowStartPos.y;
    }

    private void OnDestroy()
    {
        Instance = null;
        if (_ctx != null)
            _ctx.MoneyService.OnMoneyChanged -= RefreshEconomy;
    }
}
