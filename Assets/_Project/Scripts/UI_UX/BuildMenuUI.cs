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

    // Mode tab hover copy — pulled out as named constants (rather than left in-place on the
    // RegisterCallback calls) so the tooltip text and the button's own label can be found together.
    private const string BuildTabTooltipText = "Everything you need to build your Empire";
    private const string OrdersTabTooltipText = "Manage all orders, Inbound and Outbound";

    // ORDERS (Play mode) is the default view on startup — players land in "run the warehouse" mode,
    // with BUILD a deliberate switch away from it.
    private HudMode _mode = HudMode.Play;

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

    /// <summary>Force-reconciles <see cref="IsPointerOverBuildMenu"/> against the pointer's ACTUAL
    /// current position, rather than trusting the enter/leave events <see cref="RegisterBarPointerGuards"/>
    /// relies on. Those events stop firing on the bar for the duration of a pointer CAPTURE held by some
    /// other element — and a docked card being dragged out (DevHudWindow/SystemsLogWindow's drag-to-
    /// undock) captures the pointer on itself for exactly that reason, right while the cursor sits over
    /// the bar. The bar never gets a chance to fire its own PointerLeaveEvent as the drag carries the
    /// cursor away, so the flag is left stuck true — camera orbit/pan/zoom then reads "over the build
    /// menu" forever, no matter where the cursor actually is. Callers that reparent something out from
    /// under an active capture should call this right after, using the current real pointer position.</summary>
    public void SyncPointerOverBuildMenu(Vector2 screenPos)
    {
        var bar = ActiveBar;
        var panel = bar?.panel;
        if (bar == null || panel == null) { IsPointerOverBuildMenu = false; return; }
        var panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);
        IsPointerOverBuildMenu = bar.worldBound.Contains(panelPos);
    }

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

            // UI Toolkit's built-in `tooltip` property only renders inside the Editor's own UI — a
            // runtime UIDocument HUD like this one never shows it — so the mode tabs hook into the
            // same cursor-following tooltip the rack chevrons use instead of a second implementation.
            AttachModeTabTooltip(_tabBuild, BuildTabTooltipText);
            AttachModeTabTooltip(_tabPlay, OrdersTabTooltipText);

            WirePlayBarButtons();

            _barCallbacksRegistered = true;
        }

        ApplyHudMode();
    }

    /// <summary>
    /// Points PlayBtn0..9 at UIKeyBindingManager.ToggleUI with their own number, so a click is the
    /// exact same call the number key makes — same exclusivity, same Shift Manager unsaved-changes
    /// prompt, same Tab-closes-everything registry. Nothing here knows which panel it opens, so
    /// reassigning a hotkey moves its button with it.
    /// </summary>
    /// <summary>How many numbered buttons the play bar has. Named rather than inline so adding a
    /// tenth means changing this and the UXML, not hunting a bare literal — the loop below warns per
    /// missing button, so a mismatch is loud rather than a silently dead button.</summary>
    private const int PlayBarButtonCount = 10;

    private void WirePlayBarButtons()
    {
        for (int i = 0; i < PlayBarButtonCount; i++)
        {
            var button = _root.Q<Button>($"PlayBtn{i}");
            if (button == null)
            {
                Debug.LogWarning($"[BuildMenuUI] PlayBtn{i} not found in BottomBarPlayUI.");
                continue;
            }

            int key = i; // don't capture the loop variable
            // Blur immediately after handling the click: a runtime Button keeps keyboard/gamepad
            // focus after being clicked and never releases it on its own, and Unity's default runtime
            // theme paints the focused element with its own solid inline background/border color that
            // sits ABOVE the USS cascade (inline styles beat every stylesheet rule, !important
            // included) — that's the vivid blue fill that stuck around until the panel was closed.
            // We already track "open" via the .selected class, so this control has no use for
            // lingering focus once the click is done.
            button.clicked += () => { UIKeyBindingManager.Instance.ToggleUI(key); button.Blur(); };
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

    /// <summary>
    /// Shows <paramref name="text"/> in the shared ChevronTooltipUI singleton while the cursor is over
    /// <paramref name="tab"/>, and hides it on leave. Reuses that cursor-following tooltip rather than
    /// building a second one, since UI Toolkit's built-in `tooltip` property never renders on a
    /// runtime UIDocument HUD.
    /// </summary>
    private static void AttachModeTabTooltip(Button tab, string text)
    {
        if (tab == null) return;

        tab.RegisterCallback<PointerEnterEvent>(_ => ChevronTooltipUI.Ensure().Show(GetMouseScreenPosition(), text));
        tab.RegisterCallback<PointerLeaveEvent>(_ => ChevronTooltipUI.Ensure().Hide());
    }

    /// <summary>Mirrors ChevronTooltipUI's own cursor read so the tooltip's first frame lands under
    /// the pointer immediately instead of snapping there on the next Update.</summary>
    private static Vector2 GetMouseScreenPosition()
    {
        return UnityEngine.InputSystem.Mouse.current != null
            ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;
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
            // "Loss Prevention" is the one category name too wide for the 90px card at nowrap —
            // let it wrap to two lines instead of overrunning its neighbour.
            if (cat.displayName == "Loss Prevention") label.AddToClassList("buildmenu-category-label-wrap");
            if (cat.icon != null) icon.style.backgroundImage = new StyleBackground(cat.icon);

            var capturedCat = cat;
            // See the matching comment in WirePlayBarButtons — a runtime Button keeps focus after a
            // click and Unity's default theme paints the focused element with an inline color that
            // beats the entire USS cascade, which is what made a selected category card look like a
            // solid blue box instead of the intended dark card style.
            button.clicked += () => { OnCategoryClicked(capturedCat); button.Blur(); };
            _categoryRow.Add(ve);
        }
    }

    /// <summary>
    /// Fills the BUILD bar's utility row from the config list.
    ///
    /// The play bar had its own duplicate copy of this row for a while (Delete/Move/Undo/Redo/Lower/
    /// Raise aren't build-only actions, and Undo especially is handy right after a mistake) — but Tad
    /// asked for it back OUT of Outbound/Play: these are placement-editing actions, they don't apply
    /// once you're managing orders, and hiding them frees up real space on that bar. Reversed here by
    /// simply not populating the play row and hiding it outright — Ctrl+Z/Ctrl+Y still work everywhere
    /// via PlacementStateMachine's global shortcut handling, so Undo/Redo aren't actually lost, just
    /// not a button on this bar anymore.
    /// </summary>
    private void BuildUtilityButtons()
    {
        if (_utilityRow == null)
        {
            Debug.LogError("[BuildMenuUI] UtilityRow is null!");
            return;
        }

        PopulateUtilityRow(_utilityRow);

        // Removed from the hierarchy outright, not just display:none — the Dev HUD's drag-to-dock
        // ghost preview (DevHudWindow.ComputeInsertIndex) walks this same bar's direct children and
        // compares each one's worldBound.center.x; a display:none sibling still enumerates as a child
        // but reports degenerate (0,0) bounds, which would silently skew that index math by one slot.
        // Fully removing it avoids that class of bug rather than relying on every future bar-child
        // consumer to remember to skip hidden ones.
        if (_playUtilityRow != null) _playUtilityRow.RemoveFromHierarchy();
        else Debug.LogWarning("[BuildMenuUI] PlayUtilityRow not found.");
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
