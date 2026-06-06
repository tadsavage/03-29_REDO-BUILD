using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Unified Tools Window — Dev Console + Dev Settings.
/// F2 = toggle Dev Settings | backtick not used.
/// Clicking a pallet in the scene opens Dev Settings focused on Pallet Builder.
/// Clicking an agent opens Dev Settings focused on that agent's components.
/// Right-click or clicking an unrelated object clears the current selection.
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

    // Tabs (PLT BUILDER removed — pallet settings now live in Dev Settings)
    private Button _tabDev, _tabSettings;
    private VisualElement _contentDev, _contentSettings;
    private bool _settingsBuilt;

    // Dev console labels
    private Label _balance, _hourly, _spent;
    private Label _time, _speed;
    private Label _objects, _undo;
    private Label _state, _stack;

    // Drag
    private VisualElement _titlebar;
    private bool _dragging;
    private Vector2 _dragStartScreen;
    private Vector2 _windowStartPos;
    private Vector2 _storedPosition = new Vector2(12f, 50f); // mirrors CSS default

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

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        _doc = GetComponent<UIDocument>();
    }

    private void Start()
    {
        _ctx  = FindAnyObjectByType<GameContext>();
        _fsm  = FindAnyObjectByType<PlacementStateMachine>();
        _grid = FindAnyObjectByType<PlacementGrid>();

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

        Wire<Button>("tools-close",    root, b => b.clicked += () => Hide());
        Wire<Button>("tab-btn-dev",    root, b => { _tabDev      = b; b.clicked += () => SwitchTab("dev"); });
        Wire<Button>("tab-btn-settings", root, b => { _tabSettings = b; b.clicked += () => SwitchTab("settings"); });

        _contentDev      = root.Q("tab-content-dev");
        _contentSettings = root.Q("tab-content-settings");
        _titlebar        = root.Q("tools-titlebar");

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
        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            if (_visible && IsTabActive("settings")) Hide();
            else Show("settings");
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
                TrySelectObjectForSettings(clearUnmatched: true);

            if (Mouse.current.rightButton.wasPressedThisFrame)
                ClearAllSelections();
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

    public void Hide()
    {
        _visible = false;
        _window.style.display = DisplayStyle.None;
    }

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
        _settingsBuilt = false;
        _settingsGroups.Clear();
        _contentSettings?.Clear();
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
        globalTitle.tooltip = "Scene-wide toggles that affect all objects.";
        globalSection.Add(globalTitle);

        // Graphics preset buttons
        var presetRow = new VisualElement(); presetRow.AddToClassList("ds-row");
        var presetLbl = new Label("Graphics Preset");
        presetLbl.AddToClassList("ds-label");
        presetLbl.tooltip = "Switches the full graphics quality preset for the current session. Saved across sessions.";
        presetRow.Add(presetLbl);
        var presetBtns = new VisualElement();
        presetBtns.style.flexDirection = FlexDirection.Row;
        presetBtns.style.flexGrow = 1.4f;
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
        presetRow.Add(presetBtns);
        globalSection.Add(presetRow);

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

        // Explicit display order — Gate_Open_Close and NavMeshManager swapped
        var displayOrder = new List<string>
        {
            "FreeLookCamera", "Gate_Open_Close", "AiNavigation", "AgentAnimation",
            "VehicleThrottleAudio", "AmbientMumble", "RatBehavior", "LightPulse",
            "NavMeshManager", "PalletBuilder", "WallVisibilityManager",
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
        // Search from the hierarchy root downward so we catch all components on
        // the agent regardless of which child collider was actually hit.
        var searchRoot = go.transform.root.gameObject;
        bool anyChanged = false;

        foreach (var typeName in ObjectSpecificTypes)
        {
            MonoBehaviour found = null;
            foreach (var mb in searchRoot.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb.GetType().Name == typeName) { found = mb; break; }

            if (found != null)
            {
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

    private void OnDestroy()
    {
        Instance = null;
        if (_ctx != null) _ctx.MoneyService.OnMoneyChanged -= RefreshEconomy;
    }
}
