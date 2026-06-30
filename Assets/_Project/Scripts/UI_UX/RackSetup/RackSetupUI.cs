using UnityEngine;
using UnityEngine.UIElements;
using System;

/// <summary>
/// Modal UI for configuring a new aisle/rack setup.
/// Player enters aisle number (01-99) and designates pick vs reserve levels.
/// Receives chevron/collection context from selected chevron.
/// </summary>
public class RackSetupUI : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _overlay;
    private VisualElement _modal;
    private TextField _aisleInput;
    private DropdownField[] _levelDropdowns = new DropdownField[6];
    private Button _submitButton;
    private Button _cancelButton;
    private Button _closeButton;

    public event Action<RackSetupData> OnSubmit;
    public event Action OnCancel;

    private ChevronController _selectedChevron;
    private AisleInitializer _aisleInitializer;

    private const int HEIGHT_THRESHOLD_INCHES = 80;
    private const int LEVEL_1_HEIGHT = 0;    // inches
    private const int LEVEL_2_HEIGHT = 48;
    private const int LEVEL_3_HEIGHT = 96;   // Above 80", locked as reserve
    private const int LEVEL_4_HEIGHT = 144;  // Above 80", locked as reserve
    private const int LEVEL_5_HEIGHT = 192;  // Above 80", locked as reserve
    private const int LEVEL_6_HEIGHT = 240;  // Above 80", locked as reserve

    private int[] levelHeights = {
        LEVEL_1_HEIGHT, LEVEL_2_HEIGHT, LEVEL_3_HEIGHT,
        LEVEL_4_HEIGHT, LEVEL_5_HEIGHT, LEVEL_6_HEIGHT
    };

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        if (_doc == null) return;

        _aisleInitializer = FindObjectOfType<AisleInitializer>();

        InitializeUI();
        BindInputs();
        SetupLevelRestrictions();
    }

    private void InitializeUI()
    {
        var root = _doc.rootVisualElement;
        _overlay = root.Q<VisualElement>("Overlay");
        _modal = root.Q<VisualElement>("Modal");
        _aisleInput = root.Q<TextField>("AisleInput");

        for (int i = 0; i < 6; i++)
        {
            _levelDropdowns[i] = root.Q<DropdownField>($"Level{i + 1}Dropdown");
            if (_levelDropdowns[i] != null)
            {
                _levelDropdowns[i].choices = new() { "Pick", "Reserve" };
                _levelDropdowns[i].value = "Pick";
            }
        }

        _submitButton = root.Q<Button>("SubmitButton");
        _cancelButton = root.Q<Button>("CancelButton");
        _closeButton = root.Q<Button>("CloseButton");
    }

    private void BindInputs()
    {
        if (_aisleInput != null)
        {
            _aisleInput.RegisterValueChangedCallback(evt =>
            {
                // Only allow numeric characters
                string filtered = System.Text.RegularExpressions.Regex.Replace(evt.newValue, "[^0-9]", "");
                if (filtered != evt.newValue)
                    _aisleInput.SetValueWithoutNotify(filtered);

                // Limit to 2 digits
                if (filtered.Length > 2)
                    _aisleInput.SetValueWithoutNotify(filtered.Substring(0, 2));
            });
        }

        if (_submitButton != null)
            _submitButton.clicked += HandleSubmit;

        if (_cancelButton != null)
            _cancelButton.clicked += HandleCancel;

        if (_closeButton != null)
            _closeButton.clicked += HandleCancel;
    }

    private void SetupLevelRestrictions()
    {
        for (int i = 0; i < 6; i++)
        {
            if (levelHeights[i] >= HEIGHT_THRESHOLD_INCHES)
            {
                // Lock as reserve, disable interaction
                if (_levelDropdowns[i] != null)
                {
                    _levelDropdowns[i].value = "Reserve";
                    _levelDropdowns[i].SetEnabled(false);
                    _levelDropdowns[i].style.opacity = 0.5f;
                }
            }
        }
    }

    private void HandleSubmit()
    {
        // Validate aisle input
        if (string.IsNullOrEmpty(_aisleInput.value))
        {
            ShowValidationError("Aisle number is required");
            return;
        }

        if (!int.TryParse(_aisleInput.value, out int aisleNum) || aisleNum < 1 || aisleNum > 99)
        {
            ShowValidationError("Aisle number must be 01-99");
            return;
        }

        // Build level designations
        var levelDesignations = new string[6];
        for (int i = 0; i < 6; i++)
        {
            levelDesignations[i] = _levelDropdowns[i]?.value ?? "Reserve";
        }

        // Create data and notify
        var data = new RackSetupData
        {
            aisleNumber = aisleNum,
            levelDesignations = levelDesignations
        };

        // Pass chevron context to AisleInitializer before submission
        if (_selectedChevron != null && _aisleInitializer != null)
        {
            _aisleInitializer.SelectChevron(_selectedChevron);
        }

        OnSubmit?.Invoke(data);
        CloseUI();
    }

    public void SetSelectedChevron(ChevronController chevron)
    {
        _selectedChevron = chevron;
    }

    private void HandleCancel()
    {
        OnCancel?.Invoke();
        CloseUI();
    }

    private void ShowValidationError(string message)
    {
        // TODO: Show toast or error label
        Debug.LogWarning($"Validation Error: {message}");
    }

    private void CloseUI()
    {
        gameObject.SetActive(false);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        _aisleInput?.Focus();
    }
}

/// <summary>
/// Data returned when setup is confirmed
/// </summary>
public class RackSetupData
{
    public int aisleNumber;
    public string[] levelDesignations;  // Array of "Pick" or "Reserve" for each level
}
