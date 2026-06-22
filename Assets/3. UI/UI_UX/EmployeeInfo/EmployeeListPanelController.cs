// METADATA file_path: Assets/1. Scripts/2. UI/EmployeeListPanelController.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

/// <summary>
/// Modal employee list + detail panel.
/// Uses its own UIDocument (not shared with HUD) — following the SaveLoadWindow pattern.
///
/// Open via EmployeeListPanelController.Instance?.Open(), or toggle with F4 in play mode.
/// Wire in UIBootstrapper via [SerializeField] or FindObjectOfType.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class EmployeeListPanelController : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static EmployeeListPanelController Instance { get; private set; }

    // ─── Serialized ───────────────────────────────────────────────────────────
    [SerializeField] private VisualTreeAsset _listItemTemplate;
    [SerializeField] private RoleIconLibrary _roleIconLibrary;

    private DraggableWindow _dragger;   // drag-by-title-bar
    private DraggableWindowPersistence _posPersist;

    [Header("Behaviour")]
    [Tooltip("Toggle the panel with F4 in play mode.")]
    [SerializeField] private bool _enableHotkey = true;

    // ─── State ────────────────────────────────────────────────────────────────
    private UIDocument _doc;
    private VisualElement _overlay;
    private VisualElement _modal;
    private Button _closeButton;
    private Label _employeeCountLabel;
    private ScrollView _scrollView;
    private VisualElement _listContainer;

    // Filter buttons
    private Button _filterAll;
    private Button _filterActive;
    private Button _filterInjured;
    private Button _filterFormer;

    // Detail panel
    private VisualElement _detailColumn;
    private VisualElement _detailPlaceholder;
    private Label _detailName;
    private Label _detailId;
    private Label _detailStatusBadge;
    private VisualElement _detailJobIcon;

    // Stat bars
    private VisualElement _detailFatigueFill, _detailSafetyFill, _detailMoraleFill, _detailSkillFill;
    private Label _detailFatigueVal, _detailSafetyVal, _detailMoraleVal, _detailSkillVal, _detailSkillLevel;

    // Employment info
    private Label _detailWage, _detailHireDate, _detailTotalPaid;

    // Schedule
    private VisualElement _scheduleWeek;
    private Label _scheduleNotes;

    // Injury
    private VisualElement _injurySection;
    private Label _injuryDescription;
    private Label _injuryRecovery;

    // Action buttons
    private Button _actionFire, _actionSuspend, _actionLeave;
    private Button _actionReactivate, _actionInjure, _actionRecover;

    // Row tracking
    private readonly List<VisualElement> _rows = new List<VisualElement>();
    private EmployeeRecord _selectedRecord;
    private string _activeFilter = "all";

    // Camera cache for locate-button
    private FreeLookCamera _camera;

    // Subscription guard
    private bool _subscribed;
    private Coroutine _subscribeRetry;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDisable()
    {
        UnsubscribeFromRegistry();
    }

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        if (_doc == null)
        {
            Debug.LogError("[EmployeeListPanel] No UIDocument found on GameObject.");
            return;
        }

        var root = _doc.rootVisualElement;
        if (root == null) return;

        // Overlay / modal
        _overlay = root.Q<VisualElement>("employee-overlay");
        _modal = root.Q<VisualElement>("employee-modal");
        _closeButton = root.Q<Button>("close-button");
        _employeeCountLabel = root.Q<Label>("employee-count");

        // List
        _scrollView = root.Q<ScrollView>("employee-scroll");
        _listContainer = root.Q<VisualElement>("employee-list-container");

        // Filters
        _filterAll = root.Q<Button>("filter-all");
        _filterActive = root.Q<Button>("filter-active");
        _filterInjured = root.Q<Button>("filter-injured");
        _filterFormer = root.Q<Button>("filter-former");

        // Detail
        _detailColumn = root.Q<VisualElement>("detail-column");
        _detailPlaceholder = root.Q<VisualElement>("detail-placeholder");
        _detailName = root.Q<Label>("detail-name");
        _detailId = root.Q<Label>("detail-id");
        _detailStatusBadge = root.Q<Label>("detail-status-badge");
        _detailJobIcon = root.Q<VisualElement>("detail-job-icon");

        _detailFatigueFill = root.Q<VisualElement>("detail-fatigue-fill");
        _detailSafetyFill = root.Q<VisualElement>("detail-safety-fill");
        _detailMoraleFill = root.Q<VisualElement>("detail-morale-fill");
        _detailSkillFill = root.Q<VisualElement>("detail-skill-fill");

        _detailFatigueVal = root.Q<Label>("detail-fatigue-val");
        _detailSafetyVal = root.Q<Label>("detail-safety-val");
        _detailMoraleVal = root.Q<Label>("detail-morale-val");
        _detailSkillVal = root.Q<Label>("detail-skill-val");
        _detailSkillLevel = root.Q<Label>("detail-skill-level");

        _detailWage = root.Q<Label>("detail-wage");
        _detailHireDate = root.Q<Label>("detail-hire-date");
        _detailTotalPaid = root.Q<Label>("detail-total-paid");

        _scheduleWeek = root.Q<VisualElement>("schedule-week");
        _scheduleNotes = root.Q<Label>("schedule-notes");

        _injurySection = root.Q<VisualElement>("injury-section");
        _injuryDescription = root.Q<Label>("injury-description");
        _injuryRecovery = root.Q<Label>("injury-recovery");

        _actionFire = root.Q<Button>("action-fire");
        _actionSuspend = root.Q<Button>("action-suspend");
        _actionLeave = root.Q<Button>("action-leave");
        _actionReactivate = root.Q<Button>("action-reactivate");
        _actionInjure = root.Q<Button>("action-injure");
        _actionRecover = root.Q<Button>("action-recover");

        // ── Wire events ───────────────────────────────────────────────────────
        _closeButton?.RegisterCallback<ClickEvent>(_ => Close());

        // Drag the modal by its title bar — position persists across play sessions via PlayerPrefs.
        var titleBar = root.Q<VisualElement>(className: "title-bar");
        _dragger = new DraggableWindow(_modal, titleBar, _closeButton);
        _posPersist = new DraggableWindowPersistence(_modal, _dragger, "EmployeeListPanel");

        _filterAll?.RegisterCallback<ClickEvent>(_ => SetFilter("all"));
        _filterActive?.RegisterCallback<ClickEvent>(_ => SetFilter("active"));
        _filterInjured?.RegisterCallback<ClickEvent>(_ => SetFilter("injured"));
        _filterFormer?.RegisterCallback<ClickEvent>(_ => SetFilter("former"));

        // Action buttons — delegate to EmployeeLifecycleService
        _actionFire?.RegisterCallback<ClickEvent>(_ => FireSelected());
        _actionSuspend?.RegisterCallback<ClickEvent>(_ => SuspendSelected());
        _actionLeave?.RegisterCallback<ClickEvent>(_ => LeaveSelected());
        _actionReactivate?.RegisterCallback<ClickEvent>(_ => ReactivateSelected());
        _actionInjure?.RegisterCallback<ClickEvent>(_ => InjureSelected());
        _actionRecover?.RegisterCallback<ClickEvent>(_ => RecoverSelected());

        // Subscribe to registry events for live updates (lazy/retry)
        TrySubscribeToRegistry();

        Close();
    }

    // ─── Open / Close ─────────────────────────────────────────────────────────
    public void Open()
    {
        if (_overlay == null) return;
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.pickingMode = PickingMode.Position;
        _modal.pickingMode = PickingMode.Position;
        TrySubscribeToRegistry(); // ensure subscription alive on open
        RebuildList();
    }

    public void Close()
    {
        if (_overlay == null) return;
        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
        _modal.pickingMode = PickingMode.Ignore;
    }

    public void Toggle()
    {
        if (_overlay != null && _overlay.style.display == DisplayStyle.Flex)
            Close();
        else
            Open();
    }

    private void Update()
    {
        _posPersist?.Tick();

        if (!_enableHotkey) return;
        if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame)
            Toggle();
    }

    // ─── Registry subscription (lazy/retry) ──────────────────────────────────
    private void TrySubscribeToRegistry()
    {
        if (_subscribed) return;

        var registry = EmployeeRegistry.Instance;
        if (registry != null)
        {
            registry.OnEmployeeAdded   += OnRegistryChanged;
            registry.OnEmployeeRemoved += OnRegistryChanged;
            _subscribed = true;
            if (_subscribeRetry != null)
            {
                StopCoroutine(_subscribeRetry);
                _subscribeRetry = null;
            }
            return;
        }

        // Registry not spawned yet — start polling
        if (_subscribeRetry == null)
            _subscribeRetry = StartCoroutine(SubscribeRetryLoop());
    }

    private IEnumerator SubscribeRetryLoop()
    {
        var wait = new WaitForSeconds(0.25f);
        while (!_subscribed)
        {
            yield return wait;
            TrySubscribeToRegistry();
        }
    }

    private void UnsubscribeFromRegistry()
    {
        if (_subscribeRetry != null)
        {
            StopCoroutine(_subscribeRetry);
            _subscribeRetry = null;
        }

        if (!_subscribed) return;

        var registry = EmployeeRegistry.Instance;
        if (registry != null)
        {
            registry.OnEmployeeAdded   -= OnRegistryChanged;
            registry.OnEmployeeRemoved -= OnRegistryChanged;
        }
        _subscribed = false;
    }

    private void OnRegistryChanged(EmployeeIdentity _) => RebuildList();

    // ─── List building ────────────────────────────────────────────────────────
    private void RebuildList()
    {
        if (_listContainer == null) return;

        // Clear existing rows
        _listContainer.Clear();
        _rows.Clear();

        var registry = EmployeeRegistry.Instance;
        if (registry == null) return;

        var all = registry.All;
        int count = 0;

        foreach (var identity in all)
        {
            if (identity?.Record == null) continue;
            var record = identity.Record;

            // Apply filter
            if (!PassesFilter(record)) continue;

            var row = BuildRow(record, identity);
            _listContainer.Add(row);
            _rows.Add(row);
            count++;
        }

        if (_employeeCountLabel != null)
            _employeeCountLabel.text = $"({count})";

        // If selected record no longer in filtered list, clear selection
        if (_selectedRecord != null && !_rows.Exists(r => r.userData == _selectedRecord))
        {
            SelectRecord(null);
        }
    }

    private VisualElement BuildRow(EmployeeRecord record, EmployeeIdentity identity)
    {
        if (_listItemTemplate == null)
        {
            Debug.LogError("[EmployeeListPanel] List item template is not assigned.");
            return new VisualElement();
        }

        var row = _listItemTemplate.Instantiate();
        row.userData = record;

        // Status dot
        var dot = row.Q<VisualElement>("status-dot");
        ApplyStatusDot(dot, record);

        // Name + ID
        var nameLabel = row.Q<Label>("row-name");
        var idLabel = row.Q<Label>("row-id");
        if (nameLabel != null) nameLabel.text = record.employeeName;
        if (idLabel != null) idLabel.text = record.employeeId;

        // Mini bars
        var fatigueFill = row.Q<VisualElement>("row-fatigue-fill");
        var moraleFill = row.Q<VisualElement>("row-morale-fill");
        if (fatigueFill != null) fatigueFill.style.width = Length.Percent(Mathf.Clamp(record.fatigue, 0f, 100f));
        if (moraleFill != null) moraleFill.style.width = Length.Percent(Mathf.Clamp(record.morale, 0f, 100f));

        // Shift label
        var shiftLabel = row.Q<Label>("row-shift");
        if (shiftLabel != null)
            shiftLabel.text = record.shift.ToString().ToUpper();

        // ── Role icon ─────────────────────────────────────────────────────────
        var roleIcon = row.Q<VisualElement>("row-role-icon");
        if (_roleIconLibrary != null && roleIcon != null)
        {
            var sprite = _roleIconLibrary.GetIcon(record.role);
            if (sprite != null)
                roleIcon.style.backgroundImage = new StyleBackground(sprite);
        }

        // ── Locate button ─────────────────────────────────────────────────────
        var locBtn = row.Q<Button>("row-location-btn");
        if (locBtn != null)
        {
            var capturedRecord = record;
            var capturedIdentity = identity;
            locBtn.clicked += () => FocusCameraOn(capturedIdentity, capturedRecord);
        }

        // ── Task label ────────────────────────────────────────────────────────
        var taskLabel = row.Q<Label>("row-task");
        if (taskLabel != null)
        {
            // TODO: map to AI FSM state (Putting Up Pallet / Bringing Down Pallet / Staging a Pallet)
            taskLabel.text = "ToBeImplemented";
        }

        // ── Performance label ─────────────────────────────────────────────────
        var perfLabel = row.Q<Label>("row-performance");
        if (perfLabel != null)
        {
            var metric = record.role.PerformanceMetric();
            perfLabel.text = metric switch
            {
                EmployeePerformanceMetric.CasesPerHour   => "-- CPH",   // TODO: real CPH when work-tracking exists
                EmployeePerformanceMetric.PalletsPerHour => "-- PPH",   // TODO: real PPH
                _                                        => "Indirect"
            };
        }

        // Click to select
        row.RegisterCallback<ClickEvent>(_ => SelectRecord(record));

        // Highlight if selected
        if (_selectedRecord == record)
            row.AddToClassList("employee-row-selected");

        return row;
    }

    // ─── Selection ────────────────────────────────────────────────────────────
    private void SelectRecord(EmployeeRecord record)
    {
        _selectedRecord = record;

        // Update row highlights
        foreach (var row in _rows)
        {
            bool isSelected = row.userData == record;
            if (isSelected)
                row.AddToClassList("employee-row-selected");
            else
                row.RemoveFromClassList("employee-row-selected");
        }

        if (record == null)
        {
            _detailColumn.style.display = DisplayStyle.None;
            _detailPlaceholder.style.display = DisplayStyle.Flex;
            return;
        }

        _detailColumn.style.display = DisplayStyle.Flex;
        _detailPlaceholder.style.display = DisplayStyle.None;

        RefreshDetailPanel(record);
    }

    // ─── Detail panel refresh ────────────────────────────────────────────────
    private void RefreshDetailPanel(EmployeeRecord record)
    {
        // Header
        if (_detailName != null) _detailName.text = record.employeeName;
        if (_detailId != null) _detailId.text = record.employeeId;

        // Portrait
        if (_detailJobIcon != null)
        {
            Sprite customSprite = null;
            if (!string.IsNullOrEmpty(record.avatarResourceKey) && 
                record.avatarResourceKey.StartsWith("Custom_") && 
                EmployeePhotoBooth.CustomAvatarCache.TryGetValue(record.avatarResourceKey, out var cachedSprite))
            {
                customSprite = cachedSprite;
            }

            if (customSprite != null)
            {
                _detailJobIcon.style.backgroundImage = new StyleBackground(customSprite);
            }
            else
            {
                string resourceKey = !string.IsNullOrEmpty(record.avatarResourceKey)
                    ? $"EmployeeAssets/{record.avatarResourceKey}"
                    : (record.gender == EmployeeGender.Female ? "EmployeeAssets/Female/avatar_01" : "EmployeeAssets/Male/avatar_01");

                var tex = Resources.Load<Texture2D>(resourceKey);
                if (tex != null)
                {
                    _detailJobIcon.style.backgroundImage = Background.FromTexture2D(tex);
                }
                else
                {
                    Debug.LogWarning($"[EmployeeListPanel] Portrait not found at Resources/{resourceKey} for employee {record.employeeName} (key='{record.avatarResourceKey}')");
                    _detailJobIcon.style.backgroundImage = StyleKeyword.None;
                    // Show a gender-tinted fallback color so blank is obvious
                    _detailJobIcon.style.backgroundColor = record.gender == EmployeeGender.Female
                        ? new Color(0.7f, 0.3f, 0.5f, 0.6f)
                        : new Color(0.2f, 0.5f, 0.8f, 0.6f);
                }
            }
        }

        // Status badge
        if (_detailStatusBadge != null)
        {
            _detailStatusBadge.text = record.status.ToString().ToUpper();
            _detailStatusBadge.RemoveFromClassList("status-active");
            _detailStatusBadge.RemoveFromClassList("status-injured");
            _detailStatusBadge.RemoveFromClassList("status-onleave");
            _detailStatusBadge.RemoveFromClassList("status-suspended");
            _detailStatusBadge.RemoveFromClassList("status-terminated");
            _detailStatusBadge.RemoveFromClassList("status-resigned");
            _detailStatusBadge.RemoveFromClassList("status-deceased");

            string statusClass = record.status switch
            {
                EmploymentStatus.Active => "status-active",
                EmploymentStatus.OnLeave => "status-onleave",
                EmploymentStatus.Injured => "status-injured",
                EmploymentStatus.Suspended => "status-suspended",
                EmploymentStatus.Terminated => "status-terminated",
                EmploymentStatus.Resigned => "status-resigned",
                EmploymentStatus.Deceased => "status-deceased",
                _ => "status-active"
            };
            _detailStatusBadge.AddToClassList(statusClass);
        }

        // Stat bars
        SetBar(_detailFatigueFill, _detailFatigueVal, record.fatigue);
        SetBar(_detailSafetyFill, _detailSafetyVal, record.safety);
        SetBar(_detailMoraleFill, _detailMoraleVal, record.morale);
        SetBar(_detailSkillFill, _detailSkillVal, record.skill);

        if (_detailSkillLevel != null)
            _detailSkillLevel.text = $"LVL {record.skillLevel}";

        // Employment
        if (_detailWage != null) _detailWage.text = $"${record.hourlyWage:F2}/hr";
        if (_detailHireDate != null) _detailHireDate.text = record.hireDateIso;
        if (_detailTotalPaid != null) _detailTotalPaid.text = $"${record.totalWagesPaid:F2}";

        // Schedule
        RebuildScheduleOverview(record);

        // Injury
        if (record.isInjured)
        {
            if (_injurySection != null) _injurySection.style.display = DisplayStyle.Flex;
            if (_injuryDescription != null) _injuryDescription.text = record.injuryDescription;
            if (_injuryRecovery != null) _injuryRecovery.text = $"{record.injuryRecoveryDaysLeft} DAYS REMAINING";
        }
        else
        {
            if (_injurySection != null) _injurySection.style.display = DisplayStyle.None;
        }

        // Action button state
        UpdateActionButtons(record);
    }

    private void RebuildScheduleOverview(EmployeeRecord record)
    {
        if (_scheduleWeek == null) return;

        _scheduleWeek.Clear();

        if (record.workSchedule == null) return;

        string[] dayNames = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };

        for (int i = 0; i < 7; i++)
        {
            var dayCell = new VisualElement();
            dayCell.AddToClassList("schedule-day");

            bool isScheduled = record.workSchedule.IsScheduledForDay(i);
            bool isOvertime = record.workSchedule.IsOvertimeDay(i);

            if (!isScheduled)
                dayCell.AddToClassList("schedule-day-off");

            if (isOvertime)
                dayCell.AddToClassList("schedule-day-overtime");

            var dayNameLabel = new Label(dayNames[i]);
            dayNameLabel.AddToClassList("schedule-day-name");
            dayCell.Add(dayNameLabel);

            var shiftLabel = new Label(isScheduled ? record.workSchedule.GetShift(i).ToString().Substring(0, 3).ToUpper() : "—");
            shiftLabel.AddToClassList("schedule-day-shift");
            dayCell.Add(shiftLabel);

            _scheduleWeek.Add(dayCell);
        }

        if (_scheduleNotes != null)
            _scheduleNotes.text = record.workSchedule.notes ?? string.Empty;
    }

    private void UpdateActionButtons(EmployeeRecord record)
    {
        bool isActive = record.status == EmploymentStatus.Active;
        bool isTerminated = record.status == EmploymentStatus.Terminated
                         || record.status == EmploymentStatus.Resigned
                         || record.status == EmploymentStatus.Deceased;
        bool isInjured = record.isInjured;

        SetButtonEnabled(_actionFire, isActive);
        SetButtonEnabled(_actionSuspend, isActive);
        SetButtonEnabled(_actionLeave, isActive);
        SetButtonEnabled(_actionReactivate, !isActive && !isTerminated);
        SetButtonEnabled(_actionInjure, isActive && !isInjured);
        SetButtonEnabled(_actionRecover, isInjured);
    }

    private void SetButtonEnabled(Button btn, bool enabled)
    {
        if (btn == null) return;
        btn.SetEnabled(enabled);
        btn.style.opacity = enabled ? 1f : 0.35f;
    }

    // ─── Action handlers ──────────────────────────────────────────────────────
    private void FireSelected()
    {
        if (_selectedRecord == null) return;

        // Route through the shared termination service so firing here does the full process
        // (archive + unregister + walk-off), identical to the roster and info card. Fall back
        // to a record-only fire if the live scene object isn't found.
        var identity = EmployeeRegistry.Instance?.GetByGuid(_selectedRecord.employeeGuid);
        if (identity != null)
            EmployeeTerminationService.Terminate(identity);
        else
            EmployeeLifecycleService.Instance?.Fire(_selectedRecord.employeeGuid);

        RebuildList();
    }

    private void SuspendSelected()
    {
        if (_selectedRecord == null) return;
        EmployeeLifecycleService.Instance?.SetStatus(_selectedRecord, EmploymentStatus.Suspended);
        RebuildList();
    }

    private void LeaveSelected()
    {
        if (_selectedRecord == null) return;
        EmployeeLifecycleService.Instance?.SetStatus(_selectedRecord, EmploymentStatus.OnLeave);
        RebuildList();
    }

    private void ReactivateSelected()
    {
        if (_selectedRecord == null) return;
        EmployeeLifecycleService.Instance?.SetStatus(_selectedRecord, EmploymentStatus.Active);
        RebuildList();
    }

    private void InjureSelected()
    {
        if (_selectedRecord == null) return;
        EmployeeLifecycleService.Instance?.Injure(_selectedRecord.employeeGuid, "Workplace incident", 5);
        RebuildList();
    }

    private void RecoverSelected()
    {
        if (_selectedRecord == null) return;
        EmployeeLifecycleService.Instance?.Recover(_selectedRecord.employeeGuid);
        RebuildList();
    }

    // ─── Filters ──────────────────────────────────────────────────────────────
    private void SetFilter(string filter)
    {
        _activeFilter = filter;

        _filterAll?.RemoveFromClassList("filter-active");
        _filterActive?.RemoveFromClassList("filter-active");
        _filterInjured?.RemoveFromClassList("filter-active");
        _filterFormer?.RemoveFromClassList("filter-active");

        _filterAll?.AddToClassList("filter-inactive");
        _filterActive?.AddToClassList("filter-inactive");
        _filterInjured?.AddToClassList("filter-inactive");
        _filterFormer?.AddToClassList("filter-inactive");

        switch (filter)
        {
            case "all":
                _filterAll?.RemoveFromClassList("filter-inactive");
                _filterAll?.AddToClassList("filter-active");
                break;
            case "active":
                _filterActive?.RemoveFromClassList("filter-inactive");
                _filterActive?.AddToClassList("filter-active");
                break;
            case "injured":
                _filterInjured?.RemoveFromClassList("filter-inactive");
                _filterInjured?.AddToClassList("filter-active");
                break;
            case "former":
                _filterFormer?.RemoveFromClassList("filter-inactive");
                _filterFormer?.AddToClassList("filter-active");
                break;
        }

        RebuildList();
    }

    private bool PassesFilter(EmployeeRecord record)
    {
        return _activeFilter switch
        {
            "all" => true,
            "active" => record.status == EmploymentStatus.Active
                     || record.status == EmploymentStatus.OnLeave
                     || record.status == EmploymentStatus.Suspended,
            "injured" => record.isInjured,
            "former" => record.status == EmploymentStatus.Terminated
                     || record.status == EmploymentStatus.Resigned
                     || record.status == EmploymentStatus.Deceased,
            _ => true
        };
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private void FocusCameraOn(EmployeeIdentity identity, EmployeeRecord record)
    {
        if (identity == null) return;

        if (_camera == null)
            _camera = UnityEngine.Object.FindFirstObjectByType<FreeLookCamera>();

        _camera?.FocusOn(identity.transform.position);

        if (record != null)
            SelectRecord(record);
    }

    private void SetBar(VisualElement bar, Label valueLabel, float pct)
    {
        if (bar != null)
            bar.style.width = Length.Percent(Mathf.Clamp(pct, 0f, 100f));
        if (valueLabel != null)
            valueLabel.text = $"{pct:F0}%";
    }

    private void ApplyStatusDot(VisualElement dot, EmployeeRecord record)
    {
        if (dot == null) return;
        dot.RemoveFromClassList("status-dot-active");
        dot.RemoveFromClassList("status-dot-injured");
        dot.RemoveFromClassList("status-dot-onleave");
        dot.RemoveFromClassList("status-dot-former");

        string dotClass = record.isInjured ? "status-dot-injured"
            : record.status == EmploymentStatus.OnLeave ? "status-dot-onleave"
            : record.status == EmploymentStatus.Terminated || record.status == EmploymentStatus.Resigned || record.status == EmploymentStatus.Deceased ? "status-dot-former"
            : "status-dot-active";

        dot.AddToClassList(dotClass);
    }

    // ─── Editor debug ─────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("Open Employee List")]
    private void EditorOpen() => Open();
#endif
}
