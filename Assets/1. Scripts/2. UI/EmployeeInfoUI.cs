using UnityEngine;
using UnityEngine.UIElements;

public class EmployeeInfoUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EmployeeData _employeeData;

    [Header("Role")]
    [SerializeField] private RoleIconLibrary _roleIconLibrary;

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

    // Role displayed in the current panel (set by both Show() paths).
    private EmployeeRole _displayRole;

    private bool _isVisible = false;

    // ────────── Dragging ──────────
    private VisualElement _headerRow;
    private bool _isDragging;
    private Vector2 _pointerStart;   // pointer position (panel coords) at drag start
    private Vector2 _panelStart;     // panel top-left (panel coords) at drag start
    private Vector2? _customPosition;
    private int _capturedPointerId = -1;

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
        if (_panel.childCount > 0)
        {
            _headerRow = _panel.ElementAt(0);  // header row is first child
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

        _displayRole = _employeeData.role;
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

        _displayRole = record.role;
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

        // Release any captured pointer before hiding panel (prevents permanent input deadlock)
        if (_isDragging && _headerRow != null && _capturedPointerId >= 0)
        {
            _panel.ReleasePointer(_capturedPointerId);
            _capturedPointerId = -1;
        }
        _isDragging = false;

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
        // Ignore clicks on the close button or its child text label
        if (IsCloseButtonOrChild(evt.target as VisualElement))
            return;

        if (_panel == null) return;
        _isDragging = true;
        _capturedPointerId = evt.pointerId;

        // evt.position is ALREADY in panel (root) coordinates — the same space as worldBound.
        // Do NOT call LocalToWorld on it; that would double-transform and break the drag.
        _pointerStart = (Vector2)evt.position;
        _panelStart = _panel.worldBound.position;

        // Switch to absolute positioning seeded at the panel's current on-screen position,
        // so style.left/top are interpreted as panel-space coordinates, not USS-layout offsets.
        _panel.style.position = Position.Absolute;

        // CRITICAL: the UXML inline style pins the panel with `bottom: 130px` (and `left`).
        // If we only set `top`, both `top` and `bottom` are active with no fixed height,
        // so the layout engine STRETCHES the panel vertically (top moves, bottom stays pinned).
        // Clear bottom/right so left+top are the sole position drivers during/after drag.
        _panel.style.bottom = StyleKeyword.Auto;
        _panel.style.right = StyleKeyword.Auto;

        _panel.style.left = _panelStart.x;
        _panel.style.top = _panelStart.y;
        _customPosition = _panelStart;

        _panel.CapturePointer(evt.pointerId);
    }

    private bool IsCloseButtonOrChild(VisualElement target)
    {
        while (target != null)
        {
            if (target == _closeButton)
                return true;
            target = target.parent;
        }
        return false;
    }

    private void OnPanelPointerMove(PointerMoveEvent evt)
    {
        if (!_isDragging || _panel == null) return;

        // evt.position is in panel (root) coordinates — same space as _pointerStart/_panelStart.
        // Move the panel by the same delta the pointer moved. No coordinate conversion needed.
        Vector2 delta = (Vector2)evt.position - _pointerStart;
        Vector2 newPos = _panelStart + delta;

        // Clamp against the root visual tree's logical size (the panel's own coordinate space),
        // NOT Screen.width/height which can differ under PanelSettings scaling.
        VisualElement root = _panel.panel != null ? _panel.panel.visualTree : _panel.parent;
        float boundsW = root != null ? root.layout.width : Screen.width;
        float boundsH = root != null ? root.layout.height : Screen.height;

        float panelW = _panel.layout.width;
        float panelH = _panel.layout.height;
        float maxX = Mathf.Max(0f, boundsW - panelW);
        float maxY = Mathf.Max(0f, boundsH - panelH);

        newPos.x = Mathf.Clamp(newPos.x, 0f, maxX);
        newPos.y = Mathf.Clamp(newPos.y, 0f, maxY);

        _panel.style.left = newPos.x;
        _panel.style.top = newPos.y;
        _customPosition = newPos;
    }

    private void OnPanelPointerUp(PointerUpEvent evt)
    {
        if (!_isDragging) return;
        _isDragging = false;

        if (_panel != null && _panel.HasPointerCapture(evt.pointerId))
        {
            _panel.ReleasePointer(evt.pointerId);
            _capturedPointerId = -1;
        }
    }

    private void ApplyCustomPosition()
    {
        if (_customPosition.HasValue)
        {
            // Restore a previously dragged-to position: top/left drive it, bottom/right cleared
            // (mirrors the setup in OnHeaderPointerDown to avoid vertical stretching).
            _panel.style.position = Position.Absolute;
            _panel.style.bottom = StyleKeyword.Auto;
            _panel.style.right = StyleKeyword.Auto;
            _panel.style.left = _customPosition.Value.x;
            _panel.style.top = _customPosition.Value.y;
        }
    }

    // Default docking from HUD.uxml inline style on #employee-info-panel.
    private const float DefaultLeft = 16f;
    private const float DefaultBottom = 130f;

    private void ResetCustomPosition()
    {
        _customPosition = null;
        // Explicitly restore the original UXML docking (position: absolute; bottom: 130px;
        // left: 16px). We cannot rely on StyleKeyword.Null here: the defaults were authored
        // as INLINE styles in UXML, so clearing our runtime inline overrides to Null would
        // drop them to `auto` (collapsing the panel to the top-left corner) rather than
        // reverting to 130px/16px. Restoring the values explicitly is the robust fix.
        _panel.style.position = Position.Absolute;
        _panel.style.top = StyleKeyword.Auto;
        _panel.style.right = StyleKeyword.Auto;
        _panel.style.left = DefaultLeft;
        _panel.style.bottom = DefaultBottom;
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

        // ── Role icon (left frame) ─────────────────────────────────────    
        if (_jobIconElement != null && _roleIconLibrary != null)
        {
            var sprite = _roleIconLibrary.GetIcon(_displayRole);
            if (sprite != null)
            {
                _jobIconElement.style.backgroundImage = new StyleBackground(sprite);
                _jobIconElement.style.display = DisplayStyle.Flex;
            }
            else
            {
                _jobIconElement.style.display = DisplayStyle.None;
            }
        }

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
