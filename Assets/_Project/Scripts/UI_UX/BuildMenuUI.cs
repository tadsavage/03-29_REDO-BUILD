using GameCore.Economy;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SaveLoadSystem;

public class BuildMenuUI : MonoBehaviour
{
    [Header("Save/Load UI")]
    [SerializeField] private SaveLoadWindowController saveLoadWindowController;

    [Header("Category Config")]
    [SerializeField] private List<CategoryConfig> categories = new();

    [Header("UXML")]
    [SerializeField] private VisualTreeAsset buildMenuUxml;
    [SerializeField] private VisualTreeAsset categoryButtonUxml;
    [SerializeField] private VisualTreeAsset itemButtonUxml;
    [SerializeField] private VisualTreeAsset submenuContainerUxml;
    [SerializeField] private VisualTreeAsset utilityButtonUxml;
    [Header("Styles")]
    [SerializeField] private StyleSheet buildMenuStyle;

    [Header("Save / Load")]
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] public ObjDataRegistry registry;
    private MoneyService moneyService;

    public GameContext Context { get; private set; }

    [SerializeField] private PlacementGrid grid;

    // Last clicked category button (for submenu alignment)
    private VisualElement _lastClickedCategoryButton;

    [Serializable]
    public class UtilityButtonConfig
    {
        public string id;
        public Texture2D icon;
    }

    [SerializeField] private List<UtilityButtonConfig> utilityButtons = new();

    // Events for other systems
    public Action<ObjDataSO> OnBuildItemClicked;
    public Action OnDeleteClicked;
    public Action OnMoveClicked;
    public Action OnUndoClicked;
    public Action OnRedoClicked;
    public Action OnCancelClicked;
    public Action OnRotateClicked;

    // UI Toolkit references
    private UIDocument _uiDoc;
    private VisualElement _root;
    private VisualElement _buildBar;
    private VisualElement _playBar;
    private VisualElement _categoryRow;
    private VisualElement _utilityRow;
    /// <summary>The play bar's own utility row. Present in the UXML since the play bar was added but
    /// never filled until now — see BuildUtilityButtons.</summary>
    private VisualElement _playUtilityRow;
    private VisualElement _submenuContainer;
    private ScrollView _submenuScroll;
    private VisualElement _modeTabs;
    private Button _tabBuild;
    private Button _tabPlay;

    // Hotkey number -> its play-bar button, for reflecting the open panel back onto the bar.
    private readonly Dictionary<int, Button> _playBarButtons = new();
    private int _lastSyncedOpenMask = int.MinValue;

    // State
    private CategoryConfig _activeCategory;
    private bool _submenuOpen;

    /// <summary>Which bottom bar is up. Build is the placement HUD; Play is the run-the-warehouse HUD.</summary>
    public enum HudMode { Build, Play }

    private HudMode _mode = HudMode.Build;

    /// <summary>Fires after the visible bar has actually swapped, so listeners can read the new mode.</summary>
    public Action<HudMode> OnHudModeChanged;

    /// <summary>CacheElements runs again from Initialize() when the bootstrapper wires us up after
    /// OnEnable. Without this the pointer guards and tab handlers get registered twice, and a double
    /// tab handler toggles the mode straight back on every click.</summary>
    private bool _barCallbacksRegistered;

    [Serializable]
    public class CategoryConfig
    {
        public string id;
        public string displayName;
        public Texture2D icon;
        public List<ObjDataSO> items;
    }

    // Stop the jank in the submenu
    private bool _submenuClosePending = false;
    private float _submenuCloseDelay = .50f;
    private IVisualElementScheduledItem _submenuCloseTask;

    public bool IsPointerOverBuildMenu { get; private set; }

    /// <summary>The live bottom-HUD menu, so other HUD pieces can parent themselves into the same
    /// document instead of floating in one of their own (see DevHudWindow).</summary>
    public static BuildMenuUI Instance { get; private set; }

    /// <summary>Root of the bottom HUD document. Null until OnEnable has run.</summary>
    public VisualElement Root => _root;

    /// <summary>The build bar itself. Adding here docks a widget INTO the bar's row layout — between
    /// the category buttons and the utility buttons, since the bar is space-between — rather than
    /// leaving it floating over the HUD.</summary>
    public VisualElement BuildBar => _buildBar;

    /// <summary>The play bar. Same chrome and layout as <see cref="BuildBar"/>, empty until play-mode
    /// tools exist — dock play-side widgets here.</summary>
    public VisualElement PlayBar => _playBar;

    /// <summary>Whichever bar is currently visible. Use this when a widget should follow the mode
    /// switch; use <see cref="BuildBar"/>/<see cref="PlayBar"/> to pin it to one mode.</summary>
    public VisualElement ActiveBar => _mode == HudMode.Build ? _buildBar : _playBar;

    /// <summary>Height of the bottom bar in the USS (.buildmenu-bottom-bar) plus its 2px top border.
    /// Anything anchored just above the bar measures from here.</summary>
    public const float BottomBarHeight = 122f;

    /// <summary>Where the Build/Play tab strip is anchored (.buildmenu-mode-tabs bottom) and how tall
    /// the taller, active tab is (.buildmenu-mode-tab-active height). Must stay in step with the USS —
    /// nothing enforces that, so change both together.</summary>
    public const float ModeTabsBottom = 120f;
    public const float ModeTabHeight = 32f;

    /// <summary>The strip along the bottom of the screen the HUD owns: the bar, plus the Build/Play
    /// tabs perched on top of it. Full-screen modal scrims stop here instead of at 0, otherwise they
    /// swallow clicks on the bar and the tabs — reserving only the bar left the tabs covered, since
    /// they stick up past it.</summary>
    public const float BottomHudReservedHeight = ModeTabsBottom + ModeTabHeight;

    private void Awake()
    {
        _uiDoc = GetComponent<UIDocument>();

        // The bottom HUD is a taskbar, so it has to outrank the windows it opens. At the scene's
        // sortingOrder 0 it sat under every panel (HiringBoard 95, EmployeeRoster 99, HUD 20,
        // ToolsWindow 50...), and UI Toolkit picks top-down by sortingOrder — so an open panel ate
        // the clicks on the play buttons. Worst offender is WorkQueuePanel's overlay, which is
        // full-screen and pickable, and blanked the bar entirely.
        //
        // 120 clears every panel while staying under the things that legitimately cover the bar:
        // RackSetupUI (150), ChevronTooltip (200), LaneSetupUI (250), Toast (999999). Set here
        // rather than in the scene to match how those four do it, and so it can't drift.
        if (_uiDoc != null) _uiDoc.sortingOrder = 120;

        Instance = this;
    }

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        _root = _uiDoc.rootVisualElement;

        if (_root == null)
        {
            Debug.LogError("[BuildMenuUI] Root VisualElement is null in OnEnable!");
            return;
        }

        // Root fills the entire screen — must be Ignore so PanelRaycaster doesn't
        // block game-world physics raycasts when the cursor is over empty space.
        _root.pickingMode = PickingMode.Ignore;

        if (buildMenuStyle != null)
            _root.styleSheets.Add(buildMenuStyle);

        CacheElements();
        BuildCategoryButtons();
        BuildUtilityButtons();
        BuildSubmenuContainer();

        // Automatically initialize your stationed item popup info card 
        VisualElement stationaryPopup = GetStationedPopup();

        WorldHoverPopupUI hoverSystem = FindAnyObjectByType<WorldHoverPopupUI>();
        if (hoverSystem != null && stationaryPopup != null)
        {
            hoverSystem.Init(stationaryPopup);
        }

        CloseSubmenu();
    }

    public void Initialize(MoneyService money)
    {
        moneyService = money;
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        if (_uiDoc != null) _root = _uiDoc.rootVisualElement;

        // Re-build if we were initialized after OnEnable or during Awake
        if (_root != null)
        {
            CacheElements();
            BuildCategoryButtons();
            BuildUtilityButtons();
        }
    }

    private void CacheElements()
    {
        _buildBar = _root.Q<VisualElement>("BottomBarBuildUI");
        _playBar = _root.Q<VisualElement>("BottomBarPlayUI");
        _categoryRow = _root.Q<VisualElement>("CategoryRow");
        _utilityRow = _root.Q<VisualElement>("UtilityRow");
        _playUtilityRow = _root.Q<VisualElement>("PlayUtilityRow");
        _submenuContainer = _root.Q<VisualElement>("SubmenuContainer");
        _modeTabs = _root.Q<VisualElement>("ModeTabs");
        _tabBuild = _root.Q<Button>("TabBuild");
        _tabPlay = _root.Q<Button>("TabPlay");

        // The bars are Ignore in UXML (legacy reason) — override to Position so
        // any click inside a bar area is caught and doesn't pass through to the
        // game world. The full-screen root stays Ignore so it doesn't block clicks
        // in open space above the bar.
        if (_buildBar != null)         _buildBar.pickingMode         = PickingMode.Position;
        if (_playBar != null)          _playBar.pickingMode          = PickingMode.Position;
        if (_submenuContainer != null) _submenuContainer.pickingMode = PickingMode.Position;

        if (!_barCallbacksRegistered)
        {
            RegisterBarPointerGuards(_buildBar);
            RegisterBarPointerGuards(_playBar);
            RegisterBarPointerGuards(_modeTabs);

            if (_tabBuild != null) _tabBuild.clicked += () => SetHudMode(HudMode.Build);
            if (_tabPlay != null) _tabPlay.clicked += () => SetHudMode(HudMode.Play);

            WirePlayBarButtons();

            _barCallbacksRegistered = true;
        }

        ApplyHudMode();
    }

    /// <summary>
    /// Points PlayBtn1..9 at UIKeyBindingManager.ToggleUI with their own number, so a click is the
    /// exact same call the number key makes — same exclusivity, same Shift Manager unsaved-changes
    /// prompt, same Tab-closes-everything registry. Nothing here knows which panel it opens, so
    /// reassigning a hotkey moves its button with it.
    /// </summary>
    /// <summary>How many numbered buttons the play bar has. Named rather than inline so adding a
    /// tenth means changing this and the UXML, not hunting a bare literal — the loop below warns per
    /// missing button, so a mismatch is loud rather than a silently dead button.</summary>
    private const int PlayBarButtonCount = 9;

    private void WirePlayBarButtons()
    {
        for (int i = 1; i <= PlayBarButtonCount; i++)
        {
            var button = _root.Q<Button>($"PlayBtn{i}");
            if (button == null)
            {
                Debug.LogWarning($"[BuildMenuUI] PlayBtn{i} not found in BottomBarPlayUI.");
                continue;
            }

            int key = i; // don't capture the loop variable
            button.clicked += () => UIKeyBindingManager.Instance.ToggleUI(key);
            _playBarButtons[key] = button;
        }
    }

    /// <summary>
    /// Mirrors the open panels onto the bar using the same .selected class the build categories use.
    /// Polled rather than evented because UIKeyBindingManager has no "panel changed" notification and
    /// panels can also be closed by Tab or Escape, which never route through these buttons.
    ///
    /// Asks each panel whether it's open rather than comparing keys against CurrentOpenKey: a panel
    /// registered floating is deliberately never the "current" key, so a single-key comparison showed
    /// it as unlit the whole time it was on screen. Every panel is exclusive today, so at most one
    /// button lights up, but the mask keeps that honest if a floating window comes back.
    /// </summary>
    private void SyncPlayBarSelection()
    {
        if (_playBarButtons.Count == 0) return;

        var keys = UIKeyBindingManager.Instance;

        // Bitmask purely as the change guard the single int used to be — this runs every frame, and
        // rewriting the class list on all eight buttons unconditionally is what it exists to avoid.
        int openMask = 0;
        if (keys != null)
        {
            foreach (var kvp in _playBarButtons)
                if (keys.IsPanelOpen(kvp.Key)) openMask |= 1 << kvp.Key;
        }

        if (openMask == _lastSyncedOpenMask) return;
        _lastSyncedOpenMask = openMask;

        foreach (var kvp in _playBarButtons)
            kvp.Value.EnableInClassList("selected", (openMask & (1 << kvp.Key)) != 0);
    }

    private void Update()
    {
        SyncPlayBarSelection();
    }

    /// <summary>
    /// Keeps the cursor-is-over-HUD flag and the submenu close timer honest for anything that sits in
    /// the bottom HUD. Placement reads IsPointerOverBuildMenu to decide whether a click belongs to the
    /// world, so every clickable strip down here has to report itself — the tabs included, otherwise
    /// switching modes also drops a building on the floor behind them.
    /// </summary>
    private void RegisterBarPointerGuards(VisualElement el)
    {
        if (el == null) return;

        el.RegisterCallback<PointerEnterEvent>(_ =>
        {
            IsPointerOverBuildMenu = true;
            _submenuClosePending = false;
            _submenuCloseTask?.Pause();
        });

        el.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            IsPointerOverBuildMenu = false;
            StartDelayedSubmenuClose();
        });

        // Prevent wheel events from zooming the camera when over the UI
        el.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
    }

    /// <summary>Switches which bottom bar is up. No-ops if already in that mode.</summary>
    public void SetHudMode(HudMode mode)
    {
        if (_mode == mode) return;
        _mode = mode;

        // A submenu left open over the play bar would be a build affordance in a mode that has no
        // build tools, and its item clicks still fire.
        if (_submenuContainer != null && _categoryRow != null) CloseSubmenu();

        // Switching modes is a context switch, so nothing from the old one should survive it —
        // otherwise you land in Build with the Work Queue still up. Same call Tab makes, which also
        // means it inherits Tab's behaviour of closing the Shift Manager without its unsaved-changes
        // prompt. Clears the play buttons' highlight for free: CloseAll resets CurrentOpenKey, and
        // SyncPlayBarSelection picks that up on the next frame.
        UIKeyBindingManager.Instance?.CloseAll();

        ApplyHudMode();
        OnHudModeChanged?.Invoke(_mode);
    }

    /// <summary>
    /// Fired after the visible bar changes, so widgets docked INTO a bar can move to the one that's
    /// now on screen.
    ///
    /// A VisualElement has exactly one parent, so anything parented to the build bar simply vanishes
    /// when the play bar is shown — which is what happened to the dev HUD (FPS + graphics preset).
    /// An event rather than BuildMenuUI knowing about the dev HUD directly: the bar shouldn't have to
    /// enumerate its tenants, and anything else docked later gets the same treatment for free.
    /// </summary>
    public static event System.Action<VisualElement> OnActiveBarChanged;

    private void ApplyHudMode()
    {
        bool build = _mode == HudMode.Build;

        SetHidden(_buildBar, !build);
        SetHidden(_playBar, build);

        SetTabActive(_tabBuild, build);
        SetTabActive(_tabPlay, !build);

        OnActiveBarChanged?.Invoke(ActiveBar);
    }

    // EnableInClassList rather than Add/RemoveFromClassList: the classes are also set in the UXML, and
    // CacheElements runs twice, so the add/remove pair can leave a duplicate entry behind.
    private static void SetHidden(VisualElement el, bool hidden)
    {
        el?.EnableInClassList("buildmenu-bar-hidden", hidden);
    }

    private static void SetTabActive(Button tab, bool active)
    {
        tab?.EnableInClassList("buildmenu-mode-tab-active", active);
    }

    private void BuildCategoryButtons()
    {
        if (_categoryRow == null)
        {
            Debug.LogError("[BuildMenuUI] CategoryRow is null!");
            return;
        }

        //Debug.Log($"[BuildMenuUI] Building {categories.Count} category buttons into {_categoryRow.name}. Attached to panel: {_categoryRow.panel != null}");
        _categoryRow.Clear();
        foreach (var cat in categories)
        {
            if (cat == null) continue;

            if (categoryButtonUxml == null)
            {
                Debug.LogError("[BuildMenuUI] categoryButtonUxml is NULL!");
                continue;
            }

            var ve = categoryButtonUxml.Instantiate();
            var button = ve.Q<Button>("CategoryButton");
            var icon = ve.Q<VisualElement>("Icon");
            var label = ve.Q<Label>("Label");

            if (button == null)
            {
                Debug.LogError("[BuildMenuUI] CategoryButton not found in template!");
                continue;
            }

            label.text = cat.displayName;
            if (cat.icon != null) icon.style.backgroundImage = new StyleBackground(cat.icon);

            var capturedCat = cat;
            button.clicked += () => OnCategoryClicked(capturedCat);
            _categoryRow.Add(ve);
        }
    }

    /// <summary>
    /// Fills BOTH bars' utility rows from the same config list.
    ///
    /// The play bar's row existed in the UXML from the start and was never populated, so switching to
    /// Play lost Delete/Move/Undo/Redo/Lower/Raise — none of which are build-only actions. Undo in
    /// particular is the one you want most when you've just done something in the wrong mode.
    ///
    /// Built twice rather than reparented on mode switch: a VisualElement has exactly one parent, so
    /// a shared row would have to be moved every time the mode flips, and any handler or hover state
    /// mid-flight would go with it. Two independent sets, one config.
    /// </summary>
    private void BuildUtilityButtons()
    {
        if (_utilityRow == null)
        {
            Debug.LogError("[BuildMenuUI] UtilityRow is null!");
            return;
        }

        PopulateUtilityRow(_utilityRow);

        // Absent only if the UXML changed; warn rather than fail, since the build bar still works.
        if (_playUtilityRow != null) PopulateUtilityRow(_playUtilityRow);
        else Debug.LogWarning("[BuildMenuUI] PlayUtilityRow not found — the play bar will have no " +
                              "utility buttons.");
    }

    private void PopulateUtilityRow(VisualElement row)
    {
        row.Clear();

        foreach (var util in utilityButtons)
        {
            if (util == null) continue;

            var ve = utilityButtonUxml.Instantiate();
            var button = ve.Q<Button>("UtilityButton");
            var icon = ve.Q<VisualElement>("Icon");
            var label = ve.Q<Label>("Label");

            if (button == null)
            {
                Debug.LogError("[BuildMenuUI] UtilityButton not found in template!");
                continue;
            }

            // Set the displays and text labels
            if (util.id.ToLower() == "lower") label.text = "LOWER";
            else if (util.id.ToLower() == "raise") label.text = "RAISE";
            else if (util.id.ToLower() == "save") label.text = "Save";
            else if (util.id.ToLower() == "load") label.text = "Load";
            else label.text = util.id.ToUpper();

            if (util.icon != null) icon.style.backgroundImage = new StyleBackground(util.icon);

            // Dynamically assign names and classes so your USS styles still work!
            if (util.id.ToLower() == "lower")
            {
                button.name = "BtnLowerWall";
                icon.AddToClassList("wall-lower-icon");
                button.clicked += () =>
                {
                    if (WallVisibilityManager.Instance != null)
                    {
                        WallVisibilityManager.Instance.StepDown();
                    }
                };
            }
            else if (util.id.ToLower() == "raise")
            {
                button.name = "BtnRaiseWall";
                icon.AddToClassList("wall-raise-icon");
                button.clicked += () =>
                {
                    if (WallVisibilityManager.Instance != null)
                    {
                        WallVisibilityManager.Instance.StepUp();
                    }
                };
            }
            else
            {
                // General click mapping logic for standard buttons (Delete, Undo, etc.)
                string btnId = util.id;
                button.clicked += () => HandleGenericUtilityClick(btnId);
            }
            row.Add(ve);
        }
    }

    private void HandleGenericUtilityClick(string id)
    {
        CloseSubmenu();
        switch (id.ToLower())
        {
            case "delete": OnDeleteClicked?.Invoke(); break;
            case "move": OnMoveClicked?.Invoke(); break;
            case "undo": OnUndoClicked?.Invoke(); break;
            case "redo": OnRedoClicked?.Invoke(); break;
            case "save": saveLoadWindowController.Open(SaveLoadMode.Save); break;
            case "load": saveLoadWindowController.Open(SaveLoadMode.Load); break;
        }
    }

    private void BuildSubmenuContainer()
    {
        _submenuScroll = _submenuContainer.Q<ScrollView>("SubmenuScroll");
        var targetRoot = _submenuContainer.Q<VisualElement>("SubmenuRoot") ?? _submenuContainer;

        targetRoot.RegisterCallback<PointerEnterEvent>(_ =>
        {
            IsPointerOverBuildMenu = true;
            _submenuClosePending = false;
            _submenuCloseTask?.Pause();
        });

        targetRoot.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            IsPointerOverBuildMenu = false;
            StartDelayedSubmenuClose();
        });

        // Prevent wheel events from zooming the camera when over the submenu
        targetRoot.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());

        if (_submenuScroll != null)
        {
            _submenuScroll.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (!_submenuOpen) return;
                if (evt.newRect.height <= 20f) return;
                PositionSubmenuNow();
            });
        }
    }

    private void StartDelayedSubmenuClose()
    {
        if (_submenuClosePending) return;
        _submenuClosePending = true;
        float timer = 0f;

        _submenuCloseTask = _submenuContainer.schedule.Execute(() =>
        {
            if (IsPointerOverBuildMenu)
            {
                _submenuClosePending = false;
                _submenuCloseTask.Pause();
                return;
            }
            timer += 0.016f;
            if (timer >= _submenuCloseDelay)
            {
                if (_submenuClosePending) CloseSubmenu();
                _submenuClosePending = false;
                _submenuCloseTask.Pause();
            }
        }).Every(16);
    }

    private void OnCategoryClicked(CategoryConfig cat)
    {
        if (_activeCategory == cat && _submenuOpen)
        {
            CloseSubmenu();
            foreach (var child in _categoryRow.Children())
                child.Q<Button>("CategoryButton")?.RemoveFromClassList("selected");
            return;
        }

        _activeCategory = cat;
        _lastClickedCategoryButton = null;

        foreach (var child in _categoryRow.Children())
        {
            var btn = child.Q<Button>("CategoryButton");
            var label = child.Q<Label>("Label");

            if (btn != null) btn.RemoveFromClassList("selected");
            if (btn != null && label != null && label.text == cat.displayName)
            {
                btn.AddToClassList("selected");
                _lastClickedCategoryButton = btn;
            }
        }
        OpenSubmenu(cat);
    }

    private void OpenSubmenu(CategoryConfig cat)
    {
        if (_submenuScroll == null) return;
        _submenuScroll.Clear();

        if (cat.items != null)
        {
            foreach (var item in cat.items)
            {
                if (item == null) continue;
                var ve = itemButtonUxml.Instantiate();
                var button = ve.Q<Button>("ItemButton");
                var icon = ve.Q<VisualElement>("Icon");
                var nameLabel = ve.Q<Label>("ItemName");
                var costLabel = ve.Q<Label>("ItemCost");

                if (item.icon != null) icon.style.backgroundImage = new StyleBackground(item.icon);
                nameLabel.text = item.objName;
                costLabel.text = $"${item.cost}";

                var capturedItem = item;
                button.clicked += () =>
                {
                    CloseSubmenu();
                    OnBuildItemClicked?.Invoke(capturedItem);
                };
                _submenuScroll.Add(ve);
            }
        }

        _submenuContainer.RemoveFromClassList("buildmenu-submenu-closed");
        _submenuContainer.AddToClassList("buildmenu-submenu-open");
        _submenuOpen = true;

        PositionSubmenuAfterLayout();
    }

    private void CloseSubmenu()
    {
        _submenuContainer.RemoveFromClassList("buildmenu-submenu-open");
        _submenuContainer.AddToClassList("buildmenu-submenu-closed");
        _submenuOpen = false;

        foreach (var child in _categoryRow.Children())
            child.Q<Button>("CategoryButton")?.RemoveFromClassList("selected");
    }

    private void PositionSubmenuAfterLayout()
    {
        _submenuContainer.schedule.Execute(() =>
        {
            PositionSubmenuNow();
        }).ExecuteLater(10);
    }

    private void PositionSubmenuNow()
    {
        if (_lastClickedCategoryButton == null) return;
        if (_buildBar == null || _root == null || _submenuContainer == null) return;

        Vector2 rootPos = _root.worldBound.position;
        Vector2 buttonPos = _lastClickedCategoryButton.worldBound.position;

        float localX = buttonPos.x - rootPos.x;
        _submenuContainer.style.left = localX;
        _submenuContainer.style.bottom = 128f;
        _submenuContainer.style.top = StyleKeyword.Auto;
    }

    public VisualElement GetStationedPopup()
    {
        if (_root == null) _root = _uiDoc.rootVisualElement;
        if (_buildBar == null) _buildBar = _root.Q<VisualElement>("BottomBarBuildUI");

        VisualElement foundPopup = _root.Q<VisualElement>("WorldHoverPopup");
        if (foundPopup != null) return foundPopup;

        // Build the floating popup — lives in the full-screen root so it doesn't
        // take space in the BottomBar's flex layout.
        var fallbackPopup = new VisualElement { name = "WorldHoverPopup" };
        fallbackPopup.AddToClassList("world-hover-popup");
        fallbackPopup.pickingMode = PickingMode.Ignore;
        // 30% bigger overall
        fallbackPopup.style.paddingLeft = 20;
        fallbackPopup.style.paddingRight = 20;
        fallbackPopup.style.paddingTop = 16;
        fallbackPopup.style.paddingBottom = 16;

        // Main content container (flex row: text on left, icon on right)
        var contentContainer = new VisualElement();
        contentContainer.style.flexDirection = FlexDirection.Row;
        contentContainer.style.justifyContent = Justify.SpaceBetween;
        contentContainer.style.alignItems = Align.FlexStart;
        contentContainer.style.marginBottom = 8;

        // Left side: text content
        var textContainer = new VisualElement();
        textContainer.style.flexDirection = FlexDirection.Column;
        textContainer.style.flexGrow = 1;

        var titleLabel = new Label { name = "HoverTitle", text = "" };
        titleLabel.AddToClassList("world-hover-title");
        titleLabel.style.fontSize = 18; // 30% bigger
        titleLabel.pickingMode = PickingMode.Ignore;

        var costLabel = new Label { name = "HoverCost", text = "" };
        costLabel.AddToClassList("world-hover-cost");
        costLabel.style.fontSize = 14;
        costLabel.pickingMode = PickingMode.Ignore;

        var hourlyLabel = new Label { name = "HoverHourlyCost", text = "" };
        hourlyLabel.AddToClassList("world-hover-hourlyCost");
        hourlyLabel.style.fontSize = 14;
        hourlyLabel.pickingMode = PickingMode.Ignore;

        textContainer.Add(titleLabel);
        textContainer.Add(costLabel);
        textContainer.Add(hourlyLabel);

        // Right side: icon (postage stamp style, upper-right corner)
        var iconImage = new Image { name = "HoverIcon" };
        iconImage.style.width = 80;
        iconImage.style.height = 80;
        iconImage.style.marginLeft = 12;
        iconImage.style.borderTopWidth = 2;
        iconImage.style.borderRightWidth = 2;
        iconImage.style.borderBottomWidth = 2;
        iconImage.style.borderLeftWidth = 2;
        iconImage.style.borderTopColor = new Color(0.5f, 0.7f, 0.9f, 1f);
        iconImage.style.borderRightColor = new Color(0.5f, 0.7f, 0.9f, 1f);
        iconImage.style.borderBottomColor = new Color(0.5f, 0.7f, 0.9f, 1f);
        iconImage.style.borderLeftColor = new Color(0.5f, 0.7f, 0.9f, 1f);
        iconImage.style.borderTopLeftRadius = new Length(6, LengthUnit.Pixel);
        iconImage.style.borderTopRightRadius = new Length(6, LengthUnit.Pixel);
        iconImage.style.borderBottomLeftRadius = new Length(6, LengthUnit.Pixel);
        iconImage.style.borderBottomRightRadius = new Length(6, LengthUnit.Pixel);
        iconImage.style.backgroundColor = new Color(0.1f, 0.15f, 0.2f, 0.8f);
        iconImage.scaleMode = ScaleMode.ScaleToFit;
        iconImage.pickingMode = PickingMode.Ignore;
        iconImage.style.display = DisplayStyle.None;
        iconImage.style.flexShrink = 0;

        contentContainer.Add(textContainer);
        contentContainer.Add(iconImage);

        fallbackPopup.Add(contentContainer);

        // Pallet info panel (hidden by default, shown for pallets)
        var palletInfoPanel = new VisualElement { name = "PalletInfoPanel" };
        palletInfoPanel.style.flexDirection = FlexDirection.Column;
        palletInfoPanel.style.display = DisplayStyle.None;
        palletInfoPanel.pickingMode = PickingMode.Ignore;

        fallbackPopup.Add(palletInfoPanel);

        // Add to the root (full-screen overlay) so it floats freely over everything
        _root.Add(fallbackPopup);

        return fallbackPopup;
    }

    }
