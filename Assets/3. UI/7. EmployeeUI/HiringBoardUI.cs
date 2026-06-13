// METADATA file_path: Assets/3. UI/7. EmployeeUI/HiringBoardUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

/// <summary>
/// Hiring Board modal. Lists applicants from <see cref="HiringService"/> as cards
/// (built in code, styled via HiringBoard.uss), and wires HIRE / COUNTER OFFER /
/// POST NEW LISTING actions.
///
/// Uses its own UIDocument (not the shared HUD), following the EmployeeListPanel
/// pattern. Assign the HiringBoard.uxml as the UIDocument Source Asset and a
/// RoleIconLibrary in the inspector.
///
/// Open via HiringBoardUI.Instance?.Open() — or press H in play mode (temporary).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HiringBoardUI : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static HiringBoardUI Instance { get; private set; }

    [Header("Role Icons")]
    [SerializeField] private RoleIconLibrary _roleIconLibrary;

    [Header("Behaviour")]
    [Tooltip("Top the board back up to full size after each hire.")]
    [SerializeField] private bool _topUpAfterHire = true;
    [Tooltip("Temporary: toggle the board with the H key in play mode.")]
    [SerializeField] private bool _enableHotkey = true;

    // ─── UI refs ──────────────────────────────────────────────────────────────
    private UIDocument _doc;
    private VisualElement _overlay;
    private VisualElement _modal;
    private VisualElement _list;
    private Button _closeButton;
    private Button _refreshButton;
    private Label _countLabel;

    private bool _subscribed;

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

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        var root = _doc != null ? _doc.rootVisualElement : null;
        if (root == null)
        {
            Debug.LogError("[HiringBoardUI] No rootVisualElement.");
            return;
        }

        _overlay = root.Q<VisualElement>("hb-overlay");
        _modal = root.Q<VisualElement>("hb-modal");
        _list = root.Q<VisualElement>("hb-list");
        _closeButton = root.Q<Button>("hb-close");
        _refreshButton = root.Q<Button>("hb-refresh");
        _countLabel = root.Q<Label>("hb-count");

        _closeButton?.RegisterCallback<ClickEvent>(_ => Close());
        _refreshButton?.RegisterCallback<ClickEvent>(_ => HiringService.Instance?.RefreshRoster());

        TrySubscribe();
        Close();
    }

    private void OnDisable()
    {
        if (_subscribed && HiringService.Instance != null)
            HiringService.Instance.OnRosterChanged -= RebuildList;
        _subscribed = false;
    }

    private void Update()
    {
        if (!_enableHotkey) return;
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            Toggle();
    }

    private void TrySubscribe()
    {
        if (_subscribed || HiringService.Instance == null) return;
        HiringService.Instance.OnRosterChanged += RebuildList;
        _subscribed = true;
    }

    // ─── Open / Close ─────────────────────────────────────────────────────────
    public void Open()
    {
        if (_overlay == null) return;
        TrySubscribe();
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.pickingMode = PickingMode.Position;
        if (_modal != null) _modal.pickingMode = PickingMode.Position;
        RebuildList();
    }

    public void Close()
    {
        if (_overlay == null) return;
        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
        if (_modal != null) _modal.pickingMode = PickingMode.Ignore;
    }

    public void Toggle()
    {
        if (_overlay != null && _overlay.style.display == DisplayStyle.Flex)
            Close();
        else
            Open();
    }

    public bool IsOpen => _overlay != null && _overlay.style.display == DisplayStyle.Flex;

    // ─── List building ─────────────────────────────────────────────────────────
    private void RebuildList()
    {
        if (_list == null) return;
        _list.Clear();

        var service = HiringService.Instance;
        if (service == null)
        {
            _list.Add(new Label("Hiring service not available.") { });
            return;
        }

        var roster = service.Roster;
        if (roster.Count == 0)
        {
            var empty = new Label("No candidates right now. Post a new listing.");
            empty.AddToClassList("hb-empty");
            _list.Add(empty);
        }
        else
        {
            foreach (var candidate in roster)
                _list.Add(BuildCard(candidate));
        }

        if (_countLabel != null)
            _countLabel.text = roster.Count == 1 ? "1 candidate" : $"{roster.Count} candidates";
    }

    private VisualElement BuildCard(HiringCandidate c)
    {
        var card = new VisualElement();
        card.AddToClassList("hb-card");

        // ── Portrait ──────────────────────────────────────────────────────────
        var portraitCol = new VisualElement();
        portraitCol.AddToClassList("hb-col-portrait");
        var portrait = new VisualElement();
        portrait.AddToClassList("hb-portrait");
        var portraitSprite = LoadAvatar(c.record?.avatarResourceKey);
        if (portraitSprite != null)
            portrait.style.backgroundImage = new StyleBackground(portraitSprite);
        portraitCol.Add(portrait);
        card.Add(portraitCol);

        // ── Name ──────────────────────────────────────────────────────────────
        var nameCol = new VisualElement();
        nameCol.AddToClassList("hb-col-name");
        var first = new Label(c.FirstName.ToUpper());
        first.AddToClassList("hb-firstname");
        var last = new Label(c.LastName.ToUpper());
        last.AddToClassList("hb-lastname");
        nameCol.Add(first);
        nameCol.Add(last);
        card.Add(nameCol);

        // ── Position (icon + name) ────────────────────────────────────────────
        var posCol = new VisualElement();
        posCol.AddToClassList("hb-col-position");
        var roleIcon = new VisualElement();
        roleIcon.AddToClassList("hb-role-icon");
        if (_roleIconLibrary != null)
        {
            var sprite = _roleIconLibrary.GetIcon(c.role);
            if (sprite != null) roleIcon.style.backgroundImage = new StyleBackground(sprite);
        }
        var roleName = new Label(c.role.DisplayName());
        roleName.AddToClassList("hb-role-name");
        posCol.Add(roleIcon);
        posCol.Add(roleName);
        card.Add(posCol);

        // ── Experience ────────────────────────────────────────────────────────
        var expCol = new VisualElement();
        expCol.AddToClassList("hb-col-exp");
        var exp = new Label(c.yearsExperience == 1 ? "1 YEAR" : $"{c.yearsExperience} YEARS");
        exp.AddToClassList("hb-exp");
        expCol.Add(exp);
        card.Add(expCol);

        // ── Strength ──────────────────────────────────────────────────────────
        var strCol = new VisualElement();
        strCol.AddToClassList("hb-col-trait");
        var strChip = new Label(c.strength.ToUpper());
        strChip.AddToClassList("hb-chip");
        strCol.Add(strChip);
        card.Add(strCol);

        // ── Weakness ──────────────────────────────────────────────────────────
        var weakCol = new VisualElement();
        weakCol.AddToClassList("hb-col-trait");
        var weakChip = new Label(c.weakness.ToUpper());
        weakChip.AddToClassList("hb-chip");
        weakChip.AddToClassList("hb-chip-weak");
        weakCol.Add(weakChip);
        card.Add(weakCol);

        // ── Salary ────────────────────────────────────────────────────────────
        var salaryCol = new VisualElement();
        salaryCol.AddToClassList("hb-col-salary");
        var salary = new Label($"${c.EffectiveSalary:0.00}/HR");
        salary.AddToClassList("hb-salary");
        if (c.counterAccepted)
        {
            salary.AddToClassList("hb-salary-countered");
            var note = new Label("COUNTER ACCEPTED");
            note.AddToClassList("hb-salary-note");
            salaryCol.Add(salary);
            salaryCol.Add(note);
        }
        else
        {
            salaryCol.Add(salary);
        }
        card.Add(salaryCol);

        // ── Actions ───────────────────────────────────────────────────────────
        var actionsCol = new VisualElement();
        actionsCol.AddToClassList("hb-col-actions");

        var hireBtn = new Button(() => OnHire(c)) { text = "HIRE" };
        hireBtn.AddToClassList("hb-btn");
        hireBtn.AddToClassList("hb-btn-hire");

        var counterBtn = new Button(() => OnCounter(c)) { text = "COUNTER" };
        counterBtn.AddToClassList("hb-btn");
        counterBtn.AddToClassList("hb-btn-counter");
        if (c.counterUsed)
        {
            counterBtn.SetEnabled(false);
            counterBtn.AddToClassList("hb-btn-disabled");
        }

        actionsCol.Add(hireBtn);
        actionsCol.Add(counterBtn);
        card.Add(actionsCol);

        return card;
    }

    // ─── Actions ───────────────────────────────────────────────────────────────
    private void OnHire(HiringCandidate c)
    {
        AudioManager.Play("ButtonClick");
        HiringService.Instance?.Hire(c);
        if (_topUpAfterHire)
            HiringService.Instance?.TopUpRoster();
        // RebuildList runs via OnRosterChanged.
    }

    private void OnCounter(HiringCandidate c)
    {
        bool accepted = HiringService.Instance?.CounterOffer(c) ?? false;
        AudioManager.Play(accepted ? "ValidPlace" : "InvalidPlace");
        // RebuildList runs via OnRosterChanged.
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────
    private static Sprite LoadAvatar(string avatarResourceKey)
    {
        if (string.IsNullOrEmpty(avatarResourceKey)) return null;
        string fullKey = $"EmployeeAssets/{avatarResourceKey}";
        var sprite = Resources.Load<Sprite>(fullKey);
        if (sprite == null)
        {
            var tex = Resources.Load<Texture2D>(fullKey);
            if (tex != null)
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                       new Vector2(0.5f, 0.5f));
        }
        return sprite;
    }
}
