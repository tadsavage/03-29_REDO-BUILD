using UnityEngine;
using UnityEngine.UIElements;

public class EmployeeInfoUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EmployeeData _employeeData;

    [Header("References")]
    private VisualElement _panel;
    private Button _closeButton;
    private Label _nameLabel;
    private Label _idLabel;

    // Avatar
    private VisualElement _avatarElement;
    private VisualElement _jobIconElement;

    // Stat bars
    private VisualElement _fatigueBar;
    private VisualElement _safetyBar;
    private VisualElement _moraleBar;
    private VisualElement _skillBar;

    private Label _fatigueValue;
    private Label _safetyValue;
    private Label _moraleValue;
    private Label _skillValue;
    private Label _skillLevelLabel;

    // Runtime-only display data for record-based Show() path.
    // Avoids reusing (and potentially corrupting) a serialized asset-instance _employeeData.
    private EmployeeData _recordDisplayData;

    private bool _isVisible = false;

    public void Init(UIDocument hudDocument)
    {
        if (hudDocument == null) return;

        var root = hudDocument.rootVisualElement;
        _panel = root?.Q<VisualElement>("employee-info-panel");
        if (_panel == null)
        {
            Debug.LogError("[EmployeeInfoUI] employee-info-panel not found in HUD UXML.");
            return;
        }

        _closeButton = _panel.Q<Button>("employee-close-btn");
        _nameLabel = _panel.Q<Label>("employee-name");
        _idLabel = _panel.Q<Label>("employee-id");
        _avatarElement = _panel.Q<VisualElement>("employee-avatar");
        _jobIconElement = _panel.Q<VisualElement>("employee-job-icon");

        _fatigueBar = _panel.Q<VisualElement>("fatigue-bar-fill");
        _safetyBar = _panel.Q<VisualElement>("safety-bar-fill");
        _moraleBar = _panel.Q<VisualElement>("morale-bar-fill");
        _skillBar = _panel.Q<VisualElement>("skill-bar-fill");

        _fatigueValue = _panel.Q<Label>("fatigue-value");
        _safetyValue = _panel.Q<Label>("safety-value");
        _moraleValue = _panel.Q<Label>("morale-value");
        _skillValue = _panel.Q<Label>("skill-value");
        _skillLevelLabel = _panel.Q<Label>("skill-level");

        if (_closeButton != null)
            _closeButton.clicked += Hide;

        // Start hidden
        Hide();
    }

    public void Show(EmployeeData data = null)
    {
        if (data != null)
            _employeeData = data;

        if (_employeeData == null)
        {
            Debug.LogWarning("[EmployeeInfoUI] No EmployeeData assigned.");
            return;
        }

        _employeeData.EnsureConfigured();
        RefreshUI();

        _panel.style.display = DisplayStyle.Flex;
        _panel.pickingMode = PickingMode.Position;
        _isVisible = true;
    }

    public void Show(EmployeeRecord record)
    {
        if (record == null)
        {
            Debug.LogWarning("[EmployeeInfoUI] EmployeeRecord is null.");
            return;
        }

        // Use a dedicated runtime instance so we never corrupt a serialized asset.
        if (_recordDisplayData == null)
            _recordDisplayData = ScriptableObject.CreateInstance<EmployeeData>();

        _recordDisplayData.ApplyRecord(record);
        _recordDisplayData.SetConfigured();

        // Point to the runtime instance for RefreshUI
        _employeeData = _recordDisplayData;
        RefreshUI();

        _panel.style.display = DisplayStyle.Flex;
        _panel.pickingMode = PickingMode.Position;
        _isVisible = true;
    }

    public void Hide()
    {
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.None;
        _panel.pickingMode = PickingMode.Ignore;
        _isVisible = false;
    }

    public void Toggle(EmployeeData data = null)
    {
        if (_isVisible && data == _employeeData)
            Hide();
        else
            Show(data);
    }

    public bool IsVisible => _isVisible;

    private void RefreshUI()
    {
        if (_employeeData == null) return;

        if (_nameLabel != null)
            _nameLabel.text = _employeeData.employeeName.ToUpper();

        if (_idLabel != null)
            _idLabel.text = $"EMPLOYEE ID: {_employeeData.employeeId}";

        if (_avatarElement != null && _employeeData.avatarSprite != null)
            _avatarElement.style.backgroundImage = new StyleBackground(_employeeData.avatarSprite);

        if (_jobIconElement != null && _employeeData.jobIcon != null)
            _jobIconElement.style.backgroundImage = new StyleBackground(_employeeData.jobIcon);

        SetBar(_fatigueBar, _fatigueValue, _employeeData.fatigue, "~{0:F0}%");
        SetBar(_safetyBar, _safetyValue, _employeeData.safety, "~{0:F0}%");
        SetBar(_moraleBar, _moraleValue, _employeeData.morale, "~{0:F0}%");
        SetBar(_skillBar, _skillValue, _employeeData.skill, "{0:F0}%");

        if (_skillLevelLabel != null)
            _skillLevelLabel.text = $"LVL {_employeeData.skillLevel}";
    }

    private void SetBar(VisualElement bar, Label valueLabel, float pct, string format)
    {
        if (bar != null)
            bar.style.width = Length.Percent(Mathf.Clamp(pct, 0f, 100f));

        if (valueLabel != null)
            valueLabel.text = string.Format(format, pct);
    }
}
