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
    [SerializeField] private VisualTreeAsset savePopupUxml;

    [Header("Styles")]
    [SerializeField] private StyleSheet buildMenuStyle;

    [Header("Save / Load")]
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] public ObjDataRegistry registry;
    private MoneyService moneyService;

    public GameContext Context { get; private set; }

    // Save popup UI
    private VisualElement _savePopup;
    private TextField _saveNameField;
    private Button _confirmSaveButton;
    private Button _cancelSaveButton;

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

    private void Awake()
    {
        _uiDoc = GetComponent<UIDocument>();
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

        if (buildMenuStyle != null)
            _root.styleSheets.Add(buildMenuStyle);

        CacheElements();
        BuildCategoryButtons();
        BuildUtilityButtons();
        BuildSubmenuContainer();
        BuildSavePopup();

        // Automatically initialize your stationed item popup info card 
        VisualElement stationaryPopup = GetStationedPopup();

        WorldHoverPopupUI hoverSystem = FindFirstObjectByType<WorldHoverPopupUI>();
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

    private void BuildSavePopup()
    {
        if (_root.Q<VisualElement>("SavePopupContainer") != null) return;

        var popup = savePopupUxml.Instantiate();
        popup.name = "SavePopupContainer";
        popup.pickingMode = PickingMode.Ignore;
        _root.Add(popup);

        _savePopup = popup.Q<VisualElement>("SavePopup");
        _saveNameField = popup.Q<TextField>("SaveNameField");
        _confirmSaveButton = popup.Q<Button>("ConfirmSaveButton");
        _cancelSaveButton = popup.Q<Button>("CancelSaveButton");

        if (_confirmSaveButton != null) _confirmSaveButton.clicked += ConfirmSave;
        if (_cancelSaveButton != null) _cancelSaveButton.clicked += HideSavePopup;
        HideSavePopup();
    }

    private void ShowSavePopup()
    {
        _savePopup.RemoveFromClassList("hidden");
        _saveNameField.value = "";
    }

    private void HideSavePopup()
    {
        _savePopup.AddToClassList("hidden");
    }

    private void ConfirmSave()
    {
        if (string.IsNullOrWhiteSpace(_saveNameField.value))
        {
            Debug.LogWarning("⚠ Save name is empty.");
            return;
        }
        placementSystem.SaveGame("autosave");
        HideSavePopup();
    }

    private void CacheElements()
    {
        _bottomBar = _root.Q<VisualElement>("BottomBar");
        _categoryRow = _root.Q<VisualElement>("CategoryRow");
        _utilityRow = _root.Q<VisualElement>("UtilityRow");
        _submenuContainer = _root.Q<VisualElement>("SubmenuContainer");

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
                        WallVisibilityManager.Instance.SetVisibilityMode(WallVisibilityManager.WallVisibilityMode.Cut);
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
                        WallVisibilityManager.Instance.SetVisibilityMode(WallVisibilityManager.WallVisibilityMode.Full);
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
                button.clicked += () => OnBuildItemClicked?.Invoke(capturedItem);
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

        var fallbackPopup = new VisualElement { name = "WorldHoverPopup" };
        fallbackPopup.AddToClassList("buildmenu-stationed-popup");
        fallbackPopup.pickingMode = PickingMode.Ignore;

        var titleLabel = new Label { name = "HoverTitle", text = "No Data - Item" };
        titleLabel.AddToClassList("world-hover-title");
        titleLabel.pickingMode = PickingMode.Ignore;

        var metricsBox = new VisualElement { name = "HoverMetricsContainer" };
        metricsBox.AddToClassList("world-hover-metrics-box");
        metricsBox.pickingMode = PickingMode.Ignore;

        var costLabel = new Label { name = "HoverCost", text = "Cost: --" };
        costLabel.AddToClassList("world-hover-cost");
        costLabel.pickingMode = PickingMode.Ignore;

        var hourlyLabel = new Label { name = "HoverHourlyCost", text = "Hourly: --" };
        hourlyLabel.AddToClassList("world-hover-hourlyCost");
        hourlyLabel.pickingMode = PickingMode.Ignore;

        metricsBox.Add(costLabel);
        metricsBox.Add(hourlyLabel);
        fallbackPopup.Add(titleLabel);
        fallbackPopup.Add(metricsBox);

        if (_bottomBar != null && _utilityRow != null)
        {
            int utilityIndex = _bottomBar.IndexOf(_utilityRow);
            _bottomBar.Insert(utilityIndex, fallbackPopup);
        }
        else if (_bottomBar != null)
        {
            _bottomBar.Add(fallbackPopup);
        }
        else
        {
            _root.Add(fallbackPopup);
        }

        return fallbackPopup;
    }
    }
