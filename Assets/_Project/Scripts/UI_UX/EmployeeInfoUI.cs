using System.Collections.Generic;
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

    // Actions dropdown (same capability as the roster card). Built in code so the info card
    // gets it without a UXML change. Choices are rebuilt per-employee in RefreshActionsDropdown
    // (Patrol + Terminate are universal, plus one role-specific assignment if the role has one).
    // Only Terminate closes the card.
    private DropdownField _actionsDropdown;
    private const string ActionDefault     = "Actions...";
    private const string ActionLocate      = "Locate";
    private const string ActionPatrol      = "Patrol";
    private const string ActionAskOvertime = "Ask to Work OT";
    private const string ActionSendHome    = "Send Home";
    private const string ActionTerminate   = "Terminate";

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

    // Scene object backing the currently-displayed employee (resolved from the registry by
    // GUID). Drives the shift-click "highlight + camera focus" on the avatar. Null if the
    // displayed employee has no live scene instance (e.g. the dummy EmployeeData fallback).
    private EmployeeIdentity _currentIdentity;

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
        var panel = root?.Q<VisualElement>("employee-info-panel");
        if (panel == null)
        {
            Debug.LogError("[EmployeeInfoUI] employee-info-panel not found in HUD UXML.");
            return;
        }

        // Clean up previous event registrations if already initialized to avoid duplicate callbacks
        if (_panel != null)
        {
            if (_closeButton != null)
                _closeButton.clicked -= Hide;
            if (_headerRow != null)
                _headerRow.UnregisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            if (_avatarElement != null)
                _avatarElement.UnregisterCallback<PointerDownEvent>(OnAvatarPointerDown);
            _panel.UnregisterCallback<PointerMoveEvent>(OnPanelPointerMove);
            _panel.UnregisterCallback<PointerUpEvent>(OnPanelPointerUp);

            // Drop any actions dropdown from a previous Init/panel so we don't duplicate it.
            if (_actionsDropdown != null)
            {
                _actionsDropdown.RemoveFromHierarchy();
                _actionsDropdown = null;
            }
        }

        _panel = panel;
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
        {
            _closeButton.clicked += Hide;

            // Red hover effect on close button
            _closeButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                _closeButton.style.backgroundColor = new StyleColor(new Color(0xE6 / 255f, 0x50 / 255f, 0x50 / 255f, 0.3f));
                _closeButton.style.color = new StyleColor(Color.white);
            });
            _closeButton.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _closeButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
                _closeButton.style.color = new StyleColor(new Color(0x8A / 255f, 0xAA / 255f, 0xBB / 255f, 1f));
            });
        }

        // ────────── Dragging Setup ──────────
        if (_panel.childCount > 0)
        {
            _headerRow = _panel.ElementAt(0);  // header row is first child
            _headerRow.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            _panel.RegisterCallback<PointerMoveEvent>(OnPanelPointerMove);
            _panel.RegisterCallback<PointerUpEvent>(OnPanelPointerUp);
        }

        // Shift-click the portrait → outline the employee in the world + make them the
        // camera focal point. Plain clicks on the avatar do nothing (so it never fights drag).
        if (_avatarElement != null)
            _avatarElement.RegisterCallback<PointerDownEvent>(OnAvatarPointerDown);

        // Actions dropdown. Choices depend on the displayed employee's role, so the real list
        // is populated per-Show() by RefreshActionsDropdown — this is just a placeholder until
        // then. Appended to the bottom of the card.
        _actionsDropdown = new DropdownField { name = "employee-actions" };
        _actionsDropdown.choices = new List<string> { ActionDefault };
        _actionsDropdown.SetValueWithoutNotify(ActionDefault);
        _actionsDropdown.style.marginTop    = 20;
        _actionsDropdown.style.marginLeft   = 2;
        _actionsDropdown.style.marginRight  = 2;
        _actionsDropdown.style.fontSize = 16;
        _actionsDropdown.style.color = Color.white;

        var dropdownText = _actionsDropdown.Q(className: "unity-base-popup-field__text");
        if (dropdownText != null)
        {
            dropdownText.style.fontSize = 16;
            dropdownText.style.color = Color.white;
        }

        _actionsDropdown.RegisterValueChangedCallback(OnActionSelected);
        _panel.Add(_actionsDropdown);

        // Start hidden
        Hide();
    }

    private void EnsureInitialized()
    {
        if (_panel != null) return;

        var docs = Object.FindObjectsByType<UIDocument>();
        foreach (var doc in docs)
        {
            if (doc != null && doc.rootVisualElement != null)
            {
                var panel = doc.rootVisualElement.Q<VisualElement>("employee-info-panel");
                if (panel != null)
                {
                    Init(doc);
                    return;
                }
            }
        }
    }

    public void Show(EmployeeData data = null)
    {
        EnsureInitialized();

        if (_panel == null)
        {
            Debug.LogError("[EmployeeInfoUI] _panel is null when attempting to Show. Ensure a valid HUD UIDocument is loaded.");
            return;
        }

        if (data != null)
            _employeeData = data;

        if (_employeeData == null)
        {
            Debug.LogWarning("[EmployeeInfoUI] No EmployeeData assigned.");
            return;
        }

        _displayRole = _employeeData.role;
        _employeeData.EnsureConfigured();

        // This path is the no-record fallback; try to resolve a scene object by GUID if the
        // data carries one, otherwise shift-click highlight is simply unavailable.
        _currentIdentity = (EmployeeRegistry.Instance != null && !string.IsNullOrEmpty(_employeeData.employeeId))
            ? EmployeeRegistry.Instance.GetByGuid(_employeeData.employeeId)
            : null;

        // Opening/switching the card drops any previous focus.
        DropFocus();

        RefreshUI();

        // Start live feed for animated portrait if it's a real record or has enough data
        if (EmployeePhotoBooth.Instance != null)
        {
            // We need a record for the photo booth. If we only have EmployeeData, 
            // we can export a temporary record.
            EmployeeRecord tempRecord = _employeeData.ExportToRecord();
            EmployeePhotoBooth.Instance.StartLiveFeed(tempRecord);
            
            if (_avatarElement != null && EmployeePhotoBooth.Instance.LiveRenderTexture != null)
            {
                _avatarElement.style.backgroundImage = Background.FromRenderTexture(EmployeePhotoBooth.Instance.LiveRenderTexture);
            }
        }

        _panel.style.display = DisplayStyle.Flex;
        _panel.pickingMode = PickingMode.Position;
        ApplyCustomPosition();
        _isVisible = true;
    }

    public void Show(EmployeeRecord record)
    {
        EnsureInitialized();

        if (_panel == null)
        {
            Debug.LogError("[EmployeeInfoUI] _panel is null when attempting to Show. Ensure a valid HUD UIDocument is loaded.");
            return;
        }

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

        // Resolve the live scene object so shift-click on the avatar can highlight/focus it.
        _currentIdentity = EmployeeRegistry.Instance != null
            ? EmployeeRegistry.Instance.GetByGuid(record.employeeGuid)
            : null;

        // Opening/switching the card drops any previous focus (e.g. left-clicking a different
        // employee). A subsequent shift-click re-establishes it on this one.
        DropFocus();

        // Point to the runtime instance for RefreshUI
        _employeeData = _recordDisplayData;
        RefreshUI();

        // Start live feed for animated portrait
        if (EmployeePhotoBooth.Instance != null)
        {
            EmployeePhotoBooth.Instance.StartLiveFeed(record);
            if (_avatarElement != null && EmployeePhotoBooth.Instance.LiveRenderTexture != null)
            {
                _avatarElement.style.backgroundImage = Background.FromRenderTexture(EmployeePhotoBooth.Instance.LiveRenderTexture);
            }
        }

        _panel.style.display = DisplayStyle.Flex;
        _panel.pickingMode = PickingMode.Position;
        ApplyCustomPosition();
        _isVisible = true;
    }

    // Focus (world outline + camera follow) only lives while THIS card is open and showing the
    // employee. Every (re)open and every close drops the previous focus; a shift-click on the
    // portrait (or a roster shift-click) is what re-establishes it. Guarded so we never spin up
    // a highlighter just to clear nothing.
    private void DropFocus()
    {
        if (EmployeeHighlighter.HasInstance)
            EmployeeHighlighter.Instance.Clear();
    }

    public void Hide()
    {
        if (_panel == null) return;

        // Card closed by any means (red X, F2 toggle, Escape, terminate) → lose focus.
        DropFocus();

        // The photo booth live feed (animated model, camera, light, mood gestures) only
        // exists to feed this card's avatar — tear it down so it isn't left running/visible
        // at the booth's position in the world once the card is closed.
        if (EmployeePhotoBooth.Instance != null)
            EmployeePhotoBooth.Instance.StopLiveFeed();

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

    // ────────── Highlight / focus ──────────

    /// <summary>Shift-click the portrait → outline this employee in the world and make the
    /// camera focus on them. Plain (non-shift) clicks are ignored.</summary>
    private void OnAvatarPointerDown(PointerDownEvent evt)
    {
        if (!evt.shiftKey) return;
        if (_currentIdentity == null) return;

        EmployeeHighlighter.Instance.FocusAndHighlight(_currentIdentity);
        evt.StopPropagation();
    }

    /// <summary>Rebuilds the dropdown's choices for the currently displayed employee: Locate + Patrol +
    /// Terminate are universal, plus one role-specific assignment (Drive Reach / Drive
    /// Dockstalker / Order Selection) if EmployeeRoleExtensions.RoleSpecificAssignment returns
    /// one for _displayRole.</summary>
    private void RefreshActionsDropdown()
    {
        if (_actionsDropdown == null) return;

        var choices = new List<string> { ActionDefault, ActionLocate, ActionPatrol };
        var roleAssignment = _displayRole.RoleSpecificAssignment();
        if (roleAssignment.HasValue)
            choices.Add(roleAssignment.Value.DisplayName());
        choices.Add(ActionAskOvertime);
        choices.Add(ActionSendHome);
        choices.Add(ActionTerminate);

        _actionsDropdown.choices = choices;
        _actionsDropdown.SetValueWithoutNotify(ActionDefault);
    }

    /// <summary>Actions dropdown handler. Terminate runs the full HR/termination process and,
    /// because the person no longer works here, closes this info card. Every other action
    /// routes to EmployeeAssignmentService or EmployeeHighlighter and does NOT close the card.</summary>
    private void OnActionSelected(ChangeEvent<string> evt)
    {
        string selected = evt.newValue;

        // Reset the dropdown straight away so it never sticks on the chosen action.
        _actionsDropdown?.SetValueWithoutNotify(ActionDefault);

        if (selected == ActionDefault) return;

        var id = _currentIdentity;
        if (id == null || id.Record == null) return;

        if (selected == ActionLocate)
        {
            EmployeeHighlighter.Instance.FocusAndHighlight(id);
            return;
        }

        if (selected == ActionTerminate)
        {
            EmployeeTerminationService.Terminate(id);
            Hide(); // Terminated → they're gone from the company, so close the card.
            return;
        }

        if (selected == ActionPatrol)
        {
            EmployeeAssignmentService.Assign(id, EmployeeAssignment.Patrol);
            return;
        }

        if (selected == ActionAskOvertime)
        {
            EmployeeOvertimeService.AskToWorkOvertime(id);
            return;
        }

        if (selected == ActionSendHome)
        {
            EmployeeOvertimeService.SendHome(id);
            return;
        }

        var roleAssignment = _displayRole.RoleSpecificAssignment();
        if (roleAssignment.HasValue && selected == roleAssignment.Value.DisplayName())
            EmployeeAssignmentService.Assign(id, roleAssignment.Value);
    }

    // ────────── Dragging ──────────

    /// <summary>Clean up registered callbacks when the component is disabled.</summary>
    private void OnDisable()
    {
        if (_headerRow != null)
            _headerRow.UnregisterCallback<PointerDownEvent>(OnHeaderPointerDown);

        if (_avatarElement != null)
            _avatarElement.UnregisterCallback<PointerDownEvent>(OnAvatarPointerDown);

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

        RefreshActionsDropdown();
    }

    private void SetBar(VisualElement bar, Label valueLabel, float pct, string format)
    {
        if (bar != null)
            bar.style.width = Length.Percent(Mathf.Clamp(pct, 0f, 100f));

        if (valueLabel != null)
            valueLabel.text = string.Format(format, pct);
    }
}
