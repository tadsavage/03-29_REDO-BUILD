// METADATA file_path: Assets/3. UI/7. EmployeeUI/HiringBoardUI.cs
using System.Collections.Generic;
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
/// Open via HiringBoardUI.Instance?.Open() — or press 2 in play mode.
/// Implements IUIPanel for keybinding exclusivity via UIKeyBindingManager.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HiringBoardUI : MonoBehaviour, IUIPanel
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static HiringBoardUI Instance { get; private set; }

    [Header("Role Icons")]
    [SerializeField] private RoleIconLibrary _roleIconLibrary;

    [Header("Behaviour")]
    [Tooltip("Toggle the hiring board with the 2 key in play mode.")]
    [SerializeField] private bool _enableHotkey = true;

    // ─── UI refs ──────────────────────────────────────────────────────────────
    private UIDocument _doc;
    private VisualElement _overlay;
    private VisualElement _modal;
    private VisualElement _list;
    private VisualElement _animLayer;   // full-screen, unclipped layer for fly-off cards + sparkles
    private VisualElement _tooltip;     // styled hover tooltip for trait words
    private Label _tooltipText;
    private DraggableWindow _dragger;   // drag-by-title-bar + reset-on-X
    private Button _closeButton;
    private Button _refreshButton;
    private Label _countLabel;

    // Sortable column headers
    private Label _hApplicant, _hPosition, _hExperience, _hSalary;
    private const string T_APPLICANT  = "APPLICANT";
    private const string T_POSITION   = "POSITION";
    private const string T_EXPERIENCE = "EXPERIENCE";
    private const string T_SALARY     = "EXP. SALARY";

    private bool _subscribed;

    // ─── Sorting ──────────────────────────────────────────────────────────────
    private enum SortColumn { Name, Position, Experience, Salary }
    private SortColumn _sortColumn = SortColumn.Salary;
    private bool _sortAscending;   // false = descending (first click)
    private bool _hasSorted;       // until a header is clicked, keep roster order

    // ─── Unity lifecycle ──────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Register with UIKeyBindingManager for keybinding exclusivity (key 2)
        if (UIKeyBindingManager.Instance != null)
            UIKeyBindingManager.Instance.RegisterUI(2, this);
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

        _hApplicant  = root.Q<Label>("hb-h-applicant");
        _hPosition   = root.Q<Label>("hb-h-position");
        _hExperience = root.Q<Label>("hb-h-experience");
        _hSalary     = root.Q<Label>("hb-h-salary");

        // Full-screen, unclipped layer so cards can fly past the modal edges.
        if (_overlay != null)
        {
            _animLayer = new VisualElement { name = "hb-anim-layer" };
            _animLayer.pickingMode = PickingMode.Ignore;
            _animLayer.style.position = Position.Absolute;
            _animLayer.style.left = 0; _animLayer.style.top = 0;
            _animLayer.style.right = 0; _animLayer.style.bottom = 0;
            _animLayer.style.overflow = Overflow.Visible;
            _overlay.Add(_animLayer); // added after modal → renders on top

            // Styled trait tooltip (hidden until hover), topmost so it's never clipped.
            _tooltip = new VisualElement { name = "hb-tooltip" };
            _tooltip.AddToClassList("hb-tooltip");
            _tooltip.pickingMode = PickingMode.Ignore;
            _tooltip.style.display = DisplayStyle.None;
            _tooltipText = new Label { name = "hb-tooltip-text" };
            _tooltipText.AddToClassList("hb-tooltip-text");
            _tooltipText.pickingMode = PickingMode.Ignore;
            _tooltip.Add(_tooltipText);
            _overlay.Add(_tooltip);
        }

        // Red X → close AND reset position to the original spot next time.
        _closeButton?.RegisterCallback<ClickEvent>(_ => { _dragger?.ResetToOriginal(); Close(); });
        _refreshButton?.RegisterCallback<ClickEvent>(_ => HiringService.Instance?.RefreshRoster());

        // Drag the modal by its title bar (session-only position memory).
        var titleBar = root.Q<VisualElement>(className: "hb-title-bar");
        _dragger = new DraggableWindow(_modal, titleBar, _closeButton);

        _hApplicant?.RegisterCallback<ClickEvent>(_ => OnHeaderClicked(SortColumn.Name));
        _hPosition?.RegisterCallback<ClickEvent>(_ => OnHeaderClicked(SortColumn.Position));
        _hExperience?.RegisterCallback<ClickEvent>(_ => OnHeaderClicked(SortColumn.Experience));
        _hSalary?.RegisterCallback<ClickEvent>(_ => OnHeaderClicked(SortColumn.Salary));

        TrySubscribe();
        Close();
    }

    private void OnDisable()
    {
        if (_subscribed && HiringService.Instance != null)
            HiringService.Instance.OnRosterChanged -= RebuildList;
        _subscribed = false;

        // Unregister all UI callbacks to prevent duplicates on re-enable
        _closeButton?.UnregisterCallback<ClickEvent>(_ => { _dragger?.ResetToOriginal(); Close(); });
        _refreshButton?.UnregisterCallback<ClickEvent>(_ => HiringService.Instance?.RefreshRoster());
        _hApplicant?.UnregisterCallback<ClickEvent>(_ => OnHeaderClicked(SortColumn.Name));
        _hPosition?.UnregisterCallback<ClickEvent>(_ => OnHeaderClicked(SortColumn.Position));
        _hExperience?.UnregisterCallback<ClickEvent>(_ => OnHeaderClicked(SortColumn.Experience));
        _hSalary?.UnregisterCallback<ClickEvent>(_ => OnHeaderClicked(SortColumn.Salary));
    }

    private void Update()
    {
        if (!_enableHotkey || UIModalGuard.IsCapturing) return;
        if (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            // Route through UIKeyBindingManager for exclusivity
            if (UIKeyBindingManager.Instance != null)
                UIKeyBindingManager.Instance.ToggleUI(2);
            else
                Toggle();  // Fallback if manager not available
        }
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

    // IUIPanel interface wrappers
    void IUIPanel.Show() => Open();
    void IUIPanel.Hide() => Close();

    // ─── List building ─────────────────────────────────────────────────────────
    private void RebuildList()
    {
        if (_list == null) return;
        HideTooltip(); // a hovered chip may be about to be destroyed
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
            // Sort a display copy so the service's roster order is untouched.
            var display = new List<HiringCandidate>(roster);
            if (_hasSorted) display.Sort(CompareCandidates);

            foreach (var candidate in display)
                _list.Add(BuildCard(candidate));
        }

        if (_countLabel != null)
            _countLabel.text = roster.Count == 1 ? "1 candidate" : $"{roster.Count} candidates";

        UpdateHeaderArrows();
    }

    // ─── Sorting ───────────────────────────────────────────────────────────────
    private void OnHeaderClicked(SortColumn column)
    {
        // Same column → flip direction. New column → start descending (Excel-style).
        if (_hasSorted && _sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = false;
        }
        _hasSorted = true;
        RebuildList();
    }

    private int CompareCandidates(HiringCandidate a, HiringCandidate b)
    {
        int cmp;
        switch (_sortColumn)
        {
            case SortColumn.Name:
                cmp = string.Compare(a.LastName + a.FirstName, b.LastName + b.FirstName,
                                     System.StringComparison.OrdinalIgnoreCase);
                break;
            case SortColumn.Position:
                cmp = string.Compare(a.role.DisplayName(), b.role.DisplayName(),
                                     System.StringComparison.OrdinalIgnoreCase);
                break;
            case SortColumn.Experience:
                cmp = a.yearsExperience.CompareTo(b.yearsExperience);
                break;
            case SortColumn.Salary:
                cmp = a.EffectiveSalary.CompareTo(b.EffectiveSalary);
                break;
            default:
                cmp = 0;
                break;
        }
        return _sortAscending ? cmp : -cmp;
    }

    private void UpdateHeaderArrows()
    {
        SetHeader(_hApplicant,  T_APPLICANT,  SortColumn.Name);
        SetHeader(_hPosition,   T_POSITION,   SortColumn.Position);
        SetHeader(_hExperience, T_EXPERIENCE, SortColumn.Experience);
        SetHeader(_hSalary,     T_SALARY,     SortColumn.Salary);
    }

    private void SetHeader(Label label, string baseText, SortColumn column)
    {
        if (label == null) return;
        bool active = _hasSorted && _sortColumn == column;
        string arrow = active ? (_sortAscending ? "  ▲" : "  ▼") : "";
        label.text = baseText + arrow;
        label.EnableInClassList("hb-header-active", active);
    }

    private VisualElement BuildCard(HiringCandidate c)
    {
        var card = new VisualElement();
        card.AddToClassList("hb-card");
        card.userData = c; // lets us find the rebuilt card again (e.g. after a counter)

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
        AttachTooltip(strChip, HiringTraitLoader.GetDescription(c.strength));
        strCol.Add(strChip);
        card.Add(strCol);

        // ── Weakness ──────────────────────────────────────────────────────────
        var weakCol = new VisualElement();
        weakCol.AddToClassList("hb-col-trait");
        var weakChip = new Label(c.weakness.ToUpper());
        weakChip.AddToClassList("hb-chip");
        weakChip.AddToClassList("hb-chip-weak");
        AttachTooltip(weakChip, HiringTraitLoader.GetDescription(c.weakness));
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

        var hireBtn = new Button(() => OnHire(c, card)) { text = "HIRE" };
        hireBtn.AddToClassList("hb-btn");
        hireBtn.AddToClassList("hb-btn-hire");

        var counterBtn = new Button(() => OnCounter(c, card)) { text = "COUNTER" };
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
    private void OnHire(HiringCandidate c, VisualElement card)
    {
        AudioManager.Play("ButtonClick");

        // Capture geometry BEFORE the roster mutation rebuilds (and detaches) the list.
        Rect bound = GetBoundInLayer(card);

        HiringService.Instance?.Hire(c);

        // Celebrate: grow + slide off the bottom-right corner.
        FlyCardOff(card, bound, isHire: true);
    }

    private void OnCounter(HiringCandidate c, VisualElement card)
    {
        Rect bound = GetBoundInLayer(card);

        bool accepted = HiringService.Instance?.CounterOffer(c) ?? false;
        AudioManager.Play(accepted ? "ValidPlace" : "InvalidPlace");

        if (accepted)
        {
            // Roster rebuilt → find the refreshed card and pop its (now green) salary.
            var fresh = FindCardFor(c);
            if (fresh != null)
                fresh.schedule.Execute(() =>
                {
                    var lbl = fresh.Q<Label>(className: "hb-salary");
                    if (lbl == null) return;
                    PopLabel(lbl);
                    SpawnSparkles(CenterInLayer(lbl));
                }).ExecuteLater(16); // defer one tick so layout resolves
        }
        else
        {
            // Rejected: red tint + grow + fling off the top-left corner.
            FlyCardOff(card, bound, isHire: false);
        }
    }

    // ─── Animations ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Detaches a card to the full-screen anim layer and flies it off-screen:
    /// grows largest as it crosses the panel edge, then shrinks into the corner
    /// while fading. Hire → bottom-right (slower); reject → top-left (faster, red).
    /// </summary>
    private void FlyCardOff(VisualElement card, Rect bound, bool isHire)
    {
        if (card == null || _animLayer == null) return;

        card.pickingMode = PickingMode.Ignore;
        card.RemoveFromHierarchy();
        card.style.position = Position.Absolute;
        card.style.left = bound.x;
        card.style.top = bound.y;
        card.style.width = bound.width;
        card.style.height = bound.height;
        card.style.marginLeft = 0; card.style.marginRight = 0;
        card.style.marginTop = 0; card.style.marginBottom = 0;
        card.AddToClassList(isHire ? "hb-card-hired" : "hb-card-rejected");
        _animLayer.Add(card);

        float screenW = _overlay.worldBound.width;
        float screenH = _overlay.worldBound.height;

        float peak     = isHire ? 1.8f  : 2.0f;
        float endScale = isHire ? 0.12f : 0.05f;
        int   dur      = isHire ? 1200  : 900;

        float tx = isHire ? (screenW - bound.x + 120f) : -(bound.x + bound.width + 120f);
        float ty = isHire ? (screenH - bound.y + 120f) : -(bound.y + bound.height + 120f);

        var captured = card;
        card.experimental.animation.Start(0f, 1f, dur, (e, t) =>
        {
            float posT = t * t;                 // ease-in: slides slowly, then leaves fast
            float x = tx * posT;
            float y = ty * posT;

            // Grow to peak by 40%, then shrink toward the corner.
            float s = t < 0.4f
                ? Mathf.Lerp(1f, peak, t / 0.4f)
                : Mathf.Lerp(peak, endScale, (t - 0.4f) / 0.6f);

            // Hold opacity, fade out over the last quarter.
            float o = t < 0.75f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.75f) / 0.25f);

            e.style.translate = new Translate(new Length(x), new Length(y), 0f);
            e.style.scale = new Scale(new Vector3(s, s, 1f));
            e.style.opacity = o;
        }).OnCompleted(() => captured.RemoveFromHierarchy());
    }

    /// <summary>Quick scale pop (1 → 1.5 → 1) over ~0.5s — used on an accepted counter.</summary>
    private void PopLabel(Label label)
    {
        if (label == null) return;
        var captured = label;
        label.experimental.animation.Start(0f, 1f, 500, (e, t) =>
        {
            float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.5f; // peaks at 1.5 mid-way
            e.style.scale = new Scale(new Vector3(s, s, 1f));
        }).OnCompleted(() => captured.style.scale = new Scale(Vector3.one));
    }

    /// <summary>Asset-free glitter: a burst of small gold/white dots flying outward and fading.</summary>
    private void SpawnSparkles(Vector2 center, int count = 12)
    {
        if (_animLayer == null) return;

        for (int i = 0; i < count; i++)
        {
            var dot = new VisualElement();
            dot.pickingMode = PickingMode.Ignore;
            dot.style.position = Position.Absolute;

            float size = Random.Range(5f, 9f);
            dot.style.width = size; dot.style.height = size;
            float r = size * 0.5f;
            dot.style.borderTopLeftRadius = r; dot.style.borderTopRightRadius = r;
            dot.style.borderBottomLeftRadius = r; dot.style.borderBottomRightRadius = r;
            dot.style.backgroundColor = (i % 2 == 0)
                ? new Color(1f, 0.85f, 0.30f)   // gold
                : new Color(1f, 1f, 1f);        // white
            dot.style.left = center.x; dot.style.top = center.y;
            _animLayer.Add(dot);

            float ang  = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            float dist = Random.Range(45f, 95f);
            float tx = Mathf.Cos(ang) * dist;
            float ty = Mathf.Sin(ang) * dist;

            var captured = dot;
            dot.experimental.animation.Start(0f, 1f, 520, (e, t) =>
            {
                e.style.translate = new Translate(new Length(tx * t), new Length(ty * t), 0f);
                float s = Mathf.Lerp(1.3f, 0f, t);
                e.style.scale = new Scale(new Vector3(s, s, 1f));
                e.style.opacity = 1f - t;
            }).OnCompleted(() => captured.RemoveFromHierarchy());
        }
    }

    // ─── Geometry helpers (coords relative to the anim layer) ──────────────────────
    private VisualElement FindCardFor(HiringCandidate c)
    {
        if (_list == null) return null;
        foreach (var child in _list.Children())
            if (child.userData as HiringCandidate == c) return child;
        return null;
    }

    private Rect GetBoundInLayer(VisualElement ve)
    {
        Rect wb = ve.worldBound;
        Vector2 o = _animLayer != null ? _animLayer.worldBound.position : Vector2.zero;
        return new Rect(wb.x - o.x, wb.y - o.y, wb.width, wb.height);
    }

    private Vector2 CenterInLayer(VisualElement ve)
    {
        Rect wb = ve.worldBound;
        Vector2 o = _animLayer != null ? _animLayer.worldBound.position : Vector2.zero;
        return new Vector2(wb.center.x - o.x, wb.center.y - o.y);
    }

    // ─── Trait tooltip ───────────────────────────────────────────────────────────
    private void AttachTooltip(VisualElement target, string description)
    {
        if (string.IsNullOrEmpty(description)) return;
        target.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(description, target));
        target.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
    }

    private void ShowTooltip(string description, VisualElement target)
    {
        if (_tooltip == null || _overlay == null) return;
        _tooltipText.text = description;

        const float w = 210f, h = 140f, pad = 8f;
        Rect wb = target.worldBound;
        Vector2 o = _overlay.worldBound.position;
        float screenW = _overlay.worldBound.width;
        float screenH = _overlay.worldBound.height;

        float left = wb.center.x - o.x - w / 2f;     // centered over the chip
        float top  = wb.y - o.y - h - pad;           // above the chip…
        if (top < pad) top = (wb.yMax - o.y) + pad;  // …or below if no room

        left = Mathf.Clamp(left, pad, Mathf.Max(pad, screenW - w - pad));
        top  = Mathf.Clamp(top,  pad, Mathf.Max(pad, screenH - h - pad));

        _tooltip.style.left = left;
        _tooltip.style.top = top;
        _tooltip.style.display = DisplayStyle.Flex;
    }

    private void HideTooltip()
    {
        if (_tooltip != null) _tooltip.style.display = DisplayStyle.None;
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────
    private static Sprite LoadAvatar(string avatarResourceKey)
    {
        if (string.IsNullOrEmpty(avatarResourceKey)) return null;
        if (avatarResourceKey.StartsWith("Custom_") && 
            EmployeePhotoBooth.CustomAvatarCache.TryGetValue(avatarResourceKey, out var cachedSprite))
        {
            if (cachedSprite != null)
                return cachedSprite;
        }
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
