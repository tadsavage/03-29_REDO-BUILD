// METADATA file_path: Assets/3. UI/7. EmployeeUI/EmployeeRosterUI.cs
using System;
using System.Collections.Generic;
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
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class EmployeeRosterUI : MonoBehaviour
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
    private VisualElement  _list;
    private Button         _close;
    private Label          _count;
    private DropdownField  _filter;
    private DraggableWindow _dragger;   // drag-by-title-bar + reset-on-X

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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        var root = _doc != null ? _doc.rootVisualElement : null;
        if (root == null) { Debug.LogError("[EmployeeRosterUI] No rootVisualElement."); return; }

        _overlay = root.Q<VisualElement>("er-overlay");
        _list    = root.Q<VisualElement>("er-list");
        _close   = root.Q<Button>("er-close");
        _count   = root.Q<Label>("er-count");
        _filter  = root.Q<DropdownField>("er-filter");

        // Red X → close AND reset position to the original spot next time.
        _close?.RegisterCallback<ClickEvent>(_ => { _dragger?.ResetToOriginal(); Close(); });

        // Drag the panel by its title bar (session-only position memory).
        var panel = root.Q<VisualElement>("er-panel");
        var titleBar = root.Q<VisualElement>(className: "er-title-bar");
        _dragger = new DraggableWindow(panel, titleBar, _close);

        if (_filter != null)
        {
            _filter.choices = BuildFilterChoices();
            _filter.index = 0;
            _filter.RegisterValueChangedCallback(_ => RebuildList());
        }

        TrySubscribe();
        Close();
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
            Toggle();
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

        if (_count != null) _count.text = $"{people.Count}";
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

        var level = new Label($"LVL {Mathf.Max(1, r.skillLevel)}");
        level.AddToClassList("er-level");
        left.Add(level);

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
        taskBar.Add(taskIcon);
        taskBar.Add(taskName);
        right.Add(taskBar);

        body.Add(right);
        card.Add(body);

        // ── Actions dropdown ──────────────────────────────────────────────────
        var actions = new DropdownField();
        actions.AddToClassList("er-actions");
        actions.choices = new List<string> { "Actions...", "Terminate" };
        actions.index = 0;
        actions.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue == "Terminate") TerminateEmployee(identity);
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
