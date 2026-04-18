using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


public class BuildBarUI : MonoBehaviour
{
    [SerializeField] private UIDocument uiDoc;
    [SerializeField] private ObjDataRegistry registry;

    VisualElement root;
    VisualElement categoryBar;
    VisualElement submenuContainer;

    Dictionary<string, List<ObjDataSO>> categories;

    void Awake()
    {
        root = uiDoc.rootVisualElement;
        categoryBar = root.Q<VisualElement>("CategoryBar");
        submenuContainer = root.Q<VisualElement>("SubmenuContainer");

        BuildCategoryMap();
        CreateCategoryButtons();
    }

    void BuildCategoryMap()
    {
        categories = new()
        {/*
            ["Walls"] = new() { registry.Wall, registry.Corner, registry.TWall, registry.ManDoor, registry.ShippingDoor },
            ["Floors"] = new() { registry.Lane, registry.FloorTile },
            ["Inventory"] = new() { registry.Chep, registry.StackPlts, registry.Cases, registry.ACase },
            ["MHE"] = new() { registry.Dockstocker, registry.PalletJack, registry.ReachTruck },
            ["Staff"] = new() { registry.Worker },
            ["Racking"] = new() { registry.HalfBay, registry.FullBay, registry.FullBay48 },
            ["Barriers"] = new() { registry.Barrier1, registry.Barrier2, registry.Barrier3, registry.BarrierCone, registry.BarrierCorner, registry.BarrierLow1, registry.BarrierLow2 },
            ["Flavor"] = new() { registry.GarbageCan, registry.FireExtinguisher }
            */
        };
    }

    void CreateCategoryButtons()
    {
        foreach (var kvp in categories)
        {
            string categoryName = kvp.Key;

            var btn = new Button { text = categoryName };
            btn.AddToClassList("category-button");

            btn.RegisterCallback<MouseEnterEvent>(_ => ShowSubmenu(categoryName));
            categoryBar.Add(btn);
        }
    }

    void ShowSubmenu(string category)
    {
        submenuContainer.Clear();
        submenuContainer.style.display = DisplayStyle.Flex;

        foreach (var item in categories[category])
        {
            var btn = new Button();
            btn.AddToClassList("build-button");

            if (item.icon != null)
                btn.style.backgroundImage = new StyleBackground(item.icon);

            btn.tooltip = $"{item.name}\nCost: {item.cost}";
            btn.clicked += () => OnItemClicked(item);

            submenuContainer.Add(btn);
        }

        submenuContainer.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            submenuContainer.style.display = DisplayStyle.None;
        });
    }

    void OnItemClicked(ObjDataSO data)
    {
        // Hook into your existing placement system
        Debug.Log("Selected: " + data.name);
    }
}
