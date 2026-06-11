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

    // ────────── Dragging ──────────
    private VisualElement _headerRow;
    private bool _isDragging;
    private Vector2 _dragOffset;
    private Vector2? _customPosition;

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

        // ────────── Dragging Setup ──────────
        if (_panel.childCount > 1)
        {
            _headerRow = _panel.ElementAt(1);
            _headerRow.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            _panel.RegisterCallback<PointerMoveEvent>(OnPanelPointerMove);
            _panel.RegisterCallback<PointerUpEvent>(OnPanelPointerUp);
        }

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
        ApplyCustomPosition();
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
        ApplyCustomPosition();
        _isVisible = true;
    }

    public void Hide()
    {
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.None;
        _panel.pickingMode = PickingMode.Ignore;
        ResetCustomPosition();
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

    // ────────── Dragging ──────────

    /// <summary>Clean up registered callbacks when the component is disabled.</summary>
    private void OnDisable()
    {
        if (_headerRow != null)
            _headerRow.UnregisterCallback<PointerDownEvent>(OnHeaderPointerDown);

        if (_panel != null)
        {
            _panel.UnregisterCallback<PointerMoveEvent>(OnPanelPointerMove);
            _panel.UnregisterCallback<PointerUpEvent>(OnPanelPointerUp);
        }
    }

    private void OnHeaderPointerDown(PointerDownEvent evt)
    {
        if (_panel == null) return;
        _isDragging = true;
        // Convert pointer position to panel-local space so offset math matches move events
        Vector2 panelLocal = _headerRow.ChangeCoordinatesTo(_panel, evt.position);
        _dragOffset = (Vector2)_panel.worldBound.position - _panel.LocalToWorld(panelLocal);
        _headerRow.CapturePointer(evt.pointerId);
    }

    private void OnPanelPointerMove(PointerMoveEvent evt)
    {
        if (!_isDragging || _panel == null) return;

        // evt.position is panel-local; must convert to world to match _dragOffset
        Vector2 pointerWorld = _panel.LocalToWorld(evt.position);
        Vector2 newPos = pointerWorld + _dragOffset;
        float panelW = _panel.worldBound.width;
        float panelH = _panel.worldBound.height;
        float maxX = Screen.width - panelW;
        float maxY = Screen.height - panelH;

        newPos.x = Mathf.Clamp(newPos.x, 0f, maxX);
        newPos.y = Mathf.Clamp(newPos.y, 0f, maxY);

        newPos.x = Mathf.Round(newPos.x);
        newPos.y = Mathf.Round(newPos.y);

        _panel.style.left = newPos.x;
        _panel.style.top = newPos.y;
        _customPosition = newPos;
    }

    private void OnPanelPointerUp(PointerUpEvent evt)
    {
        if (!_isDragging) return;
        _isDragging = false;

        if (_headerRow != null && _headerRow.HasPointerCapture(evt.pointerId))
            _headerRow.ReleasePointer(evt.pointerId);
    }

    private void ApplyCustomPosition()
    {
        if (_customPosition.HasValue)
        {
            _panel.style.position = Position.Absolute;
            _panel.style.left = _customPosition.Value.x;
            _panel.style.top = _customPosition.Value.y;
        }
    }

    private void ResetCustomPosition()
    {
        _customPosition = null;
        _panel.style.left = StyleKeyword.Null;
        _panel.style.top = StyleKeyword.Null;
    }

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
