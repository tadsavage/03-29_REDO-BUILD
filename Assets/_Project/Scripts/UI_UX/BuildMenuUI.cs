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
    private VisualElement _bottomBar;
    private VisualElement _categoryRow;
    private VisualElement _utilityRow;
    private VisualElement _submenuContainer;
    private ScrollView _submenuScroll;

    // State
    private CategoryConfig _activeCategory;
    private bool _submenuOpen;

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

    /// <summary>The bar itself. Adding here docks a widget INTO the bar's row layout — between the
    /// category buttons and the utility buttons, since the bar is space-between — rather than leaving
    /// it floating over the HUD.</summary>
    public VisualElement BottomBar => _bottomBar;

    /// <summary>Height of the bottom bar in the USS (.buildmenu-bottom-bar) plus its 2px top border.
    /// Anything anchored just above the bar measures from here.</summary>
    public const float BottomBarHeight = 122f;

    private VisualElement _keybindLegend;

    private void Awake()
    {
        _uiDoc = GetComponent<UIDocument>();
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
        _bottomBar = _root.Q<VisualElement>("BottomBar");
        _categoryRow = _root.Q<VisualElement>("CategoryRow");
        _utilityRow = _root.Q<VisualElement>("UtilityRow");
        _submenuContainer = _root.Q<VisualElement>("SubmenuContainer");

        // BottomBar is Ignore in UXML (legacy reason) — override to Position so
        // any click inside the bar area is caught and doesn't pass through to the
        // game world. The full-screen root stays Ignore so it doesn't block clicks
        // in open space above the bar.
        if (_bottomBar != null)        _bottomBar.pickingMode        = PickingMode.Position;
        if (_submenuContainer != null) _submenuContainer.pickingMode = PickingMode.Position;

        // Track mouse over the bottom action bar
        _bottomBar.RegisterCallback<PointerEnterEvent>(_ =>
        {
            IsPointerOverBuildMenu = true;
            _submenuClosePending = false;
            _submenuCloseTask?.Pause();
        });

        _bottomBar.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            IsPointerOverBuildMenu = false;
            StartDelayedSubmenuClose();
        });

        // Prevent wheel events from zooming the camera when over the UI
        _bottomBar.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());

        BuildKeybindLegend();
    }

    /// <summary>
    /// The number-key cheat sheet, as a strip sitting directly above the build bar.
    ///
    /// Purely a HUD readout: the strip and every child are PickingMode.Ignore, so it can never eat a
    /// click meant for the world or the bar beneath it. Built here rather than in its own document
    /// because it belongs to the bottom HUD and should move and layer with it.
    ///
    /// The labels are the panels' human names, not the class names — "Dev Console", not
    /// "ToolsWindowController". Keys must stay in step with UIKeyBindingManager's registry.
    /// </summary>
    private void BuildKeybindLegend()
    {
        if (_root == null) return;
        if (_keybindLegend != null) { _keybindLegend.RemoveFromHierarchy(); _keybindLegend = null; }

        var strip = new VisualElement { name = "KeybindLegend" };
        strip.pickingMode = PickingMode.Ignore;
        strip.style.position = Position.Absolute;
        strip.style.bottom = BottomBarHeight;
        strip.style.left = 0;
        strip.style.right = 0;
        strip.style.height = 30;
        strip.style.flexDirection = FlexDirection.Row;
        strip.style.alignItems = Align.Center;
        strip.style.justifyContent = Justify.Center;
        // House orange (#B5743A), the same family as the action buttons, dropped to a HUD-weight alpha.
        strip.style.backgroundColor = new StyleColor(new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 0.15f));
        strip.style.borderTopWidth = strip.style.borderBottomWidth = 2;
        strip.style.borderTopColor = strip.style.borderBottomColor =
            new StyleColor(new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f));

        (int key, string label)[] binds =
        {
            (1, "Dev Console"), (2, "Hiring Board"), (3, "Employee Roster"), (4, "Employee List"),
            (5, "Shift Manager"), (6, "Contracts"), (7, "Work Queue"), (8, "New Item")
        };

        foreach (var (key, label) in binds)
        {
            var entry = new VisualElement();
            entry.pickingMode = PickingMode.Ignore;
            entry.style.flexDirection = FlexDirection.Row;
            entry.style.alignItems = Align.Center;
            entry.style.marginLeft = 10;
            entry.style.marginRight = 10;

            var num = new Label(key.ToString());
            num.pickingMode = PickingMode.Ignore;
            ApplyLegendFont(num, bold: true, size: 15);
            num.style.color = new StyleColor(new Color(0xFD / 255f, 0xE8 / 255f, 0xCC / 255f, 1f));
            num.style.marginRight = 5;

            var text = new Label(label);
            text.pickingMode = PickingMode.Ignore;
            ApplyLegendFont(text, bold: false, size: 14);
            text.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.92f));

            entry.Add(num);
            entry.Add(text);
            strip.Add(entry);
        }

        _root.Add(strip);
        _keybindLegend = strip;
    }

    private static Font _legendFont;

    private static void ApplyLegendFont(VisualElement el, bool bold, int size)
    {
        if (_legendFont == null)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
            if (guids.Length > 0)
                _legendFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            _legendFont = Resources.Load<Font>("LilitaOne-Regular");
#endif
        }
        if (_legendFont != null)
            el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_legendFont));
        if (bold) el.style.unityFontStyleAndWeight = FontStyle.Bold;
        el.style.fontSize = size;
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

    private void BuildUtilityButtons()
    {
        if (_utilityRow == null)
        {
            Debug.LogError("[BuildMenuUI] UtilityRow is null!");
            return;
        }

        //Debug.Log($"[BuildMenuUI] Building {utilityButtons.Count} utility buttons.");
        _utilityRow.Clear();

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
            _utilityRow.Add(ve);
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
        if (_bottomBar == null || _root == null || _submenuContainer == null) return;

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
        if (_bottomBar == null) _bottomBar = _root.Q<VisualElement>("BottomBar");

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
