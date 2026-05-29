using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Globalization;

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

    private List<ObjDataSO> _availablePrefabs = new();
    private List<string> _prefabNames = new();

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
        _availablePrefabs.Clear();
        _prefabNames.Clear();

        var buildMenu = FindAnyObjectByType<BuildMenuUI>();
        if (buildMenu == null || buildMenu.registry == null) return;

        int selectedIndex = 0;
        foreach (var so in buildMenu.registry.buttonSOs)
        {
            if (so != null && so.category == "Inventory" && so.prefab != null && !so.objName.Contains("Chep"))
            {
                _availablePrefabs.Add(so);
                _prefabNames.Add(so.objName);

                if (targetBuilder != null && targetBuilder.casePrefab == so.prefab)
                {
                    selectedIndex = _prefabNames.Count - 1;
                }
            }
        }

        _prefabDropdown.choices = _prefabNames;
        if (_prefabNames.Count > 0)
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

        // Update dropdown index if prefab changed externally
        if (_prefabDropdown != null && targetBuilder.casePrefab != null)
        {
            for (int i = 0; i < _availablePrefabs.Count; i++)
            {
                if (_availablePrefabs[i].prefab == targetBuilder.casePrefab)
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

        if (_prefabDropdown != null && _prefabDropdown.index >= 0 && _prefabDropdown.index < _availablePrefabs.Count)
        {
            targetBuilder.casePrefab = _availablePrefabs[_prefabDropdown.index].prefab;
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
