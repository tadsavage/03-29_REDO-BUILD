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
    private bool _visible;

    // Core services
    private GameContext _ctx;
    private PlacementStateMachine _fsm;
    private PlacementGrid _grid;
    private TruckYardManager _truckYard;

    // Tabs (PLT BUILDER removed — pallet settings now live in Dev Settings)
    private Button _tabDev, _tabSettings;
    private VisualElement _contentDev, _contentSettings;
    private bool _settingsBuilt;

    // Dev console labels
    private Label _balance, _hourly, _spent;
    private Label _time, _speed;
    private Label _objects, _undo;
    private Label _state, _stack;

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
        new ResizableWindow(_window, minW: 300f, minH: 250f, grip: 8f, titleInset: 32f);

        Wire<Button>("tools-close",    root, b => b.clicked += () => Hide());
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

        // Inbound Simulator
        Wire<Button>("btn-spawn-delivery", root, b => b.clicked += SpawnInboundTruck);
        Wire<Button>("btn-create-test-pallets", root, b => b.clicked += CreateTestPallets);
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

        SwitchTab("dev");
        RefreshEconomy();
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
                // Shift-click specifically targets PalletBuilder and forces a rebuild
                bool isShift = Keyboard.current.shiftKey.isPressed;
                TrySelectObjectForSettings(clearUnmatched: !isShift);
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
                ClearAllSelections();
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
        Debug.Log($"[ToolsWindow] OpenForPallet called for: {pb.gameObject.name}");
        _selectedComponents["PalletBuilder"] = pb;
        _pendingScrollTarget = "PalletBuilder";
        RebuildSettings();
        Show("settings");
        Debug.Log($"[ToolsWindow] OpenForPallet complete - dropdown should now show {(pb.linkedSku != null ? pb.linkedSku.ItemNumber.ToString() : "unknown")}");
    }

    /// <summary>IUIPanel.Show: opens the dev tools at the dev settings tab.</summary>
    public void Show() => Show("dev");

    public void Hide()
    {
        _visible = false;
        _window.style.display = DisplayStyle.None;
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
        Debug.Log("[ToolsWindow] ===== RebuildSettings START =====");
        _settingsBuilt = false;
        _settingsGroups.Clear();
        if (_contentSettings != null)
        {
            Debug.Log($"[ToolsWindow] Before clear: {_contentSettings.childCount} children");
            _contentSettings.Clear();
            Debug.Log($"[ToolsWindow] After clear: {_contentSettings.childCount} children");
        }
        else
        {
            Debug.LogError("[ToolsWindow] _contentSettings is NULL!");
            return;
        }
        BuildDevSettingsUI();
        Debug.Log("[ToolsWindow] ===== RebuildSettings COMPLETE =====");
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
        globalTitle.tooltip = "Scene-wide toggles that affect all objects.";
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
        presetLbl.tooltip = "Switches the full graphics quality preset for the current session. Saved across sessions.";
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
                Debug.Log($"[ToolsWindow] Adding group: {type.Name}");
                _settingsGroups[type.Name] = group;
                _contentSettings.Add(group);
            }
        }

        Debug.Log($"[ToolsWindow] BuildDevSettingsUI complete. Added {_settingsGroups.Count} groups. Setting _settingsBuilt = true");
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
        var lbl = new Label(label); lbl.AddToClassList("ds-label"); lbl.tooltip = tooltip;
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
        if (ScriptDescriptions.TryGetValue(type.Name, out string desc)) title.tooltip = desc;

        group.Add(title);

        // FreeLookCamera's move/zoom/focal-height speed and orbit/pitch sensitivity are no
        // longer [SerializeField] (see CameraDevSettings) — the generic reflection rows below
        // can no longer see them. These five are the single authoritative place to edit them,
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
        bool isShift = Keyboard.current.shiftKey.isPressed;
        bool anyChanged = false;

        // Special case: if shift-clicking an object, try to extract SKU and apply to current PalletBuilder
        if (isShift && _selectedComponents.TryGetValue("PalletBuilder", out var currentPb) && currentPb != null)
        {
            var pb = (PalletBuilder)currentPb;
            // Try to find a SKU from the hit object or its children
            SkuData foundSku = DockPalletUtility.GetSkuForPallet(go);
            if (foundSku == null) 
            {
                // Try matching by prefab name directly if it's just a loose case
                foundSku = MatchObjectToSku(go);
            }

            if (foundSku != null && pb.linkedSku != foundSku)
            {
                Debug.Log($"[ToolsWindow] Shift-click transfer: Applying SKU {foundSku.ItemNumber} to {pb.gameObject.name}");
                pb.linkedSku = foundSku;
                pb.casePrefab = foundSku.Prefab;
                pb.SaveBuildState();
                anyChanged = true;
            }
        }

        // Standard selection logic
        foreach (var typeName in ObjectSpecificTypes)
        {
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

    private void ClearAllSelections()
    {
        if (_selectedComponents.Count == 0) return;
        _selectedComponents.Clear();
        RebuildSettings();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Field controls
    // ─────────────────────────────────────────────────────────────────────────

    private VisualElement BuildFieldRow(FieldInfo field, MonoBehaviour target)
    {
        var row = new VisualElement(); row.AddToClassList("ds-row");
        var label = new Label(FormatName(field.Name)); label.AddToClassList("ds-label");
        var tooltipAttr = field.GetCustomAttribute<TooltipAttribute>();
        if (tooltipAttr != null) label.tooltip = tooltipAttr.tooltip;
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
        slider.RegisterValueChangedCallback(evt =>
        {
            setter(evt.newValue);
            float p = max > min ? Mathf.Clamp01((float)(evt.newValue - min) / (max - min)) : 0f;
            SetSliderVisuals(fill, thumb, p);
            vLabel.text = evt.newValue.ToString();
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

    private void ClearAll()
    {
        foreach (var obj in PlacedObjectRegistry.GetSnapshot())
            if (obj != null) Destroy(obj.gameObject);
        _grid?.RebuildFromRegistry();
        ServiceLocator.Get<EconomyService>()?.RebuildFromRegistry();
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
            
            // Try to get a better preview than the icon if possible
            if (currentSku.Icon != null)
                casePreview.sprite = currentSku.Icon;

#if UNITY_EDITOR
            if (currentSku.Prefab != null)
            {
                var tex = UnityEditor.AssetPreview.GetAssetPreview(currentSku.Prefab);
                if (tex != null) casePreview.image = tex;
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
        heightRow.Q<Label>(className: "ds-label").tooltip = "Target maximum height for the pallet load (meters). Range: 0.5 - 2.5m";
        group.Add(heightRow);

        // Optimizer Utilization %
        var utilizationRow = new VisualElement(); utilizationRow.AddToClassList("ds-row");
        utilizationRow.style.marginBottom = 2;
        var utilizationLbl = new Label("Optimizer Utilization"); utilizationLbl.AddToClassList("ds-label");
        utilizationLbl.tooltip = "Efficiency: Total case footprint area / Pallet footprint (40\"x48\")";
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
            int cpl = pb.manualTi > 0 ? pb.manualTi : pb.TotalCases / Mathf.Max(1, pb.manualHi); // fallback if not auto
            // If we don't have a count yet, we'll try to get it from the last build
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

        // Space Between Cases
        var spacingRow = BuildFloatSettingRow("Space Between Cases", 0.01f, 0.2f,
            () => pb.spaceBetweenCases,
            v => { pb.spaceBetweenCases = v; pb.SaveBuildState(); });
        spacingRow.style.marginBottom = 2;
        spacingRow.Q<Label>(className: "ds-label").tooltip = "Minimum horizontal gap between cases on a layer.";
        group.Add(spacingRow);

        // Vertical Gap
        var gapRow = BuildFloatSettingRow("Vertical Gap", 0f, 0.05f,
            () => pb.verticalGap,
            v => { pb.verticalGap = v; pb.SaveBuildState(); });
        gapRow.style.marginBottom = 2;
        gapRow.Q<Label>(className: "ds-label").tooltip = "Vertical space between stacked layers (meters). Range: 0 - 0.05m";
        group.Add(gapRow);

        // Use Prefab Bounds
        var usePrefabRow = new VisualElement(); usePrefabRow.AddToClassList("ds-row");
        usePrefabRow.style.marginBottom = 2;
        var usePrefabLbl = new Label("Use Prefab Bounds"); usePrefabLbl.AddToClassList("ds-label");
        usePrefabLbl.tooltip = "ON: Use actual 3D model dimensions. OFF: Use manual dimensions from SKU master data.";
        usePrefabRow.Add(usePrefabLbl);
        var usePrefabToggle = new Toggle { value = pb.usePrefabBounds };
        usePrefabToggle.AddToClassList("ds-toggle");
        usePrefabToggle.RegisterValueChangedCallback(evt => { pb.usePrefabBounds = evt.newValue; pb.SaveBuildState(); });
        usePrefabRow.Add(usePrefabToggle);
        group.Add(usePrefabRow);

        // Crooked Cases
        var crookedRow = BuildFloatSettingRow("Crooked Cases", 0f, 10f,
            () => pb.crookedCase,
            v => pb.crookedCase = v);
        crookedRow.style.marginBottom = 2;
        crookedRow.Q<Label>(className: "ds-label").tooltip = "Adds random rotation deviation for a more natural, hand-stacked look.";
        group.Add(crookedRow);

        // ── Ti / Hi (Compacted) ─────────────────────────────────────────────
        int curTi = 0, curHi = 0;
        var load = pb.transform.Find("PalletLoad");
        if (load != null && load.childCount > 0)
        {
            var layersSet = new HashSet<float>();
            foreach (Transform t in load) layersSet.Add(Mathf.Round(t.localPosition.y * 100f) / 100f);
            curHi = layersSet.Count;
            if (curHi > 0)
            {
                float bottomY = layersSet.Min();
                foreach (Transform t in load)
                    if (Mathf.Abs(t.localPosition.y - bottomY) < 0.01f) curTi++;
            }
        }

        var tiRow = BuildIntSettingRow($"Ti (Current: {curTi})", 0, 30,
            () => pb.manualTi,
            v => { if (pb.manualTi != v) { _prevManualTi = pb.manualTi; _prevManualHi = pb.manualHi; _undoPbReference = pb; } pb.manualTi = v; });
        tiRow.style.marginBottom = 2;
        tiRow.Q<Label>(className: "ds-label").tooltip = "Cases per layer. 0 = auto-calculate.";
        group.Add(tiRow);

        var hiRow = BuildIntSettingRow($"Hi (Current: {curHi})", 0, 30,
            () => pb.manualHi,
            v => { if (pb.manualHi != v) { _prevManualTi = pb.manualTi; _prevManualHi = pb.manualHi; _undoPbReference = pb; } pb.manualHi = v; });
        hiRow.style.marginBottom = 2;
        hiRow.Q<Label>(className: "ds-label").tooltip = "Number of layers. 0 = auto-calculate.";
        group.Add(hiRow);

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
            Debug.Log("[PalletBuilder] Pallet load removed. Settings preserved.");
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
        Debug.Log($"[PalletBuilder] Submitted Ti={pb.manualTi} Hi={pb.manualHi} to SKU {pb.linkedSku.ItemNumber}");
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
