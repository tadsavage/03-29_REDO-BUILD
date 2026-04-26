using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildMenuUI : MonoBehaviour
{
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
    [SerializeField] private ObjDataRegistry registry;
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

    // Utility Button setup ***********************************
    [Serializable]
    public class UtilityButtonConfig
    {
        public string id;
        public Texture2D icon;
    }
    [SerializeField] private List<UtilityButtonConfig> utilityButtons = new();
    // ********************************************************

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

    public static bool IsPointerOverBuildMenu;

    private void Awake()
    {
        _uiDoc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {

        if (_uiDoc == null)
            _uiDoc = GetComponent<UIDocument>();

        _root = _uiDoc.rootVisualElement;

        if (buildMenuStyle != null)
            _root.styleSheets.Add(buildMenuStyle);

        CacheElements();
        BuildCategoryButtons();
        BuildUtilityButtons();
        BuildSubmenuContainer();
        BuildSavePopup();
        CloseSubmenu();
    }
    public void Initialize(MoneyService money)
    {
        moneyService = money;
    }
    // ---------------------------------------------------------
    // SAVE POPUP
    // ---------------------------------------------------------
    private void BuildSavePopup()
    {
        var popup = savePopupUxml.Instantiate();
        _root.Add(popup);

        // Query inside the template content container
        _savePopup = popup.contentContainer.Q<VisualElement>("SavePopup");
        _saveNameField = popup.contentContainer.Q<TextField>("SaveNameField");
        _confirmSaveButton = popup.contentContainer.Q<Button>("ConfirmSaveButton");
        _cancelSaveButton = popup.contentContainer.Q<Button>("CancelSaveButton");

        _confirmSaveButton.clicked += ConfirmSave;
        _cancelSaveButton.clicked += HideSavePopup;

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

        string saveName = _saveNameField.value;

        // ⭐ Call the REAL save system
        placementSystem.SaveGame("autosave");

        HideSavePopup();
    }

    // ---------------------------------------------------------
    // CACHE ROOT ELEMENTS
    // ---------------------------------------------------------
    private void CacheElements()
    {
        _bottomBar = _root.Q<VisualElement>("BottomBar");
        _categoryRow = _root.Q<VisualElement>("CategoryRow");
        _utilityRow = _root.Q<VisualElement>("UtilityRow");
        _submenuContainer = _root.Q<VisualElement>("SubmenuContainer");
    }

    // ---------------------------------------------------------
    // CATEGORY BUTTONS
    // ---------------------------------------------------------
    private void BuildCategoryButtons()
    {
        _categoryRow.Clear();

        foreach (var cat in categories)
        {
            if (cat == null)
                continue;

            var ve = categoryButtonUxml.Instantiate();
            var button = ve.Q<Button>("CategoryButton");
            var icon = ve.Q<VisualElement>("Icon");
            var label = ve.Q<Label>("Label");

            label.text = cat.displayName;

            if (cat.icon != null)
                icon.style.backgroundImage = new StyleBackground(cat.icon);

            var capturedCat = cat;
            button.clicked += () => OnCategoryClicked(capturedCat);

            _categoryRow.Add(ve);
        }
    }

    // ---------------------------------------------------------
    // UTILITY BUTTONS
    // ---------------------------------------------------------
    private void BuildUtilityButtons()
    {
        _utilityRow.Clear();

        foreach (var util in utilityButtons)
        {
            var ve = utilityButtonUxml.Instantiate();

            var button = ve.Q<Button>("UtilityButton");
            var icon = ve.Q<VisualElement>("Icon");
            var label = ve.Q<Label>("Label");

            label.text = util.id;

            if (util.icon != null)
                icon.style.backgroundImage = new StyleBackground(util.icon);

            switch (util.id)
            {
                case "DELETE": button.clicked += () => OnDeleteClicked?.Invoke(); break;
                case "MOVE": button.clicked += () => OnMoveClicked?.Invoke(); break;
                case "UNDO": button.clicked += () => OnUndoClicked?.Invoke(); break;
                case "REDO": button.clicked += () => OnRedoClicked?.Invoke(); break;
                case "CANCEL": button.clicked += () => OnCancelClicked?.Invoke(); break;
                case "ROTATE": button.clicked += () => OnRotateClicked?.Invoke(); break;
                case "SAVE": button.clicked += ShowSavePopup; break;
                case "LOAD": button.clicked += LoadGame; break;
            }

            _utilityRow.Add(ve);
        }
    }

    // ---------------------------------------------------------
    // LOAD GAME
    // ---------------------------------------------------------
    private void LoadGame()
    {
        //Debug.Log("LoadGame() START");

        var data = SaveSystem.Load("autosave");
        //Debug.Log($"{(data == null ? "LoadGame: data is NULL" : "LoadGame: data loaded OK")}");
        Debug.Log($"LoadName: {data?.saveName}, money: {data?.money}, placedObjects count: {data?.placedObjects.Count}");
        if (data == null)
            return;

        //Debug.Log($"LoadGame: setting money...{data.money}");
        moneyService.SetMoney(data.money);

        //Debug.Log("LoadGame: clearing placement...");
        placementSystem.ClearAll();

       // Debug.Log("LoadGame: spawning objects, count = " + data.placedObjects.Count);

        foreach (var p in data.placedObjects)
        {
            var so = registry.GetByID(p.id);
            //Debug.Log($"Spawning {p.id} at {p.x},{p.y} rot {p.rot}, so is null? {so == null}");
            placementSystem.SpawnFromSave(so, p.x, p.y, p.rot);
        }

        //grid.RebuildFromRegistry();
        //grid.LogGridVsRegistryDiagnostics();
        //Debug.Log($"After rebuild: total registry count = {PlacedObjectRegistry.All.Count}");
        //Debug.Log("LoadGame() END");
    }

    // ---------------------------------------------------------
    // SUBMENU SYSTEM
    // ---------------------------------------------------------
    private void BuildSubmenuContainer()
    {
        _submenuContainer.Clear();

        var ve = submenuContainerUxml.Instantiate();
        _submenuContainer.Add(ve);

        var root = ve.Q<VisualElement>("SubmenuRoot");
        _submenuScroll = ve.Q<ScrollView>("SubmenuScroll");

        root.RegisterCallback<MouseLeaveEvent>(_ => CloseSubmenu());
        root.RegisterCallback<PointerEnterEvent>(_ => IsPointerOverBuildMenu = true);
        root.RegisterCallback<PointerLeaveEvent>(_ => IsPointerOverBuildMenu = false);

        _submenuScroll.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            if (!_submenuOpen)
                return;

            if (evt.newRect.height <= 20f)
                return;

            PositionSubmenuNow();
        });
    }

    private void OnCategoryClicked(CategoryConfig cat)
    {
        if (_activeCategory == cat && _submenuOpen)
        {
            CloseSubmenu();
            return;
        }

        _activeCategory = cat;
        _lastClickedCategoryButton = null;

        foreach (var child in _categoryRow.Children())
        {
            var btn = child.Q<Button>("CategoryButton");
            var label = child.Q<Label>("Label");

            if (btn != null)
                btn.RemoveFromClassList("selected");

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
        if (_submenuScroll == null)
            return;

        _submenuScroll.Clear();

        if (cat.items != null)
        {
            foreach (var item in cat.items)
            {
                if (item == null)
                    continue;

                var ve = itemButtonUxml.Instantiate();

                var button = ve.Q<Button>("ItemButton");
                var icon = ve.Q<VisualElement>("Icon");
                var nameLabel = ve.Q<Label>("ItemName");
                var costLabel = ve.Q<Label>("ItemCost");

                if (item.icon != null)
                    icon.style.backgroundImage = new StyleBackground(item.icon);

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

        AudioManager.Play("UI_Open");

        PositionSubmenuAfterLayout();
    }

    private void CloseSubmenu()
    {
        if (_submenuOpen)
            AudioManager.Play("UI_Close");

        _submenuContainer.RemoveFromClassList("buildmenu-submenu-open");
        _submenuContainer.AddToClassList("buildmenu-submenu-closed");
        _submenuOpen = false;
    }

    private void PositionSubmenuAfterLayout()
    {
        _submenuContainer.schedule.Execute(() =>
        {
            PositionSubmenuNow();
        }).ExecuteLater(0);
    }

    private void PositionSubmenuNow()
    {
        if (_lastClickedCategoryButton == null)
            return;

        if (_bottomBar == null || _root == null || _submenuContainer == null)
            return;

        Vector2 rootPos = _root.worldBound.position;
        Vector2 buttonPos = _lastClickedCategoryButton.worldBound.position;

        float localX = buttonPos.x - rootPos.x;
        _submenuContainer.style.left = localX;

        float bottomY = _bottomBar.worldBound.position.y - rootPos.y;
        float submenuHeight = _submenuContainer.resolvedStyle.height;

        float gapOffset = 16;
        float top = bottomY - submenuHeight - gapOffset;

        _submenuContainer.style.top = top;
    }
}
