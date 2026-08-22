// METADATA file_path: Assets/3. UI/7. EmployeeUI/EmployeeRosterUI.cs
using System;
using System.Collections.Generic;
using GameCore.Labor;
using GameCore.Services;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

/// <summary>
/// Left-side employee roster (toggle with the 3 key). Lists every registered employee as
/// an ID card — identity + stats + level on the left, photo + current task on the
/// right, and an actions dropdown across the bottom (Terminate for now).
///
/// Sorted A–Z by name by default; the top dropdown re-sorts or filters by role.
/// Reads live from EmployeeRegistry and rebuilds on add/remove. Same aesthetic as
/// the Hiring Board / Employee Info panels.
///
/// Uses its own UIDocument. Assign EmployeeRoster.uxml as the Source Asset and a
/// RoleIconLibrary in the inspector.
/// Implements IUIPanel for keybinding exclusivity via UIKeyBindingManager.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class EmployeeRosterUI : MonoBehaviour, IUIPanel
{
    public static EmployeeRosterUI Instance { get; private set; }

    [Header("Role icons")]
    [SerializeField] private RoleIconLibrary _roleIconLibrary;

    [Header("Behaviour")]
    [Tooltip("Toggle the roster with the 3 key in play mode.")]
    [SerializeField] private bool _enableHotkey = true;

    [Header("Termination Walk-Off")]
    [Tooltip("Angry-face emote shown over a fired employee's head. Assign the Kenney emote_faceAngry sprite, " +
             "or leave empty to load 'emote_faceAngry' from Resources/Emotes.")]
    [SerializeField] private Sprite _angryEmote;
    [Tooltip("Seconds the fired employee waves at the guard shack before leaving.")]
    [SerializeField] private float _waveSeconds = 3f;

    // ── UI refs ─────────────────────────────────────────────────────────────────
    private UIDocument     _doc;
    private VisualElement  _overlay;
    private VisualElement  _panel;
    private VisualElement  _list;
    private Button         _close;
    private Button         _scaleBtn;
    private Label          _summaryText;
    private DropdownField  _filter;
    private DraggableWindow _dragger;   // drag-by-title-bar + reset-on-X
    private ResizableWindow _resizeWindow;

    private EmployeeInfoUI _infoUI;     // cached target for shift-click → detail panel

    private bool _subscribed;

    // Sort/filter options (sorts first, then a "Role: X" entry per role)
    private const string SortNameAZ   = "Name (A-Z)";
    private const string SortMoraleLo = "Morale (Low first)";
    private const string SortMoraleHi = "Morale (High first)";
    private const string SortFatigue  = "Fatigue (High first)";
    private const string SortSkill    = "Skill (High first)";
    private const string SortId       = "Employee ID";
    private const string RolePrefix   = "Role: ";

    private void Awake()
    {
        // First instance wins; the panel is DontDestroyOnLoad and outlives scene loads. See the fuller
        // note in HiringBoardUI.Awake — re-registration after a scene load is handled in Update.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Register with UIKeyBindingManager for keybinding exclusivity (key 3)
        if (UIKeyBindingManager.Instance != null)
            UIKeyBindingManager.Instance.RegisterUI(3, this);
    }

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        var root = _doc != null ? _doc.rootVisualElement : null;
        if (root == null) { Debug.LogError("[EmployeeRosterUI] No rootVisualElement."); return; }

        // Own document must draw above the HUD document (top bar + panels 5-9 built into it),
        // matching HiringBoardUI/ToolsWindowController. Without this the roster was left at its
        // stale scene-serialized sortingOrder, well below UILayers.Hud, so the top bar covered it.
        _doc.sortingOrder = UILayers.WindowAboveHud;

        _overlay = root.Q<VisualElement>("er-overlay");
        _list    = root.Q<VisualElement>("er-list");
        _close   = root.Q<Button>("er-close");
        _summaryText = root.Q<Label>("er-summary-text");
        _filter  = root.Q<DropdownField>("er-filter");

        // Red X → close AND reset position to the original spot next time.
        _close?.RegisterCallback<ClickEvent>(_ => { _dragger?.ResetToOriginal(); Close(); });

        // Drag the panel by its title bar (session-only position memory).
        _panel = root.Q<VisualElement>("er-panel");
        var titleBar = root.Q<VisualElement>(className: "er-title-bar");
        _dragger = new DraggableWindow(_panel, titleBar, _close);

        // Resize + close corner, restyled to match the shared house look (Hiring Board, Employee
        // List, Contracts, ...) instead of the smaller bespoke buttons this panel used to build.
        if (_panel != null)
        {
            _resizeWindow = new ResizableWindow(_panel, minW: 400f, minH: 300f, grip: 8f, titleInset: 56f);
            (_scaleBtn, _) = PanelTitleChrome.Adopt(_close, _resizeWindow, onClose: null);
        }

        if (_filter != null)
        {
            _filter.choices = BuildFilterChoices();
            _filter.index = 0;
            _filter.RegisterValueChangedCallback(_ => RebuildList());
        }

        TrySubscribe();
        Close();

        // Re-register with UIKeyBindingManager in case it was created after Awake
        if (UIKeyBindingManager.Instance != null)
            UIKeyBindingManager.Instance.RegisterUI(3, this);
    }

    private void OnDisable()
    {
        if (_subscribed && EmployeeRegistry.Instance != null)
        {
            EmployeeRegistry.Instance.OnEmployeeAdded   -= OnRegistryChanged;
            EmployeeRegistry.Instance.OnEmployeeRemoved -= OnRegistryChanged;
        }
        _subscribed = false;
    }

    private void Update()
    {
        if (!_enableHotkey || UIModalGuard.IsCapturing) return;
        if (Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            Debug.Log("[EmployeeRosterUI] Digit3Key triggered, calling ToggleUI(3)");
            // Route through UIKeyBindingManager for exclusivity
            if (UIKeyBindingManager.Instance != null)
            {
                Debug.Log("[EmployeeRosterUI] UIKeyBindingManager.Instance found, calling ToggleUI(3)");
                UIKeyBindingManager.Instance.ToggleUI(3, this);
            }
            else
            {
                Debug.LogWarning("[EmployeeRosterUI] UIKeyBindingManager.Instance is null, falling back to direct toggle");
                Toggle();  // Fallback if manager not available
            }
        }
    }

    private void TrySubscribe()
    {
        if (_subscribed || EmployeeRegistry.Instance == null) return;
        EmployeeRegistry.Instance.OnEmployeeAdded   += OnRegistryChanged;
        EmployeeRegistry.Instance.OnEmployeeRemoved += OnRegistryChanged;
        _subscribed = true;
    }

    private void OnRegistryChanged(EmployeeIdentity _) => RebuildList();

    // ── Open / Close ──────────────────────────────────────────────────────────
    public void Open()
    {
        if (_overlay == null) return;
        TrySubscribe();
        _overlay.style.display = DisplayStyle.Flex;
        RebuildList();
    }

    public void Close()
    {
        if (_overlay == null) return;
        _overlay.style.display = DisplayStyle.None;
    }

    public void Toggle()
    {
        if (_overlay != null && _overlay.style.display == DisplayStyle.Flex) Close();
        else Open();
    }

    public bool IsOpen => _overlay != null && _overlay.style.display == DisplayStyle.Flex;

    // IUIPanel interface wrappers
    void IUIPanel.Show() => Open();
    void IUIPanel.Hide() => Close();

    // ── Filter choices ──────────────────────────────────────────────────────────
    private List<string> BuildFilterChoices()
    {
        var choices = new List<string>
        {
            SortNameAZ, SortMoraleLo, SortMoraleHi, SortFatigue, SortSkill, SortId,
        };
        foreach (EmployeeRole role in Enum.GetValues(typeof(EmployeeRole)))
            choices.Add(RolePrefix + role.DisplayName());
        return choices;
    }

    // ── List building ───────────────────────────────────────────────────────────
    private void RebuildList()
    {
        if (_list == null) return;
        _list.Clear();

        var registry = EmployeeRegistry.Instance;
        if (registry == null)
        {
            _list.Add(MakeEmpty("Registry not available."));
            return;
        }

        // Collect valid employees.
        var people = new List<EmployeeIdentity>();
        foreach (var e in registry.All)
            if (e != null && e.Record != null) people.Add(e);

        // Apply filter/sort from the dropdown.
        string sel = _filter != null ? _filter.value : SortNameAZ;
        ApplyFilterSort(people, sel);

        if (people.Count == 0)
        {
            _list.Add(MakeEmpty("No employees."));
        }
        else
        {
            foreach (var e in people)
                _list.Add(BuildCard(e));
        }

        UpdateSummaryText(people.Count, sel);
    }

    private void UpdateSummaryText(int count, string filterSelection)
    {
        if (_summaryText == null) return;

        string summaryLabel = "Total Employees";
        if (!string.IsNullOrEmpty(filterSelection))
        {
            if (filterSelection.StartsWith(RolePrefix))
            {
                string roleName = filterSelection.Substring(RolePrefix.Length);
                summaryLabel = $"Total {roleName}";
            }
            else
            {
                summaryLabel = filterSelection switch
                {
                    SortNameAZ => "Total Employees",
                    SortMoraleLo => "Total Employees",
                    SortMoraleHi => "Total Employees",
                    SortFatigue => "Total Employees",
                    SortSkill => "Total Employees",
                    SortId => "Total Employees",
                    _ => "Total Employees"
                };
            }
        }

        _summaryText.text = $"{summaryLabel}: {count}";
    }

    private void ApplyFilterSort(List<EmployeeIdentity> people, string sel)
    {
        if (!string.IsNullOrEmpty(sel) && sel.StartsWith(RolePrefix))
        {
            string roleName = sel.Substring(RolePrefix.Length);
            people.RemoveAll(e => e.Record.role.DisplayName() != roleName);
            people.Sort((a, b) => string.Compare(a.Record.employeeName, b.Record.employeeName, StringComparison.OrdinalIgnoreCase));
            return;
        }

        switch (sel)
        {
            case SortMoraleLo: people.Sort((a, b) => a.Record.morale.CompareTo(b.Record.morale)); break;
            case SortMoraleHi: people.Sort((a, b) => b.Record.morale.CompareTo(a.Record.morale)); break;
            case SortFatigue:  people.Sort((a, b) => b.Record.fatigue.CompareTo(a.Record.fatigue)); break;
            case SortSkill:    people.Sort((a, b) => b.Record.skill.CompareTo(a.Record.skill)); break;
            case SortId:       people.Sort((a, b) => string.Compare(a.Record.employeeId, b.Record.employeeId, StringComparison.OrdinalIgnoreCase)); break;
            default:           people.Sort((a, b) => string.Compare(a.Record.employeeName, b.Record.employeeName, StringComparison.OrdinalIgnoreCase)); break;
        }
    }

    private VisualElement BuildCard(EmployeeIdentity identity)
    {
        var r = identity.Record;

        var card = new VisualElement();
        card.AddToClassList("er-card");
        card.userData = identity;

        // Shift-click a card → tidy the screen (close roster), open this employee's detail
        // panel, and highlight + focus them in the world. Plain clicks are untouched, so the
        // Actions dropdown etc. keep working normally.
        card.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (!evt.shiftKey) return;
            OnCardShiftClick(identity);
            evt.StopPropagation();
        });

        var body = new VisualElement();
        body.AddToClassList("er-card-body");

        // ── Left: identity + stats ────────────────────────────────────────────
        var left = new VisualElement();
        left.AddToClassList("er-left");

        var name = new Label(r.employeeName.ToUpper());
        name.AddToClassList("er-name");
        var id = new Label(r.employeeId);
        id.AddToClassList("er-id");
        left.Add(name);
        left.Add(id);

        left.Add(StatRow("FATIGUE", r.fatigue, "er-fatigue", "er-fatigue-fill"));
        left.Add(StatRow("SAFETY",  r.safety,  "er-safety",  "er-safety-fill"));
        left.Add(StatRow("MORALE",  r.morale,  "er-morale",  "er-morale-fill"));
        left.Add(StatRow("SKILL",   r.skill,   "er-skill",   "er-skill-fill"));

        var levelRow = new VisualElement();
        levelRow.style.flexDirection = FlexDirection.Row;
        levelRow.style.justifyContent = Justify.SpaceBetween;
        levelRow.style.marginTop = 4;

        var level = new Label($"LVL {Mathf.Max(1, r.skillLevel)}");
        level.AddToClassList("er-level");
        levelRow.Add(level);

        var currentAction = new Label(GetCurrentActionText(r));
        currentAction.AddToClassList("er-level");
        currentAction.style.marginLeft = 60;
        levelRow.Add(currentAction);

        left.Add(levelRow);
        body.Add(left);

        // ── Right: photo + current task ───────────────────────────────────────
        var right = new VisualElement();
        right.AddToClassList("er-right");

        var avatar = new VisualElement();
        avatar.AddToClassList("er-avatar");
        var sprite = LoadAvatar(r);
        if (sprite != null) avatar.style.backgroundImage = new StyleBackground(sprite);
        right.Add(avatar);

        var taskBar = new VisualElement();
        taskBar.AddToClassList("er-task-bar");
        var taskIcon = new VisualElement();
        taskIcon.AddToClassList("er-task-icon");
        if (_roleIconLibrary != null)
        {
            var icon = _roleIconLibrary.GetIcon(r.role);
            if (icon != null) taskIcon.style.backgroundImage = new StyleBackground(icon);
        }
        var taskName = new Label(r.role.DisplayName());  // current task (role for now)
        taskName.AddToClassList("er-task-name");
        taskBar.Add(taskName);
        taskBar.Add(taskIcon);
        right.Add(taskBar);

        body.Add(right);
        card.Add(body);

        // ── Actions dropdown ──────────────────────────────────────────────────
        var actions = new DropdownField();
        actions.AddToClassList("er-actions");

        // Build action choices (Patrol, role-specific assignment, Ask OT, Send Home, Terminate)
        var choices = new List<string> { "Actions...", "Patrol" };
        var roleAssignment = r.role.RoleSpecificAssignment();
        if (roleAssignment.HasValue)
            choices.Add(roleAssignment.Value.DisplayName());
        choices.Add("Ask to Work OT");
        choices.Add("Send Home");
        choices.Add("Terminate");

        actions.choices = choices;
        actions.index = 0;
        actions.RegisterValueChangedCallback(evt =>
        {
            OnRosterActionSelected(evt.newValue, identity);
            actions.SetValueWithoutNotify("Actions...");
        });
        card.Add(actions);

        return card;
    }

    private VisualElement StatRow(string label, float value, string colorClass, string fillClass)
    {
        float pct = Mathf.Clamp(value, 0f, 100f);

        var row = new VisualElement();
        row.AddToClassList("er-stat-row");

        var nameLbl = new Label(label);
        nameLbl.AddToClassList("er-stat-name");
        nameLbl.AddToClassList(colorClass);

        var valLbl = new Label($"{Mathf.RoundToInt(pct)}%");
        valLbl.AddToClassList("er-stat-val");
        valLbl.AddToClassList(colorClass);

        var bg = new VisualElement();
        bg.AddToClassList("er-bar-bg");
        var fill = new VisualElement();
        fill.AddToClassList("er-bar-fill");
        fill.AddToClassList(fillClass);
        fill.style.width = Length.Percent(pct);
        bg.Add(fill);

        row.Add(nameLbl);
        row.Add(bg);
        row.Add(valLbl);   // % chip sits to the right of the bar (matches the mockup)
        return row;
    }

    // ── Actions ───────────────────────────────────────────────────────────────
    private void OnRosterActionSelected(string action, EmployeeIdentity identity)
    {
        if (identity == null || identity.Record == null) return;

        if (action == "Terminate")
        {
            TerminateEmployee(identity);
            return;
        }

        if (action == "Patrol")
        {
            EmployeeAssignmentService.Assign(identity, EmployeeAssignment.Patrol);
            return;
        }

        if (action == "Ask to Work OT")
        {
            EmployeeOvertimeService.AskToWorkOvertime(identity);
            return;
        }

        if (action == "Send Home")
        {
            EmployeeOvertimeService.SendHome(identity);
            return;
        }

        // Check for role-specific assignment
        var roleAssignment = identity.Record.role.RoleSpecificAssignment();
        if (roleAssignment.HasValue && action == roleAssignment.Value.DisplayName())
        {
            EmployeeAssignmentService.Assign(identity, roleAssignment.Value);
            return;
        }
    }

    private void TerminateEmployee(EmployeeIdentity identity)
    {
        if (identity == null || identity.Record == null) return;

        // Full termination (HR status, archive, unregister, walk-off) lives in one shared
        // service so the roster, info card, and list panel all behave identically.
        EmployeeTerminationService.Terminate(identity, _angryEmote, _waveSeconds);

        RebuildList();
    }

    // ── Shift-click → detail + highlight ─────────────────────────────────────────
    private void OnCardShiftClick(EmployeeIdentity identity)
    {
        if (identity == null || identity.Record == null) return;

        // Close the roster to clean up the screen, then show this employee's detail panel.
        Close();

        if (_infoUI == null) _infoUI = FindAnyObjectByType<EmployeeInfoUI>();
        _infoUI?.Show(identity.Record);

        // Outline them in the world and make them the camera focal point.
        EmployeeHighlighter.Instance.FocusAndHighlight(identity);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────
    private static VisualElement MakeEmpty(string msg)
    {
        var lbl = new Label(msg);
        lbl.AddToClassList("er-empty");
        return lbl;
    }

    private static string GetCurrentActionText(EmployeeRecord record)
    {
        if (record == null) return "—";

        // Check if employee is currently operating a vehicle
        var mheSlots = FindObjectsByType<MHEOperatorSlot>();
        foreach (var slot in mheSlots)
        {
            if (slot.CurrentOperator != null && slot.CurrentOperator.Record.employeeId == record.employeeId)
            {
                // Get the vehicle type from the slot's data
                var placedObj = slot.GetComponent<PlacedObject>();
                if (placedObj != null && placedObj.data != null)
                {
                    return $"Operating {placedObj.data.name}";
                }
                return "Operating Vehicle";
            }
        }

        // Check if employee has an active work task
        if (ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue) && workQueue != null)
        {
            var tasks = workQueue.Tasks;
            foreach (var task in tasks)
            {
                if (task.Status == WorkTaskStatus.Assigned && task.RequiredRole == record.role)
                {
                    // This is a simple check - in a real system you'd track which specific employee is assigned
                    return task.Type switch
                    {
                        WorkTaskType.Receive => "Receiving",
                        WorkTaskType.Putaway => "Putting Away",
                        WorkTaskType.Replenish => "Replenishing",
                        WorkTaskType.OrderSelect => "Selecting Order",
                        WorkTaskType.PalletPick => "Pallet Picking",
                        WorkTaskType.Load => "Loading",
                        _ => task.Type.ToString()
                    };
                }
            }
        }

        // Show their assignment if they don't have an active task.
        // Vehicle assignments (DriveReach, DriveDockstalker) only show when actively operating.
        // Until then, show as "Patrolling" instead.
        return record.currentAssignment switch
        {
            EmployeeAssignment.Patrol => "Patrolling",
            EmployeeAssignment.DriveReach => "Patrolling",      // Will show "Operating Reach Truck" if actually in one
            EmployeeAssignment.DriveDockstalker => "Patrolling", // Will show "Operating Dockstalker" if actually in one
            EmployeeAssignment.OrderSelection => "Order Selection",
            EmployeeAssignment.ReceiveInbound => "Receive Inbound",
            _ => "Patrolling"
        };
    }

    private static Sprite LoadAvatar(EmployeeRecord record)
    {
        var resolved = EmployeePhotoBooth.ResolveDisplaySprite(record);
        if (resolved != null) return resolved;

        string avatarResourceKey = record?.avatarResourceKey;
        if (string.IsNullOrEmpty(avatarResourceKey)) return null;
        string fullKey = $"EmployeeAssets/{avatarResourceKey}";
        var sprite = Resources.Load<Sprite>(fullKey);
        if (sprite == null)
        {
            var tex = Resources.Load<Texture2D>(fullKey);
            if (tex != null)
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return sprite;
    }
}
