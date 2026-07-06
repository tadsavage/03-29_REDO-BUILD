using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Globalization;
using GameCore.Inventory;

[RequireComponent(typeof(UIDocument))]
public class PalletBuilderUI : MonoBehaviour
{
    [SerializeField] private PalletBuilder targetBuilder;

    private UIDocument _doc;
    private TextField _maxHeightField;
    private TextField _spaceField;
    private TextField _gapField;
    private TextField _crookedField;
    private Toggle _overrideToggle;
    private TextField _manualTiField;
    private TextField _manualHiField;
    private DropdownField _prefabDropdown;
    private Button _buildButton;
    private Button _closeButton;

    private List<SkuData> _availableSkus = new();
    private List<string> _skuNames = new();

    public void Initialize(PalletBuilder builder)
    {
        targetBuilder = builder;
        RefreshUI();
    }

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        
        // Ensure this UI doesn't block other UI panels (like the bottom bar)
        _doc.sortingOrder = 10;

        var root = _doc.rootVisualElement;
        if (root != null)
        {
            root.pickingMode = PickingMode.Ignore;
        }

        var menuBox = root.Q<VisualElement>("Root");
        if (menuBox != null)
        {
            menuBox.pickingMode = PickingMode.Position;
            menuBox.AddManipulator(new DragManipulator(menuBox));
        }

        _maxHeightField = root.Q<TextField>("MaxHeightField");
        _spaceField = root.Q<TextField>("SpaceField");
        _gapField = root.Q<TextField>("GapField");
        _crookedField = root.Q<TextField>("CrookedField");
        _overrideToggle = root.Q<Toggle>("OverrideToggle");
        _manualTiField = root.Q<TextField>("ManualTiField");
        _manualHiField = root.Q<TextField>("ManualHiField");
        _prefabDropdown = root.Q<DropdownField>("PrefabDropdown");
        _buildButton = root.Q<Button>("BuildButton");
        _closeButton = root.Q<Button>("CloseButton");

        if (targetBuilder == null) targetBuilder = GetComponentInParent<PalletBuilder>();

        PopulatePrefabDropdown();

        if (targetBuilder != null)
        {
            RefreshUI();
        }

        _buildButton.clicked += OnBuildClicked;
        if (_closeButton != null) _closeButton.clicked += OnCloseClicked;
    }

    private void PopulatePrefabDropdown()
    {
        if (_prefabDropdown == null) return;
        _availableSkus.Clear();
        _skuNames.Clear();

        // Load all SKU assets from Resources/Inventory/SKUs
        var skus = Resources.LoadAll<SkuData>("Inventory/SKUs");
        if (skus == null || skus.Length == 0)
        {
            Debug.LogWarning("[PalletBuilderUI] No SKU data found in Resources/Inventory/SKUs");
            return;
        }

        int selectedIndex = 0;
        foreach (var sku in skus)
        {
            if (sku != null && sku.Prefab != null)
            {
                _availableSkus.Add(sku);
                string label = $"{sku.ItemNumber} - {sku.ItemDescription}";
                _skuNames.Add(label);

                // Match against current case prefab to set selected index
                if (targetBuilder != null && targetBuilder.casePrefab == sku.Prefab)
                {
                    selectedIndex = _skuNames.Count - 1;
                }
            }
        }

        _prefabDropdown.choices = _skuNames;
        if (_skuNames.Count > 0)
        {
            _prefabDropdown.index = selectedIndex;
        }
    }

    private void OnDisable()
    {
        if (_buildButton != null) _buildButton.clicked -= OnBuildClicked;
        if (_closeButton != null) _closeButton.clicked -= OnCloseClicked;
    }

    private void OnCloseClicked()
    {
        if (targetBuilder != null)
        {
            targetBuilder.ToggleUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void RefreshUI()
    {
        if (targetBuilder == null) return;

        _maxHeightField.value = targetBuilder.maxTotalHeight.ToString(CultureInfo.InvariantCulture);
        _spaceField.value = targetBuilder.spaceBetweenCases.ToString(CultureInfo.InvariantCulture);
        _gapField.value = targetBuilder.verticalGap.ToString(CultureInfo.InvariantCulture);
        _crookedField.value = targetBuilder.crookedCase.ToString(CultureInfo.InvariantCulture);
        _overrideToggle.value = targetBuilder.useTiHiOverride;
        _manualTiField.value = targetBuilder.manualTi.ToString();
        _manualHiField.value = targetBuilder.manualHi.ToString();

        // Update dropdown index if case prefab changed externally
        if (_prefabDropdown != null && targetBuilder.casePrefab != null)
        {
            for (int i = 0; i < _availableSkus.Count; i++)
            {
                if (_availableSkus[i].Prefab == targetBuilder.casePrefab)
                {
                    _prefabDropdown.index = i;
                    break;
                }
            }
        }
    }

    private void OnBuildClicked()
    {
        if (targetBuilder == null) return;

        // Set case prefab from selected SKU
        if (_prefabDropdown != null && _prefabDropdown.index >= 0 && _prefabDropdown.index < _availableSkus.Count)
        {
            var selectedSku = _availableSkus[_prefabDropdown.index];
            targetBuilder.casePrefab = selectedSku.Prefab;
            // Also link the SKU so the user can auto-compute Ti/Hi from case dimensions
            targetBuilder.linkedSku = selectedSku;
        }

        if (float.TryParse(_maxHeightField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float h))
            targetBuilder.maxTotalHeight = h;
        
        if (float.TryParse(_spaceField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float s))
            targetBuilder.spaceBetweenCases = s;

        if (float.TryParse(_gapField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float g))
            targetBuilder.verticalGap = g;

        if (float.TryParse(_crookedField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float c))
            targetBuilder.crookedCase = c;

        targetBuilder.useTiHiOverride = _overrideToggle.value;

        if (int.TryParse(_manualTiField.value, out int ti))
            targetBuilder.manualTi = ti;
        
        if (int.TryParse(_manualHiField.value, out int hi))
            targetBuilder.manualHi = hi;

        targetBuilder.Build();
    }
}
