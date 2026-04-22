using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildMenuUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ObjDataRegistry registry;

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

    // Utility Button setup stuff ***********************************
    [Serializable]
    public class UtilityButtonConfig
    {
        public string id;        // "DELETE", "MOVE", etc.
        public Texture2D icon;   // your original icon
    }
    [SerializeField] private List<UtilityButtonConfig> utilityButtons = new();
    // ***************************************************************

    // Events for other systems
    public Action<ObjDataSO> OnBuildItemClicked;
    public Action OnDeleteClicked;
    public Action OnMoveClicked;
    public Action OnUndoClicked;
    public Action OnRedoClicked;
    public Action OnCancelClicked;
    public Action OnRotateClicked;

    private UIDocument _uiDoc;
    private VisualElement _root;
    private VisualElement _bottomBar;
    private VisualElement _categoryRow;
    private VisualElement _utilityRow;
    private VisualElement _submenuContainer;
    private ScrollView _submenuScroll;

    private CategoryConfig _activeCategory;
    private bool _submenuOpen;

    [Serializable]
    public class CategoryConfig
    {
        public string id;              // "Walls", "Floors", etc.
        public string displayName;     // label text
        public Texture2D icon;         // category icon
        public List<ObjDataSO> items;  // items in this category
    }

    // Keeps camera from zooming when scrolling over the build menu
    public static bool IsPointerOverBuildMenu;

    private void Awake()
    {
        _uiDoc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (_uiDoc == null)
            _uiDoc = GetComponent<UIDocument>();

        if (buildMenuUxml != null)
        {
            _root = _uiDoc.rootVisualElement;
        }
        else
        {
            _root = _uiDoc.rootVisualElement;
        }

        if (buildMenuStyle != null)
            _root.styleSheets.Add(buildMenuStyle);

        CacheElements();
        BuildCategoryButtons();
        BuildUtilityButtons();
        BuildSubmenuContainer();
        CloseSubmenu();
    }

    private void CacheElements()
    {
        _bottomBar = _root.Q<VisualElement>("BottomBar");
        _categoryRow = _root.Q<VisualElement>("CategoryRow");
        _utilityRow = _root.Q<VisualElement>("UtilityRow");
        _submenuContainer = _root.Q<VisualElement>("SubmenuContainer");
    }

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
            }

            _utilityRow.Add(ve);
        }
    }


    private Button CreateUtilityButton(string text, Action onClick)
    {
        var btn = new Button { text = text };
        btn.AddToClassList("buildmenu-utility-button");
        btn.clicked += () => onClick?.Invoke();
        return btn;
    }

    private void BuildSubmenuContainer()
    {
        _submenuContainer.Clear();

        var ve = submenuContainerUxml.Instantiate();
        _submenuContainer.Add(ve);

        var root = ve.Q<VisualElement>("SubmenuRoot");
        _submenuScroll = ve.Q<ScrollView>("SubmenuScroll");

        // Close when mouse leaves submenu
        root.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            CloseSubmenu();
        });

        root.RegisterCallback<PointerEnterEvent>(_ =>
        {
            IsPointerOverBuildMenu = true;
        });

        root.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            IsPointerOverBuildMenu = false;
        });
    }

    private void OnCategoryClicked(CategoryConfig cat)
    {
        // If clicking the same category while open → close it
        if (_activeCategory == cat && _submenuOpen)
        {
            CloseSubmenu();
            return;
        }

        _activeCategory = cat;

        //
        // 1. Remove "selected" class from ALL category buttons
        //
        foreach (var child in _categoryRow.Children())
        {
            var btn = child.Q<Button>("CategoryButton");
            if (btn != null)
                btn.RemoveFromClassList("selected");
        }

        //
        // 2. Add "selected" class to the clicked button
        //
        // We need to find the actual button instance that was clicked.
        // The easiest way is to locate it by matching the category ID.
        //
        foreach (var child in _categoryRow.Children())
        {
            var btn = child.Q<Button>("CategoryButton");
            var label = child.Q<Label>("Label");

            if (btn != null && label != null && label.text == cat.displayName)
            {
                btn.AddToClassList("selected");
                break;
            }
        }

        //
        // 3. Open submenu for this category
        //
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

                // Set icon
                if (item.icon != null)
                    icon.style.backgroundImage = new StyleBackground(item.icon);

                // Set name + cost
                nameLabel.text = item.objName;
                costLabel.text = $"${item.cost}";

                // Tooltip + click
                button.tooltip = item.objName;
                var capturedItem = item;
                button.clicked += () => OnBuildItemClicked?.Invoke(capturedItem);

                _submenuScroll.Add(ve);
            }
        }

        _submenuContainer.RemoveFromClassList("buildmenu-submenu-closed");
        _submenuContainer.AddToClassList("buildmenu-submenu-open");
        _submenuOpen = true;
    }

    private void CloseSubmenu()
    {
        _submenuContainer.RemoveFromClassList("buildmenu-submenu-open");
        _submenuContainer.AddToClassList("buildmenu-submenu-closed");
        _submenuOpen = false;
    }
}
