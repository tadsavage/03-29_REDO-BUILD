using UnityEngine;
using UnityEngine.UIElements;
using System;

/// <summary>
/// Modal UI for configuring a new aisle. Only exposes the two reachable levels
/// (anchor ≤ 80"); levels above that are always Reserve and never shown here.
/// Opened from a chevron's double-click (see ChevronController.OpenSetup) for a BRAND NEW aisle,
/// or from double-clicking an already-live rack (see RackEditInteractionService) to EDIT an
/// existing one — even mid-game, with pallets already stored in it. Edit mode reuses the exact
/// same aisle-number + level dropdowns, but submits through <see cref="OnEditSubmit"/> instead of
/// <see cref="OnSubmit"/> so AisleInitializer can route it to a rename instead of a first commit.
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

    /// <summary>Fired instead of <see cref="OnSubmit"/> when the modal was opened via
    /// <see cref="OpenForEdit"/> (double-click on an already-live rack) rather than a chevron.</summary>
    public event Action<RackEditData> OnEditSubmit;

    private ChevronController _selectedChevron;
    private AisleInitializer _aisleInitializer;
    private bool _initialized;

    // >= 0 while editing an already-live aisle (see OpenForEdit); -1 means this is a normal
    // brand-new-aisle setup driven by a chevron double-click.
    private int _editingAisle = -1;

    /// <summary>True while the modal is currently shown — lets other double-click listeners
    /// (ChevronController, RackEditInteractionService) avoid opening a second one on top.</summary>
    public bool IsOpen => _overlay != null && _overlay.style.display == DisplayStyle.Flex;

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

        // The root can exist but be EMPTY when visualTreeAsset is assigned after the
        // UIDocument's first OnEnable (the RackingSystemManager auto-create path). If we
        // latched _initialized here we'd bind against elements that don't exist yet and
        // never re-run. Wait until the real UXML content is actually present.
        if (_doc.rootVisualElement.Q<VisualElement>("Overlay") == null) return;

        _aisleInitializer = FindAnyObjectByType<AisleInitializer>();

        InitializeUI();
        BindInputs();
        Hide(); // start hidden — visibility is driven by display, NOT GameObject active-state
                // (an inactive UIDocument sharing a PanelSettings can still render on screen)
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

        ApplyBlueprintBackground(root);
    }

    /// <summary>
    /// Loads the faded blueprint schematic into the modal background. Drop the image at
    /// Assets/_Project/Resources/RackBlueprint.png and it auto-loads in editor AND builds.
    /// The USS handles the fade (.rsu-blueprint opacity); this just assigns the texture.
    /// </summary>
    private void ApplyBlueprintBackground(VisualElement root)
    {
        var blueprint = root.Q<VisualElement>("Blueprint");
        if (blueprint == null) return;

        Texture2D tex = Resources.Load<Texture2D>("RackBlueprint");
#if UNITY_EDITOR
        if (tex == null)
            tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/_Project/Resources/RackBlueprint.png");
#endif
        if (tex != null)
            blueprint.style.backgroundImage = new StyleBackground(tex);
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

        bool isEdit = _editingAisle >= 0;

        // Duplicate aisle number → toast per spec. In edit mode, keeping the SAME number is fine
        // (that's just a level-scheme change) — only a number that belongs to a DIFFERENT aisle
        // is rejected.
        if (aisleNum != _editingAisle && AisleRegistry.IsUsed(aisleNum))
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

        if (isEdit)
        {
            var editData = new RackEditData
            {
                previousAisleNumber = _editingAisle,
                newAisleNumber = aisleNum,
                levelDesignations = levelDesignations
            };
            _editingAisle = -1;
            OnEditSubmit?.Invoke(editData);
            CloseUI();
            return;
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
        _editingAisle = -1;
        OnCancel?.Invoke();
        CloseUI();
    }

    private void CloseUI()
    {
        Hide();
    }

    /// <summary>
    /// Shows the modal. The GameObject stays active the whole time; visibility is toggled
    /// via the overlay's display style. This is deliberate — driving visibility with
    /// SetActive() is unreliable here because an inactive UIDocument that shares a
    /// PanelSettings can remain attached to the panel and keep rendering on screen.
    /// </summary>
    public void Open()
    {
        // Safety: in case OnEnable ran before the tree existed, ensure we're initialized.
        TryInitialize();

        _editingAisle = -1; // a chevron-driven open is always a brand-new aisle, never an edit

        // Reset dropdowns to "Pick" every time the modal opens — otherwise a previous
        // aisle's Reserve choice would linger for the next one.
        for (int i = 0; i < REACHABLE_LEVELS; i++)
        {
            if (_levelDropdowns[i] != null)
                _levelDropdowns[i].value = "Pick";
        }
        if (_aisleInput != null)
            _aisleInput.SetValueWithoutNotify(string.Empty);

        Show();

        if (_aisleInput != null)
            _aisleInput.Focus();
    }

    /// <summary>
    /// Reopens the modal to EDIT an already-live aisle (see RackEditInteractionService), prefilled
    /// with its current aisle number and per-level Pick/Reserve scheme. Submitting fires
    /// <see cref="OnEditSubmit"/> instead of <see cref="OnSubmit"/> — AisleInitializer routes that
    /// to AisleRenameService rather than the first-commit path, since the rack, its geometry, and
    /// its bay/travel-direction metadata are already settled and must not be touched.
    /// </summary>
    public void OpenForEdit(int aisleNumber)
    {
        TryInitialize();

        _editingAisle = aisleNumber;
        _selectedChevron = null; // edit mode never has a chevron behind it

        string[] designations = AisleRegistry.GetDesignations(aisleNumber);
        for (int i = 0; i < REACHABLE_LEVELS; i++)
        {
            if (_levelDropdowns[i] == null) continue;
            _levelDropdowns[i].value = (designations != null && i < designations.Length)
                ? designations[i]
                : "Pick";
        }
        if (_aisleInput != null)
            _aisleInput.SetValueWithoutNotify(aisleNumber.ToString("D2"));

        Show();

        if (_aisleInput != null)
            _aisleInput.Focus();
    }

    private void Show()
    {
        if (_overlay != null) _overlay.style.display = DisplayStyle.Flex;
        UIModalGuard.Push(this); // suppress number-key panel hotkeys while typing the aisle number
    }

    private void Hide()
    {
        if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        UIModalGuard.Pop(this);
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

/// <summary>
/// Data returned when an EDIT (see RackSetupUI.OpenForEdit) is confirmed — carries the aisle's OLD
/// number too, since AisleRenameService needs it to find every rack currently tagged with it.
/// </summary>
public class RackEditData
{
    public int previousAisleNumber;
    public int newAisleNumber;
    public string[] levelDesignations; // length 6: reachable from UI, upper 4 always "Reserve"
}
