using UnityEngine;
using UnityEngine.UIElements;
using System;

/// <summary>
/// Modal UI for configuring a new aisle. Only exposes the two reachable levels
/// (anchor ≤ 80"); levels above that are always Reserve and never shown here.
/// Opened from a chevron's double-click (see ChevronController.OpenSetup).
/// </summary>
public class RackSetupUI : MonoBehaviour
{
    // The reachable levels the user can configure. Everything above index REACHABLE_LEVELS
    // is auto-set to Reserve when we build the full 6-length designations array for
    // LocationNameGenerator downstream.
    private const int TOTAL_LEVELS = 6;
    private const int REACHABLE_LEVELS = 2; // Level 1 (0") + Level 2 (48")

    private UIDocument _doc;
    private VisualElement _overlay;
    private VisualElement _modal;
    private TextField _aisleInput;
    private readonly DropdownField[] _levelDropdowns = new DropdownField[REACHABLE_LEVELS];
    private Button _submitButton;
    private Button _cancelButton;
    private Button _closeButton;

    public event Action<RackSetupData> OnSubmit;
    public event Action OnCancel;

    private ChevronController _selectedChevron;
    private AisleInitializer _aisleInitializer;
    private bool _initialized;

    private void OnEnable()
    {
        TryInitialize();
    }

    /// <summary>
    /// Attempts one-time setup. Safe to call from OnEnable OR Open — whichever runs
    /// after UIDocument has built its rootVisualElement. Subsequent calls no-op.
    /// </summary>
    private void TryInitialize()
    {
        if (_initialized) return;

        _doc = GetComponent<UIDocument>();
        if (_doc == null) return;

        // If UIDocument hasn't built its tree yet (execution order), bail — Open() will
        // retry after SetActive(true), by which point the root is guaranteed to exist.
        if (_doc.rootVisualElement == null) return;

        _aisleInitializer = FindFirstObjectByType<AisleInitializer>();

        InitializeUI();
        BindInputs();
        _initialized = true;
    }

    private void InitializeUI()
    {
        var root = _doc.rootVisualElement;
        _overlay = root.Q<VisualElement>("Overlay");
        _modal = root.Q<VisualElement>("Modal");
        _aisleInput = root.Q<TextField>("AisleInput");

        for (int i = 0; i < REACHABLE_LEVELS; i++)
        {
            _levelDropdowns[i] = root.Q<DropdownField>($"Level{i + 1}Dropdown");
            if (_levelDropdowns[i] != null)
            {
                _levelDropdowns[i].choices = new() { "Pick", "Reserve" };
                _levelDropdowns[i].value = "Pick"; // per spec: always default to Pick
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
                string filtered = System.Text.RegularExpressions.Regex.Replace(evt.newValue, "[^0-9]", "");
                if (filtered != evt.newValue)
                    _aisleInput.SetValueWithoutNotify(filtered);

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

    private void HandleSubmit()
    {
        // Missing or malformed aisle number → toast per spec.
        string raw = _aisleInput != null ? _aisleInput.value : null;
        if (string.IsNullOrEmpty(raw)
            || !int.TryParse(raw, out int aisleNum)
            || aisleNum < 1 || aisleNum > 99)
        {
            UIToast.Show("Aisle Number is required in this format, ##");
            return;
        }

        // Duplicate aisle number → toast per spec.
        if (AisleRegistry.IsUsed(aisleNum))
        {
            UIToast.Show("Aisle number already in use!");
            return;
        }

        // Build the full 6-length designations array. Reachable levels come from the
        // dropdowns; everything above is Reserve — LocationNameGenerator still needs
        // all six entries to produce the AA-BB-LC labels for the upper bays.
        var levelDesignations = new string[TOTAL_LEVELS];
        for (int i = 0; i < TOTAL_LEVELS; i++)
        {
            if (i < REACHABLE_LEVELS)
                levelDesignations[i] = _levelDropdowns[i]?.value ?? "Pick";
            else
                levelDesignations[i] = "Reserve";
        }

        var data = new RackSetupData
        {
            aisleNumber = aisleNum,
            levelDesignations = levelDesignations
        };

        if (_selectedChevron != null && _aisleInitializer != null)
            _aisleInitializer.SelectChevron(_selectedChevron);

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

    private void CloseUI()
    {
        gameObject.SetActive(false);
    }

    public void Open()
    {
        gameObject.SetActive(true);

        // First activation: OnEnable may have run before UIDocument built its root, so
        // element queries returned null. Retry now — SetActive(true) has already forced
        // the panel to build.
        TryInitialize();

        // Reset dropdowns to "Pick" every time the modal opens — otherwise a previous
        // aisle's Reserve choice would linger for the next one.
        for (int i = 0; i < REACHABLE_LEVELS; i++)
        {
            if (_levelDropdowns[i] != null)
                _levelDropdowns[i].value = "Pick";
        }
        if (_aisleInput != null)
        {
            _aisleInput.SetValueWithoutNotify(string.Empty);
            _aisleInput.Focus();
        }
    }
}

/// <summary>
/// Data returned when setup is confirmed.
/// </summary>
public class RackSetupData
{
    public int aisleNumber;
    public string[] levelDesignations; // length 6: reachable from UI, upper 4 always "Reserve"
}
