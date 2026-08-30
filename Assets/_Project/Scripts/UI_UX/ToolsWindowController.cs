using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Unified Tools Window — Dev Console + Dev Settings.
/// Press 1 to toggle Dev Console.
/// Clicking a pallet in the scene opens Dev Settings focused on Pallet Builder.
/// Clicking an agent opens Dev Settings focused on that agent's components.
/// Right-click or clicking an unrelated object clears the current selection.
/// Implements IUIPanel for keybinding exclusivity via UIKeyBindingManager.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ToolsWindowController : MonoBehaviour, IUIPanel
{
    public static ToolsWindowController Instance { get; private set; }

    private UIDocument _doc;
    private VisualElement _window;
    private Button _scaleButton;
    private bool _visible;
    private ResizableWindow _resizeWindow;

    // Core services
    private GameContext _ctx;
    private PlacementStateMachine _fsm;
    private PlacementGrid _grid;
    private TruckYardManager _truckYard;

    [SerializeField] private CustomerRegistry _customerRegistry;

    // Tabs (PLT BUILDER removed — pallet settings now live in Dev Settings)
    private Button _tabDev, _tabSettings;
    private VisualElement _contentDev, _contentSettings;
    private bool _settingsBuilt;

    // Dev console labels
    private Label _balance, _hourly, _spent;
    private Label _time, _speed;
    private Label _objects, _undo;
    private Label _state, _stack;

    // Capacity Report labels
    private Label _capResTotal, _capResUtil, _capResAvail;
    private Label _capPickTotal, _capPickUtil, _capPickAvail;

    // Shipments display
    private VisualElement _shipmentsList;
    private string _shipSig = "\0";
    private HashSet<string> _expandedShipments = new();

    // Drag
    private VisualElement _titlebar;
    private bool _dragging;
    private Vector2 _dragStartScreen;
    private Vector2 _windowStartPos;
    private Vector2 _storedPosition = new Vector2(100f, 120f); // align with other UI panels, 120px down to clear TopBar

    // ── Script metadata ───────────────────────────────────────────────────────

    static readonly Dictionary<string, string> ScriptDescriptions = new()
    {
        { "FreeLookCamera",       "Player camera — movement, look and zoom sensitivity." },
        { "AiNavigation",         "NavMesh waypoint routing for a single agent." },
        { "AgentAnimation",       "Walk/idle animations, turn speed and arrival pause." },
        { "RatBehavior",          "Full rat AI: scurrying, hiding, breeding, scavenging." },
        { "WallVisibilityManager","Slides walls down (Cut mode) for a clear interior view." },
        { "LightPulse",           "Pulses emission between min and max intensity on a sine wave." },
        { "Gate_Open_Close",      "Rotates a gate arm open when agents enter the trigger." },
        { "NavMeshManager",       "Bakes NavMesh surfaces and manages the post-bake settle delay." },
        { "VehicleThrottleAudio", "3D engine audio for MHE — pitch rises with vehicle speed." },
        { "AmbientMumble",        "Random ambient voice clips from workers as 3D spatial audio." },
        { "PalletBuilder",        "Procedurally stacks cases onto a pallet in the best Ti-Hi layout." },
    };

    static readonly HashSet<string> ScanTypes = new()
    {
        "FreeLookCamera", "AiNavigation", "AgentAnimation", "RatBehavior",
        "WallVisibilityManager", "LightPulse", "Gate_Open_Close",
        "NavMeshManager", "VehicleThrottleAudio", "AmbientMumble", "PalletBuilder",
    };

    static readonly HashSet<string> GlobalTypes = new()
    {
        "FreeLookCamera", "NavMeshManager", "WallVisibilityManager",
    };

    static readonly HashSet<string> ObjectSpecificTypes = new()
    {
        "AiNavigation", "AgentAnimation", "RatBehavior",
        "VehicleThrottleAudio", "AmbientMumble", "LightPulse",
        "Gate_Open_Close", "PalletBuilder",
    };

    // Selected component per type name; cleared on unrelated click or right-click
    private readonly Dictionary<string, MonoBehaviour> _selectedComponents = new();
    // Reference to each built group element for scroll-to
    private readonly Dictionary<string, VisualElement> _settingsGroups = new();
    // Name of the type to scroll to on next settings open
    private string _pendingScrollTarget;

    // Undo state for PalletBuilder
    private int _prevManualTi = -1;
    private int _prevManualHi = -1;
    private PalletBuilder _undoPbReference;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        _doc = GetComponent<UIDocument>();
        // Own document, so covering the top bar is purely a sortingOrder question — at its old 50 the
        // HUD (999999) drew straight over it. Still below the toast, which has its own layer.
        if (_doc != null) _doc.sortingOrder = UILayers.WindowAboveHud;

        // Register with UIKeyBindingManager for keybinding exclusivity (key 1)
        if (UIKeyBindingManager.Instance != null)
            UIKeyBindingManager.Instance.RegisterUI(1, this);
    }

    private void OnEnable()
    {
        // Re-register with UIKeyBindingManager in case it was created after Awake
        if (UIKeyBindingManager.Instance != null)
            UIKeyBindingManager.Instance.RegisterUI(1, this);
    }

    private void Start()
    {
        _ctx       = FindAnyObjectByType<GameContext>();
        _fsm       = FindAnyObjectByType<PlacementStateMachine>();
        _grid      = FindAnyObjectByType<PlacementGrid>();
        _truckYard = FindAnyObjectByType<TruckYardManager>();

        var root = _doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;

        var overlay = root.Q("tools-overlay");
        if (overlay != null) overlay.pickingMode = PickingMode.Ignore;

        _window = root.Q("tools-window");
        if (_window == null) { Debug.LogError("[ToolsWindow] tools-window not found."); return; }
        _window.style.display = DisplayStyle.None;

        // Apply any position stored before Start() ran (e.g. from save load at startup)
        _window.style.right = StyleKeyword.Auto;
        _window.style.left  = _storedPosition.x;
        _window.style.top   = _storedPosition.y;

        // Add resizing capability (similar to Work Queue Panel)
        _resizeWindow = new ResizableWindow(_window, minW: 300f, minH: 250f, grip: 8f, titleInset: 32f);

        // Corner buttons come from PanelTitleChrome so this panel matches ContractsPanel (key 8)
        // exactly. The UXML's own "tools-scale" button is dropped in favour of the one the helper
        // builds — keeping both would leave two resize buttons on the row.
        var uxmlScale = root.Q<Button>("tools-scale");
        uxmlScale?.RemoveFromHierarchy();
        Wire<Button>("tools-close", root, b =>
        {
            var (scale, _) = PanelTitleChrome.Adopt(b, _resizeWindow, Hide);
            _scaleButton = scale;
        });
        Wire<Button>("tab-btn-dev",    root, b => { _tabDev      = b; b.clicked += () => SwitchTab("dev"); });
        Wire<Button>("tab-btn-settings", root, b => { _tabSettings = b; b.clicked += () => SwitchTab("settings"); });

        _contentDev      = root.Q("tab-content-dev");
        _contentSettings = root.Q("tab-content-settings");
        _titlebar        = root.Q("tools-titlebar");

        if (_titlebar != null)
            _titlebar.RegisterCallback<PointerDownEvent>(OnTitlebarDown);

        // Dev console labels
        _balance          = root.Q<Label>("stat-balance");
        _hourly           = root.Q<Label>("stat-hourly");
        _spent            = root.Q<Label>("stat-spent");
        _time             = root.Q<Label>("stat-time");
        _speed            = root.Q<Label>("stat-speed");
        _objects          = root.Q<Label>("stat-objects");
        _undo             = root.Q<Label>("stat-undo");
        _state            = root.Q<Label>("stat-state");
        _stack            = root.Q<Label>("stat-stack");

        // Capacity Report
        _capResTotal      = root.Q<Label>("cap-res-total");
        _capResUtil       = root.Q<Label>("cap-res-util");
        _capResAvail      = root.Q<Label>("cap-res-avail");
        _capPickTotal     = root.Q<Label>("cap-pick-total");
        _capPickUtil      = root.Q<Label>("cap-pick-util");
        _capPickAvail     = root.Q<Label>("cap-pick-avail");

        // Inbound Simulator
        Wire<Button>("btn-spawn-delivery", root, b => b.clicked += SpawnInboundTruck);
        Wire<Button>("btn-create-test-pallets", root, b => b.clicked += CreateTestPallets);
        // btn-create-test-order / btn-spawn-outbound-truck are no longer in this window's UXML —
        // the Contracts panel's Customers tab calls CreateTestOrder()/SpawnOutboundTruckDebug()
        // directly through ToolsWindowController.Instance.
        Wire<Button>("btn-clear-scene", root, b => b.clicked += ClearScene);
        _shipmentsList = root.Q("shipments-list");

        Wire<Button>("btn-add-1k",   root, b => b.clicked += () => _ctx?.MoneyService.Refund(1_000,   "Debug"));
        Wire<Button>("btn-add-10k",  root, b => b.clicked += () => _ctx?.MoneyService.Refund(10_000,  "Debug"));
        Wire<Button>("btn-add-100k", root, b => b.clicked += () => _ctx?.MoneyService.Refund(100_000, "Debug"));
        Wire<Button>("btn-zero",     root, b => b.clicked += () => _ctx?.MoneyService.SetMoney(0));
        Wire<Button>("btn-pause",    root, b => b.clicked += () => _ctx?.TimeService.SetTimeScale(0f));
        Wire<Button>("btn-1x",       root, b => b.clicked += () => _ctx?.TimeService.SetTimeScale(1f));
        Wire<Button>("btn-2x",       root, b => b.clicked += () => _ctx?.TimeService.SetTimeScale(2f));
        Wire<Button>("btn-5x",       root, b => b.clicked += () => _ctx?.TimeService.SetTimeScale(5f));
        Wire<Button>("btn-rebuild",  root, b => b.clicked += () =>
        {
            _grid?.RebuildFromRegistry();
            ServiceLocator.Get<EconomyService>()?.RebuildFromRegistry();
        });
        Wire<Button>("btn-clear",    root, b => b.clicked += ClearAll);

        if (_ctx != null)
            _ctx.MoneyService.OnMoneyChanged += RefreshEconomy;

        SlotRegistry.OnRegistryChanged += RefreshCapacityReport;
        LocationStatusRegistry.OnStatusChanged += RefreshCapacityReport;
        InventoryService.OnPalletMoved += (r, f, t) => RefreshCapacityReport();

        SwitchTab("dev");
        RefreshEconomy();
        RefreshCapacityReport();
        ApplySavedGlobalSettings();
    }

    private static void Wire<T>(string name, VisualElement root, System.Action<T> setup) where T : VisualElement
    {
        var el = root.Q<T>(name);
        if (el != null) setup(el);
        else Debug.LogWarning($"[ToolsWindow] Element '{name}' not found.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!UIModalGuard.IsCapturing && Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            // Route through UIKeyBindingManager for exclusivity
            if (UIKeyBindingManager.Instance != null)
                UIKeyBindingManager.Instance.ToggleUI(1);
            else if (_visible && IsTabActive("dev"))
                Hide();
            else
                Show("dev");
        }

        // Drag
        if (_dragging)
        {
            if (Mouse.current.leftButton.isPressed)
            {
                var panelNow = RuntimePanelUtils.ScreenToPanel(
                    _doc.rootVisualElement.panel, Mouse.current.position.ReadValue());
                var d = panelNow - _dragStartScreen;
                float nx = _windowStartPos.x + d.x;
                float ny = _windowStartPos.y - d.y;
                _window.style.left = nx;
                _window.style.top  = ny;
                _storedPosition    = new Vector2(nx, ny);
            }
            else _dragging = false;
        }

        // Scene object selection / deselection for Dev Settings
        if (_visible && IsTabActive("settings") && !UIInputGuard.IsPointerOverUIToolkit())
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Ctrl-click specifically targets PalletBuilder and forces a rebuild
                bool isCtrl = Keyboard.current.ctrlKey.isPressed;
                TrySelectObjectForSettings(clearUnmatched: !isCtrl);
            }
            // Right-click is reserved for camera control while Dev Settings is open — it must
            // NOT clear/rebuild the panel (that used to fire on every camera-orbit right-click,
            // silently swapping in an arbitrary "first found" PalletBuilder).
        }

        if (!_visible || _ctx == null) return;

        // Refresh shipments list
        RefreshShipments();

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

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by PalletBuilder when a pallet is clicked.
    /// Opens Dev Settings and scrolls to the Pallet Builder section.
    /// </summary>
    public void OpenForPallet(PalletBuilder pb)
    {
        _selectedComponents["PalletBuilder"] = pb;
        _pendingScrollTarget = "PalletBuilder";
        RebuildSettings();
        Show("settings");
    }

    /// <summary>IUIPanel.Show: opens the dev tools at the dev settings tab.</summary>
    public void Show() => Show("dev");

    public void Hide()
    {
        _visible = false;
        _window.style.display = DisplayStyle.None;
    }

    /// <summary>Call after a save loads (or any full-scene teardown/rebuild) — every pallet/vehicle
    /// GameObject the player had previously ctrl-clicked gets destroyed and recreated, but
    /// _selectedComponents held onto the old (now-destroyed) references. Left uncleared, the panel
    /// falls back to "first PalletBuilder FindObjectsByType happens to return" whenever it's opened
    /// before a fresh click, which reads as "stuck on [some arbitrary item]" after every load.</summary>
    public void ClearSelections()
    {
        _selectedComponents.Clear();
        if (_settingsBuilt) RebuildSettings();
    }

    /// <summary>IUIPanel implementation: true if this panel is currently visible.</summary>
    public bool IsOpen => _visible;

    /// <summary>Returns the last known panel-space position of the window.</summary>
    public Vector2 GetWindowPosition() => _storedPosition;

    /// <summary>True when the cursor is inside the visible window bounds (reliable bounds check).</summary>
    public bool IsPointerOverWindow()
    {
        if (!_visible || _window == null) return false;
        var panel = _doc.rootVisualElement.panel;
        if (panel == null) return false;
        var mousePanel = RuntimePanelUtils.ScreenToPanel(panel, UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        return _window.worldBound.Contains(mousePanel);
    }

    /// <summary>Moves the window to a saved position.</summary>
    public void SetWindowPosition(float x, float y)
    {
        _storedPosition = new Vector2(x, y);
        if (_window == null) return;
        _window.style.right = StyleKeyword.Auto;
        _window.style.left  = x;
        _window.style.top   = y;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tabs
    // ─────────────────────────────────────────────────────────────────────────

    private void Show(string tab)
    {
        _visible = true;
        _window.style.display = DisplayStyle.Flex;
        SwitchTab(tab);
        _resizeWindow?.ResetToNormal();
        // ResetToNormal drops the panel back to 1x, so the glyph has to follow or the button shows
        // "restore" on a window that is already normal size.
        PanelTitleChrome.SyncScaleGlyph(_scaleButton, _resizeWindow);
    }

    private bool IsTabActive(string tab)
    {
        if (tab == "settings") return _contentSettings?.style.display == DisplayStyle.Flex;
        return _contentDev?.style.display == DisplayStyle.Flex;
    }

    private void SwitchTab(string tab)
    {
        bool isDev      = tab == "dev";
        bool isSettings = tab == "settings";

        if (_contentDev      != null) _contentDev.style.display      = isDev      ? DisplayStyle.Flex : DisplayStyle.None;
        if (_contentSettings != null) _contentSettings.style.display = isSettings ? DisplayStyle.Flex : DisplayStyle.None;

        if (_tabDev      != null) SetTabActive(_tabDev,      isDev);
        if (_tabSettings != null) SetTabActive(_tabSettings, isSettings);

        if (isSettings)
        {
            if (!_settingsBuilt) BuildDevSettingsUI();
            // Scroll to pending target after a layout frame
            if (_pendingScrollTarget != null)
            {
                string target = _pendingScrollTarget;
                _pendingScrollTarget = null;
                _contentSettings?.schedule.Execute(() => ScrollToGroup(target)).ExecuteLater(50);
            }
        }
    }

    private static void SetTabActive(Button btn, bool active)
    {
        btn.RemoveFromClassList(active ? "tools-tab--inactive" : "tools-tab--active");
        btn.AddToClassList(active ? "tools-tab--active" : "tools-tab--inactive");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Dev Settings — dynamic UI
    // ─────────────────────────────────────────────────────────────────────────

    private void RebuildSettings()
    {
        _settingsBuilt = false;
        _settingsGroups.Clear();
        if (_contentSettings == null)
        {
            Debug.LogError("[ToolsWindow] _contentSettings is NULL!");
            return;
        }
        _contentSettings.Clear();
        BuildDevSettingsUI();
    }

    private void BuildDevSettingsUI()
    {
        if (_contentSettings == null) return;
        _contentSettings.Clear();
        _settingsGroups.Clear();

        // ── Global toggles ──────────────────────────────────────────────────
        var globalSection = new VisualElement();
        globalSection.AddToClassList("ds-global-section");
        var globalTitle = new Label("GLOBAL");
        globalTitle.AddToClassList("ds-global-title");
        RuntimeTooltip.Attach(globalTitle, "Scene-wide toggles that affect all objects.");
        globalSection.Add(globalTitle);

        // Graphics preset — centered label above buttons
        var presetBlock = new VisualElement();
        presetBlock.style.flexDirection = FlexDirection.Column;
        presetBlock.style.marginBottom = 10;
        var presetLbl = new Label("Graphics Preset");
        presetLbl.style.fontSize = 18;
        presetLbl.style.color = new Color(0.808f, 0.882f, 0.941f, 1f); // #CFE2F0
        presetLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
        presetLbl.style.alignSelf = Align.Stretch;
        presetLbl.style.marginBottom = 6;
        RuntimeTooltip.Attach(presetLbl, "Switches the full graphics quality preset for the current session. Saved across sessions.");
        presetBlock.Add(presetLbl);
        var presetBtns = new VisualElement();
        presetBtns.style.flexDirection = FlexDirection.Row;
        foreach (var (label, preset) in new[]{ ("ULTRA","Ultra"), ("GOOD","Good"), ("TOASTER","Toaster") })
        {
            var btn = new UnityEngine.UIElements.Button();
            btn.text = label;
            btn.AddToClassList("dev-btn");
            btn.AddToClassList(preset == "Ultra" ? "dev-btn-gold" : preset == "Good" ? "dev-btn-teal" : "dev-btn-danger");
            btn.style.flexGrow = 1;
            btn.style.marginRight = 4;
            string p = preset;
            btn.clicked += () =>
            {
                var mgr = FindAnyObjectByType<GraphicsPresetManager>();
                if (mgr != null) mgr.ApplyPreset((GraphicsPresetManager.Preset)System.Enum.Parse(
                    typeof(GraphicsPresetManager.Preset), p));
                else Debug.LogWarning("[DevSettings] GraphicsPresetManager not found in scene.");
            };
            presetBtns.Add(btn);
        }
        presetBlock.Add(presetBtns);
        globalSection.Add(presetBlock);

        globalSection.Add(BuildGlobalToggleRow(
            "Show Guidance Lines",
            "Show/hide path guidance lines on all NavMesh agents.",
            () => FindObjectsByType<NavAgentGuidance>(),
            (c, v) => ((NavAgentGuidance)c).showGuidanceLine = v,
            c => ((NavAgentGuidance)c).showGuidanceLine,
            "DevSettings_ShowGuidanceLines"));

        globalSection.Add(BuildGlobalToggleRow(
            "Show Waypoints",
            "Show/hide the visual waypoint markers in the scene.",
            () => FindObjectsByType<Waypoint>(),
            (c, v) => { foreach (var r in ((Waypoint)c).GetComponentsInChildren<MeshRenderer>()) r.enabled = v; },
            c => { var r = ((Waypoint)c).GetComponentInChildren<MeshRenderer>(); return r != null && r.enabled; },
            "DevSettings_ShowWaypoints"));

        globalSection.Add(BuildGlobalToggleRow(
            "Object Hover Popup",
            "Disables the object hover pop up window.",
            () => FindObjectsByType<WorldHoverPopupUI>(),
            (c, v) => ((WorldHoverPopupUI)c).SetEnabled(v),
            c => ((WorldHoverPopupUI)c).IsEnabled,
            "DevSettings_ObjectHoverPopup"));

        globalSection.Add(BuildStaticToggleRow(
            "Pause Game while working on Orders",
            "While checked, pauses the game (Time.timeScale = 0) whenever the Purchasing or "
            + "Contracts panel is open, and resumes it once both are closed. Does nothing while unchecked.",
            () => OrdersPauseGate.Enabled,
            OrdersPauseGate.SetEnabled));

        _contentSettings.Add(globalSection);

        // ── Script sections ─────────────────────────────────────────────────
        var allBehaviours = FindObjectsByType<MonoBehaviour>();
        // Collect (type, target) pairs then sort — WallVisibilityManager goes last
        var groups = new List<(System.Type type, MonoBehaviour target)>();
        var seen   = new HashSet<System.Type>();

        foreach (var mb in allBehaviours)
        {
            if (mb == null) continue;
            var t = mb.GetType();
            if (!ScanTypes.Contains(t.Name) || seen.Contains(t)) continue;
            seen.Add(t);
            MonoBehaviour target = mb;
            if (ObjectSpecificTypes.Contains(t.Name) &&
                _selectedComponents.TryGetValue(t.Name, out var sel) && sel != null)
                target = sel;
            groups.Add((t, target));
        }

        // Explicit display order — PalletBuilder moved to end (bottom)
        var displayOrder = new List<string>
        {
            "FreeLookCamera", "Gate_Open_Close", "AiNavigation", "AgentAnimation",
            "VehicleThrottleAudio", "AmbientMumble", "RatBehavior", "LightPulse",
            "NavMeshManager", "WallVisibilityManager", "PalletBuilder",
        };
        groups.Sort((a, b) =>
        {
            int ia = displayOrder.IndexOf(a.type.Name);
            int ib = displayOrder.IndexOf(b.type.Name);
            if (ia < 0) ia = 999;
            if (ib < 0) ib = 999;
            return ia.CompareTo(ib);
        });

        foreach (var (type, target) in groups)
        {
            var group = BuildScriptGroup(type, target);
            if (group != null)
            {
                _settingsGroups[type.Name] = group;
                _contentSettings.Add(group);
            }
        }

        _settingsBuilt = true;
    }

    private VisualElement BuildGlobalToggleRow(
        string label, string tooltip,
        System.Func<MonoBehaviour[]> getAll,
        System.Action<MonoBehaviour, bool> setter,
        System.Func<MonoBehaviour, bool> getter,
        string prefsKey = null)
    {
        var row = new VisualElement(); row.AddToClassList("ds-row");
        var lbl = new Label(label); lbl.AddToClassList("ds-label"); RuntimeTooltip.Attach(lbl, tooltip);
        row.Add(lbl);
        var all = getAll();
        bool cur = all.Length > 0 && getter(all[0]);
        var toggle = new Toggle { value = cur };
        toggle.AddToClassList("ds-toggle");
        toggle.RegisterValueChangedCallback(evt =>
        {
            foreach (var c in getAll()) setter(c, evt.newValue);
            if (prefsKey != null) PlayerPrefs.SetInt(prefsKey, evt.newValue ? 1 : 0);
        });
        row.Add(toggle);
        return row;
    }

    /// <summary>
    /// Same look as BuildGlobalToggleRow, but for a setting backed by a static getter/setter pair
    /// rather than a scanned set of scene MonoBehaviours — e.g. OrdersPauseGate, which has no
    /// component instance to reflect a field on.
    /// </summary>
    private VisualElement BuildStaticToggleRow(
        string label, string tooltip,
        System.Func<bool> getter,
        System.Action<bool> setter)
    {
        var row = new VisualElement(); row.AddToClassList("ds-row");
        var lbl = new Label(label); lbl.AddToClassList("ds-label"); RuntimeTooltip.Attach(lbl, tooltip);
        row.Add(lbl);
        var toggle = new Toggle { value = getter() };
        toggle.AddToClassList("ds-toggle");
        toggle.RegisterValueChangedCallback(evt => setter(evt.newValue));
        row.Add(toggle);
        return row;
    }

    private void ApplySavedGlobalSettings()
    {
        if (PlayerPrefs.HasKey("DevSettings_ShowGuidanceLines"))
        {
            bool v = PlayerPrefs.GetInt("DevSettings_ShowGuidanceLines") == 1;
            foreach (var c in FindObjectsByType<NavAgentGuidance>())
                c.showGuidanceLine = v;
        }
        if (PlayerPrefs.HasKey("DevSettings_ShowWaypoints"))
        {
            bool v = PlayerPrefs.GetInt("DevSettings_ShowWaypoints") == 1;
            foreach (var wp in FindObjectsByType<Waypoint>())
                foreach (var r in wp.GetComponentsInChildren<MeshRenderer>())
                    r.enabled = v;
        }
        if (PlayerPrefs.HasKey("DevSettings_ObjectHoverPopup"))
        {
            bool v = PlayerPrefs.GetInt("DevSettings_ObjectHoverPopup") == 1;
            foreach (var c in FindObjectsByType<WorldHoverPopupUI>())
                c.SetEnabled(v);
        }
    }

    private VisualElement BuildScriptGroup(System.Type type, MonoBehaviour target)
    {
        var fields = GetTunableFields(type);
        if (fields.Count == 0) return null;

        bool isSpecific = ObjectSpecificTypes.Contains(type.Name);
        bool hasSelection = !isSpecific ||
            (_selectedComponents.TryGetValue(type.Name, out var sel) && sel != null);

        var group = new VisualElement();
        group.AddToClassList("ds-group");
        if (isSpecific && !hasSelection) group.AddToClassList("ds-group--inactive");

        var title = new Label(FormatName(type.Name).ToUpper());
        title.AddToClassList("ds-group-title");
        if (ScriptDescriptions.TryGetValue(type.Name, out string desc)) RuntimeTooltip.Attach(title, desc);

        group.Add(title);

        // FreeLookCamera's move/zoom/focal-height speed and orbit/pitch sensitivity are no
        // longer [SerializeField] (see CameraDevSettings) — the generic reflection rows below
        // can no longer see them. These six are the single authoritative place to edit them,
        // with the explicit ranges/types the design calls for instead of GetFloatRange's guesses.
        if (type.Name == "FreeLookCamera")
        {
            // Create a container for FreeLookCamera content so it can be collapsible
            var contentContainer = new VisualElement();
            contentContainer.style.flexDirection = FlexDirection.Column;
            contentContainer.style.display = DisplayStyle.None; // Start minimized

            contentContainer.Add(BuildIntSettingRow("Move Speed",
                CameraDevSettings.MoveSpeedMin, CameraDevSettings.MoveSpeedMax,
                () => CameraDevSettings.MoveSpeed, v => CameraDevSettings.MoveSpeed = v));
            contentContainer.Add(BuildFloatSettingRow("Zoom Speed",
                CameraDevSettings.ZoomSpeedMin, CameraDevSettings.ZoomSpeedMax,
                () => CameraDevSettings.ZoomSpeed, v => CameraDevSettings.ZoomSpeed = v));
            contentContainer.Add(BuildFloatSettingRow("Pitch Sensitivity",
                CameraDevSettings.PitchSensitivityMin, CameraDevSettings.PitchSensitivityMax,
                () => CameraDevSettings.PitchSensitivity, v => CameraDevSettings.PitchSensitivity = v));
            contentContainer.Add(BuildFloatSettingRow("Orbit Sensitivity",
                CameraDevSettings.OrbitSensitivityMin, CameraDevSettings.OrbitSensitivityMax,
                () => CameraDevSettings.OrbitSensitivity, v => CameraDevSettings.OrbitSensitivity = v));
            contentContainer.Add(BuildFloatSettingRow("Rotate Speed (Q/E)",
                CameraDevSettings.RotateSpeedMin, CameraDevSettings.RotateSpeedMax,
                () => CameraDevSettings.RotateSpeed, v => CameraDevSettings.RotateSpeed = v));
            contentContainer.Add(BuildFloatSettingRow("Minimum Camera Height",
                CameraDevSettings.MinCameraHeightMin, CameraDevSettings.MinCameraHeightMax,
                () => CameraDevSettings.MinCameraHeight, v => CameraDevSettings.MinCameraHeight = v));

            // Add reflection-based fields to the content
            foreach (var field in fields)
            {
                var row = BuildFieldRow(field, target);
                if (row != null) contentContainer.Add(row);
            }

            group.Add(contentContainer);

            // Make title clickable to collapse/expand with gray text when collapsed
            bool isExpanded = false;
            title.style.color = new Color(0.6f, 0.7f, 0.8f, 0.5f); // Gray and transparent when minimized
            title.RegisterCallback<PointerDownEvent>(_ =>
            {
                isExpanded = !isExpanded;
                contentContainer.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                title.style.color = isExpanded ? new Color(0.8f, 0.9f, 1f, 1f) : new Color(0.6f, 0.7f, 0.8f, 0.5f);
            });
            return group;
        }
        // NavMeshManager - collapsible like FreeLookCamera
        else if (type.Name == "NavMeshManager")
        {
            var contentContainer = new VisualElement();
            contentContainer.style.flexDirection = FlexDirection.Column;
            contentContainer.style.display = DisplayStyle.None;

            bool isExpanded = false;
            title.style.color = new Color(0.6f, 0.7f, 0.8f, 0.5f); // Gray and transparent when minimized
            title.RegisterCallback<PointerDownEvent>(_ =>
            {
                isExpanded = !isExpanded;
                contentContainer.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                title.style.color = isExpanded ? new Color(0.8f, 0.9f, 1f, 1f) : new Color(0.6f, 0.7f, 0.8f, 0.5f);
            });

            group.Add(contentContainer);

            // Reflection-based fields go into the container
            foreach (var field in fields)
            {
                var row = BuildFieldRow(field, target);
                if (row != null) contentContainer.Add(row);
            }
            return group;
        }
        // WallVisibilityManager - collapsible like FreeLookCamera
        else if (type.Name == "WallVisibilityManager")
        {
            var contentContainer = new VisualElement();
            contentContainer.style.flexDirection = FlexDirection.Column;
            contentContainer.style.display = DisplayStyle.None;

            bool isExpanded = false;
            title.style.color = new Color(0.6f, 0.7f, 0.8f, 0.5f); // Gray and transparent when minimized
            title.RegisterCallback<PointerDownEvent>(_ =>
            {
                isExpanded = !isExpanded;
                contentContainer.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                title.style.color = isExpanded ? new Color(0.8f, 0.9f, 1f, 1f) : new Color(0.6f, 0.7f, 0.8f, 0.5f);
            });

            group.Add(contentContainer);

            // Reflection-based fields go into the container
            foreach (var field in fields)
            {
                var row = BuildFieldRow(field, target);
                if (row != null) contentContainer.Add(row);
            }
            return group;
        }

        // Custom PalletBuilder UI (redesigned 2026-07-05)
        if (type.Name == "PalletBuilder")
        {
            return BuildPalletBuilderSection((PalletBuilder)target);
        }

        if (isSpecific && !hasSelection)
        {
            var hint = new Label("↑ click an object in the scene to edit");
            hint.AddToClassList("ds-hint");
            group.Add(hint);
        }
        else
        {
            if (isSpecific && target != null)
            {
                var hint = new Label($"Editing: {target.gameObject.name}");
                hint.AddToClassList("ds-hint");
                hint.style.color = new Color(0.7f, 0.9f, 0.7f, 0.7f);
                group.Add(hint);
            }
            foreach (var field in fields)
            {
                var row = BuildFieldRow(field, target);
                if (row != null) group.Add(row);
            }
        }
        return group;
    }

    private void ScrollToGroup(string typeName)
    {
        if (!_settingsGroups.TryGetValue(typeName, out var group)) return;
        var scroll = _doc.rootVisualElement.Q<ScrollView>("tools-scroll");
        scroll?.ScrollTo(group);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scene object selection
    // ─────────────────────────────────────────────────────────────────────────

    private void TrySelectObjectForSettings(bool clearUnmatched)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        var go = hit.collider.gameObject;
        var searchRoot = go.transform.root.gameObject;
        bool isCtrl = Keyboard.current.ctrlKey.isPressed;
        bool anyChanged = false;

        // Special case: ctrl-clicking a LOOSE item (not a pallet itself — e.g. a case sitting on
        // a shelf) transfers its SKU onto the currently-open PalletBuilder. Ctrl-clicking a pallet
        // directly is a select/open gesture already handled by PalletBuilder.OnMouseDown — running
        // the transfer there too would let this method's own (separate) raycast silently overwrite
        // whichever pallet OnMouseDown just opened if the two raycasts ever disagree (e.g. two
        // pallets' colliders overlapping), which is what caused the "stuck on one SKU" bug.
        bool hitOwnPallet = searchRoot.GetComponentInChildren<PalletBuilder>() != null;
        if (isCtrl && !hitOwnPallet && _selectedComponents.TryGetValue("PalletBuilder", out var currentPb) && currentPb != null)
        {
            var pb = (PalletBuilder)currentPb;
            SkuData foundSku = DockPalletUtility.GetSkuForPallet(go) ?? MatchObjectToSku(go);

            if (foundSku != null && pb.linkedSku != foundSku)
            {
                pb.linkedSku = foundSku;
                pb.casePrefab = foundSku.Prefab;
                pb.SaveBuildState();
                anyChanged = true;
            }
        }

        // Standard selection logic — PalletBuilder is excluded here on purpose: it has its own
        // dedicated Ctrl+Click handler (PalletBuilder.OnMouseDown -> OpenForPallet). This loop used
        // to have no modifier gate at all, so ANY click (plain left-click, or a Shift+Click meant
        // for the Slot Assignment panel) that happened to land on a pallet would silently reselect
        // and rebuild the Pallet Builder section — that's what looked like "shift-click still opens
        // it" and "plain click switches items."
        foreach (var typeName in ObjectSpecificTypes)
        {
            if (typeName == "PalletBuilder") continue;

            MonoBehaviour found = null;
            foreach (var mb in searchRoot.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb.GetType().Name == typeName) { found = mb; break; }

            if (found != null)
            {
                if (_selectedComponents.ContainsKey(typeName) && _selectedComponents[typeName] == found) continue;
                _selectedComponents[typeName] = found;
                anyChanged = true;
            }
            else if (clearUnmatched && _selectedComponents.ContainsKey(typeName))
            {
                _selectedComponents.Remove(typeName);
                anyChanged = true;
            }
        }

        if (anyChanged) RebuildSettings();
    }

    private SkuData MatchObjectToSku(GameObject go)
    {
        // Try matching the object itself
        var skus = Resources.LoadAll<SkuData>("Inventory/SKUs");
        string cleanName = go.name.Replace("(Clone)", "").Trim();
        foreach (var sku in skus)
        {
            if (sku.Prefab != null && sku.Prefab.name == cleanName) return sku;
        }
        // Try children
        foreach (Transform child in go.transform)
        {
            string cName = child.name.Replace("(Clone)", "").Trim();
            foreach (var sku in skus)
                if (sku.Prefab != null && sku.Prefab.name == cName) return sku;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Field controls
    // ─────────────────────────────────────────────────────────────────────────

    private VisualElement BuildFieldRow(FieldInfo field, MonoBehaviour target)
    {
        var row = new VisualElement(); row.AddToClassList("ds-row");
        var label = new Label(FormatName(field.Name)); label.AddToClassList("ds-label");
        var tooltipAttr = field.GetCustomAttribute<TooltipAttribute>();
        if (tooltipAttr != null) RuntimeTooltip.Attach(label, tooltipAttr.tooltip);
        row.Add(label);

        VisualElement control = null;
        if (field.FieldType == typeof(float))      control = BuildFloatControl(field, target);
        else if (field.FieldType == typeof(int))   control = BuildIntControl(field, target);
        else if (field.FieldType == typeof(bool))  control = BuildBoolControl(field, target);
        else return null;

        if (control != null) row.Add(control);
        return row;
    }

    private VisualElement BuildFloatControl(FieldInfo field, MonoBehaviour target)
    {
        float val = (float)(field.GetValue(target) ?? 0f);
        var (min, max) = GetFloatRange(field);
        float pct = max > min ? Mathf.Clamp01((val - min) / (max - min)) : 0f;

        var col = new VisualElement(); col.AddToClassList("ds-slider-row");
        var vLabel = new Label(val.ToString("F2")); vLabel.AddToClassList("ds-slider-value"); col.Add(vLabel);

        var (wrap, fill, thumb) = MakeSliderWrap();
        SetSliderVisuals(fill, thumb, pct);

        var slider = new Slider(min, max) { value = val };
        slider.AddToClassList("ds-slider-overlay");
        slider.RegisterValueChangedCallback(evt =>
        {
            field.SetValue(target, evt.newValue);
            float p = max > min ? Mathf.Clamp01((evt.newValue - min) / (max - min)) : 0f;
            SetSliderVisuals(fill, thumb, p);
            vLabel.text = evt.newValue.ToString("F2");
        });
        wrap.Add(slider); col.Add(wrap);
        return col;
    }

    private VisualElement BuildIntControl(FieldInfo field, MonoBehaviour target)
    {
        int val = (int)(field.GetValue(target) ?? 0);
        var (min, max) = GetFloatRange(field);
        float pct = max > min ? Mathf.Clamp01((val - min) / (max - min)) : 0f;

        var col = new VisualElement(); col.AddToClassList("ds-slider-row");
        var vLabel = new Label(val.ToString()); vLabel.AddToClassList("ds-slider-value"); col.Add(vLabel);

        var (wrap, fill, thumb) = MakeSliderWrap();
        SetSliderVisuals(fill, thumb, pct);

        var slider = new SliderInt((int)min, (int)max) { value = val };
        slider.AddToClassList("ds-slider-overlay");
        slider.RegisterValueChangedCallback(evt =>
        {
            field.SetValue(target, evt.newValue);
            float p = max > min ? Mathf.Clamp01((evt.newValue - min) / (max - min)) : 0f;
            SetSliderVisuals(fill, thumb, p);
            vLabel.text = evt.newValue.ToString();
        });
        wrap.Add(slider); col.Add(wrap);
        return col;
    }

    private VisualElement BuildBoolControl(FieldInfo field, MonoBehaviour target)
    {
        var toggle = new Toggle { value = (bool)(field.GetValue(target) ?? false) };
        toggle.AddToClassList("ds-toggle");
        toggle.RegisterValueChangedCallback(evt => field.SetValue(target, evt.newValue));
        return toggle;
    }

    // Same visual shape as BuildFieldRow/BuildFloatControl/BuildIntControl, but bound via a
    // getter/setter pair instead of reflection — for settings (e.g. CameraDevSettings) backed
    // by something other than a MonoBehaviour's own serialized field.
    private VisualElement BuildFloatSettingRow(string label, float min, float max,
        System.Func<float> getter, System.Action<float> setter)
    {
        var row = new VisualElement(); row.AddToClassList("ds-row");
        var lbl = new Label(label); lbl.AddToClassList("ds-label"); row.Add(lbl);

        float val = getter();
        float pct = max > min ? Mathf.Clamp01((val - min) / (max - min)) : 0f;

        var col = new VisualElement(); col.AddToClassList("ds-slider-row");
        var vLabel = new Label(val.ToString("F2")); vLabel.AddToClassList("ds-slider-value"); col.Add(vLabel);

        var (wrap, fill, thumb) = MakeSliderWrap();
        SetSliderVisuals(fill, thumb, pct);

        var slider = new Slider(min, max) { value = val };
        slider.AddToClassList("ds-slider-overlay");
        slider.RegisterValueChangedCallback(evt =>
        {
            setter(evt.newValue);
            float p = max > min ? Mathf.Clamp01((evt.newValue - min) / (max - min)) : 0f;
            SetSliderVisuals(fill, thumb, p);
            vLabel.text = evt.newValue.ToString("F2");
        });
        wrap.Add(slider); col.Add(wrap);
        row.Add(col);
        return row;
    }

    private VisualElement BuildIntSettingRow(string label, int min, int max,
        System.Func<int> getter, System.Action<int> setter)
    {
        var row = new VisualElement(); row.AddToClassList("ds-row");
        var lbl = new Label(label); lbl.AddToClassList("ds-label"); row.Add(lbl);

        int val = getter();
        float pct = max > min ? Mathf.Clamp01((float)(val - min) / (max - min)) : 0f;

        var col = new VisualElement(); col.AddToClassList("ds-slider-row");
        var vLabel = new Label(val.ToString()); vLabel.AddToClassList("ds-slider-value"); col.Add(vLabel);

        var (wrap, fill, thumb) = MakeSliderWrap();
        SetSliderVisuals(fill, thumb, pct);

        var slider = new SliderInt(min, max) { value = val };
        slider.AddToClassList("ds-slider-overlay");
        // Live visual feedback (fill bar + number) updates on every drag tick — cheap, no side
        // effects. The setter can be expensive (e.g. Ti/Hi triggers a full pallet rebuild + toast)
        // and used to fire on every tick too, which made dragging janky/hard to control.
        //
        // Committing only on pointer-up turned out to be unreliable: SliderInt's internal drag
        // manipulator captures the pointer and routes PointerUpEvent in a way a listener on this
        // element never reliably caught, even registered with TrickleDown. So this debounces
        // instead — the setter only fires ~200ms after the LAST value-changed event, which in
        // practice lands right after the player releases the mouse (no more ticks arrive once
        // they let go), without depending on any slider-internal event-routing details.
        IVisualElementScheduledItem commitTimer = null;
        slider.RegisterValueChangedCallback(evt =>
        {
            float p = max > min ? Mathf.Clamp01((float)(evt.newValue - min) / (max - min)) : 0f;
            SetSliderVisuals(fill, thumb, p);
            vLabel.text = evt.newValue.ToString();

            commitTimer?.Pause();
            int committedValue = evt.newValue;
            // StartingIn, not ExecuteLater — ExecuteLater only reschedules an item that has
            // already fired once; it does not delay a first run (see CLAUDE.md's own note on
            // this exact gotcha, hit twice before in this codebase already).
            commitTimer = slider.schedule.Execute(() => setter(committedValue)).StartingIn(200);
        });
        wrap.Add(slider); col.Add(wrap);
        row.Add(col);
        return row;
    }

    private static (VisualElement wrap, VisualElement fill, VisualElement thumb) MakeSliderWrap()
    {
        var wrap  = new VisualElement(); wrap.AddToClassList("ds-slider-wrap");
        var bg    = new VisualElement(); bg.AddToClassList("ds-track-bg");
        var fill  = new VisualElement(); fill.AddToClassList("ds-track-fill");
        var thumb = new VisualElement(); thumb.AddToClassList("ds-thumb");
        wrap.Add(bg); wrap.Add(fill); wrap.Add(thumb);
        return (wrap, fill, thumb);
    }

    private static void SetSliderVisuals(VisualElement fill, VisualElement thumb, float pct)
    {
        fill.style.width = Length.Percent(pct * 100f);
        thumb.style.left = Length.Percent(pct * 100f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Reflection helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static List<FieldInfo> GetTunableFields(System.Type type)
    {
        var result = new List<FieldInfo>();
        var flags  = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var f in type.GetFields(flags))
        {
            if (!IsSupportedType(f.FieldType)) continue;
            bool serialized = (f.IsPublic && f.GetCustomAttribute<HideInInspector>() == null)
                           || (!f.IsPublic && f.GetCustomAttribute<SerializeField>() != null);
            if (!serialized) continue;
            result.Add(f);
        }
        return result;
    }

    private static bool IsSupportedType(System.Type t)
        => t == typeof(float) || t == typeof(int) || t == typeof(bool);

    private static (float min, float max) GetFloatRange(FieldInfo f)
    {
        var r = f.GetCustomAttribute<RangeAttribute>();
        if (r != null) return (r.min, r.max);
        string n = f.Name.ToLower();
        if (n.Contains("speed") || n.Contains("velocity"))      return (0, 50);
        if (n.Contains("timescale"))                            return (0, 10);
        if (n.Contains("duration") || n.Contains("delay"))      return (0, 30);
        if (n.Contains("distance") || n.Contains("range"))      return (0, 50);
        if (n.Contains("radius")   || n.Contains("diameter"))   return (0, 20);
        if (n.Contains("angle")    || n.Contains("rot"))        return (-180, 180);
        if (n.Contains("intensity"))                            return (0, 600);
        if (n.Contains("volume"))                               return (0, 1);
        if (n.Contains("pitch"))                                return (0.1f, 3);
        if (n.Contains("chance")   || n.Contains("probability"))return (0, 100);
        if (n.Contains("height")   || n.Contains("width"))      return (0, 20);
        if (n.Contains("count")    || n.Contains("population")) return (0, 50);
        if (n.Contains("sensitivity"))                          return (0, 20);
        if (n.Contains("scale") && !n.Contains("time"))         return (0, 5);
        if (f.FieldType == typeof(int))                         return (0, 100);
        return (0, 100);
    }

    private static string FormatName(string name)
    {
        if (name.StartsWith("_")) name = name.Substring(1);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
            else if (name[i] == '_') { sb.Append(' '); continue; }
            sb.Append(name[i]);
        }
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(sb.ToString().Trim().ToLower());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Dev console helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshEconomy()
    {
        if (_ctx == null) return;
        var m = _ctx.MoneyService;
        _balance.text = $"${m.CurrentCapital:N0}";
        _hourly.text  = $"${m.TotalHourlyCost:N0}/hr";
        _spent.text   = $"${m.SpentToday:N0}";
    }

    private void RefreshCapacityReport()
    {
        if (_capResTotal == null) return;

        int resTotal = 0, resUtil = 0, resAvail = 0;
        int pickTotal = 0, pickUtil = 0, pickAvail = 0;

        foreach (var slot in SlotRegistry.AllSlots)
        {
            bool isPick = slot.IsPick;
            bool isAvailable = LocationStatusRegistry.IsAvailable(slot.Address);
            
            if (isPick)
            {
                pickTotal++;
                if (isAvailable) pickAvail++;
                else pickUtil++;
            }
            else
            {
                resTotal++;
                if (isAvailable) resAvail++;
                else resUtil++;
            }
        }

        _capResTotal.text  = resTotal.ToString();
        _capResUtil.text   = resUtil.ToString();
        _capResAvail.text  = resAvail.ToString();

        _capPickTotal.text = pickTotal.ToString();
        _capPickUtil.text  = pickUtil.ToString();
        _capPickAvail.text = pickAvail.ToString();
    }

    /// <summary>"Clear Scene" button — lives in the Inbound Simulator section, so its job is
    /// resetting DOCK/INVENTORY test state, not the whole warehouse. Previously destroyed every
    /// PlacedObjectRegistry entry unconditionally, which included employees (they carry a
    /// PlacedObject too, for the hover popup) and every placed building/rack/wall — a much bigger
    /// blast radius than intended. Now scoped to category "Inventory" (pallets) only, plus the
    /// underlying InventoryService records and ShipmentService PO list those pallets came from.
    /// Employees, buildings, racks, equipment, and money are never touched by this.</summary>
    private void ClearAll()
    {
        foreach (var obj in PlacedObjectRegistry.GetSnapshot())
        {
            if (obj == null || obj.data == null) continue;
            if (obj.data.category != "Inventory") continue;
            Destroy(obj.gameObject);
        }
        _grid?.RebuildFromRegistry();
        ServiceLocator.Get<EconomyService>()?.RebuildFromRegistry();

        if (ServiceLocator.TryGet<ShipmentService>(out var shipmentService))
            shipmentService.ClearAll();

        if (ServiceLocator.TryGet<InventoryService>(out var inventoryService))
            inventoryService.ClearAllPallets();

        // Wiping the pallets without wiping the queue left every Receive/Putaway task pointing at a
        // PalletId that no longer resolved. Those tasks stayed claimable and were handed back out on a
        // loop by ReleaseStaleAssignments, failing every time. Both other callers of ClearAllPallets
        // (TestPalletSpawner, PlacementSystem's save restore) already pair it with ClearAllTasks; this
        // one was the outlier.
        if (ServiceLocator.TryGet<WorkQueueSystem>(out var workQueueSystem))
            workQueueSystem.ClearAllTasks();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Drag
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTitlebarDown(PointerDownEvent evt)
    {
        var el = evt.target as VisualElement;
        while (el != null && el != _titlebar) { if (el is Button) return; el = el.parent; }
        _dragging = true;
        _dragStartScreen = RuntimePanelUtils.ScreenToPanel(
            _doc.rootVisualElement.panel, Mouse.current.position.ReadValue());
        _windowStartPos = new Vector2(_window.resolvedStyle.left, _window.resolvedStyle.top);
        if (float.IsNaN(_windowStartPos.x))
        {
            var root = _doc.rootVisualElement;
            float right = _window.resolvedStyle.right;
            _windowStartPos.x = float.IsNaN(right) ? 12f
                : root.resolvedStyle.width - _window.resolvedStyle.width - right;
        }
        _window.style.right = StyleKeyword.Auto;
        _window.style.left  = _windowStartPos.x;
        _window.style.top   = _windowStartPos.y;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Custom PalletBuilder section (redesigned 2026-07-05)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Applies a Ti/Hi slider drag and immediately validates it against what PHYSICALLY
    /// fits. Before this, dragging the slider only ever set manualTi/manualHi — useTiHiOverride
    /// was never turned on anywhere in this UI, so Build() took its auto-calculate branch and
    /// silently ignored the slider entirely; the number shown could disagree with the pallet
    /// forever. Now every drag turns override on, rebuilds for real, and PalletBuilder.Build()'s
    /// own self-correction (see the Ti/Hi ground-truth work) snaps manualTi/manualHi back down to
    /// what actually fit if the requested count was unachievable — surfaced here as a toast so the
    /// player knows why the slider just moved on its own.</summary>
    private void ApplyTiHiChange(PalletBuilder pb, int requestedTi, int requestedHi)
    {
        if (pb.manualTi != requestedTi || pb.manualHi != requestedHi)
        {
            _prevManualTi = pb.manualTi;
            _prevManualHi = pb.manualHi;
            _undoPbReference = pb;
        }

        pb.useTiHiOverride = true;
        pb.manualTi = requestedTi;
        pb.manualHi = requestedHi;
        pb.Build(deductMoney: false);

        if (pb.manualTi != requestedTi || pb.manualHi != requestedHi)
            UIToast.Show($"Only {pb.manualTi} × {pb.manualHi} actually fits on this pallet — adjusted from {requestedTi} × {requestedHi}.");

        RebuildSettings();
    }

    private VisualElement BuildPalletBuilderSection(PalletBuilder pb)
    {
        if (pb == null) return null;

        var group = new VisualElement();
        group.AddToClassList("ds-group");
        group.style.paddingTop = 2;
        group.style.paddingBottom = 2;

        var title = new Label("PALLET BUILDER");
        title.AddToClassList("ds-group-title");
        title.style.fontSize = 20;
        title.style.marginBottom = 4;
        group.Add(title);

        // ── SKU Display with Preview (Half-Half Layout) ──────────────────
        var skuRow = new VisualElement();
        skuRow.style.flexDirection = FlexDirection.Row;
        skuRow.style.marginBottom = 4;
        skuRow.style.height = 70;

        // Left side: SKU info (50%)
        var skuInfoContainer = new VisualElement();
        skuInfoContainer.style.flexGrow = 1;
        skuInfoContainer.style.flexBasis = Length.Percent(50);
        skuInfoContainer.style.flexDirection = FlexDirection.Column;

        var skuLbl = new Label("Selected Item:");
        skuLbl.style.fontSize = 11;
        skuLbl.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        skuInfoContainer.Add(skuLbl);

        var skuValue = new Label("");
        skuValue.style.color = new Color(0.9f, 0.95f, 1f, 1f);
        skuValue.style.fontSize = 12;
        skuValue.style.whiteSpace = WhiteSpace.Normal;
        skuValue.style.unityFontStyleAndWeight = FontStyle.Bold;
        skuInfoContainer.Add(skuValue);

        skuRow.Add(skuInfoContainer);

        // Right side: Case preview image (50%)
        var previewContainer = new VisualElement();
        previewContainer.style.flexGrow = 1;
        previewContainer.style.flexBasis = Length.Percent(50);
        previewContainer.style.height = 65;
        previewContainer.style.borderBottomColor = new Color(0.5f, 0.8f, 1f, 1f); // Light blue border
        previewContainer.style.borderTopColor = new Color(0.5f, 0.8f, 1f, 1f);
        previewContainer.style.borderLeftColor = new Color(0.5f, 0.8f, 1f, 1f);
        previewContainer.style.borderRightColor = new Color(0.5f, 0.8f, 1f, 1f);
        previewContainer.style.borderBottomWidth = 1;
        previewContainer.style.borderTopWidth = 1;
        previewContainer.style.borderLeftWidth = 1;
        previewContainer.style.borderRightWidth = 1;
        previewContainer.style.marginLeft = 4;
        previewContainer.style.backgroundColor = new Color(0.1f, 0.15f, 0.2f, 0.5f);

        var casePreview = new Image();
        casePreview.style.flexGrow = 1;
        casePreview.scaleMode = ScaleMode.ScaleToFit;
        previewContainer.Add(casePreview);

        skuRow.Add(previewContainer);
        group.Add(skuRow);

        // Resolve current SKU
        SkuData currentSku = pb.linkedSku;
        if (currentSku == null) currentSku = DockPalletUtility.GetSkuForPallet(pb.gameObject);

        if (currentSku != null)
        {
            skuValue.text = $"{currentSku.ItemNumber}\n{currentSku.ItemDescription}";
            
            // Show the icon immediately — the prefab preview below is async and often isn't
            // ready yet on the first request for a given prefab this session.
            if (currentSku.Icon != null)
                casePreview.sprite = currentSku.Icon;

#if UNITY_EDITOR
            if (currentSku.Prefab != null)
            {
                var prefabForPreview = currentSku.Prefab;
                var tex = UnityEditor.AssetPreview.GetAssetPreview(prefabForPreview);
                if (tex != null)
                {
                    casePreview.image = tex;
                }
                else
                {
                    // AssetPreview.GetAssetPreview() renders asynchronously and returns null while
                    // Unity is still generating the thumbnail — this is what showed the icon
                    // instead of the case image on the first ctrl-click of a session. Poll until
                    // it's actually ready, then swap it in without a full panel rebuild.
                    void PollForPreview()
                    {
                        if (casePreview.panel == null) return; // panel rebuilt/closed since — stop polling
                        var t = UnityEditor.AssetPreview.GetAssetPreview(prefabForPreview);
                        if (t != null)
                            casePreview.image = t;
                        else if (UnityEditor.AssetPreview.IsLoadingAssetPreview(prefabForPreview.GetInstanceID()))
                            _contentSettings.schedule.Execute(PollForPreview).ExecuteLater(100);
                    }
                    _contentSettings.schedule.Execute(PollForPreview).ExecuteLater(100);
                }
            }
#endif
        }
        else
        {
            skuValue.text = "No SKU Selected";
        }

        // ── Settings Rows (Compacted) ───────────────────────────────────────
        
        // Max Total Height
        var heightRow = BuildFloatSettingRow("Max Total Height", 0.5f, 2.5f,
            () => pb.maxTotalHeight,
            v => { pb.maxTotalHeight = v; pb.SaveBuildState(); });
        heightRow.style.marginBottom = 2;
        RuntimeTooltip.Attach(heightRow.Q<Label>(className: "ds-label"), "Target maximum height for the pallet load (meters). Range: 0.5 - 2.5m");
        group.Add(heightRow);

        // Space Between Cases
        var spacingRow = BuildFloatSettingRow("Space Between Cases", 0.01f, 0.2f,
            () => pb.spaceBetweenCases,
            v => { pb.spaceBetweenCases = v; pb.SaveBuildState(); });
        spacingRow.style.marginBottom = 2;
        RuntimeTooltip.Attach(spacingRow.Q<Label>(className: "ds-label"), "Minimum horizontal gap between cases on a layer.");
        group.Add(spacingRow);

        // Vertical Gap
        var gapRow = BuildFloatSettingRow("Vertical Gap", 0f, 0.05f,
            () => pb.verticalGap,
            v => { pb.verticalGap = v; pb.SaveBuildState(); });
        gapRow.style.marginBottom = 2;
        RuntimeTooltip.Attach(gapRow.Q<Label>(className: "ds-label"), "Vertical space between stacked layers (meters). Range: 0 - 0.05m");
        group.Add(gapRow);

        // Crooked Cases
        var crookedRow = BuildFloatSettingRow("Crooked Cases", 0f, 10f,
            () => pb.crookedCase,
            v => pb.crookedCase = v);
        crookedRow.style.marginBottom = 2;
        RuntimeTooltip.Attach(crookedRow.Q<Label>(className: "ds-label"), "Adds random rotation deviation for a more natural, hand-stacked look.");
        group.Add(crookedRow);

        // ── Ti / Hi (Compacted) ─────────────────────────────────────────────
        PalletBuilder.ComputeTiHiFromLayout(pb.transform, out int curTi, out int curHi);

        if (curHi > 0 && (pb.manualTi != curTi || pb.manualHi != curHi))
        {
            pb.manualTi = curTi;
            pb.manualHi = curHi;
            pb.useTiHiOverride = true;
            pb.linkedSku?.SetTiHi(curTi, curHi);
            pb.SaveBuildState();
        }

        var tiRow = BuildIntSettingRow($"Ti (Current: {curTi})", 0, 30,
            () => pb.manualTi,
            v => ApplyTiHiChange(pb, requestedTi: v, requestedHi: pb.manualHi));
        tiRow.style.marginBottom = 2;
        RuntimeTooltip.Attach(tiRow.Q<Label>(className: "ds-label"), "Cases per layer. 0 = auto-calculate.");
        group.Add(tiRow);

        var hiRow = BuildIntSettingRow($"Hi (Current: {curHi})", 0, 30,
            () => pb.manualHi,
            v => ApplyTiHiChange(pb, requestedTi: pb.manualTi, requestedHi: v));
        hiRow.style.marginBottom = 2;
        RuntimeTooltip.Attach(hiRow.Q<Label>(className: "ds-label"), "Number of layers. 0 = auto-calculate.");
        group.Add(hiRow);

        // Optimizer Layer Utilization %
        var utilizationRow = new VisualElement(); utilizationRow.AddToClassList("ds-row");
        utilizationRow.style.marginBottom = 2;
        var utilizationLbl = new Label("Optimizer Layer Utilization"); utilizationLbl.AddToClassList("ds-label");
        RuntimeTooltip.Attach(utilizationLbl, "Efficiency: Total case footprint area / Pallet footprint (40\"x48\")");
        utilizationRow.Add(utilizationLbl);
        var utilizationValue = new Label("—%");
        utilizationValue.AddToClassList("ds-slider-value");
        utilizationValue.style.color = new Color(0.6f, 1f, 0.6f, 1f);

        // Calculate utilization: (CasesPerLayer * CaseArea) / PalletArea
        float utilPercent = 0f;
        if (currentSku != null)
        {
            float caseArea = currentSku.CaseLength * currentSku.CaseWidth;
            float palletArea = 1.016f * 1.2192f; // 40" x 48"
            
            var currentLoad = pb.transform.Find("PalletLoad");
            int actualCpl = 0;
            if (currentLoad != null && currentLoad.childCount > 0)
            {
                var firstLayerY = currentLoad.GetChild(0).localPosition.y;
                foreach (Transform child in currentLoad)
                    if (Mathf.Abs(child.localPosition.y - firstLayerY) < 0.01f) actualCpl++;
                utilPercent = (actualCpl * caseArea / palletArea) * 100f;
            }
        }
        utilizationValue.text = $"{utilPercent:F0}%";
        utilizationRow.Add(utilizationValue);
        group.Add(utilizationRow);

        // Total Pallet Height
        float caseHeight = currentSku != null ? currentSku.CaseHeight : pb.caseDimensions.y;
        float totalPltHeight = 0.165f + (curHi * caseHeight);

        var totalHeightRow = new VisualElement(); totalHeightRow.AddToClassList("ds-row");
        totalHeightRow.style.marginBottom = 6;
        var totalHeightLbl = new Label("Total Pallet Height"); totalHeightLbl.AddToClassList("ds-label");
        totalHeightRow.Add(totalHeightLbl);
        var totalHeightValue = new Label($"{totalPltHeight:F2}m");
        totalHeightValue.AddToClassList("ds-slider-value");

        // Color Logic: <1m Green, 1m-1.8m Orange, >1.8m Red
        if (totalPltHeight < 1.0f) totalHeightValue.style.color = new Color(0.31f, 0.78f, 0.39f); // Green
        else if (totalPltHeight <= 1.8f) totalHeightValue.style.color = new Color(0.93f, 0.79f, 0.16f); // Orange
        else totalHeightValue.style.color = new Color(0.86f, 0.24f, 0.24f); // Red

        totalHeightRow.Add(totalHeightValue);
        group.Add(totalHeightRow);

        // ── Buttons (Compacted) ─────────────────────────────────────────────
        var btnRow = new VisualElement();
        btnRow.style.flexDirection = FlexDirection.Row;
        btnRow.style.marginTop = 6;
        btnRow.style.height = 32;

        var buildAll = new Button(() => BuildAllDockPallets(pb)) { text = "Build All" };
        buildAll.AddToClassList("dev-btn"); buildAll.AddToClassList("dev-btn-gold");
        buildAll.style.flexGrow = 1; buildAll.style.marginRight = 2;
        btnRow.Add(buildAll);

        var undoBtn = new Button(() => {
            if (_undoPbReference == pb && _prevManualTi != -1) {
                pb.manualTi = _prevManualTi; pb.manualHi = _prevManualHi;
                RebuildSettings();
                UIToast.Show("Restored previous Ti/Hi");
            }
        }) { text = "Undo" };
        undoBtn.AddToClassList("dev-btn"); undoBtn.AddToClassList("dev-btn-teal");
        undoBtn.style.flexGrow = 1; undoBtn.style.marginRight = 2;
        btnRow.Add(undoBtn);

        var submitBtn = new Button(() => SubmitPalletTiHi(pb)) { text = "Submit" };
        submitBtn.AddToClassList("dev-btn"); submitBtn.AddToClassList("dev-btn-teal");
        submitBtn.style.flexGrow = 1;
        btnRow.Add(submitBtn);

        group.Add(btnRow);

        // Helper buttons row
        var helperRow = new VisualElement();
        helperRow.style.flexDirection = FlexDirection.Row;
        helperRow.style.marginTop = 4;
        helperRow.style.height = 28;

        var fixRot = new Button(() => DockPalletUtility.RotateAllDockPallets(-90f)) { text = "Fix Rot" };
        fixRot.AddToClassList("dev-btn"); fixRot.style.flexGrow = 1; fixRot.style.marginRight = 2;
        fixRot.style.backgroundColor = new Color(0.6f, 0.2f, 0.2f, 1f);
        helperRow.Add(fixRot);

        var recalcY = new Button(() => DockPalletUtility.RecalculateAllDockYPositions()) { text = "Recalc Y" };
        recalcY.AddToClassList("dev-btn"); recalcY.AddToClassList("dev-btn-teal");
        recalcY.style.flexGrow = 1;
        helperRow.Add(recalcY);

        group.Add(helperRow);

        return group;
    }

    private void UndoPalletBuild(PalletBuilder pb)
    {
        if (pb == null) return;
        var loadObj = pb.transform.Find("PalletLoad");
        if (loadObj != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(loadObj.gameObject);
            else Destroy(loadObj.gameObject);
#else
            Destroy(loadObj.gameObject);
#endif
        }
    }

    /// <summary>Rebuilds every dock pallet that matches the selected SKU, applying the template
    /// pallet's Ti/Hi and spacing settings. After rebuilding, recalculates all dock Y positions.</summary>
    private void BuildAllDockPallets(PalletBuilder pb)
    {
        if (pb == null || pb.linkedSku == null)
        {
            UIToast.Show("No SKU linked. Select an item from the dropdown first.");
            return;
        }

        int count = DockPalletUtility.RebuildAllDockPalletsWithSku(pb);
        if (count > 0)
            UIToast.Show($"Rebuilt {count} pallet(s) on the dock. Y positions recalculated.");
        else
            UIToast.Show("No matching pallets found on the dock for this SKU.");
    }

    private void SubmitPalletTiHi(PalletBuilder pb)
    {
        if (pb == null || pb.linkedSku == null)
        {
            UIToast.Show("No SKU linked. Cannot submit.");
            return;
        }

        // Update the SKU's master Ti/Hi values
#if UNITY_EDITOR
        var so = new UnityEditor.SerializedObject(pb.linkedSku);
        so.FindProperty("_ti").intValue = pb.manualTi;
        so.FindProperty("_hi").intValue = pb.manualHi;
        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(pb.linkedSku);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
        UIToast.Show($"✓ Ti={pb.manualTi} Hi={pb.manualHi} submitted to {pb.linkedSku.ItemDescription}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Shipments Display (Inbound Simulator)
    // ─────────────────────────────────────────────────────────────────────────

    private static int _inboundCounter = 0;

    // Debug shortcut: drop 14 test pallets (7 + 7 stacked) straight into a lane as ghosted/unreceived
    // inventory, skipping the whole truck arrive→dock→offload wait. A Receiver still has to receive
    // them, so the simulation stays real for data-persistence testing. See TestPalletSpawner.
    private void CreateTestPallets()
    {
        TestPalletSpawner.SpawnStackedTestPallets(10); // 10 pallets at a time per user request
    }

    // Debug shortcut: wipe every dock pallet + all inventory + pallet work tasks, leaving employees,
    // equipment, walls, racks, floors, and lanes exactly where they are. Clean slate for a fresh test.
    private void ClearScene()
    {
        TestPalletSpawner.ClearDockAndInventory();
    }

    private void SpawnInboundTruck()
    {
        if (!ServiceLocator.TryGet<ShipmentService>(out var shipmentService) || shipmentService == null)
        {
            Debug.LogError("[DevConsole] ShipmentService not available.");
            return;
        }
        if (!ServiceLocator.TryGet<InventoryService>(out var inventoryService) || inventoryService == null)
        {
            Debug.LogError("[DevConsole] InventoryService not available.");
            return;
        }

        _inboundCounter++;
        var items = RandomDeliveryGenerator.GenerateFullTrailerLoad(inventoryService);
        if (items.Count == 0)
        {
            Debug.LogWarning("[DevConsole] RandomDeliveryGenerator produced no items — check that SKUs have committed Ti/Hi.");
            return;
        }
        shipmentService.CreatePurchaseOrder($"SUPP_DEV{_inboundCounter}", "Dev Supplier", items);
    }

    // Debug shortcut: generate a batch of 3-5 randomized orders for one random customer (3-5 SKUs,
    // 5-10 cases per line) and register them with OrderService, Open and awaiting release — outbound
    // analog of SpawnInboundTruck. See OrderGenerator.
    //
    // PUBLIC because its button now lives on the Contracts panel's Customers tab rather than in this
    // window. It stays HERE rather than moving wholesale because the CustomerRegistry it needs is a
    // serialized field on this component — a panel built in code has no Inspector to assign one from.
    public void CreateTestOrder()
    {
        if (_customerRegistry == null)
        {
            Debug.LogError("[DevConsole] No CustomerRegistry assigned on ToolsWindowController.");
            return;
        }
        if (!ServiceLocator.TryGet<InventoryService>(out var inventoryService) || inventoryService == null)
        {
            Debug.LogError("[DevConsole] InventoryService not available.");
            return;
        }
        if (!ServiceLocator.TryGet<OrderService>(out var orderService) || orderService == null)
        {
            Debug.LogError("[DevConsole] OrderService not available.");
            return;
        }
        if (!ServiceLocator.TryGet<SimulationTimeService>(out var timeService) || timeService == null)
        {
            Debug.LogError("[DevConsole] SimulationTimeService not available.");
            return;
        }

        var orders = OrderGenerator.GenerateRandomOrdersForCustomer(_customerRegistry, inventoryService, timeService.Day, timeService.Minute);
        if (orders.Count == 0)
        {
            Debug.LogWarning("[DevConsole] OrderGenerator produced no orders — check that the CustomerRegistry and SKU catalog aren't empty.");
            return;
        }
        foreach (var order in orders)
            orderService.ReceiveOrder(order);

        UIToast.Show("Orders ready to be released");
    }

    // Debug shortcut: sends an outbound truck to the first door that currently has at least one
    // staged pallet waiting (found the same way TrailerLoadController itself finds them — scanning
    // for un-parented OutboundPalletBuilder instances and reading back which door's lane they're
    // sitting in). D1's "real" trigger (automatic, tied to order due dates) is a later polish pass;
    // for now this mirrors every other milestone's debug-triggered testing.
    /// <summary>
    /// Debug: put one new signable customer offer on the Contracts panel's Customers tab.
    ///
    /// Stands in for the real thing until reputation exists — offers are meant to arrive over time at
    /// a rate set by how well the business is run (late orders, cancellations, drivers left waiting,
    /// mispicks, damaged cases) and by difficulty. This just forces one to appear now.
    ///
    /// Prefers a customer with no offer already on the board so the list doesn't fill with duplicates
    /// of one company; falls back to any customer once the roster is exhausted (the ContractId still
    /// differs, so they stack as separate offers rather than colliding).
    ///
    /// Lives here rather than on the panel because the CustomerRegistry is a serialized field on this
    /// component and a code-built panel has no Inspector to assign one from.
    /// </summary>
    /// <returns>Company name of the offer added, or null if nothing could be generated.</returns>
    public string CreateTestCustomerOffer()
    {
        if (_customerRegistry == null || _customerRegistry.customers.Count == 0)
        {
            Debug.LogError("[DevConsole] No CustomerRegistry assigned on ToolsWindowController.");
            return null;
        }
        if (!ServiceLocator.TryGet<GameCore.Inventory.OrderArrivalService>(out var arrivals) || arrivals == null)
        {
            Debug.LogError("[DevConsole] OrderArrivalService not available.");
            return null;
        }

        var alreadyOffered = new System.Collections.Generic.HashSet<string>();
        foreach (var c in arrivals.Catalog)
            if (c?.Customer != null) alreadyOffered.Add(c.Customer.CustomerId);

        var pool = _customerRegistry.customers
            .Where(c => c != null && !alreadyOffered.Contains(c.CustomerId)).ToList();
        if (pool.Count == 0)
            pool = _customerRegistry.customers.Where(c => c != null).ToList();
        if (pool.Count == 0) return null;

        var customer = pool[UnityEngine.Random.Range(0, pool.Count)];

        // One in four is a one-off bulk drop; the rest are standing accounts. Terms are rolled so
        // successive presses produce genuinely different offers to compare, not the same card twice.
        bool bulk = UnityEngine.Random.value < 0.25f;
        int ordersMin = UnityEngine.Random.Range(1, 4);
        int casesMin = UnityEngine.Random.Range(4, 12);
        int lineMin = UnityEngine.Random.Range(2, 5);

        var contract = GameCore.Inventory.ContractData.CreateRuntime(
            contractId: $"Contract_Dev_{customer.CustomerId}_{++_devOfferCounter}",
            customer: customer,
            pitch: bulk
                ? "One trailer, full pallets, one payment. No case picking."
                : "A new account looking for a home. Terms are what they are — take it or leave it.",
            kind: bulk ? GameCore.Inventory.ContractKind.Bulk
                       : GameCore.Inventory.ContractKind.Recurring,
            palletCount: UnityEngine.Random.Range(6, 13),
            ordersPerDayMin: ordersMin,
            ordersPerDayMax: ordersMin + UnityEngine.Random.Range(0, 3),
            lineItemsMin: lineMin,
            lineItemsMax: lineMin + UnityEngine.Random.Range(1, 4),
            casesPerLineMin: casesMin,
            casesPerLineMax: casesMin + UnityEngine.Random.Range(2, 10),
            cutoffHour: UnityEngine.Random.Range(14, 20),
            leadTimeDays: UnityEngine.Random.Range(1, 4),
            payRateMultiplier: Mathf.Round(UnityEngine.Random.Range(0.75f, 1.45f) * 100f) / 100f,
            lateFeePercent: UnityEngine.Random.Range(0.10f, 0.40f),
            // Explicit rather than relying on the Daily default: a bulk offer must be OneTime so it
            // can't be mistaken for a standing account once IsBulk (not a separate wholesale flag)
            // is what OnHourChanged reads to decide whether a signed contract re-fires.
            frequency: bulk ? GameCore.Inventory.OrderFrequency.OneTime
                            : GameCore.Inventory.OrderFrequency.Daily);

        if (!arrivals.AddOffer(contract))
        {
            Debug.LogWarning($"[DevConsole] Offer {contract.ContractId} already on the board.");
            return null;
        }

        Debug.Log($"[DevConsole] New offer: {customer.CompanyName} ({contract.Title}).");
        return customer.CompanyName;
    }

    private int _devOfferCounter;

    // PUBLIC for the same reason as CreateTestOrder above — driven from the Customers tab now.
    public void SpawnOutboundTruckDebug()
    {
        if (_truckYard == null) { Debug.LogError("[DevConsole] TruckYardManager not found."); return; }
        var grid = _grid != null ? _grid : FindAnyObjectByType<PlacementGrid>();
        if (grid == null) { Debug.LogError("[DevConsole] PlacementGrid not found."); return; }

        int? doorWithStaged = null;
        foreach (var pallet in FindObjectsByType<OutboundPalletBuilder>())
        {
            if (pallet == null || pallet.transform.parent != null) continue;
            var cell = grid.WorldToCell(pallet.transform.position);
            if (!LaneNamingService.TryGetSlot(cell, out var slot)) continue;
            doorWithStaged = slot.DoorNumber;
            break;
        }

        if (!doorWithStaged.HasValue)
        {
            Debug.LogWarning("[DevConsole] No staged outbound pallets found at any door — nothing to send a truck for.");
            return;
        }

        if (!_truckYard.SpawnOutboundTruck(doorWithStaged.Value))
            Debug.LogWarning($"[DevConsole] Could not spawn outbound truck at door {doorWithStaged.Value} (door missing or already occupied).");
    }

    private void RefreshShipments()
    {
        if (_shipmentsList == null) return;
        if (!ServiceLocator.TryGet<ShipmentService>(out var svc) || svc == null) return;

        var shipments = svc.PendingShipments;
        var sb = new System.Text.StringBuilder();
        foreach (var s in shipments) sb.Append(s.PONumber).Append(s.Status).Append(s.LineItems.Count).Append('|');
        string sig = sb.ToString();
        if (sig == _shipSig) return;
        _shipSig = sig;

        _shipmentsList.Clear();
        if (shipments.Count == 0)
        {
            var none = new Label("(no inbound loads)");
            none.style.fontSize = 12;
            none.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            _shipmentsList.Add(none);
            return;
        }

        InventoryService inv = null;
        ServiceLocator.TryGet(out inv);

        foreach (var s in shipments)
            _shipmentsList.Add(BuildShipmentRow(s, inv));
    }

    private VisualElement BuildShipmentRow(ShipmentData s, InventoryService inv)
    {
        var container = new VisualElement();
        container.style.marginBottom = 4;

        bool expanded = _expandedShipments.Contains(s.PONumber);

        // Header (clickable pivot toggle) — color based on status
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.paddingTop = 4; header.style.paddingBottom = 4;
        header.style.paddingLeft = 6; header.style.paddingRight = 6;

        // Green for active, light red/pink for departed
        bool isDeparted = s.Status.ToString() == "Departed";
        Color headerBg = isDeparted
            ? new Color(0.6f, 0.2f, 0.3f, 0.9f)  // light red/pink
            : new Color(0.2f, 0.35f, 0.2f, 0.9f);  // green
        Color headerBgHi = isDeparted
            ? new Color(0.7f, 0.25f, 0.35f, 1f)
            : new Color(0.25f, 0.4f, 0.25f, 1f);

        header.style.backgroundColor = headerBg;
        header.style.borderTopLeftRadius = header.style.borderBottomLeftRadius = 4;
        header.style.borderTopRightRadius = header.style.borderBottomRightRadius = 4;

        var caret = new Label(expanded ? "▾" : "▸");
        caret.style.fontSize = 12;
        caret.style.color = Color.white;
        caret.style.width = 14;
        caret.style.marginRight = 4;
        header.Add(caret);

        var summary = new Label($"PO {s.PONumber}   {s.SupplierName}   [{s.Status}]   {s.LineItems.Count} pallets");
        summary.style.fontSize = 13;
        summary.style.color = Color.white;
        summary.style.flexGrow = 1;
        header.Add(summary);

        var detail = BuildShipmentDetail(s, inv);
        detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

        header.RegisterCallback<PointerEnterEvent>(_ => header.style.backgroundColor = headerBgHi);
        header.RegisterCallback<PointerLeaveEvent>(_ => header.style.backgroundColor = headerBg);
        header.RegisterCallback<PointerDownEvent>(_ =>
        {
            bool nowExpanded = detail.style.display == DisplayStyle.None;
            detail.style.display = nowExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            caret.text = nowExpanded ? "▾" : "▸";
            if (nowExpanded) _expandedShipments.Add(s.PONumber); else _expandedShipments.Remove(s.PONumber);
        });

        container.Add(header);
        container.Add(detail);
        return container;
    }

    private VisualElement BuildShipmentDetail(ShipmentData s, InventoryService inv)
    {
        var wrap = new VisualElement();
        wrap.style.backgroundColor = new Color(0.15f, 0.25f, 0.15f, 0.8f);
        wrap.style.paddingTop = 3; wrap.style.paddingBottom = 4;
        wrap.style.paddingLeft = 4; wrap.style.paddingRight = 4;
        wrap.style.borderBottomLeftRadius = wrap.style.borderBottomRightRadius = 4;

        wrap.Add(PivotRow("Item #", "Description", "Plts", "Cases", "Ti", "Hi", "Expected", "Received", isHeader: true, icon: null));

        var groups = s.LineItems.GroupBy(li => li.SkuId);
        foreach (var g in groups)
        {
            var sku = inv != null ? inv.GetSkuData(g.Key) : null;
            string desc = sku != null ? sku.ItemDescription : "—";
            string ti = sku != null ? sku.Ti.ToString() : "—";
            string hi = sku != null ? sku.Hi.ToString() : "—";
            int plts = g.Count();
            int cases = g.Sum(li => li.Quantity);
            int expectedQty = g.Sum(li => li.Quantity);
            int receivedQty = g.Sum(li => li.ReceivedQuantity);
            Sprite icon = sku != null ? sku.Icon : null;
            wrap.Add(PivotRow(g.Key, desc, plts.ToString(), cases.ToString(), ti, hi, expectedQty.ToString(), receivedQty.ToString(), isHeader: false, icon: icon));
        }
        return wrap;
    }

    private VisualElement PivotRow(string item, string desc, string plts, string cases, string ti, string hi, string expected, string received, bool isHeader, Sprite icon = null)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 1; row.style.paddingBottom = 1;

        Color textColor = isHeader ? new Color(0.8f, 0.8f, 0.8f, 1f) : Color.white;
        float fontSize = isHeader ? 11f : 10f;

        // Item number
        row.Add(DetailCell(item, 35, textColor, fontSize, TextAnchor.MiddleLeft));
        row.Add(DetailIconCell(icon, 18, isHeader));

        // Description - narrower to close the gap
        var d = DetailCell(desc, 0, textColor, fontSize, TextAnchor.MiddleLeft);
        d.style.flexGrow = 1; d.style.flexBasis = 70; d.style.overflow = Overflow.Hidden;
        d.style.marginRight = 4;
        row.Add(d);

        // Plts/Cases/Ti/Hi columns moved left and expanded
        row.Add(DetailCell(plts, 28, textColor, fontSize, TextAnchor.MiddleCenter));
        row.Add(DetailCell(cases, 32, textColor, fontSize, TextAnchor.MiddleCenter));
        row.Add(DetailCell(ti, 20, textColor, fontSize, TextAnchor.MiddleCenter));
        row.Add(DetailCell(hi, 20, textColor, fontSize, TextAnchor.MiddleCenter));

        // Expected and Received columns on the right - expanded to prevent header overlap
        row.Add(DetailCell(expected, 44, textColor, fontSize, TextAnchor.MiddleCenter));
        row.Add(DetailCell(received, 44, textColor, fontSize, TextAnchor.MiddleCenter));
        return row;
    }

    private VisualElement DetailCell(string text, float width, Color color, float fontSize, TextAnchor anchor)
    {
        var cell = new Label(text);
        cell.style.fontSize = fontSize;
        cell.style.color = color;
        cell.style.unityTextAlign = anchor;
        if (width > 0) cell.style.width = width;
        else cell.style.minWidth = 30;
        cell.style.paddingLeft = cell.style.paddingRight = 2;
        return cell;
    }

    private VisualElement DetailIconCell(Sprite icon, float size, bool isHeader)
    {
        var cell = new VisualElement();
        cell.style.width = size;
        cell.style.height = size;
        cell.style.marginLeft = 2;
        cell.style.marginRight = 2;
        if (!isHeader && icon != null)
        {
            cell.style.backgroundImage = new StyleBackground(icon);
            cell.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
        }
        return cell;
    }

    private void OnDestroy()
    {
        Instance = null;
        if (_ctx != null && _ctx.MoneyService != null) _ctx.MoneyService.OnMoneyChanged -= RefreshEconomy;
    }
}
